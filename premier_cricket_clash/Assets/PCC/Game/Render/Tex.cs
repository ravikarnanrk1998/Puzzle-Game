using System;
using UnityEngine;

namespace PCC.Game
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

        public void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            Color o = P[i];
            P[i] = Color.Lerp(o, c, a * c.a);
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(H, (int)Mathf.Ceil(y1)); y++)
            for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(W, (int)Mathf.Ceil(x1)); x++)
                Blend(x, y, c, 1);
        }

        /// <summary>Wraps horizontally so tiled textures stay seamless.</summary>
        public void Circle(float cx, float cy, float r, Color c, bool wrapX = false)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0) continue;
                int xx = wrapX ? ((x % W) + W) % W : x;
                Blend(xx, y, c, a);
            }
        }

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color top, Color bottom, bool wrapX = false)
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
                    int xx = wrapX ? ((x % W) + W) % W : x;
                    Blend(xx, y, c, a);
                }
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float w, Color c, bool wrapX = false)
        {
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, (int)(len * 2));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Circle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), w * 0.5f, c, wrapX);
            }
        }

        public Texture2D ToTexture(string name, bool mip = true, TextureWrapMode wrap = TextureWrapMode.Repeat, FilterMode filter = FilterMode.Trilinear, int aniso = 4)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, mip) { name = name, wrapMode = wrap, filterMode = filter, anisoLevel = aniso };
            t.SetPixels32(P);
            t.Apply(mip, true);
            return t;
        }
    }

    /// <summary>Procedural textures: grass, pitch, crowds, buildings, clouds, ball.</summary>
    public static class Tex
    {
        /// <summary>
        /// GLSL-style smoothstep: 0 below <paramref name="e0"/>, 1 above <paramref name="e1"/>.
        /// (Unity's Mathf.SmoothStep is an interpolation between two values, not this.)
        /// </summary>
        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        // --------------------------------------------------------- noise

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

        static float Band(float d, float hw, float aa) => 1 - SmoothStep(hw, hw + aa, Mathf.Abs(d));

        // --------------------------------------------------------- field

        /// <summary>
        /// Outfield texture covering a square of side <paramref name="world"/> metres
        /// centred on the pitch. Includes mowing stripes, the 30-yard circle and the apron.
        /// </summary>
        public static Texture2D Grass(int size, float world, float rx, float ry, bool day)
        {
            var px = new Px(size, size, new Color32(0, 0, 0, 255));
            float mpp = world / size;
            var gA = day ? new Color(0.16f, 0.45f, 0.12f) : new Color(0.09f, 0.33f, 0.13f);
            var gB = day ? new Color(0.30f, 0.62f, 0.19f) : new Color(0.2f, 0.52f, 0.22f);
            var apron = day ? new Color(0.48f, 0.47f, 0.44f) : new Color(0.16f, 0.17f, 0.2f);
            float aa = mpp * 1.2f;
            for (int j = 0; j < size; j++)
            {
                float gy = (float)Pitch.Mid - world / 2 + (j + 0.5f) * mpp;
                for (int i = 0; i < size; i++)
                {
                    float gx = -world / 2 + (i + 0.5f) * mpp;
                    float qx = gx / rx, qy = (gy - (float)Pitch.Mid) / ry;
                    float r = Mathf.Sqrt(qx * qx + qy * qy);
                    float ropeD = (r - 1) * rx;

                    float n1 = Fbm(gx * 0.22f, gy * 0.22f);
                    float n2 = Noise(gx * 2.3f, gy * 2.3f);
                    var c = Color.Lerp(gA, gB, n1);
                    c *= 1 + (n2 - 0.5f) * 0.14f;
                    float stripe = Mathf.Repeat(Mathf.Floor((gx + 2.5f) / 5f), 2);
                    float chk = Mathf.Repeat(Mathf.Floor((gy + 3f) / 6f), 2);
                    c *= (day ? Mathf.Lerp(0.82f, 1.12f, stripe) : Mathf.Lerp(0.88f, 1.08f, stripe)) * Mathf.Lerp(0.97f, 1.03f, chk);
                    float outer = SmoothStep(0, aa * 2, ropeD);
                    c = Color.Lerp(c, c * new Color(0.72f, 0.76f, 0.7f), outer);

                    // 30-yard circle (dashed).
                    float ic, s;
                    if (gy > (float)Pitch.Length)
                    {
                        float vx = gx, vy = gy - (float)Pitch.Length;
                        ic = Mathf.Sqrt(vx * vx + vy * vy) - 27.43f;
                        s = Mathf.Atan2(vy, vx) * 27.43f;
                    }
                    else if (gy < 0)
                    {
                        ic = Mathf.Sqrt(gx * gx + gy * gy) - 27.43f;
                        s = Mathf.Atan2(gy, gx) * 27.43f;
                    }
                    else
                    {
                        ic = Mathf.Abs(gx) - 27.43f;
                        s = gy;
                    }
                    float ph = Mathf.Repeat(s / 2.2f, 1);
                    float dash = SmoothStep(0.3f, 0.38f, ph) * (1 - SmoothStep(0.86f, 0.94f, ph));
                    c = Color.Lerp(c, new Color(0.93f, 0.93f, 0.93f), Band(ic, 0.08f, aa) * dash * 0.85f);

                    // Apron beyond the boards.
                    float ap = SmoothStep(3.0f, 3.6f, ropeD);
                    var concrete = apron * (0.92f + 0.16f * Noise(gx * 1.7f, gy * 1.7f));
                    c = Color.Lerp(c, concrete, ap);
                    c.a = 1;
                    px.P[j * size + i] = c;
                }
            }
            return px.ToTexture("Grass", true, TextureWrapMode.Clamp, FilterMode.Trilinear, 8);
        }

        /// <summary>Fine grass blades for the detail map (0.5 grey = no change).</summary>
        public static Texture2D GrassDetail(int size = 256)
        {
            var px = new Px(size, size, new Color32(128, 128, 128, 255));
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float n = Noise(i * 0.35f, j * 0.08f) * 0.6f + Noise(i * 1.3f, j * 1.3f) * 0.4f;
                byte v = (byte)(108 + n * 44);
                px.P[j * size + i] = new Color32(v, v, v, 255);
            }
            return px.ToTexture("GrassDetail");
        }

        /// <summary>Pitch strip: 3.04 m x 24 m (x -1.52..1.52, y -1.94..22.06).</summary>
        public static Texture2D PitchStrip(bool day)
        {
            int w = 192, h = 1536;
            var px = new Px(w, h, new Color32(0, 0, 0, 255));
            // Rich brown (not near-white) so the white ball reads clearly against the strip.
            var dA = day ? new Color(0.55f, 0.45f, 0.3f) : new Color(0.49f, 0.4f, 0.27f);
            var dB = day ? new Color(0.68f, 0.57f, 0.39f) : new Color(0.61f, 0.51f, 0.35f);
            for (int j = 0; j < h; j++)
            {
                float gy = -1.94f + (j + 0.5f) / h * 24f;
                for (int i = 0; i < w; i++)
                {
                    float gx = -1.52f + (i + 0.5f) / w * 3.04f;
                    float pn = Fbm(gx * 1.8f + 3, gy * 0.6f);
                    var c = Color.Lerp(dA, dB, pn);
                    c = Color.Lerp(c, day ? new Color(0.74f, 0.64f, 0.46f) : new Color(0.66f, 0.57f, 0.41f), (1 - SmoothStep(0.15f, 0.7f, Mathf.Abs(gx))) * 0.3f);
                    float near1 = 1 - SmoothStep(0.4f, 2.4f, Mathf.Abs(gy - (float)Pitch.BowlerCrease));
                    float near2 = 1 - SmoothStep(0.4f, 2.4f, Mathf.Abs(gy - (float)Pitch.StrikerCrease));
                    float rough = SmoothStep(0.45f, 0.8f, Fbm(gx * 3.3f + 7, gy * 3.3f)) * (near1 + near2) * SmoothStep(0.2f, 0.8f, Mathf.Abs(gx));
                    c *= 1 - rough * 0.3f;
                    c *= 1 + (Noise(gx * 18, gy * 18) - 0.5f) * 0.16f;
                    float edge = SmoothStep(1.15f, 1.52f, Mathf.Abs(gx));
                    c = Color.Lerp(c, c * new Color(0.78f, 0.86f, 0.66f), edge * 0.7f);
                    c.a = 1;
                    px.P[j * w + i] = c;
                }
            }
            // Creases.
            var white = new Color(0.98f, 0.98f, 0.98f);
            float ppm = w / 3.04f, ppmY = h / 24f;
            int X(float gx) => Mathf.RoundToInt((gx + 1.52f) * ppm);
            int Y(float gy) => Mathf.RoundToInt((gy + 1.94f) * ppmY);
            void HLine(float gy, float x0, float x1) => px.Rect(X(x0), Y(gy) - 2, X(x1), Y(gy) + 2, white);
            void VLine(float gx, float y0, float y1) => px.Rect(X(gx) - 1.5f, Y(y0), X(gx) + 1.5f, Y(y1), white);
            HLine((float)Pitch.BowlerCrease, -1.52f, 1.52f);
            HLine((float)Pitch.StrikerCrease, -1.52f, 1.52f);
            HLine(0, -1.32f, 1.32f);
            HLine((float)Pitch.Length, -1.32f, 1.32f);
            VLine(1.32f, -1.22f, 1.22f);
            VLine(-1.32f, -1.22f, 1.22f);
            VLine(1.32f, 18.9f, 21.34f);
            VLine(-1.32f, 18.9f, 21.34f);
            VLine(0.89f, 18.62f, 18.9f);
            VLine(-0.89f, 18.62f, 18.9f);
            VLine(0.89f, 1.22f, 1.5f);
            VLine(-0.89f, 1.22f, 1.5f);
            return px.ToTexture("Pitch", true, TextureWrapMode.Clamp, FilterMode.Trilinear, 8);
        }

        // --------------------------------------------------------- crowd

        static readonly Color[] Skins =
        {
            new Color32(0x8A, 0x5A, 0x3B, 255), new Color32(0x6E, 0x46, 0x30, 255), new Color32(0xA0, 0x70, 0x4C, 255),
            new Color32(0x5A, 0x3A, 0x26, 255), new Color32(0xB9, 0x85, 0x63, 255),
        };

        /// <summary>Rows of spectators in team colours; <paramref name="cheer"/> raises arms and flags.</summary>
        public static Texture2D Crowd(Color home, Color trim, Color away, bool day, bool cheer, int seed)
        {
            const int w = 1024, h = 448, rows = 14;
            const float rh = h / (float)rows;
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var seatTop = day ? home.Lighten(0.45f).Darken(0.1f) : new Color(0.1f, 0.12f, 0.27f);
            var seatBot = day ? home.Darken(0.15f) : new Color(0.04f, 0.05f, 0.14f);
            var px = new Px(w, h, new Color32(0, 0, 0, 255));
            var palette = new[]
            {
                home, home, home, home.Lighten(0.25f), trim, trim, Color.white, away.Lighten(0.1f), away,
                new Color(1f, 0.37f, 0.43f), new Color(0.49f, 0.96f, 1f), new Color(1f, 0.82f, 0.4f),
            };
            for (int r = 0; r < rows; r++)
            {
                // Texture rows go bottom (front of the stand) to top (back).
                float y0 = r * rh;
                for (int y = (int)y0; y < (int)(y0 + rh); y++)
                {
                    var c = Color.Lerp(seatBot, seatTop, (y - y0) / rh);
                    for (int x = 0; x < w; x++) px.P[y * w + x] = c;
                }
                px.Rect(0, y0, w, y0 + 2, day ? new Color(0.72f, 0.75f, 0.8f) : new Color(0.17f, 0.2f, 0.41f));
                float x0 = R() * 8;
                while (x0 < w)
                {
                    if (x0 % 256 < 16)
                    {
                        x0 += 16;
                        continue;
                    }
                    var col = palette[rnd.Next(palette.Length)];
                    var skin = Skins[rnd.Next(Skins.Length)];
                    float jit = (R() - 0.5f) * 3;
                    bool empty = R() < (day ? 0.05f : 0.07f);
                    bool armsUp = R() < 0.55f;
                    bool flag = R() < 0.05f;
                    bool cap = day && R() < 0.25f;
                    var flagCol = R() < 0.5f ? home : trim;
                    if (empty)
                    {
                        px.RoundRect(x0 - 5.5f, y0 + rh * 0.3f, x0 + 5.5f, y0 + rh * 0.3f + 11, 3, day ? home.Lighten(0.2f) : new Color(0.16f, 0.19f, 0.4f), day ? home.Lighten(0.1f) : new Color(0.12f, 0.15f, 0.33f), true);
                    }
                    else
                    {
                        float hy = y0 + rh * 0.68f + jit;
                        px.RoundRect(x0 - 6, y0 + rh * 0.12f + jit, x0 + 6, y0 + rh * 0.12f + jit + 16, 4.5f, col.Lighten(day ? 0.3f : 0.22f), col.Darken(day ? 0.25f : 0.4f), true);
                        if (cheer && armsUp)
                        {
                            px.Line(x0 - 4.5f, y0 + rh * 0.44f + jit, x0 - 7.5f, hy + 9, 2.6f, skin, true);
                            px.Line(x0 + 4.5f, y0 + rh * 0.44f + jit, x0 + 7.5f, hy + 9, 2.6f, skin, true);
                        }
                        px.Circle(x0, hy, 4.4f, skin, true);
                        var hair = cap ? col.Darken(0.1f) : new Color(0.09f, 0.06f, 0.05f);
                        for (int k = 0; k < 4; k++) px.Circle(x0 + (k - 1.5f) * 1.6f, hy + 2.6f, 2.4f, hair, true);
                        if (flag)
                        {
                            float top = hy + (cheer ? 18 : 12);
                            px.Line(x0 + 5, hy - 4, x0 + 5, top, 1.2f, new Color(0.87f, 0.87f, 0.87f), true);
                            px.Rect(x0 + 5, top - 7, x0 + 16, top, flagCol);
                        }
                    }
                    x0 += 13 + R() * 5;
                }
            }
            // Stair aisles.
            for (int ax = 0; ax < w; ax += 256)
            {
                px.Rect(ax, 0, ax + 16, h, day ? new Color(0.78f, 0.8f, 0.84f) : new Color(0.12f, 0.14f, 0.28f));
                for (float y = 0; y < h; y += rh / 2) px.Rect(ax, y, ax + 16, y + 1.2f, day ? new Color(0.6f, 0.63f, 0.69f) : new Color(0.18f, 0.21f, 0.39f));
            }
            return px.ToTexture(cheer ? "CrowdCheer" : "Crowd", true, TextureWrapMode.Repeat, FilterMode.Trilinear, 4);
        }

        // --------------------------------------------------------- city / sky

        /// <summary>Building facade: glass, sandstone or white tower; windows lit at night.</summary>
        public static Texture2D Facade(int kind, bool day)
        {
            const int s = 256, floors = 11, bays = 8;
            var rnd = new System.Random(kind * 97 + 5);
            Color baseC = day
                ? (kind == 0 ? new Color(0.44f, 0.56f, 0.69f) : kind == 1 ? new Color(0.85f, 0.8f, 0.71f) : new Color(0.9f, 0.91f, 0.93f))
                : new Color(0.08f, 0.09f, 0.15f);
            var px = new Px(s, s, baseC);
            const float fh = s / (float)floors, bw = s / (float)bays;
            for (int f = 0; f < floors; f++)
            for (int b = 0; b < bays; b++)
            {
                Color win;
                float r = (float)rnd.NextDouble();
                if (day)
                    win = kind == 0 ? Color.Lerp(new Color(0.18f, 0.3f, 0.44f), new Color(0.66f, 0.78f, 0.9f), r * 0.6f)
                        : kind == 1 ? new Color(0.23f, 0.27f, 0.33f) : Color.Lerp(new Color(0.32f, 0.39f, 0.47f), new Color(0.56f, 0.65f, 0.74f), r);
                else
                    win = r < 0.42f ? Color.Lerp(new Color(1f, 0.82f, 0.48f), new Color(1f, 0.95f, 0.82f), (float)rnd.NextDouble()) : new Color(0.04f, 0.05f, 0.09f);
                px.Rect(b * bw + 3, f * fh + 4, (b + 1) * bw - 3, (f + 1) * fh - 3, win);
            }
            return px.ToTexture("Facade" + kind);
        }

        /// <summary>Soft clouds with alpha for the sky dome.</summary>
        public static Texture2D Clouds(int size = 512)
        {
            var px = new Px(size, size, new Color32(255, 255, 255, 0));
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                // Tileable by sampling a torus-mapped noise.
                float u = i / (float)size, v = j / (float)size;
                float a = Fbm(Mathf.Cos(u * 6.2832f) * 2.2f + 9, Mathf.Sin(u * 6.2832f) * 2.2f + v * 7f, 5);
                float cover = SmoothStep(0.46f, 0.74f, a);
                float shade = SmoothStep(0.5f, 0.9f, Fbm(Mathf.Cos(u * 6.2832f) * 2.2f + 9.3f, Mathf.Sin(u * 6.2832f) * 2.2f + v * 7f + 0.2f, 5));
                var c = Color.Lerp(Color.white, new Color(0.74f, 0.79f, 0.87f), shade * 0.8f);
                c.a = cover * 0.92f;
                px.P[j * size + i] = c;
            }
            return px.ToTexture("Clouds", true, TextureWrapMode.Repeat, FilterMode.Bilinear, 1);
        }

        /// <summary>Cricket ball: red leather with a white seam (equirectangular).</summary>
        public static Texture2D Ball(bool white)
        {
            const int w = 128, h = 64;
            var leather = white ? new Color(0.95f, 0.95f, 0.93f) : new Color(0.62f, 0.06f, 0.07f);
            var px = new Px(w, h, leather);
            for (int i = 0; i < w; i++)
            {
                int y = h / 2 + Mathf.RoundToInt(Mathf.Sin(i / (float)w * Mathf.PI * 4) * 3);
                px.Rect(i, y - 1, i + 1, y + 1, white ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.97f, 0.95f, 0.9f));
            }
            return px.ToTexture(white ? "BallWhite" : "Ball");
        }

        /// <summary>Soft radial dot (glows, blob shadows, flares).</summary>
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

        /// <summary>LED pixel grid used behind big-screen and board text.</summary>
        public static Texture2D LedPanel(Color a, Color b, int w = 512, int h = 144)
        {
            var px = new Px(w, h, Color.black);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float dx = (i - w / 2f) / (w / 2f), dy = (j - h / 2f) / (h / 2f);
                var c = Color.Lerp(a, b, Mathf.Clamp01(Mathf.Sqrt(dx * dx * 0.6f + dy * dy)));
                if (i % 4 == 0 || j % 4 == 0) c *= 0.82f;
                c.a = 1;
                px.P[j * w + i] = c;
            }
            return px.ToTexture("Led", true, TextureWrapMode.Clamp);
        }
    }
}
