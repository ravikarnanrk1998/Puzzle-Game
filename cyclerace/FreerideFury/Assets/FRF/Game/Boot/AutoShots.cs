using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Scripted tour for store screenshots and smoke tests: launch the player with
    /// <c>-frfShots &lt;folder&gt;</c> and it walks every menu, then races each mountain on
    /// autopilot, saving PNGs and logging stats, then quits. <c>-frfTrack id</c> picks one mountain.
    /// Not active in normal play.
    /// </summary>
    public sealed class AutoShots : MonoBehaviour
    {
        string _dir;
        int _n;
        string _only;

        public static void StartIfRequested(GameApp app)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-frfShots");
            if (i < 0 || i + 1 >= args.Length) return;
            Dbg.StartWatchdog();
            var shots = app.gameObject.AddComponent<AutoShots>();
            shots._dir = Path.IsPathRooted(args[i + 1]) ? args[i + 1] : Path.Combine(Application.persistentDataPath, args[i + 1]);
            Directory.CreateDirectory(shots._dir);
            int t = Array.IndexOf(args, "-frfTrack");
            if (t >= 0 && t + 1 < args.Length) shots._only = args[t + 1];
            shots.StartCoroutine(shots.Tour());
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            _n++;
            string path = Path.Combine(_dir, _n.ToString("00") + "_" + name + ".png");
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[FRF] shot " + path);
            yield return null;
        }

        static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float t = 0;
            while (!cond() && t < timeout)
            {
                // Clamped so a long stall (window unfocused, GC) cannot eat the whole timeout at once.
                t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                yield return null;
            }
        }

        static IEnumerator Wait(float s)
        {
            yield return new WaitForSecondsRealtime(s);
        }

        /// <summary>Presses each steering key with a synthetic touch and logs the steer value it gives.</summary>
        IEnumerator SteerTest()
        {
            var pad = FindAnyObjectByType<SteerPad>();
            if (pad == null || !pad.isActiveAndEnabled)
            {
                Debug.Log("[FRF] steer test skipped (no steer pad)");
                yield break;
            }
            var corners = new Vector3[4];
            ((RectTransform)pad.transform).GetWorldCorners(corners);
            foreach (float fx in new[] { 0.22f, 0.78f })
            {
                var pos = new Vector2(Mathf.Lerp(corners[0].x, corners[2].x, fx), Mathf.Lerp(corners[0].y, corners[2].y, 0.5f));
                var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = pos, pointerId = 7 };
                pad.OnPointerDown(ev);
                yield return Wait(0.25f);
                Debug.Log("[FRF] steer test " + (fx < 0.5f ? "LEFT" : "RIGHT") + " key -> " + pad.Value);
                if (fx < 0.5f) yield return Shot("steer_left_pressed");
                pad.OnPointerUp(ev);
            }
        }

        IEnumerator Tour()
        {
            // Give the tour some money so the garage shows owned and buyable bikes.
            int cash = Profile.Cash;
            yield return Wait(4);
            {
                yield return Shot("menu");
                Menus.Career();
                yield return Wait(1.2f);
                yield return Shot("career");
                Menus.QuickRace();
                yield return Wait(1.2f);
                yield return Shot("quick_race");
                Menus.Garage();
                yield return Wait(1.5f);
                yield return Shot("garage_rider");
                Menus.SettingsScreen();
                yield return Wait(1);
                yield return Shot("settings");
                Menus.HowTo();
                yield return Wait(1);
                yield return Shot("how_to_play");
            }

            GameApp.I.Autopilot = true;
            bool first = true;
            foreach (var track in Catalog.Tracks)
            {
                if (_only != null && track.Id != _only) continue;
                float t0 = Time.realtimeSinceStartup;
                GameApp.I.StartQuick(track, 5, Difficulty.Normal);
                yield return Until(() => GameApp.I.Race != null, 30);
                var d = GameApp.I.Race;
                Debug.Log("[FRF] " + track.Id + " built in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s, length " + d.Track.Length + " m, ramps " + d.Track.Ramps.Count +
                          ", drops " + d.Track.Drops.Count + ", obstacles " + d.Track.Obstacles.Count);
                yield return Wait(1.0f);
                yield return Shot(track.Id + "_countdown");
                yield return Until(() => d.Phase == RacePhase.Racing, 8);
                yield return Wait(2.5f);
                yield return Shot(track.Id + "_start");
                // Frame-rate sample over a few seconds of racing.
                int frames = 0;
                float ft = 0;
                while (ft < 5)
                {
                    ft += Time.unscaledDeltaTime;
                    frames++;
                    yield return null;
                }
                Debug.Log("[FRF] " + track.Id + " fps " + (frames / ft).ToString("0.0"));
                yield return Shot(track.Id + "_racing");
                if (first) yield return SteerTest();
                yield return Until(() => !d.Player.Grounded && d.Player.AirTime > 0.35f, 25);
                yield return Shot(track.Id + "_air");
                yield return Until(() => d.Player.TrickBlend > 0.8f || d.Player.Crashed, 20);
                yield return Shot(track.Id + "_trick");
                if (first)
                {
                    d.Hud.ShowPause();
                    yield return Wait(0.6f);
                    yield return Shot(track.Id + "_pause");
                    d.Hud.CloseOverlay();
                    d.SetPaused(false);
                }
                // Jump ahead near the finish to see the end of the race.
                yield return Wait(4);
                yield return Shot(track.Id + "_later");
                if (first)
                {
                    yield return Until(() => !d.Player.Crashed && d.Player.Grounded, 8);
                    foreach (var r in d.Riders)
                        if (r.IsPlayer || (!r.Crashed && r.Grounded))
                            r.PlaceAt(Mathf.Max(r.S, d.Track.FinishS - 120 + (r.IsPlayer ? 0 : (r.Slot - 2) * 6)), Mathf.Clamp(r.X, -3, 3));
                    foreach (var r in d.Riders) r.V = 14;
                    yield return Until(() => d.Phase == RacePhase.Finished, 30);
                    yield return Wait(0.5f);
                    yield return Shot(track.Id + "_finish");
                    yield return Until(() => d.Phase == RacePhase.Results, 12);
                    yield return Wait(1.2f);
                    yield return Shot(track.Id + "_results");
                    var res = d.Result;
                    if (res == null) Debug.LogError("[FRF] results never appeared");
                    else Debug.Log("[FRF] result: place " + res.Place + ", time " + Ui.Time(res.Time) + ", tricks " + res.Tricks + ", takedowns " + res.Takedowns + ", crashes " + res.Crashes + ", earned " + res.Total);
                }
                Debug.Log("[FRF] " + track.Id + " player crashes " + d.Player.Crashes + ", tricks " + d.Player.TrickScore + ", takedowns " + d.Player.Takedowns + ", S " + d.Player.S.ToString("0") + "/" + d.Track.FinishS.ToString("0"));
                int aiCrashes = 0;
                foreach (var r in d.Riders)
                    if (!r.IsPlayer) aiCrashes += r.Crashes;
                Debug.Log("[FRF] " + track.Id + " AI crashes " + aiCrashes + ", positions " + string.Join(",", d.Riders.ConvertAll(r => r.Def.Nick + ":" + r.Place)));
                d.Exit(ExitAction.Menu);
                yield return Until(() => GameApp.I.Race == null, 5);
                yield return Wait(1.5f);
                first = false;
            }
            GameApp.I.Autopilot = false;
            Profile.Cash = cash;
            PlayerPrefs.Save();
            Debug.Log("[FRF] tour complete");
            yield return Wait(0.5f);
            Application.Quit();
        }
    }
}
