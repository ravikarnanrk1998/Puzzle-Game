using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>
    /// Material factory. Templates in Resources/Materials (created by UA › Setup) carry the exact URP
    /// shader keywords each kind needs, so their variants survive build stripping.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Templates = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> Shared = new Dictionary<string, Material>();
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Smooth = Shader.PropertyToID("_Smoothness");
        static readonly int Metal = Shader.PropertyToID("_Metallic");
        static readonly int Bump = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        public static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int MetalMap = Shader.PropertyToID("_MetallicGlossMap");
        static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

        /// <summary>Template names: Lit, LitNormal, LitWet, LitMetal, LitEmissive, LitCutout, LitTransparent, Unlit, UnlitTransparent, UnlitAdditive, ParticleAdd, ParticleAlpha, Sky.</summary>
        public static Material Template(string name)
        {
            if (Templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null) m = Fallback(name);
            Templates[name] = m;
            return m;
        }

        static Material Fallback(string name)
        {
            // Used in the editor before UA › Setup has created the template assets.
            string sh = name.StartsWith("Particle") ? "Universal Render Pipeline/Particles/Unlit" : name.StartsWith("Unlit") ? "Universal Render Pipeline/Unlit" : name == "Sky" ? "Skybox/Panoramic" : "Universal Render Pipeline/Lit";
            var m = new Material(Shader.Find(sh)) { name = name };
            if (name == "LitNormal" || name == "LitWet" || name == "LitMetal") m.EnableKeyword("_NORMALMAP");
            if (name == "LitWet") m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (name == "LitMetal") m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (name == "LitEmissive") m.EnableKeyword("_EMISSION");
            if (name == "LitCutout") { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cull", 0); }
            if (name.Contains("Transparent") || name.Contains("Additive") || name.StartsWith("Particle")) MakeTransparent(m, name.Contains("Add"));
            return m;
        }

        public static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
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

        /// <summary>Material for a <see cref="Surface"/> (albedo / normal / wet-alpha or metal mask).</summary>
        public static Material For(Surface s, Color tint, Vector2? tiling = null)
        {
            Material m;
            bool low = Quality.Current == Gfx.Low;
            if (s.Mask != null && !low)
            {
                m = new Material(Template("LitMetal"));
                m.SetTexture(MetalMap, s.Mask);
                // Smoothness multiplier < 1 keeps GGX peaks from overflowing the R11G11B10 HDR target under close lights.
                m.SetFloat(Smooth, 0.82f);
            }
            else if (s.WetAlpha && !low)
            {
                m = new Material(Template("LitWet"));
                m.SetFloat(Smooth, s.Smooth);
            }
            else
            {
                m = new Material(Template(low ? "Lit" : "LitNormal"));
                m.SetFloat(Smooth, s.WetAlpha ? 0.3f : s.Smooth);
            }
            m.SetColor(BaseColor, tint);
            m.SetTexture(BaseMap, s.Albedo);
            if (!low && s.Normal != null)
            {
                m.SetTexture(Bump, s.Normal);
                m.SetFloat(BumpScale, s.Bump);
            }
            m.SetFloat(Metal, s.Mask != null ? 1 : s.Metal);
            if (tiling != null) m.SetTextureScale(BaseMap, tiling.Value);
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
            m.SetColor(Emission, LdrSafe(glow));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        const bool HdrEverywhere = true;

        /// <summary>
        /// Without HDR, emission above 1 clips to white and loses its hue (golden fixtures turned white).
        /// Scale the colour so its brightest channel is 1, keeping the hue.
        /// </summary>
        static Color LdrSafe(Color glow)
        {
            // Every tier renders HDR now; kept for a possible future LDR tier.
            if (HdrEverywhere) return glow;
            float m = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));
            return m > 1f ? new Color(glow.r / m, glow.g / m, glow.b / m, glow.a) : glow;
        }

        /// <summary>Lit with a separate emission map (e.g. building facades with lit windows).</summary>
        public static Material EmissiveMap(Surface s, Color tint, Texture emission, Color glow)
        {
            var m = new Material(Template("LitEmissive"));
            m.SetColor(BaseColor, tint);
            m.SetTexture(BaseMap, s.Albedo);
            m.SetTexture(EmissionMap, emission);
            m.SetColor(Emission, LdrSafe(glow));
            m.SetFloat(Smooth, 0.35f);
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

        public static Material Transparent(Color c, float smooth = 0.6f, Texture tex = null)
        {
            var m = new Material(Template("LitTransparent"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            if (tex != null) m.SetTexture(BaseMap, tex);
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

        public static Material Particle(Texture tex, bool additive)
        {
            var m = new Material(Template(additive ? "ParticleAdd" : "ParticleAlpha"));
            m.SetTexture(BaseMap, tex);
            m.SetColor(BaseColor, Color.white);
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

        public static void ClearCache()
        {
            foreach (var m in Shared.Values) if (m != null) Object.Destroy(m);
            Shared.Clear();
        }

        public static void SetEmission(Material m, Color c) => m.SetColor(Emission, c);
        public static void SetColor(Material m, Color c) => m.SetColor(BaseColor, c);
    }
}
