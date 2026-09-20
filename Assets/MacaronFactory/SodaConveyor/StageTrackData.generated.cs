// Ported from Soda Shippers Assets/Scripts/Gameplay/Track/StageTrackData.generated.cs.
// AUTO-GENERATED from Block Shooter's Level_001..010.prefab — do not hand-edit.
// Regenerate via the extraction script if the source levels ever change.
namespace BlockShooter.SodaConveyor
{
    public static class StageTrackData
    {
        public const int TemplateCount = 10;

        public static StageGroupSpec[] MainGroups(int stageIndex) => (((stageIndex % TemplateCount) + TemplateCount) % TemplateCount) switch
        {
            0 => new StageGroupSpec[]
            {
                new(BlockColorType.Blue, 20),
                new(BlockColorType.Green, 20),
                new(BlockColorType.Yellow, 20),
                new(BlockColorType.Red, 20),
            },
            1 => new StageGroupSpec[]
            {
                new(BlockColorType.Red, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Blue, 10),
            },
            2 => new StageGroupSpec[]
            {
                new(BlockColorType.Red, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Yellow, 10),
            },
            3 => new StageGroupSpec[]
            {
                new(BlockColorType.Green, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Blue, 10),
            },
            4 => new StageGroupSpec[]
            {
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Purple, 10),
            },
            5 => new StageGroupSpec[]
            {
                new(BlockColorType.Red, 20),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Custom3, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Custom3, 10),
                new(BlockColorType.Orange, 10),
            },
            6 => new StageGroupSpec[]
            {
                new(BlockColorType.Green, 20),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Custom3, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Orange, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Yellow, 10),
            },
            7 => new StageGroupSpec[]
            {
                new(BlockColorType.Red, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Orange, 20),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Green, 10),
            },
            8 => new StageGroupSpec[]
            {
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Red, 10),
                new(BlockColorType.Green, 10),
                new(BlockColorType.Blue, 10),
                new(BlockColorType.Purple, 10),
                new(BlockColorType.Custom3, 10),
                new(BlockColorType.Yellow, 10),
                new(BlockColorType.Blue, 20),
                new(BlockColorType.Yellow, 10),
            },
            9 => new StageGroupSpec[]
            {
                new(BlockColorType.Red, 20),
                new(BlockColorType.Green, 20),
                new(BlockColorType.Custom3, 20),
                new(BlockColorType.Yellow, 20),
                new(BlockColorType.Custom2, 20),
            },
            _ => System.Array.Empty<StageGroupSpec>(),
        };

        public static StageBranchSpec[] Branches(int stageIndex) => (((stageIndex % TemplateCount) + TemplateCount) % TemplateCount) switch
        {
            0 => System.Array.Empty<StageBranchSpec>(),
            1 => System.Array.Empty<StageBranchSpec>(),
            2 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.605038f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Red, 10),
                    },
                    new BranchKnot[]
                    {
                        new(-9.757824f, 6.311789f, -0.675782f, 0.031179f, 0.675782f, -0.031179f),
                        new(-3f, 6f, -1f, 0.1f, 1f, -0.1f),
                        new(-1f, 4f, -0.2f, 0.2f, 0.2f, -0.2f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.384593f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Blue, 10),
                        new(BlockColorType.Yellow, 10),
                    },
                    new BranchKnot[]
                    {
                        new(13.791201f, 5.426171f, 1.07912f, -0.057383f, -1.07912f, 0.057383f),
                        new(3f, 6f, 1f, 0.1f, -1f, -0.1f),
                        new(1f, 4f, 0.2f, 0.2f, -0.2f, -0.2f),
                    }),
            },
            3 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.786581f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Blue, 10),
                        new(BlockColorType.Green, 10),
                    },
                    new BranchKnot[]
                    {
                        new(-6f, 7f, -2f, 0f, 2f, 0f),
                        new(-4.5f, 3f, 0f, 2f, 0f, -2f),
                        new(-2.5f, 1.5f, -0.5f, 0f, 0.5f, 0f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.21213f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Orange, 10),
                    },
                    new BranchKnot[]
                    {
                        new(6f, 7.5f, 2f, 0f, -2f, 0f),
                        new(4.5f, 3f, 0f, 2f, 0f, -2f),
                        new(2.5f, 1.5f, 0.5f, 0f, -0.5f, 0f),
                    }),
            },
            4 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.249779f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Custom3, 10),
                        new(BlockColorType.Yellow, 10),
                        new(BlockColorType.Purple, 10),
                        new(BlockColorType.Red, 10),
                        new(BlockColorType.Green, 10),
                        new(BlockColorType.Custom3, 10),
                    },
                    new BranchKnot[]
                    {
                        new(9.5f, 5f, 1.698698f, 0.052869f, -1.698698f, -0.052869f),
                        new(4.481309f, 4.852131f, 2.343549f, 0.011687f, -1.992547f, -0.021976f),
                        new(1.098418f, 1.595548f, 0.241967f, 0.631801f, -0.241967f, -0.631801f),
                    }),
            },
            5 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.716139f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Red, 10),
                        new(BlockColorType.Purple, 10),
                    },
                    new BranchKnot[]
                    {
                        new(-8f, -4.5f, -0.3f, -0.55f, 0.3f, 0.55f),
                        new(-5f, 1f, -1f, -1.85f, 0.9f, 1.75f),
                        new(-2f, 2.5f, -1.75f, 0f, 1f, 0f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.263449f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Red, 10),
                        new(BlockColorType.Purple, 10),
                        new(BlockColorType.Green, 10),
                    },
                    new BranchKnot[]
                    {
                        new(8f, 12f, 0.3f, 0.8f, -0.3f, -0.8f),
                        new(5f, 4f, 1f, 1.85f, -0.9f, -1.75f),
                        new(2f, 2.5f, 1.75f, 0f, -1f, 0f),
                    }),
            },
            6 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.665032f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Custom3, 10),
                        new(BlockColorType.Green, 10),
                        new(BlockColorType.Blue, 10),
                    },
                    new BranchKnot[]
                    {
                        new(-11.5f, 9f, -0.6f, 0.35f, 0.6f, -0.35f),
                        new(-5.5f, 5.5f, -1.73406f, 1.001187f, 1.321001f, -0.762701f),
                        new(-2f, 3.5f, -0.35f, 0.2f, 0.35f, -0.2f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.335485f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Red, 10),
                        new(BlockColorType.Yellow, 10),
                        new(BlockColorType.Green, 10),
                    },
                    new BranchKnot[]
                    {
                        new(11f, 11.5f, 0.55f, 0.5f, -0.55f, -0.5f),
                        new(5.5f, 6.5f, 1.636482f, 1.444657f, -1.288748f, -1.137684f),
                        new(2f, 3.5f, 0.35f, 0.3f, -0.35f, -0.3f),
                    }),
            },
            7 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.299613f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Orange, 20),
                        new(BlockColorType.Yellow, 10),
                        new(BlockColorType.Purple, 10),
                        new(BlockColorType.Blue, 10),
                        new(BlockColorType.Purple, 10),
                    },
                    new BranchKnot[]
                    {
                        new(12.220745f, 4.971214f, 1.469065f, -0.000124f, -1.469065f, 0.000124f),
                        new(4.269341f, 4.975779f, 2.343549f, 0.011687f, -2.437624f, -0.077967f),
                        new(1.152773f, 2.326432f, 0.514547f, 0.489021f, -0.514547f, -0.489021f),
                    }),
            },
            8 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.786581f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Green, 10),
                        new(BlockColorType.Purple, 10),
                        new(BlockColorType.Red, 20),
                        new(BlockColorType.Orange, 10),
                    },
                    new BranchKnot[]
                    {
                        new(-10.111037f, 7.438847f, -2f, 0f, 2f, 0f),
                        new(-5.122446f, 6.774145f, -0.901637f, 0.874121f, 0.786059f, -0.76207f),
                        new(-4.5f, 3f, 0f, 2f, 0f, -2f),
                        new(-2.5f, 1.5f, -0.5f, 0f, 0.5f, 0f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.21213f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Red, 10),
                        new(BlockColorType.Orange, 10),
                        new(BlockColorType.Custom3, 10),
                    },
                    new BranchKnot[]
                    {
                        new(6f, 7.5f, 2f, 0f, -2f, 0f),
                        new(4.5f, 3f, 0f, 2f, 0f, -2f),
                        new(2.5f, 1.5f, 0.5f, 0f, -0.5f, 0f),
                    }),
            },
            9 => new StageBranchSpec[]
            {
                new StageBranchSpec(
                    "Branch_0", 0.665032f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Purple, 20),
                        new(BlockColorType.Orange, 20),
                    },
                    new BranchKnot[]
                    {
                        new(-20f, 6f, -1.55f, 0f, 1.55f, 0f),
                        new(-4.5f, 6f, -3f, 0f, 1f, 0f),
                        new(-2f, 4f, -1.45f, 2f, 1.45f, -2f),
                    }),
                new StageBranchSpec(
                    "Branch_1", 0.335226f, false,
                    new StageGroupSpec[]
                    {
                        new(BlockColorType.Blue, 20),
                        new(BlockColorType.Custom2, 20),
                    },
                    new BranchKnot[]
                    {
                        new(16f, 6f, 1.15f, 0f, -1.15f, 0f),
                        new(4.5f, 6f, 3f, 0f, -1f, 0f),
                        new(2f, 4f, 1.45f, 2f, -1.45f, -2f),
                    }),
            },
            _ => System.Array.Empty<StageBranchSpec>(),
        };

        public const float LaneSpacing = 0.18f;
        public const float RowSpacing = 0.16f;
        public const float BeltHalfWidth = 0.45f;
        public const float RailWidth = 0.08f;
    }
}
