using System.Collections.Generic;
using UnityEngine;

namespace PaintDots.ECS.ABCs
{
    [System.Serializable]
    public class EdgeProfileEntry
    {
        public int TileIndex;
        public byte[] Top;    // length = tileWidth
        public byte[] Right;  // length = tileHeight
        public byte[] Bottom; // length = tileWidth
        public byte[] Left;   // length = tileHeight
        public Vector3 AvgRGB; // normalized 0..1
        // Classification result (assigned by Classifier)
        public int PhaseIndex = -1;
        public float HueCenter = 0f;
    // Resolved family id (assigned by adjacency resolver)
    public int FamilyId = -1;
        // Per-edge chroma (H,S,L) averages
        public Vector3 TopHSL = Vector3.zero;
        public Vector3 RightHSL = Vector3.zero;
        public Vector3 BottomHSL = Vector3.zero;
        public Vector3 LeftHSL = Vector3.zero;
        // Chroma compatibility metric aggregated across neighbors (0..1)
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
