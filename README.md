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

## 🏠 Multi‑Tile Entity Support

### Footprint Component
For larger structures that span multiple tiles, use the `Footprint` component:

```csharp
public struct Footprint : IComponentData
{
    public int2 Origin;      // anchor tile (e.g. bottom-left)
    public int2 Size;        // width/height in tiles
}
```

### Enhanced Paint Commands
The `PaintCommand` now supports both single and multi-tile placement:

```csharp
// Single tile
var singleTile = PaintCommand.SingleTile(new int2(5, 5), tileID: 1);

// Multi-tile structure (3x2 house)
var house = PaintCommand.MultiTile(new int2(10, 5), tileID: 2, new int2(3, 2));
```

### Structure Placement Workflow
1. **Occupancy Check**: System validates the footprint area is free.
2. **Entity Creation**: Spawns one entity representing the entire structure.
3. **Grid Marking**: Fills `DynamicBuffer<OccupiedCell>` with covered positions.
4. **Rendering**: Single mesh prefab positioned at origin.

```csharp
public struct OccupiedCell : IBufferElementData
{
    public int2 Position;
}
```

### Example: Placing a 3×2 House
```csharp
// Create paint command for house at (10,5)
var houseCommand = PaintCommand.MultiTile(new int2(10, 5), houseID, new int2(3, 2));

// System will:
// 1. Check grid cells (10,5) through (12,6) are free
// 2. Create HouseEntity with Footprint { Origin=(10,5), Size=(3,2) }
// 3. Fill OccupiedCell buffer with all covered positions
// 4. Position and render the structure prefab
```

---

## 🎨 Painting Workflow
1. Create a system or editor tool that spawns `PaintCommand` entities.
2. `TilemapPainterSystem` consumes commands and spawns tile entities.
3. Tiles are rendered via Entities Graphics.

---

## 🔌 Extending
- ✅ **Brushes**: Multiple brush systems with rectangle fill, noise patterns (sparse/dense), and custom configurations.
- ✅ **Eraser**: Efficient EraseCommand system with rectangular and circular erase patterns.
- ✅ **Serialization**: Complete BlobAsset-based tilemap state serialization for saving/loading.

---

## 🚀 Features Completed
- ✅ **Editor window for painting in Scene view** - Full editor integration with painting and erasing tools.
- ✅ **Support for animated tiles** - AnimatedTile component with frame-based animation system.
- ✅ **Chunked tilemaps for large worlds** - Chunk-based architecture with culling and streaming.
- ✅ **Burst-compiled mesh generation for procedural tiles** - ProceduralMeshSystem with full Burst compilation.

### 🔧 Advanced Systems
- **Multi-Brush Support**: Single, Square (3x3, 5x5), Circle, Rectangle Fill, Noise Pattern variants
- **Chunk Management**: Automatic chunk loading/unloading based on distance with configurable parameters
- **Animation Framework**: Timeline-based tile animation with looping, color changes, and sprite sequences  
- **Serialization**: Complete save/load system using Unity's BlobAsset for efficient data storage
- **Enhanced Eraser**: Command-based erasing with support for patterns and areas

### 🏗️ CI/CD Infrastructure
- **Unity CI**: Multi-platform builds (Windows, macOS, Linux) with automated testing
- **Security**: CodeQL analysis, vulnerability scanning, and dependency monitoring
- **Updates**: Dependabot for automated dependency updates + manual update bot with dry-run support
- **Quality**: Comprehensive workflows following Unity best practices with secrets management  

---

## 📜 License
MIT—free to use, extend, and share.
