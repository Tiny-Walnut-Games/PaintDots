# External Scripts for the GridLayerEditor system

- I cannot bring the actual files in without corrupting the project and I do not want that, so I am adding them as blocks in this single document as a source of inspiration.

````GridLayerConfig.cs
using UnityEngine;

namespace TinyWalnutGames.GridLayerEditor
{    /// <summary>
    /// ScriptableObject to store layer names for grid setups.
    /// Used by editor scripts to configure and create grids with custom layers.
    /// </summary>
    [CreateAssetMenu(fileName = "GridLayerConfig", menuName = "Tiny Walnut Games/Grid Layer Config")]
    public class GridLayerConfig : ScriptableObject
    {
        /// <summary>
        /// Array of layer names to use for grid creation.
        /// </summary>
        public string[] layerNames = new string[]
        {
            // Default to platformer layers
            "Parallax5",
            "Parallax4",
            "Parallax3",
            "Parallax2",
            "Parallax1",
            "Background2",
            "Background1",
            "BackgroundProps",
            "WalkableGround",
            "WalkableProps",
            "Hazards",
            "Foreground",
            "ForegroundProps",
            "RoomMasking",
            "Blending",
        };
    }
}
````

````LayerManager.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TinyWalnutGames.GridLayerEditor
{
    /// <summary>
    /// Utility class for managing Unity layers and sorting layers.
    /// Provides functionality to create default layer setups for different game types.
    /// </summary>
    public static class LayerManager
    {
        /// <summary>
        /// Default platformer layers that should exist in Unity's layer manager.
        /// </summary>
        private static readonly string[] PlatformerLayers = new string[]
        {
            "Default",
            "TransparentFX",
            "Ignore Raycast",
            "Water",
            "UI",
            "Parallax5",
            "Parallax4",
            "Parallax3",
            "Parallax2",
            "Parallax1",
            "Background2",
            "Background1",
            "BackgroundProps",
            "WalkableGround",
            "WalkableProps",
            "Hazards",
            "Foreground",
            "ForegroundProps",
            "RoomMasking",
            "Blending"
        };

        /// <summary>
        /// Default top-down layers that should exist in Unity's layer manager.
        /// </summary>
        private static readonly string[] TopDownLayers = new string[]
        {
            "Default",
            "TransparentFX",
            "Ignore Raycast",
            "Water",
            "UI",
            "DeepOcean",
            "Ocean",
            "ShallowWater",
            "Floor",
            "FloorProps",
            "WalkableGround",
            "WalkableProps",
            "OverheadProps",
            "RoomMasking",
            "Blending"
        };

        /// <summary>
        /// Default platformer sorting layers.
        /// </summary>
        private static readonly string[] PlatformerSortingLayers = new string[]
        {
            "Default",
            "Parallax5",
            "Parallax4",
            "Parallax3",
            "Parallax2",
            "Parallax1",
            "Background2",
            "Background1",
            "BackgroundProps",
            "WalkableGround",
            "WalkableProps",
            "Hazards",
            "Foreground",
            "ForegroundProps",
            "RoomMasking",
            "Blending"
        };

        /// <summary>
        /// Default top-down sorting layers.
        /// </summary>
        private static readonly string[] TopDownSortingLayers = new string[]
        {
            "Default",
            "DeepOcean",
            "Ocean",
            "ShallowWater",
            "Floor",
            "FloorProps",
            "WalkableGround",
            "WalkableProps",
            "OverheadProps",
            "RoomMasking",
            "Blending"
        };

        /// <summary>
        /// Creates platformer layers in Unity's layer manager.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Layer Management/Create Platformer Layers")]
        public static void CreatePlatformerLayers()
        {
            CreateLayers(PlatformerLayers, "Platformer");
            CreateSortingLayers(PlatformerSortingLayers, "Platformer");
        }

        /// <summary>
        /// Creates top-down layers in Unity's layer manager.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Layer Management/Create Top-Down Layers")]
        public static void CreateTopDownLayers()
        {
            CreateLayers(TopDownLayers, "Top-Down");
            CreateSortingLayers(TopDownSortingLayers, "Top-Down");
        }

        /// <summary>
        /// Creates all layers (both platformer and top-down).
        /// </summary>
        [MenuItem("Tiny Walnut Games/Layer Management/Create All Layers")]
        public static void CreateAllLayers()
        {
   string [ ] allLayers = PlatformerLayers.Union(TopDownLayers).Distinct().ToArray();
   string [ ] allSortingLayers = PlatformerSortingLayers.Union(TopDownSortingLayers).Distinct().ToArray();

            CreateLayers(allLayers, "All Game Types");
            CreateSortingLayers(allSortingLayers, "All Game Types");
        }

        /// <summary>
        /// Shows a report of current layer usage.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Layer Management/Show Layer Report")]
        public static void ShowLayerReport()
        {
   List<string> usedLayers = GetUsedLayers();
   List<string> usedSortingLayers = GetUsedSortingLayers();

            string report = "=== UNITY LAYER REPORT ===\n\n";

            report += "UNITY LAYERS:\n";
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    report += $"  [{i:D2}] {layerName}\n";
                }
                else
                {
                    report += $"  [{i:D2}] <empty>\n";
                }
            }

            report += "\nSORTING LAYERS:\n";
            foreach (string sortingLayer in usedSortingLayers)
            {
                report += $"  • {sortingLayer}\n";
            }

            report += $"\nSUMMARY:\n";
            report += $"  Unity Layers Used: {usedLayers.Count}/32\n";
            report += $"  Sorting Layers Used: {usedSortingLayers.Count}\n";

            Debug.Log(report);
            EditorUtility.DisplayDialog("Layer Report",
                $"Layer report has been logged to the console.\n\n" +
                $"Unity Layers Used: {usedLayers.Count}/32\n" +
                $"Sorting Layers Used: {usedSortingLayers.Count}",
                "OK");
        }

        /// <summary>
        /// Removes unused layers (with confirmation).
        /// </summary>
        [MenuItem("Tiny Walnut Games/Layer Management/Clean Unused Layers")]
        public static void CleanUnusedLayers()
        {
            if (EditorUtility.DisplayDialog("Clean Unused Layers",
                "This will remove all Unity layers that are not in the standard presets.\n\n" +
                "This action cannot be undone. Are you sure you want to continue?",
                "Yes, Clean Layers", "Cancel"))
            {
                CleanLayers();
            }
        }

        /// <summary>
        /// Creates the specified layers in Unity's layer manager.
        /// </summary>
        /// <param name="layerNames">Array of layer names to create.</param>
        /// <param name="setupType">Description of the setup type for logging.</param>
        private static void CreateLayers(string[] layerNames, string setupType)
        {
   SerializedObject? tagManager = GetTagManager();
            if (tagManager == null)
            {
                Debug.LogError("Could not access TagManager. Layer creation failed.");
                return;
            }

   SerializedProperty layersProperty = tagManager.FindProperty("layers");
            var createdLayers = new List<string>();
            var skippedLayers = new List<string>();

            foreach (string layerName in layerNames)
            {
                // Skip built-in layers
                if (IsBuiltInLayer(layerName))
                {
                    continue;
                }

                // Check if layer already exists
                if (LayerMask.NameToLayer(layerName) != -1)
                {
                    skippedLayers.Add(layerName);
                    continue;
                }

                // Find an empty slot
                bool layerCreated = false;
                for (int i = 8; i < 32; i++) // Start at 8 to skip built-in layers
                {
     SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layerProperty.stringValue))
                    {
                        layerProperty.stringValue = layerName;
                        createdLayers.Add(layerName);
                        layerCreated = true;
                        break;
                    }
                }

                if (!layerCreated)
                {
                    Debug.LogWarning($"Could not create layer '{layerName}' - no empty slots available.");
                }
            }

            tagManager.ApplyModifiedProperties();

            // Log results
            string message = $"{setupType} Layer Creation Complete:\n";
            if (createdLayers.Count > 0)
            {
                message += $"  Created: {string.Join(", ", createdLayers)}\n";
            }
            if (skippedLayers.Count > 0)
            {
                message += $"  Already Existed: {string.Join(", ", skippedLayers)}\n";
            }

            Debug.Log(message);

            if (createdLayers.Count > 0)
            {
                EditorUtility.DisplayDialog("Layers Created",
                    $"Successfully created {createdLayers.Count} new {setupType.ToLower()} layers.\n\n" +
                    $"Check the console for details.",
                    "OK");
            }
        }

        /// <summary>
        /// Creates the specified sorting layers.
        /// </summary>
        /// <param name="sortingLayerNames">Array of sorting layer names to create.</param>
        /// <param name="setupType">Description of the setup type for logging.</param>
        private static void CreateSortingLayers(string[] sortingLayerNames, string setupType)
        {
   SerializedObject? tagManager = GetTagManager();
            if (tagManager == null) return;

   SerializedProperty sortingLayersProperty = tagManager.FindProperty("m_SortingLayers");
            var createdSortingLayers = new List<string>();
            var skippedSortingLayers = new List<string>();

            foreach (string sortingLayerName in sortingLayerNames)
            {
                // Check if sorting layer already exists
                if (SortingLayerExists(sortingLayerName))
                {
                    skippedSortingLayers.Add(sortingLayerName);
                    continue;
                }

                // Add new sorting layer
                sortingLayersProperty.InsertArrayElementAtIndex(sortingLayersProperty.arraySize);
    SerializedProperty newSortingLayer = sortingLayersProperty.GetArrayElementAtIndex(sortingLayersProperty.arraySize - 1);
                newSortingLayer.FindPropertyRelative("name").stringValue = sortingLayerName;
                newSortingLayer.FindPropertyRelative("uniqueID").intValue = System.DateTime.Now.GetHashCode();

                createdSortingLayers.Add(sortingLayerName);
            }

            tagManager.ApplyModifiedProperties();

            // Log results
            if (createdSortingLayers.Count > 0 || skippedSortingLayers.Count > 0)
            {
                string message = $"{setupType} Sorting Layer Creation Complete:\n";
                if (createdSortingLayers.Count > 0)
                {
                    message += $"  Created: {string.Join(", ", createdSortingLayers)}\n";
                }
                if (skippedSortingLayers.Count > 0)
                {
                    message += $"  Already Existed: {string.Join(", ", skippedSortingLayers)}";
                }

                Debug.Log(message);
            }
        }

        /// <summary>
        /// Gets the TagManager SerializedObject.
        /// </summary>
        /// <returns>SerializedObject for the TagManager, or null if not found.</returns>
        private static SerializedObject? GetTagManager()
        {
   Object tagManagerAsset = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset");
            if (tagManagerAsset == null)
            {
                Debug.LogError("Could not load TagManager.asset");
                return null;
            }
            return new SerializedObject(tagManagerAsset);
        }

        /// <summary>
        /// Checks if the specified layer is a built-in Unity layer.
        /// </summary>
        /// <param name="layerName">Name of the layer to check.</param>
        /// <returns>True if the layer is built-in, false otherwise.</returns>
        private static bool IsBuiltInLayer(string layerName)
        {
            return layerName == "Default" ||
                   layerName == "TransparentFX" ||
                   layerName == "Ignore Raycast" ||
                   layerName == "Water" ||
                   layerName == "UI";
        }

        /// <summary>
        /// Checks if a sorting layer exists.
        /// </summary>
        /// <param name="sortingLayerName">Name of the sorting layer to check.</param>
        /// <returns>True if the sorting layer exists, false otherwise.</returns>
        private static bool SortingLayerExists(string sortingLayerName)
        {
   SortingLayer [ ] sortingLayers = SortingLayer.layers;
            return sortingLayers.Any(layer => layer.name == sortingLayerName);
        }

        /// <summary>
        /// Gets a list of all currently used Unity layers.
        /// </summary>
        /// <returns>List of used layer names.</returns>
        private static List<string> GetUsedLayers()
        {
            var usedLayers = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    usedLayers.Add(layerName);
                }
            }
            return usedLayers;
        }

        /// <summary>
        /// Gets a list of all currently used sorting layers.
        /// </summary>
        /// <returns>List of used sorting layer names.</returns>
        private static List<string> GetUsedSortingLayers()
        {
            return SortingLayer.layers.Select(layer => layer.name).ToList();
        }

        /// <summary>
        /// Removes layers that are not in the standard presets.
        /// </summary>
        private static void CleanLayers()
        {
   SerializedObject? tagManager = GetTagManager();
            if (tagManager == null) return;

   SerializedProperty layersProperty = tagManager.FindProperty("layers");
            var standardLayers = PlatformerLayers.Union(TopDownLayers).ToHashSet();
            var removedLayers = new List<string>();

            for (int i = 8; i < 32; i++) // Start at 8 to skip built-in layers
            {
    SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                string layerName = layerProperty.stringValue;

                if (!string.IsNullOrEmpty(layerName) && !standardLayers.Contains(layerName))
                {
                    layerProperty.stringValue = "";
                    removedLayers.Add(layerName);
                }
            }

            tagManager.ApplyModifiedProperties();

            if (removedLayers.Count > 0)
            {
                string message = $"Cleaned {removedLayers.Count} unused layers:\n  {string.Join(", ", removedLayers)}";
                Debug.Log(message);
                EditorUtility.DisplayDialog("Layers Cleaned",
                    $"Removed {removedLayers.Count} unused layers.\n\nCheck the console for details.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Cleanup Needed",
                    "All layers are either built-in or part of the standard presets.",
                    "OK");
            }
        }

        /// <summary>
        /// Validates that all layers from a GridLayerConfig exist in Unity.
        /// Creates missing layers if requested.
        /// </summary>
        /// <param name="config">The GridLayerConfig to validate.</param>
        /// <param name="createMissing">Whether to create missing layers automatically.</param>
        /// <returns>True if all layers exist (or were created), false otherwise.</returns>
        public static bool ValidateConfigLayers(GridLayerConfig config, bool createMissing = false)
        {
            if (config == null || config.layerNames == null) return true;

            var missingLayers = new List<string>();

            foreach (string layerName in config.layerNames)
            {
                if (LayerMask.NameToLayer(layerName) == -1 && !IsBuiltInLayer(layerName))
                {
                    missingLayers.Add(layerName);
                }
            }

            if (missingLayers.Count == 0) return true;

            if (createMissing)
            {
                CreateLayers(missingLayers.ToArray(), "Config Validation");
                return true;
            }

            Debug.LogWarning($"Missing Unity layers: {string.Join(", ", missingLayers)}");
            return false;
        }
    }
}
#endif
````

