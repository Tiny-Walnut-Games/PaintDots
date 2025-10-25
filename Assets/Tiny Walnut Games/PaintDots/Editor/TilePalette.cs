using System.Collections.Generic;
using UnityEngine;

namespace PaintDots.Editor
{
    [CreateAssetMenu(menuName = "PaintDots/Tile Palette", fileName = "TilePalette")]
    public sealed class TilePalette : ScriptableObject
    {
        public Texture2D Texture;
        public int TileWidth = 16;
        public int TileHeight = 16;
        public int Margin = 0;
        public int Spacing = 0;

        // Normalized rects (x,y,w,h) in 0..1 coordinates for DrawTextureWithTexCoords
        public List<Rect> Tiles = new List<Rect>();

        // Optional tile id mapping; if empty, index is used as tile id
        public List<int> TileIDs = new List<int>();

        // Optional per-slot mapping to an AutoTile or RuleTile asset (ScriptableObject). Use Object so we accept multiple types.
        public List<UnityEngine.Object> SlotAutoTileAssets = new List<UnityEngine.Object>();

        public void GenerateFromGrid(Texture2D tex, int tileW, int tileH, int margin = 0, int spacing = 0)
        {
            Texture = tex;
            TileWidth = tileW;
            TileHeight = tileH;
            Margin = margin;
            Spacing = spacing;

            Tiles.Clear();
            TileIDs.Clear();
            SlotAutoTileAssets.Clear();

            if (tex == null || tileW <= 0 || tileH <= 0) return;

            int cols = Mathf.Max(1, (tex.width - margin + spacing) / (tileW + spacing));
            int rows = Mathf.Max(1, (tex.height - margin + spacing) / (tileH + spacing));

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int px = margin + x * (tileW + spacing);
                    int py = margin + y * (tileH + spacing);

                    // Convert to normalized UVs (GUI.DrawTextureWithTexCoords uses bottom-left origin)
                    float nx = (float)px / tex.width;
                    // Flip Y so rows are generated top-to-bottom visually when drawn with GUI (top-left authored sheets)
                    float ny = 1f - ((float)(py + tileH) / tex.height);
                    float nw = (float)tileW / tex.width;
                    float nh = (float)tileH / tex.height;

                    Rect uv = new Rect(nx, ny, nw, nh);
                    Tiles.Add(uv);
                    TileIDs.Add(Tiles.Count - 1);
                    SlotAutoTileAssets.Add(null);
                }
            }
        }

        /// <summary>
        /// Generate tiles from a rectangular region inside the texture (pixel coordinates). px,py are pixel coords from top-left.
        /// </summary>
        public void GenerateFromRegion(Texture2D tex, int regionPx, int regionPyTop, int regionWidth, int regionHeight, int tileW, int tileH, int margin = 0, int spacing = 0)
        {
            Texture = tex;
            TileWidth = tileW;
            TileHeight = tileH;
            Margin = margin;
            Spacing = spacing;

            Tiles.Clear();
            TileIDs.Clear();
            SlotAutoTileAssets.Clear();

            if (tex == null || tileW <= 0 || tileH <= 0) return;

            // regionPx, regionPyTop: px from left, py from top
            // convert top-based py to bottom-based origin for iteration
            int regionPxLeft = regionPx;
            int regionPyBottom = tex.height - (regionPyTop + regionHeight);

            int cols = Mathf.Max(1, (regionWidth - margin + spacing) / (tileW + spacing));
            int rows = Mathf.Max(1, (regionHeight - margin + spacing) / (tileH + spacing));

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int px = regionPxLeft + x * (tileW + spacing) + margin;
                    int py = regionPyBottom + y * (tileH + spacing) + margin; // bottom-based

                    float nx = (float)px / tex.width;
                    float ny = (float)py / tex.height;
                    float nw = (float)tileW / tex.width;
                    float nh = (float)tileH / tex.height;

                    // GUI.DrawTextureWithTexCoords expects v origin at bottom-left. We can use the bottom-based values directly.
                    Rect uv = new Rect(nx, ny, nw, nh);
                    Tiles.Add(uv);
                    TileIDs.Add(Tiles.Count - 1);
                    SlotAutoTileAssets.Add(null);
                }
            }
        }
    }
}
