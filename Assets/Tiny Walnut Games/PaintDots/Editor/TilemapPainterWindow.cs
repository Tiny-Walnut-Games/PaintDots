using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using PaintDots.ECS;
using PaintDots.ECS.Utilities;
using PaintDots.ECS.Config;
using System;
using System.Reflection;
using System.IO;
using System.Linq;

namespace PaintDots.Editor
{
    /// <summary>
    /// Editor window for painting tiles in Scene view using ECS
    /// </summary>
    public sealed class TilemapPainterWindow : EditorWindow
    {
    private int _selectedTileID = 0;
    private bool _isPainting = false;
    private World _world;
    private EntityManager _entityManager;

    // Palette UI
    private int _paletteStart = 0;
    private int _paletteCount = 16;
    private int _paletteColumns = 8;
    private Vector2 _paletteScroll;
    private PaintDots.Editor.TilePalette _selectedPalette;
    private Texture2D _paletteTexturePreview;
    private int _selectedPaletteIndex = -1;
    // Preview selection for region-based palette creation
    private bool _isPreviewSelecting = false;
    private Vector2 _previewSelStart;
    private Vector2 _previewSelEnd;
    // Source texture for palette creation (persist between OnGUI calls)
    private Texture2D _createSourceTexture;
    // Last preview rect used to draw the texture (used for accurate coordinate mapping)
    private Rect _lastPreviewRect;
    // Persisted create palette settings
    private int _createTileWidth = 16;
    private int _createTileHeight = 16;
    private int _createMargin = 0;
    private int _createSpacing = 0;
    // Selection tracked in texture pixel coordinates so pan/zoom don't move the anchor
    private Vector2 _selectionStartTex;
    private Vector2 _selectionEndTex;
    private bool _snapToGrid = true;
    // Zoom & pan for preview
    private float _previewZoom = 1f; // multiplier on top of fit scale
    private Vector2 _previewPan = Vector2.zero; // in texture pixels
    private bool _isPanning = false;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;

    // Brush settings
    private BrushType _brushType = BrushType.Single;
    private int _brushSize = 1;
    private float _noiseThreshold = 0.5f;
    private int _noiseSeed = 12345;
    private bool _useAutoTile = false;
    // Overlay options
    private bool _showEdgeProfileOverlays = false;
    private PaintDots.ECS.ABCs.EdgeProfileAsset _loadedEdgeProfile;
        
        [MenuItem("Window/PaintDots/Tilemap Painter")]
        public static void ShowWindow()
        {
            GetWindow<TilemapPainterWindow>("Tilemap Painter");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            
            // Get the default world and entity manager
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != default)
            {
                _entityManager = _world.EntityManager;
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tilemap Painter", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Painting Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected Tile ID", GUILayout.Width(110));
            _selectedTileID = EditorGUILayout.IntField(_selectedTileID, GUILayout.Width(60));
            if (GUILayout.Button("Reset", GUILayout.Width(60))) _selectedTileID = 0;
            EditorGUILayout.EndHorizontal();

            // Palette (ScriptableObject support)
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            _selectedPalette = (PaintDots.Editor.TilePalette)EditorGUILayout.ObjectField("Tile Palette", _selectedPalette, typeof(PaintDots.Editor.TilePalette), false);

