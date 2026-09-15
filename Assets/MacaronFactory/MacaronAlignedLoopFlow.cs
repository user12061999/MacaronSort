using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    // Ring positions are reserved per row. Feeders can fill only an empty passing position.
    internal sealed class MacaronAlignedLoopFlow
    {
        private static readonly Unity.Profiling.ProfilerMarker TickMarker = new("Macaron.AlignedLoop.Tick");
        private readonly List<ConveyorBlock3D[]> _rows;
        private readonly Path _ring;
        private readonly Path[] _feeders;
        private readonly Queue<int>[] _queues;
        private readonly float[] _join, _headGap, _mouth, _mergeProgress;
        private readonly Vector3[][] _mergeStart;
        private readonly int[] _occupants;
        private readonly float _spacing, _feederSpacing, _laneSpacing;
        private float _phase;

        private sealed class Path
        {
            public readonly Vector3[] Points = new Vector3[513], Rights = new Vector3[513], Forwards = new Vector3[513];
            public readonly float[] Distances = new float[513];
            public float Length => Distances[^1];
            private float[][] _laneDistances;
            private float _laneSpacing;
            private int _lanes, _alignedStart, _alignedEnd;
            public float AlignmentLength => Length - Distances[_alignedEnd];

            public float PrepareLanes(float spacing, int lanes)
            {
                _laneSpacing = spacing;
                _lanes = lanes;
                _alignedStart = 1;
                _alignedEnd = Points.Length - 2;
                // Align only on the straight surrounding the collection seam, not through bends.
                while (_alignedStart < Points.Length / 4 && Vector3.Dot(Forwards[0], Forwards[_alignedStart + 1]) > .999f)
                    _alignedStart++;
                while (_alignedEnd > Points.Length * 3 / 4 && Vector3.Dot(Forwards[0], Forwards[_alignedEnd - 1]) > .999f)
                    _alignedEnd--;
                _laneDistances = new float[lanes * 2 - 1][];
                float ratio = 1;
                for (int lane = 0; lane < _laneDistances.Length; lane++)
                {
                    float offset = (lane - lanes + 1) * spacing * .5f;
                    var distances = _laneDistances[lane] = new float[Points.Length];
                    for (int i = 1; i < Points.Length; i++)
                        distances[i] = distances[i - 1] + Vector3.Distance(Points[i] + Rights[i] * offset,
                            Points[i - 1] + Rights[i - 1] * offset);
                    if (!float.IsFinite(distances[^1]) || distances[_alignedEnd] <= distances[_alignedStart])
                        throw new InvalidOperationException("Conveyor lane has no usable length. Check the spline and lane spacing.");
                    ratio = Mathf.Min(ratio, (distances[_alignedEnd] - distances[_alignedStart]) /
                        Mathf.Max(.001f, Distances[_alignedEnd] - Distances[_alignedStart]));
                }
                return ratio;
            }

            public void LanePose(float progress, int lane, out Vector3 position, out Quaternion rotation)
            {
                var distances = _laneDistances[lane];
                float arc;
                if (progress >= Distances[_alignedStart] && progress <= Distances[_alignedEnd])
                    arc = Mathf.Lerp(distances[_alignedStart], distances[_alignedEnd],
                        Mathf.InverseLerp(Distances[_alignedStart], Distances[_alignedEnd], progress));
                else
                {
                    int index = Upper(Distances, progress);
                    arc = Mathf.Lerp(distances[index - 1], distances[index],
                        Mathf.InverseLerp(Distances[index - 1], Distances[index], progress));
                }
                int hi = Upper(distances, arc);
                float fraction = Mathf.InverseLerp(distances[hi - 1], distances[hi], arc);
                float offset = (lane - _lanes + 1) * _laneSpacing * .5f;
                position = Vector3.Lerp(Points[hi - 1] + Rights[hi - 1] * offset, Points[hi] + Rights[hi] * offset, fraction);
                var forward = Vector3.Lerp(Forwards[hi - 1], Forwards[hi], fraction).normalized;
                var right = Vector3.Lerp(Rights[hi - 1], Rights[hi], fraction).normalized;
                rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward, right));
            }

            private static int Upper(float[] distances, float distance)
            {
                int index = Array.BinarySearch(distances, distance);
                return Mathf.Clamp(index >= 0 ? index : ~index, 1, distances.Length - 1);
            }
            public Path(SplineContainer spline)
            {
                for (int i = 0; i < Points.Length; i++)
                {
                    spline.Spline.Evaluate(i / 512f, out var p, out var tangent, out var up);
                    Points[i] = spline.transform.TransformPoint((Vector3)p);
                    Forwards[i] = spline.transform.TransformDirection((Vector3)tangent).normalized;
                    Rights[i] = Vector3.Cross(spline.transform.TransformDirection((Vector3)up),
                        spline.transform.TransformDirection((Vector3)tangent)).normalized;
                    if (i > 0) Distances[i] = Distances[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
                }
            }
            public void Place(ConveyorBlock3D[] row, float distance, float laneSpacing)
            {
                GetPose(distance, out var center, out var right, out var rotation);
                for (int lane = 0; lane < row.Length; lane++)
                {
                    var block = row[lane];
                    if (block == null || block.IsDestroyed) continue;
                    if (block.gameObject.activeSelf != (distance >= 0)) block.gameObject.SetActive(distance >= 0);
                    if (distance < 0) continue;
                    if (_laneDistances != null)
                    {
                        LanePose(distance, _lanes - row.Length + lane * 2, out var position, out var laneRotation);
                        block.transform.SetPositionAndRotation(position, laneRotation);
                    }
                    else block.transform.SetPositionAndRotation(center + right * ((lane - (row.Length - 1) * .5f) * laneSpacing), rotation);
                }
            }
            public void GetPose(float distance, out Vector3 center, out Vector3 right, out Quaternion rotation)
            {
                int hi = Array.BinarySearch(Distances, Mathf.Clamp(distance, 0, Length));
                hi = Mathf.Clamp(hi >= 0 ? hi : ~hi, 1, Distances.Length - 1);
                float t = Mathf.InverseLerp(Distances[hi - 1], Distances[hi], distance);
                center = Vector3.Lerp(Points[hi - 1], Points[hi], t);
                right = Vector3.Lerp(Rights[hi - 1], Rights[hi], t).normalized;
                var forward = Vector3.Lerp(Forwards[hi - 1], Forwards[hi], t).normalized;
                rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward, right));
            }
        }

        public MacaronAlignedLoopFlow(ConveyorController conveyor, List<ConveyorBlock3D[]> rows,
            List<BlockGroup> groups, ConveyorJunction[] feeders, float spacing, float laneSpacing, int lanes, float diameter,
            float loopSpacingMultiplier, float feederSpacingMultiplier)
        {
            _rows = rows;
            _laneSpacing = laneSpacing;
            _feederSpacing = Mathf.Max(spacing, diameter + .01f) * Mathf.Clamp(feederSpacingMultiplier, .5f, 1.5f);
            _ring = new Path(conveyor.SplineContainer);
            int capacity = Capacity(_ring, spacing, laneSpacing, lanes, diameter, loopSpacingMultiplier);
            if (feeders.Length == 0 && rows.Count > capacity)
                throw new InvalidOperationException("The ring cannot hold all cakes without feeders. Enlarge the loop or reduce supply.");
            _spacing = _ring.Length / capacity;
            _occupants = Enumerable.Repeat(-1, capacity).ToArray();
            _feeders = new Path[feeders.Length];
            _queues = new Queue<int>[feeders.Length];
            _headGap = new float[feeders.Length];
            _mouth = new float[feeders.Length];
            _mergeProgress = Enumerable.Repeat(1f, rows.Count).ToArray();
            _mergeStart = new Vector3[rows.Count][];
            _join = new float[feeders.Length];
            for (int f = 0; f < feeders.Length; f++)
            {
                _feeders[f] = new Path(feeders[f].GetComponent<SplineContainer>());
                _mouth[f] = _feeders[f].Distances[Mathf.Clamp(Mathf.RoundToInt(feeders[f].Branch.sweepTo * 512), 0, 512)]
                    - diameter * .55f;
                _queues[f] = new Queue<int>();
                var main = conveyor.SplineContainer;
                SplineUtility.GetNearestPoint(main.Spline, (Unity.Mathematics.float3)main.transform.InverseTransformPoint(
                    _feeders[f].Points[^1]), out var nearest, out float joinT, 32, 6);
                UnityEngine.Assertions.Assert.IsTrue((main.transform.TransformPoint((Vector3)nearest) - _feeders[f].Points[^1]).sqrMagnitude < .01f,
                    "Feeder endpoint must meet the ring centreline.");
                float sample = Mathf.Clamp01(joinT) * (_ring.Distances.Length - 1);
                int lo = Mathf.Min(Mathf.FloorToInt(sample), _ring.Distances.Length - 2);
                _join[f] = Mathf.Lerp(_ring.Distances[lo], _ring.Distances[lo + 1], sample - lo);
            }
            int seed = Mathf.Min(rows.Count, capacity);
            for (int row = 0; row < rows.Count; row++)
            {
                groups[row].gameObject.SetActive(true);
                if (row < seed) _occupants[row] = row;
                else _queues[(row - seed) % feeders.Length].Enqueue(row);
            }
            Place();
        }

        private static int Capacity(Path ring, float spacing, float laneSpacing, int lanes, float diameter, float multiplier)
        {
            // Use the full lane arc instead of the tightest corner to set the shared row clock.
            float ratio = ring.PrepareLanes(laneSpacing, lanes);
            float ringSpacing = Mathf.Max(diameter * 1.08f + .015f, spacing * Mathf.Clamp(multiplier, .5f, 1.5f))
                / Mathf.Max(.1f, ratio);
            return Mathf.Max(2, Mathf.FloorToInt(ring.Length / ringSpacing));
        }

        public static int RowCapacity(MacaronLevel level, float diameter) => Capacity(new Path(level.conveyorPath),
            Mathf.Max(level.rowSpacing, diameter * 1.08f + .015f), Mathf.Max(level.laneSpacing, diameter + .015f),
            level.columns, diameter, level.loopSpacingMultiplier);

        public static IEnumerable<(int index, Vector3 position, Quaternion rotation)> PreviewLayout(MacaronLevel level, float diameter)
        {
            var ring = new Path(level.conveyorPath);
            float lanes = Mathf.Max(level.laneSpacing, diameter + .015f);
            float spacing = Mathf.Max(level.rowSpacing, diameter * 1.08f + .015f);
            int count = level.BuildMacaronOrder().Count;
            int rows = Mathf.CeilToInt((float)count / level.columns);
            int capacity = Capacity(ring, spacing, lanes, level.columns, diameter, level.loopSpacingMultiplier);
            int seed = Mathf.Min(rows, capacity);
            if (!level.conveyorPath.Spline.Closed) seed = rows;
            var feeders = level.feederBranches.Select(f => new Path(f.GetComponent<SplineContainer>())).ToArray();
            if (level.conveyorPath.Spline.Closed && feeders.Length == 0 && rows > capacity)
                throw new InvalidOperationException("The ring cannot hold all cakes without feeders. Enlarge the loop or reduce supply.");
            for (int row = 0; row < rows; row++)
            {
                Path path = ring;
                float distance;
                if (!level.conveyorPath.Spline.Closed) distance = (ring.Length - level.stopBeforeExit) * .5f - row * spacing;
                else if (row < seed) distance = row * ring.Length / capacity;
                else
                {
                    int f = (row - seed) % feeders.Length;
                    path = feeders[f];
                    distance = path.Distances[Mathf.Clamp(Mathf.RoundToInt(level.feederBranches[f].Branch.sweepTo * 512), 0, 512)]
                        - diameter * .55f - ((row - seed) / feeders.Length) * Mathf.Max(spacing, diameter + .01f)
                        * Mathf.Clamp(level.feederSpacingMultiplier, .5f, 1.5f);
                }
                if (distance < 0) continue;
                path.GetPose(distance, out var center, out var right, out var rotation);
                int width = Mathf.Min(level.columns, count - row * level.columns);
                for (int lane = 0; lane < width; lane++)
                {
                    if (path == ring && level.conveyorPath.Spline.Closed)
                    {
                        ring.LanePose(distance, level.columns - width + lane * 2, out var position, out var laneRotation);
                        yield return (row * level.columns + lane, position, laneRotation);
                    }
                    else yield return (row * level.columns + lane, center + right * ((lane - (width - 1) * .5f) * lanes), rotation);
                }
            }
        }

        private bool Alive(int row)
        {
            if (row < 0) return false;
            foreach (var block in _rows[row]) if (block != null && !block.IsDestroyed) return true;
            return false;
        }

        public int Tick(float speed, float gateLength, List<ConveyorBlock3D> pickup)
        {
            using var sample = TickMarker.Auto();
            gateLength = Mathf.Min(gateLength, _ring.AlignmentLength);
            for (int row = 0; row < _mergeProgress.Length; row++)
                _mergeProgress[row] = Mathf.Min(1, _mergeProgress[row] + Time.deltaTime / .22f);
            for (int slot = 0; slot < _occupants.Length; slot++)
                if (!Alive(_occupants[slot])) _occupants[slot] = -1;
            float travel = Mathf.Max(0, speed) * Time.deltaTime;
            float remaining = travel;
            // Small distance steps keep admission deterministic even across a long frame.
            while (remaining > 0)
            {
                float step = Mathf.Min(remaining, _spacing * .2f);
                for (int f = 0; f < _feeders.Length; f++)
                {
                    _headGap[f] = Mathf.Max(0, _headGap[f] - step);
                    if (_queues[f].Count == 0 || _headGap[f] > .0001f) continue;
                    for (int slot = 0; slot < _occupants.Length; slot++)
                    {
                        if (_occupants[slot] >= 0) continue;
                        float distance = Mathf.Repeat(_join[f] - (_phase + slot * _spacing), _ring.Length);
                        if (distance > step) continue;
                        int row = _queues[f].Dequeue();
                        _occupants[slot] = row;
                        _mergeProgress[row] = 0;
                        _mergeStart[row] ??= new Vector3[_rows[row].Length];
                        for (int lane = 0; lane < _rows[row].Length; lane++)
                            _mergeStart[row][lane] = _rows[row][lane].transform.position;
                        _headGap[f] = _feederSpacing;
                        break;
                    }
                }
                _phase = Mathf.Repeat(_phase + step, _ring.Length);
                remaining -= step;
            }
            Place();
            pickup.Clear();
            int front = -1;
            float closest = float.PositiveInfinity;
            for (int slot = 0; slot < _occupants.Length; slot++)
            {
                if (_occupants[slot] < 0) continue;
                float toGate = Mathf.Repeat(-(_phase + slot * _spacing), _ring.Length);
                if (!IsInPickupWindow(toGate, gateLength, travel, _ring.Length)) continue;
                foreach (var block in _rows[_occupants[slot]])
                    if (block != null && !block.IsDestroyed && !block.IsTargeted) pickup.Add(block);
                if (toGate < closest)
                {
                    closest = toGate;
                    front = _occupants[slot];
                }
            }
            return front;
        }

        // Include rows that crossed the seam during this frame, even at low frame rates.
        public static bool IsInPickupWindow(float toGate, float gateLength, float travel, float loopLength)
            => loopLength > 0 && gateLength > 0 &&
               (toGate <= gateLength || toGate >= loopLength - Mathf.Max(0, travel));

        private void Place()
        {
            for (int slot = 0; slot < _occupants.Length; slot++)
                if (_occupants[slot] >= 0)
                {
                    int row = _occupants[slot];
                    _ring.Place(_rows[row], Mathf.Repeat(_phase + slot * _spacing, _ring.Length), _laneSpacing);
                    if (_mergeProgress[row] < 1)
                        for (int lane = 0; lane < _rows[row].Length; lane++)
                        {
                            var block = _rows[row][lane];
                            if (block == null || block.IsDestroyed) continue;
                            block.transform.position = Vector3.Lerp(_mergeStart[row][lane], block.transform.position,
                                Mathf.SmoothStep(0, 1, _mergeProgress[row]));
                        }
                }
            for (int f = 0; f < _queues.Length; f++)
            {
                int index = 0;
                foreach (int row in _queues[f])
                    _feeders[f].Place(_rows[row], _mouth[f] - _headGap[f] - index++ * _feederSpacing, _laneSpacing);
            }
        }

        public bool HasReachableMatch(Predicate<ConveyorBlock3D> match)
        {
            foreach (int row in _occupants)
                if (row >= 0)
                    foreach (var block in _rows[row])
                        if (block != null && !block.IsDestroyed && match(block)) return true;
            // If the ring can admit more rows, let it fill before declaring a jam.
            foreach (int row in _occupants)
                if (!Alive(row))
                {
                    foreach (var queue in _queues) if (queue.Count > 0) return true;
                    break;
                }
            return false;
        }
    }
}
