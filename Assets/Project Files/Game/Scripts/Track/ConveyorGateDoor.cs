using System;
using UnityEngine;
using UnityEngine.Events;

namespace BlockShooter
{
    /// <summary>
    /// The removable wall section that plugs one conveyor opening.
    ///
    /// The track mesh keeps the hole cut permanently; this component owns a separate
    /// panel mesh that sits in the hole. Opening and closing only moves a transform,
    /// so it costs nothing at runtime — no sweep, no mesh upload, no collider bake.
    ///
    /// Panel space: +Z points outward through the gate, +Y is up, +X runs along the belt.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class ConveyorGateDoor : MonoBehaviour
    {
        public enum Motion
        {
            /// <summary>Drops straight down, out of sight under the belt.</summary>
            SlideDown = 0,
            /// <summary>Rises out of the way.</summary>
            SlideUp = 1,
            /// <summary>Pushes out away from the belt.</summary>
            SlideOutward = 2,
            /// <summary>Slides sideways along the belt, like a pocket door.</summary>
            SlideAlongBelt = 3,
            /// <summary>Hinges outward about the belt axis, like a drawbridge.</summary>
            SwingOutward = 4,
            /// <summary>Applies nothing. Read OpenAmount and drive the pose yourself.</summary>
            Custom = 5,
        }

        public enum ColliderMode { None = 0, Box = 1, Mesh = 2 }

        [Header("Binding")]
        public ConveyorTrackMeshBuilder track;
        [Tooltip("Index into ConveyorTrackMeshBuilder.openings.")]
        [Min(0)] public int openingIndex;
        [Tooltip("Which rim this panel fills. A Both-sided opening needs two doors.")]
        public ConveyorSide side = ConveyorSide.Right;
        public ConveyorTrackMeshBuilder.GatePanelPivot pivot = ConveyorTrackMeshBuilder.GatePanelPivot.Bottom;
        [Tooltip("Shrinks each end so the panel's caps do not z-fight the hole's caps. 1 mm is plenty.")]
        [Min(0f)] public float endInset = 0.001f;

        [Header("Motion")]
        public Motion motion = Motion.SlideDown;
        [Tooltip("Travel for the sliding modes, in metres.")]
        public float distance = 0.3f;
        [Tooltip("Hinge angle for SwingOutward, in degrees.")]
        public float angle = 100f;
        [Min(0.01f)] public float duration = 0.35f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("State the door is in when the level starts.")]
        public bool openOnStart;

        [Header("Collision")]
        public ColliderMode colliderMode = ColliderMode.Box;

        [Header("Events")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        /// <summary>Fires with true when fully open, false when fully closed.</summary>
        public event Action<bool> StateChanged;

        // ── Baked by Rebuild Panel ────────────────────────────────────────────
        [SerializeField, HideInInspector] private Vector3 _closedLocalPosition;
        [SerializeField, HideInInspector] private Quaternion _closedLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Bounds _panelBounds;

        // ── Runtime ───────────────────────────────────────────────────────────
        private float _amount;        // 0 = closed, 1 = open
        private float _target;
        private bool _lastReported;

        /// <summary>0 = fully closed, 1 = fully open.</summary>
        public float OpenAmount => _amount;
        public bool IsOpen => _amount >= 0.999f;
        public bool IsClosed => _amount <= 0.001f;
        public bool IsMoving => !Mathf.Approximately(_amount, _target);

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null)
            {
                Debug.LogWarning($"{name}: no baked panel mesh, rebuilding at runtime. " +
                                 "Run 'Rebuild Panel' in the editor and save it to an asset to avoid this.", this);
                RebuildPanel();
            }

