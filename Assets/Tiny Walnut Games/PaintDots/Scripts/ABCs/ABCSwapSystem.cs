using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PaintDots.ECS;

namespace PaintDots.ECS.ABCs
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ABCSwapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhaseControl>();
            RequireForUpdate<PaletteBindingComponent>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<PhaseControl>()) return;
            var phase = SystemAPI.GetSingleton<PhaseControl>();
            if (!phase.Dirty) return;

            if (!SystemAPI.HasSingleton<PaletteBindingComponent>()) return;
            var pb = SystemAPI.GetSingleton<PaletteBindingComponent>();
            var blob = pb.BlobRef;
            if (!blob.IsCreated) return;

            int phaseIdx = math.clamp(phase.PhaseIndex, 0, blob.Value.NumPhases - 1);

            // Build mapping FamilyId -> blob index
            int familyCount = blob.Value.Families.Length;
            var familyLookup = new NativeHashMap<int,int>(familyCount, Allocator.Temp);
            for (int i = 0; i < familyCount; i++)
            {
                ref var fb = ref blob.Value.Families[i];
                familyLookup.TryAdd(fb.FamilyId, i);
            }

            // Build mapping from tile index -> family id using ResolvedFamily entities
            var rfQuery = GetEntityQuery(ComponentType.ReadOnly<ResolvedFamily>());
            using var rfEntities = rfQuery.ToEntityArray(Allocator.Temp);
            var tileToFamily = new NativeHashMap<int,int>(math.max(1, rfEntities.Length), Allocator.Temp);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            for (int i = 0; i < rfEntities.Length; i++)
            {
                var rf = em.GetComponentData<ResolvedFamily>(rfEntities[i]);
                tileToFamily.TryAdd(rf.TileIndex, rf.FamilyId);
            }

            // Iterate tiles and update SpriteIndex where mapping exists
            Entities
                .WithAll<Tile, TileRenderData>()
                .ForEach((ref TileRenderData rd, in Tile t) =>
                {
                    if (!tileToFamily.TryGetValue(t.TileID, out var familyId)) return;
                    if (!familyLookup.TryGetValue(familyId, out var famIdx)) return;
                    ref var fam = ref blob.Value.Families[famIdx];
                    if (phaseIdx < fam.PhaseSpriteIndices.Length)
                    {
                        int newSprite = fam.PhaseSpriteIndices[phaseIdx];
                        if (rd.SpriteIndex != newSprite) rd = rd.WithSpriteIndex(newSprite);
                    }
                }).Run();

            tileToFamily.Dispose();
            familyLookup.Dispose();

            // reset dirty
            phase.Dirty = false;
            SystemAPI.SetSingleton(phase);
        }
    }
}
