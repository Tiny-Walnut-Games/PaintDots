using UnityEngine;

namespace PaintDots.ECS.ABCs
{
    // Attach to a GameObject in the scene to provide the PaletteBindingAsset to runtime
    public class PaletteBindingAuthoring : MonoBehaviour
    {
        public PaletteBindingAsset BindingAsset;
    }
}
