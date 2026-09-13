using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>Which rim of the conveyor an opening cuts through.</summary>
    public enum ConveyorSide { Right = 0, Left = 1, Both = 2 }

    /// <summary>How an opening's Start/Length are interpreted.</summary>
    public enum ConveyorMeasure
    {
        /// <summary>0..1 along the spline.</summary>
        Normalized = 0,
        /// <summary>World-space metres along the spline.</summary>
        Meters = 1,
    }

    /// <summary>
    /// A hole cut into the conveyor rim. Any position, any length, either side.
    /// </summary>
    [Serializable]
    public class ConveyorOpening
    {
        public string label = "Gate";
        public bool enabled = true;

        [Tooltip("Which rim to cut. 'Both' cuts the same span out of both walls.")]
        public ConveyorSide side = ConveyorSide.Right;

        [Tooltip("Normalized = 0..1 along the spline. Meters = world distance from the spline start.")]
        public ConveyorMeasure measure = ConveyorMeasure.Normalized;

        [Tooltip("Where the opening begins.")]
        public float start = 0.25f;

        [Tooltip("How long the opening is, in the unit chosen above.")]
        [Min(0f)] public float length = 0.06f;

        [Tooltip("Close the cut wall ends with a flat cap so the rim does not look paper-thin.")]
        public bool caps = true;

        [Tooltip("Cover the exposed side of the belt slab inside the opening.")]
        public bool skirt = true;

        [Tooltip("Height of the low kerb left behind inside the opening. 0 = remove the wall completely.")]
        [Min(0f)] public float lipHeight = 0f;

        public Color gizmoColor = new Color(1f, 0.5f, 0.12f, 1f);

        public bool CutsLeft => enabled && (side == ConveyorSide.Left || side == ConveyorSide.Both);
        public bool CutsRight => enabled && (side == ConveyorSide.Right || side == ConveyorSide.Both);
    }

    /// <summary>
    /// Closed-profile spline sweep — equivalent to a Blender curve bevel extrusion.
    ///
    /// Pipeline:
    ///   1. Build a closed 2D polygon (the cross-section profile).
    ///   2. Sample the spline into (position, right, up) frames.
    ///      Opening boundaries are inserted as EXTRA samples, so a gate lands exactly
    ///      where you asked for it instead of snapping to the nearest ring.
    ///   3. Project every profile vertex into every frame → a 3D ring.
    ///   4. Connect ring[s] to ring[s+1] with quads, one isolated strip per profile edge
    ///      (isolated strips = automatic hard edges, so bevels stay crisp).
    ///   5. Skip wall quads that fall inside an opening, then add caps / kerb / skirt.
    ///
    /// Cross-section (Y = up, X = right, bevelSegments = 1):
    ///
    ///   P2────P3         P8────P9
    ///  ╱        ╲       ╱        ╲    ← bevelled top-outer corners
    /// P1          P4───P7          P10 ← wall tops
    /// |           P5═══P6           |  ← belt surface at Y = 0
    /// P0                            P11
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConveyorTrackMeshBuilder : MonoBehaviour
    {
        // ── Profile ───────────────────────────────────────────────────────────
        [Header("Cross-Section Profile")]
        [Tooltip("Half-width of the flat belt surface (inner groove width = 2 × this)")]
        public float beltHalfWidth = 0.5f;
        [Tooltip("How far the outer walls rise ABOVE the belt surface")]
        public float wallAboveBelt = 0.12f;
        [Tooltip("How far the outer walls hang DOWN below the belt surface")]
        public float railHeight = 1.0f;
        [Tooltip("Thickness of each outer wall")]
        public float railWidth = 0.05f;
        [Tooltip("Bevel size on the top corners of each wall (clamped to railWidth / wallAboveBelt)")]
        public float bevelSize = 0.04f;
        [Tooltip("1 = hard chamfer (original look). 2+ = rounded corner.")]
        [Range(1, 8)] public int bevelSegments = 1;
        [Tooltip("Add a flat underside so the track is watertight (needed for shadows / MeshCollider).")]
        public bool closeBottom = false;

        // ── Sweep ─────────────────────────────────────────────────────────────
        [Header("Sweep Quality")]
        [Range(20, 800)]
        [Tooltip("Base number of cross-section rings. Opening edges add rings on top of this.")]
        public int resolution = 60;

        [Header("UV Tiling")]
        [Tooltip("How many times the texture tiles along the spline length")]
        public float vTiling = 8f;

        // ── Partial sweep (used by junctions to stop a branch at the main track) ──
        [Header("Sweep Range")]
        [Tooltip("Start of the swept range along the spline. Leave at 0 for the whole spline.")]
        [Range(0f, 1f)] public float sweepFrom = 0f;
        [Tooltip("End of the swept range. Leave at 1 for the whole spline. " +
                 "ConveyorJunction writes this so a branch stops flush against the main track.")]
        [Range(0f, 1f)] public float sweepTo = 1f;
        [Tooltip("Cap the wall cross-sections where a partial sweep begins and ends.")]
        public bool capSweepEnds = true;

        // ── Openings ──────────────────────────────────────────────────────────
        [Header("Openings / Gates")]
        [Tooltip("Any number of holes in the rim, at any position and length, on either side.")]
        public List<ConveyorOpening> openings = new List<ConveyorOpening>();

        // ── Output ────────────────────────────────────────────────────────────
        [Header("Output")]
        [Tooltip("Put gate caps + kerb into submesh 2 so they can use their own material. " +
                 "Leave OFF to keep the existing 2-material setup (0 = walls, 1 = belt).")]
        public bool separateGateSubmesh = false;
        [Tooltip("Keep a MeshCollider in sync with the generated mesh.")]
        public bool generateCollider = false;
        [Tooltip("Rebuild automatically whenever a value changes in the inspector.")]
        public bool autoRebuild = true;

        // ── Legacy (migrated into 'openings' on first load) ────────────────────
        [HideInInspector] public bool openZoneEnabled = false;
        [HideInInspector, Range(0f, 0.25f)] public float openZoneHalfT = 0.015f;
        [HideInInspector, SerializeField] private bool _legacyMigrated = false;

        // ── Branch connection trimming (set by the level editor) ───────────────
        [HideInInspector] public bool trimBranchEnd = false;
        [HideInInspector] public SplineContainer mainTrackSpline;
        [HideInInspector] public bool branchOnRightSide = true;
        [HideInInspector] public bool isDraggingInEditor = false;

        // ── Private ───────────────────────────────────────────────────────────
        private SplineContainer _spline;
        private MeshFilter _meshFilter;
        private MeshCollider _collider;
        private Mesh _generatedMesh;

        // arc-length lookup, world space
        private float[] _lutT;
        private float[] _lutD;
        private float _totalLength;

        /// <summary>World-space length of the spline (valid after a build).</summary>
        public float TotalLength => _totalLength;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake() => Cache();

        private void Start() => BuildMesh();

        private void Reset()
        {
            _legacyMigrated = true;
            openings = new List<ConveyorOpening>();
        }

        private void OnValidate()
        {
            MigrateLegacyOpenZone();
#if UNITY_EDITOR
            if (!autoRebuild || !isActiveAndEnabled) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                BuildMesh();
            };
#endif
        }

        private void Cache()
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        }

        /// <summary>Turns the old openZoneEnabled/openZoneHalfT pair into a real opening entry.</summary>
        private void MigrateLegacyOpenZone()
        {
            if (_legacyMigrated) return;
            _legacyMigrated = true;
            if (!openZoneEnabled) return;
            if (openings == null) openings = new List<ConveyorOpening>();
            if (openings.Count > 0) return;

            openings.Add(new ConveyorOpening
            {
                label = "FireRange (migrated)",
                side = ConveyorSide.Right,
                measure = ConveyorMeasure.Normalized,
                start = -openZoneHalfT,          // wraps around T=0, same as before
                length = openZoneHalfT * 2f,
                caps = true,
                skirt = true,
                lipHeight = 0f,
            });
            openZoneEnabled = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void BuildMesh()
        {
            Cache();
            MigrateLegacyOpenZone();
            if (_spline == null || _spline.Spline == null || _spline.Spline.Count < 2) return;

            BuildLengthLut();
            var mesh = Sweep();

            // free the mesh from the previous build instead of leaking it
            if (_generatedMesh != null && _generatedMesh != mesh)
            {
                if (Application.isPlaying) Destroy(_generatedMesh);
                else DestroyImmediate(_generatedMesh);
            }
            _generatedMesh = mesh;
            _meshFilter.sharedMesh = mesh;

            if (generateCollider)
            {
                if (_collider == null) _collider = GetComponent<MeshCollider>();
                if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
                _collider.sharedMesh = null;
                _collider.sharedMesh = mesh;
            }
        }

        /// <summary>Adds an opening and rebuilds. Returns the new entry so you can tweak it.</summary>
        public ConveyorOpening AddOpening(float start, float length,
            ConveyorSide side = ConveyorSide.Right,
            ConveyorMeasure measure = ConveyorMeasure.Normalized)
        {
            if (openings == null) openings = new List<ConveyorOpening>();
            var o = new ConveyorOpening
            {
                label = "Gate " + (openings.Count + 1),
                start = start,
                length = length,
                side = side,
                measure = measure,
            };
            openings.Add(o);
            BuildMesh();
            return o;
        }

        public void RemoveOpening(ConveyorOpening opening)
        {
            if (openings != null && openings.Remove(opening)) BuildMesh();
        }

        public void SetOpeningEnabled(int index, bool value)
        {
            if (openings == null || index < 0 || index >= openings.Count) return;
            if (openings[index].enabled == value) return;
            openings[index].enabled = value;
            BuildMesh();
        }

        public int OpeningCount => openings?.Count ?? 0;

        /// <summary>Distance from the spline centre to the outer face of a rim.</summary>
        public float RimOffset => beltHalfWidth + railWidth;

        /// <summary>Normalized start/end of an opening. t1 &lt; t0 means it wraps past T=1.</summary>
        public bool TryGetOpeningSpan(int index, out float t0, out float t1)
        {
            t0 = t1 = 0f;
            if (openings == null || index < 0 || index >= openings.Count) return false;
            Cache();
            if (_spline == null || _spline.Spline == null) return false;

            BuildLengthLut();
            ToNormalizedSpan(openings[index], out t0, out t1);
            return true;
        }

        /// <summary>World length of an opening along the belt.</summary>
        public float GetOpeningLength(int index)
        {
            if (!TryGetOpeningSpan(index, out float t0, out float t1)) return 0f;
            float sweep = t1 >= t0 ? t1 - t0 : 1f - t0 + t1;
            return sweep * _totalLength;
        }

        /// <summary>
        /// World pose at a point INSIDE an opening. u = 0 is the start of the gate,
        /// u = 1 the end, u = 0.5 the middle.
        ///
        /// The rotation's FORWARD (+Z) points outward through the gate, UP (+Y) follows the
        /// spline up vector. Author chutes facing +Z and they will drop away from the belt.
        /// <paramref name="beltForward"/> is the travel direction of the belt at that point.
        /// </summary>
        public bool TryGetOpeningPose(int index, float u,
            out Vector3 position, out Quaternion rotation, out Vector3 beltForward)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            beltForward = Vector3.forward;

            if (!TryGetOpeningSpan(index, out float t0, out float t1)) return false;

            float sweep = t1 >= t0 ? t1 - t0 : 1f - t0 + t1;
            float t = Frac(t0 + sweep * Mathf.Clamp01(u));

            EvaluateWorld(t, out Vector3 pos, out Vector3 fwd, out Vector3 up, out Vector3 right);
            float sign = openings[index].side == ConveyorSide.Left ? -1f : 1f;
            Vector3 outward = right * sign;

            position = pos + outward * RimOffset;
            rotation = Quaternion.LookRotation(outward, up);
            beltForward = fwd;
            return true;
        }

        /// <summary>World pose at the middle of an opening. See TryGetOpeningPose.</summary>
        public bool TryGetOpeningAnchor(int index, out Vector3 position, out Quaternion rotation)
            => TryGetOpeningPose(index, 0.5f, out position, out rotation, out _);

        /// <summary>World-space frame at normalized position t.</summary>
        public void EvaluateWorld(float t, out Vector3 position, out Vector3 forward,
            out Vector3 up, out Vector3 right)
        {
            Cache();
            _spline.Spline.Evaluate(_spline.Spline.Closed ? Frac(t) : Mathf.Clamp01(t), out var p, out var tan, out var u);
            position = transform.TransformPoint((Vector3)p);
            forward = transform.TransformDirection((Vector3)tan).normalized;
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            up = transform.TransformDirection((Vector3)u).normalized;
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }

        /// <summary>Frame in this transform's LOCAL space — the space mesh vertices live in.</summary>
        private void EvaluateLocal(float t, out Vector3 position, out Vector3 forward,
            out Vector3 up, out Vector3 right)
        {
            _spline.Spline.Evaluate(_spline.Spline.Closed ? Frac(t) : Mathf.Clamp01(t), out var p, out var tan, out var u);
            position = (Vector3)p;
            forward = ((Vector3)tan).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            up = ((Vector3)u).normalized;
            if (up.sqrMagnitude < 0.001f) up = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gate panel (the removable door that plugs an opening)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Where a gate panel's transform origin sits, in the gate's own frame.</summary>
        public enum GatePanelPivot
        {
            /// <summary>Bottom of the wall, on its inner face. Good for a flap hinging outward.</summary>
            Bottom = 0,
            /// <summary>Belt height, on the inner face. Good for a shutter sliding down.</summary>
            BeltLevel = 1,
            /// <summary>Top of the wall, inner face. Good for a flap hinging upward.</summary>
            Top = 2,
            /// <summary>Middle of the wall cross-section.</summary>
            Center = 3,
        }

        /// <summary>
        /// Builds the wall section that an opening removed, as a standalone mesh.
        /// Parent the result to this transform at <paramref name="localPosition"/> /
        /// <paramref name="localRotation"/> and it plugs the hole exactly. Animate that
        /// transform to open and close the gate — no mesh rebuild required.
        ///
        /// In panel space +Z points outward through the gate, +Y is up, +X runs along the belt.
        /// </summary>
        /// <param name="endInset">Shrink each end by this many metres so the panel's end caps
        /// do not sit exactly on the hole's end caps and z-fight. 1 mm is plenty.</param>
        public Mesh BuildGatePanelMesh(int openingIndex, ConveyorSide side, GatePanelPivot pivot,
            out Vector3 localPosition, out Quaternion localRotation, float endInset = 0.001f)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;

            if (side == ConveyorSide.Both)
                throw new ArgumentException("A panel covers one rim. Build two panels for a Both-sided opening.");
            if (!TryGetOpeningSpan(openingIndex, out float t0, out float t1)) return null;

            ProfileData pf = BuildProfile();
            bool leftSide = side == ConveyorSide.Left;
            float sign = leftSide ? -1f : 1f;

            float sweep = t1 >= t0 ? t1 - t0 : 1f - t0 + t1;
            if (sweep <= 1e-6f || _totalLength <= 1e-4f) return null;

            // Pull the ends in slightly, in normalized units.
            float insetT = _totalLength > 1e-4f ? Mathf.Clamp(endInset / _totalLength, 0f, sweep * 0.25f) : 0f;
            float a = Frac(t0 + insetT);
            float span = sweep - insetT * 2f;

            int segs = Mathf.Max(1, Mathf.CeilToInt(sweep * resolution));
            int sCount = segs + 1;

            var ts = new float[sCount];
            for (int s = 0; s < sCount; s++) ts[s] = Frac(a + span * s / segs);

            var wPos = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp = new Vector3[sCount];
            var wFwd = new Vector3[sCount];
            SampleFrames(ts, wPos, wRight, wUp, wFwd);

            float[] perimU = ComputeProfilePerimU(pf.pts);
            float[] splineV = ComputeSplineV(wPos, sCount);

            int firstEdge = leftSide ? pf.leftFirstEdge : pf.rightFirstEdge;
            int lastEdge = leftSide ? pf.leftLastEdge : pf.rightLastEdge;

            var verts = new List<Vector3>((lastEdge - firstEdge + 1) * 2 * sCount + 64);
            var uvs = new List<Vector2>(verts.Capacity);
            var tris = new List<int>(verts.Capacity * 3);

            for (int e = firstEdge; e <= lastEdge; e++)
            {
                Vector2 pa = pf.pts[e];
                Vector2 pb = pf.pts[e + 1];
                float uA = perimU[e], uB = perimU[e + 1];

                int stripBase = verts.Count;
                for (int s = 0; s < sCount; s++)
                {
                    verts.Add(ToWorld(pa, s, wPos, wRight, wUp));
                    verts.Add(ToWorld(pb, s, wPos, wRight, wUp));
                    uvs.Add(new Vector2(uA, splineV[s]));
                    uvs.Add(new Vector2(uB, splineV[s]));
                }
                for (int s = 0; s < segs; s++)
                {
                    int b = stripBase + s * 2;
                    Quad(tris, b, b + 1, b + 2, b + 3, false);
                }
            }

            // Caps face OUTWARD from the panel — the mirror of the hole's caps.
            AddWallCap(pf, 0, leftSide, false, wPos, wRight, wUp, verts, uvs, tris);
            AddWallCap(pf, sCount - 1, leftSide, true, wPos, wRight, wUp, verts, uvs, tris);

            // ── Re-base the vertices onto the pivot ───────────────────────────
            float midT = Frac(t0 + sweep * 0.5f);
            EvaluateLocal(midT, out Vector3 mPos, out _, out Vector3 mUp, out Vector3 mRight);
            Vector3 outward = mRight * sign;

            float pivotY;
            switch (pivot)
            {
                case GatePanelPivot.BeltLevel: pivotY = 0f; break;
                case GatePanelPivot.Top: pivotY = wallAboveBelt; break;
                case GatePanelPivot.Center: pivotY = (wallAboveBelt - railHeight) * 0.5f; break;
                default: pivotY = -railHeight; break;
            }

            localPosition = mPos + outward * beltHalfWidth + mUp * pivotY;
            localRotation = Quaternion.LookRotation(outward, mUp);

            Quaternion inv = Quaternion.Inverse(localRotation);
            for (int i = 0; i < verts.Count; i++)
                verts[i] = inv * (verts[i] - localPosition);

            var mesh = new Mesh
            {
                name = $"ConveyorGatePanel_{openingIndex}_{side}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Profile
        // ─────────────────────────────────────────────────────────────────────

        private struct ProfileData
        {
            public Vector2[] pts;
            public int beltEdge;        // edge index of the belt strip
            public int leftFirstEdge;   // inclusive
            public int leftLastEdge;    // inclusive
            public int rightFirstEdge;  // inclusive
            public int rightLastEdge;   // inclusive
            public int beltLeftIdx;
            public int beltRightIdx;
            public int leftOuterBottom;
            public int rightOuterBottom;
            public float floorY;
        }

        private ProfileData BuildProfile()
        {
            float hw = beltHalfWidth;
            float rw = Mathf.Max(0.001f, railWidth);
            float rh = railHeight;
            float wa = wallAboveBelt;
            float bv = Mathf.Clamp(bevelSize, 0f, Mathf.Min(rw * 0.5f, wa));
            int n = Mathf.Max(1, bevelSegments);

            var list = new List<Vector2>(4 * n + 8);

            // ── left wall, bottom → up → over the top → down to the belt ──
            list.Add(new Vector2(-hw - rw, -rh));                                 // outer bottom
            AddArc(list, new Vector2(-hw - rw + bv, wa - bv), bv, 180f, 90f, n);  // outer top corner
            AddArc(list, new Vector2(-hw - bv, wa - bv), bv, 90f, 0f, n);         // inner top corner
            int beltLeft = list.Count;
            list.Add(new Vector2(-hw, 0f));                                       // belt left edge

            // ── right wall, mirrored ──
            int beltRight = list.Count;
            list.Add(new Vector2(hw, 0f));                                        // belt right edge
            AddArc(list, new Vector2(hw + bv, wa - bv), bv, 180f, 90f, n);        // inner top corner
            AddArc(list, new Vector2(hw + rw - bv, wa - bv), bv, 90f, 0f, n);     // outer top corner
            int rightBottom = list.Count;
            list.Add(new Vector2(hw + rw, -rh));                                  // outer bottom

            return new ProfileData
            {
                pts = list.ToArray(),
                beltEdge = beltLeft,
                leftFirstEdge = 0,
                leftLastEdge = beltLeft - 1,
                rightFirstEdge = beltRight,
                rightLastEdge = rightBottom - 1,
                beltLeftIdx = beltLeft,
                beltRightIdx = beltRight,
                leftOuterBottom = 0,
                rightOuterBottom = rightBottom,
                floorY = -rh,
            };
        }

        private static void AddArc(List<Vector2> into, Vector2 center, float radius,
            float fromDeg, float toDeg, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / segments) * Mathf.Deg2Rad;
                into.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>Normalised range actually swept. Always lo &lt; hi.</summary>
        public void SweepRange(out float lo, out float hi)
        {
            lo = Mathf.Clamp01(Mathf.Min(sweepFrom, sweepTo));
            hi = Mathf.Clamp01(Mathf.Max(sweepFrom, sweepTo));
            if (hi - lo < 1e-4f) { lo = 0f; hi = 1f; }
        }

        /// <summary>True when the sweep does not cover the whole spline.</summary>
        public bool IsPartialSweep
        {
            get { SweepRange(out float lo, out float hi); return lo > 1e-4f || hi < 1f - 1e-4f; }
        }

        /// <summary>
        /// A closed spline's first and last ring sit on the same point but hold separate
        /// vertices, so RecalculateNormals leaves a visible crease at the seam.
        /// Average the two rings back together.
        /// </summary>
        private static void WeldSeamNormals(Mesh mesh, int edgeCount, int sCount)
        {
            var normals = mesh.normals;
            if (normals == null || normals.Length == 0) return;

            for (int e = 0; e < edgeCount; e++)
            {
                int stripBase = e * 2 * sCount;
                int last = stripBase + (sCount - 1) * 2;
                for (int k = 0; k < 2; k++)
                {
                    int a = stripBase + k, b = last + k;
                    if (b >= normals.Length) continue;
                    Vector3 n = (normals[a] + normals[b]).normalized;
                    normals[a] = n;
                    normals[b] = n;
                }
            }
            mesh.normals = normals;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Opening spans
        // ─────────────────────────────────────────────────────────────────────

        private struct Span
        {
            public float t0, t1;
            public bool left, right;
            public bool caps, skirt;
            public float lip;

            public bool Contains(float t) => t0 <= t1 ? (t >= t0 && t <= t1) : (t >= t0 || t <= t1);
        }

        private static float Frac(float v)
        {
            v %= 1f;
            return v < 0f ? v + 1f : v;
        }

        private void ToNormalizedSpan(ConveyorOpening o, out float t0, out float t1)
        {
            if (o.measure == ConveyorMeasure.Meters && _totalLength > 0.0001f)
            {
                t0 = TFromDistance(o.start);
                t1 = TFromDistance(o.start + Mathf.Max(0f, o.length));
            }
            else
            {
                t0 = Frac(o.start);
                t1 = Frac(o.start + Mathf.Max(0f, o.length));
            }

            bool closed = _spline != null && _spline.Spline != null && _spline.Spline.Closed;
            if (!closed && o.measure == ConveyorMeasure.Meters)
            {
                t0 = Mathf.Clamp01(t0);
                t1 = Mathf.Clamp01(t1);
            }
        }

        private List<Span> BuildSpans()
        {
            var spans = new List<Span>();
            if (openings == null) return spans;

            foreach (var o in openings)
            {
                if (o == null || !o.enabled || o.length <= 0f) continue;
                ToNormalizedSpan(o, out float t0, out float t1);
                if (Mathf.Abs(t1 - t0) < 1e-6f) continue;

                spans.Add(new Span
                {
                    t0 = t0,
                    t1 = t1,
                    left = o.CutsLeft,
                    right = o.CutsRight,
                    caps = o.caps,
                    skirt = o.skirt,
                    lip = Mathf.Clamp(o.lipHeight, 0f, wallAboveBelt),
                });
            }
            return spans;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Arc-length LUT (world space)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildLengthLut()
        {
            const int N = 512;
            if (_lutT == null || _lutT.Length != N + 1)
            {
                _lutT = new float[N + 1];
                _lutD = new float[N + 1];
            }

            Vector3 prev = Vector3.zero;
            float acc = 0f;
            for (int i = 0; i <= N; i++)
            {
                float t = (float)i / N;
                _spline.Spline.Evaluate(t, out var p, out _, out _);
                Vector3 w = transform.TransformPoint((Vector3)p);
                if (i > 0) acc += Vector3.Distance(w, prev);
                prev = w;
                _lutT[i] = t;
                _lutD[i] = acc;
            }
            _totalLength = acc;
        }

        private float TFromDistance(float distance)
        {
            if (_lutD == null || _totalLength <= 0.0001f) return 0f;

            bool closed = _spline.Spline.Closed;
            float d = closed ? Mathf.Repeat(distance, _totalLength) : Mathf.Clamp(distance, 0f, _totalLength);

            int lo = 0, hi = _lutD.Length - 1;
            while (lo + 1 < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_lutD[mid] <= d) lo = mid; else hi = mid;
            }
            float seg = _lutD[hi] - _lutD[lo];
            float f = seg > 0.0001f ? (d - _lutD[lo]) / seg : 0f;
            return Mathf.Lerp(_lutT[lo], _lutT[hi], f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sampling
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Uniform samples plus one forced sample at every opening boundary, so an
        /// opening starts and ends exactly where the author asked.
        /// </summary>
        private float[] BuildSampleTs(List<Span> spans)
        {
            const float eps = 1e-4f;
            SweepRange(out float lo, out float hi);

            var forced = new List<float>();
            foreach (var s in spans)
            {
                float a = Mathf.Clamp01(s.t0), b = Mathf.Clamp01(s.t1);
                if (a > lo + eps && a < hi - eps) forced.Add(a);
                if (b > lo + eps && b < hi - eps) forced.Add(b);
            }

            var all = new List<float>(resolution + 1 + forced.Count * 2);
            all.AddRange(forced);

            for (int i = 0; i <= resolution; i++)
            {
                float t = Mathf.Lerp(lo, hi, (float)i / resolution);
                bool clash = false;
                foreach (var f in forced)
                {
                    if (Mathf.Abs(f - t) < eps) { clash = true; break; }
                }
                if (!clash) all.Add(t);
            }

            all.Sort();

            // strip near-duplicates that survived (two openings sharing a boundary)
            var result = new List<float>(all.Count) { all[0] };
            for (int i = 1; i < all.Count; i++)
                if (all[i] - result[result.Count - 1] > eps * 0.5f) result.Add(all[i]);

            if (result[0] > lo + eps) result.Insert(0, lo);
            if (result[result.Count - 1] < hi - eps) result.Add(hi);

            return result.ToArray();
        }

        private void SampleFrames(float[] ts, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp, Vector3[] wFwd)
        {
            for (int s = 0; s < ts.Length; s++)
            {
                _spline.Spline.Evaluate(ts[s], out var pos, out var tan, out var up);

                Vector3 fwd = ((Vector3)tan).normalized;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                Vector3 upL = ((Vector3)up).normalized;
                if (upL.sqrMagnitude < 0.001f) upL = Vector3.up;

                wPos[s] = (Vector3)pos;
                wRight[s] = Vector3.Cross(upL, fwd).normalized;
                wUp[s] = upL;
                wFwd[s] = fwd;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core sweep
        // ─────────────────────────────────────────────────────────────────────

        private Mesh Sweep()
        {
            ProfileData pf = BuildProfile();
            Vector2[] profile = pf.pts;
            int pCount = profile.Length;
            int edgeCount = closeBottom ? pCount : pCount - 1;

            List<Span> spans = BuildSpans();
            float[] ts = BuildSampleTs(spans);
            int sCount = ts.Length;
            int segCount = sCount - 1;

            var wPos = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp = new Vector3[sCount];
            var wFwd = new Vector3[sCount];
            SampleFrames(ts, wPos, wRight, wUp, wFwd);

            float[] perimU = ComputeProfilePerimU(profile);
            float[] splineV = ComputeSplineV(wPos, sCount);

            var verts = new List<Vector3>(edgeCount * 2 * sCount + 128);
            var uvs = new List<Vector2>(edgeCount * 2 * sCount + 128);
            var trisWall = new List<int>(edgeCount * segCount * 6 + 128);
            var trisBelt = new List<int>(segCount * 6);
            var trisGate = new List<int>(256);
            List<int> gateTarget = separateGateSubmesh ? trisGate : trisWall;

            bool isBranchTrack = GetComponent<ConveyorController>() == null && GetComponent<BranchPath>() != null;

            // per-segment cull flags, evaluated once at the segment midpoint
            var cutLeft = new bool[segCount];
            var cutRight = new bool[segCount];
            for (int s = 0; s < segCount; s++)
            {
                float mid = (ts[s] + ts[s + 1]) * 0.5f;
                foreach (var span in spans)
                {
                    if (!span.Contains(mid)) continue;
                    if (span.left) cutLeft[s] = true;
                    if (span.right) cutRight[s] = true;
                }
            }

            // ── Sweep vertices ────────────────────────────────────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                Vector2 pa = profile[e];
                Vector2 pb = profile[(e + 1) % pCount];
                float uA = perimU[e], uB = perimU[e + 1];

                for (int s = 0; s < sCount; s++)
                {
                    Vector3 posA = ToWorld(pa, s, wPos, wRight, wUp);
                    Vector3 posB = ToWorld(pb, s, wPos, wRight, wUp);

                    if (trimBranchEnd && mainTrackSpline != null && !isDraggingInEditor)
                    {
                        posA = ClipVertex(posA);
                        posB = ClipVertex(posB);
                    }

                    verts.Add(posA);
                    verts.Add(posB);
                    float v = splineV[s];
                    uvs.Add(new Vector2(uA, v));
                    uvs.Add(new Vector2(uB, v));
                }
            }

            // ── Triangles ─────────────────────────────────────────────────────
            for (int e = 0; e < edgeCount; e++)
            {
                int stripBase = e * 2 * sCount;
                bool isBelt = e == pf.beltEdge;
                bool isLeftWall = e >= pf.leftFirstEdge && e <= pf.leftLastEdge;
                bool isRightWall = e >= pf.rightFirstEdge && e <= pf.rightLastEdge;

                for (int s = 0; s < segCount; s++)
                {
                    if (isBranchTrack && ShouldTrimBranchSegment(s, sCount, profile, wPos, wRight, wUp)) continue;

                    if (isLeftWall && cutLeft[s]) continue;
                    if (isRightWall && cutRight[s]) continue;

                    int b = stripBase + s * 2;
                    Quad(isBelt ? trisBelt : trisWall, b, b + 1, b + 2, b + 3, false);
                }
            }

            // ── Gate geometry: caps, skirt, kerb ──────────────────────────────
            AddGateGeometry(pf, spans, ts, splineV, wPos, wRight, wUp, cutLeft, cutRight, verts, uvs, gateTarget);

            // ── Caps where a partial sweep starts and ends ─────────────────────
            // A branch trimmed at a junction would otherwise show a hollow rim.
            if (capSweepEnds && IsPartialSweep)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool leftSide = pass == 0;
                    if (!cutLeft[0] && leftSide || !cutRight[0] && !leftSide)
                        AddWallCap(pf, 0, leftSide, false, wPos, wRight, wUp, verts, uvs, trisWall);
                    if (!cutLeft[segCount - 1] && leftSide || !cutRight[segCount - 1] && !leftSide)
                        AddWallCap(pf, sCount - 1, leftSide, true, wPos, wRight, wUp, verts, uvs, trisWall);
                }
            }

            // ── Assemble ──────────────────────────────────────────────────────
            var mesh = new Mesh
            {
                name = "ConveyorTrack_Swept",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = separateGateSubmesh ? 3 : 2;
            mesh.SetTriangles(trisWall, 0);
            mesh.SetTriangles(trisBelt, 1);
            if (separateGateSubmesh) mesh.SetTriangles(trisGate, 2);
            mesh.RecalculateNormals();
            if (_spline.Spline.Closed && !IsPartialSweep) WeldSeamNormals(mesh, edgeCount, sCount);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Quad from (a0,b0) on ring s to (a1,b1) on ring s+1. Unflipped faces +right / +up.</summary>
        private static void Quad(List<int> tris, int a0, int b0, int a1, int b1, bool flip)
        {
            if (!flip)
            {
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(b0); tris.Add(a1); tris.Add(b1);
            }
            else
            {
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(b0); tris.Add(b1); tris.Add(a1);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gate geometry
        // ─────────────────────────────────────────────────────────────────────

        private void AddGateGeometry(ProfileData pf, List<Span> spans, float[] ts, float[] splineV,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            bool[] cutLeft, bool[] cutRight,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            int segCount = ts.Length - 1;
            if (segCount <= 0) return;

            // Two overlapping openings on the same rim must not each emit a skirt —
            // the duplicate faces would be coplanar and z-fight.
            var insertDone = new[] { new bool[segCount], new bool[segCount] };

            foreach (var span in spans)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool leftSide = pass == 0;
                    if (leftSide && !span.left) continue;
                    if (!leftSide && !span.right) continue;

                    bool[] done = insertDone[pass];

                    // contiguous runs of segments cut by THIS span on THIS side
                    int s = 0;
                    while (s < segCount)
                    {
                        if (!span.Contains((ts[s] + ts[s + 1]) * 0.5f)) { s++; continue; }

                        int runStart = s;
                        while (s < segCount && span.Contains((ts[s] + ts[s + 1]) * 0.5f)) s++;
                        int runEnd = s; // exclusive

                        if (span.skirt || span.lip > 0f)
                        {
                            // split the run around segments another span already covered
                            int i = runStart;
                            while (i < runEnd)
                            {
                                if (done[i]) { i++; continue; }
                                int subStart = i;
                                while (i < runEnd && !done[i]) { done[i] = true; i++; }
                                AddGateWallInsert(pf, span, subStart, i, leftSide,
                                    splineV, wPos, wRight, wUp, verts, uvs, tris);
                            }
                        }

                        if (span.caps)
                        {
                            // cap at the entry boundary faces forward, exit boundary faces back
                            bool capStart = runStart > 0 && !(leftSide ? cutLeft : cutRight)[runStart - 1];
                            bool capEnd = runEnd < segCount && !(leftSide ? cutLeft : cutRight)[runEnd];
                            if (capStart) AddWallCap(pf, runStart, leftSide, true, wPos, wRight, wUp, verts, uvs, tris);
                            if (capEnd) AddWallCap(pf, runEnd, leftSide, false, wPos, wRight, wUp, verts, uvs, tris);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// What is left behind inside an opening.
        /// lip == 0 → a single outward-facing skirt covering the side of the belt slab.
        /// lip &gt; 0 → a short kerb wall: inner face, top face and outer face.
        /// </summary>
        private void AddGateWallInsert(ProfileData pf, Span span, int sFrom, int sTo, bool leftSide,
            float[] splineV, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float sign = leftSide ? -1f : 1f;
            float innerX = sign * beltHalfWidth;
            float outerX = sign * (beltHalfWidth + railWidth);
            float topY = span.lip;
            float floorY = pf.floorY;

            int count = sTo - sFrom + 1;

            // ---- inner / skirt face at innerX, from topY down to floorY ----
            if (span.skirt || span.lip > 0f)
            {
                int b0 = verts.Count;
                for (int i = 0; i < count; i++)
                {
                    int s = sFrom + i;
                    verts.Add(ToWorld(new Vector2(innerX, topY), s, wPos, wRight, wUp));
                    verts.Add(ToWorld(new Vector2(innerX, floorY), s, wPos, wRight, wUp));
                    uvs.Add(new Vector2(0f, splineV[s])); uvs.Add(new Vector2(1f, splineV[s]));
                }
                // lip == 0: this is the visible outside of the belt slab → face outward.
                // lip  > 0: this is the inside of a kerb → face inward.
                bool faceOutward = span.lip <= 0f;
                bool flip = faceOutward ? leftSide : !leftSide;
                for (int i = 0; i < count - 1; i++)
                {
                    int b = b0 + i * 2;
                    Quad(tris, b, b + 1, b + 2, b + 3, flip);
                }
            }

            if (span.lip <= 0f) return;

            // ---- kerb top face, innerX → outerX at y = topY ----
            {
                int b0 = verts.Count;
                for (int i = 0; i < count; i++)
                {
                    int s = sFrom + i;
                    verts.Add(ToWorld(new Vector2(innerX, topY), s, wPos, wRight, wUp));
                    verts.Add(ToWorld(new Vector2(outerX, topY), s, wPos, wRight, wUp));
                    uvs.Add(new Vector2(0f, splineV[s])); uvs.Add(new Vector2(1f, splineV[s]));
                }
                for (int i = 0; i < count - 1; i++)
                {
                    int b = b0 + i * 2;
                    Quad(tris, b, b + 1, b + 2, b + 3, leftSide);
                }
            }

            // ---- kerb outer face at outerX, topY down to floorY ----
            {
                int b0 = verts.Count;
                for (int i = 0; i < count; i++)
                {
                    int s = sFrom + i;
                    verts.Add(ToWorld(new Vector2(outerX, topY), s, wPos, wRight, wUp));
                    verts.Add(ToWorld(new Vector2(outerX, floorY), s, wPos, wRight, wUp));
                    uvs.Add(new Vector2(0f, splineV[s])); uvs.Add(new Vector2(1f, splineV[s]));
                }
                for (int i = 0; i < count - 1; i++)
                {
                    int b = b0 + i * 2;
                    Quad(tris, b, b + 1, b + 2, b + 3, leftSide);
                }
            }
        }

        /// <summary>Flat cap closing the cut cross-section of a wall at ring s.</summary>
        private void AddWallCap(ProfileData pf, int s, bool leftSide, bool faceForward,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            Vector2[] profile = pf.pts;
            var poly = new List<Vector2>(profile.Length);

            if (leftSide)
            {
                for (int i = pf.beltLeftIdx; i >= pf.leftOuterBottom; i--) poly.Add(profile[i]);
                poly.Add(new Vector2(profile[pf.beltLeftIdx].x, pf.floorY));
            }
            else
            {
                for (int i = pf.beltRightIdx; i <= pf.rightOuterBottom; i++) poly.Add(profile[i]);
                poly.Add(new Vector2(profile[pf.beltRightIdx].x, pf.floorY));
            }

            int baseIdx = verts.Count;
            foreach (var p in poly)
            {
                verts.Add(ToWorld(p, s, wPos, wRight, wUp));
                uvs.Add(new Vector2(0.5f, 0.5f));
            }

            // right-side polygon winds CW in (right, up) → natural normal is -forward
            // left-side polygon winds CCW → natural normal is +forward
            bool naturalIsForward = leftSide;
            bool flip = faceForward != naturalIsForward;

            for (int i = 1; i < poly.Count - 1; i++)
            {
                int a = baseIdx, b = baseIdx + i, c = baseIdx + i + 1;
                if (!flip) { tris.Add(a); tris.Add(b); tris.Add(c); }
                else { tris.Add(a); tris.Add(c); tris.Add(b); }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Vector3 ToWorld(Vector2 p, int s, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
            => wPos[s] + wRight[s] * p.x + wUp[s] * p.y;

        private static float[] ComputeProfilePerimU(Vector2[] profile)
        {
            int n = profile.Length;
            var acc = new float[n + 1];
            acc[0] = 0f;
            for (int i = 1; i < n; i++)
                acc[i] = acc[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            acc[n] = acc[n - 1] + Vector2.Distance(profile[n - 1], profile[0]);

            float total = acc[n];
            if (total > 0f)
                for (int i = 0; i <= n; i++) acc[i] /= total;

            return acc;
        }

        private float[] ComputeSplineV(Vector3[] wPos, int sCount)
        {
            var dist = new float[sCount];
            float total = 0f;

            for (int s = 1; s < sCount; s++)
            {
                total += Vector3.Distance(wPos[s], wPos[s - 1]);
                dist[s] = total;
            }

            if (total > 0f)
                for (int s = 0; s < sCount; s++)
                    dist[s] = (dist[s] / total) * vTiling;

            return dist;
        }

        private bool ShouldTrimBranchSegment(int s, int sCount, Vector2[] profile,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            if (trimBranchEnd && mainTrackSpline != null && !isDraggingInEditor)
            {
                return IsRingFullyInsideConveyor(s, profile, wPos, wRight, wUp) &&
                       IsRingFullyInsideConveyor(s + 1, profile, wPos, wRight, wUp);
            }

            float distToEnd = Vector3.Distance(wPos[s], wPos[sCount - 1]);
            return distToEnd < beltHalfWidth + railWidth + 0.05f;
        }

        private Vector3 ClipVertex(Vector3 localPos)
        {
            if (mainTrackSpline == null) return localPos;

            Vector3 worldPos = transform.TransformPoint(localPos);
            Vector3 mainLocalPos = mainTrackSpline.transform.InverseTransformPoint(worldPos);

            SplineUtility.GetNearestPoint(mainTrackSpline.Spline, mainLocalPos,
                out var nearestLocal, out float t, 8, 4);

            Vector3 worldNearest = mainTrackSpline.transform.TransformPoint((Vector3)nearestLocal);

            mainTrackSpline.Spline.Evaluate(t, out _, out var mTan, out var mUpLocal);
            Vector3 localTan = ((Vector3)mTan).normalized;
            Vector3 localUp = ((Vector3)mUpLocal).normalized;
            if (localUp.sqrMagnitude < 0.001f) localUp = Vector3.up;
            Vector3 localRight = Vector3.Cross(localUp, localTan).normalized;
            Vector3 worldRight = mainTrackSpline.transform.TransformDirection(localRight).normalized;
            Vector3 outwardDirection = branchOnRightSide ? worldRight : -worldRight;

            float projection = Vector3.Dot(worldPos - worldNearest, outwardDirection);
            float R = beltHalfWidth + railWidth;

            if (projection < R)
            {
                Vector3 worldClipped = worldNearest + outwardDirection * R;
                worldClipped.y = worldPos.y;
                return transform.InverseTransformPoint(worldClipped);
            }

            return localPos;
        }

        private bool IsRingFullyInsideConveyor(int s, Vector2[] profile,
            Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            if (mainTrackSpline == null) return false;

            float R = beltHalfWidth + railWidth;

            for (int p = 0; p < profile.Length; p++)
            {
                Vector3 localV = ToWorld(profile[p], s, wPos, wRight, wUp);
                Vector3 worldV = transform.TransformPoint(localV);
                Vector3 mainLocalV = mainTrackSpline.transform.InverseTransformPoint(worldV);

                SplineUtility.GetNearestPoint(mainTrackSpline.Spline, mainLocalV,
                    out var nearestLocal, out float t, 8, 4);

                Vector3 worldNearest = mainTrackSpline.transform.TransformPoint((Vector3)nearestLocal);
                mainTrackSpline.Spline.Evaluate(t, out _, out var mTan, out var mUpLocal);
                Vector3 localTan = ((Vector3)mTan).normalized;
                Vector3 localUp = ((Vector3)mUpLocal).normalized;
                if (localUp.sqrMagnitude < 0.001f) localUp = Vector3.up;
                Vector3 localRight = Vector3.Cross(localUp, localTan).normalized;
                Vector3 worldRight = mainTrackSpline.transform.TransformDirection(localRight).normalized;
                Vector3 outwardDirection = branchOnRightSide ? worldRight : -worldRight;

                if (Vector3.Dot(worldV - worldNearest, outwardDirection) >= R) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (openings == null || openings.Count == 0) return;
            Cache();
            if (_spline == null || _spline.Spline == null || _spline.Spline.Count < 2) return;
            if (_lutD == null) BuildLengthLut();

            foreach (var o in openings)
            {
                if (o == null || !o.enabled || o.length <= 0f) continue;
                ToNormalizedSpan(o, out float t0, out float t1);
                Gizmos.color = o.gizmoColor;

                const int steps = 24;
                float sweep = t1 >= t0 ? t1 - t0 : 1f - t0 + t1;

                for (int side = 0; side < 2; side++)
                {
                    bool leftSide = side == 0;
                    if (leftSide && !o.CutsLeft) continue;
                    if (!leftSide && !o.CutsRight) continue;
                    float sign = leftSide ? -1f : 1f;

                    Vector3 prevTop = Vector3.zero, prevBottom = Vector3.zero;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = Frac(t0 + sweep * i / steps);
                        EvaluateWorld(t, out Vector3 pos, out _, out Vector3 up, out Vector3 right);
                        Vector3 outward = right * (sign * (beltHalfWidth + railWidth));
                        Vector3 top = pos + outward + up * wallAboveBelt;
                        Vector3 bottom = pos + outward - up * railHeight;
                        if (i > 0)
                        {
                            Gizmos.DrawLine(prevTop, top);
                            Gizmos.DrawLine(prevBottom, bottom);
                        }
                        if (i == 0 || i == steps) Gizmos.DrawLine(top, bottom);
                        prevTop = top; prevBottom = bottom;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Editor
        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorTrackMeshBuilder))]
        public class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                var builder = (ConveyorTrackMeshBuilder)target;

                DrawDefaultInspector();

                GUILayout.Space(8);
                if (builder.TotalLength > 0f)
                    UnityEditor.EditorGUILayout.HelpBox(
                        $"Spline length: {builder.TotalLength:0.###} m", UnityEditor.MessageType.None);

                using (new UnityEditor.EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(30)))
                    {
                        builder.BuildMesh();
                        UnityEditor.EditorUtility.SetDirty(builder.gameObject);
                    }
                    if (GUILayout.Button("Add Opening", GUILayout.Height(30)))
                    {
                        UnityEditor.Undo.RecordObject(builder, "Add Opening");
                        builder.AddOpening(0.25f, 0.08f);
                        UnityEditor.EditorUtility.SetDirty(builder);
                    }
                }
            }

            private void OnSceneGUI()
            {
                var builder = (ConveyorTrackMeshBuilder)target;
                if (builder.openings == null) return;

                for (int i = 0; i < builder.openings.Count; i++)
                {
                    var o = builder.openings[i];
                    if (o == null || !o.enabled) continue;

                    builder.ToNormalizedSpan(o, out float t0, out float t1);

                    // both handles must always be drawn — do not short-circuit
                    bool movedStart = DragHandle(builder, o.gizmoColor, ref t0);
                    bool movedEnd = DragHandle(builder, o.gizmoColor, ref t1);
                    if (movedStart || movedEnd)
                    {
                        UnityEditor.Undo.RecordObject(builder, "Move Opening");
                        if (o.measure == ConveyorMeasure.Meters && builder.TotalLength > 0f)
                        {
                            float d0 = t0 * builder.TotalLength;
                            float d1 = t1 * builder.TotalLength;
                            o.start = Mathf.Min(d0, d1);
                            o.length = Mathf.Abs(d1 - d0);
                        }
                        else
                        {
                            o.start = Mathf.Min(t0, t1);
                            o.length = Mathf.Abs(t1 - t0);
                        }
                        builder.BuildMesh();
                        UnityEditor.EditorUtility.SetDirty(builder);
                    }

                    if (builder.TryGetOpeningAnchor(i, out Vector3 anchor, out _))
                    {
                        UnityEditor.Handles.color = o.gizmoColor;
                        UnityEditor.Handles.Label(anchor + Vector3.up * 0.15f, o.label);
                    }
                }
            }

            private static bool DragHandle(ConveyorTrackMeshBuilder builder, Color color, ref float t)
            {
                builder.EvaluateWorld(t, out Vector3 pos, out _, out Vector3 up, out _);
                Vector3 handlePos = pos + up * builder.wallAboveBelt;

                UnityEditor.EditorGUI.BeginChangeCheck();
                UnityEditor.Handles.color = color;
                float size = UnityEditor.HandleUtility.GetHandleSize(handlePos) * 0.09f;
                Vector3 moved = UnityEditor.Handles.FreeMoveHandle(
                    handlePos, size, Vector3.zero, UnityEditor.Handles.SphereHandleCap);
                if (!UnityEditor.EditorGUI.EndChangeCheck()) return false;

                var container = builder.GetComponent<SplineContainer>();
                Vector3 local = container.transform.InverseTransformPoint(moved);
                SplineUtility.GetNearestPoint(container.Spline, local, out _, out float nearestT, 16, 4);
                t = Mathf.Clamp01(nearestT);
                return true;
            }
        }
#endif
    }
}
