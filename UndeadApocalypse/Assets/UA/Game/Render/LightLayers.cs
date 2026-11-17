using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UA.Game
{
    /// <summary>
    /// Rendering-layer masks. World renderers use bit 0 (Unity's default); the first-person weapon,
    /// hands and the armory preview use bit 1. Lights only affect renderers that share a bit, so the
    /// flashlight never blows out the viewmodel and the viewmodel rig never lights the world.
    /// </summary>
    public static class LightLayers
    {
        public const uint World = 1u, Viewmodel = 2u;

        public static void Set(Light l, uint mask)
        {
            if (l == null) return;
            var data = l.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = mask;
            // Classic culling mask too (respected by the Forward path).
            l.cullingMask = mask == Viewmodel ? 1 << Layers.Viewmodel : ~(1 << Layers.Viewmodel);
            // Cameras cull lights by GameObject layer as well: the viewmodel overlay camera only sees the Viewmodel layer.
            if (mask == Viewmodel) l.gameObject.layer = Layers.Viewmodel;
        }

        public static void Set(Renderer r, uint mask)
        {
            if (r != null) r.renderingLayerMask = mask;
        }
    }
}
