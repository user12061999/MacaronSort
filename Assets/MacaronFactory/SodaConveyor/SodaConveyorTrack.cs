// Ported from Soda Shippers Assets/Scripts/Gameplay/SodaConveyorTrack.cs.
using System.Collections.Generic;
using BlockShooter.SodaConveyor;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.SodaConveyor
{
    [RequireComponent(typeof(SplineContainer))]
    public sealed class SodaConveyorTrack : MonoBehaviour
    {
        [Header("Track shape (local space, closed loop — see TrackShapePresets/StageTrackData)")]
        [Tooltip("Stage template index 0-9 — Block Shooter's real Level_001..010 shapes.")]
        [SerializeField] int trackShapePreset;
        [Tooltip("Uniform scale on the shape; 1 reproduces the reference exactly.")]
        [SerializeField] float trackScale = 1f;
        [Tooltip("Local Y sodas ride at (Block Shooter's own levels all use 0.43).")]
        [SerializeField] float trackHeight = 0.43f;

        [Header("Belt cross-section")]
        [SerializeField] float beltHalfWidth = StageTrackData.BeltHalfWidth;

        [Header("Exit / pickup window (fraction of the loop arc from spawn to pickup)")]
        [Range(0f, 0.5f)] [SerializeField] float exitWindowFraction = 0.08f;
        [Range(0f, 0.5f)] [SerializeField] float approachWindowFraction = 0.22f;
        public const int LaneCount = StageGroupSpec.LaneCount;
        const float FinishBoostMul = 5f;
        const float JumpDuration = 0.22f;

        struct GroupEntry
        {
            public SodaGroup Group;
            public float HeadT;
            public float TailT;
        }
        struct ConveyorSlot
        {
            public float RowT;
            public bool IsOccupied;
            public int LiveLanes;
        }

        SplineContainer _spline;
        ConveyorMeshBuilder _meshBuilder;
        float _trackWorldLength = 1f;
        float speed;
        float _deltaTime;
        float _cruiseSpeed;
        int _boostBaselineCount = -1;
        float _boostMul = 1f;

        readonly List<GroupEntry> _groups = new();
        readonly List<ConveyorSlot> _slots = new();
        readonly List<SodaBranchPath> _branchPaths = new();
        readonly List<ConveyorBlock3D> _items = new();

        public IReadOnlyList<ConveyorBlock3D> Items => _items;
        public IReadOnlyList<SodaBranchPath> Branches => _branchPaths;
        public Transform PathRoot => transform;
        public float Speed { get => speed; set => speed = value; }
        public float SplineWorldLength => _trackWorldLength;
        public float OuterRadius => beltHalfWidth + StageTrackData.RailWidth;
        public float TrackHeight => trackHeight;
        public float RowSpacing => StageTrackData.RowSpacing;
        public float LaneSpacing => StageLayout.LaneSpacing;
        public float ItemRadius { get; private set; } = .07f;

        // Visual size only affects clearance at the feeder mouth, never the belt geometry.
        public void SetItemDiameter(float diameter) => ItemRadius = Mathf.Max(.01f, diameter * .5f);

        void Awake()
        {
            _spline = GetComponent<SplineContainer>();
        }
        public void SetTrackShape(int preset, float scale)
        {
            trackShapePreset = preset;
            trackScale = scale;
        }

        public void Configure(float loopSpeed)
        {
            BuildTrackShape();
            _cruiseSpeed = Mathf.Max(0.05f, loopSpeed) * _trackWorldLength;
            speed = _cruiseSpeed;
        }
        public System.Func<BlockColorType, Transform, ConveyorBlock3D> SpawnItem;
        public void Populate(int preset, float loopSpeed, float contentScale, int quantum)
        {
            SetTrackShape(preset, 1f);
            Configure(loopSpeed);
            var branches = StageLayout.FeedAllFromBranches(preset, StageLayout.MainGroups(preset), StageLayout.Branches(preset, contentScale, quantum));
            BuildVisualBelt(branches);
            BuildEmptySlots();
            BuildBranches(branches);
        }
        public void TickFinishBoost(bool yardEmpty)
        {
            if (!yardEmpty || _items.Count == 0)
            {
                _boostBaselineCount = -1;
                _boostMul = 1f;
                speed = _cruiseSpeed;
                return;
            }

            if (_boostBaselineCount < 0)
                _boostBaselineCount = _items.Count;

            var remainingFraction = _boostBaselineCount > 0
                ? Mathf.Clamp01((float)_items.Count / _boostBaselineCount)
                : 0f;
            _boostMul = Mathf.Lerp(FinishBoostMul, 1f, remainingFraction);
            speed = _cruiseSpeed * _boostMul;
        }

        public void BuildVisualBelt(StageBranchSpec[] branchSpecs = null)
        {
            BuildTrackShape();
            EnsureMeshBuilder();

            branchSpecs ??= StageLayout.FeedAllFromBranches(trackShapePreset, System.Array.Empty<StageGroupSpec>(), StageLayout.Branches(trackShapePreset));
            var branchSplines = new Spline[branchSpecs.Length];
            for (var i = 0; i < branchSpecs.Length; i++)
                branchSplines[i] = branchSpecs[i].BuildSpline(trackScale, trackHeight);

            _meshBuilder.BuildMesh();
            var mainMesh = GetComponent<MeshFilter>().sharedMesh;

            var branchMeshes = BuildBranchMeshes(branchSplines);
            GetComponent<MeshFilter>().sharedMesh = CombineIntoOne(mainMesh, branchMeshes);
            ApplyConveyorMaterials(GetComponent<MeshRenderer>());
        }
        List<Mesh> BuildBranchMeshes(Spline[] branches)
        {
            var meshes = new List<Mesh>();
            for (var i = 0; i < branches.Length; i++)
            {
                var go = new GameObject($"BranchTemp{i}");
                go.transform.SetParent(transform, false);

                var sc = go.AddComponent<SplineContainer>();
                sc.Spline = branches[i];

                var mb = go.AddComponent<ConveyorMeshBuilder>();
                mb.BeltHalfWidth = beltHalfWidth;
                mb.RailWidth = StageTrackData.RailWidth;
                mb.SetBranchTrim(_spline);
                mb.BuildMesh();

                meshes.Add(go.GetComponent<MeshFilter>().sharedMesh);

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false; // Destroy() is deferred — don't double-draw this frame
                // Destroy() silently no-ops outside Play mode (leaves BranchTemp objects behind,
                // e.g. when an editor tool calls BuildVisualBelt() directly) — DestroyImmediate there instead.
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            return meshes;
        }
        static Mesh CombineIntoOne(Mesh main, List<Mesh> branches)
        {
            var wallPieces = new List<CombineInstance>();
            var beltPieces = new List<CombineInstance>();

            void AddPiece(Mesh m)
            {
                if (m == null || m.subMeshCount < 2) return;
                wallPieces.Add(new CombineInstance { mesh = m, subMeshIndex = 0, transform = Matrix4x4.identity });
                beltPieces.Add(new CombineInstance { mesh = m, subMeshIndex = 1, transform = Matrix4x4.identity });
            }

            AddPiece(main);
            foreach (var b in branches) AddPiece(b);

            var wallMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            wallMesh.CombineMeshes(wallPieces.ToArray(), true, true);

            var beltMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            beltMesh.CombineMeshes(beltPieces.ToArray(), true, true);

            var final = new Mesh { name = "ConveyorTrack_Combined", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            final.CombineMeshes(new[]
            {
                new CombineInstance { mesh = wallMesh, transform = Matrix4x4.identity },
                new CombineInstance { mesh = beltMesh, transform = Matrix4x4.identity },
            }, false, true);
            SmoothSeamNormals(final);
            final.RecalculateBounds();
            ReleaseMesh(wallMesh);
            ReleaseMesh(beltMesh);
            ReleaseMesh(main);
            foreach (var branch in branches) ReleaseMesh(branch);
            return final;
        }
        static void SmoothSeamNormals(Mesh mesh, float weldEpsilon = 0.001f)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (verts.Length == 0 || normals.Length != verts.Length) return;

            var groups = new Dictionary<(int, int, int), List<int>>();
            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                var key = (Mathf.RoundToInt(v.x / weldEpsilon), Mathf.RoundToInt(v.y / weldEpsilon), Mathf.RoundToInt(v.z / weldEpsilon));
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    groups[key] = list;
                }
                list.Add(i);
            }

            foreach (var list in groups.Values)
            {
                if (list.Count < 2) continue;
                var avg = Vector3.zero;
                foreach (var idx in list) avg += normals[idx];
                if (avg.sqrMagnitude < 1e-8f) continue;
                avg.Normalize();
                foreach (var idx in list) normals[idx] = avg;
            }

            mesh.normals = normals;
        }

        static void ReleaseMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        void OnDestroy()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null) ReleaseMesh(filter.sharedMesh);
        }

        void BuildTrackShape()
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_spline == null) _spline = gameObject.AddComponent<SplineContainer>();

            _spline.Spline = TrackShapePresets.Build(trackShapePreset, trackScale, trackHeight);
            _trackWorldLength = Mathf.Max(0.01f, SplineUtility.CalculateLength(_spline.Spline, transform.localToWorldMatrix));
        }

        void EnsureMeshBuilder()
        {
            _meshBuilder = GetComponent<ConveyorMeshBuilder>();
            if (_meshBuilder == null) _meshBuilder = gameObject.AddComponent<ConveyorMeshBuilder>();
            if (GetComponent<MeshFilter>() == null) gameObject.AddComponent<MeshFilter>();
            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
            _meshBuilder.BeltHalfWidth = beltHalfWidth;
            _meshBuilder.RailWidth = StageTrackData.RailWidth;
            _meshBuilder.pickupWindowFraction = exitWindowFraction;
        }

        public Material SideMaterial;
        public Material TopMaterial;
        void ApplyConveyorMaterials(MeshRenderer rend)
        {
            rend.sharedMaterials = new[] { SideMaterial, TopMaterial };
        }
        public bool AllGroupsEmpty()
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group != null && !_groups[i].Group.IsEmpty) return false;
            }
            for (var i = 0; i < _branchPaths.Count; i++)
            {
                if (_branchPaths[i] != null && !_branchPaths[i].IsFullyMerged) return false;
            }
            return true;
        }

        public bool IsLoopEmpty() => AllGroupsEmpty();
        public bool AnyColorPending(BlockColorType color)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it != null && it.ColorType == color && !it.IsDestroyed) return true;
            }
            for (var i = 0; i < _branchPaths.Count; i++)
            {
                if (_branchPaths[i] != null && _branchPaths[i].HasMatchingColor(c => c == color)) return true;
            }
            return false;
        }
        static float DistanceToPickup(float rawT) => Mathf.Repeat(-rawT, 1f);
        float ScaledExitWindow => Mathf.Min(0.5f, exitWindowFraction * _boostMul);
        float ScaledApproachWindow => Mathf.Min(0.5f, approachWindowFraction * _boostMul);

        public bool IsInExitWindow(float rawT) => DistanceToPickup(rawT) <= ScaledExitWindow;
        public bool IsApproachingExit(float rawT)
        {
            var d = DistanceToPickup(rawT);
            return d > ScaledExitWindow && d <= ScaledApproachWindow;
        }

        public void Clear()
        {
            _boostBaselineCount = -1;
            _boostMul = 1f;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i] != null)
                    Destroy(_items[i].gameObject);
            }
            _items.Clear();

            for (var i = 0; i < _branchPaths.Count; i++)
            {
                if (_branchPaths[i] != null)
                    _branchPaths[i].ReleaseAllAndDestroy();
            }
            _branchPaths.Clear();

            for (var i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i].Group;
                if (g == null) continue;
                g.OnGroupCleared -= HandleGroupCleared;
                Destroy(g.gameObject);
            }
            _groups.Clear();
            _slots.Clear();
        }

        public void Add(ConveyorBlock3D item)
        {
            if (item != null) { _items.Add(item); item.OnDestroyed += Remove; }
        }

        public void Remove(ConveyorBlock3D item)
        {
            item.OnDestroyed -= Remove;
            _items.Remove(item);
        }

        // ── Stage bootstrap: pre-place every soda up front ──────────────────────────────
        public void BuildEmptySlots()
        {
            _slots.Clear();
            int count = Mathf.Max(1, Mathf.FloorToInt(_trackWorldLength / RowSpacing));
            for (int i = 0; i < count; i++)
                _slots.Add(new ConveyorSlot { RowT = (float)i / count });
        }

        public void BuildMainGroups(StageGroupSpec[] specs)
        {
            _slots.Clear();
            var currentT = 0f;
            foreach (var spec in specs)
            {
                var group = CreateGroupObject(spec);
                RegisterSlotsForGroup(group, currentT);
                AddGroupInternal(group, currentT);
                currentT += WorldLengthToT(group.SplineLength);
                if (currentT >= 1f) currentT -= 1f;
            }
        }
        public void BuildBranches(StageBranchSpec[] specs)
        {
            foreach (var spec in specs)
            {
                var go = new GameObject($"Branch_{spec.Name}");
                go.transform.SetParent(transform, false);
                var branch = go.AddComponent<SodaBranchPath>();
                branch.Setup(this, spec, trackScale, trackHeight);
                _branchPaths.Add(branch);
            }
        }

        SodaGroup CreateGroupObject(StageGroupSpec spec)
        {
            var go = new GameObject($"Group_{spec.Color}");
            go.transform.SetParent(transform, false);

            var group = go.AddComponent<SodaGroup>();
            group.colorType = spec.Color;
            group.rowCount = Mathf.Max(1, spec.RowCount);
            group.laneCount = StageGroupSpec.LaneCount;
            group.laneSpacing = LaneSpacing;
            group.rowSpacing = RowSpacing;

            for (var row = 0; row < group.rowCount; row++)
            {
                for (var lane = 0; lane < group.laneCount; lane++)
                {
                    var item = SpawnItem(spec.Color, go.transform);
                    item.SetGroupIndex(row, lane);
                    item.Phase = ConveyorItemPhase.OnLoop;
                    item.JumpProgress = 1f;
                }
            }

            group.Initialize();
            foreach (var item in group.AllItems())
                Add(item);
            return group;
        }
        void RegisterSlotsForGroup(SodaGroup group, float currentT)
        {
            var groupTLength = WorldLengthToT(group.SplineLength);
            for (var r = 0; r < group.RowCount; r++)
            {
                var rowT = Mathf.Repeat(currentT + (float)(group.RowCount - 1 - r) / group.RowCount * groupTLength, 1f);

                var liveMask = 0;
                for (var l = 0; l < group.LaneCount && l < 32; l++)
                {
                    if (group.GetItem(r, l) != null) liveMask |= 1 << l;
                }

                _slots.Add(new ConveyorSlot { RowT = rowT, LiveLanes = liveMask, IsOccupied = liveMask != 0 });

                var slotIdx = _slots.Count - 1;
                for (var l = 0; l < group.LaneCount && l < 32; l++)
                {
                    var item = group.GetItem(r, l);
                    if (item == null) continue;
                    var capturedLane = l;
                    item.OnDestroyed += _ => ClearSlotLane(slotIdx, capturedLane);
                }
            }
        }

        void AddGroupInternal(SodaGroup group, float startT)
        {
            var groupTLength = WorldLengthToT(group.SplineLength);
            _groups.Add(new GroupEntry { Group = group, HeadT = startT, TailT = Mathf.Repeat(startT + groupTLength, 1f) });
            group.transform.SetParent(transform, false);
            PlaceGroupAtT(group, startT, false);
            group.OnGroupCleared += HandleGroupCleared;
        }
        public void InsertGroupAt(SodaGroup group, float t) => AddGroupInternal(group, t);
        public SodaGroup CreateMergeGroup(BlockColorType color)
        {
            var go = new GameObject($"MergedGroup_{color}");
            var group = go.AddComponent<SodaGroup>();
            group.colorType = color;
            group.rowCount = 1;
            group.laneCount = StageGroupSpec.LaneCount;
            group.laneSpacing = LaneSpacing;
            group.rowSpacing = RowSpacing;
            group.Initialize();
            return group;
        }

        public void RegisterItemToSlot(int slotIdx, int lane, ConveyorBlock3D item)
        {
            if (slotIdx < 0 || slotIdx >= _slots.Count || item == null) return;

            var s = _slots[slotIdx];
            s.LiveLanes |= 1 << lane;
            s.IsOccupied = true;
            _slots[slotIdx] = s;

            item.OnDestroyed += _ => ClearSlotLane(slotIdx, lane);
        }

        void ClearSlotLane(int slotIdx, int lane)
        {
            if (slotIdx < 0 || slotIdx >= _slots.Count) return;
            var s = _slots[slotIdx];
            s.LiveLanes &= ~(1 << lane);
            s.IsOccupied = s.LiveLanes != 0;
            _slots[slotIdx] = s;
        }
        public bool AnyFreeSlot()
        {
            for (var i = 0; i < _slots.Count; i++)
                if (!_slots[i].IsOccupied) return true;
            return false;
        }
        public int LoopColorMask()
        {
            var mask = 0;
            for (var i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it != null && it.Phase == ConveyorItemPhase.OnLoop) mask |= 1 << (int)it.ColorType;
            }
            return mask;
        }
        public float FindClosestFreeSlotNearWorldPos(Vector3 mergeWorldPos, float maxWorldDistMeters)
        {
            if (_slots.Count == 0 || _spline == null) return -1f;

            var bestDistSq = maxWorldDistMeters * maxWorldDistMeters;
            var bestT = -1f;
            float bestForward = float.NegativeInfinity;
            foreach (var slot in _slots)
            {
                if (slot.IsOccupied) continue;
                _spline.Spline.Evaluate(slot.RowT, out var localPos, out _, out _);
                var worldPos = transform.TransformPoint((Vector3)localPos);
                var distSq = (worldPos - mergeWorldPos).sqrMagnitude;
                EvaluateWorld(slot.RowT, out var forward);
                float downstream = Vector3.Dot(worldPos - mergeWorldPos, forward);
                if (distSq < bestDistSq && downstream > bestForward)
                {
                    bestForward = downstream;
                    bestT = slot.RowT;
                }
            }
            return bestT;
        }
        public int ClaimNearestSlot(float slotT, float tolerance)
        {
            var bestDist = float.MaxValue;
            var bestIdx = -1;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied) continue;
                var diff = Mathf.Abs(_slots[i].RowT - slotT);
                var dist = Mathf.Min(diff, 1f - diff);
                if (dist < bestDist && dist <= tolerance)
                {
                    bestDist = dist;
                    bestIdx = i;
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

        public void ForceUpdateGroupPosition(SodaGroup group)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == group)
                {
                    PlaceGroupAtT(group, _groups[i].HeadT, false);
                    break;
                }
            }
        }

        void HandleGroupCleared(SodaGroup group)
        {
            group.OnGroupCleared -= HandleGroupCleared;
            _groups.RemoveAll(e => e.Group == group);
            if (group != null && group.gameObject != null)
                Destroy(group.gameObject);
        }

        float WorldLengthToT(float worldLen) => _trackWorldLength > 0f ? worldLen / _trackWorldLength : 0f;
        public Vector3 EvaluateWorld(float rawT, out Vector3 worldForward)
        {
            var t = Mathf.Repeat(rawT, 1f);
            _spline.Spline.Evaluate(t, out var pos, out var tan, out _);
            worldForward = transform.TransformDirection((Vector3)tan).normalized;
            return transform.TransformPoint(pos);
        }

        public void Advance(float deltaTime)
        {
            if (_trackWorldLength <= 0f || deltaTime <= 0) return;
            // Keep inlet decisions finer than one row even on a slow frame.
            float step = RowSpacing / Mathf.Max(.01f, speed) * .2f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / step));
            for (int i = 0; i < steps; i++) AdvanceStep(deltaTime / steps);
        }

        private void AdvanceStep(float deltaTime)
        {
            if (_trackWorldLength <= 0f) return;
            _deltaTime = Mathf.Max(0, deltaTime);

            var loopDt = _deltaTime * speed / _trackWorldLength;

            for (var i = 0; i < _groups.Count; i++)
            {
                var entry = _groups[i];
                if (entry.Group == null) continue;

                entry.HeadT = Mathf.Repeat(entry.HeadT + loopDt, 1f);
                entry.TailT = Mathf.Repeat(entry.TailT + loopDt, 1f);
                _groups[i] = entry;

                PlaceGroupAtT(entry.Group, entry.HeadT);
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                s.RowT = Mathf.Repeat(s.RowT + loopDt, 1f);
                _slots[i] = s;
            }
            foreach (var branch in _branchPaths) branch.Advance(_deltaTime);
        }
        void PlaceGroupAtT(SodaGroup group, float headT, bool advanceJump = true)
        {
            if (_trackWorldLength <= 0f || group.RowCount <= 0) return;

            var groupTLength = group.SplineLength / _trackWorldLength;

            for (var row = 0; row < group.RowCount; row++)
            {
                var rowT = Mathf.Repeat(headT + (float)(group.RowCount - 1 - row) / group.RowCount * groupTLength, 1f);
                _spline.Spline.Evaluate(rowT, out var pos, out var tangent, out var up);

                var worldPos = transform.TransformPoint(pos);
                var fwd = transform.TransformDirection((Vector3)tangent).normalized;
                if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
                var upDir = transform.TransformDirection((Vector3)up).normalized;
                if (upDir.sqrMagnitude < 1e-6f) upDir = Vector3.up;
                var right = Vector3.Cross(upDir, fwd).normalized;
                var rot = Quaternion.LookRotation(fwd, upDir);

                for (var lane = 0; lane < group.LaneCount; lane++)
                {
                    var item = group.GetItem(row, lane);
                    if (item == null || item.IsDestroyed || item.Phase != ConveyorItemPhase.OnLoop) continue;

                    item.PathT = rowT;
                    var xOff = (lane - (group.LaneCount - 1) * 0.5f) * group.LaneSpacing;
                    var targetPos = worldPos + right * xOff;
                    var targetRot = rot * Quaternion.identity;

                    if (item.JumpProgress < 0.999f)
                    {
                        item.JumpProgress = Mathf.Min(1f, item.JumpProgress + (advanceJump ? _deltaTime / JumpDuration : 0));
                        var u = item.JumpProgress;
                        var distance = Vector3.Distance(item.JumpStartPos, targetPos);
                        var jumpHeight = Mathf.Clamp(distance * 0.38f, 0.25f, 0.65f);
                        var arc = Mathf.Sin(u * Mathf.PI) * jumpHeight;
                        item.transform.SetPositionAndRotation(
                            Vector3.Lerp(item.JumpStartPos, targetPos, u) + Vector3.up * arc,
                            Quaternion.Slerp(item.JumpStartRot, targetRot, u));
                    }
                    else
                    {
                        item.transform.SetPositionAndRotation(targetPos, targetRot);
                    }
                }
            }
        }
    }
}
