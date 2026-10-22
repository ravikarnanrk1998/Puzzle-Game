using System;
using System.Collections;
using System.IO;
using System.Linq;
using PCC.Core;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Scripted tour for store screenshots and smoke tests: launch the player with
    /// <c>-pccShots &lt;folder&gt;</c> and it walks every screen and both match roles,
    /// saving PNGs, then quits. Not active in normal play.
    /// </summary>
    public sealed class AutoShots : MonoBehaviour
    {
        string _dir;
        int _n;

        public static void StartIfRequested(GameApp app)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pccShots");
            if (i < 0 || i + 1 >= args.Length) return;
            Dbg.StartWatchdog();
            int t = Array.IndexOf(args, "-pccToss");
            if (t >= 0 && t + 1 < args.Length && args[t + 1] == "aibat") MatchDirector.ForceAiBat = true;
            bool auction = Array.IndexOf(args, "-pccAuction") >= 0;
            var shots = app.gameObject.AddComponent<AutoShots>();
            // Relative folders go under persistentDataPath (the only writable place on Android).
            shots._dir = Path.IsPathRooted(args[i + 1]) ? args[i + 1] : Path.Combine(Application.persistentDataPath, args[i + 1]);
            Directory.CreateDirectory(shots._dir);
            shots.StartCoroutine(auction ? shots.AuctionTour() : shots.Tour());
        }

        static readonly string[] TourKeys = { "pcc.auction", "pcc.auction.live", "pcc.auction.use", "pcc.names", "pcc.team" };

        /// <summary>
        /// Mega auction walk-through: renames, teams, retentions, live bidding, pause + save & exit +
        /// resume, skip set, sim rest, summary. The player's own saved data is restored afterwards.
        /// </summary>
        IEnumerator AuctionTour()
        {
            var backup = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var k in TourKeys)
            {
                if (PlayerPrefs.HasKey(k)) backup[k] = k == "pcc.auction.use" ? PlayerPrefs.GetInt(k).ToString() : PlayerPrefs.GetString(k);
            }
            AuctionStore.Clear();
            AuctionStore.ClearLive();
            yield return Wait(4);

            // Renames.
            var star = PlayerDb.Instance.Squad("MUM").OrderByDescending(p => p.Overall).First();
            PlayerNames.Set(star.Id, "Captain Marvel");
            NameEditor.Show();
            yield return Wait(1.2f);
            yield return Shot("edit_names");
            Debug.Log("[PCC] rename check: " + (star.Name == "Captain Marvel" && star.ShortName == "C Marvel" ? "ok" : "FAILED"));

            AuctionScreens.Hub();
            yield return Wait(1.5f);
            yield return Shot("auction_teams");
            var team = Teams.ById("MUM");
            AuctionScreens.AutoKeep(team);
            AuctionScreens.Retention(team);
            yield return Wait(1.2f);
            yield return Shot("auction_retention_renamed");
            AuctionScreens.StartAuction(team);
            yield return Wait(0.6f);
            yield return Shot("auction_lot");
            var room = FindAnyObjectByType<AuctionRoom>();
            yield return Wait(1.0f);
            room.OnBid();
            yield return Wait(0.4f);
            yield return Shot("auction_user_bid");
            yield return Wait(2.2f);
            yield return Shot("auction_bidding");

            // Pause freezes the bidding; save & exit; resume from the menu.
            room.TogglePause();
            int lotsSaved = AuctionStore.LoadLive()?.Lots.Count ?? 0;
            string before = room.DebugState;
            yield return Wait(2.5f);
            Debug.Log("[PCC] auction pause check: " + (room.DebugState == before ? "frozen" : "STILL RUNNING (" + before + " -> " + room.DebugState + ")"));
            yield return Shot("auction_paused");
            room.SaveAndExit();
            yield return Wait(1.2f);
            Debug.Log("[PCC] auction live save: " + (AuctionStore.HasLive ? "present" : "MISSING") + " (" + lotsSaved + " lots)");
            AuctionScreens.Hub();
            yield return Wait(1.2f);
            yield return Shot("auction_resume_choice");
            AuctionScreens.Room(AuctionStore.LoadLive());
            yield return Wait(1.0f);
            room = FindAnyObjectByType<AuctionRoom>();
            yield return Shot("auction_resumed");
            yield return Until(() => room.ShowingResult, 25);
            yield return Wait(0.3f);
            yield return Shot("auction_hammer");
            yield return Wait(2.0f);
            room.OnSkipSet();
            yield return Wait(1.6f);
            yield return Shot("auction_next_set");
            room.OnSimRest();
            yield return Wait(2.5f);
            yield return Shot("auction_summary");
            Debug.Log("[PCC] auction saved: " + AuctionStore.HasResult);
            Debug.Log("[PCC] auction live save cleared after finish: " + (!AuctionStore.HasLive));
            Menus.ResetQuickMatch();
            Menus.QuickMatch();
            yield return Wait(1.5f);
            yield return Shot("quick_match_auction_squads");

            // Restore the player's own data.
            foreach (var k in TourKeys) PlayerPrefs.DeleteKey(k);
            foreach (var kv in backup)
            {
                if (kv.Key == "pcc.auction.use") PlayerPrefs.SetInt(kv.Key, int.Parse(kv.Value));
                else PlayerPrefs.SetString(kv.Key, kv.Value);
            }
            PlayerPrefs.Save();
            PlayerNames.Init();
            Debug.Log("[PCC] tour complete");
            yield return Wait(0.5f);
            Application.Quit();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            _n++;
            string path = Path.Combine(_dir, _n.ToString("00") + "_" + name + ".png");
            // CaptureScreenshot(path) prefixes persistentDataPath on mobile, so write the PNG ourselves.
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[PCC] shot " + path);
            yield return null;
        }

        static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float t = 0;
            while (!cond() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static IEnumerator Wait(float s)
        {
            yield return new WaitForSecondsRealtime(s);
        }

        IEnumerator Tour()
        {
            var batting = Settings.Batting;
            Settings.Batting = BattingControls.Buttons;
            yield return Wait(5);
            yield return Shot("menu");
            Menus.QuickMatch();
            yield return Wait(1.5f);
            yield return Shot("quick_match");
            Menus.SettingsScreen();
            yield return Wait(1);
            yield return Shot("settings");
            Menus.HowTo();
            yield return Wait(1);
            yield return Shot("how_to_play");

            // Night match, 2 overs.
            GameApp.I.StartQuickMatch(Teams.ById("MUM"), Teams.ById("CHC"), 2, false);
            yield return Wait(3);
            var d = GameApp.I.Match;
            yield return Shot("toss");
            d.CallToss(true);
            yield return Until(() => d.AwaitTossChoice || d.M.Started, 6);
            if (d.AwaitTossChoice)
            {
                yield return Shot("toss_won");
                d.ChooseBat(true);
                d.Hud.HideOverlay();
            }
            yield return Until(() => d.M.Started, 5);
            yield return Wait(1.5f);

            for (int role = 0; role < 2 && !d.M.Finished; role++)
            {
                if (d.UserBatting) yield return BatTour(d);
                else yield return BowlTour(d);
                if (role == 0)
                {
                    d.Sim(d.M.SimulateInnings);
                    yield return Until(() => d.Phase == MatchPhase.InningsBreak || d.M.Finished, 10);
                    yield return Wait(1);
                    yield return Shot("innings_break");
                    d.Hud.HideOverlay();
                    d.ContinueAfterInnings();
                    yield return Wait(1.5f);
                }
            }

            d.Hud.ShowPause();
            double clockAtPause = d.Clock;
            yield return Wait(1.5f);
            Debug.Log("[PCC] pause check: match clock " + (d.Clock == clockAtPause ? "frozen" : "STILL RUNNING"));
            yield return Shot("pause");
            d.Hud.HideOverlay();
            d.Hud.ShowScorecard();
            yield return Wait(0.6f);
            yield return Shot("scorecard");
            d.Hud.HideOverlay();
            d.Sim(d.M.SimulateToEnd);
            yield return Until(() => d.M.Finished, 10);
            yield return Wait(2);
            yield return Shot("result_card");
            d.Cfg.OnExit();
            yield return Wait(2.5f);
            yield return Shot("result_screen");

            // Afternoon match for the day look.
            GameApp.I.StartQuickMatch(Teams.ById("CHC"), Teams.ById("KOL"), 2, true);
            yield return Wait(3);
            d = GameApp.I.Match;
            d.CallToss(false);
            yield return Until(() => d.AwaitTossChoice || d.M.Started, 6);
            if (d.AwaitTossChoice)
            {
                d.ChooseBat(false);
                d.Hud.HideOverlay();
            }
            yield return Until(() => d.M.Started, 5);
            yield return Wait(1.5f);
            if (d.UserBatting) yield return BatTour(d, "day_");
            else yield return BowlTour(d, "day_");

            Settings.Batting = batting;
            Debug.Log("[PCC] tour complete");
            yield return Wait(0.5f);
            Application.Quit();
        }

        IEnumerator BatTour(MatchDirector d, string prefix = "")
        {
            for (int ball = 0; ball < 2; ball++)
            {
                Settings.Batting = ball == 0 ? BattingControls.Buttons : BattingControls.Swipe;
                yield return Until(() => d.Phase == MatchPhase.RunUp && d.PitchMarker != null && d.PitchMarker.Value.Alpha > 0.95f, 12);
                if (ball == 0) yield return Shot(prefix + "batting_pitch_marker");
                yield return Until(() => d.Phase == MatchPhase.Flight, 12);
                yield return Until(() => d.RingProgress != null && d.RingProgress < 0.45, 5);
                yield return Shot(prefix + (ball == 0 ? "batting_buttons" : "batting_swipe"));
                yield return Until(() => d.RingProgress != null && d.RingProgress < 0.03, 5);
                d.PlayShot(ball == 0 ? 35 : -40, ball == 1, false);
                yield return Wait(1.1f);
                yield return Shot(prefix + "shot_" + ball);
                yield return Until(() => d.Phase == MatchPhase.Ready || d.M.Finished, 15);
            }
            Settings.Batting = BattingControls.Buttons;
        }

        IEnumerator BowlTour(MatchDirector d, string prefix = "")
        {
            yield return Until(() => d.PickingBowler || (d.Phase == MatchPhase.Ready && d.M.Current.BowlerId != null), 8);
            if (d.PickingBowler)
            {
                yield return Wait(0.5f);
                yield return Shot(prefix + "bowler_picker");
                d.Hud.HideOverlay();
                d.PickBowler(d.M.SuggestBowler().Id);
            }
            for (int ball = 0; ball < 2; ball++)
            {
                yield return Until(() => d.Phase == MatchPhase.Ready && !d.PickingBowler, 15);
                d.Variation = ball;
                var cam = Camera.main;
                var aim = new V3(ball == 0 ? -0.15 : 0.05, Pitch.Length - (ball == 0 ? 1.2 : 6.0), 0).U;
                for (int k = 0; k < 20; k++)
                {
                    d.MoveAim(cam.WorldToScreenPoint(aim));
                    yield return null;
                }
                yield return Shot(prefix + "bowling_aim_" + ball);
                d.OnBowlButton();
                yield return Until(() => d.MeterValue > 0.45 && d.MeterValue < 0.55, 3);
                yield return Shot(prefix + "bowling_meter");
                d.OnBowlButton();
                yield return Wait(1.2f);
                yield return Shot(prefix + "bowling_delivery_" + ball);
                yield return Until(() => d.Phase == MatchPhase.Follow || d.Phase == MatchPhase.Result, 6);
                yield return Wait(0.6f);
                yield return Shot(prefix + "bowling_result_" + ball);
                yield return Until(() => d.Phase == MatchPhase.Ready || d.M.Finished, 15);
            }
        }
    }
}
