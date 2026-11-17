using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Automated play-through for testing on devices without a human: launch with
    /// <c>-kplAuto</c> (optionally <c>-kplShots dir</c>, <c>-kplPlan quick|tour|chal|all</c>).
    /// Visits every screen, plays matches with simulated touches, simulates a season,
    /// cycles graphics profiles, takes screenshots and logs FPS and errors.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static bool TossBat = true;
        static string _dir;
        static int _shot;
        static int _errors;
        static readonly HashSet<string> Taken = new HashSet<string>();
        static string _plan = "all";
        static readonly List<float> Frames = new List<float>();
        static System.Random _rng = new System.Random(5);

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == name) return i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "";
            }
            return null;
        }

        public static bool Requested => Arg("-kplAuto") != null;

        public static void StartIfRequested(GameApp app)
        {
            if (!Requested || Active) return;
            Active = true;
            _plan = Arg("-kplPlan") ?? "all";
            string d = Arg("-kplShots");
            _dir = string.IsNullOrEmpty(d) ? Path.Combine(Application.persistentDataPath, "shots") : (Path.IsPathRooted(d) ? d : Path.Combine(Application.persistentDataPath, d));
            Directory.CreateDirectory(_dir);
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error)
                {
                    _errors++;
                    if (_errors < 30) Debug.Log("[KPL] AUTO saw error: " + msg);
                }
            };
            app.gameObject.AddComponent<AutoPilot>();
            Debug.Log("[KPL] AUTOPILOT start plan=" + _plan + " shots=" + _dir);
        }

        public static void Moment(string tag)
        {
            if (!Active || Taken.Contains(tag)) return;
            Taken.Add(tag);
            Capture(tag);
        }

        static void Capture(string name)
        {
            _shot++;
            string file = Path.Combine(_dir, _shot.ToString("00") + "_" + name + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("[KPL] SHOT " + file);
        }

        /// <summary>Simulated batting: commits a shot a little either side of the ideal moment.</summary>
        public static ShotInput? BatDecision(float tPres, float contactPres)
        {
            if (!Active) return null;
            if (_pendingErr == null) _pendingErr = (float)((_rng.NextDouble() - 0.5) * 0.12);
            if (tPres < contactPres + _pendingErr.Value) return null;
            _pendingErr = null;
            double r = _rng.NextDouble();
            var kind = r < 0.55 ? ShotKind.Ground : r < 0.85 ? ShotKind.Loft : ShotKind.Defend;
            return new ShotInput { Kind = kind, Azimuth = double.NaN };
        }

        static float? _pendingErr;

        void Update()
        {
            if (GameApp.I != null && GameApp.I.Match != null) Frames.Add(Time.unscaledDeltaTime);
        }

        /// <summary>Screenshots are taken at the end of the frame, so give them two frames before changing anything.</summary>
        static IEnumerator Snap(string name)
        {
            Capture(name);
            yield return null;
            yield return null;
        }

        static IEnumerator Wait(float s)
        {
            float t = 0;
            while (t < s)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator Start()
        {
            while (!GameApp.Ready) yield return null;
            yield return Wait(1.0f);
            var save = SaveSystem.Data;
            if (string.IsNullOrEmpty(save.Fav))
            {
                yield return Snap("team_select");
                save.Fav = "MUM";
                SaveSystem.Save();
                MainMenu.Show();
            }
            yield return Wait(1.0f);
            yield return Snap("main_menu");
            GameApp.I.Canvas.enabled = false;
            yield return Wait(2.5f);
            yield return Snap("stadium_orbit_a");
            yield return Wait(6f);
            yield return Snap("stadium_orbit_b");
            var ms = GameApp.I.Menu3D;
            if (ms != null && ms.Batter != null)
            {
                var cam = CameraRig.Attach(SceneLook.MainCamera());
                var b = ms.Batter.Pos;
                cam.FocusPos = b + new Vector3(2.6f, 1.6f, 2.4f);
                cam.FocusLook = b + new Vector3(0, 1.0f, 0);
                cam.FocusFov = 34;
                cam.Set(CamMode.Focus, true);
                yield return Wait(1.0f);
                yield return Snap("closeup_batter");
                var k = ms.Keeper.Pos;
                cam.FocusPos = k + new Vector3(-1.8f, 1.3f, 2.8f);
                cam.FocusLook = k + new Vector3(0, 0.7f, 0);
                cam.Set(CamMode.Focus, true);
                yield return Wait(1.0f);
                yield return Snap("closeup_keeper");
                cam.Set(CamMode.Orbit, true);
            }
            GameApp.I.Canvas.enabled = true;
            if (_plan == "look") yield return QuickLook();
            if (_plan == "all" || _plan == "menus") yield return Menus();
            if (_plan == "all" || _plan == "quick") yield return QuickMatch(false, true);
            if (_plan == "all" || _plan == "night") yield return QuickMatch(true, false);
            if (_plan == "all" || _plan == "tour") yield return TourSim();
            if (_plan == "all" || _plan == "chal") yield return Challenge();
            if (_plan == "all" || _plan == "gfx") yield return Graphics();
            SaveCheck();
            if (Frames.Count > 0)
            {
                var sorted = Frames.OrderBy(x => x).ToList();
                float avg = Frames.Count / Frames.Sum();
                float p5 = 1f / sorted[(int)(sorted.Count * 0.95f)];
                Debug.Log("[KPL] FPS in matches: avg " + avg.ToString("0.0") + ", 5% low " + p5.ToString("0.0") + " over " + Frames.Count + " frames (target " + Application.targetFrameRate + ")");
            }
            Debug.Log("[KPL] Memory: " + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)) + " MB allocated, " + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024)) + " MB reserved");
            Debug.Log("[KPL] AUTOPILOT DONE errors=" + _errors + " shots=" + _shot);
            yield return Wait(1.5f);
            if (!Application.isEditor) Application.Quit();
        }

        IEnumerator Menus()
        {
            DailyPopup.Show();
            yield return Wait(0.8f);
            yield return Snap("daily_rewards");
            Popup.CloseAll();
            var screens = new (string, Action)[]
            {
                ("quick_match", QuickMatchScreen.Show), ("tournament", TournamentScreen.Show), ("challenges", () => ChallengesScreen.Show(0)),
                ("squad", SquadScreen.Show), ("shop_bats", () => ShopScreen.Show(0)), ("shop_coaches", () => ShopScreen.Show(3)), ("settings", SettingsScreen.Show),
                ("stats", () => StatsScreen.Show(0)), ("achievements", () => StatsScreen.Show(1)), ("how_to", () => HowToScreen.Show()), ("team_select", () => TeamSelectScreen.Show(false)),
            };
            foreach (var (name, show) in screens)
            {
                show();
                yield return Wait(0.9f);
                yield return Snap(name);
            }
            ChallengesScreen.Detail(Challenges.ById(1));
            yield return Wait(0.8f);
            yield return Snap("challenge_detail");
            Popup.CloseAll();
            // A purchase, to check the shop and save.
            var save = SaveSystem.Data;
            save.Coins = Math.Max(save.Coins, 1500);
            var r = Shop.Buy(save, "bat_kashmir");
            Debug.Log("[KPL] AUTO shop buy: " + r + ", coins now " + save.Coins + ", bat " + save.Bat);
            SaveSystem.Save();
            MainMenu.Show();
            yield return Wait(0.5f);
        }

        IEnumerator QuickMatch(bool night, bool batFirst)
        {
            Taken.RemoveWhere(t => t.StartsWith(night ? "n_" : "d_"));
            TossBat = batFirst;
            var save = SaveSystem.Data;
            var ctx = Matches.Quick(Teams.ById(save.Fav), Teams.ById(night ? "KOL" : "CHE"), 2, night);
            GameApp.I.StartMatch(ctx);
            Prefix = night ? "n_" : "d_";
            float t = 0;
            bool paused = false;
            while (GameApp.I.Match == null && t < 60)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[KPL] AUTO match started after " + t.ToString("0.0") + " s (" + (night ? "night" : "day") + ")");
            t = 0;
            while (GameApp.I.Match != null && t < 900)
            {
                t += Time.unscaledDeltaTime;
                var md = GameApp.I.Match;
                if (!paused && md.BallsBowled == 3)
                {
                    paused = true;
                    md.SetPaused(true);
                    yield return Wait(0.6f);
                    yield return Snap(Prefix + "pause");
                    md.SetPaused(false);
                }
                yield return null;
            }
            Debug.Log("[KPL] AUTO match finished in " + t.ToString("0") + " s");
            yield return Wait(1.5f);
            yield return Snap(Prefix + "result");
            yield return Wait(0.5f);
            MainMenu.Show();
            yield return Wait(0.5f);
        }

        public static string Prefix = "d_";

        /// <summary>A few deliveries of a day match, for checking visuals quickly.</summary>
        IEnumerator QuickLook()
        {
            TossBat = true;
            Prefix = "look_";
            var save = SaveSystem.Data;
            GameApp.I.StartMatch(Matches.Quick(Teams.ById(save.Fav), Teams.ById("CHE"), 2, false));
            float t = 0;
            while (GameApp.I.Match == null && t < 60)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            t = 0;
            while (GameApp.I.Match != null && GameApp.I.Match.BallsBowled < 3 && t < 120)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (GameApp.I.Match != null) GameApp.I.Match.Quit();
            yield return Wait(1.5f);
        }

        public static void Tag(string tag) => Moment(Prefix + tag);

        IEnumerator TourSim()
        {
            var save = SaveSystem.Data;
            save.Tour = Tournament.Create(TourFormat.Express, 5, save.Fav, 1234, Difficulty.Medium);
            save.HasTour = true;
            Progression.SimAhead(save.Tour);
            int guard = 0;
            var t = save.Tour;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!t.Complete && guard++ < 40)
            {
                var f = t.NextUserFixture();
                if (f == null)
                {
                    var before = t.StageEnum;
                    Progression.SimAhead(t);
                    Progression.AfterTournamentProgress(t, save, new RecordSummary(), before);
                    continue;
                }
                var m = Progression.SimUserFixture(t, f);
                Progression.Record(Matches.Tournament(t, f), m, 0);
            }
            Debug.Log("[KPL] AUTO tournament: complete=" + t.Complete + " champion=" + t.Champion + " games=" + t.Fixtures.Count + " in " + sw.ElapsedMilliseconds + " ms; user " + t.Row(t.UserTeam).W + "W/" + t.Row(t.UserTeam).L + "L");
            TournamentScreen.Show();
            yield return Wait(0.9f);
            yield return Snap("tour_overview");
            typeof(TournamentScreen).GetField("_tab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, 1);
            TournamentScreen.Show();
            yield return Wait(0.9f);
            yield return Snap("tour_table");
            typeof(TournamentScreen).GetField("_tab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, 3);
            TournamentScreen.Show();
            yield return Wait(0.9f);
            yield return Snap("tour_stats");
            typeof(TournamentScreen).GetField("_tab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, 0);
            MainMenu.Show();
            yield return Wait(0.5f);
        }

        IEnumerator Challenge()
        {
            Prefix = "c_";
            GameApp.I.StartMatch(Matches.Challenge(Challenges.ById(5)));
            float t = 0;
            while (GameApp.I.Match == null && t < 60)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            t = 0;
            while (GameApp.I.Match != null && t < 300)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Wait(1.2f);
            yield return Snap("challenge_result");
            MainMenu.Show();
            yield return Wait(0.5f);
        }

        IEnumerator Graphics()
        {
            var save = SaveSystem.Data;
            int keep = save.Settings.Graphics;
            for (int g = 0; g < 4; g++)
            {
                Quality.Apply((Gfx)g);
                MainMenu.Show();
                yield return Wait(1.2f);
                yield return Snap("gfx_" + Quality.Names[g].ToLowerInvariant());
            }
            Quality.Apply((Gfx)Mathf.Max(0, keep));
            yield return null;
        }

        static void SaveCheck()
        {
            var before = SaveSystem.Data;
            int coins = before.Coins, wins = before.Stats.Wins, matches = before.Stats.Matches;
            SaveSystem.Save();
            SaveSystem.Load();
            var after = SaveSystem.Data;
            bool ok = after.Coins == coins && after.Stats.Wins == wins && after.Stats.Matches == matches;
            Debug.Log("[KPL] AUTO save round-trip " + (ok ? "OK" : "MISMATCH") + ": coins " + after.Coins + ", matches " + after.Stats.Matches + ", wins " + after.Stats.Wins + ", stars " + after.TotalStars);
        }
    }
}
