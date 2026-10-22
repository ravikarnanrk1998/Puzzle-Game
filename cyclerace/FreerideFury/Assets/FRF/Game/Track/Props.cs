using UnityEngine;

namespace FRF
{
    /// <summary>Low-poly palette props, written into a <see cref="MeshBuilder"/> at a ground position.</summary>
    public static class Props
    {
        static Color Pick(Color[] cs, System.Random r) => cs[r.Next(cs.Length)];

        static Color Vary(Color c, System.Random r, float amt = 0.08f)
        {
            float f = 1 + ((float)r.NextDouble() - 0.5f) * 2 * amt;
            return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f));
        }

        static void Place(MeshBuilder b, Vector3 at, float yaw, float scale = 1)
        {
            b.Xf = Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
        }

        /// <summary>Layered conifer; <paramref name="snow"/> whitens the tier tops.</summary>
        public static void Pine(MeshBuilder b, Vector3 at, float height, Theme th, System.Random r, bool snow = false)
        {
            Place(b, at, (float)r.NextDouble() * 360);
            var green = Vary(Pick(th.Foliage, r), r);
            var trunk = Vary(th.Trunk, r);
            float h = height;
            b.Cylinder(new Vector3(0, -0.4f, 0), new Vector3(0, h * 0.35f, 0), h * 0.035f, h * 0.025f, 5, trunk, false);
            int tiers = h > 9 ? 4 : 3;
            float baseR = h * 0.27f;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)tiers;
                float y0 = h * (0.2f + f * 0.62f), y1 = y0 + h * (0.42f - f * 0.08f);
                float rr = baseR * (1 - f * 0.55f);
                var c = snow && t % 2 == 1 ? th.Cap : green.Lighten(f * 0.12f);
                b.Cylinder(new Vector3(0, y0, 0), new Vector3(0, y1, 0), rr, 0, 7, c, true, t * 0.5f);
            }
            if (snow) b.Cylinder(new Vector3(0, h * 0.9f, 0), new Vector3(0, h * 1.02f, 0), baseR * 0.22f, 0, 6, th.Cap, true);
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Broadleaf tree: trunk and a few faceted canopy blobs.</summary>
        public static void Broadleaf(MeshBuilder b, Vector3 at, float height, Theme th, System.Random r)
        {
            Place(b, at, (float)r.NextDouble() * 360);
            var trunk = Vary(th.Trunk, r);
            float h = height;
            b.Cylinder(new Vector3(0, -0.4f, 0), new Vector3(0, h * 0.55f, 0), h * 0.05f, h * 0.035f, 5, trunk, false);
            b.Cylinder(new Vector3(0, h * 0.45f, 0), new Vector3(h * 0.16f, h * 0.68f, 0.05f), h * 0.025f, h * 0.015f, 4, trunk, false);
            int blobs = 3 + r.Next(2);
            for (int k = 0; k < blobs; k++)
            {
                var c = Vary(Pick(th.Foliage, r), r, 0.12f);
                float a = k / (float)blobs * Mathf.PI * 2 + (float)r.NextDouble();
                var pos = new Vector3(Mathf.Cos(a) * h * 0.14f, h * (0.66f + (float)r.NextDouble() * 0.16f), Mathf.Sin(a) * h * 0.14f);
                float sz = h * (0.2f + (float)r.NextDouble() * 0.07f);
                b.Rock(pos, new Vector3(sz, sz * 1.15f, sz), r.Next(), c, 1);
            }
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Bare, burnt tree for the volcano.</summary>
        public static void DeadTree(MeshBuilder b, Vector3 at, float height, Theme th, System.Random r)
        {
            Place(b, at, (float)r.NextDouble() * 360);
            var c = Vary(th.Trunk, r);
            float h = height;
            b.Cylinder(new Vector3(0, -0.4f, 0), new Vector3(0.1f, h, 0), h * 0.045f, h * 0.012f, 5, c, false);
            for (int k = 0; k < 4; k++)
            {
                float y = h * (0.35f + k * 0.15f);
                float a = k * 2.1f + (float)r.NextDouble();
                var tip = new Vector3(Mathf.Cos(a) * h * 0.3f, y + h * 0.18f, Mathf.Sin(a) * h * 0.3f);
                b.Cylinder(new Vector3(0.05f, y, 0), tip, h * 0.02f, h * 0.006f, 4, c, false);
            }
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Saguaro cactus.</summary>
        public static void Cactus(MeshBuilder b, Vector3 at, float height, System.Random r)
        {
            Place(b, at, (float)r.NextDouble() * 360);
            var c = Vary(Mats.Hex(0x4F7A3A), r);
            float h = height, rad = h * 0.07f;
            b.Cylinder(new Vector3(0, -0.3f, 0), new Vector3(0, h, 0), rad, rad * 0.8f, 7, c, true);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? 1 : -1;
                float y = h * (0.38f + k * 0.15f + (float)r.NextDouble() * 0.1f);
                var elbow = new Vector3(side * h * 0.22f, y, 0);
                b.Cylinder(new Vector3(0, y, 0), elbow, rad * 0.6f, rad * 0.6f, 6, c, false);
                b.Cylinder(elbow, elbow + new Vector3(0, h * 0.28f, 0), rad * 0.62f, rad * 0.5f, 6, c, true);
            }
            b.Xf = Matrix4x4.identity;
        }

        public static void Boulder(MeshBuilder b, Vector3 at, float size, Theme th, System.Random r, float flat = 0.75f)
        {
            Place(b, at, (float)r.NextDouble() * 360);
            var c = Vary(Color.Lerp(th.Rock, th.RockDark, (float)r.NextDouble() * 0.6f), r);
            b.Rock(new Vector3(0, size * 0.3f, 0), new Vector3(size * (0.9f + (float)r.NextDouble() * 0.4f), size * flat, size), r.Next(), c, 1);
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Trail obstacle matching its physics box.</summary>
        public static void Obstacle(MeshBuilder b, Vector3 at, float yawDeg, Obstacle o, Theme th)
        {
            var r = new System.Random(o.Seed);
            Place(b, at, yawDeg + o.Yaw * Mathf.Rad2Deg);
            switch (o.Kind)
            {
                case ObstacleKind.Log:
                {
                    var bark = Vary(th.Trunk, r);
                    float rad = o.H * 0.5f;
                    b.Cylinder(new Vector3(-o.HalfX, rad, 0), new Vector3(o.HalfX, rad, 0), rad, rad * 0.9f, 7, bark, false);
                    var end = bark.Lighten(0.35f);
                    b.Cylinder(new Vector3(-o.HalfX - 0.01f, rad, 0), new Vector3(-o.HalfX, rad, 0), rad * 0.92f, rad * 0.92f, 7, end, true);
                    b.Cylinder(new Vector3(o.HalfX, rad, 0), new Vector3(o.HalfX + 0.01f, rad, 0), rad * 0.83f, rad * 0.83f, 7, end, true);
                    b.Cylinder(new Vector3(o.HalfX * 0.3f, rad * 1.5f, 0), new Vector3(o.HalfX * 0.45f, rad * 2.4f, 0.2f), 0.06f, 0.03f, 4, bark, false);
                    break;
                }
                case ObstacleKind.Stump:
                {
                    var bark = Vary(th.Trunk, r);
                    b.Cylinder(new Vector3(0, -0.2f, 0), new Vector3(0, o.H, 0), o.HalfX, o.HalfX * 0.9f, 8, bark, false);
                    b.Cylinder(new Vector3(0, o.H, 0), new Vector3(0, o.H + 0.01f, 0), o.HalfX * 0.85f, o.HalfX * 0.85f, 8, Mats.Hex(0xC8A070), true);
                    break;
                }
                case ObstacleKind.Cactus:
                    b.Xf = Matrix4x4.identity;
                    Cactus(b, at, o.H, r);
                    break;
                default:
                {
                    var c = Vary(Color.Lerp(th.Rock, th.RockDark, 0.3f), r);
                    b.Rock(new Vector3(0, o.H * 0.42f, 0), new Vector3(o.HalfX * 1.05f, o.H * 0.62f, o.HalfS * 1.05f), o.Seed, c, 1);
                    break;
                }
            }
            b.Xf = Matrix4x4.identity;
        }

        static readonly Color[] Shirts =
        {
            Mats.Hex(0xE53935), Mats.Hex(0x1E88E5), Mats.Hex(0xFDD835), Mats.Hex(0x43A047), Mats.Hex(0xFB8C00), Mats.Hex(0x8E24AA),
            Mats.Hex(0xFAFAFA), Mats.Hex(0x212121), Mats.Hex(0x00ACC1), Mats.Hex(0xD81B60), Mats.Hex(0x6D4C41),
        };

        /// <summary>Cheering spectator, about 1.75 m, facing +Z of <paramref name="yawDeg"/>.</summary>
        public static void Spectator(MeshBuilder b, Vector3 at, float yawDeg, System.Random r, bool cold)
        {
            Place(b, at, yawDeg, 0.92f + (float)r.NextDouble() * 0.16f);
            var shirt = Pick(Shirts, r);
            var pants = r.NextDouble() < 0.6 ? Mats.Hex(0x2B3A55) : Mats.Hex(0x3A3A3A);
            var skin = Catalog.SkinTones[r.Next(Catalog.SkinTones.Length)];
            b.Box(new Vector3(-0.1f, 0.42f, 0), new Vector3(0.08f, 0.42f, 0.09f), pants);
            b.Box(new Vector3(0.1f, 0.42f, 0), new Vector3(0.08f, 0.42f, 0.09f), pants);
            b.Box(new Vector3(0, 1.12f, 0), new Vector3(0.21f, 0.3f, 0.13f), shirt);
            b.Box(new Vector3(0, 1.58f, 0.01f), new Vector3(0.11f, 0.13f, 0.11f), skin);
            b.Box(new Vector3(0, 1.73f, 0), new Vector3(0.12f, 0.04f, 0.12f), cold ? shirt.Darken(0.3f) : Mats.Hex(0x2A1C14));
            bool up = r.NextDouble() < 0.55;
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = new Vector3(s * 0.27f, 1.36f, 0);
                var hand = up ? sh + new Vector3(s * 0.12f, 0.55f, 0.05f) : sh + new Vector3(s * 0.05f, -0.5f, 0.05f);
                b.Beam(sh, hand, 0.1f, 0.1f, shirt);
            }
            if (r.NextDouble() < 0.18)
            {
                var hand = new Vector3(0.39f, 1.9f, 0.05f);
                b.Beam(hand, hand + new Vector3(0, 0.9f, 0), 0.03f, 0.03f, Mats.Hex(0xDDDDDD));
                b.Panel(hand + new Vector3(0, 0.45f, 0), hand + new Vector3(0.7f, 0.45f, 0), hand + new Vector3(0.7f, 0.9f, 0), hand + new Vector3(0, 0.9f, 0),
                    Palette.Uv(Pick(Shirts, r)), Vector3.back);
                b.Panel(hand + new Vector3(0, 0.45f, 0), hand + new Vector3(0.7f, 0.45f, 0), hand + new Vector3(0.7f, 0.9f, 0), hand + new Vector3(0, 0.9f, 0),
                    Palette.Uv(Pick(Shirts, r)), Vector3.forward);
            }
            b.Xf = Matrix4x4.identity;
        }

        public static void HayBale(MeshBuilder b, Vector3 at, float yawDeg)
        {
            Place(b, at, yawDeg);
            b.Box(new Vector3(0, 0.4f, 0), new Vector3(0.62f, 0.4f, 0.45f), Mats.Hex(0xD9B65A));
            b.Box(new Vector3(0, 0.4f, 0), new Vector3(0.64f, 0.06f, 0.47f), Mats.Hex(0xB8923A));
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Yellow and black chevron board pointing <paramref name="dir"/> (+1 right) on two posts.</summary>
        public static void Chevron(MeshBuilder b, Vector3 at, float yawDeg, int dir)
        {
            Place(b, at, yawDeg);
            var post = Mats.Hex(0x5A4A3A);
            b.Beam(new Vector3(-0.8f, -0.3f, 0), new Vector3(-0.8f, 1.6f, 0), 0.1f, 0.1f, post);
            b.Beam(new Vector3(0.8f, -0.3f, 0), new Vector3(0.8f, 1.6f, 0), 0.1f, 0.1f, post);
            b.Box(new Vector3(0, 1.45f, 0), new Vector3(1.1f, 0.32f, 0.04f), Mats.Hex(0xFFC400));
            var black = Palette.Uv(Mats.Hex(0x151515));
            for (int k = -1; k <= 1; k++)
            {
                float cx = k * 0.62f;
                float tip = cx + dir * 0.18f, back = cx - dir * 0.14f;
                foreach (float z in new[] { -0.05f, 0.05f })
                {
                    var face = z < 0 ? Vector3.back : Vector3.forward;
                    b.Panel(new Vector3(back, 1.2f, z), new Vector3(back + dir * 0.12f, 1.2f, z), new Vector3(tip + dir * 0.12f, 1.45f, z), new Vector3(tip, 1.45f, z), black, face);
                    b.Panel(new Vector3(tip, 1.45f, z), new Vector3(tip + dir * 0.12f, 1.45f, z), new Vector3(back + dir * 0.12f, 1.7f, z), new Vector3(back, 1.7f, z), black, face);
                }
            }
            b.Xf = Matrix4x4.identity;
        }

        /// <summary>Glowing lava vent ring (emissive palette builder).</summary>
        public static void Vent(MeshBuilder rock, MeshBuilder glow, Vector3 at, float size, Theme th, System.Random r)
        {
            Place(rock, at, (float)r.NextDouble() * 360);
            Place(glow, at, 0);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2;
                rock.Rock(new Vector3(Mathf.Cos(a) * size, size * 0.2f, Mathf.Sin(a) * size), new Vector3(size * 0.55f, size * 0.45f, size * 0.55f), r.Next(), th.RockDark, 0);
            }
            glow.Cylinder(new Vector3(0, -0.2f, 0), new Vector3(0, size * 0.25f, 0), size * 0.95f, size * 0.8f, 8, Mats.Hex(0xFF5A10), true);
            rock.Xf = Matrix4x4.identity;
            glow.Xf = Matrix4x4.identity;
        }
    }
}
