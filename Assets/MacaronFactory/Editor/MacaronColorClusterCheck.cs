using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlockShooter.Editor
{
    public static class MacaronColorClusterCheck
    {
        [MenuItem("Macaron Factory/Checks/Color clusters")]
        public static void Run()
        {
            var source = new List<BlockColorType> {
                BlockColorType.Red, BlockColorType.Blue, BlockColorType.Red, BlockColorType.Green,
                BlockColorType.Red, BlockColorType.Blue, BlockColorType.Red, BlockColorType.Green,
                BlockColorType.Red, BlockColorType.Blue, BlockColorType.Red, BlockColorType.Green,
                BlockColorType.Red, BlockColorType.Blue, BlockColorType.Red, BlockColorType.Green,
            };
            var clustered = MacaronLevel.ClusterColors(source, 6, 3, 3);
            if (!source.OrderBy(color => color).SequenceEqual(clustered.OrderBy(color => color)))
                throw new Exception("Color clustering changed the supply counts.");
            CheckRuns(clustered, 3, 10, false);
            if (clustered.GroupBy(color => color).Any(group => group.Count() != source.Count(color => color == group.Key)))
                throw new Exception("Color clustering lost a macaron color.");
            var fourColumns = MacaronLevel.ClusterColors(new List<BlockColorType>(
                Enumerable.Repeat(BlockColorType.Red, 8).Concat(Enumerable.Repeat(BlockColorType.Blue, 8))), 6, 4, 3);
            CheckRuns(fourColumns, 4, 10);
            var longCluster = MacaronLevel.ClusterColors(new List<BlockColorType>(
                Enumerable.Repeat(BlockColorType.Red, 12).Concat(Enumerable.Repeat(BlockColorType.Blue, 12))), 12, 3, 3);
            CheckRuns(longCluster, 3, 10);
            var palette = MacaronLevel.ClusterColors(new List<BlockColorType>(Enumerable.Repeat(BlockColorType.Red, 12)
                .Concat(Enumerable.Repeat(BlockColorType.Blue, 12)).Concat(Enumerable.Repeat(BlockColorType.Green, 12))
                .Concat(Enumerable.Repeat(BlockColorType.Yellow, 12))), 12, 3, 3);
            if (palette.Take(36).Contains(BlockColorType.Yellow))
                throw new Exception("A supply window introduced more than three colors.");
            var feederRows = MacaronLevel.BuildFeederAssignments(longCluster, 3, 0, 2);
            if (feederRows.feeder.Any(feeder => feeder < 0 || feeder > 1))
                throw new Exception("Color runs must receive a valid feeder assignment.");
            Debug.Log("PASS: Color clusters preserve counts, fill whole rows, vary from one to ten rows and stay within the active palette.");
        }

        private static void CheckRuns(IReadOnlyList<BlockColorType> colors, int columns, int maxRows, bool requireFullRows = true)
        {
            int run = 0;
            for (int i = 0; i < colors.Count; i++)
            {
                run++;
                if (i + 1 < colors.Count && colors[i + 1] == colors[i]) continue;
                if ((requireFullRows && run % columns != 0) || run > columns * maxRows)
                    throw new Exception("Color runs must contain full rows and stay within the configured maximum.");
                run = 0;
            }
        }
    }
}
