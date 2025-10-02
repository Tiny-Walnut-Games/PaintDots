using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PaintDots.Runtime.ABCs
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
			// No explicit requirements; we create the singleton on demand.
		}

		public void OnDestroy(ref SystemState state)
		{
			if (SystemAPI.HasSingleton<PaletteBindingComponent>())
			{
				var singleton = SystemAPI.GetSingleton<PaletteBindingComponent>();
				if (singleton.BlobRef.IsCreated)
				{
					singleton.BlobRef.Dispose();
				}
			}
		}

		public void OnUpdate(ref SystemState state)
		{
			if (SystemAPI.HasSingleton<PaletteBindingComponent>())
			{
				return;
			}

#if UNITY_EDITOR
			var authors = Object.FindObjectsByType<PaletteBindingAuthoring>(FindObjectsSortMode.None);
			if (authors == null || authors.Length == 0)
			{
				return;
			}

			var author = authors[0];
			if (author.BindingAsset == null)
			{
				return;
			}

			var asset = author.BindingAsset;
			var entityManager = state.EntityManager;

			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<PaletteBindingBlob>();
			var families = builder.Allocate(ref root.Families, asset.Families.Count);
			root.NumPhases = asset.NumPhases;

			for (int i = 0; i < asset.Families.Count; i++)
			{
				var family = asset.Families[i];
				ref var familyBlob = ref families[i];
				familyBlob.FamilyId = family.FamilyId;
				var phases = builder.Allocate(ref familyBlob.PhaseSpriteIndices, family.PhaseSpriteIndices.Count);
				for (int j = 0; j < family.PhaseSpriteIndices.Count; j++)
				{
					phases[j] = family.PhaseSpriteIndices[j];
				}
			}

			var blob = builder.CreateBlobAssetReference<PaletteBindingBlob>(Allocator.Persistent);

			var singletonEntity = entityManager.CreateEntity();
			entityManager.AddComponentData(singletonEntity, new PaletteBindingComponent { BlobRef = blob });
#endif
		}
	}
}
