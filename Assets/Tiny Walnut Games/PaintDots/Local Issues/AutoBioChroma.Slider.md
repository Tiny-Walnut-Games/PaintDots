# Add AutoBioChroma Slider (ABCs) for biome phase-aware autotiling

## Summary

Introduce the AutoBioChroma Slider (ABCs): a data-to-art bridge that binds MetVanDAMN’s biome data to tile art via phase-aware autotiling. ABCs parses alpha edges and chroma signatures from tiles, groups them into “Tile Families,” and enables live, in-editor and runtime swapping of biome color phases without repainting. Paint once with blob logic; re-theme infinitely with a single slider gesture.

---

## Goals and non-goals

### Goals

- **Automate rule generation:** Parse edge alpha and adjacency to auto-create RuleTile-style compatibility.
- **Phase-aware skinning:** Group tiles by color phase (HSL bands) so a slider swaps an entire biome’s skin at once.
- **Data-first pipeline:** Keep ECS clean; defer materials and sprites until binding time via lookup tables.
- **Live swapping:** Support instant phase shifts in-editor and at runtime (global or zone-scoped).
- **Debug-first UX:** Provide visualization modes (IDs, edges, compatibility heatmaps) before committing art.
- **Deterministic mapping:** One Tile Family ID maps to N phase variants with stable indices and reproducible results.

### Non-goals

- **No hand-authored RuleTiles:** The system learns adjacency from images and metadata; manual rules are optional.
- **No per-pixel shaders initially:** Start asset-driven; add shader blending later as an enhancement.
- **No artist asset splitting enforcement:** Support messy mega-sheets; do not require source art reorganization to start.

---

## User stories

- **As a designer,** I can paint a biome using blob logic and then slide through color phases to re-theme the entire tilemap instantly.
- **As an artist,** I can drop a sprite sheet into the palette, and the editor auto-detects edges and phase families so I don’t have to author RuleTiles.
- **As a developer,** I can keep world entities material-agnostic until a single “bind” pass assigns art, and later rebind to new phases without touching the data.
- **As a tester,** I can toggle debug views (IDs, edge masks, heatmaps) to validate adjacency and phase grouping before shipping.

---

## Architecture overview

### Components

- **TileData:** TileFamilyID, BlobSignatureID, BiomeID, PhaseIndex, ChunkID.
- **TileRenderBinding:** SpriteSheetID, MaterialID, UVRegion, SortLayer.
- **PhaseLibrary:** [TileFamilyID → PhaseIndex → SpriteAtlas region + MaterialID].
- **EdgeProfile:** Per-edge alpha vector, normalized color signature.
- **BiomeProfile:** BiomeID, hue/sat/light ranges, adjacency policies, exclusions.

### Systems

- **AlphaEdgeParserSystem:** Extract 1px edge strips, compute alpha profile vectors and average color signatures.
- **AdjacencyResolverSystem:** Build compatibility matrix per TileFamily using edge complementarity.
- **PhaseClassifierSystem:** Quantize tile hues into phases; assign PhaseIndex per TileFamily variant.
- **PaletteBindingSystem:** Bind TileFamilyID + PhaseIndex to concrete SpriteSheet regions (editor/runtime).
- **ABCSwapSystem:** Apply global/zone phase changes by swapping PhaseIndex mapping (no entity churn).
- **DebugVizSystem:** Heatmap overlays for compatibility, phase colors, and unresolved edges.

### Data flow

1. Import sprite sheet → AlphaEdgeParser → EdgeProfile.
2. EdgeProfile → AdjacencyResolver → Compatibility matrices.
3. EdgeProfile (color) → PhaseClassifier → Phase families.
4. Biome data (MetVanDAMN) → PaletteBinding → TileRenderBinding.
5. Slider/trigger → ABCSwap → rebind PhaseIndex → live scene update.

---

## Technical design

### Edge parsing and adjacency

