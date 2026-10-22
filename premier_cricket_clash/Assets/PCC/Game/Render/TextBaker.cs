using System.Collections.Generic;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Draws text into procedural textures (LED boards, banners, big screens,
    /// shirt names) by copying glyphs out of the dynamic font atlas.
    /// </summary>
    public static class TextBaker
    {
        static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        public static Font Font(string name = "Teko-Bold")
        {
            if (Fonts.TryGetValue(name, out var f)) return f;
            f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Fonts[name] = f;
            return f;
        }

        const int AtlasSize = 64;

        /// <summary>Readable copy of the font atlas (the atlas itself is GPU-only).</summary>
        static Color32[] Snapshot(Texture atlas, out int w, out int h)
        {
            w = atlas.width;
            h = atlas.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(atlas, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32();
            Object.Destroy(copy);
            return px;
        }

        public static float Measure(string text, float height, string font = "Teko-Bold")
        {
            var f = Font(font);
            f.RequestCharactersInTexture(text, AtlasSize);
            float scale = height / AtlasSize, w = 0;
            foreach (char ch in text)
            {
                if (f.GetCharacterInfo(ch, out var ci, AtlasSize)) w += ci.advance * scale;
            }
            return w;
        }

        /// <summary>
        /// Draws <paramref name="text"/> with its baseline at <paramref name="baseline"/> (pixels, y up),
        /// cap height about <paramref name="height"/> px. Anchor: 0 = left, 0.5 = centre, 1 = right.
        /// </summary>
        public static void Draw(Px px, string text, float x, float baseline, float height, Color color, float anchor = 0.5f, string font = "Teko-Bold", bool wrapX = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var f = Font(font);
            f.RequestCharactersInTexture(text, AtlasSize);
            var atlas = f.material != null ? f.material.mainTexture : null;
            if (atlas == null) return;
            var data = Snapshot(atlas, out int aw, out int ah);
            float scale = height / (AtlasSize * 0.72f);
            float total = 0;
            foreach (char ch in text)
            {
                if (f.GetCharacterInfo(ch, out var ci, AtlasSize)) total += ci.advance * scale;
            }
            float pen = x - total * anchor;
            foreach (char ch in text)
            {
                if (!f.GetCharacterInfo(ch, out var ci, AtlasSize)) continue;
                int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
                if (gw > 0 && gh > 0)
                {
                    int ow = Mathf.CeilToInt(gw * scale), oh = Mathf.CeilToInt(gh * scale);
                    for (int oy = 0; oy < oh; oy++)
                    for (int ox = 0; ox < ow; ox++)
                    {
                        float tx = (ox + 0.5f) / ow, ty = (oy + 0.5f) / oh;
                        var uv = ci.uvBottomLeft + (ci.uvBottomRight - ci.uvBottomLeft) * tx + (ci.uvTopLeft - ci.uvBottomLeft) * ty;
                        int sx = Mathf.Clamp((int)(uv.x * aw), 0, aw - 1), sy = Mathf.Clamp((int)(uv.y * ah), 0, ah - 1);
                        var s = data[sy * aw + sx];
                        float a = Mathf.Max(s.a, s.r) / 255f;
                        if (a <= 0.02f) continue;
                        int dx = Mathf.RoundToInt(pen + ci.minX * scale) + ox;
                        int dy = Mathf.RoundToInt(baseline + ci.minY * scale) + oy;
                        if (wrapX) dx = ((dx % px.W) + px.W) % px.W;
                        px.Blend(dx, dy, color, a);
                    }
                }
                pen += ci.advance * scale;
            }
        }
    }
}
