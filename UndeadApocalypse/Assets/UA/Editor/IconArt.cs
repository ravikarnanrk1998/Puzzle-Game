using UA.Game;
using UnityEngine;

namespace UA.EditorTools
{
    /// <summary>Procedural launcher icon and Play feature graphic: a backlit zombie head with glowing eyes and claw slashes.</summary>
    public static class IconArt
    {
        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1) * Mathf.Min(rx, ry);
        }

        static float SMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1 - h);
        }

        /// <summary>Signed distance (in 0..1 units) to the zombie bust silhouette; negative inside.</summary>
        static float Bust(float x, float y)
        {
            float skull = Ellipse(x, y, 0.5f, 0.58f, 0.2f, 0.23f);
            float jaw = Ellipse(x, y, 0.5f, 0.42f, 0.14f, 0.12f);
            float cheekL = Ellipse(x, y, 0.4f, 0.5f, 0.07f, 0.09f);
            float cheekR = Ellipse(x, y, 0.6f, 0.5f, 0.07f, 0.09f);
            float neck = Ellipse(x, y, 0.5f, 0.27f, 0.1f, 0.12f);
            float shoulders = Ellipse(x, y, 0.5f, 0.02f, 0.46f, 0.2f);
            float d = SMin(skull, jaw, 0.04f);
            d = SMin(d, cheekL, 0.03f);
            d = SMin(d, cheekR, 0.03f);
            d = SMin(d, neck, 0.05f);
            d = SMin(d, shoulders, 0.08f);
            // Ragged edge.
            d += (Noise.Fbm(x, y, 16, 3, 3) - 0.5f) * 0.012f;
            return d;
        }

        public static Texture2D Render(int n)
        {
            var p = new Pix(n, n).Fill((u, v, x, y) => Shade(u, v, 1));
            return p.ToTexture("icon", false, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
        }

        static Color Shade(float u, float v, float aspect)
        {
            // Background: dark fog with a red glow from below and teal from the top left.
            float dist = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.45f) * (v - 0.45f));
            var bg = Color.Lerp(new Color(0.3f, 0.03f, 0.03f), new Color(0.02f, 0.025f, 0.035f), Mathf.Clamp01(dist * 1.6f));
            bg += new Color(0.05f, 0.18f, 0.22f) * Mathf.Clamp01(1 - Mathf.Sqrt((u - 0.15f) * (u - 0.15f) + (v - 0.9f) * (v - 0.9f)) * 1.8f) * 0.6f;
            float fog = Noise.Fbm(u, v, 3, 5, 11);
            bg *= 0.75f + fog * 0.5f;
            float d = Bust(u, v);
            var col = bg;
            if (d < 0.004f)
            {
                // Silhouette with rim light: teal left, orange right.
                float eps = 0.004f;
                float gx = Bust(u + eps, v) - Bust(u - eps, v), gy = Bust(u, v + eps) - Bust(u, v - eps);
                var gdir = new Vector2(gx, gy).normalized;
                float rim = Mathf.Clamp01(1 + d / 0.025f);
                var body = new Color(0.035f, 0.035f, 0.04f) * (0.8f + Noise.Fbm(u, v, 12, 3, 5) * 0.5f);
                var rimCol = Color.Lerp(new Color(0.15f, 0.65f, 0.75f), new Color(1f, 0.45f, 0.12f), Mathf.Clamp01(gdir.x * 0.5f + 0.5f));
                col = body + rimCol * rim * rim * (0.6f + Mathf.Abs(gdir.x) * 0.6f);
                col = Color.Lerp(bg, col, Mathf.Clamp01(-d / 0.004f + 0.5f));
            }
            // Eyes.
            foreach (var ex in new[] { 0.43f, 0.57f })
            {
                float e = Ellipse(u, v, ex, 0.565f, 0.035f, 0.018f);
                float glow = Mathf.Exp(-Mathf.Max(0, e) * 45);
                col += new Color(1f, 0.32f, 0.06f) * glow * 1.1f;
                if (e < 0) col = Color.Lerp(col, new Color(1f, 0.9f, 0.55f), Mathf.Clamp01(-e * 120));
            }
            // Mouth: jagged teeth glow.
            float mouth = Ellipse(u, v, 0.5f, 0.42f, 0.07f, 0.022f);
            if (mouth < 0)
            {
                float teeth = Mathf.Abs(Mathf.Sin(u * 160)) > 0.4f && Mathf.Abs(v - 0.42f) > 0.006f ? 1 : 0;
                col = Color.Lerp(new Color(0.08f, 0.0f, 0.0f), new Color(0.55f, 0.45f, 0.35f), teeth * 0.6f);
            }
            // Claw slashes across.
            for (int i = 0; i < 3; i++)
            {
                float off = (i - 1) * 0.085f;
                float line = Mathf.Abs((u - 0.5f) * 0.8f + (v - 0.5f) - off * 1.3f) - 0.008f * (1 - Mathf.Abs(u - 0.5f) * 1.6f);
                float along = Mathf.Clamp01(1 - Mathf.Abs(u - 0.5f - off * 0.3f) * 2.2f);
                if (along > 0)
                {
                    float s = Mathf.Clamp01(1 - line / 0.006f) * along;
                    col = Color.Lerp(col, new Color(0.75f, 0.04f, 0.03f), s);
                    col += new Color(0.5f, 0.02f, 0.01f) * Mathf.Exp(-Mathf.Max(0, line) * 120) * along * 0.6f;
                }
            }
            // Vignette.
            col *= Mathf.Lerp(1.1f, 0.45f, Mathf.Clamp01(dist * 1.5f - 0.2f));
            col.a = 1;
            return col;
        }

        public static Texture2D Feature(int w, int h)
        {
            var p = new Pix(w, h).Fill((u, v, x, y) =>
            {
                // Bust on the right third, fog and title band on the left.
                float uu = (u - 0.62f) * (w / (float)h) * 0.5f + 0.5f;
                var c = Shade(Mathf.Clamp01(uu), v, w / (float)h);
                if (uu < 0 || uu > 1) c = Color.Lerp(new Color(0.02f, 0.025f, 0.035f), new Color(0.25f, 0.03f, 0.03f), Mathf.Clamp01(1 - v * 1.4f)) * (0.8f + Noise.Fbm(u, v, 4, 4, 2) * 0.4f);
                return c;
            });
            PixelFont.Draw(p, "UNDEAD", 50, 300, 14, new Color(0.92f, 0.9f, 0.86f), 0.12f, 1);
            PixelFont.Draw(p, "APOCALYPSE", 54, 210, 7, new Color(0.85f, 0.12f, 0.08f), 0.1f, 2);
            PixelFont.Draw(p, "INFINITE WAR", 56, 150, 5, new Color(1f, 0.55f, 0.2f), 0.05f, 3);
            return p.ToTexture("feature", false, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
        }
    }
}
