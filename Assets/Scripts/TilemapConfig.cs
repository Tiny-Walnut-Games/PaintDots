using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.ECS.Config
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

        public TilemapConfig(float tileSize = 1.0f, int maxVariants = 16, float4 defaultColor = default, int defaultSpriteIndex = 0)
        {
            TileSize = tileSize;
            MaxVariants = maxVariants;
            DefaultTileColor = defaultColor.Equals(default) ? new float4(1, 1, 1, 1) : defaultColor;
            DefaultSpriteIndex = defaultSpriteIndex;
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

        public AutoTileConfig(byte maxNeighborMask = 255, int rulesetSize = 16, bool enableNeighborChecking = true)
        {
            MaxNeighborMask = maxNeighborMask;
            RulesetSize = rulesetSize;
            EnableNeighborChecking = enableNeighborChecking;
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

        public RenderConfig(int batchSize = 1000, bool enableColorBlending = true, float zDepthOffset = 0.01f)
        {
            BatchSize = batchSize;
            EnableColorBlending = enableColorBlending;
            ZDepthOffset = zDepthOffset;
        }
    }
}