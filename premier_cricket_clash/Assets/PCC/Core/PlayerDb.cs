using System;
using System.Collections.Generic;

namespace PCC.Core
{
    /// <summary>
    /// Fictional player database: every name is generated from first-name and
    /// surname pools, so no real cricketer's name or likeness is used. The
    /// database is deterministic - the same squads every launch.
    /// </summary>
    public sealed class PlayerDb
    {
        /// <summary>Squad size of the generated (fictional) database.</summary>
        public const int SquadSize = 22;
        const int Seed = 2027;

        /// <summary>
        /// True: real player names and squads (RealPlayers.cs). False: the fully fictional generated
        /// database (Play-safe). Must be set before <see cref="Instance"/> is first used.
        /// </summary>
        public static bool UseRealNames = true;

        static PlayerDb _instance;
        public static PlayerDb Instance => _instance ??= new PlayerDb();

        public const int FreeAgentCount = 70;

        readonly List<Player> _all = new List<Player>();
        readonly List<Player> _freeAgents = new List<Player>();
        readonly Dictionary<int, Player> _byId = new Dictionary<int, Player>();
        readonly Dictionary<string, List<Player>> _squads = new Dictionary<string, List<Player>>();

        /// <summary>Players contracted to the ten franchises.</summary>
        public IReadOnlyList<Player> All => _all;

        /// <summary>Uncontracted players who only enter the auction pool.</summary>
        public IReadOnlyList<Player> FreeAgents => _freeAgents;
        public Player ById(int id) => _byId[id];
        public IReadOnlyList<Player> Squad(string teamId) => _squads[teamId];

        // ------------------------------------------------------------ names

        static readonly string[] IndFirst =
        {
            "Aarav", "Vihaan", "Advait", "Kabir", "Reyansh", "Arnav", "Ishaan", "Shaurya", "Atharv", "Ayaan", "Krish", "Parth",
            "Rudra", "Veer", "Nikhil", "Tanmay", "Pranav", "Harsh", "Kunal", "Mihir", "Raghav", "Samar", "Tejas", "Uday", "Zaid",
            "Abhay", "Chirag", "Devansh", "Eshan", "Gautam", "Jatin", "Kartik", "Lakshya", "Manav", "Naman", "Ojas", "Pratik",
            "Rohan", "Sahil", "Tarun", "Utkarsh", "Vedant", "Aniket", "Bhavesh", "Darsh", "Faiz", "Hemant", "Imran", "Jay",
            "Kiran", "Mohit", "Neeraj", "Omkar", "Prem", "Sameer", "Tushar", "Vikas", "Aditya", "Siddharth", "Dev", "Nakul", "Anmol",
        };

        static readonly string[] IndLast =
        {
            "Mehra", "Khanna", "Bhatt", "Joshi", "Kulkarni", "Deshmukh", "Pillai", "Nair", "Menon", "Rao", "Naidu", "Chauhan",
            "Rathore", "Shekhawat", "Bose", "Sen", "Banerjee", "Mukherjee", "Chopra", "Malhotra", "Saxena", "Tripathi", "Mishra",
            "Sandhu", "Brar", "Dhillon", "Grewal", "Bajwa", "Arora", "Ahuja", "Bedi", "Qureshi", "Siddiqui", "Shaikh", "Kadam",
            "Pawar", "Jadhav", "Shetty", "Hegde", "Kamath", "Patil", "Gowda", "Bhardwaj", "Rawat", "Negi", "Bisht", "Rajput",
            "Solanki", "Desai", "Trivedi", "Vyas", "Agarwal", "Goyal", "Bansal", "Sethi", "Oberoi", "Talwar",
        };

