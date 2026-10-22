using System;
using System.Collections.Generic;
using System.Linq;
using PCC.Core;
using UnityEngine;

namespace PCC.Game
{
    public sealed class MatchConfig
    {
        public MatchEngine Engine;
        public string UserTeamId;
        public string Title;
        public bool Day = true;
        public Action<MatchEngine> OnFinished;
        public Action OnExit;
    }

    public enum MatchPhase { Toss, Ready, RunUp, Flight, Carry, Follow, Result, OverBreak, InningsBreak, Done }

    /// <summary>
    /// Runs one match: the ball cycle (run-up, delivery, shot, fielding),
    /// cameras, actors and the user's batting / bowling input. Scoring comes
    /// from <see cref="MatchEngine"/>; this class turns it into 3D action.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        public static readonly string[] PaceVariations = { "STOCK", "SLOWER BALL", "IN-SWINGER", "OUT-SWINGER" };
        public static readonly string[] SpinVariations = { "STOCK", "QUICKER", "MYSTERY" };

        public MatchConfig Cfg { get; private set; }
        public MatchEngine M => Cfg.Engine;
        public MatchPhase Phase { get; private set; } = MatchPhase.Toss;
        public Stadium Stadium { get; private set; }
        public Hud Hud { get; private set; }

        Camera _cam;
        MatchProps _props;
        Transform _actorRoot;
        readonly Dictionary<string, Cricketer> _rigs = new Dictionary<string, Cricketer>();
        readonly HashSet<Cricketer> _used = new HashSet<Cricketer>();

        double _t, _clock, _hype = 0.25, _shake;

        // Ball state.
        DeliveryPlan _plan;
        DeliveryPath _path;
        BallResult _result;
        ShotPlan _shot;
        V3? _ballPos;
        double _releaseClock, _carryStartFt;
        double? _pressClock;
        ShotDir? _pressDir;
        bool _pressDefend, _pressLoft, _carryHit, _stumpsDone;
        public TimingGrade? Grade { get; private set; }
        public bool LoftToggle;
        public string SpeedGun { get; private set; }

        // Actors.
        double _bowlerX = -1.2, _bowlerY = -6.2, _nsY = 0.9, _walk;
        Pose _bowlerPose = Poses.RunA, _batterPose = Poses.BatStance, _keeperPose = Poses.KeeperCrouch, _umpPose = Poses.Umpire;
        FieldSetting _field = FieldSetting.For(0, false, false);

        // Cameras.
        bool _following;
        double _followStartClock;
        V3 _followStart, _followEnd, _followPos, _look;
        double _followK = 1.3;
        double _aimZoom, _lastAimTouch = -10;

        // Bowling controls.
        public V3 Aim = new V3(-0.12, 14.1, 0.01);
        public int Variation;
        public bool MeterOn { get; private set; }
        double _meterT;
        public bool AutoBowl;
        readonly List<DeliveryType> _recent = new List<DeliveryType>();
        public bool PickingBowler { get; private set; }
        bool _draggingAim;

        // Swipe input.
        Vector2? _swipeStart;
        double _swipeStartClock;
        bool _swipeFired;
        public readonly List<(Vector2 P, double T)> Swipe = new List<(Vector2, double)>();

        int _inningsCount = 1;
        bool _finishedCalled;
        Action _pendingSim;

        // Toss.
        public bool? TossCall { get; private set; }
        public bool Flipping { get; private set; }
        public double FlipT { get; private set; }
        public bool AwaitTossChoice { get; private set; }
        public string TossText { get; private set; }

        public double Clock => _clock;
        public bool UserBatting => M.Started && !M.Finished && M.Current.Batting.Team.Id == Cfg.UserTeamId;
        public bool UserBowling => M.Started && !M.Finished && M.Current.Bowling.Team.Id == Cfg.UserTeamId;
        public TeamInfo UserTeam => M.SheetOf(Cfg.UserTeamId).Team;
        public TeamInfo OppTeam => (M.Home.Team.Id == Cfg.UserTeamId ? M.Away : M.Home).Team;
        double Hand => (M.Started ? M.Striker : M.Home.Order[0]).LeftBat ? -1 : 1;
        public bool CanSim => M.Started && !M.Finished && (Phase == MatchPhase.Ready || Phase == MatchPhase.OverBreak);
        public bool SimOffered => M.Started && !M.Finished && Phase != MatchPhase.InningsBreak && Phase != MatchPhase.Done;
        public bool AwaitingShot => Phase == MatchPhase.Flight && UserBatting && _pressClock == null;
        public double? RingProgress { get; private set; }

        // ------------------------------------------------------------ setup

        public static MatchDirector Create(MatchConfig cfg, Transform canvas)
        {
            var go = new GameObject("Match");
            var d = go.AddComponent<MatchDirector>();
            d.Init(cfg, canvas);
            return d;
        }

        void Init(MatchConfig cfg, Transform canvas)
        {
            Cfg = cfg;
            var venue = M.Venue;
            var away = venue.Id == M.Home.Team.Id ? M.Away.Team : M.Home.Team;
            Stadium = Stadium.Build(venue, away, cfg.Day, transform);
            SceneLook.Apply(cfg.Day, transform);
            _cam = SceneLook.MainCamera();
            _props = MatchProps.Build(transform, Mats.Hex(venue.Primary));
            _actorRoot = new GameObject("Actors").transform;
            _actorRoot.SetParent(transform, false);
            Hud = Hud.Build(canvas, this);
            Sfx.I?.StartCrowd();
            Rigs.Bowling.ApplyTo(_cam);
        }

