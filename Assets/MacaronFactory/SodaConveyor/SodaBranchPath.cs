// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/SodaBranchPath.cs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.SodaConveyor
{
    public sealed class SodaBranchPath : MonoBehaviour
    {
        struct Row
        {
            public BlockColorType Color;
            public ConveyorBlock3D[] Items;
            public int RowIndex; // within its StageGroupSpec, mirrors what SetGroupIndex is given
            public float CurrentT;
            public float RowSpacing;
        }

        SodaConveyorTrack _track;
        SplineContainer _splineContainer;
        float _splineLength;
        float _mergeT;
        float _mergeStopT = 0.95f;
        Vector3 _mainMergeWorldPos;

        readonly List<Row> _rows = new();

        [System.Serializable]
        public struct SavedRow { public BlockColorType color; public float t; }

        public SavedRow[] CaptureRows()
        {
            return _rows.ConvertAll(row => new SavedRow { color = row.Color, t = row.CurrentT }).ToArray();
        }

        public void RestoreRows(SavedRow[] saved)
        {
            foreach (var row in _rows)
                if (row.Items != null)
                    foreach (var item in row.Items)
                        if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
            _rows.Clear();
            for (int i = 0; i < saved.Length; i++)
            {
                var row = new Row { Color = saved[i].color, CurrentT = saved[i].t,
                    RowIndex = i, RowSpacing = _track.RowSpacing };
                PlaceRow(ref row);
                _rows.Add(row);
            }
        }

        public bool IsFullyMerged => _rows.Count == 0;
        public bool HasMatchingColor(System.Func<BlockColorType, bool> needsColor)
        {
            if (needsColor == null) return false;
            // Every groupSpec queued onto this branch gets its own run of rows, and different
            // groupSpecs can carry different colors (Setup() above rents each row's lanes from
            // groupSpec.Color per group) — so a color further back can differ from the front
            // row's. All rows must be scanned; stopping at the front row (as this used to)
            // reports "no match" for a color that is genuinely still queued on the branch, just
            // not in the next row to merge, which made stuck-truck detection think the color
            // was gone entirely.
            foreach (var row in _rows)
            {
                if (needsColor(row.Color)) return true;
            }
            return false;
        }
        public void Setup(SodaConveyorTrack track, StageBranchSpec spec, float scale, float height)
        {
            _track = track;

            _splineContainer = gameObject.GetComponent<SplineContainer>();
            if (_splineContainer == null) _splineContainer = gameObject.AddComponent<SplineContainer>();
            _splineContainer.Spline = spec.BuildSpline(scale, height);
            _splineLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);
            _mergeT = spec.MergeT;

            var mainOuterRadius = _track.OuterRadius;
            var laneSpacing = _track.LaneSpacing;
            var rowSpacing = _track.RowSpacing;

            _mainMergeWorldPos = _track.EvaluateWorld(_mergeT, out _);

            // Measure the final approach in world space; normalized spline T is not distance.
            // Only the cake radius needs clearance along the approach, not half the entire row.
            float clearance = mainOuterRadius + _track.ItemRadius + .015f;
            _mergeStopT = FindMergeStop(_mainMergeWorldPos, clearance);

            _rows.Clear();
            var globalRowIndex = 0;
            foreach (var groupSpec in spec.Groups)
            {
                for (var r = 0; r < groupSpec.RowCount; r++)
                {
                    // Deliberately NOT clamped to 0 (unlike BranchPath.Initialize): a branch can
                    // carry far more rows than its spline is long (StageLayout appends the main
                    // loop's surplus and the level's extra content here), and clamping would pile
                    // every overflow row onto the spline's start. Rows with T < 0 wait "off the
                    // end" with NO ConveyorBlock3Ds rented at all — PlaceRow rents them from the pool
                    // the moment the row crawls on, so a 100-row branch costs as many live cans
                    // as fit on its spline, not 400 inactive objects up front.
                    var initialT = _mergeStopT - globalRowIndex * rowSpacing / Mathf.Max(0.01f, _splineLength);
                    _rows.Add(new Row { Color = groupSpec.Color, RowIndex = r, CurrentT = initialT, RowSpacing = rowSpacing });
                    globalRowIndex++;
                }
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                PlaceRow(ref row);
                _rows[i] = row;
            }
        }
        private float FindMergeStop(Vector3 mergePosition, float clearance)
        {
            float Distance(float t)
            {
                _splineContainer.Spline.Evaluate(t, out var p, out _, out _);
                return Vector3.Distance(transform.TransformPoint((Vector3)p), mergePosition);
            }
            if (Distance(1) >= clearance) return 1;
            // Find the last crossing even when a curved branch has earlier bends near the loop.
            for (int i = 127; i >= 0; i--)
            {
                float lo = i / 128f, hi = (i + 1) / 128f;
                if (Distance(lo) < clearance) continue;
                for (int step = 0; step < 16; step++)
                {
                    float mid = (lo + hi) * .5f;
                    if (Distance(mid) >= clearance) lo = mid; else hi = mid;
                }
                return lo;
            }
            return 0;
        }

        public void ReleaseAllAndDestroy()
        {
            foreach (var row in _rows)
            {
                if (row.Items == null) continue;
                foreach (var item in row.Items)
                {
                    if (item != null)
                        Destroy(item.gameObject);
                }
            }
            _rows.Clear();
            Destroy(gameObject);
        }

        public void Advance(float deltaTime)
        {
            if (_track == null || !_track.enabled || _rows.Count == 0) return;

            var delta = _splineLength > 0f ? _track.Speed * 1.25f / _splineLength * deltaTime : 0f;

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var maxT = i == 0 ? _mergeStopT : _rows[i - 1].CurrentT - row.RowSpacing / Mathf.Max(0.01f, _splineLength);
                row.CurrentT = Mathf.Min(row.CurrentT + delta, maxT);
                PlaceRow(ref row);
                _rows[i] = row;
            }

            TryMergeFrontRow();
        }

        void PlaceRow(ref Row row)
        {
            if (_splineContainer == null || _splineLength <= 0f) return;

            // Overflow rows (see Setup) sit at T < 0, off the spline's far end, and own no
            // ConveyorBlock3Ds yet. Rows only ever move forward, so the first frame a row reaches T ≥ 0
            // is the one time it rents its cans; from then on it is placed like any other.
            if (row.CurrentT < 0f) return;
            if (row.Items == null)
            {
                row.Items = new ConveyorBlock3D[StageGroupSpec.LaneCount];
                for (var lane = 0; lane < StageGroupSpec.LaneCount; lane++)
                {
                    var item = _track.SpawnItem(row.Color, transform);
                    item.Phase = ConveyorItemPhase.OnBranch;
                    item.SetGroupIndex(row.RowIndex, lane);
                    row.Items[lane] = item;
                }
            }

            _splineContainer.Spline.Evaluate(row.CurrentT, out var pos, out var tangent, out var up);
            var worldPos = transform.TransformPoint(pos);
            var fwd = transform.TransformDirection((Vector3)tangent).normalized;
            var upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir.sqrMagnitude < 1e-4f) upDir = Vector3.up;
            var right = Vector3.Cross(upDir, fwd).normalized;
            var rot = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

            var laneSpacing = _track.LaneSpacing;
            for (var lane = 0; lane < row.Items.Length; lane++)
            {
                var item = row.Items[lane];
                if (item == null || item.Phase != ConveyorItemPhase.OnBranch) continue;
                var xOff = (lane - (StageGroupSpec.LaneCount - 1) * 0.5f) * laneSpacing;
                item.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
            }
        }

        void TryMergeFrontRow()
        {
            if (_rows.Count == 0) return;
            var front = _rows[0];
            if (front.CurrentT < _mergeStopT - 0.001f) return;

            if (front.Items == null) return;
            var searchRadius = front.RowSpacing * 1.5f;
            var slotT = _track.FindClosestFreeSlotNearWorldPos(_mainMergeWorldPos, searchRadius);
            if (slotT < 0f) return; // no gap yet — wait, the belt keeps bringing new ones around

            var dT = front.RowSpacing / Mathf.Max(0.01f, _track.SplineWorldLength);
            var slotIdx = _track.ClaimNearestSlot(slotT, dT * 0.5f);
            if (slotIdx < 0) return;


            var mergedGroup = _track.CreateMergeGroup(front.Color);
            _track.InsertGroupAt(mergedGroup, slotT);

            for (var lane = 0; lane < front.Items.Length; lane++)
            {
                var item = front.Items[lane];
                if (item == null) continue;

                item.JumpStartPos = item.transform.position;
                item.JumpStartRot = item.transform.rotation;
                item.JumpProgress = 0f;

                item.transform.SetParent(mergedGroup.transform, true);
                item.SetGroupIndex(0, lane);
                item.Phase = ConveyorItemPhase.OnLoop;
                mergedGroup.RegisterMergedItem(item, lane);
                _track.RegisterItemToSlot(slotIdx, lane, item);
                _track.Add(item);
            }

            _track.ForceUpdateGroupPosition(mergedGroup);
            _rows.RemoveAt(0);
        }
    }
}