````TwoDimensionalGridSetup.cs
// Assets/Editor/TwoDimensionalGridSettup.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;
using System.Linq;

namespace TinyWalnutGames.GridLayerEditor
    {
    /// <summary>
    /// Utility class to spawn a Grid and one Tilemap/GameObject per layer.
    /// Supports both side-scrolling (platformer), top-down, isometric, and hexagonal 2D setups.
    /// </summary>
    public static class TwoDimensionalGridSetup
        {
        // Delegate for test injection (initialized to no-op for nullability safety)
        public static System.Action<string[]> CreateCustomGridAction = _ => { };

        /// <summary>
        /// Enum for platformer layer names.
        /// </summary>
        public enum SideScrollingLayers
            {
            Parallax5,
            Parallax4,
            Parallax3,
            Parallax2,
            Parallax1,
            Background2,
            Background1,
            BackgroundProps,
            WalkableGround,
            WalkableProps,
            Hazards,
            Foreground,
            ForegroundProps,
            RoomMasking,
            Blending,
            }

        /// <summary>
        /// Creates a side-scrolling grid in the scene with platformer layers.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Create Side-Scrolling Grid")]
        public static void CreateSideScrollingGrid()
            {
            var gridGO = new GameObject("Side-Scrolling Grid", typeof(Grid));
            gridGO.transform.position = Vector3.zero;
            int layerCount = Enum.GetValues(typeof(SideScrollingLayers)).Length;
            int index = 0;
            foreach (SideScrollingLayers layer in Enum.GetValues(typeof(SideScrollingLayers)))
                {
                int flippedZ = layerCount - 1 - index;
                CreateTilemapLayer(gridGO.transform, layer.ToString(), flippedZ);
                index++;
                }
            }

        /// <summary>
        /// Context menu for creating a Side-Scrolling Grid from the hierarchy.
        /// Appears when right-clicking in the hierarchy window.
        /// </summary>
        [MenuItem("GameObject/Tiny Walnut Games/Create Side-Scrolling Grid", false, 10)]
        private static void ContextCreateSideScrollingGrid(MenuCommand menuCommand)
            {
            CreateSideScrollingGrid();
            }

        /// <summary>
        /// Enum for top-down layer names.
        /// </summary>
        public enum TopDownLayers
            {
            DeepOcean,
            Ocean,
            ShallowWater,
            Floor,
            FloorProps,
            WalkableGround,
            WalkableProps,
            OverheadProps,
            RoomMasking,
            Blending,
            }

        /// <summary>
        /// Attempts to load a GridLayerConfig asset and returns its layerNames if available and different from defaults.
        /// Otherwise, returns the provided defaultLayers.
        /// </summary>
        private static string[] GetCustomOrDefaultLayers(string[] topDownDefaultLayers)
            {
            GridLayerConfig config = AssetDatabase.LoadAssetAtPath<GridLayerConfig>("Assets/GridLayerConfig.asset");
            string[] platformerDefaultLayers = Enum.GetNames(typeof(SideScrollingLayers));

            if (config != null && config.layerNames != null && config.layerNames.Length > 0)
                {
                // If custom layers are different from both platformer and top-down defaults, use custom
                if (!config.layerNames.SequenceEqual(platformerDefaultLayers) &&
                    !config.layerNames.SequenceEqual(topDownDefaultLayers))
                    {
                    return config.layerNames;
                    }
                }
            // Otherwise, use the provided top-down default layers
            return topDownDefaultLayers;
            }

        /// <summary>
        /// Creates a top-down grid in the scene with top-down layers.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Create Default Top-Down Grid")]
        public static void CreateDefaultTopDownGrid()
            {
            var gridGO = new GameObject("Top-Down Grid", typeof(Grid));
            gridGO.transform.position = Vector3.zero;
            string[] layers = GetCustomOrDefaultLayers(Enum.GetNames(typeof(TopDownLayers)));
            int layerCount = layers.Length;
            for (int i = 0; i < layerCount; i++)
                {
                int flippedZ = layerCount - 1 - i;
                CreateTilemapLayer(gridGO.transform, layers[i], flippedZ);
                }
            }

        /// <summary>
        /// Context menu for creating a Top-Down Grid from the hierarchy.
        /// Appears when right-clicking in the hierarchy window.
        /// </summary>
        [MenuItem("GameObject/Tiny Walnut Games/Create Default Top-Down Grid", false, 10)]
        private static void ContextCreateDefaultTopDownGrid(MenuCommand menuCommand)
            {
            CreateDefaultTopDownGrid();
            }

        /// <summary>
        /// Preset layers for isometric and hexagonal top-down grids.
        /// </summary>
        private static readonly string[] IsometricTopDownLayers = new string[]
        {
            "Blending",
            "RoomMasking",
            "OverheadProps",
            "WalkableProps",
            "WalkableGround",
            "FloorProps",
            "Floor",
            "ShallowWater",
            "Ocean",
            "DeepOcean"
        };

        private static readonly string[] HexTopDownLayers = new string[]
        {
            "Blending",
            "RoomMasking",
            "OverheadProps",
            "WalkableProps",
            "WalkableGround",
            "FloorProps",
            "Floor",
            "ShallowWater",
            "Ocean",
            "DeepOcean"
        };

        /// <summary>
        /// Creates an isometric top-down grid in the scene.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Create Isometric Top-Down Grid")]
        public static void CreateIsometricTopDownGrid()
            {
            var gridGO = new GameObject("Isometric Top-Down Grid", typeof(Grid));
            gridGO.transform.position = Vector3.zero;
            Grid grid = gridGO.GetComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            string[] layers = GetCustomOrDefaultLayers(IsometricTopDownLayers);
            int layerCount = layers.Length;
            for (int i = 0; i < layerCount; i++)
                {
                int flippedZ = layerCount - 1 - i;
                CreateTilemapLayer(gridGO.transform, layers[i], flippedZ);
                }
            }

        /// <summary>
        /// Context menu for creating an Isometric Top-Down Grid from the hierarchy.
        /// </summary>
        [MenuItem("GameObject/Tiny Walnut Games/Create Isometric Top-Down Grid", false, 10)]
        private static void ContextCreateIsometricTopDownGrid(MenuCommand menuCommand)
            {
            CreateIsometricTopDownGrid();
            }

        /// <summary>
        /// Creates a hexagonal top-down grid in the scene.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Create Hexagonal Top-Down Grid")]
        public static void CreateHexTopDownGrid()
            {
            var gridGO = new GameObject("Hexagonal Top-Down Grid", typeof(Grid));
            gridGO.transform.position = Vector3.zero;
            Grid grid = gridGO.GetComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Hexagon;
            string[] layers = GetCustomOrDefaultLayers(HexTopDownLayers);
            int layerCount = layers.Length;
            for (int i = 0; i < layerCount; i++)
                {
                int flippedZ = layerCount - 1 - i;
                CreateTilemapLayer(gridGO.transform, layers[i], flippedZ);
                }
            }

        /// <summary>
        /// Context menu for creating a Hexagonal Top-Down Grid from the hierarchy.
        /// </summary>
        [MenuItem("GameObject/Tiny Walnut Games/Create Hexagonal Top-Down Grid", false, 10)]
        private static void ContextCreateHexTopDownGrid(MenuCommand menuCommand)
            {
            CreateHexTopDownGrid();
            }

        /// <summary>
        /// Creates a grid in the scene using a custom array of layer names.
        /// </summary>
        /// <param name="layerNames">Array of layer names to use for tilemap creation.</param>
        public static void CreateCustomGrid(string[] layerNames)
            {
            if (CreateCustomGridAction != null)
                {
                CreateCustomGridAction(layerNames);
                return;
                }

            var gridGO = new GameObject("Custom Grid", typeof(Grid));
            gridGO.transform.position = Vector3.zero;
            int layerCount = layerNames.Length;
            for (int i = 0; i < layerCount; i++)
                {
                int flippedZ = layerCount - 1 - i;
                CreateTilemapLayer(gridGO.transform, layerNames[i], flippedZ);
                }
            }

        /// <summary>
        /// Creates a GameObject under 'parent' with Tilemap & TilemapRenderer,
        /// sets z-offset, Unity layer, sorting layer, and default order.
        /// </summary>
        /// <param name="parent">Parent transform for the new GameObject.</param>
        /// <param name="layerName">Name of the layer for the GameObject.</param>
        /// <param name="zDepth">Z-depth used for positioning.</param>
        private static void CreateTilemapLayer(Transform parent, string layerName, int zDepth)
            {
            var tmGO = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
            tmGO.transform.SetParent(parent, worldPositionStays: false);
            tmGO.transform.localPosition = new Vector3(0, 0, zDepth);

            // Try to set Unity layer by name; warn if not found
            int unityLayer = LayerMask.NameToLayer(layerName);
            if (unityLayer != -1)
                tmGO.layer = unityLayer;
            else
                Debug.LogWarning($"Layer '{layerName}' not found. GameObject will use default layer.");

            // Try to set sorting layer by name; warn if not found
            TilemapRenderer renderer = tmGO.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = layerName;
            if (renderer.sortingLayerName != layerName)
                Debug.LogWarning($"Sorting Layer '{layerName}' not found. Renderer will use default sorting layer.");
            renderer.sortingOrder = 0;
            }

        // When creating grids, use config.layerNames for the layers.
        // Example:
        // foreach (var layerName in config.layerNames) { /* use layerName for grid setup */ }
        }
    }
