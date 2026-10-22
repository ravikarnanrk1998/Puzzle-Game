using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCC.Game
{
    public enum HeadGear { Helmet, Cap, Hat, None }

    /// <summary>Kit and equipment of one player.</summary>
    public sealed class Look
    {
        public Color Shirt, Trim, Trousers, Skin, Hair = new Color(0.09f, 0.07f, 0.05f), Helmet;
        public HeadGear Gear = HeadGear.Cap;
        public bool Pads, Gloves, KeeperGloves, Bat;
        public string Name;
        public int Number = -1;

        public static readonly Color[] SkinTones =
        {
            new Color32(0x8A, 0x5A, 0x3B, 255), new Color32(0x6E, 0x46, 0x30, 255), new Color32(0xA0, 0x70, 0x4C, 255),
            new Color32(0x5A, 0x3A, 0x26, 255), new Color32(0xB9, 0x85, 0x63, 255), new Color32(0xC7, 0x9A, 0x74, 255),
        };

        public static Color SkinFor(string seed) => SkinTones[Mathf.Abs(seed.GetHashCode()) % SkinTones.Length];

        static Color TrousersFor(Color shirt) => shirt.Luma() > 0.55f ? shirt.Darken(0.12f) : shirt.Darken(0.18f);
        static Color HelmetFor(Color shirt) => shirt.Luma() > 0.55f ? new Color(0.11f, 0.16f, 0.35f) : shirt.Darken(0.15f);

        public static Look Batter(Color shirt, Color trim, string name, int number, Color skin) => new Look
        {
            Shirt = shirt, Trim = trim, Trousers = TrousersFor(shirt), Skin = skin, Gear = HeadGear.Helmet, Helmet = HelmetFor(shirt),
            Pads = true, Gloves = true, Bat = true, Name = name, Number = number,
        };

        public static Look Fielder(Color shirt, Color trim, string name, int number, Color skin) => new Look
        {
            Shirt = shirt, Trim = trim, Trousers = TrousersFor(shirt), Skin = skin, Name = name, Number = number,
        };

        public static Look Keeper(Color shirt, Color trim, string name, int number, Color skin) => new Look
        {
            Shirt = shirt, Trim = trim, Trousers = TrousersFor(shirt), Skin = skin, Gear = HeadGear.Helmet, Helmet = HelmetFor(shirt),
            Pads = true, KeeperGloves = true, Name = name, Number = number,
        };

        public static Look Umpire(Color skin) => new Look
        {
            Shirt = new Color(0.14f, 0.23f, 0.43f), Trim = new Color(0.9f, 0.22f, 0.21f), Trousers = new Color(0.08f, 0.09f, 0.11f),
            Skin = skin, Gear = HeadGear.Hat,
        };
    }

    /// <summary>
    /// A 3D cricketer built from shaded parts (no imported model needed).
    /// Call <see cref="Apply"/> every frame with the current pose.
    /// </summary>
    public sealed class Cricketer : MonoBehaviour
    {
        static readonly Color Pad = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color Shoe = new Color(0.96f, 0.96f, 0.96f);
        static readonly Color Sole = new Color(0.15f, 0.15f, 0.18f);
        static readonly Color Wood = new Color(0.9f, 0.8f, 0.57f);
        static readonly Color GripC = new Color(0.1f, 0.1f, 0.14f);
        static readonly Color Metal = new Color(0.78f, 0.8f, 0.84f);

        public Look Look { get; private set; }
        public Skeleton Skeleton { get; } = new Skeleton();

        Transform _pelvis, _waist, _chest, _yoke, _neck, _head, _hair, _nose, _eyeL, _eyeR;
        Transform _helmet, _peak, _grille0, _grille1, _grille2, _grilleV, _cap, _capPeak, _hatBrim, _hatCrown;
        Transform _shoL, _shoR, _sleeveL, _sleeveR, _trimL, _trimR, _upperL, _upperR, _elbL, _elbR, _foreL, _foreR, _handL, _handR;
        Transform _thighL, _thighR, _kneeL, _kneeR, _shinL, _shinR, _padL, _padR, _padKneeL, _padKneeR, _shoeL, _shoeR, _soleL, _soleR;
        Transform _handle, _blade, _sticker, _number;
        readonly List<Renderer> _detail = new List<Renderer>();
        bool _detailOn = true;

        public static Cricketer Create(Transform parent, Look look, string name = "Player")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Cricketer>();
            c.Build(look);
            return c;
        }

        Transform Part(string n, Mesh mesh, Material mat, bool detail = false, bool shadow = true)
        {
            var go = MeshKit.Obj(n, transform, mesh, mat, shadow, true);
            if (detail) _detail.Add(go.GetComponent<Renderer>());
            return go.transform;
        }

        void Build(Look look)
        {
            Look = look;
            var shirt = Mats.Lit(look.Shirt, 0.32f);
            var trim = Mats.Lit(look.Trim, 0.35f);
            var trousers = Mats.Lit(look.Trousers, 0.28f);
            var skin = Mats.Lit(look.Skin, 0.35f);
            var hair = Mats.Lit(look.Hair, 0.2f);
            var white = Mats.Lit(Pad, 0.3f);
            var sphere = MeshKit.Sphere;
            var cube = MeshKit.Cube;

            _pelvis = Part("Pelvis", sphere, trousers);
            _waist = Part("Waist", sphere, shirt);
            _chest = Part("Chest", sphere, shirt);
            _yoke = Part("Collar", sphere, trim, true);
            _neck = Part("Neck", MeshKit.TaperedLimb(0.052f, 0.046f), skin);
            _head = Part("Head", sphere, skin);
            _hair = Part("Hair", sphere, hair);
            _nose = Part("Nose", sphere, Mats.Lit(look.Skin.Darken(0.08f), 0.3f), true, false);
            var eye = Mats.Lit(new Color(0.05f, 0.04f, 0.04f), 0.8f);
            _eyeL = Part("EyeL", sphere, eye, true, false);
            _eyeR = Part("EyeR", sphere, eye, true, false);

            switch (look.Gear)
            {
                case HeadGear.Helmet:
                {
                    var hm = Mats.Lit(look.Helmet, 0.75f);
                    _helmet = Part("Helmet", sphere, hm);
                    _peak = Part("Peak", cube, Mats.Lit(look.Helmet.Darken(0.25f), 0.6f));
                    var metal = Mats.Lit(Metal, 0.8f, null, false, 0.8f);
                    _grille0 = Part("Grille0", cube, metal, true, false);
                    _grille1 = Part("Grille1", cube, metal, true, false);
                    _grille2 = Part("Grille2", cube, metal, true, false);
                    _grilleV = Part("GrilleV", cube, metal, true, false);
                    break;
                }
                case HeadGear.Cap:
                    _cap = Part("Cap", sphere, Mats.Lit(look.Shirt.Darken(0.1f), 0.3f));
                    _capPeak = Part("CapPeak", cube, Mats.Lit(look.Shirt.Darken(0.25f), 0.3f));
                    break;
                case HeadGear.Hat:
                    var hat = Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.25f);
                    _hatBrim = Part("Brim", sphere, hat);
                    _hatCrown = Part("Crown", sphere, hat);
                    break;
            }

            _shoL = Part("ShoulderL", sphere, shirt);
            _shoR = Part("ShoulderR", sphere, shirt);
            var sleeve = MeshKit.TaperedLimb(0.072f, 0.064f);
            _sleeveL = Part("SleeveL", sleeve, shirt);
            _sleeveR = Part("SleeveR", sleeve, shirt);
            var trimBand = MeshKit.TaperedLimb(0.066f, 0.064f);
            _trimL = Part("TrimL", trimBand, trim, true);
            _trimR = Part("TrimR", trimBand, trim, true);
            var upper = MeshKit.TaperedLimb(0.054f, 0.049f);
            _upperL = Part("UpperL", upper, skin);
            _upperR = Part("UpperR", upper, skin);
            _elbL = Part("ElbowL", sphere, skin, true);
            _elbR = Part("ElbowR", sphere, skin, true);
            var fore = MeshKit.TaperedLimb(0.049f, 0.036f);
            _foreL = Part("ForeL", fore, skin);
            _foreR = Part("ForeR", fore, skin);
            var handMat = look.Gloves || look.KeeperGloves ? Mats.Lit(look.KeeperGloves ? new Color(0.95f, 0.93f, 0.86f) : Shoe, 0.35f) : skin;
            _handL = Part("HandL", sphere, handMat);
            _handR = Part("HandR", sphere, handMat);

            var thigh = MeshKit.TaperedLimb(0.1f, 0.07f);
            var shin = MeshKit.TaperedLimb(0.068f, 0.047f);
            _thighL = Part("ThighL", thigh, trousers);
            _thighR = Part("ThighR", thigh, trousers);
            _kneeL = Part("KneeL", sphere, trousers);
            _kneeR = Part("KneeR", sphere, trousers);
            _shinL = Part("ShinL", shin, trousers);
            _shinR = Part("ShinR", shin, trousers);
            if (look.Pads)
            {
                var pad = MeshKit.TaperedLimb(0.085f, 0.072f, 10);
                _padL = Part("PadL", pad, white);
                _padR = Part("PadR", pad, white);
                _padKneeL = Part("PadKneeL", sphere, white);
                _padKneeR = Part("PadKneeR", sphere, white);
            }
            var shoe = Mats.Lit(Shoe, 0.45f);
            _shoeL = Part("ShoeL", sphere, shoe);
            _shoeR = Part("ShoeR", sphere, shoe);
            var sole = Mats.Lit(Sole, 0.2f);
            _soleL = Part("SoleL", cube, sole, true);
            _soleR = Part("SoleR", cube, sole, true);

            if (look.Bat)
            {
                _handle = Part("Handle", MeshKit.TaperedLimb(0.019f, 0.017f, 8), Mats.Lit(GripC, 0.3f));
                _blade = Part("Blade", cube, Mats.Lit(Wood, 0.35f));
                _sticker = Part("Sticker", cube, Mats.Lit(new Color(0.85f, 0.12f, 0.16f), 0.4f), true, false);
            }

            if (look.Number >= 0 && !string.IsNullOrEmpty(look.Name))
            {
                var go = MeshKit.Obj("Number", transform, MeshKit.Quad, Mats.LitCutout(NumberTexture(look)), false, true);
                _number = go.transform;
                _detail.Add(go.GetComponent<Renderer>());
            }
        }

        static readonly Dictionary<string, Texture2D> NumberCache = new Dictionary<string, Texture2D>();

        static Texture2D NumberTexture(Look look)
        {
            var textCol = look.Trim.Luma() < 0.25f || Mathf.Abs(look.Trim.Luma() - look.Shirt.Luma()) < 0.12f ? Color.white : look.Trim;
            string key = look.Name + look.Number + ColorUtility.ToHtmlStringRGB(textCol);
            if (NumberCache.TryGetValue(key, out var t) && t != null) return t;
            var px = new Px(160, 160, new Color32(0, 0, 0, 0));
            TextBaker.Draw(px, look.Name.ToUpperInvariant(), 80, 120, look.Name.Length > 9 ? 18 : 24, textCol);
            TextBaker.Draw(px, look.Number.ToString(), 80, 20, 86, textCol);
            t = px.ToTexture("Number", true, TextureWrapMode.Clamp, FilterMode.Bilinear, 1);
            NumberCache[key] = t;
            return t;
        }

        // ------------------------------------------------------- placement

        static void Seg(Transform t, V3 a, V3 b, float radiusScale)
        {
            if (t == null) return;
            var d = (b - a).U;
            float len = d.magnitude;
            t.localPosition = a.U;
            t.localRotation = len > 1e-5f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            t.localScale = new Vector3(radiusScale, Mathf.Max(0.001f, len), radiusScale);
        }

        /// <summary>Ellipsoid / box with radii along (right, up, forward) of the given frame.</summary>
        static void Frame(Transform t, V3 c, V3 r, V3 f, V3 u, double rx, double ry, double rz)
        {
            if (t == null) return;
            t.localPosition = c.U;
            var fu = f.U;
            var uu = u.U;
            if (fu.sqrMagnitude < 1e-8 || uu.sqrMagnitude < 1e-8) return;
            t.localRotation = Quaternion.LookRotation(fu, uu);
            t.localScale = new Vector3((float)rx, (float)ry, (float)rz);
        }

        static void Ball(Transform t, V3 c, double r)
        {
            if (t == null) return;
            t.localPosition = c.U;
            t.localScale = Vector3.one * (float)r;
        }

        public void SetDetail(bool on)
        {
            if (on == _detailOn) return;
            _detailOn = on;
            foreach (var r in _detail) r.enabled = on;
        }

        /// <summary>Poses the player. <paramref name="facing"/>: 0 = towards the striker's end (+y).</summary>
        public void Apply(Pose pose, V3 foot, double facing, bool mirror = false, double scale = 1)
        {
            var k = Skeleton;
            k.Solve(pose, foot, facing, scale, mirror);
            double s = k.S;
            float fs = (float)s;

            // Torso.
            Frame(_pelvis, k.Pelvis - k.PU * (0.03 * s), k.PR, k.PF, k.PU, 0.168 * s, 0.125 * s, 0.115 * s);
            Frame(_waist, k.Pelvis + k.CU * (0.12 * s), k.CR, k.CF, k.CU, 0.165 * s, 0.16 * s, 0.112 * s);
            Frame(_chest, k.Pelvis + k.CU * (0.33 * s), k.CR, k.CF, k.CU, 0.215 * s, 0.2 * s, 0.13 * s);
            Frame(_yoke, k.Neck - k.CU * (0.02 * s), k.CR, k.CF, k.CU, 0.085 * s, 0.035 * s, 0.075 * s);
            Seg(_neck, k.Neck - k.CU * (0.04 * s), k.Head - k.HU * (0.07 * s), fs);

            // Head.
            Frame(_head, k.Head, k.HR, k.HF, k.HU, 0.096 * s, 0.118 * s, 0.108 * s);
            Frame(_hair, k.Head + k.HU * (0.022 * s) - k.HF * (0.012 * s), k.HR, k.HF, k.HU, 0.1 * s, 0.105 * s, 0.11 * s);
            Ball(_nose, k.Head + k.HF * (0.105 * s) - k.HU * (0.01 * s), 0.018 * s);
            Ball(_eyeL, k.Head + k.HF * (0.094 * s) + k.HU * (0.022 * s) - k.HR * (0.036 * s), 0.012 * s);
            Ball(_eyeR, k.Head + k.HF * (0.094 * s) + k.HU * (0.022 * s) + k.HR * (0.036 * s), 0.012 * s);
            if (_helmet != null)
            {
                Frame(_helmet, k.Head + k.HU * (0.025 * s) - k.HF * (0.01 * s), k.HR, k.HF, k.HU, 0.124 * s, 0.13 * s, 0.136 * s);
                Frame(_peak, k.Head + k.HF * (0.13 * s) + k.HU * (0.04 * s), k.HR, k.HF, k.HU, 0.088 * s, 0.012 * s, 0.05 * s);
                var g = k.Head + k.HF * (0.128 * s);
                Frame(_grille0, g - k.HU * (0.035 * s), k.HR, k.HF, k.HU, 0.085 * s, 0.006 * s, 0.006 * s);
                Frame(_grille1, g - k.HU * (0.075 * s), k.HR, k.HF, k.HU, 0.08 * s, 0.006 * s, 0.006 * s);
                Frame(_grille2, g - k.HU * (0.112 * s) - k.HF * (0.01 * s), k.HR, k.HF, k.HU, 0.07 * s, 0.006 * s, 0.006 * s);
                Frame(_grilleV, g - k.HU * (0.07 * s), k.HR, k.HF, k.HU, 0.006 * s, 0.05 * s, 0.006 * s);
            }
            if (_cap != null)
            {
                Frame(_cap, k.Head + k.HU * (0.05 * s) - k.HF * (0.005 * s), k.HR, k.HF, k.HU, 0.106 * s, 0.085 * s, 0.116 * s);
                Frame(_capPeak, k.Head + k.HF * (0.15 * s) + k.HU * (0.045 * s), k.HR, k.HF, k.HU, 0.078 * s, 0.008 * s, 0.06 * s);
            }
            if (_hatBrim != null)
            {
                Frame(_hatBrim, k.Head + k.HU * (0.06 * s), k.HR, k.HF, k.HU, 0.2 * s, 0.014 * s, 0.2 * s);
                Frame(_hatCrown, k.Head + k.HU * (0.07 * s), k.HR, k.HF, k.HU, 0.106 * s, 0.09 * s, 0.112 * s);
            }

            // Arms.
            _handRadius = Look.KeeperGloves ? 0.068 : (Look.Gloves ? 0.052 : 0.041);
            PlaceArm(k.LSho, k.LElb, k.LHand, _shoL, _sleeveL, _trimL, _upperL, _elbL, _foreL, _handL, k, s);
            PlaceArm(k.RSho, k.RElb, k.RHand, _shoR, _sleeveR, _trimR, _upperR, _elbR, _foreR, _handR, k, s);

            // Legs.
            PlaceLeg(k.LHip, k.LKnee, k.LAnkle, k.LToe, _thighL, _kneeL, _shinL, _padL, _padKneeL, _shoeL, _soleL, k, s);
            PlaceLeg(k.RHip, k.RKnee, k.RAnkle, k.RToe, _thighR, _kneeR, _shinR, _padR, _padKneeR, _shoeR, _soleR, k, s);

            // Bat.
            if (_handle != null)
            {
                bool show = k.HasBat;
                _handle.gameObject.SetActive(show);
                _blade.gameObject.SetActive(show);
                _sticker.gameObject.SetActive(show && _detailOn);
                if (show)
                {
                    var bd = (k.BatTip - k.BatTop).Normalized;
                    var side = bd.Cross(k.BatFace).Normalized;
                    var handleEnd = k.BatTop + bd * (0.3 * s);
                    Seg(_handle, k.BatTop, handleEnd, fs);
                    var bc = handleEnd + bd * (0.28 * s);
                    Frame(_blade, bc, side, k.BatFace, bd, 0.054 * s, 0.28 * s, 0.022 * s);
                    Frame(_sticker, bc - bd * (0.12 * s) + k.BatFace * (0.023 * s), side, k.BatFace, bd, 0.05 * s, 0.06 * s, 0.002 * s);
                }
            }

            // Shirt name and number on the back.
            if (_number != null)
            {
                var back = k.Pelvis + k.CU * (0.32 * s) - k.CF * (0.133 * s);
                _number.localPosition = back.U;
                _number.localRotation = Quaternion.LookRotation(k.CF.U, k.CU.U);
                _number.localScale = new Vector3(0.24f * fs, 0.24f * fs, 1);
            }
        }

        static void PlaceArm(V3 sho, V3 elb, V3 hand, Transform tSho, Transform tSleeve, Transform tTrim, Transform tUpper, Transform tElb,
            Transform tFore, Transform tHand, Skeleton k, double s)
        {
            float fs = (float)s;
            Frame(tSho, sho, k.CR, k.CF, k.CU, 0.082 * s, 0.078 * s, 0.076 * s);
            var sleeveEnd = V3.Lerp(sho, elb, 0.58);
            var hem = V3.Lerp(sho, elb, 0.66);
            Seg(tSleeve, sho, sleeveEnd, fs);
            Seg(tTrim, sleeveEnd, hem, fs);
            Seg(tUpper, hem, elb, fs);
            Ball(tElb, elb, 0.049 * s);
            var wrist = V3.Lerp(elb, hand, 0.84);
            Seg(tFore, elb, wrist, fs);
            Ball(tHand, hand, (_handRadius > 0 ? _handRadius : 0.041) * s);
        }

        [System.ThreadStatic] static double _handRadius;

        static void PlaceLeg(V3 hip, V3 knee, V3 ankle, V3 toe, Transform tThigh, Transform tKnee, Transform tShin, Transform tPad,
            Transform tPadKnee, Transform tShoe, Transform tSole, Skeleton k, double s)
        {
            float fs = (float)s;
            Seg(tThigh, hip, knee, fs);
            Ball(tKnee, knee, 0.07 * s);
            Seg(tShin, knee, ankle, fs);
            var shin = ankle - knee;
            var footDir = (toe - ankle).Normalized;
            var sn = shin.Normalized;
            var front = footDir - sn * footDir.Dot(sn);
            front = front.Length < 1e-4 ? k.Fwd : front.Normalized;
            if (tPad != null)
            {
                Seg(tPad, knee + front * (0.045 * s) - shin * 0.1, ankle + front * (0.04 * s) - shin * 0.04, fs);
                var side = sn.Cross(front).Normalized;
                Frame(tPadKnee, knee + front * (0.07 * s), side, front, -sn, 0.08 * s, 0.07 * s, 0.05 * s);
            }
            var up = V3.Up;
            var sideF = footDir.Cross(up);
            if (sideF.Length < 1e-4) sideF = new V3(1, 0, 0);
            sideF = sideF.Normalized;
            var flatF = up.Cross(sideF).Normalized;
            var sc = ankle + footDir * (0.07 * s) - up * (0.03 * s);
            Frame(tShoe, sc, sideF, flatF, up, 0.054 * s, 0.05 * s, 0.13 * s);
            Frame(tSole, sc - up * (0.04 * s), sideF, flatF, up, 0.05 * s, 0.012 * s, 0.125 * s);
        }

        public void SetShadows(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }
}
