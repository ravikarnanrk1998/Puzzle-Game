using System;
using System.Collections.Generic;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    public enum B
    {
        Root, Hips, Spine, Chest, Neck, Head,
        ClavL, ArmL, ForeL, HandL, ClavR, ArmR, ForeR, HandR,
        ThighL, ShinL, FootL, ToeL, ThighR, ShinR, FootR, ToeR,
    }

    public enum Outfit { Batter, Fielder, Keeper, KeeperSpin, Bowler, Umpire }

    /// <summary>What a character wears and looks like.</summary>
    public sealed class Look
    {
        public Outfit Outfit;
        public TeamDef Team;
        public string Name = "";
        public int Number;
        public int Skin = 2, Hair, Build = 1;
        public bool LeftHanded;

        public static Look For(Player p, TeamDef t, Outfit o) => new Look { Outfit = o, Team = t, Name = p.Last, Number = p.Jersey, Skin = p.Skin, Hair = p.Hair, Build = p.Build, LeftHanded = p.LeftBat };
    }

    /// <summary>
    /// Procedural skinned cricketer: a 22-bone skeleton and a body built from weighted tubes
    /// (torso, limbs, head) with kit, pads, gloves, shoes and helmet or cap.
    /// </summary>
    public sealed class Body : MonoBehaviour
    {
        public Transform[] Bones;
        public Transform Bat, HeadGear;
        public Look Look;
        public float Scale = 1;
        public SkinnedMeshRenderer Skin;

        static readonly B[] Parent =
        {
            B.Root, B.Root, B.Hips, B.Spine, B.Chest, B.Neck,
            B.Chest, B.ClavL, B.ArmL, B.ForeL, B.Chest, B.ClavR, B.ArmR, B.ForeR,
            B.Hips, B.ThighL, B.ShinL, B.FootL, B.Hips, B.ThighR, B.ShinR, B.FootR,
        };

        /// <summary>Bind-pose joint positions in character space (metres, facing +Z, left = -X).</summary>
        public static readonly Vector3[] BindPos =
        {
            new Vector3(0, 0, 0), new Vector3(0, 0.97f, 0), new Vector3(0, 1.08f, 0), new Vector3(0, 1.27f, 0), new Vector3(0, 1.49f, 0), new Vector3(0, 1.6f, 0),
            new Vector3(-0.03f, 1.44f, 0), new Vector3(-0.185f, 1.44f, 0), new Vector3(-0.185f, 1.15f, 0), new Vector3(-0.185f, 0.895f, 0),
            new Vector3(0.03f, 1.44f, 0), new Vector3(0.185f, 1.44f, 0), new Vector3(0.185f, 1.15f, 0), new Vector3(0.185f, 0.895f, 0),
            new Vector3(-0.09f, 0.95f, 0), new Vector3(-0.09f, 0.52f, 0), new Vector3(-0.09f, 0.085f, 0), new Vector3(-0.09f, 0.02f, 0.13f),
            new Vector3(0.09f, 0.95f, 0), new Vector3(0.09f, 0.52f, 0), new Vector3(0.09f, 0.085f, 0), new Vector3(0.09f, 0.02f, 0.13f),
        };

        public Transform this[B b] => Bones[(int)b];

        public const float UpperArm = 0.29f, ForeArm = 0.255f, Thigh = 0.43f, Shin = 0.435f;

        // Submeshes.
        const int SSkin = 0, SShirt = 1, STrousers = 2, SShoes = 3, SGear = 4, SFace = 5, SHair = 6;

        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        public static Body Create(Transform parent, Look look, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<Body>();
            body.Look = look;
            body.Build();
            return body;
        }

        void Build()
        {
            Bones = new Transform[BindPos.Length];
            for (int i = 0; i < BindPos.Length; i++)
            {
                var t = i == 0 ? transform : new GameObject(((B)i).ToString()).transform;
                Bones[i] = t;
            }
            for (int i = 1; i < BindPos.Length; i++)
            {
                Bones[i].SetParent(Bones[(int)Parent[i]], false);
                Bones[i].localPosition = BindPos[i] - BindPos[(int)Parent[i]];
                Bones[i].localRotation = Quaternion.identity;
            }

            var o = Look.Outfit;
            bool pads = o == Outfit.Batter || o == Outfit.Keeper || o == Outfit.KeeperSpin;
            bool gloves = pads;
            float build = 0.94f + Look.Build * 0.05f;
            string key = o + "_" + build + "_" + pads;
            if (!MeshCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = BuildMesh(build, pads, gloves, o == Outfit.Umpire);
                MeshCache[key] = mesh;
            }
            var skinGo = new GameObject("Skin");
            skinGo.transform.SetParent(transform, false);
            Skin = skinGo.AddComponent<SkinnedMeshRenderer>();
            Skin.sharedMesh = mesh;
            Skin.bones = Bones;
            Skin.rootBone = Bones[(int)B.Hips];
            Skin.localBounds = new Bounds(new Vector3(0, -0.1f, 0), new Vector3(2.6f, 2.8f, 2.6f));
            Skin.quality = Quality.Current >= Gfx.High ? SkinQuality.Bone4 : SkinQuality.Bone2;
            Skin.sharedMaterials = Materials(Look);

            // Head gear.
            bool helmet = o == Outfit.Batter || o == Outfit.KeeperSpin;
            bool cap = o == Outfit.Fielder || o == Outfit.Keeper;
            if (helmet) HeadGear = Gear.Helmet(this[B.Head], Look.Team);
            else if (cap) HeadGear = Gear.Cap(this[B.Head], Look.Team);
            else if (o == Outfit.Umpire) HeadGear = Gear.UmpireHat(this[B.Head]);
            if (o == Outfit.Batter) Bat = Gear.MakeBat(transform.parent);
            Scale = 0.96f + (Look.Build - 1) * 0.03f + ((Look.Number * 37) % 9 - 4) * 0.008f;
            transform.localScale = Vector3.one * Scale;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void SetVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            if (Bat != null) foreach (var r in Bat.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        void OnDestroy()
        {
            if (Bat != null) Destroy(Bat.gameObject);
        }

        // ------------------------------------------------------------ materials

        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        static Material M(string key, Func<Material> make)
        {
            if (MatCache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            MatCache[key] = m;
            return m;
        }

        public static void ClearCaches()
        {
            MatCache.Clear();
        }

        static Material[] Materials(Look look)
        {
            var t = look.Team;
            var skinCol = Tex.SkinColor(look.Skin);
            var skin = M("skin" + look.Skin, () => Mats.Lit(skinCol, 0.38f));
            var face = M("face" + look.Skin + "_" + (look.Number % 3), () => Mats.Lit(Color.white, 0.35f, 0, Tex.Face(look.Skin, look.Number % 3)));
            Material shirt, trousers;
            if (look.Outfit == Outfit.Umpire)
            {
                shirt = M("umpshirt", () => Mats.LitNormal(new Color(0.55f, 0.75f, 0.95f), null, Tex.FabricNormal, 0.25f, 0.4f, new Vector2(30, 30)));
                trousers = M("umptrousers", () => Mats.Lit(new Color(0.08f, 0.09f, 0.12f), 0.25f));
            }
            else
            {
                var kit = Kits.Shirt(t, look.Name, look.Number);
                shirt = Mats.LitNormal(Color.white, kit, Tex.FabricNormal, 0.42f, 0.35f);
                // Detail normal tiling would need a second UV set; the fabric normal is tiled via the base map scale.
                trousers = M("trousers" + t.Id, () => Mats.LitNormal(Kits.TrouserColor(t), null, Tex.FabricNormal, 0.25f, 0.3f, new Vector2(24, 24)));
            }
            var shoes = M("shoes" + t?.Id, () => Mats.Lit(new Color(0.94f, 0.94f, 0.95f), 0.45f));
            var gear = M("gear" + t?.Id, () => Mats.Lit(new Color(0.95f, 0.95f, 0.93f), 0.5f));
            var hairCol = look.Hair == 3 ? new Color(0.24f, 0.16f, 0.1f) : new Color(0.05f, 0.04f, 0.035f);
            var hair = M("hair" + look.Hair, () => Mats.Lit(hairCol, 0.3f));
            return new[] { skin, shirt, trousers, shoes, gear, face, hair };
        }

        // ---------------------------------------------------------------- mesh

        static BoneWeight W(B a, float wa, B b = B.Root, float wb = 0)
        {
            if (wb <= 0.0001f) return new BoneWeight { boneIndex0 = (int)a, weight0 = 1 };
            float s = wa + wb;
            return new BoneWeight { boneIndex0 = (int)a, weight0 = wa / s, boneIndex1 = (int)b, weight1 = wb / s };
        }

        /// <summary>Tube along a bone with blended weights at both joints.</summary>
        static void Limb(MeshBuilder mb, int sub, Vector3 a, Vector3 b, B bone, B parent, B child, Func<float, float> radius, float rx, float rz,
            float t0, float t1, int rings, int segs, float v0, float v1, bool capEnd = false, float zOff = 0)
        {
            var axis = (b - a).normalized;
            var fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.7f)
            {
                fwd = Vector3.up;
                right = Vector3.Cross(axis, fwd).normalized;
            }
            // Winding depends on which way the bone points.
            bool flip = Vector3.Dot(Vector3.Cross(axis, right), fwd) > 0;
            int start = mb.VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float t = Mathf.Lerp(t0, t1, r / (float)rings);
                var c = Vector3.Lerp(a, b, t) + fwd * zOff;
                float rad = radius(t);
                BoneWeight w;
                if (t < 0.18f && parent != bone) w = W(bone, 0.5f + t / 0.36f, parent, 0.5f - t / 0.36f);
                else if (t > 0.82f && child != bone) w = W(bone, 0.5f + (1 - t) / 0.36f, child, 0.5f - (1 - t) / 0.36f);
                else w = W(bone, 1);
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    var n = (fwd * Mathf.Cos(th) * rx + right * Mathf.Sin(th) * rz).normalized;
                    var p = c + fwd * (Mathf.Cos(th) * rad * rz) + right * (Mathf.Sin(th) * rad * rx);
                    mb.AddW(p, n, new Vector2(s / (float)segs, Mathf.Lerp(v0, v1, r / (float)rings)), w);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    if (flip) mb.Quad(sub, i0, i1, i1 + 1, i0 + 1);
                    else mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            }
            if (capEnd)
            {
                var c = Vector3.Lerp(a, b, t1) + fwd * zOff + axis * radius(t1) * 0.6f;
                int ci = mb.AddW(c, axis, new Vector2(0.5f, v1), W(child != bone && t1 > 0.82f ? child : bone, 1));
                int last = start + rings * (segs + 1);
                for (int s = 0; s < segs; s++)
                {
                    if (flip) mb.Tri(sub, last + s + 1, last + s, ci);
                    else mb.Tri(sub, last + s, last + s + 1, ci);
                }
            }
        }

        static Mesh BuildMesh(float build, bool pads, bool gloves, bool umpire)
        {
            var mb = new MeshBuilder { UseWeights = true };
            int segs = Quality.Current == Gfx.Low ? 10 : Quality.Current == Gfx.Medium ? 14 : 18;
            Vector3 P(B b) => BindPos[(int)b];

            // Torso: lofted elliptical sections from crotch to neck.
            float[][] prof =
            {
                new[] { 0.86f, 0.125f, 0.095f, 0f }, new[] { 0.93f, 0.162f, 0.112f, 0f }, new[] { 1.0f, 0.158f, 0.106f, 0f }, new[] { 1.08f, 0.142f, 0.1f, 0.006f },
                new[] { 1.16f, 0.148f, 0.104f, 0.012f }, new[] { 1.25f, 0.163f, 0.115f, 0.018f }, new[] { 1.34f, 0.172f, 0.12f, 0.015f }, new[] { 1.4f, 0.17f, 0.11f, 0.006f },
                new[] { 1.44f, 0.152f, 0.095f, 0.002f }, new[] { 1.47f, 0.118f, 0.08f, 0f }, new[] { 1.5f, 0.072f, 0.062f, 0f },
            };
            if (umpire) for (int i = 4; i < 8; i++) prof[i][2] *= 1.06f;
            int startTorso = mb.VertexCount;
            int rows = prof.Length;
            for (int r = 0; r < rows; r++)
            {
                float y = prof[r][0], rx = prof[r][1] * build, rz = prof[r][2] * build, zo = prof[r][3];
                BoneWeight w;
                if (y < 1.0f) w = W(B.Hips, 1);
                else if (y < 1.12f) w = W(B.Hips, 1.12f - y, B.Spine, y - 1.0f);
                else if (y < 1.22f) w = W(B.Spine, 1);
                else if (y < 1.32f) w = W(B.Spine, 1.32f - y, B.Chest, y - 1.22f);
                else if (y < 1.47f) w = W(B.Chest, 1);
                else w = W(B.Chest, 0.6f, B.Neck, 0.4f);
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    // Slightly squarer sections read more muscular than pure ellipses.
                    float k = 1 + 0.08f * (1 - Mathf.Abs(sx * cz) * 2);
                    var p = new Vector3(sx * rx * k, y, cz * rz * k + zo);
                    var n = new Vector3(sx / rx, 0, cz / rz).normalized;
                    var ww = w;
                    // Outer shoulders follow the upper arms.
                    if (y > 1.36f && Mathf.Abs(p.x) > 0.13f)
                    {
                        float a = Mathf.Clamp01((Mathf.Abs(p.x) - 0.13f) / 0.06f) * 0.3f;
                        ww = W(B.Chest, 1 - a, p.x < 0 ? B.ArmL : B.ArmR, a);
                    }
                    float v = Mathf.InverseLerp(1.0f, 1.5f, y) * 0.92f + 0.08f;
                    mb.AddW(p, n, new Vector2(s / (float)segs, Mathf.Clamp(v, 0.08f, 1f)), ww);
                }
            }
            for (int r = 0; r < rows - 1; r++)
            {
                int sub = prof[r][0] < 0.99f ? STrousers : SShirt;
                for (int s = 0; s < segs; s++)
                {
                    int i0 = startTorso + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            }
            // Crotch cap.
            {
                int c = mb.AddW(new Vector3(0, 0.82f, 0), Vector3.down, new Vector2(0.5f, 0), W(B.Hips, 1));
                for (int s = 0; s < segs; s++) mb.Tri(STrousers, startTorso + s + 1, startTorso + s, c);
            }

            // Neck and head.
            Limb(mb, SSkin, new Vector3(0, 1.47f, 0.0f), new Vector3(0, 1.64f, 0.01f), B.Neck, B.Chest, B.Head, t => 0.05f * build, 1, 1.05f, 0, 1, 3, segs, 0, 1);
            // Head: a sculpted loft from chin to crown (front and back depths differ), plus nose, ears and hair.
            mb.Weight = W(B.Head, 1);
            HeadLoft(mb, SFace, segs + 4, 1f, 1.615f, 1.84f, false);
            mb.Sphere(SFace, new Vector3(0, 1.71f, 0.094f), new Vector3(0.011f, 0.024f, 0.017f), 8, 6);
            mb.Sphere(SSkin, new Vector3(-0.077f, 1.712f, -0.002f), new Vector3(0.011f, 0.027f, 0.019f), 7, 5);
            mb.Sphere(SSkin, new Vector3(0.077f, 1.712f, -0.002f), new Vector3(0.011f, 0.027f, 0.019f), 7, 5);
            HeadLoft(mb, SHair, segs + 4, 1.055f, 1.695f, 1.845f, true);

            // Arms: sleeve, then skin.
            foreach (var side in new[] { -1, 1 })
            {
                B arm = side < 0 ? B.ArmL : B.ArmR, fore = side < 0 ? B.ForeL : B.ForeR, hand = side < 0 ? B.HandL : B.HandR, clav = side < 0 ? B.ClavL : B.ClavR;
                var sh = P(arm) + new Vector3(0, 0.03f, 0);
                var el = P(fore);
                var wr = P(hand);
                // Fitted sleeve just over the deltoid and biceps, then bare arm with real muscle shape.
                Limb(mb, SShirt, sh, el, arm, clav, fore, t => (0.062f - t * 0.012f) * build, 1.04f, 1, 0, 0.5f, 5, segs, 0.0f, 0.065f);
                Limb(mb, SSkin, sh, el, arm, clav, fore, t => (0.05f - (t - 0.45f) * 0.014f + 0.006f * Mathf.Sin(Mathf.Clamp01((t - 0.35f) / 0.5f) * Mathf.PI)) * build, 1.02f, 1.08f, 0.44f, 1f, 5, segs, 0, 1);
                Limb(mb, SSkin, el, wr, fore, arm, hand, t => (0.047f - t * 0.016f + 0.006f * Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI)) * build, 1.14f, 0.92f, 0, 1, 6, segs, 0, 1);
                // Hand / glove: flattened block pointing down, capped.
                var tip = wr + new Vector3(0, gloves ? -0.18f : -0.17f, 0.01f);
                if (gloves)
                {
                    // Padded glove: cuff, palm and rounded fingers.
                    Limb(mb, SGear, wr + new Vector3(0, 0.06f, 0), tip, hand, fore, hand, t => 0.05f - t * 0.012f + 0.012f * Mathf.Sin(Mathf.Clamp01(t * 1.1f) * Mathf.PI), 0.86f, 1.1f, 0, 1, 6, segs, 0, 1, true);
                }
                else
                {
                    Limb(mb, SSkin, wr, tip, hand, fore, hand, t => 0.036f - t * 0.006f + 0.006f * Mathf.Sin(t * Mathf.PI), 0.62f, 1.25f, 0, 1, 4, segs, 0, 1, true);
                    // Thumb.
                    Limb(mb, SSkin, wr + new Vector3(0, -0.03f, 0.022f), wr + new Vector3(-side * 0.0f, -0.1f, 0.05f), hand, hand, hand, t => 0.012f, 1, 1, 0, 1, 2, 6, 0, 1, true);
                }
            }

            // Legs: trousers, pads, shoes.
            foreach (var side in new[] { -1, 1 })
            {
                B thigh = side < 0 ? B.ThighL : B.ThighR, shin = side < 0 ? B.ShinL : B.ShinR, foot = side < 0 ? B.FootL : B.FootR, toe = side < 0 ? B.ToeL : B.ToeR;
                var hip = P(thigh) + new Vector3(0, 0.03f, 0);
                var knee = P(shin);
                var ankle = P(foot);
                Limb(mb, STrousers, hip, knee, thigh, B.Hips, shin, t => (0.088f - t * 0.028f + 0.01f * Mathf.Sin(t * Mathf.PI)) * build, 1, 1.1f, 0, 1, 6, segs, 0, 1);
                Limb(mb, STrousers, knee, ankle + new Vector3(0, 0.04f, 0), shin, thigh, foot, t => (0.058f - t * 0.018f + 0.008f * Mathf.Sin(t * Mathf.PI * 1.3f)) * build, 1, 1.12f, 0, 1, 6, segs, 0, 1);
                if (pads) Pad(mb, side, knee, ankle, thigh, shin, foot);
                Shoe(mb, side, ankle, foot, toe);
            }
            var m = mb.ToMesh("Cricketer");
            var bind = new Matrix4x4[BindPos.Length];
            for (int i = 0; i < bind.Length; i++) bind[i] = Matrix4x4.Translate(-BindPos[i]);
            m.bindposes = bind;
            return m;
        }

        // Head profile: height, half-width, front depth, back depth.
        static readonly float[][] HeadProfile =
        {
            new[] { 1.615f, 0.022f, 0.06f, 0.02f }, new[] { 1.632f, 0.045f, 0.075f, 0.035f }, new[] { 1.655f, 0.06f, 0.083f, 0.055f }, new[] { 1.682f, 0.07f, 0.09f, 0.075f },
            new[] { 1.71f, 0.075f, 0.094f, 0.088f }, new[] { 1.738f, 0.079f, 0.095f, 0.097f }, new[] { 1.765f, 0.08f, 0.092f, 0.1f }, new[] { 1.79f, 0.075f, 0.084f, 0.095f },
            new[] { 1.81f, 0.064f, 0.07f, 0.082f }, new[] { 1.828f, 0.044f, 0.048f, 0.058f }, new[] { 1.84f, 0.012f, 0.013f, 0.016f },
        };

        static void HeadLoft(MeshBuilder mb, int sub, int segs, float scale, float yMin, float yMax, bool hair)
        {
            var rows = new List<float[]>();
            for (int i = 0; i < HeadProfile.Length; i++)
            {
                if (HeadProfile[i][0] >= yMin - 0.001f || (i + 1 < HeadProfile.Length && HeadProfile[i + 1][0] > yMin)) rows.Add(HeadProfile[i]);
            }
            int start = mb.VertexCount;
            for (int r = 0; r < rows.Count; r++)
            {
                float y = Mathf.Max(rows[r][0], yMin), rx = rows[r][1] * scale, rf = rows[r][2] * scale, rb = rows[r][3] * scale;
                if (hair && r == rows.Count - 1) y += 0.006f;
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    float rz = cz > 0 ? rf : rb;
                    float yy = y;
                    // Hairline: the hair sits high at the forehead and low at the nape.
                    if (hair && cz > 0.35f) yy = Mathf.Max(y, Mathf.Lerp(1.77f, 1.792f, (cz - 0.35f) / 0.65f));
                    var p = new Vector3(sx * rx, yy, cz * rz + 0.004f);
                    var n = new Vector3(sx / Mathf.Max(0.01f, rx), 0.35f * (yy - 1.73f) / 0.1f, cz / Mathf.Max(0.01f, rz)).normalized;
                    mb.Add(p, n, new Vector2(s / (float)segs, Mathf.InverseLerp(1.615f, 1.84f, yy)));
                }
            }
            for (int r = 0; r < rows.Count - 1; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            }
        }

        static void Pad(MeshBuilder mb, int side, Vector3 knee, Vector3 ankle, B thigh, B shin, B foot)
        {
            // Curved shell over the front of the shin with vertical rolls; top flap above the knee.
            int cols = 9, rows = 10;
            int start = mb.VertexCount;
            float yTop = knee.y + 0.13f, yBot = ankle.y + 0.02f;
            for (int r = 0; r <= rows; r++)
            {
                float k = r / (float)rows;
                float y = Mathf.Lerp(yBot, yTop, k);
                BoneWeight w = y > knee.y + 0.02f ? W(thigh, 0.7f, shin, 0.3f) : y < ankle.y + 0.08f ? W(shin, 0.7f, foot, 0.3f) : W(shin, 1);
                float rad = 0.074f + (y > knee.y - 0.08f && y < knee.y + 0.06f ? 0.012f : 0);
                for (int c = 0; c <= cols; c++)
                {
                    float a = Mathf.Lerp(-1.9f, 1.9f, c / (float)cols);
                    float roll = 0.006f * Mathf.Abs(Mathf.Sin(c * Mathf.PI / 2));
                    var n = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var p = new Vector3(ankle.x, y, 0.012f) + n * (rad + roll);
                    mb.AddW(p, n, new Vector2(c / (float)cols, k), w);
                }
            }
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i0 = start + r * (cols + 1) + c, i1 = i0 + cols + 1;
                    mb.Quad(SGear, i0, i0 + 1, i1 + 1, i1);
                }
            }
        }

        static void Shoe(MeshBuilder mb, int side, Vector3 ankle, B foot, B toe)
        {
            int segs = 10, rings = 6;
            int start = mb.VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float k = r / (float)rings;
                float z = Mathf.Lerp(-0.07f, 0.19f, k);
                float h = Mathf.Lerp(0.105f, 0.055f, Mathf.Clamp01((k - 0.4f) / 0.6f)) * (k < 0.08f ? 0.9f : 1);
                float wdt = 0.046f * (0.85f + 0.25f * Mathf.Sin(Mathf.Clamp01(k * 1.1f) * Mathf.PI * 0.9f));
                var w = k > 0.65f ? W(toe, 0.6f, foot, 0.4f) : W(foot, 1);
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    float cx = Mathf.Sin(th), cy = Mathf.Cos(th);
                    var p = new Vector3(ankle.x + cx * wdt, Mathf.Max(0.0f, h * 0.5f + cy * h * 0.5f), z);
                    mb.AddW(p, new Vector3(cx, cy, 0).normalized, new Vector2(s / (float)segs, k), w);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(SShoes, i0, i1, i1 + 1, i0 + 1);
                }
            }
            int tip = mb.AddW(new Vector3(ankle.x, 0.03f, 0.205f), Vector3.forward, new Vector2(0.5f, 1), W(toe, 1));
            int last = start + rings * (segs + 1);
            for (int s = 0; s < segs; s++) mb.Tri(SShoes, last + s + 1, last + s, tip);
        }
    }

    /// <summary>Kit textures: shirts with crest, sponsor, trims and the player's name and number on the back.</summary>
    public static class Kits
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Color TrouserColor(TeamDef t)
        {
            var p = Crest.Primary(t);
            var s = Crest.Secondary(t);
            return p.Luma() > 0.55f ? s * 0.9f : p * 0.75f;
        }

        public static void Clear()
        {
            foreach (var kv in Cache) UnityEngine.Object.Destroy(kv.Value);
            Cache.Clear();
        }

        public static Texture2D Shirt(TeamDef t, string name, int number)
        {
            string key = t.Id + name + number;
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            int n = Quality.KitSize;
            var p = Crest.Primary(t);
            var trim = Crest.Trim(t);
            var dark = p * 0.62f;
            dark.a = 1;
            var text = p.Luma() > 0.6f ? Crest.Secondary(t) : Color.white;
            tex = TextBake.Render(n, n, p, area =>
            {
                float s = n / 256f;
                // Sleeve strip at the very bottom: body colour with a trim cuff at the end.
                var sleeve = Ui.Img(area, null, Color.Lerp(p, dark, 0.35f), "Sleeve");
                sleeve.rectTransform.anchorMin = new Vector2(0, 0);
                sleeve.rectTransform.anchorMax = new Vector2(1, 0.066f);
                sleeve.rectTransform.offsetMin = sleeve.rectTransform.offsetMax = Vector2.zero;
                var cuff = Ui.Img(area, null, trim, "Cuff");
                cuff.rectTransform.anchorMin = new Vector2(0, 0.05f);
                cuff.rectTransform.anchorMax = new Vector2(1, 0.066f);
                cuff.rectTransform.offsetMin = cuff.rectTransform.offsetMax = Vector2.zero;
                // Side panels.
                foreach (float u in new[] { 0.25f, 0.75f })
                {
                    var side = Ui.Img(area, null, dark, "Side");
                    side.rectTransform.anchorMin = new Vector2(u - 0.035f, 0.065f);
                    side.rectTransform.anchorMax = new Vector2(u + 0.035f, 1);
                    side.rectTransform.offsetMin = side.rectTransform.offsetMax = Vector2.zero;
                    var line = Ui.Img(area, null, trim, "SideLine");
                    line.rectTransform.anchorMin = new Vector2(u - 0.006f, 0.065f);
                    line.rectTransform.anchorMax = new Vector2(u + 0.006f, 1);
                    line.rectTransform.offsetMin = line.rectTransform.offsetMax = Vector2.zero;
                }
                // Shoulder yoke.
                var yoke = Ui.Img(area, null, dark, "Yoke");
                yoke.rectTransform.anchorMin = new Vector2(0, 0.86f);
                yoke.rectTransform.anchorMax = new Vector2(1, 1);
                yoke.rectTransform.offsetMin = yoke.rectTransform.offsetMax = Vector2.zero;
                var yl = Ui.Img(area, null, trim, "YokeLine");
                yl.rectTransform.anchorMin = new Vector2(0, 0.855f);
                yl.rectTransform.anchorMax = new Vector2(1, 0.868f);
                yl.rectTransform.offsetMin = yl.rectTransform.offsetMax = Vector2.zero;
                // Diagonal sash across the front (wraps at u = 0 / 1).
                foreach (float x in new[] { 0f, 1f })
                {
                    var sash = Ui.Img(area, null, trim.A(0.85f), "Sash");
                    sash.rectTransform.anchorMin = sash.rectTransform.anchorMax = new Vector2(x, 0.5f);
                    sash.rectTransform.sizeDelta = new Vector2(16 * s, 260 * s);
                    sash.rectTransform.localRotation = Quaternion.Euler(0, 0, 38);
                }
                // Front wordmark (split across the seam).
                foreach (float x in new[] { 0f, 1f })
                {
                    var word = Ui.Label(area, "KPL", (int)(30 * s), text, TextAnchor.MiddleCenter, Font.Display);
                    word.rectTransform.anchorMin = word.rectTransform.anchorMax = new Vector2(x, 0.62f);
                    word.rectTransform.sizeDelta = new Vector2(80 * s, 34 * s);
                }
                // Chest crest on the player's left (u ~ 0.91).
                var crest = Crest.Add(area, t, 30 * s, false);
                crest.anchorMin = crest.anchorMax = new Vector2(0.905f, 0.74f);
                crest.anchoredPosition = Vector2.zero;
                // Back: name and number.
                var nm = Ui.Label(area, name.ToUpperInvariant(), (int)(20 * s), text, TextAnchor.MiddleCenter, Font.Bold);
                nm.rectTransform.anchorMin = nm.rectTransform.anchorMax = new Vector2(0.5f, 0.75f);
                nm.rectTransform.sizeDelta = new Vector2(110 * s, 24 * s);
                var num = Ui.Label(area, number.ToString(), (int)(64 * s), text, TextAnchor.MiddleCenter, Font.Display);
                num.rectTransform.anchorMin = num.rectTransform.anchorMax = new Vector2(0.5f, 0.47f);
                num.rectTransform.sizeDelta = new Vector2(110 * s, 72 * s);
                var outline = num.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = trim;
                outline.effectDistance = new Vector2(1.5f * s, -1.5f * s);
            });
            tex.wrapMode = TextureWrapMode.Repeat;
            Cache[key] = tex;
            return tex;
        }
    }
}
