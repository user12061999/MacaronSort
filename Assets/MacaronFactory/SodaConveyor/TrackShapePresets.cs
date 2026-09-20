// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/TrackShapePresets.cs.
using Unity.Mathematics;
using UnityEngine.Splines;

namespace BlockShooter.SodaConveyor
{
    public static class TrackShapePresets
    {
        public const int TemplateCount = 10;
        public static Spline Build(int templateIndex, float scale, float height)
        {
            var knots = LevelKnots(((templateIndex % TemplateCount) + TemplateCount) % TemplateCount);
            var spline = new Spline();
            foreach (var k in knots)
            {
                var knot = new BezierKnot(
                    new float3(k.px * scale, height, k.pz * scale),
                    new float3(k.inX * scale, 0f, k.inZ * scale),
                    new float3(k.outX * scale, 0f, k.outZ * scale));
                spline.Add(knot, TangentMode.Broken);
            }
            spline.Closed = true;
            return spline;
        }
        public static Spline[] BuildBranches(int templateIndex, float scale, float height)
        {
            var branches = BranchKnots(((templateIndex % TemplateCount) + TemplateCount) % TemplateCount);
            var result = new Spline[branches.Length];
            for (var b = 0; b < branches.Length; b++)
            {
                var spline = new Spline();
                foreach (var k in branches[b])
                {
                    var knot = new BezierKnot(
                        new float3(k.px * scale, height, k.pz * scale),
                        new float3(k.inX * scale, 0f, k.inZ * scale),
                        new float3(k.outX * scale, 0f, k.outZ * scale));
                    spline.Add(knot, TangentMode.Broken);
                }
                spline.Closed = false;
                result[b] = spline;
            }
            return result;
        }

        readonly struct K
        {
            public readonly float px, pz, inX, inZ, outX, outZ;
            public K(float px, float pz, float inX, float inZ, float outX, float outZ)
            {
                this.px = px; this.pz = pz;
                this.inX = inX; this.inZ = inZ;
                this.outX = outX; this.outZ = outZ;
            }
        }

        static K[] LevelKnots(int templateIndex) => templateIndex switch
        {
            0 => Level1,
            1 => Level2,
            2 => Level3,
            3 => Level4,
            4 => Level5,
            5 => Level6,
            6 => Level7,
            7 => Level8,
            8 => Level9,
            _ => Level10,
        };

        // Level_001.prefab — rounded triangle.
        static readonly K[] Level1 =
        {
            new(0f, 0.5f, -1.83f, 0f, 1.83f, 0f),
            new(2f, 1.9f, 0.25f, -1f, -0.25f, 1f),
            new(0f, 4.45f, 0.9f, 0f, -0.9f, 0f),
            new(-2f, 1.9f, 0.25f, 1f, -0.25f, -1f),
        };

        // Level_002.prefab — 8-knot rounded diamond/star.
        static readonly K[] Level2 =
        {
            new(0f, 0f, -1f, 0f, 1f, 0f),
            new(1.5f, 0.45f, -0.4f, -0.5f, 0.4f, 0.5f),
            new(2.2f, 1.8f, 0f, -0.4f, 0f, 0.4f),
            new(1.5f, 3.1f, 0.4f, -0.5f, -0.4f, 0.5f),
            new(0f, 3.65f, 1f, 0f, -1f, 0f),
            new(-1.5f, 3.1f, 0.4f, 0.5f, -0.4f, -0.5f),
            new(-2.2f, 1.8f, 0f, 0.4f, 0f, -0.4f),
            new(-1.5f, 0.45f, -0.4f, 0.5f, 0.4f, -0.5f),
        };

        // Level_003.prefab — 6-knot rounded hexagon-ish loop.
        static readonly K[] Level3 =
        {
            new(0f, 0.5f, -0.87f, 0f, 0.87f, 0f),
            new(1.3f, 1f, -0.15f, -0.3f, 0.15f, 0.3f),
            new(1.75f, 2.75f, 0.25f, -0.55f, -0.25f, 0.55f),
            new(0f, 4.1f, 0.6f, 0f, -0.6f, 0f),
            new(-1.75f, 2.75f, 0.25f, 0.55f, -0.25f, -0.55f),
            new(-1.3f, 1f, -0.15f, 0.3f, 0.15f, -0.3f),
        };

        // Level_004.prefab — 8-knot lopsided loop with a squeezed waist.
        static readonly K[] Level4 =
        {
            new(0f, 0f, -0.5f, 0f, 0.5f, 0f),
            new(1.5f, 0f, -0.9f, 0f, 0.9f, 0f),
            new(2.5f, 1.25f, 0f, -0.6f, 0f, 0.6f),
            new(1.7f, 2.49f, 0.7f, -0.02f, -1.2f, -0.08f),
            new(0f, 1.3f, 1f, 0f, -1f, 0f),
            new(-1.7f, 2.49f, 1.2f, -0.02f, -0.7f, -0.08f),
            new(-2.5f, 1.25f, 0f, 0.6f, 0f, -0.6f),
            new(-1.5f, 0f, -0.9f, 0f, 0.9f, 0f),
        };

