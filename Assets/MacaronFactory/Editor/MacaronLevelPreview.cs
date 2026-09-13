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
            _source.ValidateLayout();
            _root = new GameObject("Macaron level preview (not saved)") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(_root, factory.gameObject.scene);
            _root.transform.position = Vector3.right * 1000;
            var level = Object.Instantiate(_source, _root.transform);
            level.transform.localPosition = Vector3.zero;
            level.AlignExitToWaitingSlots();
            float scale = Mathf.Max(.1f, factory.conveyorMacaronScale);
            float diameter = scale * factory.macaronPrefabs.Max(p => {
                var b = p.GetComponent<Renderer>().localBounds;
                return 2 * Mathf.Max(Mathf.Abs(b.center.x) + b.extents.x, Mathf.Abs(b.center.z) + b.extents.z);
            });
            float laneSpacing = Mathf.Max(level.laneSpacing, diameter + .015f);
            var track = level.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>();
            track.beltHalfWidth = laneSpacing * (level.columns - 1) * .5f + Mathf.Max(.16f, diameter * .5f + .02f);
            track.railHeight = .18f; track.wallAboveBelt = .045f;
            foreach (var junction in level.feederBranches)
            {
                junction.Branch.beltHalfWidth = track.beltHalfWidth;
                junction.SyncJunction();
            }
            track.BuildMesh();
            foreach (var tray in level.GetTrays()) if (tray.lid != null) tray.lid.gameObject.SetActive(tray.mystery);
            var colors = level.BuildMacaronOrder();
            foreach (var pose in MacaronLoopFlow.PreviewLayout(level, diameter))
            {
                var prefab = factory.MacaronPrefab(colors[pose.index]);
                var cake = Object.Instantiate(prefab, pose.position, pose.rotation, _root.transform);
                cake.transform.localScale = Vector3.one * scale;
                cake.transform.position += cake.transform.up * (-prefab.GetComponent<Renderer>().localBounds.min.y * scale);
            }
            foreach (var collider in _root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var bounds = _root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.name != "Factory floor").Select(r => r.bounds).ToArray();
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
            cameraObject.AddComponent<MacaronCameraFrame>().Frame(bounds, level.cameraTilt, level.cameraFieldOfView, level.cameraPadding);
            foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
