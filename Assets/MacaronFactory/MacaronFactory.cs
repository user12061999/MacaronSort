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
    public sealed class MacaronFactory : MonoBehaviour
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
        [Header("Existing package conveyor")]
        public LevelRoot conveyorSource;
        [Header("Hand-authored levels (played in list order)")]
        public MacaronLevel[] levels = System.Array.Empty<MacaronLevel>();
        private MacaronLevel _layout;
        [Header("Macaron Props prefabs")]
        [Tooltip("Berry, pistachio, lemon, blueberry, lavender, rose")]
        public GameObject[] macaronPrefabs = new GameObject[6];
        [Tooltip("Cake shell materials by color. Empty uses GameManager's GameConfig color registry. Filling keeps its prefab material.")]
        public ColorRegistryConfig colorRegistry;
        public ColorRegistryConfig ColorRegistry => colorRegistry != null ? colorRegistry : GetComponent<GameManager>().config?.colorRegistry;
        [Tooltip("Macaron size on the conveyor. Cakes shrink to their authored Pocket size when collected.")]
        [Min(.1f)] public float conveyorMacaronScale = 1.6f;
        public int maxRowWidth => _layout.columns;
        private float rowSpacing => _layout.rowSpacing;
        public float laneSpacing => Level.laneSpacing;
        [Header("Conveyor flow")]
        [Tooltip("World units per second used to move rows toward the endpoint of the conveyor.")]
        [Min(.1f)] public float conveyorSpeed = 1.2f;
        private float exitZoneLength => _layout.exitZoneLength;
        public float stopBeforeExitDistance => _layout.stopBeforeExit;
        [Header("Macaron exit")]
        [Tooltip("Flight time in seconds. Set to 0 to use Macaron Exit Speed instead.")]
        [Min(0)] public float macaronExitTime = .35f;
        [Tooltip("Flight progress over normalized time (0 to 1). Use endpoints (0,0) and (1,1).")]
        public AnimationCurve macaronExitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Jump height above the flight path, in world units.")]
        [Min(0)] public float macaronExitJumpHeight = .7f;
        [Tooltip("Delay between cakes starting their jump within each pickup row. Jumps may overlap.")]
        [Min(0)] public float macaronExitStagger = .08f;
        [Tooltip("World units per second when one macaron leaves the conveyor and flies into its tray.")]
        [Min(.1f)] public float macaronExitSpeed = 4f;
        [Tooltip("Peak size relative to the macaron's size when it leaves the belt.")]
        [Min(1)] public float macaronExitScaleMultiplier = 1.25f;
        [Header("Tray jump to waiting slot")]
        [Min(.01f)] public float trayJumpTime = .4f;
        [Min(0)] public float trayJumpHeight = .75f;
        public AnimationCurve trayJumpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(.01f)] public float trayWaitingScale = .58f;
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
        [Min(0)] public int stageOverride;
        [Min(1)] public int unlockSlotCost = 100;
        [Min(0)] public int shippingReward = 50;
        [Min(.1f)] public float deadlockDelay = 1.5f;

        public int Stage { get; private set; }
        public int OpenSlots { get; private set; } = 4;
        public IReadOnlyList<MacaronTray> Trays => _trays;
        public IReadOnlyList<MacaronTray> Slots => _slots;
        public int Remaining => _remaining;
        public bool IsBusy => _transfers > 0 || _slots.Any(t => t != null && (t.Moving || t.Shipping));
        public Material PaperMaterial { get; private set; }
        public Material ShadowMaterial { get; private set; }
        public LevelRoot Level { get; private set; }
        public IReadOnlyList<ConveyorBlock3D[]> Rows => _conveyorFlow.Rows;
        public IReadOnlyList<ConveyorBlock3D> PickupBlocks => _conveyorFlow.PickupBlocks;
        public MacaronConveyorFlow ConveyorFlow => _conveyorFlow;
        private MacaronConveyorFlow _conveyorFlow;
        private readonly List<MacaronTray> _trays = new();
        private readonly MacaronTray[] _slots = new MacaronTray[6];
        private readonly List<Material> _materials = new();
        private readonly TextMeshProUGUI[] _slotLabels = new TextMeshProUGUI[6];
        private readonly Renderer[] _slotPads = new Renderer[6];
        private TextMeshProUGUI _status, _coins, _progress, _stageText;
        private RectTransform _overlay;
        private RectTransform _settingOverlay;
        private RectTransform _hudRoot;
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
            Stage = _requestedStage > 0 ? _requestedStage : stageOverride > 0 ? stageOverride : Mathf.Max(1, PlayerPrefs.GetInt("Macaron.Stage", 1));
            _requestedStage = 0;
            if (conveyorSource == null || conveyorSource.conveyorController == null || GameManager.Instance.config == null ||
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
            prefab.ValidateLayout();
            _layout = Instantiate(prefab, transform);
            _layout.name = prefab.name;
            _layout.AlignExitToWaitingSlots();
            _layout.MoveRemainingBadgeToTraySide();
            _remainingBadge = _layout.transform.Find("Remaining badge");
            BuildTrays();
            BuildConveyor();
            if (_layout != null)
            {
                var camera = Camera.main;
                var frame = camera.GetComponent<MacaronCameraFrame>() ?? camera.gameObject.AddComponent<MacaronCameraFrame>();
                frame.FrameLevel(_layout);
                camera.backgroundColor = new Color(.88f, .85f, .78f);
            }
            BuildHud();
            RefreshAccessibility();
            GameManager.Instance.SetState(GameState.Playing);
            _ready = true;
            UpdateHud();
        }

        private void BuildTrays()
        {
            foreach (var tray in _layout.GetTrays())
            {
                tray.Initialize(this, tray.levelColor, tray.stackLayer, tray.mystery, tray.transform.localPosition);
                _trays.Add(tray);
            }
            for (int i = 0; i < 6; i++) _slotPads[i] = _layout.waitingSlots[i].GetComponent<Renderer>();
        }

        private void BuildConveyor()
        {
            Level = Instantiate(conveyorSource, transform);
            Level.name = "Package conveyor";
            foreach (Transform child in Level.transform)
                if (child.name != "ConveyorSystem") child.gameObject.SetActive(false);
            Level.cells.Clear();
            Level.branches.Clear();
            Level.groups.Clear();
            foreach (var branch in Level.GetComponentsInChildren<BranchPath>(true)) branch.gameObject.SetActive(false);

            var colors = _layout.BuildMacaronOrder();
            var payload = new List<BlockColorType[]>();
            for (int offset = 0; offset < colors.Count;)
            {
                int count = Mathf.Min(maxRowWidth, colors.Count - offset);
                payload.Add(colors.GetRange(offset, count).ToArray());
                offset += count;
            }
            // Package progress increases along the spline: reverse the spawn list so row 0 leads.
            for (int i = payload.Count - 1; i >= 0; i--)
                Level.groups.Add(new LevelConveyorGroup { color = payload[i][0], rowCount = 1, laneCount = payload[i].Length });
            float cakeScale = Mathf.Max(.1f, conveyorMacaronScale);
            float cakeDiameter = cakeScale * macaronPrefabs.Max(prefab =>
            {
                var bounds = prefab.GetComponent<Renderer>().localBounds;
                return 2 * Mathf.Max(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.z) + bounds.extents.z);
            });
            Level.laneSpacing = Mathf.Max(_layout.laneSpacing, cakeDiameter + .015f);
            Level.rowSpacing = rowSpacing;
            var conveyor = Level.conveyorController;
            conveyor.automaticMotion = false;
            if (_layout != null)
            {
                conveyor.transform.SetPositionAndRotation(_layout.conveyorPath.transform.position, _layout.conveyorPath.transform.rotation);
                conveyor.transform.localScale = _layout.conveyorPath.transform.lossyScale;
                conveyor.GetComponent<SplineContainer>().Spline = new Spline(_layout.conveyorPath.Spline);
                conveyor.loop = false;
                var preview = _layout.conveyorPath.GetComponent<Renderer>();
                if (preview != null)
                {
                    conveyor.GetComponent<Renderer>().sharedMaterials = preview.sharedMaterials;
                    preview.enabled = false;
                }
            }
            var belt = conveyor.GetComponent<ConveyorTrackMeshBuilder>();
            if (_layout != null) belt.resolution = _layout.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>().resolution;
            belt.beltHalfWidth = laneSpacing * (maxRowWidth - 1) * .5f + Mathf.Max(.16f, cakeDiameter * .5f + .02f);
            belt.railHeight = .18f;
            belt.wallAboveBelt = .045f;
            belt.openZoneEnabled = false;
            if (_layout.conveyorPath.Spline.Closed)
            {
                var authored = _layout.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>();
                belt.closeBottom = authored.closeBottom;
                authored.beltHalfWidth = belt.beltHalfWidth;
                foreach (var branch in _layout.feederBranches)
                {
                    branch.Branch.beltHalfWidth = belt.beltHalfWidth;
                    branch.SyncJunction();
                }
                belt.openings = new List<ConveyorOpening>(authored.openings);
            }
            belt.BuildMesh();
            Level.SpawnBlocksRuntime();
            conveyor.Initialize();
            conveyor.speed = conveyorSpeed;
            var rows = new List<ConveyorBlock3D[]>();
            var rowGroups = new List<BlockGroup>();
            var groups = conveyor.GetComponentsInChildren<BlockGroup>(true).Reverse().ToArray();
            for (int row = 0; row < groups.Length; row++)
            {
                var blocks = new ConveyorBlock3D[payload[row].Length];
                for (int lane = 0; lane < blocks.Length; lane++)
                {
                    var block = groups[row].GetBlock(0, lane);
                    blocks[lane] = block;
                    var color = payload[row][lane];
                    block.Initialize(color, FlavorColor(color));
                    foreach (var renderer in block.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    var prefab = MacaronPrefab(color);
                    var visual = Instantiate(prefab, block.transform).transform;
                    ApplyMacaronColor(visual.GetComponent<Renderer>(), color);
                    visual.name = "Macaron";
                    Vector3 scale = block.transform.lossyScale;
                    visual.localScale = new Vector3(cakeScale / scale.x, cakeScale / scale.y, cakeScale / scale.z);
                    visual.localPosition = new Vector3(0, -prefab.GetComponent<Renderer>().localBounds.min.y * cakeScale / scale.y, 0);
                    foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    foreach (var collider in block.GetComponents<Collider>()) collider.enabled = false;
                }
                rows.Add(blocks);
                rowGroups.Add(groups[row]);
            }
            float stopT = Mathf.Clamp01(1 - stopBeforeExitDistance / conveyor.SplineWorldLength);
            float initialFrontT = stopT * .5f;
            _conveyorFlow = new MacaronConveyorFlow(conveyor, rows, rowGroups, rowSpacing,
                _layout.conveyorPath.Spline.Closed ? 0 : initialFrontT, laneSpacing, maxRowWidth, cakeDiameter,
                _layout.conveyorPath.Spline.Closed ? _layout.feederBranches : null,
                _layout.loopSpacingMultiplier, _layout.feederSpacingMultiplier, _layout.independentLanePacking);
            _remaining = colors.Count;
        }

        private void Update()
        {
            if (!_ready || !GameManager.Instance.IsPlaying) return;
            HandleClickParticle();
            var conveyor = Level.conveyorController;
            if (conveyor.IsFrozen) return;
            _conveyorFlow.Tick(conveyorSpeed * _speedMultiplier, exitZoneLength, stopBeforeExitDistance);
            if (_layout.collectionGate != null)
            {
                bool receiving = _transfers > 0 || PickupBlocks.Any(block =>
                    _slots.Any(tray => tray != null && tray.CanReceive && tray.Color == block.ColorType));
                if (receiving) _layout.collectionGate.Open(); else _layout.collectionGate.Close();
            }
            foreach (var row in PickupBlocks.GroupBy(block => block.transform.parent))
            {
                if (_layout.collectionGate != null && !_layout.collectionGate.IsOpen) break;
                int launchIndex = 0;
                foreach (var block in row.OrderBy(DistanceToReceivingTray).ToArray())
                {
                    foreach (var tray in _slots)
                    {
                        if (tray == null || tray.Moving || tray.Shipping || tray.Color != block.ColorType || !tray.TryReserve()) continue;
                        StartCoroutine(Collect(block, tray, launchIndex++ * Mathf.Max(0, macaronExitStagger)));
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
                if (Time.time >= _noticeUntil && _status != null) _status.text = "";
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
            feedback?.Play(MacaronFeedbackEvent.SelectTray, tray.transform.position);
            _slots[slot] = tray; // Reserve before exposing the trays underneath.
            tray.LeaveTable();
            RefreshAccessibility();
            StartCoroutine(MoveToSlot(tray, slot));
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
            var jump = DOTween.Sequence().SetLink(tray.gameObject);
            jump.Join(tray.transform.DOJump(SlotPosition(slot), Mathf.Max(0, trayJumpHeight), 1, duration).SetEase(curve));
            jump.Join(tray.transform.DORotateQuaternion(rotation, duration).SetEase(curve));
            jump.Join(DOTween.Sequence()
                .Append(tray.transform.DOScale(originalScale, duration * .25f).SetEase(Ease.OutQuad))
                .Append(tray.transform.DOScale(finalScale, duration * .75f).SetEase(Ease.InOutQuad)));
            yield return jump.WaitForCompletion();
            tray.transform.SetPositionAndRotation(SlotPosition(slot), rotation);
            tray.transform.localScale = finalScale;
            tray.Moving = false;
            feedback?.Play(MacaronFeedbackEvent.TrayArrived, tray.transform.position);
            tray.Refresh(false);
        }

        private float DistanceToReceivingTray(ConveyorBlock3D block)
        {
            var tray = _slots.FirstOrDefault(t => t != null && t.CanReceive && t.Color == block.ColorType);
            return tray != null ? (block.transform.position - tray.GetPocket(tray.Filled + tray.Reserved).position).sqrMagnitude
                : float.PositiveInfinity;
        }

        private IEnumerator Collect(ConveyorBlock3D block, MacaronTray tray, float delay)
        {
            int pocket = tray.Filled + tray.Reserved - 1;
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
            tray.Lid.localPosition = tray.ClosedLidPosition + Vector3.up * .65f;
            tray.Lid.gameObject.SetActive(true);
            yield return tray.Lid.DOLocalMove(tray.ClosedLidPosition, .25f).SetEase(Ease.OutCubic)
                .SetLink(tray.gameObject).WaitForCompletion();
            feedback?.Play(MacaronFeedbackEvent.TrayPacked, tray.transform.position);
            yield return DOTween.Sequence().SetLink(tray.gameObject)
                .AppendInterval(.1f)
                .Append(tray.transform.DOMove(tray.transform.position + Vector3.right * 7f, .45f).SetEase(Ease.InQuad))
                .Join(tray.transform.DOScale(Vector3.one, .45f).SetEase(Ease.InQuad))
                .WaitForCompletion();
            int slot = System.Array.IndexOf(_slots, tray);
            if (slot >= 0) _slots[slot] = null;
            _shipped++;
            tray.gameObject.SetActive(false);
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

        private System.Predicate<ConveyorBlock3D> _canReceiveForDeadlock;
        private bool CanReceiveForDeadlock(ConveyorBlock3D block)
        {
            for (int i = 0; i < OpenSlots; i++)
                if (_slots[i] != null && _slots[i].Color == block.ColorType && _slots[i].CanReceive) return true;
            return false;
        }

        public bool IsDeadlocked()
        {
            if (!_ready || !GameManager.Instance.IsPlaying || IsBusy || _remaining == 0) return false;
            for (int i = 0; i < OpenSlots; i++) if (_slots[i] == null) return false;
            if (_conveyorFlow.IsLoop) return !_conveyorFlow.HasReachableMatch(_canReceiveForDeadlock ??= CanReceiveForDeadlock);
            // Only cakes in the leading row at the conveyor endpoint count.
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
            feedback?.Play(win ? MacaronFeedbackEvent.Win : MacaronFeedbackEvent.Lose, _layout.counterAnchor.position);
            if (win)
            {
                SaveManager.Coins += shippingReward;
                if (stageOverride == 0) PlayerPrefs.SetInt("Macaron.Stage", Stage + 1);
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
            _requestedStage = Stage;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private Vector3 SlotPosition(int i) => _layout != null
            ? _layout.waitingSlots[i].position + Vector3.up * .075f
            : new Vector3((i - 2.5f) * .96f, .08f, -.85f);

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
                    _slotLabels[i].fontSize = 20;
                    _slotLabels[i].fontStyle = FontStyles.Bold;
                    _slotLabels[i].GetComponentInParent<Button>().interactable = i == OpenSlots;
                }
                if (_slotPads[i] != null)
                {
                    _slotPads[i].sharedMaterial = i < OpenSlots ? PaperMaterial : ShadowMaterial;
                }
            }
        }

        public string FlavorName(BlockColorType color) => color switch {
            BlockColorType.Red => "BERRY", BlockColorType.Green => "PISTACHIO",
            BlockColorType.Yellow => "LEMON", BlockColorType.Blue => "BLUEBERRY",
            BlockColorType.Purple => "LAVENDER", _ => "ROSE"
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

        public GameObject MacaronPrefab(BlockColorType color) => macaronPrefabs[System.Array.IndexOf(Palette, color)];

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
            IconButton(canvas.transform, hudSettingIcon, new Vector2(.09f, .962f), new Vector2(58, 58), OpenSettings, "⚙");

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
            _status = Text(canvas.transform, "", new Vector2(.5f, .912f), new Vector2(620, 40), 20);
            _status.fontStyle = FontStyles.Bold;
            _status.color = new Color(.35f, .20f, .28f);

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

        public void OpenSettings()
        {
            if (_settingOverlay != null) return;
            if (GameManager.Instance.State == GameState.Win || GameManager.Instance.State == GameState.Fail) return;

            GameManager.Instance.SetState(GameState.Paused);
            Time.timeScale = 0f;

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) return;

            var overlayGo = new GameObject("Settings Overlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(canvas.transform, false);
            _settingOverlay = (RectTransform)overlayGo.transform;
            _settingOverlay.anchorMin = Vector2.zero;
            _settingOverlay.anchorMax = Vector2.one;
            _settingOverlay.offsetMin = _settingOverlay.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = new Color(.08f, .1f, .15f, .78f);

            var panel = new GameObject("Settings Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_settingOverlay, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.sizeDelta = new Vector2(560, 620);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = hudPanelSprite;
            panelImg.type = Image.Type.Sliced;
            panelImg.pixelsPerUnitMultiplier = 3;

            // Title
            var title = Text(panel.transform, "SETTINGS", new Vector2(.5f, .87f), new Vector2(400, 60), 40);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(.12f, .22f, .3f);

            // Close Button 'X'
            IconButton(panel.transform, hudCloseIcon, new Vector2(.88f, .88f), new Vector2(46, 46), CloseSettings, "✕");

            // Sound Button
            bool soundOn = PlayerPrefs.GetInt("SoundButton", 0) == 0;
            var soundBtn = StyledButton(panel.transform, soundOn ? "SOUND: ON" : "SOUND: OFF", new Vector2(.5f, .69f), new Vector2(360, 64), null,
                icon: soundOn ? hudSoundOnIcon : hudSoundOffIcon);
            var soundText = soundBtn.GetComponentInChildren<TextMeshProUGUI>();
            var soundIconImg = soundBtn.transform.Find("Icon")?.GetComponent<Image>();
            soundBtn.onClick.AddListener(() =>
            {
                int cur = PlayerPrefs.GetInt("SoundButton", 0);
                int next = cur == 0 ? 1 : 0;
                PlayerPrefs.SetInt("SoundButton", next);
                PlayerPrefs.Save();
                AudioListener.volume = next == 0 ? 1f : 0f;
                if (EKStudio.Audio.AudioController.Instance != null)
                    EKStudio.Audio.AudioController.Instance.IsMasterMuted = (next != 0);
                bool isOn = next == 0;
                if (soundText != null) soundText.text = isOn ? "SOUND: ON" : "SOUND: OFF";
                if (soundIconImg != null) soundIconImg.sprite = isOn ? hudSoundOnIcon : hudSoundOffIcon;
            });

            // Haptic Button
            bool hapticOn = PlayerPrefs.GetInt("HapticButton", 0) == 0;
            var hapticBtn = StyledButton(panel.transform, hapticOn ? "HAPTIC: ON" : "HAPTIC: OFF", new Vector2(.5f, .54f), new Vector2(360, 64), null,
                icon: hudHapticIcon);
            var hapticText = hapticBtn.GetComponentInChildren<TextMeshProUGUI>();
            hapticBtn.onClick.AddListener(() =>
            {
                int cur = PlayerPrefs.GetInt("HapticButton", 0);
                int next = cur == 0 ? 1 : 0;
                PlayerPrefs.SetInt("HapticButton", next);
                PlayerPrefs.SetInt("IsHapticOpen", next == 0 ? 1 : 0);
                PlayerPrefs.Save();
                bool isOn = next == 0;
                if (hapticText != null) hapticText.text = isOn ? "HAPTIC: ON" : "HAPTIC: OFF";
            });

            // Retry Button (Requested: Retry placed in Settings Popup)
            StyledButton(panel.transform, "RETRY STAGE", new Vector2(.5f, .38f), new Vector2(360, 66), () =>
            {
                Time.timeScale = 1f;
                Reload();
            }, icon: hudRetryIcon);

            // Resume Button
            var resumeBtn = StyledButton(panel.transform, "RESUME", new Vector2(.5f, .20f), new Vector2(360, 68), CloseSettings,
                customSprite: hudGreenButtonSprite, customPressed: hudGreenButtonPressedSprite);
            var resumeText = resumeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (resumeText != null)
            {
                resumeText.color = Color.white;
                resumeText.fontStyle = FontStyles.Bold;
                resumeText.fontSize = 28;
            }

            // Animate In
            panel.transform.localScale = Vector3.zero;
            panel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void CloseSettings()
        {
            if (_settingOverlay == null) return;
            var panel = _settingOverlay.GetChild(0);
            panel.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                if (_settingOverlay != null)
                {
                    Destroy(_settingOverlay.gameObject);
                    _settingOverlay = null;
                }
                Time.timeScale = 1f;
                GameManager.Instance.SetState(GameState.Playing);
            });
        }

        private void LateUpdate()
        {
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

        private void ShowOverlay(string title, string message, string button, UnityEngine.Events.UnityAction action, bool isWin = false)
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) return;
            if (_overlay != null) Destroy(_overlay.gameObject);

            var go = new GameObject("Result Overlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            _overlay = (RectTransform)go.transform;
            _overlay.anchorMin = Vector2.zero;
            _overlay.anchorMax = Vector2.one;
            _overlay.offsetMin = _overlay.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.08f, .1f, .15f, .78f);

            var panel = new GameObject("Result Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(go.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.sizeDelta = new Vector2(580, 560);
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = hudPanelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 3;

            // Header
            var titleText = Text(panel.transform, title, new Vector2(.5f, .84f), new Vector2(520, 70), 42);
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = isWin ? new Color(.1f, .45f, .25f) : new Color(.7f, .15f, .2f);

            // Message description
            var msgText = Text(panel.transform, message, new Vector2(.5f, .62f), new Vector2(520, 110), 26);
            msgText.color = new Color(.3f, .22f, .28f);

            if (isWin)
            {
                // Reward Badge: Coin Icon + Reward text
                var rewardPill = new GameObject("Reward Pill", typeof(RectTransform), typeof(Image));
                rewardPill.transform.SetParent(panel.transform, false);
                var pillRect = (RectTransform)rewardPill.transform;
                pillRect.anchorMin = pillRect.anchorMax = new Vector2(.5f, .42f);
                pillRect.sizeDelta = new Vector2(260, 56);
                var pillImg = rewardPill.GetComponent<Image>();
                pillImg.sprite = hudButtonSprite;
                pillImg.type = Image.Type.Sliced;
                pillImg.pixelsPerUnitMultiplier = 3;

                if (hudCoinIcon != null)
                {
                    var cGo = new GameObject("Coin Icon", typeof(RectTransform), typeof(Image));
                    cGo.transform.SetParent(rewardPill.transform, false);
                    var cRect = (RectTransform)cGo.transform;
                    cRect.anchorMin = cRect.anchorMax = new Vector2(.2f, .5f);
                    cRect.sizeDelta = new Vector2(34, 34);
                    var cImg = cGo.GetComponent<Image>();
                    cImg.sprite = hudCoinIcon;
                    cImg.preserveAspect = true;
                    cImg.raycastTarget = false;
                }

                var rText = Text(rewardPill.transform, $"+{shippingReward} COINS", new Vector2(.62f, .5f), new Vector2(170, 44), 24);
                rText.fontStyle = FontStyles.Bold;
                rText.color = new Color(.12f, .22f, .3f);

                // Main Action Button (Next Stage)
                var btn = StyledButton(panel.transform, button, new Vector2(.5f, .22f), new Vector2(340, 72), action,
                    customSprite: hudGreenButtonSprite, customPressed: hudGreenButtonPressedSprite);
                var btnTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTxt != null)
                {
                    btnTxt.color = Color.white;
                    btnTxt.fontStyle = FontStyles.Bold;
                    btnTxt.fontSize = 28;
                }
                btn.transform.DOPunchScale(Vector3.one * 0.06f, 1.2f, 1, 0.5f).SetLoops(-1).SetUpdate(true);
            }
            else
            {
                // Main Action Button (Try Again)
                var btn = StyledButton(panel.transform, button, new Vector2(.5f, .24f), new Vector2(340, 72), action,
                    icon: hudRetryIcon);
                var btnTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTxt != null)
                {
                    btnTxt.fontStyle = FontStyles.Bold;
                    btnTxt.fontSize = 26;
                }
            }

            // Animate In
            panel.transform.localScale = Vector3.zero;
            panel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            Time.timeScale = 1;
            foreach (var material in _materials) if (material != null) Destroy(material);
        }
    }
}