            if (_selectedPalette != null && _selectedPalette.Texture != null)
            {
                _paletteTexturePreview = _selectedPalette.Texture;
                // Display tiles from palette
                int total = _selectedPalette.Tiles != null ? _selectedPalette.Tiles.Count : 0;
                int cols = Mathf.Max(1, _paletteColumns);
                int rows = Mathf.CeilToInt((float)total / cols);

                _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.Height(80));
                int tid = 0;
                for (int r = 0; r < rows; r++)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int c = 0; c < cols; c++)
                    {
                        if (tid >= total) break;
                        var uv = _selectedPalette.Tiles[tid];
                        if (GUILayout.Button("", GUILayout.Width(40), GUILayout.Height(40)))
                        {
                            _selectedTileID = (tid < _selectedPalette.TileIDs.Count) ? _selectedPalette.TileIDs[tid] : tid;
                            _selectedPaletteIndex = tid;
                        }

                        // Draw thumbnail overlay
                        var lastRect = GUILayoutUtility.GetLastRect();
                        if (_paletteTexturePreview != null)
                        {
                            GUI.DrawTextureWithTexCoords(lastRect, _paletteTexturePreview, uv);
                        }

                        // Draw a small slot indicator for AutoTile mapping
                        if (_selectedPalette.SlotAutoTileAssets != null && tid < _selectedPalette.SlotAutoTileAssets.Count && _selectedPalette.SlotAutoTileAssets[tid] != null)
                        {
                            var badgeRect = new Rect(lastRect.xMax - 12, lastRect.yMin + 2, 10, 10);
                            EditorGUI.DrawRect(badgeRect, Color.cyan);
                            if (Event.current.type == EventType.MouseDown && badgeRect.Contains(Event.current.mousePosition))
                            {
                                // open the assigned asset
                                Selection.activeObject = _selectedPalette.SlotAutoTileAssets[tid];
                            }
                        }

                        tid++;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                // Per-slot AutoTile assignment UI
                if (_selectedPaletteIndex >= 0 && _selectedPaletteIndex < _selectedPalette.Tiles.Count)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Slot {_selectedPaletteIndex}", EditorStyles.boldLabel);
                    UnityEngine.Object current = null;
                    if (_selectedPalette.SlotAutoTileAssets != null && _selectedPaletteIndex < _selectedPalette.SlotAutoTileAssets.Count)
                        current = _selectedPalette.SlotAutoTileAssets[_selectedPaletteIndex];

                    var assigned = EditorGUILayout.ObjectField("AutoTile Asset", current, typeof(UnityEngine.Object), false);
                    if (_selectedPalette.SlotAutoTileAssets == null)
                        _selectedPalette.SlotAutoTileAssets = new System.Collections.Generic.List<UnityEngine.Object>();

                    // ensure list size
                    while (_selectedPalette.SlotAutoTileAssets.Count <= _selectedPaletteIndex)
                        _selectedPalette.SlotAutoTileAssets.Add(null);

                    if (assigned != _selectedPalette.SlotAutoTileAssets[_selectedPaletteIndex])
                    {
                        _selectedPalette.SlotAutoTileAssets[_selectedPaletteIndex] = assigned;
                        EditorUtility.SetDirty(_selectedPalette);
                        UnityEditor.AssetDatabase.SaveAssets();
                    }
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Generate AutoTile Assets for Palette"))
                {
                    // determine palette asset path
                    string assetPath = AssetDatabase.GetAssetPath(_selectedPalette);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        CreateAutoTilesForPalette(_selectedPalette, assetPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Asset Path", "Select a palette asset saved in the project to generate AutoTile assets next to it.", "OK");
                    }
                }
            }
            else
            {
                // Fallback numeric palette
                _paletteStart = EditorGUILayout.IntField("Start ID", _paletteStart);
                _paletteCount = EditorGUILayout.IntField("Count", _paletteCount);
                _paletteColumns = EditorGUILayout.IntField("Columns", _paletteColumns);

                _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.Height(60));
                int rows = Mathf.CeilToInt((float)_paletteCount / Mathf.Max(1, _paletteColumns));
                int id = _paletteStart;
                for (int r = 0; r < rows; r++)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int c = 0; c < _paletteColumns; c++)
                    {
                        if (id >= _paletteStart + _paletteCount) break;
                        if (GUILayout.Button(id.ToString(), GUILayout.Width(40)))
                        {
                            _selectedTileID = id;
                        }
                        id++;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            // Create palette from texture helper
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Create Palette", EditorStyles.boldLabel);
            UnityEngine.Object sourceObj = EditorGUILayout.ObjectField("Source Texture/Sprite", _createSourceTexture, typeof(UnityEngine.Object), false);
            // Resolve sprite -> texture or texture directly
            if (sourceObj != null)
            {
                if (sourceObj is Sprite s)
                {
                    _createSourceTexture = s.texture;
                }
                else if (sourceObj is Texture2D t)
                {
                    _createSourceTexture = t;
                }
                else
                {
                    _createSourceTexture = null;
                }
            }
            _createTileWidth = EditorGUILayout.IntField("Tile Width", _createTileWidth);
            _createTileHeight = EditorGUILayout.IntField("Tile Height", _createTileHeight);
            _createMargin = EditorGUILayout.IntField("Margin", _createMargin);
            _createSpacing = EditorGUILayout.IntField("Spacing", _createSpacing);
            _snapToGrid = EditorGUILayout.Toggle("Snap to Grid", _snapToGrid);
            _showEdgeProfileOverlays = EditorGUILayout.Toggle("Show EdgeProfile Overlays", _showEdgeProfileOverlays);
            if (_showEdgeProfileOverlays)
            {
                var picked = (PaintDots.ECS.ABCs.EdgeProfileAsset)EditorGUILayout.ObjectField("EdgeProfile Asset", _loadedEdgeProfile, typeof(PaintDots.ECS.ABCs.EdgeProfileAsset), false);
                if (picked != _loadedEdgeProfile)
                {
                    _loadedEdgeProfile = picked;
                }
            }
            // Zoom & pan controls for preview
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Zoom", GUILayout.Width(40));
            _previewZoom = EditorGUILayout.FloatField(_previewZoom, GUILayout.Width(60));
            if (GUILayout.Button("Fit", GUILayout.Width(40))) _previewZoom = 1f;
            if (GUILayout.Button("1:1", GUILayout.Width(40)))
            {
                // set zoom so that 1 tex pixel == 1 screen pixel
                // this requires computing baseScale, do it in preview draw below; for now set marker - will be adjusted when drawing
                _previewZoom = 0f; // special marker handled below
            }
            if (GUILayout.Button("24px", GUILayout.Width(50)))
            {
                // zoom to make 24 tex pixels span the preview width (will be computed when drawing)
                _previewZoom = -24f; // special marker (-N means zoom to make N pixels fill width)
            }
            if (GUILayout.Button("Reset Pan", GUILayout.Width(80))) { _previewPan = Vector2.zero; }
            EditorGUILayout.EndHorizontal();

            // Full texture preview with selection
            if (_createSourceTexture != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
                // Compute base fit scale
                float texWf = (float)_createSourceTexture.width;
                float texHf = (float)_createSourceTexture.height;
                float baseScale = Mathf.Min(previewRect.width / texWf, previewRect.height / texHf);

                // Interpret special previewZoom markers set by buttons
                float appliedZoom = _previewZoom;
                if (_previewZoom == 0f)
                {
                    // 1:1 => want texture pixels to map 1:1 to screen pixels
                    appliedZoom = 1f / Mathf.Max(1e-6f, baseScale);
                }
                else if (_previewZoom < 0f)
                {
                    // negative value encodes zoom-to-n-pixels (e.g., -24 means 24 pixels span preview width)
                    float targetTexPixels = -_previewZoom;
                    // compute zoom so that targetTexPixels * displayPerTex == previewRect.width
                    float displayPerTexNeeded = previewRect.width / targetTexPixels;
                    appliedZoom = displayPerTexNeeded / baseScale;
                }

                // Clamp zoom to reasonable range (prevent infinity)
                appliedZoom = Mathf.Clamp(appliedZoom, 1f / 1024f, 1024f);
                // If the _previewZoom was a special marker, store back a normalized value so subsequent interactions use it
                if (_previewZoom == 0f || _previewZoom < 0f) _previewZoom = appliedZoom;

                float drawW = texWf * baseScale * appliedZoom;
                float drawH = texHf * baseScale * appliedZoom;

                // compute content rect centered then offset by pan (pan is in texture pixels converted to display pixels)
                Vector2 panDisplay = new Vector2(_previewPan.x * (drawW / texWf), _previewPan.y * (drawH / texHf));
                Rect contentRect = new Rect(previewRect.x + (previewRect.width - drawW) * 0.5f + panDisplay.x,
                                            previewRect.y + (previewRect.height - drawH) * 0.5f + panDisplay.y,
                                            drawW,
                                            drawH);

                // Draw the texture into contentRect but clip to the previewRect so zoom doesn't overflow
                // contentRect is in global GUI coords; convert to local for the group
                Rect contentRectLocal = new Rect(contentRect.x - previewRect.x, contentRect.y - previewRect.y, contentRect.width, contentRect.height);
                GUI.BeginGroup(previewRect);
                GUI.DrawTexture(contentRectLocal, _createSourceTexture, ScaleMode.ScaleToFit);
                // Draw tile grid overlay inside the group
                if (_createTileWidth > 0 && _createTileHeight > 0 && _createSourceTexture != null)
                {
                    int colsTex = Mathf.Max(1, Mathf.FloorToInt((float)_createSourceTexture.width / Mathf.Max(1, _createTileWidth)));
                    int rowsTex = Mathf.Max(1, Mathf.FloorToInt((float)_createSourceTexture.height / Mathf.Max(1, _createTileHeight)));
                    float cellW = contentRectLocal.width / (float)colsTex;
                    float cellH = contentRectLocal.height / (float)rowsTex;
                    Color lineCol = new Color(1f, 1f, 1f, 0.15f);
                    for (int xi = 1; xi < colsTex; xi++)
                    {
                        Rect r = new Rect(contentRectLocal.x + xi * cellW - 0.5f, contentRectLocal.y, 1f, contentRectLocal.height);
                        EditorGUI.DrawRect(r, lineCol);
                    }
                    for (int yi = 1; yi < rowsTex; yi++)
                    {
                        Rect r = new Rect(contentRectLocal.x, contentRectLocal.y + yi * cellH - 0.5f, contentRectLocal.width, 1f);
                        EditorGUI.DrawRect(r, lineCol);
                    }
                }

                // Draw overlays if requested and an EdgeProfileAsset is loaded and matches dimensions
                if (_showEdgeProfileOverlays && _loadedEdgeProfile != null && _loadedEdgeProfile.SourceTexture == _createSourceTexture)
                {
                    // Ensure tile dims match
                    if (_loadedEdgeProfile.TileWidth == _createTileWidth && _loadedEdgeProfile.TileHeight == _createTileHeight)
                    {
                        int cols = Mathf.Max(1, Mathf.FloorToInt((float)_createSourceTexture.width / _createTileWidth));
                        int rows = Mathf.Max(1, Mathf.FloorToInt((float)_createSourceTexture.height / _createTileHeight));
                        float cellW = contentRectLocal.width / (float)cols;
                        float cellH = contentRectLocal.height / (float)rows;
                        for (int ry = 0; ry < rows; ry++)
                        {
                            for (int rx = 0; rx < cols; rx++)
                            {
                                int idx = ry * cols + rx;
                                if (idx >= _loadedEdgeProfile.Entries.Count) continue;
                                var entry = _loadedEdgeProfile.Entries[idx];
                                // draw small swatch with AvgRGB at top-left of cell
                                Rect cellRect = new Rect(contentRectLocal.x + rx * cellW, contentRectLocal.y + ry * cellH, cellW, cellH);
                                Rect sw = new Rect(cellRect.x + 2, cellRect.y + 2, 12, 12);
                                Color swc = new Color(entry.AvgRGB.x, entry.AvgRGB.y, entry.AvgRGB.z, 1f);
                                EditorGUI.DrawRect(sw, swc);

                                // draw FamilyId as label bottom-left
                                if (entry.FamilyId >= 0)
                                {
                                    GUIStyle s = new GUIStyle(EditorStyles.label);
                                    s.fontSize = 10;
                                    s.normal.textColor = Color.white;
                                    Rect lab = new Rect(cellRect.x + 2, cellRect.y + cellRect.height - 14, 20, 12);
                                    GUI.Label(lab, entry.FamilyId.ToString(), s);
                                }

                                // draw chroma compatibility heat (small overlay in top-right)
                                if (entry.ChromaCompatAvg >= 0f)
                                {
                                    float v = Mathf.Clamp01(entry.ChromaCompatAvg);
                                    // map 0..1 to red->green
                                    Color heat = Color.Lerp(Color.red, Color.green, v);
                                    Rect heatR = new Rect(cellRect.x + cellRect.width - 14, cellRect.y + 2, 12, 12);
                                    EditorGUI.DrawRect(heatR, heat);
                                }

                                // draw family color (uses Entry.FamilyId)
                                if (entry.FamilyId >= 0)
                                {
                                    int fid = entry.FamilyId;
                                    // stable color from id
                                    UnityEngine.Random.InitState(fid * 9973 + 17);
                                    Color fam = UnityEngine.Random.ColorHSV(0f, 1f, 0.4f, 0.95f, 0.4f, 0.95f);
                                    Rect famR = new Rect(cellRect.x + cellRect.width - 14, cellRect.y + cellRect.height - 14, 12, 12);
                                    EditorGUI.DrawRect(famR, fam);
                                }
                            }
                        }
                    }
                }
                GUI.EndGroup();

                // store previewRect for mapping later (use the actual texture draw rect in global coords)
                _lastPreviewRect = contentRect;

                // Handle input (selection, pan, zoom)
                Event ev = Event.current;

                // Mouse wheel zoom when over texture
                if (ev.type == EventType.ScrollWheel && _lastPreviewRect.Contains(ev.mousePosition))
                {
                    // compute old displayPerTex
                    float oldDisplayPerTexX = _lastPreviewRect.width / texWf;
                    float oldDisplayPerTexY = _lastPreviewRect.height / texHf;
                    // texture coordinate under cursor (in tex pixels)
                    float texUnderX = (ev.mousePosition.x - _lastPreviewRect.x) / oldDisplayPerTexX;
                    float texUnderY = (ev.mousePosition.y - _lastPreviewRect.y) / oldDisplayPerTexY;

                    float wheelDelta = -ev.delta.y; // scroll up positive
                    float zoomFactor = Mathf.Pow(1.1f, wheelDelta);
                    float newZoom = Mathf.Clamp(_previewZoom * zoomFactor, 1f / 1024f, 1024f);

                    // compute new draw dims
                    float newDrawW = texWf * baseScale * newZoom;
                    float newDrawH = texHf * baseScale * newZoom;

                    // compute new displayPerTex
                    float newDisplayPerTexX = newDrawW / texWf;
                    float newDisplayPerTexY = newDrawH / texHf;

                    // compute new pan so that texUnderX,texUnderY stays under cursor
                    Vector2 newPan = _previewPan;
                    float previewCenterX = previewRect.x + (previewRect.width - newDrawW) * 0.5f;
                    float previewCenterY = previewRect.y + (previewRect.height - newDrawH) * 0.5f;

                    newPan.x = (ev.mousePosition.x - previewCenterX) / newDisplayPerTexX - texUnderX;
                    newPan.y = (ev.mousePosition.y - previewCenterY) / newDisplayPerTexY - texUnderY;

                    _previewZoom = newZoom;
                    _previewPan = newPan;
                    ev.Use();
                    Repaint();
                }

                // Start panning: middle mouse or Alt+left-drag
                if (ev.type == EventType.MouseDown && (_lastPreviewRect.Contains(ev.mousePosition)) && (ev.button == 2 || (ev.button == 0 && ev.alt)))
                {
                    _isPanning = true;
                    _panStartMouse = ev.mousePosition;
                    _panStartOffset = _previewPan;
                    ev.Use();
                }

                if (_isPanning && ev.type == EventType.MouseDrag)
                {
                    // delta in display pixels -> convert to texture pixels
                    Vector2 delta = ev.mousePosition - _panStartMouse;
                    float displayPerTexX = (_lastPreviewRect.width) / texWf;
                    float displayPerTexY = (_lastPreviewRect.height) / texHf;
                    _previewPan = _panStartOffset + new Vector2(delta.x / displayPerTexX, delta.y / displayPerTexY);
                    ev.Use();
                    Repaint();
                }

                if (_isPanning && ev.type == EventType.MouseUp)
                {
                    _isPanning = false;
                    ev.Use();
                }

                // Selection with left mouse
                if (ev.type == EventType.MouseDown && ev.button == 0 && !_isPanning && _lastPreviewRect.Contains(ev.mousePosition))
                {
                    _isPreviewSelecting = true;
                    // initialize texture-space selection anchors
                    float texWf2 = (float)_createSourceTexture.width;
                    float texHf2 = (float)_createSourceTexture.height;
                    _selectionStartTex = new Vector2(
                        (ev.mousePosition.x - _lastPreviewRect.x) / Mathf.Max(1f, _lastPreviewRect.width) * texWf2,
                        (ev.mousePosition.y - _lastPreviewRect.y) / Mathf.Max(1f, _lastPreviewRect.height) * texHf2
                    );
                    _selectionStartTex.x = Mathf.Clamp(_selectionStartTex.x, 0f, texWf2);
                    _selectionStartTex.y = Mathf.Clamp(_selectionStartTex.y, 0f, texHf2);
                    _selectionEndTex = _selectionStartTex;
                    ev.Use();
                }

                if (_isPreviewSelecting && ev.type == EventType.MouseDrag)
                {
                    float texWf2 = (float)_createSourceTexture.width;
                    float texHf2 = (float)_createSourceTexture.height;
                    _selectionEndTex = new Vector2(
                        (ev.mousePosition.x - _lastPreviewRect.x) / Mathf.Max(1f, _lastPreviewRect.width) * texWf2,
                        (ev.mousePosition.y - _lastPreviewRect.y) / Mathf.Max(1f, _lastPreviewRect.height) * texHf2
                    );
                    _selectionEndTex.x = Mathf.Clamp(_selectionEndTex.x, 0f, texWf2);
                    _selectionEndTex.y = Mathf.Clamp(_selectionEndTex.y, 0f, texHf2);
                    ev.Use();
                    Repaint();
                }

                if (_isPreviewSelecting && ev.type == EventType.MouseUp)
                {
                    float texWf2 = (float)_createSourceTexture.width;
                    float texHf2 = (float)_createSourceTexture.height;
                    _selectionEndTex = new Vector2(
                        (ev.mousePosition.x - _lastPreviewRect.x) / Mathf.Max(1f, _lastPreviewRect.width) * texWf2,
                        (ev.mousePosition.y - _lastPreviewRect.y) / Mathf.Max(1f, _lastPreviewRect.height) * texHf2
                    );
                    _selectionEndTex.x = Mathf.Clamp(_selectionEndTex.x, 0f, texWf2);
                    _selectionEndTex.y = Mathf.Clamp(_selectionEndTex.y, 0f, texHf2);
                    _isPreviewSelecting = false;
                    ev.Use();
                    Repaint();
                }

                // Draw selection rect if any (clip to preview area) using texture-space selection
                if (_selectionStartTex != _selectionEndTex)
                {
                    // compute snapped/clamped tex-space min/max
                    float texWf2 = (float)_createSourceTexture.width;
                    float texHf2 = (float)_createSourceTexture.height;
                    var sMin = Vector2.Min(_selectionStartTex, _selectionEndTex);
                    var sMax = Vector2.Max(_selectionStartTex, _selectionEndTex);

                    if (_snapToGrid && _createTileWidth > 0 && _createTileHeight > 0)
                    {
                        sMin.x = Mathf.Floor(sMin.x / _createTileWidth) * _createTileWidth;
                        sMin.y = Mathf.Floor(sMin.y / _createTileHeight) * _createTileHeight;
                        sMax.x = Mathf.Ceil(sMax.x / _createTileWidth) * _createTileWidth;
                        sMax.y = Mathf.Ceil(sMax.y / _createTileHeight) * _createTileHeight;
                    }

                    sMin.x = Mathf.Clamp(sMin.x, 0f, texWf2);
                    sMin.y = Mathf.Clamp(sMin.y, 0f, texHf2);
                    sMax.x = Mathf.Clamp(sMax.x, 0f, texWf2);
                    sMax.y = Mathf.Clamp(sMax.y, 0f, texHf2);

                    // convert tex-space to GUI global coords
                    Rect selRectG = new Rect(
                        _lastPreviewRect.x + (sMin.x / texWf2) * _lastPreviewRect.width,
                        _lastPreviewRect.y + (sMin.y / texHf2) * _lastPreviewRect.height,
                        ((sMax.x - sMin.x) / texWf2) * _lastPreviewRect.width,
                        ((sMax.y - sMin.y) / texHf2) * _lastPreviewRect.height
                    );

                    // Convert to local coords inside previewRect and draw inside a clipped group
                    Rect selLocal = new Rect(selRectG.x - previewRect.x, selRectG.y - previewRect.y, selRectG.width, selRectG.height);
                    GUI.BeginGroup(previewRect);
                    EditorGUI.DrawRect(selLocal, new Color(0.2f, 0.6f, 1f, 0.25f));
                    // outline
                    Rect outline = selLocal;
                    EditorGUI.DrawRect(new Rect(outline.x, outline.y, outline.width, 2f), Color.cyan);
                    EditorGUI.DrawRect(new Rect(outline.x, outline.y + outline.height - 2f, outline.width, 2f), Color.cyan);
                    EditorGUI.DrawRect(new Rect(outline.x, outline.y, 2f, outline.height), Color.cyan);
                    EditorGUI.DrawRect(new Rect(outline.x + outline.width - 2f, outline.y, 2f, outline.height), Color.cyan);
                    GUI.EndGroup();
                }

                // grid overlay already drawn clipped inside the preview group earlier; do nothing here

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Palette Asset (Grid)"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save Tile Palette", "TilePalette", "asset", "Choose location to save the created TilePalette asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var palette = ScriptableObject.CreateInstance<PaintDots.Editor.TilePalette>();
                        palette.GenerateFromGrid(_createSourceTexture, _createTileWidth, _createTileHeight, _createMargin, _createSpacing);
                        palette.Texture = _createSourceTexture;
                            UnityEditor.AssetDatabase.CreateAsset(palette, path);
                            UnityEditor.AssetDatabase.SaveAssets();
                            UnityEditor.AssetDatabase.Refresh();
                        _selectedPalette = palette;
                    }
                }

                if (GUILayout.Button("Create Palette Asset (Selection)"))
                {
                    // compute selected pixel region based on last selection
                        // use texture-space selection values
                        var selMin = Vector2.Min(_selectionStartTex, _selectionEndTex);
                        var selMax = Vector2.Max(_selectionStartTex, _selectionEndTex);
                    if (selMin == selMax)
                    {
                        EditorUtility.DisplayDialog("No Selection", "Drag a rectangle on the preview to select a region first.", "OK");
                    }
                    else
                    {
                        // Map to texture pixel coordinates (previewRect used before)
                        // Find the last preview rect by re-drawing a layout rect of same size
                        Rect lastPreviewRect = GUILayoutUtility.GetLastRect();
                        // The last previewRect is not directly accessible here; compute from available width/height approximations
                        // As a fallback, re-use the preview size of 256 and center mapping across width
                        Rect previewRect2 = GUILayoutUtility.GetLastRect();
                        // Use the previously drawn previewRect's top-left: approximate by using position from GUIUtility.GUIToScreenPoint( Vector2.zero )? Simpler: recompute mapping relative to the last event positions stored earlier
                        // Instead, compute local coordinates relative to the preview area we drew earlier by using the stored _previewSelStart/_previewSelEnd positions and the texture size.

                        // Map selection points to texture UVs in [0,1] using the stored preview rect
                        // selMin/selMax are in texture pixels already
                        int texW = _createSourceTexture.width;
                        int texH = _createSourceTexture.height;

                        float sMinX = selMin.x;
                        float sMinY = selMin.y;
                        float sMaxX = selMax.x;
                        float sMaxY = selMax.y;

                        if (_snapToGrid && _createTileWidth > 0 && _createTileHeight > 0)
                        {
                            sMinX = Mathf.Floor(sMinX / _createTileWidth) * _createTileWidth;
                            sMinY = Mathf.Floor(sMinY / _createTileHeight) * _createTileHeight;
                            sMaxX = Mathf.Ceil(sMaxX / _createTileWidth) * _createTileWidth;
                            sMaxY = Mathf.Ceil(sMaxY / _createTileHeight) * _createTileHeight;
                        }

                        int regionPx = Mathf.Clamp(Mathf.RoundToInt(sMinX), 0, texW - 1);
                        int regionPyTop = Mathf.Clamp(Mathf.RoundToInt(sMinY), 0, texH - 1);
                        int regionWidth = Mathf.Clamp(Mathf.RoundToInt(sMaxX - sMinX), 1, texW - regionPx);
                        int regionHeight = Mathf.Clamp(Mathf.RoundToInt(sMaxY - sMinY), 1, texH - regionPyTop);

                        // Convert v coordinates since UI y=top; regionPyTop is top-based
                        // Create and save palette
                        string path = EditorUtility.SaveFilePanelInProject("Save Tile Palette", "TilePalette", "asset", "Choose location to save the created TilePalette asset");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var palette = ScriptableObject.CreateInstance<PaintDots.Editor.TilePalette>();
                            // regionPyTop computed from top-origin (v1), ensure values within bounds
                            regionPx = Mathf.Clamp(regionPx, 0, texW - 1);
                            regionPyTop = Mathf.Clamp(regionPyTop, 0, texH - 1);
                            regionWidth = Mathf.Clamp(regionWidth, 1, texW - regionPx);
                            regionHeight = Mathf.Clamp(regionHeight, 1, texH - regionPyTop);

                            palette.GenerateFromRegion(_createSourceTexture, regionPx, regionPyTop, regionWidth, regionHeight, _createTileWidth, _createTileHeight, _createMargin, _createSpacing);
                            palette.Texture = _createSourceTexture;
                            AssetDatabase.CreateAsset(palette, path);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            _selectedPalette = palette;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(_isPainting ? "Stop Painting" : "Start Painting"))
            {
                _isPainting = !_isPainting;
            }

            // Brush settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _brushType = (BrushType)EditorGUILayout.EnumPopup("Brush Type", _brushType);
            _brushSize = EditorGUILayout.IntField("Size/Radius", _brushSize);
            _useAutoTile = EditorGUILayout.Toggle("Use AutoTile", _useAutoTile);
            _noiseThreshold = EditorGUILayout.Slider("Noise Threshold", _noiseThreshold, 0f, 1f);
            _noiseSeed = EditorGUILayout.IntField("Noise Seed", _noiseSeed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chunking", EditorStyles.boldLabel);
            bool useChunking = EditorPrefs.GetBool("PaintDots_UseChunking", false);
            useChunking = EditorGUILayout.Toggle("Use Chunking", useChunking);
            EditorPrefs.SetBool("PaintDots_UseChunking", useChunking);
            // Chunk size fields
            int chunkX = EditorPrefs.GetInt("PaintDots_ChunkSizeX", 32);
            int chunkY = EditorPrefs.GetInt("PaintDots_ChunkSizeY", 32);
            chunkX = EditorGUILayout.IntField("Chunk Size X", chunkX);
            chunkY = EditorGUILayout.IntField("Chunk Size Y", chunkY);
            EditorPrefs.SetInt("PaintDots_ChunkSizeX", chunkX);
            EditorPrefs.SetInt("PaintDots_ChunkSizeY", chunkY);
            if (GUILayout.Button("Apply Chunk Settings to World"))
            {
                if (_world == default)
                {
                    Debug.LogWarning("No ECS World available to apply chunk settings.");
                }
                else
                {
                    var em = _world.EntityManager;
                    var cfg = new ChunkConfig(useChunking, new int2(chunkX, chunkY));
                    // Use an EntityQuery to find an existing ChunkConfig singleton entity
                    var query = em.CreateEntityQuery(ComponentType.ReadOnly<ChunkConfig>());
                    if (query.IsEmpty)
                    {
                        var e = em.CreateEntity();
                        em.AddComponentData(e, cfg);
                    }
                    else
                    {
                        var singleton = query.GetSingletonEntity();
                        em.SetComponentData(singleton, cfg);
                    }

                    Debug.Log($"Applied ChunkConfig useChunking={useChunking} size=({chunkX},{chunkY})");
                }
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Click 'Start Painting' to enable paint mode");
            EditorGUILayout.LabelField("• Left-click in Scene view to paint tiles");
            EditorGUILayout.LabelField("• Right-click to erase tiles");
            EditorGUILayout.LabelField("• Hold Shift for multi-tile painting");
            
            if (_world == default)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No ECS World found. Make sure you have an active scene with ECS enabled.", MessageType.Warning);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isPainting || _world == default || _entityManager == default)
                return;

            Event e = Event.current;
            
            if (e.type == EventType.MouseDown)
            {
                if (e.button == 0) // Left mouse button - Paint
                {
                    Vector3 mousePos = Event.current.mousePosition;
                    Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
                    
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        Vector3 worldPos = hit.point;
                        int2 gridPos = new int2(
                            Mathf.RoundToInt(worldPos.x),
                            Mathf.RoundToInt(worldPos.y)
                        );
                        
                        PaintTile(gridPos, _selectedTileID);
                        e.Use();
                    }
                }
                else if (e.button == 1) // Right mouse button - Erase
                {
                    Vector3 mousePos = Event.current.mousePosition;
                    Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
                    
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        Vector3 worldPos = hit.point;
                        int2 gridPos = new int2(
                            Mathf.RoundToInt(worldPos.x),
                            Mathf.RoundToInt(worldPos.y)
                        );
                        
                        EraseTile(gridPos);
                        e.Use();
                    }
                }
            }
        }

        // Create AutoTile/Tile assets for any slots that have a TileTemplate-like object assigned.
        // This uses reflection to call CreateTileAssets on the assigned template and extract the generated TileBase from the returned TileChangeData list.
        private void CreateAutoTilesForPalette(PaintDots.Editor.TilePalette palette, string paletteAssetPath)
        {
            if (palette == null || string.IsNullOrEmpty(paletteAssetPath)) return;

            try
            {
                // Find TileChangeData runtime type
                var asmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return new Type[0]; }
                });

                var tileChangeDataType = asmTypes.FirstOrDefault(t => t.Name == "TileChangeData");
                if (tileChangeDataType == null)
                {
                    Debug.Log("TileChangeData type not found in loaded assemblies. Ensure the 2D Tilemap Extras package is installed.");
                    return;
                }

                var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(tileChangeDataType);

                bool anyCreated = false;
                for (int i = 0; i < palette.SlotAutoTileAssets.Count; i++)
                {
                    var assigned = palette.SlotAutoTileAssets[i];
                    if (assigned == null) continue;

                    var mi = assigned.GetType().GetMethod("CreateTileAssets", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mi == null)
                    {
                        // Not a template
                        continue;
                    }

                    // Create an instance of List<TileChangeData>
                    var tileChangeList = Activator.CreateInstance(listType);
                    object[] args = new object[] { palette.Texture, null, tileChangeList };
                    mi.Invoke(assigned, args);

                    // If the method replaced the ref parameter, use args[2]
                    var effectiveList = args[2] ?? tileChangeList;
                    var iList = effectiveList as System.Collections.IList;
                    if (iList == null || iList.Count == 0) continue;

                    // For each TileChangeData element, find a field/property that contains the generated TileBase
                    for (int e = 0; e < iList.Count; e++)
                    {
                        var item = iList[e];
                        if (item == null) continue;
                        // Inspect fields first
                        object tileObj = null;
                        var fields = item.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var f in fields)
                        {
                            if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                            {
                                var val = f.GetValue(item) as UnityEngine.Object;
                                if (val != null && val is UnityEngine.Tilemaps.TileBase)
                                {
                                    tileObj = val;
                                    break;
                                }
                            }
                        }

                        if (tileObj == null)
                        {
                            var props = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (var p in props)
                            {
                                if (typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                                {
                                    try
                                    {
                                        var val = p.GetValue(item) as UnityEngine.Object;
                                        if (val != null && val is UnityEngine.Tilemaps.TileBase)
                                        {
                                            tileObj = val;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (tileObj != null)
                        {
                            // Save the generated tile asset next to the palette asset
                            string folder = Path.GetDirectoryName(paletteAssetPath).Replace("\\", "/");
                            if (string.IsNullOrEmpty(folder)) folder = "Assets";
                            string baseName = Path.GetFileNameWithoutExtension(paletteAssetPath);
                            string assetName = $"{baseName}_slot{i}.asset";
                            string destPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, assetName).Replace("\\", "/"));
                            AssetDatabase.CreateAsset((UnityEngine.Object)tileObj, destPath);
                            AssetDatabase.SaveAssets();
                            // Assign the created asset back to the palette slot
                            palette.SlotAutoTileAssets[i] = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destPath);
                            anyCreated = true;
                        }
                    }
                }

                if (anyCreated)
                {
                    EditorUtility.SetDirty(palette);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("AutoTile assets created for palette slots.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Failed to auto-generate tiles from templates: " + ex.Message);
            }
        }

        private void PaintTile(int2 gridPosition, int tileID)
        {
            if (_entityManager == default) return;
            // Create an ECB and use BrushSystem utilities to create paint commands
            var beginSystem = _world.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var ecb = beginSystem.CreateCommandBuffer();

            var brush = new BrushConfig(_brushType, Mathf.Max(1, _brushSize), tileID, _useAutoTile, _noiseThreshold, (uint)_noiseSeed);
            // Apply brush directly
            BrushSystem.ApplyBrush(ecb, gridPosition, brush);

            Debug.Log($"Enqueued paint brush {brush.Type} tile {tileID} at position {gridPosition}");
        }

        private void EraseTile(int2 gridPosition)
        {
            if (_entityManager == default) return;
            // Use ECB to enqueue erase command
            var beginSystem = _world.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            var ecb = beginSystem.CreateCommandBuffer();
            TilemapUtilities.CreateEraseCommand(ecb, gridPosition);
            Debug.Log($"Enqueued erase at position {gridPosition}");
        }
    }

    /// <summary>
    /// Custom inspector for TilemapAuthoring
    /// </summary>
    [CustomEditor(typeof(PaintDots.ECS.Authoring.TilemapAuthoring))]
    public sealed class TilemapAuthoringInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open Tilemap Painter"))
            {
                TilemapPainterWindow.ShowWindow();
            }
        }
    }
}