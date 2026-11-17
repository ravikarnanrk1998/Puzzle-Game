using System;
using System.Collections.Generic;
using UnityEngine;

namespace KPL.Game
{
    public enum Icon
    {
        Coin, Trophy, Star, StarEmpty, Lock, Gear, Cart, Chart, Play, Back, Calendar, Bat, Ball, Home, Pause, Check, Cross, Info,
        Sound, Music, Vibrate, Bolt, Shield, Users, Gift, Target, Stumps, Arrow, Plus, Refresh, Medal, Flame, Clock, Eye, Batter,
    }

    /// <summary>Procedurally rasterised UI sprites (anti-aliased signed-distance shapes) and icons.</summary>
    public static class Sprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            Cache[key] = s;
            return s;
        }

        static Texture2D NewTex(int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            return t;
        }

        /// <summary>White rounded rectangle, 9-sliced so it scales to any size.</summary>
        public static Sprite Round(int radius)
        {
            return Get("round" + radius, () =>
            {
                int size = radius * 2 + 4;
                var t = NewTex(size, size, "Round" + radius);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundRectSd(x + 0.5f, y + 0.5f, size, size, radius);
                        byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                t.SetPixels32(px);
                t.Apply(false, true);
                float b = radius + 1;
                return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            });
        }

        /// <summary>Rounded outline ring (stroke) 9-sliced.</summary>
        public static Sprite Outline(int radius, float stroke = 2)
        {
            return Get("outline" + radius + "_" + stroke, () =>
            {
                int size = radius * 2 + 4;
                var t = NewTex(size, size, "Outline" + radius);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundRectSd(x + 0.5f, y + 0.5f, size, size, radius);
                        float a = Mathf.Clamp01(0.5f - Mathf.Abs(d + stroke * 0.5f) + stroke * 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                t.SetPixels32(px);
                t.Apply(false, true);
                float b = radius + 1;
                return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            });
        }

        static float RoundRectSd(float x, float y, float w, float h, float r)
        {
            float cx = Mathf.Abs(x - w / 2) - (w / 2 - 2 - r);
            float cy = Mathf.Abs(y - h / 2) - (h / 2 - 2 - r);
            float ox = Mathf.Max(cx, 0), oy = Mathf.Max(cy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(cx, cy), 0) - r;
        }

        /// <summary>Vertical gradient strip (white, alpha from bottom to top).</summary>
        public static Sprite VGradient(float bottomA, float topA)
        {
            return Get("vg" + bottomA + "_" + topA, () =>
            {
                var t = NewTex(4, 64, "VGrad");
                for (int y = 0; y < 64; y++)
                {
                    float a = Mathf.Lerp(bottomA, topA, y / 63f);
                    for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                t.Apply(false, true);
                return Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
            });
        }

        public static Sprite HGradient(float leftA, float rightA)
        {
            return Get("hg" + leftA + "_" + rightA, () =>
            {
                var t = NewTex(64, 4, "HGrad");
                for (int x = 0; x < 64; x++)
                {
                    float a = Mathf.Lerp(leftA, rightA, Mathf.SmoothStep(0, 1, x / 63f));
                    for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                t.Apply(false, true);
                return Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
            });
        }

        public static Sprite Circle => Get("circle", () => Shape(128, (x, y) => 0.5f - (Mathf.Sqrt(x * x + y * y) - 60)));

        public static Sprite Ring(float thickness) => Get("ring" + thickness, () => Shape(128, (x, y) => 0.5f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - (62 - thickness / 2)) + thickness / 2));

        public static Sprite Glow => Get("glow", () =>
        {
            var t = NewTex(128, 128, "Glow");
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
                    float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        });

        /// <summary>Diagonal pin-stripe pattern used on hero panels.</summary>
        public static Sprite Stripes => Get("stripes", () =>
        {
            var t = NewTex(64, 64, "Stripes");
            t.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float v = Mathf.Repeat((x + y) / 16f, 1);
                    float a = v < 0.18f ? 0.12f : 0;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        });

        static Sprite Shape(int size, Func<float, float, float> coverage)
        {
            var t = NewTex(size, size, "Shape");
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(coverage(x + 0.5f - c, y + 0.5f - c));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            t.SetPixels32(px);
            t.Apply(true, true);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------- icons

        public static Sprite Of(Icon i) => Get("icon" + i, () => Shape(128, IconSd(i)));

        // Signed-distance helpers (negative = inside), coordinates centred, 128px box (-64..64).
        static float Circ(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r = 0)
        {
            float dx = Mathf.Abs(x - cx) - hw + r, dy = Mathf.Abs(y - cy) - hh + r;
            return Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0)) + Mathf.Min(Mathf.Max(dx, dy), 0) - r;
        }
        static float Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float h = Mathf.Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
            float dx = px - vx * h, dy = py - vy * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
        static float Poly(float x, float y, Vector2[] p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                var a = p[j];
                var b = p[i];
                d = Mathf.Min(d, Seg(x, y, a.x, a.y, b.x, b.y, 0));
                if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
            }
            return inside ? -d : d;
        }
        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Sub(float a, float b) => Mathf.Max(a, -b);
        static float Ring(float x, float y, float cx, float cy, float r, float w) => Mathf.Abs(Circ(x, y, cx, cy, r)) - w;

        static Vector2[] StarPts(float r1, float r2, float cy = 0)
        {
            var p = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                float r = i % 2 == 0 ? r1 : r2;
                p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r + cy);
            }
            return p;
        }

        static Func<float, float, float> IconSd(Icon i)
        {
            switch (i)
            {
                case Icon.Coin: return (x, y) => 0.5f - Union(Ring(x, y, 0, 0, 50, 7), Sub(Circ(x, y, 0, 0, 36), Union(Box(x, y, 0, 0, 7, 22, 3), Box(x, y, 0, 0, 20, 7, 3))));
                case Icon.Trophy:
                    return (x, y) =>
                    {
                        float cup = Union(Box(x, y, 0, 18, 30, 26, 10), Box(x, y, 0, 36, 34, 8, 2));
                        float handles = Union(Ring(x, y, -32, 20, 13, 5), Ring(x, y, 32, 20, 13, 5));
                        float stem = Box(x, y, 0, -16, 7, 14);
                        float bas = Box(x, y, 0, -38, 26, 8, 3);
                        return 0.5f - Union(Union(cup, Sub(handles, Box(x, y, 0, 20, 30, 40))), Union(stem, bas));
                    };
                case Icon.Star: return (x, y) => 0.5f - Poly(x, y, StarPts(56, 24, -3));
                case Icon.StarEmpty: return (x, y) => 0.5f - (Mathf.Abs(Poly(x, y, StarPts(54, 23, -3))) - 4);
                case Icon.Lock:
                    return (x, y) => 0.5f - Union(Sub(Box(x, y, 0, -14, 34, 28, 6), Union(Circ(x, y, 0, -8, 7), Box(x, y, 0, -20, 3, 10))), Sub(Ring(x, y, 0, 20, 22, 6), Box(x, y, 0, 6, 40, 14)));
                case Icon.Gear:
                    return (x, y) =>
                    {
                        float a = Mathf.Atan2(y, x);
                        float r = Mathf.Sqrt(x * x + y * y);
                        float teeth = 44 + 9 * Mathf.Clamp(Mathf.Cos(a * 8) * 3, -1, 1);
                        return 0.5f - Sub(r - teeth, Circ(x, y, 0, 0, 16));
                    };
                case Icon.Cart:
                    return (x, y) => 0.5f - Union(Union(Seg(x, y, -50, 40, -36, 40, 5), Poly(x, y, new[] { new Vector2(-36, 32), new Vector2(46, 32), new Vector2(36, -8), new Vector2(-26, -8) })),
                        Union(Circ(x, y, -20, -30, 9), Circ(x, y, 26, -30, 9)));
                case Icon.Chart: return (x, y) => 0.5f - Union(Union(Box(x, y, -32, -14, 10, 30, 2), Box(x, y, 0, 0, 10, 44, 2)), Box(x, y, 32, -26, 10, 18, 2));
                case Icon.Play: return (x, y) => 0.5f - Poly(x, y, new[] { new Vector2(-30, 44), new Vector2(46, 0), new Vector2(-30, -44) });
                case Icon.Back: return (x, y) => 0.5f - Union(Seg(x, y, 18, 40, -22, 0, 8), Seg(x, y, -22, 0, 18, -40, 8));
                case Icon.Arrow: return (x, y) => 0.5f - Union(Seg(x, y, -18, 40, 22, 0, 8), Seg(x, y, 22, 0, -18, -40, 8));
                case Icon.Calendar:
                    return (x, y) => 0.5f - Union(Sub(Box(x, y, 0, -6, 46, 42, 8), Box(x, y, 0, -14, 36, 26, 3)), Union(Union(Box(x, y, -22, 40, 6, 12, 3), Box(x, y, 22, 40, 6, 12, 3)), Box(x, y, -14, -10, 9, 8, 2)));
                case Icon.Bat:
                    return (x, y) =>
                    {
                        float c = Mathf.Cos(-0.75f), s = Mathf.Sin(-0.75f);
                        float u = x * c - y * s, v = x * s + y * c;
                        float blade = Box(u, v, 0, -14, 14, 38, 8);
                        float handle = Box(u, v, 0, 38, 5, 18, 3);
                        return 0.5f - Union(blade, handle);
                    };
                case Icon.Ball:
                    return (x, y) => 0.5f - Sub(Circ(x, y, 0, 0, 46), Union(Ring(x, y, -70, 0, 62, 2.5f), Ring(x, y, 70, 0, 62, 2.5f)));
                case Icon.Home:
                    return (x, y) => 0.5f - Union(Poly(x, y, new[] { new Vector2(0, 50), new Vector2(52, 6), new Vector2(-52, 6) }), Sub(Box(x, y, 0, -20, 34, 30, 3), Box(x, y, 0, -34, 10, 18)));
                case Icon.Pause: return (x, y) => 0.5f - Union(Box(x, y, -18, 0, 10, 40, 4), Box(x, y, 18, 0, 10, 40, 4));
                case Icon.Check: return (x, y) => 0.5f - Union(Seg(x, y, -40, 2, -12, -28, 9), Seg(x, y, -12, -28, 42, 32, 9));
                case Icon.Cross: return (x, y) => 0.5f - Union(Seg(x, y, -34, -34, 34, 34, 9), Seg(x, y, -34, 34, 34, -34, 9));
                case Icon.Plus: return (x, y) => 0.5f - Union(Box(x, y, 0, 0, 9, 40, 4), Box(x, y, 0, 0, 40, 9, 4));
                case Icon.Info: return (x, y) => 0.5f - Union(Sub(Circ(x, y, 0, 0, 52), Circ(x, y, 0, 0, 42)), Union(Box(x, y, 0, -8, 7, 20, 3), Circ(x, y, 0, 24, 7)));
                case Icon.Sound:
                    return (x, y) => 0.5f - Union(Poly(x, y, new[] { new Vector2(-44, 16), new Vector2(-24, 16), new Vector2(4, 40), new Vector2(4, -40), new Vector2(-24, -16), new Vector2(-44, -16) }),
                        Union(Sub(Ring(x, y, 4, 0, 22, 5), Box(x, y, -20, 0, 24, 60)), Sub(Ring(x, y, 4, 0, 40, 5), Box(x, y, -20, 0, 24, 60))));
                case Icon.Music: return (x, y) => 0.5f - Union(Union(Circ(x, y, -22, -30, 14), Circ(x, y, 26, -22, 14)), Union(Union(Box(x, y, -10, 6, 4, 36), Box(x, y, 38, 14, 4, 36)), Seg(x, y, -10, 40, 38, 48, 7)));
                case Icon.Vibrate: return (x, y) => 0.5f - Union(Sub(Box(x, y, 0, 0, 22, 42, 7), Box(x, y, 0, 0, 13, 32, 3)), Union(Seg(x, y, -38, 20, -38, -20, 4), Seg(x, y, 38, 20, 38, -20, 4)));
                case Icon.Bolt: return (x, y) => 0.5f - Poly(x, y, new[] { new Vector2(8, 56), new Vector2(-30, -4), new Vector2(-2, -4), new Vector2(-10, -56), new Vector2(30, 8), new Vector2(2, 8) });
                case Icon.Shield: return (x, y) => 0.5f - Poly(x, y, new[] { new Vector2(0, 52), new Vector2(44, 38), new Vector2(40, -8), new Vector2(0, -54), new Vector2(-40, -8), new Vector2(-44, 38) });
                case Icon.Users: return (x, y) => 0.5f - Union(Union(Circ(x, y, -18, 20, 16), Box(x, y, -18, -24, 26, 20, 12)), Union(Circ(x, y, 24, 14, 13), Box(x, y, 24, -26, 20, 16, 10)));
                case Icon.Gift: return (x, y) => 0.5f - Sub(Union(Union(Box(x, y, 0, -16, 40, 32, 4), Box(x, y, 0, 22, 46, 10, 3)), Union(Ring(x, y, -14, 40, 12, 5), Ring(x, y, 14, 40, 12, 5))), Box(x, y, 0, 0, 5, 60));
                case Icon.Target: return (x, y) => 0.5f - Union(Union(Ring(x, y, 0, 0, 46, 6), Ring(x, y, 0, 0, 26, 6)), Circ(x, y, 0, 0, 9));
                case Icon.Stumps: return (x, y) => 0.5f - Union(Union(Box(x, y, -22, -6, 5, 44, 3), Box(x, y, 0, -6, 5, 44, 3)), Union(Box(x, y, 22, -6, 5, 44, 3), Box(x, y, 0, 44, 28, 4, 2)));
                case Icon.Refresh: return (x, y) => 0.5f - Union(Sub(Ring(x, y, 0, 0, 38, 7), Poly(x, y, new[] { new Vector2(0, 0), new Vector2(60, 60), new Vector2(60, 10) })), Poly(x, y, new[] { new Vector2(22, 46), new Vector2(50, 30), new Vector2(26, 14) }));
                case Icon.Medal: return (x, y) => 0.5f - Union(Union(Circ(x, y, 0, -16, 30), Seg(x, y, -18, 50, 0, 10, 8)), Seg(x, y, 18, 50, 0, 10, 8));
                case Icon.Flame:
                    return (x, y) => 0.5f - Union(Circ(x, y, 0, -16, 32), Poly(x, y, new[] { new Vector2(-30, -10), new Vector2(-8, 56), new Vector2(4, 24), new Vector2(18, 40), new Vector2(31, -10) }));
                case Icon.Clock: return (x, y) => 0.5f - Union(Ring(x, y, 0, 0, 44, 6), Union(Seg(x, y, 0, 0, 0, 28, 5), Seg(x, y, 0, 0, 20, -10, 5)));
                case Icon.Batter:
                    // Batter at the end of a lofted drive: bat swung high over the shoulder.
                    return (x, y) =>
                    {
                        float head = Circ(x, y, -10, 36, 8.5f);
                        float torso = Poly(x, y, new[] { new Vector2(-19, 27), new Vector2(-1, 29), new Vector2(7, 1), new Vector2(-10, -3) });
                        float legs = Union(Seg(x, y, -6, -2, -24, -42, 6.5f), Seg(x, y, 4, -1, 16, -42, 6.5f));
                        float feet = Union(Seg(x, y, -24, -44, -35, -46, 4), Seg(x, y, 16, -44, 26, -46, 4));
                        float arms = Union(Seg(x, y, -14, 23, -27, 40, 4.5f), Seg(x, y, -2, 24, -24, 42, 4.5f));
                        float bat = Union(Seg(x, y, -25, 41, -15, 46, 3), Seg(x, y, -15, 46, 27, 60, 6.5f));
                        float pads = Union(Seg(x, y, -21, -30, -25, -42, 8), Seg(x, y, 14, -28, 16, -40, 8));
                        return 0.5f - Union(Union(Union(head, torso), Union(legs, feet)), Union(Union(arms, bat), pads));
                    };
                default: return (x, y) => 0.5f - Union(Sub(Box(x, y, 0, 0, 52, 30, 30), Circ(x, y, 0, 0, 20)), Circ(x, y, 0, 0, 10));
            }
        }
    }
}