        static readonly (string Code, string[] First, string[] Last)[] Overseas =
        {
            ("AUS", new[] { "Liam", "Jack", "Cooper", "Riley", "Hayden", "Blake", "Lachlan", "Brodie", "Tate", "Jarrod", "Callum", "Darcy" },
                new[] { "Whitfield", "Mercer", "Thornton", "Gallagher", "Hollis", "Prentice", "Ashby", "Calloway", "Draper", "Eldridge" }),
            ("ENG", new[] { "Oliver", "Harry", "Freddie", "Alfie", "Toby", "George", "Archie", "Rory", "Theo", "Hugo" },
                new[] { "Ashworth", "Pemberton", "Hargreaves", "Blackwood", "Fairbairn", "Langley", "Whitmore", "Castleton", "Rowntree", "Kingsley" }),
            ("SA", new[] { "Ruan", "Wian", "Thabo", "Sipho", "Pieter", "Jaco", "Andile", "Riaan", "Bongani", "Henko" },
                new[] { "Mokoena", "Nkosi", "Venter", "Kruger", "Mabaso", "Ferreira", "Swanepoel", "Steenkamp", "Dlamini", "Visagie" }),
            ("NZ", new[] { "Hamish", "Finn", "Logan", "Taine", "Rhys", "Blair", "Ethan", "Nikau", "Arlo", "Beau" },
                new[] { "Fairweather", "Ngata", "McKenzie", "Rowe", "Pateman", "Kingi", "Holloway", "Carver", "Tamihana", "Rennie" }),
            ("WI", new[] { "Keon", "Jaden", "Akeem", "Tevin", "Devon", "Shamari", "Kadeem", "Rashard", "Jovan", "Marlon" },
                new[] { "Baptiste", "Alleyne", "Bascombe", "Fredericks", "Thorne", "Lashley", "Gooding", "Mapp", "Springer", "Callender" }),
            ("AFG", new[] { "Zubair", "Hamid", "Farid", "Naveed", "Sami", "Habib", "Wahid", "Javed", "Rahim", "Bilal" },
                new[] { "Safi", "Ahmadzai", "Durrani", "Wardak", "Popalzai", "Kakar", "Hotak", "Karimi", "Noori", "Sherzad" }),
            ("SL", new[] { "Kasun", "Kavindu", "Sahan", "Nuwan", "Ravindu", "Tharindu", "Malith", "Dulith", "Yasiru", "Sachith" },
                new[] { "Weerasinghe", "Gunawardena", "Wickramasinghe", "Abeysekera", "Senanayake", "Dissanayake", "Wijeratne", "Kodikara", "Ranaweera", "Amarasekara" }),
        };

        // ------------------------------------------------------------ build

        PlayerDb()
        {
            if (UseRealNames)
            {
                BuildReal();
                return;
            }
            var rng = new Rng(Seed);
            var used = new HashSet<string>();
            int nextId = 1;
            for (int t = 0; t < Teams.All.Count; t++)
            {
                var team = Teams.All[t];
                // Small team-to-team quality spread so matches feel different.
                double teamBoost = (t * 37 % 10 - 4.5) * 0.45;
                var squad = BuildSquad(rng, used, ref nextId, teamBoost);
                _squads[team.Id] = squad;
                foreach (var p in squad)
                {
                    _all.Add(p);
                    _byId[p.Id] = p;
                }
            }

            // Free agents for the auction, from their own stream so the squads above never change.
            var fa = new Rng(Seed + 101);
            for (int i = 0; i < FreeAgentCount; i++)
            {
                var slot = Template[i % Template.Length];
                double q = -5 + fa.Gauss() * 4;
                bool os = fa.Chance(0.25);
                string country = "IND", name;
                if (os)
                {
                    var pool = Overseas[fa.NextInt(Overseas.Length)];
                    country = pool.Code;
                    name = UniqueName(fa, used, pool.First, pool.Last);
                }
                else
                {
                    name = UniqueName(fa, used, IndFirst, IndLast);
                }
                var p = MakePlayer(fa, nextId++, name, country, slot, q, fa.RangeInt(1, 99));
                _freeAgents.Add(p);
                _byId[p.Id] = p;
            }
        }

        /// <summary>Real squads: each 2026 team goes to the franchise of the same city; unsigned players are free agents.</summary>
        void BuildReal()
        {
            var rng = new Rng(Seed);
            foreach (var t in Teams.All) _squads[t.Id] = new List<Player>();
            var jerseys = new Dictionary<string, List<int>>();
            int nextId = 1;
            foreach (var row in RealPlayers.Rows)
            {
                var v = row.Split('|');
                string team = RealPlayers.FranchiseFor(v[9]);
                string key = team ?? "-";
                if (!jerseys.TryGetValue(key, out var pool))
                {
                    pool = new List<int>();
                    for (int j = 1; j <= 99; j++) pool.Add(j);
                    rng.Shuffle(pool);
                    jerseys[key] = pool;
                }
                int jersey = pool[0];
                pool.RemoveAt(0);
                var role = v[2] == "WK" ? Role.Wk : v[2] == "BAT" ? Role.Bat : v[2] == "AR" ? Role.Ar : Role.Bowl;
                var style = v[4] == "-" ? BowlStyle.None : (BowlStyle)Enum.Parse(typeof(BowlStyle), v[4]);
                var p = new Player(nextId++, v[0], v[1], role, v[3] == "L", style, int.Parse(v[5]), int.Parse(v[6]), int.Parse(v[7]), jersey, v[8] == "C");
                _byId[p.Id] = p;
                if (team != null)
                {
                    _squads[team].Add(p);
                    _all.Add(p);
                }
                else
                {
                    _freeAgents.Add(p);
                }
            }
        }

        enum Slot { Wk, Bat, BatAr, BowlArPace, BowlArSpin, Pace, Spin }

