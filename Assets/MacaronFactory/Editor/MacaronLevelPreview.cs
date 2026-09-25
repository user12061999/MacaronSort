using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BlockShooter.Editor
{
    [InitializeOnLoad]
    public static class MacaronLevelPreview
    {
        public static bool Enabled = true;
        public static string Error { get; private set; }
        private static MacaronLevel _source;
        private static GameObject _root;
        private static Bounds _bounds;
        private static string _signature;
        private static double _nextRefresh;

        static MacaronLevelPreview()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Clear(); };
            Undo.undoRedoPerformed += () => _signature = null;
        }

        public static void Watch(MacaronLevel level)
        {
            if (_source != level) { _source = level; _signature = null; }
        }
        public static void Refresh() { _signature = null; _nextRefresh = 0; }
        public static void Focus()
        {
            if (_root != null && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(_bounds.center, Quaternion.Euler(_source.cameraTilt, 0, 0), _bounds.size.magnitude * .6f);
        }
        public static void Clear()
        {
            if (_root != null)
            {
                var meshes = _root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Where(m => m != null && !AssetDatabase.Contains(m)).Distinct().ToArray();
                Object.DestroyImmediate(_root);
                foreach (var mesh in meshes) if (mesh != null) Object.DestroyImmediate(mesh);
            }
            _signature = null;
        }

        private static void Update()
        {
            if (!Enabled || _source == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + .3;
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) { Error = "Open the MacaronFactory scene to supply cake prefabs and camera settings."; return; }
            string signature = EditorJsonUtility.ToJson(_source) + EditorJsonUtility.ToJson(factory)
                + Screen.width + ":" + Screen.height;
            if (factory.ColorRegistry != null)
            {
                signature += EditorJsonUtility.ToJson(factory.ColorRegistry);
                foreach (BlockColorType color in Enum.GetValues(typeof(BlockColorType)))
                {
                    var material = factory.ColorRegistry.GetMaterial(color);
                    if (material != null) signature += EditorJsonUtility.ToJson(material);
                    var trayMaterial = factory.ColorRegistry.GetTrayMaterial(color);
                    if (trayMaterial != null) signature += EditorJsonUtility.ToJson(trayMaterial);
                }
            }
            foreach (var spline in _source.GetComponentsInChildren<UnityEngine.Splines.SplineContainer>())
                signature += EditorJsonUtility.ToJson(spline) + EditorJsonUtility.ToJson(spline.transform);
            if (_source.trayRoot != null)
                foreach (var tray in _source.GetTrays()) signature += EditorJsonUtility.ToJson(tray) + EditorJsonUtility.ToJson(tray.transform);
            if (signature == _signature) return;
            Clear();
            _signature = signature;
            try { Build(factory); Error = null; }
            catch (Exception error) { Clear(); _signature = signature; Error = error.Message; }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Build(MacaronFactory factory)
        {
            _root = new GameObject("Macaron level preview (not saved)") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(_root, factory.gameObject.scene);
            _root.transform.position = Vector3.right * 1000;
            var level = Object.Instantiate(_source, _root.transform);
            level.transform.localPosition = Vector3.zero;
            int preset = level.conveyorShape == MacaronLevel.ConveyorShape.Automatic
                ? Mathf.Max(0, Array.IndexOf(factory.levels, _source)) % 10 : (int)level.conveyorShape - 1;
            if (level.conveyorPath != null) level.conveyorPath.gameObject.SetActive(false);
            foreach (var branch in level.feederBranches) if (branch != null) branch.gameObject.SetActive(false);
            if (level.collectionGate != null) level.collectionGate.gameObject.SetActive(false);
            if (level.gateMountRoot != null) level.gateMountRoot.gameObject.SetActive(false);
            foreach (Transform child in level.transform)
                if (child.name.StartsWith("Conveyor board")) child.gameObject.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/MacaronFactory/ConveyorPresets/ConveyorTest_{preset:00}.prefab");
            if (source == null) throw new InvalidOperationException("Missing conveyor preset library.");
            var belt = Object.Instantiate(source, level.transform);
            // Runtime also adds an inlet to the two originally branchless shapes.
            var track = belt.AddComponent<SodaConveyor.SodaConveyorTrack>();
            track.SideMaterial = belt.GetComponent<Renderer>().sharedMaterials[0];
            track.TopMaterial = belt.GetComponent<Renderer>().sharedMaterials[1];
            track.SetTrackShape(preset, 1);
            float diameter = factory.macaronPrefabs.Max(prefab =>
            {
                var bounds = prefab.GetComponent<Renderer>().localBounds;
                return 2 * Mathf.Max(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.z) + bounds.extents.z);
            }) * level.conveyorMacaronScale;
            track.SetLaneCount(level.conveyorLaneCount);
            track.SetItemDiameter(diameter);
            track.SetSpacing(level.laneSpacing, level.rowSpacing);
            track.Configure(factory.sourceLoopSpeed);
            track.BuildVisualBelt();
            var mainBounds = new Bounds(track.EvaluateWorld(0, out _), Vector3.zero);
            for (int i = 1; i < 128; i++) mainBounds.Encapsulate(track.EvaluateWorld(i / 128f, out _));
            mainBounds.Expand(track.OuterRadius * 2 + .3f);
            var offset = new Vector3(level.waitingSlots.Average(slot => slot.position.x) - mainBounds.center.x,
                0, level.waitingSlots.Max(slot => slot.position.z) + 1 - mainBounds.min.z);
            belt.transform.position += offset;
            mainBounds.center += offset;
            AddConveyorMacaronPreview(factory, level, track);
            foreach (var tray in level.GetTrays())
            {
                if (tray.lid != null) tray.lid.gameObject.SetActive(tray.mystery);
                factory.ApplyTrayAppearance(tray.tintRenderers, tray.levelColor);
            }
            System.Collections.Generic.IEnumerable<Bounds> PreviewBounds() => level.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled && renderer.name != "Factory floor" && !renderer.transform.IsChildOf(belt.transform))
                .Select(renderer => renderer.bounds).Concat(new[] { mainBounds });
            foreach (var collider in _root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var bounds = PreviewBounds().ToArray();
            _bounds = bounds[0]; foreach (var b in bounds) _bounds.Encapsulate(b);
            var cameraObject = new GameObject("Level preview camera");
            cameraObject.transform.SetParent(_root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            if (Camera.main != null) camera.CopyFrom(Camera.main);
            camera.targetTexture = null;
            camera.depth = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.88f, .85f, .78f);
            camera.enabled = true;
            cameraObject.AddComponent<MacaronCameraFrame>().FrameFactoryLayout(level, PreviewBounds);
            bounds = PreviewBounds().ToArray();
            _bounds = bounds[0]; foreach (var box in bounds) _bounds.Encapsulate(box);
            foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void AddConveyorMacaronPreview(MacaronFactory factory, MacaronLevel level,
            SodaConveyor.SodaConveyorTrack track)
        {
            var root = new GameObject("Conveyor macarons (preview only)").transform;
            root.SetParent(track.transform, true);
            var colors = new[] { BlockShooter.BlockColorType.Red,
                BlockShooter.BlockColorType.Green, BlockShooter.BlockColorType.Yellow,
                BlockShooter.BlockColorType.Blue, BlockShooter.BlockColorType.Purple,
                BlockShooter.BlockColorType.Orange };
            // ponytail: cap preview geometry at 64 rows; raise this if authors need denser full-loop previews.
            int rows = Mathf.Clamp(Mathf.FloorToInt(track.SplineWorldLength / Mathf.Max(.01f, level.rowSpacing)), 1, 64);
            for (int row = 0; row < rows; row++)
            {
                float t = Mathf.Repeat(1f - row * level.rowSpacing / track.SplineWorldLength, 1f);
                var center = track.EvaluateWorld(t, out var forward);
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                var color = colors[row % colors.Length];
                var prefab = factory.MacaronPrefab(color);
                var renderer = prefab.GetComponent<Renderer>();
                float scale = level.conveyorMacaronScale;
                for (int lane = 0; lane < level.conveyorLaneCount; lane++)
                {
                    var macaron = Object.Instantiate(prefab, root);
                    macaron.name = $"Preview {color}";
                    macaron.transform.localScale = Vector3.one * scale;
                    macaron.transform.SetPositionAndRotation(
                        center + right * ((lane - (level.conveyorLaneCount - 1) * .5f) * level.laneSpacing)
                        + Vector3.up * (-renderer.localBounds.min.y * scale), rotation);
                    factory.ApplyMacaronColor(macaron.GetComponent<Renderer>(), color);
                }
            }
        }
    }
}
