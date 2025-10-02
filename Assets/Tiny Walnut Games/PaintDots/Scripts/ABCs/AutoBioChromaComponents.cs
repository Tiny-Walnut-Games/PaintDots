using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace PaintDots.ECS.ABCs
{
    // Simple singleton to control the global phase index and biome filter
    public struct PhaseControl : IComponentData
    {
        public int PhaseIndex;
        public int BiomeId;
        public int NumPhases;
        public bool Dirty;
    }

    // Edge profile placeholder - real implementation will use fixed lists and compressed data
    public struct EdgeProfile : IComponentData
    {
        public int TileId;
        public int FamilyId;
        // placeholder: hash representing edges
        public int EdgeHash;
        public float3 AvgRGB;
    }

    // Phase descriptor placeholder
    public struct PhaseDescriptor : IComponentData
    {
        public int FamilyId;
        public int PhaseIndex;
        public float HueCenter;
    }
}
