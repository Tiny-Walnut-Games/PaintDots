using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.Runtime.ABCs
{
	/// <summary>
	/// Singleton component controlling the active phase/biome for AutoBioChroma swaps.
	/// </summary>
	public struct PhaseControl : IComponentData
	{
		public int PhaseIndex;
		public int BiomeId;
		public int NumPhases;
		public bool Dirty;
	}

	/// <summary>
	/// Component storing condensed edge/color data for a tile entry.
	/// </summary>
	public struct EdgeProfile : IComponentData
	{
		public int TileId;
		public int FamilyId;
		public int EdgeHash;
		public float3 AvgRGB;
	}

	/// <summary>
	/// Component describing a phase family assignment for AutoBioChroma.
	/// </summary>
	public struct PhaseDescriptor : IComponentData
	{
		public int FamilyId;
		public int PhaseIndex;
		public float HueCenter;
	}
}
