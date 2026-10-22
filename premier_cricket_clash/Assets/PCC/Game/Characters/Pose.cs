using System;
using System.Collections.Generic;

namespace PCC.Game
{
    public enum BatHold { None, Both, Right }

    /// <summary>
    /// Character pose. Local space: x = the player's right, y = the way they
    /// face, z = up, origin on the ground between the feet (1.8 m athlete).
    /// Poses give pelvis/spine/head orientation plus hand and foot targets;
    /// arms and legs are solved with two-bone IK so limb lengths stay correct
    /// and any two poses blend naturally.
    /// </summary>
    public sealed class Pose
    {
        static V3 V(double r, double f, double u) => new V3(r, f, u);

        public V3 Hip = V(0, 0, 0.96);
        public double PelvisYaw, PelvisLean, PelvisSide;
        public double SpineYaw, SpineLean, SpineSide;
        public double HeadYaw, HeadPitch;
        public V3 LHand = V(-0.25, 0.04, 0.84);
        public V3 RHand = V(0.25, 0.04, 0.84);
        public V3 LElbow = V(-0.5, -1, -0.3);
        public V3 RElbow = V(0.5, -1, -0.3);
        public V3 LFoot = V(-0.12, 0, 0.08);
        public V3 RFoot = V(0.12, 0, 0.08);
        public V3 LToe = V(-0.12, 1, 0);
        public V3 RToe = V(0.12, 1, 0);
        public V3 LKnee = V(-0.1, 1, 0);
        public V3 RKnee = V(0.1, 1, 0);
        public BatHold Hold = BatHold.None;

        /// <summary>Top hand position on the handle (two-handed) and bat direction.</summary>
        public V3 Grip = V(0, 0.26, 0.84);
        public V3 Bat = V(0.05, 0.12, -1);

        /// <summary>Which hand is at the top of the handle (left for right-handers).</summary>
        public bool TopLeft = true;

        public Pose Clone() => (Pose)MemberwiseClone();

        static double L(double a, double b, double t) => a + (b - a) * t;

        public static Pose Lerp(Pose a, Pose b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            return new Pose
            {
                Hip = V3.Lerp(a.Hip, b.Hip, t),
                PelvisYaw = L(a.PelvisYaw, b.PelvisYaw, t),
                PelvisLean = L(a.PelvisLean, b.PelvisLean, t),
                PelvisSide = L(a.PelvisSide, b.PelvisSide, t),
                SpineYaw = L(a.SpineYaw, b.SpineYaw, t),
                SpineLean = L(a.SpineLean, b.SpineLean, t),
                SpineSide = L(a.SpineSide, b.SpineSide, t),
                HeadYaw = L(a.HeadYaw, b.HeadYaw, t),
                HeadPitch = L(a.HeadPitch, b.HeadPitch, t),
                LHand = V3.Lerp(a.LHand, b.LHand, t),
                RHand = V3.Lerp(a.RHand, b.RHand, t),
                LElbow = V3.Lerp(a.LElbow, b.LElbow, t),
                RElbow = V3.Lerp(a.RElbow, b.RElbow, t),
                LFoot = V3.Lerp(a.LFoot, b.LFoot, t),
                RFoot = V3.Lerp(a.RFoot, b.RFoot, t),
                LToe = V3.Lerp(a.LToe, b.LToe, t),
                RToe = V3.Lerp(a.RToe, b.RToe, t),
                LKnee = V3.Lerp(a.LKnee, b.LKnee, t),
                RKnee = V3.Lerp(a.RKnee, b.RKnee, t),
                Hold = a.Hold == b.Hold ? a.Hold : (t < 0.5 ? a.Hold : b.Hold),
                Grip = V3.Lerp(a.Grip, b.Grip, t),
                Bat = V3.Lerp(a.Bat.Normalized, b.Bat.Normalized, t),
                TopLeft = t < 0.5 ? a.TopLeft : b.TopLeft,
            };
        }

        static V3 M(V3 v) => new V3(-v.x, v.y, v.z);

