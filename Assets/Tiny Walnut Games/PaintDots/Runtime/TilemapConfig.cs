using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.Runtime.Config
{
    /// <summary>
    /// Configuration component for tilemap settings
    /// </summary>
    public readonly struct TilemapConfig : IComponentData
    {
        public readonly float TileSize;
        public readonly int MaxVariants;
        public readonly float4 DefaultTileColor;
        public readonly int DefaultSpriteIndex;

        public TilemapConfig(float tileSize, int maxVariants, float4 defaultColor, int defaultSpriteIndex)
        {
            TileSize = tileSize;
            MaxVariants = maxVariants;
            DefaultTileColor = defaultColor.Equals(new float4(0, 0, 0, 0)) ? new float4(1, 1, 1, 1) : defaultColor;
            DefaultSpriteIndex = defaultSpriteIndex;
        }

        public static TilemapConfig CreateDefault()
        {
            return new TilemapConfig(1.0f, 16, new float4(1, 1, 1, 1), 0);
        }
    }

    /// <summary>
    /// Configuration for AutoTile behavior
    /// </summary>
    public readonly struct AutoTileConfig : IComponentData
    {
        public readonly byte MaxNeighborMask;
        public readonly int RulesetSize;
        public readonly bool EnableNeighborChecking;

        public AutoTileConfig(byte maxNeighborMask, int rulesetSize, bool enableNeighborChecking)
        {
            MaxNeighborMask = maxNeighborMask;
            RulesetSize = rulesetSize;
            EnableNeighborChecking = enableNeighborChecking;
        }

        public static AutoTileConfig CreateDefault()
        {
            return new AutoTileConfig(255, 16, true);
        }
    }

    /// <summary>
    /// Configuration for rendering behavior
    /// </summary>
    public readonly struct RenderConfig : IComponentData
    {
        public readonly int BatchSize;
        public readonly bool EnableColorBlending;
        public readonly float ZDepthOffset;

        public RenderConfig(int batchSize, bool enableColorBlending, float zDepthOffset)
        {
            BatchSize = batchSize;
            EnableColorBlending = enableColorBlending;
            ZDepthOffset = zDepthOffset;
        }

        public static RenderConfig CreateDefault()
        {
            return new RenderConfig(1000, true, 0.01f);
        }
    }

    /// <summary>
    /// Configuration for structure types and tile IDs
    /// </summary>
    public readonly struct StructureConfig : IComponentData
    {
        public readonly int HouseID;
        public readonly int TreeID;
        public readonly int BridgeID;
        public readonly int PathID;

        public StructureConfig(int houseID, int treeID, int bridgeID, int pathID)
        {
            HouseID = houseID;
            TreeID = treeID;
            BridgeID = bridgeID;
            PathID = pathID;
        }

        public static StructureConfig CreateDefault()
        {
            return new StructureConfig(100, 101, 102, 50);
        }
    }

    /// <summary>
    /// Constants for entity references that should never be null
    /// </summary>
    public readonly struct EntityConstants
    {
        public static readonly Entity InvalidEntity = new Entity { Index = 0, Version = 0 };
    }
}