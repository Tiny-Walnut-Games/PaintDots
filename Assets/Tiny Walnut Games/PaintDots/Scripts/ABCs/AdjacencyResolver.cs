using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace PaintDots.ECS.ABCs
{
    // Component that stores resolved family id for a tile index
    public struct ResolvedFamily : IComponentData
    {
        public int TileIndex;
        public int FamilyId;
    }

    // Buffer element for compatible neighbors and scores
    public struct CompatibleNeighbor : IBufferElementData
    {
        public int NeighborIndex;
        public float Score;
    }

    public static class AdjacencyResolver
    {
        // Resolve families using a simple multi-pass strategy combining chroma and alpha
        // Returns array of family ids per tile index
        public static int[] Resolve(EdgeProfileAsset asset, float chromaWeight, float alphaWeight, float threshold, bool chromaFirst)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            int cols = Math.Max(1, (asset.SourceTexture.width - asset.Margin + asset.Spacing) / (asset.TileWidth + asset.Spacing));
            int rows = Math.Max(1, (asset.SourceTexture.height - asset.Margin + asset.Spacing) / (asset.TileHeight + asset.Spacing));
            int n = asset.Entries.Count;

            // compute adjacency scores for neighbor pairs (only direct grid neighbors)
            var edges = new List<(int a, int b, float alphaScore, float chromaScore, float total)>();

            for (int ry = 0; ry < rows; ry++)
            {
                for (int rx = 0; rx < cols; rx++)
                {
                    int i = ry * cols + rx;
                    if (i >= n) continue;
                    var ei = asset.Entries[i];

                    // right neighbor
                    if (rx + 1 < cols)
                    {
                        int j = ry * cols + (rx + 1);
                        if (j < n)
                        {
                            var ej = asset.Entries[j];
                            float alphaScore = ComputeAlphaMatch(ei.Right, ej.Left);
                            float chromaScore = ComputeHueMatch(ei.RightHSL.x, ej.LeftHSL.x);
                            float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
                            edges.Add((i, j, alphaScore, chromaScore, total));
                        }
                    }

                    // bottom neighbor
                    if (ry + 1 < rows)
                    {
                        int j = (ry + 1) * cols + rx;
                        if (j < n)
                        {
                            var ej = asset.Entries[j];
                            float alphaScore = ComputeAlphaMatch(ei.Bottom, ej.Top);
                            float chromaScore = ComputeHueMatch(ei.BottomHSL.x, ej.TopHSL.x);
                            float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
                            edges.Add((i, j, alphaScore, chromaScore, total));
                        }
                    }
                }
            }

            // Build initial graph based on pass order and thresholds
            var uf = new UnionFind(n);

            float baseThreshold = Mathf.Clamp01(threshold);

            if (chromaFirst)
            {
                // add edges where chroma strong
                foreach (var e in edges)
                {
                    if (e.chromaScore >= baseThreshold)
                        uf.Union(e.a, e.b);
                }
                // refine using total score
                foreach (var e in edges)
                {
                    if (e.total >= baseThreshold)
                        uf.Union(e.a, e.b);
                }
            }
            else
            {
                // alpha first
                foreach (var e in edges)
                {
                    if (e.alphaScore >= baseThreshold)
                        uf.Union(e.a, e.b);
                }
                foreach (var e in edges)
                {
                    if (e.total >= baseThreshold)
                        uf.Union(e.a, e.b);
                }
            }

            // assign family ids by component root
            var rootToFamily = new Dictionary<int, int>();
            int nextFamily = 0;
            int[] families = new int[n];
            for (int i = 0; i < n; i++)
            {
                int r = uf.Find(i);
                if (!rootToFamily.TryGetValue(r, out var fid))
                {
                    fid = nextFamily++;
                    rootToFamily[r] = fid;
                }
                families[i] = fid;
            }

            return families;
        }

    // Write resolved families and neighbor buffers into the provided world (DefaultWorld)
    // Accept chroma/alpha weights so the neighbor buffer uses the same weighted score as Resolve
    public static void WriteToWorld(EdgeProfileAsset asset, int[] families, float neighborThreshold, float chromaWeight, float alphaWeight)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == default) throw new InvalidOperationException("No default World to write adjacency results.");
            var em = world.EntityManager;

            // map existing ResolvedFamily entities by TileIndex
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<ResolvedFamily>());
            var existing = query.ToEntityArray(Allocator.Temp);
            var map = new Dictionary<int, Entity>();
            foreach (var ent in existing)
            {
                var rf = em.GetComponentData<ResolvedFamily>(ent);
                map[rf.TileIndex] = ent;
            }
            existing.Dispose();

            int cols = Math.Max(1, (asset.SourceTexture.width - asset.Margin + asset.Spacing) / (asset.TileWidth + asset.Spacing));
            int rows = Math.Max(1, (asset.SourceTexture.height - asset.Margin + asset.Spacing) / (asset.TileHeight + asset.Spacing));
            int n = asset.Entries.Count;

            // Precompute adjacency scores again to populate neighbor buffers
            // Use the provided chroma/alpha weights so buffer scores match Resolve's totals
            var scores = new Dictionary<(int, int), float>();
            for (int ry = 0; ry < rows; ry++)
            {
                for (int rx = 0; rx < cols; rx++)
                {
                    int i = ry * cols + rx;
                    if (i >= n) continue;
                    var ei = asset.Entries[i];
                    if (rx + 1 < cols)
                    {
                        int j = ry * cols + (rx + 1);
                        if (j < n)
                        {
                            var ej = asset.Entries[j];
                            float alphaScore = ComputeAlphaMatch(ei.Right, ej.Left);
                            float chromaScore = ComputeHueMatch(ei.RightHSL.x, ej.LeftHSL.x);
                            float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
                            scores[(i, j)] = total;
                            scores[(j, i)] = total;
                        }
                    }
                    if (ry + 1 < rows)
                    {
                        int j = (ry + 1) * cols + rx;
                        if (j < n)
                        {
                            var ej = asset.Entries[j];
                            float alphaScore = ComputeAlphaMatch(ei.Bottom, ej.Top);
                            float chromaScore = ComputeHueMatch(ei.BottomHSL.x, ej.TopHSL.x);
                            float total = chromaWeight * chromaScore + alphaWeight * alphaScore;
                            scores[(i, j)] = total;
                            scores[(j, i)] = total;
                        }
                    }
                }
            }

            // For each tile, create or update entity with ResolvedFamily + buffer of CompatibleNeighbor
            for (int i = 0; i < n; i++)
            {
                int family = families[i];
                Entity ent;
                if (!map.TryGetValue(i, out ent))
                {
                    ent = em.CreateEntity();
                    em.AddComponentData(ent, new ResolvedFamily { TileIndex = i, FamilyId = family });
                    em.AddBuffer<CompatibleNeighbor>(ent);
                }
                else
                {
                    em.SetComponentData(ent, new ResolvedFamily { TileIndex = i, FamilyId = family });
                    var buf = em.GetBuffer<CompatibleNeighbor>(ent);
                    buf.Clear();
                }

                var buffer = em.GetBuffer<CompatibleNeighbor>(ent);
                // add neighbors with score >= neighborThreshold
                foreach (var kv in scores)
                {
                    if (kv.Key.Item1 != i) continue;
                    if (kv.Value >= neighborThreshold)
                    {
                        buffer.Add(new CompatibleNeighbor { NeighborIndex = kv.Key.Item2, Score = kv.Value });
                    }
                }
            }
        }

    // Backwards-compatible overload: uses default weights (0.5/0.5) or EditorPrefs if available
    public static void WriteToWorld(EdgeProfileAsset asset, int[] families, float neighborThreshold)
    {
        // try to read EditorPrefs for default weights if running in editor
#if UNITY_EDITOR
        float chromaW = UnityEditor.EditorPrefs.GetFloat("PaintDots_ChromaWeight", 0.5f);
        float alphaW = UnityEditor.EditorPrefs.GetFloat("PaintDots_AlphaWeight", 0.5f);
        WriteToWorld(asset, families, neighborThreshold, chromaW, alphaW);
#else
        WriteToWorld(asset, families, neighborThreshold, 0.5f, 0.5f);
#endif
    }

        // Helper: compute alpha match score between two edge alpha arrays (0..1)
        private static float ComputeAlphaMatch(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0f;
            int len = Math.Min(a.Length, b.Length);
            int match = 0; int total = 0;
            for (int i = 0; i < len; i++)
            {
                float va = a[i] / 255f;
                float vb = b[i] / 255f;
                // consider positions where both are present
                if (va > 0.1f || vb > 0.1f)
                {
                    total++;
                    float diff = Math.Abs(va - vb);
                    if (diff < 0.35f) match++;
                }
            }
            if (total == 0) return 0f;
            return Mathf.Clamp01(match / (float)total);
        }

        // Helper: compute hue-based match (circular diff) -> 0..1 (1 = perfect match)
        private static float ComputeHueMatch(float h1, float h2)
        {
            float d = Math.Abs(h1 - h2);
            d = Math.Min(d, 1f - d);
            return 1f - Mathf.Clamp01(d);
        }

        // Simple union-find
        private class UnionFind
        {
            private int[] p;
            public UnionFind(int n) { p = new int[n]; for (int i = 0; i < n; i++) p[i] = i; }
            public int Find(int x) { return p[x] == x ? x : (p[x] = Find(p[x])); }
            public void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) p[rb] = ra; }
        }
    }
}