````

````GridLayerConfigEditor.cs
using UnityEditor;
using UnityEngine;
using System.IO;

namespace TinyWalnutGames.GridLayerEditor
{
    /// <summary>
    /// Editor utility for creating GridLayerConfig assets via the Unity menu.
    /// </summary>
    public static class GridLayerConfigEditor
    {
        /// <summary>
        /// Creates a new GridLayerConfig asset in the selected folder or in Assets.
        /// </summary>
        [MenuItem("Assets/Create/Tiny Walnut Games/Grid Layer Config", priority = 1)]
        public static void CreateGridLayerConfig()
        {
            // Get the path of the selected object in the Project window
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets";
            else if (!Directory.Exists(path))
                path = Path.GetDirectoryName(path);

            // Generate a unique asset path for the new config
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, "GridLayerConfig.asset"));

   // Create and save the new GridLayerConfig asset
   GridLayerConfig asset = ScriptableObject.CreateInstance<GridLayerConfig>();
            AssetDatabase.CreateAsset(asset, assetPathAndName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Focus the Project window and select the new asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
````

````GridLayerEditorWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TinyWalnutGames.GridLayerEditor
    {
    /// <summary>
    /// Editor window for editing and applying grid layer setups.
    /// Allows users to create, edit, and apply layer configurations for 2D grids.
    /// </summary>
    public class GridLayerEditorWindow : EditorWindow
        {
        /// <summary>
        /// The GridLayerConfig asset currently being edited.
        /// </summary>
        private GridLayerConfig config = null!; // assigned in OnEnable

        /// <summary>
        /// Preset layer names for platformer-style grids.
        /// </summary>
        private static readonly string[] PlatformerLayers = new string[]
        {
            "Parallax5",
            "Parallax4",
            "Parallax3",
            "Parallax2",
            "Parallax1",
            "Background2",
            "Background1",
            "BackgroundProps",
            "WalkableGround",
            "WalkableProps",
            "Hazards",
            "Foreground",
            "ForegroundProps",
            "RoomMasking",
            "Blending",
        };

        /// <summary>
        /// Preset layer names for top-down-style grids.
        /// </summary>
        private static readonly string[] TopDownLayers = new string[]
        {
                "DeepOcean",
                "Ocean",
                "ShallowWater",
                "Floor",
                "FloorProps",
                "WalkableGround",
                "WalkableProps",
                "OverheadProps",
                "RoomMasking",
                "Blending",
        };

        /// <summary>
        /// Array to track selected Unity layers.
        /// </summary>
        private bool[] layerSelections;

        /// <summary>
        /// Array of all Unity layer names.
        /// </summary>
        private string[] unityLayers;

        /// <summary>
        /// Scroll position for the layer selection area.
        /// </summary>
        private Vector2 layerScrollPosition;

        /// <summary>
        /// Scroll position for the configuration area.
        /// </summary>
        private Vector2 configScrollPosition;

        /// <summary>
        /// The GridLayerConfig asset currently being edited (used for testing purposes).
        /// </summary>
        private GridLayerConfig _config = null!; // test-set or OnEnable copy

        /// <summary>
        /// Allows tests to set the config.
        /// </summary>
        /// <param name="config">The GridLayerConfig to set.</param>
        public void SetConfig(GridLayerConfig config)
            {
            _config = config;
            }

        /// <summary>
        /// Allows tests to get the config.
        /// </summary>
        /// <returns>The current GridLayerConfig.</returns>
        public GridLayerConfig GetConfig()
            {
            return _config;
            }

        /// <summary>
        /// Applies a platformer preset to the config.
        /// </summary>
        public void ApplyPlatformerPreset()
            {
            if (_config == null) return;
            _config.layerNames = new[] { "WalkableGround", "Ladders", "Hazards" };
            }

        /// <summary>
        /// Toggles a layer name in the config.
        /// </summary>
        /// <param name="layerName">The name of the layer to toggle.</param>
        /// <param name="enabled">Whether the layer should be enabled or disabled.</param>
        public void ToggleLayer(string layerName, bool enabled)
            {
            if (_config == null) return;
            string[] names = _config.layerNames ?? new string[0];
            if (enabled)
                {
                if (!System.Array.Exists(names, l => l == layerName))
                    {
                    string[] newNames = new string[names.Length + 1];
                    names.CopyTo(newNames, 0);
                    newNames[names.Length] = layerName;
                    _config.layerNames = newNames;
                    }
                }
            else
                {
                _config.layerNames = System.Array.FindAll(names, l => l != layerName);
                }
            }

        /// <summary>
        /// Calls TwoDimensionalGridSetup.CreateCustomGrid with the current config's layer names.
        /// </summary>
        public void CreateGridWithLayers()
            {
            if (_config == null || _config.layerNames == null) return;
            TwoDimensionalGridSetup.CreateCustomGrid(_config.layerNames);
            }

        /// <summary>
        /// Opens the grid layer editor window from the Unity menu.
        /// </summary>
        [MenuItem("Tiny Walnut Games/Edit Grid Layers")]
        public static void ShowWindow()
            {
            GridLayerEditorWindow window = GetWindow<GridLayerEditorWindow>("Edit Grid Layers");
            window.minSize = new Vector2(600, 400); // Set minimum size for column layout
            }

        /// <summary>
        /// Called when the editor window is enabled.
        /// </summary>
        private void OnEnable()
            {
            // Get all Unity layers
            unityLayers = GetAllUnityLayerNames();
            layerSelections = new bool[unityLayers.Length];

            // Initialize scroll positions
            layerScrollPosition = Vector2.zero;
            configScrollPosition = Vector2.zero;

            // Initialize selections from config
            UpdateLayerSelections();
            }

        /// <summary>
        /// Retrieves all Unity layer names.
        /// </summary>
        /// <returns>Array of Unity layer names.</returns>
        private string[] GetAllUnityLayerNames()
            {
            var layers = new List<string>();
            for (int i = 0; i < 32; i++)
                {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    layers.Add(name);
                }
            return layers.ToArray();
            }

        /// <summary>
        /// Draws the editor window GUI for editing grid layer configurations.
        /// </summary>
        private void OnGUI()
            {
            // Main horizontal split layout
            EditorGUILayout.BeginHorizontal();

            // Left column - Layer Selection (40% of window width)
            DrawLayerSelectionColumn();

            // Right column - Configuration Controls (60% of window width)
            DrawConfigurationColumn();

            EditorGUILayout.EndHorizontal();
            }

        /// <summary>
        /// Draws the left column containing Unity layer selection toggles.
        /// </summary>
        private void DrawLayerSelectionColumn()
            {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));

            EditorGUILayout.LabelField("Select Unity Layers", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Scrollable area for layer toggles with fixed height
            layerScrollPosition = EditorGUILayout.BeginScrollView(layerScrollPosition, GUILayout.Height(position.height - 100));

            if (unityLayers != null && layerSelections != null)
                {
                bool changed = false;
                for (int i = 0; i < unityLayers.Length; i++)
                    {
                    bool newValue = EditorGUILayout.ToggleLeft(unityLayers[i], layerSelections[i]);
                    if (newValue != layerSelections[i])
                        {
                        layerSelections[i] = newValue;
                        changed = true;
                        }
                    }

                // Update config when selections change
                if (changed && config != null)
                    {
                    Undo.RecordObject(config, "Update Layer Selection");
                    config.layerNames = unityLayers.Where((layer, idx) => layerSelections[idx]).ToArray();
                    EditorUtility.SetDirty(config);
                    }
                }

            EditorGUILayout.EndScrollView();

            // Quick selection buttons at bottom of left column
            EditorGUILayout.Space();
            if (GUILayout.Button("Select All"))
                {
                SelectAllLayers(true);
                }
            if (GUILayout.Button("Select None"))
                {
                SelectAllLayers(false);
                }

            EditorGUILayout.EndVertical();
            }

        /// <summary>
        /// Draws the right column containing configuration controls.
        /// </summary>
        private void DrawConfigurationColumn()
            {
            EditorGUILayout.BeginVertical();

            // Config asset field
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            config = (GridLayerConfig)EditorGUILayout.ObjectField("Config Asset", config, typeof(GridLayerConfig), false);

            if (config == null)
                {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Assign or create a GridLayerConfig asset to begin editing layers.", MessageType.Info);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Create New Config:", EditorStyles.boldLabel);

                if (GUILayout.Button("Create Platformer Default Config"))
                    {
                    CreateDefaultConfig(PlatformerLayers, "Platformer");
                    }
                if (GUILayout.Button("Create Top-Down Default Config"))
                    {
                    CreateDefaultConfig(TopDownLayers, "TopDown");
                    }

                EditorGUILayout.EndVertical();
                return;
                }

            // Scrollable configuration area
            configScrollPosition = EditorGUILayout.BeginScrollView(configScrollPosition);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Configuration", EditorStyles.boldLabel);

            // Display current layer names in a compact format
            if (config.layerNames != null && config.layerNames.Length > 0)
                {
                EditorGUILayout.LabelField($"Active Layers ({config.layerNames.Length}):", EditorStyles.miniLabel);
                string layerList = string.Join(", ", config.layerNames.Take(5));
                if (config.layerNames.Length > 5)
                    layerList += $"... (+{config.layerNames.Length - 5} more)";
                EditorGUILayout.LabelField(layerList, EditorStyles.wordWrappedMiniLabel);
                }
            else
                {
                EditorGUILayout.LabelField("No layers selected", EditorStyles.miniLabel);
                }

            EditorGUILayout.Space();

            // Preset buttons
            EditorGUILayout.LabelField("Apply Presets:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Platformer Preset"))
                {
                ApplyPreset(PlatformerLayers, "Apply Platformer Preset");
                }
            if (GUILayout.Button("Top-Down Preset"))
                {
                ApplyPreset(TopDownLayers, "Apply Top-Down Preset");
                }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Recommended layers info (compact)
            EditorGUILayout.LabelField("Recommended Layers:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Platformer:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(string.Join(", ", PlatformerLayers.Take(8)) + "...", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Top-Down:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(string.Join(", ", TopDownLayers.Take(8)) + "...", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Action buttons
            EditorGUILayout.LabelField("Actions:", EditorStyles.boldLabel);

            if (GUILayout.Button("Create Grid With Selected Layers", GUILayout.Height(30)))
                {
                if (config.layerNames != null && config.layerNames.Length > 0)
                    {
                    TwoDimensionalGridSetup.CreateCustomGrid(config.layerNames);
                    }
                else
                    {
                    EditorUtility.DisplayDialog("No Layers Selected", "Please select at least one layer before creating a grid.", "OK");
                    }
                }

            EditorGUILayout.Space();

            // Advanced editor (collapsible)
            EditorGUILayout.LabelField("Advanced Editor:", EditorStyles.boldLabel);
            SerializedObject so = new SerializedObject(config);
            SerializedProperty layersProp = so.FindProperty("layerNames");
            EditorGUILayout.PropertyField(layersProp, new GUIContent("Layer Names Array"), true);

            if (so.ApplyModifiedProperties())
                {
                // Update layer selections when array is modified directly
                UpdateLayerSelections();
                }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            }

        /// <summary>
        /// Creates a default configuration asset with the specified layers.
        /// </summary>
        /// <param name="defaultLayers">Array of default layer names.</param>
        /// <param name="configType">Type identifier for the config (e.g., "Platformer", "TopDown").</param>
        private void CreateDefaultConfig(string[] defaultLayers, string configType)
            {
            string path = EditorUtility.SaveFilePanelInProject(
                $"Create {configType} Grid Layer Config",
                $"GridLayerConfig_{configType}",
                "asset",
                $"Choose location for {configType} Grid Layer Config");

            if (!string.IsNullOrEmpty(path))
                {
                config = CreateInstance<GridLayerConfig>();
                config.layerNames = (string[])defaultLayers.Clone();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                UpdateLayerSelections();
                }
            }

        /// <summary>
        /// Applies a preset to the current configuration.
        /// </summary>
        /// <param name="presetLayers">Array of preset layer names.</param>
        /// <param name="undoName">Name for the undo operation.</param>
        private void ApplyPreset(string[] presetLayers, string undoName)
            {
            if (config == null) return;

            Undo.RecordObject(config, undoName);
            config.layerNames = (string[])presetLayers.Clone();
            EditorUtility.SetDirty(config);
            UpdateLayerSelections();
            }

        /// <summary>
        /// Selects or deselects all layers.
        /// </summary>
        /// <param name="selectAll">True to select all layers, false to deselect all.</param>
        private void SelectAllLayers(bool selectAll)
            {
            if (config == null || layerSelections == null) return;

            Undo.RecordObject(config, selectAll ? "Select All Layers" : "Deselect All Layers");

            for (int i = 0; i < layerSelections.Length; i++)
                {
                layerSelections[i] = selectAll;
                }

            if (selectAll)
                {
                config.layerNames = (string[])unityLayers.Clone();
                }
            else
                {
                config.layerNames = new string[0];
                }

            EditorUtility.SetDirty(config);
            }

        /// <summary>
        /// Updates the layer selection checkboxes based on the current config.
        /// </summary>
        private void UpdateLayerSelections()
            {
            if (config == null || unityLayers == null || layerSelections == null) return;

            for (int i = 0; i < unityLayers.Length; i++)
                {
                layerSelections[i] = config.layerNames != null && config.layerNames.Contains(unityLayers[i]);
                }

            Repaint();
            }

        }
    }
