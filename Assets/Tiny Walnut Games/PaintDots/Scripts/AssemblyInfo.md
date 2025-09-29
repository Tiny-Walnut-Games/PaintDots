# PaintDots - Assembly Definitions

This folder contains Unity Assembly Definition files that organize the codebase into separate assemblies for better compile times and dependency management.

## Assembly Structure

### PaintDots.Runtime.asmdef

## Core runtime ECS components and systems

Dependencies:

- Unity.Entities
- Unity.Entities.Graphics  
- Unity.Mathematics
- Unity.Collections
- Unity.Burst
- Unity.Transforms

Contains:

- Core tile components (Tile, PaintCommand, EraseCommand, AutoTile)
- Animation system (AnimatedTile, AnimationFrame, AnimatedTileData)
- Serialization components (TilemapStateAsset, SerializedTile)
- Chunk system components (TilemapChunk, ChunkTile, TileChunkReference)
- All ECS systems (TilemapPainterSystem, TilemapEraserSystem, AnimatedTileSystem, etc.)
- Utility classes (TilemapUtilities, BrushSystem, ChunkUtilities)

### PaintDots.Editor.asmdef  

## Editor tools and windows

Dependencies:

- Unity.Entities
- Unity.Entities.Editor
- Unity.Mathematics
- Unity.Collections
- PaintDots.Runtime

Contains:

- TilemapPainterWindow - Scene view painting editor
- Custom inspectors for authoring components
- Editor-only utilities and tools

## Features by Assembly

### Runtime Assembly Features

- ✅ Enhanced brush system with noise patterns
- ✅ Command-based eraser system  
- ✅ BlobAsset serialization for save/load
- ✅ Frame-based tile animation system
- ✅ Chunked tilemap architecture
- ✅ Burst-compiled procedural mesh generation
- ✅ AutoTile integration with rule-based sprites

### Editor Assembly Features  

- ✅ Scene view painting tools
- ✅ Multi-brush support in editor
- ✅ Real-time tile preview
- ✅ Erase tool with area selection
- ✅ Custom inspectors for configuration

## ECS Best Practices

All assemblies follow Unity ECS best practices:

- **Sealed types**: All components and systems are sealed
- **Burst compilation**: Systems optimized with BurstCompile attribute
- **EntityCommandBuffer**: Minimal direct EntityManager usage  
- **Value types**: No managed references in ECS components
- **Proper dispose**: All NativeContainers properly disposed
- **Query optimization**: Efficient entity queries with proper filters

## Build Configuration

- **Runtime**: Supports all platforms, allows unsafe code for performance
- **Editor**: Editor-only assembly, no unsafe code needed
- **Auto-referenced**: Runtime assembly is auto-referenced for easy use
- **Version defines**: Support for conditional compilation based on package versions

Each assembly is properly configured with the necessary package dependencies for Unity ECS/DOTS and follows Unity's recommended assembly organization patterns for optimal performance and maintainability.
