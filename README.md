# Unity DOTS Tilemap Painter (Pure ECS)

A lightweight Unity plugin for painting and managing tilemaps using **Entities (ECS/DOTS)**.  
No GameObject conversion, no Monobehaviours—just pure ECS workflows.

---

## ✨ Features
- Paint tiles directly into an Entity grid.
- Store tile data in **IComponentData** for fast iteration.
- Render tiles using **Entities Graphics / MaterialMeshInfo**.
- Support for multiple tile palettes and brushes.
- Extensible system for procedural or editor‑driven painting.

---

## 📦 Project Setup
1. **Unity Version**: Use Unity 2022.3+ with Entities Graphics package.
2. **Packages Required**:
   - `com.unity.entities`
   - `com.unity.entities.graphics`
   - `com.unity.mathematics`
3. Create a new folder in your project:  
   ```
   Packages/com.yourname.tilemap-painter
   ```

---

## 🏗️ Core Architecture

### 1. Tile Component
Each tile is represented as an entity with position + sprite/mesh reference.

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct Tile : IComponentData
{
    public int2 GridPosition;
    public int TileID; // Index into a palette
}
```

---

### 2. Tile Palette
Define a palette as a BlobAsset for efficient lookup.

```csharp
using Unity.Entities;
using UnityEngine;

public struct TilePalette
{
    public BlobArray<Entity> TileEntities; // References to tile prefabs
}
```

You can bake this once and reference it in systems.

---

### 3. Tilemap System
A system that listens for paint commands and spawns/updates tiles.

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
public partial struct TilemapPainterSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (command, entity) in SystemAPI.Query<RefRO<PaintCommand>>().WithEntityAccess())
        {
            // Handle paint command
        }
    }
}
```

---

### 4. Paint Command
A transient component to request painting.

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct PaintCommand : IComponentData
{
    public int2 GridPosition;
    public int TileID;
}
```

---

### 5. Rendering
Attach `MaterialMeshInfo` + `RenderMeshArray` to each tile entity.  
This lets Entities Graphics handle batching and rendering.

```csharp
state.EntityManager.AddSharedComponentManaged(e, new MaterialMeshInfo
{
    Material = myMaterial,
    Mesh = myMesh
});
```

---

## 🎨 Painting Workflow
1. Create a system or editor tool that spawns `PaintCommand` entities.
2. `TilemapPainterSystem` consumes commands and spawns tile entities.
3. Tiles are rendered via Entities Graphics.

---

## 🔌 Extending
- **Brushes**: Add systems that generate multiple `PaintCommand`s at once (e.g., rectangle fill, noise brush).
- **Eraser**: Query for `Tile` at a grid position and destroy the entity.
- **Serialization**: Store tilemap state in a BlobAsset for saving/loading.

---

## 🚀 Roadmap
- [ ] Editor window for painting in Scene view.  
- [ ] Support for animated tiles.  
- [ ] Chunked tilemaps for large worlds.  
- [ ] Burst‑compiled mesh generation for procedural tiles.  

---

## 📜 License
MIT—free to use, extend, and share.
