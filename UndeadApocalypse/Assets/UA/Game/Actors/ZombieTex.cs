using System.Collections.Generic;
using UnityEngine;

namespace UA.Game
{
    /// <summary>Procedural zombie textures: decayed skin, faces, torn bloodied clothing, flesh and gear.</summary>
    public static class ZombieTex
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Texture2D> Normals = new Dictionary<string, Texture2D>();

        static int Sz(int medium) => Quality.Current == Gfx.Low ? medium / 2 : medium;

        public static readonly Color[] SkinTones =
        {
            new Color(0.54f, 0.57f, 0.5f), new Color(0.48f, 0.5f, 0.44f), new Color(0.42f, 0.39f, 0.35f), new Color(0.33f, 0.3f, 0.27f),
            new Color(0.58f, 0.52f, 0.45f), new Color(0.45f, 0.49f, 0.53f),
        };

        /// <summary>Decayed skin: mottled pallor, veins, bruises, dried blood and wounds. decay 0..1.</summary>
        public static Texture2D Skin(int tone, float decay, int seed, out Texture2D normal)
        {
            string key = "skin" + tone + "_" + decay + "_" + seed;
            if (Cache.TryGetValue(key, out var t) && t != null) { normal = Normals[key]; return t; }
            int n = Sz(256);
            var h = new float[n * n];
            var baseCol = tone >= 0 && tone < SkinTones.Length ? SkinTones[tone] : SkinTones[0];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float mott = Noise.Fbm(u, v, 6, 4, seed);
                float vein = Noise.Smooth(0.86f, 0.95f, Noise.Ridged(u, v, 5, 4, seed + 3)) * (0.4f + decay * 0.6f);
                float bruise = Noise.Smooth(0.58f, 0.78f, Noise.Fbm(u, v, 3, 4, seed + 7)) * decay;
                float blood = Noise.Smooth(0.6f, 0.68f, Noise.Fbm(u, v * 1.5f, 4, 4, seed + 11));
                float wound = Noise.Smooth(0.78f, 0.85f, Noise.Fbm(u, v, 5, 3, seed + 19)) * decay;
                float grime = Noise.Smooth(0.5f, 0.85f, Noise.Fbm(u, v, 4, 4, seed + 27));
                var c = baseCol * (0.82f + mott * 0.32f);
                c = Color.Lerp(c, new Color(0.38f, 0.42f, 0.3f), decay * 0.25f * mott);
                c = Color.Lerp(c, new Color(0.17f, 0.14f, 0.11f), grime * 0.45f);
                c = Color.Lerp(c, new Color(0.28f, 0.18f, 0.32f), vein * 0.6f);
                c = Color.Lerp(c, new Color(0.3f, 0.2f, 0.28f), bruise * 0.6f);
                c = Color.Lerp(c, new Color(0.24f, 0.03f, 0.02f), blood * 0.85f);
                c = Color.Lerp(c, new Color(0.45f, 0.06f, 0.05f), wound);
                h[y * n + x] = mott * 0.3f + vein * 0.25f - wound * 0.6f + Noise.Ridged(u, v, 12, 2, seed + 23) * 0.15f;
                c.a = 1;
                return c;
            });
            t = p.ToTexture("zskin");
            normal = Pix.NormalMap("zskin_n", h, n, n, 3f);
            Cache[key] = t;
            Normals[key] = normal;
            return t;
        }

        /// <summary>Living human skin tones (playable characters and survivors).</summary>
        public static readonly Color[] HumanTones =
        {
            new Color(0.86f, 0.69f, 0.58f), new Color(0.74f, 0.56f, 0.43f), new Color(0.55f, 0.39f, 0.28f), new Color(0.38f, 0.26f, 0.19f),
        };

        /// <summary>Clean living skin with pores, mottling and a little grime.</summary>
        public static Texture2D HumanSkin(int tone, int seed, out Texture2D normal)
        {
            string key = "hskin" + tone + "_" + seed;
            if (Cache.TryGetValue(key, out var t) && t != null) { normal = Normals[key]; return t; }
            int n = Sz(256);
            var h = new float[n * n];
            var baseCol = HumanTones[Mathf.Clamp(tone, 0, HumanTones.Length - 1)];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float mott = Noise.Fbm(u, v, 8, 4, seed);
                float pore = Noise.Hash(x, y, seed + 3);
                var c = baseCol * (0.9f + mott * 0.16f + (pore - 0.5f) * 0.04f);
                float dirt = Noise.Smooth(0.6f, 0.85f, Noise.Fbm(u, v, 4, 4, seed + 9));
                c = Color.Lerp(c, c * 0.75f, dirt * 0.4f);
                h[y * n + x] = mott * 0.2f + pore * 0.05f;
                c.a = 1;
                return c;
            });
            t = p.ToTexture("hskin");
            normal = Pix.NormalMap("hskin_n", h, n, n, 1.5f);
            Cache[key] = t;
            Normals[key] = normal;
            return t;
        }

        /// <summary>Human face on the head-loft layout: eyes with irises, brows, nose shading, lips, optional stubble.</summary>
        public static Texture2D HumanFace(int tone, int seed, bool beard, Color hair)
        {
            string key = "hface" + tone + "_" + seed + beard + hair;
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            int n = Sz(256);
            var baseCol = HumanTones[Mathf.Clamp(tone, 0, HumanTones.Length - 1)];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float mott = Noise.Fbm(u, v, 8, 4, seed);
                var c = baseCol * (0.9f + mott * 0.14f);
                float fu = (u - 0.5f) * 3.2f;
                foreach (var ex in new[] { -0.3f, 0.3f })
                {
                    float dx = (fu - ex) / 0.16f, dy = (v - 0.56f) / 0.04f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    c = Color.Lerp(c, new Color(0.84f, 0.8f, 0.76f), Noise.Smooth(1f, 0.82f, d));
                    float ix = (fu - ex) / 0.065f, iy = (v - 0.56f) / 0.034f;
                    c = Color.Lerp(c, new Color(0.17f, 0.11f, 0.07f), Noise.Smooth(1f, 0.75f, Mathf.Sqrt(ix * ix + iy * iy)) * Noise.Smooth(1.05f, 0.9f, d));
                    float lx = dx, ly = (v - 0.59f) / 0.022f;
                    c *= 1 - Noise.Smooth(1.3f, 0.8f, Mathf.Sqrt(lx * lx + ly * ly)) * 0.3f;
                    float bx = (fu - ex) / 0.22f, by = (v - 0.635f) / 0.017f;
                    c = Color.Lerp(c, hair * 0.85f, Noise.Smooth(1f, 0.6f, Mathf.Sqrt(bx * bx + by * by)) * 0.85f);
                }
                c *= 1 - Noise.Smooth(0.07f, 0f, Mathf.Abs(fu)) * Noise.Smooth(0.4f, 0.48f, v) * Noise.Smooth(0.58f, 0.5f, v) * 0.12f;
                c *= 1 - Noise.Smooth(0.14f, 0f, Mathf.Abs(fu)) * Noise.Smooth(0.025f, 0f, Mathf.Abs(v - 0.41f)) * 0.3f;
                float mx = fu / 0.3f, my = (v - 0.3f) / 0.028f;
                float md = Mathf.Sqrt(mx * mx + my * my);
                c = Color.Lerp(c, new Color(baseCol.r * 0.82f, baseCol.g * 0.5f, baseCol.b * 0.48f), Noise.Smooth(1f, 0.7f, md) * 0.8f);
                c *= 1 - Noise.Smooth(0.006f, 0f, Mathf.Abs(v - 0.3f)) * Noise.Smooth(0.32f, 0.2f, Mathf.Abs(fu)) * 0.5f;
                if (beard)
                {
                    float zone = Noise.Smooth(0.47f, 0.38f, v) * Noise.Smooth(1.25f, 0.9f, Mathf.Abs(fu));
                    float st = Noise.Hash(x, y, seed + 7);
                    c = Color.Lerp(c, hair * 0.9f, zone * (0.55f + st * 0.4f) * (md < 1.05f ? 0.3f : 1f));
                }
                float dirt = Noise.Smooth(0.62f, 0.85f, Noise.Fbm(u, v, 4, 3, seed + 11));
                c = Color.Lerp(c, c * 0.72f, dirt * 0.35f);
                c.a = 1;
                return c;
            });
            t = p.ToTexture("hface", false, true, TextureWrapMode.Clamp);
            Cache[key] = t;
            return t;
        }

        /// <summary>Emission mask of branching veins (white on black) for glowing toxic infected.</summary>
        public static Texture2D Veins(int seed)
        {
            string key = "veins" + seed;
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            int n = Sz(256);
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float big = Noise.Smooth(0.8f, 0.93f, Noise.Ridged(u, v, 4, 4, seed + 51));
                float fine = Noise.Smooth(0.86f, 0.96f, Noise.Ridged(u, v, 10, 3, seed + 53)) * 0.6f;
                float patch = Noise.Smooth(0.55f, 0.75f, Noise.Fbm(u, v, 3, 3, seed + 55)) * 0.35f;
                float k = Mathf.Clamp01(Mathf.Max(big, fine) + patch);
                return new Color(k, k, k, 1);
            });
            t = p.ToTexture("zveins");
            Cache[key] = t;
            return t;
        }

        /// <summary>
        /// Face texture laid out for the head loft (u = 0.5 front centre, v = 0 chin .. 1 crown):
        /// sunken eye sockets, a torn mouth with teeth, veins and blood around the jaw.
        /// </summary>
        public static Texture2D Face(int tone, float decay, int seed, bool masked)
        {
            string key = "face" + tone + "_" + decay + "_" + seed + masked;
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            int n = Sz(256);
            var skin = Skin(tone, decay, seed, out _);
            var baseCol = SkinTones[Mathf.Clamp(tone, 0, SkinTones.Length - 1)];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float mott = Noise.Fbm(u, v, 6, 4, seed);
                var c = baseCol * (0.82f + mott * 0.3f);
                float vein = Noise.Smooth(0.87f, 0.95f, Noise.Ridged(u, v, 6, 4, seed + 3)) * (0.3f + decay * 0.7f);
                c = Color.Lerp(c, new Color(0.25f, 0.16f, 0.3f), vein * 0.6f);
                float fu = (u - 0.5f) * 3.2f; // horizontal face coordinate (~ -1..1 across the face)
                // Eye sockets.
                foreach (var ex in new[] { -0.32f, 0.32f })
                {
                    float dx = (fu - ex) / 0.24f, dy = (v - 0.56f) / 0.06f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    c = Color.Lerp(c, new Color(0.07f, 0.04f, 0.05f), Noise.Smooth(1.6f, 0.7f, d) * 0.85f);
                }
                // Brow ridge shadow and cheek hollows.
                c *= 1 - Noise.Smooth(0.06f, 0f, Mathf.Abs(v - 0.62f)) * 0.15f * Noise.Smooth(0.9f, 0.2f, Mathf.Abs(fu));
                c *= 1 - Noise.Smooth(0.08f, 0f, Mathf.Abs(v - 0.42f)) * 0.25f * Noise.Smooth(0.25f, 0.55f, Mathf.Abs(fu)) * Noise.Smooth(0.9f, 0.6f, Mathf.Abs(fu));
                // Nose shadow.
                c *= 1 - Noise.Smooth(0.08f, 0f, Mathf.Abs(fu)) * Noise.Smooth(0.42f, 0.5f, v) * Noise.Smooth(0.56f, 0.5f, v) * 0.2f;
                // Mouth: torn gash with teeth.
                float mx = fu / 0.42f, my = (v - 0.27f) / 0.045f;
                float md = Mathf.Sqrt(mx * mx + my * my);
                if (md < 1.25f)
                {
                    float inside = Noise.Smooth(1.0f, 0.75f, md);
                    var mouthCol = new Color(0.12f, 0.02f, 0.02f);
                    bool tooth = Mathf.Abs(my) > 0.25f && Mathf.Abs(my) < 0.85f && Mathf.Abs(Mathf.Sin(fu * 46)) > 0.35f && Noise.Hash((int)(fu * 30), 1, seed) > 0.2f;
                    if (tooth) mouthCol = new Color(0.62f, 0.56f, 0.42f);
                    c = Color.Lerp(c, mouthCol, inside);
                    c = Color.Lerp(c, new Color(0.3f, 0.03f, 0.02f), Noise.Smooth(1.3f, 1.0f, md) * 0.8f);
                }
                // Blood dripping from the mouth / chin and torn cheek.
                float drip = Noise.Smooth(0.55f, 0.7f, Noise.Fbm(u * 3f, v * 0.4f, 8, 3, seed + 5)) * Noise.Smooth(0.32f, 0.05f, v) * Noise.Smooth(0.5f, 0.1f, Mathf.Abs(fu));
                c = Color.Lerp(c, new Color(0.22f, 0.02f, 0.02f), drip * 0.9f);
                float tear = Noise.Smooth(0.78f, 0.84f, Noise.Fbm(u, v, 5, 3, seed + 29)) * decay;
                c = Color.Lerp(c, new Color(0.42f, 0.05f, 0.04f), tear);
                // Back of the head: patchy scalp.
                float back = Noise.Smooth(0.25f, 0.4f, Mathf.Abs(u - 0.5f));
                c = Color.Lerp(c, c * 0.8f, back * Noise.Fbm(u, v, 8, 3, seed + 2));
                if (masked && v < 0.5f && Mathf.Abs(fu) < 1.2f)
                {
                    c = new Color(0.06f, 0.06f, 0.06f) * (0.9f + Noise.Hash(x, y, 3) * 0.2f);
                    if (Mathf.Abs(fu) > 0.3f && Mathf.Abs(fu) < 0.7f && v > 0.18f && v < 0.36f) c = new Color(0.12f, 0.12f, 0.1f);
                }
                c.a = 1;
                return c;
            });
            t = p.ToTexture("zface", false, true, TextureWrapMode.Clamp);
            Cache[key] = t;
            return t;
        }

        /// <summary>
        /// Clothing with fabric weave, dirt, blood and torn holes (alpha). kind: 0 plain, 1 denim, 2 camo,
        /// 3 hazmat, 4 hoodie, 5 riot (dark padded), 6 apron (leather, blood soaked), 7 tattered cloak.
        /// </summary>
        public static Texture2D Cloth(Color col, int kind, float tatter, int seed)
        {
            string key = "cloth" + col + kind + tatter + seed;
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            int n = Sz(256);
            Texture2D camo = kind == 2 ? Tex.Camo(seed % 2 == 0 ? 1 : 10) : null;
            Color[] camoPx = null;
            int camoW = 0;
            if (camo != null)
            {
                // Recreate the camo pattern locally (texture may be non-readable).
                camoW = 64;
                camoPx = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float f1 = Noise.Fbm(x / 64f, y / 64f, 3, 4, 13), f2 = Noise.Fbm(x / 64f, y / 64f, 5, 3, 18);
                        int i = f1 < 0.45f ? 0 : f1 < 0.55f ? 1 : 2;
                        if (f2 > 0.62f) i = 3;
                        Color[] pal = { X.Hex(0x2E3A1F), X.Hex(0x4B5A2E), X.Hex(0x6B5B3A), X.Hex(0x161A12) };
                        camoPx[y * 64 + x] = pal[i];
                    }
            }
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                Color c = col;
                switch (kind)
                {
                    case 1:
                        c = col * (0.8f + ((x + y) % 3 == 0 ? 0.15f : 0) + Noise.Fbm(u, v, 16, 2, seed) * 0.2f);
                        break;
                    case 2:
                        c = camoPx[((int)(v * 64 * 2) % 64) * camoW + (int)(u * 64 * 2) % 64];
                        break;
                    case 3:
                        c = col * (0.9f + Noise.Fbm(u, v, 8, 3, seed) * 0.15f);
                        if (Mathf.Abs(Mathf.Repeat(v * 6, 1) - 0.5f) < 0.02f) c *= 0.7f;
                        break;
                    case 5:
                        c = col * (0.7f + (Mathf.Repeat(v * 10, 1) < 0.08f ? -0.2f : 0) + Noise.Fbm(u, v, 12, 2, seed) * 0.2f);
                        break;
                    case 6:
                        c = col * (0.7f + Noise.Fbm(u, v, 6, 4, seed) * 0.4f);
                        break;
                    default:
                        c = col * (0.85f + (Mathf.Sin(x * 1.7f) * Mathf.Sin(y * 1.7f)) * 0.06f + Noise.Fbm(u, v, 12, 2, seed) * 0.15f);
                        break;
                }
                float dirt = Noise.Smooth(0.45f, 0.85f, Noise.Fbm(u, v, 4, 4, seed + 3));
                c = Color.Lerp(c, new Color(0.16f, 0.13f, 0.09f), dirt * 0.5f);
                // Blood: large soaked stains (fresh red to dried brown), fine spatter and drips running down.
                // Living characters (low tatter) get far less blood than the infected.
                float blood = Noise.Smooth(0.53f, 0.63f, Noise.Fbm(u, v * 1.4f, 3, 5, seed + 7)) * (kind == 6 ? 1.6f : 1f) * Mathf.Clamp01(tatter * 1.6f);
                float spatter = Noise.Hash(x / 2, y / 2, seed + 23) > 0.982f ? 1f : 0f;
                float drip = Noise.Smooth(0.68f, 0.84f, Noise.Fbm(u, v * 0.08f, 28, 2, seed + 29)) * Noise.Smooth(0.25f, 0.9f, v);
                var bloodCol = Color.Lerp(new Color(0.34f, 0.02f, 0.015f), new Color(0.12f, 0.03f, 0.02f), Noise.Fbm(u, v, 8, 2, seed + 31));
                c = Color.Lerp(c, bloodCol, Mathf.Clamp01(blood + (spatter * 0.8f + drip * 0.65f) * Mathf.Clamp01(tatter * 1.6f)) * 0.9f);
                // Holes: more near the hems (v near 0) and wherever the noise peaks.
                float hole = Noise.Fbm(u, v, 6, 4, seed + 17) + Noise.Smooth(0.12f, 0f, v) * 0.35f * tatter;
                float a = hole > 0.74f - tatter * 0.14f ? 0 : 1;
                // Ragged hem.
                if (v < 0.04f + Noise.Value(u * 40, 0, 40, seed) * 0.07f * tatter) a = 0;
                // Living characters' clothes are intact.
                if (tatter < 0.1f) a = 1;
                c.a = a;
                return c;
            });
            t = p.ToTexture("zcloth");
            Cache[key] = t;
            return t;
        }

        /// <summary>Exposed muscle and sinew (mutants and bosses).</summary>
        public static Texture2D Flesh(int seed, out Texture2D normal)
        {
            string key = "flesh" + seed;
            if (Cache.TryGetValue(key, out var t) && t != null) { normal = Normals[key]; return t; }
            int n = Sz(256);
            var h = new float[n * n];
            var p = new Pix(n, n).Fill((u, v, x, y) =>
            {
                float fib = Mathf.Sin((u * 40 + Noise.Fbm(u, v, 4, 3, seed) * 6) * Mathf.PI) * 0.5f + 0.5f;
                Noise.Cells(u, v, 8, seed, out float f1, out float f2, out int id);
                float cell = Noise.Smooth(0.0f, 0.15f, f2 - f1);
                var c = Color.Lerp(new Color(0.35f, 0.06f, 0.05f), new Color(0.62f, 0.2f, 0.16f), fib * 0.6f + Noise.Hash(id, 2, seed) * 0.3f);
                c = Color.Lerp(new Color(0.18f, 0.02f, 0.02f), c, cell);
                c = Color.Lerp(c, new Color(0.55f, 0.5f, 0.42f), Noise.Smooth(0.82f, 0.9f, Noise.Ridged(u, v, 3, 3, seed + 5)) * 0.6f);
                h[y * n + x] = fib * 0.3f + cell * 0.5f;
                c.a = 1;
                return c;
            });
            t = p.ToTexture("flesh");
            normal = Pix.NormalMap("flesh_n", h, n, n, 4f);
            Cache[key] = t;
            Normals[key] = normal;
            return t;
        }
    }
}
