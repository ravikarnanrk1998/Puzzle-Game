using System.Collections.Generic;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Runtime materials. The base materials live in Resources/Materials (created
    /// by the editor setup so their URP shaders are included in builds); every
    /// colour / texture variant is a clone, cached by key.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        static readonly Dictionary<Texture, int> TexIds = new Dictionary<Texture, int>();

        /// <summary>Stable small id per texture for cache keys (GetInstanceID is obsolete in Unity 6.6+).</summary>
        static int TexId(Texture tex)
        {
            if (tex == null) return 0;
            if (!TexIds.TryGetValue(tex, out int id))
            {
                id = TexIds.Count + 1;
                TexIds[tex] = id;
            }
            return id;
        }

        static Material Base(string name, string shaderFallback)
        {
            string key = "base:" + name;
            if (Cache.TryGetValue(key, out var m)) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                // Editor play mode before the setup step ran: build from the shader directly.
                var sh = Shader.Find(shaderFallback) ?? Shader.Find("Standard");
                m = new Material(sh) { name = name };
            }
            Cache[key] = m;
            return m;
        }

        public static Material LitBase => Base("PCC_Lit", "Universal Render Pipeline/Lit");
        public static Material LitDoubleBase => Base("PCC_LitDouble", "Universal Render Pipeline/Lit");

        /// <summary>Lit with the _EMISSION keyword, so builds keep the emissive shader variant.</summary>
        public static Material LitEmissiveBase => Base("PCC_LitEmissive", "Universal Render Pipeline/Lit");
        public static Material UnlitBase => Base("PCC_Unlit", "Universal Render Pipeline/Unlit");
        public static Material UnlitTransparentBase => Base("PCC_UnlitTransparent", "Universal Render Pipeline/Unlit");
        public static Material AdditiveBase => Base("PCC_Additive", "Universal Render Pipeline/Unlit");

        /// <summary>Opaque lit surface (players, stands, buildings).</summary>
        public static Material Lit(Color c, float smooth = 0.25f, Texture tex = null, bool doubleSided = false, float metallic = 0, Color? emission = null)
        {
            string key = "lit:" + ColorUtility.ToHtmlStringRGBA(c) + smooth + TexId(tex) + doubleSided + metallic + (emission?.ToString() ?? "");
            if (Cache.TryGetValue(key, out var m)) return m;
            m = new Material(emission != null ? LitEmissiveBase : (doubleSided ? LitDoubleBase : LitBase));
            m.SetColor(BaseColor, c);
            m.color = c;
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, metallic);
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.mainTexture = tex;
            }
            if (emission != null)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor(EmissionColor, emission.Value);
                // Textured emissive surfaces (lit windows) glow through their texture.
                if (tex != null) m.SetTexture("_EmissionMap", tex);
            }
            Cache[key] = m;
            return m;
        }

        public static Material LitCutoutBase => Base("PCC_LitCutout", "Universal Render Pipeline/Lit");

        /// <summary>Lit decal with alpha clipping (shirt names and numbers).</summary>
        public static Material LitCutout(Texture tex)
        {
            var m = new Material(LitCutoutBase);
            m.SetColor(BaseColor, Color.white);
            m.SetTexture(BaseMap, tex);
            m.mainTexture = tex;
            m.SetFloat(Smoothness, 0.2f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.4f);
            return m;
        }

        /// <summary>Unlit surface (screens, LED boards, sky elements).</summary>
        public static Material Unlit(Color c, Texture tex = null, bool transparent = false, bool additive = false)
        {
            string key = "unlit:" + ColorUtility.ToHtmlStringRGBA(c) + TexId(tex) + transparent + additive;
            if (Cache.TryGetValue(key, out var m)) return m;
            m = new Material(additive ? AdditiveBase : (transparent ? UnlitTransparentBase : UnlitBase));
            m.SetColor(BaseColor, c);
            m.color = c;
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.mainTexture = tex;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>A material that will be animated (not shared through the cache).</summary>
        public static Material UnlitInstance(Color c, Texture tex = null, bool transparent = false)
        {
            var m = new Material(transparent ? UnlitTransparentBase : UnlitBase);
            m.SetColor(BaseColor, c);
            m.color = c;
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.mainTexture = tex;
            }
            return m;
        }

        public static void SetOffset(Material m, Vector2 offset)
        {
            m.SetTextureOffset(BaseMap, offset);
            m.mainTextureOffset = offset;
        }

        public static void SetColor(Material m, Color c)
        {
            m.SetColor(BaseColor, c);
            m.color = c;
        }

        public static void SetTexture(Material m, Texture t)
        {
            m.SetTexture(BaseMap, t);
            m.mainTexture = t;
        }

        public static Color Hex(uint argb) => new Color(((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, ((argb >> 24) & 0xFF) / 255f);

        public static Color Darken(this Color c, float t) => Color.Lerp(c, Color.black, t);
        public static Color Lighten(this Color c, float t) => Color.Lerp(c, Color.white, t);
        public static Color WithA(this Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static float Luma(this Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
