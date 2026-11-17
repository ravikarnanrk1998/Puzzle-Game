using System.Collections;
using System.Collections.Generic;
using System.IO;
using UA.Core;
using UnityEngine;
using UnityEngine.AI;

namespace UA.Game
{
    /// <summary>
    /// Automated play-test: visits every screen, buys and upgrades, plays survival / boss / campaign /
    /// daily stages with a bot, tours every biome, cycles graphics presets, checks the save round-trip,
    /// saves screenshots and logs "[UA] AUTOPILOT DONE errors=N".
    /// Launch with: -uaAuto -uaShots &lt;dir&gt; -uaPlan all|quick|menus|biomes|gfx|boss
    /// </summary>
    public static class AutoPilot
    {
        public static bool Active;
        public static PlayerInput Input;
        static string _dir, _plan;
        static Runner _runner;
        static int _shot;

        public static void CheckArgs()
        {
            if (!Args.Has("-uaAuto")) return;
            Active = true;
            _plan = Args.Get("-uaPlan", "all");
            string d = Args.Get("-uaShots", "shots");
            _dir = Path.IsPathRooted(d) ? d : Path.Combine(Application.persistentDataPath, d);
            Directory.CreateDirectory(_dir);
            // Optional preset override for testing: -uaGfx low|medium|ultra
            string gfx = Args.Get("-uaGfx", "").ToLowerInvariant();
            if (gfx == "low") Quality.Apply(Gfx.Low);
            else if (gfx == "medium") Quality.Apply(Gfx.Medium);
            else if (gfx == "ultra") Quality.Apply(Gfx.Ultra);
            // -uaNoArt: run with the procedural fallbacks only (imported art off) for A/B comparisons.
            if (Args.Has("-uaNoArt")) ArtLibrary.Enabled = false;
            Log.I("AUTOPILOT plan=" + _plan + " shots=" + _dir + " gfx=" + Quality.Names[(int)Quality.Current] + (ArtLibrary.Enabled ? "" : " art=off"));
        }

        public static void Begin()
        {
            var go = new GameObject("AutoPilot");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            _runner.StartCoroutine(_runner.Main(_plan));
        }

        static void Shot(string name)
        {
            string file = Path.Combine(_dir, (++_shot).ToString("00") + "_" + name + ".png");
            // -uaShotScale N renders screenshots at N x the window size (store screenshots from a small window).
            int.TryParse(Args.Get("-uaShotScale", "1"), out int scale);
            scale = Mathf.Clamp(scale, 1, 4);
            // Captured at the end of this frame and written here: ScreenCapture.CaptureScreenshot is deferred on Android
            // and saved the state of the next step instead (labels one screen late).
            if (_runner != null) _runner.StartCoroutine(Capture(file, scale));
            Log.I("SHOT " + file);
        }

        static IEnumerator Capture(string file, int scale)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture(scale);
            try { File.WriteAllBytes(file, tex.EncodeToPNG()); }
            catch (System.Exception e) { Log.E("AUTOPILOT: screenshot failed: " + e.Message); }
            Object.Destroy(tex);
        }

        sealed class Runner : MonoBehaviour
        {
            float _fpsAcc;
            int _fpsN;
            float _fpsMin = 999;
            double _cpuAcc, _rtAcc, _gpuAcc;
            int _ftN;
            readonly FrameTiming[] _ft = new FrameTiming[1];

            public IEnumerator Main(string plan)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                var d = SaveSystem.Data;
                if (plan != "fresh")
                {
                    // Test wallet so purchase paths can be exercised (autopilot only).
                    d.Coins = Safe.ClampCurrency(d.Coins + 400000);
                    d.Gems = Safe.ClampCurrency(d.Gems + 3000);
                    if (d.Level < 25) { d.Level = 25; d.SkillPoints += 24; }
                }
                if (plan == "fresh") yield return Fresh();
                if (plan == "ux") yield return Ux();
                if (plan == "trailer") yield return Trailer();
                if (plan == "nade") yield return NadeTest();
                if (plan == "fall")
                {
                    // The stage where the player was squeezed out of the level (Lab, dead-end corridors), three times.
                    for (int i = 0; i < 3; i++) yield return Stage(StageGen.Survival(25, (ulong)d.WorldSeed), 70, "elite25_" + i);
                }
                if (plan == "all" || plan == "menus" || plan == "quick") yield return Menus();
                if (plan == "all" || plan == "quick")
                {
                    yield return Stage(StageGen.Survival(1, (ulong)d.WorldSeed), 70, "s1");
                }
                if (plan == "all" || plan == "zoo") yield return Zoo();
                if (plan == "all" || plan == "guns") yield return Guns();
                if (plan == "vmlab") yield return VmLab();
                if (plan == "look") yield return Showcase();
                if (plan == "art") yield return ArtCheck();
                if (plan == "perf")
                {
                    // The same stage with imported characters off, on, and on in "lite" mode (2-bone skinning,
                    // no shadows): compare the STAGE lines (fps plus CPU / GPU frame times).
                    bool art = ArtLibrary.Enabled;
                    foreach (var v in new[] { "noart", "art", "artlite" })
                    {
                        ArtLibrary.Enabled = v != "noart";
                        ArtBody.Lite = v == "artlite";
                        yield return Stage(StageGen.Survival(1, (ulong)d.WorldSeed), 40, "perf_" + v);
                    }
                    ArtLibrary.Enabled = art;
                    ArtBody.Lite = false;
                }
                if (plan == "chars")
                {
                    for (int c = 0; c < Weapons.OutfitNames.Length; c++)
                    {
                        Screens.Show(new UpgradesScreen { Tab = 2, PreviewCharacter = c });
                        yield return new WaitForSecondsRealtime(1.5f);
                        Shot("character_" + c);
                        yield return null;
                    }
                    Screens.Home();
                }
                if (plan == "loads")
                {
                    // Level-build timing on the heaviest layouts at Medium and Ultra (see "LEVEL phases" lines).
                    foreach (var g in new[] { Gfx.Medium, Gfx.Ultra })
                    {
                        Quality.Apply(g);
                        foreach (var b in new[] { Biome.City, Biome.Residential, Biome.Frozen, Biome.Factory })
                        {
                            var p = StageGen.Build(new GenParams { Mode = GameMode.Survival, Stage = 30, Seed = Rng.Hash((int)b, 7), Biome = b });
                            yield return Stage(p, 3, "load_" + Quality.Names[(int)g] + "_" + b, true);
                        }
                    }
                    Quality.Apply(Quality.FromSave());
                }
                if (plan == "all" || plan == "boss")
                {
                    yield return Stage(StageGen.Survival(25, (ulong)d.WorldSeed), 70, "elite25");
                    yield return Stage(StageGen.Survival(50, (ulong)d.WorldSeed), 90, "boss50");
                    yield return Stage(StageGen.Survival(150, (ulong)d.WorldSeed), 60, "boss150");
                }
                if (plan == "all")
                {
                    yield return Stage(Campaign.Plan(1), 45, "campaign2");
                    yield return Stage(DailyChallenge.Plan(Clock.Today, d.HighestStage), 40, "daily");
                }
                if (plan == "all" || plan == "biomes")
                {
                    for (int b = 0; b < 15; b++)
                    {
                        var p = StageGen.Build(new GenParams { Mode = GameMode.Survival, Stage = 10 + b * 4, Seed = Rng.Hash(b, 99), Biome = (Biome)b });
                        yield return Stage(p, 14, "biome_" + (Biome)b, true);
                    }
                }
                if (plan == "store")
                {
                    // Play Store screenshot candidates: Ultra preset, a boss fight and several biomes, with a burst of
                    // shots whenever zombies are clearly on screen (pick the best frames afterwards).
                    Quality.Apply(Gfx.Ultra);
                    yield return Menus();
                    yield return Stage(StageGen.Survival(50, (ulong)d.WorldSeed), 75, "store_boss50", false, 3);
                    var looks = new[] { Biome.City, Biome.Metro, Biome.Hospital, Biome.Frozen, Biome.Factory, Biome.Residential, Biome.MilitaryBase };
                    for (int i = 0; i < looks.Length; i++)
                    {
                        var p = StageGen.Build(new GenParams { Mode = GameMode.Survival, Stage = 18 + i * 6, Seed = Rng.Hash(i, 2026), Biome = looks[i] });
                        yield return Stage(p, 42, "store_" + looks[i], false, 3);
                    }
                    Quality.Apply(Quality.FromSave());
                }
                if (plan == "all" || plan == "gfx")
                {
                    for (int g = 0; g < 3; g++)
                    {
                        Quality.Apply((Gfx)g);
                        yield return Stage(StageGen.Survival(7, (ulong)d.WorldSeed), 14, "gfx_" + Quality.Names[g], true);
                    }
                    Quality.Apply(Quality.FromSave());
                }
                // Save round trip.
                SaveSystem.Save();
                var text = SaveSystem.Encode(SaveSystem.Data);
                var back = SaveSystem.Decode(text, out var why);
                bool ok = back != null && back.Coins == SaveSystem.Data.Coins && back.Weapons.Count == SaveSystem.Data.Weapons.Count;
                Log.I("SAVE ROUNDTRIP " + (ok ? "OK" : "FAILED " + why));
                if (!ok) Log.Errors++;
                GameApp.Instance.ToMenu();
                yield return new WaitForSecondsRealtime(4);
                Shot("menu_end");
                yield return new WaitForSecondsRealtime(1);
                long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                Log.I("AUTOPILOT DONE errors=" + Log.Errors + " exceptions=" + Log.Exceptions + " mem=" + mem + "MB");
                yield return new WaitForSecondsRealtime(1);
                Application.Quit();
            }

