using System;
using System.Collections.Generic;
using UnityEngine;

namespace UA.Game
{
    /// <summary>A PBR surface: albedo (alpha = smoothness), normal map and optional metallic/gloss mask.</summary>
    public sealed class Surface
    {
        public Texture2D Albedo, Normal, Mask, Emission;
        public float Smooth = 0.5f, Metal, Bump = 1, Tiling = 0.5f;
        public Color Tint = Color.white;
        public bool WetAlpha;   // albedo alpha carries smoothness
    }

    /// <summary>Procedural textures for the world, decals, effects and skies (generated once, cached).</summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Surface> Surfaces = new Dictionary<string, Surface>();

        public static int Size(int medium)
        {
            switch (Quality.Current)
            {
                case Gfx.Low: return Mathf.Max(64, medium / 2);
                case Gfx.Ultra: return Mathf.Min(1024, medium * 2);
                default: return medium;
            }
        }

        public static void ClearAll()
        {
            foreach (var t in Cache.Values) if (t != null) UnityEngine.Object.Destroy(t);
            Cache.Clear();
            Surfaces.Clear();
        }

        /// <summary>Frees the per-stage PBR surface textures (FX sprites, skies and camos stay cached).</summary>
        public static void ClearSurfaces()
        {
            foreach (var s in Surfaces.Values)
            {
                if (s == null) continue;
                if (s.Albedo != null) UnityEngine.Object.Destroy(s.Albedo);
                if (s.Normal != null) UnityEngine.Object.Destroy(s.Normal);
                if (s.Mask != null) UnityEngine.Object.Destroy(s.Mask);
                if (s.Emission != null) UnityEngine.Object.Destroy(s.Emission);
            }
            Surfaces.Clear();
        }

        static Texture2D Get(string key, Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = key;
            Cache[key] = t;
            return t;
        }

        static Surface Surf(string key, Func<Surface> make)
        {
            if (Surfaces.TryGetValue(key, out var s) && s != null && s.Albedo != null) return s;
            s = make();
            Surfaces[key] = s;
            return s;
        }

