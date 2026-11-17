using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>A built weapon: moving parts and the reference points used by the viewmodel and gunplay.</summary>
    public sealed class GunModel : MonoBehaviour
    {
        public WeaponId Id;
        public Transform Body, Mag, Slide, Pump, Lid, Drum, Bolt;
        public Transform Muzzle, Eject, Sight, GripR, GripL, MagWell;
        public bool HasScope;
        /// <summary>Red dot or holographic sight fitted (aimed through exactly, like a scope).</summary>
        public bool HasOptic;
        public float Length;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public Vector3 MagRest, SlideRest, PumpRest, BoltRest;
        public Quaternion LidRest, DrumRest, BoltRotRest;
    }

    /// <summary>Procedural weapon models (extruded side profiles + turned parts) with skins and attachments.</summary>
    public static class GunModels
    {
        // Materials (indices for the part builder).
        const int MSteel = 0, MPoly = 1, MWood = 2, MSkin = 3, MRubber = 4, MGlass = 5, MGlow = 6, MBrass = 7, MReticle = 8;

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        static Material C(string key, System.Func<Material> f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = f();
            Cache[key] = m;
            return m;
        }

        static Surface _steel, _poly;

        static Surface Steel
        {
            get
            {
                if (_steel != null) return _steel;
                int n = Tex.Size(256);
                var h = new float[n * n];
                var mask = new Pix(n, n);
                var p = new Pix(n, n).Fill((u, v, x, y) =>
                {
                    float f = Noise.Fbm(u, v, 8, 4, 401);
                    float wear = Noise.Smooth(0.72f, 0.85f, Noise.Fbm(u, v, 16, 3, 402));
                    float scratch = Noise.Smooth(0.93f, 0.99f, Noise.Ridged(u * 4, v * 0.6f, 8, 2, 403));
                    var c = new Color(0.2f, 0.205f, 0.215f) * (0.85f + f * 0.3f);
                    c = Color.Lerp(c, new Color(0.6f, 0.6f, 0.6f), Mathf.Max(wear * 0.5f, scratch * 0.7f));
                    h[y * n + x] = f * 0.2f - scratch * 0.3f;
                    mask.C[y * n + x] = new Color(0.55f + wear * 0.3f, 0, 0, 0.45f + f * 0.12f + wear * 0.2f + scratch * 0.15f);
                    c.a = 1;
                    return c;
                });
                _steel = new Surface { Albedo = p.ToTexture("gunsteel"), Normal = Pix.NormalMap("gunsteel_n", h, n, n, 2f), Mask = mask.ToTexture("gunsteel_m", true), Smooth = 1, Metal = 1 };
                return _steel;
            }
        }

        static Surface Poly
        {
            get
            {
                if (_poly != null) return _poly;
                int n = Tex.Size(256);
                var h = new float[n * n];
                var p = new Pix(n, n).Fill((u, v, x, y) =>
                {
                    float st = Noise.Hash(x, y, 404);
                    float f = Noise.Fbm(u, v, 8, 3, 405);
                    h[y * n + x] = st * 0.5f + f * 0.2f;
                    var c = new Color(0.135f, 0.138f, 0.145f) * (0.9f + f * 0.25f);
                    c.a = 0.32f + f * 0.1f;
                    return c;
                });
                _poly = new Surface { Albedo = p.ToTexture("polymer"), Normal = Pix.NormalMap("polymer_n", h, n, n, 1.5f), Smooth = 1, WetAlpha = true };
                return _poly;
            }
        }

        static Surface _wood;

        /// <summary>Gun furniture wood (kept outside the per-stage texture cache).</summary>
        static Surface GunWood
        {
            get
            {
                if (_wood != null) return _wood;
                int n = Tex.Size(256);
                var h = new float[n * n];
                var p = new Pix(n, n).Fill((u, v, x, y) =>
                {
                    float grain = Mathf.Sin((v * 40 + Noise.Fbm(u, v, 4, 3, 407) * 8) * Mathf.PI * 2) * 0.5f + 0.5f;
                    var c = X.Hex(0x6A3A1E) * (0.7f + grain * 0.25f + Noise.Fbm(u, v, 8, 3, 408) * 0.15f);
                    h[y * n + x] = grain * 0.3f;
                    c.a = 0.45f;
                    return c;
                });
                _wood = new Surface { Albedo = p.ToTexture("gunwood"), Normal = Pix.NormalMap("gunwood_n", h, n, n, 2f), Smooth = 1, WetAlpha = true };
                return _wood;
            }
        }

        static Material[] Materials(Skin skin)
        {
            var steel = C("steel", () => Mats.For(Steel, Color.white));
            var poly = C("poly", () => Mats.For(Poly, Color.white));
            var wood = C("wood", () => Mats.For(GunWood, new Color(1f, 0.9f, 0.8f)));
            Material skinMat;
            switch (skin)
            {
                case Skin.Factory: skinMat = poly; break;
                case Skin.Gold: skinMat = C("skinGold", () => Mats.Lit(Color.white, 0.82f, 1f, Tex.Camo(8))); break;
                case Skin.Carbon: skinMat = C("skinCarbon", () => Mats.Lit(Color.white, 0.75f, 0.2f, Tex.Camo(6), new Vector2(3, 3))); break;
                case Skin.Toxic: skinMat = C("skinToxic", () => Mats.Emissive(Color.white, new Color(0.08f, 0.35f, 0.02f), Tex.Camo(7), 0.5f)); break;
                case Skin.Dragon: skinMat = C("skinDragon", () => Mats.Emissive(Color.white, new Color(0.25f, 0.04f, 0.0f), Tex.Camo(9), 0.6f)); break;
                default: skinMat = C("skin" + skin, () => Mats.Lit(Color.white, 0.35f, 0, Tex.Camo((int)skin), new Vector2(2, 2))); break;
            }
            var rubber = C("rubber", () => Mats.For(Poly, new Color(0.7f, 0.7f, 0.7f)));
            // Optic glass is see-through so red-dot / holo sights can be aimed through.
            var glass = C("glass", () => Mats.Transparent(new Color(0.45f, 0.7f, 0.75f, 0.16f), 0.85f));
            var glow = C("glow", () => Mats.Emissive(new Color(0.3f, 0.8f, 1f), new Color(0.5f, 1.6f, 2.6f)));
            var brass = C("brass", () => Mats.Lit(new Color(0.8f, 0.6f, 0.25f), 0.75f, 1f));
            var reticle = C("reticle", () => Mats.Emissive(new Color(1f, 0.15f, 0.1f), new Color(3.2f, 0.25f, 0.15f)));
            return new[] { steel, poly, wood, skinMat, rubber, glass, glow, brass, reticle };
        }

        /// <summary>Collects geometry for one transform, one sub-mesh per material.</summary>
        sealed class Part
        {
            public readonly MeshBuilder B = new MeshBuilder();
            public readonly Transform T;
            public Part(Transform t) { T = t; }

            public Part Ext(int m, Vector2[] pts, float width, float x = 0, float uv = 8)
            {
                B.M = Matrix4x4.Translate(new Vector3(x, 0, 0));
                B.Extrude(m, pts, width, uv);
                B.M = Matrix4x4.identity;
                return this;
            }

            public Part RBox(int m, Vector3 c, Vector3 s, float r = 0.004f, float uv = 8) { B.RoundBox(m, c, s, r, 1, uv); return this; }
            public Part Box(int m, Vector3 c, Vector3 s, float uv = 8) { B.Box(m, c, s, uv); return this; }

            public Part Cyl(int m, Vector3 a, Vector3 b, float r, int segs = 14, float r2 = -1) { B.Cylinder(m, a, b, r, r2 < 0 ? r : r2, segs, true, true, 6); return this; }

            public Part Ring(int m, Vector3 c, float R, float r, float a0, float a1, Vector3 euler)
            {
                B.M = Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
                B.Torus(m, Vector3.zero, R, r, a0, a1, 12, 6);
                B.M = Matrix4x4.identity;
                return this;
            }

            public Part Rot(Vector3 pivot, Vector3 euler, System.Action<Part> add)
            {
                var old = B.M;
                B.M = Matrix4x4.TRS(pivot, Quaternion.Euler(euler), Vector3.one) * Matrix4x4.Translate(-pivot);
                add(this);
                B.M = old;
                return this;
            }

            /// <summary>Picatinny rail with cross slots along Z.</summary>
            public Part Rail(Vector3 start, float len, float w = 0.021f)
            {
                Box(MSteel, start + new Vector3(0, 0.004f, len / 2), new Vector3(w, 0.008f, len));
                for (float z = 0.004f; z < len - 0.004f; z += 0.01f) Box(MSteel, start + new Vector3(0, 0.0105f, z + 0.0025f), new Vector3(w, 0.005f, 0.005f));
                return this;
            }

            public void Finish(Material[] mats, int layer, List<Renderer> list, bool shadows)
            {
                if (B.Empty) return;
                var mesh = B.ToMesh(T.name, true);
                var mf = T.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = T.gameObject.AddComponent<MeshRenderer>();
                var used = new Material[mesh.subMeshCount];
                for (int i = 0; i < used.Length; i++) used[i] = mats[Mathf.Min(i, mats.Length - 1)];
                mr.sharedMaterials = used;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
                mr.renderingLayerMask = layer == Layers.Viewmodel ? LightLayers.Viewmodel : LightLayers.World;
                T.gameObject.layer = layer;
                list.Add(mr);
            }
        }

        static Vector2[] V(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return v;
        }

        static Transform Pt(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        /// <summary>
        /// Imported weapon prefab, if one exists for this weapon. Weapons with a cosmetic skin or attachments
        /// keep the procedural model (which renders those), so purchased cosmetics never disappear.
        /// </summary>
        static GunModel BuildImported(WeaponId id, WeaponSave ws, Transform parent, int layer, bool shadows)
        {
            var def = Weapons.Get(id);
            // A built-in scope is stored as the weapon's optic; that is fine when the imported model has its own scope.
            bool builtInScope = def.BuiltInScope && ws != null && ws.Optic == (int)Attachment.Scope;
            if (ws != null && (ws.Skin != (int)Skin.Factory || (ws.Optic >= 0 && !builtInScope) || ws.Suppressor || ws.Foregrip)) return null;
            var prefab = ArtLibrary.Get("Weapons/" + id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "Gun_" + id;
            var rig = go.GetComponent<ArtGunRig>();
            if (rig == null || rig.Muzzle == null || rig.Sight == null || rig.GripR == null || (def.BuiltInScope && !rig.HasScope)) { Object.Destroy(go); return null; }
            var gm = go.AddComponent<GunModel>();
            gm.Id = id;
            gm.Body = go.transform;
            gm.Muzzle = rig.Muzzle; gm.Sight = rig.Sight; gm.GripR = rig.GripR; gm.GripL = rig.GripL != null ? rig.GripL : rig.GripR;
            gm.MagWell = rig.MagWell; gm.Eject = rig.Eject;
            gm.Slide = rig.Slide; gm.Mag = rig.Mag; gm.Bolt = rig.Bolt;
            gm.HasScope = rig.HasScope;
            gm.Length = rig.Length;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask = layer == Layers.Viewmodel ? LightLayers.Viewmodel : LightLayers.World;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                gm.Renderers.Add(r);
            }
            if (gm.Mag != null) gm.MagRest = gm.Mag.localPosition;
            if (gm.Slide != null) gm.SlideRest = gm.Slide.localPosition;
            if (gm.Bolt != null) { gm.BoltRest = gm.Bolt.localPosition; gm.BoltRotRest = gm.Bolt.localRotation; }
            return gm;
        }

        /// <summary>Builds a weapon (with the player's skin and attachments) under <paramref name="parent"/>.</summary>
        public static GunModel Build(WeaponId id, WeaponSave ws, Transform parent, int layer, bool shadows = false)
        {
            var art = BuildImported(id, ws, parent, layer, shadows);
            if (art != null) return art;
            var root = new GameObject("Gun_" + id);
            root.transform.SetParent(parent, false);
            var gm = root.AddComponent<GunModel>();
            gm.Id = id;
            var skin = ws != null ? (Skin)ws.Skin : Skin.Factory;
            var mats = Materials(skin);
            var body = new Part(Pt(root.transform, "Body", Vector3.zero));
            gm.Body = body.T;
            var parts = new List<Part> { body };
            Part Sub(string name, Vector3 at)
            {
                var p = new Part(Pt(root.transform, name, at));
                parts.Add(p);
                return p;
            }
            var def = Weapons.Get(id);
            bool extMag = ws != null && ws.ExtMag;
            float sightY = 0.06f;
            float muzzleZ = 0.3f;
            switch (id)
            {
                // ------------------------------------------------------------ pistols
                case WeaponId.StandardPistol:
                case WeaponId.TacticalPistol:
                {
                    bool tac = id == WeaponId.TacticalPistol;
                    // Frame + grip.
                    body.Ext(MSkin, V(-0.06f, 0.0f, 0.1f, 0.0f, 0.1f, 0.012f, 0.03f, 0.014f, 0.022f, 0.0f), 0.026f);
                    body.Ext(MPoly, V(-0.06f, 0.002f, -0.005f, 0.002f, -0.03f, -0.11f, -0.075f, -0.115f, -0.068f, -0.02f), 0.029f);
                    body.Ring(MSkin, new Vector3(0, -0.012f, 0.01f), 0.022f, 0.0035f, 180, 360, new Vector3(0, 90, 0));
                    body.Box(MSteel, new Vector3(0, -0.012f, 0.0f), new Vector3(0.006f, 0.022f, 0.006f));
                    for (int i = 0; i < 5; i++) body.Box(MPoly, new Vector3(0, -0.03f - i * 0.016f, -0.022f - i * 0.0085f), new Vector3(0.031f, 0.004f, 0.012f));
                    // Slide (moves).
                    var slide = Sub("Slide", Vector3.zero);
                    slide.Ext(MSteel, V(-0.072f, 0.012f, 0.108f, 0.012f, 0.112f, 0.03f, 0.105f, 0.042f, -0.066f, 0.042f, -0.074f, 0.034f), 0.025f);
                    for (int i = 0; i < 7; i++) slide.Box(MSteel, new Vector3(0, 0.027f, -0.06f + i * 0.0045f), new Vector3(0.0262f, 0.024f, 0.0018f));
                    slide.Box(MSteel, new Vector3(0, 0.046f, 0.098f), new Vector3(0.004f, 0.007f, 0.006f));
                    slide.Box(MSteel, new Vector3(0, 0.046f, -0.062f), new Vector3(0.016f, 0.007f, 0.006f));
                    slide.Box(MGlow, new Vector3(0, 0.0475f, 0.098f), new Vector3(0.0015f, 0.002f, 0.0062f));
                    slide.Box(MPoly, new Vector3(0.0128f, 0.033f, 0.02f), new Vector3(0.002f, 0.009f, 0.03f));
                    slide.Cyl(MSteel, new Vector3(0, 0.026f, 0.1f), new Vector3(0, 0.026f, 0.114f), 0.006f, 10);
                    gm.Slide = slide.T;
                    var mag = Sub("Mag", Vector3.zero);
                    float ml = extMag || tac ? 0.06f : 0;
                    mag.Ext(MSteel, V(-0.028f, -0.02f, -0.012f, -0.02f, -0.034f, -0.112f - ml, -0.062f, -0.112f - ml, -0.05f, -0.02f), 0.02f);
                    mag.RBox(MPoly, new Vector3(0, -0.118f - ml, -0.049f), new Vector3(0.028f, 0.012f, 0.034f), 0.003f);
                    gm.Mag = mag.T;
                    if (tac)
                    {
                        body.Cyl(MSteel, new Vector3(0, 0.026f, 0.112f), new Vector3(0, 0.026f, 0.15f), 0.012f, 12);
                        for (int i = 0; i < 3; i++) body.Box(MPoly, new Vector3(0, 0.035f, 0.12f + i * 0.01f), new Vector3(0.026f, 0.004f, 0.004f));
                        body.Rail(new Vector3(0, -0.0f, 0.04f), 0.05f);
                        muzzleZ = 0.152f;
                    }
                    else muzzleZ = 0.115f;
                    sightY = 0.05f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.055f, -0.04f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.06f, -0.03f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.11f, -0.05f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.014f, 0.035f, 0.02f));
                    break;
                }
                case WeaponId.HeavyPistol:
                {
                    body.Ext(MSkin, V(-0.07f, 0.0f, 0.15f, 0.0f, 0.15f, 0.016f, 0.03f, 0.018f, 0.02f, 0.0f), 0.03f);
                    body.Ext(MPoly, V(-0.07f, 0.002f, -0.008f, 0.002f, -0.034f, -0.12f, -0.085f, -0.124f, -0.078f, -0.02f), 0.033f);
                    body.Ring(MSkin, new Vector3(0, -0.012f, 0.012f), 0.024f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    var slide = Sub("Slide", Vector3.zero);
                    slide.Ext(MSteel, V(-0.08f, 0.016f, 0.17f, 0.016f, 0.172f, 0.05f, -0.07f, 0.05f, -0.082f, 0.04f), 0.032f);
                    slide.Ext(MSteel, V(0.0f, 0.05f, 0.172f, 0.05f, 0.172f, 0.056f, 0.0f, 0.056f), 0.012f);
                    for (int i = 0; i < 6; i++) slide.Box(MSteel, new Vector3(0, 0.034f, -0.07f + i * 0.006f), new Vector3(0.0332f, 0.028f, 0.0024f));
                    slide.Box(MSteel, new Vector3(0, 0.06f, -0.07f), new Vector3(0.018f, 0.008f, 0.008f));
                    slide.Box(MSteel, new Vector3(0, 0.06f, 0.16f), new Vector3(0.005f, 0.009f, 0.008f));
                    slide.Box(MGlow, new Vector3(0, 0.0615f, 0.16f), new Vector3(0.0018f, 0.0025f, 0.0082f));
                    // Muzzle brake.
                    slide.Box(MSteel, new Vector3(0, 0.033f, 0.182f), new Vector3(0.034f, 0.034f, 0.022f));
                    slide.Box(MPoly, new Vector3(0.0172f, 0.033f, 0.182f), new Vector3(0.001f, 0.012f, 0.012f));
                    slide.Box(MPoly, new Vector3(-0.0172f, 0.033f, 0.182f), new Vector3(0.001f, 0.012f, 0.012f));
                    gm.Slide = slide.T;
                    var mag = Sub("Mag", Vector3.zero);
                    mag.Ext(MSteel, V(-0.03f, -0.02f, -0.014f, -0.02f, -0.038f, -0.122f - (extMag ? 0.04f : 0), -0.07f, -0.122f - (extMag ? 0.04f : 0), -0.058f, -0.02f), 0.024f);
                    gm.Mag = mag.T;
                    muzzleZ = 0.195f;
                    sightY = 0.064f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.06f, -0.045f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.065f, -0.035f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.12f, -0.055f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.017f, 0.045f, 0.03f));
                    break;
                }

                // ------------------------------------------------------------ rifles
                case WeaponId.VK74:
                {
                    // Stamped receiver with ribbed dust cover.
                    body.Ext(MSteel, V(-0.12f, -0.01f, 0.13f, -0.01f, 0.14f, 0.025f, 0.13f, 0.04f, -0.12f, 0.04f, -0.13f, 0.025f), 0.034f);
                    var cover = body;
                    for (int i = 0; i < 3; i++) body.Box(MSteel, new Vector3(0, 0.044f, -0.09f + i * 0.07f), new Vector3(0.036f, 0.008f, 0.012f));
                    body.Box(MSteel, new Vector3(0.018f, 0.018f, 0.04f), new Vector3(0.003f, 0.016f, 0.06f));
                    // Wooden furniture.
                    body.Ext(MWood, V(0.14f, -0.012f, 0.32f, -0.012f, 0.33f, 0.02f, 0.32f, 0.034f, 0.14f, 0.034f), 0.04f);
                    body.Ext(MWood, V(0.19f, 0.04f, 0.32f, 0.04f, 0.32f, 0.062f, 0.19f, 0.062f), 0.028f);
                    body.Ext(MWood, V(-0.12f, 0.03f, -0.4f, -0.04f, -0.41f, -0.11f, -0.36f, -0.115f, -0.12f, -0.025f), 0.036f);
                    body.Box(MSteel, new Vector3(0, -0.075f, -0.405f), new Vector3(0.038f, 0.08f, 0.012f));
                    // Grip.
                    body.Ext(MSkin, V(-0.04f, -0.01f, -0.005f, -0.01f, -0.03f, -0.12f, -0.07f, -0.12f), 0.03f);
                    body.Ring(MSteel, new Vector3(0, -0.012f, 0.015f), 0.024f, 0.003f, 180, 360, new Vector3(0, 90, 0));
                    // Barrel, gas tube, front sight, muzzle brake.
                    body.Cyl(MSteel, new Vector3(0, 0.012f, 0.32f), new Vector3(0, 0.012f, 0.5f), 0.0095f, 12);
                    body.Cyl(MSteel, new Vector3(0, 0.052f, 0.32f), new Vector3(0, 0.052f, 0.4f), 0.009f, 10);
                    body.Box(MSteel, new Vector3(0, 0.035f, 0.43f), new Vector3(0.016f, 0.06f, 0.02f));
                    body.Box(MSteel, new Vector3(0, 0.07f, 0.43f), new Vector3(0.003f, 0.016f, 0.003f));
                    body.Cyl(MSteel, new Vector3(0, 0.012f, 0.5f), new Vector3(0, 0.012f, 0.56f), 0.0135f, 12);
                    body.Box(MPoly, new Vector3(0, 0.022f, 0.54f), new Vector3(0.028f, 0.004f, 0.012f));
                    // Rear sight.
                    body.Box(MSteel, new Vector3(0, 0.055f, 0.14f), new Vector3(0.022f, 0.012f, 0.04f));
                    sightY = 0.074f;
                    muzzleZ = 0.56f;
                    var mag = Sub("Mag", Vector3.zero);
                    float ex = extMag ? 0.05f : 0;
                    // Curved magazine as a fan of segments.
                    var mp = new List<Vector2>();
                    for (int i = 0; i <= 8; i++) { float t = i / 8f; float a = t * 0.55f; mp.Add(new Vector2(0.075f + Mathf.Sin(a) * (0.18f + ex), -0.01f - Mathf.Sin(a + 1.2f) * 0.0f - t * (0.17f + ex))); }
                    var poly = new List<Vector2>();
                    for (int i = 0; i <= 8; i++) poly.Add(new Vector2(mp[i].x - 0.06f * (1 - i / 16f), mp[i].y));
                    for (int i = 8; i >= 0; i--) poly.Add(new Vector2(mp[i].x - 0.005f + 0.02f * (i / 8f), mp[i].y - 0.0f));
                    var magPoly = new List<Vector2>();
                    for (int i = 0; i <= 8; i++) { float t = i / 8f; magPoly.Add(new Vector2(0.02f + t * (0.07f + ex * 0.4f), -0.005f - t * (0.17f + ex))); }
                    for (int i = 8; i >= 0; i--) { float t = i / 8f; magPoly.Add(new Vector2(0.075f + t * (0.095f + ex * 0.4f), -0.005f - t * (0.16f + ex))); }
                    mag.Ext(MSteel, magPoly.ToArray(), 0.026f);
                    gm.Mag = mag.T;
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Cyl(MSteel, new Vector3(0.02f, 0.028f, 0.09f), new Vector3(0.04f, 0.028f, 0.09f), 0.006f, 8);
                    gm.Bolt = bolt.T;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.06f, -0.035f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.01f, 0.25f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.06f, 0.07f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.02f, 0.025f, 0.04f));
                    break;
                }
                case WeaponId.R4Carbine:
                case WeaponId.SemiSniper:
                case WeaponId.BastionBurst:
                {
                    bool dmr = id == WeaponId.SemiSniper, burst = id == WeaponId.BastionBurst;
                    // Upper / lower receivers.
                    body.Ext(MSteel, V(-0.11f, 0.0f, 0.1f, 0.0f, 0.1f, 0.045f, -0.11f, 0.045f), 0.03f);
                    body.Ext(MSkin, V(-0.11f, -0.035f, 0.07f, -0.035f, 0.075f, 0.0f, -0.11f, 0.0f), 0.032f);
                    body.Rail(new Vector3(0, 0.045f, -0.1f), 0.19f);
                    if (burst)
                    {
                        // Angular shroud over the upper.
                        body.Ext(MSkin, V(-0.12f, 0.0f, 0.1f, 0.0f, 0.14f, 0.025f, 0.1f, 0.05f, -0.12f, 0.05f), 0.036f);
                    }
                    // Ejection port and forward assist.
                    body.Box(MPoly, new Vector3(0.0152f, 0.022f, 0.0f), new Vector3(0.002f, 0.014f, 0.045f));
                    body.Cyl(MSteel, new Vector3(0.014f, 0.03f, -0.04f), new Vector3(0.024f, 0.03f, -0.035f), 0.006f, 8);
                    // Handguard (quad rail).
                    float hg = dmr ? 0.32f : 0.24f;
                    body.RBox(MSkin, new Vector3(0, 0.02f, 0.1f + hg / 2), new Vector3(0.042f, 0.046f, hg), 0.008f);
                    body.Rail(new Vector3(0, 0.043f, 0.1f), hg);
                    for (int i = 0; i < (int)(hg / 0.03f); i++)
                    {
                        body.Box(MPoly, new Vector3(0.0212f, 0.02f, 0.115f + i * 0.03f), new Vector3(0.001f, 0.012f, 0.016f));
                        body.Box(MPoly, new Vector3(-0.0212f, 0.02f, 0.115f + i * 0.03f), new Vector3(0.001f, 0.012f, 0.016f));
                    }
                    // Barrel and muzzle device.
                    float bl = dmr ? 0.62f : 0.48f;
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.1f + hg), new Vector3(0, 0.02f, bl), 0.0085f, 12);
                    body.Cyl(MSteel, new Vector3(0, 0.02f, bl), new Vector3(0, 0.02f, bl + 0.05f), 0.012f, 12);
                    for (int i = 0; i < 3; i++) body.Box(MPoly, new Vector3(0, 0.032f, bl + 0.012f + i * 0.012f), new Vector3(0.008f, 0.002f, 0.006f));
                    muzzleZ = bl + 0.05f;
                    // Pistol grip, trigger guard.
                    body.Ext(MPoly, V(-0.045f, -0.03f, -0.012f, -0.03f, -0.035f, -0.125f, -0.075f, -0.125f, -0.07f, -0.03f), 0.03f);
                    body.Ring(MSteel, new Vector3(0, -0.035f, 0.005f), 0.02f, 0.003f, 180, 360, new Vector3(0, 90, 0));
                    // Buffer tube and adjustable stock.
                    body.Cyl(MSteel, new Vector3(0, 0.02f, -0.11f), new Vector3(0, 0.02f, -0.27f), 0.014f, 12);
                    body.Ext(MSkin, V(-0.2f, 0.04f, -0.33f, 0.045f, -0.335f, -0.085f, -0.31f, -0.09f, -0.24f, -0.02f, -0.2f, -0.0f), 0.04f);
                    body.RBox(MRubber, new Vector3(0, -0.02f, -0.337f), new Vector3(0.042f, 0.13f, 0.012f), 0.004f);
                    // Charging handle.
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Box(MSteel, new Vector3(0, 0.05f, -0.105f), new Vector3(0.03f, 0.008f, 0.02f));
                    gm.Bolt = bolt.T;
                    var mag = Sub("Mag", Vector3.zero);
                    float ml = (dmr ? 0.12f : 0.15f) + (extMag ? 0.05f : 0);
                    mag.Ext(MSteel, V(0.012f, -0.03f, 0.068f, -0.03f, 0.076f, -0.03f - ml, 0.024f, -0.03f - ml * 0.97f), 0.024f);
                    mag.Box(MPoly, new Vector3(0, -0.035f - ml, 0.05f), new Vector3(0.028f, 0.012f, 0.06f));
                    gm.Mag = mag.T;
                    sightY = 0.08f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.07f, -0.04f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.005f, 0.24f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.07f, 0.045f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.018f, 0.022f, 0.0f));
                    if (!def.BuiltInScope && (ws == null || ws.Optic < 0))
                    {
                        // Flip-up iron sights.
                        body.Box(MSteel, new Vector3(0, 0.062f, -0.08f), new Vector3(0.018f, 0.014f, 0.012f));
                        body.Box(MSteel, new Vector3(0, 0.062f, 0.1f + hg - 0.02f), new Vector3(0.016f, 0.02f, 0.01f));
                        body.Box(MGlow, new Vector3(0, 0.073f, 0.1f + hg - 0.02f), new Vector3(0.002f, 0.003f, 0.011f));
                    }
                    break;
                }
                case WeaponId.RavenTR:
                {
                    // Bullpup: long smooth shell, mag behind the grip.
                    body.Ext(MSkin, V(-0.3f, -0.05f, 0.2f, -0.03f, 0.25f, 0.0f, 0.25f, 0.05f, 0.0f, 0.06f, -0.3f, 0.05f, -0.32f, -0.0f), 0.044f);
                    body.Ext(MPoly, V(-0.3f, -0.05f, -0.32f, -0.13f, -0.3f, -0.135f, -0.28f, -0.05f), 0.042f);
                    body.Rail(new Vector3(0, 0.06f, -0.2f), 0.36f);
                    body.Ext(MPoly, V(0.05f, -0.04f, 0.08f, -0.04f, 0.06f, -0.14f, 0.02f, -0.14f), 0.03f);
                    body.Ring(MSkin, new Vector3(0, -0.045f, 0.115f), 0.03f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.25f), new Vector3(0, 0.02f, 0.37f), 0.009f, 12);
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.37f), new Vector3(0, 0.02f, 0.42f), 0.013f, 12);
                    body.Box(MPoly, new Vector3(0.0225f, 0.0f, -0.1f), new Vector3(0.002f, 0.03f, 0.08f));
                    muzzleZ = 0.42f;
                    var mag = Sub("Mag", Vector3.zero);
                    float ml = 0.15f + (extMag ? 0.05f : 0);
                    mag.Ext(MSteel, V(-0.2f, -0.05f, -0.14f, -0.05f, -0.13f, -0.05f - ml, -0.18f, -0.05f - ml), 0.024f);
                    gm.Mag = mag.T;
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Box(MSteel, new Vector3(-0.026f, 0.04f, 0.15f), new Vector3(0.012f, 0.01f, 0.02f));
                    gm.Bolt = bolt.T;
                    sightY = 0.09f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.085f, 0.06f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.04f, 0.2f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.1f, -0.17f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.022f, 0.02f, -0.1f));
                    break;
                }

                // ------------------------------------------------------------ shotguns
                case WeaponId.PumpShotgun:
                {
                    body.Ext(MSteel, V(-0.12f, -0.02f, 0.1f, -0.02f, 0.1f, 0.05f, -0.1f, 0.05f, -0.12f, 0.035f), 0.034f);
                    body.Cyl(MSteel, new Vector3(0, 0.032f, 0.1f), new Vector3(0, 0.032f, 0.58f), 0.012f, 14);
                    body.Cyl(MSteel, new Vector3(0, 0.005f, 0.1f), new Vector3(0, 0.005f, 0.52f), 0.011f, 12);
                    body.Box(MSteel, new Vector3(0, 0.032f, 0.52f), new Vector3(0.026f, 0.04f, 0.015f));
                    body.Box(MGlow, new Vector3(0, 0.047f, 0.575f), new Vector3(0.003f, 0.003f, 0.004f));
                    body.Ext(MSkin, V(-0.12f, 0.04f, -0.4f, 0.0f, -0.41f, -0.1f, -0.37f, -0.1f, -0.12f, -0.02f), 0.038f);
                    body.Ext(MSkin, V(-0.05f, -0.02f, -0.01f, -0.02f, -0.06f, -0.1f, -0.1f, -0.095f), 0.03f);
                    body.Ring(MSteel, new Vector3(0, -0.022f, 0.015f), 0.022f, 0.003f, 180, 360, new Vector3(0, 90, 0));
                    body.RBox(MRubber, new Vector3(0, -0.05f, -0.412f), new Vector3(0.04f, 0.11f, 0.014f), 0.004f);
                    // Pump forend (moves).
                    var pump = Sub("Pump", Vector3.zero);
                    pump.RBox(MPoly, new Vector3(0, 0.004f, 0.3f), new Vector3(0.038f, 0.034f, 0.14f), 0.01f);
                    for (int i = 0; i < 7; i++) pump.Box(MPoly, new Vector3(0, 0.004f, 0.245f + i * 0.018f), new Vector3(0.0395f, 0.0355f, 0.004f));
                    gm.Pump = pump.T;
                    body.Box(MPoly, new Vector3(0.0172f, 0.02f, 0.03f), new Vector3(0.001f, 0.018f, 0.05f));
                    muzzleZ = 0.58f;
                    sightY = 0.05f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.05f, -0.04f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.02f, 0.3f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.01f, 0.06f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.018f, 0.02f, 0.03f));
                    break;
                }
                case WeaponId.AutoShotgun:
                case WeaponId.HeavyShotgun:
                {
                    bool drum = id == WeaponId.AutoShotgun;
                    body.Ext(MSkin, V(-0.16f, -0.03f, 0.16f, -0.03f, 0.18f, 0.02f, 0.16f, 0.065f, -0.16f, 0.065f), 0.046f);
                    body.Rail(new Vector3(0, 0.065f, -0.12f), 0.24f);
                    body.Cyl(MSteel, new Vector3(0, 0.025f, 0.16f), new Vector3(0, 0.025f, drum ? 0.45f : 0.5f), 0.016f, 14);
                    body.RBox(MSteel, new Vector3(0, 0.025f, drum ? 0.42f : 0.47f), new Vector3(0.04f, 0.04f, 0.07f), 0.006f);
                    for (int i = 0; i < 4; i++) body.Box(MPoly, new Vector3(0.0202f, 0.025f, (drum ? 0.4f : 0.45f) + i * 0.012f), new Vector3(0.001f, 0.022f, 0.006f));
                    body.RBox(MPoly, new Vector3(0, 0.0f, 0.24f), new Vector3(0.05f, 0.05f, 0.14f), 0.01f);
                    body.Ext(MPoly, V(-0.06f, -0.03f, -0.02f, -0.03f, -0.05f, -0.13f, -0.09f, -0.13f), 0.032f);
                    body.Ring(MSkin, new Vector3(0, -0.035f, 0.005f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MSkin, V(-0.16f, 0.06f, -0.38f, 0.04f, -0.39f, -0.09f, -0.34f, -0.09f, -0.16f, -0.02f), 0.044f);
                    body.RBox(MRubber, new Vector3(0, -0.02f, -0.392f), new Vector3(0.046f, 0.13f, 0.014f), 0.004f);
                    muzzleZ = drum ? 0.46f : 0.51f;
                    sightY = 0.088f;
                    var mag = Sub("Mag", Vector3.zero);
                    if (drum)
                    {
                        mag.Cyl(MPoly, new Vector3(-0.03f, -0.09f, 0.08f), new Vector3(0.03f, -0.09f, 0.08f), 0.075f, 18);
                        mag.Cyl(MSteel, new Vector3(-0.031f, -0.09f, 0.08f), new Vector3(0.031f, -0.09f, 0.08f), 0.02f, 10);
                    }
                    else mag.Ext(MSteel, V(0.04f, -0.03f, 0.11f, -0.03f, 0.115f, -0.15f - (extMag ? 0.05f : 0), 0.045f, -0.15f - (extMag ? 0.05f : 0)), 0.036f);
                    gm.Mag = mag.T;
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Box(MSteel, new Vector3(-0.028f, 0.045f, 0.08f), new Vector3(0.014f, 0.012f, 0.024f));
                    gm.Bolt = bolt.T;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.075f, -0.07f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.02f, 0.24f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.09f, 0.08f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.024f, 0.04f, 0.04f));
                    break;
                }

                // ------------------------------------------------------------ snipers
                case WeaponId.TacticalSniper:
                case WeaponId.HeavySniper:
                {
                    bool heavy = id == WeaponId.HeavySniper;
                    float s = heavy ? 1.2f : 1f;
                    body.Ext(MSteel, V(-0.12f, -0.005f, 0.14f, -0.005f, 0.14f, 0.045f, -0.12f, 0.045f), 0.034f * s);
                    body.Rail(new Vector3(0, 0.045f, -0.11f), 0.24f);
                    body.Ext(MSkin, V(0.14f, -0.03f, 0.42f, -0.02f, 0.42f, 0.02f, 0.14f, 0.025f), 0.046f * s);
                    body.Ext(MSkin, V(-0.12f, 0.03f, -0.46f, 0.02f, -0.47f, -0.11f, -0.42f, -0.115f, -0.3f, -0.06f, -0.2f, -0.06f, -0.14f, -0.03f), 0.04f * s);
                    body.Ext(MSkin, V(-0.08f, -0.005f, -0.03f, -0.005f, -0.06f, -0.12f, -0.1f, -0.12f), 0.034f);
                    body.Ring(MSteel, new Vector3(0, -0.01f, 0.0f), 0.022f, 0.003f, 180, 360, new Vector3(0, 90, 0));
                    float bl = heavy ? 0.95f : 0.82f;
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.14f), new Vector3(0, 0.02f, bl), (heavy ? 0.014f : 0.011f), 14, heavy ? 0.012f : 0.009f);
                    if (heavy)
                    {
                        body.RBox(MSteel, new Vector3(0, 0.02f, bl + 0.04f), new Vector3(0.05f, 0.032f, 0.09f), 0.006f);
                        for (int i = 0; i < 3; i++) { body.Box(MPoly, new Vector3(0.0252f, 0.02f, bl + 0.015f + i * 0.025f), new Vector3(0.001f, 0.02f, 0.012f)); body.Box(MPoly, new Vector3(-0.0252f, 0.02f, bl + 0.015f + i * 0.025f), new Vector3(0.001f, 0.02f, 0.012f)); }
                        muzzleZ = bl + 0.09f;
                    }
                    else muzzleZ = bl;
                    body.RBox(MRubber, new Vector3(0, -0.045f, -0.472f), new Vector3(0.042f, 0.15f, 0.014f), 0.004f);
                    // Folded bipod.
                    body.Cyl(MSteel, new Vector3(0.012f, -0.01f, 0.4f), new Vector3(0.012f, -0.012f, 0.6f), 0.004f, 6);
                    body.Cyl(MSteel, new Vector3(-0.012f, -0.01f, 0.4f), new Vector3(-0.012f, -0.012f, 0.6f), 0.004f, 6);
                    var bolt = Sub("Bolt", new Vector3(0.02f, 0.03f, -0.02f));
                    bolt.Cyl(MSteel, new Vector3(0.0f, 0, 0), new Vector3(0.04f, -0.02f, 0), 0.0045f, 8);
                    bolt.B.Sphere(MSteel, new Vector3(0.045f, -0.022f, 0), Vector3.one * 0.009f, 8, 6);
                    gm.Bolt = bolt.T;
                    var mag = Sub("Mag", Vector3.zero);
                    mag.Ext(MSteel, V(0.02f, -0.005f, 0.09f, -0.005f, 0.09f, -0.06f - (extMag ? 0.03f : 0), 0.02f, -0.06f - (extMag ? 0.03f : 0)), 0.03f * s);
                    gm.Mag = mag.T;
                    sightY = 0.1f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.065f, -0.07f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.04f, 0.3f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.04f, 0.055f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.02f, 0.03f, 0.02f));
                    break;
                }

                // ------------------------------------------------------------ machine guns
                case WeaponId.LightMG:
                case WeaponId.HeavyMG:
                {
                    bool heavy = id == WeaponId.HeavyMG;
                    float s = heavy ? 1.25f : 1f;
                    body.Ext(MSkin, V(-0.16f, -0.03f, 0.18f, -0.03f, 0.18f, 0.05f, -0.16f, 0.05f), 0.05f * s);
                    var lid = Sub("Lid", new Vector3(0, 0.05f, -0.15f));
                    lid.Ext(MSteel, V(0.0f, 0.0f, 0.3f, 0.0f, 0.29f, 0.022f, 0.01f, 0.025f), 0.052f * s);
                    lid.Rail(new Vector3(0, 0.024f, 0.02f), 0.22f);
                    gm.Lid = lid.T;
                    float bl = heavy ? 0.85f : 0.72f;
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.18f), new Vector3(0, 0.02f, bl), 0.012f * s, 14);
                    // Perforated barrel jacket.
                    body.Cyl(MPoly, new Vector3(0, 0.02f, 0.18f), new Vector3(0, 0.02f, 0.18f + (heavy ? 0.5f : 0.36f)), 0.024f * s, 16);
                    for (int i = 0; i < 8; i++)
                    {
                        float z = 0.21f + i * (heavy ? 0.06f : 0.044f);
                        body.Box(MSteel, new Vector3(0.0245f * s, 0.02f, z), new Vector3(0.003f, 0.012f, 0.012f));
                        body.Box(MSteel, new Vector3(-0.0245f * s, 0.02f, z), new Vector3(0.003f, 0.012f, 0.012f));
                    }
                    body.Cyl(MSteel, new Vector3(0, 0.02f, bl), new Vector3(0, 0.02f, bl + 0.06f), 0.016f * s, 12);
                    muzzleZ = bl + 0.06f;
                    // Carry handle, grip, stock, bipod.
                    body.Box(MSteel, new Vector3(0, 0.1f, 0.22f), new Vector3(0.012f, 0.012f, 0.1f));
                    body.Box(MSteel, new Vector3(0, 0.075f, 0.18f), new Vector3(0.01f, 0.05f, 0.01f));
                    body.Box(MSteel, new Vector3(0, 0.075f, 0.26f), new Vector3(0.01f, 0.05f, 0.01f));
                    body.Ext(MPoly, V(-0.07f, -0.03f, -0.03f, -0.03f, -0.06f, -0.14f, -0.1f, -0.14f), 0.034f);
                    body.Ring(MSteel, new Vector3(0, -0.035f, -0.01f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MSkin, V(-0.16f, 0.04f, -0.42f, 0.03f, -0.43f, -0.1f, -0.38f, -0.1f, -0.16f, -0.025f), 0.048f);
                    body.Cyl(MSteel, new Vector3(0.015f, -0.01f, bl - 0.12f), new Vector3(0.02f, -0.02f, bl - 0.36f), 0.005f, 6);
                    body.Cyl(MSteel, new Vector3(-0.015f, -0.01f, bl - 0.12f), new Vector3(-0.02f, -0.02f, bl - 0.36f), 0.005f, 6);
                    // Box magazine with ammo belt.
                    var mag = Sub("Mag", Vector3.zero);
                    mag.RBox(MSkin, new Vector3(-0.0f, -0.09f * s, 0.08f), new Vector3(0.09f * s, 0.11f * s, 0.12f), 0.008f);
                    for (int i = 0; i < 6; i++) mag.Cyl(MBrass, new Vector3(-0.045f - i * 0.004f, -0.03f + i * 0.008f, 0.04f + i * 0.0f), new Vector3(-0.045f - i * 0.004f, -0.03f + i * 0.008f, 0.08f), 0.004f, 6);
                    gm.Mag = mag.T;
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Box(MSteel, new Vector3(0.03f * s, 0.01f, 0.05f), new Vector3(0.016f, 0.01f, 0.02f));
                    gm.Bolt = bolt.T;
                    sightY = 0.11f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.075f, -0.08f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, 0.0f, 0.3f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.1f, 0.08f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0, -0.035f, 0.1f));
                    break;
                }

                // ------------------------------------------------------------ specials
                case WeaponId.Flamethrower:
                {
                    body.RBox(MSkin, new Vector3(0, 0.0f, 0.02f), new Vector3(0.05f, 0.06f, 0.3f), 0.012f);
                    body.Cyl(MSteel, new Vector3(0, 0.0f, 0.17f), new Vector3(0, 0.0f, 0.5f), 0.016f, 14);
                    body.Cyl(MSteel, new Vector3(0, 0.0f, 0.5f), new Vector3(0, 0.0f, 0.58f), 0.026f, 14, 0.02f);
                    for (int i = 0; i < 5; i++) body.Cyl(MPoly, new Vector3(0, 0, 0.22f + i * 0.05f), new Vector3(0, 0, 0.235f + i * 0.05f), 0.022f, 12);
                    body.Cyl(MGlow, new Vector3(0, -0.025f, 0.56f), new Vector3(0, -0.025f, 0.575f), 0.004f, 8);
                    body.Ext(MPoly, V(-0.06f, -0.03f, -0.02f, -0.03f, -0.05f, -0.14f, -0.09f, -0.14f), 0.032f);
                    body.Ext(MPoly, V(0.18f, -0.02f, 0.21f, -0.02f, 0.2f, -0.12f, 0.17f, -0.12f), 0.028f);
                    body.Ring(MSteel, new Vector3(0, -0.035f, 0.005f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    var mag = Sub("Mag", Vector3.zero);
                    mag.Cyl(MSkin, new Vector3(0.0f, -0.07f, -0.1f), new Vector3(0.0f, -0.07f, 0.16f), 0.045f, 16);
                    mag.B.Sphere(MSkin, new Vector3(0, -0.07f, 0.16f), new Vector3(0.045f, 0.045f, 0.03f), 14, 6);
                    mag.Cyl(MSteel, new Vector3(0.0f, -0.025f, 0.12f), new Vector3(0.0f, -0.01f, 0.16f), 0.008f, 8);
                    mag.Box(MPoly, new Vector3(0.046f, -0.07f, 0.02f), new Vector3(0.002f, 0.03f, 0.1f));
                    gm.Mag = mag.T;
                    muzzleZ = 0.58f;
                    sightY = 0.06f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.07f, -0.05f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.07f, 0.19f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.07f, 0.03f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0, 0, 0));
                    break;
                }
                case WeaponId.Launcher:
                {
                    body.RBox(MSkin, new Vector3(0, 0.03f, 0.25f), new Vector3(0.07f, 0.07f, 0.12f), 0.01f);
                    body.Cyl(MSteel, new Vector3(0, 0.03f, 0.3f), new Vector3(0, 0.03f, 0.52f), 0.03f, 16);
                    body.Cyl(MPoly, new Vector3(0, 0.03f, 0.5f), new Vector3(0, 0.03f, 0.53f), 0.034f, 16);
                    body.Rail(new Vector3(0, 0.065f, 0.2f), 0.12f);
                    body.Box(MSteel, new Vector3(0, 0.03f, -0.05f), new Vector3(0.02f, 0.02f, 0.2f));
                    body.Ext(MPoly, V(-0.05f, 0.0f, -0.01f, 0.0f, -0.04f, -0.12f, -0.08f, -0.12f), 0.032f);
                    body.Ring(MSteel, new Vector3(0, -0.005f, 0.015f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MSkin, V(-0.15f, 0.06f, -0.36f, 0.04f, -0.37f, -0.08f, -0.33f, -0.08f, -0.15f, 0.0f), 0.04f);
                    body.Ext(MPoly, V(0.3f, -0.02f, 0.34f, -0.02f, 0.33f, -0.12f, 0.29f, -0.12f), 0.028f);
                    // Ladder sight.
                    body.Box(MSteel, new Vector3(0, 0.1f, 0.28f), new Vector3(0.02f, 0.05f, 0.004f));
                    var drum = Sub("Drum", new Vector3(0, 0.03f, 0.13f));
                    drum.Cyl(MSteel, new Vector3(0, 0, -0.08f), new Vector3(0, 0, 0.08f), 0.07f, 18);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60 * Mathf.Deg2Rad;
                        drum.Cyl(MPoly, new Vector3(Mathf.Cos(a) * 0.045f, Mathf.Sin(a) * 0.045f, -0.081f), new Vector3(Mathf.Cos(a) * 0.045f, Mathf.Sin(a) * 0.045f, -0.08f), 0.018f, 10);
                        drum.Box(MPoly, new Vector3(Mathf.Cos(a + 0.5f) * 0.071f, Mathf.Sin(a + 0.5f) * 0.071f, 0), new Vector3(0.006f, 0.006f, 0.15f));
                    }
                    gm.Drum = drum.T;
                    gm.Mag = drum.T;
                    muzzleZ = 0.53f;
                    sightY = 0.115f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.06f, -0.04f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.07f, 0.31f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0.06f, 0.03f, 0.13f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.05f, 0.03f, 0.1f));
                    break;
                }
                case WeaponId.ArcRifle:
                {
                    body.Ext(MSkin, V(-0.2f, -0.03f, 0.18f, -0.03f, 0.24f, 0.0f, 0.22f, 0.05f, 0.0f, 0.065f, -0.2f, 0.055f), 0.05f);
                    body.Rail(new Vector3(0, 0.065f, -0.1f), 0.16f);
                    for (int i = 0; i < 5; i++)
                    {
                        float z = 0.04f + i * 0.035f;
                        body.Cyl(MGlow, new Vector3(0, 0.012f, z), new Vector3(0, 0.012f, z + 0.012f), 0.03f, 16);
                    }
                    // Prongs.
                    foreach (var y in new[] { 0.035f, -0.01f })
                        body.Box(MSteel, new Vector3(0, y, 0.3f), new Vector3(0.012f, 0.008f, 0.14f));
                    body.Cyl(MGlow, new Vector3(0, 0.012f, 0.24f), new Vector3(0, 0.012f, 0.36f), 0.004f, 8);
                    body.Ext(MPoly, V(-0.06f, -0.03f, -0.02f, -0.03f, -0.05f, -0.14f, -0.09f, -0.14f), 0.032f);
                    body.Ring(MSkin, new Vector3(0, -0.035f, 0.005f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MSkin, V(-0.2f, 0.05f, -0.38f, 0.03f, -0.39f, -0.08f, -0.35f, -0.08f, -0.2f, -0.02f), 0.044f);
                    var mag = Sub("Mag", Vector3.zero);
                    mag.RBox(MPoly, new Vector3(0, -0.07f, 0.09f), new Vector3(0.036f, 0.08f, 0.07f), 0.008f);
                    mag.Box(MGlow, new Vector3(0, -0.07f, 0.126f), new Vector3(0.02f, 0.05f, 0.002f));
                    gm.Mag = mag.T;
                    muzzleZ = 0.37f;
                    sightY = 0.09f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.075f, -0.04f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.02f, 0.18f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.08f, 0.09f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0, 0, 0));
                    break;
                }
                case WeaponId.Smg:
                {
                    // Compact receiver, vented barrel shroud, forward magazine, folding wire stock.
                    body.Ext(MSteel, V(-0.1f, 0.0f, 0.12f, 0.0f, 0.12f, 0.045f, -0.1f, 0.045f), 0.032f);
                    body.Ext(MSkin, V(-0.08f, -0.03f, 0.1f, -0.03f, 0.1f, 0.0f, -0.08f, 0.0f), 0.034f);
                    body.Rail(new Vector3(0, 0.045f, -0.08f), 0.17f);
                    body.Box(MPoly, new Vector3(0.0167f, 0.022f, 0.02f), new Vector3(0.002f, 0.014f, 0.04f));
                    body.Cyl(MSkin, new Vector3(0, 0.02f, 0.12f), new Vector3(0, 0.02f, 0.25f), 0.019f, 14);
                    for (int i = 0; i < 5; i++) body.Box(MPoly, new Vector3(0, 0.038f, 0.135f + i * 0.022f), new Vector3(0.012f, 0.004f, 0.012f));
                    body.Cyl(MSteel, new Vector3(0, 0.02f, 0.25f), new Vector3(0, 0.02f, 0.29f), 0.012f, 12);
                    for (int i = 0; i < 2; i++) body.Box(MPoly, new Vector3(0, 0.031f, 0.262f + i * 0.014f), new Vector3(0.008f, 0.002f, 0.006f));
                    muzzleZ = 0.29f;
                    body.Ext(MPoly, V(-0.05f, -0.03f, -0.015f, -0.03f, -0.04f, -0.12f, -0.078f, -0.12f, -0.074f, -0.03f), 0.03f);
                    body.Ring(MSteel, new Vector3(0, -0.035f, 0.0f), 0.02f, 0.003f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MPoly, V(0.15f, 0.0f, 0.2f, 0.0f, 0.19f, -0.06f, 0.16f, -0.06f), 0.028f);
                    foreach (var y in new[] { 0.035f, -0.01f }) body.Box(MSteel, new Vector3(0, y, -0.2f), new Vector3(0.008f, 0.008f, 0.2f));
                    body.RBox(MRubber, new Vector3(0, 0.012f, -0.305f), new Vector3(0.036f, 0.07f, 0.012f), 0.004f);
                    var bolt = Sub("Bolt", Vector3.zero);
                    bolt.Box(MSteel, new Vector3(-0.0185f, 0.032f, 0.06f), new Vector3(0.012f, 0.008f, 0.02f));
                    gm.Bolt = bolt.T;
                    var mag = Sub("Mag", Vector3.zero);
                    float ml = 0.13f + (extMag ? 0.05f : 0);
                    mag.Ext(MSteel, V(0.04f, -0.03f, 0.075f, -0.03f, 0.085f, -0.03f - ml, 0.05f, -0.03f - ml * 0.98f), 0.022f);
                    mag.Box(MPoly, new Vector3(0, -0.035f - ml, 0.067f), new Vector3(0.026f, 0.01f, 0.04f));
                    gm.Mag = mag.T;
                    sightY = 0.08f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.07f, -0.045f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.02f, 0.17f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, -0.07f, 0.062f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0.018f, 0.022f, 0.02f));
                    if (ws == null || ws.Optic < 0)
                    {
                        body.Box(MSteel, new Vector3(0, 0.062f, -0.07f), new Vector3(0.018f, 0.014f, 0.012f));
                        body.Box(MSteel, new Vector3(0, 0.06f, 0.1f), new Vector3(0.014f, 0.018f, 0.01f));
                        body.Box(MGlow, new Vector3(0, 0.07f, 0.1f), new Vector3(0.002f, 0.003f, 0.011f));
                    }
                    break;
                }
                case WeaponId.RocketLauncher:
                {
                    // Launch tube with a flared rear venturi, grips, iron sights and a loaded warhead up front.
                    body.Cyl(MSkin, new Vector3(0, 0.03f, -0.42f), new Vector3(0, 0.03f, 0.42f), 0.045f, 18);
                    body.Cyl(MSteel, new Vector3(0, 0.03f, -0.42f), new Vector3(0, 0.03f, -0.52f), 0.045f, 18, 0.064f);
                    foreach (var z in new[] { -0.3f, 0.0f, 0.3f }) body.Cyl(MPoly, new Vector3(0, 0.03f, z - 0.012f), new Vector3(0, 0.03f, z + 0.012f), 0.049f, 18);
                    body.Ext(MPoly, V(-0.06f, -0.015f, -0.02f, -0.015f, -0.045f, -0.13f, -0.085f, -0.13f), 0.032f);
                    body.Ring(MSteel, new Vector3(0, -0.02f, 0.005f), 0.022f, 0.004f, 180, 360, new Vector3(0, 90, 0));
                    body.Ext(MPoly, V(0.18f, -0.015f, 0.22f, -0.015f, 0.21f, -0.11f, 0.17f, -0.11f), 0.03f);
                    body.Box(MSteel, new Vector3(0, 0.09f, 0.18f), new Vector3(0.004f, 0.03f, 0.004f));
                    body.Ring(MSteel, new Vector3(0, 0.1f, -0.12f), 0.01f, 0.002f, 0, 360, Vector3.zero);
                    body.Box(MSteel, new Vector3(0, 0.085f, -0.12f), new Vector3(0.004f, 0.02f, 0.004f));
                    var mag = Sub("Mag", Vector3.zero);
                    mag.Cyl(MBrass, new Vector3(0, 0.03f, 0.42f), new Vector3(0, 0.03f, 0.52f), 0.04f, 16);
                    mag.Cyl(MSteel, new Vector3(0, 0.03f, 0.52f), new Vector3(0, 0.03f, 0.64f), 0.04f, 16, 0.01f);
                    gm.Mag = mag.T;
                    muzzleZ = 0.64f;
                    sightY = 0.1f;
                    gm.GripR = Pt(root.transform, "GripR", new Vector3(0, -0.07f, -0.045f));
                    gm.GripL = Pt(root.transform, "GripL", new Vector3(0, -0.06f, 0.2f));
                    gm.MagWell = Pt(root.transform, "MagWell", new Vector3(0, 0.03f, 0.48f));
                    gm.Eject = Pt(root.transform, "Eject", new Vector3(0, 0, 0));
                    break;
                }
            }

            // ------------------------------------------------------------ attachments
            var optic = ws != null ? ws.Optic : (def.BuiltInScope ? (int)Attachment.Scope : -1);
            if (def.BuiltInScope) optic = (int)Attachment.Scope;
            float railY = sightY - 0.02f;
            if (optic == (int)Attachment.RedDot)
            {
                // Open-window micro red dot: mount, short hood around a glass window, dot on the glass.
                var o = Sub("Optic", Vector3.zero);
                float cy = railY + 0.032f;
                o.RBox(MSteel, new Vector3(0, railY + 0.0085f, 0.002f), new Vector3(0.026f, 0.017f, 0.046f), 0.003f);
                o.Box(MSteel, new Vector3(0.0135f, cy, 0.006f), new Vector3(0.004f, 0.03f, 0.03f));
                o.Box(MSteel, new Vector3(-0.0135f, cy, 0.006f), new Vector3(0.004f, 0.03f, 0.03f));
                o.RBox(MSteel, new Vector3(0, cy + 0.0145f, 0.006f), new Vector3(0.031f, 0.005f, 0.03f), 0.002f);
                o.Box(MSteel, new Vector3(0.0135f, cy - 0.006f, -0.014f), new Vector3(0.004f, 0.012f, 0.012f));
                o.Box(MGlass, new Vector3(0, cy, 0.019f), new Vector3(0.023f, 0.025f, 0.0008f));
                o.Box(MReticle, new Vector3(0, cy, 0.0182f), new Vector3(0.0017f, 0.0017f, 0.0003f));
                sightY = cy;
                gm.HasOptic = true;
            }
            else if (optic == (int)Attachment.Holo)
            {
                var o = Sub("Optic", Vector3.zero);
                o.RBox(MSteel, new Vector3(0, railY + 0.012f, -0.0f), new Vector3(0.034f, 0.018f, 0.07f), 0.004f);
                o.Box(MSteel, new Vector3(0.016f, railY + 0.04f, 0.02f), new Vector3(0.003f, 0.04f, 0.03f));
                o.Box(MSteel, new Vector3(-0.016f, railY + 0.04f, 0.02f), new Vector3(0.003f, 0.04f, 0.03f));
                o.Box(MSteel, new Vector3(0, railY + 0.061f, 0.02f), new Vector3(0.035f, 0.003f, 0.03f));
                o.Box(MGlass, new Vector3(0, railY + 0.04f, 0.03f), new Vector3(0.028f, 0.034f, 0.001f));
                o.Ring(MReticle, new Vector3(0, railY + 0.04f, 0.029f), 0.006f, 0.0005f, 0, 360, Vector3.zero);
                o.Box(MReticle, new Vector3(0, railY + 0.04f, 0.029f), new Vector3(0.001f, 0.001f, 0.0003f));
                sightY = railY + 0.04f;
                gm.HasOptic = true;
            }
            else if (optic == (int)Attachment.Scope)
            {
                var o = Sub("Optic", Vector3.zero);
                float sy = railY + 0.042f;
                o.Cyl(MSteel, new Vector3(0, sy, -0.12f), new Vector3(0, sy, 0.12f), 0.016f, 16);
                o.Cyl(MSteel, new Vector3(0, sy, 0.12f), new Vector3(0, sy, 0.19f), 0.016f, 16, 0.024f);
                o.Cyl(MSteel, new Vector3(0, sy, -0.12f), new Vector3(0, sy, -0.16f), 0.016f, 16, 0.02f);
                o.Cyl(MSteel, new Vector3(0, sy + 0.012f, -0.01f), new Vector3(0, sy + 0.028f, -0.01f), 0.008f, 10);
                o.Cyl(MSteel, new Vector3(0.012f, sy, -0.01f), new Vector3(0.028f, sy, -0.01f), 0.008f, 10);
                foreach (var z in new[] { -0.07f, 0.07f }) o.Box(MSteel, new Vector3(0, railY + 0.02f, z), new Vector3(0.024f, 0.032f, 0.018f));
                o.B.Disc(MGlass, new Vector3(0, sy, 0.191f), Vector3.forward, Vector3.right, 0.022f, 16);
                o.B.Disc(MGlass, new Vector3(0, sy, -0.161f), Vector3.back, Vector3.right, 0.018f, 16);
                gm.HasScope = true;
                sightY = sy;
            }
            if (ws != null && ws.Suppressor)
            {
                var o = Sub("Suppressor", Vector3.zero);
                float y = id == WeaponId.StandardPistol || id == WeaponId.TacticalPistol || id == WeaponId.HeavyPistol ? (id == WeaponId.HeavyPistol ? 0.033f : 0.026f) : 0.02f;
                o.Cyl(MPoly, new Vector3(0, y, muzzleZ), new Vector3(0, y, muzzleZ + 0.17f), 0.018f, 16);
                o.Cyl(MSteel, new Vector3(0, y, muzzleZ + 0.165f), new Vector3(0, y, muzzleZ + 0.172f), 0.012f, 12);
                muzzleZ += 0.17f;
            }
            if (ws != null && ws.Foregrip && gm.GripL != null)
            {
                var o = Sub("Foregrip", Vector3.zero);
                var g = gm.GripL.localPosition;
                o.Cyl(MPoly, new Vector3(0, g.y + 0.0f, g.z), new Vector3(0, g.y - 0.085f, g.z - 0.01f), 0.016f, 12);
                gm.GripL.localPosition = new Vector3(0, g.y - 0.05f, g.z - 0.005f);
            }

            float muzzleY = id == WeaponId.StandardPistol || id == WeaponId.TacticalPistol ? 0.026f : id == WeaponId.HeavyPistol ? 0.033f : id == WeaponId.PumpShotgun ? 0.032f : id == WeaponId.Launcher || id == WeaponId.RocketLauncher ? 0.03f : id == WeaponId.Flamethrower ? -0.0f : 0.02f;
            gm.Muzzle = Pt(root.transform, "Muzzle", new Vector3(0, muzzleY, muzzleZ));
            gm.Sight = Pt(root.transform, "Sight", new Vector3(0, sightY, 0));
            gm.Length = muzzleZ;
            foreach (var p in parts) p.Finish(mats, layer, gm.Renderers, shadows);
            if (gm.Mag != null) gm.MagRest = gm.Mag.localPosition;
            if (gm.Slide != null) gm.SlideRest = gm.Slide.localPosition;
            if (gm.Pump != null) gm.PumpRest = gm.Pump.localPosition;
            if (gm.Bolt != null) { gm.BoltRest = gm.Bolt.localPosition; gm.BoltRotRest = gm.Bolt.localRotation; }
            if (gm.Lid != null) gm.LidRest = gm.Lid.localRotation;
            if (gm.Drum != null) gm.DrumRest = gm.Drum.localRotation;
            return gm;
        }
    }
}
