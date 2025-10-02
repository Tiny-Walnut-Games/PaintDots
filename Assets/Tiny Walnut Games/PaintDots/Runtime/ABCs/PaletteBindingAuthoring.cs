using UnityEngine;

namespace PaintDots.Runtime.ABCs
{
    // Attach to a GameObject in the scene to provide the PaletteBindingAsset to runtime
    public class PaletteBindingAuthoring : MonoBehaviour
    {
        public PaletteBindingAsset BindingAsset;
    }
}
