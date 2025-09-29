using Unity.Entities;
using Unity.Mathematics;
using PaintDots.ECS;
using PaintDots.ECS.Config;
using PaintDots.ECS.Utilities;

namespace PaintDots.ECS.Examples
{
    /// <summary>
    /// Example usage patterns for multi-tile entity placement
    /// </summary>
    public static sealed class MultiTileExamples
    {
        /// <summary>
        /// Example: Create a house structure spanning 3x2 tiles using ECB
        /// </summary>
        public static void CreateHousePaintCommand(EntityCommandBuffer.ParallelWriter ecb, int unfilteredChunkIndex, int2 position, StructureConfig config)
        {
            var command = PaintCommand.MultiTile(position, config.HouseID, new int2(3, 2));
            
            var entity = ecb.CreateEntity(unfilteredChunkIndex);
            ecb.AddComponent(unfilteredChunkIndex, entity, command);
        }

        /// <summary>
        /// Example: Create a large tree structure spanning 2x2 tiles using ECB
        /// </summary>
        public static void CreateTreePaintCommand(EntityCommandBuffer.ParallelWriter ecb, int unfilteredChunkIndex, int2 position, StructureConfig config)
        {
            var command = PaintCommand.MultiTile(position, config.TreeID, new int2(2, 2));
            
            var entity = ecb.CreateEntity(unfilteredChunkIndex);
            ecb.AddComponent(unfilteredChunkIndex, entity, command);
        }

        /// <summary>
        /// Example: Create a bridge structure spanning 5x1 tiles using ECB
        /// </summary>
        public static void CreateBridgePaintCommand(EntityCommandBuffer.ParallelWriter ecb, int unfilteredChunkIndex, int2 position, StructureConfig config)
        {
            var command = PaintCommand.MultiTile(position, config.BridgeID, new int2(5, 1));
            
            var entity = ecb.CreateEntity(unfilteredChunkIndex);
            ecb.AddComponent(unfilteredChunkIndex, entity, command);
        }

        /// <summary>
        /// Example: Create a path tile using ECB
        /// </summary>
        public static void CreatePathPaintCommand(EntityCommandBuffer.ParallelWriter ecb, int unfilteredChunkIndex, int2 position, StructureConfig config)
        {
            var command = PaintCommand.SingleTile(position, config.PathID);
            
            var entity = ecb.CreateEntity(unfilteredChunkIndex);
            ecb.AddComponent(unfilteredChunkIndex, entity, command);
        }

        /// <summary>
        /// Example: Batch creation of structures for level generation using ECB
        /// </summary>
        public static void CreateExampleLevel(EntityCommandBuffer.ParallelWriter ecb, int unfilteredChunkIndex, StructureConfig config)
        {
            // Place houses in a residential area
            CreateHousePaintCommand(ecb, unfilteredChunkIndex, new int2(10, 10), config);
            CreateHousePaintCommand(ecb, unfilteredChunkIndex, new int2(14, 10), config);
            CreateHousePaintCommand(ecb, unfilteredChunkIndex, new int2(18, 10), config);
            
            // Add trees between houses
            CreateTreePaintCommand(ecb, unfilteredChunkIndex, new int2(12, 8), config);
            CreateTreePaintCommand(ecb, unfilteredChunkIndex, new int2(16, 8), config);
            
            // Connect with a bridge
            CreateBridgePaintCommand(ecb, unfilteredChunkIndex, new int2(10, 6), config);
            
            // Add single tiles for paths (mixing single and multi-tile placement)
            CreatePathPaintCommand(ecb, unfilteredChunkIndex, new int2(9, 9), config);
        }
    }

    /// <summary>
    /// Example system that demonstrates procedural multi-tile placement using ECB
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public sealed partial struct ExampleLevelGeneratorSystem : ISystem
    {
        private bool _levelGenerated;

        public void OnCreate(ref SystemState state)
        {
            _levelGenerated = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_levelGenerated) return;
            
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
                
            var config = SystemAPI.HasSingleton<StructureConfig>() 
                ? SystemAPI.GetSingleton<StructureConfig>() 
                : StructureConfig.CreateDefault();
            
            // Generate example level once on startup using ECB
            MultiTileExamples.CreateExampleLevel(ecb.AsParallelWriter(), 0, config);
            _levelGenerated = true;
        }

        public void OnDestroy(ref SystemState state) { }
    }

    /// <summary>
    /// Example authoring component for multi-tile prefab definition
    /// </summary>
    public class MultiTileStructureAuthoring : UnityEngine.MonoBehaviour
    {
        [UnityEngine.SerializeField] private int2 footprintSize = new int2(2, 2);
        [UnityEngine.SerializeField] private int tileID = 100;
        [UnityEngine.SerializeField] private bool useAsTemplate = true;

        public class Baker : Baker<MultiTileStructureAuthoring>
        {
            public override void Bake(MultiTileStructureAuthoring authoring)
            {
                if (!authoring.useAsTemplate) return;

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Add footprint component for baked prefab
                AddComponent(entity, new Footprint(int2.zero, authoring.footprintSize));
                
                // Add structure visual data
                AddComponent(entity, new StructureVisual(entity, authoring.footprintSize));
                
                // Add tilemap tag
                AddComponent<TilemapTag>(entity);
                
                // Add buffer for occupied cells (empty during baking)
                AddBuffer<OccupiedCell>(entity);
            }
        }
    }
}