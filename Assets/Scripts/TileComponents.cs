using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.ECS
{
    /// <summary>
    /// Core tile component - each tile is represented as an entity with position + tile reference
    /// </summary>
    public struct Tile : IComponentData
    {
        public int2 GridPosition;
        public int TileID; // Index into a palette
    }

    /// <summary>
    /// Tile palette structure for efficient lookup using BlobAsset
    /// </summary>
    public struct TilePalette
    {
        public BlobArray<Entity> TileEntities; // References to tile prefabs
        public BlobArray<int> TileIDs; // Corresponding tile IDs
    }

    /// <summary>
    /// Transient component to request painting
    /// </summary>
    public struct PaintCommand : IComponentData
    {
        public int2 GridPosition;
        public int TileID;
    }

    /// <summary>
    /// Component for AutoTile integration with Unity's 2D Tilemap Extras
    /// </summary>
    public struct AutoTile : IComponentData
    {
        public Entity AutoTileAssetEntity; // Reference to AutoTile ScriptableObject
        public byte RuleFlags; // Current auto-tile rule state for 8-directional neighbors
        public int VariantIndex; // Current sprite variant based on rules
    }

    /// <summary>
    /// Tag component to identify tilemap entities
    /// </summary>
    public struct TilemapTag : IComponentData { }

    /// <summary>
    /// Component to store rendering information for tiles
    /// </summary>
    public struct TileRenderData : IComponentData
    {
        public Entity MaterialEntity;
        public Entity MeshEntity;
        public float4 Color;
        public int SpriteIndex; // Index in sprite array
    }
}