using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace BlockShooter
{
    [RequireComponent(typeof(GameManager))]
    public sealed partial class MacaronFactory : MonoBehaviour
    {
        public MacaronFeedbackPlayer feedback;
        [Header("GUI-SimpleRound")]
        public Sprite hudButtonSprite;
        public Sprite hudButtonPressedSprite;
        public Sprite hudPanelSprite;
        public TMP_FontAsset hudFont;
        [Header("GUI-SimpleRound Icons & Popups")]
        public Sprite hudSettingIcon;
        public Sprite hudRetryIcon;
        public Sprite hudSoundOnIcon;
        public Sprite hudSoundOffIcon;
        public Sprite hudHapticIcon;
        public Sprite hudCoinIcon;
        public Sprite hudCloseIcon;
        public Sprite hudHomeIcon;
        public Sprite hudGreenButtonSprite;
        public Sprite hudGreenButtonPressedSprite;
        [Header("Existing cake block prefab source")]
        public LevelRoot conveyorSource;
        [Header("Tray-board templates (cycled; conveyor uses Soda Shippers stages)")]
        public MacaronLevel[] levels = System.Array.Empty<MacaronLevel>();
        private MacaronLevel _layout;
        [Header("Macaron Props prefabs")]
        [Tooltip("Berry, pistachio, lemon, blueberry, lavender, rose")]
        public GameObject[] macaronPrefabs = new GameObject[6];
        [Tooltip("Cake shell materials by color. Empty uses GameManager's GameConfig color registry. Filling keeps its prefab material.")]
        public ColorRegistryConfig colorRegistry;
        public ColorRegistryConfig ColorRegistry => colorRegistry != null ? colorRegistry : GetComponent<GameManager>().config?.colorRegistry;
        [Tooltip("Visual macaron scale only; belt dimensions and spacing stay unchanged. Collected cakes use the authored Pocket size.")]
        [Min(.1f)] public float conveyorMacaronScale = 2f;
        public int maxRowWidth => SodaConveyor.StageGroupSpec.LaneCount;
        public float laneSpacing => Conveyor != null ? Conveyor.LaneSpacing : SodaConveyor.StageLayout.LaneSpacing;
        [Header("Macaron exit")]
        [Tooltip("Flight time in seconds. Set to 0 to use Macaron Exit Speed instead.")]
        [Min(0)] public float macaronExitTime = .18f;
        [Tooltip("Flight progress over normalized time (0 to 1). Use endpoints (0,0) and (1,1).")]
        public AnimationCurve macaronExitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Jump height above the flight path, in world units.")]
        [Min(0)] public float macaronExitJumpHeight = .7f;
        [Tooltip("Delay between cakes starting their jump within each pickup row. Jumps may overlap.")]
        [Min(0)] public float macaronExitStagger = .035f;
        [Tooltip("World units per second when one macaron leaves the conveyor and flies into its tray.")]
        [Min(.1f)] public float macaronExitSpeed = 4f;
        [Tooltip("Peak size relative to the macaron's size when it leaves the belt.")]
        [Min(1)] public float macaronExitScaleMultiplier = 1.25f;
        [Header("Tray jump to waiting slot")]
        [Min(.01f)] public float trayJumpTime = .4f;
        [Min(0)] public float trayJumpHeight = .75f;
        public AnimationCurve trayJumpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(.01f)] public float trayWaitingScale = .58f;
        [Header("Tray packing")]
        [Min(.01f)] public float trayPackingTime = .4f;
        [Min(1)] public float trayPackingScaleMultiplier = 1.35f;
        [Header("Tray click feedback")]
        [Min(.01f)] public float trayValidClickTime = .1f;
        [Range(.5f, 1)] public float trayValidClickScale = .92f;
        [Min(.01f)] public float trayInvalidClickTime = .25f;
        [Min(0)] public float trayInvalidClickAngle = 6f;
        [Header("Tray receive bounce")]
        [Min(1)] public float trayReceiveScaleMultiplier = 1.08f;
        [Min(.01f)] public float trayReceiveBounceTime = .18f;
        [Header("Covered tray appearance")]
        [Tooltip("Black tint applied only while another tray blocks this tray. 0 keeps the original color.")]
        [Range(0, 1)] public float coveredTrayDarkness = .18f;
        [Header("Balance")]
        [Tooltip("0 uses normal player progress. A positive value starts a separate saved test progression at that stage.")]
        [Min(0)] public int stageOverride;
        [Min(1)] public int unlockSlotCost = 100;
        [Min(0)] public int shippingReward = 50;
        [Header("Packing combo")]
        [Min(.1f)] public float comboWindow = 5f;
        [Min(0)] public int comboCoinStep = 5;
        [Min(0)] public int comboMaxBonus = 20;
        [Min(.1f)] public float deadlockDelay = 1.5f;

        public int Stage { get; private set; }
        public int OpenSlots { get; private set; } = 4;
        public IReadOnlyList<MacaronTray> Trays => _trays;
        public IReadOnlyList<MacaronTray> Slots => _slots;
        public int Remaining => _remaining;
        public bool IsBusy => _transfers > 0 || _slots.Any(t => t != null && (t.Moving || t.Shipping));
        public Material PaperMaterial { get; private set; }
        public Material ShadowMaterial { get; private set; }
        public IReadOnlyList<ConveyorBlock3D[]> Rows => Conveyor == null ? System.Array.Empty<ConveyorBlock3D[]>()
            : Conveyor.Items.GroupBy(item => (item.transform.parent, item.RowIndex))
                .Select(row => row.OrderBy(item => item.LaneIndex).ToArray()).ToArray();
        private List<ConveyorBlock3D> _pickupOverride;
        public void SetPickupOverride(List<ConveyorBlock3D> overrideList) => _pickupOverride = overrideList;

        public IReadOnlyList<ConveyorBlock3D> PickupBlocks => _pickupOverride ?? GetSourcePickupBlocks();
        private readonly List<MacaronTray> _trays = new();
        private readonly MacaronTray[] _slots = new MacaronTray[6];
        private readonly List<Material> _materials = new();
        [Header("Editable scene HUD")]
        [SerializeField] private TextMeshProUGUI[] _slotLabels = new TextMeshProUGUI[6];
        private readonly Renderer[] _slotPads = new Renderer[6];
        [SerializeField] private TextMeshProUGUI _status, _coins, _progress, _stageText;
        [SerializeField] private TextMeshProUGUI _comboText;
        [SerializeField] private UnityEngine.UI.Slider _comboTimer;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Image _comboBackplate, _statusBackplate;
        private int _comboCount, _warningSlot = -1;
        private float _comboUntil;
        private MaterialPropertyBlock _warningTint;
        private RectTransform _overlay;

        [SerializeField] private RectTransform _hudRoot;
        private Transform _remainingBadge;
        private int _remaining, _transfers, _shipped;
        private float _deadlockTime, _noticeUntil, _speedMultiplier = 1;
        private bool _ready;
        private static int _requestedStage;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _requestedStage = 0;
        private static readonly BlockColorType[] Palette = {
            BlockColorType.Red, BlockColorType.Green, BlockColorType.Yellow,
            BlockColorType.Blue, BlockColorType.Purple, BlockColorType.Orange
        };

#if UNITY_EDITOR
        private void Reset() => AutoAssignSprites();

        public void AutoAssignSprites()
        {
            if (hudButtonSprite == null)
                hudButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/Button/Btn_Oval00_Sky_n.png");
            if (hudButtonPressedSprite == null)
                hudButtonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/Button/Btn_Oval00_Sky_s.png");
            if (hudPanelSprite == null)
                hudPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/Popup/Popup02.png");
            if (hudFont == null)
                hudFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GUI-SimpleRound/ResourceData/Font/BalooThambi-Regular SDF.asset");
            if (hudSettingIcon == null)
                hudSettingIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_setting.png");
            if (hudRetryIcon == null)
                hudRetryIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_retry.png");
            if (hudSoundOnIcon == null)
                hudSoundOnIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_sound_on.png");
            if (hudSoundOffIcon == null)
                hudSoundOffIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_sound_off.png");
            if (hudHapticIcon == null)
                hudHapticIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project Files/Game/2D/Vibration.png");
            if (hudCoinIcon == null)
                hudCoinIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_coin.png");
            if (hudCloseIcon == null)
                hudCloseIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_close.png");
            if (hudHomeIcon == null)
                hudHomeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/ButtonIcons/ButtonIcon_Blue/btn_blue_home.png");
            if (hudGreenButtonSprite == null)
                hudGreenButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/Button/Btn_Oval00_Green_n.png");
            if (hudGreenButtonPressedSprite == null)
                hudGreenButtonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI-SimpleRound/ResourceData/Sprites/Components/Button/Btn_Oval00_Green_s.png");
        }
#endif

        private void Awake()
        {
            // Keep the package state/config provider, but this scene owns its puzzle loop.
            var manager = GetComponent<GameManager>();
            manager.enabled = false;
        }

        private void Start()
        {
            Stage = _requestedStage > 0 ? _requestedStage : Mathf.Max(1, PlayerPrefs.GetInt(StageSaveKey, stageOverride > 0 ? stageOverride : 1));
            _requestedStage = 0;
            if (conveyorSource == null || conveyorSource.conveyorBlockPrefab == null || GameManager.Instance.config == null ||
                levels == null || levels.Length == 0 || levels.Any(level => level == null) ||
                macaronPrefabs == null || macaronPrefabs.Length != Palette.Length || macaronPrefabs.Any(p => p == null))
            {
                Debug.LogError("Macaron Factory needs its conveyor, authored levels and six macaron prefabs.");
                enabled = false;
                return;
            }
            PaperMaterial = Material("Vanilla paper", new Color(1f, .9f, .75f));
            ShadowMaterial = Material("Pocket shadow", new Color(.56f, .4f, .38f));
            var prefab = levels[(Stage - 1) % levels.Length];
            _layout = Instantiate(prefab, transform);
            _layout.name = prefab.name;
            _layout.MoveRemainingBadgeToTraySide();
            MacaronLevelVisualPolish.Apply(_layout);
            _remainingBadge = _layout.transform.Find("Remaining badge");
            BuildConveyor();
            FrameTrayBoard();
            BuildTrays();
            BuildPuzzleSupply();
            BuildHud();
            InitializeRunSave();
            RefreshAccessibility();
            GameManager.Instance.SetState(GameState.Playing);
            _ready = true;
            ResumePacking();
            SaveRun();
            UpdateHud();
            if (!string.IsNullOrEmpty(PuzzleHint))
            {
                _status.text = PuzzleHint;
                _noticeUntil = Time.time + 6f;
            }
        }

        private void BuildTrays()
        {
            foreach (var tray in _layout.GetTrays())
            {
                tray.Initialize(this, tray.levelColor, tray.stackLayer, false, tray.transform.localPosition);
                MacaronLevelVisualPolish.MarkForOutline(tray.transform);
                _trays.Add(tray);
            }
            var candidates = _trays.Where(tray => _trays.Any(tray.IsBlockedBy)).ToList();
            var random = new System.Random(_layout.trayArrangementSeed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            int hiddenCount = Mathf.Min(candidates.Count, Mathf.RoundToInt(_trays.Count * Mathf.Clamp01(_layout.mysteryTrayRatio)));
            for (int i = 0; i < hiddenCount; i++) candidates[i].SetMystery(_layout);
            for (int i = 0; i < 6; i++) _slotPads[i] = _layout.waitingSlots[i].GetComponent<Renderer>();
        }

        private void SetupMacaronVisual(ConveyorBlock3D block, BlockColorType color, float cakeScale)
        {
            block.Initialize(color, FlavorColor(color));
            foreach (var renderer in block.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var prefab = MacaronPrefab(color);
            var visual = Instantiate(prefab, block.transform).transform;
            MacaronLevelVisualPolish.MarkForOutline(visual);
            ApplyMacaronColor(visual.GetComponent<Renderer>(), color);
            visual.name = "Macaron";
            block.transform.localScale = Vector3.one;
            visual.localScale = Vector3.one * cakeScale;
            visual.localPosition = new Vector3(0, -prefab.GetComponent<Renderer>().localBounds.min.y * cakeScale, 0);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var collider in block.GetComponents<Collider>()) collider.enabled = false;
        }

        private ConveyorBlock3D SpawnMacaronBlock(BlockColorType color, Transform parent, float cakeScale)
        {
            var go = Instantiate(conveyorSource.conveyorBlockPrefab, parent);
            go.name = $"MacaronBlock_{color}";
            var block = go.GetComponent<ConveyorBlock3D>();
            if (block == null) block = go.AddComponent<ConveyorBlock3D>();
            SetupMacaronVisual(block, color, cakeScale);
            return block;
        }

        private void Update()
        {
            if (!_ready || !GameManager.Instance.IsPlaying) return;
            UpdatePackingFeedback();
            HandleClickParticle();
            Conveyor.TickFinishBoost(!_trays.Any(tray => tray.OnTable));
            Conveyor.Advance(Time.deltaTime * _speedMultiplier);
            var candidates = PickupBlocks;
            var launchCounts = new Dictionary<MacaronTray, int>();

            foreach (var block in candidates)
            {
                if (block == null || block.IsDestroyed || block.IsTargeted) continue;

                foreach (var tray in _slots)
                {
                    if (tray == null || tray.Moving || tray.Shipping || tray.Color != block.ColorType || !tray.CanReceive) continue;

                    if (tray.TryReserve())
                    {
                        int pocket = tray.Filled + tray.Reserved - 1;
                        launchCounts.TryGetValue(tray, out int count);
                        launchCounts[tray] = count + 1;
                        StartCoroutine(Collect(block, tray, pocket, count * macaronExitStagger));
                        break;
                    }
                }
            }
            CheckCompletion();
            if (IsDeadlocked())
            {
                _deadlockTime += Time.deltaTime;
                if (_status != null) _status.text = "No matching tray. Unlock another slot!";
                if (_deadlockTime >= deadlockDelay) Finish(false);
            }
            else
            {
                _deadlockTime = 0;
                if (Time.time >= _noticeUntil && _status != null)
                    _status.text = _warningSlot >= 0 ? "ONLY 1 SLOT LEFT!" : "";
            }
        }

        private void HandleClickParticle()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var cam = Camera.main;
            if (cam == null) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 50f))
            {
                if (hit.collider.GetComponentInParent<MacaronTray>() == null)
                    feedback?.Play(MacaronFeedbackEvent.SelectTray, hit.point);
            }
            else
            {
                var plane = new Plane(Vector3.up, new Vector3(0, .1f, 0));
                if (plane.Raycast(ray, out float enter))
                    feedback?.Play(MacaronFeedbackEvent.SelectTray, ray.GetPoint(enter));
            }
        }

        public bool TrySelect(MacaronTray tray)
        {
            if (!_ready || !GameManager.Instance.IsPlaying || tray == null || tray.Factory != this ||
                !tray.OnTable) return false;
            int slot = System.Array.FindIndex(_slots, 0, OpenSlots, t => t == null);
            if (!tray.Accessible || slot < 0)
            {
                tray.PlayInvalidClick(trayInvalidClickTime, trayInvalidClickAngle);
                feedback?.Play(MacaronFeedbackEvent.InvalidTray, tray.transform.position);
                return false;
            }
            tray.StopClickFeedback();
            tray.StopRevealFeedback();
            feedback?.Play(MacaronFeedbackEvent.SelectTray, tray.transform.position);
            _slots[slot] = tray; // Reserve before exposing the trays underneath.
            UpdatePackingFeedback();
            tray.LeaveTable();
            RefreshAccessibility();
            StartCoroutine(MoveToSlot(tray, slot));
            _saveDirty = true;
            return true;
        }

        private IEnumerator MoveToSlot(MacaronTray tray, int slot)
        {
            Vector3 originalScale = tray.transform.localScale;
            yield return tray.transform.DOScale(originalScale * Mathf.Clamp(trayValidClickScale, .5f, 1),
                Mathf.Max(.01f, trayValidClickTime)).SetEase(Ease.OutQuad).SetLink(tray.gameObject).WaitForCompletion();
            float duration = Mathf.Max(.01f, trayJumpTime);
            var curve = trayJumpCurve != null && trayJumpCurve.length >= 2
                ? trayJumpCurve : AnimationCurve.Linear(0, 0, 1, 1);
            Quaternion rotation = _layout != null ? _layout.waitingSlots[slot].rotation : Quaternion.identity;
            Vector3 finalScale = Vector3.one * Mathf.Max(.01f, trayWaitingScale);
            Vector3 start = tray.transform.position;
            Vector3 target = SlotPosition(slot);
            float flightY = Mathf.Max(start.y, target.y);
            foreach (var other in _trays)
            {
                if (other == null || other == tray || !other.gameObject.activeInHierarchy || other.Moving || other.Shipping) continue;
                foreach (var renderer in other.tintRenderers)
                    if (renderer != null && renderer.enabled) flightY = Mathf.Max(flightY, renderer.bounds.max.y);
            }
            flightY += Mathf.Max(.1f, trayJumpHeight);
            var jump = DOTween.Sequence().SetLink(tray.gameObject);
            jump.Append(tray.transform.DOMoveY(flightY, duration * .25f).SetEase(Ease.OutQuad));
            jump.Append(tray.transform.DOMove(new Vector3(target.x, flightY, target.z), duration * .5f).SetEase(curve));
            jump.Join(tray.transform.DORotateQuaternion(rotation, duration * .5f).SetEase(curve));
            jump.Append(tray.transform.DOMoveY(target.y, duration * .25f).SetEase(Ease.InQuad));
            jump.Insert(0, DOTween.Sequence()
                .Append(tray.transform.DOScale(originalScale, duration * .25f).SetEase(Ease.OutQuad))
                .Append(tray.transform.DOScale(finalScale, duration * .75f).SetEase(Ease.InOutQuad)));
            while (jump.IsActive() && !jump.IsComplete())
            {
                if (tray.ReleaseTableBlockIfClear()) RefreshAccessibility();
                yield return null;
            }
            tray.transform.SetPositionAndRotation(SlotPosition(slot), rotation);
            tray.transform.localScale = finalScale;
            tray.Moving = false;
            tray.ReleaseTableBlockIfClear(true);
            RefreshAccessibility();
            feedback?.Play(MacaronFeedbackEvent.TrayArrived, tray.transform.position);
            tray.Refresh(false);
        }

        private float DistanceToReceivingTray(ConveyorBlock3D block)
        {
            var tray = _slots.FirstOrDefault(t => t != null && t.CanReceive && t.Color == block.ColorType);
            return tray != null ? (block.transform.position - tray.GetPocket(tray.Filled + tray.Reserved).position).sqrMagnitude
                : float.PositiveInfinity;
        }

        private IEnumerator Collect(ConveyorBlock3D block, MacaronTray tray, int pocket, float delay)
        {
            _transfers++;
            block.SetTargeted(true);
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (block != null) block.SetTargeted(false);
            if (block == null || block.IsDestroyed || tray == null)
            {
                if (tray != null) tray.CancelReservation();
                _transfers--;
                yield break;
            }
            // The last item clears/destroys its BlockGroup. Detach before firing that event.
            var parent = block.transform.parent;
            block.transform.SetParent(transform, true);
            if (!block.TryCollect())
            {
                block.transform.SetParent(parent, true);
                tray.CancelReservation();
                _transfers--;
                yield break;
            }
            _remaining--;
            foreach (var collider in block.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var target = tray.GetPocket(pocket);
            var visual = block.transform.Find("Macaron");
            visual.SetParent(target, true);
            _saveDirty = true;
            float exitDuration = macaronExitTime > 0 ? Mathf.Max(.01f, macaronExitTime)
                : Mathf.Max(.08f, Vector3.Distance(visual.position, target.position) / Mathf.Max(.1f, macaronExitSpeed));
            var curve = macaronExitCurve != null && macaronExitCurve.length >= 2
                ? macaronExitCurve : AnimationCurve.Linear(0, 0, 1, 1);
            visual.DOLocalRotateQuaternion(Quaternion.identity, exitDuration).SetEase(curve).SetLink(visual.gameObject);
            DOTween.Sequence().SetLink(visual.gameObject)
                .Append(visual.DOScale(visual.localScale * Mathf.Max(1, macaronExitScaleMultiplier), exitDuration * .45f).SetEase(Ease.OutQuad))
                .Append(visual.DOScale(Vector3.one, exitDuration * .55f).SetEase(Ease.InOutQuad));
            // Follow the Pocket while its tray bounces from other arriving cakes.
            float localJumpHeight = Mathf.Max(0, macaronExitJumpHeight) / Mathf.Max(.001f, Mathf.Abs(target.lossyScale.y));
            yield return visual.DOLocalJump(Vector3.zero, localJumpHeight, 1, exitDuration).SetEase(curve)
                .SetLink(visual.gameObject).WaitForCompletion();
            // Snap the exact authored pose, including scale after the tray moved to its slot.
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            Destroy(block.gameObject);
            tray.Receive();
            feedback?.Play(MacaronFeedbackEvent.CakeLanded, target.position);
            _transfers--;
            UpdateHud();
            if (tray.Filled == tray.Capacity && tray.Reserved == 0) StartCoroutine(Ship(tray));
        }

        private IEnumerator Ship(MacaronTray tray)
        {
            tray.Shipping = true;
            tray.Label.text = "PACKED!";
            yield return new WaitForSeconds(Mathf.Max(.01f, trayReceiveBounceTime));
            tray.StopReceiveBounce();
            Vector3 lidScale = tray.Lid.localScale;
            float packingTime = Mathf.Max(.01f, trayPackingTime);
            tray.Lid.localPosition = tray.ClosedLidPosition + Vector3.up * .65f;
            tray.Lid.localScale = lidScale * Mathf.Max(1, trayPackingScaleMultiplier);
            tray.Lid.gameObject.SetActive(true);
            yield return DOTween.Sequence().SetLink(tray.gameObject)
                .Append(tray.Lid.DOLocalMove(tray.ClosedLidPosition, packingTime).SetEase(Ease.InOutCubic))
                .Join(tray.Lid.DOScale(lidScale, packingTime).SetEase(Ease.InOutCubic))
                .WaitForCompletion();
            feedback?.Play(MacaronFeedbackEvent.TrayPacked, tray.transform.position);
            if (_rewardedTrays.Add(tray)) RegisterPackedTray();
            SaveRun();
            yield return DOTween.Sequence().SetLink(tray.gameObject)
                .AppendInterval(.1f)
                .Append(tray.transform.DOMove(tray.transform.position + Vector3.right * 7f, .45f).SetEase(Ease.InQuad))
                .Join(tray.transform.DOScale(Vector3.one, .45f).SetEase(Ease.InQuad))
                .WaitForCompletion();
            int slot = System.Array.IndexOf(_slots, tray);
            if (slot >= 0) _slots[slot] = null;
            _shipped++;
            tray.gameObject.SetActive(false);
            _saveDirty = true;
            UpdateHud();
            CheckCompletion();
        }

        public void RefreshAccessibility()
        {
            // ponytail: O(n²) for these 9-12 tray boards; use spatial buckets for hundreds of trays.
            foreach (var tray in _trays)
            {
                if (!tray.OnTable) continue;
                bool blocked = _trays.Any(tray.IsBlockedBy);
                tray.Refresh(!blocked);
            }
        }

        private bool CanReceiveForDeadlock(ConveyorBlock3D block)
        {
            for (int i = 0; i < OpenSlots; i++)
                if (_slots[i] != null && _slots[i].Color == block.ColorType && _slots[i].CanReceive) return true;
            return false;
        }

        private System.Func<BlockColorType, bool> _canReceiveColorForDeadlock;
        private bool CanReceiveColorForDeadlock(BlockColorType color)
        {
            for (int i = 0; i < OpenSlots; i++)
                if (_slots[i] != null && _slots[i].Color == color && _slots[i].CanReceive) return true;
            return false;
        }

        public bool IsDeadlocked()
        {
            if (!_ready || !GameManager.Instance.IsPlaying || IsBusy || _remaining == 0) return false;
            for (int i = 0; i < OpenSlots; i++) if (_slots[i] == null) return false;
            if (_pickupOverride == null)
            {
                if (Conveyor != null)
                {
                    if (Conveyor.Items.Any(block => block != null && !block.IsDestroyed && CanReceiveForDeadlock(block))) return false;
                    if (Conveyor.AnyFreeSlot() && Conveyor.Branches.Any(branch => branch.HasMatchingColor(_canReceiveColorForDeadlock ??= CanReceiveColorForDeadlock))) return false;
                    return true;
                }
            }
            // Explicit pickup overrides are also used by the manual reservation check.
            var incoming = PickupBlocks;
            bool hasIncoming = false;
            foreach (var block in incoming)
            {
                if (block == null || block.IsDestroyed) continue;
                hasIncoming = true;
                for (int i = 0; i < OpenSlots; i++)
                    if (_slots[i].Color == block.ColorType &&
                        _slots[i].Filled + _slots[i].Reserved < _slots[i].Capacity) return false;
            }
            return hasIncoming;
        }

        public bool TryUnlockSlot()
        {
            if (!_ready || !GameManager.Instance.IsPlaying || OpenSlots >= 6) return false;
            if (SaveManager.Coins < unlockSlotCost)
            {
                _status.text = "Not enough coins";
                _noticeUntil = Time.time + 1.5f;
                return false;
            }
            SaveManager.Coins -= unlockSlotCost;
            feedback?.Play(MacaronFeedbackEvent.UnlockSlot, SlotPosition(OpenSlots));
            OpenSlots++;
            SaveRun();
            _deadlockTime = 0;
            UpdateHud();
            return true;
        }

        private void CheckCompletion()
        {
            if (_ready && _remaining == 0 && _transfers == 0 && _shipped == _trays.Count)
                Finish(true);
        }

        private void Finish(bool win)
        {
            if (!GameManager.Instance.IsPlaying) return;
            GameManager.Instance.SetState(win ? GameState.Win : GameState.Fail);
            DiscardRun();
            ClearPackingFeedback();
            feedback?.Play(win ? MacaronFeedbackEvent.Win : MacaronFeedbackEvent.Lose, _layout.counterAnchor.position);
            if (win)
            {
                SaveManager.Coins += shippingReward;
                PlayerPrefs.SetInt(StageSaveKey, Stage + 1);
                PlayerPrefs.Save();
            }
            StartCoroutine(ShowFinishOverlay(win));
        }

        private IEnumerator ShowFinishOverlay(bool win)
        {
            while (win && feedback != null && feedback.isActiveAndEnabled && feedback.IsEffectPlaying(MacaronFeedbackEvent.Win))
                yield return null;

            ShowOverlay(win ? "ORDER COMPLETE!" : "PACKING JAM!",
                win ? "All delicious macarons shipped!" : "All open slots are full.\nThe arriving colors do not match.",
                win ? "NEXT STAGE" : "TRY AGAIN", () => {
                    if (win) Stage++;
                    Reload();
                }, isWin: win);
        }

        private void Reload()
        {
            DiscardRun();
            _ready = false;
            PlayerPrefs.SetInt(StageSaveKey, Stage);
            PlayerPrefs.Save();
            _requestedStage = Stage;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private Vector3 SlotPosition(int i) => _layout != null
            ? _layout.waitingSlots[i].position + Vector3.up * .075f
            : new Vector3((i - 2.5f) * .96f, .08f, -.85f);

        private int AdvanceCombo(float now)
        {
            _comboCount = _comboCount > 0 && now <= _comboUntil ? _comboCount + 1 : 1;
            _comboUntil = now + Mathf.Max(.1f, comboWindow);
            return (int)System.Math.Min((long)(_comboCount - 1) * Mathf.Max(0, comboCoinStep), Mathf.Max(0, comboMaxBonus));
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Macaron Factory/Checks/Combo and last waiting slot")]
        public static void CheckPackingFeedback()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Run this check outside Play Mode.");
            var root = new GameObject("Packing feedback check");
            root.SetActive(false);
            try
            {
                var factory = root.AddComponent<MacaronFactory>();
                if (factory.AdvanceCombo(0) != 0 || factory.AdvanceCombo(5) != 5 ||
                    factory.AdvanceCombo(6) != 10 || factory.AdvanceCombo(12) != 0)
                    throw new System.Exception("Combo window, reward progression or expiry failed.");
                for (int i = 0; i < 10; i++)
                    if (factory.AdvanceCombo(12) > factory.comboMaxBonus)
                        throw new System.Exception("Combo reward exceeded cap.");
                var tray = root.AddComponent<MacaronTray>();
                if (factory.LastFreeSlot() != -1) throw new System.Exception("Empty board warns too early.");
                for (int i = 0; i < 3; i++) factory._slots[i] = tray;
                if (factory.LastFreeSlot() != 3) throw new System.Exception("Last open slot not found.");
                factory._slots[3] = tray;
                if (factory.LastFreeSlot() != -1) throw new System.Exception("Full board warns on locked slot.");
                factory.OpenSlots = 5;
                if (factory.LastFreeSlot() != 4) throw new System.Exception("Unlocked slot not counted.");
                factory.OpenSlots = 6;
                if (factory.LastFreeSlot() != -1) throw new System.Exception("Two free slots still warn.");
                var timer = new GameObject("Timer check", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
                timer.transform.SetParent(root.transform);
                factory._comboTimer = timer.GetComponent<UnityEngine.UI.Slider>();
                factory._comboUntil = Time.time + factory.comboWindow * .5f;
                factory.UpdatePackingFeedback();
                if (!Mathf.Approximately(factory._comboTimer.value, .5f) || !timer.activeSelf)
                    throw new System.Exception("Combo timer does not show remaining time.");
                factory._comboUntil = Time.time - 1;
                factory.UpdatePackingFeedback();
                if (timer.activeSelf || factory._comboTimer.value != 0)
                    throw new System.Exception("Expired combo timer remains visible.");
                Debug.Log("PASS: Combo expiry, boundary, reward cap and open-slot warning counts.");
            }
            finally { DestroyImmediate(root); }
        }
#endif

        private void RegisterPackedTray()
        {
            if (!GameManager.Instance.IsPlaying) return;
            int bonus = AdvanceCombo(Time.time);
            if (_comboTimer != null) { _comboTimer.gameObject.SetActive(true); _comboTimer.value = 1; }
            if (bonus > 0) SaveManager.Coins += bonus;
            if (_comboText != null)
            {
                _comboText.text = _comboCount < 2 ? "PACKED!" : $"COMBO x{_comboCount}  +{bonus} COINS";
                _comboText.transform.DOKill();
                _comboText.transform.localScale = Vector3.one;
                _comboText.transform.DOPunchScale(Vector3.one * .18f, .3f, 1).SetLink(_comboText.gameObject);
            }
            UpdateHud();
        }

        private int LastFreeSlot()
        {
            int result = -1;
            for (int i = 0; i < OpenSlots; i++)
                if (_slots[i] == null)
                {
                    if (result >= 0) return -1;
                    result = i;
                }
            return result;
        }

        private void UpdatePackingFeedback()
        {
            if (_comboTimer != null)
            {
                _comboTimer.value = Mathf.Clamp01((_comboUntil - Time.time) / Mathf.Max(.1f, comboWindow));
                _comboTimer.gameObject.SetActive(_comboCount > 0 && Time.time <= _comboUntil);
            }
            if (_comboCount > 0 && Time.time > _comboUntil)
            {
                _comboCount = 0;
                if (_comboText != null) _comboText.text = "";
            }
            int slot = _remaining > 0 ? LastFreeSlot() : -1;
            if (slot != _warningSlot)
            {
                if (_warningSlot >= 0 && _slotPads[_warningSlot] != null)
                    MacaronLevelVisualPolish.SetSlotLocked(_slotPads[_warningSlot].transform, false);
                bool enteringWarning = _warningSlot < 0 && slot >= 0;
                _warningSlot = slot;
                if (enteringWarning) feedback?.Play(MacaronFeedbackEvent.InvalidTray, SlotPosition(slot), true);
            }
            if (slot < 0 || _slotPads[slot] == null) return;
            Color color = Color.Lerp(new Color(.79f, .60f, .40f), new Color(1f, .28f, .08f),
                .35f + .3f * (1f + Mathf.Sin(Time.time * 5f)));
            _warningTint ??= new MaterialPropertyBlock();
            _slotPads[slot].GetPropertyBlock(_warningTint);
            _warningTint.SetColor("_BaseColor", color);
            _warningTint.SetColor("_Color", color);
            _slotPads[slot].SetPropertyBlock(_warningTint);
        }

        private void ClearPackingFeedback()
        {
            _comboCount = 0;
            if (_comboTimer != null) _comboTimer.gameObject.SetActive(false);
            if (_comboText != null) { _comboText.transform.DOKill(); _comboText.text = ""; }
            if (_warningSlot >= 0 && _slotPads[_warningSlot] != null)
                MacaronLevelVisualPolish.SetSlotLocked(_slotPads[_warningSlot].transform, false);
            _warningSlot = -1;
            if (_status != null) _status.text = "";
        }

        private void UpdateHud()
        {
            if (_coins != null) _coins.text = SaveManager.Coins.ToString();
            if (_stageText != null) _stageText.text = $"STAGE {Stage:00}";
            if (_progress != null) _progress.text = $"{_remaining} MACARONS";
            for (int i = 0; i < 6; i++)
            {
                if (_slotLabels[i] != null)
                {
                    _slotLabels[i].text = i < OpenSlots ? "" : i == OpenSlots ? $"+\n{unlockSlotCost}" : "LOCKED";
                    _slotLabels[i].GetComponentInParent<Button>().interactable = i == OpenSlots;
                }
                if (_slotPads[i] != null)
                {
                    MacaronLevelVisualPolish.SetSlotLocked(_slotPads[i].transform, i >= OpenSlots);
                }
            }
        }

        public string FlavorName(BlockColorType color) => color switch {
            BlockColorType.Red => "BERRY", BlockColorType.Green => "PISTACHIO",
            BlockColorType.Yellow => "LEMON", BlockColorType.Blue => "BLUEBERRY",
            BlockColorType.Purple => "LAVENDER", BlockColorType.Custom1 => "PINK",
            BlockColorType.Custom2 => "GRAY", BlockColorType.Custom3 => "LIGHT BLUE", _ => "ROSE"
        };

        public Color FlavorColor(BlockColorType color)
        {
            var registry = ColorRegistry;
            if (registry == null) return MacaronPrefab(color).GetComponent<Renderer>().sharedMaterial.color;
            var material = registry.GetMaterial(color);
            return material != null && !registry.OverridesColor(color) ? material.color : registry.GetColor(color);
        }

        public Color TrayColor(BlockColorType color)
            => ColorRegistry != null ? ColorRegistry.GetTrayColor(color, FlavorColor(color)) : FlavorColor(color);

        public void ApplyTrayAppearance(Renderer[] renderers, BlockColorType color)
        {
            var material = ColorRegistry != null ? ColorRegistry.GetTrayMaterial(color) : null;
            var tint = new MaterialPropertyBlock();
            var trayColor = TrayColor(color);
            foreach (var renderer in renderers)
            {
                if (material != null)
                {
                    var materials = renderer.sharedMaterials;
                    materials[0] = material;
                    renderer.sharedMaterials = materials;
                }
                renderer.GetPropertyBlock(tint, 0);
                tint.SetColor("_BaseColor", trayColor);
                tint.SetColor("_Color", trayColor);
                renderer.SetPropertyBlock(tint, 0);
            }
        }

        public void ApplyMacaronColor(Renderer renderer, BlockColorType color)
        {
            var registry = ColorRegistry;
            if (registry == null) return;
            var material = registry.GetMaterial(color);
            if (material != null)
            {
                var materials = renderer.sharedMaterials;
                materials[0] = material;
                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null, 0);
            }
            if (material == null || registry.OverridesColor(color))
            {
                var tint = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(tint, 0);
                tint.SetColor("_BaseColor", registry.GetColor(color));
                tint.SetColor("_Color", registry.GetColor(color));
                renderer.SetPropertyBlock(tint, 0);
            }
        }

        private Material Material(string label, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = label;
            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Smoothness", .24f);
            _materials.Add(material);
            return material;
        }

        public GameObject Part(string label, Transform parent, Vector3 position, Vector3 scale,
            Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        public GameObject MacaronPrefab(BlockColorType color) => macaronPrefabs[Mathf.Max(0, System.Array.IndexOf(Palette, color))];

        public TextMeshPro WorldText(string label, Transform parent, Vector3 position, string text, float size)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(65, 0, 0);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size * 10;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(.29f, .17f, .26f);
            tmp.rectTransform.sizeDelta = new Vector2(3, .8f);
            return tmp;
        }

        private TextMeshProUGUI Text(Transform parent, string text, Vector2 anchor, Vector2 size, int fontSize)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (hudFont != null) tmp.font = hudFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(.3f, .18f, .28f);
            tmp.raycastTarget = false;
            var rect = tmp.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return tmp;
        }

        private GameObject Panel(Transform parent, Vector2 anchor, Vector2 size, Sprite sprite = null, Color? color = null)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite != null ? sprite : hudPanelSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3;
            img.color = color ?? Color.white;
            return go;
        }

        private Button IconButton(Transform parent, Sprite icon, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action, string fallbackText = "⚙")
        {
            var go = new GameObject("IconButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.sprite = hudButtonSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = hudButtonPressedSprite };
            button.onClick.AddListener(action);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(8, 8);
                iconRect.offsetMax = new Vector2(-8, -8);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                var t = Text(go.transform, fallbackText, new Vector2(.5f, .5f), size, 22);
                t.color = new Color(.12f, .22f, .3f);
            }
            return button;
        }

        private Button Button(Transform parent, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
            => StyledButton(parent, text, anchor, size, action, null, null, null);

        private Button StyledButton(Transform parent, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action,
            Sprite customSprite = null, Sprite customPressed = null, Sprite icon = null)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.sprite = customSprite != null ? customSprite : hudButtonSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = customPressed != null ? customPressed : hudButtonPressedSprite };
            if (action != null) button.onClick.AddListener(action);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.18f, 0.5f);
                iconRect.sizeDelta = new Vector2(34, 34);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                var txt = Text(go.transform, text, new Vector2(.58f, .5f), new Vector2(size.x - 50, size.y), 24);
                txt.fontStyle = FontStyles.Bold;
                txt.color = new Color(.12f, .22f, .3f);
            }
            else
            {
                var txt = Text(go.transform, text ?? "", new Vector2(.5f, .5f), size, 24);
                txt.fontStyle = FontStyles.Bold;
                txt.color = new Color(.12f, .22f, .3f);
            }
            return button;
        }

        private void BuildHud()
        {
            foreach (var popup in new[] { settingsPopup, winPopup, losePopup })
                if (popup != null) popup.gameObject.SetActive(false);
            if (_hudRoot != null)
            {
                _settingsButton.onClick.AddListener(OpenSettings);
                for (int i = 0; i < _slotLabels.Length; i++)
                {
                    int index = i;
                    _slotLabels[i].GetComponentInParent<Button>().onClick.AddListener(() => { if (index >= OpenSlots) TryUnlockSlot(); });
                }
                _comboText.text = "";
                _status.text = "";
                _comboTimer.gameObject.SetActive(false);
                PositionHudMarkers();
                return;
            }
            var canvas = new GameObject("Factory HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var safeRoot = new GameObject("Safe area", typeof(RectTransform));
            safeRoot.transform.SetParent(canvas.transform, false);
            _hudRoot = (RectTransform)safeRoot.transform;
            canvas = safeRoot;

            // Settings Button (Top-Left)
            _settingsButton = IconButton(canvas.transform, hudSettingIcon, new Vector2(.09f, .962f), new Vector2(58, 58), OpenSettings, "⚙");

            // Level Badge (Top-Center)
            var levelBadge = Panel(canvas.transform, new Vector2(.5f, .962f), new Vector2(240, 56), hudButtonSprite);
            _stageText = Text(levelBadge.transform, $"STAGE {Stage:00}", new Vector2(.5f, .5f), new Vector2(230, 50), 32);
            _stageText.fontStyle = FontStyles.Bold;
            _stageText.color = new Color(.12f, .22f, .3f);

            // Coins Display (Top-Right)
            var coinBadge = Panel(canvas.transform, new Vector2(.86f, .962f), new Vector2(164, 52), hudButtonSprite);
            if (hudCoinIcon != null)
            {
                var cGo = new GameObject("Coin Icon", typeof(RectTransform), typeof(Image));
                cGo.transform.SetParent(coinBadge.transform, false);
                var cRect = (RectTransform)cGo.transform;
                cRect.anchorMin = cRect.anchorMax = new Vector2(.22f, .5f);
                cRect.sizeDelta = new Vector2(34, 34);
                var cImg = cGo.GetComponent<Image>();
                cImg.sprite = hudCoinIcon;
                cImg.preserveAspect = true;
                cImg.raycastTarget = false;
            }
            _coins = Text(coinBadge.transform, SaveManager.Coins.ToString(), new Vector2(.64f, .5f), new Vector2(100, 44), 26);
            _coins.fontStyle = FontStyles.Bold;
            _coins.color = new Color(.12f, .22f, .3f);

            // The text is a screen-space overlay centered on the physical Remaining badge.
            _progress = Text(canvas.transform, "", new Vector2(.13f, .65f), new Vector2(136, 42), 20);
            _progress.color = new Color(.12f, .22f, .3f);
            _progress.enableAutoSizing = true;
            _progress.fontSizeMin = 10;
            _progress.fontSizeMax = 22;

            // Floating Status Line (Placed higher to avoid conveyor overlap, empty when idle)
            _status = Text(canvas.transform, "", new Vector2(.5f, .884f), new Vector2(620, 40), 20);
            _status.fontStyle = FontStyles.Bold;
            _status.color = new Color(.35f, .20f, .28f);
            _comboText = Text(canvas.transform, "", new Vector2(.5f, .926f), new Vector2(620, 48), 32);
            _comboText.fontStyle = FontStyles.Bold;
            _comboText.color = new Color(.36f, .12f, .04f);
            _comboText.raycastTarget = false;
            var timer = Panel(canvas.transform, new Vector2(.5f, .904f), new Vector2(260, 10),
                hudButtonSprite, new Color(.36f, .24f, .16f));
            timer.name = "Combo time remaining";
            timer.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var fill = Panel(timer.transform, new Vector2(.5f, .5f), Vector2.zero,
                hudButtonSprite, new Color(1f, .72f, .12f));
            fill.name = "Fill";
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            _comboTimer = timer.AddComponent<UnityEngine.UI.Slider>();
            _comboTimer.fillRect = fillRect;
            _comboTimer.interactable = false;
            _comboTimer.transition = UnityEngine.UI.Selectable.Transition.None;
            _comboTimer.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            _comboTimer.minValue = 0;
            _comboTimer.maxValue = 1;
            timer.SetActive(false);

            // Floating slot buttons over 3D slots
            var cam = Camera.main;
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var button = Button(canvas.transform, "", new Vector2(.5f, .5f), new Vector2(88, 52),
                    () => { if (index >= OpenSlots) TryUnlockSlot(); });
                _slotLabels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
                if (_slotLabels[i] != null)
                {
                    _slotLabels[i].fontSize = 20;
                    _slotLabels[i].fontStyle = FontStyles.Bold;
                }
                button.GetComponent<Image>().color = Color.clear;
                var screen = cam != null ? cam.WorldToViewportPoint(SlotPosition(i) + Vector3.back * .48f) : new Vector3(.5f, .5f, 0);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(screen.x, screen.y);
            }

            // Note: Bottom bar UI is removed completely for full playing field visibility.
            _overlay = new GameObject("Overlay anchor", typeof(RectTransform)).GetComponent<RectTransform>();
            _overlay.SetParent(canvas.transform, false);
            _overlay.gameObject.SetActive(false);
            PositionHudMarkers();
        }

        private void LateUpdate()
        {
            if (_ready && Time.realtimeSinceStartup - _lastAutosave >= (_saveDirty ? .5f : 3f)) SaveRun();
            if (_comboBackplate != null) _comboBackplate.enabled = !string.IsNullOrEmpty(_comboText.text);
            if (_statusBackplate != null) _statusBackplate.enabled = !string.IsNullOrEmpty(_status.text);
            if (_hudRoot != null) PositionHudMarkers();
        }

        private void PositionHudMarkers()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            _hudRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _hudRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _hudRoot.offsetMin = _hudRoot.offsetMax = Vector2.zero;
            for (int i = 0; i < 6; i++)
            {
                if (_slotLabels[i] == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(SlotPosition(i));
                var rect = (RectTransform)_slotLabels[i].transform.parent;
                rect.anchorMin = rect.anchorMax = new Vector2((screen.x - safe.xMin) / safe.width, (screen.y - safe.yMin) / safe.height);
            }
            if (_remainingBadge != null && _progress != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(_remainingBadge.position);
                var rect = _progress.rectTransform;
                rect.anchorMin = rect.anchorMax =
                    new Vector2((screen.x - safe.xMin) / safe.width, (screen.y - safe.yMin) / safe.height);
            }
        }

        private void OnDestroy()
        {
            if (_sourceColors != null) Destroy(_sourceColors);
            StopAllCoroutines();
            Time.timeScale = 1;
            foreach (var material in _materials) if (material != null) Destroy(material);
        }
    }
}

