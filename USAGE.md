# PaintDots Usage Guide

This guide covers how to use the advanced features of PaintDots Unity ECS Tilemap Painter.

## 🎨 Brush System

### Basic Usage
```csharp
using PaintDots.ECS.Utilities;
using Unity.Entities;
using Unity.Mathematics;

// Create a brush configuration
var brush = new BrushConfig(
    BrushType.NoisePattern,    // Brush type
    radius: 5,                 // Brush size
    tileID: 1,                 // Tile to paint
    useAutoTile: true,         // Enable auto-tiling
    noiseThreshold: 0.4f,      // Noise density (0.0-1.0)
    noiseSeed: 12345          // Random seed
);

// Apply brush using Entity Command Buffer
var ecb = // ... get ECB from system
BrushSystem.ApplyBrush(ecb, gridPosition, brush);
```

### Available Brush Types
- `BrushType.Single` - Paint single tile
- `BrushType.Square3x3` - 3x3 square pattern
- `BrushType.Square5x5` - 5x5 square pattern  
- `BrushType.Circle` - Filled circle
- `BrushType.RectangleFill` - Filled rectangle
- `BrushType.NoisePattern` - Custom noise pattern
- `BrushType.NoiseSparse` - Sparse random pattern
- `BrushType.NoiseDense` - Dense random pattern

## 🧹 Eraser System

### Creating Erase Commands
```csharp
using PaintDots.ECS.Utilities;

// Erase single tile
TilemapUtilities.CreateEraseCommand(ecb, gridPosition);

// Erase rectangular area
TilemapUtilities.EraseRectangle(ecb, minPos, maxPos);

// Erase circular area
TilemapUtilities.EraseCircle(ecb, centerPos, radius);
```

## 💾 Serialization System

### Saving Tilemap State
```csharp
using PaintDots.ECS.Systems;
using Unity.Entities;

// Save current tilemap to BlobAsset
var stateAsset = TilemapSerializationSystem.SerializeTilemap(
    entityManager, 
    Allocator.Persistent
);

// Store reference in component
var entity = entityManager.CreateEntity();
entityManager.AddComponent(entity, new TilemapStateComponent(stateAsset));
```

### Loading Tilemap State
```csharp
// Load tilemap from BlobAsset
var stateComponent = entityManager.GetComponent<TilemapStateComponent>(entity);
TilemapSerializationSystem.DeserializeTilemap(entityManager, stateComponent.StateAsset);
```

## 🎬 Animated Tiles

### Creating Animated Tiles
```csharp
using Unity.Entities;
using Unity.Collections;

// Create animation data
using var builder = new BlobBuilder(Allocator.Temp);
ref var animData = ref builder.ConstructRoot<AnimatedTileData>();

// Define animation frames
var frames = builder.Allocate(ref animData.Frames, 4);
frames[0] = new AnimationFrame(spriteIndex: 0, duration: 0.25f);
frames[1] = new AnimationFrame(spriteIndex: 1, duration: 0.25f);
frames[2] = new AnimationFrame(spriteIndex: 2, duration: 0.25f);
frames[3] = new AnimationFrame(spriteIndex: 3, duration: 0.25f);

animData.TotalDuration = 1.0f;
animData.Loop = true;

var blobAsset = builder.CreateBlobAssetReference<AnimatedTileData>(Allocator.Persistent);

// Add to tile entity
entityManager.AddComponent(tileEntity, new AnimatedTile(blobAsset));
```

## 🗺️ Chunked Tilemaps

### Chunk Configuration
```csharp
using PaintDots.ECS.Utilities;

// Convert world position to chunk coordinates
var chunkSize = new int2(32, 32);
var chunkCoords = ChunkUtilities.WorldToChunk(gridPosition, chunkSize);

// Create tiles in appropriate chunks
var tileEntity = ChunkUtilities.CreateChunkedTile(ecb, gridPosition, tileID, chunkSize);
```

### Chunk Management
The `ChunkManagementSystem` automatically:
- Creates chunks as needed
- Culls distant chunks based on view distance
- Enables/disables chunk tiles for performance

## 🔧 Editor Integration

### Using the Tilemap Painter Window
1. Open **Window → PaintDots → Tilemap Painter**
2. Select tile ID to paint
3. **Left Click** to paint tiles
4. **Right Click** to erase tiles
5. Use different brush modes for various patterns

### Custom Editor Tools
```csharp
[MenuItem("Tools/Paint Rectangle")]
static void PaintCustomRectangle()
{
    var world = World.DefaultGameObjectInjectionWorld;
    var ecb = world.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>()
        .CreateCommandBuffer();
    
    var brush = new BrushConfig(BrushType.RectangleFill, 10, 1);
    BrushSystem.ApplyBrush(ecb, new int2(0, 0), brush);
}
```

## 🏗️ ECS Best Practices

### Performance Tips
- Use EntityCommandBuffer for all entity operations
- Batch operations when possible using `BatchPaint`
- Enable Burst compilation for custom systems
- Use `DynamicBuffer` instead of `NativeArray` for runtime data

### Code Guidelines
- Always use `readonly struct` for components
- Seal all new types and systems
- Avoid hard-coded parameters - use config components
- Never use refs in Burst-compiled code
- Minimize sync points between systems

## 🚀 Advanced Usage

### Custom Noise Functions
```csharp
// Custom noise brush with multiple octaves
public static void PaintCustomNoise(EntityCommandBuffer ecb, int2 center, int radius, int tileID)
{
    var random = Unity.Mathematics.Random.CreateFromIndex(42);
    
    for (int x = center.x - radius; x <= center.x + radius; x++)
    {
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            var pos = new int2(x, y);
            var distance = math.distance(new float2(center.x, center.y), new float2(x, y));
            
            if (distance <= radius)
            {
                // Multi-octave noise
                var noise1 = noise.cnoise(new float2(x * 0.1f, y * 0.1f));
                var noise2 = noise.cnoise(new float2(x * 0.05f, y * 0.05f)) * 0.5f;
                var combinedNoise = (noise1 + noise2) * 0.5f;
                
                if (combinedNoise > 0.3f)
                {
                    TilemapUtilities.CreatePaintCommand(ecb, pos, tileID);
                }
            }
        }
    }
}
```

### Performance Monitoring
- Use Unity Profiler to monitor system performance
- Check chunk activation/deactivation frequency
- Monitor GC allocations in serialization operations
- Use Unity's ECS Debugger to inspect entity data

For more advanced usage patterns and performance optimization, see the Unity ECS documentation.