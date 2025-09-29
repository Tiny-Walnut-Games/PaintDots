using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PaintDots.ECS.AutoTile
{
    /// <summary>
    /// Integration component for Unity's AutoTile assets from 2D Tilemap Extras
    /// </summary>
    public struct AutoTileAsset : IComponentData
    {
        public BlobAssetReference<AutoTileRuleSet> Rules;
        public Entity SpriteAtlasEntity;
        public int DefaultSpriteIndex;
    }

    /// <summary>
    /// Blob asset containing AutoTile rule data
    /// </summary>
    public struct AutoTileRuleSet
    {
        public BlobArray<AutoTileRule> Rules;
        public BlobArray<int> SpriteIndices;
    }

    /// <summary>
    /// Individual AutoTile rule matching Unity's AutoTile system
    /// </summary>
    public struct AutoTileRule
    {
        public byte NeighborMask; // 8-bit mask for 8 neighbors
        public int OutputSpriteIndex;
        public AutoTileRuleOutput Output;
    }

    /// <summary>
    /// AutoTile rule output types
    /// </summary>
    public enum AutoTileRuleOutput
    {
        Single,
        Random,
        Animation,
        Fixed
    }

    /// <summary>
    /// System for processing AutoTile assets and converting them to ECS data
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AutoTileAssetProcessorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Process AutoTile ScriptableObjects and convert to ECS data
            // This would typically run once during initialization
            Entities
                .WithAll<AutoTileAsset>()
                .WithNone<AutoTileProcessedTag>()
                .ForEach((Entity entity) =>
                {
                    // Process AutoTile asset and create rule data
                    EntityManager.AddComponent<AutoTileProcessedTag>(entity);
                }).WithoutBurst().Run();
        }
    }

    /// <summary>
    /// Tag component to mark processed AutoTile assets
    /// </summary>
    public struct AutoTileProcessedTag : IComponentData { }

    /// <summary>
    /// MonoBehaviour authoring component for AutoTile assets
    /// </summary>
    public class AutoTileAssetAuthoring : MonoBehaviour
    {
        [Header("AutoTile Asset")]
        public ScriptableObject AutoTileScriptableObject; // Reference to Unity's AutoTile asset
        public Sprite[] TileSprites;
        public Material TileMaterial;

        class Baker : Baker<AutoTileAssetAuthoring>
        {
            public override void Bake(AutoTileAssetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                if (authoring.AutoTileScriptableObject != null)
                {
                    // Create BlobAsset for AutoTile rules
                    using var builder = new BlobBuilder(Allocator.Temp);
                    ref var ruleSet = ref builder.ConstructRoot<AutoTileRuleSet>();
                    
                    // Convert Unity AutoTile rules to ECS format
                    // This is where you'd parse the AutoTile ScriptableObject
                    var rulesArray = builder.Allocate(ref ruleSet.Rules, 16); // Example size
                    var spritesArray = builder.Allocate(ref ruleSet.SpriteIndices, authoring.TileSprites?.Length ?? 1);
                    
                    // Fill with example data (in production, parse from actual AutoTile asset)
                    for (int i = 0; i < rulesArray.Length; i++)
                    {
                        rulesArray[i] = new AutoTileRule
                        {
                            NeighborMask = (byte)i,
                            OutputSpriteIndex = i % (authoring.TileSprites?.Length ?? 1),
                            Output = AutoTileRuleOutput.Single
                        };
                    }
                    
                    if (authoring.TileSprites != null)
                    {
                        for (int i = 0; i < spritesArray.Length; i++)
                        {
                            spritesArray[i] = i;
                        }
                    }
                    
                    var blobAsset = builder.CreateBlobAssetReference<AutoTileRuleSet>(Allocator.Persistent);
                    
                    AddComponent(entity, new AutoTileAsset
                    {
                        Rules = blobAsset,
                        DefaultSpriteIndex = 0
                    });
                }
            }
        }
    }

    /// <summary>
    /// Utility class for AutoTile rule evaluation
    /// </summary>
    public static class AutoTileEvaluator
    {
        /// <summary>
        /// Evaluates AutoTile rules and returns the appropriate sprite index
        /// </summary>
        public static int EvaluateRules(byte neighborMask, in AutoTileRuleSet ruleSet)
        {
            // Find matching rule based on neighbor mask
            for (int i = 0; i < ruleSet.Rules.Length; i++)
            {
                if ((ruleSet.Rules[i].NeighborMask & neighborMask) == ruleSet.Rules[i].NeighborMask)
                {
                    return ruleSet.Rules[i].OutputSpriteIndex;
                }
            }
            
            // Return default sprite if no rule matches
            return 0;
        }

        /// <summary>
        /// Calculates neighbor mask for a given position
        /// </summary>
        public static byte CalculateNeighborMask(int2 position, EntityQuery tileQuery)
        {
            byte mask = 0;
            var tiles = tileQuery.ToComponentDataArray<Tile>(Allocator.TempJob);
            
            try
            {
                // Check each of the 8 neighboring positions
                var neighborOffsets = new int2[]
                {
                    new int2(-1, -1), new int2(0, -1), new int2(1, -1),
                    new int2(-1,  0),                   new int2(1,  0),
                    new int2(-1,  1), new int2(0,  1), new int2(1,  1)
                };
                
                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    var checkPos = position + neighborOffsets[i];
                    
                    // Check if there's a tile at this position
                    for (int j = 0; j < tiles.Length; j++)
                    {
                        if (tiles[j].GridPosition.Equals(checkPos))
                        {
                            mask |= (byte)(1 << i);
                            break;
                        }
                    }
                }
            }
            finally
            {
                tiles.Dispose();
            }
            
            return mask;
        }
    }
}