        /// <summary>Left-right mirror (left-handed batters, left-arm bowlers).</summary>
        public Pose Mirrored() => new Pose
        {
            Hip = M(Hip),
            PelvisYaw = -PelvisYaw,
            PelvisLean = PelvisLean,
            PelvisSide = -PelvisSide,
            SpineYaw = -SpineYaw,
            SpineLean = SpineLean,
            SpineSide = -SpineSide,
            HeadYaw = -HeadYaw,
            HeadPitch = HeadPitch,
            LHand = M(RHand),
            RHand = M(LHand),
            LElbow = M(RElbow),
            RElbow = M(LElbow),
            LFoot = M(RFoot),
            RFoot = M(LFoot),
            LToe = M(RToe),
            RToe = M(LToe),
            LKnee = M(RKnee),
            RKnee = M(LKnee),
            Hold = Hold,
            Grip = M(Grip),
            Bat = M(Bat),
            TopLeft = !TopLeft,
        };

        public Pose WithBat(BatHold hold, V3 bat)
        {
            var p = Clone();
            p.Hold = hold;
            p.Bat = bat;
            return p;
        }

        /// <summary>Plays (time, pose) keyframes with smooth interpolation.</summary>
        public static Pose Sample(IList<(double T, Pose P)> keys, double t)
        {
            if (t <= keys[0].T) return keys[0].P;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];
                if (t <= b.T)
                {
                    double u = (t - a.T) / Math.Max(1e-6, b.T - a.T);
                    return Lerp(a.P, b.P, u * u * (3 - 2 * u));
                }
            }
            return keys[keys.Count - 1].P;
        }
    }

    /// <summary>
    /// Pose library (right-handed batter facing the off side, right-arm bowler
    /// running towards +y). Values carried over from the tuned 2D/3D prototype.
    /// </summary>
    public static class Poses
    {
        static V3 V(double r, double f, double u) => new V3(r, f, u);

        static readonly V3 BElbL = V(-0.6, 0.5, -0.4);
        static readonly V3 BElbR = V(0.5, 0.3, -0.8);

        public static readonly Pose BatStance = new Pose {
          Hip = V(0.0, -0.02, 0.89),
          PelvisLean = 0.12,
          SpineLean = 0.22,
          SpineYaw = 0.12,
          HeadYaw = 1.25,
          HeadPitch = 0.18,
          LFoot = V(-0.24, 0.03, 0.08),
          RFoot = V(0.22, 0.0, 0.08),
          LToe = V(-0.15, 1, 0),
          RToe = V(0.25, 1, 0),
          LKnee = V(-0.3, 1, 0),
          RKnee = V(0.3, 1, 0),
          Hold = BatHold.Both,
          Grip = V(0.04, 0.27, 0.84),
          Bat = V(0.08, 0.1, -1),
          LElbow = BElbL,
          RElbow = BElbR,
        };

        public static readonly Pose BatBacklift = new Pose {
          Hip = V(0.02, -0.02, 0.87),
          PelvisLean = 0.12,
          SpineLean = 0.2,
          SpineYaw = -0.12,
          HeadYaw = 1.3,
          HeadPitch = 0.15,
          LFoot = V(-0.27, 0.04, 0.08),
          RFoot = V(0.22, 0.0, 0.08),
          LToe = V(-0.15, 1, 0),
          RToe = V(0.25, 1, 0),
          LKnee = V(-0.3, 1, 0),
          RKnee = V(0.3, 1, 0),
          Hold = BatHold.Both,
          Grip = V(0.2, 0.14, 1.1),
          Bat = V(0.55, -0.25, 0.8),
          LElbow = V(-0.2, 0.6, -0.5),
          RElbow = V(0.8, 0.2, -0.6),
        };

        public static readonly Pose DriveContact = new Pose {
          Hip = V(-0.3, 0.02, 0.8),
          PelvisYaw = 0.2,
          PelvisLean = 0.18,
          PelvisSide = -0.12,
          SpineLean = 0.38,
          SpineYaw = 0.15,
          SpineSide = -0.18,
          HeadYaw = 1.35,
          HeadPitch = 0.45,
          LFoot = V(-0.68, 0.1, 0.08),
          RFoot = V(0.24, -0.02, 0.1),
          LToe = V(-0.6, 0.8, 0),
          RToe = V(0.35, 1, -0.2),
          LKnee = V(-1, 0.5, 0),
          RKnee = V(0.2, 1, -0.3),
          Hold = BatHold.Both,
          Grip = V(-0.42, 0.25, 0.74),
          Bat = V(-0.3, 0.12, -0.95),
          LElbow = V(-0.5, 0.8, 0.0),
          RElbow = V(0.3, 0.6, -0.6),
        };

        public static readonly Pose DriveFollow = new Pose {
          Hip = V(-0.3, 0.02, 0.82),
          PelvisYaw = 0.5,
          PelvisLean = 0.12,
          PelvisSide = -0.1,
          SpineLean = 0.18,
          SpineYaw = 0.4,
          SpineSide = -0.1,
          HeadYaw = 1.0,
          HeadPitch = 0.1,
          LFoot = V(-0.68, 0.1, 0.08),
          RFoot = V(0.22, -0.02, 0.12),
          LToe = V(-0.6, 0.8, 0),
          RToe = V(0.35, 1, -0.3),
          LKnee = V(-1, 0.5, 0),
          RKnee = V(0.2, 1, -0.3),
          Hold = BatHold.Both,
          Grip = V(-0.32, 0.05, 1.55),
          Bat = V(0.35, -0.45, 0.82),
          LElbow = V(-0.8, 0.3, 0.4),
          RElbow = V(0.2, 0.9, 0.0),
        };

        public static readonly Pose LoftFollow = new Pose {
          Hip = V(-0.2, 0.0, 0.86),
          PelvisYaw = 0.55,
          PelvisLean = 0.02,
          SpineLean = -0.12,
          SpineYaw = 0.35,
          HeadYaw = 0.9,
          HeadPitch = -0.35,
          LFoot = V(-0.55, 0.1, 0.08),
          RFoot = V(0.24, -0.02, 0.12),
          LToe = V(-0.6, 0.8, 0),
          RToe = V(0.35, 1, -0.3),
          LKnee = V(-1, 0.5, 0),
          RKnee = V(0.2, 1, -0.3),
          Hold = BatHold.Both,
          Grip = V(-0.15, 0.12, 1.78),
          Bat = V(0.3, -0.25, 0.92),
          LElbow = V(-0.8, 0.2, 0.5),
          RElbow = V(0.6, 0.6, 0.3),
        };

        public static readonly Pose PullContact = new Pose {
          Hip = V(0.12, -0.02, 0.88),
          PelvisYaw = 0.75,
          PelvisLean = 0.05,
          SpineYaw = 0.5,
          SpineLean = 0.05,
          SpineSide = 0.12,
          HeadYaw = 0.55,
          HeadPitch = 0.05,
          LFoot = V(-0.26, 0.14, 0.08),
          RFoot = V(0.26, -0.08, 0.08),
          LToe = V(-0.8, 0.6, 0),
          RToe = V(0.2, 1, 0),
          LKnee = V(-0.8, 0.6, 0),
          RKnee = V(0.2, 1, 0),
          Hold = BatHold.Both,
          Grip = V(-0.05, 0.32, 1.18),
          Bat = V(-0.5, 0.85, 0.12),
          LElbow = V(-0.3, 0.2, -1),
          RElbow = V(0.6, 0.2, -0.8),
        };

        public static readonly Pose PullFollow = new Pose {
          Hip = V(0.12, -0.02, 0.88),
          PelvisYaw = 1.25,
          PelvisLean = 0.05,
          SpineYaw = 0.75,
          SpineSide = 0.1,
          HeadYaw = 0.2,
          HeadPitch = 0.0,
          LFoot = V(-0.26, 0.14, 0.1),
          RFoot = V(0.26, -0.08, 0.08),
          LToe = V(-0.8, 0.4, 0),
          RToe = V(-0.2, 1, 0),
          LKnee = V(-0.8, 0.4, 0),
          RKnee = V(-0.2, 1, 0),
          Hold = BatHold.Both,
          Grip = V(-0.25, -0.12, 1.42),
          Bat = V(0.45, -0.85, 0.3),
          LElbow = V(-0.9, 0.0, 0.2),
          RElbow = V(0.2, 0.6, -0.6),
        };

        public static readonly Pose CutContact = new Pose {
          Hip = V(0.08, 0.05, 0.86),
          PelvisYaw = -0.1,
          PelvisLean = 0.15,
          SpineLean = 0.25,
          SpineYaw = -0.15,
          SpineSide = 0.08,
          HeadYaw = 0.9,
          HeadPitch = 0.35,
          LFoot = V(-0.22, 0.05, 0.08),
          RFoot = V(0.18, 0.32, 0.08),
          LToe = V(-0.2, 1, 0),
          RToe = V(-0.3, 1, 0),
          LKnee = V(-0.3, 1, 0),
          RKnee = V(0.0, 1, 0),
          Hold = BatHold.Both,
          Grip = V(-0.02, 0.42, 1.08),
          Bat = V(-0.25, 0.85, -0.45),
          LElbow = V(-0.7, 0.2, -0.5),
          RElbow = V(0.5, 0.3, -0.8),
        };

        public static readonly Pose CutFollow = new Pose {
          Hip = V(0.08, 0.05, 0.86),
          PelvisYaw = 0.05,
          PelvisLean = 0.15,
          SpineLean = 0.25,
          SpineYaw = 0.15,
          SpineSide = 0.05,
          HeadYaw = 0.8,
          HeadPitch = 0.3,
          LFoot = V(-0.22, 0.05, 0.08),
          RFoot = V(0.18, 0.32, 0.08),
          LToe = V(-0.2, 1, 0),
          RToe = V(-0.3, 1, 0),
          LKnee = V(-0.3, 1, 0),
          RKnee = V(0.0, 1, 0),
          Hold = BatHold.Both,
          Grip = V(-0.28, 0.36, 0.92),
          Bat = V(-0.75, 0.35, -0.55),
          LElbow = V(-0.8, 0.0, -0.4),
          RElbow = V(0.3, 0.6, -0.7),
        };

        public static readonly Pose Defend = new Pose {
          Hip = V(-0.22, 0.02, 0.84),
          PelvisYaw = 0.1,
          PelvisLean = 0.18,
          PelvisSide = -0.08,
          SpineLean = 0.42,
          SpineYaw = 0.1,
          SpineSide = -0.15,
          HeadYaw = 1.4,
          HeadPitch = 0.5,
          LFoot = V(-0.52, 0.1, 0.08),
          RFoot = V(0.22, 0.0, 0.08),
          LToe = V(-0.5, 0.9, 0),
          RToe = V(0.3, 1, 0),
          LKnee = V(-1, 0.5, 0),
          RKnee = V(0.3, 1, 0),
          Hold = BatHold.Both,
          Grip = V(-0.36, 0.3, 0.82),
          Bat = V(-0.22, 0.05, -0.97),
          LElbow = V(-0.4, 0.9, 0.2),
          RElbow = V(0.4, 0.6, -0.5),
        };

        public static readonly Pose Sweep = new Pose {
          Hip = V(-0.02, 0.05, 0.56),
          PelvisYaw = 0.25,
          PelvisLean = 0.25,
          SpineLean = 0.45,
          SpineYaw = 0.2,
          SpineSide = -0.1,
          HeadYaw = 1.3,
          HeadPitch = 0.45,
          LFoot = V(-0.6, 0.18, 0.08),
          RFoot = V(0.38, -0.28, 0.12),
          LToe = V(-0.7, 0.7, 0),
          RToe = V(0.3, -0.4, -0.85),
          LKnee = V(-1, 0.6, 0.3),
          RKnee = V(0.0, 1, -1.5),
          Hold = BatHold.Both,
          Grip = V(-0.25, 0.48, 0.55),
          Bat = V(-0.85, 0.4, -0.3),
          LElbow = V(-0.6, 0.3, -0.6),
          RElbow = V(0.5, 0.5, -0.6),
        };

        public static readonly Pose Leave = new Pose {
          Hip = V(0.0, -0.02, 0.9),
          PelvisLean = 0.08,
          SpineLean = 0.1,
          SpineYaw = -0.1,
          HeadYaw = 1.2,
          HeadPitch = 0.1,
          LFoot = V(-0.26, 0.04, 0.08),
          RFoot = V(0.22, 0.0, 0.08),
          LToe = V(-0.15, 1, 0),
          RToe = V(0.25, 1, 0),
          Hold = BatHold.Both,
          Grip = V(0.05, 0.12, 1.55),
          Bat = V(0.35, -0.3, 0.88),
          LElbow = V(-0.6, 0.4, 0.2),
          RElbow = V(0.8, 0.3, -0.2),
        };

        // --------------------------------------------------------------- bowling
        // Right-arm bowler running towards +y.

        public static readonly Pose RunA = new Pose {
          Hip = V(0, 0, 0.94),
          PelvisLean = 0.12,
          SpineLean = 0.12,
          SpineYaw = 0.12,
          LFoot = V(-0.1, 0.42, 0.14),
          RFoot = V(0.11, -0.42, 0.3),
          LToe = V(0, 1, 0.1),
          RToe = V(0, 0.5, -0.86),
          LKnee = V(0, 1, 0.2),
          RKnee = V(0, 1, -0.3),
          LHand = V(-0.24, -0.26, 0.98),
          RHand = V(0.2, 0.32, 1.12),
          LElbow = V(-0.3, -1, 0.2),
          RElbow = V(0.3, -1, -0.6),
        };

        public static readonly Pose RunB = new Pose {
          Hip = V(0, 0, 0.94),
          PelvisLean = 0.12,
          SpineLean = 0.12,
          SpineYaw = -0.12,
          LFoot = V(-0.11, -0.42, 0.3),
          RFoot = V(0.1, 0.42, 0.14),
          LToe = V(0, 0.5, -0.86),
          RToe = V(0, 1, 0.1),
          LKnee = V(0, 1, -0.3),
          RKnee = V(0, 1, 0.2),
          LHand = V(-0.2, 0.32, 1.12),
          RHand = V(0.24, -0.26, 0.98),
          LElbow = V(-0.3, -1, -0.6),
          RElbow = V(0.3, -1, 0.2),
        };

        public static readonly Pose Gather = new Pose {
          Hip = V(0, 0.05, 1.18),
          PelvisYaw = -0.9,
          PelvisLean = -0.05,
          SpineYaw = -0.4,
          SpineLean = -0.08,
          HeadYaw = 1.1,
          HeadPitch = 0.05,
          LFoot = V(-0.08, 0.3, 0.5),
          RFoot = V(0.1, -0.2, 0.36),
          LToe = V(0, 1, -0.3),
          RToe = V(0.4, 0.4, -0.8),
          LKnee = V(-0.2, 1, 0.5),
          RKnee = V(0, 1, -0.2),
          LHand = V(-0.12, 0.5, 2.15),
          RHand = V(0.2, -0.05, 1.3),
          LElbow = V(-0.6, 0.2, 0.3),
          RElbow = V(0.8, -0.3, -0.5),
        };

        public static readonly Pose Load = new Pose {
          Hip = V(0, 0.0, 0.92),
          PelvisYaw = -1.2,
          PelvisLean = -0.1,
          SpineYaw = -0.3,
          SpineLean = -0.22,
          SpineSide = 0.12,
          HeadYaw = 1.35,
          HeadPitch = 0.0,
          LFoot = V(-0.05, 0.62, 0.32),
          RFoot = V(0.06, -0.1, 0.08),
          LToe = V(0, 1, 0.1),
          RToe = V(1, 0.2, 0),
          LKnee = V(0, 1, 0.4),
          RKnee = V(0.6, 0.6, 0),
          LHand = V(-0.08, 0.72, 1.95),
          RHand = V(0.12, -0.62, 1.15),
          LElbow = V(-0.8, 0.2, 0.2),
          RElbow = V(0.6, -0.2, -0.8),
        };

        public static readonly Pose Release = new Pose {
          Hip = V(0, 0.38, 0.9),
          PelvisYaw = -0.25,
          PelvisLean = 0.15,
          SpineYaw = 0.05,
          SpineLean = 0.25,
          SpineSide = -0.12,
          HeadYaw = 0.1,
          HeadPitch = 0.1,
          LFoot = V(-0.08, 0.88, 0.08),
          RFoot = V(0.1, -0.3, 0.2),
          LToe = V(0, 1, 0),
          RToe = V(0.6, 0.5, -0.6),
          LKnee = V(0, 1, 0.1),
          RKnee = V(0.3, 1, -0.3),
          LHand = V(-0.34, 0.0, 1.0),
          RHand = V(0.1, 0.42, 2.28),
          LElbow = V(-1, -0.4, -0.2),
          RElbow = V(0.6, -0.5, 0.0),
        };

        public static readonly Pose Follow = new Pose {
          Hip = V(-0.02, 0.72, 0.88),
          PelvisYaw = 0.4,
          PelvisLean = 0.22,
          SpineYaw = 0.3,
          SpineLean = 0.42,
          SpineSide = -0.08,
          HeadYaw = -0.2,
          HeadPitch = -0.25,
          LFoot = V(-0.08, 0.95, 0.08),
          RFoot = V(0.12, 0.5, 0.32),
          LToe = V(0, 1, 0),
          RToe = V(0, 1, -0.5),
          LKnee = V(0, 1, 0.1),
          RKnee = V(0, 1, 0.3),
          LHand = V(-0.38, 0.35, 1.05),
          RHand = V(-0.3, 1.08, 0.7),
          LElbow = V(-1, -0.5, 0.0),
          RElbow = V(0.6, 0.2, 0.4),
        };

        // -------------------------------------------------------- fielding etc.

        public static readonly Pose KeeperCrouch = new Pose {
          Hip = V(0, -0.08, 0.5),
          PelvisLean = 0.35,
          SpineLean = 0.45,
          HeadPitch = -0.35,
          LFoot = V(-0.3, 0.06, 0.08),
          RFoot = V(0.3, 0.06, 0.08),
          LToe = V(-0.35, 1, 0),
          RToe = V(0.35, 1, 0),
          LKnee = V(-0.8, 1, 0),
          RKnee = V(0.8, 1, 0),
          LHand = V(-0.07, 0.42, 0.42),
          RHand = V(0.07, 0.42, 0.42),
          LElbow = V(-1, 0.2, -0.3),
          RElbow = V(1, 0.2, -0.3),
        };

        public static readonly Pose KeeperUp = new Pose {
          Hip = V(0, -0.02, 0.86),
          PelvisLean = 0.15,
          SpineLean = 0.2,
          LFoot = V(-0.28, 0.06, 0.08),
          RFoot = V(0.28, 0.06, 0.08),
          LToe = V(-0.35, 1, 0),
          RToe = V(0.35, 1, 0),
          LKnee = V(-0.6, 1, 0),
          RKnee = V(0.6, 1, 0),
          LHand = V(-0.09, 0.42, 1.22),
          RHand = V(0.09, 0.42, 1.22),
          LElbow = V(-1, 0, -0.5),
          RElbow = V(1, 0, -0.5),
        };

        public static readonly Pose Slip = new Pose {
          Hip = V(0, -0.04, 0.72),
          PelvisLean = 0.3,
          SpineLean = 0.4,
          HeadPitch = -0.3,
          LFoot = V(-0.28, 0.04, 0.08),
          RFoot = V(0.28, 0.04, 0.08),
          LToe = V(-0.25, 1, 0),
          RToe = V(0.25, 1, 0),
          LKnee = V(-0.5, 1, 0),
          RKnee = V(0.5, 1, 0),
          LHand = V(-0.12, 0.46, 0.6),
          RHand = V(0.12, 0.46, 0.6),
          LElbow = V(-1, 0, -0.4),
          RElbow = V(1, 0, -0.4),
        };

        public static readonly Pose FieldReady = new Pose {
          Hip = V(0, -0.02, 0.86),
          PelvisLean = 0.18,
          SpineLean = 0.28,
          HeadPitch = -0.2,
          LFoot = V(-0.22, 0.04, 0.08),
          RFoot = V(0.22, 0.04, 0.08),
          LToe = V(-0.2, 1, 0),
          RToe = V(0.2, 1, 0),
          LKnee = V(-0.4, 1, 0),
          RKnee = V(0.4, 1, 0),
          LHand = V(-0.17, 0.38, 0.66),
          RHand = V(0.17, 0.38, 0.66),
          LElbow = V(-0.6, -0.6, 0),
          RElbow = V(0.6, -0.6, 0),
        };

        public static readonly Pose Catch = new Pose {
          Hip = V(0, 0, 0.95),
          SpineLean = -0.08,
          HeadPitch = -0.5,
          LFoot = V(-0.16, 0.04, 0.08),
          RFoot = V(0.16, 0.0, 0.08),
          LHand = V(-0.08, 0.28, 2.0),
          RHand = V(0.08, 0.28, 2.0),
          LElbow = V(-1, 0, 0),
          RElbow = V(1, 0, 0),
        };

        public static readonly Pose Throw = new Pose {
          Hip = V(0, 0, 0.9),
          PelvisYaw = -0.9,
          SpineYaw = -0.4,
          SpineLean = -0.05,
          HeadYaw = 1.0,
          LFoot = V(-0.12, 0.45, 0.08),
          RFoot = V(0.15, -0.38, 0.08),
          LToe = V(0, 1, 0),
          RToe = V(1, 0.3, 0),
          LKnee = V(0, 1, 0),
          RKnee = V(0.5, 0.6, 0),
          LHand = V(-0.15, 0.85, 1.5),
          RHand = V(0.35, -0.55, 1.75),
          LElbow = V(-0.6, 0.2, -0.6),
          RElbow = V(0.6, -0.4, -0.2),
        };

        public static readonly Pose Stand = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.13, 0.02, 0.08),
          RFoot = V(0.13, 0.0, 0.08),
          LHand = V(-0.24, 0.06, 0.84),
          RHand = V(0.24, 0.06, 0.84),
        };

        /// Non-striker: bat grounded in the right hand.
        public static readonly Pose NonStriker = new Pose {
          Hip = V(0, 0, 0.95),
          SpineLean = 0.05,
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.04, 0.08),
          LHand = V(-0.24, 0.05, 0.86),
          RHand = V(0.26, 0.24, 0.88),
          RElbow = V(0.6, -0.6, -0.4),
          Hold = BatHold.Right,
          Bat = V(0.1, 0.38, -0.92),
        };

        public static readonly Pose Umpire = new Pose {
          Hip = V(0, 0, 0.96),
          SpineLean = 0.08,
          HeadPitch = 0.05,
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.02, 0.08),
          LHand = V(-0.06, -0.2, 0.96),
          RHand = V(0.06, -0.2, 0.98),
          LElbow = V(-1, -0.3, -0.2),
          RElbow = V(1, -0.3, -0.2),
        };

        public static readonly Pose UmpOut = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.02, 0.08),
          LHand = V(-0.24, 0.06, 0.84),
          RHand = V(0.16, 0.2, 2.18),
          RElbow = V(1, 0, 0),
        };

        public static readonly Pose UmpSix = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.02, 0.08),
          LHand = V(-0.3, 0.08, 2.15),
          RHand = V(0.3, 0.08, 2.15),
          LElbow = V(-1, 0, 0),
          RElbow = V(1, 0, 0),
        };

        public static readonly Pose UmpFour = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.02, 0.08),
          LHand = V(-0.24, 0.06, 0.84),
          RHand = V(0.78, 0.25, 1.32),
          RElbow = V(0.3, -1, 0),
        };

        public static readonly Pose UmpWide = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.16, 0.02, 0.08),
          RFoot = V(0.16, 0.02, 0.08),
          LHand = V(-0.8, 0.05, 1.42),
          RHand = V(0.8, 0.05, 1.42),
          LElbow = V(0, -1, 0),
          RElbow = V(0, -1, 0),
        };

        public static readonly Pose UmpNoBall = new Pose {
          Hip = V(0, 0, 0.96),
          LFoot = V(-0.14, 0.02, 0.08),
          RFoot = V(0.14, 0.02, 0.08),
          LHand = V(-0.24, 0.06, 0.84),
          RHand = V(0.8, 0.08, 1.42),
          RElbow = V(0, -1, 0),
        };
    }
}
