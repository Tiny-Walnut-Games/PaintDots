using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PaintDots.Runtime;

namespace PaintDots.Runtime.ABCs
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
			if (!SystemAPI.HasSingleton<PhaseControl>())
			{
				return;
			}

			var phase = SystemAPI.GetSingleton<PhaseControl>();
			if (!phase.Dirty)
			{
				return;
			}

			if (!SystemAPI.HasSingleton<PaletteBindingComponent>())
			{
				return;
			}

			var binding = SystemAPI.GetSingleton<PaletteBindingComponent>();
			var blob = binding.BlobRef;
			if (!blob.IsCreated)
			{
				return;
			}

			int phaseIndex = math.clamp(phase.PhaseIndex, 0, blob.Value.NumPhases - 1);

			int familyCount = blob.Value.Families.Length;
			var familyLookup = new NativeHashMap<int, int>(familyCount, Allocator.Temp);
			for (int i = 0; i < familyCount; i++)
			{
				ref var family = ref blob.Value.Families[i];
				familyLookup.TryAdd(family.FamilyId, i);
			}

			var resolvedQuery = GetEntityQuery(ComponentType.ReadOnly<ResolvedFamily>());
			using var resolvedEntities = resolvedQuery.ToEntityArray(Allocator.Temp);
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			var tileToFamily = new NativeHashMap<int, int>(math.max(1, resolvedEntities.Length), Allocator.Temp);

			for (int i = 0; i < resolvedEntities.Length; i++)
			{
				var resolved = entityManager.GetComponentData<ResolvedFamily>(resolvedEntities[i]);
				tileToFamily.TryAdd(resolved.TileIndex, resolved.FamilyId);
			}

			Entities
				.WithAll<Tile, TileRenderData>()
				.ForEach((ref TileRenderData renderData, in Tile tile) =>
				{
					if (!tileToFamily.TryGetValue(tile.TileID, out var familyId))
					{
						return;
					}

					if (!familyLookup.TryGetValue(familyId, out var familyIndex))
					{
						return;
					}

					ref var family = ref blob.Value.Families[familyIndex];
					if (phaseIndex < family.PhaseSpriteIndices.Length)
					{
						int newSpriteIndex = family.PhaseSpriteIndices[phaseIndex];
						if (renderData.SpriteIndex != newSpriteIndex)
						{
							renderData = renderData.WithSpriteIndex(newSpriteIndex);
						}
					}
				}).Run();

			tileToFamily.Dispose();
			familyLookup.Dispose();

			phase.Dirty = false;
			SystemAPI.SetSingleton(phase);
		}
	}
}
