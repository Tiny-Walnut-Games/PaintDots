using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using Unity.Mathematics;
using UnityEngine;
using PaintDots.ECS.Config;

namespace PaintDots.ECS.Authoring
{
    /// <summary>
    /// MonoBehaviour authoring component for creating tilemap entities in the editor
    /// This is only used for authoring and will be converted to pure ECS at runtime
    /// </summary>
    public sealed class TilemapAuthoring : MonoBehaviour
    {
        [Header("Tilemap Settings")]
        public int2 MapSize = new(100, 100);
        public Material TileMaterial;
        public Mesh TileMesh;
        
        [Header("Tile Palette")]
        public GameObject[] TilePrefabs = System.Array.Empty<GameObject>();

        [Header("Configuration")]
        public float TileSize = 1.0f;
        public int MaxVariants = 16;
        public Color DefaultTileColor = Color.white;
        
        private sealed class Baker : Baker<TilemapAuthoring>
        {
            public override void Bake(TilemapAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Add tilemap tag to the main entity
                AddComponent<TilemapTag>(entity);
                
                // Add configuration components
                AddComponent(entity, new TilemapConfig(
                    authoring.TileSize, 
                    authoring.MaxVariants,
                    new float4(authoring.DefaultTileColor.r, authoring.DefaultTileColor.g, authoring.DefaultTileColor.b, authoring.DefaultTileColor.a),
                    0 // default sprite index
                ));
                
                AddComponent(entity, new AutoTileConfig());
                AddComponent(entity, new RenderConfig());
                
                // Bake tile prefabs into entities
                if (authoring.TilePrefabs.Length > 0)
                {
                    // Use a managed array here during bake (authoring time) to avoid NativeArray usage warnings
                    var tileEntities = new Entity[authoring.TilePrefabs.Length];
                    for (int i = 0; i < authoring.TilePrefabs.Length; i++)
                    {
                        if (authoring.TilePrefabs[i] != default)
                        {
                            tileEntities[i] = GetEntity(authoring.TilePrefabs[i], TransformUsageFlags.Dynamic);
                        }
                    }

                    // Store tile palette data
                    // In a full implementation, this would create a BlobAsset for the palette
                }
            }
        }
    }

    /// <summary>
    /// Authoring component for individual tiles
    /// </summary>
    public sealed class TileAuthoring : MonoBehaviour
    {
        [Header("Tile Data")]
        public int TileID;
        public int2 GridPosition;
        public bool UseAutoTile = false;
        
        private sealed class Baker : Baker<TileAuthoring>
        {
            public override void Bake(TileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new Tile(authoring.GridPosition, authoring.TileID));
                AddComponent<TilemapTag>(entity);
                
                if (authoring.UseAutoTile)
                {
                    AddComponent(entity, new AutoTileComponent(EntityConstants.InvalidEntity));
                }
                
                // Get config from tilemap entity or use defaults
                var config = new TilemapConfig();
                AddComponent(entity, new TileRenderData(
                    EntityConstants.InvalidEntity,
                    EntityConstants.InvalidEntity,
                    config.DefaultTileColor,
                    authoring.TileID
                ));
            }
        }
    }
}