// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/StageTrackTypes.cs.
using Unity.Mathematics;
using UnityEngine.Splines;

namespace BlockShooter.SodaConveyor
{
    public readonly struct StageGroupSpec
    {
        public const int LaneCount = 4;
        public readonly BlockColorType Color;
        public readonly int RowCount;

        public StageGroupSpec(BlockColorType color, int rowCount)
        {
            Color = color;
            RowCount = rowCount;
        }
    }
    public readonly struct BranchKnot
    {
        public readonly float Px, Pz, InX, InZ, OutX, OutZ;

        public BranchKnot(float px, float pz, float inX, float inZ, float outX, float outZ)
        {
            Px = px; Pz = pz;
            InX = inX; InZ = inZ;
            OutX = outX; OutZ = outZ;
        }
    }
    public readonly struct StageBranchSpec
    {
        public readonly string Name;
        public readonly float MergeT;
        public readonly bool ConnectFromLeft;
        public readonly StageGroupSpec[] Groups;
        public readonly BranchKnot[] Knots;

        public StageBranchSpec(string name, float mergeT, bool connectFromLeft, StageGroupSpec[] groups, BranchKnot[] knots)
        {
            Name = name;
            MergeT = mergeT;
            ConnectFromLeft = connectFromLeft;
            Groups = groups;
            Knots = knots;
        }
        public Spline BuildSpline(float scale, float height)
        {
            var spline = new Spline();
            foreach (var k in Knots)
            {
                var knot = new BezierKnot(
                    new float3(k.Px * scale, height, k.Pz * scale),
                    new float3(k.InX * scale, 0f, k.InZ * scale),
                    new float3(k.OutX * scale, 0f, k.OutZ * scale));
                spline.Add(knot, TangentMode.Broken);
            }
            spline.Closed = false;
            return spline;
        }
    }
}