        void OnDestroy()
        {
            Sfx.I?.StopCrowd();
            if (Hud != null) Destroy(Hud.gameObject);
        }

        // ------------------------------------------------------------ toss

        public void CallToss(bool heads)
        {
            if (Flipping || TossCall != null) return;
            TossCall = heads;
            Flipping = true;
            FlipT = 0;
        }

        /// <summary>Test hook: the AI always wins the toss and bats.</summary>
        public static bool ForceAiBat;

        void ResolveToss()
        {
            Flipping = false;
            bool headsUp = M.Rng.Chance(0.5);
            if (ForceAiBat) headsUp = TossCall != true;
            if (headsUp == TossCall)
            {
                AwaitTossChoice = true;
                TossText = (headsUp ? "HEADS" : "TAILS") + "! You won the toss";
            }
            else
            {
                bool bat = ForceAiBat || M.AiElectBat();
                TossText = (headsUp ? "HEADS" : "TAILS") + "! " + OppTeam.Id + " won the toss and chose to " + (bat ? "bat" : "bowl");
                Invoke(nameof(AiTossDone), 1.7f);
                _aiTossBat = bat;
            }
        }

        bool _aiTossBat;

        void AiTossDone()
        {
            Dbg.Mark("AiTossDone");
            M.SetToss(OppTeam.Id, _aiTossBat);
            StartMatch();
        }

        public void ChooseBat(bool bat)
        {
            if (!AwaitTossChoice) return;
            AwaitTossChoice = false;
            M.SetToss(Cfg.UserTeamId, bat);
            StartMatch();
        }

        void StartMatch()
        {
            Dbg.Mark("StartMatch");
            _inningsCount = M.InningsList.Count;
            Hud.ShowBanner("PLAY!", Pal.Gold, M.TossText);
            Dbg.Mark("StartMatch banner");
            EnterReady();
            Dbg.Mark("StartMatch done");
        }

        // ------------------------------------------------------- ball cycle

        void EnterReady()
        {
            Phase = MatchPhase.Ready;
            _t = 0;
            _following = false;
            _plan = null;
            _path = null;
            _result = null;
            _shot = null;
            _ballPos = null;
            _pressClock = null;
            _pressDir = null;
            _pressDefend = false;
            MeterOn = false;
            _stumpsDone = false;
            SpeedGun = null;
            _walk = 0;
            _nsY = 0.9;
            _umpPose = Poses.Umpire;
            _keeperPose = Poses.KeeperCrouch;
            _batterPose = Poses.BatStance;
            RingProgress = null;
            Dbg.Mark("ER.marker");
            UpdateMarker(0);
            Dbg.Mark("ER.stumps");
            _props.ResetStumps();
            _props.ClearTrail();
            Dbg.Mark("ER.screen");
            Stadium.SetScreen(ScreenEvent.None);
            Dbg.Mark("ER.state");
            if (M.Finished)
            {
                EnterDone();
                return;
            }
            var pending = _pendingSim;
            if (pending != null)
            {
                Sim(pending);
                return;
            }
            Dbg.Mark("ER.needBowler");
            if (M.NeedBowler)
            {
                Dbg.Mark("ER.userBowling");
                if (UserBowling && !AutoBowl)
                {
                    PickingBowler = true;
                    Dbg.Mark("ShowBowlerPicker");
                    Hud.ShowBowlerPicker();
                    Dbg.Mark("ShowBowlerPicker done");
                    return;
                }
                M.AutoBowlerIfNeeded();
            }
            SetupBall();
        }

        public void PickBowler(int id)
        {
            PickingBowler = false;
            M.SetBowler(id);
            SetupBall();
        }

        void SetupBall()
        {
            var inn = M.Current;
            var bowler = M.Bowler;
            _field = FieldSetting.For(inn.Phase, bowler.IsSpin, M.Striker.LeftBat, M.Venue.Boundary);
            double side = bowler.BowlStyle.LeftArm() ? 1 : -1;
            _bowlerX = side * (bowler.IsSpin ? 0.85 : 1.2);
            _bowlerY = bowler.IsSpin ? -3.6 : -6.2;
            _bowlerPose = Poses.RunA;
            if (Variation >= (bowler.IsSpin ? SpinVariations : PaceVariations).Length) Variation = 0;
        }

        // --------------------------------------------------- user bowling

        public (DeliveryType, BallLine) AimToDelivery()
        {
            var bowler = M.Bowler;
            double lat = Aim.x * Hand;
            var line = lat < -0.45 ? BallLine.WideOff : (lat < -0.06 ? BallLine.Off : (lat < 0.07 ? BallLine.Middle : BallLine.Leg));
            double len = Pitch.Length - Aim.y;
            DeliveryType t;
            if (bowler.IsSpin)
            {
                t = len < 3.9 ? DeliveryType.Flighted : (len < 7.2 ? DeliveryType.Stock : DeliveryType.Quicker);
                if (Variation == 1) t = DeliveryType.Quicker;
                if (Variation == 2) t = DeliveryType.Variation;
            }
            else
            {
                t = len < 2.0 ? (line == BallLine.WideOff ? DeliveryType.WideYorker : DeliveryType.Yorker)
                    : len < 4.5 ? DeliveryType.Full
                    : len < 7.2 ? DeliveryType.GoodLength
                    : len < 9.6 ? DeliveryType.Short
                    : DeliveryType.Bouncer;
                if (Variation == 1) t = DeliveryType.Slower;
            }
            return (t, line);
        }

