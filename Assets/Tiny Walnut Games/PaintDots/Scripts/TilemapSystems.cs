using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Burst.Intrinsics;
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
    // Story: Listens for paint commands and spawns or updates tiles accordingly using ECB on the main thread.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TilemapPainterSystem : ISystem
    {
        private EntityQuery _paintCommandQuery;
        private EntityQuery _existingTileQuery;
        private EntityQuery _configQuery;
        private EntityQuery _structureQuery;

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
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            // Get configuration
            var config = SystemAPI.HasSingleton<TilemapConfig>() 
                ? SystemAPI.GetSingleton<TilemapConfig>() 
                : TilemapConfig.CreateDefault();
            
            // Process paint commands on the main thread to avoid passing NativeArray fields into job structs
            using var existingTiles = _existingTileQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            using var structureEntities = _structureQuery.ToEntityArray(Allocator.TempJob);
            var footprintLookup = SystemAPI.GetComponentLookup<Footprint>(true);

            // Create managed arrays for footprint checks to avoid passing NativeArray into helper API
            int2[] existingTilePositions = new int2[existingTiles.Length];
            for (int i = 0; i < existingTiles.Length; i++) existingTilePositions[i] = existingTiles[i].GridPosition;
            var structureEntityArray = new Entity[structureEntities.Length];
            for (int i = 0; i < structureEntities.Length; i++) structureEntityArray[i] = structureEntities[i];

            foreach (var (paintRef, commandEntity) in SystemAPI.Query<RefRO<PaintCommand>>().WithEntityAccess())
            {
                var command = paintRef.ValueRO;
                if (command.IsMultiTile)
                {
                    ProcessMultiTileCommand(command, commandEntity, ecb, config, existingTilePositions, structureEntityArray, footprintLookup);
                }
                else
                {
                    ProcessSingleTileCommand(command, commandEntity, ecb, config, existingTilePositions);
                }
            }
        }

        public void OnDestroy(ref SystemState state) { }

        // Main-thread helpers that mirror the job logic but avoid NativeArray fields in job structs
        private void ProcessSingleTileCommand(PaintCommand command, Entity commandEntity, EntityCommandBuffer ecb, TilemapConfig config, int2[] existingTilePositions)
        {
            var existingTileIndex = FindTileAtPosition(command.GridPosition, existingTilePositions);

            if (existingTileIndex >= 0)
            {
                // Update existing tile - skipping destroy of old entity in this simplified pass
            }

            // Create new tile entity
            var newTile = ecb.CreateEntity();
            ecb.AddComponent(newTile, new Tile(command.GridPosition, command.TileID));
            ecb.AddComponent<TilemapTag>(newTile);
            ecb.AddComponent(newTile, LocalTransform.FromPosition(
                new float3(command.GridPosition.x * config.TileSize, command.GridPosition.y * config.TileSize, 0)));

            // Add rendering components
            ecb.AddComponent(newTile, new TileRenderData(
                EntityConstants.InvalidEntity,
                EntityConstants.InvalidEntity,
                config.DefaultTileColor,
                command.TileID
            ));

            // Remove the paint command
            ecb.DestroyEntity(commandEntity);
        }

        private void ProcessMultiTileCommand(PaintCommand command, Entity commandEntity, EntityCommandBuffer ecb, TilemapConfig config, int2[] existingTilePositions, Entity[] structureEntities, ComponentLookup<Footprint> footprintLookup)
        {
            // Check if the footprint area is free
            if (!GridOccupancyManager.IsFootprintFree(command.GridPosition, command.Size, existingTilePositions, structureEntities, footprintLookup))
            {
                // Area is occupied, remove command without creating entity
                ecb.DestroyEntity(commandEntity);
                return;
            }

            // Create structure entity with footprint
            var structureEntity = ecb.CreateEntity();
            ecb.AddComponent(structureEntity, new Footprint(command.GridPosition, command.Size));
            ecb.AddComponent<TilemapTag>(structureEntity);

            // Position at the origin
            ecb.AddComponent(structureEntity, LocalTransform.FromPosition(
                new float3(command.GridPosition.x * config.TileSize, command.GridPosition.y * config.TileSize, 0)));

            // Add rendering components
            ecb.AddComponent(structureEntity, new TileRenderData(
                EntityConstants.InvalidEntity,
                EntityConstants.InvalidEntity,
                config.DefaultTileColor,
                command.TileID
            ));

            // Add buffer for occupied cells and fill it
            var buffer = ecb.AddBuffer<OccupiedCell>(structureEntity);
            for (int x = 0; x < command.Size.x; x++)
            {
                for (int y = 0; y < command.Size.y; y++)
                {
                    buffer.Add(new OccupiedCell(command.GridPosition + new int2(x, y)));
                }
            }

            // Remove the paint command
            ecb.DestroyEntity(commandEntity);
        }

        private int FindTileAtPosition(int2 position, int2[] tilePositions)
        {
            // Treat missing or empty arrays as no match
            if (!(tilePositions?.Length > 0)) return -1;
            for (int i = 0; i < tilePositions.Length; i++)
            {
                if (tilePositions[i].Equals(position))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// System for processing AutoTile rules and updating tile variants
    /// </summary>
    // Story: Applies autotile rules to nearby tiles and updates variant indices for rendering.
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
                .WithAll<Tile, AutoTileComponent, TileRenderData>()
                .Build();
                
            _allTilesQuery = SystemAPI.QueryBuilder()
                .WithAll<Tile>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_autoTileQuery.IsEmpty) return;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var config = SystemAPI.HasSingleton<AutoTileConfig>()
                ? SystemAPI.GetSingleton<AutoTileConfig>()
                : AutoTileConfig.CreateDefault();

            using var allTiles = _allTilesQuery.ToComponentDataArray<Tile>(Allocator.TempJob);

            // Build managed int2[] array to avoid NativeArray usage in helper APIs
            int2[] allTilePositions = new int2[allTiles.Length];
            for (int i = 0; i < allTiles.Length; i++) allTilePositions[i] = allTiles[i].GridPosition;

            // Iterate on main thread and use ECB to apply changes
            foreach (var (tileRef, autoRef, renderRef, entity) in SystemAPI.Query<RefRO<Tile>, RefRO<AutoTileComponent>, RefRO<TileRenderData>>().WithEntityAccess())
            {
                var tile = tileRef.ValueRO;
                var autoTile = autoRef.ValueRO;
                var render = renderRef.ValueRO;

                var neighbors = CalculateNeighbors(tile.GridPosition, allTilePositions);
                var newFlags = CalculateAutoTileFlags(neighbors);

                if (autoTile.RuleFlags != newFlags)
                {
                    var variantIndex = GetVariantIndexFromFlags(newFlags, config);
                    var newAutoTile = autoTile.WithRuleFlags(newFlags).WithVariantIndex(variantIndex);
                    var newRenderData = render.WithSpriteIndex(variantIndex);

                    ecb.SetComponent(entity, newAutoTile);
                    ecb.SetComponent(entity, newRenderData);
                }
            }
        }

        public void OnDestroy(ref SystemState state) { }

        // Helper methods moved to system to avoid job structs with NativeArray fields
        private static byte CalculateNeighbors(int2 position, int2[] allTilePositions)
        {
            byte neighbors = 0;

            // Check 8 surrounding positions (3x3 grid minus center)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip center

                    var checkPos = position + new int2(x, y);
                    if (HasTileAtPosition(checkPos, allTilePositions))
                    {
                        var bitIndex = (y + 1) * 3 + (x + 1);
                        if (bitIndex > 4) bitIndex--; // Skip center position in bit calculation
                        neighbors |= (byte)(1 << bitIndex);
                    }
                }
            }
            return neighbors;
        }

        private static bool HasTileAtPosition(int2 position, int2[] tilePositions)
        {
            if (!(tilePositions?.Length > 0)) return false;
            for (int i = 0; i < tilePositions.Length; i++)
            {
                if (tilePositions[i].Equals(position))
                    return true;
            }
            return false;
        }

        private static byte CalculateAutoTileFlags(byte neighbors)
        {
            return neighbors;
        }

        private static int GetVariantIndexFromFlags(byte flags, AutoTileConfig config)
        {
            return flags % config.RulesetSize;
        }
    }

    /// <summary>
    /// System that processes erase commands and removes tiles
    /// </summary>
    // Story: Handles eraser paint commands and removes tiles or structures when requested.
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
            try
            {
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
            }
            finally
            {
                tileMap.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// System for rendering tile entities using Unity's Entities Graphics
    /// </summary>
    // Story: Builds and updates rendering components/meshes for tiles each frame.
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
                SystemAPI.Query<Tile, RefRW<LocalTransform>>().WithAll<TilemapTag>())
            {
                var worldPos = new float3(tile.GridPosition.x * config.TileSize, tile.GridPosition.y * config.TileSize, 0);
                transform.ValueRW.Position = worldPos;
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
    /// System for serializing and deserializing tilemap state
    /// </summary>
    // Story: Serializes and deserializes tilemap state for save/load operations.
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
                
                bool isAutoTile = entityManager.HasComponent<AutoTileComponent>(tileEntities[i]);
                
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
                entityManager.AddComponentData(entity, new Tile(serializedTile.GridPosition, serializedTile.TileID));
                entityManager.AddComponent<TilemapTag>(entity);
                
                // Add rendering data
                entityManager.AddComponentData(entity, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    serializedTile.Color,
                    serializedTile.VariantIndex
                ));

                // Add AutoTile if needed
                    if (serializedTile.IsAutoTile)
                    {
                        entityManager.AddComponentData(entity, new AutoTileComponent(EntityConstants.InvalidEntity));
                    }

                // Set world position
                var worldPos = new float3(
                    serializedTile.GridPosition.x * asset.TileSize,
                    serializedTile.GridPosition.y * asset.TileSize,
                    0
                );
                entityManager.AddComponentData(entity, new LocalTransform
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
    // Story: Advances animated tile frames and updates render data based on BlobAsset animation data.
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

                // Use a single-level ref to the blob data (allowed by Burst for blittable blob access)
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

    // (Removed duplicated/malformed block inserted by automated edits.)
}