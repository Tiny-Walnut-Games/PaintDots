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
        public readonly BlobArray<StructureVisual> StructureVisuals; // Multi-tile structure prefabs
    }

    /// <summary>
    /// Transient component to request painting
    /// </summary>
    public readonly struct PaintCommand : IComponentData
    {
        public readonly int2 GridPosition;
        public readonly int TileID;
        public readonly bool IsMultiTile; // Flag to indicate if this is a multi-tile structure
        public readonly int2 Size; // Size for multi-tile structures (ignored for single tiles)

        public PaintCommand(int2 gridPosition, int tileID, bool isMultiTile = false, int2 size = default)
        {
            GridPosition = gridPosition;
            TileID = tileID;
            IsMultiTile = isMultiTile;
            Size = size.Equals(default) && isMultiTile ? new int2(1, 1) : size;
        }

        // Factory method for single tiles
        public static PaintCommand SingleTile(int2 gridPosition, int tileID)
        {
            return new PaintCommand(gridPosition, tileID, false, default);
        }

        // Factory method for multi-tile structures
        public static PaintCommand MultiTile(int2 gridPosition, int tileID, int2 size)
        {
            return new PaintCommand(gridPosition, tileID, true, size);
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

    /// <summary>
    /// Footprint component for multi-tile entities
    /// </summary>
    public readonly struct Footprint : IComponentData
    {
        public readonly int2 Origin;   // anchor tile (e.g. bottom-left)
        public readonly int2 Size;     // width/height in tiles

        public Footprint(int2 origin, int2 size)
        {
            Origin = origin;
            Size = size;
        }
    }

    /// <summary>
    /// Buffer element for tracking occupied cells in multi-tile entities
    /// </summary>
    public readonly struct OccupiedCell : IBufferElementData
    {
        public readonly int2 Position;

        public OccupiedCell(int2 position)
        {
            Position = position;
        }

        public static implicit operator int2(OccupiedCell cell)
        {
            return cell.Position;
        }

        public static implicit operator OccupiedCell(int2 position)
        {
            return new OccupiedCell(position);
        }
    }

    /// <summary>
    /// Structure visual definition for multi-tile prefabs in palette
    /// </summary>
    public readonly struct StructureVisual
    {
        public readonly Entity Prefab;  // prefab with render + transform
        public readonly int2 Size;      // footprint dimensions

        public StructureVisual(Entity prefab, int2 size)
        {
            Prefab = prefab;
            Size = size;
        }
    }
}