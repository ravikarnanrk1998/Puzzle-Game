using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    public enum ZB { Root, Hips, Spine, Chest, Neck, Head, Jaw, ClavL, ArmL, ForeL, HandL, ClavR, ArmR, ForeR, HandR, ThighL, ShinL, FootL, ThighR, ShinR, FootR }

    /// <summary>Mesh-shaping parameters of a zombie variant.</summary>
    public sealed class ZShape
    {
        public float Build = 1, Gaunt, Belly, Muscle, BigArm, LegLen = 1, ArmLen = 1;
        public int Shirt = 1, Pants, Hair, Shoes = 1;
        public bool Helmet, Riot, Vest, GasMask, Claws, Spikes, Apron, Cloak, Cleaver, Sacs, Plates, Pustules, Legless;
        /// <summary>Strapped explosive charges on the vest; a hood over the head (stalkers).</summary>
        public bool Charges, Hood;

        public string Key => string.Join("_", Build.ToString("0.00"), Gaunt.ToString("0.0"), Belly.ToString("0.0"), Muscle.ToString("0.0"), BigArm.ToString("0.0"), LegLen, ArmLen, Shirt, Pants, Hair, Shoes,
            Helmet, Riot, Vest, GasMask, Claws, Spikes, Apron, Cloak, Cleaver, Sacs, Plates, Pustules, Legless, Charges, Hood, Quality.ZombieSegs);
    }

    /// <summary>Material choices of a zombie variant.</summary>
    public sealed class ZLook
    {
        public int Tone, Seed, ShirtKind, PantsKind;
        public float Decay = 0.5f, Tatter = 0.5f;
        public Color ShirtCol = Color.gray, PantsCol = Color.gray, ShoeCol = new Color(0.12f, 0.1f, 0.09f), GearCol = new Color(0.1f, 0.11f, 0.12f), Gear2Col = new Color(0.3f, 0.3f, 0.3f), HairCol = new Color(0.08f, 0.06f, 0.05f);
        public Color EyeGlow = new Color(1.2f, 1.1f, 0.6f);
        public bool FleshSkin;
        public float Gear2Metal = 0.3f;
        /// <summary>Glowing vein emission on the skin (toxic infected); black = none.</summary>
        public Color SkinGlow = Color.black;
        /// <summary>Living human (playable characters): human skin tone index and face instead of infected ones.</summary>
        public bool Human, Beard;
        public int HumanTone;

        public string Key => string.Join("_", Tone, Seed % 7, ShirtKind, PantsKind, Decay, Tatter, ShirtCol, PantsCol, EyeGlow, FleshSkin, GearCol, Gear2Col, SkinGlow, Human, Beard, HumanTone, HairCol);
    }

    /// <summary>
    /// Procedural skinned zombie: a 21-bone skeleton, skin and torn clothing layers, a moving jaw,
    /// glowing eyes and type-specific gear. Meshes and materials are cached per variant.
    /// </summary>
    public sealed class ZombieRig : MonoBehaviour
    {
        public Transform[] Bones;
        public SkinnedMeshRenderer Skin;
        public ZShape Shape;
        public ZLook LookDef;
        public Material[] Materials;
        public float Height = 1.75f;
        /// <summary>Imported body following this rig (null = the procedural mesh is shown).</summary>
        public ArtBody Art;
        Material[] _cloakMats;

        public static ZB ParentOf(ZB b) => Parent[(int)b];

        /// <summary>Whether any of the visible body's renderers was seen by a camera last frame.</summary>
        public bool IsVisible => Art != null ? Art.IsVisible : Skin.isVisible;

        public void SetVisible(bool v)
        {
            if (Art != null) { Art.SetVisible(v); Skin.enabled = false; }
            else Skin.enabled = v;
        }

        /// <summary>Restores normal materials and visibility (on spawn from the pool).</summary>
        public void ResetLook()
        {
            if (Art != null) Art.SetCloak(false, null);
            else if (Skin.sharedMaterials[0] != Materials[0]) Skin.sharedMaterials = Materials;
            SetVisible(true);
        }

        /// <summary>Shimmering near-invisible look (Hunter / Wraith); the eyes keep glowing.</summary>
        public void SetCloak(bool on)
        {
            var shimmer = Mats.Cached("cloak", () => Mats.Transparent(new Color(0.55f, 0.6f, 0.7f, 0.08f), 0.98f));
            if (Art != null) { Art.SetCloak(on, shimmer); return; }
            if (_cloakMats == null)
            {
                _cloakMats = new Material[Materials.Length];
                for (int i = 0; i < _cloakMats.Length; i++) _cloakMats[i] = shimmer;
                _cloakMats[6] = Materials[6];
            }
            var want = on ? _cloakMats : Materials;
            if (Skin.sharedMaterials[0] != want[0]) Skin.sharedMaterials = want;
        }

        public void SetRenderingLayer(uint mask)
        {
            Skin.renderingLayerMask = mask;
            if (Art != null) Art.SetRenderingLayer(mask);
        }

        /// <summary>Attaches an imported body (Resources/UAArt/&lt;key&gt;) if one exists; the procedural mesh is then hidden.</summary>
        void AttachArt(string key)
        {
            var prefab = ArtLibrary.Get(key);
            if (prefab == null) return;
            var go = Instantiate(prefab, transform, false);
            var art = go.GetComponent<ArtBody>();
            if (art == null || art.Bones == null || art.Bones.Length != Bones.Length) { Destroy(go); return; }
            Art = art;
            Art.Bind(this, gameObject.layer);
            ArtGear.Attach(Art, this);
            // Legless crawlers: the imported lower legs fold into the knee (the game skeleton keeps its shins).
            if (Shape != null && Shape.Legless)
            {
                Art.SetBoneScale(ZB.ShinL, Vector3.one * 0.02f);
                Art.SetBoneScale(ZB.ShinR, Vector3.one * 0.02f);
            }
            Skin.enabled = false;
        }

        /// <summary>Imported-art key with variety: "&lt;group&gt;/&lt;name&gt;_&lt;n&gt;" when that variant exists, else the base key.</summary>
        public static string ArtVariant(string baseKey, int seed, int variants)
        {
            if (variants <= 1) return baseKey;
            int v = ((seed % variants) + variants) % variants;
            if (v == 0) return baseKey;
            string key = baseKey + "_" + v;
            return ArtLibrary.Get(key) != null ? key : baseKey;
        }

        static readonly ZB[] Parent =
        {
            ZB.Root, ZB.Root, ZB.Hips, ZB.Spine, ZB.Chest, ZB.Neck, ZB.Head,
            ZB.Chest, ZB.ClavL, ZB.ArmL, ZB.ForeL, ZB.Chest, ZB.ClavR, ZB.ArmR, ZB.ForeR,
            ZB.Hips, ZB.ThighL, ZB.ShinL, ZB.Hips, ZB.ThighR, ZB.ShinR,
        };

        public static readonly Vector3[] BindPos =
        {
            new Vector3(0, 0, 0), new Vector3(0, 0.97f, 0), new Vector3(0, 1.1f, 0), new Vector3(0, 1.3f, 0), new Vector3(0, 1.5f, 0), new Vector3(0, 1.6f, 0), new Vector3(0, 1.64f, 0.02f),
            new Vector3(-0.03f, 1.44f, 0), new Vector3(-0.19f, 1.44f, 0), new Vector3(-0.19f, 1.15f, 0), new Vector3(-0.19f, 0.89f, 0),
            new Vector3(0.03f, 1.44f, 0), new Vector3(0.19f, 1.44f, 0), new Vector3(0.19f, 1.15f, 0), new Vector3(0.19f, 0.89f, 0),
            new Vector3(-0.1f, 0.94f, 0), new Vector3(-0.1f, 0.52f, 0), new Vector3(-0.1f, 0.09f, 0),
            new Vector3(0.1f, 0.94f, 0), new Vector3(0.1f, 0.52f, 0), new Vector3(0.1f, 0.09f, 0),
        };

        public Transform this[ZB b] => Bones[(int)b];

        const int SSkin = 0, SFace = 1, SShirt = 2, SPants = 3, SShoes = 4, SGear = 5, SEyes = 6, SHair = 7, SGear2 = 8;

        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material[]> MatCache = new Dictionary<string, Material[]>();

        public static void ClearCaches()
        {
            MeshCache.Clear();
            MatCache.Clear();
        }

        /// <summary>Drops cached materials (their surface textures are freed between stages).</summary>
        public static void ClearMaterials()
        {
            foreach (var mats in MatCache.Values)
                foreach (var m in mats)
                    if (m != null) Destroy(m);
            MatCache.Clear();
        }

        public static ZombieRig Create(Transform parent, ZShape shape, ZLook look, string artKey = null)
        {
            var go = new GameObject("Zombie");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<ZombieRig>();
            rig.Shape = shape;
            rig.LookDef = look;
            rig.Build();
            if (artKey != null) rig.AttachArt(artKey);
            return rig;
        }

        void Build()
        {
            Bones = new Transform[BindPos.Length];
            for (int i = 0; i < BindPos.Length; i++) Bones[i] = i == 0 ? transform : new GameObject(((ZB)i).ToString()).transform;
            for (int i = 1; i < BindPos.Length; i++)
            {
                Bones[i].SetParent(Bones[(int)Parent[i]], false);
                Bones[i].localPosition = BindPos[i] - BindPos[(int)Parent[i]];
                Bones[i].localRotation = Quaternion.identity;
            }
            string key = Shape.Key;
            if (!MeshCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = BuildMesh(Shape);
                MeshCache[key] = mesh;
            }
            var skinGo = new GameObject("Skin");
            skinGo.transform.SetParent(transform, false);
            Skin = skinGo.AddComponent<SkinnedMeshRenderer>();
            Skin.sharedMesh = mesh;
            Skin.bones = Bones;
            Skin.rootBone = Bones[(int)ZB.Hips];
            Skin.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(2.8f, 3.2f, 2.8f));
            Skin.quality = Quality.Current == Gfx.Low ? SkinQuality.Bone2 : SkinQuality.Bone4;
            Skin.updateWhenOffscreen = false;
            Skin.shadowCastingMode = Quality.Current == Gfx.Low ? ShadowCastingMode.Off : ShadowCastingMode.On;
            Materials = MaterialsFor(LookDef, Shape);
            Skin.sharedMaterials = Materials;
            Height = 1.75f;
        }

        // ------------------------------------------------------------------ materials

        static Material[] MaterialsFor(ZLook l, ZShape s)
        {
            string key = l.Key;
            if (MatCache.TryGetValue(key, out var mats) && mats[0] != null) return mats;
            Texture2D skinN;
            Texture2D skinTex = l.Human ? ZombieTex.HumanSkin(l.HumanTone, l.Seed % 5, out skinN)
                : l.FleshSkin ? ZombieTex.Flesh(l.Seed, out skinN) : ZombieTex.Skin(l.Tone, l.Decay, l.Seed % 5, out skinN);
            var skinSurf = new Surface { Albedo = skinTex, Normal = skinN, Smooth = 0.38f, Bump = 1 };
            var skin = l.SkinGlow.maxColorComponent > 0.01f
                ? Mats.EmissiveMap(skinSurf, Color.white, ZombieTex.Veins(l.Seed % 5), l.SkinGlow)
                : Mats.For(skinSurf, Color.white);
            var faceTex = l.Human ? ZombieTex.HumanFace(l.HumanTone, l.Seed % 5, l.Beard, l.HairCol) : ZombieTex.Face(l.Tone, l.Decay, l.Seed % 5, s.GasMask);
            var face = Mats.For(new Surface { Albedo = faceTex, Normal = skinN, Smooth = 0.42f }, Color.white);
            var shirt = Mats.Cutout(Color.white, ZombieTex.Cloth(l.ShirtCol, l.ShirtKind, l.Tatter, l.Seed % 9), 0.5f, l.ShirtKind == 6 ? 0.45f : 0.15f);
            var pants = Mats.Cutout(Color.white, ZombieTex.Cloth(l.PantsCol, l.PantsKind, l.Tatter * 0.8f, l.Seed % 9 + 3), 0.5f, 0.12f);
            var shoes = Mats.Lit(l.ShoeCol, 0.3f);
            var gear = Mats.For(Tex.PaintedMetal(l.GearCol, 200 + l.Seed % 3, 0.25f), Color.white);
            var eyes = Mats.Emissive(new Color(0.9f, 0.85f, 0.6f), l.EyeGlow);
            var hair = Mats.Lit(l.HairCol, 0.25f);
            var gear2 = Mats.Lit(l.Gear2Col, 0.6f, l.Gear2Metal);
            mats = new[] { skin, face, shirt, pants, shoes, gear, eyes, hair, gear2 };
            MatCache[key] = mats;
            return mats;
        }

        // ------------------------------------------------------------------ mesh

        static BoneWeight W(ZB a, float wa, ZB b = ZB.Root, float wb = 0)
        {
            if (wb <= 0.0001f) return new BoneWeight { boneIndex0 = (int)a, weight0 = 1 };
            float s = wa + wb;
            return new BoneWeight { boneIndex0 = (int)a, weight0 = wa / s, boneIndex1 = (int)b, weight1 = wb / s };
        }

        /// <summary>Tube along a bone with weights blended into the parent and child at the ends.</summary>
        static void Limb(MeshBuilder mb, int sub, Vector3 a, Vector3 b, ZB bone, ZB parent, ZB child, Func<float, float> radius, float t0, float t1, int rings, int segs, bool capEnd = false, float rz = 1)
        {
            var axis = (b - a).normalized;
            var fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.7f)
            {
                fwd = Vector3.up;
                right = Vector3.Cross(axis, fwd).normalized;
            }
            bool flip = Vector3.Dot(Vector3.Cross(axis, right), fwd) > 0;
            int start = mb.VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float t = Mathf.Lerp(t0, t1, r / (float)rings);
                var c = Vector3.Lerp(a, b, t);
                float rad = radius(t);
                BoneWeight w;
                if (t < 0.18f && parent != bone) w = W(bone, 0.5f + t / 0.36f, parent, 0.5f - t / 0.36f);
                else if (t > 0.82f && child != bone) w = W(bone, 0.5f + (1 - t) / 0.36f, child, 0.5f - (1 - t) / 0.36f);
                else w = W(bone, 1);
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    var n = (fwd * Mathf.Cos(th) * rz + right * Mathf.Sin(th)).normalized;
                    var p = c + fwd * (Mathf.Cos(th) * rad * rz) + right * (Mathf.Sin(th) * rad);
                    mb.AddW(p, n, new Vector2(s / (float)segs, Mathf.Lerp(0, 1, r / (float)rings)), w);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    if (flip) mb.Quad(sub, i0, i1, i1 + 1, i0 + 1);
                    else mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            if (capEnd)
            {
                var c = Vector3.Lerp(a, b, t1) + axis * radius(t1) * 0.6f;
                int ci = mb.AddW(c, axis, new Vector2(0.5f, 1), W(child != bone && t1 > 0.82f ? child : bone, 1));
                int last = start + rings * (segs + 1);
                for (int s = 0; s < segs; s++)
                {
                    if (flip) mb.Tri(sub, last + s + 1, last + s, ci);
                    else mb.Tri(sub, last + s, last + s + 1, ci);
                }
            }
        }

        static Vector3 P(ZB b) => BindPos[(int)b];

        static Mesh BuildMesh(ZShape sh)
        {
            var mb = new MeshBuilder { UseWeights = true };
            int segs = Quality.ZombieSegs;
            float bw = sh.Build * (1 - sh.Gaunt * 0.25f);
            // ---- torso (skin layer, then shirt layer slightly larger)
            float[][] prof =
            {
                new[] { 0.86f, 0.13f, 0.095f, 0f }, new[] { 0.93f, 0.16f, 0.11f, 0f }, new[] { 1.0f, 0.155f, 0.105f, 0f }, new[] { 1.08f, 0.14f, 0.1f, 0.006f },
                new[] { 1.16f, 0.145f, 0.103f, 0.01f }, new[] { 1.25f, 0.16f, 0.112f, 0.015f }, new[] { 1.34f, 0.17f, 0.115f, 0.012f }, new[] { 1.4f, 0.168f, 0.105f, 0.005f },
                new[] { 1.44f, 0.15f, 0.092f, 0.002f }, new[] { 1.47f, 0.115f, 0.078f, 0f }, new[] { 1.5f, 0.07f, 0.06f, 0f },
            };
            foreach (var row in prof)
            {
                float y = row[0];
                float belly = sh.Belly * Mathf.Clamp01(1 - Mathf.Abs(y - 1.15f) / 0.22f);
                float chest = sh.Muscle * Mathf.Clamp01(1 - Mathf.Abs(y - 1.36f) / 0.15f);
                row[1] *= bw * (1 + belly * 0.9f + chest * 0.6f);
                row[2] *= bw * (1 + belly * 1.5f + chest * 0.4f) * (1 - sh.Gaunt * 0.15f);
                row[3] += belly * 0.08f;
            }
            Torso(mb, prof, SSkin, segs, 1f, false);
            if (sh.Shirt > 0 || sh.Riot) Torso(mb, prof, SShirt, segs, 1.06f, sh.Shirt == 3);
            // Pants waist.
            if (sh.Pants < 2) Torso(mb, new[] { prof[0], prof[1], prof[2], prof[3] }, SPants, segs, 1.08f, false);
            // Crotch cap.
            {
                int start = mb.VertexCount - 0;
                int c = mb.AddW(new Vector3(0, 0.82f, 0), Vector3.down, new Vector2(0.5f, 0), W(ZB.Hips, 1));
                int ring = mb.VertexCount;
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    mb.AddW(new Vector3(Mathf.Sin(th) * prof[0][1] * 1.08f, 0.86f, Mathf.Cos(th) * prof[0][2] * 1.08f), Vector3.down, new Vector2(s / (float)segs, 0), W(ZB.Hips, 1));
                }
                for (int s = 0; s < segs; s++) mb.Tri(sh.Pants < 2 ? SPants : SSkin, ring + s + 1, ring + s, c);
            }

            // ---- neck and head
            Limb(mb, SSkin, new Vector3(0, 1.46f, 0), new Vector3(0, 1.64f, 0.01f), ZB.Neck, ZB.Chest, ZB.Head, t => 0.048f * bw * (1 + sh.Muscle * 0.6f), 0, 1, 3, segs);
            HeadLoft(mb, SFace, segs + 4, 1f + sh.Muscle * 0.08f);
            // Inner mouth (samples the dark mouth area of the face texture).
            mb.Weight = W(ZB.Head, 1);
            int ms = mb.VertexCount;
            mb.Sphere(SFace, new Vector3(0, 1.665f, 0.035f), new Vector3(0.045f, 0.03f, 0.05f), 6, 4);
            mb.Weight = W(ZB.Jaw, 1);
            // Eyes.
            mb.Weight = W(ZB.Head, 1);
            foreach (var ex in new[] { -0.033f, 0.033f }) mb.Sphere(SEyes, new Vector3(ex, 1.72f, 0.086f), new Vector3(0.013f, 0.009f, 0.007f), 6, 4);
            // Hair.
            if (sh.Hair > 0 && !sh.Helmet) HairCap(mb, segs + 4, sh.Hair);

            // ---- arms
            foreach (var side in new[] { -1, 1 })
            {
                ZB arm = side < 0 ? ZB.ArmL : ZB.ArmR, fore = side < 0 ? ZB.ForeL : ZB.ForeR, hand = side < 0 ? ZB.HandL : ZB.HandR, clav = side < 0 ? ZB.ClavL : ZB.ClavR;
                float big = side > 0 ? 1 + sh.BigArm : 1;
                float ar = bw * (1 - sh.Gaunt * 0.3f) * (1 + sh.Muscle * 0.5f) * big;
                var shPos = P(arm) + new Vector3(0, 0.03f, 0);
                var el = P(fore);
                var wr = P(hand);
                Limb(mb, SSkin, shPos, el, arm, clav, fore, t => (0.052f - t * 0.012f) * ar, 0, 1, 5, segs);
                Limb(mb, SSkin, el, wr, fore, arm, hand, t => (0.045f - t * 0.014f) * ar, 0, 1, 5, segs);
                if (sh.Shirt == 1 || sh.Shirt == 2) Limb(mb, SShirt, shPos, el, arm, clav, fore, t => (0.058f - t * 0.01f) * ar * 1.05f, 0, sh.Shirt == 2 ? 1f : 0.45f, 4, segs);
                if (sh.Shirt == 2) Limb(mb, SShirt, el, wr, fore, arm, hand, t => (0.05f - t * 0.01f) * ar * 1.05f, 0, 0.85f, 4, segs);
                // Hand: palm + curled fingers.
                var tip = wr + new Vector3(0, -0.17f * big, 0.01f);
                Limb(mb, SSkin, wr, tip, hand, fore, hand, t => (0.034f - t * 0.008f + 0.006f * Mathf.Sin(t * Mathf.PI)) * big * bw, 0, 1, 4, Math.Max(6, segs - 4), true, 0.6f);
                if (sh.Claws)
                {
                    mb.Weight = W(hand, 1);
                    for (int f = 0; f < 3; f++)
                    {
                        float fx = wr.x + (f - 1) * 0.022f;
                        mb.Cylinder(SGear2, new Vector3(fx, wr.y - 0.14f, 0.01f), new Vector3(fx + side * 0.01f, wr.y - 0.36f, 0.06f), 0.008f, 0.001f, 5, false, false);
                    }
                }
                if (sh.Riot || sh.Plates)
                {
                    mb.Weight = W(arm, 1);
                    mb.RoundBox(SGear, shPos + new Vector3(side * 0.02f, -0.04f, 0), new Vector3(0.14f, 0.14f, 0.15f) * ar * 1.4f, 0.03f, 1);
                }
                if (sh.Spikes)
                {
                    mb.Weight = W(clav, 1);
                    for (int k = 0; k < 3; k++)
                        mb.Cylinder(SGear2, shPos + new Vector3(side * 0.02f, 0.02f, -0.03f + k * 0.03f), shPos + new Vector3(side * (0.08f + k * 0.02f), 0.16f + k * 0.03f, -0.05f), 0.02f, 0.002f, 5, false, false);
                }
            }
            if (sh.Cleaver)
            {
                mb.Weight = W(ZB.HandR, 1);
                var h = P(ZB.HandR);
                mb.Cylinder(SGear2, h + new Vector3(0, -0.08f, 0.02f), h + new Vector3(0, -0.2f, 0.02f), 0.018f, 0.018f, 6);
                mb.Box(SGear2, h + new Vector3(0, -0.42f, 0.09f), new Vector3(0.012f, 0.42f, 0.2f));
            }

            // ---- legs
            foreach (var side in new[] { -1, 1 })
            {
                ZB thigh = side < 0 ? ZB.ThighL : ZB.ThighR, shin = side < 0 ? ZB.ShinL : ZB.ShinR, foot = side < 0 ? ZB.FootL : ZB.FootR;
                float lr = bw * (1 - sh.Gaunt * 0.25f) * (1 + sh.Muscle * 0.35f);
                var hip = P(thigh) + new Vector3(0, 0.03f, 0);
                var knee = P(shin);
                var ankle = P(foot);
                Limb(mb, SSkin, hip, knee, thigh, ZB.Hips, shin, t => (0.085f - t * 0.028f + 0.01f * Mathf.Sin(t * Mathf.PI)) * lr, 0, 1, 5, segs);
                if (sh.Legless)
                {
                    // Torn stump just below the knee.
                    Limb(mb, SSkin, knee, Vector3.Lerp(knee, ankle, 0.25f), shin, thigh, shin, t => 0.055f * lr, 0, 1, 2, segs, true);
                    continue;
                }
                Limb(mb, SSkin, knee, ankle + new Vector3(0, 0.04f, 0), shin, thigh, foot, t => (0.056f - t * 0.018f + 0.008f * Mathf.Sin(t * Mathf.PI * 1.3f)) * lr, 0, 1, 5, segs);
                if (sh.Pants == 0)
                {
                    Limb(mb, SPants, hip, knee, thigh, ZB.Hips, shin, t => (0.092f - t * 0.026f + 0.01f * Mathf.Sin(t * Mathf.PI)) * lr * 1.05f, 0, 1, 5, segs);
                    Limb(mb, SPants, knee, ankle + new Vector3(0, 0.06f, 0), shin, thigh, foot, t => (0.066f - t * 0.012f) * lr * 1.06f, 0, 1, 5, segs);
                }
                else if (sh.Pants == 1) Limb(mb, SPants, hip, knee, thigh, ZB.Hips, shin, t => (0.094f - t * 0.024f) * lr * 1.05f, 0, 0.6f, 4, segs);
                if (sh.Riot || sh.Plates)
                {
                    mb.Weight = W(shin, 1);
                    mb.RoundBox(SGear, (knee + ankle) / 2 + new Vector3(0, 0.05f, 0.055f), new Vector3(0.1f, 0.3f, 0.04f) * lr * 1.2f, 0.015f, 1);
                }
                Foot(mb, ankle, foot, sh.Shoes > 0 ? SShoes : SSkin);
            }

            // ---- gear
            if (sh.Helmet)
            {
                mb.Weight = W(ZB.Head, 1);
                mb.Lathe(SGear, new[] { new Vector2(0.115f, 1.71f), new Vector2(0.118f, 1.76f), new Vector2(0.105f, 1.81f), new Vector2(0.07f, 1.85f), new Vector2(0.001f, 1.865f) }, 12);
                if (sh.Riot) mb.Box(SGear2, new Vector3(0, 1.72f, 0.11f), new Vector3(0.2f, 0.09f, 0.01f));
            }
            if (sh.Riot)
            {
                mb.Weight = W(ZB.Chest, 1);
                mb.RoundBox(SGear, new Vector3(0, 1.3f, 0.1f * bw), new Vector3(0.34f * bw, 0.34f, 0.07f), 0.03f, 1);
                mb.RoundBox(SGear, new Vector3(0, 1.3f, -0.1f * bw), new Vector3(0.32f * bw, 0.32f, 0.06f), 0.03f, 1);
            }
            if (sh.Vest)
            {
                mb.Weight = W(ZB.Chest, 1);
                mb.RoundBox(SGear, new Vector3(0, 1.28f, 0.0f), new Vector3(0.36f * bw, 0.32f, 0.27f * bw), 0.04f, 1);
                for (int k = 0; k < 3; k++) mb.RoundBox(SGear, new Vector3(-0.1f + k * 0.1f, 1.18f, 0.14f * bw), new Vector3(0.08f, 0.1f, 0.05f), 0.015f, 1);
            }
            if (sh.Charges)
            {
                // Explosive charges strapped across the vest, a detonator box and its blinking light.
                mb.Weight = W(ZB.Chest, 1);
                float cz = 0.155f * bw;
                for (int k = 0; k < 4; k++)
                {
                    float cx = -0.12f + k * 0.08f;
                    mb.Cylinder(SGear2, new Vector3(cx, 1.12f, cz), new Vector3(cx, 1.33f, cz), 0.028f, 0.028f, 8);
                }
                mb.Box(SShoes, new Vector3(0, 1.31f, cz + 0.012f), new Vector3(0.32f, 0.025f, 0.05f));
                mb.Box(SShoes, new Vector3(0, 1.15f, cz + 0.012f), new Vector3(0.32f, 0.025f, 0.05f));
                mb.RoundBox(SShoes, new Vector3(0.07f, 1.41f, cz + 0.015f), new Vector3(0.08f, 0.055f, 0.04f), 0.01f, 1);
                mb.Sphere(SEyes, new Vector3(0.07f, 1.42f, cz + 0.04f), Vector3.one * 0.013f, 6, 4);
            }
            if (sh.Hood)
            {
                // Cowl over the skull with a peak over the brow, draped onto the shoulders.
                mb.Weight = W(ZB.Head, 1);
                mb.Sphere(SShirt, new Vector3(0, 1.73f, -0.045f), new Vector3(0.135f, 0.15f, 0.14f), 12, 8);
                mb.RoundBox(SShirt, new Vector3(0, 1.83f, 0.055f), new Vector3(0.2f, 0.05f, 0.12f), 0.02f, 1);
                mb.Weight = W(ZB.Neck, 0.5f, ZB.Chest, 0.5f);
                mb.Sphere(SShirt, new Vector3(0, 1.5f, -0.02f), new Vector3(0.2f * bw, 0.1f, 0.15f * bw), 10, 6);
            }
            if (sh.GasMask)
            {
                mb.Weight = W(ZB.Head, 1);
                mb.RoundBox(SGear2, new Vector3(0, 1.66f, 0.09f), new Vector3(0.12f, 0.09f, 0.06f), 0.025f, 1);
                mb.Cylinder(SGear2, new Vector3(0.03f, 1.64f, 0.12f), new Vector3(0.06f, 1.62f, 0.17f), 0.03f, 0.03f, 8);
            }
            if (sh.Spikes)
            {
                mb.Weight = W(ZB.Chest, 1);
                for (int k = 0; k < 5; k++)
                {
                    var b = new Vector3(0, 1.22f + k * 0.06f, -0.1f * bw);
                    mb.Cylinder(SGear2, b, b + new Vector3(0, 0.05f, -0.12f - k * 0.01f), 0.025f, 0.002f, 5, false, false);
                }
            }
            if (sh.Pustules || sh.Sacs)
            {
                var r = new System.Random(sh.Key.GetHashCode());
                int n = sh.Sacs ? 8 : 6;
                for (int k = 0; k < n; k++)
                {
                    float a = (float)r.NextDouble() * Mathf.PI * (sh.Sacs ? 1f : 1.2f) + (sh.Sacs ? Mathf.PI * 0.5f : -Mathf.PI * 0.1f);
                    float y = 1.0f + (float)r.NextDouble() * 0.35f;
                    float rad = 0.16f * bw * (1 + sh.Belly);
                    mb.Weight = W(y > 1.2f ? ZB.Chest : ZB.Spine, 1);
                    var c = new Vector3(Mathf.Sin(a) * rad, y, Mathf.Cos(a) * rad * (sh.Sacs ? -1 : 1) + (sh.Sacs ? -0.02f : 0.06f));
                    float sz = 0.03f + (float)r.NextDouble() * (sh.Sacs ? 0.06f : 0.03f);
                    mb.Sphere(SEyes, c, Vector3.one * sz, 7, 5);
                }
            }
            if (sh.Plates)
            {
                mb.Weight = W(ZB.Chest, 1);
                mb.RoundBox(SGear, new Vector3(0, 1.33f, 0.12f * bw), new Vector3(0.38f * bw, 0.3f, 0.08f), 0.04f, 1);
                mb.RoundBox(SGear, new Vector3(0, 1.3f, -0.13f * bw), new Vector3(0.42f * bw, 0.38f, 0.08f), 0.04f, 1);
                mb.Weight = W(ZB.Hips, 1);
                mb.RoundBox(SGear, new Vector3(0, 0.98f, 0.11f * bw), new Vector3(0.32f * bw, 0.16f, 0.07f), 0.03f, 1);
                // Glowing weak points (back and chest seam).
                mb.Weight = W(ZB.Chest, 1);
                mb.Sphere(SEyes, new Vector3(0, 1.3f, -0.18f * bw), Vector3.one * 0.05f, 8, 6);
                mb.Sphere(SEyes, new Vector3(0.12f, 1.42f, -0.16f * bw), Vector3.one * 0.035f, 8, 6);
                mb.Sphere(SEyes, new Vector3(-0.12f, 1.42f, -0.16f * bw), Vector3.one * 0.035f, 8, 6);
            }
            if (sh.Apron)
            {
                // Leather apron panel from chest to knees.
                int start = mb.VertexCount;
                for (int r = 0; r <= 6; r++)
                {
                    float y = Mathf.Lerp(1.42f, 0.55f, r / 6f);
                    ZB bone = y > 1.2f ? ZB.Chest : y > 1.0f ? ZB.Spine : y > 0.85f ? ZB.Hips : ZB.Hips;
                    var w = y < 0.85f ? W(ZB.Hips, 0.6f, ZB.ThighR, 0.2f) : W(bone, 1);
                    float hw = (y > 1.2f ? 0.12f : 0.17f) * bw;
                    float z = (y > 1.0f ? 0.13f : 0.16f) * bw * (1 + sh.Belly * 0.8f);
                    for (int c = 0; c <= 4; c++)
                    {
                        float u = c / 4f;
                        mb.AddW(new Vector3(Mathf.Lerp(-hw, hw, u), y, z + Mathf.Sin(u * Mathf.PI) * 0.03f), Vector3.forward, new Vector2(u, 1 - r / 6f), w);
                    }
                }
                for (int r = 0; r < 6; r++)
                    for (int c = 0; c < 4; c++)
                    {
                        int i0 = start + r * 5 + c, i1 = i0 + 5;
                        mb.Quad(SShirt, i0, i1, i1 + 1, i0 + 1);
                    }
            }
            if (sh.Cloak)
            {
                // Tattered cape from the shoulders down the back.
                int start = mb.VertexCount;
                int cols = 8;
                for (int r = 0; r <= 8; r++)
                {
                    float k = r / 8f;
                    float y = Mathf.Lerp(1.46f, 0.35f, k);
                    var w = y > 1.2f ? W(ZB.Chest, 1) : y > 0.95f ? W(ZB.Spine, 0.6f, ZB.Hips, 0.4f) : W(ZB.Hips, 1);
                    for (int c = 0; c <= cols; c++)
                    {
                        float a = Mathf.Lerp(-1.6f, 1.6f, c / (float)cols);
                        float rad = Mathf.Lerp(0.2f, 0.34f, k) * bw;
                        mb.AddW(new Vector3(Mathf.Sin(a) * rad, y, -Mathf.Cos(a) * rad * 0.8f - 0.02f), new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)), new Vector2(c / (float)cols, 1 - k), w);
                    }
                }
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int i0 = start + r * (cols + 1) + c, i1 = i0 + cols + 1;
                        mb.Quad(SShirt, i0, i0 + 1, i1 + 1, i1);
                    }
            }

            var m = mb.ToMesh("Zombie", true);
            var bind = new Matrix4x4[BindPos.Length];
            for (int i = 0; i < bind.Length; i++) bind[i] = Matrix4x4.Translate(-BindPos[i]);
            m.bindposes = bind;
            return m;
        }

        static void Torso(MeshBuilder mb, float[][] prof, int sub, int segs, float scale, bool tank)
        {
            int start = mb.VertexCount;
            int rows = prof.Length;
            for (int r = 0; r < rows; r++)
            {
                float y = prof[r][0], rx = prof[r][1] * scale, rz = prof[r][2] * scale, zo = prof[r][3];
                BoneWeight w;
                if (y < 1.0f) w = W(ZB.Hips, 1);
                else if (y < 1.12f) w = W(ZB.Hips, 1.12f - y, ZB.Spine, y - 1.0f);
                else if (y < 1.22f) w = W(ZB.Spine, 1);
                else if (y < 1.32f) w = W(ZB.Spine, 1.32f - y, ZB.Chest, y - 1.22f);
                else if (y < 1.47f) w = W(ZB.Chest, 1);
                else w = W(ZB.Chest, 0.6f, ZB.Neck, 0.4f);
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    float k = 1 + 0.08f * (1 - Mathf.Abs(sx * cz) * 2);
                    var p = new Vector3(sx * rx * k, y, cz * rz * k + zo);
                    var n = new Vector3(sx / rx, 0, cz / rz).normalized;
                    var ww = w;
                    if (y > 1.36f && Mathf.Abs(p.x) > 0.13f)
                    {
                        float a = Mathf.Clamp01((Mathf.Abs(p.x) - 0.13f) / 0.06f) * 0.3f;
                        ww = W(ZB.Chest, 1 - a, p.x < 0 ? ZB.ArmL : ZB.ArmR, a);
                    }
                    float v = Mathf.InverseLerp(0.86f, 1.5f, y);
                    mb.AddW(p, n, new Vector2(s / (float)segs, v), ww);
                }
            }
            for (int r = 0; r < rows - 1; r++)
            {
                if (tank && prof[r][0] > 1.38f) continue;
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            }
        }

        // Head profile: height, half-width, front depth, back depth.
        static readonly float[][] HeadProfile =
        {
            new[] { 1.6f, 0.025f, 0.06f, 0.02f }, new[] { 1.62f, 0.048f, 0.078f, 0.04f }, new[] { 1.645f, 0.062f, 0.088f, 0.06f }, new[] { 1.675f, 0.07f, 0.092f, 0.078f },
            new[] { 1.705f, 0.075f, 0.096f, 0.09f }, new[] { 1.735f, 0.079f, 0.097f, 0.099f }, new[] { 1.765f, 0.08f, 0.093f, 0.102f }, new[] { 1.79f, 0.075f, 0.085f, 0.097f },
            new[] { 1.81f, 0.064f, 0.071f, 0.084f }, new[] { 1.828f, 0.044f, 0.049f, 0.06f }, new[] { 1.84f, 0.012f, 0.013f, 0.016f },
        };

        static void HeadLoft(MeshBuilder mb, int sub, int segs, float scale)
        {
            int start = mb.VertexCount;
            int rows = HeadProfile.Length;
            for (int r = 0; r < rows; r++)
            {
                float y = HeadProfile[r][0], rx = HeadProfile[r][1] * scale, rf = HeadProfile[r][2] * scale, rb = HeadProfile[r][3] * scale;
                // Lower face rows ride on the jaw.
                BoneWeight w = y < 1.655f ? W(ZB.Jaw, 1) : y < 1.69f ? W(ZB.Jaw, 0.35f, ZB.Head, 0.65f) : W(ZB.Head, 1);
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float th = (u - 0.5f) * Mathf.PI * 2;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    float rz = cz > 0 ? rf : rb;
                    // Sunken eye sockets and cheekbones.
                    float socket = (y > 1.7f && y < 1.75f && cz > 0.75f && Mathf.Abs(sx) > 0.15f && Mathf.Abs(sx) < 0.6f) ? -0.008f : 0;
                    var p = new Vector3(sx * rx, y, cz * (rz + socket) + 0.004f);
                    var n = new Vector3(sx / Mathf.Max(0.01f, rx), 0.35f * (y - 1.72f) / 0.1f, cz / Mathf.Max(0.01f, rz)).normalized;
                    mb.AddW(p, n, new Vector2(u, Mathf.InverseLerp(1.6f, 1.84f, y)), w);
                }
            }
            for (int r = 0; r < rows - 1; r++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
            // Chin cap.
            int c = mb.AddW(new Vector3(0, 1.6f, 0.02f), Vector3.down, new Vector2(0.5f, 0), W(ZB.Jaw, 1));
            for (int s = 0; s < segs; s++) mb.Tri(sub, start + s + 1, start + s, c);
            // Nose and ears.
            mb.Weight = W(ZB.Head, 1);
            mb.Sphere(sub, new Vector3(0, 1.7f, 0.098f * scale), new Vector3(0.011f, 0.022f, 0.016f), 6, 4);
        }

        static void HairCap(MeshBuilder mb, int segs, int style)
        {
            mb.Weight = W(ZB.Head, 1);
            int start = mb.VertexCount;
            float[] ys = { 1.72f, 1.76f, 1.8f, 1.825f, 1.845f };
            for (int r = 0; r < ys.Length; r++)
            {
                float y = ys[r];
                int idx = Mathf.Clamp((int)((y - 1.6f) / 0.024f), 0, HeadProfile.Length - 1);
                var hp = HeadProfile[Mathf.Min(HeadProfile.Length - 1, idx + 1)];
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float th = (u - 0.5f) * Mathf.PI * 2;
                    float sx = Mathf.Sin(th), cz = Mathf.Cos(th);
                    float yy = y;
                    if (cz > 0.3f) yy = Mathf.Max(y, Mathf.Lerp(1.775f, 1.8f, (cz - 0.3f) / 0.7f));
                    if (style == 2 && cz < -0.2f) yy -= 0.06f * (1 - r / (float)ys.Length);
                    float s2 = 1.06f;
                    var p = new Vector3(sx * hp[1] * s2, yy + 0.004f, cz * (cz > 0 ? hp[2] : hp[3]) * s2);
                    mb.Add(p, new Vector3(sx, 0.5f, cz).normalized, new Vector2(u, r / (float)ys.Length));
                }
            }
            for (int r = 0; r < ys.Length - 1; r++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(SHair, i0, i0 + 1, i1 + 1, i1);
                }
        }

        static void Foot(MeshBuilder mb, Vector3 ankle, ZB foot, int sub)
        {
            int segs = 8, rings = 5;
            int start = mb.VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float k = r / (float)rings;
                float z = Mathf.Lerp(-0.06f, 0.18f, k);
                float h = Mathf.Lerp(0.1f, 0.05f, Mathf.Clamp01((k - 0.4f) / 0.6f));
                float wdt = 0.045f * (0.85f + 0.25f * Mathf.Sin(Mathf.Clamp01(k * 1.1f) * Mathf.PI * 0.9f));
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    float cx = Mathf.Sin(th), cy = Mathf.Cos(th);
                    var p = new Vector3(ankle.x + cx * wdt, Mathf.Max(0.0f, h * 0.5f + cy * h * 0.5f), z);
                    mb.AddW(p, new Vector3(cx, cy, 0).normalized, new Vector2(s / (float)segs, k), W(foot, 1));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    mb.Quad(sub, i0, i1, i1 + 1, i0 + 1);
                }
            int tip = mb.AddW(new Vector3(ankle.x, 0.03f, 0.195f), Vector3.forward, new Vector2(0.5f, 1), W(foot, 1));
            int last = start + rings * (segs + 1);
            for (int s = 0; s < segs; s++) mb.Tri(sub, last + s + 1, last + s, tip);
        }

        // ------------------------------------------------------------------ variants

        static readonly Color[] ShirtColors =
        {
            X.Hex(0x8A8A86), X.Hex(0x2A3550), X.Hex(0x5A1E1E), X.Hex(0x3A4A2E), X.Hex(0x6E6250), X.Hex(0x1C1C1E), X.Hex(0xA8A49A), X.Hex(0x3E5A6E), X.Hex(0x704020),
        };

        static readonly Color[] PantsColors = { X.Hex(0x2A3A55), X.Hex(0x1E1E20), X.Hex(0x4A4236), X.Hex(0x3A3A3C), X.Hex(0x2E3A2A) };

        /// <summary>Picks shape and look for a zombie type (or boss), varied by seed.</summary>
        public static void Variant(ZombieType type, BossType boss, bool elite, int seed, out ZShape s, out ZLook l)
        {
            var r = new System.Random(seed * 7919 + (int)type * 131 + (int)boss * 977);
            s = new ZShape();
            l = new ZLook { Seed = seed, Tone = r.Next(ZombieTex.SkinTones.Length), Decay = 0.45f + (float)r.NextDouble() * 0.5f, Tatter = 0.4f + (float)r.NextDouble() * 0.55f };
            l.ShirtCol = ShirtColors[r.Next(ShirtColors.Length)];
            l.PantsCol = PantsColors[r.Next(PantsColors.Length)];
            l.ShirtKind = r.NextDouble() < 0.3 ? 4 : 0;
            l.PantsKind = r.NextDouble() < 0.6 ? 1 : 0;
            s.Shirt = 1 + r.Next(3);
            s.Hair = 1 + r.Next(2);
            // Adult builds (the old 0.92-1.08 range read as thin mannequins); visual only, hitboxes are separate.
            s.Build = 1.06f + (float)r.NextDouble() * 0.16f;
            s.Muscle = 0.12f + (float)r.NextDouble() * 0.2f;
            l.EyeGlow = new Color(1.4f, 1.25f, 0.7f) * 0.8f;
            switch (type)
            {
                case ZombieType.Runner:
                    s.Gaunt = 0.3f; s.Shirt = 1 + r.Next(2); l.Decay *= 0.6f;
                    l.EyeGlow = new Color(2.5f, 0.35f, 0.15f);
                    break;
                case ZombieType.Crawler:
                    s.Legless = r.NextDouble() < 0.7; s.Gaunt = 0.4f; s.Shirt = 3; l.Decay = 0.9f; l.Tatter = 0.9f;
                    break;
                case ZombieType.Explosive:
                    // Hi-vis vest with strapped charges (reads as "will explode" at a glance).
                    s.Belly = 0.45f; s.Build = 1.15f; s.Shirt = 1; s.Vest = true; s.Charges = true; s.Hair = 0;
                    l.GearCol = X.Hex(0xD0601A); l.Gear2Col = X.Hex(0x8A1A12); l.ShirtCol = X.Hex(0x3A3632);
                    l.EyeGlow = new Color(3f, 1.4f, 0.2f); l.Tone = 0; l.Decay = 0.9f;
                    break;
                case ZombieType.Armored:
                    s.Riot = true; s.Helmet = true; s.Shirt = 2; s.Build = 1.08f;
                    l.ShirtCol = X.Hex(0x15181D); l.ShirtKind = 5; l.PantsCol = X.Hex(0x14161A); l.PantsKind = 5; l.GearCol = X.Hex(0x1B1E22); l.Gear2Col = X.Hex(0x0A0C10);
                    l.Gear2Metal = 0.1f;
                    break;
                case ZombieType.Toxic:
                    // Shirtless, glowing green veins and pustules.
                    s.Shirt = 0; s.GasMask = false; s.Hair = 0; s.Pustules = true; s.Muscle = 0.35f;
                    l.PantsCol = X.Hex(0x2A2E22); l.PantsKind = 0; l.Gear2Col = X.Hex(0x111111);
                    l.SkinGlow = new Color(0.35f, 2.4f, 0.25f);
                    l.EyeGlow = new Color(0.6f, 3f, 0.4f); l.Tone = 1;
                    break;
                case ZombieType.Military:
                    s.Helmet = true; s.Vest = true; s.Shirt = 2; s.Build = 1.05f;
                    l.ShirtKind = 2; l.PantsKind = 2; l.ShirtCol = Color.white; l.PantsCol = Color.white; l.GearCol = X.Hex(0x3A4028);
                    break;
                case ZombieType.Mutant:
                    s.Muscle = 1f; s.BigArm = 0.9f; s.Build = 1.38f; s.Shirt = 0; s.Spikes = true; s.Hair = 0; s.Pants = 1; s.Shoes = 0;
                    l.FleshSkin = r.NextDouble() < 0.5; l.Gear2Col = X.Hex(0xC8BEA8); l.EyeGlow = new Color(3f, 0.6f, 0.2f); l.Decay = 1;
                    break;
                case ZombieType.Hunter:
                    // Hooded stalker: dark tattered cowl and cloak, cold blue eyes.
                    s.Gaunt = 0.85f; s.Claws = true; s.Shirt = 0; s.Hair = 0; s.Pants = 1; s.Shoes = 0; s.ArmLen = 1.15f; s.Cloak = true; s.Hood = true;
                    l.ShirtCol = X.Hex(0x12161F); l.ShirtKind = 7; l.Tatter = 0.7f;
                    l.Tone = 3; l.Decay = 1; l.Gear2Col = X.Hex(0x0E0E10); l.EyeGlow = new Color(0.4f, 1.5f, 4.5f);
                    break;
            }
            switch (boss)
            {
                case BossType.Butcher:
                    s = new ZShape { Muscle = 1f, BigArm = 0.5f, Build = 1.35f, Belly = 0.5f, Shirt = 0, Apron = true, Cleaver = true, Hair = 0, Spikes = false, Pants = 0, Shoes = 1 };
                    l.ShirtCol = X.Hex(0x4A3A2A); l.ShirtKind = 6; l.Tatter = 0.3f; l.PantsCol = X.Hex(0x2A2A2A); l.Gear2Col = X.Hex(0x9AA0A6); l.Gear2Metal = 1; l.EyeGlow = new Color(3f, 0.4f, 0.1f); l.Decay = 0.8f;
                    break;
                case BossType.BroodMother:
                    s = new ZShape { Belly = 1.4f, Build = 1.2f, Shirt = 0, Sacs = true, Pustules = true, Hair = 2, Pants = 2, Shoes = 0, Gaunt = 0.3f, Claws = true };
                    l.EyeGlow = new Color(0.8f, 3.5f, 0.5f); l.Tone = 1; l.Decay = 1; l.Gear2Col = X.Hex(0x1A1A10);
                    break;
                case BossType.Juggernaut:
                    s = new ZShape { Muscle = 1f, Build = 1.4f, Shirt = 2, Plates = true, Helmet = true, Riot = false, Spikes = true, Hair = 0, Pants = 0, Shoes = 1 };
                    l.ShirtCol = X.Hex(0x22262A); l.ShirtKind = 5; l.PantsCol = X.Hex(0x1A1C1E); l.PantsKind = 5; l.GearCol = X.Hex(0x3A3E42); l.Gear2Col = X.Hex(0xBDB4A0); l.EyeGlow = new Color(3.5f, 1.2f, 0.1f);
                    break;
                case BossType.Wraith:
                    s = new ZShape { Gaunt = 1f, Build = 0.95f, Shirt = 0, Cloak = true, Claws = true, Hair = 2, Pants = 1, Shoes = 0, ArmLen = 1.2f };
                    l.ShirtCol = X.Hex(0x0C0C0E); l.ShirtKind = 7; l.Tatter = 1f; l.Tone = 3; l.HairCol = X.Hex(0x0A0A0A); l.Gear2Col = X.Hex(0x0A0A0C); l.EyeGlow = new Color(4f, 0.15f, 0.15f);
                    break;
            }
            if (elite) l.EyeGlow = new Color(4f, 0.25f, 0.1f);
        }
    }
}
