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
            SourceStage = Stage <= 10 ? Stage : new System.Random(Stage).Next(3, 11);
            int preset = SourceStage - 1;
            float contentScale = SourceStage <= 3 ? 1f : SourceStage <= 7 ? 1.25f : 1.5f;
            int quantum = SourceStage <= 3 ? 20 : 24;
            var main = StageLayout.MainGroups(preset);
            var branches = StageLayout.Branches(preset, contentScale, quantum);
            var supply = main.Concat(branches.SelectMany(branch => branch.Groups)).ToArray();
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
            Conveyor.BuildBranches(branches);
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

            var orders = new List<(MacaronTray template, BlockColorType color)>();
            foreach (var group in supply)
            {
                int left = group.RowCount * StageGroupSpec.LaneCount;
                while (left > 0)
                {
                    var pose = authored[orders.Count % authored.Length];
                    var template = pose.Capacity <= left && pose.Capacity % StageGroupSpec.LaneCount == 0
                        ? pose : templates.First(tray => tray.Capacity == StageGroupSpec.LaneCount);
                    orders.Add((template, group.Color));
                    left -= template.Capacity;
                }
            }

            // Reuse authored tray positions/stacks; add full stack tiers only when source content needs them.
            int copies = Mathf.CeilToInt((float)orders.Count / authored.Length);
            int layers = authored.Max(tray => tray.stackLayer) + 1;
            float bottom = authored.Min(tray => tray.transform.localPosition.y);
            float top = authored.Max(tray => tray.transform.localPosition.y);
            float height = authored.Max(tray => tray.GetComponent<BoxCollider>().size.y * tray.transform.localScale.y);
            float tierHeight = top - bottom + height + .04f;
            for (int i = 0; i < orders.Count; i++)
            {
                var pose = authored[i % authored.Length];
                var order = orders[i];
                int tier = copies - 1 - i / authored.Length;
                var tray = Instantiate(order.template, _layout.trayRoot);
                tray.name = $"Source tray {i + 1} {order.color}";
                tray.transform.localPosition = pose.transform.localPosition + Vector3.up * (tier * tierHeight);
                tray.transform.localRotation = pose.transform.localRotation;
                tray.transform.localScale = pose.transform.localScale;
                tray.levelColor = order.color;
                tray.stackLayer = pose.stackLayer + tier * layers;
                tray.mystery = pose.mystery;
            }
            foreach (var tray in authored)
            {
                tray.gameObject.SetActive(false);
                tray.transform.SetParent(transform, true);
                Destroy(tray.gameObject);
            }
        }
    }
}
