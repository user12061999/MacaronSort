using System;
using System.Collections.Generic;
using System.Linq;
using BlockShooter.SodaConveyor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    public sealed partial class MacaronFactory
    {
        [Header("Soda Shippers conveyor")]
        [Tooltip("Loops per second, matching the source game's 0.09. Independent of belt length.")]
        [Min(.05f)] public float sourceLoopSpeed = .09f;
        public SodaConveyorTrack Conveyor { get; private set; }
        public int SourceStage { get; private set; }
        private readonly List<ConveyorBlock3D> _sourcePickup = new();
        private ColorRegistryConfig _sourceColors;
        private StageBranchSpec[] _sourceBranches;
        public MacaronLevel.PuzzleStyle PuzzleStyle => _layout.ResolvePuzzleStyle(Stage);

        private void FrameTrayBoard()
        {
            var camera = Camera.main;
            var frame = camera.GetComponent<MacaronCameraFrame>() ?? camera.gameObject.AddComponent<MacaronCameraFrame>();
            frame.FrameFactoryLayout(_layout, SourceCameraBounds);
        }

        private IEnumerable<Bounds> SourceCameraBounds()
        {
            foreach (var renderer in _layout.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && renderer.name != "Factory floor" && !renderer.transform.IsChildOf(Conveyor.transform))
                    yield return renderer.bounds;
            yield return SourceBeltBounds();
        }

        private Bounds SourceBeltBounds()
        {
            var beltBounds = new Bounds(Conveyor.EvaluateWorld(0, out _), Vector3.zero);
            for (int i = 1; i < 128; i++) beltBounds.Encapsulate(Conveyor.EvaluateWorld(i / 128f, out _));
            beltBounds.Expand(Conveyor.OuterRadius * 2 + .3f);
            return beltBounds;
        }

        private IReadOnlyList<ConveyorBlock3D> GetSourcePickupBlocks()
        {
            _sourcePickup.Clear();
            if (Conveyor != null)
                foreach (var item in Conveyor.Items)
                    if (item != null && !item.IsDestroyed && !item.IsTargeted &&
                        item.Phase == ConveyorItemPhase.OnLoop && item.JumpProgress >= .999f &&
                        Conveyor.IsInExitWindow(item.PathT)) _sourcePickup.Add(item);
            return _sourcePickup;
        }

        private void BuildConveyor()
        {
            if (_layout.waitingSlots == null || _layout.waitingSlots.Length != 6 ||
                _layout.waitingSlots.Any(slot => slot == null) || _layout.GetTrays().Length == 0)
                throw new InvalidOperationException("Macaron layout needs six waiting slots and authored tray templates.");

            // Source stages 1–10, then replays of 3–10. Seed by displayed stage so Retry keeps its layout.
            SourceStage = _layout.ResolveConveyorStage(Stage);
            int preset = SourceStage - 1;
            var supply = _layout.BuildConveyorSupply(SourceStage, out var main, out var branches);
            branches = StageLayout.FeedAllFromBranches(preset, main, branches);
            AddSourceColors();

            if (_layout.conveyorPath != null) _layout.conveyorPath.gameObject.SetActive(false);
            foreach (var branch in _layout.feederBranches)
                if (branch != null) branch.gameObject.SetActive(false);
            if (_layout.collectionGate != null) _layout.collectionGate.gameObject.SetActive(false);
            if (_layout.gateMountRoot != null) _layout.gateMountRoot.gameObject.SetActive(false);
            foreach (Transform child in _layout.transform)
                if (child.name.StartsWith("Conveyor board")) child.gameObject.SetActive(false);

            var root = new GameObject("Soda Shippers conveyor");
            root.transform.SetParent(_layout.transform, false);
            Conveyor = root.AddComponent<SodaConveyorTrack>();
            Conveyor.SideMaterial = Material("Conveyor sides", new Color(.35f, .35f, .4f));
            Conveyor.TopMaterial = Material("Conveyor top", new Color(.2f, .2f, .24f));
            Conveyor.SetTrackShape(preset, 1f);
            float diameter = macaronPrefabs.Max(prefab =>
            {
                var box = prefab.GetComponent<Renderer>().localBounds;
                return 2 * Mathf.Max(Mathf.Abs(box.center.x) + box.extents.x, Mathf.Abs(box.center.z) + box.extents.z);
            }) * conveyorMacaronScale;
            Conveyor.SetItemDiameter(diameter);
            Conveyor.Configure(sourceLoopSpeed);
            Conveyor.BuildVisualBelt(branches);

            // Keep the original belt placement behind the receiving slots.
            var bounds = SourceBeltBounds();
            float slotX = _layout.waitingSlots.Average(slot => slot.position.x);
            float slotBack = _layout.waitingSlots.Max(slot => slot.position.z);
            root.transform.position += new Vector3(slotX - bounds.center.x, 0, slotBack + 1f - bounds.min.z);
            Conveyor.SpawnItem = (color, parent) => SpawnMacaronBlock(color, parent, conveyorMacaronScale);
            Conveyor.BuildEmptySlots();
            _sourceBranches = branches;
            _remaining = supply.Sum(group => group.RowCount * StageGroupSpec.LaneCount);
            RebuildSourceTrays(supply);
        }

        private void AddSourceColors()
        {
            // Keep the source's extra colors distinct; merging them into six flavors changes the puzzle.
            colorRegistry = ColorRegistry != null ? Instantiate(ColorRegistry) : ScriptableObject.CreateInstance<ColorRegistryConfig>();
            _sourceColors = colorRegistry;
            var extras = new[] {
                (BlockColorType.Custom1, "Pink", new Color(1f, .38f, .64f)),
                (BlockColorType.Custom2, "Gray", new Color(.48f, .52f, .57f)),
                (BlockColorType.Custom3, "Light blue", new Color(.32f, .78f, 1f))
            };
            foreach (var (color, label, tint) in extras)
                if (!colorRegistry.colors.Any(entry => entry.colorType == color))
                    colorRegistry.colors.Add(new ColorRegistryConfig.ColorDefinition {
                        colorType = color, displayName = label, editorColor = tint, overrideColor = true
                    });
        }

        private void RebuildSourceTrays(StageGroupSpec[] supply)
        {
            var authored = _layout.GetTrays().OrderByDescending(tray => tray.stackLayer).ToArray();
            var templates = authored.GroupBy(tray => tray.Capacity).Select(group => group.First())
                .OrderByDescending(tray => tray.Capacity).ToArray();
            if (!templates.Any(tray => tray.Capacity == StageGroupSpec.LaneCount))
                throw new InvalidOperationException("Source conveyor needs a four-pocket tray template to match complete rows.");
            // Large trays use authored large-tray poses, never a small tray's footprint.
            var poses = PuzzleStyle == MacaronLevel.PuzzleStyle.BigOrders
                ? authored.Where(tray => tray.Capacity == templates[0].Capacity).ToArray() : authored;

            var orders = new List<(MacaronTray template, BlockColorType color)>();
            foreach (var group in supply)
            {
                int left = group.RowCount * StageGroupSpec.LaneCount;
                while (left > 0)
                {
                    var pose = poses[orders.Count % poses.Length];
                    var template = pose.Capacity <= left && pose.Capacity % StageGroupSpec.LaneCount == 0
                        ? pose : templates.First(tray => tray.Capacity == StageGroupSpec.LaneCount);
                    orders.Add((template, group.Color));
                    left -= template.Capacity;
                }
            }

            // Reuse authored tray positions/stacks; add full stack tiers only when source content needs them.
            if (_layout.shuffleTrayColors)
            {
                var random = new System.Random(_layout.trayArrangementSeed);
                foreach (var capacity in orders.Select(order => order.template.Capacity).Distinct().ToArray())
                {
                    var indices = Enumerable.Range(0, orders.Count).Where(i => orders[i].template.Capacity == capacity).ToArray();
                    for (int i = indices.Length - 1; i > 0; i--)
                    {
                        int a = indices[i], b = indices[random.Next(i + 1)];
                        var color = orders[a].color;
                        orders[a] = (orders[a].template, orders[b].color);
                        orders[b] = (orders[b].template, color);
                    }
                }
            }
            int copies = Mathf.CeilToInt((float)orders.Count / poses.Length);
            int layers = authored.Max(tray => tray.stackLayer) + 1;
            float bottom = authored.Min(tray => tray.transform.localPosition.y);
            float top = authored.Max(tray => tray.transform.localPosition.y);
            float height = authored.Max(tray => tray.GetComponent<BoxCollider>().size.y * tray.transform.localScale.y);
            float tierHeight = top - bottom + height + .04f;
            for (int i = 0; i < orders.Count; i++)
            {
                var pose = poses[i % poses.Length];
                var order = orders[i];
                int tier = copies - 1 - i / poses.Length;
                var tray = Instantiate(order.template, _layout.trayRoot);
                tray.name = $"Source tray {i + 1} {order.color}";
                tray.transform.localPosition = pose.transform.localPosition + Vector3.up * (tier * tierHeight);
                if (tier % 2 != 0)
                    tray.transform.localPosition += new Vector3(_layout.repeatedTierOffset.x, 0, _layout.repeatedTierOffset.y);
                tray.transform.localRotation = pose.transform.localRotation;
                tray.transform.localScale = pose.transform.localScale;
                tray.levelColor = order.color;
                tray.stackLayer = pose.stackLayer + tier * layers;
                tray.mystery = false; // Selected from covered trays after all footprints are initialized.
            }
            foreach (var tray in authored)
            {
                tray.gameObject.SetActive(false);
                tray.transform.SetParent(transform, true);
                Destroy(tray.gameObject);
            }
        }

        private void BuildPuzzleSupply()
        {
            if (PuzzleStyle == MacaronLevel.PuzzleStyle.Original)
            {
                Conveyor.BuildBranches(_sourceBranches);
                return;
            }
            var order = BuildPuzzleOrder(_trays, PuzzleStyle);
            var batches = order.Select(tray => new StageGroupSpec(tray.Color, tray.Capacity / StageGroupSpec.LaneCount)).ToArray();
            // ponytail: one ordered feeder gives a constructive solution (ship one accessible tray at a time).
            // Parallel feeders need a bounded look-ahead scheduler before sharing this queue.
            int inlet = (Stage - 1) % _sourceBranches.Length;
            Conveyor.BuildBranches(_sourceBranches.Select((branch, index) => new StageBranchSpec(
                branch.Name, branch.MergeT, branch.ConnectFromLeft,
                index == inlet ? batches : Array.Empty<StageGroupSpec>(), branch.Knots)).ToArray());
        }

        public static List<MacaronTray> BuildPuzzleOrder(IReadOnlyList<MacaronTray> trays, MacaronLevel.PuzzleStyle style)
        {
            var remaining = trays.ToList();
            var result = new List<MacaronTray>(remaining.Count);
            // ponytail: bounded tray boards use a direct blocker scan; cache the dependency graph for hundreds of trays.
            while (remaining.Count > 0)
            {
                var accessible = remaining.Where(tray => !remaining.Any(tray.IsBlockedBy)).ToList();
                if (accessible.Count == 0) throw new InvalidOperationException("Tray layout has no accessible removal order.");
                MacaronTray next;
                if (style == MacaronLevel.PuzzleStyle.ColorChains)
                {
                    var color = result.Count > 0 && accessible.Any(tray => tray.Color == result[result.Count - 1].Color)
                        ? result[result.Count - 1].Color
                        : accessible.GroupBy(tray => tray.Color).OrderByDescending(group => group.Sum(tray => tray.Capacity)).First().Key;
                    next = accessible.First(tray => tray.Color == color);
                }
                else if (style == MacaronLevel.PuzzleStyle.BigOrders)
                    next = accessible.OrderByDescending(tray => tray.Capacity).ThenByDescending(tray => tray.Layer).First();
                else
                    next = accessible.OrderByDescending(tray => remaining.Count(below => below.IsBlockedBy(tray)))
                        .ThenByDescending(tray => tray.Layer).First();
                if (next.Capacity <= 0 || next.Capacity % StageGroupSpec.LaneCount != 0)
                    throw new InvalidOperationException("Puzzle trays must hold complete conveyor rows.");
                result.Add(next);
                remaining.Remove(next);
            }
            return result;
        }

        private string PuzzleHint => PuzzleStyle switch
        {
            MacaronLevel.PuzzleStyle.ClearLayers => "CLEAR LAYERS: uncover the next color.",
            MacaronLevel.PuzzleStyle.ColorChains => "COLOR CHAINS: keep packing the same color.",
            MacaronLevel.PuzzleStyle.BigOrders => "BIG ORDERS: leave room for a large tray.",
            _ => _layout.instruction
        };

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Macaron Factory/Checks/Stage puzzles (Play Mode)")]
        public static void CheckStagePuzzles()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var source = UnityEngine.Object.FindFirstObjectByType<MacaronFactory>();
            if (source == null) throw new InvalidOperationException("Open the factory scene.");
            int checks = 0;
            foreach (var prefab in source.levels)
                foreach (var style in new[] { MacaronLevel.PuzzleStyle.ClearLayers,
                    MacaronLevel.PuzzleStyle.ColorChains, MacaronLevel.PuzzleStyle.BigOrders })
                {
                    var root = new GameObject("Stage puzzle check");
                    root.SetActive(false);
                    try
                    {
                        var factory = root.AddComponent<MacaronFactory>();
                        factory.colorRegistry = source.ColorRegistry;
                        factory.macaronPrefabs = source.macaronPrefabs;
                        factory.PaperMaterial = source.PaperMaterial;
                        factory.Stage = checks / 3 + 1;
                        factory._layout = Instantiate(prefab, root.transform);
                        factory._layout.puzzleStyle = MacaronLevel.PuzzleStyle.Automatic;
                        if (factory._layout.ResolvePuzzleStyle(1) != MacaronLevel.PuzzleStyle.Original ||
                            factory._layout.ResolvePuzzleStyle(2) != MacaronLevel.PuzzleStyle.ClearLayers ||
                            factory._layout.ResolvePuzzleStyle(3) != MacaronLevel.PuzzleStyle.ColorChains ||
                            factory._layout.ResolvePuzzleStyle(4) != MacaronLevel.PuzzleStyle.BigOrders ||
                            factory._layout.ResolvePuzzleStyle(5) != MacaronLevel.PuzzleStyle.ClearLayers)
                            throw new Exception("Automatic stage progression changed.");
                        factory._layout.puzzleStyle = style;
                        var supply = factory._layout.BuildConveyorSupply(factory._layout.ResolveConveyorStage(factory.Stage), out _, out _);
                        factory.RebuildSourceTrays(supply);
                        factory.BuildTrays();
                        var order = BuildPuzzleOrder(factory.Trays, style);
                        if (order.Count != factory.Trays.Count || order.Distinct().Count() != order.Count ||
                            !order.SequenceEqual(BuildPuzzleOrder(factory.Trays, style)))
                            throw new Exception("Missing/duplicate trays or non-deterministic puzzle order.");
                        var remaining = order.ToList();
                        MacaronTray previous = null;
                        foreach (var tray in order)
                        {
                            if (remaining.Any(tray.IsBlockedBy)) throw new Exception("Solution selects a covered tray.");
                            if (style == MacaronLevel.PuzzleStyle.ColorChains && previous != null && tray.Color != previous.Color &&
                                remaining.Any(other => other.Color == previous.Color && !remaining.Any(other.IsBlockedBy)))
                                throw new Exception("Color Chains broke an available matching-color chain.");
                            if (style == MacaronLevel.PuzzleStyle.BigOrders && remaining.Any(other =>
                                other.Capacity > tray.Capacity && !remaining.Any(other.IsBlockedBy)))
                                throw new Exception("Big Orders did not prioritize an accessible large tray.");
                            remaining.Remove(tray);
                            previous = tray;
                        }
                        var expected = supply.GroupBy(group => group.Color).ToDictionary(group => group.Key,
                            group => group.Sum(batch => batch.RowCount * StageGroupSpec.LaneCount));
                        var actual = order.GroupBy(tray => tray.Color).ToDictionary(group => group.Key, group => group.Sum(tray => tray.Capacity));
                        if (expected.Count != actual.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out int count) || count != pair.Value))
                            throw new Exception("Puzzle changed per-color cake totals.");
                        checks++;
                    }
                    finally { DestroyImmediate(root); }
                }
            Debug.Log($"PASS: {checks} stage/style combinations preserve color totals, deterministic order and an unblocked solution.");
        }
#endif
    }
}
