using System;
using System.Collections.Generic;

namespace PCC.Core
{
    /// <summary>
    /// Franchise of the fictional Premier Cricket Clash league. Names, colours
    /// and venues are original - no real league, club or stadium branding.
    /// </summary>
    public sealed class TeamInfo
    {
        public TeamInfo(string id, string name, string city, string venue, string venueShort, uint primary, uint secondary, uint glow,
            int titles, double batFactor, double spinFactor, int boundary)
        {
            Id = id;
            Name = name;
            City = city;
            Venue = venue;
            VenueShort = venueShort;
            Primary = primary;
            Secondary = secondary;
            Glow = glow;
            Titles = titles;
            BatFactor = batFactor;
            SpinFactor = spinFactor;
            Boundary = boundary;
        }

        public string Id { get; }
        public string Name { get; }
        public string City { get; }
        public string Venue { get; }
        public string VenueShort { get; }

        /// <summary>0xAARRGGBB colours.</summary>
        public uint Primary { get; }
        public uint Secondary { get; }
        public uint Glow { get; }
        public int Titles { get; }

        /// <summary>Venue character: >1 favours batting; spinFactor >1 helps spinners.</summary>
        public double BatFactor { get; }
        public double SpinFactor { get; }

        /// <summary>Average boundary distance in metres.</summary>
        public int Boundary { get; }

        public override string ToString() => Name;
    }

    public static class Teams
    {
        public static readonly IReadOnlyList<TeamInfo> All = new[]
        {
            new TeamInfo("CHC", "Chennai Chargers", "Chennai", "Marina Bay Cricket Ground", "Marina Bay", 0xFFF7C600, 0xFF1546A0, 0xFFFFD84A, 4, 0.95, 1.14, 66),
            new TeamInfo("MUM", "Mumbai Mavericks", "Mumbai", "Arabian Sea Arena", "Sea Arena", 0xFF1B5FC9, 0xFFD9A62E, 0xFF4FA3FF, 5, 1.08, 0.95, 64),
            new TeamInfo("BLZ", "Bengaluru Blazers", "Bengaluru", "Garden City Oval", "Garden Oval", 0xFFC8102E, 0xFF1A1A1A, 0xFFFF4D5E, 2, 1.18, 0.9, 62),
            new TeamInfo("KOL", "Kolkata Thunder", "Kolkata", "Hooghly Riverside Stadium", "Riverside", 0xFF46257A, 0xFFE0B23C, 0xFFA77BFF, 3, 1.0, 1.08, 67),
            new TeamInfo("HYD", "Hyderabad Hurricanes", "Hyderabad", "Deccan Plateau Stadium", "Deccan", 0xFFF26A1B, 0xFF151515, 0xFFFF9A4D, 1, 1.12, 0.96, 68),
            new TeamInfo("JAI", "Jaipur Jaguars", "Jaipur", "Pink City Cricket Park", "Pink City", 0xFFE5007D, 0xFF213C8F, 0xFFFF5CB8, 1, 1.0, 1.05, 69),
            new TeamInfo("DEL", "Delhi Defenders", "Delhi", "Yamuna Capital Ground", "Capital Ground", 0xFF0F3F96, 0xFFE2231A, 0xFF5A8DFF, 0, 1.12, 1.0, 63),
            new TeamInfo("PUN", "Punjab Panthers", "Mohali", "Five Rivers Stadium", "Five Rivers", 0xFFD71920, 0xFFC0C6CE, 0xFFFF5A5F, 0, 1.06, 0.94, 66),
            new TeamInfo("AHM", "Ahmedabad Arrows", "Ahmedabad", "Sabarmati Super Stadium", "Sabarmati", 0xFF17223F, 0xFFC9A54B, 0xFF7E9BFF, 1, 1.02, 1.0, 72),
            new TeamInfo("LKO", "Lucknow Lions", "Lucknow", "Nawab Heritage Stadium", "Nawab Park", 0xFF00A3E0, 0xFFF47920, 0xFF4FD3FF, 0, 0.94, 1.12, 70),
        };

        public static TeamInfo ById(string id)
        {
            foreach (var t in All)
            {
                if (t.Id == id) return t;
            }
            throw new ArgumentException("Unknown team " + id);
        }

        public static int IndexOf(TeamInfo t)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == t.Id) return i;
            }
            return -1;
        }
    }

    public static class League
    {
        public const string Name = "PREMIER CRICKET CLASH";
        public const string ShortName = "PCC";
        public const int Year = 2027;
        public const int T20Overs = 20;
        public const int MaxOverseasXI = 4;
    }
}
