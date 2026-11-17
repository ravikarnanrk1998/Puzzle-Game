using System;
using System.Collections.Generic;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// Original franchise crests: a shield in the team colours with a geometric emblem and the
    /// three-letter code. Built from procedural sprites so they stay sharp at any size.
    /// </summary>
    public static class Crest
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Color Primary(TeamDef t) => Pal.Argb(t.Primary);
        public static Color Secondary(TeamDef t) => Pal.Argb(t.Secondary);
        public static Color Accent(TeamDef t) => Pal.Argb(t.Accent);

        /// <summary>Trim colour that reads on the primary (dark secondaries become gold).</summary>
        public static Color Trim(TeamDef t)
        {
            var s = Secondary(t);
            return s.Luma() < 0.15f ? Pal.Gold : s;
        }

        static Sprite Shape(string key, Func<float, float, float> sd)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = Mathf.Clamp01(0.5f - sd(x + 0.5f - 64, y + 0.5f - 64));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            Cache[key] = s;
            return s;
        }

        static float Poly(float x, float y, Vector2[] p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                var a = p[j];
                var b = p[i];
                float px = x - a.x, py = y - a.y, vx = b.x - a.x, vy = b.y - a.y;
                float h = Mathf.Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
                float dx = px - vx * h, dy = py - vy * h;
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dy * dy));
                if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        static float Circ(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static readonly Vector2[] ShieldPts =
        {
            new Vector2(0, 60), new Vector2(50, 50), new Vector2(52, 4), new Vector2(40, -30), new Vector2(0, -62), new Vector2(-40, -30), new Vector2(-52, 4), new Vector2(-50, 50),
        };

        public static Sprite Shield => Shape("shield", (x, y) => Poly(x, y, ShieldPts));
        public static Sprite ShieldInner => Shape("shieldIn", (x, y) => Poly(x, y, ShieldPts) + 7);

        public static Sprite EmblemSprite(Emblem e) => Shape("emb" + e, Emblem(e));

        static Func<float, float, float> Emblem(Emblem e)
        {
            switch (e)
            {
                case Core.Emblem.Crown:
                    return (x, y) => Mathf.Min(Poly(x, y, new[] { new Vector2(-40, -20), new Vector2(-44, 30), new Vector2(-20, 6), new Vector2(0, 40), new Vector2(20, 6), new Vector2(44, 30), new Vector2(40, -20) }),
                        Poly(x, y, new[] { new Vector2(-40, -26), new Vector2(40, -26), new Vector2(40, -38), new Vector2(-40, -38) }));
                case Core.Emblem.Cyclone:
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float spiral = Mathf.Abs(Mathf.Repeat(r / 14f - a / (Mathf.PI * 2) * 1f, 1f) - 0.5f) * 14f - 3.5f;
                        return Mathf.Max(spiral, r - 44);
                    };
                case Core.Emblem.Bolt: return (x, y) => Poly(x, y, new[] { new Vector2(10, 50), new Vector2(-30, -2), new Vector2(-4, -2), new Vector2(-14, -50), new Vector2(30, 8), new Vector2(4, 8) });
                case Core.Emblem.Comet:
                    return (x, y) => Mathf.Min(Circ(x, y, 18, 18, 18), Poly(x, y, new[] { new Vector2(4, 30), new Vector2(-44, -38), new Vector2(30, 4) }));
                case Core.Emblem.Shield:
                    return (x, y) => Mathf.Max(Poly(x, y, new[] { new Vector2(0, 42), new Vector2(34, 32), new Vector2(30, -8), new Vector2(0, -44), new Vector2(-30, -8), new Vector2(-34, 32) }),
                        -Mathf.Min(Poly(x, y, new[] { new Vector2(-4, 30), new Vector2(4, 30), new Vector2(4, -32), new Vector2(-4, -32) }), Poly(x, y, new[] { new Vector2(-26, 8), new Vector2(26, 8), new Vector2(26, 0), new Vector2(-26, 0) })));
                case Core.Emblem.Wing:
                    return (x, y) => Mathf.Min(Mathf.Min(Poly(x, y, new[] { new Vector2(-44, 30), new Vector2(40, 10), new Vector2(-30, 14) }), Poly(x, y, new[] { new Vector2(-40, 10), new Vector2(40, -6), new Vector2(-26, -4) })),
                        Poly(x, y, new[] { new Vector2(-34, -10), new Vector2(38, -22), new Vector2(-20, -24) }));
                case Core.Emblem.Lance:
                    return (x, y) => Mathf.Min(Poly(x, y, new[] { new Vector2(0, 50), new Vector2(14, 20), new Vector2(4, 20), new Vector2(4, -48), new Vector2(-4, -48), new Vector2(-4, 20), new Vector2(-14, 20) }),
                        Poly(x, y, new[] { new Vector2(-24, -8), new Vector2(24, -8), new Vector2(24, -16), new Vector2(-24, -16) }));
                case Core.Emblem.Sun:
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float rays = r - (30 + 14 * Mathf.Clamp(Mathf.Cos(a * 12) * 2, -1, 1));
                        return Mathf.Min(Circ(x, y, 0, 0, 22), Mathf.Max(rays, 22 - r + 4));
                    };
                case Core.Emblem.Trident:
                    return (x, y) => Mathf.Min(Mathf.Min(Poly(x, y, new[] { new Vector2(-4, 30), new Vector2(4, 30), new Vector2(4, -48), new Vector2(-4, -48) }),
                            Poly(x, y, new[] { new Vector2(0, 50), new Vector2(10, 30), new Vector2(-10, 30) })),
                        Mathf.Min(Poly(x, y, new[] { new Vector2(-30, 44), new Vector2(-22, 0), new Vector2(22, 0), new Vector2(30, 44), new Vector2(24, 44), new Vector2(18, 8), new Vector2(-18, 8), new Vector2(-24, 44) }), 99));
                default:
                    return (x, y) => Mathf.Max(Circ(x, y, 0, 0, 40), -Circ(x, y, 16, 10, 34));
            }
        }

        /// <summary>Adds a crest to a UI parent; returns its root.</summary>
        public static RectTransform Add(Transform parent, TeamDef t, float size, bool code = true)
        {
            var root = Ui.Node(parent, "Crest " + t.Id);
            root.sizeDelta = new Vector2(size, size);
            var outer = Ui.Img(root, Shield, Trim(t), "Outer");
            outer.rectTransform.Fill();
            var inner = Ui.Img(root, ShieldInner, Primary(t), "Inner");
            inner.rectTransform.Fill();
            var sheen = Ui.Img(root, ShieldInner, new Color(1, 1, 1, 0.12f), "Sheen");
            sheen.rectTransform.Fill(0, 0, size * 0.5f, size * 0.45f);
            var emb = Ui.Img(root, EmblemSprite(t.Emblem), Trim(t), "Emblem");
            emb.rectTransform.At(0.5f, 0.5f, 0, code ? size * 0.08f : 0, size * (code ? 0.5f : 0.62f), size * (code ? 0.5f : 0.62f));
            if (code && size >= 40)
            {
                var l = Ui.Label(root, t.Id, Mathf.RoundToInt(size * 0.2f), Color.white, TextAnchor.MiddleCenter, Font.Display);
                l.rectTransform.At(0.5f, 0, 0, size * 0.14f, size, size * 0.24f);
                Ui.Shadow(l, 1, 0.5f);
            }
            return root;
        }

        /// <summary>Baked crest texture for 3D use (flags, kits, screens).</summary>
        public static Texture2D Texture(TeamDef t, int size = 128)
        {
            return TextBake.Render(size, size, new Color(0, 0, 0, 0), area =>
            {
                var c = Add(area, t, size, true);
                c.At(0.5f, 0.5f, 0, 0, size, size);
            });
        }
    }
}
