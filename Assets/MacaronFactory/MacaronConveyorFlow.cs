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
        private readonly Vector3[][] _lanePoints;
        private readonly float[][] _laneDistances;
        private readonly float[][] _blockT;
        private readonly int _lanes;
        private readonly float _alignmentStartT;
        private readonly float _entryStraightLength;
        private readonly float[] _entryPhase;
        private float _progress;

        public IReadOnlyList<ConveyorBlock3D[]> Rows => _rows;
        public IReadOnlyList<ConveyorBlock3D> PickupBlocks => _pickupBlocks;
        public int LeadingRow { get; private set; } = -1;
        public bool IsStoppedAtExit { get; private set; }

        public MacaronConveyorFlow(ConveyorController conveyor, List<ConveyorBlock3D[]> rows,
            List<BlockGroup> groups, float rowSpacing, float initialFrontT, float laneSpacing,
            int lanes, float cakeDiameter)
        {
            _conveyor = conveyor;
            _rows = rows;
            _groups = groups;
            _lanes = lanes;
            // A small chord allowance keeps round cakes clear on the authored broad bends.
            _rowSpacing = Mathf.Max(rowSpacing, cakeDiameter * 1.08f + .015f);
            int samples = Mathf.Clamp(Mathf.CeilToInt(conveyor.SplineWorldLength / .015f), 32, 4096);
            int paths = lanes * 2 - 1; // Include centered offsets for an incomplete final row.
            _lanePoints = new Vector3[paths][];
            _laneDistances = new float[paths][];
            for (int lane = 0; lane < paths; lane++)
            {
                _lanePoints[lane] = new Vector3[samples + 1];
                _laneDistances[lane] = new float[samples + 1];
            }
            for (int i = 0; i <= samples; i++)
            {
                conveyor.SplineContainer.Spline.Evaluate((float)i / samples, out var position, out var tangent, out var up);
                Vector3 forward = conveyor.transform.TransformDirection((Vector3)tangent).normalized;
                Vector3 normal = conveyor.transform.TransformDirection((Vector3)up).normalized;
                if (normal == Vector3.zero) normal = Vector3.up;
                Vector3 right = Vector3.Cross(normal, forward).normalized;
                for (int lane = 0; lane < paths; lane++)
                {
                    var points = _lanePoints[lane];
                    points[i] = conveyor.transform.TransformPoint((Vector3)position) + right * ((lane - lanes + 1) * laneSpacing * .5f);
                    if (i > 0) _laneDistances[lane][i] = _laneDistances[lane][i - 1] + Vector3.Distance(points[i], points[i - 1]);
                }
            }
            _blockT = new float[rows.Count][];
            var centerPoints = _lanePoints[lanes - 1];
            Vector3 entryDirection = (centerPoints[1] - centerPoints[0]).normalized;
            _entryStraightLength = conveyor.SplineWorldLength;
            for (int i = 1; i < samples; i++)
                if (Vector3.Angle(centerPoints[i + 1] - centerPoints[i], entryDirection) > 1)
                { _entryStraightLength = _laneDistances[lanes - 1][i]; break; }
            _entryPhase = new float[paths];
            for (int lane = 0; lane < paths; lane++)
                _entryPhase[lane] = Mathf.Repeat(_laneDistances[lane][^1] - conveyor.SplineWorldLength, _rowSpacing);
            _alignmentStartT = FinalStraightStartT();
            for (int row = 0; row < rows.Count; row++) _blockT[row] = new float[rows[row].Length];
            _progress = initialFrontT * conveyor.SplineWorldLength;
            PlaceRows();
        }

        private void PlaceRows()
        {
            for (int row = 0; row < _rows.Count; row++)
            {
                var group = _groups[row];
                if (group == null) continue;
                float progress = _progress - row * _rowSpacing;
                // Retain package group tracking; final cake poses use their individual lane paths.
                _conveyor.SetGroupHeadT(group, Mathf.Clamp01(progress / _conveyor.SplineWorldLength));
                group.gameObject.SetActive(true);
                for (int lane = 0; lane < _rows[row].Length; lane++)
                {
                    var block = _rows[row][lane];
                    if (block == null || block.IsDestroyed) continue;
                    int path = _lanes - _rows[row].Length + lane * 2;
                    var distances = _laneDistances[path];
                    var points = _lanePoints[path];
                    // Equal remaining lane distance aligns logical rows naturally on the final straight.
                    float distance = progress + distances[^1] - _conveyor.SplineWorldLength;
                    // Match the visual grid at the entrance, then ease out its sub-row phase
                    // before the bend. Whole-row lane offsets remain logical grouping only.
                    float transition = Mathf.InverseLerp(Mathf.Max(0, _entryStraightLength - 2 * _rowSpacing),
                        _entryStraightLength, distance);
                    distance -= _entryPhase[path] * (1 - Mathf.SmoothStep(0, 1, transition));
                    bool visible = distance >= 0;
                    block.gameObject.SetActive(visible);
                    if (!visible) { _blockT[row][lane] = -1; continue; }
                    distance = Mathf.Min(distance, distances[^1]);
                    int index = Array.BinarySearch(distances, distance);
                    int upper = index >= 0 ? Mathf.Max(1, index) : ~index;
                    upper = Mathf.Min(upper, distances.Length - 1);
                    float fraction = Mathf.InverseLerp(distances[upper - 1], distances[upper], distance);
                    _blockT[row][lane] = (upper - 1 + fraction) / (distances.Length - 1);
                    Vector3 forward = points[upper] - points[upper - 1];
                    block.transform.SetPositionAndRotation(Vector3.Lerp(points[upper - 1], points[upper], fraction),
                        forward.sqrMagnitude > .000001f ? Quaternion.LookRotation(forward, _conveyor.transform.up) : Quaternion.identity);
                }
            }
        }

        public bool LeadingRowMatches(Predicate<ConveyorBlock3D> canCollect)
        {
            var row = _rows.Find(items => items.Any(block => block != null && !block.IsDestroyed));
            return row != null && row.Any(block => block != null && !block.IsDestroyed && !block.IsTargeted && canCollect(block));
        }

        public void Tick(float speed, float exitZoneLength, float stopBeforeExitDistance)
        {
            LeadingRow = _rows.FindIndex(row => row.Any(block => block != null && !block.IsDestroyed));
            _pickupBlocks.Clear();
            IsStoppedAtExit = false;
            if (LeadingRow < 0) return;
            float stop = Mathf.Max(0, _conveyor.SplineWorldLength - Mathf.Max(0, stopBeforeExitDistance));
            _progress = Mathf.Min(_progress + Mathf.Max(0, speed) * Time.deltaTime, stop + LeadingRow * _rowSpacing);
            PlaceRows();
            IsStoppedAtExit = _progress >= stop + LeadingRow * _rowSpacing - .0001f;
            // Wait until the whole leading row reaches the final straight before offering its cakes.
            float alignmentStart = Mathf.Min(_alignmentStartT, stop / _conveyor.SplineWorldLength);
            for (int lane = 0; lane < _rows[LeadingRow].Length; lane++)
            {
                var block = _rows[LeadingRow][lane];
                if (block != null && !block.IsDestroyed && _blockT[LeadingRow][lane] < alignmentStart) return;
            }
            for (int lane = 0; lane < _rows[LeadingRow].Length; lane++)
            {
                var block = _rows[LeadingRow][lane];
                if (block != null && !block.IsDestroyed && !block.IsTargeted &&
                    IsInExitZone(_blockT[LeadingRow][lane], _conveyor.SplineWorldLength, exitZoneLength, stopBeforeExitDistance))
                    _pickupBlocks.Add(block);
            }
            // Closest to the exit first; retain lane order when cakes are aligned.
            _pickupBlocks.Sort((a, b) =>
            {
                int laneA = Array.IndexOf(_rows[LeadingRow], a), laneB = Array.IndexOf(_rows[LeadingRow], b);
                int order = _blockT[LeadingRow][laneB].CompareTo(_blockT[LeadingRow][laneA]);
                return order != 0 ? order : laneA.CompareTo(laneB);
            });
        }

        private float FinalStraightStartT()
        {
            var points = _lanePoints[_lanes - 1];
            Vector3 endDirection = (points[^1] - points[^2]).normalized;
            for (int i = points.Length - 2; i > 0; i--)
                if (Vector3.Angle(points[i] - points[i - 1], endDirection) > 1)
                    return (float)(i + 1) / (points.Length - 1);
            return 0;
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
