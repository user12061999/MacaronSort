// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/StageLayout.cs.
using System.Collections.Generic;

namespace BlockShooter.SodaConveyor
{
    public static class StageLayout
    {
        public const int SourceLaneCount = 5;
        public const float LaneSpacing = 0.22f;
        public static StageBranchSpec[] FeedAllFromBranches(int preset, StageGroupSpec[] main, StageBranchSpec[] branches)
        {
            if (branches.Length == 0)
            {
                // The first two source layouts have no feeder. Add a straight inlet at the far side.
                var path = TrackShapePresets.Build(preset, 1, 0);
                UnityEngine.Splines.SplineUtility.Evaluate(path, .5f, out var p, out _, out _);
                var center = Unity.Mathematics.float3.zero;
                for (int i = 0; i < path.Count; i++) center += path[i].Position;
                center /= path.Count;
                var outward = Unity.Mathematics.math.normalizesafe(p - center, new Unity.Mathematics.float3(0, 0, 1));
                var start = p + outward * 4f;
                var tangent = (p - start) / 3f;
                branches = new[] { new StageBranchSpec("Inlet", .5f, false, System.Array.Empty<StageGroupSpec>(), new[] {
                    new BranchKnot(start.x, start.z, -tangent.x, -tangent.z, tangent.x, tangent.z),
                    new BranchKnot(p.x, p.z, -tangent.x, -tangent.z, tangent.x, tangent.z)
                }) };
            }

            var queues = new List<StageGroupSpec>[branches.Length];
            for (int i = 0; i < queues.Length; i++) queues[i] = new List<StageGroupSpec>();
            for (int i = 0; i < main.Length; i++) queues[i % queues.Length].Add(main[i]);
            var result = new StageBranchSpec[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                var branch = branches[i];
                queues[i].AddRange(branch.Groups);
                result[i] = new StageBranchSpec(branch.Name, branch.MergeT, branch.ConnectFromLeft, queues[i].ToArray(), branch.Knots);
            }
            return result;
        }
        sealed class Block
        {
            public int Branch;
            public BlockColorType Color;
            public int Cans;
            public int Rows;
        }
        public static StageGroupSpec[] MainGroups(int templateIndex) => StageTrackData.MainGroups(templateIndex);
        public static StageBranchSpec[] Branches(int templateIndex, float contentScale = 1f, int canQuantum = 0, int laneCount = StageGroupSpec.LaneCount)
        {
            var source = StageTrackData.Branches(templateIndex);
            if (source.Length == 0) return source;
            contentScale = System.Math.Max(1f, contentScale);

            // Source branch groups first (their order within a branch is the merge order), then
            // one surplus block per main-loop color in first-seen order — deterministic output.
            var blocks = new List<Block>();
            for (var b = 0; b < source.Length; b++)
                foreach (var g in source[b].Groups)
                    blocks.Add(new Block { Branch = b, Color = g.Color, Cans = g.RowCount * SourceLaneCount });

            // Surplus block per color: the main loop's lost lane, plus this level's extra content
            // as a share of the color's authored total (main + branches). Colors that only live
            // on a branch still get their extra share, so scaling keeps the color mix intact.
            var surplusOrder = new List<BlockColorType>();
            var surplusCans = new Dictionary<BlockColorType, int>();
            var authoredCans = new Dictionary<BlockColorType, int>();
            void Note(BlockColorType color, int authored, int surplus)
            {
                if (!authoredCans.ContainsKey(color)) surplusOrder.Add(color);
                authoredCans.TryGetValue(color, out var a);
                authoredCans[color] = a + authored;
                surplusCans.TryGetValue(color, out var n);
                surplusCans[color] = n + surplus;
            }
            foreach (var g in StageTrackData.MainGroups(templateIndex))
                Note(g.Color, g.RowCount * SourceLaneCount, g.RowCount * (SourceLaneCount - laneCount));
            foreach (var b in source)
                foreach (var g in b.Groups)
                    Note(g.Color, g.RowCount * SourceLaneCount, 0);

            foreach (var color in surplusOrder)
            {
                var authored = authoredCans[color];
                var target = authored * contentScale;
                var targetCans = canQuantum > 0
                    ? (int)System.Math.Ceiling(target / canQuantum - 1e-4f) * canQuantum
                    : (int)System.Math.Round(target);
                var cans = surplusCans[color] + System.Math.Max(0, targetCans - authored);
                if (cans > 0) blocks.Add(new Block { Branch = -1, Color = color, Cans = cans });
            }

            ApportionRows(blocks, laneCount);

            var groups = new List<StageGroupSpec>[source.Length];
            for (var b = 0; b < source.Length; b++)
                groups[b] = new List<StageGroupSpec>(source[b].Groups.Length + 4);

            foreach (var block in blocks)
            {
                if (block.Rows <= 0) continue;
                if (block.Branch >= 0)
                {
                    groups[block.Branch].Add(new StageGroupSpec(block.Color, block.Rows));
                    continue;
                }

                var list = groups[PickBranch(groups, block.Color)];
                var last = list.Count - 1;
                if (last >= 0 && list[last].Color == block.Color)
                    list[last] = new StageGroupSpec(block.Color, list[last].RowCount + block.Rows);
                else
                    list.Add(new StageGroupSpec(block.Color, block.Rows));
            }

            var result = new StageBranchSpec[source.Length];
            for (var b = 0; b < source.Length; b++)
            {
                var s = source[b];
                result[b] = new StageBranchSpec(s.Name, s.MergeT, s.ConnectFromLeft, groups[b].ToArray(), s.Knots);
            }
            return result;
        }
        static void ApportionRows(List<Block> blocks, int laneCount)
        {
            var lanes = laneCount;
            var rowQuantum = 4 / lanes; // Authored trays hold at least four cakes.
            var seen = new HashSet<BlockColorType>();
            foreach (var first in blocks)
            {
                if (!seen.Add(first.Color)) continue;

                var total = 0;
                var floorSum = 0;
                foreach (var block in blocks)
                {
                    if (block.Color != first.Color) continue;
                    total += block.Cans;
                    block.Rows = block.Cans / 4 * rowQuantum;
                    floorSum += block.Rows;
                }

                var leftover = RowsFor(total, 4) * rowQuantum - floorSum;
                while (leftover >= rowQuantum)
                {
                    Block best = null;
                    foreach (var block in blocks)
                    {
                        if (block.Color != first.Color) continue;
                        if (best == null || block.Cans % lanes >= best.Cans % lanes) best = block;
                    }
                    if (best == null) break;
                    best.Rows += rowQuantum;
                    best.Cans -= best.Cans % 4; // spend its fraction so ties go to the next block
                    leftover -= rowQuantum;
                }
            }
        }
        static int RowsFor(int cans, int lanes)
        {
            return (cans * 2 + lanes) / (2 * lanes);
        }
        static int PickBranch(List<StageGroupSpec>[] groups, BlockColorType color)
        {
            var best = -1;
            var bestRows = int.MaxValue;
            var bestHasColor = false;
            for (var b = 0; b < groups.Length; b++)
            {
                var rows = 0;
                var hasColor = false;
                foreach (var g in groups[b])
                {
                    rows += g.RowCount;
                    if (g.Color == color) hasColor = true;
                }

                if (hasColor && !bestHasColor || hasColor == bestHasColor && rows < bestRows)
                {
                    best = b;
                    bestRows = rows;
                    bestHasColor = hasColor;
                }
            }
            return best;
        }
    }
}