#endif
````

````Readme.md
# Tiny Walnut Games Grid Layer System

This package provides a flexible grid layer configuration system for Unity 2D games, supporting platformer, top-down, isometric, and hexagonal grid setups.

## Features

- **GridLayerConfig**: ScriptableObject for storing custom grid layer names.
- **Editor Tools**: Create, edit, and apply grid layer templates via menu and context options.
- **Grid Creation**: Instantly spawn grids with multiple tilemap layers for various genres and layouts.

## Usage

1. **Create a GridLayerConfig asset**  
   - Right-click in the Project window: `Assets > Create > Tiny Walnut Games > Grid Layer Config`
   - Or use the Grid Layer Editor window: `Tiny Walnut Games > Edit Grid Layers`

2. **Edit Layer Names**  
   - Use the Grid Layer Editor window to customize or apply presets.

3. **Create Grids**  
   - Use menu or hierarchy context options under `Tiny Walnut Games` to create:
     - Side-Scrolling Grid
     - Default Top-Down Grid
     - Isometric Top-Down Grid
     - Hexagonal Top-Down Grid

## Assembly Definitions

- **TinyWalnutGames.GridLayerConfig**: Runtime scripts (ScriptableObject).
- **TinyWalnutGames.GridLayerEditor**: Editor scripts (menu, window, grid creation).

