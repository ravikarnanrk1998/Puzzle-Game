using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FRF
{
    public sealed class RaceConfig
    {
        public TrackDef Track;
        public EventDef Event;
        public int Rivals = 5;
        public float Skill = 0.6f;
        public Difficulty Difficulty = Difficulty.Normal;
        public bool Autopilot;
        public Action<RaceConfig, RaceResult> OnExit;

        public string Title => Event != null ? "EVENT " + (Event.Index + 1) + " · " + Event.Title : "QUICK RACE";
    }

    public enum ExitAction { Menu, Retry, Next }

    public sealed class Standing
    {
        public RiderDef Rider;
        public float Time;
        public bool Player;
        public int Takedowns, Tricks;
    }

    public sealed class RaceResult
    {
        public bool Finished;
        public int Place, Tricks, Takedowns, Crashes;
        public float Time;
        public readonly List<Standing> Standings = new List<Standing>();
        public int PrizePlace, PrizeTricks, PrizeTakedowns, PrizeMedal, Total;
        public int Medal;
        public bool NewBest;
        public ExitAction Action;
    }

    public enum RacePhase { Countdown, Racing, Finished, Results }

    /// <summary>Runs one downhill: builds the mountain and riders, steps the simulation and drives the camera, HUD and sound.</summary>
    public sealed class RaceDirector : MonoBehaviour
    {
        public RaceConfig Cfg { get; private set; }
        public TrackScene Scene { get; private set; }
        public Track Track => Scene.Track;
        public readonly List<Rider> Riders = new List<Rider>();
        public Rider Player { get; private set; }
        public Hud Hud { get; private set; }
        public RacePhase Phase { get; private set; }
        public float Clock { get; private set; }
        public float Countdown { get; private set; } = 4.2f;
        public RaceResult Result { get; private set; }
        public bool Paused { get; private set; }

        readonly List<RiderModel> _models = new List<RiderModel>();
        readonly Dictionary<Rider, RiderAI> _ai = new Dictionary<Rider, RiderAI>();
        readonly List<RiderEvent> _events = new List<RiderEvent>();
        readonly List<(OrbSpot Spot, Transform T, float Hidden)> _orbs = new List<(OrbSpot, Transform, float)>();
        Camera _cam;
        Vector3 _camPos, _camVel, _look;
        float _fov = 62, _shake, _finishTimer, _dustAcc, _barrierCd;
        RiderInput _input;

        public static RaceDirector Create(RaceConfig cfg, RectTransform hudRoot)
        {
            var go = new GameObject("Race");
            var d = go.AddComponent<RaceDirector>();
            d.Cfg = cfg;
            d.Build(hudRoot);
            return d;
        }

        void Build(RectTransform hudRoot)
        {
            var q = Settings.Graphics;
            Fx.Create(transform);
            Scene = TrackScene.Build(Cfg.Track, transform, q);
            SceneLook.Apply(Track.Theme, transform);
            _cam = SceneLook.MainCamera();

            // Riders: the player plus rivals drawn from the roster.
            var me = Profile.Rider;
            var pool = Catalog.Riders.Where(r => r.Id != me.Id).ToList();
            var rnd = new System.Random(Environment.TickCount);
            pool = pool.OrderBy(_ => rnd.Next()).ToList();
            int rivals = Mathf.Clamp(Cfg.Rivals, 0, pool.Count);
            int total = rivals + 1;
            // Start line across the gate; the player starts in the middle of the pack.
            int playerSlot = total / 2;
            float diffMul = Cfg.Difficulty == Difficulty.Easy ? 0.92f : Cfg.Difficulty == Difficulty.Hard ? 1.025f : 0.98f;
            int poolIdx = 0;
            for (int i = 0; i < total; i++)
            {
                bool isPlayer = i == playerSlot;
                RiderDef def;
                BikeDef bike;
                Color paint;
                if (isPlayer)
                {
                    def = me;
                    bike = Profile.Bike;
                    paint = Profile.PaintColor;
                }
                else
                {
                    def = pool[poolIdx++];
                    // Rivals ride bikes near the player's tier, a little better on harder events.
                    int tier = Array.IndexOf(Catalog.Bikes, Profile.Bike);
                    int b = Mathf.Clamp(tier + rnd.Next(-1, 2) + (Cfg.Skill > 0.75f ? 1 : 0), 0, Catalog.Bikes.Length - 1);
                    bike = Catalog.Bikes[b];
                    paint = Catalog.Paints[rnd.Next(Catalog.Paints.Length)];
                }
                var r = new Rider(def, bike, paint, isPlayer, i, Track, _events);
                float half = Track.WidthAt(Track.StartS) * 0.5f - 1.6f;
                float x = total == 1 ? 0 : Mathf.Lerp(-half, half, i / (float)(total - 1));
                r.PlaceAt(Track.StartS + (i % 2) * 0.8f, x);
                Riders.Add(r);
                if (isPlayer) Player = r;
                bool shadows = isPlayer || q == GraphicsQuality.High;
                var m = RiderModel.Create(transform, def, bike, paint, shadows);
                _models.Add(m);
                if (!isPlayer || Cfg.Autopilot)
                {
                    float skill = isPlayer ? 0.85f : Mathf.Clamp01(Cfg.Skill + (float)(rnd.NextDouble() - 0.5) * 0.3f);
                    _ai[r] = new RiderAI(r, skill, rnd.Next());
                    if (!isPlayer) r.SpeedMul = diffMul * (0.95f + skill * 0.07f);
                }
            }

            // Adrenaline orbs.
            var orbMat = Mats.Lit(Pal.Cyan, 0.8f, null, false, 0, new Color(0.4f, 1.6f, 2.2f));
            var coreMat = Mats.Lit(Color.white, 0.9f, null, false, 0, new Color(2, 2, 2));
            foreach (var o in Track.Orbs)
            {
                var go = MeshKit.Obj("Orb", transform, MeshKit.Sphere, orbMat, false, false);
                go.transform.position = Track.WorldOnGround(o.S, o.X, o.Lift);
                go.transform.localScale = Vector3.one * 0.32f;
                var core = MeshKit.Obj("Core", go.transform, MeshKit.Sphere, coreMat, false, false);
                core.transform.localScale = Vector3.one * 0.5f;
                _orbs.Add((o, go.transform, 0));
            }
            if (Scene.VolcanoTop != null) Fx.I.Plume(Scene.VolcanoTop.Value);
            Fx.I.StartWeather(Track.Theme.Weather, _cam.transform);

            Hud = Hud.Create(hudRoot, this);
            Sfx.I?.StartRide();
            Sfx.I?.Music(true, 0.22f);
            Phase = RacePhase.Countdown;
            PlaceCamera(true);
            Palette.Apply();
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            Sfx.I?.StopRide();
            if (Hud != null) Destroy(Hud.gameObject);
            SceneLook.Clear();
        }

        // ------------------------------------------------------------ control

        public void SetPaused(bool p)
        {
            if (Phase == RacePhase.Results) p = false;
            Paused = p;
            _slowmo = 0;
            Time.timeScale = p ? 0 : 1;
            AudioListener.pause = p;
        }

        public void Exit(ExitAction action)
        {
            SetPaused(false);
            if (Result == null) Result = BuildResult(false);
            Result.Action = action;
            Cfg.OnExit?.Invoke(Cfg, Result);
        }

        // ------------------------------------------------------------ frame

        float _slowmo;

        void Update()
        {
            if (Paused) return;
            if (_slowmo > 0)
            {
                _slowmo -= Time.unscaledDeltaTime;
                Time.timeScale = _slowmo > 0 ? 0.4f : 1;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0) return;

            if (Phase == RacePhase.Countdown)
            {
                float before = Countdown;
                Countdown -= dt;
                for (int n = 3; n >= 1; n--)
                    if (before > n && Countdown <= n) Sfx.I?.Beep();
                if (Countdown <= 0)
                {
                    Phase = RacePhase.Racing;
                    foreach (var r in Riders) r.Started = true;
                    // A shove off the start ramp.
                    foreach (var r in Riders) r.V = 3.5f;
                    Sfx.I?.Go();
                    Hud.Big("GO!", Pal.Yellow, 0.9f);
                }
            }
            else if (Phase == RacePhase.Racing) Clock += dt;
            else if (Phase == RacePhase.Finished) Clock += dt;

            // Inputs.
            _input = Hud.ReadInput();
            var inputs = new RiderInput[Riders.Count];
            for (int i = 0; i < Riders.Count; i++)
            {
                var r = Riders[i];
                if (_ai.TryGetValue(r, out var ai)) inputs[i] = ai.Think(dt, Riders, Player);
                else inputs[i] = _input;
            }

            // Simulation substeps.
            int steps = Mathf.CeilToInt(dt / (1 / 60f));
            float h = dt / steps;
            for (int k = 0; k < steps; k++)
            {
                for (int i = 0; i < Riders.Count; i++)
                {
                    Riders[i].Step(h, inputs[i]);
                    if (Riders[i].PunchStrikeNow) ResolvePunch(Riders[i]);
                    inputs[i].ADown = inputs[i].BDown = inputs[i].CDown = inputs[i].BoostDown = false;
                }
                Interactions();
            }
            RubberBand();
            Standings();
            Orbs(dt);
            HandleEvents();
            Effects(dt);

            for (int i = 0; i < Riders.Count; i++)
            {
                bool near = (Riders[i].WorldPos - _cam.transform.position).sqrMagnitude < 80 * 80;
                _models[i].Sync(Riders[i], dt, near);
            }
            PlaceCamera(false);
            FinishLogic(dt);
            Coach();
            Hud.Tick(dt);
            bool snow = Track.Theme.Biome == Biome.Glacier;
            float crowd = 0;
            foreach (float cs in Track.Crowds) crowd = Mathf.Max(crowd, 1 - Mathf.Abs(Player.S - cs) / 60f);
            Sfx.I?.Ride(Player.Crashed ? 0 : Player.Speed01, Player.Grounded, snow, Mathf.Clamp01(crowd));
        }

        // First-race coaching: one hint per situation, only in the player's first two races.
        int _coached;
        float _coachCd;

        void Coach()
        {
            if (Cfg.Autopilot || Profile.Races >= 2 || Phase != RacePhase.Racing) return;
            _coachCd -= Time.deltaTime;
            if (_coachCd > 0) return;
            var p = Player;
            void Say(int bit, string text, Color c)
            {
                if ((_coached & bit) != 0) return;
                _coached |= bit;
                _coachCd = 3.5f;
                Hud.Coach(text, c);
            }
            if (Clock > 1.5f) Say(1, "STEER WITH THE ARROWS  ·  BRAKE BEFORE TIGHT BENDS", Pal.Text);
            var ramp = Track.NextRamp(p.S, 30);
            if (ramp != null && ramp.S0 - p.S < 22 && p.X > ramp.X0 && p.X < ramp.X1) Say(2, "HOLD JUMP INTO THE KICKER FOR A BIG POP!", Pal.Yellow);
            if (!p.Grounded && p.AirTime > 0.2f && p.TimeToLand() > 0.7f) Say(4, "TRICK! HOLD SUPERMAN / NO HANDS / CAN-CAN, LET GO BEFORE LANDING", Pal.Violet);
            foreach (var r in Riders)
            {
                if (r == p || r.Crashed) continue;
                if (Mathf.Abs(r.S - p.S) < 2 && Mathf.Abs(r.X - p.X) < 2.2f) Say(8, "RIVAL ALONGSIDE: PUNCH!", Pal.Red);
            }
            if (p.Adrenaline >= 25) Say(16, "ADRENALINE FULL: HIT BOOST", Pal.Cyan);
        }

        void ResolvePunch(Rider a)
        {
            Rider best = null;
            float bestD = float.MaxValue;
            foreach (var b in Riders)
            {
                if (b == a || b.Crashed || b.Invuln > 0) continue;
                float ds = b.S - a.S, dx = b.X - a.X;
                if (Mathf.Abs(ds) > 2.5f || Mathf.Abs(dx) > 2.5f || Mathf.Abs(b.Y - a.Y) > 1.4f) continue;
                if (Mathf.Sign(dx) != a.PunchSide && Mathf.Abs(dx) > 0.4f) continue;
                float d = Mathf.Abs(ds) + Mathf.Abs(dx);
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            if (best != null) best.TakeHit(a, a.PunchSide);
        }

        /// <summary>Obstacles, rider-to-rider bumps.</summary>
        void Interactions()
        {
            foreach (var r in Riders)
            {
                if (r.Crashed) continue;
                foreach (var o in Track.Obstacles)
                {
                    if (Mathf.Abs(o.S - r.S) > o.HalfS + 0.55f || Mathf.Abs(o.X - r.X) > o.HalfX + 0.3f) continue;
                    float clear = r.Y - Track.GroundY(o.S, o.X);
                    if (clear > o.H * 0.85f || r.Invuln > 0) continue;
                    if (r.V > 6.5f && r.Started) r.Crash(o.Kind == ObstacleKind.Log ? "CLIPPED A LOG" : o.Kind == ObstacleKind.Cactus ? "OUCH! CACTUS" : o.Kind == ObstacleKind.Stump ? "HIT A STUMP" : "HIT A ROCK");
                    else
                    {
                        r.V = Mathf.Min(r.V, 1.5f);
                        r.S = Mathf.Min(r.S, o.S - o.HalfS - 0.56f);
                    }
                }
            }
            for (int i = 0; i < Riders.Count; i++)
            for (int j = i + 1; j < Riders.Count; j++)
            {
                var a = Riders[i];
                var b = Riders[j];
                if (a.Crashed || b.Crashed) continue;
                float ds = b.S - a.S, dx = b.X - a.X;
                if (Mathf.Abs(ds) > 1.6f || Mathf.Abs(dx) > 0.85f || Mathf.Abs(b.Y - a.Y) > 1.0f) continue;
                float push = (0.86f - Mathf.Abs(dx)) * 0.5f;
                float sx = dx >= 0 ? 1 : -1;
                a.X -= sx * push;
                b.X += sx * push;
                a.DriftV -= sx * 1.0f;
                b.DriftV += sx * 1.0f;
                // The rider behind cannot pass through the one ahead.
                var behind = ds > 0 ? a : b;
                var ahead = ds > 0 ? b : a;
                if (Mathf.Abs(dx) < 0.55f) behind.V = Mathf.Min(behind.V, ahead.V + 0.3f);
                if (a.IsPlayer || b.IsPlayer)
                {
                    if (_barrierCd <= 0) Sfx.I?.Bump();
                    _barrierCd = 0.3f;
                }
            }
        }

        void RubberBand()
        {
            foreach (var kv in _ai)
            {
                var r = kv.Key;
                if (r.IsPlayer) continue;
                float gap = r.S - Player.S;
                float baseMul = (Cfg.Difficulty == Difficulty.Easy ? 0.92f : Cfg.Difficulty == Difficulty.Hard ? 1.025f : 0.98f) * (0.95f + kv.Value.Skill * 0.07f);
                float band = gap > 25 ? Mathf.Lerp(1, 0.93f, (gap - 25) / 70f) : gap < -35 ? Mathf.Lerp(1, 1.07f, (-gap - 35) / 90f) : 1;
                r.SpeedMul = baseMul * Mathf.Clamp(band, 0.93f, 1.07f);
            }
        }

        void Standings()
        {
            var order = Riders.OrderByDescending(r => r.Finished ? 1e6f - r.FinishTime : r.S).ToList();
            for (int i = 0; i < order.Count; i++) order[i].Place = i + 1;
        }

        void Orbs(float dt)
        {
            float t = Time.time;
            for (int i = 0; i < _orbs.Count; i++)
            {
                var (spot, tr, hidden) = _orbs[i];
                if (hidden > 0)
                {
                    hidden -= dt;
                    if (hidden <= 0) tr.gameObject.SetActive(true);
                    _orbs[i] = (spot, tr, hidden);
                    continue;
                }
                if (Mathf.Abs(spot.S - Player.S) < 90)
                {
                    tr.position = Track.WorldOnGround(spot.S, spot.X, spot.Lift + Mathf.Sin(t * 3 + spot.S) * 0.12f);
                    tr.localScale = Vector3.one * (0.3f + Mathf.Sin(t * 6 + spot.S) * 0.03f);
                }
                foreach (var r in Riders)
                {
                    if (r.Crashed) continue;
                    if (Mathf.Abs(spot.S - r.S) > 1.3f || Mathf.Abs(spot.X - r.X) > 1.1f) continue;
                    float oy = Track.GroundY(spot.S, spot.X) + spot.Lift;
                    if (Mathf.Abs(r.Y + 0.9f - oy) > 1.4f) continue;
                    r.Adrenaline = Mathf.Min(100, r.Adrenaline + 12);
                    tr.gameObject.SetActive(false);
                    _orbs[i] = (spot, tr, 8);
                    Fx.I?.Sparks(tr.position, Pal.Cyan, 14, 4);
                    if (r.IsPlayer)
                    {
                        Sfx.I?.Orb();
                        Hud.Toast("+ ADRENALINE", Pal.Cyan);
                    }
                    break;
                }
            }
        }

        void HandleEvents()
        {
            foreach (var e in _events)
            {
                var r = e.Rider;
                bool me = r == Player;
                bool near = (r.WorldPos - _cam.transform.position).sqrMagnitude < 45 * 45;
                switch (e.Kind)
                {
                    case RiderEventKind.Crash:
                        if (near) Sfx.I?.Crash(me ? 1 : 0.6f);
                        Fx.I?.Burst(r.WorldPos + Vector3.up * 0.4f, Track.Theme.Dust, 14, 3.5f, 1.3f);
                        if (me)
                        {
                            Hud.Big("CRASH!", Pal.Red, 1.4f, e.Text);
                            _shake = 0.6f;
                            Settings.Buzz();
                        }
                        break;
                    case RiderEventKind.Respawn:
                        break;
                    case RiderEventKind.Hop:
                        if (me) Sfx.I?.Hop();
                        break;
                    case RiderEventKind.Pop:
                        if (me)
                        {
                            Sfx.I?.Hop();
                            Hud.Toast("POP!", Pal.Yellow);
                        }
                        break;
                    case RiderEventKind.Land:
                        if (near) Sfx.I?.Land(Mathf.Clamp01(e.Points / 90f) * (me ? 1 : 0.5f));
                        if (e.Points > 55) Fx.I?.Burst(r.WorldPos + Vector3.up * 0.1f, Track.Theme.Dust, 8, 2.4f, 1.0f);
                        if (me && e.Points > 70) _shake = Mathf.Max(_shake, 0.18f);
                        break;
                    case RiderEventKind.HardLanding:
                        if (me) Hud.Toast("HARD LANDING", Pal.Dim);
                        break;
                    case RiderEventKind.TrickLanded:
                        if (me)
                        {
                            Sfx.I?.Trick();
                            Hud.Trick(e.Text, e.Points);
                        }
                        break;
                    case RiderEventKind.Punch:
                        // Swing at the nearest rival.
                        Rider target = null;
                        float best = 9;
                        foreach (var o in Riders)
                        {
                            if (o == r || o.Crashed) continue;
                            float d = Mathf.Abs(o.S - r.S) + Mathf.Abs(o.X - r.X);
                            if (d < best && Mathf.Abs(o.S - r.S) < 3.5f)
                            {
                                best = d;
                                target = o;
                            }
                        }
                        r.PunchSide = target != null ? (target.X >= r.X ? 1 : -1) : (r.Yaw >= 0 ? 1 : -1);
                        if (near) Sfx.I?.Swish();
                        break;
                    case RiderEventKind.Hit:
                        if (near) Sfx.I?.Hit(me || e.Other == Player ? 1 : 0.6f);
                        Fx.I?.Sparks(r.WorldPos + Vector3.up * 1.3f, Pal.Yellow, 10, 3);
                        if (me)
                        {
                            Hud.Flash(Pal.Red);
                            _shake = Mathf.Max(_shake, 0.3f);
                            Settings.Buzz();
                        }
                        if (e.Other == Player) Hud.Toast("HIT!  " + r.Def.Nick, Pal.Orange);
                        break;
                    case RiderEventKind.Takedown:
                        if (r == Player)
                        {
                            Hud.Big("TAKEDOWN!", Pal.Orange, 1.2f, e.Other.Def.Name);
                            Sfx.I?.Cheer(0.7f);
                            _slowmo = 0.55f;
                        }
                        else if (e.Other == Player) Hud.Toast("TAKEN DOWN BY " + r.Def.Nick, Pal.Red);
                        break;
                    case RiderEventKind.Boost:
                        if (me)
                        {
                            Sfx.I?.Boost();
                            _shake = Mathf.Max(_shake, 0.12f);
                        }
                        break;
                    case RiderEventKind.Barrier:
                        if (me && _barrierCd <= 0)
                        {
                            Sfx.I?.Bump();
                            _barrierCd = 0.4f;
                        }
                        break;
                    case RiderEventKind.Finish:
                        r.FinishTime = Clock;
                        Standings();
                        if (me)
                        {
                            Phase = RacePhase.Finished;
                            _finishTimer = 0;
                            Sfx.I?.Finish();
                            Sfx.I?.Cheer();
                            Hud.Big(Ui.Ord(r.Place), r.Place <= 3 ? Pal.Gold : Pal.Text, 2.2f, "FINISHED · " + Ui.Time(Clock));
                        }
                        break;
                }
            }
            _events.Clear();
        }

        void Effects(float dt)
        {
            _barrierCd -= dt;
            if (Fx.I == null) return;
            var dust = Track.Theme.Dust;
            float q = Settings.Graphics == GraphicsQuality.Low ? 0.4f : Settings.Graphics == GraphicsQuality.Medium ? 0.7f : 1f;
            foreach (var r in Riders)
            {
                if (r.Crashed || !r.Grounded || r.V < 4) continue;
                bool near = (r.WorldPos - _cam.transform.position).sqrMagnitude < 60 * 60;
                if (!near) continue;
                bool off = Mathf.Abs(r.X) > r.Half;
                _dustAcc += r.V * dt * (off ? 1.6f : 0.75f) * q;
                var back = r.WorldPos - Track.Forward(r.S) * 0.6f;
                while (_dustAcc > 1)
                {
                    _dustAcc -= 1;
                    var v = -Track.Forward(r.S) * (r.V * 0.12f) + UnityEngine.Random.insideUnitSphere * 0.6f + Vector3.up * 0.9f;
                    Fx.I.Dust(back + UnityEngine.Random.insideUnitSphere * 0.15f, v, dust.WithA(0.55f), 0.5f + r.Speed01 * 0.7f, 0.9f + UnityEngine.Random.value * 0.6f);
                }
            }
            foreach (var r in Riders)
            {
                if (!r.Boosting || r.Crashed) continue;
                var p = r.WorldPos + Vector3.up * 0.8f - Track.Forward(r.S) * 0.8f;
                for (int k = 0; k < 2; k++)
                    Fx.I.Glow(p + UnityEngine.Random.insideUnitSphere * 0.3f, -Track.Forward(r.S) * 2 + UnityEngine.Random.insideUnitSphere, Color.Lerp(Pal.Orange, Pal.Yellow, UnityEngine.Random.value), 0.35f, 0.35f);
            }
        }

        // ------------------------------------------------------------ camera

        void PlaceCamera(bool snap)
        {
            var r = Player;
            var tr = Track;
            bool far = Settings.FarCamera;
            float back = far ? 6.4f : 4.4f, up = far ? 2.7f : 1.95f;
            Vector3 target, look;
            if (Phase == RacePhase.Countdown && Countdown > 1.4f)
            {
                // Opening shot: in front of the pack, looking back up the start ramp, easing round to the chase view.
                float t = Mathf.Clamp01((4.2f - Countdown) / 2.8f);
                float e = t * t * (3 - 2 * t);
                var front = tr.World(r.S + 9, r.X * 0.4f + 3.5f, tr.GroundY(r.S + 9, 0) + 2.2f);
                var chase = tr.World(r.S - back, r.X, tr.GroundY(r.S - back, r.X) + up);
                target = Vector3.Lerp(front, chase, e);
                look = r.WorldPos + Vector3.up * 1.0f + tr.Forward(r.S) * Mathf.Lerp(0, 6, e);
            }
            else if (Phase == RacePhase.Results || (Phase == RacePhase.Finished && _finishTimer > 1.2f))
            {
                // Orbit the player in the run-out.
                float a = Time.time * 0.35f;
                var c = r.WorldPos;
                target = c + new Vector3(Mathf.Sin(a) * 6.5f, 2.4f, Mathf.Cos(a) * 6.5f);
                target.y = Mathf.Max(target.y, Scene.Field.HeightAt(target.x, target.z) + 1.2f);
                look = c + Vector3.up * 1.0f;
            }
            else if (r.Crashed)
            {
                var c = r.BodyPos;
                float s = r.S - back * 1.3f;
                target = tr.World(s, r.X * 0.7f, Mathf.Max(tr.GroundY(s, r.X) + up * 1.3f, c.y + 1.5f));
                look = c;
            }
            else
            {
                float s = r.S - back;
                float x = r.X * 0.85f - Mathf.Sin(r.Yaw) * 1.2f;
                float gy = tr.GroundY(s, x);
                float y = Mathf.Max(gy + up, r.Y + up * 0.75f);
                if (!r.Grounded) y = Mathf.Max(y, r.Y + up * 0.55f);
                target = tr.World(s, x, y);
                look = r.WorldPos + Vector3.up * 0.95f + tr.Forward(r.S + 4) * 6f;
                look.y = Mathf.Lerp(look.y, r.Y + 0.9f, 0.6f);
            }
            if (snap)
            {
                _camPos = target;
                _look = look;
                _camVel = Vector3.zero;
            }
            else
            {
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                _camPos = Vector3.SmoothDamp(_camPos, target, ref _camVel, Phase == RacePhase.Countdown ? 0.25f : 0.11f, 300, dt);
                _look = Vector3.Lerp(_look, look, 1 - Mathf.Exp(-dt * 12));
            }
            var pos = _camPos;
            if (Scene.Field != null) pos.y = Mathf.Max(pos.y, Scene.Field.HeightAt(pos.x, pos.z) + 0.8f);
            if (_shake > 0)
            {
                _shake = Mathf.MoveTowards(_shake, 0, Time.deltaTime * 1.6f);
                pos += UnityEngine.Random.insideUnitSphere * _shake * 0.25f;
            }
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation(_look - pos, Vector3.up);
            float fovTarget = 60 + r.Speed01 * 12 + (r.Boosting ? 9 : 0) + (far ? 0 : 2);
            _fov = Mathf.Lerp(_fov, fovTarget, 1 - Mathf.Exp(-Time.deltaTime * 3));
            _cam.fieldOfView = _fov;
        }

        // ------------------------------------------------------------ finish

        void FinishLogic(float dt)
        {
            if (Phase != RacePhase.Finished) return;
            _finishTimer += dt;
            bool allDone = Riders.All(x => x.Finished);
            if (_finishTimer > 4.2f || (allDone && _finishTimer > 2.5f))
            {
                Phase = RacePhase.Results;
                Result = BuildResult(true);
                Hud.ShowResults(Result);
            }
        }

        RaceResult BuildResult(bool finished)
        {
            var res = new RaceResult { Finished = finished && Player.Finished };
            // Riders still on the hill get an estimated time from their pace.
            var times = new Dictionary<Rider, float>();
            foreach (var r in Riders)
            {
                if (r.Finished) times[r] = r.FinishTime;
                else
                {
                    float pace = Mathf.Max(7, r.S / Mathf.Max(1, Clock));
                    times[r] = Clock + (Track.FinishS - r.S) / pace + (r.Crashed ? 2 : 0);
                }
            }
            var order = Riders.OrderBy(r => times[r]).ToList();
            foreach (var r in order)
                res.Standings.Add(new Standing { Rider = r.Def, Time = times[r], Player = r.IsPlayer, Takedowns = r.Takedowns, Tricks = r.TrickScore });
            res.Place = order.IndexOf(Player) + 1;
            res.Time = times[Player];
            res.Tricks = Player.TrickScore;
            res.Takedowns = Player.Takedowns;
            res.Crashes = Player.Crashes;
            if (res.Finished)
            {
                res.PrizePlace = Catalog.PlacePrize(res.Place);
                res.PrizeTricks = Mathf.RoundToInt(res.Tricks / 25f / 10f) * 10;
                res.PrizeTakedowns = res.Takedowns * 120;
                if (Cfg.Event != null)
                {
                    res.Medal = Cfg.Event.Medal(res.Place, res.Tricks, res.Takedowns, true);
                    int prev = Profile.Medal(Cfg.Event.Index);
                    // Medal money is paid for the improvement over the best medal so far.
                    if (res.Medal > prev) res.PrizeMedal = Cfg.Event.MedalPrize(res.Medal) - Cfg.Event.MedalPrize(prev);
                }
                res.Total = res.PrizePlace + res.PrizeTricks + res.PrizeTakedowns + res.PrizeMedal;
                // Bank it.
                Profile.Cash += res.Total;
                Profile.Races += 1;
                if (Cfg.Event != null) Profile.SetMedal(Cfg.Event.Index, res.Medal);
                res.NewBest = Profile.SubmitTime(Cfg.Track.Id, res.Time);
                Profile.SubmitTricks(Cfg.Track.Id, res.Tricks);
                PlayerPrefs.Save();
            }
            return res;
        }

        // ------------------------------------------------------------ test hooks

        public RiderInput LastInput => _input;
    }
}
