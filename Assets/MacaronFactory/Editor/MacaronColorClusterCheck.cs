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
            if (clustered.Take(6).Any(color => color != BlockColorType.Red) ||
                clustered.Skip(6).Take(4).Any(color => color != BlockColorType.Blue))
                throw new Exception("Color clustering did not keep matching macarons together.");
            if (clustered.GroupBy(color => color).Any(group => group.Count() != source.Count(color => color == group.Key)))
                throw new Exception("Color clustering lost a macaron color.");
            var fourColumns = MacaronLevel.ClusterColors(new List<BlockColorType>(
                Enumerable.Repeat(BlockColorType.Red, 8).Concat(Enumerable.Repeat(BlockColorType.Blue, 8))), 6, 4, 3);
            if (fourColumns.Take(8).Any(color => color != BlockColorType.Red))
                throw new Exception("Color clusters must fill complete rows based on Columns.");
            var longCluster = MacaronLevel.ClusterColors(new List<BlockColorType>(
                Enumerable.Repeat(BlockColorType.Red, 12).Concat(Enumerable.Repeat(BlockColorType.Blue, 12))), 12, 3, 3);
            if (longCluster.Take(12).Any(color => color != BlockColorType.Red))
                throw new Exception("The default cluster must keep twelve matching macarons together.");
            var palette = MacaronLevel.ClusterColors(new List<BlockColorType>(Enumerable.Repeat(BlockColorType.Red, 12)
                .Concat(Enumerable.Repeat(BlockColorType.Blue, 12)).Concat(Enumerable.Repeat(BlockColorType.Green, 12))
                .Concat(Enumerable.Repeat(BlockColorType.Yellow, 12))), 12, 3, 3);
            if (palette.Take(36).Contains(BlockColorType.Yellow))
                throw new Exception("A supply window introduced more than three colors.");
            var feederRows = MacaronLevel.BuildFeederAssignments(longCluster, 3, 0, 2);
            if (feederRows.feeder.Take(4).Any(feeder => feeder != 0) || feederRows.feeder.Skip(4).Take(4).Any(feeder => feeder != 1))
                throw new Exception("Matching color rows were split between feeder queues.");
            Debug.Log("PASS: Color clusters preserve counts, fill whole rows and stay on one feeder.");
        }
    }
}
