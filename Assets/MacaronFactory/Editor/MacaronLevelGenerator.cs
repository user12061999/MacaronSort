using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace BlockShooter.Editor
{
    public enum ConveyorShape { RoundedRectangle = 0, Triangle = 15 }

    [Serializable]
    public sealed class ConveyorFeederSettings
    {
        [Tooltip("Connection along the loop: 0/1 is the collection point at the bottom.")]
        [Range(0, 1)] public float position = .25f;
        [Min(.3f)] public float length = 1.5f;
    }

    [FilePath("UserSettings/MacaronLevelGenerator.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MacaronGeneratorSettings : ScriptableSingleton<MacaronGeneratorSettings>
    {
        public string levelName = "Factory_Level";
        public int seed = 12345;
        [Range(1, 48)] public int trayCount = 18;
        [Range(1, 6)] public int colorCount = 6;
        [Range(0, 100)] public int largeTrayPercent = 60;
        [Range(1, 5)] public int tableColumns = 3;
        [Range(1, 3)] public int stackLayers = 1;
        public bool mixVerticalTrays;
        public bool mysteryUnderStacks;
        [Range(.5f, 1.5f)] public float trayScale = .98f;
        [Range(0, .3f)] public float trayGap = 0f;
        [Range(1, 5)] public int conveyorColumns = 2;
        public ConveyorShape conveyorShape = ConveyorShape.RoundedRectangle;
        [Tooltip("On: current video-style packing. Off: previous packing with rows aligned before collection.")]
        public bool independentLanePacking = true;
        [Tooltip("Radius of the conveyor centreline corners. Kept above belt half-width to avoid folding the inner edge.")]
        [Min(.1f)] public float cornerRadius = 1f;
        [Tooltip("Each entry creates one feeder. Empty = no feeders. Position is along the loop; Length controls the feeder length.")]
        public ConveyorFeederSettings[] feeders = { new ConveyorFeederSettings { position = .25f },
            new ConveyorFeederSettings { position = .75f } };
        [Range(.5f, 1.5f)] public float loopSpacingMultiplier = .9f;
        [Range(.5f, 1.5f)] public float feederSpacingMultiplier = .9f;
        [Tooltip("Off: supply follows top trays first. On: shuffles whole tray batches; may require more planning.")]
        public bool shuffleSupply;
        [Header("Bus Jam supply")]
        [Tooltip("Keep matching macarons in full-row color blocks on the conveyor.")]
        public bool clusterColors = true;
        [Range(6, 24)] public int colorClusterSize = 12;
        [Range(1, 10)] public int minColorRunRows = 1;
        [Range(1, 10)] public int maxColorRunRows = 10;
        [Tooltip("Maximum colors in each generated supply window. Keep it below the four starting tray slots.")]
        [Range(1, 3)] public int activeColorLimit = 3;
        [Tooltip("Assign tray colors in the same top-to-bottom, front-to-back order used by the conveyor supply.")]
        public bool arrangeTraysForSupply = true;
        public bool addToFactory = true;
        public bool selectAsStartingStage;
        public void Persist() => Save(true);
    }

    [InitializeOnLoad]
    public static class MacaronLevelGenerator
    {
        private static bool _expanded = true;
        private static string _message;
        private static MacaronLevel _draft;
        private static UnityEngine.SceneManagement.Scene _draftScene;

        static MacaronLevelGenerator()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearDraft;
            EditorApplication.quitting += ClearDraft;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) ClearDraft();
            };
        }

        private static void ClearDraft()
        {
            var meshes = _draft == null ? Array.Empty<Mesh>() : _draft.GetComponentsInChildren<MeshFilter>(true)
                .Select(f => f.sharedMesh).Where(m => m != null && !AssetDatabase.Contains(m)).Distinct().ToArray();
            MacaronLevelPreview.Clear();
            if (_draftScene.IsValid()) EditorSceneManager.ClosePreviewScene(_draftScene);
            foreach (var mesh in meshes) if (mesh != null) Object.DestroyImmediate(mesh);
            _draft = null;
        }
        private static readonly BlockColorType[] Colors = { BlockColorType.Red, BlockColorType.Green,
            BlockColorType.Blue, BlockColorType.Yellow, BlockColorType.Purple, BlockColorType.Orange };

        [MenuItem("Tools/Macaron Factory/Open Level Generator")]
        public static void Open()
        {
            _expanded = true;
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MacaronLevel>() : null;
            if (selected == null && factory != null && factory.levels.Length > 0)
                selected = factory.levels[Mathf.Max(0, factory.stageOverride - 1) % factory.levels.Length];
            if (selected == null) selected = AssetDatabase.LoadAssetAtPath<MacaronLevel>("Assets/MacaronFactory/Levels/Level_04_JunctionLoop.prefab");
            Selection.activeObject = selected;
            if (selected != null) EditorGUIUtility.PingObject(selected);
        }

        public static void Draw(MacaronLevel template)
        {
            _expanded = EditorGUILayout.Foldout(_expanded, "Generate new level", true);
            if (!_expanded) return;
            EditorGUILayout.HelpBox("Choose Rectangle or Triangle. Feeders list size sets the branch count; each branch has Position and Length. Generate previews a draft; Save writes it to a new prefab.", MessageType.Info);
            var settings = MacaronGeneratorSettings.instance;
            if (!Enum.IsDefined(typeof(ConveyorShape), settings.conveyorShape)) { settings.conveyorShape = ConveyorShape.RoundedRectangle; settings.Persist(); }
            var serialized = new SerializedObject(settings);
            serialized.Update();
            var property = serialized.GetIterator();
            if (property.NextVisible(true))
                do { if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true); } while (property.NextVisible(false));
            if (serialized.ApplyModifiedProperties()) settings.Persist();
            EditorGUILayout.LabelField("Cake count", $"{settings.trayCount * 4}–{settings.trayCount * 8} (exact total depends on tray sizes)");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Generate"))
                {
                    try
                    {
                        var result = Generate(template, settings);
                        _message = $"Draft {result.name}: {result.GetTrays().Length} trays, {result.BuildMacaronOrder().Count} cakes. Not saved yet.";
                        Selection.activeObject = result;
                        MacaronLevelPreview.Enabled = true;
                        MacaronLevelPreview.Watch(result);
                        MacaronLevelPreview.Refresh();
                    }
                    catch (Exception error) { _message = error.Message; }
                }
            using (new EditorGUI.DisabledScope(_draft == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Save"))
                {
                    try
                    {
                        var asset = SaveDraft(settings);
                        Selection.activeObject = asset;
                        MacaronLevelPreview.Watch(asset);
                        MacaronLevelPreview.Refresh();
                        _message = $"Saved {AssetDatabase.GetAssetPath(asset)}";
                    }
                    catch (Exception error) { _message = error.Message; }
                }
            EditorGUILayout.HelpBox("Generate creates a temporary draft for preview. Save writes that draft to a new prefab. After changing generator settings, Generate again to update the draft. Unsaved drafts are discarded on script reload, Play Mode or editor close.", MessageType.Info);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
        }

        public static MacaronLevel Generate(MacaronLevel template, MacaronGeneratorSettings config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before generating a level.");
            if (template == null || template.conveyorPath == null || template.trayRoot == null)
                throw new InvalidOperationException("Select a configured MacaronLevel template.");
            string name = (config.levelName ?? "").Trim();
            if (name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith("."))
                throw new InvalidOperationException("Enter a valid level file name without path separators.");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (factory == null) throw new InvalidOperationException("Open the MacaronFactory scene first.");
            if (config.trayCount < 1 || config.trayCount > 48 || config.tableColumns < 1 || config.tableColumns > 5 ||
                config.stackLayers < 1 || config.stackLayers > 3 || config.colorCount < 1 || config.colorCount > 6)
                throw new InvalidOperationException("Generator counts are outside the supported range.");
            if (!Enum.IsDefined(typeof(ConveyorShape), config.conveyorShape) || !float.IsFinite(config.cornerRadius) || config.cornerRadius <= 0)
                throw new InvalidOperationException("Choose Rectangle or Triangle.");
            if (config.feeders != null && config.feeders.Any(f => f == null || !float.IsFinite(f.position) ||
                !float.IsFinite(f.length) || f.length <= 0))
                throw new InvalidOperationException("Each feeder needs a finite position and a positive length.");
            var small = AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_1x4.prefab");
            var large = AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_2x4.prefab");
            if (small == null || large == null) throw new InvalidOperationException("Both tray prefabs are required.");
            GameObject root = null;
            bool generatedSuccessfully = false;
            var draftScene = EditorSceneManager.NewPreviewScene();
            try
            {
                root = Object.Instantiate(template.gameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, draftScene);
                root.name = name;
                root.hideFlags = HideFlags.None;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var level = root.GetComponent<MacaronLevel>();
                foreach (Transform child in level.trayRoot.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var random = new System.Random(config.seed);
                void SetTrayColor(MacaronTray tray, BlockColorType color)
                {
                    tray.levelColor = color;
                    tray.name = $"{tray.name.Split(' ')[0]} {color} {tray.gameObject.name.Split(' ')[^1]}";
                    var tint = AssetDatabase.LoadAssetAtPath<Material>($"Assets/MacaronFactory/Levels/Tray_{color}.mat");
                    foreach (var renderer in tray.tintRenderers)
                    {
                        var materials = renderer.sharedMaterials;
                        materials[0] = tint;
                        renderer.sharedMaterials = materials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tray);
                }
                int slots = Mathf.CeilToInt((float)config.trayCount / config.stackLayers);
                var trays = new List<MacaronTray>();
                for (int i = 0; i < config.trayCount; i++)
                {
                    var prefab = random.Next(100) < config.largeTrayPercent ? large : small;
                    var tray = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, level.trayRoot)).GetComponent<MacaronTray>();
                    tray.stackLayer = i / slots;
                    tray.mystery = config.mysteryUnderStacks && i + slots < config.trayCount;
                    tray.name = $"{i + 1:00} {prefab.name}";
                    tray.transform.localRotation = Quaternion.Euler(0, config.mixVerticalTrays && random.Next(2) == 1 ? 90 : 0, 0);
                    tray.transform.localScale = Vector3.one * Mathf.Clamp(config.trayScale, .5f, 1.5f);
                    SetTrayColor(tray, Colors[i % Mathf.Min(config.colorCount, config.trayCount)]);
                    trays.Add(tray);
                }
                var footprints = CompactTrays(level, trays, config.tableColumns, config.trayGap, config.trayScale, true);
                foreach (var tray in trays)
                    tray.mystery = config.mysteryUnderStacks && trays.Any(other => other.stackLayer > tray.stackLayer
                        && footprints[other].Overlaps(footprints[tray]));
                var order = trays.OrderByDescending(t => t.stackLayer).ToList();
                if (config.arrangeTraysForSupply)
                {
                    order = trays.OrderByDescending(t => t.stackLayer).ThenByDescending(t => t.transform.localPosition.z)
                        .ThenBy(t => t.transform.localPosition.x).ToList();
                    var colorCycle = Colors.Take(Mathf.Min(config.colorCount, config.trayCount)).ToList();
                    if (config.shuffleSupply)
                        for (int i = colorCycle.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (colorCycle[i], colorCycle[j]) = (colorCycle[j], colorCycle[i]); }
                    for (int i = 0; i < order.Count; i++) SetTrayColor(order[i], colorCycle[i % colorCycle.Count]);
                }
                else if (config.shuffleSupply)
                    for (int i = order.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
                level.useCustomMacaronOrder = true;
                level.macaronOrder = order.Select(t => new MacaronLevel.MacaronBatch { color = t.levelColor, count = t.pockets.Length }).ToArray();
                level.columns = Mathf.Clamp(config.conveyorColumns, 1, 5);
                level.clusterColors = config.clusterColors;
                level.colorClusterSize = config.colorClusterSize;
                level.minColorRunRows = config.minColorRunRows;
                level.maxColorRunRows = config.maxColorRunRows;
                level.activeColorLimit = config.activeColorLimit;
                level.loopSpacingMultiplier = config.loopSpacingMultiplier;
                level.independentLanePacking = config.independentLanePacking;
                level.feederSpacingMultiplier = config.feederSpacingMultiplier;
                float diameter = factory.conveyorMacaronScale * factory.macaronPrefabs.Max(p => {
                    var b = p.GetComponent<Renderer>().localBounds;
                    return 2 * Mathf.Max(Mathf.Abs(b.center.x) + b.extents.x, Mathf.Abs(b.center.z) + b.extents.z);
                });
                float halfWidth = Mathf.Max(level.laneSpacing, diameter + .015f) * (level.columns - 1) * .5f + Mathf.Max(.16f, diameter * .5f + .02f);
                level.conveyorPath.GetComponent<ConveyorTrackMeshBuilder>().beltHalfWidth = halfWidth;
                BuildConveyor(level, config, halfWidth);
                if (level.feederBranches.Length == 0) FitSupplyOnRing(level, diameter);
                foreach (var junction in level.feederBranches) { junction.Branch.beltHalfWidth = halfWidth; junction.SyncJunction(); }
                foreach (var track in root.GetComponentsInChildren<ConveyorTrackMeshBuilder>()) track.BuildMesh();
                level.ValidateLayout();
                ClearDraft();
                _draftScene = draftScene;
                _draft = level;
                generatedSuccessfully = true;
                config.Persist();
                return level;
            }
            finally
            {
                if (!generatedSuccessfully) EditorSceneManager.ClosePreviewScene(draftScene);
            }
        }

        public static void CompactTrays(MacaronLevel level)
        {
            var config = MacaronGeneratorSettings.instance;
            var trays = level.GetTrays().ToList();
            if (trays.Count == 0) throw new InvalidOperationException("This level has no trays to arrange.");
            var changed = trays.Select(tray => tray.transform).Concat(new[] { level.transform })
                .Concat(new[] { level.transform.Find("Tray board rim"), level.transform.Find("Tray board inset") }
                    .Where(board => board != null)).ToArray();
            Undo.RecordObjects(changed, "Compact trays");
            CompactTrays(level, trays, config.tableColumns, config.trayGap, config.trayScale, false);
            EditorUtility.SetDirty(level);
            PrefabUtility.RecordPrefabInstancePropertyModifications(level);
        }

        private static Dictionary<MacaronTray, Rect> CompactTrays(MacaronLevel level, List<MacaronTray> trays,
            int columns, float requestedGap, float trayScale, bool resetStackHeight)
        {
            float gap = Mathf.Clamp(requestedGap, 0, .3f);
            var footprints = new Dictionary<MacaronTray, Rect>();
            Vector2 Size(MacaronTray tray)
            {
                var size = Vector3.Scale(tray.GetComponent<BoxCollider>().size, tray.transform.localScale);
                bool vertical = Mathf.Abs(tray.transform.localEulerAngles.y - 90) < 1;
                return vertical ? new Vector2(size.z, size.x) : new Vector2(size.x, size.z);
            }
            int columnCount = Mathf.Clamp(columns, 1, 5);
            float tableWidth = trays.GroupBy(t => t.stackLayer).Max(layer =>
            {
                int rows = Mathf.CeilToInt(layer.Count() / (float)columnCount);
                return layer.Sum(tray => Size(tray).x + gap) / rows - gap;
            });
            tableWidth = Mathf.Max(tableWidth, trays.Max(tray => Size(tray).x));
            // ponytail: at most 48 trays; edge-candidate packing is quadratic per candidate.
            // For hundreds of trays replace this search with a free-rectangle packer.
            foreach (var layer in trays.GroupBy(t => t.stackLayer).OrderBy(g => g.Key))
            {
                var placed = new List<Rect>();
                foreach (var tray in layer.OrderByDescending(t => Size(t).x * Size(t).y))
                {
                    var size = Size(tray);
                    var xs = placed.Select(r => r.xMax + gap).Append(0f).Distinct().OrderBy(x => x);
                    var zs = placed.Select(r => r.yMax + gap).Append(0f).Distinct().OrderBy(z => z);
                    Rect? chosen = null;
                    foreach (float z in zs)
                    {
                        foreach (float x in xs)
                        {
                            var candidate = new Rect(x, z, size.x, size.y);
                            var padded = new Rect(x - gap * .99f, z - gap * .99f, size.x + gap * 1.98f, size.y + gap * 1.98f);
                            if (candidate.xMax <= tableWidth + .001f && !placed.Any(r => r.Overlaps(padded)))
                            {
                                chosen = candidate;
                                break;
                            }
                        }
                        if (chosen.HasValue) break;
                    }
                    if (!chosen.HasValue) throw new InvalidOperationException("Unable to place tray within the table width.");
                    placed.Add(chosen.Value);
                    footprints[tray] = chosen.Value;
                }
            }
            float packedWidth = footprints.Values.Max(r => r.xMax);
            float packedDepth = footprints.Values.Max(r => r.yMax);
            float boardDepth = packedDepth + .35f;
            foreach (var tray in trays)
            {
                var rect = footprints[tray];
                var box = tray.GetComponent<BoxCollider>();
                var offset = tray.transform.localRotation * Vector3.Scale(box.center, tray.transform.localScale);
                float y = resetStackHeight ? tray.stackLayer * .48f * trayScale : tray.transform.localPosition.y;
                tray.transform.localPosition = new Vector3(rect.center.x - packedWidth * .5f - offset.x,
                    y, -.775f - rect.center.y - offset.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tray.transform);
            }
            float centerZ = -.6f - boardDepth * .5f;
            foreach (string boardName in new[] { "Tray board rim", "Tray board inset" })
            {
                var board = level.transform.Find(boardName);
                if (board == null) continue;
                board.localPosition = new Vector3(0, board.localPosition.y, centerZ);
                float inset = boardName.EndsWith("inset") ? .2f : 0;
                board.localScale = new Vector3(Mathf.Max(6.2f, packedWidth + .35f) - inset, board.localScale.y, boardDepth - inset);
                PrefabUtility.RecordPrefabInstancePropertyModifications(board);
            }
            return footprints;
        }

        private static MacaronLevel SaveDraft(MacaronGeneratorSettings config)
        {
            if (_draft == null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate a draft in Edit Mode before saving.");
            _draft.ValidateLayout();
            if (string.IsNullOrWhiteSpace(_draft.name) || _draft.name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || _draft.name.EndsWith("."))
                throw new InvalidOperationException("Give the draft a valid level file name before saving.");
            var factory = Object.FindFirstObjectByType<MacaronFactory>();
            if (config.addToFactory && factory == null)
                throw new InvalidOperationException("Open the MacaronFactory scene before adding this level.");
            const string parent = "Assets/MacaronFactory/Levels/Generated";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets/MacaronFactory/Levels", "Generated");
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + _draft.name);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder))))
                throw new InvalidOperationException("Could not create the output folder.");
            var root = Object.Instantiate(_draft.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, _draftScene);
            root.name = _draft.name;
            bool saved = false;
            try
            {
                int meshIndex = 0;
                foreach (var track in root.GetComponentsInChildren<ConveyorTrackMeshBuilder>())
                {
                    var mesh = Object.Instantiate(track.GetComponent<MeshFilter>().sharedMesh);
                    AssetDatabase.CreateAsset(mesh, folder + $"/Track_{meshIndex++}.asset");
                    track.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var collider = track.GetComponent<MeshCollider>();
                    if (collider != null) collider.sharedMesh = mesh;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + root.name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Could not save the level prefab. Draft retained.");
                var asset = prefab.GetComponent<MacaronLevel>();
                saved = true;
                if (config.addToFactory)
                {
                    Undo.RecordObject(factory, "Add generated level");
                    factory.levels = factory.levels.Concat(new[] { asset }).ToArray();
                    if (config.selectAsStartingStage) factory.stageOverride = factory.levels.Length;
                    EditorUtility.SetDirty(factory);
                    EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
                }
                AssetDatabase.SaveAssets();
                ClearDraft();
                if (config.addToFactory && !EditorSceneManager.SaveScene(factory.gameObject.scene))
                    Debug.LogWarning("Level prefab saved, but the scene could not be saved. Save the scene manually.");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (!saved) AssetDatabase.DeleteAsset(folder);
            }
        }
        private static void BuildConveyor(MacaronLevel level, MacaronGeneratorSettings config, float halfWidth)
        {
            bool rectangle = config.conveyorShape == ConveyorShape.RoundedRectangle;
            // Leave a visible inner curve even when four/five lanes widen the belt.
            float radius = Mathf.Max(halfWidth + (rectangle ? .5f : .1f), config.cornerRadius);
            float width = rectangle ? Mathf.Max(1.85f, radius + .55f) : Mathf.Max(1.85f, radius * 2);
            float height = rectangle ? Mathf.Max(3.5f, radius * 2 + .8f) : Mathf.Max(3.5f, radius * 3.5f);
            float centerX = level.waitingSlots.Average(t => level.transform.InverseTransformPoint(t.position).x);
            float bottom = level.waitingSlots.Max(t => level.transform.InverseTransformPoint(t.position).z) + halfWidth + .9f;
            float top = bottom + height;
            var path = level.conveyorPath;
            path.transform.localPosition = Vector3.zero;
            path.transform.localRotation = Quaternion.identity;
            path.transform.localScale = Vector3.one;
            path.Spline = MacaronConveyorShapes.Create(config.conveyorShape, width, height, radius, centerX, bottom);
            var track = path.GetComponent<ConveyorTrackMeshBuilder>();
            level.conveyorTrack = track;
            track.openings.Clear();
            track.sweepFrom = 0; track.sweepTo = 1; track.openZoneEnabled = false;
            float collectionWidth = Mathf.Max(.8f, halfWidth * 2);
            track.openings.Add(new ConveyorOpening { label = "Collection gate", side = ConveyorSide.Left,
                measure = ConveyorMeasure.Meters, start = -collectionWidth * .5f, length = collectionWidth, lipHeight = 0 });
            if (level.collectionGate != null) Object.DestroyImmediate(level.collectionGate.gameObject);
            level.collectionGate = null;
            level.gateMounts = Array.Empty<MacaronLevel.GateMount>();
            level.bindWaitingSlotsToGates = false;
            var hood = level.transform.Find("Entrance hood");
            if (hood != null) Object.DestroyImmediate(hood.gameObject);
            foreach (var old in level.feederBranches)
                if (old != null && old.gameObject != path.gameObject) Object.DestroyImmediate(old.gameObject);
            var feederConfig = config.feeders ?? Array.Empty<ConveyorFeederSettings>();
            level.feederBranches = new ConveyorJunction[feederConfig.Length];
            var bounds = new Bounds((Vector3)path.Spline[0].Position, Vector3.zero);
            for (int i = 0; i <= 512; i++)
            {
                path.Spline.Evaluate(i / 512f, out var p, out _, out _);
                bounds.Encapsulate((Vector3)p);
            }
            for (int i = 0; i < feederConfig.Length; i++)
            {
                var entry = feederConfig[i] ?? throw new InvalidOperationException("Assign every feeder entry.");
                float position = Mathf.Repeat(entry.position, 1);
                path.Spline.Evaluate(position, out var p, out var tangent, out _);
                var tip = (Vector3)p;
                var direction = -Vector3.Cross(Vector3.up, (Vector3)tangent).normalized;
                float length = Mathf.Max(halfWidth + .6f, entry.length);
                var start = tip + direction * length;
                var go = new GameObject($"Feeder {i + 1:00}");
                go.transform.SetParent(level.transform, false);
                var spline = go.AddComponent<SplineContainer>();
                spline.Spline = MacaronLoopLevelAuthoring.MakeSpline(new[] { start, tip },
                    new[] { -direction, -direction }, false);
                var branch = go.AddComponent<ConveyorTrackMeshBuilder>();
                branch.beltHalfWidth = halfWidth; branch.railHeight = track.railHeight;
                branch.wallAboveBelt = track.wallAboveBelt;
                go.GetComponent<MeshRenderer>().sharedMaterials = track.GetComponent<MeshRenderer>().sharedMaterials;
                var junction = go.AddComponent<ConveyorJunction>();
                junction.mainTrack = track; junction.joinAt = ConveyorJunction.BranchEnd.End;
                junction.branchClearance = 0;
                level.feederBranches[i] = junction;
                bounds.Encapsulate(start);
            }
            foreach (string name in new[] { "Conveyor board rim", "Conveyor board inset" })
            {
                var board = level.transform.Find(name);
                if (board == null) continue;
                float padding = name.EndsWith("inset") ? .25f : .45f;
                board.localPosition = new Vector3(bounds.center.x, board.localPosition.y, bounds.center.z);
                board.localScale = new Vector3(bounds.size.x + 2 * halfWidth + padding,
                    board.localScale.y, bounds.size.z + 2 * halfWidth + padding);
            }
            top = bounds.max.z;
            level.counterAnchor.localPosition = new Vector3(centerX, .05f, (bottom + top) * .5f);
            level.MoveRemainingBadgeToTraySide();
        }

        private static void FitSupplyOnRing(MacaronLevel level, float diameter)
        {
            int rows = Mathf.CeilToInt((float)level.BuildMacaronOrder().Count / level.columns);
            var spline = level.conveyorPath.Spline;
            var pivot = (Vector3)spline[0].Position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int capacity = MacaronLoopFlow.RowCapacity(level, diameter);
                if (capacity >= rows) return;
                float factor = Mathf.Max(1.05f, (float)rows / capacity * 1.05f);
                for (int i = 0; i < spline.Count; i++)
                {
                    var knot = spline[i];
                    knot.Position = (Unity.Mathematics.float3)(pivot + ((Vector3)knot.Position - pivot) * factor);
                    knot.TangentIn *= factor; knot.TangentOut *= factor;
                    spline[i] = knot;
                }
                foreach (string name in new[] { "Conveyor board rim", "Conveyor board inset", "Remaining badge", "Cakes remaining anchor" })
                {
                    var part = level.transform.Find(name);
                    if (part == null) continue;
                    var p = part.localPosition;
                    part.localPosition = new Vector3(pivot.x + (p.x - pivot.x) * factor, p.y, pivot.z + (p.z - pivot.z) * factor);
                    if (name.StartsWith("Conveyor board"))
                        part.localScale = Vector3.Scale(part.localScale, new Vector3(factor, 1, factor));
                }
            }
            throw new InvalidOperationException("Unable to fit this supply on a no-feeder loop. Reduce tray count.");
        }
    }
}
