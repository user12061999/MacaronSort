using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace BlockShooter
{
    public static class MacaronLoopLevelAuthoring
    {
        private const string Folder = "Assets/MacaronFactory/Levels";
        private const string Path = Folder + "/Level_04_JunctionLoop.prefab";

        [MenuItem("Tools/Macaron Factory/Create Junction Loop Level")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new InvalidOperationException("Open MacaronFactory scene first.");
            var asset = AssetDatabase.LoadAssetAtPath<MacaronLevel>(Path);
            if (asset == null)
            {
                var root = PrefabUtility.LoadPrefabContents(Folder + "/Level_01_Welcome.prefab");
                try
                {
                    root.name = "Level_04_JunctionLoop";
                    var level = root.GetComponent<MacaronLevel>();
                    level.columns = 2;
                    level.stopBeforeExit = .08f;
                    level.exitZoneLength = .6f;
                    level.instruction = "Two feeders, one queue. Match the front macarons.";
                    var hood = root.transform.Find("Entrance hood");
                    if (hood != null) Object.DestroyImmediate(hood.gameObject);
                    var track = level.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>();
                    level.conveyorTrack = track;
                    level.conveyorPath.transform.localPosition = Vector3.zero;
                    level.conveyorPath.transform.localRotation = Quaternion.identity;
                    level.conveyorPath.transform.localScale = Vector3.one;
                    var points = new[] {
                        new Vector3(0,0,1.5f), new Vector3(-.85f,0,1.5f), new Vector3(-1.65f,0,2.3f),
                        new Vector3(-1.65f,0,3.2f), new Vector3(-1.65f,0,4.1f), new Vector3(-.85f,0,4.9f),
                        new Vector3(.85f,0,4.9f), new Vector3(1.65f,0,4.1f), new Vector3(1.65f,0,3.2f),
                        new Vector3(1.65f,0,2.3f), new Vector3(.85f,0,1.5f) };
                    var directions = new[] { Vector3.left,Vector3.left,Vector3.forward,Vector3.forward,Vector3.forward,
                        Vector3.right,Vector3.right,Vector3.back,Vector3.back,Vector3.back,Vector3.left };
                    level.conveyorPath.Spline = MakeSpline(points, directions, true);
                    track.openings.Clear();
                    track.openZoneEnabled = false;
                    track.sweepFrom = 0; track.sweepTo = 1;
                    float diameter = level.conveyorMacaronScale * factory.macaronPrefabs.Max(p => {
                        var b = p.GetComponent<Renderer>().localBounds;
                        return 2 * Mathf.Max(Mathf.Abs(b.center.x) + b.extents.x, Mathf.Abs(b.center.z) + b.extents.z);
                    });
                    level.laneSpacing = Mathf.Max(level.laneSpacing, diameter + .015f);
                    track.beltHalfWidth = level.laneSpacing * .5f + Mathf.Max(.16f, diameter * .5f + .02f);
                    track.resolution = 240;
                    track.railHeight = .18f; track.wallAboveBelt = .045f;
                    level.feederBranches = new ConveyorJunction[2];
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1 : 1;
                        var go = new GameObject(i == 0 ? "Left feeder" : "Right feeder");
                        go.transform.SetParent(root.transform, false);
                        var spline = go.AddComponent<SplineContainer>();
                        spline.Spline = MakeSpline(new[] { new Vector3(side * 3.05f,0,3.2f + side * .75f),
                            new Vector3(side * 1.65f,0,3.2f) },
                            new[] { Vector3.left * side, Vector3.back * side }, false);
                        var branch = go.AddComponent<ConveyorTrackMeshBuilder>();
                        branch.beltHalfWidth = track.beltHalfWidth;
                        branch.railHeight = track.railHeight; branch.wallAboveBelt = track.wallAboveBelt;
                        branch.resolution = 100;
                        go.GetComponent<MeshRenderer>().sharedMaterials = track.GetComponent<MeshRenderer>().sharedMaterials;
                        var junction = go.AddComponent<ConveyorJunction>();
                        junction.mainTrack = track;
                        junction.joinAt = ConveyorJunction.BranchEnd.End;
                        junction.branchClearance = -.04f;
                        junction.SyncJunction();
                        level.feederBranches[i] = junction;
                        SaveMesh(branch, i == 0 ? "Left" : "Right");
                    }
                    SaveMesh(track, "Main");
                    level.counterAnchor.localPosition = new Vector3(-.45f,.05f,3.3f);
                    ConfigureContinuousLevel(level);
                    level.ValidateLayout();
                    asset = PrefabUtility.SaveAsPrefabAsset(root, Path).GetComponent<MacaronLevel>();
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Undo.RecordObject(factory, "Add junction loop level");
            if (!factory.levels.Contains(asset)) factory.levels = factory.levels.Concat(new[] { asset }).ToArray();
            factory.stageOverride = Array.IndexOf(factory.levels, asset) + 1;
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            EditorSceneManager.SaveScene(factory.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        internal static Spline MakeSpline(Vector3[] points, Vector3[] directions, bool closed)
        {
            var incoming = new Vector3[points.Length];
            var outgoing = new Vector3[points.Length];
            for (int i = 0; i < (closed ? points.Length : points.Length - 1); i++)
            {
                int next = (i + 1) % points.Length;
                var delta = points[next] - points[i];
                bool straight = directions[i] == directions[next];
                outgoing[i] = directions[i] * (straight ? delta.magnitude / 3 : Mathf.Abs(Vector3.Dot(delta, directions[i])) * .55228475f);
                incoming[next] = -directions[next] * (straight ? delta.magnitude / 3 : Mathf.Abs(Vector3.Dot(delta, directions[next])) * .55228475f);
            }
            var spline = new Spline();
            for (int i = 0; i < points.Length; i++) spline.Add(new BezierKnot(points[i], incoming[i], outgoing[i]), TangentMode.Broken);
            spline.Closed = closed;
            return spline;
        }

        public static void ConfigureContinuousLevel(MacaronLevel level)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            level.instruction = "Fill the trays as macarons pass the gate. Feeders refill empty spaces.";
            level.exitZoneLength = .65f;
            level.counterAnchor.localPosition = new Vector3(0,.05f,3.3f);
            var badge = level.transform.Find("Remaining badge");
            if (badge != null) badge.localPosition = new Vector3(0,.07f,3.3f);
            foreach (string name in new[] { "Tray board rim", "Tray board inset" })
            {
                var board = level.transform.Find(name);
                var position = board.localPosition; position.z = -2.75f; board.localPosition = position;
                var scale = board.localScale; scale.z = name.EndsWith("rim") ? 4.65f : 4.45f; board.localScale = scale;
            }
            foreach (Transform child in level.trayRoot.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var colors = new[] { BlockColorType.Red, BlockColorType.Green, BlockColorType.Blue,
                BlockColorType.Yellow, BlockColorType.Purple, BlockColorType.Orange };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MacaronFactory/Prefabs/Tray_2x4.prefab");
            for (int i = 0; i < 12; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.trayRoot);
                var tray = go.GetComponent<MacaronTray>();
                tray.levelColor = colors[i % 6]; tray.stackLayer = 0; tray.mystery = false;
                go.name = $"{i + 1:00} {tray.levelColor} Tray 2x4";
                go.transform.localPosition = new Vector3((i % 3 - 1) * 1.75f, 0, -1.12f - i / 3 * 1.08f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * .98f;
                var tint = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/Tray_{tray.levelColor}.mat");
                foreach (var renderer in tray.tintRenderers)
                {
                    var materials = renderer.sharedMaterials; materials[0] = tint; renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                foreach (var renderer in tray.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Macaron_Row"))
                    {
                        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Tray lining.mat");
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                PrefabUtility.RecordPrefabInstancePropertyModifications(tray);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            level.useCustomMacaronOrder = true;
            level.macaronOrder = Enumerable.Range(0, 24).Select(i => new MacaronLevel.MacaronBatch {
                color = colors[i % 6], count = 4 }).ToArray();
            var track = level.conveyorTrack;
            int index = track.openings.FindIndex(o => o.label == "Collection gate");
            if (index < 0)
            {
                index = track.openings.Count;
                track.openings.Add(new ConveyorOpening { label = "Collection gate", side = ConveyorSide.Left,
                    measure = ConveyorMeasure.Normalized, start = .965f, length = .07f, lipHeight = 0 });
            }
            RefineLayout(level);
            SaveMesh(track, "Main");
        }

        public static void RefineLayout(MacaronLevel level)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (level.collectionGate != null) Object.DestroyImmediate(level.collectionGate.gameObject);
            level.collectionGate = null;
            for (int i = 0; i < level.feederBranches.Length; i++)
            {
                var junction = level.feederBranches[i];
                float side = i == 0 ? -1 : 1;
                float z = i == 0 ? 2.9f : 3.5f;
                junction.GetComponent<SplineContainer>().Spline = MakeSpline(
                    new[] { new Vector3(side * 3.05f,0,z), new Vector3(side * 1.65f,0,z) },
                    new[] { Vector3.left * side, Vector3.left * side }, false);
                junction.branchClearance = 0;
                junction.SyncJunction();
                SaveMesh(junction.Branch, i == 0 ? "Left" : "Right");
            }
            var trays = level.GetTrays();
            float width = trays.Max(t => t.GetComponent<BoxCollider>().bounds.size.x) + .025f;
            float depth = trays.Max(t => t.GetComponent<BoxCollider>().bounds.size.z) + .025f;
            float centerZ = level.transform.Find("Tray board inset").localPosition.z;
            int rows = Mathf.CeilToInt(trays.Length / 3f);
            for (int i = 0; i < trays.Length; i++)
            {
                var bounds = trays[i].GetComponent<BoxCollider>().bounds;
                var current = level.transform.InverseTransformPoint(bounds.center);
                var target = new Vector3((i % 3 - 1) * width, current.y, centerZ + ((rows - 1) * .5f - i / 3) * depth);
                trays[i].transform.position += level.transform.TransformVector(target - current);
                PrefabUtility.RecordPrefabInstancePropertyModifications(trays[i].transform);
            }
            SaveMesh(level.conveyorTrack, "Main");
        }

        private static void SaveMesh(ConveyorTrackMeshBuilder builder, string suffix)
        {
            builder.BuildMesh();
            // Copy generated geometry so future rebuilds cannot destroy the persistent asset.
            var mesh = Object.Instantiate(builder.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "JunctionLoop_" + suffix;
            string path = Folder + "/Level_04_JunctionLoop_" + suffix + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            builder.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}
