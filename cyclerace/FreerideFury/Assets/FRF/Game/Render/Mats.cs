using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Runtime materials. The base materials live in Resources/Materials (created
    /// by the editor setup so their URP shaders and keywords are included in builds);
    /// every colour / texture variant is a clone, cached by key.
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

        public static Material LitBase => Base("FRF_Lit", "Universal Render Pipeline/Lit");
        public static Material LitDoubleBase => Base("FRF_LitDouble", "Universal Render Pipeline/Lit");
        public static Material LitEmissiveBase => Base("FRF_LitEmissive", "Universal Render Pipeline/Lit");
        public static Material LitDetailBase => Base("FRF_LitDetail", "Universal Render Pipeline/Lit");
        public static Material LitCutoutBase => Base("FRF_LitCutout", "Universal Render Pipeline/Lit");
        public static Material UnlitBase => Base("FRF_Unlit", "Universal Render Pipeline/Unlit");
        public static Material UnlitTransparentBase => Base("FRF_UnlitTransparent", "Universal Render Pipeline/Unlit");
        public static Material AdditiveBase => Base("FRF_Additive", "Universal Render Pipeline/Unlit");
        public static Material ParticleAlphaBase => Base("FRF_ParticleAlpha", "Universal Render Pipeline/Particles/Unlit");
        public static Material ParticleAddBase => Base("FRF_ParticleAdd", "Universal Render Pipeline/Particles/Unlit");

        /// <summary>Opaque lit surface.</summary>
        public static Material Lit(Color c, float smooth = 0.25f, Texture tex = null, bool doubleSided = false, float metallic = 0, Color? emission = null)
        {
            string key = "lit:" + ColorUtility.ToHtmlStringRGBA(c) + smooth + "|" + TexId(tex) + doubleSided + metallic + (emission?.ToString() ?? "");
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
                if (tex != null) m.SetTexture("_EmissionMap", tex);
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Terrain: a baked colour map over the whole area plus a tiled grain detail map.</summary>
        public static Material Terrain(Texture colorMap, Texture detail, Vector2 detailTiling, float smooth = 0.08f)
        {
            var m = new Material(LitDetailBase);
            m.SetColor(BaseColor, Color.white);
            m.SetTexture(BaseMap, colorMap);
            m.mainTexture = colorMap;
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, 0);
            m.EnableKeyword("_DETAIL_MULX2");
            m.SetTexture("_DetailAlbedoMap", detail);
            m.SetTextureScale("_DetailAlbedoMap", detailTiling);
            m.SetFloat("_DetailAlbedoMapScale", 1);
            return m;
        }

        /// <summary>Lit decal with alpha clipping (number plates, leaves).</summary>
        public static Material LitCutout(Texture tex, bool doubleSided = false)
        {
            var m = new Material(LitCutoutBase);
            m.SetColor(BaseColor, Color.white);
            m.SetTexture(BaseMap, tex);
            m.mainTexture = tex;
            m.SetFloat(Smoothness, 0.2f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.4f);
            if (doubleSided) m.SetFloat("_Cull", 0);
            return m;
        }

        /// <summary>Unlit surface (banners, sky elements, glows).</summary>
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

        /// <summary>Particle material (alpha blended or additive), tinted by particle colours.</summary>
        public static Material Particle(Texture tex, bool additive)
        {
            string key = "particle:" + TexId(tex) + additive;
            if (Cache.TryGetValue(key, out var m)) return m;
            m = new Material(additive ? ParticleAddBase : ParticleAlphaBase);
            m.SetColor(BaseColor, Color.white);
            m.SetTexture(BaseMap, tex);
            m.mainTexture = tex;
            Cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            m.SetColor(BaseColor, c);
            m.color = c;
        }

        public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);

        public static Color Darken(this Color c, float t) => Color.Lerp(c, Color.black, t);
        public static Color Lighten(this Color c, float t) => Color.Lerp(c, Color.white, t);
        public static Color WithA(this Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static float Luma(this Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }

    /// <summary>
    /// One small texture of flat colour swatches. Low-poly props (trees, rocks, gates,
    /// spectators) pick their colours by UV, so a whole chunk of scenery is one mesh
    /// and one draw call.
    /// </summary>
    public static class Palette
    {
        const int Cells = 64, CellPx = 2, Size = Cells * CellPx;
        static readonly Dictionary<int, Vector2> Slots = new Dictionary<int, Vector2>();
        static Texture2D _tex;
        static Color32[] _px;
        static bool _dirty;

        public static Texture2D Texture
        {
            get
            {
                Ensure();
                if (_dirty) Apply();
                return _tex;
            }
        }

        static void Ensure()
        {
            if (_tex != null) return;
            _tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _px = new Color32[Size * Size];
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(255, 0, 255, 255);
        }

        /// <summary>UV of the swatch for <paramref name="c"/> (allocated on first use).</summary>
        public static Vector2 Uv(Color c)
        {
            Ensure();
            Color32 c32 = c;
            // 6 bits per channel: random tints on thousands of props share a few hundred swatches.
            c32.r = (byte)(c32.r & 0xFC);
            c32.g = (byte)(c32.g & 0xFC);
            c32.b = (byte)(c32.b & 0xFC);
            int key = (c32.r << 16) | (c32.g << 8) | c32.b;
            if (Slots.TryGetValue(key, out var uv)) return uv;
            int n = Slots.Count;
            if (n >= Cells * Cells) n = Cells * Cells - 1;
            int cx = n % Cells, cy = n / Cells;
            for (int y = 0; y < CellPx; y++)
            for (int x = 0; x < CellPx; x++)
                _px[(cy * CellPx + y) * Size + cx * CellPx + x] = new Color32(c32.r, c32.g, c32.b, 255);
            uv = new Vector2((cx * CellPx + CellPx * 0.5f) / Size, (cy * CellPx + CellPx * 0.5f) / Size);
            Slots[key] = uv;
            _dirty = true;
            return uv;
        }

        /// <summary>Uploads swatches added since the last upload (called every frame by GameApp).</summary>
        public static void Flush()
        {
            if (_dirty) Apply();
        }

        public static void Apply()
        {
            Ensure();
            _tex.SetPixels32(_px);
            _tex.Apply(false, false);
            _dirty = false;
        }

        static Material _lit, _glow, _litDouble;

        /// <summary>Opaque lit palette material (shadows on).</summary>
        public static Material Lit => _lit != null ? _lit : (_lit = Mats.Lit(Color.white, 0.12f, Texture));

        public static Material LitDouble => _litDouble != null ? _litDouble : (_litDouble = Mats.Lit(Color.white, 0.12f, Texture, true));

        /// <summary>Self-lit palette material (lava, lamps, LED strips).</summary>
        public static Material Glow => _glow != null ? _glow : (_glow = Mats.Lit(Color.white, 0.3f, Texture, false, 0, new Color(1.6f, 1.6f, 1.6f)));
    }
}
