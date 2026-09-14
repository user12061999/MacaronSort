using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    public sealed class MacaronLoopFlow
    {
        private readonly MacaronAlignedLoopFlow _aligned;
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
            public float NearestDistance(Vector3 point)
            {
                float best = float.PositiveInfinity, distance = 0;
                for (int i = 1; i < Points.Length; i++)
                {
                    var segment = Points[i] - Points[i - 1];
                    float t = Mathf.Clamp01(Vector3.Dot(point - Points[i - 1], segment) / Mathf.Max(.000001f, segment.sqrMagnitude));
                    float squared = (point - Vector3.Lerp(Points[i - 1], Points[i], t)).sqrMagnitude;
                    if (squared >= best) continue;
                    best = squared;
                    distance = Mathf.Lerp(Distances[i - 1], Distances[i], t);
                }
                return distance;
            }
        }

        private readonly List<ConveyorBlock3D[]> _rows;
        private readonly Path[] _lanes, _feeders;
        private readonly ConveyorBlock3D[][] _slots;
        private readonly Queue<int>[] _queues;
        private readonly float[] _phase, _pitch, _mouth, _headGap;
        private readonly float[,] _join;
        private readonly float _laneSpacing, _feederSpacing;
        private readonly Dictionary<ConveyorBlock3D, int> _rowOf = new();
        private readonly Dictionary<ConveyorBlock3D, (Vector3 start, float time)> _merging = new();

        private static bool Alive(ConveyorBlock3D block) => block != null && !block.IsDestroyed;
        private static float Spacing(float spacing, float diameter, float multiplier)
            => Mathf.Max(diameter * 1.08f + .015f, spacing * Mathf.Clamp(multiplier, .5f, 1.5f));
        private static Path[] Lanes(SplineContainer spline, int count, float spacing)
            => Enumerable.Range(0, count).Select(i => new Path(spline, (i - (count - 1) * .5f) * spacing)).ToArray();
        private static int Capacity(Path lane, float spacing) => Mathf.Max(1, Mathf.FloorToInt(lane.Length / spacing));

        public MacaronLoopFlow(ConveyorController conveyor, List<ConveyorBlock3D[]> rows,
            List<BlockGroup> groups, ConveyorJunction[] feeders, float spacing, float laneSpacing, int lanes, float diameter,
            float loopSpacingMultiplier, float feederSpacingMultiplier, bool independentLanePacking = true)
        {
            if (!independentLanePacking)
            {
                _aligned = new MacaronAlignedLoopFlow(conveyor, rows, groups, feeders, spacing, laneSpacing, lanes, diameter,
                    loopSpacingMultiplier, feederSpacingMultiplier);
                return;
            }
            _rows = rows;
            _laneSpacing = laneSpacing;
            _feederSpacing = Spacing(spacing, diameter, feederSpacingMultiplier);
            _lanes = Lanes(conveyor.SplineContainer, lanes, laneSpacing);
            float desired = Spacing(spacing, diameter, loopSpacingMultiplier);
            _slots = _lanes.Select(p => new ConveyorBlock3D[Capacity(p, desired)]).ToArray();
            _phase = new float[lanes];
            _pitch = _lanes.Select((p, i) => p.Length / _slots[i].Length).ToArray();
            _feeders = feeders.Select(f => new Path(f.GetComponent<SplineContainer>())).ToArray();
            _queues = feeders.Select(f => new Queue<int>()).ToArray();
            _mouth = new float[feeders.Length];
            _headGap = new float[feeders.Length];
            _join = new float[feeders.Length, lanes];
            for (int f = 0; f < feeders.Length; f++)
            {
                _mouth[f] = _feeders[f].Distances[Mathf.Clamp(Mathf.RoundToInt(feeders[f].Branch.sweepTo * 512), 0, 512)] - diameter * .55f;
                for (int lane = 0; lane < lanes; lane++) _join[f, lane] = _lanes[lane].NearestDistance(_feeders[f].Points[^1]);
            }
            int seed = _slots.Min(s => s.Length);
            if (feeders.Length == 0 && rows.Count > seed)
                throw new InvalidOperationException("The shortest lane cannot hold this supply without feeders. Enlarge the loop or reduce supply.");
            for (int row = 0; row < rows.Count; row++)
            {
                groups[row].gameObject.SetActive(true);
                for (int lane = 0; lane < rows[row].Length; lane++)
                {
                    _rowOf[rows[row][lane]] = row;
                    if (row < seed) _slots[lane][row] = rows[row][lane];
                }
                if (row >= seed) _queues[(row - seed) % feeders.Length].Enqueue(row);
            }
            Place();
        }

        public int Tick(float speed, float gateLength, List<ConveyorBlock3D> pickup)
        {
            if (_aligned != null) return _aligned.Tick(speed, gateLength, pickup);
            float travel = Mathf.Max(0, speed) * Time.deltaTime;
            float remaining = travel;
            while (remaining > 0)
            {
                float step = Mathf.Min(remaining, _pitch.Min() * .2f);
                for (int lane = 0; lane < _lanes.Length; lane++)
                    _phase[lane] = Mathf.Repeat(_phase[lane] + step, _lanes[lane].Length);
                for (int f = 0; f < _feeders.Length; f++)
                {
                    _headGap[f] = Mathf.Max(0, _headGap[f] - step);
                    if (_queues[f].Count == 0 || _headGap[f] > .0001f) continue;
                    int row = _queues[f].Peek();
                    var candidates = new int[_rows[row].Length];
                    bool free = true;
                    for (int lane = 0; lane < candidates.Length; lane++)
                    {
                        // Reserve the next position just past the mouth in every lane before admitting a row.
                        int slot = Mathf.CeilToInt(Mathf.Repeat(_join[f, lane] - _phase[lane], _lanes[lane].Length) / _pitch[lane]) % _slots[lane].Length;
                        candidates[lane] = slot;
                        if (Alive(_slots[lane][slot])) free = false;
                    }
                    if (!free) continue;
                    _queues[f].Dequeue();
                    for (int lane = 0; lane < candidates.Length; lane++)
                    {
                        var block = _rows[row][lane];
                        _slots[lane][candidates[lane]] = block;
                        _merging[block] = (block.transform.position, Time.time);
                    }
                    _headGap[f] = _feederSpacing;
                }
                remaining -= step;
            }
            Place();
            pickup.Clear();
            var available = new List<(ConveyorBlock3D block, float distance)>();
            for (int lane = 0; lane < _lanes.Length; lane++)
                for (int slot = 0; slot < _slots[lane].Length; slot++)
                {
                    var block = _slots[lane][slot];
                    if (!Alive(block) || block.IsTargeted || _merging.ContainsKey(block)) continue;
                    float distance = Mathf.Repeat(-(_phase[lane] + slot * _pitch[lane]), _lanes[lane].Length);
                    if (IsInPickupWindow(distance, gateLength, travel, _lanes[lane].Length))
                        available.Add((block, distance > _lanes[lane].Length - travel ? distance - _lanes[lane].Length : distance));
                }
            pickup.AddRange(available.OrderBy(p => p.distance).Select(p => p.block));
            return pickup.Count == 0 ? -1 : _rowOf[pickup[0]];
        }

        public static bool IsInPickupWindow(float toGate, float gateLength, float travel, float loopLength)
            => loopLength > 0 && gateLength > 0 && (toGate <= gateLength || toGate >= loopLength - Mathf.Max(0, travel));

        private void Place()
        {
            foreach (var block in _merging.Keys.Where(b => !Alive(b)).ToArray()) _merging.Remove(block);
            for (int lane = 0; lane < _lanes.Length; lane++)
                for (int slot = 0; slot < _slots[lane].Length; slot++)
                {
                    var block = _slots[lane][slot];
                    if (!Alive(block)) continue;
                    _lanes[lane].Pose(Mathf.Repeat(_phase[lane] + slot * _pitch[lane], _lanes[lane].Length), out var position, out var rotation);
                    if (_merging.TryGetValue(block, out var merge))
                    {
                        float t = Mathf.Clamp01((Time.time - merge.time) / .22f);
                        position = Vector3.Lerp(merge.start, position, Mathf.SmoothStep(0, 1, t));
                        if (t >= 1) _merging.Remove(block);
                    }
                    block.gameObject.SetActive(true);
                    block.transform.SetPositionAndRotation(position, rotation);
                }
            for (int f = 0; f < _queues.Length; f++)
            {
                int index = 0;
                foreach (int row in _queues[f])
                {
                    float distance = _mouth[f] - _headGap[f] - index++ * _feederSpacing;
                    _feeders[f].Pose(distance, out var position, out var rotation);
                    for (int lane = 0; lane < _rows[row].Length; lane++)
                    {
                        var block = _rows[row][lane];
                        if (!Alive(block)) continue;
                        block.gameObject.SetActive(distance >= 0);
                        block.transform.SetPositionAndRotation(position + rotation * Vector3.right * ((lane - (_rows[row].Length - 1) * .5f) * _laneSpacing), rotation);
                    }
                }
            }
        }

        public bool HasReachableMatch(Predicate<ConveyorBlock3D> match)
        {
            if (_aligned != null) return _aligned.HasReachableMatch(match);
            if (_slots.Any(lane => lane.Any(b => Alive(b) && match(b)))) return true;
            return _queues.Any(q => q.Count > 0 && Enumerable.Range(0, _rows[q.Peek()].Length)
                .All(lane => _slots[lane].Any(b => !Alive(b))));
        }

        public static int RowCapacity(MacaronLevel level, float diameter)
        {
            if (!level.independentLanePacking) return MacaronAlignedLoopFlow.RowCapacity(level, diameter);
            float spacing = Spacing(level.rowSpacing, diameter, level.loopSpacingMultiplier);
            return Lanes(level.conveyorPath, level.columns, Mathf.Max(level.laneSpacing, diameter + .015f)).Min(p => Capacity(p, spacing));
        }

        public static IEnumerable<(int index, Vector3 position, Quaternion rotation)> PreviewLayout(MacaronLevel level, float diameter)
        {
            if (!level.independentLanePacking)
            {
                foreach (var pose in MacaronAlignedLoopFlow.PreviewLayout(level, diameter)) yield return pose;
                yield break;
            }
            float laneSpacing = Mathf.Max(level.laneSpacing, diameter + .015f);
            float spacing = Spacing(level.rowSpacing, diameter, level.loopSpacingMultiplier);
            var lanes = Lanes(level.conveyorPath, level.columns, laneSpacing);
            int count = level.BuildMacaronOrder().Count;
            int rows = Mathf.CeilToInt((float)count / level.columns);
            int seed = level.conveyorPath.Spline.Closed ? lanes.Min(p => Capacity(p, spacing)) : rows;
            var feeders = level.feederBranches.Select(f => new Path(f.GetComponent<SplineContainer>())).ToArray();
            if (level.conveyorPath.Spline.Closed && feeders.Length == 0 && rows > seed)
                throw new InvalidOperationException("The shortest lane cannot hold this supply without feeders.");
            for (int row = 0; row < rows; row++)
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
                        int f = (row - seed) % feeders.Length;
                        float distance = feeders[f].Distances[Mathf.Clamp(Mathf.RoundToInt(level.feederBranches[f].Branch.sweepTo * 512), 0, 512)]
                            - diameter * .55f - ((row - seed) / feeders.Length) * Spacing(level.rowSpacing, diameter, level.feederSpacingMultiplier);
                        if (distance < 0) continue;
                        feeders[f].Pose(distance, out var p, out var rotation);
                        int width = Mathf.Min(level.columns, count - row * level.columns);
                        yield return (row * level.columns + lane, p + rotation * Vector3.right * ((lane - (width - 1) * .5f) * laneSpacing), rotation);
                    }
                }
        }
    }
}
