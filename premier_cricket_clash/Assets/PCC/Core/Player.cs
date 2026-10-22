using System;

namespace PCC.Core
{
    public enum Role { Bat, Wk, Ar, Bowl }

    public enum BowlStyle { None, RF, RFM, RM, LF, LFM, LM, OB, LB, SLA, LWS }

    public static class RoleX
    {
        static readonly string[] Codes = { "BAT", "WK", "AR", "BOWL" };
        static readonly string[] Labels = { "Batter", "Wicketkeeper", "All-rounder", "Bowler" };
        public static string Code(this Role r) => Codes[(int)r];
        public static string Label(this Role r) => Labels[(int)r];
    }

    public static class BowlStyleX
    {
        static readonly string[] Codes = { "-", "RF", "RFM", "RM", "LF", "LFM", "LM", "OB", "LB", "SLA", "LWS" };
        static readonly string[] Labels =
        {
            "-", "Right-arm fast", "Right-arm fast-medium", "Right-arm medium", "Left-arm fast", "Left-arm fast-medium",
            "Left-arm medium", "Right-arm off-break", "Right-arm leg-break", "Left-arm orthodox", "Left-arm wrist-spin",
        };
        static readonly double[] Speeds = { 0, 145, 138, 129, 143, 136, 127, 88, 86, 90, 85 };

        public static bool IsPace(this BowlStyle s) => (int)s >= 1 && (int)s <= 6;
        public static bool IsSpin(this BowlStyle s) => (int)s >= 7;
        public static bool LeftArm(this BowlStyle s) =>
            s == BowlStyle.LF || s == BowlStyle.LFM || s == BowlStyle.LM || s == BowlStyle.SLA || s == BowlStyle.LWS;
        public static string Code(this BowlStyle s) => Codes[(int)s];
        public static string Label(this BowlStyle s) => Labels[(int)s];

        /// <summary>Typical release speed in km/h.</summary>
        public static double BaseSpeed(this BowlStyle s) => Speeds[(int)s];

        /// <summary>Direction of the stock ball's turn in world x (+x = right-hander's leg side).</summary>
        public static double StockTurn(this BowlStyle s)
        {
            switch (s)
            {
                case BowlStyle.OB:
                case BowlStyle.LWS: return 1;
                case BowlStyle.LB:
                case BowlStyle.SLA: return -1;
                default: return 0;
            }
        }
    }

    public sealed class Player
    {
        public Player(int id, string name, string country, Role role, bool leftBat, BowlStyle bowlStyle, int bat, int bowl, int power, int jersey, bool? capped = null)
        {
            Id = id;
            CappedFlag = capped;
            OriginalName = name;
            Country = country;
            Role = role;
            LeftBat = leftBat;
            BowlStyle = bowlStyle;
            Bat = bat;
            Bowl = bowl;
            Power = power;
            Jersey = jersey;
        }

        public int Id { get; }

        /// <summary>The name the game ships with (before any EDIT NAMES rename).</summary>
        public string OriginalName { get; }

        /// <summary>International (capped) status from the data, or null to derive it from the rating.</summary>
        public bool? CappedFlag { get; }

        /// <summary>
        /// User renames (the EDIT NAMES screen): returns a custom name for a player id, or null.
        /// Set by the game layer; everything that shows a name goes through <see cref="Name"/>.
        /// </summary>
        public static Func<int, string> NameOverride;

        public string Name => NameOverride?.Invoke(Id) ?? OriginalName;
        public string Country { get; }
        public Role Role { get; }
        public bool LeftBat { get; }
        public BowlStyle BowlStyle { get; }

        /// <summary>Batting skill (T20), 0-99.</summary>
        public int Bat { get; }

        /// <summary>Bowling skill (T20), 0-99.</summary>
        public int Bowl { get; }

        /// <summary>Hitting power / intent, 0-99.</summary>
        public int Power { get; }
        public int Jersey { get; }

        public bool Overseas => Country != "IND";
        public bool IsPace => BowlStyle.IsPace();
        public bool IsSpin => BowlStyle.IsSpin();
        public bool IsKeeper => Role == Role.Wk;
        public bool IsBowlingOption => BowlStyle != BowlStyle.None && Bowl >= 58;

        public int Overall
        {
            get
            {
                double b = Bat, w = Bowl, p = Power, v;
                switch (Role)
                {
                    case Role.Bat: v = b * 0.88 + p * 0.12; break;
                    case Role.Wk: v = b * 0.88 + p * 0.12 + 1; break;
                    case Role.Bowl: v = w * 0.96 + b * 0.05; break;
                    default:
                        double hi = Math.Max(b, w), lo = Math.Min(b, w);
                        v = Math.Max(hi * 0.97, hi * 0.72 + lo * 0.33);
                        if (b >= w) v += (p - 75) * 0.05;
                        break;
                }
                return Math.Clamp((int)Math.Round(v), 30, 99);
            }
        }

        public string Tier
        {
            get
            {
                int o = Overall;
                if (o >= 88) return "ELITE";
                if (o >= 80) return "GOLD";
                if (o >= 72) return "SILVER";
                return "BRONZE";
            }
        }

        /// <summary>"A Mehra" style short name.</summary>
        public string ShortName
        {
            get
            {
                int sp = Name.IndexOf(' ');
                return sp <= 0 ? Name : Name[0] + Name.Substring(sp);
            }
        }

        public string Surname
        {
            get
            {
                int sp = Name.LastIndexOf(' ');
                return (sp < 0 ? Name : Name.Substring(sp + 1)).ToUpperInvariant();
            }
        }

        public string StyleLine
        {
            get
            {
                string batting = LeftBat ? "Left-hand bat" : "Right-hand bat";
                return BowlStyle == BowlStyle.None ? batting : batting + " • " + BowlStyle.Label();
            }
        }

        public override string ToString() => Name;
    }
}