            _amount = _target = openOnStart ? 1f : 0f;
            _lastReported = openOnStart;
            ApplyPose(_amount);
        }

        private void Update()
        {
            if (Mathf.Approximately(_amount, _target)) return;

            _amount = Mathf.MoveTowards(_amount, _target, Time.deltaTime / Mathf.Max(0.01f, duration));
            ApplyPose(_amount);

            if (!Mathf.Approximately(_amount, _target)) return;
            bool open = _target > 0.5f;
            if (open == _lastReported) return;
            _lastReported = open;
            StateChanged?.Invoke(open);
            if (open) onOpened?.Invoke(); else onClosed?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Control
        // ─────────────────────────────────────────────────────────────────────

        public void Open() => _target = 1f;
        public void Close() => _target = 0f;
        public void Toggle() => _target = _target > 0.5f ? 0f : 1f;

        /// <summary>Jumps to a state with no animation.</summary>
        public void SetOpenInstant(bool open)
        {
            _amount = _target = open ? 1f : 0f;
            _lastReported = open;
            ApplyPose(_amount);
        }

        /// <summary>Drives the panel directly. Use with Motion.Custom, or to scrub the animation.</summary>
        public void SetOpenAmount(float amount)
        {
            _amount = _target = Mathf.Clamp01(amount);
            ApplyPose(_amount);
        }

        private void ApplyPose(float raw)
        {
            float k = ease != null && ease.length > 0 ? ease.Evaluate(Mathf.Clamp01(raw)) : raw;

            Vector3 up = _closedLocalRotation * Vector3.up;
            Vector3 outward = _closedLocalRotation * Vector3.forward;
            Vector3 along = _closedLocalRotation * Vector3.right;

            switch (motion)
            {
                case Motion.SlideDown:
                    transform.localPosition = _closedLocalPosition - up * (distance * k);
                    transform.localRotation = _closedLocalRotation;
                    break;
                case Motion.SlideUp:
                    transform.localPosition = _closedLocalPosition + up * (distance * k);
                    transform.localRotation = _closedLocalRotation;
                    break;
                case Motion.SlideOutward:
                    transform.localPosition = _closedLocalPosition + outward * (distance * k);
                    transform.localRotation = _closedLocalRotation;
                    break;
                case Motion.SlideAlongBelt:
                    transform.localPosition = _closedLocalPosition + along * (distance * k);
                    transform.localRotation = _closedLocalRotation;
                    break;
                case Motion.SwingOutward:
                    transform.localPosition = _closedLocalPosition;
                    transform.localRotation = _closedLocalRotation * Quaternion.AngleAxis(angle * k, Vector3.right);
                    break;
                case Motion.Custom:
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Baking
        // ─────────────────────────────────────────────────────────────────────

        public ConveyorTrackMeshBuilder ResolveTrack()
        {
            if (track != null) return track;
            track = GetComponentInParent<ConveyorTrackMeshBuilder>();
            return track;
        }

        /// <summary>
        /// Regenerates the panel mesh from the opening and parks the transform in the closed pose.
        /// Editor-time operation — call it after moving or resizing the gate.
        /// </summary>
        [ContextMenu("Rebuild Panel")]
        public void RebuildPanel()
        {
            var t = ResolveTrack();
            if (t == null)
            {
                Debug.LogError($"{name}: no ConveyorTrackMeshBuilder found. Assign Track.", this);
                return;
            }
            if (openingIndex < 0 || openingIndex >= t.OpeningCount)
            {
                Debug.LogError($"{name}: opening {openingIndex} does not exist ({t.OpeningCount} on the track).", this);
                return;
            }
            if (side == ConveyorSide.Both)
            {
                Debug.LogError($"{name}: a door covers one rim. Use two doors for a Both-sided opening.", this);
                return;
            }

            var opening = t.openings[openingIndex];
            if (!opening.enabled)
                Debug.LogWarning($"{name}: opening '{opening.label}' is disabled, so the wall is still solid there. " +
                                 "The panel will z-fight it.", this);
            if (opening.side != ConveyorSide.Both && opening.side != side)
                Debug.LogWarning($"{name}: this door covers the {side} rim but opening '{opening.label}' " +
                                 $"cuts the {opening.side} rim.", this);

            Mesh mesh = t.BuildGatePanelMesh(openingIndex, side, pivot,
                out _closedLocalPosition, out _closedLocalRotation, endInset);
            if (mesh == null)
            {
                Debug.LogError($"{name}: the opening has no length, nothing to build.", this);
                return;
            }

            _panelBounds = mesh.bounds;

            var filter = GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            // Parent to the track so the baked local pose means what it says.
            if (transform.parent != t.transform) transform.SetParent(t.transform, false);
            transform.localScale = Vector3.one;

            SyncCollider();
            SetOpenInstant(openOnStart);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        private void SyncCollider()
        {
            var box = GetComponent<BoxCollider>();
            var meshCol = GetComponent<MeshCollider>();

            switch (colliderMode)
            {
                case ColliderMode.None:
                    if (box != null) DestroySafe(box);
                    if (meshCol != null) DestroySafe(meshCol);
                    break;

                case ColliderMode.Box:
                    if (meshCol != null) DestroySafe(meshCol);
                    if (box == null) box = gameObject.AddComponent<BoxCollider>();
                    box.center = _panelBounds.center;
                    box.size = _panelBounds.size;
                    break;

                case ColliderMode.Mesh:
                    if (box != null) DestroySafe(box);
                    if (meshCol == null) meshCol = gameObject.AddComponent<MeshCollider>();
                    meshCol.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                    break;
            }
        }

        private static void DestroySafe(Component c)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Writes the panel mesh to a .asset so it survives domain reloads and prefab saves.
        /// Do this once the gate geometry is final.
        /// </summary>
        [ContextMenu("Save Panel Mesh to Asset")]
        public void SavePanelMeshToAsset()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null) { RebuildPanel(); }
            if (filter.sharedMesh == null) return;

            const string folder = "Assets/MacaronFactory/Generated";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/MacaronFactory"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "MacaronFactory");
                UnityEditor.AssetDatabase.CreateFolder("Assets/MacaronFactory", "Generated");
            }

            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{gameObject.name}_Panel.asset");
            var copy = Instantiate(filter.sharedMesh);
            copy.name = $"{gameObject.name}_Panel";
            UnityEditor.AssetDatabase.CreateAsset(copy, path);
            UnityEditor.AssetDatabase.SaveAssets();

            filter.sharedMesh = copy;
            if (colliderMode == ColliderMode.Mesh)
            {
                var meshCol = GetComponent<MeshCollider>();
                if (meshCol != null) meshCol.sharedMesh = copy;
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"{name}: panel mesh saved to {path}", this);
        }

        [ContextMenu("Preview Open")]
        private void PreviewOpen() => SetOpenInstant(true);

        [ContextMenu("Preview Closed")]
        private void PreviewClosed() => SetOpenInstant(false);
#endif
    }
}
