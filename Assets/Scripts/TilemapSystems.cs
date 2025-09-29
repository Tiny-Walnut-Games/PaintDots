using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using PaintDots.ECS.Config;
using PaintDots.ECS.Utilities;

namespace PaintDots.ECS.Systems
{
    /// <summary>
    /// System that listens for paint commands and spawns/updates tiles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public sealed partial struct TilemapPainterSystem : ISystem
    {
        private EntityQuery _paintCommandQuery;
        private EntityQuery _existingTileQuery;
        private EntityQuery _configQuery;
        private EntityQuery _structureQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _paintCommandQuery = SystemAPI.QueryBuilder()
                .WithAll<PaintCommand>()
                .Build();
                
            _existingTileQuery = SystemAPI.QueryBuilder()
                .WithAll<Tile, TilemapTag>()
                .Build();

            _configQuery = SystemAPI.QueryBuilder()
                .WithAll<TilemapConfig>()
                .Build();

            _structureQuery = SystemAPI.QueryBuilder()
                .WithAll<Footprint>()
                .Build();
                
            state.RequireForUpdate(_paintCommandQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            // Get configuration
            var config = SystemAPI.HasSingleton<TilemapConfig>() 
                ? SystemAPI.GetSingleton<TilemapConfig>() 
                : TilemapConfig.CreateDefault();
            
            // Process paint commands
            var job = new PaintCommandJob
            {
                ECB = ecb.AsParallelWriter(),
                Config = config,
                ExistingTiles = _existingTileQuery.ToComponentDataArray<Tile>(Allocator.TempJob),
                StructureEntities = _structureQuery.ToEntityArray(Allocator.TempJob),
                FootprintLookup = SystemAPI.GetComponentLookup<Footprint>(true)
            };
            
            state.Dependency = job.ScheduleParallel(_paintCommandQuery, state.Dependency);
            job.ExistingTiles.Dispose(state.Dependency);
            job.StructureEntities.Dispose(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        private struct PaintCommandJob : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public TilemapConfig Config;
            [ReadOnly] public NativeArray<Tile> ExistingTiles;
            [ReadOnly] public NativeArray<Entity> StructureEntities;
            [ReadOnly] public ComponentLookup<Footprint> FootprintLookup;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var commands = chunk.GetNativeArray(SystemAPI.GetComponentTypeHandle<PaintCommand>(true));
                var entities = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                
                while (enumerator.NextEntityIndex(out var i))
                {
                    var command = commands[i];
                    var commandEntity = entities[i];
                    
                    if (command.IsMultiTile)
                    {
                        ProcessMultiTileCommand(command, commandEntity, unfilteredChunkIndex);
                    }
                    else
                    {
                        ProcessSingleTileCommand(command, commandEntity, unfilteredChunkIndex);
                    }
                }
            }

            private void ProcessSingleTileCommand(PaintCommand command, Entity commandEntity, int unfilteredChunkIndex)
            {
                var existingTileIndex = FindTileAtPosition(command.GridPosition, ExistingTiles);
                
                if (existingTileIndex >= 0)
                {
                    // Update existing tile - this would require a different approach in pure ECS
                    // For now, we'll create a new tile and mark the old one for destruction
                    ECB.DestroyEntity(unfilteredChunkIndex, ExistingTiles[existingTileIndex].GridPosition.GetHashCode()); // Using a proxy entity
                }
                
                // Create new tile entity
                var newTile = ECB.CreateEntity(unfilteredChunkIndex);
                ECB.AddComponent(unfilteredChunkIndex, newTile, new Tile(command.GridPosition, command.TileID));
                ECB.AddComponent<TilemapTag>(unfilteredChunkIndex, newTile);
                ECB.AddComponent(unfilteredChunkIndex, newTile, LocalTransform.FromPosition(
                    new float3(command.GridPosition.x * Config.TileSize, command.GridPosition.y * Config.TileSize, 0)));
                
                // Add rendering components
                ECB.AddComponent(unfilteredChunkIndex, newTile, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    Config.DefaultTileColor,
                    command.TileID
                ));
                
                // Remove the paint command
                ECB.DestroyEntity(unfilteredChunkIndex, commandEntity);
            }

            private void ProcessMultiTileCommand(PaintCommand command, Entity commandEntity, int unfilteredChunkIndex)
            {
                // Check if the footprint area is free
                if (!GridOccupancyManager.IsFootprintFree(command.GridPosition, command.Size, ExistingTiles, StructureEntities, FootprintLookup))
                {
                    // Area is occupied, remove command without creating entity
                    ECB.DestroyEntity(unfilteredChunkIndex, commandEntity);
                    return;
                }

                // Create structure entity with footprint
                var structureEntity = ECB.CreateEntity(unfilteredChunkIndex);
                ECB.AddComponent(unfilteredChunkIndex, structureEntity, new Footprint(command.GridPosition, command.Size));
                ECB.AddComponent<TilemapTag>(unfilteredChunkIndex, structureEntity);
                
                // Position at the origin
                ECB.AddComponent(unfilteredChunkIndex, structureEntity, LocalTransform.FromPosition(
                    new float3(command.GridPosition.x * Config.TileSize, command.GridPosition.y * Config.TileSize, 0)));
                
                // Add rendering components
                ECB.AddComponent(unfilteredChunkIndex, structureEntity, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    Config.DefaultTileColor,
                    command.TileID
                ));

                // Add buffer for occupied cells and fill it
                var buffer = ECB.AddBuffer<OccupiedCell>(unfilteredChunkIndex, structureEntity);
                for (int x = 0; x < command.Size.x; x++)
                {
                    for (int y = 0; y < command.Size.y; y++)
                    {
                        buffer.Add(new OccupiedCell(command.GridPosition + new int2(x, y)));
                    }
                }
                
                // Remove the paint command
                ECB.DestroyEntity(unfilteredChunkIndex, commandEntity);
            }
            
