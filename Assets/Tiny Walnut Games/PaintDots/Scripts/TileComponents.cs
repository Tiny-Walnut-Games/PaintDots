using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using PaintDots.ECS.Config;

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
        public readonly BlobArray<int> StructureVisuals; // Placeholder: indices for multi-tile structure prefabs
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

        public PaintCommand(int2 gridPosition, int tileID, bool isMultiTile, int2 size)
        {
            GridPosition = gridPosition;
            TileID = tileID;
            IsMultiTile = isMultiTile;
            Size = isMultiTile && size.Equals(new int2(0, 0)) ? new int2(1, 1) : size;
        }

        // Factory method for single tiles
        public static PaintCommand SingleTile(int2 gridPosition, int tileID)
        {
            return new PaintCommand(gridPosition, tileID, false, new int2(0, 0));
        }

        // Factory method for multi-tile structures
        public static PaintCommand MultiTile(int2 gridPosition, int tileID, int2 size)
        {
            return new PaintCommand(gridPosition, tileID, true, size);
        }
    }

    /// <summary>
    /// Transient component to request erasing
    /// </summary>
    public readonly struct EraseCommand : IComponentData
    {
        public readonly int2 GridPosition;

        public EraseCommand(int2 gridPosition)
        {
            GridPosition = gridPosition;
        }
    }

    /// <summary>
    /// Component for AutoTile integration with Unity's 2D Tilemap Extras
    /// </summary>
    public readonly struct AutoTileComponent : IComponentData
    {
        public readonly Entity AutoTileAssetEntity; // Reference to AutoTile ScriptableObject
        public readonly byte RuleFlags; // Current auto-tile rule state for 8-directional neighbors
        public readonly int VariantIndex; // Current sprite variant based on rules

        public AutoTileComponent(Entity autoTileAssetEntity, byte ruleFlags = 0, int variantIndex = 0)
        {
            AutoTileAssetEntity = autoTileAssetEntity;
            RuleFlags = ruleFlags;
            VariantIndex = variantIndex;
        }

        public AutoTileComponent WithRuleFlags(byte newRuleFlags)
        {
            return new AutoTileComponent(AutoTileAssetEntity, newRuleFlags, VariantIndex);
        }

        public AutoTileComponent WithVariantIndex(int newVariantIndex)
        {
            return new AutoTileComponent(AutoTileAssetEntity, RuleFlags, newVariantIndex);
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
    /// Serializable tilemap state stored as BlobAsset for saving/loading
    /// </summary>
    public struct TilemapStateAsset
    {
        public BlobArray<SerializedTile> Tiles;
        public BlobArray<SerializedTilemap> Tilemaps;
        public float TileSize;
        public int Version;
    }

    /// <summary>
    /// Serialized tile data for BlobAsset storage
    /// </summary>
    public readonly struct SerializedTile
    {
        public readonly int2 GridPosition;
        public readonly int TileID;
        public readonly int VariantIndex;
        public readonly float4 Color;
        public readonly bool IsAutoTile;

        public SerializedTile(int2 gridPosition, int tileID, int variantIndex = 0, float4 color = default, bool isAutoTile = false)
        {
            GridPosition = gridPosition;
            TileID = tileID;
            VariantIndex = variantIndex;
            Color = (color.x == 0 && color.y == 0 && color.z == 0 && color.w == 0) ? new float4(1, 1, 1, 1) : color;
            IsAutoTile = isAutoTile;
        }
    }

    /// <summary>
    /// Serialized tilemap metadata
    /// </summary>
    public readonly struct SerializedTilemap
    {
        public readonly int2 ChunkSize;
        public readonly int2 MinBounds;
        public readonly int2 MaxBounds;
        public readonly int TileCount;

        public SerializedTilemap(int2 chunkSize, int2 minBounds, int2 maxBounds, int tileCount)
        {
            ChunkSize = chunkSize;
            MinBounds = minBounds;
            MaxBounds = maxBounds;
            TileCount = tileCount;
        }
    }

    /// <summary>
    /// Component to store a reference to tilemap state BlobAsset
    /// </summary>
    public readonly struct TilemapStateComponent : IComponentData
    {
        public readonly BlobAssetReference<TilemapStateAsset> StateAsset;

        public TilemapStateComponent(BlobAssetReference<TilemapStateAsset> stateAsset)
        {
            StateAsset = stateAsset;
        }
    }

    /// <summary>
    /// Component for animated tiles
    /// </summary>
    public readonly struct AnimatedTile : IComponentData
    {
        public readonly BlobAssetReference<AnimatedTileData> AnimationData;
        public readonly float CurrentTime;
        public readonly int CurrentFrame;
        public readonly bool IsPlaying;

        public AnimatedTile(BlobAssetReference<AnimatedTileData> animationData, float currentTime = 0f, int currentFrame = 0, bool isPlaying = true)
        {
            AnimationData = animationData;
            CurrentTime = currentTime;
            CurrentFrame = currentFrame;
            IsPlaying = isPlaying;
        }

        public AnimatedTile WithTime(float time, int frame)
        {
            return new AnimatedTile(AnimationData, time, frame, IsPlaying);
        }
    }

    /// <summary>
    /// BlobAsset containing animation frame data
    /// </summary>
    public readonly struct AnimatedTileData
    {
        public readonly BlobArray<AnimationFrame> Frames;
        public readonly float TotalDuration;
        public readonly bool Loop;
    }

    /// <summary>
    /// Individual animation frame data
    /// </summary>
    public readonly struct AnimationFrame
    {
        public readonly int SpriteIndex;
        public readonly float Duration;
        public readonly float4 Color;

        public AnimationFrame(int spriteIndex, float duration, float4 color = default)
        {
            SpriteIndex = spriteIndex;
            Duration = duration;
            Color = (color.x == 0f && color.y == 0f && color.z == 0f && color.w == 0f) ? new float4(1, 1, 1, 1) : color;
        }
    }

    /// <summary>
    /// Component representing a tilemap chunk for large world support
    /// </summary>
    public readonly struct TilemapChunk : IComponentData
    {
        public readonly int2 ChunkCoordinates;
        public readonly int2 ChunkSize;
        public readonly int TileCount;
        public readonly bool IsActive;

        public TilemapChunk(int2 chunkCoordinates, int2 chunkSize, int tileCount = 0, bool isActive = true)
        {
            ChunkCoordinates = chunkCoordinates;
            ChunkSize = chunkSize;
            TileCount = tileCount;
            IsActive = isActive;
        }

        public TilemapChunk WithTileCount(int newTileCount)
        {
            return new TilemapChunk(ChunkCoordinates, ChunkSize, newTileCount, IsActive);
        }

        public TilemapChunk WithActiveState(bool active)
        {
            return new TilemapChunk(ChunkCoordinates, ChunkSize, TileCount, active);
        }
    }

    /// <summary>
    /// Buffer element for storing tile references within a chunk
    /// </summary>
    public readonly struct ChunkTile : IBufferElementData
    {
        public readonly Entity TileEntity;
        public readonly int2 LocalPosition; // Position within chunk
        
        public ChunkTile(Entity tileEntity, int2 localPosition)
        {
            TileEntity = tileEntity;
            LocalPosition = localPosition;
        }
    }

    /// <summary>
    /// Component linking tiles to their parent chunk
    /// </summary>
    public readonly struct TileChunkReference : IComponentData
    {
        public readonly Entity ChunkEntity;
        public readonly int2 LocalPosition;

        public TileChunkReference(Entity chunkEntity, int2 localPosition)
        {
            ChunkEntity = chunkEntity;
            LocalPosition = localPosition;
        }
    }

    /// <summary>
    /// Placeholder StructureVisual component used for multi-tile authoring examples.
    /// Replace with actual visual/prefab reference when integrating rendering.
    /// </summary>
    public readonly struct StructureVisual : IComponentData
    {
        public readonly Entity Prefab;
        public readonly int2 Size;

        public StructureVisual(Entity prefab, int2 size)
        {
            Prefab = prefab;
            Size = size;
        }
    }

    // Minimal Footprint/component definitions to satisfy references in systems.
    public readonly struct Footprint : IComponentData
    {
        public readonly int2 Origin;
        public readonly int2 Size;

        public Footprint(int2 origin, int2 size)
        {
            Origin = origin;
            Size = size;
        }
    }

    public readonly struct OccupiedCell : IBufferElementData
    {
        public readonly int2 GridPosition;

        public int2 Position => GridPosition;

        public OccupiedCell(int2 gridPosition)
        {
            GridPosition = gridPosition;
        }

        public static implicit operator int2(OccupiedCell cell) => cell.GridPosition;
        public static implicit operator OccupiedCell(int2 pos) => new OccupiedCell(pos);
    }

    public static class GridOccupancyManager
    {
        // Simple overlap check used by systems. This is a placeholder; replace with full implementation as needed.
        // Preferred overload for DynamicBuffer usage
        public static bool IsFootprintFree(int2 origin, int2 size, in DynamicBuffer<OccupiedCell> existingTilesBuffer, in ComponentLookup<Footprint> footprintLookup)
        {
            for (int i = 0; i < existingTilesBuffer.Length; i++)
            {
                var pos = existingTilesBuffer[i].Position;
                if (pos.x >= origin.x && pos.x < origin.x + size.x && pos.y >= origin.y && pos.y < origin.y + size.y)
                    return false;
            }
            return true;
        }

        // Managed-array overload to avoid NativeArray in public APIs and job fields.
        public static bool IsFootprintFree(int2 origin, int2 size, int2[] existingTilePositions, Entity[] structureEntities, ComponentLookup<Footprint> footprintLookup)
        {
            // Treat missing or empty arrays as no collision
            if (existingTilePositions?.Length > 0)
            {
                for (int i = 0; i < existingTilePositions.Length; i++)
                {
                    var pos = existingTilePositions[i];
                    if (pos.x >= origin.x && pos.x < origin.x + size.x && pos.y >= origin.y && pos.y < origin.y + size.y)
                        return false;
                }
            }

            // Optionally check structure entities' footprints if provided
            if (structureEntities?.Length > 0)
            {
                for (int i = 0; i < structureEntities.Length; i++)
                {
                    var e = structureEntities[i];
                    if (e == PaintDots.ECS.EntityConstants.InvalidEntity) continue;
                    if (footprintLookup.HasComponent(e))
                    {
                        var f = footprintLookup[e];
                        if (FootprintsOverlap(origin, size, f.Origin, f.Size))
                            return false;
                    }
                }
            }

            return true;
        }

        public static bool FootprintsOverlap(Footprint a, Footprint b)
        {
            if (a.Origin.x + a.Size.x <= b.Origin.x) return false;
            if (b.Origin.x + b.Size.x <= a.Origin.x) return false;
            if (a.Origin.y + a.Size.y <= b.Origin.y) return false;
            if (b.Origin.y + b.Size.y <= a.Origin.y) return false;
            return true;
        }

        // Convenience overload used by tests and callers that pass raw positions/sizes
        public static bool FootprintsOverlap(int2 aOrigin, int2 aSize, int2 bOrigin, int2 bSize)
        {
            var a = new Footprint(aOrigin, aSize);
            var b = new Footprint(bOrigin, bSize);
            return FootprintsOverlap(a, b);
        }
    }
}