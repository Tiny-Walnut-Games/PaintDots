using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using PaintDots.Runtime.Config;
using PaintDots.Runtime.Utilities;

namespace PaintDots.Runtime.Systems
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

        public void OnUpdate(ref SystemState state)
        {
            if (_paintCommandQuery.IsEmpty) return;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var config = SystemAPI.HasSingleton<TilemapConfig>()
                ? SystemAPI.GetSingleton<TilemapConfig>()
                : TilemapConfig.CreateDefault();

            var existingTiles = _existingTileQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            var existingTileEntities = _existingTileQuery.ToEntityArray(Allocator.TempJob);
            var structureEntities = _structureQuery.ToEntityArray(Allocator.TempJob);
            var footprintLookup = SystemAPI.GetComponentLookup<Footprint>(true);
            var entityManager = state.EntityManager;

            foreach (var (commandRO, commandEntity) in SystemAPI.Query<RefRO<PaintCommand>>().WithEntityAccess())
            {
                var command = commandRO.ValueRO;

                if (command.IsMultiTile)
                {
                    ProcessMultiTileCommand(command, commandEntity);
                }
                else
                {
                    ProcessSingleTileCommand(command, commandEntity);
                }
            }

            existingTiles.Dispose();
            existingTileEntities.Dispose();
            structureEntities.Dispose();

            void ProcessSingleTileCommand(in PaintCommand command, Entity commandEntity)
            {
                var existingIndex = FindTileAtPosition(command.GridPosition, existingTiles);
                if (existingIndex >= 0)
                {
                    var existingEntity = existingTileEntities[existingIndex];
                    if (entityManager.Exists(existingEntity))
                    {
                        ecb.DestroyEntity(existingEntity);
                    }
                }

                var position = new float3(command.GridPosition.x * config.TileSize, command.GridPosition.y * config.TileSize, 0f);
                var newTile = ecb.CreateEntity();
                ecb.AddComponent(newTile, new Tile(command.GridPosition, command.TileID));
                ecb.AddComponent<TilemapTag>(newTile);
                ecb.AddComponent(newTile, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                ecb.AddComponent(newTile, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    config.DefaultTileColor,
                    command.TileID));

                ecb.DestroyEntity(commandEntity);
            }

            void ProcessMultiTileCommand(in PaintCommand command, Entity commandEntity)
            {
                if (!GridOccupancyManager.IsFootprintFree(command.GridPosition, command.Size, existingTiles, structureEntities, footprintLookup))
                {
                    ecb.DestroyEntity(commandEntity);
                    return;
                }

                var position = new float3(command.GridPosition.x * config.TileSize, command.GridPosition.y * config.TileSize, 0f);
                var structureEntity = ecb.CreateEntity();
                ecb.AddComponent(structureEntity, new Footprint(command.GridPosition, command.Size));
                ecb.AddComponent<TilemapTag>(structureEntity);
                ecb.AddComponent(structureEntity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                ecb.AddComponent(structureEntity, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    config.DefaultTileColor,
                    command.TileID));

                var buffer = ecb.AddBuffer<OccupiedCell>(structureEntity);
                for (int x = 0; x < command.Size.x; x++)
                {
                    for (int y = 0; y < command.Size.y; y++)
                    {
                        buffer.Add(new OccupiedCell(command.GridPosition + new int2(x, y)));
                    }
                }

                ecb.DestroyEntity(commandEntity);
            }

            static int FindTileAtPosition(int2 position, NativeArray<Tile> tiles)
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].GridPosition.Equals(position))
                        return i;
                }
                return -1;
            }
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

        public void OnUpdate(ref SystemState state)
        {
            if (_autoTileQuery.IsEmpty) return;

            var config = SystemAPI.HasSingleton<AutoTileConfig>()
                ? SystemAPI.GetSingleton<AutoTileConfig>()
                : AutoTileConfig.CreateDefault();

            var allTiles = _allTilesQuery.ToComponentDataArray<Tile>(Allocator.TempJob);

            foreach (var (tile, autoTile, render) in SystemAPI.Query<RefRO<Tile>, RefRW<AutoTile>, RefRW<TileRenderData>>())
            {
                var neighbors = CalculateNeighbors(tile.ValueRO.GridPosition, allTiles);
                var newFlags = CalculateAutoTileFlags(neighbors);

                if (autoTile.ValueRO.RuleFlags == newFlags)
                {
                    continue;
                }

                var variantIndex = GetVariantIndexFromFlags(newFlags, config);
                autoTile.ValueRW = autoTile.ValueRO.WithRuleFlags(newFlags).WithVariantIndex(variantIndex);
                render.ValueRW = render.ValueRO.WithSpriteIndex(variantIndex);
            }

            allTiles.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static byte CalculateNeighbors(int2 position, NativeArray<Tile> allTiles)
        {
            byte neighbors = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    var checkPos = position + new int2(x, y);
                    if (HasTileAtPosition(checkPos, allTiles))
                    {
                        var bitIndex = (y + 1) * 3 + (x + 1);
                        if (bitIndex > 4)
                        {
                            bitIndex--;
                        }

                        neighbors |= (byte)(1 << bitIndex);
                    }
                }
            }

            return neighbors;
        }

        private static bool HasTileAtPosition(int2 position, NativeArray<Tile> tiles)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].GridPosition.Equals(position))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte CalculateAutoTileFlags(byte neighbors) => neighbors;

        private static int GetVariantIndexFromFlags(byte flags, AutoTileConfig config)
        {
            return config.RulesetSize == 0 ? 0 : flags % config.RulesetSize;
        }
    }

    /// <summary>
    /// System that processes erase commands and removes tiles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TilemapPainterSystem))]
    public partial struct TilemapEraserSystem : ISystem
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

            // Build a hash map for fast tile lookup by grid position
            using var existingTiles = _tileQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            using var tileEntities = _tileQuery.ToEntityArray(Allocator.TempJob);
            var tileMap = new NativeHashMap<int2, Entity>(existingTiles.Length, Allocator.Temp);
            for (int i = 0; i < existingTiles.Length; i++)
            {
                tileMap[existingTiles[i].GridPosition] = tileEntities[i];
            }

            // Process erase commands
            foreach (var (eraseCommand, commandEntity) in 
                SystemAPI.Query<RefRO<EraseCommand>>().WithEntityAccess())
            {
                var gridPos = eraseCommand.ValueRO.GridPosition;

                // Use hash map to find and destroy tile at this position
                if (tileMap.TryGetValue(gridPos, out var tileEntity))
                {
                    ecb.DestroyEntity(tileEntity);
                }

                // Remove the erase command
                ecb.DestroyEntity(commandEntity);
            }

            tileMap.Dispose();
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
            var config = SystemAPI.HasSingleton<TilemapConfig>() 
                ? SystemAPI.GetSingleton<TilemapConfig>() 
                : TilemapConfig.CreateDefault();
            
            // Update transforms based on grid positions
            foreach (var (tile, transform) in 
                SystemAPI.Query<RefRO<Tile>, RefRW<LocalTransform>>().WithAll<TilemapTag>())
            {
                var worldPos = new float3(tile.ValueRO.GridPosition.x * config.TileSize, tile.ValueRO.GridPosition.y * config.TileSize, 0);
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
    public partial struct TilemapSerializationSystem : ISystem
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
                entityManager.AddComponent<TilemapTag>(entity);
                entityManager.AddComponentData(entity, new Tile(serializedTile.GridPosition, serializedTile.TileID));
                entityManager.AddComponentData(entity, new TileRenderData(
                    Entity.Null,
                    Entity.Null,
                    serializedTile.Color,
                    serializedTile.VariantIndex));

                if (serializedTile.IsAutoTile)
                {
                    entityManager.AddComponentData(entity, new AutoTile(Entity.Null));
                }

                var worldPos = new float3(
                    serializedTile.GridPosition.x * asset.TileSize,
                    serializedTile.GridPosition.y * asset.TileSize,
                    0f);

                entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(worldPos, quaternion.identity, 1f));
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
    public partial struct AnimatedTileSystem : ISystem
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

                ref var animData = ref animatedTile.ValueRO.AnimationData.Value;
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
    public partial struct ChunkManagementSystem : ISystem
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
    public partial struct ProceduralMeshSystem : ISystem
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