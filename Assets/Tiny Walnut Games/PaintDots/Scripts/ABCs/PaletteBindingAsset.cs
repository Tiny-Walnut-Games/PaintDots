using System.Collections.Generic;
using UnityEngine;

namespace PaintDots.ECS.ABCs
{
    [CreateAssetMenu(menuName = "PaintDots/ABCs/PaletteBinding")]
    public class PaletteBindingAsset : ScriptableObject
    {
        public int NumPhases = 6;

        [System.Serializable]
        public class FamilyEntry
        {
            public int FamilyId = -1;
            // sprite/tile indices per phase (length should be NumPhases)
            public List<int> PhaseSpriteIndices = new List<int>();
        }

        public List<FamilyEntry> Families = new List<FamilyEntry>();
    }
}
