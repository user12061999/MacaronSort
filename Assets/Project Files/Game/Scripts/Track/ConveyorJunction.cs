using UnityEngine;
using UnityEngine.Splines;

namespace BlockShooter
{
    /// <summary>
    /// Wires a branch conveyor into a main one — the layout in the reference screenshot,
    /// where two feeder belts poke into a closed loop.
    ///
    /// Sync does two things:
    ///   1. Cuts an opening in the MAIN track's rim, exactly as wide as the branch,
    ///      by projecting the branch's outer corners onto the main spline.
    ///   2. Trims the BRANCH so it stops flush against the main rim, by binary-searching
    ///      the branch spline for the point where it leaves the main track's footprint
    ///      and writing that into the branch's sweepFrom / sweepTo.
    ///
    /// Both are authoring-time operations. Nothing runs per frame.
    ///
    /// Put this on the branch conveyor (the GameObject with its ConveyorTrackMeshBuilder).
    /// </summary>
    [RequireComponent(typeof(ConveyorTrackMeshBuilder))]
    [DisallowMultipleComponent]
    public sealed class ConveyorJunction : MonoBehaviour
    {
        public enum BranchEnd
        {
            /// <summary>The branch's spline START touches the main track.</summary>
            Start = 0,
            /// <summary>The branch's spline END touches the main track.</summary>
            End = 1,
        }

        [Header("Connection")]
        [Tooltip("The conveyor this branch feeds into. Usually the closed centre loop.")]
        public ConveyorTrackMeshBuilder mainTrack;
        [Tooltip("Which end of the branch spline meets the main track.")]
        public BranchEnd joinAt = BranchEnd.End;

        [Header("Opening cut into the main track")]
        [Tooltip("Widen the hole by this much on each side, in metres. " +
                 "A little slack hides the seam where the two rims meet.")]
        [Min(0f)] public float openingPadding = 0.02f;
        [Tooltip("Leave a low kerb in the hole instead of removing the wall entirely. 0 = full hole.")]
        [Min(0f)] public float openingLipHeight = 0f;
        public bool openingCaps = true;
        public bool openingSkirt = true;
        public Color gizmoColor = new Color(0.2f, 0.85f, 1f, 1f);

        [Header("Branch trim")]
        [Tooltip("Push the branch back from the main rim by this much. " +
                 "Negative values let it overlap, which hides angled joins.")]
        public float branchClearance = 0f;
        [Tooltip("Rebuild both tracks after syncing.")]
        public bool rebuildOnSync = true;

        // Stable handle so repeated syncs update the same opening instead of stacking new ones.
        [SerializeField, HideInInspector] private string _openingId;

        private ConveyorTrackMeshBuilder _branch;

        public ConveyorTrackMeshBuilder Branch
        {
            get
            {
                if (_branch == null) _branch = GetComponent<ConveyorTrackMeshBuilder>();
                return _branch;
            }
        }

