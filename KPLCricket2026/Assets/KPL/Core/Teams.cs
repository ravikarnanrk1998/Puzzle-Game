using System;
using System.Collections.Generic;

namespace KPL.Core
{
    /// <summary>Emblem drawn on crests, flags and kits.</summary>
    public enum Emblem { Crown, Cyclone, Bolt, Comet, Shield, Wing, Lance, Sun, Trident, Crescent }

    /// <summary>
    /// A fictional KPL franchise. Names, colours, crests and grounds are original and are not
    /// based on any real league or club branding.
    /// </summary>
    public sealed class TeamDef
    {
        public string Id, Name, City, Nick;
        public uint Primary, Secondary, Accent;
        public Emblem Emblem;
        public string Ground, GroundShort;

        /// <summary>Boundary radius straight down the ground and square of the wicket, in metres.</summary>
        public double BoundaryStraight, BoundarySquare;

        /// <summary>Pitch character: pace/bounce (1 = neutral) and spin assistance (1 = neutral).</summary>
        public double PitchPace, PitchSpin;

        /// <summary>Stadium style: 0 = open bowl, 1 = cantilever roofs, 2 = twin tier with towers.</summary>
        public int StadiumStyle;
        public int Floodlights;
        public string Founded;
        public int Titles;

        public string Short => Id;
        public override string ToString() => Name;
    }

    public static class Teams
    {
        public static readonly IReadOnlyList<TeamDef> All = new[]
        {
            new TeamDef { Id = "MUM", Name = "Mumbai Monarchs", City = "Mumbai", Nick = "Monarchs", Primary = 0xFF0B3D91, Secondary = 0xFFE8B931, Accent = 0xFF6FA8FF, Emblem = Emblem.Crown,
                Ground = "Harbour Front Stadium", GroundShort = "Harbour Front", BoundaryStraight = 68, BoundarySquare = 63, PitchPace = 1.05, PitchSpin = 0.95, StadiumStyle = 1, Floodlights = 6, Titles = 3 },
            new TeamDef { Id = "CHE", Name = "Chennai Cyclones", City = "Chennai", Nick = "Cyclones", Primary = 0xFFF5C400, Secondary = 0xFF0E2F6B, Accent = 0xFFFFE36B, Emblem = Emblem.Cyclone,
                Ground = "Coromandel Coast Ground", GroundShort = "Coromandel", BoundaryStraight = 71, BoundarySquare = 66, PitchPace = 0.94, PitchSpin = 1.18, StadiumStyle = 0, Floodlights = 4, Titles = 4 },
            new TeamDef { Id = "BEN", Name = "Bengaluru Bolts", City = "Bengaluru", Nick = "Bolts", Primary = 0xFFC8102E, Secondary = 0xFF141414, Accent = 0xFFFF5A6E, Emblem = Emblem.Bolt,
                Ground = "Silicon Garden Oval", GroundShort = "Garden Oval", BoundaryStraight = 64, BoundarySquare = 59, PitchPace = 1.02, PitchSpin = 0.92, StadiumStyle = 2, Floodlights = 4, Titles = 1 },
            new TeamDef { Id = "KOL", Name = "Kolkata Comets", City = "Kolkata", Nick = "Comets", Primary = 0xFF4B2A85, Secondary = 0xFFD9A93A, Accent = 0xFFB18CFF, Emblem = Emblem.Comet,
                Ground = "Eastern Lights Arena", GroundShort = "Eastern Lights", BoundaryStraight = 70, BoundarySquare = 65, PitchPace = 0.98, PitchSpin = 1.1, StadiumStyle = 2, Floodlights = 6, Titles = 2 },
            new TeamDef { Id = "DEL", Name = "Delhi Sentinels", City = "Delhi", Nick = "Sentinels", Primary = 0xFF15338C, Secondary = 0xFFD62828, Accent = 0xFF5D86FF, Emblem = Emblem.Shield,
                Ground = "Capital Fort Ground", GroundShort = "Capital Fort", BoundaryStraight = 66, BoundarySquare = 61, PitchPace = 1.0, PitchSpin = 1.04, StadiumStyle = 1, Floodlights = 4, Titles = 0 },
            new TeamDef { Id = "HYD", Name = "Hyderabad Hawks", City = "Hyderabad", Nick = "Hawks", Primary = 0xFFF2661C, Secondary = 0xFF101010, Accent = 0xFFFF9C57, Emblem = Emblem.Wing,
                Ground = "Pearl City Stadium", GroundShort = "Pearl City", BoundaryStraight = 72, BoundarySquare = 67, PitchPace = 1.08, PitchSpin = 0.98, StadiumStyle = 0, Floodlights = 6, Titles = 2 },
            new TeamDef { Id = "PUN", Name = "Punjab Lancers", City = "Mohali", Nick = "Lancers", Primary = 0xFFD7192A, Secondary = 0xFFC9CED6, Accent = 0xFFFF6470, Emblem = Emblem.Lance,
                Ground = "Golden Fields Ground", GroundShort = "Golden Fields", BoundaryStraight = 69, BoundarySquare = 64, PitchPace = 1.12, PitchSpin = 0.9, StadiumStyle = 1, Floodlights = 4, Titles = 0 },
            new TeamDef { Id = "RAJ", Name = "Rajasthan Rangers", City = "Jaipur", Nick = "Rangers", Primary = 0xFFE4207E, Secondary = 0xFF1D3A8A, Accent = 0xFFFF6FB5, Emblem = Emblem.Sun,
                Ground = "Desert Rose Stadium", GroundShort = "Desert Rose", BoundaryStraight = 73, BoundarySquare = 68, PitchPace = 0.97, PitchSpin = 1.08, StadiumStyle = 0, Floodlights = 4, Titles = 1 },
            new TeamDef { Id = "GUJ", Name = "Gujarat Gladiators", City = "Ahmedabad", Nick = "Gladiators", Primary = 0xFF0F2547, Secondary = 0xFF2BC4B4, Accent = 0xFF5EE6D6, Emblem = Emblem.Trident,
                Ground = "Narmada Park Stadium", GroundShort = "Narmada Park", BoundaryStraight = 74, BoundarySquare = 69, PitchPace = 1.03, PitchSpin = 1.0, StadiumStyle = 2, Floodlights = 6, Titles = 1 },
            new TeamDef { Id = "LKO", Name = "Lucknow Nawabs", City = "Lucknow", Nick = "Nawabs", Primary = 0xFF1BA3D9, Secondary = 0xFFF57C22, Accent = 0xFF7AD6FF, Emblem = Emblem.Crescent,
                Ground = "Gomti Riverside Ground", GroundShort = "Gomti Riverside", BoundaryStraight = 70, BoundarySquare = 66, PitchPace = 0.95, PitchSpin = 1.12, StadiumStyle = 1, Floodlights = 4, Titles = 0 },
        };

        public static TeamDef ById(string id)
        {
            foreach (var t in All)
            {
                if (t.Id == id) return t;
            }
            return All[0];
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) return i;
            }
            return -1;
        }
    }

    public static class League
    {
        public const string Name = "KPL";
        public const string FullName = "KPL Cricket";
        public const int Year = 2026;
        public const int MaxOverseasXI = 4;
        public const int SquadSize = 16;
    }
}