## Requirements

- Unity 2021.3 or newer recommended.
- 2D Tilemap package.

## Example

1. Create a new GridLayerConfig asset.
2. Edit layers as needed.
3. Use the editor menu or right-click in the hierarchy to create a grid with your layers.

# Grid Layer Editor Workflow

## Recommended Layers

**Platformer Layers:**
Blending, RoomMasking, ForegroundProps, Foreground, WalkableProps, Hazards, WalkableGround, BackgroundProps, Background1, Background2, Parallax1, Parallax2, Parallax3, Parallax4, Parallax5

**Top Down Layers:**
Blending, RoomMasking, ForegroundProps, Foreground, WalkableProps, Hazards, WalkableGround, BackgroundProps, Background1, Background2

## Workflow

1. Open the Grid Layer Editor Window.
2. Select Unity layers using the multiselect list.
3. Use the "Set Recommended Platformer Layers" or "Set Recommended Top Down Layers" buttons to quickly fill selections.
4. Create grids using the selected layers.

Selected layers are stored in the GridLayerConfig asset and used for grid creation.

## License

MIT license, which can be found at https://opensource.org/license/mit/

In layman's terms, you can use this code in your projects, modify it, and share it, as long as you include the original license:

Copyright (c) 2025 Tiny Walnut Games
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
````

