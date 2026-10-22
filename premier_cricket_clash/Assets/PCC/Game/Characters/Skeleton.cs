using System;

namespace PCC.Game
{
    /// <summary>Joint positions of a posed player in cricket space.</summary>
    public sealed class Skeleton
    {
        public V3 Pelvis, Neck, Head, LSho, RSho, LElb, RElb, LHand, RHand;
        public V3 LHip, RHip, LKnee, RKnee, LAnkle, RAnkle, LToe, RToe;

        /// <summary>Pelvis, chest and head frames (right / forward / up).</summary>
        public V3 PR, PF, PU, CR, CF, CU, HR, HF, HU;
        public V3 Fwd;
        public bool HasBat;
        public V3 BatTop, BatTip, BatFace;
        public double S = 1;

        /// <summary>Two-bone IK: returns (middle joint, end joint).</summary>
        static (V3, V3) Ik(V3 root, V3 target, double a, double b, V3 pole)
        {
            var d = target - root;
            double dist = d.Length;
            double minD = Math.Abs(a - b) + 1e-3, maxD = a + b - 1e-3;
            if (dist < minD) dist = minD;
            if (dist > maxD) dist = maxD;
            var dir = d.Length < 1e-6 ? new V3(0, 0, -1) : d.Normalized;
            double x = (a * a - b * b + dist * dist) / (2 * dist);
            double h = Math.Sqrt(Math.Max(0, a * a - x * x));
            var pp = pole - dir * pole.Dot(dir);
            if (pp.Length < 1e-6)
            {
                pp = dir.Cross(V3.Up);
                if (pp.Length < 1e-6) pp = dir.Cross(new V3(1, 0, 0));
            }
            pp = pp.Normalized;
            return (root + dir * x + pp * h, root + dir * dist);
        }

        /// <param name="facing">0 = facing +y (towards the striker), pi/2 = facing +x.</param>
        public void Solve(Pose p, V3 origin, double facing, double scale, bool mirror = false)
        {
            var pose = mirror ? p.Mirrored() : p;
            S = scale;
            var fw = new V3(Math.Sin(facing), Math.Cos(facing), 0);
            var rt = new V3(Math.Cos(facing), -Math.Sin(facing), 0);
            var up = V3.Up;
            Fwd = fw;
            V3 Dir(V3 v) => rt * v.x + fw * v.y + up * v.z;
            V3 At(V3 v) => origin + Dir(v) * scale;

            // Pelvis frame.
            PF = V3.Rotate(fw, up, pose.PelvisYaw);
            PR = V3.Rotate(rt, up, pose.PelvisYaw);
            PU = up;
            PU = V3.Rotate(PU, PR, -pose.PelvisLean);
            PF = V3.Rotate(PF, PR, -pose.PelvisLean);
            PU = V3.Rotate(PU, PF, pose.PelvisSide);
            PR = V3.Rotate(PR, PF, pose.PelvisSide);
            // Chest frame.
            CF = V3.Rotate(PF, PU, pose.SpineYaw);
            CR = V3.Rotate(PR, PU, pose.SpineYaw);
            CU = PU;
            CU = V3.Rotate(CU, CR, -pose.SpineLean);
            CF = V3.Rotate(CF, CR, -pose.SpineLean);
            CU = V3.Rotate(CU, CF, pose.SpineSide);
            CR = V3.Rotate(CR, CF, pose.SpineSide);
            // Head frame.
            HF = V3.Rotate(CF, CU, pose.HeadYaw);
            HR = V3.Rotate(CR, CU, pose.HeadYaw);
            HU = CU;
            HU = V3.Rotate(HU, HR, -pose.HeadPitch);
            HF = V3.Rotate(HF, HR, -pose.HeadPitch);

            double s = scale;
            Pelvis = At(pose.Hip);
            Neck = Pelvis + CU * (0.5 * s);
            Head = Neck + HU * (0.15 * s) + HF * (0.025 * s);
            LSho = Neck + CR * (-0.19 * s) + CU * (-0.05 * s);
            RSho = Neck + CR * (0.19 * s) + CU * (-0.05 * s);
            LHip = Pelvis + PR * (-0.1 * s) + PU * (-0.05 * s);
            RHip = Pelvis + PR * (0.1 * s) + PU * (-0.05 * s);

            var lTarget = At(pose.LHand);
            var rTarget = At(pose.RHand);
            HasBat = pose.Hold != BatHold.None;
            if (pose.Hold == BatHold.Both)
            {
                var bd = Dir(pose.Bat).Normalized;
                var top = At(pose.Grip);
                var low = top + bd * (0.105 * s);
                if (pose.TopLeft)
                {
                    lTarget = top;
                    rTarget = low;
                }
                else
                {
                    rTarget = top;
                    lTarget = low;
                }
                BatTop = top - bd * (0.07 * s);
                BatTip = BatTop + bd * (0.86 * s);
            }
            var la = Ik(LSho, lTarget, 0.29 * s, 0.31 * s, Dir(pose.LElbow));
            var ra = Ik(RSho, rTarget, 0.29 * s, 0.31 * s, Dir(pose.RElbow));
            LElb = la.Item1;
            LHand = la.Item2;
            RElb = ra.Item1;
            RHand = ra.Item2;
            if (pose.Hold == BatHold.Right)
            {
                var bd = Dir(pose.Bat).Normalized;
                BatTop = RHand - bd * (0.08 * s);
                BatTip = BatTop + bd * (0.86 * s);
            }
            if (HasBat)
            {
                var bd = (BatTip - BatTop).Normalized;
                var toBowler = Dir(new V3(pose.TopLeft ? -1 : 1, 0.15, 0));
                var face = toBowler - bd * toBowler.Dot(bd);
                if (face.Length < 1e-4) face = bd.Cross(V3.Up);
                BatFace = face.Normalized;
            }

            var ll = Ik(LHip, At(pose.LFoot), 0.44 * s, 0.43 * s, Dir(pose.LKnee));
            var rl = Ik(RHip, At(pose.RFoot), 0.44 * s, 0.43 * s, Dir(pose.RKnee));
            LKnee = ll.Item1;
            LAnkle = ll.Item2;
            RKnee = rl.Item1;
            RAnkle = rl.Item2;
            LToe = LAnkle + Dir(pose.LToe).Normalized * (0.17 * s);
            RToe = RAnkle + Dir(pose.RToe).Normalized * (0.17 * s);
        }
    }
}
