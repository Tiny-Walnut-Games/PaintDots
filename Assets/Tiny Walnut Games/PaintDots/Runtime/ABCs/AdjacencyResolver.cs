using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PaintDots.Runtime.ABCs
{
	public struct ResolvedFamily : IComponentData
	{
		public int TileIndex;
		public int FamilyId;
	}

	public struct CompatibleNeighbor : IBufferElementData
	{
		public int NeighborIndex;
		public float Score;
	}

	public static class AdjacencyResolver
	{
		public static int[] Resolve(EdgeProfileAsset asset, float chromaWeight, float alphaWeight, float threshold, bool chromaFirst)
		{
			if (asset == null) throw new ArgumentNullException(nameof(asset));

			int cols = Math.Max(1, (asset.SourceTexture.width - asset.Margin + asset.Spacing) / (asset.TileWidth + asset.Spacing));
			int rows = Math.Max(1, (asset.SourceTexture.height - asset.Margin + asset.Spacing) / (asset.TileHeight + asset.Spacing));
			int totalEntries = asset.Entries.Count;

			var edges = new List<(int a, int b, float alphaScore, float chromaScore, float total)>();

			for (int ry = 0; ry < rows; ry++)
			{
				for (int rx = 0; rx < cols; rx++)
				{
					int index = ry * cols + rx;
					if (index >= totalEntries) continue;
					var entry = asset.Entries[index];

					if (rx + 1 < cols)
					{
						int rightIndex = ry * cols + (rx + 1);
						if (rightIndex < totalEntries)
						{
							var right = asset.Entries[rightIndex];
							float alphaScore = ComputeAlphaMatch(entry.Right, right.Left);
							float chromaScore = ComputeHueMatch(entry.RightHSL.x, right.LeftHSL.x);
							float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
							edges.Add((index, rightIndex, alphaScore, chromaScore, total));
						}
					}

					if (ry + 1 < rows)
					{
						int bottomIndex = (ry + 1) * cols + rx;
						if (bottomIndex < totalEntries)
						{
							var bottom = asset.Entries[bottomIndex];
							float alphaScore = ComputeAlphaMatch(entry.Bottom, bottom.Top);
							float chromaScore = ComputeHueMatch(entry.BottomHSL.x, bottom.TopHSL.x);
							float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
							edges.Add((index, bottomIndex, alphaScore, chromaScore, total));
						}
					}
				}
			}

			var unionFind = new UnionFind(totalEntries);
			float baseThreshold = Mathf.Clamp01(threshold);

			if (chromaFirst)
			{
				foreach (var edge in edges)
				{
					if (edge.chromaScore >= baseThreshold)
					{
						unionFind.Union(edge.a, edge.b);
					}
				}

				foreach (var edge in edges)
				{
					if (edge.total >= baseThreshold)
					{
						unionFind.Union(edge.a, edge.b);
					}
				}
			}
			else
			{
				foreach (var edge in edges)
				{
					if (edge.alphaScore >= baseThreshold)
					{
						unionFind.Union(edge.a, edge.b);
					}
				}

				foreach (var edge in edges)
				{
					if (edge.total >= baseThreshold)
					{
						unionFind.Union(edge.a, edge.b);
					}
				}
			}

			var rootToFamily = new Dictionary<int, int>();
			int nextFamily = 0;
			var families = new int[totalEntries];

			for (int i = 0; i < totalEntries; i++)
			{
				int root = unionFind.Find(i);
				if (!rootToFamily.TryGetValue(root, out var familyId))
				{
					familyId = nextFamily++;
					rootToFamily[root] = familyId;
				}

				families[i] = familyId;
			}

			return families;
		}

		public static void WriteToWorld(EdgeProfileAsset asset, int[] families, float neighborThreshold, float chromaWeight, float alphaWeight)
		{
			var world = World.DefaultGameObjectInjectionWorld;
			if (world == default) throw new InvalidOperationException("No default World to write adjacency results.");

			var entityManager = world.EntityManager;
			var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResolvedFamily>());
			using var existingEntities = query.ToEntityArray(Allocator.Temp);

			var existingMap = new Dictionary<int, Entity>();
			foreach (var entity in existingEntities)
			{
				var resolved = entityManager.GetComponentData<ResolvedFamily>(entity);
				existingMap[resolved.TileIndex] = entity;
			}

			int cols = Math.Max(1, (asset.SourceTexture.width - asset.Margin + asset.Spacing) / (asset.TileWidth + asset.Spacing));
			int rows = Math.Max(1, (asset.SourceTexture.height - asset.Margin + asset.Spacing) / (asset.TileHeight + asset.Spacing));
			int totalEntries = asset.Entries.Count;

			var scores = new Dictionary<(int, int), float>();

			for (int ry = 0; ry < rows; ry++)
			{
				for (int rx = 0; rx < cols; rx++)
				{
					int index = ry * cols + rx;
					if (index >= totalEntries) continue;
					var entry = asset.Entries[index];

					if (rx + 1 < cols)
					{
						int rightIndex = ry * cols + (rx + 1);
						if (rightIndex < totalEntries)
						{
							var right = asset.Entries[rightIndex];
							float alphaScore = ComputeAlphaMatch(entry.Right, right.Left);
							float chromaScore = ComputeHueMatch(entry.RightHSL.x, right.LeftHSL.x);
							float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
							scores[(index, rightIndex)] = total;
							scores[(rightIndex, index)] = total;
						}
					}

					if (ry + 1 < rows)
					{
						int bottomIndex = (ry + 1) * cols + rx;
						if (bottomIndex < totalEntries)
						{
							var bottom = asset.Entries[bottomIndex];
							float alphaScore = ComputeAlphaMatch(entry.Bottom, bottom.Top);
							float chromaScore = ComputeHueMatch(entry.BottomHSL.x, bottom.TopHSL.x);
							float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
							scores[(index, bottomIndex)] = total;
							scores[(bottomIndex, index)] = total;
						}
					}
				}
			}

			for (int i = 0; i < totalEntries; i++)
			{
				int familyId = families[i];
				if (!existingMap.TryGetValue(i, out var entity))
				{
					entity = entityManager.CreateEntity();
					entityManager.AddComponentData(entity, new ResolvedFamily { TileIndex = i, FamilyId = familyId });
					entityManager.AddBuffer<CompatibleNeighbor>(entity);
				}
				else
				{
					entityManager.SetComponentData(entity, new ResolvedFamily { TileIndex = i, FamilyId = familyId });
					entityManager.GetBuffer<CompatibleNeighbor>(entity).Clear();
				}

				var buffer = entityManager.GetBuffer<CompatibleNeighbor>(entity);
				foreach (var kvp in scores)
				{
					if (kvp.Key.Item1 != i) continue;
					if (kvp.Value >= neighborThreshold)
					{
						buffer.Add(new CompatibleNeighbor
						{
							NeighborIndex = kvp.Key.Item2,
							Score = kvp.Value
						});
					}
				}
			}
		}

		public static void WriteToWorld(EdgeProfileAsset asset, int[] families, float neighborThreshold)
		{
#if UNITY_EDITOR
			float chromaWeight = UnityEditor.EditorPrefs.GetFloat("PaintDots_ChromaWeight", 0.5f);
			float alphaWeight = UnityEditor.EditorPrefs.GetFloat("PaintDots_AlphaWeight", 0.5f);
			WriteToWorld(asset, families, neighborThreshold, chromaWeight, alphaWeight);
#else
			WriteToWorld(asset, families, neighborThreshold, 0.5f, 0.5f);
#endif
		}

		private static float ComputeAlphaMatch(byte[] a, byte[] b)
		{
			if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0f;
			int len = Math.Min(a.Length, b.Length);
			int matches = 0;
			int total = 0;

			for (int i = 0; i < len; i++)
			{
				float va = a[i] / 255f;
				float vb = b[i] / 255f;

				if (va > 0.1f || vb > 0.1f)
				{
					total++;
					float diff = Mathf.Abs(va - vb);
					if (diff < 0.35f) matches++;
				}
			}

			if (total == 0) return 0f;
			return Mathf.Clamp01(matches / (float)total);
		}

		private static float ComputeHueMatch(float h1, float h2)
		{
			float diff = Mathf.Abs(h1 - h2);
			diff = Mathf.Min(diff, 1f - diff);
			return 1f - Mathf.Clamp01(diff);
		}

		private sealed class UnionFind
		{
			private readonly int[] _parents;

			public UnionFind(int count)
			{
				_parents = new int[count];
				for (int i = 0; i < count; i++)
				{
					_parents[i] = i;
				}
			}

			public int Find(int value)
			{
				if (_parents[value] == value)
				{
					return value;
				}

				_parents[value] = Find(_parents[value]);
				return _parents[value];
			}

			public void Union(int a, int b)
			{
				int rootA = Find(a);
				int rootB = Find(b);
				if (rootA != rootB)
				{
					_parents[rootB] = rootA;
				}
			}
		}
	}
}