        // Level_005.prefab — 7-knot asymmetric loop.
        static readonly K[] Level5 =
        {
            new(0f, 0f, -1.5f, 0f, 1.35f, 0f),
            new(1.7f, 0.7f, 0f, -0.6f, 0f, 0.52f),
            new(0.834f, 1.8525f, 0.5757f, -0.5971f, -0.5757f, 0.5971f),
            new(0f, 3.1f, 0f, -0.4f, 0f, 1f),
            new(-1f, 4f, 0.2f, 0f, -0.2f, 0f),
            new(-2f, 3.1f, 0f, 1f, 0f, -1f),
            new(-2f, 1f, 0f, 0.67f, 0f, -0.67f),
        };

        // Level_006.prefab — 4-knot tall oval.
        static readonly K[] Level6 =
        {
            new(0f, 0f, -2.2f, 0f, 2.2f, 0f),
            new(2.1f, 2.25f, 0f, -0.75f, 0f, 0.75f),
            new(0f, 4.5f, 2.2f, 0f, -2.2f, 0f),
            new(-2.1f, 2.25f, 0f, 0.75f, 0f, -0.75f),
        };

        // Level_007.prefab — 8-knot symmetric rounded diamond.
        static readonly K[] Level7 =
        {
            new(0f, 0f, -1.08f, 0f, 1.08f, 0f),
            new(1.7f, 0.4f, -0.4f, -0.5f, 0.4f, 0.5f),
            new(2.6f, 2.25f, 0f, -0.6f, 0f, 0.6f),
            new(1.7f, 4.25f, 0.4f, -0.5f, -0.4f, 0.5f),
            new(0f, 4.7f, 1.09f, 0f, -1.09f, 0f),
            new(-1.7f, 4.25f, 0.4f, 0.5f, -0.4f, -0.5f),
            new(-2.6f, 2.25f, 0f, 0.6f, 0f, -0.6f),
            new(-1.7f, 0.4f, -0.4f, 0.5f, 0.4f, -0.5f),
        };

        // Level_008.prefab — 8-knot lopsided teardrop loop.
        static readonly K[] Level8 =
        {
            new(0f, 0f, -1.86f, 0f, 1.57f, 0f),
            new(2.3f, 0.6f, -0.2214f, -0.7836f, 0.0756f, 0.5254f),
            new(1.9849f, 1.6323f, 0.2938f, -0.2375f, -0.2938f, 0.2375f),
            new(0.7637f, 2.65f, 0.6038f, -0.4898f, -0.6038f, 0.4898f),
            new(0f, 4.2f, 0.0342f, -1.0038f, 0f, 1f),
            new(-1f, 5.1f, 0.2f, 0f, -0.2f, 0f),
            new(-2f, 4.2f, 0f, 1f, 0f, -1f),
            new(-2f, 1f, 0f, 0.67f, 0f, -0.67f),
        };

        // Level_009.prefab — 8-knot loop, wide shoulders.
        static readonly K[] Level9 =
        {
            new(0f, 0f, -0.5f, 0f, 0.5f, 0f),
            new(1.5f, 0f, -0.9f, 0f, 0.9f, 0f),
            new(2.72f, 1.25f, 0f, -1.19f, 0f, 1.19f),
            new(1.7f, 2.7f, 0.7276f, 0.029f, -0.8814f, -0.0607f),
            new(0f, 1.3f, 1f, 0f, -1f, 0f),
            new(-1.7f, 2.7f, 0.8814f, -0.0607f, -0.7276f, 0.029f),
            new(-2.72f, 1.25f, 0f, 1.19f, 0f, -1.19f),
            new(-1.5f, 0f, -0.9f, 0f, 0.9f, 0f),
        };

        // Level_010.prefab — 6-knot largest loop.
        static readonly K[] Level10 =
        {
            new(0f, 0f, -1.11f, 0f, 1.11f, 0f),
            new(1.7f, 0.4f, -0.4f, -0.5f, 0.4f, 0.5f),
            new(2.6f, 2.75f, 0f, -0.6f, 0f, 0.6f),
            new(0f, 5.03f, 0.96f, 0f, -0.96f, 0f),
            new(-2.6f, 2.75f, 0f, 0.6f, 0f, -0.6f),
            new(-1.7f, 0.4f, -0.4f, 0.5f, 0.4f, -0.5f),
        };

        static readonly K[][] NoBranches = System.Array.Empty<K[]>();

        static K[][] BranchKnots(int templateIndex) => templateIndex switch
        {
            0 => NoBranches, // Level_001 — no branches
            1 => NoBranches, // Level_002 — no branches
            2 => Level3Branches,
            3 => Level4Branches,
            4 => Level5Branches,
            5 => Level6Branches,
            6 => Level7Branches,
            7 => Level8Branches,
            8 => Level9Branches,
            _ => Level10Branches,
        };

