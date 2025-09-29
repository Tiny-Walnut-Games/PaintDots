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
}