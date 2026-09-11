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
            factory.cartonDeliveryPrefab = AssetDatabase.LoadAssetAtPath<CandyBlast.Cartoon.CartonDeliverySequence>(
                "Assets/MacaronFactory/Prefabs/CartonDelivery.prefab");
            CreateTrayPrefab("2x4");
            CreateTrayPrefab("1x4");
            int[] colors = { 2, 4, 3, 6, 7, 1 };
            factory.macaronPrefabs = colors.Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Macaron_Props/Prefabs/Macaron_{i}.prefab")).ToArray();
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
            var rect = new Rect(0, 0, 2, 1);
            Check(MacaronTray.Blocks(new Rect(1.9f, .9f, 2, 1), 1, rect, 0), "Partial corner overlap blocks");
            Check(!MacaronTray.Blocks(rect, 0, rect, 0), "Same layer cannot block");
            Check(!MacaronTray.Blocks(new Rect(2, 0, 2, 1), 1, rect, 0), "Touching edges do not block");
            Check(!MacaronTray.Blocks(rect, 0, rect, 1), "Lower tray cannot block upper tray");

            Check(MacaronConveyorFlow.IsInExitZone(.9f, 10, 1, .5f), "The final conveyor section can fill trays");
            Check(!MacaronConveyorFlow.IsInExitZone(.84f, 10, 1, .5f), "Only the final conveyor section can fill trays");
            Check(!MacaronConveyorFlow.IsInExitZone(.96f, 10, 1, .5f), "Rows must not pass the configured stop point");
            Check(!MacaronConveyorFlow.IsInExitZone(1, 0, 1), "An uninitialized conveyor cannot fill trays");
            Check(Mathf.Approximately(MacaronConveyorFlow.DistanceToExit(.9f, 10, .5f), .5f), "Leading row can move to the configured stop point");
            Check(Mathf.Approximately(MacaronConveyorFlow.DistanceToExit(.93f, 10, .5f), .2f), "Approaching row stops before t=1 without wrapping");
            Check(MacaronConveyorFlow.DistanceToExit(.95f, 10, .5f) == 0, "A row at the configured stop point must stop");
            Check(MacaronConveyorFlow.DistanceToExit(.950001f, 10, .5f) == 0, "Float drift must not release the conveyor stop");
            float limitedStep = Mathf.Min(2, MacaronConveyorFlow.DistanceToExit(.9f, 10, .5f));
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
                Check(factory.Level.conveyorController.SplineWorldLength > 0, "Package spline must be initialized");
                Check(!factory.Level.conveyorController.automaticMotion && factory.conveyorSource.conveyorController.automaticMotion,
                    "Factory owns endpoint flow while the package controller remains reusable");
                Check(factory.Rows.All(row => row.Length >= 1 && row.Length <= factory.maxRowWidth), "Rows must respect width 1-5");
                Check(factory.Rows.Take(factory.Rows.Count - 1).All(row => row.Length == factory.maxRowWidth), "Every row except the last must fill all conveyor columns");
                Check(Mathf.Approximately(factory.Level.conveyorController.GetComponent<ConveyorTrackMeshBuilder>().beltHalfWidth,
                    factory.laneSpacing * (factory.maxRowWidth - 1) * .5f + .16f), "Belt width must fit the configured columns");
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
            float speed = factory.conveyorSpeed;
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
                CheckDeadlockAndReservations(factory);
                var leadingGroup = factory.Rows[0][0].GetComponentInParent<BlockGroup>();
                float stopDeadline = Time.realtimeSinceStartup + 10;
                while (MacaronConveyorFlow.DistanceToExit(factory.Level.conveyorController.GetGroupHeadT(leadingGroup),
                    factory.Level.conveyorController.SplineWorldLength, factory.stopBeforeExitDistance) > 0 && Time.realtimeSinceStartup < stopDeadline)
                    yield return null;
                Check(MacaronConveyorFlow.DistanceToExit(factory.Level.conveyorController.GetGroupHeadT(leadingGroup),
                    factory.Level.conveyorController.SplineWorldLength, factory.stopBeforeExitDistance) == 0, "Leading row must reach the configured stop before the endpoint");
                var waiting = factory.Rows.SelectMany(r => r).ToDictionary(b => b, b => b.transform.position);
                yield return new WaitForSeconds(.3f);
                Check(waiting.All(pair => Vector3.Distance(pair.Key.transform.position, pair.Value) < .0001f), "Belt must stay stopped until the leading cakes are collected");
                Check(factory.Remaining == waiting.Count, "No tray means no cakes collected");
                Check(factory.PickupBlocks.Count == factory.Rows[0].Length && factory.Rows[0].All(b => factory.PickupBlocks.Contains(b)),
                    "Only the leading row may fill, even when later rows are near the exit");
                factory.conveyorSpeed *= 4;
                float deadline = Time.realtimeSinceStartup + 100;
                while (GameManager.Instance.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    Check(factory.Trays.Sum(t => t.Reserved) <= 1, "Only one macaron may fly at a time across all trays");
                    Check(factory.Rows.Count(row => row.Any(b => !ReferenceEquals(b, null) && factory.PickupBlocks.Contains(b))) <= 1,
                        "A pickup batch must never include cakes from different rows");
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
                Check(GameManager.Instance.State == GameState.Win, "The complete board must win within 100 seconds");
                Check(factory.Remaining == 0 && factory.Slots.All(t => t == null), "Win leaves no belt items or occupied slots");
                Check(factory.Trays.All(t => t.Filled == t.Capacity), "Every tray must ship exactly its capacity");
                Debug.Log("MACARON PLAYTHROUGH PASSED: endpoint stop, automatic single pickup, authored pocket poses, purchasing, deadlock, reservations, shipping and win.");
            }
            finally
            {
                if (hadCoins) PlayerPrefs.SetInt("Coins", coins); else PlayerPrefs.DeleteKey("Coins");
                if (hadStage) PlayerPrefs.SetInt("Macaron.Stage", stage); else PlayerPrefs.DeleteKey("Macaron.Stage");
                PlayerPrefs.Save();
                if (factory != null)
                {
                    factory.conveyorSpeed = speed;
                }
            }
        }

        private static void CheckDeadlockAndReservations(MacaronFactory factory)
        {
            var slots = (MacaronTray[])typeof(MacaronFactory).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(factory);
            var front = (List<ConveyorBlock3D>)typeof(MacaronConveyorFlow)
                .GetField("_pickupBlocks", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(factory.ConveyorFlow);
            var originalFront = front.ToArray();
            var first = factory.Rows[0][0];
            var later = factory.Rows.SelectMany(r => r).First(b => b.ColorType != first.ColorType);
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
                front.Clear();
                front.Add(first);
                Check(factory.IsDeadlocked(), "A color outside the pickup zone must not save full slots");
                temporary.Moving = true;
                Check(!factory.IsDeadlocked(), "Do not fail during tray movement");
                temporary.Moving = false;
                slots[0] = null;
                Check(!factory.IsDeadlocked(), "An empty open slot prevents fail");
                slots[0] = temporary;
                front[0] = null;
                Check(!factory.IsDeadlocked(), "An empty pickup zone is not an arriving mismatch");
                front[0] = later;
                Check(!factory.IsDeadlocked(), "A matching arriving color prevents fail");
            }
            finally
            {
                Array.Clear(slots, 0, slots.Length);
                front.Clear();
                front.AddRange(originalFront);
                UnityEngine.Object.Destroy(temporary.gameObject);
            }
        }
    }
}
