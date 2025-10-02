using Unity.Collections;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PaintDots.Runtime.AutoTileIntegration
{
    /// <summary>
    /// Integration component for Unity's AutoTile assets from 2D Tilemap Extras
    /// </summary>
    public readonly struct AutoTileAsset : IComponentData
    {
        public readonly BlobAssetReference<AutoTileRuleSet> Rules;
        public readonly Entity SpriteAtlasEntity;
        public readonly int DefaultSpriteIndex;

        public AutoTileAsset(BlobAssetReference<AutoTileRuleSet> rules, Entity spriteAtlasEntity, int defaultSpriteIndex = 0)
        {
            Rules = rules;
            SpriteAtlasEntity = spriteAtlasEntity;
            DefaultSpriteIndex = defaultSpriteIndex;
        }
    }

    /// <summary>
    /// Blob asset containing AutoTile rule data
    /// </summary>
    public readonly struct AutoTileRuleSet
    {
        public readonly BlobArray<AutoTileRule> Rules;
        public readonly BlobArray<int> SpriteIndices;
    }

    /// <summary>
    /// Individual AutoTile rule matching Unity's AutoTile system
    /// </summary>
    public readonly struct AutoTileRule
    {
        public readonly byte NeighborMask; // 8-bit mask for 8 neighbors
        public readonly int OutputSpriteIndex;
        public readonly AutoTileRuleOutput Output;

        public AutoTileRule(byte neighborMask, int outputSpriteIndex, AutoTileRuleOutput output)
        {
            NeighborMask = neighborMask;
            OutputSpriteIndex = outputSpriteIndex;
            Output = output;
        }
    }

    /// <summary>
    /// AutoTile rule output types
    /// </summary>
    public enum AutoTileRuleOutput : byte
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
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(EntityManager.WorldUnmanaged);
            
            // Process AutoTile ScriptableObjects and convert to ECS data
            // This would typically run once during initialization
            Entities
                .WithAll<AutoTileAsset>()
                .WithNone<AutoTileProcessedTag>()
                .ForEach((Entity entity) =>
                {
                    // Process AutoTile asset and create rule data
                    ecb.AddComponent<AutoTileProcessedTag>(entity);
                }).WithoutBurst().Run();
        }
    }

    /// <summary>
    /// Tag component to mark processed AutoTile assets
    /// </summary>
    public readonly struct AutoTileProcessedTag : IComponentData { }

    /// <summary>
    /// MonoBehaviour authoring component for AutoTile assets
    /// </summary>
    public class AutoTileAssetAuthoring : MonoBehaviour
    {
        [Header("AutoTile Asset")]
        public ScriptableObject AutoTileScriptableObject; // Reference to Unity's AutoTile asset
        public Sprite[] TileSprites = System.Array.Empty<Sprite>();
        public Material TileMaterial;

        private sealed class Baker : Baker<AutoTileAssetAuthoring>
        {
            public override void Bake(AutoTileAssetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                if (authoring.AutoTileScriptableObject != default)
                {
                    // Create BlobAsset for AutoTile rules
                    using var builder = new BlobBuilder(Allocator.Temp);
                    ref var ruleSet = ref builder.ConstructRoot<AutoTileRuleSet>();

                    // Convert Unity AutoTile rules to ECS format
                    const int ruleCount = 16;

                    unsafe
                    {
                        var rootPtr = UnsafeUtility.AddressOf(ref ruleSet);
                        var rulesOffset = UnsafeUtility.GetFieldOffset(typeof(AutoTileRuleSet).GetField(nameof(AutoTileRuleSet.Rules))!);
                        var spritesOffset = UnsafeUtility.GetFieldOffset(typeof(AutoTileRuleSet).GetField(nameof(AutoTileRuleSet.SpriteIndices))!);

                        var rulesArray = builder.Allocate(
                            ref UnsafeUtility.AsRef<BlobArray<AutoTileRule>>((byte*)rootPtr + rulesOffset),
                            ruleCount);

                        var spritesArray = builder.Allocate(
                            ref UnsafeUtility.AsRef<BlobArray<int>>((byte*)rootPtr + spritesOffset),
                            authoring.TileSprites.Length);

                        // ⁉ Fill with data (in production, parse from actual AutoTile asset)
                        for (int i = 0; i < rulesArray.Length; i++)
                        {
                            rulesArray[i] = new AutoTileRule((byte)i, i % authoring.TileSprites.Length, AutoTileRuleOutput.Single);
                        }

                        for (int i = 0; i < spritesArray.Length; i++)
                        {
                            spritesArray[i] = i;
                        }
                    }

                    // ⁉ Fill with data (in production, parse from actual AutoTile asset)
                    var blobAsset = builder.CreateBlobAssetReference<AutoTileRuleSet>(Allocator.Persistent);
                    
                    AddComponent(entity, new AutoTileAsset(blobAsset, Entity.Null));
                }
            }
        }
    }

    /// <summary>
    /// Buffer element for tile positions during neighbor calculations
    /// </summary>
    public readonly struct TilePositionElement : IBufferElementData
    {
        public readonly int2 Position;
        
        public TilePositionElement(int2 position)
        {
            Position = position;
        }

        public static implicit operator int2(TilePositionElement element)
        {
            return element.Position;
        }

        public static implicit operator TilePositionElement(int2 position)
        {
            return new TilePositionElement(position);
        }
    }

    /// <summary>
    /// Utility class for AutoTile rule evaluation
    /// </summary>
    public static class AutoTileEvaluator
    {
        private static readonly int2[] NeighborOffsets =
        {
            new int2(-1, -1), new int2(0, -1), new int2(1, -1),
            new int2(-1,  0),                    new int2(1,  0),
            new int2(-1,  1), new int2(0,  1), new int2(1,  1)
        };

        /// <summary>
        /// Evaluates AutoTile rules and returns the appropriate sprite index
        /// </summary>
        public static int EvaluateRules(byte neighborMask, ref AutoTileRuleSet ruleSet)
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
        /// Calculates neighbor mask for a given position using buffer
        /// </summary>
        public static byte CalculateNeighborMask(int2 position, DynamicBuffer<TilePositionElement> tiles)
        {
            byte mask = 0;
            
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                var checkPos = position + NeighborOffsets[i];
                
                // Check if there's a tile at this position
                for (int j = 0; j < tiles.Length; j++)
                {
                    if (tiles[j].Position.Equals(checkPos))
                    {
                        mask |= (byte)(1 << i);
                        break;
                    }
                }
            }
            
            return mask;
        }
    }
}