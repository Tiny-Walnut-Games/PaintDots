using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

namespace PaintDots.ECS.Systems
{
    /// <summary>
    /// System that listens for paint commands and spawns/updates tiles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TilemapPainterSystem : ISystem
    {
        private EntityQuery _paintCommandQuery;
        private EntityQuery _existingTileQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _paintCommandQuery = SystemAPI.QueryBuilder()
                .WithAll<PaintCommand>()
                .Build();
                
            _existingTileQuery = SystemAPI.QueryBuilder()
                .WithAll<Tile, TilemapTag>()
                .Build();
                
            state.RequireForUpdate(_paintCommandQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Process paint commands
            foreach (var (command, commandEntity) in 
                SystemAPI.Query<RefRO<PaintCommand>>().WithEntityAccess())
            {
                var gridPos = command.ValueRO.GridPosition;
                var tileID = command.ValueRO.TileID;
                
                // Check if tile already exists at this position
                var existingTile = FindTileAtPosition(gridPos, state.EntityManager);
                
                if (existingTile != Entity.Null)
                {
                    // Update existing tile
                    var tileComponent = state.EntityManager.GetComponentData<Tile>(existingTile);
                    tileComponent.TileID = tileID;
                    state.EntityManager.SetComponentData(existingTile, tileComponent);
                }
                else
                {
                    // Create new tile entity
                    var newTile = ecb.CreateEntity();
                    ecb.AddComponent(newTile, new Tile 
                    { 
                        GridPosition = gridPos, 
                        TileID = tileID 
                    });
                    ecb.AddComponent<TilemapTag>(newTile);
                    ecb.AddComponent(newTile, LocalTransform.FromPosition(
                        new float3(gridPos.x, gridPos.y, 0)));
                    
                    // Add rendering components (will be handled by rendering system)
                    ecb.AddComponent(newTile, new TileRenderData
                    {
                        Color = new float4(1, 1, 1, 1),
                        SpriteIndex = tileID
                    });
                }
                
                // Remove the paint command
                ecb.DestroyEntity(commandEntity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity FindTileAtPosition(int2 position, EntityManager entityManager)
        {
            foreach (var (tile, entity) in 
                SystemAPI.Query<RefRO<Tile>>().WithEntityAccess())
            {
                if (tile.ValueRO.GridPosition.Equals(position))
                    return entity;
            }
            return Entity.Null;
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for processing AutoTile rules and updating tile variants
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public partial struct AutoTileSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System will update when there are tiles with AutoTile components
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Process AutoTile rules for tiles that have been modified
            foreach (var (tile, autoTile, renderData, entity) in 
                SystemAPI.Query<RefRO<Tile>, RefRW<AutoTile>, RefRW<TileRenderData>>().WithEntityAccess())
            {
                var neighbors = CalculateNeighbors(tile.ValueRO.GridPosition, state.EntityManager);
                var newFlags = CalculateAutoTileFlags(neighbors);
                
                if (autoTile.ValueRO.RuleFlags != newFlags)
                {
                    autoTile.ValueRW.RuleFlags = newFlags;
                    var variantIndex = GetVariantIndexFromFlags(newFlags);
                    autoTile.ValueRW.VariantIndex = variantIndex;
                    renderData.ValueRW.SpriteIndex = variantIndex;
                }
            }
        }

        private byte CalculateNeighbors(int2 position, EntityManager entityManager)
        {
            byte neighbors = 0;
            
            // Check 8 surrounding positions (3x3 grid minus center)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip center
                    
                    var checkPos = position + new int2(x, y);
                    if (HasTileAtPosition(checkPos, entityManager))
                    {
                        var bitIndex = (y + 1) * 3 + (x + 1);
                        if (bitIndex > 4) bitIndex--; // Skip center position in bit calculation
                        neighbors |= (byte)(1 << bitIndex);
                    }
                }
            }
            return neighbors;
        }

        private bool HasTileAtPosition(int2 position, EntityManager entityManager)
        {
            foreach (var tile in SystemAPI.Query<RefRO<Tile>>())
            {
                if (tile.ValueRO.GridPosition.Equals(position))
                    return true;
            }
            return false;
        }

        private byte CalculateAutoTileFlags(byte neighbors)
        {
            // Convert neighbor information to AutoTile rule flags
            // This follows Unity's AutoTile pattern matching logic
            return neighbors;
        }

        private int GetVariantIndexFromFlags(byte flags)
        {
            // Map rule flags to sprite variant index
            // This would use the AutoTile asset's rule definitions in production
            return flags % 16; // Simplified mapping for now
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for rendering tile entities using Unity's Entities Graphics
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TileRenderingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Initialize rendering setup
        }

        public void OnUpdate(ref SystemState state)
        {
            // Update transforms based on grid positions
            foreach (var (tile, transform) in 
                SystemAPI.Query<RefRO<Tile>, RefRW<LocalTransform>>().WithAll<TilemapTag>())
            {
                var worldPos = new float3(tile.ValueRO.GridPosition.x, tile.ValueRO.GridPosition.y, 0);
                transform.ValueRW.Position = worldPos;
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}