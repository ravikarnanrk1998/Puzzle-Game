using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UA.Game
{
    /// <summary>Tileable value / fbm / cellular noise for texture synthesis (thread-safe, allocation-free).</summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Wrap(int v, int p) => p <= 0 ? v : ((v % p) + p) % p;

        /// <summary>Value noise; x, y in lattice units; period wraps the lattice (tileable).</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            // Hot path of all texture synthesis: integer floor and a single wrap per axis.
            int xi = (int)x, yi = (int)y;
            if (x < xi) xi--;
            if (y < yi) yi--;
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int x0, x1, y0, y1;
            if (period > 0)
            {
                x0 = xi % period; if (x0 < 0) x0 += period;
                y0 = yi % period; if (y0 < 0) y0 += period;
                x1 = x0 + 1; if (x1 == period) x1 = 0;
                y1 = y0 + 1; if (y1 == period) y1 = 0;
            }
            else { x0 = xi; x1 = xi + 1; y0 = yi; y1 = yi + 1; }
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
        }

        /// <summary>Fractal noise over u, v in 0..1 (tileable), result about 0..1.</summary>
        public static float Fbm(float u, float v, int freq, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = freq;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u * f, v * f, f, seed + o * 131) * amp;
                norm += amp;
                amp *= gain;
                f *= 2;
            }
            return sum / norm;
        }

        /// <summary>Ridged fractal noise (sharp crests) for veins, cracks and wrinkles; 0..1.</summary>
        public static float Ridged(float u, float v, int freq, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = freq;
            for (int o = 0; o < octaves; o++)
            {
                float n = Value(u * f, v * f, f, seed + o * 71);
                n = 1 - Math.Abs(n * 2 - 1);
                sum += n * n * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        /// <summary>Cellular noise: distance to nearest and second-nearest feature point (tileable).</summary>
        public static void Cells(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
        {
            float x = u * cells, y = v * cells;
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            f1 = f2 = 9;
            id = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = xi + dx, cy = yi + dy;
                    int wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    float px = cx + Hash(wx, wy, seed), py = cy + Hash(wx, wy, seed + 17);
                    float d = (float)Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; id = wx * 7919 + wy; }
                    else if (d < f2) f2 = d;
                }
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Math.Max(0, Math.Min(1, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>Float RGBA pixel buffer with parallel fill and conversion to textures.</summary>
    public sealed class Pix
    {
        public readonly int W, H;
        public readonly Color[] C;

        public Pix(int w, int h)
        {
            W = w;
            H = h;
            C = new Color[w * h];
        }

        public delegate Color Shader(float u, float v, int x, int y);

        /// <summary>Main-thread milliseconds spent generating textures (fills, uploads, normal maps); for load profiling.</summary>
        public static double GenMs, FillMs, UploadMs, NormalMs;
        public static int GenCount, GenPixels;

        public static void ResetStats() { GenMs = FillMs = UploadMs = NormalMs = 0; GenCount = GenPixels = 0; }

        public static string Stats() => "texgen " + GenMs.ToString("0") + " ms (fill " + FillMs.ToString("0") + ", upload " + UploadMs.ToString("0") + ", normal " + NormalMs.ToString("0") + "), " + GenCount + " fills, " + (GenPixels / 1000000f).ToString("0.0") + " Mpx, " + Environment.ProcessorCount + " cpus";

        public Pix Fill(Shader f)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int w = W, h = H;
            var c = C;
            Parallel.For(0, h, y =>
            {
                float v = (y + 0.5f) / h;
                int row = y * w;
                for (int x = 0; x < w; x++) c[row + x] = f((x + 0.5f) / w, v, x, y);
            });
            double ms = sw.Elapsed.TotalMilliseconds;
            GenMs += ms;
            FillMs += ms;
            GenCount++;
            GenPixels += w * h;
            return this;
        }

        public Color this[int x, int y]
        {
            get => C[((y % H + H) % H) * W + ((x % W + W) % W)];
            set => C[((y % H + H) % H) * W + ((x % W + W) % W)] = value;
        }

        /// <summary>Alpha-blends a colour into a soft disc (used for splats, holes, sprites).</summary>
        public void Blot(float cx, float cy, float r, Color col, float hardness = 0.6f, bool wrap = true)
        {
            int x0 = (int)(cx - r - 1), x1 = (int)(cx + r + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!wrap && (x < 0 || y < 0 || x >= W || y >= H)) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d >= 1) continue;
                    float a = Mathf.Clamp01((1 - d) / Mathf.Max(0.01f, 1 - hardness)) * col.a;
                    var o = this[x, y];
                    this[x, y] = new Color(Mathf.Lerp(o.r, col.r, a), Mathf.Lerp(o.g, col.g, a), Mathf.Lerp(o.b, col.b, a), Mathf.Max(o.a, a));
                }
        }

        public void Rect(int x0, int y0, int w, int h, Color col)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    var o = C[y * W + x];
                    C[y * W + x] = Color.Lerp(o, new Color(col.r, col.g, col.b, Mathf.Max(o.a, col.a)), col.a);
                }
        }

        public Texture2D ToTexture(string name, bool linear = false, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat, FilterMode filter = FilterMode.Trilinear, bool keepReadable = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var px = new Color32[C.Length];
            var c = C;
            int w = W;
            Parallel.For(0, H, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++) px[row + x] = c[row + x];
            });
            var t = Upload(px, W, H, mips, linear, name, wrap, filter, keepReadable);
            GenMs += sw.Elapsed.TotalMilliseconds;
            UploadMs += sw.Elapsed.TotalMilliseconds;
            return t;
        }

        /// <summary>
        /// Creates an RGBA32 texture from pixels. Mip levels are box-filtered on worker threads (Unity's
        /// Apply(true) builds them single-threaded on the main thread, which dominated level load time).
        /// </summary>
        public static Texture2D Upload(Color32[] px, int w, int h, bool mips, bool linear, string name, TextureWrapMode wrap = TextureWrapMode.Repeat, FilterMode filter = FilterMode.Trilinear, bool keepReadable = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { name = name, wrapMode = wrap, filterMode = filter, anisoLevel = 2 };
            t.SetPixelData(px, 0);
            if (mips)
            {
                int levels = t.mipmapCount;
                var src = px;
                int sw = w, sh = h;
                for (int l = 1; l < levels; l++)
                {
                    int dw = Math.Max(1, sw >> 1), dh = Math.Max(1, sh >> 1);
                    var dst = new Color32[dw * dh];
                    var s = src;
                    int sW = sw, sH = sh;
                    Action<int> row = y =>
                    {
                        int y0 = Math.Min(sH - 1, y * 2) * sW, y1 = Math.Min(sH - 1, y * 2 + 1) * sW;
                        for (int x = 0; x < dw; x++)
                        {
                            int x0 = Math.Min(sW - 1, x * 2), x1 = Math.Min(sW - 1, x * 2 + 1);
                            Color32 a = s[y0 + x0], b = s[y0 + x1], c = s[y1 + x0], d = s[y1 + x1];
                            dst[y * dw + x] = new Color32((byte)((a.r + b.r + c.r + d.r + 2) >> 2), (byte)((a.g + b.g + c.g + d.g + 2) >> 2),
                                (byte)((a.b + b.b + c.b + d.b + 2) >> 2), (byte)((a.a + b.a + c.a + d.a + 2) >> 2));
                        }
                    };
                    if (dw * dh >= 16384) Parallel.For(0, dh, row);
                    else for (int y = 0; y < dh; y++) row(y);
                    t.SetPixelData(dst, l);
                    src = dst;
                    sw = dw;
                    sh = dh;
                }
            }
            t.Apply(false, !keepReadable);
            return t;
        }

        /// <summary>
        /// Tangent-space normal map from a height field. Mobile shaders unpack RGB (or AG with ASTC
        /// encoding), so mobile stores (x, y, z, x); desktop shaders unpack DXT5nm-style (a * r, g),
        /// so desktop stores (1, y, z, x).
        /// </summary>
        public static Texture2D NormalMap(string name, float[] height, int w, int h, float strength)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool mobile = Application.isMobilePlatform;
            var px = new Color32[w * h];
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    float l = height[y * w + (x + w - 1) % w], r = height[y * w + (x + 1) % w];
                    float d = height[((y + h - 1) % h) * w + x], u = height[((y + 1) % h) * w + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized;
                    byte bx = (byte)(Mathf.Clamp01(n.x * 0.5f + 0.5f) * 255), by = (byte)(Mathf.Clamp01(n.y * 0.5f + 0.5f) * 255), bz = (byte)(Mathf.Clamp01(n.z * 0.5f + 0.5f) * 255);
                    px[y * w + x] = new Color32(mobile ? bx : (byte)255, by, bz, bx);
                }
            });
            var t = Upload(px, w, h, true, true, name);
            GenMs += sw.Elapsed.TotalMilliseconds;
            NormalMs += sw.Elapsed.TotalMilliseconds;
            return t;
        }
    }

    /// <summary>5x7 bitmap font for painting signs, posters and labels into textures.</summary>
    public static class PixelFont
    {
        // Each glyph: 7 rows (top to bottom), 5 bits per row (MSB = left column).
        static readonly (char, byte[])[] Table =
        {
            ('A', new byte[] { 14, 17, 17, 31, 17, 17, 17 }), ('B', new byte[] { 30, 17, 17, 30, 17, 17, 30 }), ('C', new byte[] { 14, 17, 16, 16, 16, 17, 14 }),
            ('D', new byte[] { 30, 17, 17, 17, 17, 17, 30 }), ('E', new byte[] { 31, 16, 16, 30, 16, 16, 31 }), ('F', new byte[] { 31, 16, 16, 30, 16, 16, 16 }),
            ('G', new byte[] { 14, 17, 16, 23, 17, 17, 15 }), ('H', new byte[] { 17, 17, 17, 31, 17, 17, 17 }), ('I', new byte[] { 14, 4, 4, 4, 4, 4, 14 }),
            ('J', new byte[] { 7, 2, 2, 2, 2, 18, 12 }), ('K', new byte[] { 17, 18, 20, 24, 20, 18, 17 }), ('L', new byte[] { 16, 16, 16, 16, 16, 16, 31 }),
            ('M', new byte[] { 17, 27, 21, 21, 17, 17, 17 }), ('N', new byte[] { 17, 17, 25, 21, 19, 17, 17 }), ('O', new byte[] { 14, 17, 17, 17, 17, 17, 14 }),
            ('P', new byte[] { 30, 17, 17, 30, 16, 16, 16 }), ('Q', new byte[] { 14, 17, 17, 17, 21, 18, 13 }), ('R', new byte[] { 30, 17, 17, 30, 20, 18, 17 }),
            ('S', new byte[] { 15, 16, 16, 14, 1, 1, 30 }), ('T', new byte[] { 31, 4, 4, 4, 4, 4, 4 }), ('U', new byte[] { 17, 17, 17, 17, 17, 17, 14 }),
            ('V', new byte[] { 17, 17, 17, 17, 17, 10, 4 }), ('W', new byte[] { 17, 17, 17, 21, 21, 21, 10 }), ('X', new byte[] { 17, 17, 10, 4, 10, 17, 17 }),
            ('Y', new byte[] { 17, 17, 10, 4, 4, 4, 4 }), ('Z', new byte[] { 31, 1, 2, 4, 8, 16, 31 }),
            ('0', new byte[] { 14, 17, 19, 21, 25, 17, 14 }), ('1', new byte[] { 4, 12, 4, 4, 4, 4, 14 }), ('2', new byte[] { 14, 17, 1, 2, 4, 8, 31 }),
            ('3', new byte[] { 31, 2, 4, 2, 1, 17, 14 }), ('4', new byte[] { 2, 6, 10, 18, 31, 2, 2 }), ('5', new byte[] { 31, 16, 30, 1, 1, 17, 14 }),
            ('6', new byte[] { 6, 8, 16, 30, 17, 17, 14 }), ('7', new byte[] { 31, 1, 2, 4, 8, 8, 8 }), ('8', new byte[] { 14, 17, 17, 14, 17, 17, 14 }),
            ('9', new byte[] { 14, 17, 17, 15, 1, 2, 12 }),
            ('-', new byte[] { 0, 0, 0, 31, 0, 0, 0 }), ('!', new byte[] { 4, 4, 4, 4, 4, 0, 4 }), ('.', new byte[] { 0, 0, 0, 0, 0, 0, 4 }),
            ('>', new byte[] { 8, 4, 2, 1, 2, 4, 8 }), ('<', new byte[] { 2, 4, 8, 16, 8, 4, 2 }), ('%', new byte[] { 25, 26, 2, 4, 8, 11, 19 }),
            ('+', new byte[] { 0, 4, 4, 31, 4, 4, 0 }), ('/', new byte[] { 1, 1, 2, 4, 8, 16, 16 }), (':', new byte[] { 0, 4, 4, 0, 4, 4, 0 }),
            ('\'', new byte[] { 4, 4, 0, 0, 0, 0, 0 }), ('?', new byte[] { 14, 17, 1, 2, 4, 0, 4 }), ('#', new byte[] { 10, 10, 31, 10, 31, 10, 10 }),
        };

        static bool[][] _map;

        static bool[] Glyph(char ch)
        {
            if (_map == null)
            {
                var map = new bool[128][];
                foreach (var (k, rows) in Table)
                {
                    var bits = new bool[35];
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 5; c++)
                            bits[r * 5 + c] = (rows[r] & (1 << (4 - c))) != 0;
                    map[k] = bits;
                }
                _map = map;
            }
            ch = char.ToUpperInvariant(ch);
            return ch < 128 ? _map[ch] : null;
        }

        public static int Width(string s, int scale) => s.Length * 6 * scale - scale;

        /// <summary>Draws text with its lower-left corner at (x, y) in pixel space (y up).</summary>
        public static void Draw(Pix p, string s, int x, int y, int scale, Color col, float jitter = 0, int seed = 0)
        {
            for (int c = 0; c < s.Length; c++)
            {
                var g = Glyph(s[c]);
                if (g != null)
                {
                    for (int row = 0; row < 7; row++)
                        for (int colI = 0; colI < 5; colI++)
                        {
                            if (!g[row * 5 + colI]) continue;
                            int px = x + c * 6 * scale + colI * scale;
                            int py = y + (6 - row) * scale;
                            float a = col.a * (jitter > 0 ? 1 - jitter * Noise.Hash(px, py, seed) : 1);
                            p.Rect(px, py, scale, scale, new Color(col.r, col.g, col.b, a));
                        }
                }
            }
        }

        public static void DrawCentered(Pix p, string s, int cx, int cy, int scale, Color col, float jitter = 0, int seed = 0)
        {
            Draw(p, s, cx - Width(s, scale) / 2, cy - 7 * scale / 2, scale, col, jitter, seed);
        }
    }
}
