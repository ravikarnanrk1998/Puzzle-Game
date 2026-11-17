using System;
using System.Collections.Generic;
using UnityEngine;

namespace UA.Game
{
    public enum Icon
    {
        Coin, Gem, Skull, Ammo, Health, Armor, Grenade, Reload, Jump, Crouch, Aim, Fire, Swap, Pause, Gear, Cart, Trophy, Lock, Star, Play,
        Back, Check, Plus, Flashlight, Pin, Boss, Timer, Survivor, Nest, Supply, Beacon, Pistol, Rifle, Shotgun, Sniper, MachineGun, Special,
        Calendar, Map, Skill, Close, Video, Bolt, Radiation, Restore, Info,
    }

    /// <summary>Procedural UI sprites: panels (9-slice), gradients, glows and an SDF icon set.</summary>
    public static class Sprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, int w, int h, Func<float, float, float> alpha, Vector4 border = default, int ppu = 100)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var p = new Pix(w, h).Fill((u, v, x, y) => new Color(1, 1, 1, Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f))));
            var t = p.ToTexture("ui_" + key, false, false, TextureWrapMode.Clamp, FilterMode.Bilinear);
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static float Edge(float d) => Mathf.Clamp01(0.5f - d);   // d = signed distance in pixels

        /// <summary>Rounded rectangle (9-slice).</summary>
        public static Sprite Rounded(int r = 16) => Make("round" + r, r * 2 + 4, r * 2 + 4, (x, y) =>
        {
            float hw = r + 2, hh = r + 2;
            float dx = Mathf.Max(Mathf.Abs(x - hw) - 2, 0), dy = Mathf.Max(Mathf.Abs(y - hh) - 2, 0);
            float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
            return Edge(d);
        }, new Vector4(r + 1, r + 1, r + 1, r + 1));

        /// <summary>Military panel with two cut corners (top-left, bottom-right) (9-slice).</summary>
        public static Sprite Cut(int c = 18) => Make("cut" + c, c * 3, c * 3, (x, y) =>
        {
            float n = c * 3;
            float d1 = (x + (n - y)) - c;          // top-left corner
            float d2 = ((n - x) + y) - c;          // bottom-right corner
            float a = Mathf.Clamp01(Mathf.Min(d1, d2) / 1.4f + 0.5f);
            return a;
        }, new Vector4(c + 2, c + 2, c + 2, c + 2));

        /// <summary>Outline only version of <see cref="Cut"/>.</summary>
        public static Sprite CutOutline(int c = 18, float th = 2.5f) => Make("cutline" + c + th, c * 3, c * 3, (x, y) =>
        {
            float n = c * 3;
            float d1 = (x + (n - y)) - c, d2 = ((n - x) + y) - c;
            float inside = Mathf.Min(Mathf.Min(d1, d2) / 1.414f, Mathf.Min(Mathf.Min(x, n - x), Mathf.Min(y, n - y)));
            return Mathf.Clamp01(inside + 0.5f) * Mathf.Clamp01(th - inside + 0.5f);
        }, new Vector4(c + 2, c + 2, c + 2, c + 2));

        public static Sprite RoundedOutline(int r = 16, float th = 2.5f) => Make("roundline" + r + th, r * 2 + 4, r * 2 + 4, (x, y) =>
        {
            float hw = r + 2;
            float dx = Mathf.Max(Mathf.Abs(x - hw) - 2, 0), dy = Mathf.Max(Mathf.Abs(y - hw) - 2, 0);
            float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
            return Edge(d) * Edge(-d - th);
        }, new Vector4(r + 1, r + 1, r + 1, r + 1));

        public static Sprite Circle => Make("circle", 128, 128, (x, y) => Edge(Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) - 62));

        public static Sprite Ring(float th) => Make("ring" + th, 128, 128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64));
            return Edge(d - 62) * Edge(62 - th - d);
        });

        public static Sprite Glow => Make("glow", 128, 128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64f;
            float a = Mathf.Clamp01(1 - d);
            return a * a;
        });

        public static Sprite VGradient => Make("vgrad", 4, 128, (x, y) => y / 128f);
        public static Sprite HGradient => Make("hgrad", 128, 4, (x, y) => x / 128f);
        public static Sprite White => Make("white", 4, 4, (x, y) => 1);

        /// <summary>Radial vignette (transparent centre, dark edges).</summary>
        public static Sprite Vignette => Make("vignette", 256, 144, (x, y) =>
        {
            float u = (x / 256f - 0.5f) * 1.6f, v = (y / 144f - 0.5f) * 1.6f;
            float d = Mathf.Sqrt(u * u + v * v);
            return Mathf.Clamp01((d - 0.55f) * 1.6f);
        });

        /// <summary>Sniper scope overlay: black with a round window and fine reticle.</summary>
        public static Sprite Scope => Make("scope", 512, 512, (x, y) =>
        {
            float dx = x - 256, dy = y - 256;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float outside = Mathf.Clamp01((d - 246) / 4f);
            float cross = (Mathf.Abs(dx) < 1.2f || Mathf.Abs(dy) < 1.2f) && d > 14 ? 1 : 0;
            float thick = (Mathf.Abs(dx) < 3.5f && Mathf.Abs(dy) > 120) || (Mathf.Abs(dy) < 3.5f && Mathf.Abs(dx) > 120) ? 1 : 0;
            float ticks = 0;
            for (int k = 1; k < 6; k++) if (Mathf.Abs(dy + k * 22) < 1 && Mathf.Abs(dx) < 10 - k) ticks = 1;
            return Mathf.Max(outside, Mathf.Max(cross * 0.85f, Mathf.Max(thick, ticks)));
        });

        // ------------------------------------------------------------------ icons

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r = 0)
        {
            float dx = Mathf.Abs(x - cx) - hw + r, dy = Mathf.Abs(y - cy) - hh + r;
            return Mathf.Min(Mathf.Max(dx, dy), 0) + new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude - r;
        }

        static float Circ(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float RingD(float x, float y, float cx, float cy, float r, float th) => Mathf.Abs(Circ(x, y, cx, cy, r)) - th;

        static float Seg(float x, float y, float ax, float ay, float bx, float by, float th)
        {
            var pa = new Vector2(x - ax, y - ay);
            var ba = new Vector2(bx - ax, by - ay);
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - th;
        }

        static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            // Signed distance to a triangle (approx via edges).
            float e0 = Side(x, y, a, b), e1 = Side(x, y, b, c), e2 = Side(x, y, c, a);
            float inside = Mathf.Max(e0, Mathf.Max(e1, e2));
            return inside;
        }

        static float Side(float x, float y, Vector2 a, Vector2 b)
        {
            var n = new Vector2(b.y - a.y, a.x - b.x).normalized;
            return Vector2.Dot(new Vector2(x, y) - a, n);
        }

        static float U(float a, float b) => Mathf.Min(a, b);
        static float Sub(float a, float b) => Mathf.Max(a, -b);

        /// <summary>White icon sprite (tint with Image.color). Coordinates in a 0..64 design box.</summary>
        public static Sprite Of(Icon icon) => Make("icon" + icon, 128, 128, (px, py) =>
        {
            float x = px / 2f, y = py / 2f;   // 64 design units
            float d;
            switch (icon)
            {
                case Icon.Coin: d = Sub(Circ(x, y, 32, 32, 26), U(RingD(x, y, 32, 32, 19, 1.6f), Box(x, y, 32, 32, 3, 11, 1.5f))); break;
                case Icon.Gem:
                    // Cut diamond: pavilion (bottom point) + crown, with facet lines.
                    d = Tri(x, y, new Vector2(32, 4), new Vector2(60, 38), new Vector2(4, 38));
                    d = U(d, Box(x, y, 32, 46, 16, 8));
                    d = U(d, U(Tri(x, y, new Vector2(4, 38), new Vector2(16, 38), new Vector2(16, 54)), Tri(x, y, new Vector2(48, 38), new Vector2(60, 38), new Vector2(48, 54))));
                    d = Sub(d, Seg(x, y, 6, 39, 58, 39, 1.1f));
                    d = Sub(d, U(Seg(x, y, 22, 39, 32, 6, 0.9f), Seg(x, y, 42, 39, 32, 6, 0.9f)));
                    d = Sub(d, U(Seg(x, y, 24, 39, 20, 54, 0.9f), Seg(x, y, 40, 39, 44, 54, 0.9f)));
                    break;
                case Icon.Skull:
                    d = U(Circ(x, y, 32, 38, 20), Box(x, y, 32, 18, 11, 9, 3));
                    d = Sub(d, U(Circ(x, y, 24, 36, 6), Circ(x, y, 40, 36, 6)));
                    d = Sub(d, Tri(x, y, new Vector2(32, 30), new Vector2(29, 24), new Vector2(35, 24)));
                    d = Sub(d, U(Box(x, y, 28, 13, 1, 4), Box(x, y, 36, 13, 1, 4)));
                    break;
                case Icon.Ammo:
                    d = 999;
                    for (int i = 0; i < 3; i++)
                    {
                        float cx = 20 + i * 12;
                        d = U(d, Box(x, y, cx, 24, 4.5f, 14, 1));
                        d = U(d, U(Circ(x, y, cx, 40, 4.5f), Tri(x, y, new Vector2(cx - 4.5f, 40), new Vector2(cx + 4.5f, 40), new Vector2(cx, 52))));
                    }
                    break;
                case Icon.Health: d = U(Box(x, y, 32, 32, 8, 22, 2), Box(x, y, 32, 32, 22, 8, 2)); break;
                case Icon.Armor:
                    d = U(Box(x, y, 32, 40, 20, 14, 4), Tri(x, y, new Vector2(12, 30), new Vector2(52, 30), new Vector2(32, 6)));
                    d = Sub(d, Seg(x, y, 32, 14, 32, 50, 1.5f));
                    break;
                case Icon.Grenade:
                    d = U(Circ(x, y, 30, 26, 17), Box(x, y, 32, 46, 6, 5, 1));
                    d = U(d, RingD(x, y, 44, 50, 6, 1.6f));
                    d = Sub(d, U(Seg(x, y, 18, 26, 42, 26, 1), Seg(x, y, 30, 12, 30, 40, 1)));
                    break;
                case Icon.Reload:
                {
                    float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;
                    d = RingD(x, y, 32, 32, 18, 3.5f);
                    if (ang > 50 && ang < 110) d = 999;
                    d = U(d, Tri(x, y, new Vector2(40, 58), new Vector2(46, 42), new Vector2(54, 54)));
                    break;
                }
                case Icon.Jump: d = U(Tri(x, y, new Vector2(32, 56), new Vector2(12, 34), new Vector2(52, 34)), Box(x, y, 32, 22, 7, 14, 2)); break;
                case Icon.Crouch: d = U(Tri(x, y, new Vector2(32, 10), new Vector2(12, 32), new Vector2(52, 32)), Box(x, y, 32, 46, 7, 10, 2)); d = U(d, Box(x, y, 32, 6, 22, 2.5f)); break;
                case Icon.Aim:
                    d = RingD(x, y, 32, 32, 18, 2.5f);
                    d = U(d, U(Box(x, y, 32, 8, 2, 8), Box(x, y, 32, 56, 2, 8)));
                    d = U(d, U(Box(x, y, 8, 32, 8, 2), Box(x, y, 56, 32, 8, 2)));
                    d = U(d, Circ(x, y, 32, 32, 3));
                    break;
                case Icon.Fire:
                    d = U(Box(x, y, 26, 32, 14, 6, 2), Box(x, y, 20, 22, 5, 12, 2));
                    d = U(d, Box(x, y, 46, 34, 8, 2.5f));
                    for (int i = 0; i < 3; i++) d = U(d, Seg(x, y, 54, 34, 62, 26 + i * 8, 1.4f));
                    break;
                case Icon.Swap:
                    d = U(Box(x, y, 28, 42, 16, 3), Tri(x, y, new Vector2(44, 50), new Vector2(44, 34), new Vector2(54, 42)));
                    d = U(d, U(Box(x, y, 36, 22, 16, 3), Tri(x, y, new Vector2(20, 30), new Vector2(20, 14), new Vector2(10, 22))));
                    break;
                case Icon.Pause: d = U(Box(x, y, 23, 32, 5, 18, 1.5f), Box(x, y, 41, 32, 5, 18, 1.5f)); break;
                case Icon.Gear:
                {
                    float ang = Mathf.Atan2(y - 32, x - 32);
                    float r = 20 + (Mathf.Repeat(ang * 8 / (Mathf.PI * 2), 1) < 0.5f ? 5 : 0);
                    d = Sub(Circ(x, y, 32, 32, r), Circ(x, y, 32, 32, 8));
                    break;
                }
                case Icon.Cart:
                    d = Box(x, y, 34, 36, 18, 10, 2);
                    d = U(d, Seg(x, y, 8, 52, 16, 52, 2));
                    d = U(d, Seg(x, y, 16, 52, 16, 26, 2));
                    d = U(d, U(Circ(x, y, 22, 14, 5), Circ(x, y, 46, 14, 5)));
                    break;
                case Icon.Trophy:
                    d = U(Box(x, y, 32, 42, 14, 14, 6), Box(x, y, 32, 22, 3, 8));
                    d = U(d, Box(x, y, 32, 12, 12, 3, 1));
                    d = U(d, U(RingD(x, y, 16, 44, 7, 2), RingD(x, y, 48, 44, 7, 2)));
                    break;
                case Icon.Lock: d = U(Box(x, y, 32, 24, 16, 13, 3), RingD(x, y, 32, 40, 11, 3)); d = Sub(d, Box(x, y, 32, 24, 2, 5)); break;
                case Icon.Star:
                {
                    float ang = Mathf.Atan2(y - 32, x - 32) + Mathf.PI / 2;
                    float k = Mathf.Abs(Mathf.Repeat(ang * 5 / (Mathf.PI * 2), 1) - 0.5f) * 2;
                    float r = Mathf.Lerp(12, 28, k);
                    d = Circ(x, y, 32, 32, r);
                    break;
                }
                case Icon.Play: d = Tri(x, y, new Vector2(18, 54), new Vector2(18, 10), new Vector2(54, 32)); break;
                case Icon.Back: d = U(Seg(x, y, 40, 54, 20, 32, 4), Seg(x, y, 20, 32, 40, 10, 4)); break;
                case Icon.Check: d = U(Seg(x, y, 12, 32, 26, 18, 4.5f), Seg(x, y, 26, 18, 52, 48, 4.5f)); break;
                case Icon.Plus: d = U(Box(x, y, 32, 32, 4, 20, 1.5f), Box(x, y, 32, 32, 20, 4, 1.5f)); break;
                case Icon.Close: d = U(Seg(x, y, 14, 14, 50, 50, 4), Seg(x, y, 14, 50, 50, 14, 4)); break;
                case Icon.Flashlight:
                    d = U(Box(x, y, 24, 32, 14, 7, 2), Tri(x, y, new Vector2(38, 26), new Vector2(38, 38), new Vector2(50, 46)));
                    d = U(d, Tri(x, y, new Vector2(38, 38), new Vector2(50, 18), new Vector2(50, 46)));
                    d = U(d, Seg(x, y, 54, 50, 62, 56, 1.4f));
                    d = U(d, Seg(x, y, 56, 32, 63, 32, 1.4f));
                    d = U(d, Seg(x, y, 54, 14, 62, 8, 1.4f));
                    break;
                case Icon.Pin: d = U(Circ(x, y, 32, 40, 16), Tri(x, y, new Vector2(18, 34), new Vector2(46, 34), new Vector2(32, 4))); d = Sub(d, Circ(x, y, 32, 40, 6)); break;
                case Icon.Boss:
                    d = U(Circ(x, y, 32, 32, 18), Box(x, y, 32, 14, 10, 8, 3));
                    d = U(d, U(Tri(x, y, new Vector2(14, 40), new Vector2(22, 46), new Vector2(6, 62)), Tri(x, y, new Vector2(50, 40), new Vector2(42, 46), new Vector2(58, 62))));
                    d = Sub(d, U(Tri(x, y, new Vector2(20, 36), new Vector2(30, 30), new Vector2(22, 26)), Tri(x, y, new Vector2(44, 36), new Vector2(34, 30), new Vector2(42, 26))));
                    break;
                case Icon.Timer:
                    d = U(RingD(x, y, 32, 30, 20, 3), Box(x, y, 32, 56, 6, 3));
                    d = U(d, U(Seg(x, y, 32, 30, 32, 42, 2.5f), Seg(x, y, 32, 30, 42, 26, 2.5f)));
                    break;
                case Icon.Survivor: d = U(Circ(x, y, 32, 48, 9), Box(x, y, 32, 20, 14, 16, 7)); break;
                case Icon.Nest:
                    d = U(U(Circ(x, y, 24, 26, 14), Circ(x, y, 42, 28, 12)), Circ(x, y, 32, 40, 13));
                    d = Sub(d, U(Circ(x, y, 26, 30, 3), Circ(x, y, 40, 34, 3)));
                    break;
                case Icon.Supply: d = Sub(Box(x, y, 32, 32, 22, 18, 3), U(Box(x, y, 32, 32, 3, 11), Box(x, y, 32, 32, 11, 3))); break;
                case Icon.Beacon:
                    d = U(Box(x, y, 32, 14, 14, 9, 2), Seg(x, y, 32, 22, 32, 50, 2));
                    d = U(d, Circ(x, y, 32, 52, 4));
                    d = U(d, U(RingD(x, y, 32, 52, 10, 1.5f), RingD(x, y, 32, 52, 17, 1.5f)));
                    d = Sub(d, Box(x, y, 32, 40, 30, 10));
                    d = U(d, Seg(x, y, 32, 22, 32, 50, 2));
                    break;
                case Icon.Pistol:
                    d = U(Box(x, y, 30, 40, 22, 6, 1.5f), Box(x, y, 18, 24, 6, 13, 2));
                    d = U(d, RingD(x, y, 28, 30, 5, 1.5f));
                    break;
                case Icon.Rifle:
                    d = U(Box(x, y, 34, 36, 18, 5, 1), Box(x, y, 58, 37, 6, 1.6f));
                    d = U(d, Box(x, y, 8, 32, 8, 6, 1.5f));
                    d = U(d, U(Box(x, y, 24, 26, 3, 7, 1), Box(x, y, 36, 24, 3, 9, 1)));
                    break;
                case Icon.Shotgun:
                    d = U(Box(x, y, 36, 38, 22, 3.5f, 1), Box(x, y, 40, 32, 10, 3, 1.5f));
                    d = U(d, Box(x, y, 9, 32, 9, 6, 2));
                    d = U(d, Box(x, y, 22, 28, 3, 7, 1));
                    break;
                case Icon.Sniper:
                    d = U(Box(x, y, 36, 34, 22, 3, 1), Box(x, y, 30, 42, 10, 3.5f, 2));
                    d = U(d, Box(x, y, 8, 30, 8, 6, 1.5f));
                    d = U(d, Box(x, y, 20, 24, 3, 7, 1));
                    break;
                case Icon.MachineGun:
                    d = U(Box(x, y, 34, 36, 20, 6, 1), Box(x, y, 58, 36, 6, 2));
                    d = U(d, Box(x, y, 8, 34, 8, 6, 1.5f));
                    d = U(d, Box(x, y, 30, 24, 7, 6, 1.5f));
                    d = U(d, U(Seg(x, y, 46, 30, 42, 14, 1.4f), Seg(x, y, 46, 30, 52, 14, 1.4f)));
                    break;
                case Icon.Special:
                    d = U(Box(x, y, 32, 36, 18, 8, 3), RingD(x, y, 52, 36, 6, 2));
                    d = U(d, Box(x, y, 22, 24, 4, 8, 1.5f));
                    d = U(d, Circ(x, y, 32, 36, 4));
                    break;
                case Icon.Calendar:
                    d = Sub(Box(x, y, 32, 28, 22, 20, 3), Box(x, y, 32, 24, 18, 12));
                    d = U(d, U(Box(x, y, 22, 50, 2.5f, 6), Box(x, y, 42, 50, 2.5f, 6)));
                    d = U(d, Box(x, y, 26, 22, 4, 4));
                    break;
                case Icon.Map:
                    d = Box(x, y, 32, 32, 24, 18, 1);
                    d = Sub(d, U(Seg(x, y, 24, 14, 24, 50, 1), Seg(x, y, 40, 14, 40, 50, 1)));
                    break;
                case Icon.Skill:
                    d = U(Circ(x, y, 32, 52, 7), U(Circ(x, y, 16, 20, 7), Circ(x, y, 48, 20, 7)));
                    d = U(d, U(Seg(x, y, 32, 52, 16, 20, 2), Seg(x, y, 32, 52, 48, 20, 2)));
                    break;
                case Icon.Video:
                    d = Box(x, y, 26, 32, 18, 14, 3);
                    d = U(d, Tri(x, y, new Vector2(44, 32), new Vector2(58, 20), new Vector2(58, 44)));
                    break;
                case Icon.Bolt: d = U(Tri(x, y, new Vector2(36, 62), new Vector2(14, 28), new Vector2(34, 30)), Tri(x, y, new Vector2(28, 2), new Vector2(50, 36), new Vector2(30, 34))); break;
                case Icon.Radiation:
                {
                    float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg + 90;
                    bool blade = Mathf.Repeat(ang, 120) < 60;
                    d = blade ? Sub(Circ(x, y, 32, 32, 26), Circ(x, y, 32, 32, 10)) : 999;
                    d = U(d, Circ(x, y, 32, 32, 6));
                    break;
                }
                case Icon.Restore:
                {
                    float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;
                    d = RingD(x, y, 32, 32, 18, 3.5f);
                    if (ang > 120 && ang < 170) d = 999;
                    d = U(d, Tri(x, y, new Vector2(6, 40), new Vector2(22, 40), new Vector2(14, 28)));
                    d = U(d, Seg(x, y, 32, 32, 32, 42, 2.5f));
                    d = U(d, Seg(x, y, 32, 32, 40, 32, 2.5f));
                    break;
                }
                default:
                    d = Sub(Circ(x, y, 32, 32, 26), U(Box(x, y, 32, 26, 3, 11, 1), Circ(x, y, 32, 44, 3.5f)));
                    break;
            }
            return Mathf.Clamp01(0.5f - d * 2);
        });

        // ------------------------------------------------------------------ touch-control art

        static Sprite MakeColor(string key, int w, int h, Func<float, float, Color> f)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var p = new Pix(w, h).Fill((u, v, x, y) => f(x + 0.5f, y + 0.5f));
            // Mipmapped: these are drawn well below their texture size on most screens.
            var t = p.ToTexture("ui_" + key, false, true, TextureWrapMode.Clamp, FilterMode.Trilinear);
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        /// <summary>Triangle with either winding (<see cref="Tri"/> needs counter-clockwise points).</summary>
        static float TriA(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            return area >= 0 ? Tri(x, y, a, b, c) : Tri(x, y, a, c, b);
        }

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1) * Mathf.Min(rx, ry);
        }

        static float Rot(float x, float y, float cx, float cy, float deg, out float ry)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float dx = x - cx, dy = y - cy;
            ry = cy + dx * s + dy * c;
            return cx + dx * c - dy * s;
        }

        /// <summary>
        /// Touch-control icons (fire, aim, reload, jump, crouch, grenade, swap): 192 px, mipmapped, two-tone
        /// (main shape opaque, detail at 60 %) so they read at a glance and stay sharp on large phones.
        /// Other icons fall back to <see cref="Of"/>.
        /// </summary>
        public static Sprite Control(Icon icon)
        {
            switch (icon)
            {
                case Icon.Fire: case Icon.Aim: case Icon.Reload: case Icon.Jump: case Icon.Crouch: case Icon.Grenade: case Icon.Swap: break;
                default: return Of(icon);
            }
            return MakeColor("ctrl" + icon, 192, 192, (px, py) =>
            {
                float x = px / 3f, y = py / 3f;   // 64 design units
                float a = 999, b = 999;            // a: main shape, b: detail
                switch (icon)
                {
                    case Icon.Fire:
                    {
                        // Cartridge pointing right with speed lines: "shoot".
                        float bullet = U(Box(x, y, 45, 32, 4.5f, 7.4f), Sub(Ellipse(x, y, 47, 32, 12, 7.4f), Box(x, y, 36, 32, 11, 10)));
                        float casing = U(Box(x, y, 29.5f, 32, 10.5f, 8.2f, 1.2f), Box(x, y, 18, 32, 2, 9.4f, 0.8f));
                        casing = Sub(casing, Box(x, y, 21.3f, 32, 0.9f, 8.4f));
                        a = U(Sub(bullet, Box(x, y, 40.6f, 32, 0.7f, 9)), casing);
                        b = U(Seg(x, y, 5, 23, 12, 23, 1.7f), U(Seg(x, y, 1.5f, 32, 12, 32, 1.7f), Seg(x, y, 5, 41, 12, 41, 1.7f)));
                        break;
                    }
                    case Icon.Aim:
                        // Scope reticle: ring, broken crosshair, centre dot; outer ticks as detail.
                        a = RingD(x, y, 32, 32, 21, 2.6f);
                        a = U(a, U(Seg(x, y, 32, 14, 32, 25, 1.7f), Seg(x, y, 32, 39, 32, 50, 1.7f)));
                        a = U(a, U(Seg(x, y, 14, 32, 25, 32, 1.7f), Seg(x, y, 39, 32, 50, 32, 1.7f)));
                        a = U(a, Circ(x, y, 32, 32, 2.6f));
                        b = U(U(Seg(x, y, 32, 55.5f, 32, 62, 2.4f), Seg(x, y, 32, 2, 32, 8.5f, 2.4f)), U(Seg(x, y, 2, 32, 8.5f, 32, 2.4f), Seg(x, y, 55.5f, 32, 62, 32, 2.4f)));
                        break;
                    case Icon.Reload:
                    {
                        // Curved magazine inside a circular arrow.
                        float mx = Rot(x, y, 32, 31, 14, out float my);
                        a = Box(mx, my, 32, 29, 8, 12.5f, 1.8f);                                  // body
                        a = U(a, Box(mx, my, 32, 15.3f, 9.5f, 2, 0.9f));                          // base plate
                        a = U(a, Box(mx, my, 32, 42.5f, 6.5f, 1.6f, 0.6f));                       // feed lips
                        a = U(a, U(Ellipse(mx, my, 32, 46.4f, 3.4f, 4.4f), Box(mx, my, 32, 44.2f, 3.4f, 1.6f))); // top round
                        a = Sub(a, Box(mx, my, 32, 29, 1, 8.5f));                                 // stamped rib
                        float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;
                        b = RingD(x, y, 32, 32, 25.5f, 2.4f);
                        if (ang > 62 && ang < 118) b = 999;
                        b = U(b, TriA(x, y, new Vector2(48.7f, 60.6f), new Vector2(41.1f, 47.5f), new Vector2(35.6f, 58.8f)));
                        break;
                    }
                    case Icon.Jump:
                        // Double chevron up.
                        a = U(Seg(x, y, 13, 31, 32, 50, 4.4f), Seg(x, y, 32, 50, 51, 31, 4.4f));
                        b = U(Seg(x, y, 13, 13, 32, 32, 4.4f), Seg(x, y, 32, 32, 51, 13, 4.4f));
                        break;
                    case Icon.Crouch:
                        // Kneeling soldier, with a down arrow as detail.
                        a = Circ(x, y, 41, 50.5f, 6.4f);
                        a = U(a, Seg(x, y, 39, 41, 31.5f, 25, 5.4f));
                        a = U(a, U(Seg(x, y, 31.5f, 24, 45, 21.5f, 4.8f), Seg(x, y, 45.5f, 21.5f, 46.5f, 7.5f, 4.2f)));
                        a = U(a, Seg(x, y, 46, 5.8f, 53.5f, 5.8f, 2.6f));
                        a = U(a, U(Seg(x, y, 31.5f, 24, 25, 9, 4.8f), Seg(x, y, 25, 8, 12.5f, 7, 4)));
                        a = U(a, U(Seg(x, y, 38, 38.5f, 47, 31, 3.2f), Seg(x, y, 47, 31, 55, 35, 2.8f)));
                        b = U(Seg(x, y, 11, 57, 11, 38, 2.4f), TriA(x, y, new Vector2(4, 40), new Vector2(18, 40), new Vector2(11, 29)));
                        break;
                    case Icon.Grenade:
                        // Frag grenade: segmented body and fuze; spoon and pin ring as detail.
                        a = Ellipse(x, y, 30, 24, 15.5f, 18.5f);
                        a = Sub(a, U(Seg(x, y, 12, 24, 48, 24, 0.85f), U(Seg(x, y, 14, 14.5f, 46, 14.5f, 0.85f), Seg(x, y, 14, 33.5f, 46, 33.5f, 0.85f))));
                        a = Sub(a, Seg(x, y, 30, 4, 30, 44, 0.85f));
                        a = U(a, Box(x, y, 31, 45.5f, 6.5f, 4, 1));
                        b = U(Seg(x, y, 37, 48.5f, 48.5f, 43, 2.1f), Seg(x, y, 48.5f, 43, 48, 22, 2.1f));
                        b = U(b, U(RingD(x, y, 18.5f, 53, 5.6f, 1.6f), Seg(x, y, 23.5f, 50.5f, 26.5f, 48, 1.3f)));
                        break;
                    case Icon.Swap:
                        // Swap weapons: two opposed arrows.
                        a = U(Seg(x, y, 9, 41, 44, 41, 3), TriA(x, y, new Vector2(43, 50.5f), new Vector2(43, 31.5f), new Vector2(57, 41)));
                        b = U(Seg(x, y, 55, 23, 20, 23, 3), TriA(x, y, new Vector2(21, 32.5f), new Vector2(21, 13.5f), new Vector2(7, 23)));
                        break;
                }
                float ca = Mathf.Clamp01(0.5f - a * 3), cb = Mathf.Clamp01(0.5f - b * 3) * 0.6f;
                return new Color(1, 1, 1, Mathf.Max(ca, cb));
            });
        }

        /// <summary>
        /// Round button face: white at the top shading to 62 % at the bottom with a darker rim, so a single tint
        /// gives a lit, slightly domed look.
        /// </summary>
        public static Sprite ButtonFace => MakeColor("btnface", 192, 192, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 96) * (x - 96) + (y - 96) * (y - 96));
            float a = Mathf.Clamp01(0.5f - (r - 94));
            float v = Mathf.Lerp(0.62f, 1f, y / 192f) * Mathf.Lerp(1f, 0.78f, Mathf.SmoothStep(0.72f, 1f, r / 94f));
            return new Color(v, v, v, a);
        });

        /// <summary>Soft highlight across the top of a round button (glass sheen).</summary>
        public static Sprite ButtonSheen => MakeColor("btnsheen", 192, 192, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 96) * (x - 96) + (y - 96) * (y - 96));
            float inside = Mathf.Clamp01(0.5f - (r - 88));
            float top = Mathf.SmoothStep(0.5f, 0.95f, y / 192f);
            float ell = Mathf.Clamp01(1 - Mathf.Abs(x - 96) / 80f);
            return new Color(1, 1, 1, inside * top * Mathf.Sqrt(ell) * 0.32f);
        });

        /// <summary>
        /// Glow outside a circle only (clear inside 69 % of the radius), for a halo drawn over a round button
        /// whose rect is 1.44x the button.
        /// </summary>
        public static Sprite OuterGlow => Make("outerglow", 128, 128, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64f;
            if (r >= 1) return 0;
            float fade = 1 - Mathf.Clamp01((r - 0.69f) / 0.31f);
            return Mathf.Clamp01((r - 0.68f) * 60) * fade * fade;
        });

        /// <summary>Joystick base: ring with four direction chevrons.</summary>
        public static Sprite JoyBase => MakeColor("joybase", 256, 256, (px, py) =>
        {
            float x = px / 4f, y = py / 4f;   // 64 design units
            float d = RingD(x, y, 32, 32, 30.5f, 1.2f);
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90 * Mathf.Deg2Rad;
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                // Chevron pointing outwards at radius 24.
                var tip = new Vector2(32 + c * 26, 32 + s * 26);
                var l = new Vector2(32 + c * 22 - s * 4, 32 + s * 22 + c * 4);
                var r = new Vector2(32 + c * 22 + s * 4, 32 + s * 22 - c * 4);
                d = U(d, U(Seg(x, y, l.x, l.y, tip.x, tip.y, 1.1f), Seg(x, y, r.x, r.y, tip.x, tip.y, 1.1f)));
            }
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d * 4));
        });

        public static Icon ClassIcon(UA.Core.WeaponClass c)
        {
            switch (c)
            {
                case UA.Core.WeaponClass.Pistol: return Icon.Pistol;
                case UA.Core.WeaponClass.Rifle: return Icon.Rifle;
                case UA.Core.WeaponClass.Shotgun: return Icon.Shotgun;
                case UA.Core.WeaponClass.Sniper: return Icon.Sniper;
                case UA.Core.WeaponClass.MachineGun: return Icon.MachineGun;
                default: return Icon.Special;
            }
        }
    }
}
