# PaintDots - Assembly Definitions

This folder contains Unity Assembly Definition files that organize the codebase into separate assemblies for better compile times and dependency management.

## Assembly Structure

- **PaintDots.Runtime.asmdef**: Core runtime ECS components and systems
- **PaintDots.Editor.asmdef**: Editor tools and windows
- **PaintDots.AutoTile.asmdef**: AutoTile integration components

Each assembly is properly configured with the necessary package dependencies for Unity ECS/DOTS.