- **Per-edge sampling:** For each tile, sample a 1px strip on top/bottom/left/right.
- **Alpha profile:** Thresholded vector per edge; optionally store gradient strength.
- **Complement rules:** Right edge of A matches left edge of B if alpha profiles are complementary within tolerance.
- **Corner inference:** Optional corner masks derived from edge intersections for inner/outer corners.

```csharp
public struct EdgeProfile {
    public FixedList512Bytes<byte> TopAlpha;   // normalized 0..255
    public FixedList512Bytes<byte> RightAlpha;
    public FixedList512Bytes<byte> BottomAlpha;
    public FixedList512Bytes<byte> LeftAlpha;
    public float3 AvgRGB; // normalized 0..1
}

public struct Compatibility {
    public int TileIdA;
    public int TileIdB;
    public Direction EdgeA; // Right
    public Direction EdgeB; // Left
    public float Score; // 0..1 similarity
}
```

### Color-phase classification

- **Convert to HSL:** Average RGB of the edge strips → HSL.
- **Quantize hue:** e.g., 12 bins; configurable per biome.
- **Phase families:** Group tiles by TileFamilyID and PhaseIndex (hue bin + optional sat/light band).

```csharp
public struct PhaseDescriptor {
    public int TileFamilyId;
    public int PhaseIndex; // quantized hue band
    public float HueCenter;
    public float HueTolerance;
    public float SatMin, SatMax;
    public float LightMin, LightMax;
}
```

### Palette binding and swapping

- **Lookup table:** TileFamilyID + PhaseIndex → SpriteAtlas region + MaterialID.
- **Deferred binding:** Keep TileRenderBinding empty until a bind pass sets sprites/materials.
- **Live swap:** Change the lookup mapping; systems update TileRenderBinding in-place.

```csharp
public struct PhaseLibrary {
    public BlobAssetReference<PhaseLibraryData> Data;
}

public struct PhaseLibraryData {
    public NativeHashMap<int, PhaseFamily>; // TileFamilyId → PhaseFamily
}

public struct PhaseFamily {
    public FixedList64Bytes<PhaseVariant> Variants; // index = PhaseIndex
}

public struct PhaseVariant {
    public int SpriteSheetId;
    public Rect UV; // atlas region
    public int MaterialId;
}
```

### ECS flow (minimal)

- **Archetype:** TileData, EdgeProfile (optional after import), TileRenderBinding (late).
- **Import pass:** Populate EdgeProfile and register TileFamilyIDs.
- **Bind pass:** For all TileData, read PhaseLibrary → write TileRenderBinding.
- **Swap pass:** Update PhaseIndex globally or per-zone → refresh TileRenderBinding.

---

## Editor UX and runtime behavior

### Editor tools

- **ABCs slider:** Global slider scrubs PhaseIndex (with optional per-biome filters).
- **Palette grid buttons:** Thumbnail-over-button trick; syncs all tiles to current PhaseIndex family.
- **Debug toggles:**
  - **IDs view:** Color by TileFamilyID.
  - **Edges view:** Show alpha masks per edge.
  - **Heatmap view:** Adjacency score visualization.
- **“Bind Art” button:** One-pass apply of materials/sprites to current scene.

### Runtime options

- **Global phase shift:** Season/corruption/dream filter toggles PhaseIndex globally.
- **Zone-scoped phase:** Trigger volumes or biome nodes shift PhaseIndex locally.
- **Blended transitions:** Optional shader lerp between two PhaseIndex states over duration.
- **Performance:** Data unchanged; only bindings swap—low overhead, chunk-friendly.

---

## Testing and acceptance criteria

### Unit tests

- **Edge parsing:** Given known alpha patterns, profiles match expected vectors.
- **Adjacency:** Compatibility matrices reflect complementary edges within tolerance.
- **Phase classification:** Known HSL values land in correct PhaseIndex bins.
- **Lookup stability:** TileFamilyID + PhaseIndex maps deterministically to the same variant.

### Integration tests

