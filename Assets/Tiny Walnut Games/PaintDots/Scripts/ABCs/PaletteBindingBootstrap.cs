using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using UnityEngine;

namespace PaintDots.ECS.ABCs
{
    public struct PaletteBindingComponent : IComponentData
    {
        public BlobAssetReference<PaletteBindingBlob> BlobRef;
    }

    public struct PaletteBindingBlob
    {
        public BlobArray<FamilyBlob> Families;
        public int NumPhases;
    }

    public struct FamilyBlob
    {
        public int FamilyId;
        public BlobArray<int> PhaseSpriteIndices;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PaletteBindingBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Require nothing; we'll create if authoring GameObject exists
        }

        public void OnDestroy(ref SystemState state)
        {
            // cleanup
            if (state.EntityManager.HasComponent<PaletteBindingComponent>(SystemAPI.GetSingletonEntity<PaletteBindingComponent>()))
            {
                // nothing special
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // If a PaletteBindingComponent singleton exists, nothing to do
            if (SystemAPI.HasSingleton<PaletteBindingComponent>()) return;

            // Find any PaletteBindingAuthoring in the scene
#if UNITY_EDITOR
            var authors = UnityEngine.Object.FindObjectsByType<PaletteBindingAuthoring>(FindObjectsSortMode.None);
            if (authors == null || authors.Length == 0) return;
            var author = authors[0];
            var asset = author.BindingAsset;
            if (asset == null) return;

            var em = state.EntityManager;
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<PaletteBindingBlob>();
                builder.Allocate(ref root.Families, asset.Families.Count);
                root.NumPhases = asset.NumPhases;
                for (int i = 0; i < asset.Families.Count; i++)
                {
                    var f = asset.Families[i];
                    ref var fb = ref root.Families[i];
                    fb.FamilyId = f.FamilyId;
                    var arr = builder.Allocate(ref fb.PhaseSpriteIndices, f.PhaseSpriteIndices.Count);
                    for (int j = 0; j < f.PhaseSpriteIndices.Count; j++) arr[j] = f.PhaseSpriteIndices[j];
                }

                var blob = builder.CreateBlobAssetReference<PaletteBindingBlob>(Allocator.Persistent);

                var archetype = em.CreateArchetype(typeof(PaletteBindingComponent));
                var e = em.CreateEntity(archetype);
                em.SetComponentData(e, new PaletteBindingComponent { BlobRef = blob });
            }
#endif
        }
    }
}