        private string OpeningId
        {
            get
            {
                if (string.IsNullOrEmpty(_openingId))
                    _openingId = $"Junction:{gameObject.name}:{GetInstanceID()}";
                return _openingId;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sync
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Sync Junction")]
        public void SyncJunction()
        {
            if (mainTrack == null)
            {
                Debug.LogError($"{name}: assign Main Track first.", this);
                return;
            }

            var branch = Branch;
            var mainSpline = mainTrack.GetComponent<SplineContainer>();
            var branchSpline = branch.GetComponent<SplineContainer>();
            if (mainSpline?.Spline == null || branchSpline?.Spline == null)
            {
                Debug.LogError($"{name}: both tracks need a SplineContainer with a spline.", this);
                return;
            }

            // The main track's length is needed to convert padding to normalized units.
            mainTrack.BuildMesh();

            // The opening removes the wall thickness too: join the belt edge, not the outer rim.
            float threshold = mainTrack.beltHalfWidth + branchClearance;
            float trimT = FindTrimT(branch, mainSpline, threshold, joinAt == BranchEnd.End);
            // Project the mouth at the rim, not the endpoint already on the main centreline.
            branch.EvaluateWorld(trimT, out Vector3 tipPos, out _, out _, out Vector3 tipRight);
            float branchRim = branch.RimOffset;

            // ── 1. Which rim of the main track does the branch hit? ───────────
            float centreT = NearestT(mainSpline, tipPos);
            mainTrack.EvaluateWorld(centreT, out Vector3 mainPos, out _, out _, out Vector3 mainRight);
            bool onRight = Vector3.Dot(tipPos - mainPos, mainRight) >= 0f;
            ConveyorSide side = onRight ? ConveyorSide.Right : ConveyorSide.Left;

            // ── 2. Opening span, from the branch's two outer corners ──────────
            float tA = NearestT(mainSpline, tipPos - tipRight * branchRim);
            float tB = NearestT(mainSpline, tipPos + tipRight * branchRim);

            float t0 = Mathf.Min(tA, tB);
            float t1 = Mathf.Max(tA, tB);
            // A junction is small; a span over half the loop means it wrapped past T=1.
            if (t1 - t0 > 0.5f) { float swap = t0; t0 = t1; t1 = swap; }

            float sweep = t1 >= t0 ? t1 - t0 : 1f - t0 + t1;
            float padT = mainTrack.TotalLength > 1e-4f ? openingPadding / mainTrack.TotalLength : 0f;

            var opening = FindOrCreateOpening();
            opening.side = side;
            opening.measure = ConveyorMeasure.Normalized;
            opening.start = Frac(t0 - padT);
            opening.length = Mathf.Min(sweep + padT * 2f, 0.95f);
            opening.lipHeight = openingLipHeight;
            opening.caps = openingCaps;
            opening.skirt = openingSkirt;
            opening.gizmoColor = gizmoColor;
            opening.enabled = true;

            // ── 3. Trim the branch so it stops at the main rim ────────────────
            if (joinAt == BranchEnd.End) { branch.sweepFrom = 0f; branch.sweepTo = trimT; }
            else { branch.sweepFrom = trimT; branch.sweepTo = 1f; }

            if (rebuildOnSync)
            {
                mainTrack.BuildMesh();
                branch.BuildMesh();
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mainTrack);
            UnityEditor.EditorUtility.SetDirty(branch);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Remove Junction Opening")]
        public void RemoveJunctionOpening()
        {
            if (mainTrack?.openings == null) return;
            int removed = mainTrack.openings.RemoveAll(o => o != null && o.label == OpeningId);
            if (removed == 0) return;

            Branch.sweepFrom = 0f;
            Branch.sweepTo = 1f;
            if (!rebuildOnSync) return;
            mainTrack.BuildMesh();
            Branch.BuildMesh();
        }

        private ConveyorOpening FindOrCreateOpening()
        {
            if (mainTrack.openings == null)
                mainTrack.openings = new System.Collections.Generic.List<ConveyorOpening>();

            var existing = mainTrack.openings.Find(o => o != null && o.label == OpeningId);
            if (existing != null) return existing;

            var created = new ConveyorOpening { label = OpeningId };
            mainTrack.openings.Add(created);
            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Geometry helpers
        // ─────────────────────────────────────────────────────────────────────

        private static float Frac(float v)
        {
            v %= 1f;
            return v < 0f ? v + 1f : v;
        }

        private static float NearestT(SplineContainer container, Vector3 worldPoint)
        {
            Vector3 local = container.transform.InverseTransformPoint(worldPoint);
            SplineUtility.GetNearestPoint(container.Spline, local, out _, out float t, 32, 6);
            return Mathf.Clamp01(t);
        }

        private static float DistanceToSpline(SplineContainer container, Vector3 worldPoint)
        {
            Vector3 local = container.transform.InverseTransformPoint(worldPoint);
            SplineUtility.GetNearestPoint(container.Spline, local, out var nearest, out _, 32, 6);
            Vector3 world = container.transform.TransformPoint((Vector3)nearest);
            Vector3 d = worldPoint - world;
            return d.magnitude;
        }

        /// <summary>
        /// Walks inward from the joining end until the branch centreline clears the main
        /// track's footprint, then bisects to land on the boundary.
        /// </summary>
        private static float FindTrimT(ConveyorTrackMeshBuilder branch, SplineContainer mainSpline,
            float threshold, bool fromEnd)
        {
            float inner = fromEnd ? 1f : 0f;
            float dir = fromEnd ? -1f : 1f;

            branch.EvaluateWorld(inner, out Vector3 tip, out _, out _, out _);
            if (DistanceToSpline(mainSpline, tip) > threshold) return inner;  // already clear

            const int steps = 64;
            float prev = inner;
            for (int i = 1; i <= steps; i++)
            {
                float t = Mathf.Clamp01(inner + dir * i / steps);
                branch.EvaluateWorld(t, out Vector3 p, out _, out _, out _);
                if (DistanceToSpline(mainSpline, p) <= threshold) { prev = t; continue; }

                float a = prev, b = t;
                for (int k = 0; k < 20; k++)
                {
                    float m = (a + b) * 0.5f;
                    branch.EvaluateWorld(m, out Vector3 mp, out _, out _, out _);
                    if (DistanceToSpline(mainSpline, mp) > threshold) b = m; else a = m;
                }
                return Mathf.Clamp01(b);
            }

            Debug.LogWarning($"{branch.name}: the whole branch sits inside the main track's footprint. " +
                             "Move it further out or lower Branch Clearance.", branch);
            return fromEnd ? 1f : 0f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (mainTrack == null) return;

            var branch = Branch;
            float endT = joinAt == BranchEnd.End ? branch.sweepTo : branch.sweepFrom;
            branch.EvaluateWorld(endT, out Vector3 tip, out _, out Vector3 up, out Vector3 right);

            Gizmos.color = gizmoColor;
            Vector3 a = tip - right * branch.RimOffset;
            Vector3 b = tip + right * branch.RimOffset;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(a, a + up * branch.wallAboveBelt);
            Gizmos.DrawLine(b, b + up * branch.wallAboveBelt);

            var mainSpline = mainTrack.GetComponent<SplineContainer>();
            if (mainSpline?.Spline == null) return;
            float t = NearestT(mainSpline, tip);
            mainTrack.EvaluateWorld(t, out Vector3 mainPos, out _, out _, out _);
            Gizmos.DrawLine(tip, mainPos);
        }
    }
}