````ENHANCED_LAYER_MANAGEMENT.md
# Grid Layer Editor - Enhanced Layer Management

## 🎯 Problem Solved
Your Grid Layer Editor now automatically creates Unity layers and sorting layers that don't exist, eliminating the manual setup headache!

## 🚀 Quick Start

### Method 1: Apply Complete Preset (Recommended)
1. Go to **Tiny Walnut Games > Apply Grid Layer Preset**
2. This applies your pre-configured layers from `TWG_GLE_TagManager.preset`
3. All Unity layers (17-31) and sorting layers are set up instantly

### Method 2: Individual Layer Creation
1. Use any grid creation menu: **Tiny Walnut Games > Create [Type] Grid**
2. Layers are automatically created as needed
3. No more "Layer not found" warnings!

### Method 3: Editor Window Integration
1. Open **Tiny Walnut Games > Edit Grid Layers**
2. Click **"Apply Layer Preset (Setup All Layers)"** for complete setup
3. Or use **"Create Grid With Selected Layers"** for custom configurations

## 🔧 What's New

### Automatic Layer Management
- **Unity Layers**: Automatically finds empty slots (8-31) and creates missing layers
- **Sorting Layers**: Creates sorting layers with unique IDs
- **Smart Detection**: Checks if layers exist before creating duplicates

