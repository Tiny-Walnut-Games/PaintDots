using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PaintDots.ECS;
using PaintDots.ECS.Utilities;

namespace PaintDots.ECS.Validation
{
    /// <summary>
    /// Validation utilities for multi-tile functionality
    /// </summary>
    public static class MultiTileValidation
    {
        /// <summary>
        /// Validates that footprint overlap detection works correctly
        /// </summary>
        public static bool ValidateFootprintOverlap()
        {
            // Test case 1: Non-overlapping footprints
            var result1 = GridOccupancyManager.FootprintsOverlap(
                new int2(0, 0), new int2(2, 2),  // 2x2 at (0,0)
                new int2(3, 0), new int2(2, 2)   // 2x2 at (3,0)
            );
            if (result1) return false; // Should not overlap

            // Test case 2: Overlapping footprints
            var result2 = GridOccupancyManager.FootprintsOverlap(
                new int2(0, 0), new int2(3, 3),  // 3x3 at (0,0)
                new int2(2, 2), new int2(2, 2)   // 2x2 at (2,2)
            );
            if (!result2) return false; // Should overlap

            // Test case 3: Adjacent but not overlapping
            var result3 = GridOccupancyManager.FootprintsOverlap(
                new int2(0, 0), new int2(2, 2),  // 2x2 at (0,0) covers (0,0) to (1,1)
                new int2(2, 0), new int2(2, 2)   // 2x2 at (2,0) covers (2,0) to (3,1)
            );
            if (result3) return false; // Should not overlap (adjacent)

            return true;
        }

        /// <summary>
        /// Validates PaintCommand factory methods work correctly
        /// </summary>
        public static bool ValidatePaintCommandFactory()
        {
            // Test single tile command
            var singleTile = PaintCommand.SingleTile(new int2(5, 5), 10);
            if (singleTile.IsMultiTile) return false;
            if (!singleTile.GridPosition.Equals(new int2(5, 5))) return false;
            if (singleTile.TileID != 10) return false;

            // Test multi-tile command
            var multiTile = PaintCommand.MultiTile(new int2(10, 10), 20, new int2(3, 2));
            if (!multiTile.IsMultiTile) return false;
            if (!multiTile.GridPosition.Equals(new int2(10, 10))) return false;
            if (multiTile.TileID != 20) return false;
            if (!multiTile.Size.Equals(new int2(3, 2))) return false;

            return true;
        }

        /// <summary>
        /// Validates that occupied cell buffer operations work correctly
        /// </summary>
        public static bool ValidateOccupiedCellOperations()
        {
            // Test implicit conversions
            var cell = new OccupiedCell(new int2(5, 10));
            int2 position = cell; // Implicit conversion
            if (!position.Equals(new int2(5, 10))) return false;

            OccupiedCell cell2 = new int2(15, 20); // Implicit conversion
            if (!cell2.Position.Equals(new int2(15, 20))) return false;

            return true;
        }

        /// <summary>
        /// Validates Footprint component functionality
        /// </summary>
        public static bool ValidateFootprintComponent()
        {
            var footprint = new Footprint(new int2(5, 5), new int2(3, 2));
            
            if (!footprint.Origin.Equals(new int2(5, 5))) return false;
            if (!footprint.Size.Equals(new int2(3, 2))) return false;
            
            return true;
        }

        /// <summary>
        /// Validates StructureVisual component functionality
        /// </summary>
        public static bool ValidateStructureVisual()
        {
            var mockEntity = new Entity { Index = 123, Version = 1 };
            var structureVisual = new StructureVisual(mockEntity, new int2(4, 3));
            
            if (structureVisual.Prefab.Index != 123) return false;
            if (!structureVisual.Size.Equals(new int2(4, 3))) return false;
            
            return true;
        }

        /// <summary>
        /// Runs all validation tests
        /// </summary>
        public static bool RunAllValidations()
        {
            return ValidateFootprintOverlap() &&
                   ValidatePaintCommandFactory() &&
                   ValidateOccupiedCellOperations() &&
                   ValidateFootprintComponent() &&
                   ValidateStructureVisual();
        }
    }

    /// <summary>
    /// System that runs validation tests on startup in debug builds
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ValidationTestSystem : SystemBase
    {
        private bool _validationRun = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = UnityEngine.Debug.isDebugBuild;
        }

        protected override void OnUpdate()
        {
            if (_validationRun) return;
            
            var allTestsPassed = MultiTileValidation.RunAllValidations();
            
            if (allTestsPassed)
            {
                UnityEngine.Debug.Log("[MultiTile] All validation tests passed!");
            }
            else
            {
                UnityEngine.Debug.LogError("[MultiTile] Some validation tests failed!");
            }
            
            _validationRun = true;
        }
    }
}