using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CandyBlast.Cartoon;
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
        [Header("Existing package conveyor")]
        public LevelRoot conveyorSource;
        [Header("Hand-authored levels (played in list order)")]
        public MacaronLevel[] levels = System.Array.Empty<MacaronLevel>();
        private MacaronLevel _layout;
        [Header("Macaron Props prefabs")]
        [Tooltip("Berry, pistachio, lemon, blueberry, lavender, rose")]
        public GameObject[] macaronPrefabs = new GameObject[6];
        [Tooltip("Macaron size on the conveyor. Cakes shrink to their authored Pocket size when collected.")]
        [Min(.1f)] public float conveyorMacaronScale = 1.6f;
        public int maxRowWidth => _layout.columns;
        private float rowSpacing => _layout.rowSpacing;
        public float laneSpacing => Level.laneSpacing;
        [Header("Conveyor flow")]
        [Tooltip("World units per second used to move rows toward the endpoint of the conveyor.")]
        [Min(.1f)] public float conveyorSpeed = 1.2f;
        [Tooltip("Conveyor speed multiplier while the leading row matches an available waiting tray.")]
        [Min(1)] public float matchingConveyorMultiplier = 2.5f;
        [Tooltip("Seconds to transition between normal movement and automatic packing speed.")]
        [Min(.01f)] public float matchingSpeedTransition = .15f;
        private float _matchingSpeed = 1;
        private float exitZoneLength => _layout.exitZoneLength;
        public float stopBeforeExitDistance => _layout.stopBeforeExit;
        [Header("Macaron exit")]
        [Tooltip("Seconds between consecutive macaron launches, shared across all trays. Independent of flight duration.")]
        [Min(.01f)] public float macaronLaunchInterval = .08f;
        private float _nextMacaronLaunchTime;
        [Tooltip("Flight time in seconds. Set to 0 to use Macaron Exit Speed instead.")]
        [Min(0)] public float macaronExitTime = .35f;
        [Tooltip("Flight progress over normalized time (0 to 1). Use endpoints (0,0) and (1,1).")]
        public AnimationCurve macaronExitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Jump height above the flight path, in world units.")]
        [Min(0)] public float macaronExitJumpHeight = .7f;
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
        [Header("Carton shipping")]
        public CartonDeliverySequence cartonDeliveryPrefab;
        [Tooltip("Box bottom relative to the full tray's position, in world units.")]
        public Vector3 cartonDockOffset = new(0, 0, -1.2f);
        [Tooltip("Random dock offset on X/Z, sampled once per box. Y stays unchanged.")]
        public Vector2 cartonDockRandomRange = new(.2f, .25f);
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
        private readonly Dictionary<BlockColorType, Material> _flavors = new();
        private readonly TextMeshProUGUI[] _slotLabels = new TextMeshProUGUI[6];
        private readonly Renderer[] _slotPads = new Renderer[6];
        private TextMeshProUGUI _status, _coins, _progress, _stageText;
        private RectTransform _overlay;
        private RectTransform _hudRoot;
        private Sprite _hudSprite;
        private Texture2D _hudTexture;
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
                cartonDeliveryPrefab == null || cartonDeliveryPrefab.Carton == null ||
                levels == null || levels.Length == 0 || levels.Any(level => level == null) ||
                macaronPrefabs == null || macaronPrefabs.Length != Palette.Length || macaronPrefabs.Any(p => p == null))
            {
                Debug.LogError("Macaron Factory needs its conveyor, authored levels, carton delivery and six macaron prefabs.");
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
            BuildTrays();
            BuildConveyor();
            if (_layout != null)
            {
                var bounds = new List<Bounds>();
                foreach (var renderer in _layout.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && renderer.gameObject.name != "Factory floor") bounds.Add(renderer.bounds);
                bounds.Add(Level.conveyorController.GetComponent<Renderer>().bounds);
                var camera = Camera.main;
                var frame = camera.GetComponent<MacaronCameraFrame>() ?? camera.gameObject.AddComponent<MacaronCameraFrame>();
                frame.Frame(bounds, _layout.cameraTilt, _layout.cameraFieldOfView, _layout.cameraPadding);
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
                    block.Initialize(color, FlavorMaterial(color).color);
                    foreach (var renderer in block.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    var prefab = MacaronPrefab(color);
                    var visual = Instantiate(prefab, block.transform).transform;
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
                initialFrontT, laneSpacing, maxRowWidth, cakeDiameter);
            _remaining = colors.Count;
        }

        private void Update()
        {
            if (!_ready || !GameManager.Instance.IsPlaying) return;
            var conveyor = Level.conveyorController;
            if (conveyor.IsFrozen) return;
            bool packing = _transfers > 0 || _conveyorFlow.LeadingRowMatches(block =>
                _slots.Any(tray => tray != null && tray.CanReceive && tray.Color == block.ColorType));
            float fast = Mathf.Max(1, matchingConveyorMultiplier);
            _matchingSpeed = Mathf.MoveTowards(_matchingSpeed, packing ? fast : 1,
                Mathf.Max(.01f, fast - 1) * Time.deltaTime / Mathf.Max(.01f, matchingSpeedTransition));
            _conveyorFlow.Tick(conveyorSpeed * _speedMultiplier * _matchingSpeed, exitZoneLength, stopBeforeExitDistance);
            foreach (var block in PickupBlocks)
            {
                if (Time.time < _nextMacaronLaunchTime) break;
                foreach (var tray in _slots)
                {
                    if (tray == null || tray.Moving || tray.Shipping || tray.Color != block.ColorType || !tray.TryReserve()) continue;
                    StartCoroutine(Collect(block, tray));
                    break;
                }
            }
            CheckCompletion();
            if (IsDeadlocked())
            {
                _deadlockTime += Time.deltaTime;
                _status.text = "No matching tray. Unlock another slot!";
                if (_deadlockTime >= deadlockDelay) Finish(false);
            }
            else
            {
                _deadlockTime = 0;
                if (Time.time >= _noticeUntil) _status.text = _layout != null && !string.IsNullOrWhiteSpace(_layout.instruction)
                    ? _layout.instruction : "Choose a tray for the front macarons.";
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
                return false;
            }
            tray.StopClickFeedback();
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
            tray.Refresh(false);
        }

        private IEnumerator Collect(ConveyorBlock3D block, MacaronTray tray)
        {
            int pocket = tray.Filled + tray.Reserved - 1;
            // The last item clears/destroys its BlockGroup. Detach before firing that event.
            var parent = block.transform.parent;
            block.transform.SetParent(transform, true);
            _transfers++;
            if (!block.TryCollect())
            {
                block.transform.SetParent(parent, true);
                tray.CancelReservation();
                _transfers--;
                yield break;
            }
            _remaining--;
            _nextMacaronLaunchTime = Time.time + Mathf.Max(.01f, macaronLaunchInterval);
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
            _transfers--;
            UpdateHud();
            if (tray.Filled == tray.Capacity && tray.Reserved == 0) StartCoroutine(Ship(tray));
        }

        private IEnumerator Ship(MacaronTray tray)
        {
            tray.Shipping = true;
            tray.Label.text = "PACKED!";
            yield return new WaitForSeconds(Mathf.Max(.01f, trayReceiveBounceTime));
            CartonDeliverySequence delivery = null;
            GameObject proxy = null;
            bool completed = false;
            try
            {
                tray.StopReceiveBounce();
                proxy = new GameObject("Packed tray visual");
                proxy.transform.SetParent(transform, false);
                var bounds = CopyTrayVisual(tray, proxy.transform);
                delivery = Instantiate(cartonDeliveryPrefab, tray.transform.position, Quaternion.identity, transform);
                delivery.Carton.SetDimensions(bounds.size.x * 1.3f, bounds.size.z * 1.3f,
                    Mathf.Max(.3f, bounds.size.y * 2));
                delivery.OnCompleted.AddListener(() => completed = true);
                var dockJitter = new Vector3(Random.Range(-Mathf.Abs(cartonDockRandomRange.x), Mathf.Abs(cartonDockRandomRange.x)), 0,
                    Random.Range(-Mathf.Abs(cartonDockRandomRange.y), Mathf.Abs(cartonDockRandomRange.y)));
                delivery.PlayAt(tray.transform.position + cartonDockOffset + dockJitter, new[] { proxy.transform });
                if (!delivery.IsPlaying) throw new System.InvalidOperationException("Carton delivery could not start.");
                tray.gameObject.SetActive(false);
                while (delivery != null && delivery.isActiveAndEnabled && delivery.IsPlaying) yield return null;
                if (!completed) throw new System.InvalidOperationException("Carton delivery stopped before shipping completed.");
            }
            finally
            {
                // ResetSequence restores supplied visuals; hide them before disabling the carton.
                if (proxy != null) { proxy.SetActive(false); Destroy(proxy); }
                if (delivery != null) { delivery.gameObject.SetActive(false); Destroy(delivery.gameObject); }
                if (!completed && tray != null) tray.gameObject.SetActive(true);
            }
            int slot = System.Array.IndexOf(_slots, tray);
            if (slot >= 0) _slots[slot] = null;
            _shipped++;
            tray.gameObject.SetActive(false);
            UpdateHud();
            CheckCompletion();
        }

        private static Bounds CopyTrayVisual(MacaronTray tray, Transform parent)
        {
            var renderers = tray.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.enabled && r.GetComponent<TMP_Text>() == null && r.GetComponent<MeshFilter>() != null).ToArray();
            UnityEngine.Assertions.Assert.IsTrue(renderers.Length > 0, "A packed tray needs visible meshes.");
            var bounds = renderers[0].bounds;
            foreach (var source in renderers) bounds.Encapsulate(source.bounds);
            parent.position = bounds.center;
            var properties = new MaterialPropertyBlock();
            foreach (var source in renderers)
            {
                var copy = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                copy.transform.SetParent(parent, true);
                copy.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var renderer = copy.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                source.GetPropertyBlock(properties);
                renderer.SetPropertyBlock(properties);
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(properties, i);
                    renderer.SetPropertyBlock(properties, i);
                }
            }
            return bounds;
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

        public bool IsDeadlocked()
        {
            if (!_ready || !GameManager.Instance.IsPlaying || IsBusy || _remaining == 0) return false;
            for (int i = 0; i < OpenSlots; i++) if (_slots[i] == null) return false;
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
            if (win)
            {
                SaveManager.Coins += shippingReward;
                if (stageOverride == 0) PlayerPrefs.SetInt("Macaron.Stage", Stage + 1);
                PlayerPrefs.Save();
            }
            ShowOverlay(win ? "ORDER COMPLETE!" : "PACKING JAM!",
                win ? $"Every macaron shipped. +{shippingReward} coins" : "All open slots are full. The arriving colors do not match.",
                win ? "NEXT STAGE" : "TRY AGAIN", () => {
                    if (win) Stage++;
                    Reload();
                });
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
            _coins.text = $"COINS  {SaveManager.Coins}";
            _stageText.text = $"STAGE {Stage:00}";
            _progress.text = $"<b>{_remaining}</b>\nMacarons left";
            for (int i = 0; i < 6; i++)
            {
                _slotLabels[i].text = i < OpenSlots ? "" : i == OpenSlots ? $"+\n{unlockSlotCost}" : "LOCKED";
                _slotLabels[i].GetComponentInParent<Button>().interactable = i == OpenSlots;
                _slotPads[i].sharedMaterial = i < OpenSlots ? PaperMaterial : ShadowMaterial;
            }
        }

        public string FlavorName(BlockColorType color) => color switch {
            BlockColorType.Red => "BERRY", BlockColorType.Green => "PISTACHIO",
            BlockColorType.Yellow => "LEMON", BlockColorType.Blue => "BLUEBERRY",
            BlockColorType.Purple => "LAVENDER", _ => "ROSE"
        };

        public Material FlavorMaterial(BlockColorType color)
        {
            if (_flavors.TryGetValue(color, out var material)) return material;
            Color tint = MacaronPrefab(color).GetComponent<Renderer>().sharedMaterials[0].color;
            material = Material(FlavorName(color), tint);
            _flavors.Add(color, material);
            return material;
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

        private Button Button(Transform parent, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1, .92f, .81f, .96f);
            go.GetComponent<Image>().sprite = HudSprite();
            go.GetComponent<Image>().type = Image.Type.Sliced;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text(go.transform, text, new Vector2(.5f, .5f), size, 20);
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
            _stageText = Text(canvas.transform, "", new Vector2(.5f, .965f), new Vector2(210, 48), 28);
            _stageText.fontStyle = FontStyles.Bold;
            _stageText.color = Color.white;
            var levelBadge = new GameObject("Level badge", typeof(RectTransform), typeof(Image));
            levelBadge.transform.SetParent(canvas.transform, false);
            var badgeRect = (RectTransform)levelBadge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, .965f);
            badgeRect.sizeDelta = new Vector2(216, 50);
            levelBadge.GetComponent<Image>().sprite = HudSprite();
            levelBadge.GetComponent<Image>().type = Image.Type.Sliced;
            levelBadge.GetComponent<Image>().color = new Color(.77f, .19f, .13f);
            levelBadge.transform.SetAsFirstSibling();
            _coins = Text(canvas.transform, "", new Vector2(.85f, .965f), new Vector2(170, 42), 23);
            _progress = Text(canvas.transform, "", new Vector2(.25f, .61f), new Vector2(130, 70), 20);
            _progress.color = new Color(1, .93f, .7f);
            _status = Text(canvas.transform, "", new Vector2(.5f, .102f), new Vector2(660, 42), 17);
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var button = Button(canvas.transform, "", new Vector2(.5f, .5f), new Vector2(72, 42),
                    () => { if (index >= OpenSlots) TryUnlockSlot(); });
                _slotLabels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
                _slotLabels[i].fontSize = 15;
                button.GetComponent<Image>().color = Color.clear;
                var screen = Camera.main.WorldToViewportPoint(SlotPosition(i) + Vector3.back * .48f);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(screen.x, screen.y);
            }
            Button(canvas.transform, "RETRY", new Vector2(.19f, .043f), new Vector2(180, 58), Reload);
            var speed = Button(canvas.transform, "SPEED x1", new Vector2(.5f, .043f), new Vector2(180, 58), () => { });
            speed.onClick.AddListener(() => {
                if (!GameManager.Instance.IsPlaying) return;
                _speedMultiplier = _speedMultiplier > 1 ? 1 : 2;
                Level.conveyorController.speed = conveyorSpeed * _speedMultiplier;
                speed.GetComponentInChildren<TextMeshProUGUI>().text = _speedMultiplier > 1 ? "SPEED x2" : "SPEED x1";
            });
            Button(canvas.transform, "PAUSE", new Vector2(.81f, .043f), new Vector2(180, 58), () => {
                if (!GameManager.Instance.IsPlaying) return;
                GameManager.Instance.SetState(GameState.Paused);
                Time.timeScale = 0;
                ShowOverlay("TEA BREAK", "Your macarons can wait.", "RESUME", () => {
                    Destroy(_overlay.gameObject);
                    Time.timeScale = 1;
                    GameManager.Instance.SetState(GameState.Playing);
                });
            });
            _overlay = new GameObject("Overlay anchor", typeof(RectTransform)).GetComponent<RectTransform>();
            _overlay.SetParent(canvas.transform, false);
            _overlay.gameObject.SetActive(false);
            PositionHudMarkers();
        }

        private void LateUpdate()
        {
            if (_hudRoot != null) PositionHudMarkers();
        }

        private void PositionHudMarkers()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            _hudRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _hudRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _hudRoot.offsetMin = _hudRoot.offsetMax = Vector2.zero;
            for (int i = 0; i < 6; i++)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(SlotPosition(i));
                var rect = (RectTransform)_slotLabels[i].transform.parent;
                rect.anchorMin = rect.anchorMax = new Vector2((screen.x - safe.xMin) / safe.width, (screen.y - safe.yMin) / safe.height);
            }
            if (_layout != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(_layout.counterAnchor.position);
                _progress.rectTransform.anchorMin = _progress.rectTransform.anchorMax =
                    new Vector2((screen.x - safe.xMin) / safe.width, (screen.y - safe.yMin) / safe.height);
            }
        }

        private Sprite HudSprite()
        {
            if (_hudSprite != null) return _hudSprite;
            const int size = 32;
            _hudTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(8 - x, x - 23, 0), dy = Mathf.Max(8 - y, y - 23, 0);
                    _hudTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy))));
                }
            _hudTexture.Apply();
            _hudSprite = Sprite.Create(_hudTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
            return _hudSprite;
        }

        private void ShowOverlay(string title, string message, string button, UnityEngine.Events.UnityAction action)
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (_overlay != null) Destroy(_overlay.gameObject);
            var go = new GameObject("Result", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            _overlay = (RectTransform)go.transform;
            _overlay.anchorMin = Vector2.zero;
            _overlay.anchorMax = Vector2.one;
            _overlay.offsetMin = _overlay.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(1f, .91f, .85f, .97f);
            Text(go.transform, title, new Vector2(.5f, .6f), new Vector2(680, 100), 46).fontStyle = FontStyles.Bold;
            Text(go.transform, message, new Vector2(.5f, .49f), new Vector2(590, 140), 28);
            Button(go.transform, button, new Vector2(.5f, .35f), new Vector2(320, 85), action);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            Time.timeScale = 1;
            foreach (var material in _materials) if (material != null) Destroy(material);
            if (_hudSprite != null) Destroy(_hudSprite);
            if (_hudTexture != null) Destroy(_hudTexture);
        }
    }
}
