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
        private bool _showReferences;
        public override void OnInspectorGUI()
        {
            MacaronLevelPreview.Watch((MacaronLevel)target);
            bool preview = EditorGUILayout.Toggle("Auto scene / game preview", MacaronLevelPreview.Enabled);
            if (preview != MacaronLevelPreview.Enabled)
            {
                MacaronLevelPreview.Enabled = preview;
                if (!preview) MacaronLevelPreview.Clear(); else MacaronLevelPreview.Refresh();
            }
            var selected = (MacaronLevel)target;
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            var asset = PrefabUtility.GetCorrespondingObjectFromSource(selected) ?? selected;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && selected.gameObject.scene == prefabStage.scene)
                asset = AssetDatabase.LoadAssetAtPath<MacaronLevel>(prefabStage.assetPath);
            int stage = factory != null ? System.Array.IndexOf(factory.levels, asset) + 1 : 1;
            stage = Mathf.Max(1, stage);
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level cake configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("conveyorShape"));
            var laneCount = serializedObject.FindProperty("conveyorLaneCount");
            laneCount.intValue = EditorGUILayout.IntPopup("Macarons per conveyor row", laneCount.intValue,
                new[] { "2", "4" }, new[] { 2, 4 });
            if (serializedObject.ApplyModifiedProperties()) MacaronLevelPreview.Refresh();
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("conveyorMacaronScale"), new GUIContent("Macaron Scale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("conveyorWidth"), new GUIContent("Conveyor Width"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("laneSpacing"), new GUIContent("Lane Spacing"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rowSpacing"), new GUIContent("Row Spacing"));
            var custom = serializedObject.FindProperty("overrideCakeSupply");
            var colors = serializedObject.FindProperty("colorCount");
            var cakes = serializedObject.FindProperty("cakeCount");
            bool wasCustom = custom.boolValue;
            EditorGUILayout.PropertyField(custom, new GUIContent("Override Cake Supply"));
            if (custom.boolValue && !wasCustom)
            {
                var defaults = selected.BuildConveyorSupply(selected.ResolveConveyorStage(stage), out _, out _);
                colors.intValue = defaults.Select(group => group.Color).Distinct().Count();
                cakes.intValue = defaults.Sum(group => group.RowCount * selected.conveyorLaneCount);
            }
            if (custom.boolValue)
            {
                EditorGUILayout.PropertyField(colors, new GUIContent("Color Count"));
                EditorGUILayout.PropertyField(cakes, new GUIContent("Cake Count"));
            }
            EditorGUILayout.HelpBox("Override enables per-level counts. Cake Count must be a multiple of 4 and at least Color Count × 4. Runtime trays use the same supply. Automatic shape counts below refer to this level's first stage.", MessageType.Info);
            if (serializedObject.ApplyModifiedProperties()) MacaronLevelPreview.Refresh();
            try
            {
                var supply = selected.BuildConveyorSupply(selected.ResolveConveyorStage(stage), out _, out _);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Effective Color Count", supply.Select(group => group.Color).Distinct().Count());
                    EditorGUILayout.IntField("Effective Cake Count", supply.Sum(group => group.RowCount * selected.conveyorLaneCount));
                }
            }
            catch (System.Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layout and camera", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trayLayoutStyle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shuffleTrayColors"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trayArrangementSeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("repeatedTierOffset"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mystery trays", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mysteryTrayRatio"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mysteryTrayColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mysteryRevealDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mysteryRevealScale"));
            EditorGUILayout.HelpBox("Ratio applies to all trays, capped by initially covered trays. Color shuffle preserves capacity per color. Reveal waits until the covering tray clears its original footprint. Preview uses authored tray counts; difficulty settings apply at runtime.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("waitingSlotScreenGap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraTilt"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraFieldOfView"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraPadding"));
            _showReferences = EditorGUILayout.Foldout(_showReferences, "Scene references", true);
            if (_showReferences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("trayRoot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("waitingSlots"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("counterAnchor"));
                EditorGUI.indentLevel--;
            }
            if (serializedObject.ApplyModifiedProperties()) MacaronLevelPreview.Refresh();
            EditorGUILayout.HelpBox("Tray Layout Style applies to preview and gameplay. Preview uses authored tray counts; runtime rebuilds colors and quantities from the cake configuration.", MessageType.Info);
            if (!string.IsNullOrEmpty(MacaronLevelPreview.Error)) EditorGUILayout.HelpBox(MacaronLevelPreview.Error, MessageType.Warning);
            if (GUILayout.Button("Refresh scene / game preview")) MacaronLevelPreview.Refresh();
            if (GUILayout.Button("Focus preview in Scene")) MacaronLevelPreview.Focus();
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
            var welcome = AssetDatabase.LoadAssetAtPath<MacaronLevel>(Folder + "/Level_01_Welcome.prefab");
            factory.levels = welcome != null ? new[] { welcome, compact, stacked } : new[] { compact, stacked };
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            EditorSceneManager.SaveScene(factory.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = compact;
        }

        [MenuItem("Tools/Macaron Factory/Create First Level")]
        public static void CreateFirstLevel()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before authoring levels.");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new System.InvalidOperationException("Open MacaronFactory scene first.");
            string path = Folder + "/Level_01_Welcome.prefab";
            var first = AssetDatabase.LoadAssetAtPath<MacaronLevel>(path);
            if (first == null)
            {
                var root = PrefabUtility.LoadPrefabContents(Folder + "/Level_01_Packed.prefab");
                try
                {
                    root.name = "Level_01_Welcome";
                    var level = root.GetComponent<MacaronLevel>();
                    foreach (Transform child in level.trayRoot.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    level.columns = 2;
                    level.useCustomMacaronOrder = true;
                    level.instruction = "Choose a tray matching the front macarons.";
                    var colors = new[] { BlockColorType.Red, BlockColorType.Green, BlockColorType.Blue,
                        BlockColorType.Red, BlockColorType.Green, BlockColorType.Blue };
                    level.macaronOrder = colors.Select(color => new MacaronLevel.MacaronBatch { color = color, count = 4 }).ToArray();
                    var small = AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_1x4.prefab");
                    for (int i = 0; i < colors.Length; i++)
                    {
                        Tray(level, small, (i % 3 - 1) * 1.78f, i < 3 ? -1.35f : -2.25f, 0, 0, colors[i], false, factory);
                        var tray = level.trayRoot.GetChild(i);
                        tray.localScale = Vector3.one * 1.15f;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(tray);
                        foreach (var renderer in tray.GetComponentsInChildren<Renderer>(true))
                            if (renderer.name.StartsWith("Macaron_Row"))
                            {
                                renderer.sharedMaterial = Material("Tray lining", new Color(.85f,.8f,.7f));
                                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                            }
                    }
                    root.transform.Find("Tray board rim").localPosition = new Vector3(0,-.24f,-1.85f);
                    root.transform.Find("Tray board rim").localScale = new Vector3(6.2f,.3f,2.65f);
                    root.transform.Find("Tray board inset").localPosition = new Vector3(0,-.09f,-1.85f);
                    root.transform.Find("Tray board inset").localScale = new Vector3(6.04f,.025f,2.49f);
                    var builder = level.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>();
                    float diameter = factory.macaronPrefabs.Max(prefab => {
                        var bounds = prefab.GetComponent<Renderer>().localBounds;
                        return 2 * level.conveyorMacaronScale * Mathf.Max(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                            Mathf.Abs(bounds.center.z) + bounds.extents.z);
                    });
                    level.laneSpacing = Mathf.Max(level.laneSpacing, diameter + .015f);
                    builder.beltHalfWidth = level.laneSpacing * .5f + Mathf.Max(.16f, diameter * .5f + .02f);
                    builder.BuildMesh();
                    AssetDatabase.CreateAsset(builder.GetComponent<MeshFilter>().sharedMesh, Folder + "/Level_01_Welcome_Track.asset");
                    first = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<MacaronLevel>();
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Undo.RecordObject(factory, "Add welcome level");
            factory.levels = new[] { first }.Concat(factory.levels.Where(level => level != null && level != first)).ToArray();
            factory.stageOverride = 1;
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            EditorSceneManager.SaveScene(factory.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = first;
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
                ApplyVisualLayout(level, stacked);
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
            Color tint = factory.TrayColor(color);
            var material = factory.ColorRegistry != null ? factory.ColorRegistry.GetTrayMaterial(color) : null;
            if (material == null) material = Material("Tray_" + color, tint);
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

        public static void ApplyVisualLayout(MacaronLevel level, bool stacked)
        {
            // Coordinates are authored for the existing 1x4 / 2x4 variants at 1.15 scale.
            Vector3[] poses = stacked ? new[] {
                new Vector3(-1.78f,-1.5f,0), new Vector3(0,-1.5f,0), new Vector3(1.78f,-1.5f,0),
                new Vector3(-2.15f,-3.05f,90), new Vector3(0,-2.72f,0), new Vector3(2.15f,-3.05f,90),
                new Vector3(-1.75f,-1.75f,90), new Vector3(0,-1.65f,15), new Vector3(1.75f,-1.75f,90),
                new Vector3(-.88f,-3.12f,0), new Vector3(1.2f,-3.42f,-12), new Vector3(0,-4.2f,0)
            } : new[] {
                new Vector3(-1.78f,-1.45f,0), new Vector3(0,-1.45f,0), new Vector3(1.78f,-1.45f,0),
                new Vector3(-2.18f,-3.02f,90), new Vector3(-.89f,-2.66f,0), new Vector3(.89f,-2.66f,0),
                new Vector3(2.18f,-3.02f,90), new Vector3(-1.78f,-4.32f,0), new Vector3(0,-4.32f,0), new Vector3(1.78f,-4.32f,0)
            };
            var trays = level.GetTrays();
            for (int i = 0; i < Mathf.Min(trays.Length, poses.Length); i++)
            {
                trays[i].transform.localScale = Vector3.one * 1.15f;
                trays[i].transform.localPosition = new Vector3(poses[i].x, trays[i].stackLayer * .48f, poses[i].y);
                trays[i].transform.localRotation = Quaternion.Euler(0, poses[i].z, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(trays[i].transform);
                foreach (var renderer in trays[i].GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Macaron_Row"))
                    {
                        renderer.sharedMaterial = Material("Tray lining", new Color(.85f,.8f,.7f));
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
            }
            var table = level.transform.Find("Tray board rim");
            table.localPosition = new Vector3(0,-.24f,-2.75f);
            table.localScale = new Vector3(6.2f,.3f,4.45f);
            var inset = level.transform.Find("Tray board inset");
            inset.localPosition = new Vector3(0,-.09f,-2.75f);
            inset.localScale = new Vector3(6.04f,.025f,4.29f);
            table = level.transform.Find("Conveyor board rim");
            table.localPosition = new Vector3(0,-.24f,3.025f);
            table.localScale = new Vector3(6.6f,.3f,5.25f);
            inset = level.transform.Find("Conveyor board inset");
            inset.localPosition = new Vector3(0,-.09f,3.025f);
            inset.localScale = new Vector3(6.44f,.025f,5.09f);
            level.transform.Find("Entrance hood").localPosition = new Vector3(2.45f,.3f,4.95f);
            level.counterAnchor.localPosition = new Vector3(1.7f,.16f,1.3f);
            level.transform.Find("Remaining badge").localPosition = new Vector3(1.7f,.07f,1.3f);
        }

        public static Spline CreateStarterConveyorPath(bool stacked)
        {
            Vector3[] points = {
                new(2.4f,0,4.95f), new(-1.4f,0,4.95f), new(-2.5f,0,3.85f),
                new(-1.4f,0,2.75f), new(-.85f,0,2.75f), new(0,0,1.9f), new(0,0,.85f)
            };
            Vector3[] directions = {
                Vector3.left, Vector3.left, Vector3.back, Vector3.right,
                Vector3.right, Vector3.back, Vector3.back
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
