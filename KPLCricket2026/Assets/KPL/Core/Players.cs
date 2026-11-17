using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    public enum Role { Batter, Keeper, AllRounder, Bowler }

    public enum BowlType { None, Fast, FastMedium, Swing, OffSpin, LegSpin, LeftArmSpin }

    public static class BowlTypeX
    {
        public static bool IsPace(this BowlType t) => t == BowlType.Fast || t == BowlType.FastMedium || t == BowlType.Swing;
        public static bool IsSpin(this BowlType t) => t == BowlType.OffSpin || t == BowlType.LegSpin || t == BowlType.LeftArmSpin;

        public static string Label(this BowlType t)
        {
            switch (t)
            {
                case BowlType.Fast: return "Fast";
                case BowlType.FastMedium: return "Fast-medium";
                case BowlType.Swing: return "Swing";
                case BowlType.OffSpin: return "Off-spin";
                case BowlType.LegSpin: return "Leg-spin";
                case BowlType.LeftArmSpin: return "Left-arm spin";
                default: return "-";
            }
        }
    }

    public static class RoleX
    {
        public static string Label(this Role r)
        {
            switch (r)
            {
                case Role.Keeper: return "WK-Batter";
                case Role.AllRounder: return "All-rounder";
                case Role.Bowler: return "Bowler";
                default: return "Batter";
            }
        }
    }

    /// <summary>A fictional cricketer. Ratings run 1-99.</summary>
    [Serializable]
    public sealed class Player
    {
        public int Id;
        public string First, Last, TeamId, Country;
        public Role Role;
        public BowlType Bowl;
        public bool LeftBat, LeftArm, Overseas;

        /// <summary>Technique and timing.</summary>
        public int Bat;
        /// <summary>Hitting power.</summary>
        public int Power;
        public int BowlSkill;
        public int Field;
        /// <summary>Running speed between the wickets and in the field.</summary>
        public int Pace;
        public int Age, Jersey, Skin, Hair, Build;
        /// <summary>Opener, top order, middle, finisher, tail (1-5), used for default batting orders.</summary>
        public int Slot;

        public string Name => First + " " + Last;
        public string Short => First.Substring(0, 1) + " " + Last;
        public bool IsPace => Bowl.IsPace();
        public bool IsSpin => Bowl.IsSpin();
        public bool CanBowl => Bowl != BowlType.None;
        public bool IsKeeper => Role == Role.Keeper;

        public int Overall
        {
            get
            {
                switch (Role)
                {
                    case Role.Batter: return (int)Math.Round(Bat * 0.62 + Power * 0.28 + Field * 0.1);
                    case Role.Keeper: return (int)Math.Round(Bat * 0.55 + Power * 0.25 + Field * 0.2);
                    case Role.Bowler: return (int)Math.Round(BowlSkill * 0.85 + Field * 0.1 + Bat * 0.05);
                    default: return (int)Math.Round(Math.Max(Bat, BowlSkill) * 0.5 + Math.Min(Bat, BowlSkill) * 0.35 + Power * 0.1 + Field * 0.05);
                }
            }
        }

        public Player Clone() => (Player)MemberwiseClone();
    }

    /// <summary>Generates the fixed, fictional player database (same every launch).</summary>
    public static class PlayerDb
    {
        static readonly string[] IndFirst =
        {
            "Aarav", "Aditya", "Akhil", "Amogh", "Anirban", "Arnav", "Ayaan", "Chaitanya", "Darshan", "Devansh", "Dhruv", "Eshan", "Gaurang", "Girish",
            "Harsh", "Hrithik", "Jatin", "Kabir", "Karthik", "Keshav", "Kunal", "Lakshay", "Madhav", "Manav", "Mihir", "Naman", "Neel", "Nikhil", "Omkar",
            "Parth", "Pranav", "Pratik", "Raghav", "Rajat", "Ritvik", "Rohan", "Rudra", "Sahil", "Samar", "Shaurya", "Siddhant", "Soham", "Tanmay",
            "Tejas", "Uday", "Utkarsh", "Vedant", "Viraj", "Vivaan", "Yuvan", "Zoravar", "Anshul", "Bhavin", "Chirag", "Dev", "Farhan", "Gurnoor",
            "Harjot", "Jaiveer", "Kanav", "Mayur", "Nirav", "Ojas", "Pranit", "Reyansh", "Sarthak", "Tarun", "Vikrant", "Yatin", "Arvind", "Balraj",
        };

        static readonly string[] IndLast =
        {
            "Acharya", "Bhandari", "Bhatnagar", "Bhosale", "Chandran", "Deshmukh", "Dhaliwal", "Gaonkar", "Ghatak", "Gokhale", "Hegde", "Inamdar",
            "Jaiswar", "Joglekar", "Kakade", "Kamath", "Kanwar", "Kapadia", "Karnik", "Khatri", "Kulkarni", "Lalwani", "Mahajan", "Malviya", "Menon",
            "Mhatre", "Nadkarni", "Naidu", "Nambiar", "Oberoi", "Padhye", "Panicker", "Parmar", "Pillai", "Purohit", "Rajput", "Ranade", "Rathi",
            "Sabharwal", "Sawant", "Sehgal", "Sengupta", "Shenoy", "Shirke", "Sodhi", "Tandon", "Thapar", "Trivedi", "Upadhyay", "Vaidya", "Venkatraman",
            "Wagle", "Walia", "Zaveri", "Bhattacharya", "Chakraborty", "Dasgupta", "Ganguli", "Majumdar", "Mitra", "Sarkar", "Grewal", "Bajwa", "Sandhu",
        };

        static readonly string[] OvFirst =
        {
            "Callum", "Declan", "Brodie", "Kieran", "Lachlan", "Hamish", "Fergus", "Owen", "Reece", "Tristan", "Ruan", "Pieter", "Jandre", "Wian",
            "Akeem", "Darnell", "Tevin", "Rahmat", "Zubair", "Asitha", "Nuwan", "Corbin", "Dylan", "Ewan", "Finn", "Gideon", "Hayden", "Ivo",
            "Jarrod", "Kobus", "Lewin", "Morne", "Nathaniel", "Oakley", "Preston", "Quade", "Rory", "Stellan", "Thabo", "Wade",
        };

        static readonly (string Name, string Country)[] OvLast =
        {
            ("Hartley", "ENG"), ("Pemberton", "ENG"), ("Ashworth", "ENG"), ("Calloway", "ENG"), ("Whitlock", "ENG"), ("Redmond", "ENG"),
            ("Fairbairn", "AUS"), ("Langford", "AUS"), ("McAllister", "AUS"), ("Thornton", "AUS"), ("Kingsley", "AUS"), ("Dunmore", "AUS"),
            ("Van der Berg", "RSA"), ("Kruger", "RSA"), ("Joubert", "RSA"), ("Visagie", "RSA"), ("Swanepoel", "RSA"),
            ("Alleyne", "WI"), ("Boyce", "WI"), ("Baptiste", "WI"), ("Greenidge-Lowe", "WI"), ("Mottley", "WI"),
            ("Hotak", "AFG"), ("Safi", "AFG"), ("Momand", "AFG"),
            ("Wijeratne", "SL"), ("Amarasinghe", "SL"), ("Liyanage", "SL"),
            ("Fairweather", "NZ"), ("McCrae", "NZ"), ("Tane", "NZ"), ("Halloran", "NZ"),
        };

        static List<Player> _all;
        static Dictionary<int, Player> _byId;

        public static IReadOnlyList<Player> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static Player ById(int id)
        {
            if (_byId == null) Build();
            return _byId.TryGetValue(id, out var p) ? p : null;
        }

        public static List<Player> Squad(string teamId) => All.Where(p => p.TeamId == teamId).ToList();

        // Role template for a 16-player squad: role, bowl type, slot, overseas.
        static readonly (Role Role, BowlType Bowl, int Slot, bool Ov)[] Template =
        {
            (Role.Batter, BowlType.None, 1, false),
            (Role.Batter, BowlType.None, 1, true),
            (Role.Batter, BowlType.None, 2, false),
            (Role.Batter, BowlType.OffSpin, 3, false),
            (Role.Batter, BowlType.None, 3, true),
            (Role.Keeper, BowlType.None, 4, false),
            (Role.Keeper, BowlType.None, 2, false),
            (Role.AllRounder, BowlType.FastMedium, 4, true),
            (Role.AllRounder, BowlType.LeftArmSpin, 4, false),
            (Role.AllRounder, BowlType.LegSpin, 4, false),
            (Role.Bowler, BowlType.Fast, 5, true),
            (Role.Bowler, BowlType.Fast, 5, false),
            (Role.Bowler, BowlType.Swing, 5, false),
            (Role.Bowler, BowlType.FastMedium, 5, false),
            (Role.Bowler, BowlType.LegSpin, 5, true),
            (Role.Bowler, BowlType.OffSpin, 5, false),
        };

        static void Build()
        {
            _all = new List<Player>();
            _byId = new Dictionary<int, Player>();
            var used = new HashSet<string>();
            int id = 1000;
            for (int ti = 0; ti < Teams.All.Count; ti++)
            {
                var team = Teams.All[ti];
                var rng = new Rng(7919 * (ti + 3) + 2026);
                // Two star players per team, with team strength varying slightly.
                double teamBias = (rng.Next() - 0.5) * 4;
                int starA = rng.Int(Template.Length), starB = (starA + 5 + rng.Int(9)) % Template.Length;
                var jerseys = new HashSet<int>();
                for (int k = 0; k < Template.Length; k++)
                {
                    var (role, bowl, slot, ov) = Template[k];
                    var p = new Player { Id = id++, TeamId = team.Id, Role = role, Bowl = bowl, Slot = slot, Overseas = ov };
                    for (int tries = 0; tries < 200; tries++)
                    {
                        if (ov)
                        {
                            var (last, country) = OvLast[rng.Int(OvLast.Length)];
                            p.First = OvFirst[rng.Int(OvFirst.Length)];
                            p.Last = last;
                            p.Country = country;
                        }
                        else
                        {
                            p.First = IndFirst[rng.Int(IndFirst.Length)];
                            p.Last = IndLast[rng.Int(IndLast.Length)];
                            p.Country = "IND";
                        }
                        if (!used.Contains(p.Name) && !used.Contains(p.Last + "#" + team.Id)) break;
                    }
                    used.Add(p.Name);
                    used.Add(p.Last + "#" + team.Id);
                    bool star = k == starA || k == starB;
                    double boost = (star ? 8 : 0) + teamBias + (ov ? 3 : 0);
                    int R(double mean, double sd) => (int)MathX.Clamp(Math.Round(mean + rng.Gauss() * sd + boost), 25, 97);
                    switch (role)
                    {
                        case Role.Batter:
                            p.Bat = R(76, 5);
                            p.Power = R(slot == 1 ? 72 : 74, 8);
                            p.BowlSkill = bowl == BowlType.None ? R(28, 6) : R(52, 6);
                            break;
                        case Role.Keeper:
                            p.Bat = R(72, 5);
                            p.Power = R(70, 8);
                            p.BowlSkill = 15;
                            break;
                        case Role.AllRounder:
                            p.Bat = R(66, 6);
                            p.Power = R(74, 8);
                            p.BowlSkill = R(70, 5);
                            break;
                        default:
                            p.Bat = R(36, 8);
                            p.Power = R(48, 10);
                            p.BowlSkill = R(77, 5);
                            break;
                    }
                    p.Field = R(role == Role.Keeper ? 80 : 70, 7);
                    p.Pace = R(role == Role.Bowler ? 62 : 70, 9);
                    p.LeftBat = rng.Chance(role == Role.Bowler ? 0.2 : 0.3);
                    p.LeftArm = bowl == BowlType.LeftArmSpin || (bowl.IsPace() && rng.Chance(0.25));
                    p.Age = rng.Int(20, 36);
                    int j;
                    do j = rng.Chance(0.5) ? rng.Int(1, 34) : rng.Int(1, 100);
                    while (!jerseys.Add(j));
                    p.Jersey = j;
                    p.Skin = ov ? (p.Country == "WI" ? 5 : p.Country == "AFG" || p.Country == "SL" ? rng.Int(2, 5) : rng.Int(0, 2)) : rng.Int(2, 5);
                    p.Hair = rng.Int(4);
                    p.Build = rng.Int(3);
                    _all.Add(p);
                    _byId[p.Id] = p;
                }
            }
        }
    }

    /// <summary>A playing XI in batting order, with the keeper and captain chosen.</summary>
    public sealed class TeamSheet
    {
        public TeamDef Team;
        public List<Player> XI;
        public int KeeperId, CaptainId;

        public Player ById(int id)
        {
            foreach (var p in XI)
            {
                if (p.Id == id) return p;
            }
            return null;
        }

        public bool Has(int id) => ById(id) != null;

        public int BattingStrength => (int)Math.Round(XI.Take(7).Average(p => p.Bat * 0.7 + p.Power * 0.3));
        public int BowlingStrength => (int)Math.Round(XI.Where(p => p.CanBowl).OrderByDescending(p => p.BowlSkill).Take(5).DefaultIfEmpty(XI[0]).Average(p => (double)p.BowlSkill));
        public int Overall => (BattingStrength + BowlingStrength) / 2;

        /// <summary>Picks a balanced XI from a squad: 5-6 batters, a keeper, all-rounders and at least five bowling options.</summary>
        public static List<Player> AutoXI(IList<Player> squad)
        {
            var xi = new List<Player>();
            int Overseas() => xi.Count(p => p.Overseas);
            bool Add(Player p)
            {
                if (p == null || xi.Contains(p)) return false;
                if (p.Overseas && Overseas() >= League.MaxOverseasXI) return false;
                xi.Add(p);
                return true;
            }

            var keeper = squad.Where(p => p.IsKeeper).OrderByDescending(p => p.Overall).FirstOrDefault();
            Add(keeper);
            foreach (var p in squad.Where(p => p.Role == Role.Batter).OrderByDescending(p => p.Overall).Take(5)) Add(p);
            foreach (var p in squad.Where(p => p.Role == Role.AllRounder).OrderByDescending(p => p.Overall).Take(2)) Add(p);
            var bowlers = squad.Where(p => p.Role == Role.Bowler).OrderByDescending(p => p.Overall).ToList();
            // At least two pacers and one spinner.
            Add(bowlers.FirstOrDefault(b => b.IsPace));
            Add(bowlers.Where(b => b.IsPace && !xi.Contains(b)).FirstOrDefault(b => !b.Overseas || Overseas() < League.MaxOverseasXI));
            Add(bowlers.FirstOrDefault(b => b.IsSpin && (!b.Overseas || Overseas() < League.MaxOverseasXI)));
            foreach (var p in squad.OrderByDescending(p => p.Overall))
            {
                if (xi.Count >= 11) break;
                Add(p);
            }
            return BattingOrder(xi);
        }

        public static List<Player> BattingOrder(IEnumerable<Player> xi) =>
            xi.OrderBy(p => p.Slot).ThenByDescending(p => p.Bat * 0.7 + p.Power * 0.3).ToList();

        public static TeamSheet Create(TeamDef team, IList<Player> xiInOrder)
        {
            var xi = xiInOrder.ToList();
            var keeper = xi.FirstOrDefault(p => p.IsKeeper) ?? xi.OrderByDescending(p => p.Field).First(p => !p.CanBowl || true);
            var captain = xi.OrderByDescending(p => p.Overall + p.Age * 0.3).First();
            return new TeamSheet { Team = team, XI = xi, KeeperId = keeper.Id, CaptainId = captain.Id };
        }

        /// <summary>Default sheet for a team straight from the database.</summary>
        public static TeamSheet Default(TeamDef team) => Create(team, AutoXI(PlayerDb.Squad(team.Id)));

        /// <summary>Returns a sheet with copies of the players boosted by the given amounts (upgrades, coaches).</summary>
        public TeamSheet Boosted(Func<Player, (int bat, int power, int bowl, int field)> boost)
        {
            var copy = XI.Select(p =>
            {
                var c = p.Clone();
                var (b, pw, bw, f) = boost(p);
                c.Bat = Math.Min(99, c.Bat + b);
                c.Power = Math.Min(99, c.Power + pw);
                c.BowlSkill = Math.Min(99, c.BowlSkill + bw);
                c.Field = Math.Min(99, c.Field + f);
                return c;
            }).ToList();
            return new TeamSheet { Team = Team, XI = copy, KeeperId = KeeperId, CaptainId = CaptainId };
        }
    }
}
