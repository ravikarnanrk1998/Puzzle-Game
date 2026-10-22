using UnityEngine;

namespace FRF
{
    /// <summary>Pixel canvas for drawing procedural textures.</summary>
    public sealed class Px
    {
        public readonly int W, H;
        public readonly Color32[] P;

        public Px(int w, int h, Color32 fill)
        {
            W = w;
            H = h;
            P = new Color32[w * h];
            for (int i = 0; i < P.Length; i++) P[i] = fill;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            P[y * W + x] = c;
        }

        public Color Get(int x, int y) => P[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];

        public void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            Color o = P[i];
            var n = Color.Lerp(o, c, a * c.a);
            n.a = Mathf.Max(o.a, a * c.a);
            P[i] = n;
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(H, (int)Mathf.Ceil(y1)); y++)
            for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(W, (int)Mathf.Ceil(x1)); x++)
                Blend(x, y, c, 1);
        }

        public void Circle(float cx, float cy, float r, Color c, bool wrap = false)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0) continue;
                int xx = wrap ? ((x % W) + W) % W : x;
                int yy = wrap ? ((y % H) + H) % H : y;
                Blend(xx, yy, c, a);
            }
        }

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color top, Color bottom)
        {
            for (int y = (int)y0; y <= (int)y1; y++)
            {
                float t = Mathf.InverseLerp(y1, y0, y);
                var c = Color.Lerp(bottom, top, t);
                for (int x = (int)x0; x <= (int)x1; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(x0 + r - x, x - (x1 - r)));
                    float dy = Mathf.Max(0, Mathf.Max(y0 + r - y, y - (y1 - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (a <= 0) continue;
                    Blend(x, y, c, a);
                }
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float w, Color c, bool wrap = false)
        {
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, (int)(len * 2));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Circle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), w * 0.5f, c, wrap);
            }
        }

        /// <summary>
        /// Anti-aliased fill of any shape given as an inside test on normalised coordinates
        /// (0..1, y up), with 4x4 supersampling. Used for icons.
        /// </summary>
        public void Fill(System.Func<float, float, bool> inside, Color c)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (inside((x + (sx + 0.5f) / 4) / W, (y + (sy + 0.5f) / 4) / H)) hits++;
                if (hits > 0) Blend(x, y, c, hits / 16f);
            }
        }

        /// <summary>Even-odd point-in-polygon test (normalised coordinates).</summary>
        public static bool InPoly(Vector2[] p, float x, float y)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) c = !c;
            }
            return c;
        }

        public Texture2D ToTexture(string name, bool mip = true, TextureWrapMode wrap = TextureWrapMode.Repeat, FilterMode filter = FilterMode.Trilinear, int aniso = 4)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, mip) { name = name, wrapMode = wrap, filterMode = filter, anisoLevel = aniso };
            t.SetPixels32(P);
            t.Apply(mip, true);
            return t;
        }
    }

    /// <summary>Noise and procedural textures: trail dirt, ground grain, planks, banners, particles.</summary>
    public static class Tex
    {
        /// <summary>GLSL-style smoothstep: 0 below <paramref name="e0"/>, 1 above <paramref name="e1"/>.</summary>
        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        public static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>Tileable value noise with integer period (px, py).</summary>
        public static float NoiseTiled(float x, float y, int px, int py)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            int x0 = ((ix % px) + px) % px, x1 = (x0 + 1) % px, y0 = ((iy % py) + py) % py, y1 = (y0 + 1) % py;
            float a = Hash(x0, y0), b = Hash(x1, y0), c = Hash(x0, y1), d = Hash(x1, y1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        public static float Fbm(float x, float y, int oct = 4)
        {
            float v = 0, amp = 0.5f;
            for (int i = 0; i < oct; i++)
            {
                v += amp * Noise(x, y);
                x = x * 2.07f + 17.3f;
                y = y * 2.07f + 9.1f;
                amp *= 0.5f;
            }
            return v;
        }

        static float FbmTiled(float x, float y, int px, int py, int oct)
        {
            float v = 0, amp = 0.5f;
            for (int i = 0; i < oct; i++)
            {
                v += amp * NoiseTiled(x, y, px, py);
                x *= 2;
                y *= 2;
                px *= 2;
                py *= 2;
                amp *= 0.5f;
            }
            return v;
        }

        /// <summary>
        /// Trail surface, tiling along its length: u across the track (0..1 = edge to edge),
        /// v along it. Packed centre line, tyre ruts, loose stones and darker edges.
        /// </summary>
        public static Texture2D Trail(Theme th)
        {
            const int w = 256, h = 512;
            var px = new Px(w, h, Color.black);
            var rnd = new System.Random(th.Biome.GetHashCode() + 7);
            bool snow = th.Biome == Biome.Glacier;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float u = (i + 0.5f) / w, v = (j + 0.5f) / h;
                float n = FbmTiled(u * 8, v * 16, 8, 16, 4);
                float fine = NoiseTiled(u * 64, v * 128, 64, 128);
                var c = Color.Lerp(th.DirtDark, th.Dirt, 0.35f + n * 0.8f);
                c *= 0.9f + fine * 0.2f;
                // Two worn racing ruts.
                float rut = 0;
                foreach (float rc in new[] { 0.36f, 0.64f })
                {
                    float wob = (NoiseTiled(v * 6, rc * 10, 6, 10) - 0.5f) * 0.05f;
                    rut += Mathf.Exp(-Mathf.Pow((u - rc - wob) / 0.035f, 2));
                }
                c = Color.Lerp(c, snow ? th.DirtDark.Lighten(0.2f) : th.DirtDark.Darken(0.1f), Mathf.Clamp01(rut) * 0.45f);
                // Edges blend towards the verge colour.
                float edge = SmoothStep(0.36f, 0.5f, Mathf.Abs(u - 0.5f));
                var verge = Color.Lerp(th.GrassDark, th.Grass, n);
                c = Color.Lerp(c, verge, edge * (0.55f + 0.45f * NoiseTiled(u * 20, v * 40, 20, 40)));
                c.a = 1;
                px.P[j * w + i] = c;
            }
            // Pebbles.
            for (int k = 0; k < 520; k++)
            {
                float x = (float)rnd.NextDouble() * w, y = (float)rnd.NextDouble() * h;
                float r = 0.8f + (float)rnd.NextDouble() * (snow ? 1.2f : 2.2f);
                var c = Color.Lerp(th.Rock, th.RockDark, (float)rnd.NextDouble());
                if (snow) c = Color.Lerp(th.Dirt.Lighten(0.4f), th.DirtDark, (float)rnd.NextDouble() * 0.4f);
                px.Circle(x, y, r + 0.6f, th.DirtDark.Darken(0.35f).WithA(0.5f), true);
                px.Circle(x, y, r, c, true);
            }
            return px.ToTexture("Trail", true, TextureWrapMode.Repeat, FilterMode.Trilinear, 8);
        }

        /// <summary>Neutral grain for detail maps (0.5 grey = no change).</summary>
        public static Texture2D Grain(Biome biome)
        {
            const int s = 256;
            var px = new Px(s, s, new Color32(128, 128, 128, 255));
            bool blades = biome == Biome.Forest || biome == Biome.Autumn;
            for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float n = blades
                    ? NoiseTiled(i * 0.3f, j * 0.08f, (int)(s * 0.3f), (int)(s * 0.08f)) * 0.55f + NoiseTiled(i * 0.9f, j * 0.9f, (int)(s * 0.9f), (int)(s * 0.9f)) * 0.45f
                    : FbmTiled(i / 16f, j / 16f, s / 16, s / 16, 4);
                byte v = (byte)Mathf.Clamp(100 + n * 60, 0, 255);
                px.P[j * s + i] = new Color32(v, v, v, 255);
            }
            return px.ToTexture("Grain", true, TextureWrapMode.Repeat, FilterMode.Trilinear, 4);
        }

        /// <summary>Weathered planks for kickers and bridges (u across, v along the boards).</summary>
        public static Texture2D Planks()
        {
            const int w = 128, h = 256, boards = 6;
            var px = new Px(w, h, Color.black);
            var a = new Color(0.62f, 0.45f, 0.28f);
            var b = new Color(0.45f, 0.31f, 0.19f);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int board = i * boards / w;
                float grain = NoiseTiled(i * 0.5f + board * 13, j * 0.04f, 64, 10);
                var c = Color.Lerp(b, a, 0.4f + grain * 0.6f + (board % 2) * 0.08f);
                float gap = (i * boards % w) / (float)w * boards;
                if (gap < 0.06f) c = c.Darken(0.6f);
                c.a = 1;
                px.P[j * w + i] = c;
            }
            return px.ToTexture("Planks", true, TextureWrapMode.Repeat, FilterMode.Trilinear, 4);
        }

        /// <summary>Banner with centred text, colour band and border.</summary>
        public static Texture2D Banner(string text, Color bg, Color fg, Color band, int w = 512, int h = 128)
        {
            var px = new Px(w, h, bg);
            px.Rect(0, 0, w, h * 0.12f, band);
            px.Rect(0, h * 0.88f, w, h, band);
            TextBaker.Draw(px, text, w * 0.5f, h * 0.24f, h * 0.56f, fg);
            return px.ToTexture("Banner " + text, true, TextureWrapMode.Clamp, FilterMode.Trilinear, 4);
        }

        public static Texture2D Checker(int cells = 8, int px0 = 16)
        {
            int w = cells * px0, h = 2 * px0;
            var px = new Px(w, h, Color.white);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                if (((i / px0) + (j / px0)) % 2 == 0) px.P[j * w + i] = new Color32(20, 20, 24, 255);
            return px.ToTexture("Checker", true, TextureWrapMode.Repeat, FilterMode.Bilinear, 2);
        }

        /// <summary>Soft radial dot (glows, dust puffs, blob shadows).</summary>
        public static Texture2D Glow(int size = 64)
        {
            var px = new Px(size, size, new Color32(255, 255, 255, 0));
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float dx = (i + 0.5f) / size * 2 - 1, dy = (j + 0.5f) / size * 2 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1 - d);
                px.P[j * size + i] = new Color(1, 1, 1, a * a);
            }
            return px.ToTexture("Glow", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
        }

        static Texture2D _glow, _puff, _leaf, _flake, _streak;

        public static Texture2D GlowTex => _glow != null ? _glow : (_glow = Glow(64));

        /// <summary>Lumpy dust puff.</summary>
        public static Texture2D Puff
        {
            get
            {
                if (_puff != null) return _puff;
                const int s = 64;
                var px = new Px(s, s, new Color32(255, 255, 255, 0));
                for (int j = 0; j < s; j++)
                for (int i = 0; i < s; i++)
                {
                    float dx = (i + 0.5f) / s * 2 - 1, dy = (j + 0.5f) / s * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + (Fbm(i * 0.12f, j * 0.12f, 3) - 0.5f) * 0.5f;
                    float a = Mathf.Clamp01(1 - d);
                    px.P[j * s + i] = new Color(1, 1, 1, a * a * 0.9f);
                }
                return _puff = px.ToTexture("Puff", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            }
        }

        public static Texture2D Leaf
        {
            get
            {
                if (_leaf != null) return _leaf;
                const int s = 32;
                var px = new Px(s, s, new Color32(255, 255, 255, 0));
                for (int j = 0; j < s; j++)
                for (int i = 0; i < s; i++)
                {
                    float x = (i + 0.5f) / s * 2 - 1, y = (j + 0.5f) / s * 2 - 1;
                    float r = Mathf.Abs(x) / (0.6f * Mathf.Sqrt(Mathf.Max(0, 1 - y * y)) + 1e-3f);
                    if (r < 1) px.P[j * s + i] = Color.white;
                }
                return _leaf = px.ToTexture("Leaf", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            }
        }

        public static Texture2D Flake => _flake != null ? _flake : (_flake = Glow(16));

        /// <summary>Horizontal soft streak for boost trails and speed lines.</summary>
        public static Texture2D Streak
        {
            get
            {
                if (_streak != null) return _streak;
                const int w = 64, h = 16;
                var px = new Px(w, h, new Color32(255, 255, 255, 0));
                for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float x = (i + 0.5f) / w, y = (j + 0.5f) / h * 2 - 1;
                    float a = Mathf.Sin(x * Mathf.PI) * Mathf.Clamp01(1 - y * y);
                    px.P[j * w + i] = new Color(1, 1, 1, a);
                }
                return _streak = px.ToTexture("Streak", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            }
        }
    }
}
