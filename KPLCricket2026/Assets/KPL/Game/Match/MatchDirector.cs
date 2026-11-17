using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Runs a live 3D match on top of the rules engine: toss, overs, deliveries, the user's batting
    /// or bowling input, the fielding script (chases, catches, throws, run-outs), umpire signals,
    /// crowd, cameras and the result.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        public MatchContext Ctx;
        public MatchState M;
        public BallEngine E;
        public Stadium S;
        public Hud Hud;
        public CameraRig Cam;
        BallView _ball;
        Transform _marker;
        Material _markerMat;
        readonly Dictionary<int, Actor> _field = new Dictionary<int, Actor>();
        readonly Dictionary<int, Actor> _bat = new Dictionary<int, Actor>();
        Actor _umpire, _squareUmp;
        string _fieldTeam = "", _batTeam = "";
        int _userCatches;
        bool _skip, _quit;
        enum SimReq { None, Over, Innings, Match }
        SimReq _sim;
        public bool Paused { get; private set; }
        public bool InPlay { get; private set; }
        public int BallsBowled { get; private set; }

        // User bowling choices.
        DeliveryKind _kind = DeliveryKind.Stock;
        double _line = 0.15, _length = 6;
        bool _bowlPressed, _autoBowl;
        public bool SettingTarget { get; private set; }

        public bool UserBatting => E.UserBatting && (!Ctx.Practice || Ctx.NetsBat);
        public bool UserBowling => E.UserBowling && (!Ctx.Practice || !Ctx.NetsBat);
        bool CatchPrompts => UserBowling && SaveSystem.Data.Settings.CatchPrompts;

        float Slow
        {
            get
            {
                switch (M.Setup.Difficulty)
                {
                    case Difficulty.Easy: return 0.68f;
                    case Difficulty.Medium: return 0.78f;
                    case Difficulty.Hard: return 0.88f;
                    default: return 0.96f;
                }
            }
        }

        public static MatchDirector Create(MatchContext ctx, Transform world, RectTransform hudRoot, Stadium s)
        {
            var go = new GameObject("Match");
            go.transform.SetParent(world, false);
            var md = go.AddComponent<MatchDirector>();
            md.Ctx = ctx;
            md.S = s;
            md.M = new MatchState(ctx.Setup);
            md.E = new BallEngine(md.M);
            md.E.SetBoosts(ctx.UserId, Economy.Boosts(SaveSystem.Data));
            md.Cam = CameraRig.Attach(SceneLook.MainCamera());
            md._ball = BallView.Create(go.transform);
            md.Cam.Ball = md._ball.Ball;
            md.MakeMarker();
            md.Hud = Hud.Create(hudRoot, md);
            md.StartCoroutine(md.Run());
            return md;
        }

        void MakeMarker()
        {
            _markerMat = Mats.UnlitTransparent(new Color(1, 0.8f, 0.2f, 0.9f), Tex.RingMark);
            var q = MeshKit.Spawn("PitchMarker", MeshKit.Quad(0.55f, 0.55f), transform, _markerMat);
            q.transform.rotation = Quaternion.Euler(90, 0, 0);
            MeshKit.NoShadows(q);
            _marker = q.transform;
            _marker.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            foreach (var a in _field.Values) if (a != null) Destroy(a.gameObject);
            foreach (var a in _bat.Values) if (a != null) Destroy(a.gameObject);
            if (_umpire != null) Destroy(_umpire.gameObject);
            if (_squareUmp != null) Destroy(_squareUmp.gameObject);
            if (Hud != null) Destroy(Hud.gameObject);
        }

        // ------------------------------------------------------------- flow

        IEnumerator Run()
        {
            Audio.Crowd(SceneLook.IsNight ? 0.5f : 0.42f);
            MakeUmpires();
            Cam.OrbitCenter = Vector3.zero;
            Cam.OrbitRadius = 58;
            Cam.OrbitHeight = 14;
            Cam.OrbitLookY = 6;
            Cam.OrbitFov = 44;
            Cam.Set(CamMode.Orbit, true);
            S.SetScreen(M.Setup.Home.Team.Id + " v " + M.Setup.Away.Team.Id, M.Setup.Title);
            yield return Hud.Intro(this);
            if (M.IsScenario) M.StartScenario();
            else yield return Toss();
            while (!M.Finished && !_quit)
            {
                var inn = M.Current;
                SetupInnings(inn);
                yield return InningsIntro(inn);
                while (!M.Finished && M.Current == inn && !_quit)
                {
                    if (_sim != SimReq.None)
                    {
                        DoSim();
                        continue;
                    }
                    yield return Ball();
                    if (Ctx.Practice && !_quit) Hud.Refresh(M);
                }
                if (!M.Finished && !_quit) yield return InningsBreak(inn);
            }
            Time.timeScale = 1;
            if (_quit)
            {
                GameApp.I.MatchEnded(Ctx, M, _userCatches, !Ctx.Practice);
                yield break;
            }
            yield return Finale();
            GameApp.I.MatchEnded(Ctx, M, _userCatches, false);
        }

        void DoSim()
        {
            var req = _sim;
            _sim = SimReq.None;
            var inn = M.Current;
            if (req == SimReq.Over) E.SimOver();
            else if (req == SimReq.Innings) E.SimInnings();
            else E.SimToEnd();
            Hud.Refresh(M);
            Hud.Commentary("Simulated " + (req == SimReq.Over ? "the over" : req == SimReq.Innings ? "the innings" : "to the end") + ".  " + inn.Bat.Team.Id + " " + inn.Score + " (" + inn.OversText + ")");
            S.ResetStumps();
        }

        public void RequestSim(int which)
        {
            _sim = which == 0 ? SimReq.Over : which == 1 ? SimReq.Innings : SimReq.Match;
            SetPaused(false);
            _skip = true;
        }

        public void Quit()
        {
            _quit = true;
            SetPaused(false);
            _skip = true;
            StopAllCoroutines();
            Time.timeScale = 1;
            GameApp.I.MatchEnded(Ctx, M, _userCatches, !Ctx.Practice);
        }

        public void EndPractice()
        {
            _quit = true;
            SetPaused(false);
            StopAllCoroutines();
            Time.timeScale = 1;
            GameApp.I.MatchEnded(Ctx, M, _userCatches, false);
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0 : 1;
            Hud.ShowPause(p);
        }

        public void OnBack()
        {
            if (Hud.CloseModal()) return;
            SetPaused(!Paused);
        }

        public void OnAppPaused()
        {
            if (!Paused && !M.Finished) SetPaused(true);
        }

        public void Skip()
        {
            if (InPlay) _skip = true;
        }

        IEnumerator Toss()
        {
            Cam.FocusPos = new Vector3(2.6f, 1.7f, 3.8f);
            Cam.FocusLook = new Vector3(0, 0.6f, 0);
            Cam.FocusFov = 40;
            Cam.Set(CamMode.Toss);
            bool? call = null;
            Hud.TossCall(h => call = h);
            while (call == null) yield return null;
            bool heads = M.Rng.Chance(0.5);
            bool userWon = heads == call.Value;
            yield return Hud.TossFlip(heads);
            var user = M.Sheet(Ctx.UserId).Team;
            var ai = M.Other(Ctx.UserId).Team;
            if (userWon)
            {
                bool? bat = null;
                Hud.TossChoose(user, b => bat = b);
                while (bat == null) yield return null;
                M.SetToss(user.Id, bat.Value);
            }
            else
            {
                bool aiBat = M.Rng.Chance(M.Setup.Venue.PitchSpin > 1.05 ? 0.6 : 0.4);
                M.SetToss(ai.Id, aiBat);
                yield return Hud.Message(ai.Name.ToUpperInvariant() + " WON THE TOSS", "They chose to " + (aiBat ? "bat" : "bowl") + " first", 2.2f);
            }
            Hud.Commentary(M.TossText + ".");
        }

        IEnumerator InningsIntro(Innings inn)
        {
            Hud.Refresh(M);
            string big, small;
            if (inn.SuperOver)
            {
                big = "SUPER OVER";
                small = inn.Bat.Team.Name + (inn.Target != null ? " need " + inn.Target + " to win" : " bat first");
            }
            else if (M.IsScenario)
            {
                big = Ctx.Challenge != null ? Ctx.Challenge.Title.ToUpperInvariant() : Ctx.Practice ? "NETS SESSION" : "CHALLENGE";
                small = Ctx.Challenge != null ? Ctx.Challenge.GoalText : Ctx.Practice ? (Ctx.NetsBat ? "Bat freely - pause to end the session" : "Bowl freely - pause to end the session") : "";
            }
            else if (inn.Target != null)
            {
                big = inn.Bat.Team.Name.ToUpperInvariant();
                small = "need " + inn.Target + " runs from " + inn.MaxOvers + " overs";
            }
            else
            {
                big = inn.Bat.Team.Name.ToUpperInvariant();
                small = "to bat first  •  " + inn.MaxOvers + " overs";
            }
            S.SetScreen(inn.Bat.Team.Id + " " + inn.Score, small);
            Cam.Set(CamMode.Orbit);
            yield return Hud.Message(big, small, 2.4f);
        }

        IEnumerator InningsBreak(Innings done)
        {
            Audio.Play(Sfx.Applause, 0.7f);
            bool cont = false;
            Hud.InningsBreak(M, done, () => cont = true);
            Cam.Set(CamMode.Orbit);
            float t = 0;
            while (!cont && !(AutoPilot.Active && (t += Time.unscaledDeltaTime) > 2.5f)) yield return null;
            Hud.CloseModal();
        }

        IEnumerator Finale()
        {
            var r = M.Result;
            bool scenario = M.IsScenario;
            bool userWon = r.WinnerId == Ctx.UserId;
            if (Ctx.Challenge != null) userWon = Ctx.Challenge.Evaluate(M, Ctx.UserId) > 0;
            var winners = r.WinnerId == null ? null : r.WinnerId;
            foreach (var a in _field.Values.Concat(_bat.Values))
            {
                if (a == null) continue;
                bool w = winners != null && (M.Sheet(winners).Has(PlayerIdOf(a)));
                float off = UnityEngine.Random.value * 3;
                if (w) a.Proc(t => Poses.Celebrate(t + off));
                else a.Hold(Poses.Dejected(), 0.4f);
            }
            if (userWon || (!scenario && r.WinnerId == Ctx.UserId))
            {
                Audio.Play(Sfx.Fanfare);
                Audio.Play(Sfx.Dhol, 0.7f);
                Fx.Fireworks(transform, SceneLook.IsNight ? 10 : 5);
            }
            Audio.CrowdSwell(1f, 4);
            S.Cheer(5);
            S.FlashScreen(r.WinnerId != null ? Teams.ById(r.WinnerId).Id + " WIN!" : "TIED", Pal.Gold, 6);
            Cam.OrbitRadius = 22;
            Cam.OrbitHeight = 4;
            Cam.OrbitLookY = 3;
            Cam.OrbitFov = 42;
            Cam.OrbitCenter = new Vector3(0, 0, -4);
            Cam.Set(CamMode.Orbit);
            yield return Hud.Message(userWon ? "VICTORY" : r.WinnerId == null ? "MATCH TIED" : "DEFEAT", r.Text, 3.2f);
        }

        int PlayerIdOf(Actor a)
        {
            foreach (var kv in _field) if (kv.Value == a) return kv.Key;
            foreach (var kv in _bat) if (kv.Value == a) return kv.Key;
            return -1;
        }

        // ----------------------------------------------------------- actors

        void MakeUmpires()
        {
            var look = new Look { Outfit = Outfit.Umpire, Team = M.Setup.Venue, Skin = 2, Number = 1, Build = 2 };
            _umpire = Actor.Create(transform, look, "Umpire", new Vector3(-0.7f, 0, (float)Pitch.StumpsZ + 1.7f), 180);
            _umpire.Hold(Poses.UmpireStand(), 0);
            var look2 = new Look { Outfit = Outfit.Umpire, Team = M.Setup.Venue, Skin = 4, Number = 2, Build = 1 };
            _squareUmp = Actor.Create(transform, look2, "SquareLegUmpire", new Vector3(-22, 0, (float)Pitch.StrikerCreaseZ), 90);
            _squareUmp.Hold(Poses.UmpireStand(), 0);
        }

        void SetupInnings(Innings inn)
        {
            if (_fieldTeam != inn.Bowl.Team.Id)
            {
                foreach (var a in _field.Values) if (a != null) Destroy(a.gameObject);
                _field.Clear();
                foreach (var p in inn.Bowl.XI)
                {
                    var outfit = p.Id == inn.Bowl.KeeperId ? Outfit.Keeper : Outfit.Fielder;
                    var a = Actor.Create(transform, Look.For(p, inn.Bowl.Team, outfit), p.Last, new Vector3(0, 0, 40), 180);
                    _field[p.Id] = a;
                }
                _fieldTeam = inn.Bowl.Team.Id;
            }
            if (_batTeam != inn.Bat.Team.Id)
            {
                foreach (var a in _bat.Values) if (a != null) Destroy(a.gameObject);
                _bat.Clear();
                _batTeam = inn.Bat.Team.Id;
            }
            EnsureBatters(inn);
        }

        void EnsureBatters(Innings inn)
        {
            var need = new[] { inn.StrikerCard.P, inn.NonStrikerCard.P };
            foreach (var kv in _bat.ToList())
            {
                if (!need.Any(p => p.Id == kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    _bat.Remove(kv.Key);
                }
            }
            foreach (var p in need)
            {
                if (_bat.ContainsKey(p.Id)) continue;
                var a = Actor.Create(transform, Look.For(p, inn.Bat.Team, Outfit.Batter), p.Last, new Vector3(0, 0, 0), 0);
                a.Mirror = p.LeftBat;
                a.CarryBat = true;
                _bat[p.Id] = a;
            }
        }

        Actor StrikerActor => _bat[M.Current.StrikerCard.P.Id];
        Actor NonStrikerActor => _bat[M.Current.NonStrikerCard.P.Id];

        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        Vector3 StrikerRoot(Player p) => new Vector3(p.LeftBat ? 0.5f : -0.5f, 0, (float)Pitch.StrikerCreaseZ - 0.05f);
        float StrikerYaw(Player p) => p.LeftBat ? -90 : 90;

        /// <summary>Puts everyone in place for the next delivery (walking if they are close, snapping otherwise).</summary>
        void PlaceForDelivery(bool snap)
        {
            var inn = M.Current;
            var striker = inn.StrikerCard.P;
            var sa = StrikerActor;
            sa.Mirror = striker.LeftBat;
            sa.Pos = StrikerRoot(striker);
            sa.Yaw = StrikerYaw(striker);
            sa.Proc(t => BatAnim.Stance(t), snap ? 0 : 0.3f);
            var na = NonStrikerActor;
            bool rightArm = !inn.Bowler.LeftArm;
            na.Mirror = false;
            na.Pos = new Vector3(rightArm ? -1.25f : 1.25f, 0, (float)Pitch.CreaseZ + 0.2f);
            na.Yaw = 180;
            na.Hold(Poses.Stand(), snap ? 0 : 0.3f);
            var striker3 = new Vector3(0, 0, (float)Pitch.StrikerCreaseZ);
            var side = E.Side;
            for (int i = 0; i < 9; i++)
            {
                var a = _field[side.Players[i].Id];
                var pos = ToV(side.Pos[i]);
                a.Mirror = false;
                if (snap || (a.Pos - pos).magnitude > 25) a.Pos = pos;
                if ((a.Pos - pos).magnitude > 0.3f) a.MoveTo(pos, 5.5f, YawTo(pos, striker3), null, Poses.Idle(0));
                else
                {
                    a.Pos = pos;
                    a.Yaw = YawTo(pos, striker3);
                    float off = i * 0.7f;
                    a.Proc(t => Poses.Idle(t + off), 0.3f);
                }
                a.Body.HeadGear?.gameObject.SetActive(true);
            }
            var keeper = _field[inn.Bowl.KeeperId];
            keeper.Pos = ToV(side.Pos[9]);
            keeper.Yaw = 0;
            keeper.Hold(Poses.KeeperCrouch(), snap ? 0 : 0.3f);
            var bowler = _field[inn.Bowler.Id];
            bowler.Body.HeadGear?.gameObject.SetActive(false);
            bowler.Mirror = inn.Bowler.LeftArm;
            bowler.Pos = RunupStart(inn.Bowler);
            bowler.Yaw = 180;
            bowler.Hold(Poses.Stand(), 0);
            _umpire.Pos = new Vector3(rightArm ? -0.75f : 0.75f, 0, (float)Pitch.StumpsZ + 1.7f);
            _umpire.Yaw = 180;
            _umpire.Hold(Poses.UmpireStand(), 0.2f);
            float leg = striker.LeftBat ? 1 : -1;
            _squareUmp.Pos = new Vector3(leg * 22, 0, (float)Pitch.StrikerCreaseZ);
            _squareUmp.Yaw = leg > 0 ? -90 : 90;
            _squareUmp.Hold(Poses.UmpireStand(), 0.2f);
        }

        static Vector3 ToV(V3 v) => new Vector3((float)v.x, (float)v.y, (float)v.z);

        Vector3 RunupStart(Player b)
        {
            bool spin = b.IsSpin;
            float x = (b.LeftArm ? -1 : 1) * 0.55f;
            return new Vector3(x, 0, (float)Pitch.StumpsZ + (spin ? 8 : 19) + BatAnim.StrideToRelease(spin));
        }

        // ------------------------------------------------------------- ball

        IEnumerator Ball()
        {
            var inn = M.Current;
            bool newOver = false;
            if (M.NeedBowler)
            {
                newOver = true;
                if (UserBowling)
                {
                    Player chosen = null;
                    var options = M.EligibleBowlers();
                    var suggest = AiBowling.PickBowler(M, new Rng(inn.Balls + 7));
                    Hud.PickBowler(M, options, suggest, p => chosen = p);
                    while (chosen == null && !_quit) yield return null;
                    if (_quit) yield break;
                    M.SetBowler(chosen);
                }
                else E.EnsureBowler();
            }
            E.PrepareField();
            EnsureBatters(inn);
            PlaceForDelivery(newOver || BallsBowled == 0);
            Hud.Refresh(M);
            S.SetScreen(inn.Bat.Team.Id + " " + inn.Score + " (" + inn.OversText + ")", inn.Target != null ? "NEED " + inn.RunsNeeded + " OFF " + inn.BallsLeft : inn.Bowler.Short.ToUpperInvariant());
            var batCam = UserBatting && SaveSystem.Data.Settings.Camera == 1 ? CamMode.BehindBatter : CamMode.Broadcast;
            Cam.Set(batCam, newOver || BallsBowled == 0);
            S.ResetStumps();
            _ball.Hide();
            _marker.gameObject.SetActive(false);
            if (newOver && inn.Balls > 0)
            {
                Hud.Commentary("New over: " + inn.Bowler.Name + " (" + inn.Bowler.Bowl.Label() + ")");
                yield return Wait(0.6f);
            }

            var bowler = inn.Bowler;
            bool spin = bowler.IsSpin;
            var bowlActor = _field[bowler.Id];

            // --- choose the delivery
            DeliveryPlan plan = null;
            if (UserBowling)
            {
                yield return UserBowlSetup(bowler);
                if (_quit) yield break;
            }
            Hud.SetBatting(UserBatting, M);

            // --- run-up
            float runSpeed = spin ? 3.6f : 6.8f;
            var gather = new Vector3(bowlActor.Pos.x * 0.6f, 0, (float)Pitch.StumpsZ - 1.0f + BatAnim.StrideToRelease(spin) + 0.6f);
            bowlActor.MoveTo(gather, runSpeed, 180);
            float meterAcc = -1;
            if (UserBowling && !_autoBowl) Hud.StartMeter(a => meterAcc = a);
            foreach (var kv in _field)
            {
                if (kv.Key == bowler.Id || kv.Key == inn.Bowl.KeeperId) continue;
                var a = kv.Value;
                if (a.Moving) continue;
                var fwd = (new Vector3(0, 0, (float)Pitch.StrikerCreaseZ) - a.Pos).normalized;
                if ((a.Pos - new Vector3(0, 0, (float)Pitch.StrikerCreaseZ)).magnitude > 15) a.MoveTo(a.Pos + fwd * 1.8f, 1.5f, a.Yaw, null, Poses.Ready());
            }
            NonStrikerActor.MoveTo(NonStrikerActor.Pos + new Vector3(0, 0, -1.2f), 1.4f, 180, null, Poses.Stand());
            float guard = 0;
            while (bowlActor.Moving && (guard += Time.deltaTime) < 8) yield return null;
            if (UserBowling && !_autoBowl && meterAcc < 0) meterAcc = Hud.StopMeter();
            var clip = BatAnim.Deliver(spin);
            bowlActor.Play(bowler.LeftArm ? clip : clip, 1);
            // The striker's trigger movement and backlift.
            var striker = inn.StrikerCard.P;
            var sa = StrikerActor;
            sa.Hold(BatAnim.Backlift(false), 0.35f);
            float rel = BatAnim.ReleaseTime(spin);
            guard = 0;
            while (bowlActor.ClipTime < rel && (guard += Time.deltaTime) < 3) yield return null;

            // --- release
            if (UserBowling && !_autoBowl)
            {
                var b = E.BoostsFor(Ctx.UserId);
                plan = new DeliveryPlan { Kind = _kind, Line = _line, Length = _length, Accuracy = Mathf.Clamp01(meterAcc) * (0.9f + (float)b.AccuracyBonus) + (float)b.AccuracyBonus * 0.5f, Effort = Mathf.Clamp01(meterAcc) };
                Hud.ShowAccuracy(meterAcc);
            }
            else plan = E.AiPlan();
            var tr = E.BuildTrack(plan);
            var hand = bowlActor.HandPos(!bowler.LeftArm);
            _ball.Show(hand);
            AutoPilot.Tag(UserBatting ? "release_bat" : "release_bowl");
            Audio.Play(Sfx.Whoosh, 0.35f, spin ? 0.8f : 1.1f);
            Hud.Speed(tr.SpeedKph, plan.Kind.Label(tr.BowlType));
            bool assist = SaveSystem.Data.Settings.TimingAssist && UserBatting;
            if (assist && !tr.FullToss)
            {
                _marker.position = new Vector3((float)tr.Bounce.x, 0.02f, (float)tr.Bounce.z);
                _markerMat.color = LengthColor(tr.LengthClass);
                _marker.gameObject.SetActive(true);
            }
            if (UserBowling) Hud.HideBowling();

            // --- flight to the batter
            float slow = Slow;
            float contactPres = (float)tr.TContact / slow;
            ShotInput shot = default;
            bool committed = false;
            float pressPres = -1;
            Play play = null;
            Vector3 lateOffset = Vector3.zero;
            if (!UserBatting)
            {
                shot = E.AiShot(tr);
                committed = true;
                pressPres = contactPres + (float)(shot.TimingMs ?? 0) / 1000f;
                play = E.Resolve(tr, shot, false);
                ScheduleSwing(sa, striker, tr, play, pressPres, contactPres);
            }
            else Hud.ArmShot(true);
            float tPres = 0;
            bool bounced = false;
            var windows = E.UserWindows;
            float windowEnd = contactPres + (float)windows.Ok * 1.7f / 1000f;
            InPlay = true;
            while (true)
            {
                tPres += Time.deltaTime;
                float tReal = tPres * slow;
                if (UserBatting && !committed)
                {
                    var req = AutoPilot.Active ? AutoPilot.BatDecision(tPres, contactPres) : Hud.ConsumeShot();
                    if (tPres > contactPres * 0.6f) AutoPilot.Tag("delivery");
                    if (req != null)
                    {
                        committed = true;
                        pressPres = tPres;
                        shot = req.Value;
                        if (double.IsNaN(shot.Azimuth)) shot.Azimuth = NaturalAz(tr, shot.Kind);
                        shot.TimingMs = (pressPres - contactPres) * 1000.0 - SaveSystem.Data.Settings.TimingOffset;
                        play = E.Resolve(tr, shot, true);
                        Hud.ShowGrade(play.Contact.Grade, play.Contact.ShotName);
                        Hud.ArmShot(false);
                        if (tPres > contactPres) lateOffset = _ball.Ball.position - ToV(play.Contact.Pos);
                        ScheduleSwing(sa, striker, tr, play, pressPres, contactPres);
                    }
                    else if (tPres > windowEnd)
                    {
                        committed = true;
                        shot = ShotInput.Leave;
                        play = E.Resolve(tr, shot, true);
                        Hud.ArmShot(false);
                        sa.Hold(BatAnim.Leave(), 0.25f);
                    }
                    Hud.Timing(Mathf.Clamp01(tPres / contactPres), true);
                }
                bool batHit = play != null && play.Contact.BatHit;
                float tHit = Mathf.Max(contactPres, pressPres);
                if (committed && batHit && tPres >= tHit) break;
                if (committed && !batHit && tPres >= contactPres) break;
                var p = ToV(tr.PosAt(Math.Min(tReal, tr.TStumps + 0.4)));
                if (tPres < 0.07f) p = Vector3.Lerp(hand, p, tPres / 0.07f);
                _ball.Move(p);
                if (!bounced && !tr.FullToss && tReal >= tr.TBounce)
                {
                    bounced = true;
                    Audio.Play(Sfx.Bounce, 0.6f);
                    Fx.Dust(transform, p);
                    _marker.gameObject.SetActive(false);
                }
                yield return null;
            }
            Hud.Timing(1, false);
            _marker.gameObject.SetActive(false);

            // --- the play
            yield return RunPlay(tr, play, slow, contactPres, lateOffset);
            InPlay = false;
            _skip = false;
            if (_quit) yield break;

            // --- record
            var before = inn.Score;
            var outCard = play.Wicket != null ? (play.OutBatter == 0 ? inn.StrikerCard : inn.NonStrikerCard) : null;
            var rec = E.Commit(play);
            BallsBowled++;
            yield return Reactions(play, rec, outCard?.P);
        }

        /// <summary>With the stick centred the batter plays the shot that suits the ball best.</summary>
        static double NaturalAz(DeliveryTrack tr, ShotKind kind)
        {
            double best = 0, bs = double.MinValue;
            foreach (var az in new[] { 0.0, 40, 90, -40, -90, 135, -140 })
            {
                double s = Batting.Suitability(tr, kind, az) - (Math.Abs(az) > 120 ? 0.25 : 0);
                if (s > bs)
                {
                    bs = s;
                    best = az;
                }
            }
            return best;
        }

        static Color LengthColor(LengthClass l)
        {
            switch (l)
            {
                case LengthClass.Yorker: return new Color(1f, 0.35f, 0.35f, 0.95f);
                case LengthClass.Full: return new Color(0.4f, 1f, 0.5f, 0.95f);
                case LengthClass.Good: return new Color(1f, 0.85f, 0.3f, 0.95f);
                case LengthClass.Short: return new Color(1f, 0.55f, 0.2f, 0.95f);
                default: return new Color(0.75f, 0.45f, 1f, 0.95f);
            }
        }

        /// <summary>Backlift held, downswing timed so the bat arrives at the ball on contact.</summary>
        void ScheduleSwing(Actor sa, Player striker, DeliveryTrack tr, Play play, float pressPres, float contactPres)
        {
            var c = play.Contact;
            var shot = c.Input;
            var rootPos = StrikerRoot(striker);
            float yaw = StrikerYaw(striker);
            var world = ToV(c.Pos);
            var local = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * (world - rootPos);
            if (striker.LeftBat) local.x = -local.x;
            if (shot.Kind == ShotKind.Leave || shot.TimingMs == null)
            {
                sa.Hold(BatAnim.Leave(), 0.25f);
                return;
            }
            bool spin = tr.BowlType.IsSpin();
            bool horizontal = BatAnim.IsHorizontal(shot.Kind, shot.Azimuth, tr.LengthClass, local.y, spin);
            bool sweep = horizontal && spin && shot.Azimuth < -60 && local.y < 0.9f;
            var contact = BatAnim.Contact(local, shot.Kind, shot.Azimuth, tr.FrontFoot, horizontal, sweep);
            var follow = BatAnim.Follow(contact, shot.Kind, shot.Azimuth, horizontal);
            var back = BatAnim.Backlift(horizontal);
            float visualHit = Mathf.Max(pressPres + 0.04f, contactPres + Mathf.Clamp((pressPres - contactPres) * 0.5f, -0.12f, 0.15f));
            float startProc = Time.time;
            float wait = Mathf.Max(0, visualHit - 0.16f - 0);
            // Times relative to when the proc starts (now).
            float now = 0;
            float swingStart = wait, hit = wait + 0.16f, end = hit + 0.3f;
            sa.Proc(t =>
            {
                if (t < swingStart) return back;
                if (t < hit) return Pose.Lerp(back, contact, Mathf.SmoothStep(0, 1, (t - swingStart) / (hit - swingStart)));
                if (t < end) return Pose.Lerp(contact, follow, Mathf.SmoothStep(0, 1, (t - hit) / (end - hit)));
                return follow;
            }, 0.12f);
            _ = startProc;
            _ = now;
        }

        // ------------------------------------------------------------ play

        IEnumerator RunPlay(DeliveryTrack tr, Play play, float slow, float contactPres, Vector3 lateOffset)
        {
            var c = play.Contact;
            var inn = M.Current;
            var side = E.Side;
            _running[0] = _running[1] = false;
            Actor Fielder(int idx)
            {
                if (idx < 0) return null;
                if (idx == 9) return _field[inn.Bowl.KeeperId];
                if (idx == 10) return _field[inn.Bowler.Id];
                return _field[side.Players[idx].Id];
            }
            var keeper = _field[inn.Bowl.KeeperId];
            var bowlerA = _field[inn.Bowler.Id];
            var striker = StrikerActor;
            var non = NonStrikerActor;
            bool catchResolved = !play.CatchChance;
            bool caught = play.Wicket != null && (play.Wicket == WicketKind.Caught || play.Wicket == WicketKind.CaughtBehind || play.Wicket == WicketKind.CaughtAndBowled);
            bool askedCatch = false;

            if (c.BatHit)
            {
                Audio.Play(c.Type == ContactType.Clean && c.Input.Kind != ShotKind.Defend ? (play.Six || play.Four ? Sfx.BatBig : Sfx.Bat) : c.Type == ContactType.Defended ? Sfx.Defend : Sfx.Edge,
                    1, UnityEngine.Random.Range(0.95f, 1.05f));
                if (play.Six) Cam.Shake(0.05f);
            }

            // Chaser and running scripts.
            var chaser = Fielder(play.Chaser);
            bool chaseStarted = false, chaseArrived = false, thrown = false, picked = false, catchPosed = false;
            float runStart = c.Type == ContactType.Defended ? 0.6f : 0.45f;
            bool runners = play.RunTimes.Length > 0 && !play.Four && !play.Six;
            var strikerStart = striker.Pos;
            var nonStart = non.Pos;
            Vector3 ballPos = _ball.Ball.position;
            float t = 0;
            bool camFollow = false;
            bool bowled = play.Wicket == WicketKind.Bowled && !c.BatHit;
            bool stumpsBroken = false;
            float lbwStop = -1;
            bool keeperTook = false;
            float duration = (float)play.Duration;
            Vector3 keeperHands = keeper.Pos + new Vector3(0, 0.6f, 0.5f);
            while (t < duration && !_quit)
            {
                float dt = Time.deltaTime * (_skip ? 4f : 1f);
                t += dt;
                // Ball.
                if (c.BatHit && play.Flight != null)
                {
                    float ft = t;
                    if (play.CatchChance && caught && ft >= play.CatchTime) ballPos = chaser != null ? chaser.HandPos() : ballPos;
                    else if (play.Thrown && t >= play.ThrowStart)
                    {
                        var target = play.ThrowEnd == 0 ? new Vector3(0, 0.75f, (float)Pitch.StrikerStumpsZ - 0.4f) : new Vector3(0, 0.75f, (float)Pitch.StumpsZ + 0.4f);
                        var from = chaser != null ? ToV(play.PickPos) + Vector3.up * 1.6f : ballPos;
                        float k = Mathf.Clamp01((t - (float)play.ThrowStart) / Mathf.Max(0.1f, (float)(play.ThrowArrive - play.ThrowStart)));
                        float arc = Vector3.Distance(from, target) * 0.08f;
                        ballPos = Vector3.Lerp(from, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                        if (!thrown)
                        {
                            thrown = true;
                            Audio.Play(Sfx.Whoosh, 0.4f, 1.2f);
                            if (chaser != null) chaser.Play(Poses.Throw(), 1.2f);
                        }
                        if (k >= 1 && play.Wicket == WicketKind.RunOut && !stumpsBroken)
                        {
                            stumpsBroken = true;
                            S.BreakStumps(play.ThrowEnd == 0, new Vector3(0, 0, play.ThrowEnd == 0 ? -1 : 1));
                            Audio.Play(Sfx.Stumps);
                        }
                    }
                    else if (play.PickTime >= 0 && t >= play.ChaseArrive) ballPos = t >= play.PickTime && chaser != null ? chaser.HandPos() : ToV(play.PickPos) + Vector3.up * 0.05f;
                    else
                    {
                        ballPos = ToV(play.Flight.At(ft));
                        float fade = Mathf.Clamp01(1 - ft / 0.3f);
                        ballPos += lateOffset * fade;
                    }
                    if (play.Flight != null && t > play.Flight.Duration && (play.Six || play.Four)) _ball.Hide();
                    if (!camFollow && t > 0.18f && (play.Flight.Carry > 14 || ToV(play.Flight.At(1.0)).magnitude > 18))
                    {
                        camFollow = true;
                        Cam.Set(CamMode.Follow);
                    }
                    if (t > 0.12f && t < 0.2f) AutoPilot.Tag("contact");
                    if (camFollow && t > 1.2f) AutoPilot.Tag(play.Six ? "six_flight" : "follow_cam");
                }
                else
                {
                    // Missed or left: the ball carries on to the stumps or the keeper.
                    float tReal = (contactPres + t) * slow;
                    var p = ToV(tr.PosAt(tReal));
                    if (bowled && p.z <= (float)Pitch.StrikerStumpsZ + 0.05f)
                    {
                        if (!stumpsBroken)
                        {
                            stumpsBroken = true;
                            S.BreakStumps(true, new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, -1));
                            Audio.Play(Sfx.Stumps);
                            Cam.Shake(0.03f);
                        }
                        p = new Vector3(p.x, Mathf.Max(0.05f, p.y - t), (float)Pitch.StrikerStumpsZ - 0.3f);
                    }
                    else if (c.Lbw || (c.HitPad && !bowled))
                    {
                        if (lbwStop < 0)
                        {
                            lbwStop = t;
                            Audio.Play(Sfx.Defend, 0.6f, 0.7f);
                        }
                        p = ToV(c.Pos) + new Vector3(0, -Mathf.Min(0.6f, (t - lbwStop) * 1.5f), 0.3f * (t - lbwStop));
                    }
                    else if (p.z <= keeper.Pos.z + 0.6f || keeperTook)
                    {
                        if (!keeperTook)
                        {
                            keeperTook = true;
                            keeperHands = p;
                            Audio.Play(Sfx.Glove, 0.8f);
                            var local = Quaternion.Inverse(Quaternion.Euler(0, keeper.Yaw, 0)) * (p - keeper.Pos);
                            keeper.Hold(Poses.Reach(local, false), 0.08f);
                            if (play.Wicket == WicketKind.Stumped)
                            {
                                S.BreakStumps(true, Vector3.back);
                                Audio.Play(Sfx.Stumps);
                            }
                        }
                        p = keeper.HandPos();
                    }
                    else if (!keeperTook && p.z < (float)Pitch.StrikerStumpsZ + 1.5f)
                    {
                        // Keeper rises towards the line of the ball.
                        var local = Quaternion.Inverse(Quaternion.Euler(0, keeper.Yaw, 0)) * (p + new Vector3(0, 0, -1.2f) - keeper.Pos);
                        keeper.Hold(Poses.Reach(new Vector3(local.x, Mathf.Max(0.3f, p.y), 0.45f), false), 0.12f);
                    }
                    ballPos = p;
                    if (bowled && !stumpsBroken && p.z < (float)Pitch.StrikerStumpsZ + 2.5f && Cam.Mode != CamMode.Stumps)
                    {
                        Cam.Set(CamMode.Stumps, true);
                        Time.timeScale = Paused ? 0 : 0.35f;
                    }
                    if (stumpsBroken && Time.timeScale < 1 && t > 0.8f) Time.timeScale = Paused ? 0 : 1;
                }
                _ball.Move(ballPos);

                // Chaser.
                if (chaser != null && c.BatHit)
                {
                    if (!chaseStarted && t >= play.ChaseStart)
                    {
                        chaseStarted = true;
                        float dist = Vector3.Distance(chaser.Pos, ToV(play.ChaseTo));
                        float time = Mathf.Max(0.2f, (float)(play.ChaseArrive - play.ChaseStart));
                        chaser.MoveTo(ToV(play.ChaseTo), Mathf.Clamp(dist / time * 1.05f, 2, 9.5f), null, null, Poses.Ready());
                    }
                    if (!chaseArrived && t >= play.ChaseArrive - 0.12f)
                    {
                        chaseArrived = true;
                        var hands = ToV(play.Flight.At(play.ChaseArrive));
                        var local = Quaternion.Inverse(Quaternion.Euler(0, chaser.Yaw, 0)) * (hands - chaser.Pos);
                        if (play.Dive) chaser.Hold(Poses.Dive(local.x > 0, Mathf.Clamp01(hands.y)), 0.1f);
                        else if (play.CatchChance) chaser.Hold(Poses.Reach(new Vector3(local.x, hands.y, Mathf.Max(0.2f, local.z)), false), 0.12f);
                        else chaser.Hold(Poses.Pickup(), 0.15f);
                    }
                    if (!picked && play.PickTime >= 0 && t >= play.PickTime)
                    {
                        picked = true;
                        if (!play.Dive) chaser.Hold(Poses.Pickup(), 0.1f);
                        Audio.Play(Sfx.Glove, 0.5f);
                    }
                    // Catch prompt for the user's fielders.
                    if (play.CatchChance && !catchResolved && CatchPrompts && !askedCatch && t >= Mathf.Max(0.25f, (float)play.CatchTime - 0.9f))
                    {
                        askedCatch = true;
                        float result = -2;
                        float gloves = (float)(Shop.ById(SaveSystem.Data.Gloves)?.Catch ?? 0);
                        Time.timeScale = 0.3f;
                        Hud.CatchPrompt(0.9f + gloves * 1.2f, r => result = r);
                        while (result < -1.5f && !_quit)
                        {
                            float d2 = Time.deltaTime;
                            t += d2;
                            _ball.Move(ToV(play.Flight.At(t)));
                            yield return null;
                        }
                        Time.timeScale = Paused ? 0 : 1;
                        double p = result >= 0.66f ? 0.97 : result >= 0 ? 0.78 : play.CatchProb * 0.55;
                        bool take = M.Rng.Chance(p);
                        if (take)
                        {
                            BallEngine.MakeCaught(play);
                            _userCatches++;
                        }
                        else BallEngine.MakeDropped(play);
                        caught = take;
                        catchResolved = true;
                        duration = (float)play.Duration;
                        runners = play.RunTimes.Length > 0 && !play.Four && !play.Six;
                        Hud.Banner(take ? "CAUGHT!" : "DROPPED!", take ? "Great hands" : "A chance goes down", take ? Pal.Green : Pal.Red, 1.4f);
                    }
                    if (play.CatchChance && t >= play.CatchTime && !catchPosed)
                    {
                        catchPosed = true;
                        Audio.Play(caught ? Sfx.Glove : Sfx.Pop, 0.9f);
                    }
                }
                else if (c.BatHit && chaser == null && play.Fielder >= 0) { }

                // Runners.
                if (runners)
                {
                    RunnerUpdate(striker, strikerStart, true, play, t, runStart);
                    RunnerUpdate(non, nonStart, false, play, t, runStart);
                }
                if (Input.GetMouseButtonDown(0) && !Hud.PointerOverControl() && t > 0.6f) _skip = true;
                yield return null;
            }
            Time.timeScale = Paused ? 0 : 1;
        }

        /// <summary>Moves a batter along the pitch following the run schedule.</summary>
        void RunnerUpdate(Actor a, Vector3 start, bool striker, Play play, float t, float runStart)
        {
            var times = play.RunTimes;
            int attempted = play.RunsAttempted;
            if (attempted <= 0 || times.Length == 0) return;
            float lane = striker ? 0.9f : -0.9f;
            float zA = striker ? (float)Pitch.StrikerCreaseZ - 0.2f : (float)Pitch.CreaseZ + 0.2f;
            float zB = -zA;
            float segStart = runStart;
            float pos = 0;
            float speed = 0;
            bool done = true;
            for (int j = 0; j < attempted && j < times.Length; j++)
            {
                float segEnd = (float)times[j];
                if (t < segStart)
                {
                    done = false;
                    break;
                }
                if (t <= segEnd)
                {
                    float k = Mathf.Clamp01((t - segStart) / Mathf.Max(0.1f, segEnd - segStart));
                    bool outRun = play.Wicket == WicketKind.RunOut && j == attempted - 1 && ((play.OutBatter == 0) == striker);
                    if (outRun) k = Mathf.Min(k, 0.88f);
                    pos = j % 2 == 0 ? k : 1 - k;
                    speed = 16f / Mathf.Max(0.1f, segEnd - segStart);
                    done = false;
                    break;
                }
                pos = j % 2 == 0 ? 1 : 0;
                segStart = segEnd + 0.5f;
            }
            var p = new Vector3(lane, 0, Mathf.Lerp(zA, zB, pos));
            int key = striker ? 0 : 1;
            if (speed > 0)
            {
                var dir = p - a.Pos;
                if (dir.sqrMagnitude > 1e-5f) a.Yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                a.Mirror = false;
                a.Pos = p;
                _runSpeed[key] = speed;
                if (!_running[key])
                {
                    // Start the gait once; it reads the live speed each frame.
                    _running[key] = true;
                    _runPhase[key] = 0;
                    a.Proc(tt =>
                    {
                        _runPhase[key] = Mathf.Repeat(_runPhase[key] + Time.deltaTime * _runSpeed[key] / 2.6f, 1);
                        return Poses.Gait(_runPhase[key], Mathf.Max(0.5f, _runSpeed[key]), true);
                    }, 0.15f);
                }
            }
            else if (done)
            {
                a.Pos = Vector3.Lerp(a.Pos, p, 0.2f);
                if (_running[key])
                {
                    _running[key] = false;
                    a.Hold(Poses.Stand(), 0.3f);
                }
            }
            else
            {
                // Waiting between runs (turning at the crease).
                _runSpeed[key] = 0.6f;
            }
        }

        readonly float[] _runSpeed = new float[2], _runPhase = new float[2];
        readonly bool[] _running = new bool[2];

        // ------------------------------------------------------------ after the ball

        IEnumerator Reactions(Play play, BallRecord rec, Player outPlayer)
        {
            var inn = M.Finished ? M.InningsList.Last() : M.Current;
            Hud.Refresh(M);
            Hud.Commentary(rec.Commentary);
            bool wicket = rec.Wicket;
            float hold = 0.9f;
            if (play.Six && !play.Wide)
            {
                _umpire.Hold(Poses.Signal(Sig.Six, 0), 0.25f);
                Audio.Play(Sfx.Horn, 0.8f);
                Audio.Play(Sfx.Cheer);
                Audio.CrowdSwell(1f, 3);
                S.Cheer(4);
                S.FlashScreen("SIX!", Pal.Gold, 3);
                Hud.Banner("SIX!", (int)Math.Round((play.Flight?.Carry ?? 80) + 4) + " METRES", Pal.Gold, 1.6f);
                Fx.Fireworks(transform, SceneLook.IsNight ? 4 : 2);
                hold = 1.8f;
            }
            else if (play.Four && !play.Wide)
            {
                _umpire.Proc(t => Poses.Signal(Sig.Four, t));
                Audio.Play(Sfx.Cheer, 0.8f);
                Audio.CrowdSwell(0.85f, 2.2f);
                S.Cheer(2.5f);
                S.FlashScreen("FOUR!", Pal.Sky, 2.5f);
                Hud.Banner("FOUR!", play.Contact?.ShotName ?? "", Pal.Sky, 1.4f);
                hold = 1.5f;
            }
            else if (wicket)
            {
                _umpire.Hold(Poses.Signal(Sig.Out, 0), 0.25f);
                Audio.Play(Sfx.Cheer, 0.9f);
                Audio.CrowdSwell(0.9f, 3);
                Haptics.Pulse(60);
                S.Cheer(3);
                string how = play.Wicket.Value.Label();
                S.FlashScreen("OUT!", Pal.Red, 3);
                Hud.Banner("OUT!", how + (outPlayer != null ? "  •  " + outPlayer.Name.ToUpperInvariant() : ""), Pal.Red, 1.8f);
                var bowlA = _field.TryGetValue(rec.Bowler.Id, out var ba) ? ba : null;
                bowlA?.Proc(t => Poses.Celebrate(t));
                if (play.Fielder >= 0 && play.Fielder <= 8 && E.Side != null && _field.TryGetValue(E.Side.Players[play.Fielder].Id, out var fa)) fa.Proc(t => Poses.Celebrate(t + 1));
                if (outPlayer != null && _bat.TryGetValue(outPlayer.Id, out var oa))
                {
                    oa.Hold(Poses.Dejected(), 0.3f);
                    StartCoroutine(WalkOff(oa, outPlayer.Id));
                }
                hold = 2.2f;
                Cam.Set(CamMode.Broadcast);
            }
            else if (play.Wide)
            {
                _umpire.Hold(Poses.Signal(Sig.Wide, 0), 0.25f);
                Hud.Banner("WIDE", "", Pal.Text, 0.9f);
            }
            else if (play.NoBall)
            {
                _umpire.Hold(Poses.Signal(Sig.NoBall, 0), 0.25f);
                Hud.Banner("NO BALL", "FREE HIT NEXT BALL", Pal.Accent, 1.4f);
            }
            else if (play.Byes > 0) _umpire.Hold(Poses.Signal(Sig.Bye, 0), 0.25f);
            else if (play.LegByes > 0) _umpire.Hold(Poses.Signal(Sig.LegBye, 0), 0.25f);
            if (inn.FreeHit && !M.Finished) _umpire.Proc(t => Poses.Signal(Sig.FreeHit, t));
            if (wicket) AutoPilot.Tag("wicket");
            else if (play.Six) AutoPilot.Tag("six");
            else if (play.Four) AutoPilot.Tag("four");
            // Milestones.
            foreach (var card in new[] { inn.StrikerCard, inn.NonStrikerCard })
            {
                if (card == null || !card.Batted || card.Out) continue;
                int r = card.Runs, before = r - (rec.Batter == card.P ? rec.Runs : 0);
                if ((before < 50 && r >= 50) || (before < 100 && r >= 100))
                {
                    Hud.Banner(r >= 100 ? "CENTURY!" : "FIFTY!", card.P.Name.ToUpperInvariant() + "  " + r + " (" + card.Balls + ")", Pal.Gold, 2.2f);
                    Audio.Play(Sfx.Applause);
                    if (_bat.TryGetValue(card.P.Id, out var ma)) ma.Proc(t => Poses.Fist(t));
                    hold = Mathf.Max(hold, 2.2f);
                }
            }
            var bowlerCard = inn.BowlCards.FirstOrDefault(b => b.P.Id == rec.Bowler.Id);
            if (bowlerCard != null && bowlerCard.HatTrick && bowlerCard.Streak == 3) Hud.Banner("HAT-TRICK!", rec.Bowler.Name.ToUpperInvariant(), Pal.Violet, 2.4f);
            yield return Wait(AutoPilot.Active ? Mathf.Min(hold, 0.6f) : hold);
            _ball.Hide();
        }

        IEnumerator WalkOff(Actor a, int id)
        {
            yield return new WaitForSeconds(0.8f);
            if (a == null) yield break;
            a.MoveTo(a.Pos + new Vector3(a.Pos.x < 0 ? -25 : 25, 0, 6), 2.2f);
            yield return new WaitForSeconds(2.5f);
            if (a != null && _bat.TryGetValue(id, out var cur) && cur == a)
            {
                _bat.Remove(id);
                Destroy(a.gameObject);
            }
            else if (a != null && !_bat.Values.Contains(a)) Destroy(a.gameObject);
        }

        IEnumerator Wait(float s)
        {
            float t = 0;
            while (t < s && !_quit)
            {
                t += Time.deltaTime * (_skip ? 3 : 1);
                if (Input.GetMouseButtonDown(0) && !Hud.PointerOverControl()) _skip = true;
                yield return null;
            }
            _skip = false;
        }

        // ------------------------------------------------------- user bowling

        IEnumerator UserBowlSetup(Player bowler)
        {
            var kinds = DeliveryKindX.For(bowler.CanBowl ? bowler.Bowl : BowlType.FastMedium);
            if (Array.IndexOf(kinds, _kind) < 0) _kind = kinds[0];
            var band = _kind.LengthBand(bowler.IsSpin);
            _length = Mathf.Clamp((float)_length, (float)band.Min, (float)band.Max);
            _bowlPressed = false;
            SettingTarget = true;
            UpdateTargetMarker(bowler);
            if (AutoPilot.Active) StartCoroutine(AutoBowl(bowler, kinds));
            Hud.ShowBowling(bowler, kinds, _kind, k =>
            {
                _kind = k;
                var b = k.LengthBand(bowler.IsSpin);
                _length = b.Def;
                UpdateTargetMarker(bowler);
            }, auto =>
            {
                _autoBowl = auto;
                _bowlPressed = true;
            });
            while (!_bowlPressed && !_quit)
            {
                if (_sim != SimReq.None) break;
                HandleTargetDrag(bowler);
                yield return null;
            }
            SettingTarget = false;
            _marker.gameObject.SetActive(false);
        }

        IEnumerator AutoBowl(Player bowler, DeliveryKind[] kinds)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            AutoPilot.Tag("bowl_setup");
            _kind = kinds[UnityEngine.Random.Range(0, kinds.Length)];
            var b = _kind.LengthBand(bowler.IsSpin);
            _length = b.Def;
            _line = UnityEngine.Random.Range(0.0f, 0.3f);
            UpdateTargetMarker(bowler);
            yield return new WaitForSecondsRealtime(0.2f);
            _autoBowl = false;
            _bowlPressed = true;
        }

        void UpdateTargetMarker(Player bowler)
        {
            var striker = M.Current.StrikerCard.P;
            float side = striker.LeftBat ? -1 : 1;
            _marker.position = new Vector3((float)_line * side, 0.025f, (float)(Pitch.StrikerStumpsZ + _length));
            _markerMat.color = new Color(1, 0.82f, 0.25f, 0.95f);
            _marker.gameObject.SetActive(true);
            Hud.TargetInfo(DeliveryTrack.Classify(_length), _line);
        }

        void HandleTargetDrag(Player bowler)
        {
            if (!Input.GetMouseButton(0) || Hud.PointerOverControl()) return;
            var ray = Cam.Cam.ScreenPointToRay(Input.mousePosition);
            if (ray.direction.y >= -0.01f) return;
            float d = -ray.origin.y / ray.direction.y;
            var p = ray.origin + ray.direction * d;
            var striker = M.Current.StrikerCard.P;
            float side = striker.LeftBat ? -1 : 1;
            var band = _kind.LengthBand(bowler.IsSpin);
            _length = Mathf.Clamp(p.z - (float)Pitch.StrikerStumpsZ, (float)band.Min, (float)band.Max);
            _line = Mathf.Clamp(p.x * side, -0.6f, 1.0f);
            UpdateTargetMarker(bowler);
        }

        public void SetField(FieldSetting f)
        {
            E.UserField = f;
            if (!InPlay)
            {
                E.PrepareField();
                var side = E.Side;
                var striker3 = new Vector3(0, 0, (float)Pitch.StrikerCreaseZ);
                for (int i = 0; i < 9; i++)
                {
                    var a = _field[side.Players[i].Id];
                    var pos = ToV(side.Pos[i]);
                    a.MoveTo(pos, 7f, YawTo(pos, striker3), null, Poses.Idle(0));
                }
            }
        }
    }
}