        static readonly Slot[] Template =
        {
            Slot.Wk, Slot.Wk, Slot.Bat, Slot.Bat, Slot.Bat, Slot.Bat, Slot.Bat, Slot.Bat,
            Slot.BatAr, Slot.BatAr, Slot.BowlArPace, Slot.BowlArPace, Slot.BowlArSpin,
            Slot.Pace, Slot.Pace, Slot.Pace, Slot.Pace, Slot.Pace, Slot.Spin, Slot.Spin, Slot.Spin, Slot.Spin,
        };

        List<Player> BuildSquad(Rng rng, HashSet<string> used, ref int nextId, double teamBoost)
        {
            // Seven overseas players, spread over the roles.
            var overseasSlots = new HashSet<int> { 1, 2, 4, 8, 10, 13, 18 };
            var jerseys = new List<int>();
            for (int j = 1; j <= 99; j++) jerseys.Add(j);
            rng.Shuffle(jerseys);

            var squad = new List<Player>();
            var seenSlot = new Dictionary<Slot, int>();
            for (int i = 0; i < Template.Length; i++)
            {
                var slot = Template[i];
                seenSlot.TryGetValue(slot, out int rank);
                seenSlot[slot] = rank + 1;
                // The first player of each slot type is the star, later ones are depth.
                double q = teamBoost + (rank == 0 ? 6 : rank == 1 ? 2 : -2 - rank) + rng.Gauss() * 3;
                bool os = overseasSlots.Contains(i);
                string country = "IND";
                string name;
                if (os)
                {
                    var pool = Overseas[rng.NextInt(Overseas.Length)];
                    country = pool.Code;
                    name = UniqueName(rng, used, pool.First, pool.Last);
                }
                else
                {
                    name = UniqueName(rng, used, IndFirst, IndLast);
                }
                squad.Add(MakePlayer(rng, nextId++, name, country, slot, q, jerseys[i]));
            }
            return squad;
        }

        static string UniqueName(Rng rng, HashSet<string> used, string[] first, string[] last)
        {
            for (int guard = 0; guard < 500; guard++)
            {
                string n = rng.Pick(first) + " " + rng.Pick(last);
                if (used.Add(n)) return n;
            }
            throw new InvalidOperationException("Name pool exhausted");
        }

        static int R(double v) => Math.Clamp((int)Math.Round(v), 5, 95);

        static Player MakePlayer(Rng rng, int id, string name, string country, Slot slot, double q, int jersey)
        {
            bool left = rng.Chance(0.28);
            BowlStyle Pace() => rng.Weighted(
                new[] { BowlStyle.RF, BowlStyle.RFM, BowlStyle.RM, BowlStyle.LF, BowlStyle.LFM, BowlStyle.LM },
                new[] { 0.28, 0.3, 0.08, 0.12, 0.16, 0.06 });
            BowlStyle Spin() => rng.Weighted(
                new[] { BowlStyle.OB, BowlStyle.LB, BowlStyle.SLA, BowlStyle.LWS },
                new[] { 0.32, 0.3, 0.3, 0.08 });

            switch (slot)
            {
                case Slot.Wk:
                    return new Player(id, name, country, Role.Wk, left, BowlStyle.None, R(78 + q), 0, R(80 + q + rng.Gauss() * 6), jersey);
                case Slot.Bat:
                {
                    var part = rng.Chance(0.25) ? (rng.Chance(0.5) ? BowlStyle.OB : BowlStyle.LB) : BowlStyle.None;
                    int bowl = part == BowlStyle.None ? R(5) : R(32 + rng.Gauss() * 8);
                    return new Player(id, name, country, Role.Bat, left, part, R(80 + q), bowl, R(80 + q * 0.5 + rng.Gauss() * 7), jersey);
                }
                case Slot.BatAr:
                {
                    var style = rng.Chance(0.5) ? Pace() : Spin();
                    return new Player(id, name, country, Role.Ar, left, style, R(76 + q), R(66 + q * 0.6), R(84 + rng.Gauss() * 5), jersey);
                }
                case Slot.BowlArPace:
                    return new Player(id, name, country, Role.Ar, left, Pace(), R(62 + q * 0.5), R(76 + q), R(72 + rng.Gauss() * 8), jersey);
                case Slot.BowlArSpin:
                    return new Player(id, name, country, Role.Ar, left, Spin(), R(64 + q * 0.5), R(76 + q), R(66 + rng.Gauss() * 8), jersey);
                case Slot.Pace:
                    return new Player(id, name, country, Role.Bowl, rng.Chance(0.2), Pace(), R(22 + rng.Gauss() * 9), R(79 + q), R(30 + rng.Gauss() * 8), jersey);
                default:
                    return new Player(id, name, country, Role.Bowl, rng.Chance(0.2), Spin(), R(22 + rng.Gauss() * 9), R(78 + q), R(28 + rng.Gauss() * 8), jersey);
            }
        }
    }
}
