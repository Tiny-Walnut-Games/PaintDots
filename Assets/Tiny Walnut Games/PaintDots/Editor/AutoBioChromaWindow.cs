using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using PaintDots.ECS.ABCs;

public class AutoBioChromaWindow : EditorWindow
{
    private int _phase = 0;
    private int _numPhases = 6;
    private int _biomeId = 0;

    [MenuItem("PaintDots/AutoBioChroma Slider")]
    public static void ShowWindow()
    {
        GetWindow<AutoBioChromaWindow>("ABCs");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AutoBioChroma Slider (ABCs)", EditorStyles.boldLabel);
        _numPhases = EditorGUILayout.IntField("Num Phases", _numPhases);
        _phase = EditorGUILayout.IntSlider("Phase Index", _phase, 0, Mathf.Max(1, _numPhases - 1));
        _biomeId = EditorGUILayout.IntField("Biome ID", _biomeId);

        if (GUILayout.Button("Apply to World"))
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == default)
            {
                Debug.LogWarning("No default World found. Enter Play Mode or create a World.");
            }
            else
            {
                var em = world.EntityManager;
                Entity singleton;
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<PhaseControl>());
                if (query.IsEmpty)
                {
                    singleton = em.CreateEntity(typeof(PhaseControl));
                }
                else
                {
                    singleton = query.GetSingletonEntity();
                }

