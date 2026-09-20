using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    public enum ConveyorMeasure
    {
        Normalized = 0,
        Meters = 1,
    }

    public enum ConveyorSide { Left = 0, Right = 1, Both = 2 }

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

        // Compatibility
        public string name { get => label; set => label = value; }
        public float startT { get => start; set => start = value; }
        public float endT { get => start + length; set => length = value - start; }
        public bool cutInnerWall = true;
        public bool cutOuterWall = false;
        public bool cutBelt = false;
        public float Length(float splineLength) => measure == ConveyorMeasure.Meters ? length : length * splineLength;
        public bool CutsLeft => enabled && (side == ConveyorSide.Left || side == ConveyorSide.Both);
        public bool CutsRight => enabled && (side == ConveyorSide.Right || side == ConveyorSide.Both);
    }

    /// <summary>
    /// Clean closed-profile spline sweep — cloned from Soda Shippers (ConveyorMeshBuilder.cs).
    /// Builds a grooved belt with raised side rails, swept along a SplineContainer.
    /// Sub-mesh 0 = side rails, sub-mesh 1 = flat belt surface.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ConveyorTrackMeshBuilder : MonoBehaviour
    {
        public enum GatePanelPivot { Bottom, Top, Center, BeltLevel, Left, Right }

        [Header("Cross-Section Profile")]
        [Tooltip("Half-width of the flat belt surface (inner groove width = 2 x this)")]
        public float beltHalfWidth = 0.5f;
        [Tooltip("How far the outer walls rise ABOVE the belt surface")]
        public float wallAboveBelt = 0.12f;
        [Tooltip("How far the outer walls hang DOWN below the belt surface")]
        public float railHeight = 0.5f;
        [Tooltip("Thickness of each outer wall")]
        public float railWidth = 0.05f;
        [Tooltip("Chamfer size on the top-outer corner of each wall (clamped to railWidth)")]
        public float bevelSize = 0.04f;

        [Header("Sweep Quality")]
        [Range(20, 400)]
        public int resolution = 60;

        [Header("UV Tiling")]
        [Tooltip("How many times the belt-top texture tiles along the spline length")]
        public float vTiling = 8f;

        [Header("Branch end trim")]
        [Tooltip("Clip this track's s=resolution end onto MainTrackSpline's outer wall.")]
        public bool trimBranchEnd;
        public SplineContainer mainTrackSpline;

        // Backward compatibility fields
        public float sweepFrom = 0f;
        public float sweepTo = 1f;
        [HideInInspector] public bool closeBottom;
        [HideInInspector] public bool openZoneEnabled;
        [HideInInspector] public float openZoneHalfT = 0.015f;
        [HideInInspector] public List<ConveyorOpening> openings = new();
        [HideInInspector] public bool branchOnRightSide = true;
        [HideInInspector] public bool isDraggingInEditor;

        public int OpeningCount => openings != null ? openings.Count : 0;
        public float RimOffset => beltHalfWidth + railWidth;
        public float TotalLength
        {
            get
            {
                if (_spline == null) _spline = GetComponent<SplineContainer>();
                return (_spline != null && _spline.Spline != null)
                    ? SplineUtility.CalculateLength(_spline.Spline, transform.localToWorldMatrix)
                    : 1f;
            }
        }

        public void EvaluateWorld(float t, out Vector3 position, out Vector3 forward,
            out Vector3 up, out Vector3 right)
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_spline == null || _spline.Spline == null)
            {
                position = transform.position;
                forward = transform.forward;
                up = transform.up;
                right = transform.right;
                return;
            }
            float clampedT = _spline.Spline.Closed ? (t % 1f + 1f) % 1f : Mathf.Clamp01(t);
            _spline.Spline.Evaluate(clampedT, out var p, out var tan, out var u);
            position = transform.TransformPoint((Vector3)p);
            forward = transform.TransformDirection(((Vector3)tan).normalized);
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            up = transform.TransformDirection(((Vector3)u).normalized);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }

        public bool TryGetOpeningSpan(int index, out float t0, out float t1)
        {
            t0 = t1 = 0f;
            if (openings == null || index < 0 || index >= openings.Count) return false;
            var op = openings[index];
            if (op == null || !op.enabled) return false;
            float len = TotalLength;
            t0 = op.start;
            float sweep = op.measure == ConveyorMeasure.Meters && len > 1e-4f ? op.length / len : op.length;
            t1 = t0 + sweep;
            return true;
        }

        public Mesh BuildGatePanelMesh(int openingIndex, ConveyorSide side, GatePanelPivot pivot,
            out Vector3 localPosition, out Quaternion localRotation, float endInset = 0.001f)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            if (openings == null || openingIndex < 0 || openingIndex >= openings.Count) return null;
            var op = openings[openingIndex];
            if (op == null) return null;

            float sweep = op.measure == ConveyorMeasure.Meters && TotalLength > 1e-4f ? op.length / TotalLength : op.length;
            float midT = op.start + sweep * 0.5f;
            EvaluateWorld(midT, out var wPos, out var wFwd, out var wUp, out var wRight);
            float sign = side == ConveyorSide.Left ? -1f : 1f;
            Vector3 center = wPos + wRight * (sign * RimOffset);

            localPosition = transform.InverseTransformPoint(center);
            localRotation = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(wFwd, wUp);

            float gateLength = op.measure == ConveyorMeasure.Meters ? op.length : op.length * TotalLength;
            gateLength = Mathf.Max(0.01f, gateLength - endInset * 2f);
            float gateHeight = wallAboveBelt + railHeight;
            float gateThickness = railWidth;

            var mesh = new Mesh { name = $"GatePanel_{openingIndex}" };
            Vector3 h = new Vector3(gateThickness, gateHeight, gateLength) * 0.5f;
            Vector3[] v = new Vector3[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            int[] tri = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };
            mesh.vertices = v;
            mesh.triangles = tri;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public float GetOpeningLength(int index)
        {
            if (openings == null || index < 0 || index >= openings.Count) return 0f;
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            float len = (_spline != null && _spline.Spline != null)
                ? SplineUtility.CalculateLength(_spline.Spline, transform.localToWorldMatrix)
                : 1f;
            return openings[index].Length(len);
        }

        public bool TryGetOpeningPose(int index, float u, out Vector3 pos, out Quaternion rot, out Vector3 forward)
        {
            pos = transform.position;
            rot = transform.rotation;
            forward = transform.forward;
            if (openings == null || index < 0 || index >= openings.Count) return false;
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_spline == null || _spline.Spline == null) return false;

            var opening = openings[index];
            float t = Mathf.Lerp(opening.startT, opening.endT, u);
            _spline.Spline.Evaluate(t, out var p, out var tan, out var up);
            pos = transform.TransformPoint((Vector3)p);
            forward = transform.TransformDirection(((Vector3)tan).normalized);
            var upDir = transform.TransformDirection(((Vector3)up).normalized);
            if (upDir == Vector3.zero) upDir = Vector3.up;
            rot = Quaternion.LookRotation(forward, upDir);
            return true;
        }

        private SplineContainer _spline;
        private MeshFilter _meshFilter;

        public float BeltHalfWidth { get => beltHalfWidth; set => beltHalfWidth = value; }
        public float RailWidth { get => railWidth; set => railWidth = value; }
        public float OuterRadius => beltHalfWidth + railWidth;

        public void SetBranchTrim(SplineContainer mainTrack)
        {
            trimBranchEnd = mainTrack != null;
            mainTrackSpline = mainTrack;
        }

        private void Awake()
        {
            _spline = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        public void BuildMesh()
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = Sweep();
        }

        public Vector2[] BuildProfile()
        {
            float hw = beltHalfWidth;
            float rw = railWidth;
            float rh = railHeight;
            float wa = wallAboveBelt;
            float bv = Mathf.Clamp(bevelSize, 0f, Mathf.Min(rw * 0.5f, wa));

            return new[]
            {
                new Vector2(-hw - rw, -rh),          // P0  left outer-bottom
                new Vector2(-hw - rw, wa - bv),       // P1  left outer-top, vertical side
                new Vector2(-hw - rw + bv, wa),       // P2  left outer-top, horizontal side
                new Vector2(-hw - bv, wa),            // P3  left inner-top, horizontal side
                new Vector2(-hw, wa - bv),             // P4  left inner-top, vertical side
                new Vector2(-hw, 0f),                  // P5  belt left edge
                new Vector2(hw, 0f),                   // P6  belt right edge
                new Vector2(hw, wa - bv),              // P7
                new Vector2(hw + bv, wa),              // P8
                new Vector2(hw + rw - bv, wa),         // P9
                new Vector2(hw + rw, wa - bv),         // P10
                new Vector2(hw + rw, -rh),             // P11 right outer-bottom
            };
        }

        public Mesh Sweep()
        {
            if (_spline == null) _spline = GetComponent<SplineContainer>();
            if (_spline == null || _spline.Spline == null || _spline.Spline.Count < 2)
                return null;

            var profile = BuildProfile();
            int pCount = profile.Length;
            int edgeCount = pCount - 1; // excludes underside
            int sCount = resolution + 1;

            var wPos = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp = new Vector3[sCount];
            SampleFrames(sCount, wPos, wRight, wUp);

            var perimU = ComputeProfilePerimU(profile);
            var splineV = ComputeSplineV(wPos, sCount);

            const int beltEdge = 5;
            int sweepVCount = edgeCount * 2 * sCount;

            var verts = new List<Vector3>(sweepVCount + 32);
            var uvs = new List<Vector2>(sweepVCount + 32);
            var trisWall = new List<int>((edgeCount - 1) * resolution * 6 + 32);
            var trisBelt = new List<int>(resolution * 6);

            bool trimming = trimBranchEnd && mainTrackSpline != null;

            for (int e = 0; e < edgeCount; e++)
            {
                var pa = profile[e];
                var pb = profile[(e + 1) % pCount];
                float uA = perimU[e];
                float uB = perimU[(e + 1) % pCount];

                for (int s = 0; s < sCount; s++)
                {
                    var posA = ToWorld(pa, s, wPos, wRight, wUp);
                    var posB = ToWorld(pb, s, wPos, wRight, wUp);
                    if (trimming)
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

            for (int e = 0; e < edgeCount; e++)
            {
                int stripBase = e * 2 * sCount;
                bool isBelt = e == beltEdge;
                var tris = isBelt ? trisBelt : trisWall;

                for (int s = 0; s < resolution; s++)
                {
                    if (trimming &&
                        IsRingFullyInsideConveyor(s, profile, wPos, wRight, wUp) &&
                        IsRingFullyInsideConveyor(s + 1, profile, wPos, wRight, wUp))
                        continue;

                    int b = stripBase + s * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            if (!_spline.Spline.Closed)
            {
                AddEndCap(profile, wPos, wRight, wUp, verts, uvs, trisWall, 0);
                if (!trimming)
                    AddEndCap(profile, wPos, wRight, wUp, verts, uvs, trisWall, resolution);
            }

            var mesh = new Mesh
            {
                name = "ConveyorTrack_Swept",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisWall, 0);
            mesh.SetTriangles(trisBelt, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddEndCap(Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> trisWall, int s)
        {
            int baseIdx = verts.Count;
            foreach (var p in profile)
            {
                verts.Add(ToWorld(p, s, wPos, wRight, wUp));
                uvs.Add(Vector2.zero);
            }

            for (int i = 1; i < profile.Length - 1; i++)
            {
                int a = baseIdx;
                int b = baseIdx + i;
                int c = baseIdx + i + 1;
                trisWall.Add(a); trisWall.Add(b); trisWall.Add(c);
                trisWall.Add(a); trisWall.Add(c); trisWall.Add(b);
            }
        }

        private static Vector3 ToWorld(Vector2 p, int s, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
            => wPos[s] + wRight[s] * p.x + wUp[s] * p.y;

        private Vector3 ClipVertex(Vector3 localPos)
        {
            var worldPos = transform.TransformPoint(localPos);
            var mainLocalPos = mainTrackSpline.transform.InverseTransformPoint(worldPos);

            SplineUtility.GetNearestPoint(mainTrackSpline.Spline, mainLocalPos, out var nearestLocal, out var t, 8, 4);

            var worldNearest = mainTrackSpline.transform.TransformPoint((Vector3)nearestLocal);

            mainTrackSpline.Spline.Evaluate(t, out _, out var mTan, out var mUp);
            var localTan = ((Vector3)mTan).normalized;
            var localUp = ((Vector3)mUp).normalized;
            if (localUp.sqrMagnitude < 0.001f) localUp = Vector3.up;
            var localRight = Vector3.Cross(localUp, localTan).normalized;
            var worldRight = mainTrackSpline.transform.TransformDirection(localRight).normalized;

            var toPos = worldPos - worldNearest;
            var outwardDirection = Vector3.Dot(toPos, worldRight) >= 0f ? worldRight : -worldRight;
            float projection = Vector3.Dot(toPos, outwardDirection);
            float r = beltHalfWidth + railWidth;

            if (projection < r)
            {
                var worldClipped = worldNearest + outwardDirection * r;
                worldClipped.y = worldPos.y;
                return transform.InverseTransformPoint(worldClipped);
            }

            return localPos;
        }

        private bool IsRingFullyInsideConveyor(int s, Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            float r = beltHalfWidth + railWidth;

            for (int p = 0; p < profile.Length; p++)
            {
                var localV = ToWorld(profile[p], s, wPos, wRight, wUp);
                var worldV = transform.TransformPoint(localV);
                var mainLocalV = mainTrackSpline.transform.InverseTransformPoint(worldV);

                SplineUtility.GetNearestPoint(mainTrackSpline.Spline, mainLocalV, out var nearestLocal, out var t, 8, 4);

                var worldNearest = mainTrackSpline.transform.TransformPoint((Vector3)nearestLocal);
                mainTrackSpline.Spline.Evaluate(t, out _, out var mTan, out var mUp);
                var localTan = ((Vector3)mTan).normalized;
                var localUp = ((Vector3)mUp).normalized;
                if (localUp.sqrMagnitude < 0.001f) localUp = Vector3.up;
                var localRight = Vector3.Cross(localUp, localTan).normalized;
                var worldRight = mainTrackSpline.transform.TransformDirection(localRight).normalized;

                var toPos = worldV - worldNearest;
                float projection = Mathf.Abs(Vector3.Dot(toPos, worldRight));

                if (projection >= r)
                    return false;
            }
            return true;
        }

        private void SampleFrames(int sCount, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            for (int s = 0; s < sCount; s++)
            {
                float t = Mathf.Lerp(sweepFrom, sweepTo, (float)s / resolution);
                _spline.Spline.Evaluate(t, out var pos, out var tan, out var up);

                var fwd = ((Vector3)tan).normalized;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                var upL = ((Vector3)up).normalized;
                if (upL.sqrMagnitude < 0.001f) upL = Vector3.up;

                wPos[s] = (Vector3)pos;
                wRight[s] = Vector3.Cross(upL, fwd).normalized;
                wUp[s] = upL;
            }
        }

        private static float[] ComputeProfilePerimU(Vector2[] profile)
        {
            int n = profile.Length;
            var acc = new float[n + 1];
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
                    dist[s] = dist[s] / total * vTiling;
            return dist;
        }

        public static void SmoothSeamNormals(Mesh mesh, float weldEpsilon = 0.001f)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (verts.Length == 0 || normals.Length != verts.Length) return;

            var groups = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < verts.Length; i++)
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
                foreach (int idx in list) avg += normals[idx];
                if (avg.sqrMagnitude < 1e-8f) continue;
                avg.Normalize();
                foreach (int idx in list) normals[idx] = avg;
            }

            mesh.normals = normals;
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorTrackMeshBuilder))]
        public sealed class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                GUILayout.Space(8);
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(32)))
                {
                    var builder = (ConveyorTrackMeshBuilder)target;
                    builder._spline = builder.GetComponent<SplineContainer>();
                    builder._meshFilter = builder.GetComponent<MeshFilter>();
                    builder.BuildMesh();
                    UnityEditor.EditorUtility.SetDirty(builder.gameObject);
                }
            }
        }
#endif
    }
}