        double? SwingOverride()
        {
            if (M.Bowler.IsSpin) return null;
            if (Variation == 2) return 0.27 * Hand;
            if (Variation == 3) return -0.27 * Hand;
            return null;
        }

        public void MoveAim(Vector2 screen)
        {
            if (_cam == null) return;
            var ray = _cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0) return;
            var g = V3.FromUnity(ray.origin + ray.direction * t);
            _lastAimTouch = _clock;
            Aim = new V3(Math.Clamp(g.x, -1.3, 1.3), Math.Clamp(g.y, Pitch.Length - 12.0, Pitch.Length - 0.4), 0.01);
        }

        public void OnBowlButton()
        {
            if (Phase != MatchPhase.Ready || !UserBowling || PickingBowler) return;
            if (!MeterOn)
            {
                MeterOn = true;
                _meterT = 0;
                return;
            }
            double accuracy = Math.Clamp(1 - Math.Abs(MeterValue - 0.5) * 2, 0.0, 1.0);
            var (type, line) = AimToDelivery();
            _plan = Deliveries.PlanUser(M.Rng, M.Bowler, type, line, accuracy, M.Current.Phase, M.Venue.SpinFactor, Aim.y, Aim.x, SwingOverride());
            int pct = (int)Math.Round(accuracy * 100);
            Hud.ShowToast("ACCURACY " + pct + "%  •  " + (pct >= 85 ? "SPOT ON!" : pct >= 60 ? "CLOSE" : "WAYWARD"));
            MeterOn = false;
            StartDelivery();
        }

        public double MeterValue
        {
            get
            {
                double speed = M.Difficulty == Difficulty.Easy ? 3.0 : (M.Difficulty == Difficulty.Normal ? 4.1 : 5.4);
                return 0.5 + 0.5 * Math.Sin(_meterT * speed - Math.PI / 2);
            }
        }

        // ------------------------------------------------------- delivery

        void StartDelivery()
        {
            var inn = M.Current;
            var bowler = M.Bowler;
            if (UserBatting)
            {
                _plan = Deliveries.PlanAi(M.Rng, bowler, inn.Phase, M.Venue.SpinFactor, M.Difficulty.AiBowlBonus());
            }
            else if (UserBowling)
            {
                if (AutoBowl || _plan == null) _plan = Deliveries.PlanAi(M.Rng, bowler, inn.Phase, M.Venue.SpinFactor);
                _result = M.ResolveUserBowling(_plan, _recent);
                _recent.Insert(0, _plan.Type);
                if (_recent.Count > 6) _recent.RemoveAt(_recent.Count - 1);
            }
            _path = new DeliveryPath(_plan, M.Striker.LeftBat, M.Difficulty.SlowMo(), _field.Keeper.y);
            Phase = MatchPhase.RunUp;
            _t = 0;
        }

        double RunUpDur => M.Bowler.IsSpin ? 0.8 : 1.15;

        /// <summary>Where the current ball pitches, with its length label (for the HUD), or null.</summary>
        public (V3 Pos, string Label, Color Color, float Alpha)? PitchMarker { get; private set; }

        /// <summary>
        /// Pitch marker: batters (with the timing hint on) see the length from mid run-up;
        /// user bowlers see where their ball actually landed next to their target.
        /// </summary>
        void UpdateMarker(double alpha)
        {
            bool show = _path != null && _plan != null && _path.Bounces && !_plan.Wide && alpha > 0.01
                && ((UserBatting && Settings.TimingHint) || (UserBowling && !AutoBowl));
            if (!show)
            {
                PitchMarker = null;
                _props.ShowMarker(null, 0);
                return;
            }
            var pos = new V3(_path.BounceX, _path.BounceY, 0);
            var (label, color) = MatchProps.ZoneAt(Pitch.Length - _path.BounceY);
            PitchMarker = (pos, label, color, (float)alpha);
            _props.ShowMarker(pos, (float)alpha, color);
        }

        void TickRunUp()
        {
            double u = MathX.Clamp01(_t / RunUpDur);
            bool spin = M.Bowler.IsSpin;
            double startY = spin ? -3.6 : -6.2;
            const double endY = 0.3, runEnd = 0.7;
            double side = M.Bowler.BowlStyle.LeftArm() ? 1 : -1;
            if (u < runEnd)
            {
                double k = u / runEnd;
                _bowlerY = MathX.Lerp(startY, endY - 1.0, k);
                _bowlerX = side * MathX.Lerp(spin ? 0.85 : 1.2, 0.62, k);
                _bowlerPose = Pose.Lerp(Poses.RunA, Poses.RunB, 0.5 + 0.5 * Math.Sin(_t * (spin ? 9.0 : 11.5)));
            }
            else
            {
                double k = (u - runEnd) / (1 - runEnd);
                _bowlerY = MathX.Lerp(endY - 1.0, endY, k);
                _bowlerX = side * 0.55;
                _bowlerPose = Pose.Sample(new List<(double, Pose)> { (0, Poses.Gather), (0.45, Poses.Load), (0.85, Poses.Release), (1.0, Poses.Release) }, k);
            }
            _nsY = MathX.Lerp(0.9, 1.7, u);
            _walk = 1.8 * u;
            // Batters read the length from mid run-up, like a broadcast pitch map.
            if (UserBatting) UpdateMarker(MathX.Clamp01((u - 0.3) / 0.3));
            if (u >= 1)
            {
                Phase = MatchPhase.Flight;
                _t = 0;
                _releaseClock = _clock;
                UpdateMarker(1);
                SpeedGun = _plan.Speed.ToString("F1") + " km/h";
                _props.ClearTrail();
            }
        }

        void TickFlight()
        {
            var p = _path;
            double ft = _clock - _releaseClock;
            _ballPos = p.At(ft);
            _bowlerPose = Pose.Sample(new List<(double, Pose)> { (0, Poses.Release), (0.28, Poses.Follow) }, ft);
            _bowlerY = 0.3 + Math.Min(ft, 0.7) * 2.0;
            _nsY = 1.7 + Math.Min(ft, 0.6);
            // Batting: fade out just after the bounce. Bowling: keep it up so the accuracy is readable.
            UpdateMarker(UserBatting ? (ft < p.TBounce ? 1 : MathX.Clamp01(1 - (ft - p.TBounce) * 3)) : 1);
            if (_pressClock == null)
            {
                _batterPose = Pose.Lerp(Poses.BatStance, Poses.BatBacklift, MathX.Clamp01(ft / 0.35));
                RingProgress = UserBatting && Settings.TimingHint ? MathX.Clamp01((p.TArrive - ft) / p.TArrive) : (double?)null;
            }
            else
            {
                RingProgress = null;
            }
            if (ft > p.TArrive - 0.2) _keeperPose = Pose.Lerp(Poses.KeeperCrouch, Poses.KeeperUp, MathX.Clamp01((ft - p.TArrive + 0.2) / 0.3));
            if (UserBowling && _pressClock == null && ft >= p.TArrive - 0.1) AiSwing(_result);

            if (UserBatting)
            {
                double late = p.TArrive + M.Difficulty.Timing().Poor / 1000;
                if (_pressClock != null && ft >= p.TArrive)
                {
                    BeginAfterContact(ft);
                }
                else if (_pressClock == null && ft >= late)
                {
                    _result = M.ResolveUserShot(_plan, ShotInput.None);
                    Grade = null;
                    BeginAfterContact(ft);
                }
            }
            else if (ft >= p.TArrive)
            {
                BeginAfterContact(ft);
            }
        }

        // ---------------------------------------------------- user batting

        /// <summary>
        /// Plays a shot now. <paramref name="screenAngle"/>: shot direction in degrees on the
        /// broadcast view (0 = up = straight back, +90 = screen right). The camera faces the
        /// batter, so screen right is a right-hander's leg side.
        /// </summary>
        public void PlayShot(double? screenAngle, bool loft, bool defend)
        {
            if (Phase != MatchPhase.Flight || !UserBatting || _pressClock != null) return;
            var p = _path;
            double ft = _clock - _releaseClock;
            double err = (ft - p.TArrive) * 1000;
            ShotDir? dir = defend ? (ShotDir?)null : DeliveryX.Nearest(-(screenAngle ?? 0) * Hand);
            _pressClock = _clock;
            _pressDir = dir;
            _pressDefend = defend;
            _pressLoft = loft;
            _result = M.ResolveUserShot(_plan, new ShotInput { Dir = dir, Defend = defend, Loft = loft, TimingMs = err });
            Grade = _result.Timing ?? M.Difficulty.Timing().Grade(err);
            Hud.ShowTiming(Grade);
        }

        void AiSwing(BallResult r)
        {
            _pressClock = _clock;
            _pressLoft = r.Lofted;
            if (r.Extra == ExtraType.Wide || (r.Beaten && M.Rng.Chance(0.55)))
            {
                _pressDir = null;
                _pressDefend = false;
                return;
            }
            if (r.Defended && r.Runs == 0)
            {
                _pressDefend = true;
                return;
            }
            _pressDir = DeliveryX.Nearest(r.Angle ?? 0);
        }

        Pose SwingPose(double dt)
        {
            if (_pressDefend) return Pose.Lerp(Poses.BatBacklift, Poses.Defend, MathX.Clamp01(dt / 0.12));
            if (_pressDir == null) return Pose.Lerp(Poses.BatBacklift, Poses.Leave, MathX.Clamp01(dt / 0.2));
            bool spin = _plan != null && _plan.Spin;
            Pose contact, follow;
            switch (_pressDir.Value)
            {
                case ShotDir.Straight:
                case ShotDir.Cover:
                    contact = Poses.DriveContact;
                    follow = _pressLoft ? Poses.LoftFollow : Poses.DriveFollow;
                    break;
                case ShotDir.Point:
                case ShotDir.ThirdMan:
                    contact = Poses.CutContact;
                    follow = Poses.CutFollow;
                    break;
                case ShotDir.FineLeg when spin:
                    contact = Poses.Sweep;
                    follow = Poses.Sweep;
                    break;
                default:
                    contact = Poses.PullContact;
                    follow = Poses.PullFollow;
                    break;
            }
            return Pose.Sample(new List<(double, Pose)> { (0, Poses.BatBacklift), (0.11, contact), (0.42, follow) }, dt);
        }

        static bool IsHit(BallResult r)
        {
            if (r.Extra == ExtraType.Wide || r.Beaten) return false;
            if (r.Wicket)
            {
                switch (r.Dismissal)
                {
                    case Dismissal.Bowled:
                    case Dismissal.Lbw:
                    case Dismissal.Stumped:
                    case Dismissal.CaughtBehind:
                        return false;
                }
            }
            return true;
        }

        void BeginAfterContact(double ft)
        {
            var r = _result;
            RingProgress = null;
            UpdateMarker(0);
            _carryHit = IsHit(r);
            if (_carryHit && !(r.Defended && r.Total == 0)) Sfx.I?.Bat(r.IsSix ? 1.2f : (r.IsFour ? 1 : 0.7f));
            bool defendedDot = r.Defended && r.Total == 0 && !r.Wicket;
            if (_carryHit && defendedDot) Sfx.I?.Bat(0.35f);
            if (_carryHit && !defendedDot)
            {
                BeginFollow();
                return;
            }
            Phase = MatchPhase.Carry;
            _t = 0;
            _carryStartFt = ft;
        }

        void TickCarry()
        {
            var r = _result;
            var p = _path;
            double ft = _carryStartFt + _t;
            if (_carryHit)
            {
                var start = p.At(_carryStartFt);
                double k = MathX.Clamp01(_t / 0.6);
                _ballPos = new V3(start.x + 0.4 * k * Hand, start.y - 3.2 * k, Math.Max(0.036, start.z * (1 - k) + 0.25 * Math.Sin(k * Math.PI)));
            }
            else
            {
                switch (r.Dismissal)
                {
                    case Dismissal.Bowled:
                        if (ft < p.TStumps) _ballPos = p.At(ft);
                        else if (!_stumpsDone)
                        {
                            _stumpsDone = true;
                            _props.HitStumps(true);
                            _shake = 0.8;
                            Sfx.I?.Stumps();
                            Settings.Buzz();
                        }
                        break;
                    case Dismissal.Lbw:
                    {
                        var at = p.At(Math.Min(ft, p.TArrive));
                        _ballPos = new V3(at.x, at.y, Math.Max(0.036, at.z - Math.Max(0, ft - p.TArrive) * 1.5));
                        break;
                    }
                    case Dismissal.Stumped:
                        _ballPos = p.At(ft);
                        if (ft > p.TAfter * 0.5 + p.TArrive && !_stumpsDone)
                        {
                            _stumpsDone = true;
                            _props.HitStumps(true);
                            Sfx.I?.Stumps();
                        }
                        break;
                    default:
                        _ballPos = p.At(ft);
                        break;
                }
                if (ft > p.TArrive - 0.1) _keeperPose = Poses.KeeperUp;
            }
            if (_t > 0.9) EnterResult();
        }

        void BeginFollow()
        {
            _shot = ShotPlan.Make(_result, _field, M.Striker.LeftBat, M.Venue.Boundary, M.Rng);
            _following = true;
            _followStartClock = _clock;
            var b = BaseShot();
            _followStart = b.Pos;
            _followPos = b.Pos;
            _look = b.Look;
            _followK = b.K;
            // Both views now sit behind the bowler: pull back and up, broadcast style.
            _followEnd = new V3(0, -44, 20);
            Phase = MatchPhase.Follow;
            _t = 0;
            _props.ClearTrail();
        }

        void TickFollow()
        {
            var s = _shot;
            _ballPos = s.Path.At(_t);
            if (_t >= s.Duration + 0.3) EnterResult();
        }

        // ------------------------------------------------------------ result

        void EnterResult()
        {
            var r = _result;
            var inn = M.Current;
            var batter = M.Striker;
            var bowler = M.Bowler;
            string outText = null;
            if (r.Wicket)
            {
                int outId = r.OutBatterId ?? inn.Striker;
                var outP = inn.Batting.ById(outId);
                var card = inn.Bat[outId];
                var fielder = r.FielderId == null ? null : inn.Bowling.ById(r.FielderId.Value);
                outText = outP.ShortName + " " + Scorecard.DismissalLine(r.Dismissal.Value, fielder, bowler) + " • " + card.Runs + " (" + (card.Balls + (r.Legal ? 1 : 0)) + ")";
            }
            int before = inn.Bat.TryGetValue(batter.Id, out var bc) ? bc.Runs : 0;
            M.Apply(r);
            int after = inn.Bat.TryGetValue(batter.Id, out var ac) ? ac.Runs : 0;
            Phase = MatchPhase.Result;
            _t = 0;

            if (r.Wicket)
            {
                Hud.ShowBanner(r.Dismissal == Dismissal.RunOut ? "RUN OUT!" : "OUT!", Pal.Red, outText);
                if (r.Dismissal != Dismissal.RunOut) _umpPose = Poses.UmpOut;
                _hype = 1;
                _shake = Math.Max(_shake, 0.5);
                Stadium.SetScreen(ScreenEvent.Out);
                if (UserBatting || inn.Batting.Team.Id == Cfg.UserTeamId) Sfx.I?.Groan();
                else Sfx.I?.Cheer(1);
                Settings.Buzz();
            }
            else if (r.IsSix)
            {
                Hud.ShowBanner("SIX!", Pal.Violet, batter.ShortName + " • " + Mathf.RoundToInt((float)(78 + M.Rng.Next() * 28)) + "m");
                _umpPose = Poses.UmpSix;
                _hype = 1;
                _shake = 0.7;
                Stadium.SetScreen(ScreenEvent.Six);
                Hud.Confetti(UserTeam);
                Sfx.I?.Cheer(1);
            }
            else if (r.IsFour)
            {
                Hud.ShowBanner("FOUR!", Pal.Cyan, batter.ShortName);
                _umpPose = Poses.UmpFour;
                _hype = 0.85;
                Stadium.SetScreen(ScreenEvent.Four);
                Sfx.I?.Cheer(0.8f);
            }
            else if (r.Extra == ExtraType.Wide)
            {
                Hud.ShowBanner("WIDE", Pal.Gold, null);
                _umpPose = Poses.UmpWide;
                Stadium.SetScreen(ScreenEvent.Wide);
            }
            else if (r.Extra == ExtraType.NoBall)
            {
                Hud.ShowBanner("NO BALL", Pal.Gold, "FREE HIT NEXT BALL");
                _umpPose = Poses.UmpNoBall;
                Stadium.SetScreen(ScreenEvent.NoBall);
            }
            else if (r.Total > 0)
            {
                Hud.ShowBanner(r.Total == 1 ? "1 RUN" : r.Total + " RUNS", Pal.Green, null);
            }
            if (!r.Wicket && ((before < 50 && after >= 50) || (before < 100 && after >= 100)))
            {
                Hud.ShowBanner(after >= 100 ? "CENTURY!" : "FIFTY!", Pal.Gold, batter.Name + " • " + after + " off " + inn.Bat[batter.Id].Balls);
                Hud.Confetti(inn.Batting.Team);
                _hype = 1;
                Sfx.I?.Cheer(1);
            }
        }

        void TickResult()
        {
            var r = _result;
            bool big = r != null && (r.Wicket || r.IsSix || r.IsFour);
            if (_t < (big ? 2.1 : 1.15)) return;
            Proceed();
        }

        void Proceed()
        {
            if (M.Finished)
            {
                EnterDone();
            }
            else if (M.InningsList.Count != _inningsCount)
            {
                _inningsCount = M.InningsList.Count;
                Phase = MatchPhase.InningsBreak;
                _following = false;
                _pendingSim = null;
                Hud.ShowInningsBreak();
            }
            else if (M.NeedBowler)
            {
                var inn = M.Current;
                int last = inn.OverRuns.Count == 0 ? 0 : inn.OverRuns[inn.OverRuns.Count - 1];
                Hud.ShowToast("END OF OVER " + inn.OverIndex + " • " + last + " RUN" + (last == 1 ? "" : "S") + " • " + inn.Batting.Team.Id + " " + inn.ScoreText);
                Phase = MatchPhase.OverBreak;
                _t = 0;
            }
            else
            {
                EnterReady();
            }
        }

        public void ContinueAfterInnings()
        {
            if (Phase != MatchPhase.InningsBreak) return;
            EnterReady();
        }

        void EnterDone()
        {
            Phase = MatchPhase.Done;
            _following = false;
            if (_finishedCalled) return;
            _finishedCalled = true;
            Cfg.OnFinished?.Invoke(M);
            if (M.Outcome?.WinnerId == Cfg.UserTeamId)
            {
                Hud.Confetti(UserTeam);
                _hype = 1;
                Sfx.I?.Cheer(1);
            }
            Hud.ShowDone();
        }

        // -------------------------------------------------------------- sims

        public void Sim(Action run)
        {
            if (!CanSim)
            {
                if (SimOffered)
                {
                    _pendingSim = run;
                    Hud.ShowToast("Simulating after this ball…");
                }
                return;
            }
            _pendingSim = null;
            Hud.HideOverlay();
            run();
            if (M.Finished)
            {
                EnterDone();
            }
            else if (M.InningsList.Count != _inningsCount)
            {
                _inningsCount = M.InningsList.Count;
                Phase = MatchPhase.InningsBreak;
                Hud.ShowInningsBreak();
            }
            else
            {
                EnterReady();
            }
        }

        public void QuitAndSimulate()
        {
            if (!M.Started) M.AutoToss();
            M.SimulateToEnd();
            EnterDone();
        }

        // ------------------------------------------------------------ input

        public void PointerDown(Vector2 pos, float uiScale)
        {
            if (UserBowling && Phase == MatchPhase.Ready && !AutoBowl && !MeterOn && !PickingBowler)
            {
                _draggingAim = true;
                MoveAim(pos);
                return;
            }
            if (UserBatting && Settings.Batting == BattingControls.Swipe)
            {
                _swipeStart = pos;
                _swipeStartClock = _clock;
                _swipeFired = false;
                Swipe.Clear();
                Swipe.Add((pos, _clock));
            }
        }

        public void PointerDrag(Vector2 pos, float uiScale)
        {
            if (_draggingAim)
            {
                MoveAim(pos);
                return;
            }
            if (_swipeStart == null) return;
            Swipe.Add((pos, _clock));
            if (Swipe.Count > 26) Swipe.RemoveAt(0);
            if (_swipeFired) return;
            var v = (pos - _swipeStart.Value) / Mathf.Max(0.01f, uiScale);
            if (v.magnitude < 26) return;
            _swipeFired = true;
            double speed = v.magnitude / Math.Max(0.016, _clock - _swipeStartClock);
            double phi = Math.Atan2(v.x, v.y) * 180 / Math.PI;
            PlayShot(phi, LoftToggle || speed > 1800, false);
        }

        public void PointerUp(Vector2 pos, bool tap)
        {
            _draggingAim = false;
            if (tap && UserBatting && Settings.Batting == BattingControls.Swipe && !_swipeFired) PlayShot(null, false, true);
            _swipeStart = null;
        }

        // ------------------------------------------------------------ frame

        void Update()
        {
            double dt = Math.Min(0.05, Time.deltaTime);
            _clock += dt;
            _t += dt;
            _hype = Math.Max(0.15, _hype - dt * 0.3);
            _shake = Math.Max(0, _shake - dt * 1.6);

            switch (Phase)
            {
                case MatchPhase.Toss:
                    if (Flipping)
                    {
                        FlipT += dt;
                        if (FlipT > 1.6) ResolveToss();
                    }
                    break;
                case MatchPhase.Ready:
                    if (MeterOn) _meterT += dt;
                    _batterPose = Pose.Lerp(Poses.BatStance, Poses.BatBacklift, 0.05 + 0.05 * Math.Sin(_clock * 5));
                    if (!PickingBowler && M.Started && !M.Finished && M.Current.BowlerId != null)
                    {
                        if (UserBatting && _t > 1.0) StartDelivery();
                        if (UserBowling && AutoBowl && _t > 0.7) StartDelivery();
                    }
                    break;
                case MatchPhase.RunUp:
                    TickRunUp();
                    break;
                case MatchPhase.Flight:
                    TickFlight();
                    break;
                case MatchPhase.Carry:
                    TickCarry();
                    break;
                case MatchPhase.Follow:
                    TickFollow();
                    break;
                case MatchPhase.Result:
                    TickResult();
                    break;
                case MatchPhase.OverBreak:
                    if (_t > 1.6) EnterReady();
                    break;
            }
            if (_pressClock != null && Phase != MatchPhase.Ready) _batterPose = SwingPose(_clock - _pressClock.Value);
            bool aiming = UserBowling && Phase == MatchPhase.Ready && !AutoBowl && (_draggingAim || _clock - _lastAimTouch < 0.9);
            _aimZoom += ((aiming ? 1.0 : 0.0) - _aimZoom) * (1 - Math.Exp(-dt * 7));
            UpdateFollow(dt);
            for (int i = Swipe.Count - 1; i >= 0; i--)
            {
                if (_clock - Swipe[i].T > 0.5) Swipe.RemoveAt(i);
            }

            Dbg.Mark("Dir.Camera");
            Stadium.SetHype((float)_hype);
            Sfx.I?.SetHype((float)_hype);
            UpdateCamera();
            Dbg.Mark("Dir.SyncActors");
            SyncActors();
            Dbg.Mark("Dir.Props");
            _props.SetBall(_ballPos);
            bool showMap = UserBowling && Phase == MatchPhase.Ready && !AutoBowl && !PickingBowler;
            // Keep the target up while the user's delivery is bowled, to compare with where it lands.
            bool showTarget = showMap || (UserBowling && !AutoBowl && (Phase == MatchPhase.RunUp || Phase == MatchPhase.Flight));
            // White target (user's choice); the length zone under it lights up.
            int aimZone = MatchProps.ZoneIndex(Pitch.Length - Aim.y);
            _props.ShowZones(showMap);
            _props.HighlightZone(showMap ? aimZone : -1);
            _props.ShowAim(showTarget ? Aim : (V3?)null, (float)_clock, Color.white);
        }

        // ----------------------------------------------------------- camera

        Shot BaseShot()
        {
            var s = UserBatting ? Rigs.Batting(M.Striker.LeftBat) : Rigs.Bowling;
            if (!UserBatting && _aimZoom > 0.001) s = Shot.Lerp(s, Rigs.Aim, MathX.EaseInOut(_aimZoom));
            return s;
        }

        void UpdateFollow(double dt)
        {
            if (!_following) return;
            double tf = _clock - _followStartClock;
            var b = _ballPos ?? _look;
            var want = new V3(b.x, b.y, Math.Min(b.z, 8) * 0.55);
            _look = V3.Lerp(_look, want, 1 - Math.Exp(-dt * 3.4));
            _followPos = V3.Lerp(_followStart, _followEnd, MathX.EaseInOut(tf / 1.3));
            double d = _followPos.DistTo(_look);
            double wantK = Math.Clamp(d / 42, 0.95, 2.7);
            _followK += (wantK - _followK) * (1 - Math.Exp(-dt * 2.4));
        }

        void UpdateCamera()
        {
            if (_cam == null) return;
            var shot = BaseShot();
            if (_following)
            {
                var f = new Shot(_followPos, _look, _followK);
                shot = Shot.Lerp(shot, f, MathX.EaseInOut((_clock - _followStartClock) / 0.5));
            }
            shot.ApplyTo(_cam, _shake, _clock);
        }

        // ----------------------------------------------------------- actors

        static Color TrimOf(TeamInfo t)
        {
            var s = Mats.Hex(t.Secondary);
            return s.Luma() < 0.2f ? Pal.Gold : s;
        }

        Cricketer Rig(Player p, TeamInfo team, char role)
        {
            string key = p.Id + ":" + role;
            if (_rigs.TryGetValue(key, out var c)) return c;
            var shirt = Mats.Hex(team.Primary);
            var trim = TrimOf(team);
            // Appearance follows the original generated name, so renaming never changes the look.
            var skin = Look.SkinFor(p.OriginalName);
            var look = role == 'B' ? Look.Batter(shirt, trim, p.Surname, p.Jersey, skin)
                : role == 'K' ? Look.Keeper(shirt, trim, p.Surname, p.Jersey, skin)
                : Look.Fielder(shirt, trim, p.Surname, p.Jersey, skin);
            c = Cricketer.Create(_actorRoot, look, p.Name);
            _rigs[key] = c;
            return c;
        }

        Cricketer Umpire(int n)
        {
            string key = "ump" + n;
            if (_rigs.TryGetValue(key, out var c)) return c;
            c = Cricketer.Create(_actorRoot, Look.Umpire(Look.SkinTones[n == 0 ? 1 : 4]), "Umpire" + n);
            _rigs[key] = c;
            return c;
        }

        void Place(Cricketer c, Pose pose, V3 foot, double facing, bool mirror = false)
        {
            var camPos = _cam != null ? V3.FromUnity(_cam.transform.position) : new V3(0, -10, 5);
            double dist = foot.DistTo(camPos);
            // Hide players right next to the lens (keeper / slips behind the batter's camera).
            bool visible = dist > 4.5;
            if (!visible) return;
            if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
            c.SetDetail(dist < 55);
            c.Apply(pose, foot, facing, mirror);
            _used.Add(c);
        }

        Pose RunPose(double phase) => Pose.Lerp(Poses.RunA, Poses.RunB, 0.5 + 0.5 * Math.Sin(phase));

        void SyncActors()
        {
            _used.Clear();
            if (M.Started)
            {
                var inn = M.Finished ? M.InningsList[M.InningsList.Count - 1] : M.Current;
                var batTeam = inn.Batting.Team;
                var bowlTeam = inn.Bowling.Team;
                var striker = inn.Batting.ById(inn.Striker);
                var nonStriker = inn.Batting.ById(inn.NonStriker);
                double hand = striker.LeftBat ? -1 : 1;
                var strikerPos = new V3(0.18 * hand, 19.32, 0);
                var shot = _shot;
                double tf = _clock - _followStartClock;
                bool showShot = shot != null && (Phase == MatchPhase.Follow || Phase == MatchPhase.Result);
                int? bowlerId = inn.BowlerId ?? inn.LastOverBowler;
                var bowlerP = bowlerId != null ? inn.Bowling.ById(bowlerId.Value) : null;
                int keeperId = inn.Bowling.KeeperId;
                var fieldSide = inn.Bowling.Order.Where(p => p.Id != bowlerId && p.Id != keeperId).ToList();

                // Fielders.
                for (int i = 0; i < _field.Positions.Count && i < fieldSide.Count; i++)
                {
                    var home = _field.Positions[i];
                    var toS = strikerPos - home;
                    double len = Math.Max(1.0, toS.Length);
                    var p = home + toS * (_walk / len);
                    double facing = Math.Atan2(strikerPos.x - p.x, strikerPos.y - p.y);
                    var pose = Poses.FieldReady;
                    if (showShot && shot.Fielder == i && shot.FielderTarget != null)
                    {
                        double arrive = shot.Caught ? shot.Duration * 0.97 : shot.Duration * 0.9;
                        double u = MathX.Clamp01(tf / arrive), e = u * u * (3 - 2 * u);
                        var dest = shot.Four ? V3.Lerp(p, shot.FielderTarget.Value, 0.85) : shot.FielderTarget.Value;
                        bool moving = (dest - p).Length > 1;
                        if (moving) facing = Math.Atan2(dest.x - p.x, dest.y - p.y);
                        p = V3.Lerp(p, dest, e);
                        pose = u < 1 && moving ? RunPose(_clock * 13 + i) : (shot.Caught ? Poses.Catch : Poses.Throw);
                    }
                    Place(Rig(fieldSide[i], bowlTeam, 'F'), pose, p, facing);
                }

                // Keeper.
                Place(Rig(inn.Bowling.ById(keeperId), bowlTeam, 'K'), _keeperPose, _field.Keeper, Math.PI);

                // Bowler.
                if (bowlerP != null) Place(Rig(bowlerP, bowlTeam, 'F'), _bowlerPose, new V3(_bowlerX, _bowlerY, 0), 0, bowlerP.BowlStyle.LeftArm());

                // Batters.
                int runs = showShot && !shot.Four && !shot.Six ? shot.Runs : 0;
                double armSide = bowlerP != null && bowlerP.BowlStyle.LeftArm() ? -1 : 1;
                bool runOut = showShot && shot.RunOut;
                if (runs > 0 || runOut)
                {
                    double progress = MathX.Clamp01(tf / (1.1 * Math.Max(1, runs))) * Math.Max(1, runs);
                    if (runOut) progress = Math.Min(progress, 0.85);
                    int leg = (int)Math.Floor(progress);
                    double frac = progress - leg;
                    double EndY(bool st, int l) => (st ? l % 2 == 0 : l % 2 == 1) ? 19.3 : 1.0;
                    foreach (bool st in new[] { true, false })
                    {
                        double from = EndY(st, leg);
                        double to = leg >= Math.Max(1, runs) ? from : EndY(st, leg + 1);
                        double y = from + (to - from) * (leg >= Math.Max(1, runs) ? 0 : frac);
                        double x = (st ? 0.7 : -0.7) * hand;
                        var who = st ? striker : nonStriker;
                        var run = RunPose(_clock * 13 + (st ? 0 : 2)).WithBat(BatHold.Right, new V3(0.12, 0.55, -0.82));
                        Place(Rig(who, batTeam, 'B'), run, new V3(x, y, 0), to > from ? 0 : Math.PI);
                    }
                }
                else
                {
                    Place(Rig(striker, batTeam, 'B'), _batterPose, strikerPos, striker.LeftBat ? Math.PI / 2 : -Math.PI / 2, striker.LeftBat);
                    Place(Rig(nonStriker, batTeam, 'B'), Poses.NonStriker, new V3(2.1 * armSide, _nsY + 0.6, 0), 0, nonStriker.LeftBat);
                }

                // Umpires.
                Place(Umpire(0), _umpPose, new V3(1.15 * armSide, -2.4, 0), 0);
                Place(Umpire(1), Poses.Umpire, new V3(22 * hand, 19.4, 0), hand > 0 ? -Math.PI / 2 : Math.PI / 2);
            }
            foreach (var c in _rigs.Values)
            {
                if (!_used.Contains(c) && c.gameObject.activeSelf) c.gameObject.SetActive(false);
            }
        }
    }
}
