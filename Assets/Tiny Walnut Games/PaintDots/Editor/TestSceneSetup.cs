using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaintDots.ECS.Authoring;
using Unity.Mathematics;
using System.IO;

namespace PaintDots.Editor
{
    /// <summary>
    /// Small editor helper to generate a test scene with a TilemapAuthoring GameObject so entering Play Mode produces a usable ECS world.
    /// </summary>
    public static class TestSceneSetup
    {
        [MenuItem("PaintDots/Tests/Create Tilemap Test Scene")]
        public static void CreateTestScene()
        {
            // Ensure folder exists
            string testFolder = "Assets/Tiny Walnut Games/PaintDots/Tests";
            if (!Directory.Exists(testFolder)) Directory.CreateDirectory(testFolder);

            // Create a simple material for tile visuals
            string matPath = Path.Combine(testFolder, "TestTileMat.mat").Replace("\\", "/");
            Material mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, matPath);

            // Create a simple prefab to act as a tile prefab
            GameObject tilePrefab = new GameObject("TilePrefab");
            tilePrefab.AddComponent<SpriteRenderer>();
            string prefabPath = Path.Combine(testFolder, "TilePrefab.prefab").Replace("\\", "/");
            var prefab = PrefabUtility.SaveAsPrefabAsset(tilePrefab, prefabPath);
            Object.DestroyImmediate(tilePrefab);

            // Create a new scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create a GameObject with TilemapAuthoring
            GameObject go = new GameObject("TilemapTestRoot");
            var authoring = go.AddComponent<TilemapAuthoring>();
            // small map for fast iteration
            authoring.MapSize = new int2(16, 16);
            // Assign the material
            authoring.TileMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            // Assign the prefab as the single TilePrefab
            authoring.TilePrefabs = new GameObject[] { AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) };

            // Save the scene
            string scenePath = Path.Combine(testFolder, "TilemapPainter_TestScene.unity").Replace("\\", "/");
            EditorSceneManager.SaveScene(scene, scenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Test Scene Created", $"Test scene saved to: {scenePath}\nOpen the scene and enter Play Mode to run with a valid ECS world.", "OK");
        }
    }
}
