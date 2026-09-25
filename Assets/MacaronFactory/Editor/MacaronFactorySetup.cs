using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockShooter.Editor
{
    public static class MacaronFactorySetup
    {
        public const string ScenePath = "Assets/MacaronFactory/MacaronFactory.unity";

        [MenuItem("Tools/Macaron Factory/Create Playable Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before creating the factory scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 7.3f;
            camera.transform.position = new Vector3(0, 16, -7.3f);
            camera.transform.rotation = Quaternion.Euler(65, 0, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.96f, .85f, .8f);
            var light = new GameObject("Factory skylight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientLight = new Color(.75f, .7f, .68f);
            var root = new GameObject("Macaron Factory");
            var manager = root.AddComponent<GameManager>();
            manager.enabled = false;
            manager.config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Project Files/Data/GameConfig.asset");
            var factory = root.AddComponent<MacaronFactory>();
            factory.conveyorSource = AssetDatabase.LoadAssetAtPath<LevelRoot>("Assets/Project Files/Data/Levels/Level_001.prefab");
            ConfigureProps(factory);
            EditorSceneManager.SaveScene(scene, ScenePath);
            MacaronLevelAuthoring.CreateStarterLevels();
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            PlayerSettings.productName = "Macaron Factory";
            Selection.activeGameObject = root;
            Debug.Log("Macaron Factory scene created. Open it and press Play.");
        }

        [MenuItem("Tools/Macaron Factory/Use Macaron Props Prefabs")]
        public static void UseProps()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            Check(factory != null, "Open the MacaronFactory scene first");
            Undo.RecordObject(factory, "Bind Macaron Props");
            ConfigureProps(factory);
            EditorUtility.SetDirty(factory);
            EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            EditorSceneManager.SaveScene(factory.gameObject.scene);
        }

        private static void ConfigureProps(MacaronFactory factory)
        {
            CreateTrayPrefab("2x4");
            CreateTrayPrefab("1x4");
            int[] colors = { 2, 4, 3, 6, 7, 1 };
            factory.macaronPrefabs = colors.Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Macaron_Props/Prefabs/Macaron_{i}.prefab")).ToArray();
            factory.AutoAssignSprites();
            ConfigureFeedback(factory.feedback);
        }

        public static void ConfigureFeedback(MacaronFeedbackPlayer player)
        {
            if (player == null || player.config == null) return;
            var config = player.config;
            var clickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/clickEffect.prefab");
            var fillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Epic Toon FX/Prefabs/Combat/Explosions (Text)/FillComplete.prefab");
            var smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX (legacy)/CFX Prefabs/Smoke/CFX_Sole_Smoke.prefab");

            foreach (var cue in config.cues)
            {
                if (cue.trigger == MacaronFeedbackEvent.SelectTray || cue.trigger == MacaronFeedbackEvent.InvalidTray)
                {
                    if (clickPrefab != null) { cue.effectPrefab = clickPrefab; cue.effectScale = 0.35f; cue.cooldown = 0.05f; cue.poolSize = 4; }
                }
                else if (cue.trigger == MacaronFeedbackEvent.CakeLanded)
                {
                    if (smokePrefab != null) { cue.effectPrefab = smokePrefab; cue.effectScale = 0.45f; cue.cooldown = 0.03f; cue.poolSize = 6; cue.offset = new Vector3(0, 0.05f, 0); }
                }
                else if (cue.trigger == MacaronFeedbackEvent.TrayPacked)
                {
                    if (fillPrefab != null) { cue.effectPrefab = fillPrefab; cue.effectScale = 0.55f; cue.offset = new Vector3(0, 0.35f, 0); }
                }
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static MacaronTray CreateTrayPrefab(string size)
        {
            const string folder = "Assets/MacaronFactory/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/MacaronFactory", "Prefabs");
            string path = $"{folder}/Tray_{size}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<MacaronTray>(path);
            if (existing != null) return existing;
            var layout = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Macaron_Props/Prefabs/Packaged_Macarons/Macaron_{size}_Set.prefab");
            var box = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Macaron_Props/Prefabs/Macaron_Box_{size}.prefab");
            Check(layout != null && box != null, "Macaron Props box and packed reference must be imported");
            var root = new GameObject($"Tray_{size}");
            try
            {
                var tray = root.AddComponent<MacaronTray>();
                var model = new GameObject("Authored layout").transform;
                model.SetParent(root.transform, false);
                model.localRotation = Quaternion.Euler(0, 90, 0);
                model.localScale = Vector3.one * 1.4f;
                var boxInstance = (GameObject)PrefabUtility.InstantiatePrefab(box, model);
                foreach (var collider in boxInstance.GetComponentsInChildren<Collider>()) collider.enabled = false;
                tray.lid = boxInstance.transform.Find($"Macaron_Box_{size}_");
                Check(tray.lid != null, "Box lid must be present");
                tray.tintRenderers = new[] { boxInstance.GetComponent<Renderer>(), tray.lid.GetComponent<Renderer>() };
                var samples = layout.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.parent != null && t.parent.name.StartsWith("Set_")).ToArray();
                // The supplied packed prefabs are the calibration reference; never estimate a grid.
                tray.pockets = samples.Select((sample, index) => {
                    var pocket = new GameObject($"Pocket_{index + 1:00}").transform;
                    pocket.SetParent(model, false);
                    pocket.localPosition = layout.transform.InverseTransformPoint(sample.position);
                    pocket.localRotation = Quaternion.Inverse(layout.transform.rotation) * sample.rotation;
                    pocket.localScale = sample.lossyScale;
                    return pocket;
                }).ToArray();
                Check(tray.pockets.Length == (size == "2x4" ? 8 : 4), "Reference must contain the correct number of samples");
                var bounds = boxInstance.GetComponent<Renderer>().bounds;
                foreach (var renderer in boxInstance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                var hit = root.AddComponent<BoxCollider>();
                hit.center = bounds.center;
                hit.size = bounds.size;
                tray.lid.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<MacaronTray>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/Macaron Factory/Run Checks")]
        public static void RunChecks()
        {
            foreach (MacaronLevel.TrayLayoutStyle style in System.Enum.GetValues(typeof(MacaronLevel.TrayLayoutStyle)))
            foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 10, 16, 32 })
                for (int layer = 0; layer < 3; layer++)
                {
                    var sizes = Enumerable.Range(0, count).Select(i => new Vector2(1.48f, i % 3 == 0 ? .64f : 1f)).ToArray();
                    var packed = MacaronLevel.PackTrayLayer(sizes, layer, style);
                    Check(packed.SequenceEqual(MacaronLevel.PackTrayLayer(sizes, layer, style)), "Tray packing must be deterministic");
                    for (int i = 0; i < count; i++)
                    {
                        Check(Mathf.Abs(packed[i].width * packed[i].height - sizes[i].x * sizes[i].y) < .001f,
                            "Packing must preserve each tray footprint");
                        for (int j = 0; j < i; j++)
                            Check(!packed[i].Overlaps(packed[j]), "Trays on the same layer must not overlap");
                    }
                    if (count > 0)
                        Check(Mathf.Abs(packed.Min(p => p.xMin) + packed.Max(p => p.xMax)) < .001f &&
                            Mathf.Abs(packed.Min(p => p.yMin) + packed.Max(p => p.yMax)) < .001f,
                            "Every style must center its actual bounds so board fitting keeps all edges inside");
                    if (count == 16 && style == MacaronLevel.TrayLayoutStyle.Rectangle)
                        Check(packed.Select(p => Mathf.RoundToInt(p.center.y * 100)).Distinct().Count() == 4 &&
                            packed.Select(p => Mathf.RoundToInt(p.center.x * 100)).Distinct().Count() == 4, "Sixteen trays must form a regular 4x4 rectangle");
                    if (count >= 4 && style == MacaronLevel.TrayLayoutStyle.Pyramid)
                    {
                        var rows = packed.GroupBy(p => Mathf.RoundToInt(p.center.y * 100)).OrderByDescending(row => row.Key)
                            .Select(row => row.Count()).ToArray();
                        Check(rows.SequenceEqual(rows.Select((_, i) => i == rows.Length - 1 ? count - i * i : i * 2 + 1)),
                            "Pyramid rows must grow into a solid centered peak");
                    }
                    if (count >= 10 && style == MacaronLevel.TrayLayoutStyle.Compact)
                        Check(packed.Any(p => p.width > p.height) && packed.Any(p => p.height > p.width),
                            "The pile must mix horizontal and vertical trays");
                }
            var cameraObject = new GameObject("Tray clearance check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var supplyLevel = cameraObject.AddComponent<MacaronLevel>();
                supplyLevel.overrideCakeSupply = true;
                supplyLevel.colorCount = 9;
                supplyLevel.cakeCount = 100;
                var configured = supplyLevel.BuildConveyorSupply(1, out var mainSupply, out var branchSupply);
                Check(configured.Sum(group => group.RowCount * 4) == 100 &&
                    configured.Select(group => group.Color).Distinct().Count() == 9,
                    "Custom supply must preserve exact cake and color counts including remainder rows");
                var fed = SodaConveyor.StageLayout.FeedAllFromBranches(0, mainSupply, branchSupply);
                Check(fed.SelectMany(branch => branch.Groups).Sum(group => group.RowCount * 4) == 100,
                    "Feeding custom supply must not append the source stage's original cakes");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 10, -10), Quaternion.Euler(45, 0, 0));
                camera.fieldOfView = 35;
                var slots = new[] { new Bounds(Vector3.zero, Vector3.one) };
                var pile = new Bounds(new Vector3(0, 3, -1), new Vector3(2, 3, 2));
                float shift = MacaronCameraFrame.TrayClearanceShift(camera, new[] { pile }, slots, Vector3.back, .015f);
                Check(float.IsFinite(shift) && shift > 0, "A tall foreground pile must move below the waiting slots");
                pile.center += Vector3.back * (shift + .001f);
                Check(MacaronCameraFrame.TrayClearanceShift(camera, new[] { pile }, slots, Vector3.back, .015f) < .0001f,
                    "Projected tray bounds must clear the waiting slots after shifting");
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
            for (int preset = 0; preset < 10; preset++)
            {
                var main = SodaConveyor.StageLayout.MainGroups(preset);
                var original = SodaConveyor.StageLayout.Branches(preset);
                var feeders = SodaConveyor.StageLayout.FeedAllFromBranches(preset, main, original);
                Check(feeders.Length > 0, "Every stage needs an inlet when the loop starts empty");
                var before = main.Concat(original.SelectMany(branch => branch.Groups)).GroupBy(group => group.Color)
                    .ToDictionary(group => group.Key, group => group.Sum(batch => batch.RowCount));
                var after = feeders.SelectMany(branch => branch.Groups).GroupBy(group => group.Color)
                    .ToDictionary(group => group.Key, group => group.Sum(batch => batch.RowCount));
                Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out int rows) && rows == pair.Value),
                    "Moving the initial supply to feeders must preserve every color's row count");
            }
            Check(MacaronLoopFlow.IsInPickupWindow(.8f, 1, 0, 10), "All rows inside the gate can be picked up, not just the nearest row");
            Check(MacaronLoopFlow.IsInPickupWindow(9.8f, 1, .3f, 10), "A cake crossing the gate between frames must still be picked up");
            Check(!MacaronLoopFlow.IsInPickupWindow(9.8f, 1, .1f, 10), "A cake that passed the gate earlier must wait for the next lap");
            Check(!MacaronLoopFlow.IsInPickupWindow(2, 1, .3f, 10), "Cakes outside the gate must stay on the conveyor");
            Check(!MacaronLoopFlow.IsInPickupWindow(0, 0, .3f, 10), "A disabled pickup zone must not collect");
            var rect = new Rect(0, 0, 2, 1);
            Check(MacaronTray.Blocks(new Rect(1.9f, .9f, 2, 1), 1, rect, 0), "Partial corner overlap blocks");
            Check(!MacaronTray.Blocks(rect, 0, rect, 0), "Same layer cannot block");
            Check(!MacaronTray.Blocks(new Rect(2, 0, 2, 1), 1, rect, 0), "Touching edges do not block");
            Check(!MacaronTray.Blocks(rect, 0, rect, 1), "Lower tray cannot block upper tray");

            Check(ConveyorController.IsInExitZone(.9f, 10, 1, .5f), "The final conveyor section can fill trays");
            Check(!ConveyorController.IsInExitZone(.84f, 10, 1, .5f), "Only the final conveyor section can fill trays");
            Check(!ConveyorController.IsInExitZone(.96f, 10, 1, .5f), "Rows must not pass the configured stop point");
            Check(!ConveyorController.IsInExitZone(1, 0, 1), "An uninitialized conveyor cannot fill trays");
            Check(Mathf.Approximately(ConveyorController.DistanceToExit(.9f, 10, .5f), .5f), "Leading row can move to the configured stop point");
            Check(Mathf.Approximately(ConveyorController.DistanceToExit(.93f, 10, .5f), .2f), "Approaching row stops before t=1 without wrapping");
            Check(ConveyorController.DistanceToExit(.95f, 10, .5f) == 0, "A row at the configured stop point must stop");
            Check(ConveyorController.DistanceToExit(.950001f, 10, .5f) == 0, "Float drift must not release the conveyor stop");
            float limitedStep = Mathf.Min(2, ConveyorController.DistanceToExit(.9f, 10, .5f));
            Check(Mathf.Abs(.9f + limitedStep / 10 - .95f) < .0001f, "Fast movement must stop 0.5 units before the endpoint");

            if (EditorApplication.isPlaying)
            {
                var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
                Check(factory != null, "Open the MacaronFactory scene");
                Check(factory.Slots.Count == 6 && factory.OpenSlots >= 4 && factory.OpenSlots <= 6, "Six slots, four initially open");
                Check(factory.Trays.Sum(t => t.Capacity) >= factory.Remaining, "Supply cannot exceed tray capacity");
                Check(factory.Trays.Select(t => t.Capacity).Distinct().Count() >= 2, "Factory scene must use multiple tray variants");
                foreach (var tray in factory.Trays)
                {
                    Check(tray.Capacity >= 4 && tray.Capacity <= 8, "Capacity must be 4-8");
                    Check(tray.Filled + tray.Reserved <= tray.Capacity, "Reservations must not overfill");
                    if (tray.OnTable && !tray.Accessible)
                        Check(!factory.TrySelect(tray), "Blocked trays must reject input");
                    if (tray.Hidden)
                        Check(tray.Label.text == "?", "Hidden tray must display a question mark");
                }
                Check(factory.Conveyor != null && factory.Conveyor.SplineWorldLength > 0, "Source spline must be initialized");
                Check(factory.Rows.All(row => row.Length > 0 && row.Length <= factory.maxRowWidth), "Live source rows must fit the level conveyor width");
                Check(factory.PickupBlocks.All(block => block.Phase == ConveyorItemPhase.OnLoop && !block.IsDestroyed &&
                    factory.Conveyor.IsInExitWindow(block.PathT)), "Only live loop items inside the pickup window can fill trays");

            }
            Debug.Log("MACARON CHECKS PASSED");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Tools/Macaron Factory/Run Playthrough Check (Play Mode)")]
        public static void RunPlaythroughCheck()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in a fresh factory scene first.");
            var factory = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            Check(factory != null && factory.Trays.All(t => t.OnTable), "Check requires a fresh board");
            factory.StartCoroutine(Playthrough(factory));
        }

        private static IEnumerator Playthrough(MacaronFactory factory)
        {
            bool hadCoins = PlayerPrefs.HasKey("Coins");
            int coins = SaveManager.Coins;
            bool hadStage = PlayerPrefs.HasKey("Macaron.Stage");
            int stage = PlayerPrefs.GetInt("Macaron.Stage", 1);
            try
            {
                RunChecks();
                Check(factory.OpenSlots == 4, "Exactly four slots start unlocked");
                SaveManager.Coins = 0;
                Check(!factory.TryUnlockSlot() && factory.OpenSlots == 4 && SaveManager.Coins == 0,
                    "Insufficient funds must not spend or unlock");
                SaveManager.Coins = factory.unlockSlotCost * 3;
                Check(factory.TryUnlockSlot() && factory.TryUnlockSlot(), "Both locked slots can be purchased");
                Check(factory.OpenSlots == 6 && SaveManager.Coins == factory.unlockSlotCost, "Spend exactly once per slot");
                Check(!factory.TryUnlockSlot() && SaveManager.Coins == factory.unlockSlotCost, "Never charge for a seventh slot");
                float feedDeadline = Time.realtimeSinceStartup + 30;
                while (factory.Conveyor.Items.Select(item => item.ColorType).Distinct().Count() < 2 && Time.realtimeSinceStartup < feedDeadline)
                    yield return null;
                Check(factory.Conveyor.Items.Select(item => item.ColorType).Distinct().Count() >= 2, "Feeders must introduce items onto the empty loop");
                CheckDeadlockAndReservations(factory);
                var first = factory.Conveyor.Items.First();
                float previousT = first.PathT;
                int remaining = factory.Remaining;
                yield return new WaitForSeconds(.3f);
                Check(first.PathT != previousT, "Loop keeps moving while no tray is waiting");
                Check(factory.Remaining == remaining, "No tray means no cakes collected");
                float deadline = Time.realtimeSinceStartup + 600;
                while (GameManager.Instance.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var tray in factory.Trays.OrderByDescending(t => t.Layer))
                    {
                        if (!tray.OnTable || !tray.Accessible || factory.Slots.Take(factory.OpenSlots).All(t => t != null)) continue;
                        if (factory.Slots.Any(t => t != null && t.Color == tray.Color)) continue;
                        Check(factory.TrySelect(tray), "Accessible tray must move");
                        Check(!factory.TrySelect(tray), "Double tap must not reserve two slots");
                    }
                    foreach (var tray in factory.Trays)
                    {
                        Check(tray.Filled + tray.Reserved <= tray.Capacity, "Never overfill during concurrent flights");
                        for (int i = 0; i < tray.Filled; i++)
                        {
                            var anchor = tray.GetPocket(i);
                            Check(anchor.childCount == 1, "One macaron per pocket");
                            var macaron = anchor.GetChild(0);
                            Check(macaron.localPosition.sqrMagnitude < .000001f, "Macaron must land at the authored pocket position");
                            Check(Quaternion.Angle(macaron.localRotation, Quaternion.identity) < .01f, "Macaron must use the authored pocket rotation");
                            Check((macaron.localScale - Vector3.one).sqrMagnitude < .000001f, "Macaron must fit the authored pocket scale");
                        }
                    }
                    if (factory.Remaining == 0 && factory.IsBusy)
                        Check(GameManager.Instance.IsPlaying, "Win must wait for flights and shipping");
                    yield return null;
                }
                Check(GameManager.Instance.State == GameState.Win, "The complete board must win within 600 seconds");
                Check(factory.Remaining == 0 && factory.Slots.All(t => t == null), "Win leaves no belt items or occupied slots");
                Check(factory.Trays.All(t => t.Filled == t.Capacity), "Every tray must ship exactly its capacity");
                Debug.Log("MACARON PLAYTHROUGH PASSED: source loop, automatic pickup, authored pocket poses, purchasing, deadlock, reservations, shipping and win.");
            }
            finally
            {
                if (hadCoins) PlayerPrefs.SetInt("Coins", coins); else PlayerPrefs.DeleteKey("Coins");
                if (hadStage) PlayerPrefs.SetInt("Macaron.Stage", stage); else PlayerPrefs.DeleteKey("Macaron.Stage");
                PlayerPrefs.Save();
            }
        }

        private static void CheckDeadlockAndReservations(MacaronFactory factory)
        {
            var slots = (MacaronTray[])typeof(MacaronFactory).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(factory);
            var testPickup = new List<ConveyorBlock3D>();
            var first = factory.Conveyor.Items.First();
            var later = factory.Conveyor.Items.First(b => b.ColorType != first.ColorType);
            var temporary = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MacaronTray>("Assets/MacaronFactory/Prefabs/Tray_2x4.prefab"));
            try
            {
                temporary.Initialize(factory, later.ColorType, 0, false, Vector3.zero);
                temporary.LeaveTable();
                temporary.Moving = false;
                for (int i = 0; i < temporary.Capacity; i++) Check(temporary.TryReserve(), "Each pocket can be reserved");
                Check(!temporary.TryReserve(), "Extra reservation must fail for a full tray");
                for (int i = 0; i < temporary.Capacity; i++) temporary.CancelReservation();
                for (int i = 0; i < slots.Length; i++) slots[i] = temporary;
                testPickup.Clear();
                testPickup.Add(first);
                factory.SetPickupOverride(testPickup);
                Check(factory.IsDeadlocked(), "A color outside the pickup zone must not save full slots");
                temporary.Moving = true;
                Check(!factory.IsDeadlocked(), "Do not fail during tray movement");
                temporary.Moving = false;
                slots[0] = null;
                Check(!factory.IsDeadlocked(), "An empty open slot prevents fail");
                slots[0] = temporary;
                testPickup.Clear();
                testPickup.Add(null);
                Check(!factory.IsDeadlocked(), "An empty pickup zone is not an arriving mismatch");
                testPickup[0] = later;
                Check(!factory.IsDeadlocked(), "A matching arriving color prevents fail");
            }
            finally
            {
                factory.SetPickupOverride(null);
                Array.Clear(slots, 0, slots.Length);
                UnityEngine.Object.Destroy(temporary.gameObject);
            }
        }
    }
}
