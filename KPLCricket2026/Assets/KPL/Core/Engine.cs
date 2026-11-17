using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    /// <summary>Per-side modifiers from the player's progress (equipment, coaches).</summary>
    public sealed class SideBoosts
    {
        /// <summary>Widens the user's timing windows (0.1 = +10%).</summary>
        public double TimingBonus;
        /// <summary>Extra hitting power for the user's batters.</summary>
        public double PowerBonus;
        /// <summary>Lifts bowling execution (accuracy) for this side.</summary>
        public double AccuracyBonus;
        /// <summary>Raises catch success for this side's fielders.</summary>
        public double CatchBonus;
    }

    /// <summary>
    /// Runs one delivery at a time: picks AI plans, builds the ball's path, resolves the batter's
    /// response and the fielding. Used by both the 3D match and the instant simulator.
    /// </summary>
    public sealed class BallEngine
    {
        public readonly MatchState M;
        public readonly Ground Ground;
        readonly Dictionary<string, SideBoosts> _boosts = new Dictionary<string, SideBoosts>();
        readonly List<DeliveryKind> _recent = new List<DeliveryKind>();

        public FieldSetting? UserField;
        public FieldLayout Layout { get; private set; }
        public Fielding.Side Side { get; private set; }

        public BallEngine(MatchState m)
        {
            M = m;
            Ground = new Ground(m.Setup.Venue);
        }

        public void SetBoosts(string teamId, SideBoosts b) => _boosts[teamId] = b;

        public SideBoosts BoostsFor(string teamId) => _boosts.TryGetValue(teamId, out var b) ? b : new SideBoosts();

        public string UserId => M.Setup.UserTeamId;
        public bool UserBatting => UserId != null && M.Current.Bat.Team.Id == UserId;
        public bool UserBowling => UserId != null && M.Current.Bowl.Team.Id == UserId;

        double DifficultyScale
        {
            get
            {
                switch (M.Setup.Difficulty)
                {
                    case Difficulty.Easy: return 1.3;
                    case Difficulty.Medium: return 1.08;
                    case Difficulty.Hard: return 0.94;
                    default: return 0.84;
                }
            }
        }

        /// <summary>Lays out the field for the current striker and bowler.</summary>
        public void PrepareField()
        {
            var inn = M.Current;
            var striker = inn.StrikerCard.P;
            var bowler = inn.Bowler ?? inn.Bowl.XI[inn.Bowl.XI.Count - 1];
            var setting = UserBowling && UserField != null ? UserField.Value : AiBowling.PickField(inn);
            Layout = FieldLayout.Create(setting, bowler.IsSpin, striker.LeftBat, Ground, inn.Phase);
            var side = new Fielding.Side { Layout = Layout };
            var fielders = inn.Bowl.XI.Where(p => p.Id != inn.Bowl.KeeperId && p.Id != bowler.Id).ToList();
            // Best fielders in the ring, weaker ones in the deep.
            for (int i = 0; i < 9; i++)
            {
                side.Players[i] = i < fielders.Count ? fielders[i] : fielders[fielders.Count - 1];
                side.Pos[i] = Layout.Pos[i];
            }
            side.Players[9] = inn.Bowl.ById(inn.Bowl.KeeperId);
            side.Pos[9] = Layout.Keeper;
            side.Players[10] = bowler;
            side.Pos[10] = Layout.Bowler;
            Side = side;
        }

        public void EnsureBowler()
        {
            if (M.NeedBowler) M.SetBowler(AiBowling.PickBowler(M, M.Rng));
        }

        public DeliveryPlan AiPlan()
        {
            var inn = M.Current;
            double bias = BoostsFor(inn.Bowl.Team.Id).AccuracyBonus;
            if (UserBatting)
            {
                // Difficulty shapes how well the AI bowls at the user.
                switch (M.Setup.Difficulty)
                {
                    case Difficulty.Easy: bias -= 0.14; break;
                    case Difficulty.Hard: bias += 0.05; break;
                    case Difficulty.Pro: bias += 0.1; break;
                }
            }
            return AiBowling.Plan(inn.Bowler, inn.StrikerCard.P, inn, M.Rng, bias);
        }

        public DeliveryTrack BuildTrack(DeliveryPlan plan)
        {
            var inn = M.Current;
            var tr = Deliveries.Build(plan, inn.Bowler, inn.StrikerCard.P, M.Setup.Venue, M.Rng);
            _recent.Insert(0, plan.Kind);
            if (_recent.Count > 4) _recent.RemoveAt(4);
            // Predictability: the same delivery again and again is easier to read.
            int reps = _recent.Skip(1).Take(3).Count(k => k == plan.Kind);
            tr.Quality = MathX.Clamp01(tr.Quality - reps * 0.12);
            return tr;
        }

        public ShotInput AiShot(DeliveryTrack tr)
        {
            var inn = M.Current;
            double scale = UserBowling ? DifficultyScale : 1.0;
            return AiBatting.Choose(tr, inn, inn.StrikerCard, M.Rng, scale);
        }

        public TimingWindows UserWindows => TimingWindows.For(M.Setup.Difficulty, UserId != null ? BoostsFor(UserId).TimingBonus : 0);

        /// <summary>Resolves the batter's response and the fielding. Not yet applied to the score.</summary>
        public Play Resolve(DeliveryTrack tr, ShotInput shot, bool userShot)
        {
            var inn = M.Current;
            var bat = BoostsFor(inn.Bat.Team.Id);
            var w = userShot ? UserWindows : TimingWindows.For(Difficulty.Medium);
            var contact = Batting.Resolve(tr, inn.StrikerCard.P, shot, w, bat.PowerBonus, M.Rng);
            if (Side == null) PrepareField();
            double agg = AiBatting.Aggression(inn, inn.StrikerCard);
            var play = Fielding.Resolve(tr, contact, Side, inn.StrikerCard.P, inn.NonStrikerCard.P, Ground, agg, M.Rng);
            var fb = BoostsFor(inn.Bowl.Team.Id);
            if (play.CatchChance && fb.CatchBonus > 0 && play.Dropped && play.DropBranch != null)
            {
                // Coaching: a second look at a dropped chance.
                double extra = fb.CatchBonus * play.CatchProb;
                if (M.Rng.Chance(extra)) MakeCaught(play);
            }
            return play;
        }

        /// <summary>Turns a play with a catch chance into the caught version (user took the catch).</summary>
        public static void MakeCaught(Play play)
        {
            if (!play.CatchChance) return;
            play.Dropped = false;
            play.Runs = 0;
            play.Four = play.Six = false;
            play.Thrown = false;
            play.RunTimes = new double[0];
            play.Overthrow = false;
            play.BoundaryOverRope = false;
            play.Wicket = play.Fielder == 9 ? WicketKind.CaughtBehind : play.Fielder == 10 ? WicketKind.CaughtAndBowled : WicketKind.Caught;
            play.Duration = play.CatchTime + 2.2;
        }

        /// <summary>Turns a play with a catch chance into the dropped version.</summary>
        public static void MakeDropped(Play play)
        {
            if (!play.CatchChance || play.DropBranch == null) return;
            Fielding.CopyInto(play.DropBranch, play);
            play.Dropped = true;
        }

        public BallRecord Commit(Play play)
        {
            var inn = M.Current;
            string text = Commentary.Line(play, inn, M.Rng);
            var rec = M.Apply(play, text);
            if (!M.Finished && M.Current != inn) Side = null;
            return rec;
        }

        // ------------------------------------------------------------ fast sim

        /// <summary>One AI-vs-AI delivery, applied.</summary>
        public BallRecord SimBall()
        {
            if (M.Finished) return null;
            EnsureBowler();
            var inn = M.Current;
            if (inn.BallInOver == 0 || Side == null || Side.Players[10]?.Id != inn.Bowler.Id) PrepareField();
            var plan = AiPlan();
            var tr = BuildTrack(plan);
            var shot = AiShot(tr);
            var play = Resolve(tr, shot, false);
            return Commit(play);
        }

        public void SimOver()
        {
            if (M.Finished) return;
            var inn = M.Current;
            int over = inn.OverIndex;
            int guard = 0;
            while (!M.Finished && M.Current == inn && inn.OverIndex == over && guard++ < 60) SimBall();
        }

        public void SimInnings()
        {
            if (M.Finished) return;
            var inn = M.Current;
            int guard = 0;
            while (!M.Finished && M.Current == inn && guard++ < 1000) SimBall();
        }

        public void SimToEnd()
        {
            if (!M.Started)
            {
                if (M.IsScenario) M.StartScenario();
                else M.SetToss(M.Rng.Chance(0.5) ? M.Setup.Home.Team.Id : M.Setup.Away.Team.Id, M.Rng.Chance(0.45));
            }
            int guard = 0;
            while (!M.Finished && guard++ < 4000) SimBall();
        }

        /// <summary>Plays a whole AI-vs-AI match instantly.</summary>
        public static MatchState QuickSim(MatchSetup setup)
        {
            var m = new MatchState(setup);
            new BallEngine(m).SimToEnd();
            return m;
        }
    }

    public static class Commentary
    {
        static string Pick(Rng rng, params string[] options) => options[rng.Int(options.Length)];

        public static string Line(Play p, Innings inn, Rng rng)
        {
            string bat = inn.StrikerCard.P.Last;
            string bowl = inn.Bowler?.Last ?? "the bowler";
            string shot = p.Contact?.ShotName?.ToLowerInvariant() ?? "";
            string len = p.Track != null ? p.Track.LengthText.ToLowerInvariant() : "";
            if (p.Wicket != null && !(p.NoBall && p.Wicket != WicketKind.RunOut) && !(inn.FreeHit && p.Wicket != WicketKind.RunOut))
            {
                switch (p.Wicket.Value)
                {
                    case WicketKind.Bowled:
                        return p.Contact != null && p.Contact.PlayedOn ? Pick(rng, "Dragged on! " + bat + " chops it onto the stumps.", "Inside edge and onto the stumps - " + bat + " has to go.")
                            : Pick(rng, "Bowled him! " + bowl + " rattles the timber.", "Through the gate! The stumps are a mess.", "Clean bowled - " + bat + " misses and the bails go flying.");
                    case WicketKind.Lbw: return Pick(rng, "Plumb in front! The finger goes up - lbw.", "Trapped on the crease. " + bowl + " gets the lbw.");
                    case WicketKind.Caught: return Pick(rng, "Gone! Straight down the fielder's throat.", "Caught in the deep! " + bat + " couldn't clear the rope.", "Up in the air... and taken! Big wicket.");
                    case WicketKind.CaughtBehind: return Pick(rng, "Edged and taken behind! A thin nick.", "Feather of an edge - the keeper does the rest.");
                    case WicketKind.CaughtAndBowled: return Pick(rng, "Caught and bowled! Sharp reflexes from " + bowl + ".", "Straight back to the bowler - " + bowl + " holds on!");
                    case WicketKind.Stumped: return Pick(rng, "Stumped! Beaten in the flight and the keeper whips the bails off.", "Down the track, misses, and he's stumped by a mile.");
                    default: return p.DirectHit ? "Direct hit! Run out - what a throw!" : Pick(rng, "Run out! Terrible mix-up between the batters.", "They went for the extra run and paid the price - run out.");
                }
            }
            if (p.Wide) return Pick(rng, "Wide. Strays too far from the batter.", "Called wide - " + bowl + " loses his line.");
            string nb = p.NoBall ? "No-ball! " : "";
            if (p.Six)
            {
                int d = p.Flight != null ? (int)Math.Round(p.Flight.Carry + 4) : 80;
                return nb + Pick(rng, "SIX! " + bat + " launches it " + d + " metres!", "That's huge! A " + shot + " into the stands for six.", "Out of the ground! " + d + "m maximum.", "Gone all the way - massive six off a " + len + " ball.");
            }
            if (p.Four) return nb + Pick(rng, "FOUR! A glorious " + shot + ".", "Cracking shot - races away to the rope.", "Beats the fielder and it's four.", "Timed to perfection, four runs.");
            if (p.Dropped) return nb + "Dropped! A chance goes down - " + p.Runs + " run" + (p.Runs == 1 ? "" : "s") + " taken.";
            if (p.Byes > 0) return "Beats everyone - " + p.Byes + " bye" + (p.Byes == 1 ? "" : "s") + ".";
            if (p.LegByes > 0) return "Off the pad, they scamper a leg bye.";
            switch (p.Runs)
            {
                case 0:
                    if (p.Contact != null && p.Contact.Type == ContactType.Defended) return nb + Pick(rng, "Solid defence from " + bat + ".", "Dead bat, no run.", "Watchful - blocked back to " + bowl + ".");
                    if (p.Contact != null && (p.Contact.Type == ContactType.Missed)) return nb + Pick(rng, "Beaten! Lovely delivery from " + bowl + ".", "Plays and misses.", "Past the outside edge.");
                    if (p.Contact != null && p.Contact.Type == ContactType.Left) return nb + Pick(rng, "Left alone.", "Shoulders arms.");
                    return nb + Pick(rng, "Dot ball. Good fielding.", "Straight to the fielder, no run.", "Tight from " + bowl + ".");
                case 1: return nb + Pick(rng, "Worked away for a single.", "Quick single taken.", "Pushed into the gap for one.");
                case 2: return nb + Pick(rng, "Two runs - good running.", "Into the gap, they come back for two.");
                case 3: return nb + "Excellent running, three taken.";
                default: return nb + p.Runs + " runs.";
            }
        }
    }
}
