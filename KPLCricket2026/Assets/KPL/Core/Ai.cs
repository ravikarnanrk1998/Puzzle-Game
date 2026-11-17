using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    /// <summary>AI captain and bowler decisions.</summary>
    public static class AiBowling
    {
        public static Player PickBowler(MatchState m, Rng rng)
        {
            var inn = m.Current;
            var options = m.EligibleBowlers();
            int oversLeft = inn.MaxOvers - inn.OverIndex;
            int phase = inn.Phase;
            Player best = null;
            double bestScore = double.MinValue;
            foreach (var p in options)
            {
                int left = inn.QuotaPerBowler - inn.OversBy(p);
                double s = p.CanBowl ? p.BowlSkill : p.BowlSkill - 30;
                if (!p.CanBowl && p.BowlSkill < 40) s -= 40;
                if (phase == 0) s += p.IsPace ? 6 : -4;
                if (phase == 1) s += p.IsSpin ? 8 : -2;
                if (phase == 2) s += p.IsPace ? 5 + (p.BowlSkill - 75) * 0.4 : -8;
                // Keep the best seamer's overs for the death.
                if (phase == 1 && p.IsPace && p.BowlSkill >= 80 && left <= 2 && oversLeft > 4) s -= 10;
                // Late in the innings, prefer bowlers who still have overs so nobody is stranded.
                s += left * (2 + inn.OverIndex * 0.6);
                if (left <= 0) s -= 100;
                s += rng.Range(-3, 3);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = p;
                }
            }
            return best ?? options.First();
        }

        public static FieldSetting PickField(Innings inn)
        {
            if (inn.Phase == 0) return FieldSetting.Attacking;
            if (inn.Phase == 2) return FieldSetting.Defensive;
            if (inn.Target != null && inn.RequiredRate > 11) return FieldSetting.Defensive;
            if (inn.Wkts >= 6) return FieldSetting.Attacking;
            return FieldSetting.Balanced;
        }

        /// <summary>Chooses delivery type and landing spot. <paramref name="accuracyBias"/> lifts or lowers execution.</summary>
        public static DeliveryPlan Plan(Player bowler, Player batter, Innings inn, Rng rng, double accuracyBias = 0)
        {
            var bt = bowler.CanBowl ? bowler.Bowl : BowlType.FastMedium;
            bool spin = bt.IsSpin();
            int phase = inn.Phase;
            var kinds = DeliveryKindX.For(bt);
            double[] w;
            if (spin)
            {
                // natural, flighted, arm/wrong'un..., yorker
                w = phase == 2 ? new[] { 0.36, 0.08, 0.2, 0.18, 0.18 } : new[] { 0.48, 0.18, 0.14, 0.14, 0.06 };
                if (bt == BowlType.LegSpin) { w[2] += 0.06; w[3] -= 0.04; }
            }
            else
            {
                // stock, outswing, inswing, yorker, bouncer, slower
                w = phase == 0 ? new[] { 0.3, 0.26, 0.16, 0.06, 0.12, 0.1 }
                    : phase == 1 ? new[] { 0.34, 0.14, 0.12, 0.1, 0.13, 0.17 }
                    : new[] { 0.12, 0.06, 0.06, 0.4, 0.12, 0.24 };
                if (bt == BowlType.Swing) { w[1] += 0.1; w[2] += 0.06; w[4] -= 0.06; }
            }
            var kind = kinds[rng.Weighted(w)];
            var band = kind.LengthBand(spin);
            double length;
            switch (kind)
            {
                case DeliveryKind.Yorker: length = rng.Range(0.5, 1.6); break;
                case DeliveryKind.Bouncer: length = rng.Range(9.2, 11.0); break;
                case DeliveryKind.Slower: length = rng.Range(4.0, 7.5); break;
                case DeliveryKind.Flighted: length = rng.Range(2.6, 4.2); break;
                default: length = spin ? rng.Range(3.4, 5.6) : rng.Range(4.8, 7.2); break;
            }
            length = MathX.Clamp(length, band.Min, band.Max);
            double line;
            if (kind == DeliveryKind.Outswing || kind == DeliveryKind.LegBreak) line = rng.Range(0.12, 0.38);
            else if (kind == DeliveryKind.Yorker) line = phase == 2 && rng.Chance(0.3) ? rng.Range(0.55, 0.78) : rng.Range(-0.05, 0.12);
            else if (kind == DeliveryKind.Bouncer) line = rng.Range(-0.15, 0.25);
            else if (kind == DeliveryKind.Inswing || kind == DeliveryKind.OffBreak) line = rng.Range(0.05, 0.3);
            else line = rng.Range(0.0, 0.3);
            double acc = 0.5 + bowler.BowlSkill / 100.0 * 0.38 + accuracyBias + rng.Gauss() * 0.08;
            if (!bowler.CanBowl) acc -= 0.15;
            return new DeliveryPlan { Kind = kind, Line = line, Length = length, Accuracy = MathX.Clamp(acc, 0.15, 0.98), Effort = rng.Range(0.3, 0.8) };
        }
    }

    public static class AiBatting
    {
        static readonly double[] Centers = { 0, 45, 95, 140, -45, -95, -145 };

        public static double Aggression(Innings inn, BatCard card)
        {
            var p = card.P;
            double a = 0.32 + (p.Power - 60) / 150.0;
            switch (inn.Phase)
            {
                case 0: a += 0.1; break;
                case 2: a += 0.32; break;
            }
            if (inn.Target != null && inn.BallsLeft > 0)
            {
                double rrr = inn.RunsNeeded * 6.0 / inn.BallsLeft;
                a += MathX.Clamp((rrr - 8.0) / 9.0, -0.25, 0.5);
            }
            int wkLeft = inn.MaxWickets - inn.Wkts;
            if (wkLeft <= 3 && inn.BallsLeft > 12) a -= 0.18;
            if (card.Balls > 15) a += 0.06;
            if (card.Balls < 3) a -= 0.08;
            if (inn.SuperOver) a += 0.4;
            return MathX.Clamp(a, 0.05, 1.0);
        }

        /// <summary>
        /// Chooses the AI batter's response to a delivery. <paramref name="skillScale"/> > 1 makes the
        /// batter's timing worse (easier for a human bowler).
        /// </summary>
        public static ShotInput Choose(DeliveryTrack tr, Innings inn, BatCard card, Rng rng, double skillScale = 1.0)
        {
            var p = card.P;
            double agg = Aggression(inn, card);
            var len = tr.LengthClass;
            double line = tr.LineAtBat;

            // Leave wide ones outside off unless going hard.
            if (line > 0.55 && len != LengthClass.FullToss && !tr.Wide && rng.Chance(0.5 - agg * 0.45)) return ShotInput.Leave;
            if (tr.Wide && line > 0.9 && rng.Chance(0.7)) return ShotInput.Leave;
            if (len == LengthClass.Bouncer && tr.HeightAtBat > 1.75 && rng.Chance(0.55 - agg * 0.4)) return ShotInput.Leave;

            double wDef = 0.16 * (1 - agg) + tr.Quality * 0.14 + (len == LengthClass.Yorker ? 0.25 : 0);
            double wGround = 0.62;
            double wLoft = agg * 0.55 + (len == LengthClass.FullToss ? 0.4 : 0) + (p.Power - 65) / 150.0;
            if (len == LengthClass.Good) wLoft *= 0.65;
            if (len == LengthClass.Yorker) wLoft *= 0.4;
            if (inn.Target != null && inn.RunsNeeded <= 1) wLoft *= 0.3;
            int k = rng.Weighted(new[] { Math.Max(0.02, wDef), wGround, Math.Max(0.02, wLoft) });
            var kind = k == 0 ? ShotKind.Defend : k == 1 ? ShotKind.Ground : ShotKind.Loft;

            // Direction: lean towards the shots that suit the ball.
            double az = 0;
            if (kind != ShotKind.Defend)
            {
                var weights = new double[Centers.Length];
                for (int i = 0; i < Centers.Length; i++)
                {
                    double s = Batting.Suitability(tr, kind, Centers[i]);
                    weights[i] = Math.Exp(s / 0.22);
                    if (Centers[i] > 120 || Centers[i] < -120) weights[i] *= kind == ShotKind.Loft ? 0.25 : 0.5;
                }
                az = Centers[rng.Weighted(weights)] + rng.Gauss() * 12;
            }

            // Timing error.
            double sigma = 72 - (p.Bat - 60) * 0.7;
            sigma *= 1 + tr.Quality * 0.45;
            if (card.Balls < 4) sigma += 14;
            if (tr.BowlType.IsSpin() && tr.Kind == DeliveryKind.Wrongun) sigma += 18;
            sigma = Math.Max(22, sigma) * skillScale;
            double bias = 0;
            if (tr.Kind == DeliveryKind.Slower && rng.Chance(0.62 - p.Bat / 230.0)) bias = -rng.Range(55, 110);
            if (tr.Kind == DeliveryKind.ArmBall && rng.Chance(0.4)) bias = rng.Range(25, 60);
            double timing = bias + rng.Gauss() * sigma;
            bool charged = tr.BowlType.IsSpin() && kind == ShotKind.Loft && rng.Chance(0.35);
            bool soft = kind == ShotKind.Ground && rng.Chance(0.6 * (1 - agg));
            return new ShotInput { Kind = kind, Azimuth = az, TimingMs = timing, Charged = charged, Soft = soft };
        }
    }
}