- **Bind pass:** Applying PhaseLibrary populates TileRenderBinding for a sample scene.
- **Swap pass:** Moving the slider updates all rendered tiles without entity churn or leaks.
- **Zone overrides:** Entering/exiting triggers apply local phase changes correctly.
- **Debug views:** IDs/edges/heatmap render accurately and toggle cleanly.

### Acceptance criteria

- **AC1:** Importing a sprite sheet auto-generates EdgeProfiles, Phase families, and compatibility matrices.
- **AC2:** The ABCs slider re-themes an entire painted biome in-editor instantly.
- **AC3:** Runtime global and zone-scoped swaps function without rebuilds or frame stutter.
- **AC4:** A “Bind Art” pass cleanly assigns materials/sprites; a “Unbind/Debug” mode restores data-only visuals.
- **AC5:** Documentation and tooltips explain blob rules, phase bands, and debug views.

---

## Tasks and checklist

### Implementation

- **Parser:** Implement AlphaEdgeParserSystem and color signature extraction.
- **Resolver:** Implement AdjacencyResolverSystem with compatibility scoring and tolerance.
- **Classifier:** Implement PhaseClassifierSystem (HSL quantization, biome-configurable bands).
- **Library:** Define PhaseLibrary data model and authoring UI.
- **Binding:** Implement PaletteBindingSystem and ABCSwapSystem (global + zone scope).
- **Debug:** Implement DebugVizSystem (IDs, edges, heatmap) and toggles.

### Editor/UX

- **Slider UI:** Global ABCs slider with biome filter dropdown.
- **Palette grid:** Thumbnail-over-button with phase sync; “Reset Paint” and “Bind Art” actions.
- **Inspector:** Per-biome phase band configuration (bins, tolerances).
- **Tooltips:** Inline help for blob rules and phase families.

### Runtime

- **Trigger volumes:** Components for zone-scoped PhaseIndex overrides.
- **Blend option:** Optional shader lerp path; configurable durations and curves.
- **Performance audit:** Chunk-size alignment and memory footprint validation.

### Docs

- **README section:** “ABCs: Paint Once, Re-Theme Forever.”
- **Style bible note:** Blob edge discipline and color-phase authoring tips for artists.
- **Changelog entry:** MetVanDAMN: “AutoBioChroma Slider introduced.”

- [ ] Parser implemented and tested
- [ ] Resolver implemented and tested
- [ ] Classifier implemented and tested
- [ ] PhaseLibrary authoring UI
- [ ] Binding and swap systems
- [ ] Debug visualization modes
- [ ] Editor slider + palette sync
- [ ] Runtime triggers + blending
- [ ] Documentation and tooltips
- [ ] Performance and memory validation

---

## Risks, mitigations, and dependencies

- **Risk:** Messy mega-sheets with inconsistent edges.  
  - **Mitigation:** Debug views + tolerance sliders; fallback to manual overrides per tile.
- **Risk:** Hue bins too coarse for certain palettes.  
  - **Mitigation:** Per-biome band configs; allow custom PhaseIndex maps.
- **Risk:** Material thrash during swaps.  
  - **Mitigation:** Late binding pass; shared materials per phase; batching-friendly atlases.
- **Dependency:** Stable TileFamilyID assignment from import step.  
  - **Mitigation:** Deterministic hashing of source rect + edge profile.

---

## Glossary

- **TileFamilyID:** Logical group of tiles sharing blob logic; independent of color phase.
- **PhaseIndex:** Chromatic band identifier (e.g., hue bin) for re-skinning.
- **Blob logic:** Edge alpha-based adjacency discipline used to infer connectivity.
- **ABCs:** AutoBioChroma Slider—global control for phase-aware re-theming.

---

## Suggested changelog entry

- **MetVanDAMN vNext:** Added AutoBioChroma Slider (ABCs). Data-first worldcasting now binds to art via phase-aware autotiling. Paint once with blob logic; re-theme infinitely by sliding biome phases live in the editor or at runtime. Debug overlays for IDs, edges, and adjacency heatmaps included.
