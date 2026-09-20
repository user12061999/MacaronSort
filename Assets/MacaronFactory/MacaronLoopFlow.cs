using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Static preview and capacity calculations for Macaron Factory Editor tools.
    /// Runtime conveyor execution is handled directly by ConveyorController.
    /// </summary>
    public static class MacaronLoopFlow
    {
        private sealed class Path
        {
            public readonly Vector3[] Points = new Vector3[513];
            public readonly float[] Distances = new float[513];
            public float Length => Distances[^1];

            public Path(SplineContainer spline, float offset = 0)
            {
                for (int i = 0; i < Points.Length; i++)
                {
                    spline.Spline.Evaluate(i / 512f, out var p, out var tangent, out var up);
                    var right = Vector3.Cross(spline.transform.TransformDirection((Vector3)up),
                        spline.transform.TransformDirection((Vector3)tangent)).normalized;
                    Points[i] = spline.transform.TransformPoint((Vector3)p) + right * offset;
                    if (i > 0) Distances[i] = Distances[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
                }
                if (!float.IsFinite(Length) || Length <= .001f)
                    throw new InvalidOperationException("Conveyor lane has no usable length.");
            }

            public void Pose(float distance, out Vector3 position, out Quaternion rotation)
            {
                int index = Array.BinarySearch(Distances, Mathf.Clamp(distance, 0, Length));
                int hi = Mathf.Clamp(index >= 0 ? index : ~index, 1, Distances.Length - 1);
                float t = Mathf.InverseLerp(Distances[hi - 1], Distances[hi], distance);
                position = Vector3.Lerp(Points[hi - 1], Points[hi], t);
                rotation = Quaternion.LookRotation(Points[hi] - Points[hi - 1], Vector3.up);
            }
        }

        private static float Spacing(float spacing, float diameter, float multiplier)
            => Mathf.Max(diameter * 1.08f + .015f, spacing * Mathf.Clamp(multiplier, .5f, 1.5f));

        private static Path[] Lanes(SplineContainer spline, int count, float spacing)
            => Enumerable.Range(0, count).Select(i => new Path(spline, (i - (count - 1) * .5f) * spacing)).ToArray();

        private static int Capacity(Path lane, float spacing) => Mathf.Max(1, Mathf.FloorToInt(lane.Length / spacing));

        public static int RowCapacity(MacaronLevel level, float diameter)
        {
            float spacing = Spacing(level.rowSpacing, diameter, level.loopSpacingMultiplier);
            return Lanes(level.conveyorPath, level.columns, Mathf.Max(level.laneSpacing, diameter + .015f)).Min(p => Capacity(p, spacing));
        }

        public static IEnumerable<(int index, Vector3 position, Quaternion rotation)> PreviewLayout(MacaronLevel level, float diameter)
        {
            float laneSpacing = Mathf.Max(level.laneSpacing, diameter + .015f);
            float spacing = Spacing(level.rowSpacing, diameter, level.loopSpacingMultiplier);
            var lanes = Lanes(level.conveyorPath, level.columns, laneSpacing);
            var colors = level.BuildMacaronOrder();
            int count = colors.Count;
            int rows = Mathf.CeilToInt((float)count / level.columns);
            var feeders = level.feederBranches.Select(f => new Path(f.GetComponent<SplineContainer>())).ToArray();
            int seed = !level.conveyorPath.Spline.Closed ? rows : feeders.Length > 0 ? 0 : lanes.Min(p => Capacity(p, spacing));
            if (level.conveyorPath.Spline.Closed && feeders.Length == 0 && rows > seed)
                throw new InvalidOperationException("The shortest lane cannot hold this supply without feeders.");
            var feederRows = MacaronLevel.BuildFeederAssignments(colors, level.columns, seed, feeders.Length);

            for (int row = 0; row < rows; row++)
            {
                for (int lane = 0; lane < Mathf.Min(level.columns, count - row * level.columns); lane++)
                {
                    if (row < seed)
                    {
                        float distance = level.conveyorPath.Spline.Closed ? row * lanes[lane].Length / Capacity(lanes[lane], spacing)
                            : (lanes[lane].Length - level.stopBeforeExit) * .5f - row * spacing;
                        if (distance < 0) continue;
                        lanes[lane].Pose(distance, out var p, out var rotation);
                        yield return (row * level.columns + lane, p, rotation);
                    }
                    else
                    {
                        int f = feederRows.feeder[row];
                        float distance = feeders[f].Distances[Mathf.Clamp(Mathf.RoundToInt(level.feederBranches[f].Branch.sweepTo * 512), 0, 512)]
                            - diameter * .55f - feederRows.index[row] * Spacing(level.rowSpacing, diameter, level.feederSpacingMultiplier);
                        if (distance < 0) continue;
                        feeders[f].Pose(distance, out var p, out var rotation);
                        int width = Mathf.Min(level.columns, count - row * level.columns);
                        yield return (row * level.columns + lane, p + rotation * Vector3.right * ((lane - (width - 1) * .5f) * laneSpacing), rotation);
                    }
                }
            }
        }

        public static bool IsInPickupWindow(float toGate, float gateLength, float travel, float loopLength)
            => loopLength > 0 && gateLength > 0 && (toGate <= gateLength || toGate >= loopLength - Mathf.Max(0, travel));
    }
}
