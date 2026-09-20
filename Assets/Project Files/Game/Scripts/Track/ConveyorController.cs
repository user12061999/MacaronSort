using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Moves pre-placed BlockGroup children along a SplineContainer loop.
    /// Cloned directly from Soda Shippers ConveyorController.
    /// Supports deterministic slot grid, branch merging, exit windows, and item tracking.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class ConveyorController : MonoBehaviour
    {
        public static ConveyorController Instance { get; private set; }

        [Header("Movement")]
        public float speed = 1.5f;
        public bool  loop  = true;
        public bool  automaticMotion = true;
        public float stopBeforeExitDistance = 0.5f;

        [Header("Direction Arrows")]
        [Tooltip("Arrow prefab that moves along the track")]
        public GameObject arrowPrefab;
        [Tooltip("World-unit distance between consecutive arrows")]
        public float      arrowSpacing = 2.0f;

        [Header("Exit / pickup window")]
        [Range(0f, 0.5f)] public float exitWindowFraction = 0.35f;

        public bool  IsFrozen         { get => _isFrozen; set => _isFrozen = value; }
        public float Speed            { get => speed; set => speed = value; }
        public float SplineWorldLength => _splineWorldLength;
        public SplineContainer SplineContainer => _splineContainer;

        public float OuterRadius
        {
            get
            {
                var builder = GetComponent<ConveyorTrackMeshBuilder>();
                return builder != null ? (builder.beltHalfWidth + builder.railWidth) : 0.55f;
            }
        }

        private SplineContainer _splineContainer;
        private float _splineWorldLength;
        private float _baseSpeed = 1.5f;
        private float _speedMultiplier = 1f;
        private bool  _isFrozen;
        private float _travelT = 0f;

        public struct GroupEntry
        {
            public BlockGroup Group;
            public float HeadT;
            public float TailT;
        }

        private struct ArrowMarker
        {
            public Transform Transform;
            public float T;
            public Quaternion PrefabLocalRot;
        }

        public struct ConveyorSlot
        {
            public float RowT;        // Current spline T of this row (updated every frame)
            public bool  IsOccupied;  // false = all lanes destroyed (or never filled) → branch can use this slot
            public int   LiveLanes;   // Bitmask of lanes still alive (bits 0-4). 0 = fully free.
        }

        private readonly List<GroupEntry> _groups = new();
        private readonly List<ArrowMarker> _arrows = new();
        private readonly List<ConveyorSlot> _slots = new();
        private readonly List<BranchPath> _branchPaths = new();

        public IReadOnlyList<GroupEntry> Groups => _groups;
        public IReadOnlyList<ConveyorSlot> Slots => _slots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(float speedMultiplier = 1f)
        {
            if (_splineContainer == null) _splineContainer = GetComponent<SplineContainer>();
            
            if (GameManager.Instance != null && GameManager.Instance.config != null)
            {
                _baseSpeed = GameManager.Instance.config.conveyorSpeed;
            }
            else
            {
                _baseSpeed = speed;
            }

            _speedMultiplier = speedMultiplier;
            UpdateConveyorSpeed();

            if (_splineContainer != null && _splineContainer.Spline != null)
            {
                _splineWorldLength = SplineUtility.CalculateLength(
                    _splineContainer.Spline, transform.localToWorldMatrix);
            }

            _slots.Clear();
            _groups.Clear();

            var blockGroups = GetComponentsInChildren<BlockGroup>(true);
            float currentT = 0f;
            foreach (var group in blockGroups)
            {
                // Skip block groups that belong to branch paths
                if (group.GetComponentInParent<BranchPath>() != null) continue;

                group.Initialize();

                float groupTLength = _splineWorldLength > 0f ? group.SplineLength / _splineWorldLength : 0f;
                for (int r = 0; r < group.RowCount; r++)
                {
                    float rowT = (currentT + (float)(group.RowCount - 1 - r) / group.RowCount * groupTLength) % 1f;

                    int liveMask = 0;
                    for (int l = 0; l < group.LaneCount && l < 5; l++)
                    {
                        var blk = group.GetBlock(r, l);
                        if (blk != null && !blk.IsDestroyed)
                            liveMask |= (1 << l);
                    }

                    _slots.Add(new ConveyorSlot
                    {
                        RowT       = rowT,
                        LiveLanes  = liveMask,
                        IsOccupied = liveMask != 0
                    });

                    int slotIdx = _slots.Count - 1;
                    for (int l = 0; l < group.LaneCount && l < 5; l++)
                    {
                        var blk = group.GetBlock(r, l);
                        if (blk == null || blk.IsDestroyed) continue;
                        int capturedLane = l;
                        blk.OnDestroyed += (_) => ClearSlotLane(slotIdx, capturedLane);
                    }
                }

                AddGroup(group, currentT);
                currentT += WorldLengthToT(group.SplineLength);
                if (currentT >= 1f) currentT -= 1f;
            }

            // Initialize all branch paths in the scene
            _branchPaths.Clear();
            var branchPaths = FindObjectsByType<BranchPath>(FindObjectsSortMode.None);
            foreach (var bp in branchPaths)
            {
                _branchPaths.Add(bp);
                bp.Initialize();
            }

            SpawnArrows();
        }

        public void EnsureLoopSlots(float rowSpacing)
        {
            if (!loop || _splineWorldLength <= 0f || rowSpacing <= 0f) return;
            int totalSlots = Mathf.Max(1, Mathf.RoundToInt(_splineWorldLength / rowSpacing));
            float slotStep = 1f / totalSlots;

            if (_slots.Count == 0)
            {
                for (int i = 0; i < totalSlots; i++)
                {
                    _slots.Add(new ConveyorSlot { RowT = i * slotStep, IsOccupied = false, LiveLanes = 0 });
                }
                return;
            }

            for (int i = 0; i < totalSlots; i++)
            {
                float targetT = i * slotStep;
                bool exists = false;
                for (int s = 0; s < _slots.Count; s++)
                {
                    float diff = Mathf.Abs(_slots[s].RowT - targetT);
                    diff = Mathf.Min(diff, 1f - diff);
                    if (diff < slotStep * 0.5f) { exists = true; break; }
                }
                if (!exists)
                {
                    _slots.Add(new ConveyorSlot { RowT = targetT, IsOccupied = false, LiveLanes = 0 });
                }
            }
        }

        public void RegisterBranchPath(BranchPath branch)
        {
            if (branch != null && !_branchPaths.Contains(branch))
                _branchPaths.Add(branch);
        }

        private void SpawnArrows()
        {
            foreach (var a in _arrows)
                if (a.Transform != null) Destroy(a.Transform.gameObject);
            _arrows.Clear();

            if (arrowPrefab == null || arrowSpacing <= 0f || _splineWorldLength <= 0f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(_splineWorldLength / arrowSpacing));
            Quaternion prefabRot = arrowPrefab.transform.localRotation;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                var go = Instantiate(arrowPrefab, transform);
                go.hideFlags = HideFlags.DontSave;
                var marker = new ArrowMarker { Transform = go.transform, T = t, PrefabLocalRot = prefabRot };
                PlaceArrow(go.transform, t, prefabRot);
                _arrows.Add(marker);
            }
        }

        private void PlaceArrow(Transform obj, float t, Quaternion prefabLocalRot)
        {
            if (_splineContainer == null || _splineContainer.Spline == null || obj == null) return;
            _splineContainer.Spline.Evaluate(t, out var pos, out var tangent, out var up);

            pos.y = 0f;
            obj.position = transform.TransformPoint((Vector3)pos);

            Vector3 fwd = transform.TransformDirection(((Vector3)tangent).normalized);
            Vector3 upDir = transform.TransformDirection(((Vector3)up).normalized);
            if (upDir == Vector3.zero) upDir = Vector3.up;
            if (fwd != Vector3.zero)
                obj.rotation = Quaternion.LookRotation(fwd, upDir) * prefabLocalRot;
        }

        private void Update()
        {
            if (_isFrozen) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (_splineWorldLength <= 0f) return;

            if (automaticMotion)
            {
                AdvanceBy(speed * Time.smoothDeltaTime);
            }
        }

        public void AdvanceBy(float worldDistance)
        {
            if (_splineWorldLength <= 0f) return;

            float delta = worldDistance / _splineWorldLength;
            _travelT = (_travelT + delta) % 1f;

            for (int i = 0; i < _groups.Count; i++)
            {
                var entry = _groups[i];
                if (entry.Group == null) continue;

                if (loop)
                {
                    entry.HeadT = (entry.HeadT + delta) % 1f;
                    entry.TailT = (entry.TailT + delta) % 1f;
                }
                else
                {
                    entry.HeadT += delta;
                    entry.TailT += delta;
                }
                _groups[i]  = entry;

                PlaceGroupAtT(entry.Group, entry.HeadT);
            }

            // Advance all tracked slots with the belt — frame-perfect, no drift.
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                s.RowT = loop ? (s.RowT + delta) % 1f : (s.RowT + delta);
                _slots[i] = s;
            }

            for (int i = 0; i < _arrows.Count; i++)
            {
                var a = _arrows[i];
                if (a.Transform == null) continue;

                a.T = (a.T + delta) % 1f;
                _arrows[i] = a;
                PlaceArrow(a.Transform, a.T, a.PrefabLocalRot);
            }
        }

        public void AddGroup(BlockGroup group, float startT = 0f)
        {
            float groupTLength = WorldLengthToT(group.SplineLength);
            _groups.Add(new GroupEntry
            {
                Group = group,
                HeadT = startT,
                TailT = loop ? ((startT + groupTLength) % 1f) : (startT + groupTLength)
            });
            group.transform.SetParent(transform, false);
            PlaceGroupAtT(group, startT);
            group.OnGroupCleared += HandleGroupCleared;
        }

        public void InsertGroupAt(BlockGroup group, float t) => AddGroup(group, t);

        public float GetGroupHeadT(BlockGroup group)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == group)
                    return _groups[i].HeadT;
            }
            return -1f;
        }

        public void ForceUpdateGroupPosition(BlockGroup group)
        {
            foreach (var entry in _groups)
            {
                if (entry.Group == group)
                {
                    PlaceGroupAtT(group, entry.HeadT);
                    break;
                }
            }
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
            UpdateConveyorSpeed();
        }

        public void UpdateConveyorSpeed()
        {
            float baseSpeedVal = _baseSpeed;
            if (GameManager.Instance != null && GameManager.Instance.config != null)
                baseSpeedVal = GameManager.Instance.config.conveyorSpeed;

            speed = baseSpeedVal * _speedMultiplier;
        }

        // ── Deterministic Slot Grid API ──────────────────────────────────────────

        public float FindClosestFreeSlotNearWorldPos(Vector3 mergeWorldPos, float maxWorldDistMeters)
        {
            if (_slots.Count == 0 || _splineContainer == null) return -1f;

            float bestDistSq = maxWorldDistMeters * maxWorldDistMeters;
            float bestT      = -1f;

            foreach (var slot in _slots)
            {
                if (slot.IsOccupied) continue;

                _splineContainer.Spline.Evaluate(slot.RowT, out var localPos, out _, out _);
                Vector3 worldPos = transform.TransformPoint((Vector3)localPos);

                float distSq = (worldPos - mergeWorldPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT      = slot.RowT;
                }
            }
            return bestT;
        }

        public int ClaimNearestSlot(float slotT, float tolerance)
        {
            float bestDist = float.MaxValue;
            int   bestIdx  = -1;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied) continue;
                float diff = Mathf.Abs(_slots[i].RowT - slotT);
                float dist = loop ? Mathf.Min(diff, 1f - diff) : diff;
                if (dist < bestDist && dist <= tolerance)
                {
                    bestDist = dist;
                    bestIdx  = i;
                }
            }
            if (bestIdx >= 0)
            {
                var s = _slots[bestIdx];
                s.IsOccupied = true;
                _slots[bestIdx] = s;
            }
            return bestIdx;
        }

        public void RegisterBlockToSlot(int slotIdx, int lane, ConveyorBlock3D block)
        {
            if (slotIdx < 0 || slotIdx >= _slots.Count) return;
            if (block == null || block.IsDestroyed) return;

            var s = _slots[slotIdx];
            s.LiveLanes |= (1 << lane);
            s.IsOccupied = true;
            _slots[slotIdx] = s;

            block.OnDestroyed += (_) => ClearSlotLane(slotIdx, lane);
        }

        public void ClearSlotLane(int slotIdx, int lane)
        {
            if (slotIdx < 0 || slotIdx >= _slots.Count) return;
            var s = _slots[slotIdx];
            s.LiveLanes &= ~(1 << lane);
            s.IsOccupied = s.LiveLanes != 0;
            _slots[slotIdx] = s;
        }

        public void PlaceGroupAtT(BlockGroup group, float headT)
        {
            if (_splineWorldLength <= 0f || group == null || _splineContainer == null) return;

            float groupTLength = group.SplineLength / _splineWorldLength;

            for (int row = 0; row < group.RowCount; row++)
            {
                float rowT = (headT + (float)(group.RowCount - 1 - row) / Mathf.Max(1, group.RowCount) * groupTLength);
                if (loop) rowT = Mathf.Repeat(rowT, 1f);

                _splineContainer.Spline.Evaluate(rowT, out var pos, out var tangent, out var up);

                Vector3 worldPos = transform.TransformPoint((Vector3)pos);
                Vector3 fwd     = transform.TransformDirection(((Vector3)tangent).normalized);
                Vector3 upDir   = transform.TransformDirection(((Vector3)up).normalized);
                if (upDir == Vector3.zero) upDir = Vector3.up;
                Vector3 right   = Vector3.Cross(upDir, fwd).normalized;
                Quaternion rot  = fwd != Vector3.zero ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

                for (int lane = 0; lane < group.LaneCount; lane++)
                {
                    var block = group.GetBlock(row, lane);
                    if (block == null || !block.gameObject.activeSelf || block.IsDestroyed) continue;
                    float xOff = (lane - (group.LaneCount - 1) * 0.5f) * group.LaneSpacing;
                    Vector3 targetPos = worldPos + right * xOff;
                    Quaternion targetRot = rot;

                    if (block.jumpProgress < 0.999f)
                    {
                        float tVal = block.jumpProgress;
                        
                        // Calculate dynamic height based on distance so longer jumps have higher arcs
                        float distance = Vector3.Distance(block.jumpStartPos, targetPos);
                        float jumpHeight = Mathf.Clamp(distance * 0.38f, 0.25f, 0.65f);
                        
                        float arc = Mathf.Sin(tVal * Mathf.PI) * jumpHeight;
                        block.transform.position = Vector3.Lerp(block.jumpStartPos, targetPos, tVal) + Vector3.up * arc;
                        block.transform.rotation = Quaternion.Slerp(block.jumpStartRot, targetRot, tVal);

                        // Squash and stretch along the jump path:
                        float sinVal = Mathf.Sin(tVal * Mathf.PI);
                        float scaleY = 1.0f + (0.18f * (1.0f - sinVal));
                        float scaleXZ = 1.0f - (0.09f * (1.0f - sinVal));
                        block.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    }
                    else
                    {
                        block.transform.position = targetPos;
                        block.transform.rotation = targetRot;
                    }

                    if (!block.IsDestroyed && !block.HasEnteredFireRange && FireRange.Instance != null)
                    {
                        if (FireRange.Instance.GetBounds().Contains(block.transform.position))
                        {
                            FireRange.Instance.RegisterBlockManually(block);
                        }
                    }
                }
            }

            _splineContainer.Spline.Evaluate(headT, out var hPos, out _, out _);
            group.transform.position = transform.TransformPoint(hPos);
        }

        public Vector3 EvaluateWorld(float rawT, out Vector3 worldForward)
        {
            if (_splineContainer == null)
            {
                worldForward = transform.forward;
                return transform.position;
            }
            float t = loop ? Mathf.Repeat(rawT, 1f) : Mathf.Clamp01(rawT);
            _splineContainer.Spline.Evaluate(t, out var pos, out var tan, out _);
            worldForward = transform.TransformDirection((Vector3)tan).normalized;
            return transform.TransformPoint((Vector3)pos);
        }

        public bool IsInExitWindow(float rawT)
        {
            if (!loop)
            {
                float stopT = Mathf.Clamp01(1f - stopBeforeExitDistance / _splineWorldLength);
                return rawT >= stopT - exitWindowFraction - 0.001f;
            }
            float dist = Mathf.Repeat(-rawT, 1f);
            return dist <= exitWindowFraction;
        }

        public List<ConveyorBlock3D> GetPickupBlocks()
        {
            var result = new List<ConveyorBlock3D>();
            if (_groups.Count == 0) return result;

            float searchWindow = Mathf.Max(exitWindowFraction, 0.35f);

            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;

                float groupTLength = _splineWorldLength > 0f ? entry.Group.SplineLength / _splineWorldLength : 0f;

                for (int r = 0; r < entry.Group.RowCount; r++)
                {
                    float rowT = (entry.HeadT + (float)(entry.Group.RowCount - 1 - r) / Mathf.Max(1, entry.Group.RowCount) * groupTLength) % 1f;

                    float distToExit = loop ? Mathf.Repeat(-rowT, 1f) : Mathf.Abs(1f - rowT);
                    if (distToExit <= searchWindow)
                    {
                        for (int l = 0; l < entry.Group.LaneCount; l++)
                        {
                            var b = entry.Group.GetBlock(r, l);
                            if (b != null && !b.IsDestroyed && !b.IsTargeted && b.gameObject.activeSelf)
                            {
                                result.Add(b);
                            }
                        }
                    }
                }
            }

            result.Sort((a, b) =>
            {
                float da = loop ? Mathf.Repeat(-GetBlockT(a), 1f) : Mathf.Abs(1f - GetBlockT(a));
                float db = loop ? Mathf.Repeat(-GetBlockT(b), 1f) : Mathf.Abs(1f - GetBlockT(b));
                int cmp = da.CompareTo(db);
                if (cmp != 0) return cmp;
                return a.LaneIndex.CompareTo(b.LaneIndex);
            });

            return result;
        }

        private float GetBlockT(ConveyorBlock3D block)
        {
            var group = block.GetComponentInParent<BlockGroup>();
            if (group == null) return 0f;
            float headT = GetGroupHeadT(group);
            float groupTLength = _splineWorldLength > 0f ? group.SplineLength / _splineWorldLength : 0f;
            return (headT + (float)(group.RowCount - 1 - block.RowIndex) / Mathf.Max(1, group.RowCount) * groupTLength) % 1f;
        }

        public bool HasReachableMatch(Predicate<ConveyorBlock3D> match, Func<BlockColorType, bool> colorMatch = null)
        {
            if (match == null && colorMatch == null) return false;

            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                for (int r = 0; r < entry.Group.RowCount; r++)
                    for (int l = 0; l < entry.Group.LaneCount; l++)
                    {
                        var b = entry.Group.GetBlock(r, l);
                        if (b != null && !b.IsDestroyed)
                        {
                            if (match != null && match(b)) return true;
                            if (colorMatch != null && colorMatch(b.ColorType)) return true;
                        }
                    }
            }

            foreach (var branch in _branchPaths)
            {
                if (branch != null)
                {
                    foreach (var row in branch.Rows)
                    {
                        if (row.Blocks != null)
                        {
                            foreach (var b in row.Blocks)
                            {
                                if (b != null && !b.IsDestroyed)
                                {
                                    if (match != null && match(b)) return true;
                                    if (colorMatch != null && colorMatch(b.ColorType)) return true;
                                }
                            }
                        }
                        else if (colorMatch != null)
                        {
                            if (colorMatch(row.ColorType)) return true;
                        }
                    }
                }
            }
            return false;
        }

        private void HandleGroupCleared(BlockGroup group)
        {
            group.OnGroupCleared -= HandleGroupCleared;
            _groups.RemoveAll(e => e.Group == group);
            if (group != null && group.gameObject != null)
            {
                Destroy(group.gameObject);
            }
        }

        public void RemoveGroup(BlockGroup group)
        {
            if (group == null) return;
            group.OnGroupCleared -= HandleGroupCleared;
            _groups.RemoveAll(e => e.Group == group);
            Destroy(group.gameObject);
        }

        private float WorldLengthToT(float worldLen)
        {
            return _splineWorldLength > 0f ? worldLen / _splineWorldLength : 0f;
        }

        public HashSet<BlockColorType> GetLiveColorSet()
        {
            var colors = new HashSet<BlockColorType>();
            foreach (var entry in _groups)
            {
                var group = entry.Group;
                if (group == null) continue;
                for (int r = 0; r < group.RowCount; r++)
                    for (int l = 0; l < group.LaneCount; l++)
                    {
                        var block = group.GetBlock(r, l);
                        if (block != null && !block.IsDestroyed)
                            colors.Add(block.ColorType);
                    }
            }
            return colors;
        }

        public List<ConveyorBlock3D> GetOrderedBlocks(BlockColorType colorType)
        {
            var result = new List<ConveyorBlock3D>();
            foreach (var entry in _groups)
            {
                if (entry.Group == null || entry.Group.IsEmpty) continue;
                if (entry.Group.colorType != colorType) continue;

                for (int row = 0; row < entry.Group.RowCount; row++)
                    for (int lane = 0; lane < entry.Group.LaneCount; lane++)
                    {
                        var block = entry.Group.GetBlock(row, lane);
                        if (block != null && !block.IsDestroyed && block.gameObject.activeSelf)
                            result.Add(block);
                    }
            }
            return result;
        }
        public bool IsConveyorFull(float requiredSpacing = 0.2f)
        {
            if (_slots.Count == 0) return false;
            foreach (var slot in _slots)
            {
                if (!slot.IsOccupied) return false;
            }
            return true;
        }

        public bool AllGroupsEmpty()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group != null && !_groups[i].Group.IsEmpty) return false;
            }
            for (int i = 0; i < _branchPaths.Count; i++)
            {
                if (_branchPaths[i] != null && !_branchPaths[i].IsFullyMerged) return false;
            }
            return true;
        }

        public bool IsLoopEmpty() => AllGroupsEmpty();

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

    public class ConveyorTrack : ConveyorController { }
}
