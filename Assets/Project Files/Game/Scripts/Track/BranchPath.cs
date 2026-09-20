using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace BlockShooter
{
    public class BranchPath : MonoBehaviour
    {
        public BranchPathData data;
        public float mergeT = 0.5f;

        public bool IsFullyMerged => _rows.Count == 0;

        /// <summary>
        /// Returns true if this branch has ANY pending row whose color matches slotColors.
        /// Gap availability is NOT checked here: the looping conveyor continuously moves groups
        /// past mergeT, so a gap will always open up. The only relevant question is whether
        /// the branch carries a color that would actually resolve the deadlock.
        /// </summary>
        public bool HasMatchingColorInQueue(HashSet<BlockColorType> slotColors)
        {
            foreach (var row in _rows)
            {
                bool hasActiveBlock = false;
                foreach (var b in row.Blocks)
                {
                    if (b != null && !b.IsDestroyed)
                    {
                        hasActiveBlock = true;
                        break;
                    }
                }
                if (hasActiveBlock && slotColors.Contains(row.ColorType))
                    return true;
            }
            return false;
        }

        public bool HasMatchingColor(Func<BlockColorType, bool> needsColor)
        {
            if (needsColor == null) return false;
            foreach (var row in _rows)
            {
                bool hasActiveBlock = false;
                foreach (var b in row.Blocks)
                {
                    if (b != null && !b.IsDestroyed)
                    {
                        hasActiveBlock = true;
                        if (needsColor(b.ColorType)) return true;
                    }
                }
                if (hasActiveBlock && needsColor(row.ColorType)) return true;
            }
            return false;
        }

        public IReadOnlyList<BranchRowEntry> Rows => _rows;

        private SplineContainer _splineContainer;
        private float _splineLength;
        private float _mergeStopT = 0.95f; // T value where blocks stop (outer wall of main conveyor)
        private bool _frontRowAtMergePoint;
        private readonly List<BranchRowEntry> _rows = new();

        private Vector3 _mainMergeWorldPos;
        private Vector3 _mainMergeWorldFwd;
        private float _mainBeltRadius;
        private float _laneSpacing;
        private bool _isRightSideBranch;

        public struct BranchRowEntry
        {
            public ConveyorBlock3D[] Blocks;
            public BlockColorType ColorType;
            public float CurrentT; // Position along branch spline (0.0 to 1.0)
            public float RowSpacing;
            public BlockGroup OriginalGroup;
            public BlockGroup MergedGroup;
            public bool[] MergedLanes;
            public float LastPositionedT;
            public int SlotIndex;
        }

        public void Setup(
            ConveyorController track,
            float branchMergeT,
            float rowSpacing,
            float laneSpacing,
            int laneCount,
            IReadOnlyList<BlockColorType[]> rows,
            Func<BlockColorType, int, ConveyorBlock3D> blockSpawner,
            float? customMergeStopT = null)
        {
            _splineContainer = GetComponent<SplineContainer>();
            mergeT = branchMergeT;
            _laneSpacing = laneSpacing;

            // Clear any old child groups
            var oldGroups = GetComponentsInChildren<BlockGroup>(true);
            foreach (var g in oldGroups)
            {
                if (Application.isPlaying) Destroy(g.gameObject);
                else DestroyImmediate(g.gameObject);
            }

            // Create BlockGroups for the rows so Initialize() natively reads them
            if (rows != null)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    var rowData = rows[r];
                    if (rowData == null || rowData.Length == 0) continue;

                    GameObject gGo = new GameObject($"Group_{r}_{rowData[0]}");
                    gGo.transform.SetParent(transform, false);
                    var bg = gGo.AddComponent<BlockGroup>();
                    bg.colorType = rowData[0];
                    bg.rowCount = 1;
                    bg.laneCount = laneCount;
                    bg.laneSpacing = laneSpacing;
                    bg.rowSpacing = rowSpacing;

                    for (int l = 0; l < rowData.Length; l++)
                    {
                        var block = blockSpawner != null ? blockSpawner(rowData[l], l) : null;
                        if (block != null)
                        {
                            block.transform.SetParent(gGo.transform, false);
                            block.SetGroupIndex(0, l);
                        }
                    }
                }
            }

            Initialize();

            if (customMergeStopT.HasValue && customMergeStopT.Value > 0f)
            {
                _mergeStopT = Mathf.Min(_mergeStopT, customMergeStopT.Value);
            }
        }

        public void Initialize()
        {
            _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null) return;
            _splineLength = SplineUtility.CalculateLength(_splineContainer.Spline, transform.localToWorldMatrix);
            mergeT = data != null ? data.mergeT : mergeT;

            // Calculate stop T: blocks should stop at the outer edge of the main conveyor
            // The last knot is at the center of the main track, so we offset by the track half-width
            // plus half the block's row spacing and a small safety margin to prevent clipping
            float mainTrackHalfWidth = 0f;
            var mainTrackBuilder = FindFirstMainTrackBuilder();
            if (mainTrackBuilder != null)
            {
                mainTrackHalfWidth = mainTrackBuilder.beltHalfWidth + mainTrackBuilder.railWidth;
            }
            else
            {
                mainTrackHalfWidth = 0.55f; // fallback
            }

            _laneSpacing = GetMainTrackLaneSpacing();

            // Precalculate main track merge reference positions and radius for extremely cheap boundary checks
            if (mainTrackBuilder != null && ConveyorController.Instance != null && ConveyorController.Instance.SplineContainer != null)
            {
                var mainSpline = ConveyorController.Instance.SplineContainer;
                mainSpline.Spline.Evaluate(mergeT, out var localPos, out var localTangent, out _);
                _mainMergeWorldPos = mainSpline.transform.TransformPoint((Vector3)localPos);
                _mainMergeWorldFwd = mainSpline.transform.TransformDirection((Vector3)localTangent).normalized;
                _mainBeltRadius = mainTrackBuilder.beltHalfWidth + mainTrackBuilder.railWidth;
            }
            else
            {
                _mainMergeWorldPos = Vector3.zero;
                _mainMergeWorldFwd = Vector3.forward;
                _mainBeltRadius = 0.55f;
            }

            float firstRowSpacing = 0.18f; // fallback
            var blockGroups = GetComponentsInChildren<BlockGroup>(true);
            var groupList = new List<BlockGroup>(blockGroups);
            groupList.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            if (groupList.Count > 0)
            {
                firstRowSpacing = groupList[0].rowSpacing;
            }

            // Align the closest possible block (lane 0 or 4, which is offset by 2 * _laneSpacing)
            // exactly 0.05m outside the conveyor outer wall
            float safetyOffset = mainTrackHalfWidth + 2f * _laneSpacing + 0.05f;

            if (_splineLength > 0f)
            {
                _mergeStopT = Mathf.Clamp01(1.0f - (safetyOffset / _splineLength));
            }

            // Geometrically determine if this branch merges from the right side of the main conveyor
            if (_mainMergeWorldPos != Vector3.zero && _splineContainer != null)
            {
                _splineContainer.Spline.Evaluate(_mergeStopT, out var localStopPos, out _, out _);
                Vector3 worldStopPos = transform.TransformPoint((Vector3)localStopPos);
                Vector3 mainRight = Vector3.Cross(Vector3.up, _mainMergeWorldFwd).normalized;
                _isRightSideBranch = Vector3.Dot(worldStopPos - _mainMergeWorldPos, mainRight) >= 0f;
            }
            else
            {
                _isRightSideBranch = true;
            }

            _rows.Clear();

            int globalRowIndex = 0;
            foreach (var group in groupList)
            {
                group.Initialize();

                for (int r = 0; r < group.RowCount; r++)
                {
                    var laneBlocks = new List<ConveyorBlock3D>();
                    for (int l = 0; l < group.LaneCount; l++)
                    {
                        var b = group.GetBlock(r, l);
                        if (b != null) laneBlocks.Add(b);
                    }

                    if (laneBlocks.Count > 0)
                    {
                        float rowSpacing = group.rowSpacing;
                        // Start from _mergeStopT backwards instead of 1.0
                        float initialT = _mergeStopT - (globalRowIndex * rowSpacing) / _splineLength;
                        initialT = Mathf.Clamp01(initialT);

                        _rows.Add(new BranchRowEntry
                        {
                            Blocks = laneBlocks.ToArray(),
                            ColorType = group.colorType,
                            CurrentT = initialT,
                            RowSpacing = rowSpacing,
                            OriginalGroup = group,
                            MergedGroup = null,
                            MergedLanes = new bool[5],
                            LastPositionedT = -1f,
                            SlotIndex = -1
                        });
                        globalRowIndex++;
                    }
                }
            }

            foreach (var row in _rows)
            {
                PlaceRowAtT(row);
            }
        }

        private ConveyorTrackMeshBuilder FindFirstMainTrackBuilder()
        {
            var cc = ConveyorController.Instance;
            if (cc != null)
            {
                return cc.GetComponent<ConveyorTrackMeshBuilder>();
            }
            var lr = GetComponentInParent<LevelRoot>();
            if (lr != null && lr.conveyorController != null)
            {
                return lr.conveyorController.GetComponent<ConveyorTrackMeshBuilder>();
            }
            return null;
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (_rows.Count == 0) return;

            float speed = ConveyorController.Instance != null ? ConveyorController.Instance.speed : 1.5f;
            if (ConveyorController.Instance != null && ConveyorController.Instance.IsFrozen) speed = 0f;
            float delta = _splineLength > 0f ? (speed / _splineLength) * Time.deltaTime : 0f;

            for (int i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                float maxT = 1.0f;
                if (i == 0)
                {
                    maxT = (entry.MergedGroup != null) ? 1.0f : _mergeStopT;
                }
                else
                {
                    float spacingInT = _rows[i].RowSpacing / _splineLength;
                    maxT = _rows[i - 1].CurrentT - spacingInT;
                    if (entry.MergedGroup == null)
                    {
                        maxT = Mathf.Min(maxT, _mergeStopT);
                    }
                }

                float nextT = Mathf.Min(entry.CurrentT + delta, maxT);

                // Optimization: skip spline calculation & transform repositioning if T has not changed.
                if (Mathf.Abs(nextT - entry.LastPositionedT) > 0.0001f)
                {
                    entry.CurrentT = nextT;
                    entry.LastPositionedT = nextT;
                    _rows[i] = entry;
                    PlaceRowAtT(entry);
                }
                else
                {
                    entry.CurrentT = nextT;
                    _rows[i] = entry;
                }
            }

            // Check and merge blocks for all rows that have reached the stop point or are already merging
            for (int i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                if (entry.CurrentT >= _mergeStopT - 0.001f || entry.MergedGroup != null)
                {
                    CheckAndMergeBlocks(ref entry);
                    _rows[i] = entry;
                }
            }

            // Remove fully merged rows from the front
            int rowsBefore = _rows.Count;
            while (_rows.Count > 0 && AllLanesMerged(_rows[0]))
            {
                var firstRow = _rows[0];
                if (firstRow.MergedGroup != null && firstRow.MergedGroup.IsEmpty)
                {
                    ConveyorController.Instance?.RemoveGroup(firstRow.MergedGroup);
                }
                _rows.RemoveAt(0);
            }

            if (rowsBefore > 0 && _rows.Count == 0)
            {
                _frontRowAtMergePoint = false;
                GameManager.Instance?.CheckFailCondition();
            }
            else if (_rows.Count > 0)
            {
                bool atMergePoint = _rows[0].CurrentT >= _mergeStopT - 0.001f;
                if (atMergePoint && !_frontRowAtMergePoint)
                    GameManager.Instance?.CheckFailCondition();
                _frontRowAtMergePoint = atMergePoint;
            }
        }

        private void PlaceRowAtT(BranchRowEntry row)
        {
            if (_splineContainer == null || _splineLength <= 0f) return;

            _splineContainer.Spline.Evaluate(row.CurrentT, out var pos, out var tangent, out var up);

            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 fwd = transform.TransformDirection((Vector3)tangent).normalized;
            Vector3 upDir = transform.TransformDirection((Vector3)up).normalized;
            if (upDir == Vector3.zero) upDir = Vector3.up;
            Vector3 right = Vector3.Cross(upDir, fwd).normalized;
            Quaternion rot = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

            const int totalLanes = 5;
            float laneSpacing = _laneSpacing;

            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                if (row.MergedLanes != null && row.MergedLanes[block.LaneIndex]) continue; // skip merged
                
                int lane = block.LaneIndex;
                float xOff = (lane - (totalLanes - 1) * 0.5f) * laneSpacing;
                block.transform.SetPositionAndRotation(worldPos + right * xOff, rot);
            }
        }

        private bool AllLanesMerged(BranchRowEntry row)
        {
            if (row.MergedLanes == null) return false;
            foreach (var block in row.Blocks)
            {
                if (block == null || block.IsDestroyed) continue;
                if (!row.MergedLanes[block.LaneIndex]) return false;
            }
            return true;
        }

        private void CheckAndMergeBlocks(ref BranchRowEntry row)
        {
            if (row.MergedLanes == null) row.MergedLanes = new bool[5];

            // If no group has been assigned yet, find a free slot on the main conveyor
            if (row.MergedGroup == null)
            {
                // Lookahead: begin merge check before fully reaching _mergeStopT for smooth on-the-fly flow
                float lookaheadT = (row.RowSpacing * 2.2f) / _splineLength;
                if (row.CurrentT < _mergeStopT - lookaheadT - 0.001f) return;

                if (ConveyorController.Instance == null) return;
                float conveyorLength = ConveyorController.Instance.SplineWorldLength;
                if (conveyorLength <= 0f) return;

                float dT = row.RowSpacing / conveyorLength;
                float searchRadiusMeters = row.RowSpacing * 1.5f;
                float slotT = ConveyorController.Instance.FindClosestFreeSlotNearWorldPos(
                    _mainMergeWorldPos, searchRadiusMeters);

                if (slotT < 0f)
                {
                    // No free slot available yet; wait.
                    return;
                }

                // Claim the slot immediately so no other branch targets the same slot this frame.
                row.SlotIndex = ConveyorController.Instance.ClaimNearestSlot(slotT, tolerance: dT * 0.5f);

                // Insert group on main conveyor at the exact slot T position.
                GameObject tempGo = new GameObject("MergedRowGroup");
                var newGroup = tempGo.AddComponent<BlockGroup>();
                newGroup.colorType = row.ColorType;
                newGroup.rowCount = 1;
                newGroup.laneCount = 5;
                newGroup.laneSpacing = _laneSpacing;
                newGroup.rowSpacing = row.RowSpacing;
                newGroup.Initialize();

                row.MergedGroup = newGroup;
                ConveyorController.Instance.InsertGroupAt(newGroup, slotT);
            }

            // Merge all active blocks immediately in the same frame — snappy cascade wave animations
            for (int lane = 0; lane < 5; lane++)
            {
                var block = GetBlockFromEntry(row, lane);
                if (block == null || block.IsDestroyed) continue;
                if (row.MergedLanes[lane]) continue;
                MergeBlock(ref row, block, lane);
                row.MergedLanes[lane] = true;
            }
        }

        private ConveyorBlock3D GetBlockFromEntry(BranchRowEntry row, int lane)
        {
            foreach (var b in row.Blocks)
            {
                if (b != null && b.LaneIndex == lane) return b;
            }
            return null;
        }

        private void MergeBlock(ref BranchRowEntry row, ConveyorBlock3D block, int lane)
        {
            Vector3 prevPosition = block.transform.position;
            Quaternion prevRotation = block.transform.rotation;

            block.transform.SetParent(row.MergedGroup.transform, true);
            block.SetGroupIndex(0, lane);
            row.MergedGroup.RegisterMergedBlock(block, lane);

            if (row.SlotIndex >= 0 && ConveyorController.Instance != null)
            {
                ConveyorController.Instance.RegisterBlockToSlot(row.SlotIndex, lane, block);
            }

            // Force update Conveyor positions so target position is updated
            if (ConveyorController.Instance != null)
            {
                ConveyorController.Instance.ForceUpdateGroupPosition(row.MergedGroup);
            }

            // Setup jump start positions
            block.jumpStartPos = prevPosition;
            block.jumpStartRot = prevRotation;
            block.jumpProgress = 0f;

            // Immediately set the position and rotation back to prevent 1-frame visual flicker
            block.transform.position = prevPosition;
            block.transform.rotation = prevRotation;

            // Sequential wave delay: lanes jump one by one, closest to farthest, tightly following each other
            // Lane 0 is closest on right branches; Lane 4 is closest on left branches.
            float delay = _isRightSideBranch ? lane * 0.028f : (4 - lane) * 0.028f;
            float duration = 0.22f; // Smooth but snappy jump duration

            // Clean up any existing tweens on this block to avoid collisions
            DOTween.Kill(block);
            block.transform.localScale = Vector3.one;

            // Animate jumpProgress from 0 to 1 with an OutQuad curve for fast start and smooth lock-in
            DOTween.To(() => block.jumpProgress, x => block.jumpProgress = x, 1f, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutQuad)
                .SetId(block)
                .OnComplete(() =>
                {
                    // Apply a juicy landing bounce scale punch (squash on impact, then snap back)
                    block.transform.localScale = Vector3.one;
                    DOTween.Kill(block.transform);
                    block.transform.DOPunchScale(new Vector3(0.12f, -0.15f, 0.12f), 0.22f, 8, 0.5f);
                });
        }

        private float GetMainTrackLaneSpacing()
        {
            var branchGroups = GetComponentsInChildren<BlockGroup>(true);
            if (branchGroups != null && branchGroups.Length > 0)
            {
                foreach (var g in branchGroups)
                {
                    if (g != null && g.laneSpacing > 0.01f)
                    {
                        return g.laneSpacing;
                    }
                }
            }

            var cc = ConveyorController.Instance;
            if (cc != null)
            {
                var mainGroups = cc.GetComponentsInChildren<BlockGroup>(true);
                foreach (var g in mainGroups)
                {
                    if (g != null && g.GetComponentInParent<BranchPath>() == null && g.laneSpacing > 0.01f)
                    {
                        return g.laneSpacing;
                    }
                }
            }
            return 0.18f;
        }
    }

    public class ConveyorBranchPath : BranchPath { }
}