        // Branches keep their real far knot (the off-screen spawner position in Block
        // Shooter's own level) so the open tip reliably clears our camera frame too —
        // same world scale as the main loop, same camera tilt convention we copied from
        // Block Shooter, so their "off in the distance" numbers work for us as well.
        static readonly K[][] Level3Branches =
        {
            new K[] { new(-9.7578f, 6.3118f, -0.6758f, 0.0312f, 0.6758f, -0.0312f), new(-3f, 6f, -1f, 0.1f, 1f, -0.1f), new(-1f, 4f, -0.2f, 0.2f, 0.2f, -0.2f) },
            new K[] { new(13.7912f, 5.4262f, 1.0791f, -0.0574f, -1.0791f, 0.0574f), new(3f, 6f, 1f, 0.1f, -1f, -0.1f), new(1f, 4f, 0.2f, 0.2f, -0.2f, -0.2f) },
        };

        static readonly K[][] Level4Branches =
        {
            new K[] { new(-6f, 7f, -2f, 0f, 2f, 0f), new(-4.5f, 3f, 0f, 2f, 0f, -2f), new(-2.5f, 1.5f, -0.5f, 0f, 0.5f, 0f) },
            new K[] { new(6f, 7.5f, 2f, 0f, -2f, 0f), new(4.5f, 3f, 0f, 2f, 0f, -2f), new(2.5f, 1.5f, 0.5f, 0f, -0.5f, 0f) },
        };

        static readonly K[][] Level5Branches =
        {
            new K[] { new(9.5f, 5f, 1.6987f, 0.0529f, -1.6987f, -0.0529f), new(4.4813f, 4.8521f, 2.3435f, 0.0117f, -1.9925f, -0.0220f), new(1.0984f, 1.5955f, 0.2420f, 0.6318f, -0.2420f, -0.6318f) },
        };

        static readonly K[][] Level6Branches =
        {
            new K[] { new(8f, 12f, 0.3f, 0.8f, -0.3f, -0.8f), new(5f, 4f, 1f, 1.85f, -0.9f, -1.75f), new(2f, 2.5f, 1.75f, 0f, -1f, 0f) },
            new K[] { new(-8f, -4.5f, -0.3f, -0.55f, 0.3f, 0.55f), new(-5f, 1f, -1f, -1.85f, 0.9f, 1.75f), new(-2f, 2.5f, -1.75f, 0f, 1f, 0f) },
        };

        static readonly K[][] Level7Branches =
        {
            new K[] { new(11f, 11.5f, 0.55f, 0.5f, -0.55f, -0.5f), new(5.5f, 6.5f, 1.6365f, 1.4447f, -1.2887f, -1.1377f), new(2f, 3.5f, 0.35f, 0.3f, -0.35f, -0.3f) },
            new K[] { new(-11.5f, 9f, -0.6f, 0.35f, 0.6f, -0.35f), new(-5.5f, 5.5f, -1.7341f, 1.0012f, 1.3210f, -0.7627f), new(-2f, 3.5f, -0.35f, 0.2f, 0.35f, -0.2f) },
        };

        static readonly K[][] Level8Branches =
        {
            new K[] { new(12.2207f, 4.9712f, 1.4691f, -0.0001f, -1.4691f, 0.0001f), new(4.2693f, 4.9758f, 2.3435f, 0.0117f, -2.4376f, -0.0780f), new(1.1528f, 2.3264f, 0.5145f, 0.4890f, -0.5145f, -0.4890f) },
        };

        static readonly K[][] Level9Branches =
        {
            new K[] { new(-10.1110f, 7.4388f, -2f, 0f, 2f, 0f), new(-5.1224f, 6.7741f, -0.9016f, 0.8741f, 0.7861f, -0.7621f), new(-4.5f, 3f, 0f, 2f, 0f, -2f), new(-2.5f, 1.5f, -0.5f, 0f, 0.5f, 0f) },
            new K[] { new(6f, 7.5f, 2f, 0f, -2f, 0f), new(4.5f, 3f, 0f, 2f, 0f, -2f), new(2.5f, 1.5f, 0.5f, 0f, -0.5f, 0f) },
        };

        static readonly K[][] Level10Branches =
        {
            new K[] { new(16f, 6f, 1.15f, 0f, -1.15f, 0f), new(4.5f, 6f, 3f, 0f, -1f, 0f), new(2f, 4f, 1.45f, 2f, -1.45f, -2f) },
            new K[] { new(-20f, 6f, -1.55f, 0f, 1.55f, 0f), new(-4.5f, 6f, -3f, 0f, 1f, 0f), new(-2f, 4f, -1.45f, 2f, 1.45f, -2f) },
        };
    }
}