            private int FindTileAtPosition(int2 position, NativeArray<Tile> tiles)
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].GridPosition.Equals(position))
                        return i;
                }
                return -1;
            }
        }
    }

    /// <summary>
    /// System for processing AutoTile rules and updating tile variants
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public sealed partial struct AutoTileSystem : ISystem
    {
        private EntityQuery _autoTileQuery;
        private EntityQuery _allTilesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _autoTileQuery = SystemAPI.QueryBuilder()
                .WithAll<Tile, AutoTile, TileRenderData>()
                .Build();
                
            _allTilesQuery = SystemAPI.QueryBuilder()
                .WithAll<Tile>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_autoTileQuery.IsEmpty) return;
            
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
                
            var config = SystemAPI.HasSingleton<AutoTileConfig>()
                ? SystemAPI.GetSingleton<AutoTileConfig>()
                : AutoTileConfig.CreateDefault();

            var allTiles = _allTilesQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            
            var job = new AutoTileJob
            {
                ECB = ecb.AsParallelWriter(),
                Config = config,
                AllTiles = allTiles
            };
            
            state.Dependency = job.ScheduleParallel(_autoTileQuery, state.Dependency);
            allTiles.Dispose(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        private struct AutoTileJob : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public AutoTileConfig Config;
            [ReadOnly] public NativeArray<Tile> AllTiles;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var tiles = chunk.GetNativeArray(SystemAPI.GetComponentTypeHandle<Tile>(true));
                var autoTiles = chunk.GetNativeArray(SystemAPI.GetComponentTypeHandle<AutoTile>(true));
                var renderData = chunk.GetNativeArray(SystemAPI.GetComponentTypeHandle<TileRenderData>(true));
                var entities = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                
                while (enumerator.NextEntityIndex(out var i))
                {
                    var tile = tiles[i];
                    var autoTile = autoTiles[i];
                    var render = renderData[i];
                    var entity = entities[i];
                    
                    var neighbors = CalculateNeighbors(tile.GridPosition, AllTiles);
                    var newFlags = CalculateAutoTileFlags(neighbors);
                    
                    if (autoTile.RuleFlags != newFlags)
                    {
                        var variantIndex = GetVariantIndexFromFlags(newFlags, Config);
                        var newAutoTile = autoTile.WithRuleFlags(newFlags).WithVariantIndex(variantIndex);
                        var newRenderData = render.WithSpriteIndex(variantIndex);
                        
                        ECB.SetComponent(unfilteredChunkIndex, entity, newAutoTile);
                        ECB.SetComponent(unfilteredChunkIndex, entity, newRenderData);
                    }
                }
            }
            
            private byte CalculateNeighbors(int2 position, NativeArray<Tile> allTiles)
            {
                byte neighbors = 0;
                
                // Check 8 surrounding positions (3x3 grid minus center)
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue; // Skip center
                        
                        var checkPos = position + new int2(x, y);
                        if (HasTileAtPosition(checkPos, allTiles))
                        {
                            var bitIndex = (y + 1) * 3 + (x + 1);
                            if (bitIndex > 4) bitIndex--; // Skip center position in bit calculation
                            neighbors |= (byte)(1 << bitIndex);
                        }
                    }
                }
                return neighbors;
            }

            private bool HasTileAtPosition(int2 position, NativeArray<Tile> tiles)
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].GridPosition.Equals(position))
                        return true;
                }
                return false;
            }

            private byte CalculateAutoTileFlags(byte neighbors)
            {
                return neighbors;
            }

            private int GetVariantIndexFromFlags(byte flags, AutoTileConfig config)
            {
                return flags % config.RulesetSize;
            }
        }
    }

    /// <summary>
    /// System for rendering tile entities using Unity's Entities Graphics
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public sealed partial struct TileRenderingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Initialize rendering setup
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.HasSingleton<TilemapConfig>() 
                ? SystemAPI.GetSingleton<TilemapConfig>() 
                : TilemapConfig.CreateDefault();
            
            // Update transforms based on grid positions
            foreach (var (tile, transform) in 
                SystemAPI.Query<Tile, RefRW<LocalTransform>>().WithAll<TilemapTag>())
            {
                var worldPos = new float3(tile.GridPosition.x * config.TileSize, tile.GridPosition.y * config.TileSize, 0);
                transform.ValueRW.Position = worldPos;
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}