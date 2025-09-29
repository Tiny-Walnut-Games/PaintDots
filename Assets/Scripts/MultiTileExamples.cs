using Unity.Entities;
using Unity.Mathematics;
using PaintDots.ECS;
using PaintDots.ECS.Utilities;

namespace PaintDots.ECS.Examples
{
    /// <summary>
    /// Example usage patterns for multi-tile entity placement
    /// </summary>
    public static class MultiTileExamples
    {
        /// <summary>
        /// Example: Create a house structure spanning 3x2 tiles
        /// </summary>
        public static Entity CreateHousePaintCommand(EntityManager entityManager, int2 position)
        {
            const int houseID = 100;
            var command = PaintCommand.MultiTile(position, houseID, new int2(3, 2));
            
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, command);
            
            return entity;
        }

        /// <summary>
        /// Example: Create a large tree structure spanning 2x2 tiles  
        /// </summary>
        public static Entity CreateTreePaintCommand(EntityManager entityManager, int2 position)
        {
            const int treeID = 101;
            var command = PaintCommand.MultiTile(position, treeID, new int2(2, 2));
            
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, command);
            
            return entity;
        }

        /// <summary>
        /// Example: Create a bridge structure spanning 5x1 tiles
        /// </summary>
        public static Entity CreateBridgePaintCommand(EntityManager entityManager, int2 position)
        {
            const int bridgeID = 102;
            var command = PaintCommand.MultiTile(position, bridgeID, new int2(5, 1));
            
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, command);
            
            return entity;
        }

        /// <summary>
        /// Example: Batch creation of structures for level generation
        /// </summary>
        public static void CreateExampleLevel(EntityManager entityManager)
        {
            // Place houses in a residential area
            CreateHousePaintCommand(entityManager, new int2(10, 10));
            CreateHousePaintCommand(entityManager, new int2(14, 10));
            CreateHousePaintCommand(entityManager, new int2(18, 10));
            
            // Add trees between houses
            CreateTreePaintCommand(entityManager, new int2(12, 8));
            CreateTreePaintCommand(entityManager, new int2(16, 8));
            
            // Connect with a bridge
            CreateBridgePaintCommand(entityManager, new int2(10, 6));
            
            // Add single tiles for paths (mixing single and multi-tile placement)
            var pathTile = PaintCommand.SingleTile(new int2(9, 9), 50);
            var pathEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(pathEntity, pathTile);
        }
    }

    /// <summary>
    /// Example system that demonstrates procedural multi-tile placement
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ExampleLevelGeneratorSystem : SystemBase
    {
        private bool _levelGenerated = false;

        protected override void OnUpdate()
        {
            if (_levelGenerated) return;
            
            // Generate example level once on startup
            MultiTileExamples.CreateExampleLevel(EntityManager);
            _levelGenerated = true;
        }
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