using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PaintDots.Runtime.ABCs;

namespace PaintDots.Editor.ABCs
{
    public static class PaletteBindingBakeUtility
    {
        public static PaletteBindingAsset BakeFromEdgeProfile(EdgeProfileAsset edgeProfile, int numPhases, string assetPath)
        {
            if (edgeProfile == null) throw new System.ArgumentNullException(nameof(edgeProfile));
            if (string.IsNullOrEmpty(assetPath)) throw new System.ArgumentException("Invalid asset path", nameof(assetPath));

            var familyGroups = new Dictionary<int, List<EdgeProfileEntry>>();
            int fallbackId = 100000;

            foreach (var entry in edgeProfile.Entries)
            {
                if (entry == null) continue;
                int familyId = entry.FamilyId >= 0 ? entry.FamilyId : fallbackId++;
                if (!familyGroups.TryGetValue(familyId, out var list))
                {
                    list = new List<EdgeProfileEntry>();
                    familyGroups[familyId] = list;
                }
                list.Add(entry);
            }

            var binding = ScriptableObject.CreateInstance<PaletteBindingAsset>();
            binding.NumPhases = Mathf.Max(1, numPhases);

            foreach (var kvp in familyGroups)
            {
                var familyEntry = new PaletteBindingAsset.FamilyEntry
                {
                    FamilyId = kvp.Key,
                };

                for (int phase = 0; phase < binding.NumPhases; phase++)
                {
                    int chosen = -1;
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        var candidate = kvp.Value[i];
                        if (candidate.PhaseIndex == phase)
                        {
                            chosen = candidate.TileIndex;
                            break;
                        }
                    }

                    if (chosen < 0 && kvp.Value.Count > 0)
                    {
                        chosen = kvp.Value[0].TileIndex;
                    }

                    familyEntry.PhaseSpriteIndices.Add(chosen);
                }

                binding.Families.Add(familyEntry);
            }

            AssetDatabase.CreateAsset(binding, assetPath);
            EditorUtility.SetDirty(binding);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return binding;
        }

        public static PaletteBindingAuthoring EnsureAuthoring(PaletteBindingAsset bindingAsset)
        {
            if (bindingAsset == null) throw new System.ArgumentNullException(nameof(bindingAsset));

            PaletteBindingAuthoring existing = null;
#if UNITY_2023_1_OR_NEWER
            existing = Object.FindFirstObjectByType<PaletteBindingAuthoring>();
#else
            existing = Object.FindObjectOfType<PaletteBindingAuthoring>();
#endif
            if (existing != null)
            {
                Undo.RecordObject(existing, "Assign PaletteBindingAsset");
                existing.BindingAsset = bindingAsset;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject("PaletteBindingAuthoring");
            Undo.RegisterCreatedObjectUndo(go, "Create PaletteBindingAuthoring");
            var authoring = go.AddComponent<PaletteBindingAuthoring>();
            authoring.BindingAsset = bindingAsset;
            EditorUtility.SetDirty(authoring);
            return authoring;
        }
    }
}
