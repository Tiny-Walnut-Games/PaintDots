using System;
using System.Collections;
using System.Collections.Generic;
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
            private static readonly int RuleSetRulesOffset = UnsafeUtility.GetFieldOffset(typeof(AutoTileRuleSet).GetField(nameof(AutoTileRuleSet.Rules))!);
            private static readonly int RuleSetSpritesOffset = UnsafeUtility.GetFieldOffset(typeof(AutoTileRuleSet).GetField(nameof(AutoTileRuleSet.SpriteIndices))!);

            public override void Bake(AutoTileAssetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                if (authoring.AutoTileScriptableObject == default)
                {
                    return;
                }

                var assetType = authoring.AutoTileScriptableObject.GetType();
                if (!IsRuleTileType(assetType))
                {
                    Debug.LogError($"AutoTileAssetAuthoring requires a RuleTile-derived asset, but received {assetType.Name}", authoring);
                    return;
                }

                var convertedRules = new List<AutoTileRule>();
                var spriteLookup = new Dictionary<Sprite, int>(authoring.TileSprites?.Length ?? 0);
                var spriteInstanceIds = new List<int>(authoring.TileSprites?.Length ?? 0);

                int RegisterSprite(Sprite sprite)
                {
                    if (sprite == null)
                    {
                        return -1;
                    }

                    if (spriteLookup.TryGetValue(sprite, out var existingIndex))
                    {
                        return existingIndex;
                    }

                    var newIndex = spriteInstanceIds.Count;
                    spriteLookup.Add(sprite, newIndex);
                    spriteInstanceIds.Add(sprite.GetInstanceID());
                    return newIndex;
                }

                if (authoring.TileSprites != null)
                {
                    for (int i = 0; i < authoring.TileSprites.Length; i++)
                    {
                        RegisterSprite(authoring.TileSprites[i]);
                    }
                }

                var binding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var defaultSprite = assetType.GetField("m_DefaultSprite", binding)?.GetValue(authoring.AutoTileScriptableObject) as Sprite;
                var defaultSpriteIndex = RegisterSprite(defaultSprite);

                var tilingRulesObject = assetType.GetField("m_TilingRules", binding)?.GetValue(authoring.AutoTileScriptableObject);
                if (tilingRulesObject is IEnumerable tilingRules)
                {
                    foreach (var tilingRule in tilingRules)
                    {
                        if (tilingRule == null)
                        {
                            continue;
                        }

                        var ruleType = tilingRule.GetType();
                        IDictionary neighbors = null;
                        if (ruleType.GetField("m_Neighbors", binding)?.GetValue(tilingRule) is IDictionary neighborDictionary)
                        {
                            neighbors = neighborDictionary;
                        }

                        var neighborMask = BuildNeighborMask(neighbors);
                        var outputValue = ruleType.GetField("m_Output", binding)?.GetValue(tilingRule);
                        var outputType = ConvertOutput(outputValue);

                        var spriteIndex = defaultSpriteIndex;
                        if (ruleType.GetField("m_Sprites", binding)?.GetValue(tilingRule) is Array spriteArray)
                        {
                            for (int i = 0; i < spriteArray.Length; i++)
                            {
                                if (spriteArray.GetValue(i) is Sprite sprite && sprite != null)
                                {
                                    var resolved = RegisterSprite(sprite);
                                    if (i == 0 && resolved >= 0)
                                    {
                                        spriteIndex = resolved;
                                    }
                                }
                            }
                        }

                        if (spriteIndex < 0)
                        {
                            spriteIndex = spriteInstanceIds.Count > 0 ? 0 : -1;
                        }

                        convertedRules.Add(new AutoTileRule(neighborMask, spriteIndex < 0 ? 0 : spriteIndex, outputType));
                    }
                }

                if (convertedRules.Count == 0)
                {
                    convertedRules.Add(new AutoTileRule(0, defaultSpriteIndex >= 0 ? defaultSpriteIndex : 0, AutoTileRuleOutput.Single));
                }

                if (spriteInstanceIds.Count == 0)
                {
                    spriteInstanceIds.Add(0);
                    if (defaultSpriteIndex < 0)
                    {
                        defaultSpriteIndex = 0;
                    }
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var ruleSet = ref builder.ConstructRoot<AutoTileRuleSet>();

                unsafe
                {
                    var rootPtr = UnsafeUtility.AddressOf(ref ruleSet);
                    var rulesArray = builder.Allocate(
                        ref UnsafeUtility.AsRef<BlobArray<AutoTileRule>>((byte*)rootPtr + RuleSetRulesOffset),
                        convertedRules.Count);

                    for (int i = 0; i < convertedRules.Count; i++)
                    {
                        rulesArray[i] = convertedRules[i];
                    }

                    var spritesArray = builder.Allocate(
                        ref UnsafeUtility.AsRef<BlobArray<int>>((byte*)rootPtr + RuleSetSpritesOffset),
                        spriteInstanceIds.Count);

                    for (int i = 0; i < spriteInstanceIds.Count; i++)
                    {
                        spritesArray[i] = spriteInstanceIds[i];
                    }
                }

                var blobAsset = builder.CreateBlobAssetReference<AutoTileRuleSet>(Allocator.Persistent);
                var resolvedDefaultIndex = defaultSpriteIndex >= 0 ? defaultSpriteIndex : 0;

                AddComponent(entity, new AutoTileAsset(blobAsset, Entity.Null, resolvedDefaultIndex));
            }

            private static bool IsRuleTileType(Type type)
            {
                if (type == null)
                {
                    return false;
                }

                while (type != null)
                {
                    if (type.FullName == "UnityEngine.Tilemaps.RuleTile")
                    {
                        return true;
                    }

                    type = type.BaseType;
                }

                return false;
            }

            private static byte BuildNeighborMask(IDictionary neighbors)
            {
                if (neighbors == null)
                {
                    return 0;
                }

                byte mask = 0;

                foreach (DictionaryEntry entry in neighbors)
                {
                    if (entry.Key is not Vector3Int offset || offset == Vector3Int.zero)
                    {
                        continue;
                    }

                    var bitIndex = GetNeighborBit(offset.x, offset.y);
                    if (bitIndex < 0)
                    {
                        continue;
                    }

                    var neighborName = entry.Value != null
                        ? Enum.GetName(entry.Value.GetType(), entry.Value)
                        : null;

                    if (neighborName == "This")
                    {
                        mask |= (byte)(1 << bitIndex);
                    }
                }

                return mask;
            }

            private static AutoTileRuleOutput ConvertOutput(object outputValue)
            {
                if (outputValue == null)
                {
                    return AutoTileRuleOutput.Single;
                }

                var name = Enum.GetName(outputValue.GetType(), outputValue);
                return name switch
                {
                    "Animation" => AutoTileRuleOutput.Animation,
                    "Random" => AutoTileRuleOutput.Random,
                    "Fixed" => AutoTileRuleOutput.Fixed,
                    _ => AutoTileRuleOutput.Single
                };
            }

            private static int GetNeighborBit(int x, int y)
            {
                var index = (y + 1) * 3 + (x + 1);
                if (index > 4)
                {
                    index--;
                }

                return index is < 0 or > 7 ? -1 : index;
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