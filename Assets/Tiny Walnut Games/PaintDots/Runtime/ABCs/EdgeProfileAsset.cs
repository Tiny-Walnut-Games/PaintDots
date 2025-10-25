using System.Collections.Generic;
using UnityEngine;

namespace PaintDots.Runtime.ABCs
{
	[System.Serializable]
	public class EdgeProfileEntry
	{
		public int TileIndex;
		public byte[] Top;
		public byte[] Right;
		public byte[] Bottom;
		public byte[] Left;
		public Vector3 AvgRGB;
		public int PhaseIndex = -1;
		public float HueCenter = 0f;
		public int FamilyId = -1;
		public Vector3 TopHSL = Vector3.zero;
		public Vector3 RightHSL = Vector3.zero;
		public Vector3 BottomHSL = Vector3.zero;
		public Vector3 LeftHSL = Vector3.zero;
		public float ChromaCompatAvg = -1f;
	}

	public class EdgeProfileAsset : ScriptableObject
	{
		public Texture2D SourceTexture;
		public int TileWidth;
		public int TileHeight;
		public int Margin;
		public int Spacing;
		public List<EdgeProfileEntry> Entries = new List<EdgeProfileEntry>();
	}
}
