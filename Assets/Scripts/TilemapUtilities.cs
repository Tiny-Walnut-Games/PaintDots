using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
        public static int2 WorldToGrid(float3 worldPos, float tileSize = 1.0f)
        {
            return new int2(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }

        /// <summary>
        /// Converts grid position to world position
        /// </summary>
        public static float3 GridToWorld(int2 gridPos, float tileSize = 1.0f)
        {
            return new float3(gridPos.x * tileSize, gridPos.y * tileSize, 0);
        }

        /// <summary>
        /// Gets neighboring grid positions (8-directional)
        /// </summary>
        public static NativeArray<int2> GetNeighbors(int2 center, Allocator allocator)
        {
            var neighbors = new NativeArray<int2>(8, allocator);
            int index = 0;
            
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    neighbors[index++] = center + new int2(x, y);
                }
            }
            
            return neighbors;
        }

        /// <summary>
        /// Creates a paint command entity for batch painting
        /// </summary>
        public static Entity CreatePaintCommand(EntityManager entityManager, int2 gridPos, int tileID)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new PaintCommand
            {
                GridPosition = gridPos,
                TileID = tileID
            });
            return entity;
        }

        /// <summary>
        /// Batch paint multiple tiles at once
        /// </summary>
        public static void BatchPaint(EntityManager entityManager, NativeArray<int2> positions, int tileID)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                CreatePaintCommand(entityManager, positions[i], tileID);
            }
        }

        /// <summary>
        /// Creates a rectangular area of paint commands
        /// </summary>
        public static void PaintRectangle(EntityManager entityManager, int2 min, int2 max, int tileID)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    CreatePaintCommand(entityManager, new int2(x, y), tileID);
                }
            }
        }

        /// <summary>
        /// Creates a filled circle of paint commands
        /// </summary>
        public static void PaintCircle(EntityManager entityManager, int2 center, int radius, int tileID)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    var pos = new int2(x, y);
                    var distance = math.distance(new float2(center.x, center.y), new float2(x, y));
                    
                    if (distance <= radius)
                    {
                        CreatePaintCommand(entityManager, pos, tileID);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Brush types for different painting patterns
    /// </summary>
    public enum BrushType
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
    public struct BrushConfig
    {
        public BrushType Type;
        public int Size;
        public int TileID;
        public bool UseAutoTile;
    }

    /// <summary>
    /// Brush system for applying different painting patterns
    /// </summary>
    public static class BrushSystem
    {
        /// <summary>
        /// Apply brush at given position
        /// </summary>
        public static void ApplyBrush(EntityManager entityManager, int2 position, BrushConfig brush)
        {
            switch (brush.Type)
            {
                case BrushType.Single:
                    TilemapUtilities.CreatePaintCommand(entityManager, position, brush.TileID);
                    break;
                    
                case BrushType.Square3x3:
                    PaintSquare(entityManager, position, 1, brush.TileID);
                    break;
                    
                case BrushType.Square5x5:
                    PaintSquare(entityManager, position, 2, brush.TileID);
                    break;
                    
                case BrushType.Circle:
                    TilemapUtilities.PaintCircle(entityManager, position, brush.Size, brush.TileID);
                    break;
            }
        }

        private static void PaintSquare(EntityManager entityManager, int2 center, int radius, int tileID)
        {
            var min = center - new int2(radius, radius);
            var max = center + new int2(radius, radius);
            TilemapUtilities.PaintRectangle(entityManager, min, max, tileID);
        }
    }
}