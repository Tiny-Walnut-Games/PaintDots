using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.ECS
{
    /// <summary>
    /// Core tile component - each tile is represented as an entity with position + tile reference
    /// </summary>
    public readonly struct Tile : IComponentData
    {
        public readonly int2 GridPosition;
        public readonly int TileID; // Index into a palette

        public Tile(int2 gridPosition, int tileID)
        {
            GridPosition = gridPosition;
            TileID = tileID;
        }
    }

    /// <summary>
    /// Tile palette structure for efficient lookup using BlobAsset
    /// </summary>
    public readonly struct TilePalette
    {
        public readonly BlobArray<Entity> TileEntities; // References to tile prefabs
        public readonly BlobArray<int> TileIDs; // Corresponding tile IDs
    }

    /// <summary>
    /// Transient component to request painting
    /// </summary>
    public readonly struct PaintCommand : IComponentData
    {
        public readonly int2 GridPosition;
        public readonly int TileID;

        public PaintCommand(int2 gridPosition, int tileID)
        {
            GridPosition = gridPosition;
            TileID = tileID;
        }
    }

    /// <summary>
    /// Component for AutoTile integration with Unity's 2D Tilemap Extras
    /// </summary>
    public readonly struct AutoTile : IComponentData
    {
        public readonly Entity AutoTileAssetEntity; // Reference to AutoTile ScriptableObject
        public readonly byte RuleFlags; // Current auto-tile rule state for 8-directional neighbors
        public readonly int VariantIndex; // Current sprite variant based on rules

        public AutoTile(Entity autoTileAssetEntity, byte ruleFlags = 0, int variantIndex = 0)
        {
            AutoTileAssetEntity = autoTileAssetEntity;
            RuleFlags = ruleFlags;
            VariantIndex = variantIndex;
        }

        public AutoTile WithRuleFlags(byte newRuleFlags)
        {
            return new AutoTile(AutoTileAssetEntity, newRuleFlags, VariantIndex);
        }

        public AutoTile WithVariantIndex(int newVariantIndex)
        {
            return new AutoTile(AutoTileAssetEntity, RuleFlags, newVariantIndex);
        }
    }

    /// <summary>
    /// Tag component to identify tilemap entities
    /// </summary>
    public readonly struct TilemapTag : IComponentData { }

    /// <summary>
    /// Component to store rendering information for tiles
    /// </summary>
    public readonly struct TileRenderData : IComponentData
    {
        public readonly Entity MaterialEntity;
        public readonly Entity MeshEntity;
        public readonly float4 Color;
        public readonly int SpriteIndex; // Index in sprite array

        public TileRenderData(Entity materialEntity, Entity meshEntity, float4 color, int spriteIndex)
        {
            MaterialEntity = materialEntity;
            MeshEntity = meshEntity;
            Color = color;
            SpriteIndex = spriteIndex;
        }

        public TileRenderData WithSpriteIndex(int newSpriteIndex)
        {
            return new TileRenderData(MaterialEntity, MeshEntity, Color, newSpriteIndex);
        }
    }
}