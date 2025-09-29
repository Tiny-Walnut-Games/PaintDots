using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace PaintDots.Editor
{
    /// <summary>
    /// Editor window for painting tiles in Scene view using ECS
    /// </summary>
    public class TilemapPainterWindow : EditorWindow
    {
        private int _selectedTileID = 0;
        private bool _isPainting = false;
        private World _world;
        private EntityManager _entityManager;
        
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
            if (_world != null)
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
            _selectedTileID = EditorGUILayout.IntField("Selected Tile ID", _selectedTileID);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(_isPainting ? "Stop Painting" : "Start Painting"))
            {
                _isPainting = !_isPainting;
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Click 'Start Painting' to enable paint mode");
            EditorGUILayout.LabelField("• Left-click in Scene view to paint tiles");
            EditorGUILayout.LabelField("• Right-click to erase tiles");
            EditorGUILayout.LabelField("• Hold Shift for multi-tile painting");
            
            if (_world == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No ECS World found. Make sure you have an active scene with ECS enabled.", MessageType.Warning);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isPainting || _world == null || _entityManager == null)
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

        private void PaintTile(int2 gridPosition, int tileID)
        {
            if (_entityManager == null) return;
            
            // Create a paint command entity
            Entity paintCommand = _entityManager.CreateEntity();
            _entityManager.AddComponentData(paintCommand, new PaintDots.ECS.PaintCommand
            {
                GridPosition = gridPosition,
                TileID = tileID
            });
            
            Debug.Log($"Painted tile {tileID} at position {gridPosition}");
        }

        private void EraseTile(int2 gridPosition)
        {
            if (_entityManager == null) return;
            
            // Find and destroy tile at position
            EntityQuery tileQuery = _entityManager.CreateEntityQuery(typeof(PaintDots.ECS.Tile));
            var tiles = tileQuery.ToEntityArray(Unity.Collections.Allocator.TempJob);
            var tileComponents = tileQuery.ToComponentDataArray<PaintDots.ECS.Tile>(Unity.Collections.Allocator.TempJob);
            
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tileComponents[i].GridPosition.Equals(gridPosition))
                {
                    _entityManager.DestroyEntity(tiles[i]);
                    Debug.Log($"Erased tile at position {gridPosition}");
                    break;
                }
            }
            
            tiles.Dispose();
            tileComponents.Dispose();
        }
    }

    /// <summary>
    /// Custom inspector for TilemapAuthoring
    /// </summary>
    [CustomEditor(typeof(PaintDots.ECS.Authoring.TilemapAuthoring))]
    public class TilemapAuthoringInspector : UnityEditor.Editor
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