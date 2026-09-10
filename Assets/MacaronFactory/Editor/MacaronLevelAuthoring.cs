using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.Editor
{
    [CustomEditor(typeof(MacaronLevel))]
    public sealed class MacaronLevelInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Edit this prefab in Prefab Mode. Move/rotate children under Trays. Each tray stores its color, stack layer and mystery flag. Sibling order breaks ties in cake supply order. Edit Conveyor Path with Unity's Spline tool.", MessageType.Info);
            if (GUILayout.Button("Rebuild conveyor preview"))
            {
                var level = (MacaronLevel)target;
                var builder = level.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>();
                Undo.RecordObject(builder, "Resize conveyor preview");
                builder.beltHalfWidth = level.laneSpacing * (level.columns - 1) * .5f + .16f;
                builder.BuildMesh();
                // Save a private mesh for this level so other level previews are not overwritten.
                string path = AssetDatabase.GetAssetPath(level);
                if (string.IsNullOrEmpty(path)) path = PrefabStageUtility.GetCurrentPrefabStage()?.assetPath;
                if (!string.IsNullOrEmpty(path))
                {
                    string meshPath = System.IO.Path.ChangeExtension(path, null) + "_Track.asset";
                    var mesh = builder.GetComponent<MeshFilter>().sharedMesh;
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else { EditorUtility.CopySerialized(mesh, existing); builder.GetComponent<MeshFilter>().sharedMesh = existing; }
                }
                EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            }
        }
    }

    public static class MacaronLevelAuthoring
    {
        private const string Folder = "Assets/MacaronFactory/Levels";

        [MenuItem("Tools/Macaron Factory/Create Hand-authored Starter Levels")]
        public static void CreateStarterLevels()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before authoring levels.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/MacaronFactory", "Levels");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new System.InvalidOperationException("Open MacaronFactory scene first.");
            var cream = Material("Cream", new Color(1f, .91f, .74f));
            var wood = Material("Caramel", new Color(.69f, .38f, .14f));
            var belt = Material("Belt", new Color(.32f, .28f, .24f));
            var pad = Material("Slot", new Color(.94f, .81f, .62f));
            var panelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/RoundedPanel.asset");
            if (panelMesh == null) { panelMesh = RoundedPanel(); AssetDatabase.CreateAsset(panelMesh, Folder + "/RoundedPanel.asset"); }
            var compact = CreateLevel("Level_01_Packed", false, factory, panelMesh, cream, wood, belt, pad);
            var stacked = CreateLevel("Level_02_Stacked", true, factory, panelMesh, cream, wood, belt, pad);
            Undo.RecordObject(factory, "Assign hand-authored levels");
            factory.levels = new[] { compact, stacked };
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            EditorSceneManager.SaveScene(factory.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = compact;
        }

        private static MacaronLevel CreateLevel(string name, bool stacked, MacaronFactory factory, Mesh panel,
            Material cream, Material wood, Material belt, Material pad)
        {
            string path = $"{Folder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<MacaronLevel>(path);
            if (existing != null) return existing;
            var root = new GameObject(name);
            try
            {
                var level = root.AddComponent<MacaronLevel>();
                level.columns = 3;
                level.cameraTilt = 72;
                Board("Conveyor board", root.transform, new Vector3(0, -.24f, 3.1f), new Vector3(6.6f, .3f, 5.6f), panel, cream, wood);
                Board("Waiting bench", root.transform, new Vector3(0, -.12f, .1f), new Vector3(6.6f, .34f, 1.18f), panel, cream, wood);
                Board("Tray board", root.transform, new Vector3(0, -.24f, -3.15f), new Vector3(6.2f, .3f, 5.1f), panel, cream, wood);
                // Background sits behind the three boards and fills their visible surroundings.
                Panel("Factory floor", root.transform, new Vector3(0, -.6f, 0), new Vector3(9, .08f, 14), panel,
                    Material("Floor", new Color(.84f, .81f, .74f)));
                for (int i = 0; i < 6; i++)
                    level.waitingSlots[i] = Panel($"Waiting slot {i + 1}", root.transform,
                        new Vector3((i - 2.5f) * 1.02f, .08f, .1f), new Vector3(.95f, .055f, .92f), panel, pad).transform;
                level.counterAnchor = new GameObject("Cakes remaining anchor").transform;
                level.counterAnchor.SetParent(root.transform, false);
                level.counterAnchor.localPosition = new Vector3(-1.5f, .16f, 1.15f);
                Panel("Remaining badge", root.transform, new Vector3(-1.5f, .07f, 1.15f), new Vector3(1.55f, .05f, .66f), panel, belt);

                var track = new GameObject("Conveyor Path");
                track.transform.SetParent(root.transform, false);
                level.conveyorPath = track.AddComponent<SplineContainer>();
                level.conveyorPath.Spline = CreateStarterConveyorPath(stacked);
                var builder = track.AddComponent<ConveyorTrackMeshBuilder>();
                builder.beltHalfWidth = .41f;
                builder.railHeight = .16f;
                builder.wallAboveBelt = .045f;
                builder.openZoneEnabled = false;
                builder.resolution = 180;
                track.GetComponent<MeshRenderer>().sharedMaterials = new[] { wood, belt };
                builder.BuildMesh();
                AssetDatabase.CreateAsset(track.GetComponent<MeshFilter>().sharedMesh, $"{Folder}/{name}_Track.asset");
                Panel("Entrance hood", root.transform, new Vector3(2.8f, .3f, 5.1f), new Vector3(.85f, .5f, 1.05f), panel, wood);
                level.trayRoot = new GameObject("Trays - move and rotate these").transform;
                level.trayRoot.SetParent(root.transform, false);
                var small = AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_1x4.prefab");
                var large = AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_2x4.prefab");
                if (stacked)
                {
                    Tray(level, large, -1.57f,-1.8f,0,0,BlockColorType.Red,false,factory);
                    Tray(level, small, 0,-1.8f,0,0,BlockColorType.Blue,true,factory);
                    Tray(level, large, 1.57f,-1.8f,0,0,BlockColorType.Green,false,factory);
                    Tray(level, small, -1.57f,-3.6f,90,0,BlockColorType.Yellow,true,factory);
                    Tray(level, large, 0,-3.6f,0,0,BlockColorType.Purple,true,factory);
                    Tray(level, small, 1.57f,-3.6f,90,0,BlockColorType.Orange,false,factory);
                    Tray(level, small, -1.6f,-1.95f,90,1,BlockColorType.Blue,false,factory);
                    Tray(level, small, 0,-1.6f,32,1,BlockColorType.Orange,false,factory);
                    Tray(level, small, 1.6f,-1.95f,90,1,BlockColorType.Purple,false,factory);
                    Tray(level, large, -.85f,-3.15f,0,1,BlockColorType.Green,false,factory);
                    Tray(level, small, 1.35f,-3.9f,-22,1,BlockColorType.Red,false,factory);
                    Tray(level, small, -.9f,-4.65f,0,0,BlockColorType.Yellow,false,factory);
                }
                else
                {
                    Tray(level, large,-1.56f,-1.6f,0,0,BlockColorType.Red,false,factory);
                    Tray(level, large,0,-1.6f,0,0,BlockColorType.Blue,false,factory);
                    Tray(level, large,1.56f,-1.6f,0,0,BlockColorType.Green,false,factory);
                    Tray(level, small,-2,-2.91f,90,0,BlockColorType.Yellow,false,factory);
                    Tray(level, large,-.7f,-2.68f,0,0,BlockColorType.Purple,false,factory);
                    Tray(level, large,.86f,-2.68f,0,0,BlockColorType.Orange,false,factory);
                    Tray(level, small,2.04f,-2.91f,90,0,BlockColorType.Blue,false,factory);
                    Tray(level, small,-1.56f,-4.05f,0,0,BlockColorType.Green,false,factory);
                    Tray(level, small,0,-4.05f,0,0,BlockColorType.Red,false,factory);
                    Tray(level, small,1.56f,-4.05f,0,0,BlockColorType.Yellow,false,factory);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<MacaronLevel>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Tray(MacaronLevel level, MacaronTray prefab, float x, float z, float angle,
            int layer, BlockColorType color, bool mystery, MacaronFactory factory)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, level.trayRoot);
            var tray = go.GetComponent<MacaronTray>();
            go.name = $"{level.trayRoot.childCount:00} {color} {prefab.name}";
            go.transform.localPosition = new Vector3(x, layer * .46f, z);
            go.transform.localRotation = Quaternion.Euler(0, angle, 0);
            tray.levelColor = color;
            tray.stackLayer = layer;
            tray.mystery = mystery;
            int paletteIndex = System.Array.IndexOf(new[] { BlockColorType.Red, BlockColorType.Green, BlockColorType.Yellow,
                BlockColorType.Blue, BlockColorType.Purple, BlockColorType.Orange }, color);
            Color tint = factory.macaronPrefabs[paletteIndex].GetComponent<Renderer>().sharedMaterial.color;
            var material = Material("Tray_" + color, tint);
            foreach (var renderer in tray.tintRenderers)
            {
                var materials = renderer.sharedMaterials;
                materials[0] = material;
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(tray);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        public static Spline CreateStarterConveyorPath(bool stacked)
        {
            float exitX = stacked ? .7f : 1.1f;
            Vector3[] points = {
                new(2.75f,0,5.1f), new(-1.85f,0,5.1f), new(-2.5f,0,4.35f),
                new(-1.85f,0,3.6f), new(1.85f,0,3.6f), new(2.5f,0,2.85f),
                new(1.85f,0,2.1f), new(exitX + .6f,0,2.1f),
                new(exitX,0,1.5f), new(exitX,0,.85f)
            };
            Vector3[] directions = {
                Vector3.left, Vector3.left, Vector3.back, Vector3.right, Vector3.right,
                Vector3.back, Vector3.left, Vector3.left, Vector3.back, Vector3.back
            };
            var incoming = new Vector3[points.Length];
            var outgoing = new Vector3[points.Length];
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                // Cubic quarter-circle handles keep the inner wall from folding at AutoSmooth knots.
                bool straight = directions[i] == directions[i + 1];
                outgoing[i] = directions[i] * (straight ? delta.magnitude / 3 :
                    Mathf.Abs(Vector3.Dot(delta, directions[i])) * .55228475f);
                incoming[i + 1] = -directions[i + 1] * (straight ? delta.magnitude / 3 :
                    Mathf.Abs(Vector3.Dot(delta, directions[i + 1])) * .55228475f);
            }
            var spline = new Spline();
            for (int i = 0; i < points.Length; i++)
                spline.Add(new BezierKnot(points[i], incoming[i], outgoing[i]), TangentMode.Broken);
            spline.Closed = false;
            return spline;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", .23f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Panel(string name, Transform parent, Vector3 position, Vector3 size, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void Board(string name, Transform parent, Vector3 center, Vector3 size, Mesh mesh, Material cream, Material wood)
        {
            Panel(name + " rim", parent, center, size, mesh, wood);
            Panel(name + " inset", parent, center + Vector3.up * (size.y * .5f),
                new Vector3(size.x - .16f, .025f, size.z - .16f), mesh, cream);
        }

        private static Mesh RoundedPanel()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int steps = 8;
            const float radius = .075f;
            for (int corner = 0; corner < 4; corner++)
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (corner * 90 + step * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 center = new Vector2(corner == 0 || corner == 3 ? .5f - radius : -.5f + radius,
                        corner < 2 ? .5f - radius : -.5f + radius);
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    vertices.Add(new Vector3(point.x, -.5f, point.y));
                    vertices.Add(new Vector3(point.x, .5f, point.y));
                }
            int count = vertices.Count / 2;
            for (int i = 0; i < count; i++)
            {
                int a = i * 2, b = ((i + 1) % count) * 2;
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
                if (i > 0 && i < count - 1) triangles.AddRange(new[] { 1, b + 1, a + 1, 0, a, b });
            }
            var mesh = new Mesh { name = "Rounded factory panel" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
