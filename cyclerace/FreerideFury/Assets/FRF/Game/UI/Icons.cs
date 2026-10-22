using System;
using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Procedural white icons (tinted by the Image colour) and control-skin sprites:
    /// glossy button faces, progress rings and fade gradients. Drawn once, cached.
    /// </summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Get(string key, int size, Action<Px> draw, int h = 0)
        {
            if (Cache.TryGetValue(key, out var s)) return s;
            int hh = h > 0 ? h : size;
            var px = new Px(size, hh, new Color32(255, 255, 255, 0));
            draw(px);
            var tex = px.ToTexture("Icon " + key, false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            s = Sprite.Create(tex, new Rect(0, 0, size, hh), new Vector2(0.5f, 0.5f));
            Cache[key] = s;
            return s;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static bool InCircle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;

        /// <summary>Solid steering arrow pointing left (-1) or right (+1).</summary>
        public static Sprite Arrow(int dir) => Get("arrow" + dir, 96, px =>
        {
            float S(float x) => dir < 0 ? x : 1 - x;
            var tri = new[] { V(S(0.16f), 0.5f), V(S(0.66f), 0.15f), V(S(0.66f), 0.85f) };
            var shaft = new[] { V(S(0.6f), 0.38f), V(S(0.86f), 0.38f), V(S(0.86f), 0.62f), V(S(0.6f), 0.62f) };
            px.Fill((x, y) => Px.InPoly(tri, x, y) || Px.InPoly(shaft, x, y), Color.white);
        });

        static Vector2[] Chevron(float y0) => new[]
        {
            V(0.16f, y0), V(0.5f, y0 + 0.28f), V(0.84f, y0), V(0.84f, y0 - 0.15f), V(0.5f, y0 + 0.13f), V(0.16f, y0 - 0.15f),
        };

        /// <summary>Double chevron up.</summary>
        public static Sprite Jump => Get("jump", 96, px =>
        {
            var a = Chevron(0.58f);
            var b = Chevron(0.3f);
            px.Fill((x, y) => Px.InPoly(a, x, y) || Px.InPoly(b, x, y), Color.white);
        });

        /// <summary>Impact starburst.</summary>
        public static Sprite Punch => Get("punch", 96, px =>
        {
            var pts = new Vector2[20];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2 + 0.1f;
                float r = i % 2 == 0 ? 0.46f : 0.24f;
                pts[i] = V(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r);
            }
            px.Fill((x, y) => Px.InPoly(pts, x, y), Color.white);
        });

        static Vector2[] Octagon(float r)
        {
            var p = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2;
                p[i] = V(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r);
            }
            return p;
        }

        /// <summary>Stop-sign octagon with a bar.</summary>
        public static Sprite Brake => Get("brake", 96, px =>
        {
            var outer = Octagon(0.44f);
            var inner = Octagon(0.32f);
            px.Fill((x, y) => (Px.InPoly(outer, x, y) && !Px.InPoly(inner, x, y)) || (x > 0.3f && x < 0.7f && y > 0.43f && y < 0.57f), Color.white);
        });

        /// <summary>Lightning bolt.</summary>
        public static Sprite Bolt => Get("bolt", 96, px =>
        {
            var p = new[] { V(0.62f, 0.95f), V(0.24f, 0.46f), V(0.48f, 0.46f), V(0.36f, 0.05f), V(0.78f, 0.57f), V(0.54f, 0.57f), V(0.7f, 0.95f) };
            px.Fill((x, y) => Px.InPoly(p, x, y), Color.white);
        });

        public static Sprite Pause => Get("pause", 64, px =>
            px.Fill((x, y) => y > 0.2f && y < 0.8f && ((x > 0.26f && x < 0.42f) || (x > 0.58f && x < 0.74f)), Color.white));

        /// <summary>Five-point star (trick mode).</summary>
        public static Sprite Star => Get("star", 96, px =>
        {
            var p = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i / 10f * Mathf.PI * 2;
                float r = i % 2 == 0 ? 0.46f : 0.2f;
                p[i] = V(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
            }
            px.Fill((x, y) => Px.InPoly(p, x, y), Color.white);
        });

        /// <summary>Chequered finish flag (full colour).</summary>
        public static Sprite Flag => Get("flag", 64, px =>
        {
            px.Fill((x, y) => x > 0.14f && x < 0.22f && y > 0.06f && y < 0.94f, new Color(0.9f, 0.9f, 0.92f));
            px.Fill((x, y) => x >= 0.22f && x < 0.9f && y > 0.42f && y < 0.92f, Color.white);
            px.Fill((x, y) =>
            {
                if (x < 0.22f || x >= 0.9f || y <= 0.42f || y >= 0.92f) return false;
                int cx = (int)((x - 0.22f) / 0.17f), cy = (int)((y - 0.42f) / 0.125f);
                return (cx + cy) % 2 == 0;
            }, new Color(0.08f, 0.08f, 0.1f));
        });

        /// <summary>Round button face: vertical shading, top gloss and a darker rim.</summary>
        public static Sprite ButtonFace => Get("face", 128, px =>
        {
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = (x + 0.5f) / 64 - 1, dy = (y + 0.5f) / 64 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1 - d) * 64);
                if (a <= 0) continue;
                float shade = Mathf.Lerp(0.72f, 1f, (dy + 1) * 0.5f);
                shade *= Mathf.Lerp(1, 0.78f, Tex.SmoothStep(0.8f, 1f, d));
                // Soft gloss highlight on the top half.
                float gx = dx / 0.7f, gy = (dy - 0.42f) / 0.42f;
                float gloss = Mathf.Clamp01(1 - (gx * gx + gy * gy)) * 0.28f;
                float v = Mathf.Clamp01(shade + gloss);
                px.P[y * 128 + x] = new Color(v, v, v, a);
            }
        });

        /// <summary>Ring with the given inner radius (fraction of the outer), for meters and rims.</summary>
        public static Sprite Ring(float inner) => Get("ring" + Mathf.RoundToInt(inner * 100), 128, px =>
        {
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = (x + 0.5f) / 64 - 1, dy = (y + 0.5f) / 64 - 1;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1 - d) * 64) * Mathf.Clamp01((d - inner) * 64);
                px.P[y * 128 + x] = new Color(1, 1, 1, a);
            }
        });

        /// <summary>Horizontal fade: opaque on the left to clear on the right.</summary>
        public static Sprite FadeRight => Get("fadeR", 64, px =>
        {
            for (int x = 0; x < 64; x++)
            {
                float a = 1 - Tex.SmoothStep(0, 1, x / 63f);
                for (int y = 0; y < 4; y++) px.P[y * 64 + x] = new Color(1, 1, 1, a);
            }
        }, 4);

        /// <summary>Vertical fade: opaque at the bottom to clear at the top.</summary>
        public static Sprite FadeUp => Get("fadeU", 4, px =>
        {
            for (int y = 0; y < 64; y++)
            {
                float a = 1 - Tex.SmoothStep(0, 1, y / 63f);
                for (int x = 0; x < 4; x++) px.P[y * 4 + x] = new Color(1, 1, 1, a);
            }
        }, 64);
    }
}
