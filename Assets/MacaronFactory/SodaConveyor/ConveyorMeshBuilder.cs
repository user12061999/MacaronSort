// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/ConveyorMeshBuilder.cs.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter.SodaConveyor
{
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ConveyorMeshBuilder : MonoBehaviour
    {
        [Header("Cross-Section Profile")]
        [Tooltip("Half-width of the flat belt surface (inner groove width = 2 x this)")]
        [SerializeField] float beltHalfWidth = 0.5f;
        [Tooltip("How far the outer walls rise ABOVE the belt surface")]
        [SerializeField] float wallAboveBelt = 0.12f;
        [Tooltip("How far the outer walls hang DOWN below the belt surface")]
        [SerializeField] float railHeight = 0.5f;
        [Tooltip("Thickness of each outer wall")]
        [SerializeField] float railWidth = 0.05f;
        [Tooltip("Chamfer size on the top-outer corner of each wall (clamped to railWidth)")]
        [SerializeField] float bevelSize = 0.04f;

        [Header("Sweep Quality")]
        [Range(20, 400)]
        [SerializeField] int resolution = 60;

        [Header("UV Tiling")]
        [Tooltip("How many times the belt-top texture tiles along the spline length")]
        [SerializeField] float vTiling = 8f;

        [Header("Branch end trim (set by SodaConveyorTrack for decorative branches)")]
        [Tooltip("Clip this track's s=resolution end onto MainTrackSpline's outer wall instead of rendering the full tube through it.")]
        [SerializeField] bool trimBranchEnd;
        [SerializeField] SplineContainer mainTrackSpline;

        [Header("Pickup gate (outer/right wall, ending at the loop seam)")]
        [Range(0f, 0.5f)] public float pickupWindowFraction;

        SplineContainer _spline;
        MeshFilter _meshFilter;

        public float BeltHalfWidth
        {
            get => beltHalfWidth;
            set => beltHalfWidth = value;
        }

        public float RailWidth
        {
            get => railWidth;
            set => railWidth = value;
        }
        public float OuterRadius => beltHalfWidth + railWidth;

        public void SetBranchTrim(SplineContainer mainTrack)
        {
            trimBranchEnd = mainTrack != null;
            mainTrackSpline = mainTrack;
        }

        void Awake()
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

        Vector2[] BuildProfile()
        {
            var hw = beltHalfWidth;
            var rw = railWidth;
            var rh = railHeight;
            var wa = wallAboveBelt;
            var bv = Mathf.Clamp(bevelSize, 0f, Mathf.Min(rw * 0.5f, wa));

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

        Mesh Sweep()
        {
            var profile = BuildProfile();
            var pCount = profile.Length;
            var edgeCount = pCount - 1; // excludes the open underside (P11 -> P0)
            var samples = new List<float>(resolution + 2);
            for (var s = 0; s <= resolution; s++) samples.Add((float)s / resolution);
            var gateStart = 1f - Mathf.Clamp(pickupWindowFraction, 0f, .5f);
            var hasGate = _spline.Spline.Closed && gateStart < 1f;
            if (hasGate && !samples.Contains(gateStart)) samples.Add(gateStart);
            samples.Sort();
            var sCount = samples.Count;

            var wPos = new Vector3[sCount];
            var wRight = new Vector3[sCount];
            var wUp = new Vector3[sCount];
            SampleFrames(samples, wPos, wRight, wUp);

            var perimU = ComputeProfilePerimU(profile);
            var splineV = ComputeSplineV(wPos, sCount);

            const int beltEdge = 5;
            var sweepVCount = edgeCount * 2 * sCount;

            var verts = new List<Vector3>(sweepVCount + 32);
            var uvs = new List<Vector2>(sweepVCount + 32);
            var trisWall = new List<int>((edgeCount - 1) * resolution * 6 + 32);
            var trisBelt = new List<int>(resolution * 6);

            var trimming = trimBranchEnd && mainTrackSpline != null;

            for (var e = 0; e < edgeCount; e++)
            {
                var pa = profile[e];
                var pb = profile[(e + 1) % pCount];
                var uA = perimU[e];
                var uB = perimU[(e + 1) % pCount];

                for (var s = 0; s < sCount; s++)
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
                    var v = splineV[s];
                    uvs.Add(new Vector2(uA, v));
                    uvs.Add(new Vector2(uB, v));
                }
            }

            for (var e = 0; e < edgeCount; e++)
            {
                var stripBase = e * 2 * sCount;
                var isBelt = e == beltEdge;
                var tris = isBelt ? trisBelt : trisWall;

                for (var s = 0; s < sCount - 1; s++)
                {
                    if (hasGate && e > beltEdge && samples[s] >= gateStart)
                    {
                        // Keep the lower outer wall and a flat sill exactly at belt height.
                        if (e == 6 || e == 10)
                        {
                            var a = e == 6 ? profile[6] : new Vector2(beltHalfWidth + railWidth, 0f);
                            var end = e == 6 ? new Vector2(beltHalfWidth + railWidth, 0f) : profile[11];
                            var first = verts.Count;
                            for (var ring = s; ring <= s + 1; ring++)
                            {
                                verts.Add(ToWorld(a, ring, wPos, wRight, wUp));
                                verts.Add(ToWorld(end, ring, wPos, wRight, wUp));
                                uvs.Add(new Vector2(perimU[e], splineV[ring]));
                                uvs.Add(new Vector2(perimU[e + 1], splineV[ring]));
                            }
                            trisWall.Add(first); trisWall.Add(first + 2); trisWall.Add(first + 1);
                            trisWall.Add(first + 1); trisWall.Add(first + 2); trisWall.Add(first + 3);
                        }
                        continue;
                    }
                    // A branch's ring fully clipped onto the main track's outer wall at
                    // both ends of this segment is a degenerate (zero-extent) strip —
                    // skip it instead of drawing a collapsed sliver into the main track.
                    if (trimming &&
                        IsRingFullyInsideConveyor(s, profile, wPos, wRight, wUp) &&
                        IsRingFullyInsideConveyor(s + 1, profile, wPos, wRight, wUp))
                        continue;

                    var b = stripBase + s * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            if (hasGate)
            {
                var wallProfile = new[] { profile[6], profile[7], profile[8], profile[9],
                    profile[10], new Vector2(beltHalfWidth + railWidth, 0f) };
                AddEndCap(wallProfile, wPos, wRight, wUp, verts, uvs, trisWall, samples.IndexOf(gateStart));
                AddEndCap(wallProfile, wPos, wRight, wUp, verts, uvs, trisWall, sCount - 1);
            }

            // Closed loop (Block Shooter's own belts are always loops) needs no end
            // caps — s=0 and s=resolution meet seamlessly. An open run (straight track,
            // or a branch) has two ends that would otherwise show a hollow cross-section
            // from the side — except a branch's s=resolution end, which merges into the
            // main track instead and shouldn't show a flat cap sitting on its surface.
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

        static void AddEndCap(Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp,
            List<Vector3> verts, List<Vector2> uvs, List<int> trisWall, int s)
        {
            var baseIdx = verts.Count;
            foreach (var p in profile)
            {
                verts.Add(ToWorld(p, s, wPos, wRight, wUp));
                uvs.Add(Vector2.zero);
            }

            for (var i = 1; i < profile.Length - 1; i++)
            {
                var a = baseIdx;
                var b = baseIdx + i;
                var c = baseIdx + i + 1;
                trisWall.Add(a); trisWall.Add(b); trisWall.Add(c);
                trisWall.Add(a); trisWall.Add(c); trisWall.Add(b); // double-sided
            }
        }

        static Vector3 ToWorld(Vector2 p, int s, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
            => wPos[s] + wRight[s] * p.x + wUp[s] * p.y;
        Vector3 ClipVertex(Vector3 localPos)
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
            var projection = Vector3.Dot(toPos, outwardDirection);
            var r = beltHalfWidth + railWidth;

            if (projection < r) // penetrated inside the main track's outer wall
            {
                var worldClipped = worldNearest + outwardDirection * r;
                worldClipped.y = worldPos.y; // preserve height
                return transform.InverseTransformPoint(worldClipped);
            }

            return localPos;
        }
        bool IsRingFullyInsideConveyor(int s, Vector2[] profile, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            var r = beltHalfWidth + railWidth;

            for (var p = 0; p < profile.Length; p++)
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
                var projection = Mathf.Abs(Vector3.Dot(toPos, worldRight));

                if (projection >= r)
                    return false; // at least one vertex is still outside
            }
            return true; // all vertices are inside
        }

        void SampleFrames(List<float> samples, Vector3[] wPos, Vector3[] wRight, Vector3[] wUp)
        {
            for (var s = 0; s < samples.Count; s++)
            {
                var t = samples[s];
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

        static float[] ComputeProfilePerimU(Vector2[] profile)
        {
            var n = profile.Length;
            var acc = new float[n + 1];
            for (var i = 1; i < n; i++)
                acc[i] = acc[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            acc[n] = acc[n - 1] + Vector2.Distance(profile[n - 1], profile[0]);

            var total = acc[n];
            if (total > 0f)
                for (var i = 0; i <= n; i++) acc[i] /= total;
            return acc;
        }

        float[] ComputeSplineV(Vector3[] wPos, int sCount)
        {
            var dist = new float[sCount];
            var total = 0f;
            for (var s = 1; s < sCount; s++)
            {
                total += Vector3.Distance(wPos[s], wPos[s - 1]);
                dist[s] = total;
            }
            if (total > 0f)
                for (var s = 0; s < sCount; s++)
                    dist[s] = dist[s] / total * vTiling;
            return dist;
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(ConveyorMeshBuilder))]
        public sealed class Editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                GUILayout.Space(8);
                if (GUILayout.Button("Rebuild Mesh", GUILayout.Height(32)))
                {
                    var builder = (ConveyorMeshBuilder)target;
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