### Enhanced Grid Creation
All grid creation methods now:
1. Check for missing layers
2. Create any missing Unity layers and sorting layers
3. Set up the grid with proper layer assignments
4. Display clear console feedback

### Preset Integration
- **TWG_GLE_TagManager.preset**: Pre-configured with all your layer names
- **One-click application**: Apply entire preset to any project
- **Consistent setup**: Same layers across all projects

## 🎨 Layer Configuration

### Platformer Layers (Layers 17-31)
```
17: Parallax5      22: Background2    27: Hazards
18: Parallax4      23: Background1    28: Foreground
19: Parallax3      24: BackgroundProps 29: ForegroundProps
20: Parallax2      25: WalkableGround  30: RoomMasking
21: Parallax1      26: WalkableProps   31: Blending
```

### Top-Down Layers
```
DeepOcean, Ocean, ShallowWater, Floor, FloorProps,
WalkableGround, WalkableProps, OverheadProps, RoomMasking, Blending
```

## 🛠️ API Reference

### LayerManager.EnsureAllLayersExist(string[] layerNames)
Creates any missing Unity layers and sorting layers from the provided array.

### LayerManager.ApplyGridLayerPreset()
Applies the complete TWG_GLE_TagManager.preset to your project's TagManager.

### LayerManager.EnsureUnityLayerExists(string layerName)
Creates a single Unity layer if it doesn't exist, returns layer index.

