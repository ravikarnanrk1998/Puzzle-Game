using System;
using System.Collections.Generic;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>One arm: either forward kinematics (shoulder Euler + elbow bend) or an IK hand target.</summary>
    public struct ArmP
    {
        public bool IK;
        /// <summary>IK: hand target and elbow pole point, both in root space.</summary>
        public Vector3 Target, Pole;
        /// <summary>FK: upper arm Euler relative to the chest (X- = raise forward, Z = sideways).</summary>
        public Vector3 Fk;
        public float Elbow;
        public Vector3 Hand;

        public static ArmP F(float x, float y, float z, float elbow) => new ArmP { Fk = new Vector3(x, y, z), Elbow = elbow };
        public static ArmP I(Vector3 target, Vector3 pole) => new ArmP { IK = true, Target = target, Pole = pole };

        public static ArmP Lerp(ArmP a, ArmP b, float t)
        {
            if (a.IK != b.IK) return t < 0.5f ? a : b;
            return new ArmP
            {
                IK = a.IK,
                Target = Vector3.Lerp(a.Target, b.Target, t),
                Pole = Vector3.Lerp(a.Pole, b.Pole, t),
                Fk = new Vector3(Mathf.LerpAngle(a.Fk.x, b.Fk.x, t), Mathf.LerpAngle(a.Fk.y, b.Fk.y, t), Mathf.LerpAngle(a.Fk.z, b.Fk.z, t)),
                Elbow = Mathf.Lerp(a.Elbow, b.Elbow, t),
                Hand = Vector3.Lerp(a.Hand, b.Hand, t),
            };
        }
    }

    /// <summary>
    /// A full-body pose in the character's root space (facing +Z, left = -X): hips, spine, head,
    /// foot targets (legs are IK), arms, and optionally a bat held in both hands.
    /// </summary>
    public sealed class Pose
    {
        public float HipH = 0.94f;
        public Vector3 HipShift, HipRot, Spine, Chest, Neck, Head;
        public Vector3 FootL = new Vector3(-0.11f, 0.085f, 0), FootR = new Vector3(0.11f, 0.085f, 0);
        public float FootLYaw, FootRYaw, FootLPitch, FootRPitch;
        public float KneeOut = 0.15f;
        public ArmP L = ArmP.F(0, 0, -6, 12), R = ArmP.F(0, 0, 6, 12);
        public bool HasBat;
        public Vector3 BatGrip, BatDir = Vector3.down, BatFace = Vector3.forward;
        /// <summary>Extra root offset (jumps, dives) applied in root space.</summary>
        public Vector3 RootOffset;
        public float RootRoll;

        public Pose Clone() => (Pose)MemberwiseClone();

        static Vector3 LerpE(Vector3 a, Vector3 b, float t) => new Vector3(Mathf.LerpAngle(a.x, b.x, t), Mathf.LerpAngle(a.y, b.y, t), Mathf.LerpAngle(a.z, b.z, t));

        public static Pose Lerp(Pose a, Pose b, float t)
        {
            var p = new Pose
            {
                HipH = Mathf.Lerp(a.HipH, b.HipH, t),
                HipShift = Vector3.Lerp(a.HipShift, b.HipShift, t),
                HipRot = LerpE(a.HipRot, b.HipRot, t),
                Spine = LerpE(a.Spine, b.Spine, t),
                Chest = LerpE(a.Chest, b.Chest, t),
                Neck = LerpE(a.Neck, b.Neck, t),
                Head = LerpE(a.Head, b.Head, t),
                FootL = Vector3.Lerp(a.FootL, b.FootL, t),
                FootR = Vector3.Lerp(a.FootR, b.FootR, t),
                FootLYaw = Mathf.LerpAngle(a.FootLYaw, b.FootLYaw, t),
                FootRYaw = Mathf.LerpAngle(a.FootRYaw, b.FootRYaw, t),
                FootLPitch = Mathf.Lerp(a.FootLPitch, b.FootLPitch, t),
                FootRPitch = Mathf.Lerp(a.FootRPitch, b.FootRPitch, t),
                KneeOut = Mathf.Lerp(a.KneeOut, b.KneeOut, t),
                L = ArmP.Lerp(a.L, b.L, t),
                R = ArmP.Lerp(a.R, b.R, t),
                HasBat = t < 0.5f ? a.HasBat : b.HasBat,
                BatGrip = Vector3.Lerp(a.BatGrip, b.BatGrip, t),
                BatDir = Vector3.Slerp(a.BatDir, b.BatDir, t).normalized,
                BatFace = Vector3.Slerp(a.BatFace, b.BatFace, t).normalized,
                RootOffset = Vector3.Lerp(a.RootOffset, b.RootOffset, t),
                RootRoll = Mathf.Lerp(a.RootRoll, b.RootRoll, t),
            };
            if (a.HasBat && b.HasBat) p.HasBat = true;
            return p;
        }

        static Vector3 MX(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        static Vector3 ME(Vector3 e) => new Vector3(e.x, -e.y, -e.z);

        static ArmP MA(ArmP a) => new ArmP { IK = a.IK, Target = MX(a.Target), Pole = MX(a.Pole), Fk = ME(a.Fk), Elbow = a.Elbow, Hand = ME(a.Hand) };

        /// <summary>Left-right mirror (left-handed batters and bowlers).</summary>
        public Pose Mirror()
        {
            var p = Clone();
            p.HipShift = MX(HipShift);
            p.HipRot = ME(HipRot);
            p.Spine = ME(Spine);
            p.Chest = ME(Chest);
            p.Neck = ME(Neck);
            p.Head = ME(Head);
            p.FootL = MX(FootR);
            p.FootR = MX(FootL);
            p.FootLYaw = -FootRYaw;
            p.FootRYaw = -FootLYaw;
            p.FootLPitch = FootRPitch;
            p.FootRPitch = FootLPitch;
            p.L = MA(R);
            p.R = MA(L);
            p.BatGrip = MX(BatGrip);
            p.BatDir = MX(BatDir);
            p.BatFace = MX(BatFace);
            p.RootOffset = MX(RootOffset);
            p.RootRoll = -RootRoll;
            return p;
        }
    }

    /// <summary>Keyframed clip with root motion; keys are interpolated with smoothstep.</summary>
    public sealed class Clip
    {
        public readonly List<(float T, Pose P)> Keys = new List<(float, Pose)>();
        public float Length => Keys.Count == 0 ? 0 : Keys[Keys.Count - 1].T;

        public Clip Key(float t, Pose p)
        {
            Keys.Add((t, p));
            return this;
        }

        public Pose Sample(float t)
        {
            if (Keys.Count == 0) return new Pose();
            if (t <= Keys[0].T) return Keys[0].P;
            for (int i = 0; i < Keys.Count - 1; i++)
            {
                if (t <= Keys[i + 1].T)
                {
                    float k = Mathf.InverseLerp(Keys[i].T, Keys[i + 1].T, t);
                    return Pose.Lerp(Keys[i].P, Keys[i + 1].P, k * k * (3 - 2 * k));
                }
            }
            return Keys[Keys.Count - 1].P;
        }

        public Clip Mirror()
        {
            var c = new Clip();
            foreach (var (t, p) in Keys) c.Key(t, p.Mirror());
            return c;
        }
    }

    /// <summary>Applies poses to a <see cref="Body"/>: FK spine and arms, analytic two-bone IK for legs and held-bat arms.</summary>
    public static class Rig
    {
        public static void Apply(Body b, Pose p, Vector3 rootPos, float yaw)
        {
            var root = b.transform;
            root.position = rootPos + Quaternion.Euler(0, yaw, 0) * p.RootOffset * b.Scale;
            root.rotation = Quaternion.Euler(0, yaw, p.RootRoll);
            var hips = b[B.Hips];
            hips.localPosition = new Vector3(0, p.HipH, 0) + p.HipShift;
            hips.localRotation = Quaternion.Euler(p.HipRot);
            b[B.Spine].localRotation = Quaternion.Euler(p.Spine);
            b[B.Chest].localRotation = Quaternion.Euler(p.Chest);
            b[B.Neck].localRotation = Quaternion.Euler(p.Neck);
            b[B.Head].localRotation = Quaternion.Euler(p.Head);
            float s = b.Scale;

            // Legs.
            var fwd = hips.forward;
            var outL = -root.right;
            Leg(b, B.ThighL, B.ShinL, B.FootL, root.TransformPoint(p.FootL), fwd + outL * p.KneeOut, s);
            Leg(b, B.ThighR, B.ShinR, B.FootR, root.TransformPoint(p.FootR), fwd - outL * p.KneeOut, s);
            b[B.FootL].rotation = root.rotation * Quaternion.Euler(p.FootLPitch, p.FootLYaw, 0);
            b[B.FootR].rotation = root.rotation * Quaternion.Euler(p.FootRPitch, p.FootRYaw, 0);

            // Bat in both hands: hands go to the handle.
            if (p.HasBat && b.Bat != null)
            {
                var grip = root.TransformPoint(p.BatGrip);
                var dir = root.TransformDirection(p.BatDir).normalized;
                var face = root.TransformDirection(p.BatFace).normalized;
                b.Bat.position = grip;
                b.Bat.rotation = Quaternion.LookRotation(face, -dir);
                b.Bat.localScale = Vector3.one * s;
                bool leftHander = b.Look.LeftHanded;
                // Top hand nearer the end of the handle.
                var top = grip + dir * 0.075f * s;
                var bottom = grip + dir * 0.19f * s;
                var lT = leftHander ? bottom : top;
                var rT = leftHander ? top : bottom;
                Arm(b, B.ArmL, B.ForeL, B.HandL, lT, root.TransformPoint(p.L.IK ? p.L.Pole : new Vector3(-0.5f, 0.9f, 0.1f)), s);
                Arm(b, B.ArmR, B.ForeR, B.HandR, rT, root.TransformPoint(p.R.IK ? p.R.Pole : new Vector3(0.5f, 0.9f, 0.1f)), s);
                b[B.HandL].rotation = Quaternion.LookRotation(face, -dir) * Quaternion.Euler(0, 90, 0);
                b[B.HandR].rotation = Quaternion.LookRotation(face, -dir) * Quaternion.Euler(0, -90, 0);
                return;
            }
            ApplyArm(b, p.L, B.ArmL, B.ForeL, B.HandL, root, s);
            ApplyArm(b, p.R, B.ArmR, B.ForeR, B.HandR, root, s);
            if (b.Bat != null)
            {
                // Carried in the right (or left) hand while running.
                var hand = b[b.Look.LeftHanded ? B.HandL : B.HandR];
                b.Bat.position = hand.position + hand.rotation * new Vector3(0, -0.06f, 0) * s;
                b.Bat.rotation = hand.rotation * Quaternion.Euler(-35, 0, 0);
                b.Bat.localScale = Vector3.one * s;
            }
        }

        static void ApplyArm(Body b, ArmP a, B upper, B lower, B hand, Transform root, float s)
        {
            if (a.IK)
            {
                Arm(b, upper, lower, hand, root.TransformPoint(a.Target), root.TransformPoint(a.Pole), s);
                b[hand].localRotation = Quaternion.Euler(a.Hand);
            }
            else
            {
                b[upper].localRotation = Quaternion.Euler(a.Fk);
                b[lower].localRotation = Quaternion.Euler(-a.Elbow, 0, 0);
                b[hand].localRotation = Quaternion.Euler(a.Hand);
            }
        }

        static void Leg(Body b, B upper, B lower, B end, Vector3 target, Vector3 poleDir, float s)
        {
            var a = b[upper].position;
            TwoBone(b[upper], b[lower], target, a + poleDir.normalized, Body.Thigh * s, Body.Shin * s, true);
        }

        static void Arm(Body b, B upper, B lower, B hand, Vector3 target, Vector3 pole, float s)
        {
            TwoBone(b[upper], b[lower], target, pole, Body.UpperArm * s, Body.ForeArm * s, false);
        }

        /// <summary>
        /// Analytic two-bone IK. Bones point down their local -Y; the bend plane contains the pole.
        /// Legs bend towards their local +Z (knees forward), arms away from it (elbows back).
        /// </summary>
        public static void TwoBone(Transform upper, Transform lower, Vector3 target, Vector3 polePoint, float l1, float l2, bool bendFront)
        {
            var a = upper.position;
            var toT = target - a;
            float d = Mathf.Clamp(toT.magnitude, 0.03f, l1 + l2 - 0.0005f);
            var dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : Vector3.down;
            var pole = polePoint - a;
            var bend = pole - dir * Vector3.Dot(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, Vector3.right);
            bend.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1);
            float sinA = Mathf.Sqrt(Mathf.Max(0, 1 - cosA * cosA));
            var elbow = a + dir * (cosA * l1) + bend * (sinA * l1);
            var end = a + dir * d;
            var u = (elbow - a).normalized;
            var f = bendFront ? bend : -bend;
            var f1 = f - u * Vector3.Dot(f, u);
            if (f1.sqrMagnitude < 1e-6f) f1 = Vector3.Cross(u, Vector3.right);
            upper.rotation = Quaternion.LookRotation(f1.normalized, -u);
            var v = (end - elbow).normalized;
            var f2 = f - v * Vector3.Dot(f, v);
            if (f2.sqrMagnitude < 1e-6f) f2 = f1;
            lower.rotation = Quaternion.LookRotation(f2.normalized, -v);
        }
    }

    public enum Sig { Out, Four, Six, Wide, NoBall, Bye, LegBye, FreeHit }

    /// <summary>Library of authored poses (right-handed; mirror for left-handers).</summary>
    public static class Poses
    {
        public static Pose Stand() => new Pose();

        public static Pose Idle(float t)
        {
            var p = new Pose { HipH = 0.945f };
            p.HipShift = new Vector3(Mathf.Sin(t * 0.7f) * 0.012f, 0, 0);
            p.Chest = new Vector3(Mathf.Sin(t * 1.3f) * 1.2f, Mathf.Sin(t * 0.4f) * 3, 0);
            p.Head = new Vector3(0, Mathf.Sin(t * 0.35f) * 12, 0);
            p.L = ArmP.F(4, 0, -7, 14);
            p.R = ArmP.F(4, 0, 7, 14);
            return p;
        }

        /// <summary>Fielder's ready crouch as the bowler delivers.</summary>
        public static Pose Ready()
        {
            var p = new Pose { HipH = 0.8f, HipRot = new Vector3(22, 0, 0), Spine = new Vector3(10, 0, 0), Chest = new Vector3(6, 0, 0), Head = new Vector3(-26, 0, 0), KneeOut = 0.4f };
            p.FootL = new Vector3(-0.22f, 0.085f, 0.02f);
            p.FootR = new Vector3(0.22f, 0.085f, 0.02f);
            p.FootLYaw = -12;
            p.FootRYaw = 12;
            p.L = ArmP.I(new Vector3(-0.16f, 0.52f, 0.3f), new Vector3(-0.6f, 0.9f, 0.1f));
            p.R = ArmP.I(new Vector3(0.16f, 0.52f, 0.3f), new Vector3(0.6f, 0.9f, 0.1f));
            return p;
        }

        public static Pose KeeperCrouch()
        {
            var p = new Pose { HipH = 0.5f, HipRot = new Vector3(28, 0, 0), Spine = new Vector3(12, 0, 0), Chest = new Vector3(4, 0, 0), Head = new Vector3(-38, 0, 0), KneeOut = 0.9f };
            p.FootL = new Vector3(-0.27f, 0.085f, 0.06f);
            p.FootR = new Vector3(0.27f, 0.085f, 0.06f);
            p.FootLYaw = -25;
            p.FootRYaw = 25;
            p.FootLPitch = -10;
            p.FootRPitch = -10;
            p.L = ArmP.I(new Vector3(-0.05f, 0.32f, 0.45f), new Vector3(-0.6f, 0.3f, 0.2f));
            p.R = ArmP.I(new Vector3(0.05f, 0.32f, 0.45f), new Vector3(0.6f, 0.3f, 0.2f));
            return p;
        }

        /// <summary>Hands reaching for a point (catch / keeper take) in root space.</summary>
        public static Pose Reach(Vector3 hands, bool deep)
        {
            float h = Mathf.Clamp(hands.y, 0.2f, 2.4f);
            var p = new Pose { HipH = h < 0.7f ? 0.65f : 0.92f, Head = new Vector3(Mathf.Clamp(-(h - 1.5f) * 30, -45, 50), 0, 0) };
            p.HipRot = new Vector3(h < 0.7f ? 30 : 4, 0, 0);
            p.Spine = new Vector3(h < 0.8f ? 18 : h > 1.8f ? -8 : 4, 0, 0);
            p.FootL = new Vector3(-0.2f, 0.085f, deep ? -0.1f : 0.1f);
            p.FootR = new Vector3(0.2f, 0.085f, deep ? 0.15f : -0.05f);
            p.KneeOut = 0.4f;
            p.L = ArmP.I(hands + new Vector3(-0.05f, 0, 0), new Vector3(-0.6f, h - 0.4f, 0f));
            p.R = ArmP.I(hands + new Vector3(0.05f, 0, 0), new Vector3(0.6f, h - 0.4f, 0f));
            return p;
        }

        public static Pose Pickup()
        {
            var p = new Pose { HipH = 0.62f, HipRot = new Vector3(45, 0, 0), Spine = new Vector3(25, 0, 0), Chest = new Vector3(10, 0, 0), Head = new Vector3(-30, 0, 0), KneeOut = 0.3f };
            p.FootL = new Vector3(-0.15f, 0.085f, 0.3f);
            p.FootR = new Vector3(0.18f, 0.085f, -0.25f);
            p.R = ArmP.I(new Vector3(0.1f, 0.12f, 0.6f), new Vector3(0.6f, 0.5f, 0.2f));
            p.L = ArmP.F(-20, 0, -20, 30);
            return p;
        }

        public static Clip Throw()
        {
            var a = new Pose { HipH = 0.9f, HipRot = new Vector3(0, 60, 0), Chest = new Vector3(-5, 20, 0), Head = new Vector3(0, -70, 0) };
            a.FootL = new Vector3(-0.1f, 0.085f, 0.35f);
            a.FootR = new Vector3(0.15f, 0.085f, -0.2f);
            a.R = ArmP.F(110, 0, 30, 70);
            a.L = ArmP.F(-80, 0, -10, 10);
            var b = a.Clone();
            b.HipRot = new Vector3(10, -10, 0);
            b.Chest = new Vector3(20, -20, 0);
            b.Head = new Vector3(0, 0, 0);
            b.R = ArmP.F(-150, 0, 20, 10);
            b.L = ArmP.F(10, 0, -20, 40);
            var c = b.Clone();
            c.R = ArmP.F(-40, 0, -20, 20);
            c.Spine = new Vector3(25, 0, 0);
            return new Clip().Key(0, a).Key(0.22f, b).Key(0.45f, c).Key(0.8f, Stand());
        }

        public static Pose Dive(bool right, float h)
        {
            var p = new Pose { HipH = 0.28f, RootRoll = right ? -78 : 78, HipRot = new Vector3(5, 0, 0), Head = new Vector3(0, 0, right ? 40 : -40), KneeOut = 0.2f };
            p.FootL = new Vector3(-0.12f, 0.15f, -0.2f);
            p.FootR = new Vector3(0.12f, 0.2f, -0.25f);
            var hand = new Vector3(right ? 0.9f : -0.9f, 0.35f + h * 0.3f, 0.1f);
            p.L = ArmP.I(hand + new Vector3(0, 0, -0.05f), new Vector3(right ? 0.4f : -0.4f, 1.2f, 0));
            p.R = ArmP.I(hand + new Vector3(0, 0, 0.05f), new Vector3(right ? 0.4f : -0.4f, 1.2f, 0));
            return p;
        }

        public static Pose Celebrate(float t)
        {
            float j = Mathf.Abs(Mathf.Sin(t * 5)) * 0.18f;
            var p = new Pose { HipH = 0.94f, RootOffset = new Vector3(0, j, 0), Head = new Vector3(-20, 0, 0), Chest = new Vector3(-8, 0, 0) };
            p.L = ArmP.F(-165, 0, -25 + Mathf.Sin(t * 10) * 8, 15);
            p.R = ArmP.F(-165, 0, 25 - Mathf.Sin(t * 10) * 8, 15);
            p.FootL = new Vector3(-0.14f, 0.085f + j * 0.3f, 0);
            p.FootR = new Vector3(0.14f, 0.085f + j * 0.3f, 0);
            return p;
        }

        public static Pose Fist(float t)
        {
            var p = new Pose { HipH = 0.9f, Chest = new Vector3(-6, 0, 0), Head = new Vector3(-10, 0, 0) };
            p.R = ArmP.F(-90 - Mathf.Sin(t * 8) * 25, 0, 30, 110);
            p.L = ArmP.F(10, 0, -10, 30);
            p.FootL = new Vector3(-0.18f, 0.085f, 0.05f);
            p.FootR = new Vector3(0.16f, 0.085f, -0.05f);
            return p;
        }

        public static Pose Dejected()
        {
            var p = new Pose { HipH = 0.93f, Spine = new Vector3(8, 0, 0), Head = new Vector3(18, 0, 0) };
            p.L = ArmP.I(new Vector3(-0.08f, 1.82f, 0.03f), new Vector3(-0.6f, 1.6f, 0.3f));
            p.R = ArmP.I(new Vector3(0.08f, 1.82f, 0.03f), new Vector3(0.6f, 1.6f, 0.3f));
            return p;
        }

        public static Pose HandsOnHips()
        {
            var p = new Pose { HipH = 0.94f, Head = new Vector3(6, 0, 0) };
            p.L = ArmP.I(new Vector3(-0.17f, 1.0f, -0.02f), new Vector3(-0.6f, 1.2f, -0.3f));
            p.R = ArmP.I(new Vector3(0.17f, 1.0f, -0.02f), new Vector3(0.6f, 1.2f, -0.3f));
            return p;
        }

        // ---------------------------------------------------------------- umpire

        public static Pose UmpireStand()
        {
            var p = new Pose { HipH = 0.9f, HipRot = new Vector3(12, 0, 0), Spine = new Vector3(6, 0, 0), Head = new Vector3(-8, 0, 0), KneeOut = 0.25f };
            p.FootL = new Vector3(-0.16f, 0.085f, 0);
            p.FootR = new Vector3(0.16f, 0.085f, 0);
            p.L = ArmP.I(new Vector3(-0.03f, 0.88f, 0.22f), new Vector3(-0.6f, 1.0f, -0.2f));
            p.R = ArmP.I(new Vector3(0.03f, 0.88f, 0.22f), new Vector3(0.6f, 1.0f, -0.2f));
            return p;
        }

        public static Pose Signal(Sig s, float t)
        {
            var p = new Pose { HipH = 0.94f };
            switch (s)
            {
                case Sig.Out:
                    p.R = ArmP.F(-172, 0, 8, 0);
                    p.L = ArmP.F(0, 0, -8, 10);
                    break;
                case Sig.Six:
                    p.L = ArmP.F(-178, 0, -12, 0);
                    p.R = ArmP.F(-178, 0, 12, 0);
                    break;
                case Sig.Four:
                    p.R = ArmP.F(-80, Mathf.Sin(t * 7) * 45, 30, 15);
                    p.L = ArmP.F(0, 0, -8, 10);
                    break;
                case Sig.Wide:
                    p.L = ArmP.F(0, 0, -88, 0);
                    p.R = ArmP.F(0, 0, 88, 0);
                    break;
                case Sig.NoBall:
                    p.R = ArmP.F(0, 0, 88, 0);
                    p.L = ArmP.F(0, 0, -8, 10);
                    break;
                case Sig.Bye:
                    p.R = ArmP.F(-170, 0, 0, 0);
                    p.L = ArmP.F(0, 0, -8, 10);
                    break;
                case Sig.LegBye:
                    p.FootR = new Vector3(0.12f, 0.45f, 0.25f);
                    p.R = ArmP.I(new Vector3(0.12f, 0.62f, 0.32f), new Vector3(0.7f, 1, 0));
                    p.L = ArmP.F(0, 0, -15, 10);
                    break;
                default:
                    p.R = ArmP.F(-170 + Mathf.Sin(t * 6) * 10, Mathf.Sin(t * 6) * 40, 10, 20);
                    p.L = ArmP.F(0, 0, -8, 10);
                    break;
            }
            return p;
        }

        // ------------------------------------------------------------- locomotion

        /// <summary>Walk / run cycle. <paramref name="phase"/> 0..1 per two steps.</summary>
        public static Pose Gait(float phase, float speed, bool carryBat)
        {
            float run = Mathf.Clamp01((speed - 1.6f) / 3.5f);
            float step = 0.32f + speed * 0.12f;
            float lift = Mathf.Lerp(0.07f, 0.26f, run);
            var p = new Pose();
            float bob = -Mathf.Cos(phase * Mathf.PI * 4) * Mathf.Lerp(0.015f, 0.04f, run);
            p.HipH = Mathf.Lerp(0.93f, 0.9f, run) + bob;
            p.HipRot = new Vector3(Mathf.Lerp(2, 10, run), Mathf.Sin(phase * Mathf.PI * 2) * Mathf.Lerp(6, 9, run), 0);
            p.Spine = new Vector3(Mathf.Lerp(2, 6, run), 0, 0);
            p.Chest = new Vector3(0, -Mathf.Sin(phase * Mathf.PI * 2) * Mathf.Lerp(6, 12, run), 0);
            p.Head = new Vector3(-Mathf.Lerp(0, 8, run), Mathf.Sin(phase * Mathf.PI * 2) * 4, 0);
            Vector3 Foot(float s, float x)
            {
                s = Mathf.Repeat(s, 1);
                float stance = Mathf.Lerp(0.6f, 0.42f, run);
                if (s < stance)
                {
                    float k = s / stance;
                    return new Vector3(x, 0.085f, Mathf.Lerp(step / 2, -step / 2, k));
                }
                float u = (s - stance) / (1 - stance);
                float e = u * u * (3 - 2 * u);
                return new Vector3(x, 0.085f + Mathf.Sin(u * Mathf.PI) * lift, Mathf.Lerp(-step / 2, step / 2, e));
            }
            p.FootL = Foot(phase, -0.1f);
            p.FootR = Foot(phase + 0.5f, 0.1f);
            p.FootLPitch = p.FootL.y > 0.12f ? -15 * run : 0;
            p.FootRPitch = p.FootR.y > 0.12f ? -15 * run : 0;
            float swing = Mathf.Lerp(16, 48, run);
            float c = Mathf.Cos(phase * Mathf.PI * 2);
            float elbow = Mathf.Lerp(18, 85, run);
            p.L = ArmP.F(swing * c, 0, -8, elbow);
            p.R = ArmP.F(-swing * c, 0, 8, carryBat ? elbow * 0.6f : elbow);
            return p;
        }

        /// <summary>Side-step shuffle used by the non-striker and keepers.</summary>
        public static Pose BackUp(float k)
        {
            var p = Gait(k * 2, 1.6f, true);
            p.HipH = 0.92f;
            return p;
        }
    }
}