            IEnumerator WaitStage()
            {
                float wait = 0;
                while (StageRunner.Instance == null || PlayerRig.Instance == null)
                {
                    wait += Time.unscaledDeltaTime;
                    if (wait > 60) { Log.E("Stage did not start"); yield break; }
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(1f);
            }

            /// <summary>Close-up line-up of every zombie type and boss in a well lit arena.</summary>
            IEnumerator Zoo()
            {
                var plan = StageGen.Build(new GenParams
                {
                    Mode = GameMode.Survival, Stage = 40, Seed = 777, Biome = Biome.Hospital, Layout = LayoutKind.Arena, Mood = LightMood.Fluorescent,
                    Objective = ObjectiveType.Survive, SurviveTime = 900, Event = StageEvent.None, Weather = Weather.Clear,
                });
                GameApp.Instance.Play(plan, 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var pl = PlayerRig.Instance;
                if (run == null || pl == null) yield break;
                run.Director.Paused = true;
                run.Director.ClearAll();
                pl.Invulnerable = true;
                pl.SetFlashlight(true);
                Input = default;
                var fwd = Quaternion.Euler(0, pl.Yaw, 0) * Vector3.forward;
                foreach (ZombieType t in System.Enum.GetValues(typeof(ZombieType)))
                {
                    var zs = new List<Zombie>();
                    for (int k = 0; k < 3; k++)
                    {
                        var right = Vector3.Cross(Vector3.up, fwd);
                        var pos = Level.OnNav(pl.transform.position + fwd * (t == ZombieType.Mutant ? 5.5f : 4.2f) + right * (k - 1) * 1.4f, 2);
                        var z = run.Director.SpawnOne(t, k == 2, pos, false);
                        if (z == null) continue;
                        z.Frozen = true;
                        z.transform.rotation = Quaternion.LookRotation(-fwd);
                        zs.Add(z);
                    }
                    yield return new WaitForSecondsRealtime(1.2f);
                    Shot("zoo_" + t);
                    yield return null;
                    foreach (var z in zs) z.Despawn();
                }
                // Rescue survivors (imported civilians when available).
                {
                    var right = Vector3.Cross(Vector3.up, fwd);
                    var survivors = new List<ZombieRig>();
                    for (int k = 0; k < 3; k++)
                    {
                        var look = new ZLook { Tone = 4, Decay = 0f, Tatter = 0.2f, EyeGlow = new Color(0.05f, 0.05f, 0.05f), Seed = k };
                        var rig = ZombieRig.Create(run.transform, new ZShape { Shirt = 2, Hair = 1 }, look, ZombieRig.ArtVariant("Survivors/Survivor", k, 3));
                        rig.transform.position = Level.OnNav(pl.transform.position + fwd * 4.2f + right * (k - 1) * 1.3f, 2);
                        rig.transform.rotation = Quaternion.LookRotation(-fwd);
                        survivors.Add(rig);
                    }
                    yield return new WaitForSecondsRealtime(1f);
                    Shot("zoo_survivors");
                    yield return null;
                    foreach (var s in survivors) Object.Destroy(s.gameObject);
                    // Pickups: medkit, ammo, grenades, armour (imported models where available).
                    var loot = new List<Pickup>();
                    var kinds = new[] { LootKind.Health, LootKind.Ammo, LootKind.Grenades, LootKind.Armor };
                    for (int k = 0; k < kinds.Length; k++)
                        loot.Add(Pickup.Create(kinds[k], Level.OnNav(pl.transform.position + fwd * 2.6f + right * (k - 1.5f) * 0.75f, 2), run.transform));
                    yield return new WaitForSecondsRealtime(1f);
                    Shot("zoo_pickups");
                    yield return null;
                    foreach (var l in loot) if (l != null) Object.Destroy(l.gameObject);
                }
                for (int b = 1; b <= 4; b++)
                {
                    var pos = Level.OnNav(pl.transform.position + fwd * 9f, 3);
                    var boss = Boss.Spawn((BossType)b, plan, pos, run.transform);
                    boss.transform.rotation = Quaternion.LookRotation(-fwd);
                    yield return new WaitForSecondsRealtime(1.3f);
                    Shot("zoo_boss_" + (BossType)b);
                    yield return null;
                    Object.Destroy(boss.gameObject);
                    Boss.Current = null;
                }
                run.Forfeit();
                yield return new WaitForSecondsRealtime(2f);
            }

            /// <summary>Imported-art quality check: each imported asset next to its procedural fallback.</summary>
            IEnumerator ArtCheck()
            {
                var ids = new[] { WeaponId.StandardPistol, WeaponId.TacticalSniper };
                var cgo = new GameObject("ArtCheck");
                var cv = cgo.AddComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.sortingOrder = 5000;
                var bg = new GameObject("Bg").AddComponent<UnityEngine.UI.RawImage>();
                bg.transform.SetParent(cgo.transform, false);
                bg.color = new Color(0.07f, 0.08f, 0.1f);
                var view = new GameObject("View").AddComponent<UnityEngine.UI.RawImage>();
                view.transform.SetParent(cgo.transform, false);
                foreach (var img in new[] { bg, view })
                {
                    img.rectTransform.anchorMin = Vector2.zero;
                    img.rectTransform.anchorMax = Vector2.one;
                    img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                }
                foreach (var id in ids)
                    foreach (bool on in new[] { true, false })
                    {
                        ArtLibrary.Enabled = on;
                        view.texture = WeaponPreview.Show(id, null);
                        WeaponPreview.Tick(0);
                        yield return new WaitForSecondsRealtime(0.8f);
                        Shot("art_" + id + "_preview_" + (on ? "imported" : "procedural"));
                        yield return null;
                    }
                WeaponPreview.Hide();
                Object.Destroy(cgo);
                // Characters tab: the soldier survivor.
                foreach (bool on in new[] { true, false })
                {
                    ArtLibrary.Enabled = on;
                    CharacterPreview.Invalidate();
                    Screens.Show(new UpgradesScreen { Tab = 2, PreviewCharacter = 0 });
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot("art_character_soldier_" + (on ? "imported" : "procedural"));
                    yield return null;
                }
                Screens.Home();
                CharacterPreview.Invalidate();
                // Settings, scrolled to the bottom: credits and third-party licence notices.
                Screens.Show(new SettingsScreen());
                yield return new WaitForSecondsRealtime(0.6f);
                foreach (var sr in Object.FindObjectsByType<UnityEngine.UI.ScrollRect>(FindObjectsSortMode.None)) sr.verticalNormalizedPosition = 0;
                yield return new WaitForSecondsRealtime(0.4f);
                Shot("art_settings_credits");
                yield return null;
                Screens.Home();
                foreach (bool on in new[] { true, false })
                {
                    ArtLibrary.Enabled = on;
                    yield return ArtZoo(on ? "imported" : "procedural");
                }
                foreach (bool on in new[] { true, false })
                {
                    ArtLibrary.Enabled = on;
                    yield return Guns(ids, on ? "imported" : "procedural");
                }
                ArtLibrary.Enabled = true;
            }

            /// <summary>Infected walkers (standing, then walking) and the Juggernaut in a lit arena.</summary>
            IEnumerator ArtZoo(string tag)
            {
                var plan = StageGen.Build(new GenParams
                {
                    Mode = GameMode.Survival, Stage = 40, Seed = 777, Biome = Biome.Hospital, Layout = LayoutKind.Arena, Mood = LightMood.Fluorescent,
                    Objective = ObjectiveType.Survive, SurviveTime = 900, Event = StageEvent.None, Weather = Weather.Clear,
                });
                GameApp.Instance.Play(plan, 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var pl = PlayerRig.Instance;
                if (run == null || pl == null) yield break;
                run.Director.Paused = true;
                run.Director.ClearAll();
                pl.Invulnerable = true;
                pl.SetFlashlight(true);
                Input = default;
                var fwd = Quaternion.Euler(0, pl.Yaw, 0) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var zs = new List<Zombie>();
                foreach (var (d, s) in new[] { (2.8f, 0f), (4.6f, -1.5f), (4.6f, 1.5f) })
                {
                    var z = run.Director.SpawnOne(ZombieType.Walker, false, Level.OnNav(pl.transform.position + fwd * d + right * s, 2), false);
                    if (z == null) continue;
                    z.Frozen = true;
                    z.transform.rotation = Quaternion.LookRotation(-fwd);
                    zs.Add(z);
                }
                yield return new WaitForSecondsRealtime(1.2f);
                Shot("art_walker_" + tag);
                yield return null;
                foreach (var z in zs) { z.Frozen = false; z.Aware = true; }
                yield return new WaitForSecondsRealtime(0.9f);
                Shot("art_walker_moving_" + tag);
                yield return null;
                foreach (var z in zs) z.Despawn();
                // Close-up of one head under the flashlight.
                var cz = run.Director.SpawnOne(ZombieType.Walker, false, Level.OnNav(pl.transform.position + fwd * 1.25f, 2), false);
                if (cz != null)
                {
                    cz.Frozen = true;
                    cz.transform.rotation = Quaternion.LookRotation(-fwd);
                    yield return new WaitForSecondsRealtime(0.8f);
                    Shot("art_walker_closeup_" + tag);
                    yield return null;
                    cz.Despawn();
                }
                var boss = Boss.Spawn(BossType.Juggernaut, plan, Level.OnNav(pl.transform.position + fwd * 9f, 3), run.transform);
                boss.transform.rotation = Quaternion.LookRotation(-fwd);
                yield return new WaitForSecondsRealtime(1.3f);
                Shot("art_juggernaut_" + tag);
                yield return null;
                Object.Destroy(boss.gameObject);
                Boss.Current = null;
                run.Forfeit();
                yield return new WaitForSecondsRealtime(2f);
            }

            /// <summary>Every weapon in hand: hip, aiming down sights and firing.</summary>
            IEnumerator Guns(WeaponId[] only = null, string tag = "")
            {
                var plan = StageGen.Build(new GenParams
                {
                    Mode = GameMode.Survival, Stage = 3, Seed = 4242, Biome = Biome.Metro, Mood = LightMood.Sodium,
                    Objective = ObjectiveType.Survive, SurviveTime = 900, Event = StageEvent.None,
                });
                GameApp.Instance.Play(plan, 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var pl = PlayerRig.Instance;
                if (run == null || pl == null) yield break;
                run.Director.Paused = true;
                run.Director.ClearAll();
                pl.Invulnerable = true;
                string sfx = string.IsNullOrEmpty(tag) ? "" : "_" + tag;
                foreach (WeaponId id in only ?? (WeaponId[])System.Enum.GetValues(typeof(WeaponId)))
                {
                    pl.Guns.DebugEquip(id);
                    Input = new PlayerInput { SelectSlot = -1 };
                    yield return new WaitForSecondsRealtime(0.6f);
                    Shot("gun_" + id + sfx);
                    yield return null;
                    Input = new PlayerInput { SelectSlot = -1, Aim = true };
                    yield return new WaitForSecondsRealtime(0.5f);
                    Shot("gun_" + id + "_ads" + sfx);
                    yield return null;
                    Input = new PlayerInput { SelectSlot = -1, Fire = true };
                    yield return new WaitForSecondsRealtime(id == WeaponId.Flamethrower ? 0.5f : 0.12f);
                    Shot("gun_" + id + "_fire" + sfx);
                    yield return null;
                    Input = new PlayerInput { SelectSlot = -1, ReloadPressed = true };
                    yield return null;
                    Input = new PlayerInput { SelectSlot = -1 };
                    yield return new WaitForSecondsRealtime(pl.Guns.W.St.Reload * 0.45f);
                    if (only != null || id == WeaponId.R4Carbine || id == WeaponId.PumpShotgun || id == WeaponId.LightMG || id == WeaponId.StandardPistol) Shot("gun_" + id + "_reload" + sfx);
                    yield return null;
                    pl.Guns.RefillAll();
                }
                Input = default;
                run.Forfeit();
                yield return new WaitForSecondsRealtime(2f);
            }

            /// <summary>Graphics review: showcase stages (no zombies), four view directions each.</summary>
            IEnumerator Showcase()
            {
                var looks = new (Biome b, LightMood m, Weather w)[]
                {
                    (Biome.Metro, LightMood.Sodium, Weather.Clear), (Biome.Metro, LightMood.Fluorescent, Weather.Clear), (Biome.Metro, LightMood.Emergency, Weather.Clear),
                    (Biome.City, LightMood.Night, Weather.Rain), (Biome.Hospital, LightMood.Fluorescent, Weather.Clear), (Biome.Factory, LightMood.Sodium, Weather.Clear),
                    (Biome.MilitaryBase, LightMood.Dusk, Weather.Clear), (Biome.Mall, LightMood.Fluorescent, Weather.Clear),
                };
                foreach (var (b, m, w) in looks)
                {
                    var plan = StageGen.Build(new GenParams { Mode = GameMode.Survival, Stage = 3, Seed = 4242, Biome = b, Mood = m, Weather = w, Objective = ObjectiveType.Survive, SurviveTime = 900, Event = StageEvent.None });
                    GameApp.Instance.Play(plan, 1);
                    yield return WaitStage();
                    var run = StageRunner.Instance;
                    var pl = PlayerRig.Instance;
                    if (run == null || pl == null) continue;
                    run.Director.Paused = true;
                    run.Director.ClearAll();
                    pl.Invulnerable = true;
                    Input = new PlayerInput { SelectSlot = -1 };
                    float yaw0 = pl.Yaw;
                    for (int k = 0; k < 4; k++)
                    {
                        pl.Yaw = yaw0 + k * 90;
                        pl.Pitch = 0;
                        yield return new WaitForSecondsRealtime(0.8f);
                        Shot("look_" + b + "_" + m + "_" + k);
                        yield return null;
                    }
                    Input = default;
                    run.Forfeit();
                    yield return new WaitForSecondsRealtime(1.5f);
                }
            }

            /// <summary>Viewmodel lighting diagnostics: one variable changed per screenshot.</summary>
            IEnumerator VmLab()
            {
                var plan = StageGen.Build(new GenParams
                {
                    Mode = GameMode.Survival, Stage = 3, Seed = 4242, Biome = Biome.Metro, Mood = LightMood.Sodium,
                    Objective = ObjectiveType.Survive, SurviveTime = 900, Event = StageEvent.None,
                });
                GameApp.Instance.Play(plan, 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var pl = PlayerRig.Instance;
                if (run == null || pl == null) yield break;
                run.Director.Paused = true;
                run.Director.ClearAll();
                pl.Invulnerable = true;
                pl.Guns.DebugEquip(WeaponId.R4Carbine);
                Input = new PlayerInput { SelectSlot = -1, Aim = true };
                yield return new WaitForSecondsRealtime(1.2f);
                var lights = pl.ViewCam.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    var ad = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                    Log.I("VMLAB light " + l.name + " layer=" + l.gameObject.layer + " on=" + l.isActiveAndEnabled + " int=" + l.intensity + " range=" + l.range + " cull=" + l.cullingMask + " rl=" + (ad != null ? ad.renderingLayers.ToString() : "none") + " pos=" + pl.ViewCam.transform.InverseTransformPoint(l.transform.position));
                }
                var rends = new List<Renderer>();
                foreach (var r in pl.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.layer == Layers.Viewmodel) rends.Add(r);
                foreach (var r in rends)
                {
                    var m = r.sharedMaterial;
                    Log.I("VMLAB rend " + r.name + " on=" + r.enabled + "/" + r.gameObject.activeInHierarchy + " rlm=" + r.renderingLayerMask + " mat=" + (m != null ? m.name + "/" + m.shader.name + " kw=" + string.Join(",", m.shaderKeywords) : "null") + " pos=" + pl.ViewCam.transform.InverseTransformPoint(r.bounds.center));
                }
                Log.I("VMLAB renderingLayers defined=" + string.Join(",", RenderingLayerMask.GetDefinedRenderingLayerNames()) + " mask=" + RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue() + " count=" + RenderingLayerMask.GetRenderingLayerCount());
                Log.I("VMLAB ambient mode=" + RenderSettings.ambientMode + " sky=" + RenderSettings.ambientSkyColor + " eq=" + RenderSettings.ambientEquatorColor + " gnd=" + RenderSettings.ambientGroundColor + " int=" + RenderSettings.ambientIntensity + " refl=" + RenderSettings.reflectionIntensity);
                Shot("vm_0_base");
                yield return null;
                foreach (var l in lights)
                {
                    var ad = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                    if (ad != null) ad.renderingLayers = 0xFFFF;
                    l.cullingMask = ~0;
                }
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_1_lightsAllLayers");
                yield return null;
                foreach (var r in rends) r.renderingLayerMask = 0xFFFF;
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_2_rendAllLayers");
                yield return null;
                var tg = new GameObject("TestLight") { layer = Layers.Viewmodel };
                tg.transform.SetParent(pl.ViewCam.transform, false);
                tg.transform.localPosition = new Vector3(0.1f, 0.25f, 0.2f);
                var tl = tg.AddComponent<Light>();
                tl.type = LightType.Point;
                tl.intensity = 4;
                tl.range = 3;
                tl.renderMode = LightRenderMode.ForcePixel;
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_3_testLight");
                yield return null;
                tg.layer = 0;
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_4_testLightLayer0");
                yield return null;
                pl.ViewCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                pl.WorldCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_5_noPost");
                yield return null;
                foreach (var r in rends) r.gameObject.layer = 0;
                tg.layer = 0;
                foreach (var l in lights) l.gameObject.layer = 0;
                yield return new WaitForSecondsRealtime(0.3f);
                Shot("vm_6_inWorldCam");
                yield return null;
                Input = default;
                run.Forfeit();
                yield return new WaitForSecondsRealtime(2f);
            }

            IEnumerator Menus()
            {
                yield return new WaitForSecondsRealtime(1);
                Shot("main_menu");
                yield return null;
                var d = SaveSystem.Data;
                // Shop: buy a few weapons and upgrades through the same calls the UI uses.
                foreach (var id in new[] { WeaponId.R4Carbine, WeaponId.PumpShotgun, WeaponId.LightMG, WeaponId.TacticalSniper, WeaponId.Flamethrower, WeaponId.ArcRifle })
                    if (!d.Owns(id)) Log.I("buy " + id + ": " + Wallet.BuyWeapon(d, id));
                for (int i = 0; i < 4; i++) Wallet.UpgradeWeapon(d, WeaponId.R4Carbine, WStat.Damage);
                Wallet.BuyAttachment(d, WeaponId.R4Carbine, Attachment.RedDot);
                Wallet.BuyAttachment(d, WeaponId.R4Carbine, Attachment.Foregrip);
                Wallet.BuySkin(d, WeaponId.R4Carbine, Skin.UrbanDigital);
                Wallet.UpgradePlayer(d, PStat.Health);
                Skill.Unlock(d, SkillId.Vitality);
                Skill.Unlock(d, SkillId.Firepower);
                d.Loadout[0] = (int)WeaponId.R4Carbine;
                d.Loadout[1] = (int)WeaponId.PumpShotgun;
                Store.BuyOffer(d, Store.Offers[4]);
                d.Loadout[2] = (int)WeaponId.StandardPistol;
                SaveSystem.Save();
                UiScreen[] screens =
                {
                    new SurvivalScreen(), new CampaignScreen(), new DailyScreen(), new BossRushScreen(), new ArmoryScreen(), new UpgradesScreen(), new UpgradesScreen { Tab = 2 },
                    new StoreScreen(), new StoreScreen { Section = 1 }, new StoreScreen { Section = 3 }, new MissionsScreen(), new SettingsScreen(),
                };
                foreach (var s in screens)
                {
                    Screens.Show(s);
                    yield return new WaitForSecondsRealtime(1.2f);
                    Shot(s.GetType().Name + (s is StoreScreen st ? st.Section.ToString() : ""));
                    yield return null;
                }
                LoginPopup.Show();
                yield return new WaitForSecondsRealtime(0.8f);
                Shot("login_popup");
                yield return null;
                Screens.ClosePopups();
                Screens.Home();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // ------------------------------------------------------------ UX coverage plans

            static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

            /// <summary>Presses a visible button by its label, through the same callback a tap uses.</summary>
            static bool Click(string label)
            {
                var go = GameObject.Find("Btn_" + label);
                var b = go != null ? go.GetComponent<UiButton>() : null;
                if (b == null || b.OnClick == null) { Log.E("AUTOPILOT: button not found: " + label); return false; }
                b.OnClick();
                return true;
            }

            /// <summary>Sets a screen's private field (tab, chapter) and rebuilds it.</summary>
            static void SetField(object o, string name, object v)
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var f = o.GetType().GetField(name, F);
                if (f == null) { Log.E("AUTOPILOT: no field " + name + " on " + o.GetType().Name); return; }
                f.SetValue(o, v);
                o.GetType().GetMethod("Rebuild", F)?.Invoke(o, null);
            }

            static void ScrollTo(float pos)
            {
                var root = Screens.Current?.Root;
                var sr = root != null ? root.GetComponentInChildren<UnityEngine.UI.ScrollRect>() : null;
                if (sr != null) sr.verticalNormalizedPosition = pos;
            }

            /// <summary>Screenshot, then two frames before anything else changes (the capture runs at end of frame).</summary>
            IEnumerator Snap(string tag)
            {
                Shot(tag);
                yield return null;
                yield return null;
            }

            IEnumerator ShowShot(UiScreen s, string tag)
            {
                Screens.Show(s);
                yield return Wait(1.2f);
                yield return Snap(tag);
            }

            IEnumerator WaitResult(float max = 10)
            {
                float w = 0;
                while (!(Screens.Current is ResultScreen) && w < max) { w += Time.unscaledDeltaTime; yield return null; }
                yield return Wait(1f);
            }

            /// <summary>A brand-new player: no test wallet, every menu at level 1, then stage 1.</summary>
            IEnumerator Fresh()
            {
                yield return Wait(1);
                yield return Snap("fresh_main_menu");
                LoginPopup.Show();
                yield return Wait(0.8f);
                yield return Snap("fresh_login_popup");
                Screens.ClosePopups();
                var screens = new List<UiScreen>
                {
                    new SurvivalScreen(), new CampaignScreen(), new DailyScreen(), new BossRushScreen(), new ArmoryScreen(),
                    new UpgradesScreen(), new UpgradesScreen { Tab = 1 }, new UpgradesScreen { Tab = 2 }, new MissionsScreen(), new SettingsScreen(),
                };
                for (int i = 0; i < Store.Sections.Length; i++) screens.Add(new StoreScreen { Section = i });
                foreach (var s in screens)
                    yield return ShowShot(s, "fresh_" + s.GetType().Name + (s is StoreScreen st ? "_" + Store.Sections[st.Section] : s is UpgradesScreen us ? "_" + us.Tab : ""));
                Screens.Home();
                yield return Wait(1);
                yield return Stage(StageGen.Survival(1, (ulong)SaveSystem.Data.WorldSeed), 70, "fresh_s1");
            }

            /// <summary>Every tab, dialog and in-game flow the menus pass does not reach.</summary>
            IEnumerator Ux()
            {
                var d = SaveSystem.Data;
                yield return ShowShot(new UpgradesScreen { Tab = 1 }, "ux_skill_tree");
                for (int i = 0; i < Store.Sections.Length; i++) yield return ShowShot(new StoreScreen { Section = i }, "ux_store_" + Store.Sections[i]);
                for (int t = 1; t < 4; t++)
                {
                    var m = new MissionsScreen();
                    Screens.Show(m);
                    SetField(m, "_tab", t);
                    yield return Wait(1f);
                    yield return Snap("ux_missions_tab" + t);
                }
                var camp = new CampaignScreen();
                Screens.Show(camp);
                SetField(camp, "_sel", 6);
                SetField(camp, "_chapter", 1);
                yield return Wait(1f);
                yield return Snap("ux_campaign_chapter2");
                for (int t = 1; t < 3; t++)
                {
                    var a = new ArmoryScreen();
                    Screens.Show(a);
                    SetField(a, "_tab", t);
                    yield return Wait(1.2f);
                    yield return Snap("ux_armory_tab" + t);
                }
                Screens.Show(new SettingsScreen());
                yield return Wait(1f);
                ScrollTo(0.5f);
                yield return Wait(0.6f);
                yield return Snap("ux_settings_middle");
                ScrollTo(0);
                yield return Wait(0.6f);
                yield return Snap("ux_settings_bottom");
                Screens.Confirm("BUY BEHEMOTH .50?", "Spend 480 gems on the Behemoth .50.", "BUY", null);
                yield return Wait(0.6f);
                yield return Snap("ux_confirm_dialog");
                Screens.ClosePopups();
                Screens.Message("STORE UNAVAILABLE", Iap.Status);
                yield return Wait(0.6f);
                yield return Snap("ux_message_dialog");
                Screens.ClosePopups();
                Screens.Toast("Layout saved");
                Screens.Toast("+500 COINS");
                yield return Wait(0.5f);
                yield return Snap("ux_toasts");
                LayoutEditor.Open();
                yield return Wait(1.2f);
                yield return Snap("ux_layout_editor");
                var le = GameObject.Find("LayoutEditor");
                if (le != null) Object.Destroy(le);
                Screens.Home();
                yield return Wait(1);

                // In game: low health, pause, in-game settings, resume, death, revive, give up.
                GameApp.Instance.Play(StageGen.Survival(3, (ulong)d.WorldSeed), 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var p = PlayerRig.Instance;
                if (run != null && p != null)
                {
                    p.Invulnerable = true;
                    yield return Wait(6);
                    yield return Snap("ux_ingame");
                    p.Hp = p.MaxHp * 0.18f;
                    yield return Wait(1.5f);
                    yield return Snap("ux_low_health");
                    p.Hp = p.MaxHp;
                    run.Pause();
                    yield return Wait(0.8f);
                    yield return Snap("ux_pause");
                    Click("SETTINGS");
                    yield return Wait(1.2f);
                    yield return Snap("ux_ingame_settings");
                    Click("DONE");
                    yield return Wait(0.8f);
                    yield return Snap("ux_pause_after_settings");
                    Click("RESUME");
                    yield return Wait(2);
                    yield return Snap("ux_resumed");
                    p.Invulnerable = false;
                    p.Hurt(1e6f, p.transform.position + p.transform.forward, HurtKind.Melee);
                    p.Hurt(1e6f, p.transform.position + p.transform.forward, HurtKind.Melee);   // a second hit in case Second Wind saved the first
                    yield return Wait(2.5f);
                    yield return Snap("ux_revive");
                    Click("REVIVE  (20 GEMS)");
                    yield return Wait(2);
                    yield return Snap("ux_revived");
                    p.Invulnerable = false;
                    p.Hurt(1e6f, p.transform.position + p.transform.forward, HurtKind.Explosion);
                    yield return Wait(2.5f);
                    yield return Snap("ux_revive_used");
                    Click("GIVE UP");
                    yield return WaitResult();
                    yield return Snap("ux_failed_result");
                }
                // Quit through the pause menu.
                GameApp.Instance.Play(StageGen.Survival(2, (ulong)d.WorldSeed), 1);
                yield return WaitStage();
                run = StageRunner.Instance;
                if (run != null)
                {
                    PlayerRig.Instance.Invulnerable = true;
                    yield return Wait(4);
                    run.Pause();
                    yield return Wait(0.6f);
                    Click("QUIT TO MENU");
                    yield return Wait(0.8f);
                    yield return Snap("ux_quit_confirm");
                    Click("QUIT");
                    yield return WaitResult();
                    yield return Snap("ux_quit_result");
                }
                GameApp.Instance.ToMenu();
                yield return Wait(3);
                // Modes with their own result screens.
                yield return Stage(BossRush.Plan(1), 45, "ux_bossrush");
                yield return Stage(Campaign.Plan(0), 100, "ux_campaign1");
                yield return Stage(DailyChallenge.Plan(Clock.Today, d.HighestStage), 40, "ux_daily");
            }

            // ------------------------------------------------------------ grenade aiming ("nade" plan)

            /// <summary>
            /// Hold-to-aim grenade: holds the button (arc visible) while looking level, down and up, releases, and logs
            /// where the arc predicted the explosion against where it happened. Outdoors and in the metro (bounces).
            /// </summary>
            IEnumerator NadeTest()
            {
                var d = SaveSystem.Data;
                var stages = new[]
                {
                    StageGen.Build(new GenParams { Stage = 12, Seed = Rng.Hash(21, 2026), Biome = Biome.Airport, Objective = ObjectiveType.Survive, Mood = LightMood.Overcast, Weather = Weather.Clear }),
                    StageGen.Build(new GenParams { Stage = 12, Seed = Rng.Hash(22, 2026), Biome = Biome.Metro, Objective = ObjectiveType.Survive }),
                };
                int k = 0;
                foreach (var plan in stages)
                {
                    k++;
                    GameApp.Instance.Play(plan, 1);
                    yield return WaitStage();
                    var p = PlayerRig.Instance;
                    if (p == null) continue;
                    p.Invulnerable = true;
                    p.Guns.Grenades = 5;   // test only: two throws plus the cancel check
                    Input = new PlayerInput { SelectSlot = -1 };
                    yield return Wait(3);
                    for (int throwN = 0; throwN < 2; throwN++)
                    {
                        string tag = "nade" + k + "_" + throwN;
                        Input = new PlayerInput { SelectSlot = -1, GrenadeHeld = true };
                        yield return Wait(0.8f);
                        yield return Snap(tag + "_hold_level");
                        // Look down ~14 degrees, then up ~28 (positive Look.y looks down).
                        for (int f = 0; f < 14; f++) { Input.Look = new Vector2(0, 1); yield return null; }
                        Input.Look = Vector2.zero;
                        yield return Wait(0.4f);
                        yield return Snap(tag + "_hold_down");
                        for (int f = 0; f < 28; f++) { Input.Look = new Vector2(throwN == 0 ? 0.5f : -0.5f, -1); yield return null; }
                        Input.Look = Vector2.zero;
                        yield return Wait(0.4f);
                        yield return Snap(tag + "_hold_up");
                        Log.I("NADE " + tag + " aiming=" + p.Guns.GrenadeAiming + " grenades=" + p.Guns.Grenades + " predicted " + GrenadeArc.LastPredicted.ToString("F2"));
                        Input.GrenadeHeld = false;   // release: throw
                        yield return Wait(0.6f);
                        yield return Snap(tag + "_thrown");
                        yield return Wait(1.75f);
                        yield return Snap(tag + "_explode");
                        yield return Wait(1.5f);
                        // Back to level for the next throw.
                        for (int f = 0; f < 14; f++) { Input.Look = new Vector2(0, 1); yield return null; }
                        Input.Look = Vector2.zero;
                    }
                    // Cancel: hold, then the touch input is reset (as when pausing): no throw, grenade kept.
                    int before = p.Guns.Grenades;
                    Input = new PlayerInput { SelectSlot = -1, GrenadeHeld = true };
                    yield return Wait(0.6f);
                    Input = new PlayerInput { SelectSlot = -1, GrenadeCancel = true };
                    yield return null;
                    yield return null;   // two frames: the input is copied before or after Gunplay reads it
                    Input = new PlayerInput { SelectSlot = -1 };
                    yield return Wait(1f);
                    Log.I("NADE cancel test: grenades " + before + " -> " + p.Guns.Grenades + (p.Guns.Grenades == before ? " (kept, OK)" : " (LOST)"));
                    if (p.Guns.Grenades != before) Log.Errors++;
                    var run = StageRunner.Instance;
                    if (run != null && !run.Ended) run.Forfeit();
                    yield return WaitResult();
                }
                Input = default;
                GameApp.Instance.ToMenu();
                yield return Wait(3);
            }

            // ------------------------------------------------------------ promo video ("trailer" plan)

            /// <summary>Bot camera speed (1 = test speed; the trailer turns more slowly so the footage is not jerky).</summary>
            static float LookGain = 1;

            enum Cue { Horde, Crowd, Boss, Scope }

            /// <summary>
            /// Promo video for the store listing: title card, gameplay clips with captions, two menu shots and an end
            /// card, captured by <see cref="TrailerKit"/> into the -uaShots folder. Encode with Tools\ua.ps1 promovideo.
            /// </summary>
            IEnumerator Trailer()
            {
                var d = SaveSystem.Data;
                foreach (var id in new[] { WeaponId.HeavyMG, WeaponId.HeavyShotgun, WeaponId.Flamethrower, WeaponId.TacticalSniper, WeaponId.RocketLauncher, WeaponId.ArcRifle, WeaponId.R4Carbine, WeaponId.PumpShotgun })
                    if (!d.Owns(id)) Log.I("trailer buy " + id + ": " + Wallet.BuyWeapon(d, id));
                float music = d.Settings.Music;
                d.Settings.Music = 0;   // one continuous music bed is mixed in by the encoder instead
                Quality.Apply(Gfx.Ultra);
                LookGain = 0.5f;
                int.TryParse(Args.Get("-uaShotScale", "1"), out int scale);
                var kit = new TrailerKit(_dir, Mathf.Clamp(scale, 1, 4));
                var seed = (ulong)d.WorldSeed;
                Screens.Home();
                yield return Wait(2);
                yield return Record(kit, 60, null, null, card: true);
                yield return Clip(kit, StageGen.Build(new GenParams { Stage = 60, Seed = Rng.Hash(11, 2026), Biome = Biome.Metro, Objective = ObjectiveType.Eliminate, Event = StageEvent.HordeRush }), WeaponId.HeavyShotgun, 150, "SURVIVE", " THE HORDE", Cue.Crowd);
                yield return Clip(kit, StageGen.Survival(50, seed), WeaponId.HeavyMG, 180, "FIGHT", " EPIC BOSSES", Cue.Boss);
                yield return Clip(kit, StageGen.Build(new GenParams { Stage = 30, Seed = Rng.Hash(12, 2026), Biome = Biome.Factory, Objective = ObjectiveType.Eliminate }), WeaponId.Flamethrower, 120, "MASTER", " 20 WEAPONS", Cue.Horde);
                yield return Clip(kit, StageGen.Build(new GenParams { Stage = 40, Seed = Rng.Hash(13, 2026), Biome = Biome.Frozen, Objective = ObjectiveType.Eliminate }), WeaponId.R4Carbine, 120, "15", " DEADLY LOCATIONS", Cue.Horde);
                yield return Clip(kit, StageGen.Build(new GenParams { Stage = 28, Seed = Rng.Hash(14, 2026), Biome = Biome.MilitaryBase, Objective = ObjectiveType.Eliminate }), WeaponId.TacticalSniper, 105, null, null, Cue.Scope);
                // Daylight: at night the airport was too dark to read on a phone screen.
                yield return Clip(kit, StageGen.Build(new GenParams { Stage = 36, Seed = Rng.Hash(15, 2026), Biome = Biome.Airport, Objective = ObjectiveType.Eliminate, Event = StageEvent.HordeRush, Mood = LightMood.Overcast, Weather = Weather.Clear }), WeaponId.RocketLauncher, 120, null, null, Cue.Horde);
                yield return Clip(kit, StageGen.Survival(150, seed), WeaponId.ArcRifle, 150, "INFINITE", " SURVIVAL", Cue.Boss);
                GameApp.Instance.ToMenu();
                yield return Wait(4);
                Screens.Show(new ArmoryScreen());
                yield return Wait(1.5f);
                yield return Record(kit, 90, "UPGRADE", " YOUR ARSENAL", bot: false);
                Screens.Show(new BossRushScreen());
                yield return Wait(1.5f);
                yield return Record(kit, 75, "TAKE ON", " BOSS RUSH", bot: false);
                yield return Record(kit, 105, null, null, card: true, sub: "HOW LONG CAN YOU SURVIVE?", bot: false);
                kit.Finish();
                d.Settings.Music = music;
                LookGain = 1;
                Quality.Apply(Quality.FromSave());
                Screens.Home();
                yield return Wait(1);
            }

            /// <summary>Plays the stage in real time until the moment is right, then records <paramref name="frames"/> frames.</summary>
            IEnumerator Clip(TrailerKit kit, StagePlan plan, WeaponId weapon, int frames, string accent, string rest, Cue cue)
            {
                var d = SaveSystem.Data;
                if (d.Owns(weapon)) d.Loadout[0] = (int)weapon;
                GameApp.Instance.Play(plan, 1);
                yield return WaitStage();
                var run = StageRunner.Instance;
                var p = PlayerRig.Instance;
                if (run == null || p == null) yield break;
                p.Invulnerable = true;
                if (Hud.Instance != null && Hud.Instance.Touch != null) Hud.Instance.Touch.SetVisible(false);
                float swapT = 999, nadeT = 6, t = 0;
                while (t < 60 && !run.Ended && !Ready(p, cue))
                {
                    float dt = Time.unscaledDeltaTime;
                    t += dt;
                    Bot(p, run, ref swapT, ref nadeT, dt, false);
                    yield return null;
                }
                Log.I("TRAILER clip " + plan.Title + " (" + weapon + ") ready after " + t.ToString("0.0") + " s");
                yield return Record(kit, frames, accent, rest);
                if (run != null && !run.Ended) run.Forfeit();
                float w2 = 0;
                while (run != null && run.Result == null && w2 < 6) { w2 += Time.unscaledDeltaTime; yield return null; }
                yield return Wait(0.5f);
            }

            static bool Ready(PlayerRig p, Cue cue)
            {
                switch (cue)
                {
                    case Cue.Boss:
                    {
                        var b = Boss.Current;
                        if (b == null || !b.IsAlive) return false;
                        var vp = p.WorldCam.WorldToViewportPoint(b.Rig[ZB.Chest].position);
                        return vp.z > 3 && vp.z < 30 && vp.x > 0.25f && vp.x < 0.75f && vp.y > 0.1f && vp.y < 0.95f;
                    }
                    case Cue.Scope: return p.Guns.Scoped && OnScreen(p) >= 1;
                    case Cue.Crowd: return OnScreen(p) >= 5;
                    default: return OnScreen(p) >= 3;
                }
            }

            /// <summary>Records frames at a fixed 30 fps game time with the bot playing (when a stage is running).</summary>
            IEnumerator Record(TrailerKit kit, int frames, string accent, string rest, bool card = false, string sub = null, bool bot = true)
            {
                var p = PlayerRig.Instance;
                var run = StageRunner.Instance;
                bool drive = bot && p != null && run != null;
                float swapT = 999, nadeT = 4;
                kit.Begin(accent, rest, card, sub);
                for (int i = 0; i < frames; i++)
                {
                    kit.Pose(i, frames);
                    if (drive) Bot(p, run, ref swapT, ref nadeT, 1f / TrailerKit.Fps, false);
                    yield return new WaitForEndOfFrame();
                    if (i == 2) Log.I("TRAILER time step " + Time.deltaTime.ToString("0.0000") + " unscaled " + Time.unscaledDeltaTime.ToString("0.0000"));
                    kit.Capture();
                }
                Input = default;
                kit.End();
            }

            /// <summary>Zombies in the middle of the view, within 28 m and not hidden behind walls.</summary>
            static int OnScreen(PlayerRig p)
            {
                var cam = p.WorldCam;
                int n = 0;
                foreach (var z in Zombie.Alive)
                {
                    var head = z.Rig[ZB.Head].position;
                    var vp = cam.WorldToViewportPoint(head);
                    if (vp.z < 1.5f || vp.z > 28 || vp.x < 0.12f || vp.x > 0.88f || vp.y < 0.15f || vp.y > 0.9f) continue;
                    if (Physics.Linecast(cam.transform.position, head, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    n++;
                }
                return n;
            }

            IEnumerator Stage(StagePlan plan, float seconds, string tag, bool tourOnly = false, float burst = 0)
            {
                GameApp.Instance.Play(plan, plan.Mode == GameMode.BossRush ? 1 : 1);
                float wait = 0;
                while (StageRunner.Instance == null || PlayerRig.Instance == null)
                {
                    wait += Time.unscaledDeltaTime;
                    if (wait > 60) { Log.E("Stage did not start: " + tag); yield break; }
                    yield return null;
                }
                var run = StageRunner.Instance;
                var p = PlayerRig.Instance;
                p.Invulnerable = true;
                _fpsAcc = 0; _fpsN = 0; _fpsMin = 999;
                _cpuAcc = _rtAcc = _gpuAcc = 0; _ftN = 0;
                float t = 0;
                bool shot1 = false, shot2 = false, shot3 = false;
                float swapT = 20, nadeT = 8;
                float burstT = 6;
                int bursts = 0;
                // Minimap check: the player must always stand on (or within 0.5 m of) a cell the minimap draws as floor.
                // Starts after 1 s: on the first frame the player has not been placed yet.
                float mapT = 1;
                int mapChecks = 0, mapOpen = 0;
                // Flight recorder: the last 4 s of player positions, dumped once if the player drops below the floor.
                var trail = new Queue<string>();
                float trailT = 0;
                bool fellLogged = false;
                Vector3 lastGood = PlayerRig.Pos;
                while (t < seconds && run != null && !run.Ended)
                {
                    if (t > 1 && p != null)
                    {
                        var pp = PlayerRig.Pos;
                        if (t >= trailT)
                        {
                            trailT = t + 0.1f;
                            trail.Enqueue(t.ToString("0.0") + "s " + pp.ToString("F2") + (p.Grounded ? " G" : " air") + (p.Crouched ? " crouch" : "") + " v" + p.Cc.velocity.ToString("F1"));
                            while (trail.Count > 40) trail.Dequeue();
                        }
                        if (pp.y > -0.3f && p.Grounded) lastGood = pp;
                        if (!fellLogged && pp.y < -1f)
                        {
                            fellLogged = true;
                            Log.W("FELL below the floor in " + tag + " at " + pp.ToString("F2") + "; last on floor " + lastGood.ToString("F2"));
                            foreach (var line in trail) Log.I("FELL trail " + line);
                            foreach (var c in Physics.OverlapSphere(lastGood + Vector3.up * 0.9f, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                                Log.I("FELL near " + c.name + " (" + c.GetType().Name + ") bounds " + c.bounds.center.ToString("F2") + " size " + c.bounds.size.ToString("F2") + " layer " + LayerMask.LayerToName(c.gameObject.layer));
                        }
                    }
                    if (t >= mapT && run.Level != null && run.Level.Plan != null)
                    {
                        mapT = t + 2;
                        var gp = PlayerRig.Pos;
                        mapChecks++;
                        var gr = run.Level.Plan.Grid;
                        int cx = Mathf.FloorToInt(gp.x / Level.Cell), cz = Mathf.FloorToInt(gp.z / Level.Cell);
                        bool near = false;
                        for (int k = 0; k < 9 && !near; k++)
                        {
                            float ox = (k % 3 - 1) * 0.5f, oz = (k / 3 - 1) * 0.5f;
                            near = gr.IsOpen(Mathf.FloorToInt((gp.x + ox) / Level.Cell), Mathf.FloorToInt((gp.z + oz) / Level.Cell));
                        }
                        if (near) mapOpen++;
                        else if (mapChecks - mapOpen <= 3)
                        {
                            float fx = gp.x / Level.Cell - cx, fz = gp.z / Level.Cell - cz;
                            Log.I("MAP outside at " + gp.ToString("F2") + " cell " + cx + "," + cz + " frac " + fx.ToString("0.00") + "," + fz.ToString("0.00")
                                + " open L/R/D/U " + gr.IsOpen(cx - 1, cz) + "/" + gr.IsOpen(cx + 1, cz) + "/" + gr.IsOpen(cx, cz - 1) + "/" + gr.IsOpen(cx, cz + 1)
                                + " layout " + run.Plan.Kind + " grounded " + PlayerRig.Instance.Grounded);
                        }
                    }
                    if (burst > 0)
                    {
                        // Burst mode: only frames with zombies in view, at most 14 per stage.
                        shot1 = shot2 = shot3 = true;
                        if (t > burstT && bursts < 14 && OnScreen(p) >= 2) { burstT = t + burst; bursts++; Shot(tag + "_" + bursts.ToString("00")); }
                    }
                    float dt = Time.unscaledDeltaTime;
                    t += dt;
                    if (t > 2)
                    {
                        _fpsAcc += 1f / Mathf.Max(1e-4f, dt); _fpsN++; _fpsMin = Mathf.Min(_fpsMin, 1f / Mathf.Max(1e-4f, dt));
                        // CPU main / render thread and GPU frame times (needs Frame Timing Stats; zero where unsupported).
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, _ft) > 0)
                        {
                            _cpuAcc += _ft[0].cpuMainThreadFrameTime; _rtAcc += _ft[0].cpuRenderThreadFrameTime; _gpuAcc += _ft[0].gpuFrameTime; _ftN++;
                        }
                    }
                    Bot(p, run, ref swapT, ref nadeT, dt, tourOnly && t < 3);
                    if (!shot1 && t > 2.5f) { shot1 = true; Shot(tag + "_start"); }
                    if (!shot2 && t > (tourOnly ? 9 : 14)) { shot2 = true; Shot(tag + "_action"); }
                    if (!shot3 && !tourOnly && t > 32) { shot3 = true; Shot(tag + "_late"); }
                    yield return null;
                }
                Input = default;
                Log.I("MAP " + tag + " player on minimap floor " + mapOpen + "/" + mapChecks);
                if (mapChecks > 0 && mapOpen < mapChecks) Log.W("MAP " + tag + ": player outside the minimap floor plan");
                int skins = 0, skinVerts = 0;
                foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                    if (r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null) { skins++; skinVerts += r.sharedMesh.vertexCount; }
                string ft = _ftN > 0 ? " | ms cpu " + (_cpuAcc / _ftN).ToString("0.0") + " render " + (_rtAcc / _ftN).ToString("0.0") + " gpu " + (_gpuAcc / _ftN).ToString("0.0") : "";
                Log.I("STAGE " + tag + " fps avg " + (_fpsN > 0 ? _fpsAcc / _fpsN : 0).ToString("0") + " min " + _fpsMin.ToString("0") + " kills " + (run != null ? run.Kills : 0) + " alive " + Zombie.Alive.Count + " t " + t.ToString("0")
                    + ft + " | skinned " + skins + " (" + skinVerts + " verts) gfx " + Quality.Names[(int)Quality.Current] + (ArtLibrary.Enabled ? (ArtBody.Lite ? " art lite" : " art") : " noart"));
                if (run != null && !run.Ended) run.Forfeit();
                float w2 = 0;
                while (run != null && run.Result == null && w2 < 6) { w2 += Time.unscaledDeltaTime; yield return null; }
                yield return new WaitForSecondsRealtime(0.8f);
                if (!tourOnly && burst <= 0) Shot(tag + "_result");
                yield return null;
            }

            /// <summary>A simple bot: path toward the objective, aim at the nearest visible zombie, fire, reload, grenade.</summary>
            void Bot(PlayerRig p, StageRunner run, ref float swapT, ref float nadeT, float dt, bool idle)
            {
                var input = new PlayerInput { SelectSlot = -1 };
                if (idle || p == null || !p.Alive) { Input = input; return; }
                var cam = p.WorldCam.transform;
                // Target selection.
                Vector3? aim = null;
                float best = float.MaxValue;
                int close = 0;
                foreach (var z in Zombie.Alive)
                {
                    var head = z.Rig[ZB.Head].position;
                    float dist = (head - cam.position).magnitude;
                    if (dist < 10) close++;
                    if (dist > 45 || dist > best) continue;
                    if (Physics.Linecast(cam.position, head, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    best = dist;
                    aim = Vector3.Lerp(z.Rig[ZB.Chest].position, head, 0.6f);
                }
                var boss = Boss.Current;
                if (boss != null && boss.IsAlive)
                {
                    var bp = boss.Rig[ZB.Chest].position;
                    float bd = (bp - cam.position).magnitude;
                    if (bd < best + 6) { aim = bp; best = bd; }
                }
                foreach (var o in ObjectiveThing.All)
                {
                    if (o.Kind != ObjKind.Nest || o.Done) continue;
                    var np = o.transform.position + Vector3.up * 0.5f;
                    float nd = (np - cam.position).magnitude;
                    if (nd < 25 && aim == null && !Physics.Linecast(cam.position, np, Layers.WorldMask, QueryTriggerInteraction.Ignore)) aim = np;
                }
                if (aim != null)
                {
                    var dir = (aim.Value - cam.position).normalized;
                    float yaw = Mathf.DeltaAngle(cam.eulerAngles.y, Quaternion.LookRotation(dir).eulerAngles.y);
                    float pitch = Mathf.DeltaAngle(cam.eulerAngles.x, Quaternion.LookRotation(dir).eulerAngles.x);
                    float k = LookGain;
                    input.Look = new Vector2(Mathf.Clamp(yaw * 0.25f * k, -12 * k, 12 * k), Mathf.Clamp(pitch * 0.25f * k, -8 * k, 8 * k));
                    input.Fire = Mathf.Abs(yaw) < 5 && Mathf.Abs(pitch) < 5;
                }
                // Movement: toward the objective marker / the action, along the NavMesh.
                Vector3? goal = run.MarkerTarget;
                if (goal == null && aim != null && best > 12) goal = aim;
                if (goal != null)
                {
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(Level.OnNav(p.transform.position, 2), Level.OnNav(goal.Value, 3), NavMesh.AllAreas, path) && path.corners.Length > 1)
                    {
                        var next = path.corners[1] - p.transform.position;
                        next.y = 0;
                        if (next.magnitude > 0.3f && (goal.Value - p.transform.position).Flat().magnitude > 2.5f)
                        {
                            var local = Quaternion.Inverse(Quaternion.Euler(0, p.Yaw, 0)) * next.normalized;
                            input.Move = new Vector2(local.x, local.z) * (aim != null && best < 6 ? 0.5f : 0.85f);
                            if (aim == null)
                            {
                                float yaw = Mathf.DeltaAngle(p.Yaw, Quaternion.LookRotation(next).eulerAngles.y);
                                input.Look = new Vector2(Mathf.Clamp(yaw * 0.12f, -6, 6), Mathf.Clamp(-p.Pitch * 0.1f, -3, 3));
                            }
                        }
                    }
                }
                else if (aim != null && best < 5)
                {
                    input.Move = new Vector2(Mathf.Sin(Time.time), -0.7f);
                }
                // Utilities.
                swapT -= dt;
                if (swapT <= 0) { swapT = 25; input.SwapPressed = true; }
                nadeT -= dt;
                if (nadeT <= 0 && close >= 4) { nadeT = 12; input.GrenadePressed = true; }
                var g = p.Guns;
                if (g.W.Mag == 0 && !g.Reloading) input.ReloadPressed = true;
                input.Aim = aim != null && best > 18 && g.W.Def.Class != WeaponClass.Shotgun;
                Input = input;
            }
        }
    }
}