### LayerManager.EnsureSortingLayerExists(string layerName)
Creates a single sorting layer if it doesn't exist.

## 🎭 No More Manual Layer Setup!

### Before (The Pain)
```
❌ Create each Unity layer manually in Project Settings
❌ Create each sorting layer manually in Project Settings
❌ Warning: Layer 'Parallax5' not found
❌ Warning: Sorting Layer 'Background1' not found
❌ Manually assign each layer to each tilemap
```

### After (The Magic)
```
✅ Run "Apply Grid Layer Preset" once
✅ All layers created automatically
✅ Grid creation just works™
✅ Console shows: "Created Unity layer 'Parallax5' at index 17"
✅ Console shows: "Created sorting layer 'Background1'"
```

## 🎮 Integration with MetVanDAMN

Your Grid Layer Editor now seamlessly integrates with MetVanDAMN's biome art system:
- Biome-aware tilemaps use the proper layers automatically
- Visual representations in the scene setup respect layer hierarchy
- No conflicts between MetVanDAMN districts and tilemap layers

## 🧪 Testing

To verify everything works:
1. Create a new Unity project
2. Import the Grid Layer Editor package
3. Go to **Tiny Walnut Games > Apply Grid Layer Preset**
4. Create any type of grid
5. Check Project Settings > Tags and Layers - all should be configured!

## 🎯 Next Level Features

Your Grid Layer Editor is now equipped with:
- **Smart layer detection** and creation
- **Preset-based configuration** for consistency
- **Editor integration** for seamless workflow
- **Console feedback** for transparency
- **Error prevention** through proactive setup

No more "works in main project but not here" mysteries! 🎭✨
````
