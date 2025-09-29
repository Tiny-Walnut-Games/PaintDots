using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PaintDots.ECS.Config;

namespace PaintDots.ECS.Utilities
{
    /// <summary>
    /// Static utility class for common tilemap operations
    /// </summary>
    public static class TilemapUtilities
    {
        /// <summary>
        /// Converts world position to grid position
        /// </summary>
        public static int2 WorldToGrid(float3 worldPos, TilemapConfig config)
        {
            return new int2(
                Mathf.FloorToInt(worldPos.x / config.TileSize),
                Mathf.FloorToInt(worldPos.y / config.TileSize)
            );
        }

        /// <summary>
        /// Converts grid position to world position
        /// </summary>
        public static float3 GridToWorld(int2 gridPos, TilemapConfig config)
        {
            return new float3(gridPos.x * config.TileSize, gridPos.y * config.TileSize, 0);
        }

        /// <summary>
        /// Gets neighboring grid positions (8-directional) using DynamicBuffer
        /// </summary>
        public static void GetNeighbors(int2 center, DynamicBuffer<int2> neighbors)
        {
            neighbors.Clear();
            
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    neighbors.Add(center + new int2(x, y));
                }
            }
        }

        /// <summary>
        /// Creates a paint command using ECB
        /// </summary>
        public static Entity CreatePaintCommand(EntityCommandBuffer ecb, int2 gridPos, int tileID)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new PaintCommand(gridPos, tileID));
            return entity;
        }

        /// <summary>
        /// Batch paint multiple tiles at once using ECB
        /// </summary>
        public static void BatchPaint(EntityCommandBuffer ecb, DynamicBuffer<int2> positions, int tileID)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                CreatePaintCommand(ecb, positions[i], tileID);
            }
        }

        /// <summary>
        /// Creates a rectangular area of paint commands using ECB
        /// </summary>
        public static void PaintRectangle(EntityCommandBuffer ecb, int2 min, int2 max, int tileID)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    CreatePaintCommand(ecb, new int2(x, y), tileID);
                }
            }
        }

        /// <summary>
        /// Creates a filled circle of paint commands using ECB
        /// </summary>
        public static void PaintCircle(EntityCommandBuffer ecb, int2 center, int radius, int tileID)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    var pos = new int2(x, y);
                    var distance = math.distance(new float2(center.x, center.y), new float2(x, y));
                    
                    if (distance <= radius)
                    {
                        CreatePaintCommand(ecb, pos, tileID);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Buffer element for storing neighbor positions
    /// </summary>
    public readonly struct NeighborPosition : IBufferElementData
    {
        public readonly int2 Position;
        
        public NeighborPosition(int2 position)
        {
            Position = position;
        }

        public static implicit operator int2(NeighborPosition neighbor)
        {
            return neighbor.Position;
        }

        public static implicit operator NeighborPosition(int2 position)
        {
            return new NeighborPosition(position);
        }
    }

    /// <summary>
    /// Brush types for different painting patterns
    /// </summary>
    public enum BrushType : byte
    {
        Single,
        Square3x3,
        Square5x5,
        Circle,
        Line,
        Fill
    }

    /// <summary>
    /// Brush configuration for painting
    /// </summary>
    [System.Serializable]
    public readonly struct BrushConfig
    {
        public readonly BrushType Type;
        public readonly int Size;
        public readonly int TileID;
        public readonly bool UseAutoTile;

        public BrushConfig(BrushType type, int size, int tileID, bool useAutoTile = false)
        {
            Type = type;
            Size = size;
            TileID = tileID;
            UseAutoTile = useAutoTile;
        }
    }

    /// <summary>
    /// Brush system for applying different painting patterns
    /// </summary>
    public static class BrushSystem
    {
        /// <summary>
        /// Apply brush at given position using ECB
        /// </summary>
        public static void ApplyBrush(EntityCommandBuffer ecb, int2 position, BrushConfig brush)
        {
            switch (brush.Type)
            {
                case BrushType.Single:
                    TilemapUtilities.CreatePaintCommand(ecb, position, brush.TileID);
                    break;
                    
                case BrushType.Square3x3:
                    PaintSquare(ecb, position, 1, brush.TileID);
                    break;
                    
                case BrushType.Square5x5:
                    PaintSquare(ecb, position, 2, brush.TileID);
                    break;
                    
                case BrushType.Circle:
                    TilemapUtilities.PaintCircle(ecb, position, brush.Size, brush.TileID);
                    break;
            }
        }

        private static void PaintSquare(EntityCommandBuffer ecb, int2 center, int radius, int tileID)
        {
            var min = center - new int2(radius, radius);
            var max = center + new int2(radius, radius);
            TilemapUtilities.PaintRectangle(ecb, min, max, tileID);
        }
    }

    /// <summary>
    /// Static utility class for grid occupancy management and multi-tile operations
    /// </summary>
    public static class GridOccupancyManager
    {
        /// <summary>
        /// Checks if a footprint area is free of occupied cells
        /// </summary>
        public static bool IsFootprintFree(int2 origin, int2 size, NativeArray<Tile> existingTiles, NativeArray<Entity> structureEntities, ComponentLookup<Footprint> footprintLookup)
        {
            // Check single tiles first
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    var checkPos = origin + new int2(x, y);
                    
                    // Check if any single tile occupies this position
                    for (int i = 0; i < existingTiles.Length; i++)
                    {
                        if (existingTiles[i].GridPosition.Equals(checkPos))
                            return false;
                    }
                }
            }

            // Check multi-tile structures
            for (int i = 0; i < structureEntities.Length; i++)
            {
                if (!footprintLookup.HasComponent(structureEntities[i])) continue;
                
                var footprint = footprintLookup[structureEntities[i]];
                
                // Check if footprints overlap
                if (FootprintsOverlap(origin, size, footprint.Origin, footprint.Size))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if two footprints overlap
        /// </summary>
        public static bool FootprintsOverlap(int2 origin1, int2 size1, int2 origin2, int2 size2)
        {
            var max1 = origin1 + size1 - 1;
            var max2 = origin2 + size2 - 1;
            
            return !(max1.x < origin2.x || origin1.x > max2.x || max1.y < origin2.y || origin1.y > max2.y);
        }

        /// <summary>
        /// Fills a DynamicBuffer with all positions covered by a footprint
        /// </summary>
        public static void FillOccupiedCells(DynamicBuffer<OccupiedCell> buffer, int2 origin, int2 size)
        {
            buffer.Clear();
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    buffer.Add(new OccupiedCell(origin + new int2(x, y)));
                }
            }
        }

        /// <summary>
        /// Gets the structure entity occupying a specific grid position, if any
        /// </summary>
        public static Entity GetStructureAtPosition(int2 position, NativeArray<Entity> structureEntities, ComponentLookup<Footprint> footprintLookup)
        {
            for (int i = 0; i < structureEntities.Length; i++)
            {
                var entity = structureEntities[i];
                if (!footprintLookup.HasComponent(entity)) continue;
                
                var footprint = footprintLookup[entity];
                
                if (position.x >= footprint.Origin.x && position.x < footprint.Origin.x + footprint.Size.x &&
                    position.y >= footprint.Origin.y && position.y < footprint.Origin.y + footprint.Size.y)
                {
                    return entity;
                }
            }
            
            return Entity.Null;
        }
    }
}