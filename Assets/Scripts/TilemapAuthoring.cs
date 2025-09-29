using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PaintDots.ECS.Authoring
{
    /// <summary>
    /// MonoBehaviour authoring component for creating tilemap entities in the editor
    /// This is only used for authoring and will be converted to pure ECS at runtime
    /// </summary>
    public class TilemapAuthoring : MonoBehaviour
    {
        [Header("Tilemap Settings")]
        public int2 MapSize = new int2(100, 100);
        public Material TileMaterial;
        public Mesh TileMesh;
        
        [Header("Tile Palette")]
        public GameObject[] TilePrefabs;
        
        class Baker : Baker<TilemapAuthoring>
        {
            public override void Bake(TilemapAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Add tilemap tag to the main entity
                AddComponent<TilemapTag>(entity);
                
                // Bake tile prefabs into entities
                if (authoring.TilePrefabs != null && authoring.TilePrefabs.Length > 0)
                {
                    var tileEntities = new NativeArray<Entity>(authoring.TilePrefabs.Length, Allocator.Temp);
                    
                    for (int i = 0; i < authoring.TilePrefabs.Length; i++)
                    {
                        if (authoring.TilePrefabs[i] != null)
                        {
                            tileEntities[i] = GetEntity(authoring.TilePrefabs[i], TransformUsageFlags.Dynamic);
                        }
                    }
                    
                    // Store tile palette data
                    // In a full implementation, this would create a BlobAsset for the palette
                    
                    tileEntities.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Authoring component for individual tiles
    /// </summary>
    public class TileAuthoring : MonoBehaviour
    {
        [Header("Tile Data")]
        public int TileID;
        public int2 GridPosition;
        public bool UseAutoTile = false;
        
        class Baker : Baker<TileAuthoring>
        {
            public override void Bake(TileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new Tile
                {
                    GridPosition = authoring.GridPosition,
                    TileID = authoring.TileID
                });
                
                AddComponent<TilemapTag>(entity);
                
                if (authoring.UseAutoTile)
                {
                    AddComponent(entity, new AutoTile
                    {
                        AutoTileAssetEntity = Entity.Null,
                        RuleFlags = 0,
                        VariantIndex = 0
                    });
                }
                
                AddComponent(entity, new TileRenderData
                {
                    Color = new float4(1, 1, 1, 1),
                    SpriteIndex = authoring.TileID
                });
            }
        }
    }
}