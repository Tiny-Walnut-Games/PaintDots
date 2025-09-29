using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using PaintDots.ECS.Config;

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
                : new TilemapConfig();
            
            // Process paint commands
            var job = new PaintCommandJob
            {
                ECB = ecb.AsParallelWriter(),
                Config = config,
                ExistingTiles = _existingTileQuery.ToComponentDataArray<Tile>(Allocator.TempJob)
            };
            
            state.Dependency = job.ScheduleParallel(_paintCommandQuery, state.Dependency);
            job.ExistingTiles.Dispose(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        private struct PaintCommandJob : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public TilemapConfig Config;
            [ReadOnly] public NativeArray<Tile> ExistingTiles;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var commands = chunk.GetNativeArray(SystemAPI.GetComponentTypeHandle<PaintCommand>(true));
                var entities = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                
                while (enumerator.NextEntityIndex(out var i))
                {
                    var command = commands[i];
                    var commandEntity = entities[i];
                    
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
                        default,
                        default,
                        Config.DefaultTileColor,
                        command.TileID
                    ));
                    
                    // Remove the paint command
                    ECB.DestroyEntity(unfilteredChunkIndex, commandEntity);
                }
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
                : new AutoTileConfig();

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
    /// System that processes erase commands and removes tiles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public sealed partial struct TilemapEraserSystem : ISystem
    {
        private EntityQuery _tileQuery;

        public void OnCreate(ref SystemState state)
        {
            _tileQuery = state.GetEntityQuery(typeof(Tile));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Get all existing tiles for efficient lookup
            using var existingTiles = _tileQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            using var tileEntities = _tileQuery.ToEntityArray(Allocator.TempJob);

            // Process erase commands
            foreach (var (eraseCommand, commandEntity) in 
                SystemAPI.Query<RefRO<EraseCommand>>().WithEntityAccess())
            {
                var gridPos = eraseCommand.ValueRO.GridPosition;
                
                // Find tile at this position and destroy it
                for (int i = 0; i < existingTiles.Length; i++)
                {
                    if (existingTiles[i].GridPosition.Equals(gridPos))
                    {
                        ecb.DestroyEntity(tileEntities[i]);
                        break;
                    }
                }

                // Remove the erase command
                ecb.DestroyEntity(commandEntity);
            }
        }

        public void OnDestroy(ref SystemState state) { }
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
                : new TilemapConfig();
            
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

    /// <summary>
    /// System for serializing and deserializing tilemap state
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public sealed partial struct TilemapSerializationSystem : ISystem
    {
        public void OnCreate(ref SystemState state) 
        {
            // Require tilemap config to exist
            state.RequireForUpdate<TilemapConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system responds to serialization commands
            // Implementation would handle SaveTilemapCommand and LoadTilemapCommand
        }

        /// <summary>
        /// Creates a BlobAsset containing the current tilemap state
        /// </summary>
        public static BlobAssetReference<TilemapStateAsset> SerializeTilemap(EntityManager entityManager, Allocator allocator)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var stateAsset = ref builder.ConstructRoot<TilemapStateAsset>();

            // Get all tiles
            using var tileQuery = entityManager.CreateEntityQuery(typeof(Tile), typeof(TileRenderData));
            using var tileEntities = tileQuery.ToEntityArray(Allocator.Temp);
            using var tiles = tileQuery.ToComponentDataArray<Tile>(Allocator.Temp);
            using var renderData = tileQuery.ToComponentDataArray<TileRenderData>(Allocator.Temp);

            // Serialize tiles
            var tilesArray = builder.Allocate(ref stateAsset.Tiles, tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                var render = renderData[i];
                
                bool isAutoTile = entityManager.HasComponent<AutoTile>(tileEntities[i]);
                
                tilesArray[i] = new SerializedTile(
                    tile.GridPosition,
                    tile.TileID,
                    render.SpriteIndex,
                    render.Color,
                    isAutoTile
                );
            }

            // Calculate bounds and create tilemap metadata
            var minBounds = new int2(int.MaxValue, int.MaxValue);
            var maxBounds = new int2(int.MinValue, int.MinValue);
            
            for (int i = 0; i < tiles.Length; i++)
            {
                var pos = tiles[i].GridPosition;
                minBounds = math.min(minBounds, pos);
                maxBounds = math.max(maxBounds, pos);
            }

            var tilemapsArray = builder.Allocate(ref stateAsset.Tilemaps, 1);
            tilemapsArray[0] = new SerializedTilemap(
                new int2(32, 32), // Default chunk size
                minBounds,
                maxBounds,
                tiles.Length
            );

            stateAsset.TileSize = 1.0f; // Default tile size
            stateAsset.Version = 1;

            return builder.CreateBlobAssetReference<TilemapStateAsset>(allocator);
        }

        /// <summary>
        /// Loads tilemap state from BlobAsset and creates tile entities
        /// </summary>
        public static void DeserializeTilemap(EntityManager entityManager, BlobAssetReference<TilemapStateAsset> stateAsset)
        {
            ref var asset = ref stateAsset.Value;
            
            // Clear existing tiles
            using var existingQuery = entityManager.CreateEntityQuery(typeof(Tile));
            entityManager.DestroyEntity(existingQuery);

            // Create new tiles from serialized data
            for (int i = 0; i < asset.Tiles.Length; i++)
            {
                var serializedTile = asset.Tiles[i];
                
                var entity = entityManager.CreateEntity();
                entityManager.AddComponent(entity, new Tile(serializedTile.GridPosition, serializedTile.TileID));
                entityManager.AddComponent<TilemapTag>(entity);
                
                // Add rendering data
                entityManager.AddComponent(entity, new TileRenderData(
                    Entity.Null,
                    Entity.Null,
                    serializedTile.Color,
                    serializedTile.VariantIndex
                ));

                // Add AutoTile if needed
                if (serializedTile.IsAutoTile)
                {
                    entityManager.AddComponent(entity, new AutoTile(Entity.Null));
                }

                // Set world position
                var worldPos = new float3(
                    serializedTile.GridPosition.x * asset.TileSize,
                    serializedTile.GridPosition.y * asset.TileSize,
                    0
                );
                entityManager.AddComponent(entity, new LocalTransform
                {
                    Position = worldPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for updating animated tiles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public sealed partial struct AnimatedTileSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Update animated tiles
            foreach (var (animatedTile, renderData, entity) in 
                SystemAPI.Query<RefRW<AnimatedTile>, RefRW<TileRenderData>>()
                    .WithEntityAccess())
            {
                if (!animatedTile.ValueRO.IsPlaying) continue;

                ref readonly var animData = ref animatedTile.ValueRO.AnimationData.Value;
                if (animData.Frames.Length == 0) continue;

                var currentTime = animatedTile.ValueRO.CurrentTime + deltaTime;
                var currentFrame = animatedTile.ValueRO.CurrentFrame;

                // Check if we need to advance to next frame
                if (currentFrame < animData.Frames.Length)
                {
                    var frame = animData.Frames[currentFrame];
                    
                    if (currentTime >= frame.Duration)
                    {
                        // Advance to next frame
                        currentTime = 0f;
                        currentFrame++;
                        
                        // Handle looping
                        if (currentFrame >= animData.Frames.Length)
                        {
                            if (animData.Loop)
                            {
                                currentFrame = 0;
                            }
                            else
                            {
                                currentFrame = animData.Frames.Length - 1;
                            }
                        }

                        // Update render data with new frame
                        if (currentFrame < animData.Frames.Length)
                        {
                            var newFrame = animData.Frames[currentFrame];
                            renderData.ValueRW = renderData.ValueRO.WithSpriteIndex(newFrame.SpriteIndex);
                        }
                    }
                }

                // Update animated tile state
                animatedTile.ValueRW = animatedTile.ValueRO.WithTime(currentTime, currentFrame);
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for managing tilemap chunks and culling
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public sealed partial struct ChunkManagementSystem : ISystem
    {
        private EntityQuery _chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            _chunkQuery = state.GetEntityQuery(typeof(TilemapChunk));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Simple chunk culling - in practice this would be based on camera position
            var viewDistance = 100f; // Max view distance
            
            foreach (var (chunk, transform, entity) in 
                SystemAPI.Query<RefRW<TilemapChunk>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                var chunkPos = transform.ValueRO.Position;
                var distanceFromOrigin = math.length(chunkPos);
                
                bool shouldBeActive = distanceFromOrigin <= viewDistance;
                
                if (chunk.ValueRO.IsActive != shouldBeActive)
                {
                    chunk.ValueRW = chunk.ValueRO.WithActiveState(shouldBeActive);
                    
                    // Enable/disable chunk tiles based on chunk active state
                    var chunkTiles = SystemAPI.GetBuffer<ChunkTile>(entity);
                    for (int i = 0; i < chunkTiles.Length; i++)
                    {
                        var tileEntity = chunkTiles[i].TileEntity;
                        if (state.EntityManager.Exists(tileEntity))
                        {
                            state.EntityManager.SetEnabled(tileEntity, shouldBeActive);
                        }
                    }
                }
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for burst-compiled procedural mesh generation
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public sealed partial struct ProceduralMeshSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Generate procedural meshes for tiles that need them
            // This is a simplified version - full implementation would handle mesh generation
            
            foreach (var (tile, renderData, entity) in 
                SystemAPI.Query<RefRO<Tile>, RefRW<TileRenderData>>()
                    .WithEntityAccess()
                    .WithNone<ProceduralMeshGenerated>())
            {
                // In practice, this would generate actual mesh data
                // For now, we just mark it as generated
                var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged);
                    
                ecb.AddComponent<ProceduralMeshGenerated>(entity);
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// Tag component to mark tiles with procedurally generated meshes
    /// </summary>
    public readonly struct ProceduralMeshGenerated : IComponentData { }
}