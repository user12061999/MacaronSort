using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Owns the Macaron conveyor only: rows move toward the spline endpoint,
    /// the leading live row stops there, and only that row may enter the exit zone.
    /// </summary>
    public sealed class MacaronConveyorFlow
    {
        private readonly ConveyorController _conveyor;
        private readonly List<ConveyorBlock3D[]> _rows;
        private readonly List<BlockGroup> _groups;
        private readonly List<ConveyorBlock3D> _pickupBlocks = new();
        private readonly float _rowSpacing;
        private int _nextQueuedRow;

        public IReadOnlyList<ConveyorBlock3D[]> Rows => _rows;
        public IReadOnlyList<ConveyorBlock3D> PickupBlocks => _pickupBlocks;
        public int LeadingRow { get; private set; } = -1;
        public bool IsStoppedAtExit { get; private set; }

        private readonly float[] _travel;

        public MacaronConveyorFlow(ConveyorController conveyor, List<ConveyorBlock3D[]> rows,
            List<BlockGroup> groups, float rowSpacing, float initialFrontT, float laneSpacing,
            int lanes, float cakeDiameter)
        {
            _conveyor = conveyor;
            _rows = rows;
            _groups = groups;
            _rowSpacing = Mathf.Max(rowSpacing, cakeDiameter + .015f);
            float length = conveyor.SplineWorldLength;
            int samples = Mathf.Clamp(Mathf.CeilToInt(length / .02f), 2, 4096);
            _travel = new float[samples + 1];
            Vector3 previousForward = Vector3.zero;
            float halfWidth = (lanes - 1) * laneSpacing * .5f;
            for (int i = 0; i <= samples; i++)
            {
                conveyor.SplineContainer.Spline.Evaluate((float)i / samples, out _, out var tangent, out _);
                Vector3 forward = conveyor.transform.TransformDirection((Vector3)tangent).normalized;
                if (i > 0)
                {
                    float step = length / samples;
                    float curvature = Vector3.Angle(previousForward, forward) * Mathf.Deg2Rad / Mathf.Max(.0001f, step);
                    // Concentric lanes share the row's angle. Only the inside of a bend
                    // needs extra angular separation; straight sections keep the configured pitch.
                    // ponytail: sampled planar curvature assumes the inner lane radius stays positive;
                    // widen an authored bend whose radius is smaller than the belt half-width.
                    float innerRatio = Mathf.Max(.05f, 1 - curvature * halfWidth);
                    float bendScale = Mathf.Max(1, (cakeDiameter + .015f) / (_rowSpacing * innerRatio));
                    _travel[i] = _travel[i - 1] + step * bendScale;
                }
                previousForward = forward;
            }
            float frontTravel = TravelAt(initialFrontT);
            _nextQueuedRow = Mathf.Min(groups.Count, Mathf.Max(1, Mathf.FloorToInt(frontTravel / _rowSpacing) + 1));
            for (int row = 0; row < groups.Count; row++)
            {
                conveyor.SetGroupHeadT(groups[row], TAt(Mathf.Max(0, frontTravel - row * _rowSpacing)));
                groups[row].gameObject.SetActive(row < _nextQueuedRow);
            }
        }

        private float TravelAt(float t)
        {
            float index = Mathf.Clamp01(t) * (_travel.Length - 1);
            int lower = Mathf.Min(Mathf.FloorToInt(index), _travel.Length - 2);
            return Mathf.Lerp(_travel[lower], _travel[lower + 1], index - lower);
        }

        private float TAt(float travel)
        {
            int index = Array.BinarySearch(_travel, Mathf.Clamp(travel, 0, _travel[^1]));
            if (index >= 0) return (float)index / (_travel.Length - 1);
            int upper = ~index;
            return (upper - 1 + Mathf.InverseLerp(_travel[upper - 1], _travel[upper], travel)) / (_travel.Length - 1);
        }

        public void Tick(float speed, float exitZoneLength, float stopBeforeExitDistance)
        {
            LeadingRow = FindLeadingRow(stopBeforeExitDistance);
            float distance = Mathf.Max(0, speed) * Time.deltaTime;
            if (LeadingRow >= 0)
            {
                float stopT = Mathf.Clamp01(1 - stopBeforeExitDistance / _conveyor.SplineWorldLength);
                distance = Mathf.Min(distance, Mathf.Max(0, TravelAt(stopT) - TravelAt(_conveyor.GetGroupHeadT(_groups[LeadingRow]))));
            }
            for (int row = 0; row < _nextQueuedRow; row++)
                if (_groups[row] != null && _groups[row].gameObject.activeSelf)
                    _conveyor.SetGroupHeadT(_groups[row], TAt(TravelAt(_conveyor.GetGroupHeadT(_groups[row])) + distance));
            RevealQueuedRows();

            LeadingRow = FindLeadingRow(stopBeforeExitDistance);
            _pickupBlocks.Clear();
            if (LeadingRow < 0) return;
            float currentT = _conveyor.GetGroupHeadT(_groups[LeadingRow]);
            IsStoppedAtExit = DistanceToExit(currentT, _conveyor.SplineWorldLength, stopBeforeExitDistance) == 0;
            if (!IsInExitZone(currentT, _conveyor.SplineWorldLength, exitZoneLength, stopBeforeExitDistance)) return;
            foreach (var block in _rows[LeadingRow])
                if (block != null && !block.IsDestroyed && !block.IsTargeted) _pickupBlocks.Add(block);
        }

        private int FindLeadingRow(float stopBeforeExitDistance)
        {
            int leadingRow = -1;
            float shortestDistance = float.PositiveInfinity;
            for (int row = 0; row < _nextQueuedRow; row++)
            {
                var group = _groups[row];
                if (group == null || !group.gameObject.activeSelf || !_rows[row].Any(b => b != null && !b.IsDestroyed)) continue;
                float distance = DistanceToExit(_conveyor.GetGroupHeadT(group), _conveyor.SplineWorldLength, stopBeforeExitDistance);
                if (distance >= shortestDistance) continue;
                leadingRow = row;
                shortestDistance = distance;
            }
            return leadingRow;
        }

        private void RevealQueuedRows()
        {
            // ponytail: this checks the small visible queue; use a free-slot index for very long conveyors.
            while (_nextQueuedRow < _groups.Count)
            {
                var queued = _groups[_nextQueuedRow];
                bool occupied = _groups.Any(group => group != null && group.gameObject.activeSelf &&
                    TravelAt(_conveyor.GetGroupHeadT(group)) < _rowSpacing - .0001f);
                if (occupied) return;
                _conveyor.SetGroupHeadT(queued, 0);
                queued.gameObject.SetActive(true);
                _nextQueuedRow++;
            }
        }

        public static float DistanceToExit(float currentT, float conveyorLength, float stopBeforeExitDistance = 0)
        {
            if (conveyorLength <= 0 || currentT < 0) return 0;
            float stopT = Mathf.Clamp01(1 - Mathf.Max(0, stopBeforeExitDistance) / conveyorLength);
            float distance = (stopT - Mathf.Clamp01(currentT)) * conveyorLength;
            return distance < .0001f ? 0 : distance;
        }

        public static bool IsInExitZone(float currentT, float conveyorLength, float exitZoneLength,
            float stopBeforeExitDistance = 0)
        {
            if (conveyorLength <= 0 || currentT < 0 || exitZoneLength <= 0) return false;
            float stopDistance = Mathf.Max(0, conveyorLength - stopBeforeExitDistance);
            float currentDistance = currentT * conveyorLength;
            return currentDistance >= stopDistance - exitZoneLength - .0001f && currentDistance <= stopDistance + .0001f;
        }
    }
}
