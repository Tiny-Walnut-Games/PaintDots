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

        /// <summary>
        /// Creates noise-based paint commands using Perlin noise
        /// </summary>
        public static void PaintNoise(EntityCommandBuffer ecb, int2 center, int radius, int tileID, float threshold, uint seed)
        {
            var random = Unity.Mathematics.Random.CreateFromIndex(seed);
            
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    var pos = new int2(x, y);
                    var distance = math.distance(new float2(center.x, center.y), new float2(x, y));
                    
                    if (distance <= radius)
                    {
                        // Use noise to determine if tile should be placed
                        var noiseValue = noise.cnoise(new float2(x * 0.1f, y * 0.1f) + random.NextFloat2());
                        
                        if (noiseValue > threshold)
                        {
                            CreatePaintCommand(ecb, pos, tileID);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates sparse random noise pattern
        /// </summary>
        public static void PaintNoiseSparse(EntityCommandBuffer ecb, int2 center, int radius, int tileID, uint seed)
        {
            PaintNoise(ecb, center, radius, tileID, 0.7f, seed); // Higher threshold = sparser
        }

        /// <summary>
        /// Creates dense random noise pattern
        /// </summary>
        public static void PaintNoiseDense(EntityCommandBuffer ecb, int2 center, int radius, int tileID, uint seed)
        {
            PaintNoise(ecb, center, radius, tileID, 0.2f, seed); // Lower threshold = denser
        }

        /// <summary>
        /// Creates an erase command for a specific position
        /// </summary>
        public static void CreateEraseCommand(EntityCommandBuffer ecb, int2 gridPos)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new EraseCommand(gridPos));
        }

        /// <summary>
        /// Creates multiple erase commands for a rectangular area
        /// </summary>
        public static void EraseRectangle(EntityCommandBuffer ecb, int2 min, int2 max)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    CreateEraseCommand(ecb, new int2(x, y));
                }
            }
        }

        /// <summary>
        /// Creates erase commands for a circular area
        /// </summary>
        public static void EraseCircle(EntityCommandBuffer ecb, int2 center, int radius)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    var pos = new int2(x, y);
                    var distance = math.distance(new float2(center.x, center.y), new float2(x, y));
                    
                    if (distance <= radius)
                    {
                        CreateEraseCommand(ecb, pos);
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
        Fill,
        RectangleFill,
        NoisePattern,
        NoiseSparse,
        NoiseDense
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
        public readonly float NoiseThreshold;
        public readonly uint NoiseSeed;

        public BrushConfig(BrushType type, int size, int tileID, bool useAutoTile = false, float noiseThreshold = 0.5f, uint noiseSeed = 12345)
        {
            Type = type;
            Size = size;
            TileID = tileID;
            UseAutoTile = useAutoTile;
            NoiseThreshold = noiseThreshold;
            NoiseSeed = noiseSeed;
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

                case BrushType.RectangleFill:
                    var min = position - new int2(brush.Size / 2, brush.Size / 2);
                    var max = position + new int2(brush.Size / 2, brush.Size / 2);
                    TilemapUtilities.PaintRectangle(ecb, min, max, brush.TileID);
                    break;

                case BrushType.NoisePattern:
                    TilemapUtilities.PaintNoise(ecb, position, brush.Size, brush.TileID, brush.NoiseThreshold, brush.NoiseSeed);
                    break;

                case BrushType.NoiseSparse:
                    TilemapUtilities.PaintNoiseSparse(ecb, position, brush.Size, brush.TileID, brush.NoiseSeed);
                    break;

                case BrushType.NoiseDense:
                    TilemapUtilities.PaintNoiseDense(ecb, position, brush.Size, brush.TileID, brush.NoiseSeed);
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
    /// Utility class for managing chunked tilemaps
    /// </summary>
    public static class ChunkUtilities
    {
        /// <summary>
        /// Converts world grid position to chunk coordinates
        /// </summary>
        public static int2 WorldToChunk(int2 gridPosition, int2 chunkSize)
        {
            return new int2(
                gridPosition.x / chunkSize.x,
                gridPosition.y / chunkSize.y
            );
        }

        /// <summary>
        /// Converts world grid position to local position within chunk
        /// </summary>
        public static int2 WorldToLocal(int2 gridPosition, int2 chunkSize)
        {
            return new int2(
                ((gridPosition.x % chunkSize.x) + chunkSize.x) % chunkSize.x,
                ((gridPosition.y % chunkSize.y) + chunkSize.y) % chunkSize.y
            );
        }

        /// <summary>
        /// Converts chunk coordinates and local position to world grid position
        /// </summary>
        public static int2 ChunkToWorld(int2 chunkCoords, int2 localPos, int2 chunkSize)
        {
            return chunkCoords * chunkSize + localPos;
        }

        /// <summary>
        /// Creates or gets a chunk entity for the given chunk coordinates
        /// </summary>
        public static Entity GetOrCreateChunk(EntityCommandBuffer ecb, int2 chunkCoords, int2 chunkSize)
        {
            // In a full implementation, this would check for existing chunks first
            var chunkEntity = ecb.CreateEntity();
            ecb.AddComponent(chunkEntity, new TilemapChunk(chunkCoords, chunkSize));
            ecb.AddComponent<TilemapTag>(chunkEntity);
            ecb.AddBuffer<ChunkTile>(chunkEntity);
            
            // Set chunk world position
            var worldPos = new float3(
                chunkCoords.x * chunkSize.x,
                chunkCoords.y * chunkSize.y,
                0
            );
            ecb.AddComponent(chunkEntity, new LocalTransform
            {
                Position = worldPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            return chunkEntity;
        }

        /// <summary>
        /// Creates a tile in the appropriate chunk
        /// </summary>
        public static Entity CreateChunkedTile(EntityCommandBuffer ecb, int2 gridPos, int tileID, int2 chunkSize)
        {
            var chunkCoords = WorldToChunk(gridPos, chunkSize);
            var localPos = WorldToLocal(gridPos, chunkSize);
            
            var chunkEntity = GetOrCreateChunk(ecb, chunkCoords, chunkSize);
            
            var tileEntity = ecb.CreateEntity();
            ecb.AddComponent(tileEntity, new Tile(gridPos, tileID));
            ecb.AddComponent<TilemapTag>(tileEntity);
            ecb.AddComponent(tileEntity, new TileChunkReference(chunkEntity, localPos));
            
            // Add tile to chunk buffer
            var chunkTile = new ChunkTile(tileEntity, localPos);
            // Note: In practice, you'd need to get the buffer and append to it
            // This would require a different approach or deferred execution
            
            return tileEntity;
        }
    }
}