        static Texture2D Flat(string key, Color c) => Get(key, () =>
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels32(px);
            t.Apply();
            return t;
        });

        public static Texture2D White => Flat("white", Color.white);
        public static Texture2D Black => Flat("black", Color.black);
        public static Texture2D FlatNormal => Get("flatnormal", () =>
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(Application.isMobilePlatform ? (byte)128 : (byte)255, 128, 255, 128);
            t.SetPixels32(px);
            t.Apply();
            return t;
        });

        static Color Lerp3(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);

        // ===================================================================== floors

        /// <summary>Cast concrete; wet puddles raise smoothness (stored in alpha).</summary>
        public static Surface Concrete(int seed, Color tint, float wet, bool cracks = true) => Surf("concrete" + seed + tint + wet, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float base_ = Noise.Fbm(u, v, 4, 5, seed);
                float fine = Noise.Fbm(u, v, 32, 3, seed + 5);
                float stain = Noise.Fbm(u, v, 3, 4, seed + 9);
                float crack = cracks ? Noise.Smooth(0.86f, 0.97f, Noise.Ridged(u, v, 3, 4, seed + 13)) : 0;
                float puddle = Noise.Smooth(0.58f, 0.66f, Noise.Fbm(u, v, 2, 4, seed + 21)) * wet;
                float pits = Noise.Hash(x, y, seed) > 0.985f ? 0.5f : 0;
                h[y * n + x] = base_ * 0.4f + fine * 0.3f - crack * 0.6f - pits * 0.3f - puddle * 0.2f;
                float l = 0.85f + (base_ - 0.5f) * 0.35f + (fine - 0.5f) * 0.18f - Noise.Smooth(0.55f, 0.85f, stain) * 0.35f - crack * 0.45f - pits * 0.3f;
                l *= 1 - puddle * 0.35f;
                var c = tint * l;
                c.a = Mathf.Lerp(0.18f + fine * 0.08f, 0.92f, puddle);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("concrete"), Normal = Pix.NormalMap("concrete_n", h, n, n, 6f), Tiling = 0.25f, WetAlpha = true, Smooth = 1, Bump = 1 };
        });

        public static Surface Asphalt(int seed, float wet) => Surf("asphalt" + seed + wet, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float agg = Noise.Hash(x, y, seed);
                float base_ = Noise.Fbm(u, v, 6, 4, seed);
                float patch = Noise.Smooth(0.6f, 0.62f, Noise.Fbm(u, v, 2, 3, seed + 3));
                float crack = Noise.Smooth(0.88f, 0.98f, Noise.Ridged(u, v, 2, 5, seed + 7));
                float puddle = Noise.Smooth(0.56f, 0.64f, Noise.Fbm(u, v, 2, 4, seed + 11)) * wet;
                h[y * n + x] = agg * 0.25f + base_ * 0.3f - crack * 0.7f - puddle * 0.25f;
                float l = 0.16f + agg * 0.07f + (base_ - 0.5f) * 0.06f - patch * 0.04f - crack * 0.08f;
                var c = new Color(l * 1.02f, l, l * 0.98f);
                c *= 1 - puddle * 0.4f;
                c.a = Mathf.Lerp(0.22f + agg * 0.1f, 0.95f, puddle);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("asphalt"), Normal = Pix.NormalMap("asphalt_n", h, n, n, 5f), Tiling = 0.2f, WetAlpha = true, Smooth = 1 };
        });

        /// <summary>Square floor tiles with grout, grime, blood smears and wet patches.</summary>
        public static Surface FloorTiles(int seed, Color a, Color b, int tiles, float wet, float blood, bool checker = false) => Surf("ftiles" + seed + a + b + tiles + wet + blood + checker, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float tu = u * tiles, tv = v * tiles;
                int ti = (int)tu, tj = (int)tv;
                float fu = tu - ti, fv = tv - tj;
                float g = Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv));
                float grout = 1 - Noise.Smooth(0.015f, 0.04f, g);
                float tileVar = Noise.Hash(ti, tj, seed);
                var c = checker && ((ti + tj) & 1) == 1 ? b : Color.Lerp(a, b, tileVar * 0.5f);
                c *= 0.9f + tileVar * 0.15f;
                float dirt = Noise.Fbm(u, v, 4, 5, seed + 3);
                float grime = Noise.Smooth(0.45f, 0.8f, dirt);
                c = Color.Lerp(c, new Color(0.18f, 0.16f, 0.13f), grime * 0.4f);
                c = Color.Lerp(c, new Color(0.12f, 0.11f, 0.1f), grout * 0.85f);
                float bl = Noise.Smooth(0.7f, 0.75f, Noise.Fbm(u * 1.0f, v, 3, 4, seed + 17)) * blood;
                c = Color.Lerp(c, new Color(0.22f, 0.02f, 0.02f), bl * 0.85f);
                float crack = Noise.Smooth(0.9f, 0.97f, Noise.Ridged(u, v, 4, 3, seed + 23)) * (1 - grout);
                c *= 1 - crack * 0.4f;
                float puddle = Noise.Smooth(0.57f, 0.64f, Noise.Fbm(u, v, 2, 4, seed + 29)) * wet;
                h[y * n + x] = -grout * 0.8f - crack * 0.3f + Noise.Fbm(u, v, 24, 2, seed + 31) * 0.1f;
                c *= 1 - puddle * 0.3f;
                // Wet floors keep a glossy film everywhere (reflects the fixtures), not only in puddles.
                float film = Mathf.Lerp(0.55f, 0.8f, Mathf.Clamp01(wet));
                c.a = Mathf.Lerp(Mathf.Lerp(film, 0.2f + wet * 0.3f, grime) * (1 - grout * 0.6f), 0.93f, puddle);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("ftiles"), Normal = Pix.NormalMap("ftiles_n", h, n, n, 4f), Tiling = 1f / 2.4f, WetAlpha = true, Smooth = 1 };
        });

        /// <summary>Railway ballast: packed angular stones with dark gaps, rust staining and wet patches.</summary>
        public static Surface Ballast(int seed, float wet) => Surf("ballast" + seed + wet, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                Noise.Cells(u, v, 34, seed, out float f1, out float f2, out int id);
                float edge = f2 - f1;
                float stone = Noise.Smooth(0.02f, 0.2f, edge);
                float tone = Noise.Hash(id, 7, seed + 3);
                float speck = Noise.Fbm(u, v, 64, 2, seed + 5);
                var c = Color.Lerp(new Color(0.3f, 0.3f, 0.29f), new Color(0.62f, 0.6f, 0.57f), tone) * (0.82f + speck * 0.36f);
                c = Color.Lerp(new Color(0.05f, 0.045f, 0.04f), c, stone);
                float rust = Noise.Smooth(0.55f, 0.82f, Noise.Fbm(u, v, 3, 3, seed + 7));
                c = Color.Lerp(c, new Color(0.32f, 0.2f, 0.12f), rust * 0.35f);
                h[y * n + x] = stone * (0.45f + Mathf.Sqrt(Mathf.Clamp01(edge * 2.5f)) * 0.55f) + speck * 0.05f;
                float puddle = Noise.Smooth(0.58f, 0.66f, Noise.Fbm(u, v, 2, 4, seed + 11)) * wet;
                c *= 1 - puddle * 0.3f;
                c.a = Mathf.Lerp(0.22f + stone * 0.18f + wet * 0.25f, 0.9f, puddle);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("ballast"), Normal = Pix.NormalMap("ballast_n", h, n, n, 8f), Tiling = 1f / 3f, WetAlpha = true, Smooth = 1 };
        });

        public static Surface Linoleum(int seed, Color col) => Surf("lino" + seed + col, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float sp = Noise.Hash(x, y, seed);
                float m = Noise.Fbm(u, v, 8, 4, seed);
                float dirt = Noise.Smooth(0.5f, 0.85f, Noise.Fbm(u, v, 3, 4, seed + 4));
                float tile = Mathf.Min(Mathf.Repeat(u * 4, 1), Mathf.Repeat(v * 4, 1));
                float seam = 1 - Noise.Smooth(0.0f, 0.012f, tile);
                var c = col * (0.88f + m * 0.2f + (sp > 0.92f ? 0.08f : 0) - (sp < 0.06f ? 0.08f : 0));
                c = Color.Lerp(c, new Color(0.2f, 0.18f, 0.14f), dirt * 0.5f + seam * 0.5f);
                float bl = Noise.Smooth(0.72f, 0.76f, Noise.Fbm(u, v, 3, 4, seed + 9));
                c = Color.Lerp(c, new Color(0.25f, 0.03f, 0.03f), bl * 0.8f);
                h[y * n + x] = -seam * 0.5f + sp * 0.03f;
                c.a = 0.62f - dirt * 0.35f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("lino"), Normal = Pix.NormalMap("lino_n", h, n, n, 3f), Tiling = 1f / 4f, WetAlpha = true, Smooth = 1 };
        });

        public static Surface Marble(int seed, Color col) => Surf("marble" + seed + col, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float vein = Noise.Ridged(u + Noise.Fbm(u, v, 3, 3, seed) * 0.3f, v, 2, 5, seed + 3);
                float tu = u * 2, tv = v * 2;
                float fu = tu - Mathf.Floor(tu), fv = tv - Mathf.Floor(tv);
                float seam = 1 - Noise.Smooth(0.0f, 0.008f, Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv)));
                float dirt = Noise.Smooth(0.55f, 0.85f, Noise.Fbm(u, v, 3, 4, seed + 8));
                var c = col * (0.95f - Noise.Smooth(0.75f, 0.95f, vein) * 0.35f);
                c = Color.Lerp(c, new Color(0.25f, 0.22f, 0.18f), dirt * 0.45f + seam * 0.6f);
                h[y * n + x] = -seam;
                c.a = 0.88f - dirt * 0.4f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("marble"), Normal = Pix.NormalMap("marble_n", h, n, n, 3f), Tiling = 1f / 3f, WetAlpha = true, Smooth = 1 };
        });

        public static Surface Grating(int seed) => Surf("grating" + seed, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float gu = Mathf.Repeat(u * 8, 1), gv = Mathf.Repeat(v * 16, 1);
                float bar = Mathf.Max(1 - Noise.Smooth(0.08f, 0.14f, Mathf.Min(gu, 1 - gu)), 1 - Noise.Smooth(0.1f, 0.18f, Mathf.Min(gv, 1 - gv)));
                float rust = Noise.Smooth(0.55f, 0.8f, Noise.Fbm(u, v, 4, 4, seed));
                var metal = Color.Lerp(new Color(0.32f, 0.33f, 0.34f), new Color(0.35f, 0.18f, 0.09f), rust);
                var c = Color.Lerp(new Color(0.03f, 0.03f, 0.03f), metal, bar);
                h[y * n + x] = bar;
                c.a = bar * (0.55f - rust * 0.35f);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("grating"), Normal = Pix.NormalMap("grating_n", h, n, n, 8f), Tiling = 1f / 1.5f, WetAlpha = true, Smooth = 1, Metal = 0.6f };
        });

        /// <summary>Outdoor ground: 0 dirt, 1 sand, 2 snow, 3 dead grass, 4 gravel.</summary>
        public static Surface Ground(int kind, int seed) => Surf("ground" + kind + "_" + seed, () =>
        {
            int n = Size(512);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float f = Noise.Fbm(u, v, 6, 5, seed);
                float big = Noise.Fbm(u, v, 2, 4, seed + 3);
                float pebble = Noise.Hash(x / 3, y / 3, seed) > 0.93f ? 1 : 0;
                Color c;
                float hh;
                float sm;
                switch (kind)
                {
                    case 1:
                    {
                        float rip = Mathf.Sin((v * 40 + Noise.Fbm(u, v, 3, 3, seed) * 8) * Mathf.PI * 2) * 0.5f + 0.5f;
                        c = Color.Lerp(new Color(0.62f, 0.5f, 0.34f), new Color(0.78f, 0.66f, 0.46f), f * 0.7f + rip * 0.3f);
                        hh = rip * 0.4f + f * 0.3f;
                        sm = 0.12f;
                        break;
                    }
                    case 2:
                        c = Color.Lerp(new Color(0.72f, 0.78f, 0.86f), new Color(0.93f, 0.95f, 0.98f), f * 0.8f + big * 0.2f);
                        c = Color.Lerp(c, new Color(0.45f, 0.48f, 0.52f), Noise.Smooth(0.68f, 0.8f, big) * 0.5f);
                        hh = f * 0.6f;
                        sm = 0.35f + (Noise.Hash(x, y, seed + 1) > 0.97f ? 0.5f : 0);
                        break;
                    case 3:
                    {
                        float blade = Noise.Hash(x, y / 2, seed + 4);
                        c = Color.Lerp(new Color(0.24f, 0.22f, 0.12f), new Color(0.42f, 0.38f, 0.2f), f * 0.6f + blade * 0.4f);
                        c = Color.Lerp(c, new Color(0.2f, 0.15f, 0.1f), Noise.Smooth(0.55f, 0.75f, big) * 0.7f);
                        hh = blade * 0.5f + f * 0.3f;
                        sm = 0.08f;
                        break;
                    }
                    case 4:
                        c = Color.Lerp(new Color(0.28f, 0.27f, 0.25f), new Color(0.5f, 0.48f, 0.45f), Noise.Hash(x / 2, y / 2, seed + 5));
                        hh = Noise.Hash(x / 2, y / 2, seed + 5) * 0.6f;
                        sm = 0.2f;
                        break;
                    default:
                        c = Color.Lerp(new Color(0.2f, 0.16f, 0.11f), new Color(0.38f, 0.31f, 0.22f), f);
                        c = Color.Lerp(c, new Color(0.12f, 0.1f, 0.08f), Noise.Smooth(0.6f, 0.75f, big) * 0.6f);
                        hh = f * 0.5f + pebble * 0.4f;
                        sm = 0.1f + Noise.Smooth(0.62f, 0.7f, big) * 0.5f;
                        break;
                }
                if (pebble > 0 && kind != 2) c = Color.Lerp(c, new Color(0.45f, 0.43f, 0.4f), 0.6f);
                h[y * n + x] = hh;
                c.a = sm;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("ground"), Normal = Pix.NormalMap("ground_n", h, n, n, kind == 2 ? 2f : 5f), Tiling = 0.2f, WetAlpha = true, Smooth = 1 };
        });

        // ===================================================================== walls

        /// <summary>
        /// Full-height wall texture (v = 0 floor .. 1 ceiling): tiles or plaster with a colour band,
        /// grime rising from the floor, water streaks and blood. style 0 subway tile, 1 plaster, 2 concrete, 3 panels, 4 brick.
        /// </summary>
        public static Surface Wall(int style, Color baseCol, Color band, int seed, float blood, float grime = 1f) => Surf("wall" + style + baseCol + band + seed + blood + grime, () =>
        {
            int w = Size(512), hh = Size(512);
            var h = new float[w * hh];
            var p = new Pix(w, hh).Fill((u, v, x, y) =>
            {
                float grout = 0, tileVar = 0;
                Color c = baseCol;
                float smooth = 0.3f;
                switch (style)
                {
                    case 0:
                    {
                        // Glazed subway tile, about 40 x 20 cm in a running bond, bevelled edges.
                        float tv = v * 22, row = Mathf.Floor(tv);
                        float tu = u * 10 + (row % 2) * 0.5f;
                        float fu = tu - Mathf.Floor(tu), fv = tv - row;
                        float g = Mathf.Min(Mathf.Min(fu, 1 - fu) * 0.5f, Mathf.Min(fv, 1 - fv));
                        grout = 1 - Noise.Smooth(0.025f, 0.06f, g);
                        tileVar = Noise.Hash((int)Mathf.Floor(tu), (int)row, seed);
                        float bevel = Noise.Smooth(0.0f, 0.14f, g);
                        // Slight waviness of the glaze catches highlights like real ceramic.
                        h[y * w + x] = bevel * 0.6f + Noise.Fbm(u, v, 20, 2, seed + 41) * 0.04f;
                        smooth = 0.86f;
                        break;
                    }
                    case 3:
                    {
                        float fu = Mathf.Repeat(u * 2, 1), fv = Mathf.Repeat(v * 3, 1);
                        float g = Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv));
                        grout = 1 - Noise.Smooth(0.005f, 0.02f, g);
                        tileVar = Noise.Hash((int)(u * 2), (int)(v * 3), seed);
                        h[y * w + x] = -grout;
                        smooth = 0.5f;
                        break;
                    }
                    case 4:
                    {
                        float tv = v * 48, row = Mathf.Floor(tv);
                        float tu = u * 12 + (row % 2) * 0.5f;
                        float fu = tu - Mathf.Floor(tu), fv = tv - row;
                        float g = Mathf.Min(Mathf.Min(fu, 1 - fu) * 0.5f, Mathf.Min(fv, 1 - fv));
                        grout = 1 - Noise.Smooth(0.04f, 0.1f, g);
                        tileVar = Noise.Hash((int)Mathf.Floor(tu), (int)row, seed);
                        h[y * w + x] = -grout * 0.8f + Noise.Fbm(u, v, 32, 2, seed) * 0.2f;
                        smooth = 0.15f;
                        break;
                    }
                    default:
                        h[y * w + x] = Noise.Fbm(u, v, 16, 3, seed) * 0.4f;
                        smooth = style == 1 ? 0.25f : 0.2f;
                        break;
                }
                c *= 0.9f + tileVar * 0.12f;
                if (style == 2) c *= 0.8f + Noise.Fbm(u, v, 6, 4, seed) * 0.4f;
                // Colour band at about 1.1 m - 1.5 m (v 0.25 - 0.33 of a 4.5 m wall).
                float bandMask = Noise.Smooth(0.24f, 0.245f, v) * (1 - Noise.Smooth(0.33f, 0.335f, v));
                c = Color.Lerp(c, band, bandMask * band.a);
                // Grime rising from the floor, and patchy dirt everywhere.
                float dirt = Noise.Fbm(u, v, 4, 5, seed + 7);
                float rise = Noise.Smooth(0.35f, 0.0f, v + (dirt - 0.5f) * 0.25f) * grime;
                c = Color.Lerp(c, new Color(0.14f, 0.12f, 0.09f), rise * 0.75f + Noise.Smooth(0.6f, 0.9f, dirt) * 0.35f * grime);
                // Water streaks running down.
                float streak = Noise.Smooth(0.6f, 0.9f, Noise.Fbm(u * 1f, v * 0.05f, 24, 2, seed + 11)) * Noise.Smooth(0.2f, 1f, v);
                c = Color.Lerp(c, new Color(0.2f, 0.18f, 0.14f), streak * 0.4f * grime);
                // Blood smears (hand prints / sprays) in the lower half.
                float bl = Noise.Smooth(0.74f, 0.78f, Noise.Fbm(u, v * 0.6f, 3, 5, seed + 13)) * Noise.Smooth(0.8f, 0.2f, v) * blood;
                c = Color.Lerp(c, new Color(0.26f, 0.02f, 0.02f), bl * 0.9f);
                c = Color.Lerp(c, new Color(0.1f, 0.09f, 0.08f), grout * 0.8f);
                c.a = Mathf.Clamp01(smooth * (1 - rise * 0.7f) * (1 - grout * 0.6f) + bl * 0.3f);
                return c;
            });
            return new Surface { Albedo = p.ToTexture("wall"), Normal = Pix.NormalMap("wall_n", h, w, hh, style == 0 ? 6f : 4f), Tiling = 1f / 4f, WetAlpha = true, Smooth = 1 };
        });

        /// <summary>Building facade: style 0 ground floor shopfronts, 1 upper floor windows. Emission = lit windows.</summary>
        public static Surface Facade(int style, Color wall, int seed) => Surf("facade" + style + wall + seed, () =>
        {
            int w = Size(512), hh = Size(256);
            var h = new float[w * hh];
            var emi = new Pix(w, hh);
            var p = new Pix(w, hh).Fill((u, v, x, y) =>
            {
                Color c = wall * (0.85f + Noise.Fbm(u, v, 8, 4, seed) * 0.3f);
                float hgt = Noise.Fbm(u, v, 24, 2, seed) * 0.2f;
                float sm = 0.15f;
                if (style == 1)
                {
                    // Two windows per 6 m bay.
                    float bu = Mathf.Repeat(u * 2, 1), bv = v;
                    bool win = bu > 0.22f && bu < 0.78f && bv > 0.28f && bv < 0.82f;
                    bool sill = bu > 0.18f && bu < 0.82f && bv > 0.24f && bv < 0.28f;
                    int wi = (int)(u * 2) + seed * 3;
                    float r = Noise.Hash(wi, 7, seed);
                    if (win)
                    {
                        bool lit = r > 0.86f;
                        bool broken = r < 0.25f;
                        bool boarded = r >= 0.25f && r < 0.4f;
                        float mull = Mathf.Abs(bu - 0.5f) < 0.012f || Mathf.Abs(bv - 0.55f) < 0.012f ? 1 : 0;
                        if (boarded)
                        {
                            float plank = Mathf.Repeat(bv * 9, 1);
                            c = new Color(0.32f, 0.24f, 0.15f) * (0.7f + Noise.Hash((int)(bv * 9), wi, 3) * 0.4f) * (plank < 0.08f ? 0.4f : 1);
                            sm = 0.1f;
                        }
                        else
                        {
                            c = lit ? new Color(0.9f, 0.62f, 0.3f) : new Color(0.03f, 0.035f, 0.045f);
                            if (broken) c = Color.Lerp(c, new Color(0.01f, 0.01f, 0.01f), Noise.Smooth(0.4f, 0.6f, Noise.Fbm(u, v, 12, 3, wi)));
                            if (lit) emi.C[y * w + x] = new Color(1f, 0.6f, 0.25f) * (0.6f + Noise.Fbm(u, v, 4, 2, wi) * 0.6f) * (1 - mull);
                            c = Color.Lerp(c, new Color(0.25f, 0.25f, 0.25f), mull);
                            sm = 0.92f;
                        }
                        hgt = -0.5f;
                    }
                    if (sill) { c = wall * 1.25f; hgt = 0.4f; }
                }
                else
                {
                    // Ground floor: shop window or rolling shutter per 6 m bay, plus a sign band.
                    float bu = Mathf.Repeat(u, 1);
                    int bay = (int)u + seed;
                    float r = Noise.Hash(bay, 3, seed);
                    bool opening = bu > 0.08f && bu < 0.92f && v < 0.72f;
                    bool signBand = v > 0.76f && v < 0.92f && bu > 0.04f && bu < 0.96f;
                    if (opening)
                    {
                        if (r < 0.5f)
                        {
                            float slat = Mathf.Repeat(v * 40, 1);
                            c = new Color(0.4f, 0.41f, 0.42f) * (0.8f + slat * 0.25f);
                            c = Color.Lerp(c, new Color(0.35f, 0.18f, 0.08f), Noise.Smooth(0.55f, 0.8f, Noise.Fbm(u, v, 6, 4, bay)) * 0.6f);
                            hgt = slat * 0.3f;
                            sm = 0.35f;
                        }
                        else
                        {
                            c = new Color(0.02f, 0.025f, 0.03f);
                            if (r > 0.85f) emi.C[y * w + x] = new Color(0.25f, 0.5f, 0.6f) * 0.6f * Noise.Smooth(0.0f, 0.6f, v);
                            sm = 0.9f;
                            hgt = -0.6f;
                        }
                    }
                    if (signBand)
                    {
                        var sc = Color.HSVToRGB(Noise.Hash(bay, 9, seed), 0.6f, 0.45f);
                        c = sc;
                        sm = 0.4f;
                    }
                }
                float rise = Noise.Smooth(0.25f, 0.0f, v) * (style == 0 ? 1 : 0);
                c = Color.Lerp(c, new Color(0.12f, 0.1f, 0.08f), rise * 0.6f + Noise.Smooth(0.65f, 0.9f, Noise.Fbm(u, v, 4, 4, seed + 5)) * 0.3f);
                h[y * w + x] = hgt;
                c.a = sm;
                return c;
            });
            // Graffiti on the ground floor.
            if (style == 0)
            {
                var r = new System.Random(seed);
                for (int g = 0; g < 3; g++) Graffiti(p, r, (int)(r.NextDouble() * w), (int)(hh * (0.15 + r.NextDouble() * 0.35)), (int)(hh * 0.18));
            }
            return new Surface { Albedo = p.ToTexture("facade"), Normal = Pix.NormalMap("facade_n", h, w, hh, 5f), Emission = emi.ToTexture("facade_e"), Tiling = 1f / 12f, WetAlpha = true, Smooth = 1 };
        });

        static void Graffiti(Pix p, System.Random r, int x, int y, int size)
        {
            var col = Color.HSVToRGB((float)r.NextDouble(), 0.75f, 0.85f);
            float ang = (float)(r.NextDouble() * 6.28);
            float px = x, py = y;
            int strokes = 30 + r.Next(40);
            for (int i = 0; i < strokes; i++)
            {
                ang += (float)(r.NextDouble() - 0.5) * 1.4f;
                px += Mathf.Cos(ang) * size * 0.08f;
                py += Mathf.Sin(ang) * size * 0.05f;
                p.Blot(px, py, size * 0.06f, col.A(0.9f), 0.7f);
                if (r.NextDouble() < 0.06)
                    for (int d = 0; d < 8; d++) p.Blot(px, py - d * size * 0.03f, size * 0.02f, col.A(0.7f), 0.8f);
            }
        }

        public static Surface CeilingPanels(int seed) => Surf("ceiling" + seed, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float fu = Mathf.Repeat(u * 2, 1), fv = Mathf.Repeat(v * 2, 1);
                float g = Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv));
                float grid = 1 - Noise.Smooth(0.01f, 0.03f, g);
                int id = (int)(u * 2) + (int)(v * 2) * 7;
                bool missing = Noise.Hash(id, 1, seed) > 0.85f;
                float stain = Noise.Smooth(0.5f, 0.8f, Noise.Fbm(u, v, 4, 4, seed + id));
                var c = new Color(0.55f, 0.54f, 0.5f) * (0.9f + Noise.Hash(x, y, seed) * 0.1f);
                c = Color.Lerp(c, new Color(0.35f, 0.28f, 0.18f), stain * 0.6f);
                if (missing) c = new Color(0.03f, 0.03f, 0.03f);
                c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.3f), grid);
                h[y * n + x] = missing ? -1 : -grid * 0.5f;
                c.a = 0.15f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("ceiling"), Normal = Pix.NormalMap("ceiling_n", h, n, n, 3f), Tiling = 1f / 1.2f, WetAlpha = true, Smooth = 1 };
        });

        // ===================================================================== props

        /// <summary>Painted metal with scratches and rust; Mask holds metallic (R) and smoothness (A).</summary>
        public static Surface PaintedMetal(Color paint, int seed, float rust, float stripes = 0) => Surf("pmetal" + paint + seed + rust + stripes, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var mask = new Pix(n, n);
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float r = Noise.Smooth(0.62f - rust * 0.25f, 0.78f - rust * 0.2f, Noise.Fbm(u, v, 4, 5, seed));
                float scratch = Noise.Smooth(0.94f, 0.99f, Noise.Ridged(u * 3f, v * 0.5f, 6, 2, seed + 5));
                var c = paint * (0.88f + Noise.Fbm(u, v, 16, 3, seed + 2) * 0.2f);
                if (stripes > 0)
                {
                    float s = Mathf.Repeat((u + v) * stripes, 1);
                    c = s < 0.5f ? new Color(0.85f, 0.65f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                }
                c = Color.Lerp(c, new Color(0.62f, 0.62f, 0.6f), scratch * 0.7f);
                c = Color.Lerp(c, new Color(0.33f, 0.15f, 0.06f) * (0.7f + Noise.Hash(x, y, seed) * 0.5f), r);
                h[y * n + x] = -r * 0.4f - scratch * 0.2f + Noise.Fbm(u, v, 32, 2, seed + 9) * 0.1f;
                float metal = Mathf.Max(scratch, 0) * (1 - r);
                mask.C[y * n + x] = new Color(metal, 0, 0, Mathf.Lerp(0.45f, 0.12f, r) + scratch * 0.2f);
                c.a = 1;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("pmetal"), Normal = Pix.NormalMap("pmetal_n", h, n, n, 3f), Mask = mask.ToTexture("pmetal_m", true), Tiling = 0.5f, Smooth = 1, Metal = 1 };
        });

        public static Surface Corrugated(Color paint, int seed) => Surf("corr" + paint + seed, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var mask = new Pix(n, n);
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float rib = Mathf.Sin(u * Mathf.PI * 2 * 16) * 0.5f + 0.5f;
                float r = Noise.Smooth(0.6f, 0.8f, Noise.Fbm(u, v, 3, 5, seed));
                float streak = Noise.Smooth(0.55f, 0.85f, Noise.Fbm(u * 1f, v * 0.08f, 32, 2, seed + 4));
                var c = paint * (0.75f + rib * 0.3f);
                c = Color.Lerp(c, new Color(0.3f, 0.14f, 0.06f), Mathf.Max(r, streak * 0.6f));
                h[y * n + x] = rib;
                mask.C[y * n + x] = new Color(0.1f, 0, 0, 0.35f - r * 0.2f);
                c.a = 1;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("corr"), Normal = Pix.NormalMap("corr_n", h, n, n, 5f), Mask = mask.ToTexture("corr_m", true), Tiling = 0.25f, Smooth = 1, Metal = 1 };
        });

        public static Surface Wood(int seed, Color col) => Surf("wood" + seed + col, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float plank = Mathf.Floor(u * 4);
                float fu = u * 4 - plank;
                float grain = Mathf.Sin((v * 30 + Noise.Fbm(u, v, 4, 3, seed + (int)plank) * 6) * Mathf.PI * 2) * 0.5f + 0.5f;
                float gap = 1 - Noise.Smooth(0.0f, 0.04f, Mathf.Min(fu, 1 - fu));
                var c = col * (0.75f + grain * 0.2f + Noise.Hash((int)plank, 0, seed) * 0.15f);
                c = Color.Lerp(c, new Color(0.05f, 0.04f, 0.03f), gap);
                c = Color.Lerp(c, new Color(0.15f, 0.12f, 0.1f), Noise.Smooth(0.6f, 0.85f, Noise.Fbm(u, v, 3, 4, seed + 9)) * 0.5f);
                h[y * n + x] = grain * 0.3f - gap;
                c.a = 0.2f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("wood"), Normal = Pix.NormalMap("wood_n", h, n, n, 4f), Tiling = 0.8f, WetAlpha = true, Smooth = 1 };
        });

        public static Surface Fabric(Color col, int seed, float dirt = 0.4f) => Surf("fabric" + col + seed + dirt, () =>
        {
            int n = Size(256);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float weave = (Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f)) * 0.5f + 0.5f;
                float d = Noise.Smooth(0.5f, 0.9f, Noise.Fbm(u, v, 4, 4, seed));
                var c = col * (0.85f + weave * 0.15f);
                c = Color.Lerp(c, new Color(0.18f, 0.15f, 0.1f), d * dirt);
                h[y * n + x] = weave * 0.5f;
                c.a = 0.1f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("fabric"), Normal = Pix.NormalMap("fabric_n", h, n, n, 2f), Tiling = 1.5f, WetAlpha = true, Smooth = 1 };
        });

        public static Surface Rubber => Surf("rubber", () =>
        {
            int n = Size(128);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float tread = Mathf.Repeat(v * 12 + Mathf.Abs(u - 0.5f) * 3, 1) < 0.5f ? 1 : 0;
                h[y * n + x] = tread;
                var c = new Color(0.06f, 0.06f, 0.06f) * (0.9f + Noise.Hash(x, y, 1) * 0.2f);
                c.a = 0.25f;
                return c;
            });
            return new Surface { Albedo = p.ToTexture("rubber"), Normal = Pix.NormalMap("rubber_n", h, n, n, 3f), Tiling = 1, WetAlpha = true, Smooth = 1 };
        });

        // ===================================================================== signs & posters

        /// <summary>Glowing advert / poster (lightbox). Returns albedo; emission uses the same texture.</summary>
        public static Texture2D Poster(int seed) => Get("poster" + seed, () =>
        {
            int w = 256, hh = 384;
            var r = new System.Random(seed * 97 + 13);
            var top = Color.HSVToRGB((float)r.NextDouble(), 0.7f, 0.85f);
            var bot = Color.HSVToRGB((float)r.NextDouble(), 0.8f, 0.5f);
            var p = new Pix(w, hh).Fill((u, v, x, y) => Color.Lerp(bot, top, v + Noise.Fbm(u, v, 3, 3, seed) * 0.2f));
            // Big shapes.
            int shapes = 2 + r.Next(3);
            for (int i = 0; i < shapes; i++)
            {
                var sc = Color.HSVToRGB((float)r.NextDouble(), 0.5f, 1f);
                p.Blot((float)r.NextDouble() * w, hh * (0.3f + (float)r.NextDouble() * 0.5f), 30 + (float)r.NextDouble() * 70, sc.A(0.9f), 0.92f, false);
            }
            string[] words = { "VARDEN", "FRESH", "COLA", "METRO", "SALE", "ROCK", "NOVA", "ZEST", "PULSE", "LIVE", "CITY", "50%", "NEW", "BURGER", "RADIO" };
            string a = words[r.Next(words.Length)], b = words[r.Next(words.Length)];
            int s1 = Mathf.Clamp(230 / Mathf.Max(1, PixelFont.Width(a, 1)), 3, 9);
            PixelFont.DrawCentered(p, a, w / 2, (int)(hh * 0.78f), s1, Color.white);
            PixelFont.DrawCentered(p, b, w / 2, (int)(hh * 0.18f), 4, new Color(1, 1, 0.85f));
            // Torn / blood-sprayed corners.
            for (int i = 0; i < 6; i++) p.Blot((float)r.NextDouble() * w, (float)r.NextDouble() * hh * 0.4f, 4 + (float)r.NextDouble() * 12, new Color(0.3f, 0.02f, 0.02f, 0.85f), 0.6f, false);
            return p.ToTexture("poster", false, true, TextureWrapMode.Clamp);
        });

        /// <summary>Sign plate with text (white on colour). Used for EXIT, platform, ward and warning signs.</summary>
        public static Texture2D Sign(string text, Color bg, Color fg) => Get("sign" + text + bg + fg, () =>
        {
            int scale = 4;
            int w = Mathf.NextPowerOfTwo(PixelFont.Width(text, scale) + 24), hh = 64;
            var p = new Pix(w, hh).Fill((u, v, x, y) =>
            {
                float edge = Mathf.Min(Mathf.Min(u, 1 - u) * w, Mathf.Min(v, 1 - v) * hh);
                var c = edge < 3 ? fg * 0.9f : bg * (0.9f + Noise.Fbm(u, v, 6, 3, text.Length) * 0.2f);
                c.a = 1;
                return c;
            });
            PixelFont.DrawCentered(p, text, w / 2, hh / 2, scale, fg, 0.15f, text.GetHashCode());
            return p.ToTexture("sign", false, true, TextureWrapMode.Clamp);
        });

        // ===================================================================== decals

        /// <summary>2x2 atlas of blood splats (alpha).</summary>
        public static Texture2D BloodAtlas => Get("blood", () =>
        {
            int n = 512;
            var p = new Pix(n, n).Fill((u, v, x, y) => new Color(0.32f, 0.01f, 0.01f, 0));
            var r = new System.Random(5);
            for (int q = 0; q < 4; q++)
            {
                float cx = (q % 2 + 0.5f) * n / 2, cy = (q / 2 + 0.5f) * n / 2;
                float rad = n * 0.12f;
                p.Blot(cx, cy, rad, new Color(0.28f, 0.0f, 0.0f, 0.95f), 0.5f, false);
                for (int i = 0; i < 40; i++)
                {
                    float a = (float)(r.NextDouble() * 6.28), d = (float)(r.NextDouble() * rad * 1.6f);
                    float s = (float)(4 + r.NextDouble() * 16) * (1 - d / (rad * 1.8f));
                    p.Blot(cx + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, Mathf.Max(2, s), new Color(0.3f, 0.01f, 0.01f, 0.95f), 0.6f, false);
                }
                // Streaks.
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)(r.NextDouble() * 6.28);
                    for (int k = 0; k < 20; k++)
                    {
                        float d = rad * (0.8f + k * 0.06f);
                        p.Blot(cx + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, Mathf.Max(1.5f, 6 - k * 0.3f), new Color(0.25f, 0.0f, 0.0f, 0.9f), 0.7f, false);
                    }
                }
            }
            // Darken the centres slightly with noise for depth.
            for (int i = 0; i < p.C.Length; i++)
            {
                var c = p.C[i];
                float nz = Noise.Fbm((i % n) / (float)n, (i / n) / (float)n, 8, 3, 2);
                p.C[i] = new Color(c.r * (0.7f + nz * 0.5f), c.g, c.b, c.a);
            }
            return p.ToTexture("blood", false, true, TextureWrapMode.Clamp);
        });

        public static Texture2D BulletHole => Get("hole", () =>
        {
            int n = 64;
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
                float ang = Mathf.Atan2(v - 0.5f, u - 0.5f);
                float jag = 0.12f * Noise.Value(ang * 3 + 10, 0, 0, 5);
                float core = 1 - Noise.Smooth(0.12f, 0.18f, d);
                float ring = (1 - Noise.Smooth(0.3f + jag, 0.6f + jag, d)) * 0.6f;
                return new Color(0.04f, 0.04f, 0.04f, Mathf.Max(core, ring));
            });
            return p.ToTexture("hole", false, true, TextureWrapMode.Clamp);
        });

        public static Texture2D Scorch => Get("scorch", () =>
        {
            int n = 128;
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
                float nz = Noise.Fbm(u, v, 6, 4, 9);
                float a = Mathf.Clamp01((1 - d) * 1.6f - nz * 0.6f);
                return new Color(0.03f, 0.025f, 0.02f, a * 0.9f);
            });
            return p.ToTexture("scorch", false, true, TextureWrapMode.Clamp);
        });

        /// <summary>4x4 atlas of litter: newspaper sheets, flyers, wrappers.</summary>
        public static Texture2D Litter => Get("litter", () =>
        {
            int n = 512, cell = n / 4;
            var p = new Pix(n, n).Fill((u, v, x, y) => new Color(0, 0, 0, 0));
            var r = new System.Random(77);
            for (int i = 0; i < 16; i++)
            {
                int cx = (i % 4) * cell, cy = (i / 4) * cell;
                int w = (int)(cell * (0.55f + r.NextDouble() * 0.35f)), h = (int)(cell * (0.45f + r.NextDouble() * 0.45f));
                int ox = cx + (cell - w) / 2, oy = cy + (cell - h) / 2;
                int kind = r.Next(4);
                Color paper = kind == 0 ? new Color(0.78f, 0.76f, 0.7f) : kind == 1 ? Color.HSVToRGB((float)r.NextDouble(), 0.5f, 0.8f) : kind == 2 ? new Color(0.9f, 0.9f, 0.88f) : new Color(0.55f, 0.42f, 0.28f);
                for (int y = oy; y < oy + h; y++)
                    for (int x = ox; x < ox + w; x++)
                    {
                        float fu = (x - ox) / (float)w, fv = (y - oy) / (float)h;
                        float dirt = Noise.Fbm(fu, fv, 4, 3, i);
                        var c = paper * (0.75f + dirt * 0.3f);
                        if (kind == 0 && (y - oy) % 6 < 2 && fu > 0.08f && fu < 0.92f && Noise.Hash(x / 5, y / 6, i) > 0.2f) c *= 0.55f;
                        if (kind == 2 && (y - oy) % 9 < 2 && fu > 0.1f && fu < 0.8f) c *= 0.6f;
                        float edge = Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv));
                        float tear = Noise.Fbm(fu, fv, 8, 2, i + 40) * 0.12f;
                        if (edge < tear) continue;
                        c.a = 1;
                        p.C[y * n + x] = c;
                    }
            }
            return p.ToTexture("litter", false, true, TextureWrapMode.Clamp);
        });

        public static Texture2D Puddle => Get("puddle", () =>
        {
            int n = 128;
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
                float nz = Noise.Fbm(u, v, 4, 4, 3);
                float a = Noise.Smooth(0.95f, 0.75f, d + (nz - 0.5f) * 0.5f);
                return new Color(0.02f, 0.02f, 0.025f, a * 0.55f);
            });
            return p.ToTexture("puddle", false, true, TextureWrapMode.Clamp);
        });

        // ===================================================================== FX sprites

        public static Texture2D Soft => Get("soft", () => new Pix(64, 64).Fill((u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
            float a = Mathf.Clamp01(1 - d);
            return new Color(1, 1, 1, a * a);
        }).ToTexture("soft", false, true, TextureWrapMode.Clamp));

        public static Texture2D Flash => Get("flash", () => new Pix(128, 128).Fill((u, v, x, y) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) * 2;
            float ang = Mathf.Atan2(dy, dx);
            float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3)), 12) * 0.8f + Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2 + 0.5f)), 20) * 0.5f;
            float core = Mathf.Clamp01(1 - d * 2.5f);
            float a = Mathf.Clamp01(core + rays * Mathf.Clamp01(1 - d) * (0.7f + Noise.Value(ang * 4 + 8, d * 6, 0, 3) * 0.6f));
            var c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.95f, 0.8f), core);
            c.a = a;
            return c;
        }).ToTexture("flash", false, true, TextureWrapMode.Clamp));

        public static Texture2D Smoke => Get("smoke", () => new Pix(128, 128).Fill((u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2;
            float nz = Noise.Fbm(u, v, 4, 5, 21);
            float a = Mathf.Clamp01((1 - d) * 1.4f) * Mathf.Clamp01(nz * 1.6f - 0.3f);
            return new Color(0.9f + nz * 0.1f, 0.9f + nz * 0.1f, 0.9f + nz * 0.1f, a);
        }).ToTexture("smoke", false, true, TextureWrapMode.Clamp));

        public static Texture2D Fire => Get("fire", () => new Pix(128, 128).Fill((u, v, x, y) =>
        {
            float dx = (u - 0.5f) * 2;
            float nz = Noise.Fbm(u, v * 0.7f, 4, 4, 33);
            float shape = Mathf.Clamp01(1 - Mathf.Abs(dx) / (0.9f - v * 0.6f + nz * 0.3f)) * Mathf.Clamp01(1.2f - v * 1.1f) * Mathf.Clamp01(v * 6);
            var c = Color.Lerp(new Color(1f, 0.25f, 0.02f), new Color(1f, 0.85f, 0.4f), shape * shape);
            c.a = shape;
            return c;
        }).ToTexture("fire", false, true, TextureWrapMode.Clamp));

        public static Texture2D Spark => Get("spark", () => new Pix(32, 128).Fill((u, v, x, y) =>
        {
            float dx = Mathf.Abs(u - 0.5f) * 2;
            float a = Mathf.Clamp01(1 - dx * 2.5f) * Mathf.Clamp01(Mathf.Sin(v * Mathf.PI));
            return new Color(1, 0.85f, 0.5f, a);
        }).ToTexture("spark", false, true, TextureWrapMode.Clamp));

        /// <summary>Vertical gradient for fake volumetric light cones (bright at the top, fading out, soft sides).</summary>
        public static Texture2D Beam => Get("beam", () => new Pix(64, 128).Fill((u, v, x, y) =>
        {
            float side = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f);
            float fall = Mathf.Pow(v, 1.8f);
            float dust = 0.85f + Noise.Fbm(u, v, 6, 3, 41) * 0.3f;
            return new Color(1, 1, 1, side * fall * dust);
        }).ToTexture("beam", false, true, TextureWrapMode.Clamp));

        public static Texture2D Streak => Get("streak", () => new Pix(16, 64).Fill((u, v, x, y) =>
        {
            float a = Mathf.Clamp01(1 - Mathf.Abs(u - 0.5f) * 3) * Mathf.Sin(v * Mathf.PI);
            return new Color(0.8f, 0.85f, 0.9f, a * 0.6f);
        }).ToTexture("streak", false, true, TextureWrapMode.Clamp));

        // ===================================================================== sky

        /// <summary>Equirectangular sky: 0 night, 1 dusk, 2 overcast, 3 blood moon, 4 toxic, 5 snow storm.</summary>
        public static Texture2D Sky(int kind) => Get("sky" + kind, () =>
        {
            int w = Size(1024), h = w / 2;
            Color zen, hor, cloud;
            switch (kind)
            {
                case 1: zen = new Color(0.12f, 0.14f, 0.25f); hor = new Color(0.9f, 0.42f, 0.18f); cloud = new Color(0.35f, 0.22f, 0.2f); break;
                case 2: zen = new Color(0.32f, 0.35f, 0.38f); hor = new Color(0.55f, 0.56f, 0.55f); cloud = new Color(0.42f, 0.43f, 0.44f); break;
                case 3: zen = new Color(0.08f, 0.01f, 0.02f); hor = new Color(0.45f, 0.08f, 0.05f); cloud = new Color(0.2f, 0.04f, 0.04f); break;
                case 4: zen = new Color(0.05f, 0.09f, 0.05f); hor = new Color(0.3f, 0.42f, 0.15f); cloud = new Color(0.15f, 0.2f, 0.1f); break;
                case 5: zen = new Color(0.4f, 0.44f, 0.5f); hor = new Color(0.7f, 0.74f, 0.8f); cloud = new Color(0.6f, 0.64f, 0.7f); break;
                default: zen = new Color(0.01f, 0.015f, 0.035f); hor = new Color(0.08f, 0.1f, 0.16f); cloud = new Color(0.06f, 0.07f, 0.1f); break;
            }
            float moonU = 0.62f, moonV = 0.68f;
            var p = new Pix(w, h).Fill((u, v, x, y) =>
            {
                float el = (v - 0.5f) * 2;
                if (el < 0) return hor * 0.4f;
                var c = Color.Lerp(hor, zen, Mathf.Pow(el, 0.55f));
                float cl = Noise.Fbm(u, v * 2, 4, 6, 77 + kind);
                float cover = kind == 2 || kind == 5 ? 0.35f : 0.5f;
                float cm = Noise.Smooth(cover, cover + 0.25f, cl) * Mathf.Clamp01(el * 4);
                if (kind == 0 || kind == 3)
                {
                    float star = Noise.Hash(x, y, 5) > 0.997f ? Noise.Hash(x, y, 6) : 0;
                    c += new Color(star, star, star) * (1 - cm);
                    float du = (u - moonU) * 2, dv = (v - moonV);
                    float md = Mathf.Sqrt(du * du + dv * dv);
                    var moonCol = kind == 3 ? new Color(1f, 0.25f, 0.15f) : new Color(0.85f, 0.88f, 0.95f);
                    c += moonCol * (Mathf.Clamp01((0.025f - md) * 300) * 1.5f + Mathf.Exp(-md * 18) * 0.35f) * (1 - cm * 0.7f);
                }
                if (kind == 1)
                {
                    float du = (u - 0.3f) * 2, dv = (v - 0.53f);
                    float sd = Mathf.Sqrt(du * du + dv * dv);
                    c += new Color(1f, 0.6f, 0.3f) * Mathf.Exp(-sd * 9) * 0.8f;
                }
                var lit = kind == 1 ? cloud * (1.2f + (1 - el) * 0.8f) : cloud;
                c = Color.Lerp(c, lit * (0.7f + cl * 0.5f), cm * 0.9f);
                c.a = 1;
                return c;
            });
            return p.ToTexture("sky", false, false, TextureWrapMode.Clamp, FilterMode.Bilinear);
        });

        // ===================================================================== camo / skins

        /// <summary>Weapon skin / outfit pattern. 0 factory (plain), 1 woodland, 2 desert, 3 urban digital, 4 arctic,
        /// 5 blood red, 6 carbon weave, 7 toxic, 8 gold, 9 dragon scales, 10 ranger digital, 11 black ops, 12 hazmat.</summary>
        public static Texture2D Camo(int kind) => Get("camo" + kind, () =>
        {
            int n = Size(256);
            Color[] pal;
            switch (kind)
            {
                case 1: pal = new[] { X.Hex(0x2E3A1F), X.Hex(0x4B5A2E), X.Hex(0x6B5B3A), X.Hex(0x161A12) }; break;
                case 2: pal = new[] { X.Hex(0xC2A878), X.Hex(0x9C7E55), X.Hex(0x6E5A3E), X.Hex(0xD8C49A) }; break;
                case 3: pal = new[] { X.Hex(0x5A5E63), X.Hex(0x8B9096), X.Hex(0x2F3236), X.Hex(0xB5B9BD) }; break;
                case 4: pal = new[] { X.Hex(0xE6EBEF), X.Hex(0xB9C2CA), X.Hex(0x8C969F), X.Hex(0xF5F7F8) }; break;
                case 5: pal = new[] { X.Hex(0x5A0A0A), X.Hex(0x8E1212), X.Hex(0x2B0505), X.Hex(0xB21E1E) }; break;
                case 7: pal = new[] { X.Hex(0x1C2A10), X.Hex(0x58A01C), X.Hex(0x0D150A), X.Hex(0x9BE31C) }; break;
                case 10: pal = new[] { X.Hex(0x3B4A2C), X.Hex(0x5C6B3F), X.Hex(0x7B7151), X.Hex(0x232B1A) }; break;
                case 11: pal = new[] { X.Hex(0x15171A), X.Hex(0x23262B), X.Hex(0x0C0D0F), X.Hex(0x30343A) }; break;
                default: pal = new[] { X.Hex(0x1E2124), X.Hex(0x2A2E33), X.Hex(0x16181B), X.Hex(0x33383E) }; break;
            }
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                switch (kind)
                {
                    case 3:
                    case 10:
                    {
                        // Digital pixels.
                        int bx = (int)(u * 32), by = (int)(v * 32);
                        float f = Noise.Fbm(bx / 32f, by / 32f, 4, 3, kind);
                        int i = f < 0.42f ? 0 : f < 0.52f ? 1 : f < 0.6f ? 2 : 3;
                        return pal[i];
                    }
                    case 6:
                    {
                        float a = Mathf.Repeat(u * 24 + Mathf.Floor(v * 24) * 0.5f, 1), b = Mathf.Repeat(v * 24, 1);
                        float weave = ((int)(u * 24) + (int)(v * 24)) % 2 == 0 ? a : b;
                        return Color.Lerp(X.Hex(0x111214), X.Hex(0x3A3D42), weave * 0.6f + 0.2f);
                    }
                    case 8:
                    {
                        float f = Noise.Fbm(u, v, 6, 4, 8);
                        return Color.Lerp(X.Hex(0xA87A1E), X.Hex(0xFFD86B), f);
                    }
                    case 9:
                    {
                        Noise.Cells(u, v, 10, 9, out float f1, out float f2, out int id);
                        float edge = Noise.Smooth(0.0f, 0.12f, f2 - f1);
                        var c = Color.Lerp(X.Hex(0x7A0D0D), X.Hex(0xE0461E), Noise.Hash(id, 1, 9) * 0.6f + f1 * 0.4f);
                        return Color.Lerp(X.Hex(0x0E0606), c, edge);
                    }
                    case 12:
                    {
                        float f = Noise.Fbm(u, v, 4, 4, 12);
                        return Color.Lerp(X.Hex(0xC9A715), X.Hex(0xE8C826), f) * (0.85f + Noise.Hash(x, y, 1) * 0.1f);
                    }
                    case 0:
                        return pal[0] * (0.9f + Noise.Fbm(u, v, 16, 2, 0) * 0.2f);
                    default:
                    {
                        float f1 = Noise.Fbm(u, v, 3, 4, kind * 13);
                        float f2 = Noise.Fbm(u, v, 5, 3, kind * 13 + 5);
                        int i = f1 < 0.45f ? 0 : f1 < 0.55f ? 1 : 2;
                        if (f2 > 0.62f) i = 3;
                        return pal[i] * (0.92f + Noise.Hash(x, y, kind) * 0.08f);
                    }
                }
            });
            return p.ToTexture("camo");
        });
    }
}
