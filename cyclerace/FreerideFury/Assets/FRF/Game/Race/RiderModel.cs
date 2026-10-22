using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FRF
{
    /// <summary>Joint targets of a rider pose in bike space (+Z forward, +Y up, origin on the ground between the wheels).</summary>
    public struct RiderPose
    {
        public Vector3 Pelvis, Neck, HandL, HandR, FootL, FootR, ElbowPoleL, ElbowPoleR, KneePoleL, KneePoleR;
        public float HeadPitch;

        public static RiderPose Lerp(RiderPose a, RiderPose b, float t)
        {
            return new RiderPose
            {
                Pelvis = Vector3.Lerp(a.Pelvis, b.Pelvis, t), Neck = Vector3.Lerp(a.Neck, b.Neck, t),
                HandL = Vector3.Lerp(a.HandL, b.HandL, t), HandR = Vector3.Lerp(a.HandR, b.HandR, t),
                FootL = Vector3.Lerp(a.FootL, b.FootL, t), FootR = Vector3.Lerp(a.FootR, b.FootR, t),
                ElbowPoleL = Vector3.Lerp(a.ElbowPoleL, b.ElbowPoleL, t), ElbowPoleR = Vector3.Lerp(a.ElbowPoleR, b.ElbowPoleR, t),
                KneePoleL = Vector3.Lerp(a.KneePoleL, b.KneePoleL, t), KneePoleR = Vector3.Lerp(a.KneePoleR, b.KneePoleR, t),
                HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t),
            };
        }
    }

    /// <summary>
    /// Procedural downhill bike and rider (no imported models). <see cref="Sync"/> places it
    /// from a <see cref="Rider"/> each frame; <see cref="ShowPose"/> poses it for menus.
    /// </summary>
    public sealed class RiderModel : MonoBehaviour
    {
        public const float WheelR = 0.36f;
        static readonly Vector3 RearAxle = new Vector3(0, WheelR, -0.6f), FrontAxle = new Vector3(0, WheelR, 0.6f);
        static readonly Vector3 Bb = new Vector3(0, 0.37f, -0.07f);
        static readonly Vector3 GripL = new Vector3(-0.35f, 1.1f, 0.31f), GripR = new Vector3(0.35f, 1.1f, 0.31f);
        const float CrankLen = 0.17f, PedalX = 0.13f;
        const float Thigh = 0.44f, Shin = 0.43f, UpperArm = 0.29f, ForeArm = 0.31f;

        Transform _bike, _body, _wheelF, _wheelR, _crank, _pedalL, _pedalR, _bars;
        Transform _torso, _chest, _hips, _head, _visor, _chin, _goggle;
        Transform _upperL, _upperR, _foreL, _foreR, _handL, _handR, _thighL, _thighR, _shinL, _shinR, _kneeL, _kneeR, _shoeL, _shoeR, _shoulderL, _shoulderR;
        Transform _shadow;
        readonly List<Renderer> _renderers = new List<Renderer>();
        float _wheelAngle;
        RiderPose _pose;
        bool _hasPose;

        public RiderDef Def { get; private set; }

        public static RiderModel Create(Transform parent, RiderDef def, BikeDef bike, Color paint, bool shadows = true)
        {
            var go = new GameObject("Rider " + def.Nick);
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<RiderModel>();
            m.Def = def;
            m.BuildBike(bike, paint, shadows);
            m.BuildBody(def, shadows);
            m.BuildShadow();
            Palette.Apply();
            return m;
        }

        Transform Part(Transform parent, string n, Mesh mesh, Material mat, bool shadow = true)
        {
            var go = MeshKit.Obj(n, parent, mesh, mat, shadow, true);
            _renderers.Add(go.GetComponent<Renderer>());
            return go.transform;
        }

        // ------------------------------------------------------------ bike

        static Mesh WheelMesh(Color rim, Color hub)
        {
            var b = new MeshBuilder();
            var tire = Mats.Hex(0x1A1A1C);
            const int seg = 18;
            const float ro = WheelR, ri = WheelR - 0.06f, hw = 0.035f, rr = ri - 0.025f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Vector3 P(float r, float a, float x) => new Vector3(x, Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                var tu = Palette.Uv(i % 2 == 0 ? tire : tire.Lighten(0.06f));
                b.FlatQuad(P(ro, a0, -hw), P(ro, a1, -hw), P(ro, a1, hw), P(ro, a0, hw), tu, Vector3.zero);
                var ru = Palette.Uv(rim);
                // Tyre and rim side walls (both sides).
                foreach (float x in new[] { -hw, hw })
                {
                    var inside = new Vector3(-x * 4, 0, 0);
                    b.FlatQuad(P(ri, a0, x), P(ro, a0, x), P(ro, a1, x), P(ri, a1, x), Palette.Uv(tire), inside);
                    b.FlatQuad(P(rr, a0, x * 0.6f), P(ri, a0, x * 0.8f), P(ri, a1, x * 0.8f), P(rr, a1, x * 0.6f), ru, inside);
                }
            }
            var spoke = Mats.Hex(0xC9CED6);
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2;
                float x = k % 2 == 0 ? 0.03f : -0.03f;
                b.Beam(new Vector3(x, 0, 0), new Vector3(0, Mathf.Sin(a) * rr, Mathf.Cos(a) * rr), 0.008f, 0.008f, spoke);
            }
            b.Cylinder(new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), 0.03f, 0.03f, 8, hub);
            // Brake disc on the left.
            b.Cylinder(new Vector3(-0.045f, 0, 0), new Vector3(-0.04f, 0, 0), 0.1f, 0.1f, 12, Mats.Hex(0xB8BEC6));
            return b.Build("Wheel");
        }

        void BuildBike(BikeDef bike, Color paint, bool shadows)
        {
            _bike = new GameObject("Bike").transform;
            _bike.SetParent(transform, false);
            var frame = paint.a > 0.01f ? paint : bike.Frame;
            var accent = bike.Accent;
            var black = Mats.Hex(0x16171B);
            var silver = Mats.Hex(0xB9C0C9);
            var gold = Mats.Hex(0xE8B23A);
            var b = new MeshBuilder();
            var headTop = new Vector3(0, 1.0f, 0.36f);
            var headBot = new Vector3(0, 0.84f, 0.43f);
            var seatTop = new Vector3(0, 0.86f, -0.3f);
            // Main triangle.
            b.Cylinder(headBot, headTop, 0.045f, 0.045f, 8, frame);
            b.Cylinder(headBot + new Vector3(0, 0.02f, -0.02f), Bb + new Vector3(0, 0.02f, 0.03f), 0.042f, 0.036f, 8, frame);
            b.Cylinder(headTop + new Vector3(0, -0.04f, 0), new Vector3(0, 0.8f, -0.22f), 0.03f, 0.027f, 8, frame);
            b.Cylinder(Bb, seatTop, 0.03f, 0.028f, 8, frame);
            // Rear end.
            foreach (float x in new[] { -0.06f, 0.06f })
            {
                b.Cylinder(Bb + new Vector3(x * 0.6f, 0, 0), RearAxle + new Vector3(x, 0, 0), 0.022f, 0.018f, 6, frame);
                b.Cylinder(new Vector3(x * 0.4f, 0.76f, -0.25f), RearAxle + new Vector3(x, 0, 0), 0.018f, 0.015f, 6, frame);
            }
            if (!bike.Hardtail)
            {
                // Coil shock.
                b.Cylinder(new Vector3(0, 0.6f, -0.02f), new Vector3(0, 0.79f, -0.2f), 0.022f, 0.022f, 6, black);
                b.Cylinder(new Vector3(0, 0.63f, -0.05f), new Vector3(0, 0.75f, -0.16f), 0.038f, 0.038f, 8, accent);
            }
            // Dual-crown fork.
            foreach (float x in new[] { -0.075f, 0.075f })
            {
                var top = new Vector3(x, 1.03f, 0.345f);
                var low = FrontAxle + new Vector3(x, 0, 0);
                var mid = Vector3.Lerp(top, low, 0.45f);
                b.Cylinder(top, mid, 0.02f, 0.02f, 6, silver);
                b.Cylinder(mid, low, 0.026f, 0.024f, 6, accent.Luma() > 0.85f || accent.Luma() < 0.1f ? black : accent);
            }
            b.Box(new Vector3(0, 1.02f, 0.35f), new Vector3(0.1f, 0.018f, 0.035f), black);
            b.Box(new Vector3(0, 0.86f, 0.42f), new Vector3(0.1f, 0.018f, 0.035f), black);
            // Stem, bar, grips.
            b.Cylinder(headTop, new Vector3(0, 1.08f, 0.33f), 0.022f, 0.022f, 6, black);
            b.Cylinder(GripL + new Vector3(-0.04f, -0.01f, 0), GripR + new Vector3(0.04f, -0.01f, 0), 0.014f, 0.014f, 6, black);
            b.Cylinder(GripL + new Vector3(-0.05f, -0.01f, 0), GripL + new Vector3(0.06f, -0.01f, 0), 0.02f, 0.02f, 6, accent.Luma() < 0.1f ? frame : accent);
            b.Cylinder(GripR + new Vector3(-0.06f, -0.01f, 0), GripR + new Vector3(0.05f, -0.01f, 0), 0.02f, 0.02f, 6, accent.Luma() < 0.1f ? frame : accent);
            // Saddle and seat post.
            b.Cylinder(seatTop, seatTop + new Vector3(0, 0.05f, -0.02f), 0.016f, 0.016f, 6, black);
            b.Box(seatTop + new Vector3(0, 0.07f, -0.02f), new Vector3(0.06f, 0.025f, 0.13f), black);
            // Chainring, chain guide, number plate mount.
            b.Cylinder(Bb + new Vector3(0.05f, 0, 0), Bb + new Vector3(0.06f, 0, 0), 0.09f, 0.09f, 12, Mats.Hex(0x2B2D33));
            b.Beam(Bb + new Vector3(0.062f, 0.09f, 0), RearAxle + new Vector3(0.062f, 0.05f, 0), 0.008f, 0.012f, Mats.Hex(0x3A3C42));
            b.Beam(Bb + new Vector3(0.062f, -0.09f, 0), RearAxle + new Vector3(0.062f, -0.05f, 0), 0.008f, 0.012f, Mats.Hex(0x3A3C42));
            // Frame decal stripe.
            b.Cylinder(Vector3.Lerp(headBot, Bb, 0.25f) + new Vector3(0, 0.02f, 0), Vector3.Lerp(headBot, Bb, 0.5f) + new Vector3(0, 0.02f, 0), 0.044f, 0.04f, 8, accent);
            Part(_bike, "Frame", b.Build("Frame"), Palette.Lit, shadows);

            var wheel = WheelMesh(accent.Luma() > 0.85f ? Mats.Hex(0x22252B) : accent, gold);
            _wheelF = new GameObject("WheelF").transform;
            _wheelF.SetParent(_bike, false);
            _wheelF.localPosition = FrontAxle;
            Part(_wheelF, "Wheel", wheel, Palette.Lit, shadows);
            _wheelR = new GameObject("WheelR").transform;
            _wheelR.SetParent(_bike, false);
            _wheelR.localPosition = RearAxle;
            Part(_wheelR, "Wheel", wheel, Palette.Lit, shadows);

            // Cranks rotate about the bottom bracket.
            _crank = new GameObject("Crank").transform;
            _crank.SetParent(_bike, false);
            _crank.localPosition = Bb;
            var cb = new MeshBuilder();
            cb.Beam(new Vector3(0.075f, 0, 0), new Vector3(0.075f, 0, CrankLen), 0.02f, 0.035f, black);
            cb.Beam(new Vector3(-0.075f, 0, 0), new Vector3(-0.075f, 0, -CrankLen), 0.02f, 0.035f, black);
            cb.Cylinder(new Vector3(-0.08f, 0, 0), new Vector3(0.08f, 0, 0), 0.02f, 0.02f, 6, silver);
            cb.Box(new Vector3(PedalX, 0, CrankLen), new Vector3(0.05f, 0.012f, 0.05f), Mats.Hex(0x2A2A2A));
            cb.Box(new Vector3(-PedalX, 0, -CrankLen), new Vector3(0.05f, 0.012f, 0.05f), Mats.Hex(0x2A2A2A));
            Part(_crank, "Cranks", cb.Build("Cranks"), Palette.Lit, false);

            // Number plate.
            var plate = new Px(128, 96, Color.white);
            plate.Rect(0, 0, 128, 12, Def.Accent);
            plate.Rect(0, 84, 128, 96, Def.Accent);
            TextBaker.Draw(plate, Def.Number.ToString(), 64, 22, 54, new Color(0.08f, 0.08f, 0.1f));
            var plateTex = plate.ToTexture("Plate" + Def.Number, true, TextureWrapMode.Clamp, FilterMode.Trilinear, 2);
            _bars = new GameObject("Plate").transform;
            _bars.SetParent(_bike, false);
            _bars.localPosition = new Vector3(0, 1.0f, 0.41f);
            _bars.localRotation = Quaternion.Euler(-12, 180, 0);
            var pr = Part(_bars, "PlateQuad", MeshKit.Quad, Mats.Lit(Color.white, 0.3f, plateTex), false);
            pr.localScale = new Vector3(0.2f, 0.15f, 1);
        }

        // ------------------------------------------------------------ body

        void BuildBody(RiderDef def, bool shadows)
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var jersey = Mats.Lit(def.Jersey, 0.3f);
            var jerseyDark = Mats.Lit(def.Jersey.Darken(0.25f), 0.3f);
            var pants = Mats.Lit(def.Pants, 0.25f);
            var helmet = Mats.Lit(def.Helmet, 0.65f);
            var accent = Mats.Lit(def.Accent, 0.5f);
            var glove = Mats.Lit(def.Accent.Luma() > 0.8f ? Mats.Hex(0x222222) : def.Accent, 0.3f);
            var shoe = Mats.Lit(Mats.Hex(0x1E1E22), 0.3f);
            var pad = Mats.Lit(Mats.Hex(0x2A2A30), 0.4f);
            var lens = Mats.Lit(Mats.Hex(0xFF8A2A), 0.95f, null, false, 0.6f);
            var sphere = MeshKit.Sphere;
            var cube = MeshKit.Cube;

            _hips = Part(_body, "Hips", sphere, pants, shadows);
            _torso = Part(_body, "Torso", MeshKit.TaperedLimb(0.13f, 0.155f), jersey, shadows);
            _chest = Part(_body, "Chest", sphere, jersey, shadows);
            _shoulderL = Part(_body, "ShoulderL", sphere, jerseyDark, shadows);
            _shoulderR = Part(_body, "ShoulderR", sphere, jerseyDark, shadows);
            _head = Part(_body, "Helmet", sphere, helmet, shadows);
            _chin = Part(_head, "Chin", sphere, helmet, false);
            _visor = Part(_head, "Visor", cube, accent, false);
            _goggle = Part(_head, "Goggle", cube, lens, false);
            _chin.localPosition = new Vector3(0, -0.45f, 0.55f);
            _chin.localScale = new Vector3(0.62f, 0.42f, 0.5f);
            _visor.localPosition = new Vector3(0, 0.62f, 0.72f);
            _visor.localRotation = Quaternion.Euler(-12, 0, 0);
            _visor.localScale = new Vector3(0.62f, 0.05f, 0.32f);
            _goggle.localPosition = new Vector3(0, 0.1f, 0.88f);
            _goggle.localScale = new Vector3(0.66f, 0.2f, 0.12f);

            var upper = MeshKit.TaperedLimb(0.055f, 0.048f);
            var fore = MeshKit.TaperedLimb(0.048f, 0.04f);
            _upperL = Part(_body, "UpperL", upper, jersey, shadows);
            _upperR = Part(_body, "UpperR", upper, jersey, shadows);
            _foreL = Part(_body, "ForeL", fore, jerseyDark, shadows);
            _foreR = Part(_body, "ForeR", fore, jerseyDark, shadows);
            _handL = Part(_body, "HandL", sphere, glove, false);
            _handR = Part(_body, "HandR", sphere, glove, false);
            var thigh = MeshKit.TaperedLimb(0.085f, 0.065f);
            var shin = MeshKit.TaperedLimb(0.064f, 0.05f);
            _thighL = Part(_body, "ThighL", thigh, pants, shadows);
            _thighR = Part(_body, "ThighR", thigh, pants, shadows);
            _shinL = Part(_body, "ShinL", shin, pants, shadows);
            _shinR = Part(_body, "ShinR", shin, pants, shadows);
            _kneeL = Part(_body, "KneeL", sphere, pad, false);
            _kneeR = Part(_body, "KneeR", sphere, pad, false);
            _shoeL = Part(_body, "ShoeL", sphere, shoe, false);
            _shoeR = Part(_body, "ShoeR", sphere, shoe, false);
        }

        void BuildShadow()
        {
            var go = MeshKit.Obj("BlobShadow", transform, MeshKit.Quad, Mats.Unlit(new Color(0, 0, 0, 0.42f), Tex.GlowTex, true), false, false);
            _shadow = go.transform;
            _shadow.localScale = new Vector3(0.9f, 1.9f, 1);
        }

        // ------------------------------------------------------------ poses

        static RiderPose Attack(float crank)
        {
            var (fl, fr) = Feet(crank);
            return new RiderPose
            {
                Pelvis = new Vector3(0, 1.1f, -0.27f), Neck = new Vector3(0, 1.47f, 0.04f),
                HandL = GripL, HandR = GripR, FootL = fl, FootR = fr,
                ElbowPoleL = new Vector3(-1, -0.2f, -0.4f), ElbowPoleR = new Vector3(1, -0.2f, -0.4f),
                KneePoleL = new Vector3(-0.25f, 0, 1), KneePoleR = new Vector3(0.25f, 0, 1), HeadPitch = -20,
            };
        }

        static (Vector3 l, Vector3 r) Feet(float crank)
        {
            // Crank angle 0 = right pedal forward.
            var rf = Bb + new Vector3(PedalX, Mathf.Sin(crank) * CrankLen + 0.035f, Mathf.Cos(crank) * CrankLen);
            var lf = Bb + new Vector3(-PedalX, -Mathf.Sin(crank) * CrankLen + 0.035f, -Mathf.Cos(crank) * CrankLen);
            return (lf, rf);
        }

        public static RiderPose PoseFor(Rider r, float crank)
        {
            var p = Attack(crank);
            if (r.Pedalling > 0)
            {
                var seated = p;
                seated.Pelvis = new Vector3(0, 1.04f, -0.29f);
                seated.Neck = new Vector3(0, 1.47f, -0.02f);
                p = RiderPose.Lerp(p, seated, 0.7f);
            }
            if (r.Preload > 0)
            {
                var low = p;
                low.Pelvis = new Vector3(0, 0.9f, -0.37f);
                low.Neck = new Vector3(0, 1.28f, 0.0f);
                low.HeadPitch = -35;
                p = RiderPose.Lerp(p, low, r.Preload);
            }
            if (r.PunchTimer >= 0)
            {
                int side = r.PunchSide;
                float t = r.PunchTimer;
                float ext = t < 0.15f ? -0.4f * (t / 0.15f) : t < 0.24f ? Mathf.Lerp(-0.4f, 1f, (t - 0.15f) / 0.09f) : Mathf.Lerp(1, 0, (t - 0.24f) / 0.18f);
                var fist = side > 0
                    ? Vector3.LerpUnclamped(GripR, new Vector3(0.98f, 1.42f, 0.18f), ext)
                    : Vector3.LerpUnclamped(GripL, new Vector3(-0.98f, 1.42f, 0.18f), ext);
                if (side > 0) p.HandR = fist;
                else p.HandL = fist;
                p.Neck += new Vector3(side * 0.07f, 0.03f, 0) * Mathf.Clamp01(ext + 0.4f);
            }
            if (r.TrickBlend > 0 && r.CurTrick != Trick.None)
            {
                var t = p;
                switch (r.CurTrick)
                {
                    case Trick.Superman:
                        t.Pelvis = new Vector3(0, 1.32f, -0.78f);
                        t.Neck = new Vector3(0, 1.36f, -0.2f);
                        t.FootL = new Vector3(-0.14f, 1.32f, -1.6f);
                        t.FootR = new Vector3(0.14f, 1.36f, -1.58f);
                        t.KneePoleL = new Vector3(-0.2f, -1, 0);
                        t.KneePoleR = new Vector3(0.2f, -1, 0);
                        t.HeadPitch = 10;
                        break;
                    case Trick.NoHander:
                        t.Pelvis = new Vector3(0, 1.08f, -0.32f);
                        t.Neck = new Vector3(0, 1.6f, -0.12f);
                        t.HandL = new Vector3(-0.78f, 1.62f, -0.05f);
                        t.HandR = new Vector3(0.78f, 1.62f, -0.05f);
                        t.ElbowPoleL = new Vector3(-0.3f, -1, 0);
                        t.ElbowPoleR = new Vector3(0.3f, -1, 0);
                        t.HeadPitch = 5;
                        break;
                    default:
                        t.Pelvis = new Vector3(0.06f, 1.14f, -0.3f);
                        t.FootL = new Vector3(0.62f, 0.95f, 0.22f);
                        t.KneePoleL = new Vector3(0.5f, 0.3f, 1);
                        t.Neck = new Vector3(-0.04f, 1.5f, 0.02f);
                        break;
                }
                p = RiderPose.Lerp(p, t, Tex.SmoothStep(0, 1, r.TrickBlend));
            }
            return p;
        }

        static RiderPose Tumble(float t)
        {
            float w = Mathf.Sin(t * 9) * 0.15f;
            return new RiderPose
            {
                Pelvis = Vector3.zero, Neck = new Vector3(0, 0.48f, 0.12f),
                HandL = new Vector3(-0.55f, 0.45f + w, 0.3f), HandR = new Vector3(0.55f, 0.4f - w, 0.32f),
                FootL = new Vector3(-0.28f, -0.62f, 0.35f - w), FootR = new Vector3(0.3f, -0.7f, 0.15f + w),
                ElbowPoleL = new Vector3(-1, 0, -0.5f), ElbowPoleR = new Vector3(1, 0, -0.5f),
                KneePoleL = new Vector3(-0.3f, 0, 1), KneePoleR = new Vector3(0.3f, 0, 1), HeadPitch = 0,
            };
        }

        static Vector3 Ik(Vector3 root, Vector3 target, float a, float b, Vector3 pole, out Vector3 end)
        {
            var d = target - root;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            var dir = d.sqrMagnitude < 1e-8f ? Vector3.down : d.normalized;
            float x = (a * a - b * b + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
            var pp = pole - dir * Vector3.Dot(pole, dir);
            if (pp.sqrMagnitude < 1e-8f) pp = Vector3.Cross(dir, Vector3.right);
            pp.Normalize();
            end = root + dir * dist;
            return root + dir * x + pp * h;
        }

        void ApplyPose(RiderPose p)
        {
            var up = (p.Neck - p.Pelvis).normalized;
            var right = Vector3.right;
            var fwd = Vector3.Cross(right, up).normalized;
            right = Vector3.Cross(up, fwd).normalized;
            var rot = Quaternion.LookRotation(fwd, up);

            _hips.localPosition = p.Pelvis;
            _hips.localRotation = rot;
            _hips.localScale = new Vector3(0.17f, 0.12f, 0.13f);
            MeshKit.Span(_torso, p.Pelvis, p.Neck - up * 0.05f);
            _chest.localPosition = Vector3.Lerp(p.Pelvis, p.Neck, 0.68f) + fwd * 0.01f;
            _chest.localRotation = rot;
            _chest.localScale = new Vector3(0.2f, 0.17f, 0.15f);
            var head = p.Neck + up * 0.17f + fwd * 0.04f;
            _head.localPosition = head;
            _head.localRotation = rot * Quaternion.Euler(p.HeadPitch, 0, 0);
            _head.localScale = new Vector3(0.145f, 0.155f, 0.165f);

            var shL = p.Neck - right * 0.18f - up * 0.04f;
            var shR = p.Neck + right * 0.18f - up * 0.04f;
            _shoulderL.localPosition = shL;
            _shoulderR.localPosition = shR;
            _shoulderL.localScale = _shoulderR.localScale = Vector3.one * 0.075f;
            var elL = Ik(shL, p.HandL, UpperArm, ForeArm, p.ElbowPoleL, out var hL);
            var elR = Ik(shR, p.HandR, UpperArm, ForeArm, p.ElbowPoleR, out var hR);
            MeshKit.Span(_upperL, shL, elL);
            MeshKit.Span(_upperR, shR, elR);
            MeshKit.Span(_foreL, elL, hL);
            MeshKit.Span(_foreR, elR, hR);
            _handL.localPosition = hL;
            _handR.localPosition = hR;
            _handL.localScale = _handR.localScale = Vector3.one * 0.05f;

            var hipL = p.Pelvis - right * 0.1f - up * 0.04f;
            var hipR = p.Pelvis + right * 0.1f - up * 0.04f;
            var knL = Ik(hipL, p.FootL, Thigh, Shin, p.KneePoleL, out var aL);
            var knR = Ik(hipR, p.FootR, Thigh, Shin, p.KneePoleR, out var aR);
            MeshKit.Span(_thighL, hipL, knL);
            MeshKit.Span(_thighR, hipR, knR);
            MeshKit.Span(_shinL, knL, aL);
            MeshKit.Span(_shinR, knR, aR);
            _kneeL.localPosition = knL + (knL - hipL).normalized * 0.01f;
            _kneeR.localPosition = knR + (knR - hipR).normalized * 0.01f;
            _kneeL.localScale = _kneeR.localScale = Vector3.one * 0.072f;
            _shoeL.localPosition = aL + new Vector3(0, -0.02f, 0.03f);
            _shoeR.localPosition = aR + new Vector3(0, -0.02f, 0.03f);
            _shoeL.localScale = _shoeR.localScale = new Vector3(0.055f, 0.045f, 0.12f);
            var shinDirL = (aL - knL).normalized;
            var shinDirR = (aR - knR).normalized;
            _shoeL.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, shinDirL).normalized + Vector3.forward * 0.2f, -shinDirL);
            _shoeR.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, shinDirR).normalized + Vector3.forward * 0.2f, -shinDirR);
        }

        // ------------------------------------------------------------ sync

        public void Sync(Rider r, float dt, bool detail)
        {
            var tr = r.Tr;
            if (r.Crashed)
            {
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                _bike.SetPositionAndRotation(r.BikePos, r.BikeRot);
                _body.SetPositionAndRotation(r.BodyPos, r.BodyRot);
                ApplyPose(Tumble(r.CrashTime));
                PlaceShadow(tr, r.S, r.BikePos, r.BikeRot * Vector3.forward, 0.6f);
                SetVisible(true);
                return;
            }
            var pos = r.WorldPos;
            var rot = r.Rotation;
            transform.SetPositionAndRotation(pos, rot);
            _bike.localPosition = Vector3.zero;
            _bike.localRotation = Quaternion.identity;
            _body.localPosition = Vector3.zero;
            _body.localRotation = Quaternion.identity;
            _wheelAngle += r.V / WheelR * dt * Mathf.Rad2Deg;
            _wheelF.localRotation = _wheelR.localRotation = Quaternion.Euler(_wheelAngle, 0, 0);
            float crank = r.Pedalling > 0 ? r.PedalPhase : 0;
            _crank.localRotation = Quaternion.Euler(-crank * Mathf.Rad2Deg, 0, 0);
            if (detail || !_hasPose)
            {
                var target = PoseFor(r, crank);
                _pose = _hasPose ? RiderPose.Lerp(_pose, target, Mathf.Clamp01(dt * 18)) : target;
                if (r.PunchTimer >= 0 || r.CurTrick != Trick.None) _pose = target;
                _hasPose = true;
                ApplyPose(_pose);
            }
            PlaceShadow(tr, r.S, pos, transform.forward, Mathf.Clamp01(1 - (pos.y - tr.GroundY(r.S, r.X)) / 8f));
            // Blink while invulnerable after a respawn.
            SetVisible(r.Invuln <= 0 || Mathf.Repeat(r.Invuln, 0.2f) > 0.08f);
        }

        /// <summary>Blob shadow on the ground under a world point near track distance <paramref name="nearS"/>.</summary>
        void PlaceShadow(Track tr, float nearS, Vector3 worldPos, Vector3 forward, float strength)
        {
            if (_shadow == null) return;
            var c = tr.Center(nearS);
            float s = nearS + Vector3.Dot(worldPos - c, tr.Forward(nearS));
            float x = Vector3.Dot(worldPos - c, tr.Right(nearS));
            float gy = tr.GroundY(s, x);
            _shadow.position = new Vector3(worldPos.x, gy + 0.06f, worldPos.z);
            forward.y = 0;
            _shadow.rotation = Quaternion.LookRotation(Vector3.down, forward.sqrMagnitude > 1e-4f ? forward : Vector3.forward);
            _shadow.localScale = new Vector3(0.9f, 1.9f, 1) * Mathf.Lerp(0.6f, 1f, strength);
            _shadow.gameObject.SetActive(strength > 0.05f);
        }

        bool _visible = true;

        void SetVisible(bool v)
        {
            if (v == _visible) return;
            _visible = v;
            foreach (var r in _renderers) r.enabled = v;
        }

        /// <summary>Static pose for menus: standing on level pedals, optionally crouched.</summary>
        public void ShowPose(float crouch = 0, float crank = 0)
        {
            var p = Attack(crank);
            if (crouch > 0)
            {
                var low = p;
                low.Pelvis = new Vector3(0, 0.9f, -0.37f);
                low.Neck = new Vector3(0, 1.28f, 0.0f);
                p = RiderPose.Lerp(p, low, crouch);
            }
            ApplyPose(p);
            _hasPose = true;
            _pose = p;
            if (_shadow != null)
            {
                _shadow.localPosition = new Vector3(0, 0.02f, 0);
                _shadow.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
        }

        public void ShadowsOn(bool on)
        {
            foreach (var r in _renderers) r.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }
}
