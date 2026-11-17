using System;
using System.Collections.Generic;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>Procedural textures (noise-based grass, clay pitch, crowd, skies, concrete, ...). Cached.</summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D Get(string key, Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = key;
            Cache[key] = t;
            return t;
        }

        public static void ClearMatchTextures()
        {
            var drop = new List<string>();
            foreach (var kv in Cache)
            {
                if (kv.Key.StartsWith("crowd") || kv.Key.StartsWith("outfield"))
                {
                    UnityEngine.Object.Destroy(kv.Value);
                    drop.Add(kv.Key);
                }
            }
            foreach (var k in drop) Cache.Remove(k);
        }

        // --------------------------------------------------------------- noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Fade(float t) => t * t * (3 - 2 * t);

        /// <summary>Value noise, tileable with the given period (in lattice cells).</summary>
        public static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Fade(x - x0), fy = Fade(y - y0);
            int Wrap(int v) => period > 0 ? ((v % period) + period) % period : v;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int octaves, int period, int seed)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                s += Noise(x, y, period, seed + o * 17) * amp;
                norm += amp;
                x *= 2;
                y *= 2;
                period *= 2;
                amp *= 0.5f;
            }
            return s / norm;
        }

        /// <summary>Distance to the nearest Voronoi edge (cracks), tileable.</summary>
        static float Cells(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float d1 = 9, d2 = 9;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j;
                    int wx = period > 0 ? ((cx % period) + period) % period : cx, wy = period > 0 ? ((cy % period) + period) % period : cy;
                    float px = cx + Hash(wx, wy, seed), py = cy + Hash(wx, wy, seed + 7);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < d1)
                    {
                        d2 = d1;
                        d1 = d;
                    }
                    else if (d < d2) d2 = d;
                }
            }
            return Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
        }

        static Texture2D New(int w, int h, bool mips = true, bool linear = false, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = Quality.Current >= Gfx.High ? 4 : 1 };
        }

        static Texture2D Finish(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply(t.mipmapCount > 1, true);
            return t;
        }

        /// <summary>Tangent-space normal map from a height field (wraps at edges).</summary>
        static Texture2D NormalFromHeight(float[] h, int w, int hh, float strength, string name)
        {
            var t = New(w, hh, true, true);
            var px = new Color32[w * hh];
            for (int y = 0; y < hh; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = h[y * w + (x + w - 1) % w], r = h[y * w + (x + 1) % w];
                    float d = h[((y + hh - 1) % hh) * w + x], u = h[((y + 1) % hh) * w + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized;
                    px[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
            }
            t.name = name;
            return Finish(t, px);
        }

        public static Texture2D FlatNormal => Get("flatnormal", () =>
        {
            var t = New(4, 4, false, true);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(128, 128, 255, 255);
            return Finish(t, px);
        });

        public static Texture2D White => Get("white", () =>
        {
            var t = New(4, 4, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            return Finish(t, px);
        });

        // -------------------------------------------------------------- grass

        static float GrassHeight(int x, int y, int n)
        {
            float fx = x / (float)n, fy = y / (float)n;
            // Blades: stretched noise, plus clumps.
            float blades = Noise(fx * 96, fy * 22, 96, 11) * 0.6f + Noise(fx * 160, fy * 40, 160, 12) * 0.4f;
            float clumps = Fbm(fx * 8, fy * 8, 3, 8, 13);
            return blades * 0.7f + clumps * 0.3f;
        }

        /// <summary>Tileable neutral-grey grass detail (×2 multiply detail map).</summary>
        public static Texture2D GrassDetail => Get("grassdetail", () =>
        {
            int n = Quality.TexSize(512);
            var t = New(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float h = GrassHeight(x, y, n);
                    float v = 0.36f + h * 0.3f;
                    float warm = Fbm(x / (float)n * 4, y / (float)n * 4, 2, 4, 21) - 0.5f;
                    px[y * n + x] = new Color(v + warm * 0.05f, v + 0.015f, v - warm * 0.04f - 0.02f, 1);
                }
            }
            return Finish(t, px);
        });

        public static Texture2D GrassNormal => Get("grassnormal", () =>
        {
            int n = Quality.TexSize(512);
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++) h[y * n + x] = GrassHeight(x, y, n);
            }
            return NormalFromHeight(h, n, n, 2.2f, "grassnormal");
        });

        /// <summary>The whole outfield as one colour map: mowing stripes, 30-yard circle, wear around the square.</summary>
        public static Texture2D Outfield(float extent, double rSquare, double rStraight, uint teamTint)
        {
            return Get("outfield" + extent + "_" + rSquare + "_" + rStraight, () =>
            {
                int n = Quality.TexSize(1024);
                var t = New(n, n, true, false, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                // Lush, freshly mown outfield with bold alternating stripes across the pitch.
                var g0 = new Color(0.13f, 0.4f, 0.08f);
                var g1 = new Color(0.3f, 0.6f, 0.15f);
                var dry = new Color(0.42f, 0.5f, 0.22f);
                for (int y = 0; y < n; y++)
                {
                    float wz = (y + 0.5f) / n * 2 * extent - extent;
                    for (int x = 0; x < n; x++)
                    {
                        float wx = (x + 0.5f) / n * 2 * extent - extent;
                        float sp = Mathf.Repeat(wz / 11f, 1);
                        // Soft edges between bands, like real mowing.
                        float stripe = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(sp - 0.5f) - 0.22f) / 0.06f));
                        var c = Color.Lerp(g0, g1, 0.2f + stripe * 0.7f);
                        float big = Fbm(wx / 40f + 50, wz / 40f + 50, 3, 0, 31);
                        c *= 0.92f + big * 0.16f;
                        float worn = Fbm(wx / 6f, wz / 6f, 3, 0, 32);
                        // Edges of the playing area turn a little drier.
                        double norm = Math.Sqrt(wx * wx / (rSquare * rSquare) + wz * wz / (rStraight * rStraight));
                        if (norm > 0.92) c = Color.Lerp(c, dry, (float)Math.Min(0.35, (norm - 0.92) * 1.6) * worn);
                        // The square around the pitch is mown short and lighter.
                        if (Mathf.Abs(wx) < 9 && Mathf.Abs(wz) < 15) c = Color.Lerp(c, new Color(0.3f, 0.5f, 0.2f), 0.25f);
                        // 30-yard ring (27.4 m from each set of stumps joined by straight lines).
                        float ring = Mathf.Abs(wz) < 10.06f ? Mathf.Abs(Mathf.Abs(wx) - 27.43f) : Mathf.Abs(Mathf.Sqrt(wx * wx + (Mathf.Abs(wz) - 10.06f) * (Mathf.Abs(wz) - 10.06f)) - 27.43f);
                        float dash = Mathf.Repeat(Mathf.Atan2(wz, wx) * 30f, 1f) < 0.55f ? 1 : 0;
                        if (ring < 0.12f * extent / 90f * 1.6f && dash > 0) c = Color.Lerp(c, Color.white, 0.75f);
                        // Rope shadow ring.
                        if (norm > 0.995 && norm < 1.01) c *= 0.85f;
                        c.a = 1;
                        px[y * n + x] = c;
                    }
                }
                return Finish(t, px);
            });
        }

        // -------------------------------------------------------------- pitch

        const float PitchW = 3.66f, PitchL = 24f;

        static float PitchHeight(float wx, float wz)
        {
            float crack = Mathf.Clamp01(Cells(wx * 6.5f, wz * 6.5f, 0, 41) * 14f) * 0.5f + 0.5f;
            float rough = Fbm(wx * 6, wz * 6, 3, 0, 42);
            float foot = 0;
            foreach (float end in new[] { -1f, 1f })
            {
                float dz = wz - end * 9.2f;
                if (Mathf.Abs(dz) < 2.2f && Mathf.Abs(wx) < 1.4f) foot += Fbm(wx * 10, wz * 10, 2, 0, 43) * (1 - Mathf.Abs(dz) / 2.2f) * 0.6f;
            }
            return crack * 0.55f + rough * 0.35f - foot * 0.4f;
        }

        public static Texture2D Pitch => Get("pitch", () =>
        {
            int w = Quality.TexSize(256), h = w * 4;
            var t = New(w, h, true, false, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            var clay = new Color(0.72f, 0.6f, 0.42f);
            var dark = new Color(0.52f, 0.41f, 0.27f);
            var grassy = new Color(0.45f, 0.5f, 0.28f);
            for (int y = 0; y < h; y++)
            {
                float wz = (y + 0.5f) / h * PitchL - PitchL / 2;
                for (int x = 0; x < w; x++)
                {
                    float wx = (x + 0.5f) / w * PitchW - PitchW / 2;
                    // Fine hairline cracks, fading out where the surface is smoother.
                    float crack = Mathf.Clamp01(Cells(wx * 6.5f, wz * 6.5f, 0, 41) * 14f);
                    float crackMask = Mathf.Clamp01(Fbm(wx * 0.9f, wz * 0.9f, 2, 0, 47) * 1.8f - 0.45f);
                    float n1 = Fbm(wx * 2, wz * 2, 4, 0, 44);
                    var c = Color.Lerp(dark, clay, 0.45f + n1 * 0.6f);
                    c = Color.Lerp(c * (1 - 0.22f * crackMask), c, crack);
                    float tufts = Fbm(wx * 14, wz * 14, 2, 0, 45);
                    if (tufts > 0.62f) c = Color.Lerp(c, grassy, (tufts - 0.62f) * 2.2f);
                    // Edges blend into the grass.
                    float edge = Mathf.Abs(wx) / (PitchW / 2);
                    if (edge > 0.82f) c = Color.Lerp(c, new Color(0.33f, 0.47f, 0.2f), Mathf.SmoothStep(0, 1, (edge - 0.82f) / 0.18f));
                    // Bowlers' footmarks: darker, scuffed.
                    foreach (float end in new[] { -1f, 1f })
                    {
                        float dz = wz - end * 9.6f;
                        if (Mathf.Abs(dz) < 1.6f && Mathf.Abs(wx) < 1.3f)
                        {
                            float f = Fbm(wx * 9, wz * 9, 2, 0, 46) * (1 - Mathf.Abs(dz) / 1.6f);
                            c = Color.Lerp(c, dark * 0.85f, f * 0.7f);
                        }
                    }
                    // White crease lines (5 cm).
                    float lw = 0.03f;
                    bool line = false;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        if (Mathf.Abs(wz - s * 8.84f) < lw && Mathf.Abs(wx) < 1.83f) line = true;
                        if (Mathf.Abs(wz - s * 10.06f) < lw && Mathf.Abs(wx) < 1.32f) line = true;
                        if (Mathf.Abs(Mathf.Abs(wx) - 1.32f) < lw && s * wz > 8.84f && s * wz < 11.3f) line = true;
                        // Wide guidelines.
                        if (Mathf.Abs(Mathf.Abs(wx) - 0.89f) < lw * 0.8f && s * wz > 8.1f && s * wz < 8.84f) line = true;
                    }
                    if (line) c = Color.Lerp(c, new Color(0.97f, 0.97f, 0.95f), 0.92f);
                    c.a = 1;
                    px[y * w + x] = c;
                }
            }
            return Finish(t, px);
        });

        public static Texture2D PitchNormal => Get("pitchnormal", () =>
        {
            int w = Quality.TexSize(256), h = w * 4;
            var hf = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float wz = (y + 0.5f) / h * PitchL - PitchL / 2;
                for (int x = 0; x < w; x++) hf[y * w + x] = PitchHeight((x + 0.5f) / w * PitchW - PitchW / 2, wz);
            }
            var t = NormalFromHeight(hf, w, h, 3f, "pitchnormal");
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        });

        // -------------------------------------------------------------- crowd

        static readonly Color[] ShirtPool =
        {
            new Color(0.9f, 0.9f, 0.9f), new Color(0.12f, 0.12f, 0.14f), new Color(0.75f, 0.12f, 0.15f), new Color(0.12f, 0.3f, 0.75f),
            new Color(0.95f, 0.75f, 0.1f), new Color(0.2f, 0.6f, 0.3f), new Color(0.9f, 0.45f, 0.1f), new Color(0.55f, 0.25f, 0.65f), new Color(0.3f, 0.7f, 0.85f),
        };

        static readonly Color[] SkinPool = { new Color(0.86f, 0.68f, 0.55f), new Color(0.74f, 0.55f, 0.4f), new Color(0.6f, 0.42f, 0.3f), new Color(0.45f, 0.31f, 0.22f), new Color(0.33f, 0.22f, 0.16f) };

        static readonly Color[] Clothes =
        {
            new Color(0.93f, 0.93f, 0.92f), new Color(0.55f, 0.72f, 0.92f), new Color(0.12f, 0.18f, 0.36f), new Color(0.1f, 0.1f, 0.11f), new Color(0.55f, 0.56f, 0.58f),
            new Color(0.95f, 0.8f, 0.2f), new Color(0.95f, 0.5f, 0.15f), new Color(0.78f, 0.15f, 0.18f), new Color(0.2f, 0.55f, 0.3f), new Color(0.92f, 0.45f, 0.6f),
        };

        static readonly Color[] Faces = { new Color(0.78f, 0.58f, 0.44f), new Color(0.66f, 0.47f, 0.34f), new Color(0.55f, 0.38f, 0.27f), new Color(0.43f, 0.29f, 0.2f), new Color(0.86f, 0.68f, 0.55f) };

        /// <summary>
        /// Rows of fans drawn person by person: shaded shirt, neck, face, hair or cap and arms, with
        /// front rows overlapping the ones behind. Frame 1 has many fans standing with arms and flags up.
        /// </summary>
        public static Texture2D Crowd(Color home, Color homeSec, Color away, Color seat, int frame, int seed)
        {
            return Get("crowd" + home + homeSec + away + seat + frame + seed, () =>
            {
                bool hi = Quality.Current >= Gfx.High;
                int w = hi ? 1024 : Quality.TexSize(512), h = w;
                int rows = hi ? 10 : 8, cols = hi ? 20 : 16;
                var t = New(w, h, true, false);
                var px = new Color32[w * h];
                float cw = w / (float)cols, rh = h / (float)rows;
                // Seats and risers.
                for (int y = 0; y < h; y++)
                {
                    float v = y / rh - Mathf.Floor(y / rh);
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / cw - Mathf.Floor(x / cw);
                        Color c;
                        if (v < 0.14f) c = new Color(0.2f, 0.2f, 0.22f) * (0.7f + v * 2);
                        else if (v < 0.5f) c = seat * (0.55f + v * 0.5f) * (u < 0.06f || u > 0.94f ? 0.6f : 1);
                        else c = seat * 0.45f;
                        c.a = 1;
                        px[y * w + x] = c;
                    }
                }
                void Fill(int x0, int y0, int x1, int y1, System.Func<int, int, Color?> f)
                {
                    for (int y = Mathf.Max(0, y0); y < y1; y++)
                    {
                        int yy = y >= h ? y - h : y;
                        if (yy < 0 || yy >= h) continue;
                        for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                        {
                            var c = f(x, y);
                            if (c != null) px[yy * w + x] = c.Value;
                        }
                    }
                }
                var palRng = new System.Random(seed * 17);
                // Back rows first, so the rows in front overlap them.
                for (int r = rows - 1; r >= 0; r--)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        double pr = palRng.NextDouble();
                        bool empty = pr < 0.07;
                        double pick = palRng.NextDouble();
                        // Mostly the home colours and white, a few away fans and casual clothes, slightly muted.
                        Color shirt = pick < 0.44 ? home : pick < 0.58 ? homeSec : pick < 0.66 ? away : pick < 0.84 ? Clothes[palRng.Next(2)] : Clothes[palRng.Next(Clothes.Length)];
                        shirt = Color.Lerp(shirt, new Color(0.6f, 0.6f, 0.62f), 0.18f) * (0.85f + (float)palRng.NextDouble() * 0.25f);
                        var skin = Faces[palRng.Next(Faces.Length)];
                        bool cap = palRng.NextDouble() < 0.22;
                        // Always draw both numbers so frame 0 and frame 1 show the same people.
                        double stand = palRng.NextDouble(), flagRoll = palRng.NextDouble();
                        bool standing = frame == 1 && stand < 0.6;
                        bool flag = standing && flagRoll < 0.12;
                        float jx = (float)(palRng.NextDouble() - 0.5) * cw * 0.16f;
                        float jy = (float)(palRng.NextDouble() - 0.5) * rh * 0.05f + (standing ? rh * 0.16f : 0);
                        if (empty) continue;
                        float cx = col * cw + cw * 0.5f + jx;
                        float baseY = r * rh + rh * 0.12f + jy;
                        float torsoW = cw * 0.41f, torsoTop = baseY + rh * 0.6f;
                        float headY = baseY + rh * 0.81f, hrx = cw * 0.2f, hry = rh * 0.165f;
                        // Torso with rounded shoulders and side shading.
                        Fill((int)(cx - torsoW - 2), (int)baseY, (int)(cx + torsoW + 2), (int)(torsoTop + 2), (x, y) =>
                        {
                            float dx = (x - cx) / torsoW;
                            float top = torsoTop - rh * 0.1f * dx * dx;
                            if (Mathf.Abs(dx) > 1 || y > top) return null;
                            float sh = 0.72f + 0.38f * (1 - dx * dx) + (y - baseY) / rh * 0.15f;
                            var c = shirt * sh;
                            c.a = 1;
                            return c;
                        });
                        // Arms raised when standing.
                        if (standing)
                        {
                            foreach (float sgn in new[] { -1f, 1f })
                            {
                                float ax = cx + sgn * torsoW * 0.85f;
                                Fill((int)(ax - cw * 0.06f), (int)(torsoTop - rh * 0.1f), (int)(ax + cw * 0.06f), (int)(headY + rh * 0.42f), (x, y) => skin * 0.92f);
                            }
                            if (flag)
                            {
                                float fx = cx + torsoW * 0.85f;
                                Fill((int)fx, (int)(headY + rh * 0.3f), (int)(fx + cw * 0.5f), (int)(headY + rh * 0.62f), (x, y) => (y - (int)(headY + rh * 0.3f)) < rh * 0.12f ? homeSec : home);
                            }
                        }
                        // Neck and head.
                        Fill((int)(cx - hrx * 0.45f), (int)(torsoTop - 3), (int)(cx + hrx * 0.45f), (int)(headY), (x, y) => skin * 0.85f);
                        Fill((int)(cx - hrx - 1), (int)(headY - hry - 1), (int)(cx + hrx + 1), (int)(headY + hry + 2), (x, y) =>
                        {
                            float dx = (x - cx) / hrx, dy = (y - headY) / hry;
                            if (dx * dx + dy * dy > 1) return null;
                            if (cap && dy > 0.25f) return dy > 0.25f && dy < 0.45f && dx < 0 ? home * 0.7f : home;
                            if (dy > 0.35f || (dy > 0.1f && Mathf.Abs(dx) > 0.75f)) return new Color(0.07f, 0.055f, 0.045f);
                            var c = skin * (0.85f + 0.2f * (1 - Mathf.Abs(dx)));
                            if (dy > -0.05f && dy < 0.12f && Mathf.Abs(Mathf.Abs(dx) - 0.38f) < 0.12f) c *= 0.55f;
                            c.a = 1;
                            return c;
                        });
                    }
                }
                // Subtle grain so it doesn't look flat.
                for (int i = 0; i < px.Length; i++)
                {
                    float n = 0.94f + (Hash(i % w, i / w, seed + 99) * 0.12f);
                    var c = px[i];
                    px[i] = new Color32((byte)Mathf.Min(255, c.r * n), (byte)Mathf.Min(255, c.g * n), (byte)Mathf.Min(255, c.b * n), 255);
                }
                return Finish(t, px);
            });
        }

        /// <summary>Older dot-style crowd (kept for very low memory budgets).</summary>
        public static Texture2D CrowdSimple(Color home, Color away, Color seat, int frame, int seed)
        {
            return Get("crowd" + home + away + seat + frame + seed, () =>
            {
                int w = Quality.TexSize(512), h = w;
                int rows = Quality.CrowdRows, cols = 16;
                var t = New(w, h, true, false);
                var px = new Color32[w * h];
                float cw = w / (float)cols, rh = h / (float)rows;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int row = (int)(y / rh), col = (int)(x / cw);
                        float u = x / cw - col, v = y / rh - row;
                        // Step riser at the bottom of each row, seat in the middle.
                        Color c;
                        if (v < 0.16f) c = new Color(0.16f, 0.16f, 0.18f) * (0.8f + v * 1.5f);
                        else c = seat * (0.65f + 0.2f * v);
                        int id = row * 131 + col * 17 + seed * 7;
                        float r0 = Hash(col, row, seed), r1 = Hash(col, row, seed + 1), r2 = Hash(col, row, seed + 2);
                        bool present = r0 < 0.88f;
                        if (present)
                        {
                            var shirt = r1 < 0.5f ? home : r1 < 0.68f ? away : ShirtPool[(int)(r2 * ShirtPool.Length) % ShirtPool.Length];
                            shirt *= 0.8f + Hash(col, row, seed + 3) * 0.3f;
                            var skin = SkinPool[(int)(Hash(col, row, seed + 4) * SkinPool.Length) % SkinPool.Length];
                            float cx = 0.5f + (Hash(col, row, seed + 5) - 0.5f) * 0.15f;
                            bool cheer = frame == 1 && Hash(col, row, seed + 6) < 0.55f;
                            float lift = cheer ? 0.06f : 0;
                            // Torso.
                            float tdx = (u - cx) / 0.3f, tdy = (v - (0.38f + lift)) / 0.26f;
                            if (tdx * tdx + tdy * tdy * 0.6f < 1 && v > 0.16f) c = shirt * (0.85f + 0.15f * (1 - Mathf.Abs(tdx)));
                            // Head.
                            float hdx = (u - cx) / 0.15f, hdy = (v - (0.7f + lift)) / 0.13f;
                            if (hdx * hdx + hdy * hdy < 1)
                            {
                                c = skin;
                                if (hdy > 0.35f && Hash(col, row, seed + 8) < 0.8f) c = new Color(0.08f, 0.06f, 0.05f);
                            }
                            // Arms up.
                            if (cheer && v > 0.6f && v < 0.98f && (Mathf.Abs(u - (cx - 0.24f)) < 0.05f || Mathf.Abs(u - (cx + 0.24f)) < 0.05f)) c = skin * 0.95f;
                            // Flags in some hands.
                            if (cheer && Hash(col, row, seed + 9) < 0.12f && v > 0.82f && u > cx + 0.2f && u < cx + 0.48f) c = home * 1.1f;
                        }
                        _ = id;
                        c.a = 1;
                        px[y * w + x] = c;
                    }
                }
                return Finish(t, px);
            });
        }

        // ---------------------------------------------------------------- sky

        /// <summary>Equirectangular sky. Day: haze, scattered cumulus and a sun glow. Night: dark gradient, stars and city glow.</summary>
        public static Texture2D Sky(bool day, float sunAz, float sunEl)
        {
            return Get("sky" + day + sunAz + sunEl, () =>
            {
                int w = Quality.Current >= Gfx.High ? 2048 : 1024, h = w / 2;
                var t = New(w, h, false, false);
                t.wrapModeU = TextureWrapMode.Repeat;
                t.wrapModeV = TextureWrapMode.Clamp;
                var px = new Color32[w * h];
                var sunDir = new Vector3(Mathf.Cos(sunEl * Mathf.Deg2Rad) * Mathf.Sin(sunAz * Mathf.Deg2Rad), Mathf.Sin(sunEl * Mathf.Deg2Rad), Mathf.Cos(sunEl * Mathf.Deg2Rad) * Mathf.Cos(sunAz * Mathf.Deg2Rad));
                for (int y = 0; y < h; y++)
                {
                    float el = (y + 0.5f) / h * 180f - 90f;
                    float s = Mathf.Sin(el * Mathf.Deg2Rad);
                    for (int x = 0; x < w; x++)
                    {
                        float az = (x + 0.5f) / w * 360f;
                        Color c;
                        float u = x / (float)w;
                        if (day)
                        {
                            // Deep summer blue overhead, pale haze at the horizon.
                            var zen = new Color(0.09f, 0.33f, 0.82f);
                            var hor = new Color(0.62f, 0.8f, 0.97f);
                            c = Color.Lerp(hor, zen, Mathf.Pow(Mathf.Clamp01(s), 0.5f));
                            if (el < 0) c = Color.Lerp(hor, new Color(0.55f, 0.62f, 0.6f), Mathf.Clamp01(-s * 4));
                            var dir = new Vector3(Mathf.Cos(el * Mathf.Deg2Rad) * Mathf.Sin(az * Mathf.Deg2Rad), s, Mathf.Cos(el * Mathf.Deg2Rad) * Mathf.Cos(az * Mathf.Deg2Rad));
                            float sd = Mathf.Max(0, Vector3.Dot(dir, sunDir));
                            c += new Color(1f, 0.92f, 0.75f) * (Mathf.Pow(sd, 18) * 0.45f + Mathf.Pow(sd, 400) * 3f);
                            if (el > 0.5f && el < 60)
                            {
                                // Puffy cumulus: billowing tops, flatter grey-blue bases.
                                float k = 1 - Mathf.Abs(el - 16) / 44f;
                                float warp = Fbm(u * 9 + 3, el / 14f, 3, 9, 62) * 0.6f;
                                float cl = Fbm(u * 22 + warp, el / 7.5f + warp * 0.5f, 5, 22, 61);
                                float cover = Mathf.Clamp01((cl - 0.47f) * 5f) * Mathf.Clamp01(k * 1.8f);
                                float shade = Fbm(u * 22 + warp + 0.05f, el / 7.5f + warp * 0.5f - 0.12f, 4, 22, 61);
                                float body = Mathf.Clamp01((shade - 0.45f) * 3f);
                                var cloud = Color.Lerp(new Color(0.98f, 0.99f, 1f), new Color(0.66f, 0.73f, 0.84f), body * 0.75f);
                                cloud += new Color(1f, 0.93f, 0.78f) * Mathf.Pow(sd, 5) * 0.3f;
                                c = Color.Lerp(c, cloud, cover * 0.97f);
                            }
                        }
                        else
                        {
                            var zen = new Color(0.008f, 0.012f, 0.035f);
                            var hor = new Color(0.06f, 0.07f, 0.12f);
                            c = Color.Lerp(hor, zen, Mathf.Pow(Mathf.Clamp01(s), 0.45f));
                            float glow = Mathf.Exp(-Mathf.Max(0, el) / 6f) * (0.7f + 0.3f * Fbm(u * 12, 0.5f, 2, 12, 71));
                            c += new Color(0.22f, 0.13f, 0.07f) * glow;
                            if (el < 0) c = Color.Lerp(c, new Color(0.03f, 0.03f, 0.04f), Mathf.Clamp01(-s * 5));
                            if (el > 8 && Hash(x, y, 72) > 0.9965f)
                            {
                                float b = 0.4f + Hash(x, y, 73) * 0.6f;
                                c += new Color(b, b, b * 1.05f);
                            }
                            if (el > 10 && el < 40)
                            {
                                float cl = Fbm(u * 14, el / 10f, 4, 14, 74);
                                c = Color.Lerp(c, new Color(0.1f, 0.09f, 0.11f), Mathf.Clamp01((cl - 0.55f) * 3f) * 0.5f);
                            }
                        }
                        c.a = 1;
                        px[y * w + x] = c;
                    }
                }
                return Finish(t, px);
            });
        }

        // ----------------------------------------------------------- materials

        public static Texture2D Concrete => Get("concrete", () =>
        {
            int n = Quality.TexSize(256);
            var t = New(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float f = Fbm(x / (float)n * 8, y / (float)n * 8, 4, 8, 81);
                    float v = 0.62f + (f - 0.5f) * 0.22f;
                    float stain = Fbm(x / (float)n * 3, y / (float)n * 3, 2, 3, 82);
                    v -= Mathf.Clamp01(stain - 0.6f) * 0.3f;
                    px[y * n + x] = new Color(v, v * 0.99f, v * 0.97f, 1);
                }
            }
            return Finish(t, px);
        });

        public static Texture2D MetalPanels => Get("metal", () =>
        {
            int n = Quality.TexSize(256);
            var t = New(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float rib = Mathf.Repeat(x / (float)n * 16, 1);
                    float v = 0.7f + (rib < 0.12f ? -0.18f : rib < 0.2f ? 0.08f : 0) + (Fbm(x / (float)n * 6, y / (float)n * 6, 3, 6, 91) - 0.5f) * 0.1f;
                    px[y * n + x] = new Color(v, v, v * 1.02f, 1);
                }
            }
            return Finish(t, px);
        });

        /// <summary>Office-block facade; the night variant lights a random set of windows.</summary>
        public static Texture2D Windows(bool night) => Get("windows" + night, () =>
        {
            int n = 256;
            var t = New(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int cx = x / 16, cy = y / 16;
                    float u = (x % 16) / 16f, v = (y % 16) / 16f;
                    bool win = u > 0.15f && u < 0.85f && v > 0.2f && v < 0.8f;
                    Color c;
                    if (night)
                    {
                        c = new Color(0.03f, 0.035f, 0.05f);
                        if (win && Hash(cx, cy, 101) < 0.42f) c = Color.Lerp(new Color(1f, 0.82f, 0.5f), new Color(0.75f, 0.85f, 1f), Hash(cx, cy, 102)) * (0.6f + Hash(cx, cy, 103) * 0.4f);
                    }
                    else
                    {
                        c = new Color(0.68f, 0.7f, 0.72f);
                        if (win) c = Color.Lerp(new Color(0.3f, 0.4f, 0.5f), new Color(0.55f, 0.65f, 0.75f), Hash(cx, cy, 104));
                    }
                    c.a = 1;
                    px[y * n + x] = c;
                }
            }
            return Finish(t, px);
        });

        public static Texture2D Ball => Get("ball", () =>
        {
            int w = 128, h = 64;
            var t = New(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)h;
                    float n = Fbm(x / 16f, y / 16f, 2, 8, 111);
                    var c = new Color(0.96f, 0.95f, 0.92f) * (0.92f + n * 0.08f);
                    float seam = Mathf.Abs(v - 0.5f - Mathf.Sin(x / (float)w * Mathf.PI * 2) * 0.05f);
                    if (seam < 0.035f) c = new Color(0.55f, 0.6f, 0.62f);
                    if (seam < 0.07f && seam > 0.04f && (x % 4) < 2) c = new Color(0.25f, 0.28f, 0.3f);
                    c.a = 1;
                    px[y * w + x] = c;
                }
            }
            return Finish(t, px);
        });

        public static Texture2D Willow => Get("willow", () =>
        {
            int w = 64, h = 256;
            var t = New(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float grain = Noise(x / 2.5f, y / 40f, 0, 121) * 0.6f + Noise(x / 1.2f, y / 25f, 0, 122) * 0.4f;
                    var c = Color.Lerp(new Color(0.84f, 0.74f, 0.55f), new Color(0.95f, 0.88f, 0.7f), grain);
                    c.a = 1;
                    px[y * w + x] = c;
                }
            }
            return Finish(t, px);
        });

        /// <summary>Head texture with simple facial features (eyes, brows, mouth) on a skin tone.</summary>
        public static Texture2D Face(int tone, int beard) => Get("face" + tone + "_" + beard, () =>
        {
            int n = 128;
            var t = New(n, n);
            var px = new Color32[n * n];
            var skin = SkinPool[Mathf.Clamp(tone, 0, SkinPool.Length - 1)];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    // Face centred at u = 0 / 1 (front of the head).
                    float fu = u > 0.5f ? u - 1 : u;
                    var c = skin * (0.93f + Fbm(u * 8, v * 8, 2, 8, 131) * 0.1f);
                    float ax = Mathf.Abs(fu);
                    // Eye sockets (soft shadow), eyes with a light sclera, brows.
                    float ex = (ax - 0.062f) / 0.03f, ey = (v - 0.49f) / 0.035f;
                    if (ex * ex + ey * ey < 1) c *= 0.82f;
                    if (Mathf.Abs(ax - 0.062f) < 0.02f && Mathf.Abs(v - 0.49f) < 0.011f) c = Color.Lerp(new Color(0.92f, 0.9f, 0.86f), new Color(0.1f, 0.07f, 0.05f), Mathf.Abs(ax - 0.062f) < 0.009f ? 1 : 0.15f);
                    if (Mathf.Abs(ax - 0.06f) < 0.03f && Mathf.Abs(v - 0.565f) < 0.01f) c = c * 0.35f;
                    // Nose shading and mouth.
                    if (ax < 0.014f && v > 0.36f && v < 0.47f) c *= 0.9f;
                    if (ax < 0.032f && Mathf.Abs(v - 0.245f) < 0.009f) c = c * 0.6f + new Color(0.3f, 0.1f, 0.1f) * 0.3f;
                    // Cheek shading for shape.
                    if (ax > 0.1f && ax < 0.2f && v > 0.25f && v < 0.45f) c *= 0.93f;
                    // Stubble / beard.
                    if (beard > 0 && ax < 0.13f && v > 0.03f && v < 0.31f) c *= beard == 2 ? 0.55f : 0.86f;
                    c.a = 1;
                    px[y * n + x] = c;
                }
            }
            return Finish(t, px);
        });

        public static Color SkinColor(int tone) => SkinPool[Mathf.Clamp(tone, 0, SkinPool.Length - 1)];

        /// <summary>Fabric weave normal for kits.</summary>
        public static Texture2D FabricNormal => Get("fabricnormal", () =>
        {
            int n = 128;
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++) h[y * n + x] = (Mathf.Sin(x * Mathf.PI / 2) * Mathf.Sin(y * Mathf.PI / 2)) * 0.5f + Noise(x / 8f, y / 8f, 16, 141) * 0.3f;
            }
            return NormalFromHeight(h, n, n, 1.2f, "fabricnormal");
        });

        /// <summary>Soft radial glow (lamp flares, ball shadow).</summary>
        public static Texture2D Glow => Get("glowtex", () =>
        {
            int n = 64;
            var t = New(n, n, false, false, TextureWrapMode.Clamp);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                    float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.4f);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            }
            return Finish(t, px);
        });

        /// <summary>Ring marker used for the pitch target / landing spot.</summary>
        public static Texture2D RingMark => Get("ringmark", () =>
        {
            int n = 128;
            var t = New(n, n, true, false, TextureWrapMode.Clamp);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
                    float ring = Mathf.Clamp01(1 - Mathf.Abs(d - 0.82f) / 0.08f);
                    float dot = Mathf.Clamp01(1 - d / 0.22f);
                    float fill = d < 0.82f ? 0.18f : 0;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Max(ring, Mathf.Max(dot, fill)));
                }
            }
            return Finish(t, px);
        });
    }
}
