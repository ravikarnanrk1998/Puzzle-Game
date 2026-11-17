using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Batting and bowling motion. Batting poses are authored in a canonical right-hander frame:
    /// the batter faces +Z (the off side), the bowler is towards -X. Left-handers are mirrored.
    /// </summary>
    public static class BatAnim
    {
        static Vector3 Orth(Vector3 v, Vector3 axis) => (v - axis * Vector3.Dot(v, axis)).normalized;

        static Pose Base()
        {
            var p = new Pose { HasBat = true, KneeOut = 0.3f };
            p.L = ArmP.I(Vector3.zero, new Vector3(-0.55f, 1.0f, 0.55f));
            p.R = ArmP.I(Vector3.zero, new Vector3(0.45f, 0.85f, 0.35f));
            return p;
        }

        static void SetBat(Pose p, Vector3 grip, Vector3 dir, Vector3 face)
        {
            p.BatGrip = grip;
            p.BatDir = dir.normalized;
            p.BatFace = Orth(face, p.BatDir);
        }

        public static Pose Stance(float t = 0)
        {
            var p = Base();
            p.HipH = 0.86f;
            p.HipRot = new Vector3(12, -12, 0);
            p.Spine = new Vector3(8, 0, 0);
            p.Chest = new Vector3(8, -8, 0);
            p.Head = new Vector3(6, -62, 0);
            p.FootL = new Vector3(-0.3f, 0.085f, 0.02f);
            p.FootR = new Vector3(0.2f, 0.085f, 0.0f);
            p.FootLYaw = -15;
            p.FootRYaw = 5;
            float tap = Mathf.Max(0, Mathf.Sin(t * 3.2f)) * 0.04f;
            SetBat(p, new Vector3(0.04f, 0.86f + tap, 0.34f), new Vector3(0.06f, -1, 0.1f), new Vector3(-1, 0, 0));
            return p;
        }

        public static Pose Backlift(bool horizontal)
        {
            var p = Stance();
            p.HipH = 0.84f;
            p.FootL = new Vector3(-0.36f, 0.12f, 0.06f);
            p.Head = new Vector3(8, -66, 0);
            if (horizontal) SetBat(p, new Vector3(0.12f, 1.48f, 0.28f), new Vector3(0.55f, 0.72f, -0.38f), new Vector3(-0.3f, 0.3f, 0.9f));
            else SetBat(p, new Vector3(0.16f, 1.32f, 0.2f), new Vector3(0.38f, 0.85f, -0.36f), new Vector3(-0.3f, 0.2f, 0.92f));
            return p;
        }

        public static Pose Leave()
        {
            var p = Stance();
            p.HipH = 0.88f;
            p.Head = new Vector3(14, -50, 0);
            p.Chest = new Vector3(2, -12, 0);
            SetBat(p, new Vector3(0.04f, 1.72f, 0.1f), new Vector3(0.22f, 0.96f, -0.12f), new Vector3(-1, 0, 0));
            return p;
        }

        /// <summary>Canonical direction of a batter-relative shot angle.</summary>
        public static Vector3 ShotDir(double az)
        {
            float a = (float)az * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Cos(a), 0, Mathf.Sin(a));
        }

        public static bool IsHorizontal(ShotKind kind, double az, LengthClass len, float contactY, bool spin)
        {
            if (kind == ShotKind.Defend || kind == ShotKind.Leave) return false;
            double a = az;
            bool shortish = len == LengthClass.Short || len == LengthClass.Bouncer;
            if (contactY > 1.25f) return true;
            if (shortish && (a > 60 || a < -40)) return true;
            if (spin && a < -60 && (len == LengthClass.Full || len == LengthClass.Good)) return true;
            return false;
        }

        /// <summary>The moment of impact: sweet spot of the bat on the ball.</summary>
        public static Pose Contact(Vector3 c, ShotKind kind, double az, bool frontFoot, bool horizontal, bool sweep)
        {
            var p = Base();
            var d = ShotDir(az);
            float a = (float)az;
            float bodyYaw = Mathf.Clamp(-60 + a * 0.5f, -140, 25);
            p.HipRot = new Vector3(14, bodyYaw * 0.45f, 0);
            p.Chest = new Vector3(8, bodyYaw * 0.4f, 0);
            p.Spine = new Vector3(frontFoot ? 18 : 6, 0, 0);
            if (sweep)
            {
                p.HipH = 0.5f;
                p.Spine = new Vector3(26, 0, 0);
                p.FootL = new Vector3(-0.72f, 0.085f, 0.3f);
                p.FootR = new Vector3(0.36f, 0.1f, -0.1f);
                p.FootRPitch = -55;
                p.FootLYaw = -40;
            }
            else if (frontFoot)
            {
                p.HipH = 0.8f;
                p.FootL = new Vector3(Mathf.Min(-0.55f, c.x + 0.05f), 0.085f, Mathf.Clamp(c.z - 0.3f, -0.15f, 0.42f));
                p.FootR = new Vector3(0.22f, 0.1f, 0.0f);
                p.FootLYaw = -35;
                p.FootRPitch = -25;
                p.HipShift = new Vector3(-0.12f, 0, 0.05f);
            }
            else
            {
                p.HipH = 0.89f;
                p.FootR = new Vector3(0.32f, 0.085f, 0.24f);
                p.FootL = new Vector3(-0.18f, 0.085f, 0.1f);
                p.FootRYaw = 10;
                p.HipShift = new Vector3(0.08f, 0, 0.05f);
            }

            Vector3 dir, face;
            if (kind == ShotKind.Defend)
            {
                dir = new Vector3(-0.18f, -1, 0.06f);
                face = new Vector3(-1, -0.15f, 0.15f);
                p.Spine = new Vector3(frontFoot ? 22 : 8, 0, 0);
            }
            else if (horizontal)
            {
                face = (d + Vector3.up * (kind == ShotKind.Loft ? 0.45f : 0.05f)).normalized;
                dir = new Vector3(-0.92f, sweep ? -0.25f : -0.18f, 0.12f);
            }
            else
            {
                dir = kind == ShotKind.Loft ? (Vector3.down + d * 0.5f) : (Vector3.down + d * 0.18f);
                face = kind == ShotKind.Loft ? (d + Vector3.up * 0.55f) : d;
            }
            dir.Normalize();
            var grip = c - dir * 0.6f;
            // Keep the hands within reach of the shoulders.
            var sh = new Vector3(0.0f, 1.38f, 0.05f);
            var off = grip - sh;
            if (off.magnitude > 0.66f) grip = sh + off.normalized * 0.66f;
            SetBat(p, grip, dir, face);

            var head = new Vector3(0, 1.62f, 0.05f);
            var toBall = c - head;
            float yaw = Mathf.Atan2(toBall.x, toBall.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toBall.y, new Vector2(toBall.x, toBall.z).magnitude) * Mathf.Rad2Deg;
            p.Head = new Vector3(Mathf.Clamp(pitch * 0.8f, -30, 55), Mathf.Clamp(yaw - bodyYaw * 0.85f, -85, 85), 0);
            return p;
        }

        public static Pose Follow(Pose contact, ShotKind kind, double az, bool horizontal)
        {
            var p = contact.Clone();
            var d = ShotDir(az);
            if (kind == ShotKind.Defend) return p;
            p.HipRot = new Vector3(p.HipRot.x - 4, p.HipRot.y - 12, 0);
            p.Chest = new Vector3(p.Chest.x - 6, p.Chest.y - 18, 0);
            p.Head = new Vector3(-10, p.Head.y * 0.5f, 0);
            if (horizontal)
            {
                SetBat(p, new Vector3(-0.02f, 1.36f, -0.22f), new Vector3(0.75f, 0.35f, -0.55f), new Vector3(-0.4f, 0.2f, -0.8f));
            }
            else if (kind == ShotKind.Loft)
            {
                SetBat(p, new Vector3(-0.1f, 1.62f, 0.22f), new Vector3(0.55f, 0.72f, -0.42f), new Vector3(-0.6f, 0.4f, 0.6f));
            }
            else
            {
                var g = new Vector3(-0.3f + d.x * 0.18f, 1.48f, 0.32f + d.z * 0.18f);
                SetBat(p, g, (d * 0.55f + Vector3.up * 0.8f + new Vector3(0.2f, 0, -0.2f)).normalized, (d + Vector3.down * 0.3f).normalized);
            }
            return p;
        }

        // --------------------------------------------------------------- bowling

        /// <summary>Delivery stride for a right-arm bowler running towards +Z; release at <see cref="ReleaseTime"/>.</summary>
        public static Clip Deliver(bool spin)
        {
            float jump = spin ? 0.08f : 0.22f;
            var gather = new Pose { HipH = 1.0f, RootOffset = new Vector3(0, jump, 0), HipRot = new Vector3(0, 40, 0), Chest = new Vector3(-4, 30, 0), Head = new Vector3(0, -55, 0) };
            gather.FootL = new Vector3(-0.1f, 0.3f, 0.2f);
            gather.FootR = new Vector3(0.1f, 0.18f, -0.2f);
            gather.L = ArmP.F(-160, 0, -10, 10);
            gather.R = ArmP.F(30, 0, 20, 25);

            var back = new Pose { HipH = 0.92f, RootOffset = new Vector3(0, 0, spin ? 0.5f : 0.9f), HipRot = new Vector3(-5, 70, 0), Chest = new Vector3(-10, 22, 0), Head = new Vector3(0, -82, 0) };
            back.FootR = new Vector3(0.05f, 0.085f, -0.1f);
            back.FootRYaw = 70;
            back.FootL = new Vector3(-0.05f, 0.32f, 0.32f);
            back.L = ArmP.F(-150, 0, -15, 5);
            back.R = ArmP.F(95, 0, 15, 15);

            var front = new Pose { HipH = 0.9f, RootOffset = new Vector3(0, 0, spin ? 0.9f : 1.6f), HipRot = new Vector3(10, 30, 0), Chest = new Vector3(5, 10, 0), Head = new Vector3(5, -22, 0) };
            front.FootL = new Vector3(-0.08f, 0.085f, 0.55f);
            front.FootR = new Vector3(0.12f, 0.13f, -0.35f);
            front.FootRPitch = -40;
            front.L = ArmP.F(-60, 0, -30, 60);
            front.R = ArmP.F(180, 0, 10, 5);

            var release = front.Clone();
            release.RootOffset = new Vector3(0, 0, spin ? 1.0f : 1.8f);
            release.HipRot = new Vector3(25, 0, 0);
            release.Chest = new Vector3(20, -10, 0);
            release.Head = new Vector3(10, -5, 0);
            release.R = ArmP.F(235, 0, 5, 0);
            release.L = ArmP.F(-10, 0, -35, 80);

            var follow = new Pose { HipH = 0.84f, RootOffset = new Vector3(0, 0, spin ? 1.5f : 2.6f), HipRot = new Vector3(35, -25, 0), Spine = new Vector3(18, 0, 0), Chest = new Vector3(14, -18, 0), Head = new Vector3(-20, 10, 0) };
            follow.FootL = new Vector3(-0.06f, 0.085f, 0.1f);
            follow.FootR = new Vector3(0.15f, 0.25f, 0.35f);
            follow.R = ArmP.F(330, 0, -35, 15);
            follow.L = ArmP.F(25, 0, -10, 40);

            var recover = Poses.Gait(0.25f, 3f, false);
            recover.RootOffset = new Vector3(0, 0, spin ? 2.4f : 3.9f);
            float k = spin ? 1.2f : 1f;
            return new Clip().Key(0, gather).Key(0.22f * k, back).Key(0.4f * k, front).Key(ReleaseTime(spin), release).Key(0.68f * k, follow).Key(1.05f * k, recover);
        }

        public static float ReleaseTime(bool spin) => spin ? 0.47f * 1.2f : 0.47f;

        /// <summary>Distance covered by the delivery stride's root motion up to release.</summary>
        public static float StrideToRelease(bool spin) => spin ? 1.0f : 1.8f;
    }
}