                em.SetComponentData(singleton, new PhaseControl { PhaseIndex = _phase, BiomeId = _biomeId, NumPhases = _numPhases, Dirty = true });
                Debug.Log($"ABCs applied: phase={_phase} biome={_biomeId}");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parsing & Classification", EditorStyles.boldLabel);
        if (GUILayout.Button("Parse Selected Texture / Palette"))
        {
            ParseSelectedTexture();
        }
        if (GUILayout.Button("Run Chroma Pass (sample per-edge HSL)"))
        {
            RunChromaPassOnSelected();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Chroma Weight", GUILayout.Width(100));
        float chromaWeight = EditorPrefs.GetFloat("PaintDots_ChromaWeight", 0.5f);
        chromaWeight = EditorGUILayout.Slider(chromaWeight, 0f, 1f);
        EditorPrefs.SetFloat("PaintDots_ChromaWeight", chromaWeight);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bake Palette Bindings", EditorStyles.boldLabel);
        if (GUILayout.Button("Bake PaletteBinding Asset from EdgeProfile"))
        {
            var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
            if (obj == null)
            {
                EditorUtility.DisplayDialog("No EdgeProfile Asset selected", "Select an EdgeProfile asset in the Project view.", "OK");
            }
            else
            {
                // ensure families have been resolved
                bool anyUnresolved = false;
                foreach (var e in obj.Entries) if (e.FamilyId < 0) { anyUnresolved = true; break; }
                if (anyUnresolved)
                {
                    if (!EditorUtility.DisplayDialog("Unresolved families", "Some entries have FamilyId == -1. Run Resolve Adjacency first. Continue and treat unresolved as separate families?", "Continue", "Cancel"))
                    {
                        return;
                    }
                }

                // collect family ids
                var familiesSet = new System.Collections.Generic.HashSet<int>();
                foreach (var e in obj.Entries) familiesSet.Add(e.FamilyId);

                string savePath = EditorUtility.SaveFilePanelInProject("Save PaletteBinding Asset", obj.name + "_PaletteBinding", "asset", "Save palette binding asset");
                if (string.IsNullOrEmpty(savePath)) return;

                var binding = ScriptableObject.CreateInstance<PaintDots.ECS.ABCs.PaletteBindingAsset>();
                binding.NumPhases = _numPhases;

                foreach (var famId in familiesSet)
                {
                    var fe = new PaintDots.ECS.ABCs.PaletteBindingAsset.FamilyEntry();
                    fe.FamilyId = famId;
                    // for each phase, pick a representative tile index from the family with matching PhaseIndex
                    for (int p = 0; p < _numPhases; p++)
                    {
                        int chosen = -1;
                        // find tile in family with that phase
                        for (int i = 0; i < obj.Entries.Count; i++)
                        {
                            var en = obj.Entries[i];
                            if (en.FamilyId != famId) continue;
                            if (en.PhaseIndex == p) { chosen = en.TileIndex; break; }
                        }
                        // fallback: pick any tile from the family
                        if (chosen < 0)
                        {
                            for (int i = 0; i < obj.Entries.Count; i++)
                            {
                                var en = obj.Entries[i];
                                if (en.FamilyId == famId) { chosen = en.TileIndex; break; }
                            }
                        }
                        fe.PhaseSpriteIndices.Add(chosen);
                    }
                    binding.Families.Add(fe);
                }

                AssetDatabase.CreateAsset(binding, savePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Baked", $"PaletteBinding asset created at {savePath} with {binding.Families.Count} families and {_numPhases} phases.", "OK");
            }
        }
        if (GUILayout.Button("Classify Parsed Profiles"))
        {
            ClassifyParsedProfiles(_numPhases);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Adjacency Resolver", EditorStyles.boldLabel);
        float chromaW = EditorPrefs.GetFloat("PaintDots_ChromaWeight", 0.5f);
        float alphaW = EditorPrefs.GetFloat("PaintDots_AlphaWeight", 0.5f);
        chromaW = EditorGUILayout.Slider("Chroma Weight", chromaW, 0f, 1f);
        alphaW = EditorGUILayout.Slider("Alpha Weight", alphaW, 0f, 1f);
        EditorPrefs.SetFloat("PaintDots_ChromaWeight", chromaW);
        EditorPrefs.SetFloat("PaintDots_AlphaWeight", alphaW);
        float threshold = EditorPrefs.GetFloat("PaintDots_AdjThreshold", 0.6f);
        threshold = EditorGUILayout.Slider("Adjacency Threshold", threshold, 0f, 1f);
        EditorPrefs.SetFloat("PaintDots_AdjThreshold", threshold);
        bool chromaFirst = EditorPrefs.GetBool("PaintDots_ChromaFirst", true);
        chromaFirst = EditorGUILayout.Toggle("Chroma First", chromaFirst);
        EditorPrefs.SetBool("PaintDots_ChromaFirst", chromaFirst);

        if (GUILayout.Button("Resolve Adjacency (multi-pass)"))
        {
            var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
            if (obj == null)
            {
                EditorUtility.DisplayDialog("No EdgeProfile Asset selected", "Select an EdgeProfile asset in the Project view.", "OK");
            }
            else
            {
                int[] fams = PaintDots.ECS.ABCs.AdjacencyResolver.Resolve(obj, chromaW, alphaW, threshold, chromaFirst);
                // persist family ids into asset entries (FamilyId field)
                for (int i = 0; i < obj.Entries.Count && i < fams.Length; i++) obj.Entries[i].FamilyId = fams[i];
                EditorUtility.SetDirty(obj);
                AssetDatabase.SaveAssets();

                // write to world (AdjacencyResolver will read weights from EditorPrefs or use defaults)
                PaintDots.ECS.ABCs.AdjacencyResolver.WriteToWorld(obj, fams, threshold);
                var set = new System.Collections.Generic.HashSet<int>(fams);
                EditorUtility.DisplayDialog("Resolved", $"Adjacency resolved into {set.Count} families and written to World.", "OK");
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preset: Platformer"))
        {
            EditorPrefs.SetFloat("PaintDots_ChromaWeight", 0.2f);
            EditorPrefs.SetFloat("PaintDots_AlphaWeight", 0.9f);
            EditorPrefs.SetBool("PaintDots_ChromaFirst", false);
            // trigger resolve if asset selected
            var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
            if (obj != null)
            {
                int[] fams = PaintDots.ECS.ABCs.AdjacencyResolver.Resolve(obj, 0.2f, 0.9f, EditorPrefs.GetFloat("PaintDots_AdjThreshold", 0.6f), false);
                for (int i = 0; i < obj.Entries.Count && i < fams.Length; i++) obj.Entries[i].FamilyId = fams[i];
                EditorUtility.SetDirty(obj); AssetDatabase.SaveAssets();
                PaintDots.ECS.ABCs.AdjacencyResolver.WriteToWorld(obj, fams, EditorPrefs.GetFloat("PaintDots_AdjThreshold", 0.6f));
            }
        }
        if (GUILayout.Button("Preset: Top-Down"))
        {
            EditorPrefs.SetFloat("PaintDots_ChromaWeight", 0.9f);
            EditorPrefs.SetFloat("PaintDots_AlphaWeight", 0.2f);
            EditorPrefs.SetBool("PaintDots_ChromaFirst", true);
            var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
            if (obj != null)
            {
                int[] fams = PaintDots.ECS.ABCs.AdjacencyResolver.Resolve(obj, 0.9f, 0.2f, EditorPrefs.GetFloat("PaintDots_AdjThreshold", 0.6f), true);
                for (int i = 0; i < obj.Entries.Count && i < fams.Length; i++) obj.Entries[i].FamilyId = fams[i];
                EditorUtility.SetDirty(obj); AssetDatabase.SaveAssets();
                PaintDots.ECS.ABCs.AdjacencyResolver.WriteToWorld(obj, fams, EditorPrefs.GetFloat("PaintDots_AdjThreshold", 0.6f));
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ParseSelectedTexture()
    {
        var obj = Selection.activeObject;
        Texture2D tex = null;
        PaintDots.Editor.TilePalette pal = null;
        if (obj is PaintDots.Editor.TilePalette p)
        {
            pal = p;
            tex = pal.Texture;
        }
        else if (obj is Texture2D t)
        {
            tex = t;
        }

        if (tex == null)
        {
            EditorUtility.DisplayDialog("No texture/palette selected", "Select a TilePalette asset or a Texture2D in the Project view.", "OK");
            return;
        }

        int tileW = pal != null ? pal.TileWidth : 16;
        int tileH = pal != null ? pal.TileHeight : 16;
        int margin = pal != null ? pal.Margin : 0;
        int spacing = pal != null ? pal.Spacing : 0;

        string path = EditorUtility.SaveFilePanelInProject("Save EdgeProfile Asset", tex.name + "_EdgeProfiles", "asset", "Save edge profiles");
        if (string.IsNullOrEmpty(path)) return;

        var asset = ScriptableObject.CreateInstance<PaintDots.ECS.ABCs.EdgeProfileAsset>();
        asset.SourceTexture = tex;
        asset.TileWidth = tileW;
        asset.TileHeight = tileH;
        asset.Margin = margin;

        // perform parsing
        var pixels = tex.GetPixels32();
        int cols = Mathf.Max(1, (tex.width - margin + spacing) / (tileW + spacing));
        int rows = Mathf.Max(1, (tex.height - margin + spacing) / (tileH + spacing));

        for (int ry = 0; ry < rows; ry++)
        {
            for (int rx = 0; rx < cols; rx++)
            {
                int px = margin + rx * (tileW + spacing);
                int pyTop = margin + ry * (tileH + spacing);
                // convert top-based py to bottom-based index
                int pyBottom = tex.height - (pyTop + tileH);

                var entry = new PaintDots.ECS.ABCs.EdgeProfileEntry();
                entry.TileIndex = ry * cols + rx;
                entry.Top = new byte[tileW];
                entry.Bottom = new byte[tileW];
                entry.Left = new byte[tileH];
                entry.Right = new byte[tileH];

                uint rSum = 0, gSum = 0, bSum = 0, aSum = 0;
                int colorCount = 0;

                // top edge (y = pyTop + tileH - 1 from bottom-based)
                for (int x = 0; x < tileW; x++)
                {
                    int sx = px + x;
                    int sy = pyBottom + tileH - 1;
                    var c = tex.GetPixel(sx, sy);
                    entry.Top[x] = (byte)(c.a * 255f);
                    if (c.a > 0f) { rSum += (uint)(c.r * 255f); gSum += (uint)(c.g * 255f); bSum += (uint)(c.b * 255f); aSum += (uint)(c.a * 255f); colorCount++; }
                }

                // bottom edge (y = pyBottom)
                for (int x = 0; x < tileW; x++)
                {
                    int sx = px + x;
                    int sy = pyBottom;
                    var c = tex.GetPixel(sx, sy);
                    entry.Bottom[x] = (byte)(c.a * 255f);
                    if (c.a > 0f) { rSum += (uint)(c.r * 255f); gSum += (uint)(c.g * 255f); bSum += (uint)(c.b * 255f); aSum += (uint)(c.a * 255f); colorCount++; }
                }

                // left edge
                for (int y = 0; y < tileH; y++)
                {
                    int sx = px;
                    int sy = pyBottom + y;
                    var c = tex.GetPixel(sx, sy);
                    entry.Left[y] = (byte)(c.a * 255f);
                    if (c.a > 0f) { rSum += (uint)(c.r * 255f); gSum += (uint)(c.g * 255f); bSum += (uint)(c.b * 255f); aSum += (uint)(c.a * 255f); colorCount++; }
                }

                // right edge
                for (int y = 0; y < tileH; y++)
                {
                    int sx = px + tileW - 1;
                    int sy = pyBottom + y;
                    var c = tex.GetPixel(sx, sy);
                    entry.Right[y] = (byte)(c.a * 255f);
                    if (c.a > 0f) { rSum += (uint)(c.r * 255f); gSum += (uint)(c.g * 255f); bSum += (uint)(c.b * 255f); aSum += (uint)(c.a * 255f); colorCount++; }
                }

                if (colorCount > 0)
                {
                    entry.AvgRGB = new Vector3(rSum / (float)colorCount / 255f, gSum / (float)colorCount / 255f, bSum / (float)colorCount / 255f);
                }
                else
                {
                    entry.AvgRGB = new Vector3(0f, 0f, 0f);
                }

                // Compute per-edge average HSL for chroma matching
                // Top edge
                uint tr = 0, tg = 0, tb = 0; int tc = 0;
                for (int x = 0; x < tileW; x++)
                {
                    int sx = px + x;
                    int sy = pyBottom + tileH - 1;
                    var c = tex.GetPixel(sx, sy);
                    if (c.a > 0f) { tr += (uint)(c.r * 255f); tg += (uint)(c.g * 255f); tb += (uint)(c.b * 255f); tc++; }
                }
                if (tc > 0)
                {
                    float rr = tr / (float)tc / 255f; float rg = tg / (float)tc / 255f; float rb = tb / (float)tc / 255f;
                    float h, s, l; RGBToHSL(rr, rg, rb, out h, out s, out l);
                    entry.TopHSL = new Vector3(h, s, l);
                }
                // Bottom edge
                tr = tg = tb = 0; tc = 0;
                for (int x = 0; x < tileW; x++)
                {
                    int sx = px + x;
                    int sy = pyBottom;
                    var c = tex.GetPixel(sx, sy);
                    if (c.a > 0f) { tr += (uint)(c.r * 255f); tg += (uint)(c.g * 255f); tb += (uint)(c.b * 255f); tc++; }
                }
                if (tc > 0)
                {
                    float rr = tr / (float)tc / 255f; float rg = tg / (float)tc / 255f; float rb = tb / (float)tc / 255f;
                    float h, s, l; RGBToHSL(rr, rg, rb, out h, out s, out l);
                    entry.BottomHSL = new Vector3(h, s, l);
                }
                // Left edge
                tr = tg = tb = 0; tc = 0;
                for (int y = 0; y < tileH; y++)
                {
                    int sx = px;
                    int sy = pyBottom + y;
                    var c = tex.GetPixel(sx, sy);
                    if (c.a > 0f) { tr += (uint)(c.r * 255f); tg += (uint)(c.g * 255f); tb += (uint)(c.b * 255f); tc++; }
                }
                if (tc > 0)
                {
                    float rr = tr / (float)tc / 255f; float rg = tg / (float)tc / 255f; float rb = tb / (float)tc / 255f;
                    float h, s, l; RGBToHSL(rr, rg, rb, out h, out s, out l);
                    entry.LeftHSL = new Vector3(h, s, l);
                }
                // Right edge
                tr = tg = tb = 0; tc = 0;
                for (int y = 0; y < tileH; y++)
                {
                    int sx = px + tileW - 1;
                    int sy = pyBottom + y;
                    var c = tex.GetPixel(sx, sy);
                    if (c.a > 0f) { tr += (uint)(c.r * 255f); tg += (uint)(c.g * 255f); tb += (uint)(c.b * 255f); tc++; }
                }
                if (tc > 0)
                {
                    float rr = tr / (float)tc / 255f; float rg = tg / (float)tc / 255f; float rb = tb / (float)tc / 255f;
                    float h, s, l; RGBToHSL(rr, rg, rb, out h, out s, out l);
                    entry.RightHSL = new Vector3(h, s, l);
                }

                asset.Entries.Add(entry);
            }
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Parsed", $"Edge profiles saved to {path}", "OK");
    }

    private void ClassifyParsedProfiles(int numPhases)
    {
        var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("No EdgeProfile Asset selected", "Select an EdgeProfile asset in the Project view.", "OK");
            return;
        }

        // simple hue quantization and idempotent PhaseDescriptor creation
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == default)
        {
            EditorUtility.DisplayDialog("No World", "No default World found. Enter Play Mode or create a World to persist PhaseDescriptor entities.", "OK");
            return;
        }

        var em = world.EntityManager;
        // Ensure we can find/update existing PhaseDescriptor entities: query all and build a map by FamilyId
        var existingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PhaseDescriptor>());
        var existingEntities = existingQuery.ToEntityArray(Unity.Collections.Allocator.TempJob);
        var familyToEntity = new System.Collections.Generic.Dictionary<int, Entity>();
        foreach (var ent in existingEntities)
        {
            var pd = em.GetComponentData<PhaseDescriptor>(ent);
            familyToEntity[pd.FamilyId] = ent;
        }
        existingEntities.Dispose();

        foreach (var e in obj.Entries)
        {
            var rgb = e.AvgRGB;
            float h, s, l;
            RGBToHSL(rgb.x, rgb.y, rgb.z, out h, out s, out l);
            int phase = Mathf.FloorToInt(h * numPhases) % numPhases;

            // store classification into the asset entry (persisted)
            e.PhaseIndex = phase;
            e.HueCenter = h;

            // create or update a single PhaseDescriptor entity for this family/tile index
            if (familyToEntity.TryGetValue(e.TileIndex, out var ent))
            {
                em.SetComponentData(ent, new PhaseDescriptor { FamilyId = e.TileIndex, PhaseIndex = phase, HueCenter = h });
            }
            else
            {
                var archetype = em.CreateArchetype(typeof(PhaseDescriptor));
                var newEnt = em.CreateEntity(archetype);
                em.SetComponentData(newEnt, new PhaseDescriptor { FamilyId = e.TileIndex, PhaseIndex = phase, HueCenter = h });
                familyToEntity[e.TileIndex] = newEnt;
            }
        }

        // Save back into the asset so the PhaseIndex/HueCenter are persisted
        EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Classified", $"Assigned phases (0..{numPhases - 1}) for {obj.Entries.Count} tiles (persisted in asset and world).", "OK");
    }

    private void RunChromaPassOnSelected()
    {
        var obj = Selection.activeObject as PaintDots.ECS.ABCs.EdgeProfileAsset;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("No EdgeProfile Asset selected", "Select an EdgeProfile asset in the Project view.", "OK");
            return;
        }

        int cols = Mathf.Max(1, (obj.SourceTexture.width - obj.Margin + obj.Spacing) / (obj.TileWidth + obj.Spacing));
        int rows = Mathf.Max(1, (obj.SourceTexture.height - obj.Margin + obj.Spacing) / (obj.TileHeight + obj.Spacing));

        // Compute chroma compatibility per tile by comparing hue of matching edges to neighbors
        for (int ry = 0; ry < rows; ry++)
        {
            for (int rx = 0; rx < cols; rx++)
            {
                int idx = ry * cols + rx;
                if (idx >= obj.Entries.Count) continue;
                var e = obj.Entries[idx];
                float accum = 0f; int count = 0;
                // right neighbor
                if (rx + 1 < cols)
                {
                    var n = obj.Entries[idx + 1];
                    float d = Mathf.Abs(e.RightHSL.x - n.LeftHSL.x);
                    accum += 1f - Mathf.Clamp01(d);
                    count++;
                }
                // bottom neighbor
                if (ry + 1 < rows)
                {
                    var n = obj.Entries[idx + cols];
                    float d = Mathf.Abs(e.BottomHSL.x - n.TopHSL.x);
                    accum += 1f - Mathf.Clamp01(d);
                    count++;
                }

                e.ChromaCompatAvg = count > 0 ? accum / count : 0f;
            }
        }

        EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Chroma Pass", "Chroma compatibility computed and saved.", "OK");
    }

    private static void RGBToHSL(float r, float g, float b, out float h, out float s, out float l)
    {
        float max = Mathf.Max(r, Mathf.Max(g, b));
        float min = Mathf.Min(r, Mathf.Min(g, b));
        l = (max + min) / 2f;
        if (Mathf.Approximately(max, min))
        {
            h = 0f; s = 0f; return;
        }
        float d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
        if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
        else if (max == g) h = (b - r) / d + 2f;
        else h = (r - g) / d + 4f;
        h /= 6f;
    }
}
