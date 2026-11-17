using System.Collections.Generic;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Material factory. Templates in Resources/Materials (created by KPL › Setup) carry the exact
    /// URP shader keywords each kind needs, so their variants survive build stripping.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Templates = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> Shared = new Dictionary<string, Material>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Smooth = Shader.PropertyToID("_Smoothness");
        static readonly int Metal = Shader.PropertyToID("_Metallic");
        static readonly int Bump = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int DetailAlbedo = Shader.PropertyToID("_DetailAlbedoMap");
        static readonly int DetailNormal = Shader.PropertyToID("_DetailNormalMap");
        static readonly int DetailScale = Shader.PropertyToID("_DetailNormalMapScale");
        static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

        public static Material Template(string name)
        {
            if (Templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                // Editor fallback before KPL › Setup has created the assets.
                var sh = Shader.Find(name.StartsWith("Unlit") ? "Universal Render Pipeline/Unlit" : name == "Sky" ? "Skybox/Panoramic" : "Universal Render Pipeline/Lit");
                m = new Material(sh) { name = name };
                if (name.Contains("Normal") || name.Contains("Detail")) m.EnableKeyword("_NORMALMAP");
                if (name.Contains("Detail")) m.EnableKeyword("_DETAIL_MULX2");
                if (name.Contains("Emissive")) m.EnableKeyword("_EMISSION");
            }
            Templates[name] = m;
            return m;
        }

        public static Material Lit(Color c, float smooth = 0.3f, float metal = 0, Texture tex = null, Vector2? tiling = null)
        {
            var m = new Material(Template("Lit"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            m.SetFloat(Metal, metal);
            if (tex != null) m.SetTexture(BaseMap, tex);
            if (tiling != null) m.SetTextureScale(BaseMap, tiling.Value);
            return m;
        }

        public static Material LitNormal(Color c, Texture tex, Texture normal, float smooth = 0.3f, float bump = 1, Vector2? tiling = null, float metal = 0)
        {
            if (normal == null || Quality.Current == Gfx.Low) return Lit(c, smooth, metal, tex, tiling);
            var m = new Material(Template("LitNormal"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            m.SetFloat(Metal, metal);
            if (tex != null) m.SetTexture(BaseMap, tex);
            m.SetTexture(Bump, normal);
            m.SetFloat(BumpScale, bump);
            if (tiling != null) m.SetTextureScale(BaseMap, tiling.Value);
            return m;
        }

        /// <summary>Large-scale colour map plus a tiled detail albedo/normal for close-up texture.</summary>
        public static Material LitDetail(Texture baseMap, Texture detailAlbedo, Texture detailNormal, Vector2 detailTiling, float smooth = 0.15f, Texture normal = null)
        {
            if (Quality.Current == Gfx.Low) return Lit(Color.white, smooth, 0, baseMap);
            var m = new Material(Template("LitDetail"));
            m.SetColor(BaseColor, Color.white);
            m.SetTexture(BaseMap, baseMap);
            m.SetFloat(Smooth, smooth);
            m.SetFloat(Metal, 0);
            m.SetTexture(DetailAlbedo, detailAlbedo);
            m.SetTexture(DetailNormal, detailNormal);
            m.SetFloat(DetailScale, 1f);
            m.SetTextureScale(DetailAlbedo, detailTiling);
            if (normal != null) m.SetTexture(Bump, normal);
            else m.SetTexture(Bump, Tex.FlatNormal);
            return m;
        }

        public static Material Emissive(Color c, Color glow, Texture tex = null, float smooth = 0.4f)
        {
            var m = new Material(Template("LitEmissive"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.SetTexture(EmissionMap, tex);
            }
            m.SetColor(Emission, glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public static Material Cutout(Color c, Texture tex, float cutoff = 0.5f, float smooth = 0.2f)
        {
            var m = new Material(Template("LitCutout"));
            m.SetColor(BaseColor, c);
            m.SetTexture(BaseMap, tex);
            m.SetFloat(Cutoff, cutoff);
            m.SetFloat(Smooth, smooth);
            return m;
        }

        public static Material Transparent(Color c, float smooth = 0.6f)
        {
            var m = new Material(Template("LitTransparent"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            var m = new Material(Template("Unlit"));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material UnlitTransparent(Color c, Texture tex = null)
        {
            var m = new Material(Template("UnlitTransparent"));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Additive(Color c, Texture tex = null)
        {
            var m = new Material(Template("UnlitAdditive"));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        /// <summary>Cached material shared between renderers (key must describe it fully).</summary>
        public static Material Cached(string key, System.Func<Material> make)
        {
            if (Shared.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            m.name = key;
            Shared[key] = m;
            return m;
        }

        public static void SetEmission(Material m, Color c) => m.SetColor(Emission, c);
        public static void SetBase(Material m, Texture t) => m.SetTexture(BaseMap, t);
        public static void SetColor(Material m, Color c) => m.SetColor(BaseColor, c);

        public static Color Hex(uint argb) => Pal.Argb(argb);
    }
}
