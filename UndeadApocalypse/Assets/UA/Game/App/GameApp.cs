using System.Collections;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace UA.Game
{
    /// <summary>Boot, navigation between the menu and the modes, stage loading / teardown, app lifecycle.</summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance;
        const string DefaultPrivacyUrl = "https://rkgames.github.io/privacy/undead-apocalypse.html";

        /// <summary>Privacy policy link: "privacyPolicyUrl" in UAServices.json, else the placeholder (host a real page before release).</summary>
        public static string PrivacyUrl => string.IsNullOrWhiteSpace(ServicesConfig.Current.privacyPolicyUrl) ? DefaultPrivacyUrl : ServicesConfig.Current.privacyPolicyUrl.Trim();

        StagePlan _lastPlan;
        int _lastRound = 1;
        Level _level;
        bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameApp>();
        }

        void Start() => StartCoroutine(BootRoutine());

        IEnumerator BootRoutine()
        {
            Log.Hook();
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Layers.ConfigurePhysics();
            SaveSystem.Load();
            Quality.Apply(Quality.FromSave());
            Quality.Changed += Look.RefreshQuality;
            Screens.Init();
            Screens.ShowLoading("UNDEAD APOCALYPSE", "INFINITE WAR", "");
            yield return null;
            Fx.Init();
            Sfx.Init();
            Monetization.Init();
            AutoPilot.CheckArgs();
            float t = 0;
            while (!Sfx.Ready && t < 20)
            {
                t += Time.unscaledDeltaTime;
                Screens.SetLoading(Sfx.Progress * 0.7f, "Synthesising audio");
                yield return null;
            }
            Log.I("Boot: save " + SaveSystem.LastLoadNote + ", gfx " + Quality.Names[(int)Quality.Current] + ", audio " + (Sfx.Ready ? "ready" : "late") + " in " + t.ToString("0.0") + "s, device " + SystemInfo.deviceModel + " " + SystemInfo.systemMemorySize + "MB " + SystemInfo.graphicsDeviceType);
            yield return ShowMenuRoutine();
            if (AutoPilot.Active) AutoPilot.Begin();
        }

        // --------------------------------------------------------------- menu

        public void ToMenu()
        {
            if (_busy) return;
            StartCoroutine(ShowMenuRoutine());
        }

        IEnumerator ShowMenuRoutine()
        {
            _busy = true;
            Screens.Hide();
            Screens.ShowLoading("UNDEAD APOCALYPSE", "INFINITE WAR", "");
            Teardown();
            yield return null;
            yield return MenuBackdrop.Show(k => Screens.SetLoading(0.7f + k * 0.3f, "Preparing"));
            Screens.HideLoading();
            Screens.Home();
            Quality.ApplyFrameRate(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _busy = false;
        }

        // --------------------------------------------------------------- modes

        public void PlaySurvival(long stage) => Play(StageGen.Survival(stage, (ulong)SaveSystem.Data.WorldSeed), 1);
        public void PlayCampaign(int mission) => Play(Campaign.Plan(mission), 1);
        public void PlayDaily() => Play(DailyChallenge.Plan(Clock.Today, SaveSystem.Data.HighestStage), 1);
        public void PlayBossRush(int round) => Play(BossRush.Plan(round), round);

        public void Next()
        {
            var r = StageRunner.Instance?.Result;
            var plan = r?.Plan ?? _lastPlan;
            if (plan == null) { ToMenu(); return; }
            switch (plan.Mode)
            {
                case GameMode.Survival: PlaySurvival(SaveSystem.Data.Stage); break;
                case GameMode.Campaign: PlayCampaign(Mathf.Min(plan.CampaignMission + 1, Campaign.All.Length - 1)); break;
                case GameMode.BossRush: PlayBossRush(_lastRound + 1); break;
                default: ToMenu(); break;
            }
        }

        public void Retry()
        {
            var plan = _lastPlan;
            if (plan == null) { ToMenu(); return; }
            switch (plan.Mode)
            {
                case GameMode.Survival: PlaySurvival(plan.Stage); break;
                case GameMode.Campaign: PlayCampaign(plan.CampaignMission); break;
                case GameMode.Daily: PlayDaily(); break;
                case GameMode.BossRush: PlayBossRush(1); break;
            }
        }

        public void Play(StagePlan plan, int round)
        {
            if (_busy) return;
            StartCoroutine(PlayRoutine(plan, round));
        }

        IEnumerator PlayRoutine(StagePlan plan, int round)
        {
            _busy = true;
            _lastPlan = plan;
            _lastRound = round;
            Screens.Hide();
            Screens.ClosePopups();
            Screens.ShowLoading(plan.Title, plan.Subtitle, plan.Briefing);
            Sfx.Music(MusicKind.None);
            Teardown();
            yield return null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Level built = null;
            yield return LevelBuilder.Build(plan, lv => built = lv, (k, what) => Screens.SetLoading(k, what));
            _level = built;
            Log.I("Level built in " + sw.ElapsedMilliseconds + " ms (" + plan.Biome + " " + plan.Kind + " " + plan.Grid.W + "x" + plan.Grid.H + ")");
            StageRunner.Begin(plan, _level, round);
            yield return null;
            Screens.HideLoading();
            if (!Application.isMobilePlatform && !AutoPilot.Active)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            _busy = false;
        }

        /// <summary>Destroys the running stage and frees per-stage resources.</summary>
        void Teardown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            MenuBackdrop.Hide();
            if (StageRunner.Instance != null) Destroy(StageRunner.Instance.gameObject);
            if (_level != null) { _level.Destroy(); _level = null; }
            Zombie.Alive.Clear();
            Pickup.All.Clear();
            ObjectiveThing.All.Clear();
            Boss.Current = null;
            PlayerRig.Instance = null;
            Controls.ResetTouch();
            Sfx.StopAmbience();
            Fx.ClearWorld();
            WeaponPreview.Hide();
            Tex.ClearSurfaces();
            Mats.ClearCache();
            ZombieRig.ClearMaterials();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        // --------------------------------------------------------------- lifecycle

        void Update()
        {
            SaveSystem.FlushIfDirty();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) BackPressed();
            if (StageRunner.Instance == null) MenuBackdrop.Tick(Time.deltaTime);
        }

        void BackPressed()
        {
            var run = StageRunner.Instance;
            if (run != null && !run.Ended)
            {
                if (run.Paused) { Screens.ClosePopups(); run.Resume(); }
                else run.Pause();
                return;
            }
            if (Screens.PopupOpen) Screens.ClosePopups();
            else if (Screens.Current != null && !(Screens.Current is MainMenuScreen)) Screens.Back();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            SaveSystem.Save();
            var run = StageRunner.Instance;
            if (run != null && !run.Ended && !run.Paused && !AutoPilot.Active) run.Pause();
        }

        void OnApplicationQuit() => SaveSystem.Save();
    }

    /// <summary>Live 3D scene behind the main menu: a dim metro station with shambling infected.</summary>
    public static class MenuBackdrop
    {
        static Level _level;
        static Camera _cam;
        static readonly List<Zombie> Zs = new List<Zombie>();
        static float _t;
        static Vector3 _camBase;
        static Quaternion _camRot;

        public static IEnumerator Show(System.Action<float> progress)
        {
            if (_level != null) yield break;
            var plan = StageGen.Build(new GenParams
            {
                Mode = GameMode.Survival, Stage = 8, Seed = 424242, Biome = Biome.Metro, Mood = LightMood.Sodium, Weather = Weather.Fog,
                Objective = ObjectiveType.Eliminate, Event = StageEvent.None,
            });
            Level lv = null;
            yield return LevelBuilder.Build(plan, l => lv = l, (k, w) => progress?.Invoke(k));
            _level = lv;
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(lv.Root.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 52;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 120;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = Quality.Current == Gfx.Low ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            // Stand on the platform looking down its length.
            var g = plan.Grid;
            _camBase = new Vector3(1.6f * Level.Cell, 1.75f, 4.6f * Level.Cell);
            var look = new Vector3(g.W * Level.Cell * 0.7f, 1.2f, 4.2f * Level.Cell);
            _camRot = Quaternion.LookRotation(look - _camBase);
            camGo.transform.SetPositionAndRotation(_camBase, _camRot);
            Look.Apply(lv.Atmosphere);
            Fx.SetWeather(plan.Weather, plan.Indoor, camGo.transform);
            Sfx.Ambience(plan.Weather, plan.Indoor);
            // A few infected wandering in the haze.
            Zs.Clear();
            var types = new[] { ZombieType.Walker, ZombieType.Walker, ZombieType.Runner, ZombieType.Military, ZombieType.Walker, ZombieType.Toxic };
            for (int i = 0; i < types.Length; i++)
            {
                var z = Zombie.Create(lv.Root.transform, types[i], false, i);
                var p = Level.OnNav(_camBase + _camRot * new Vector3(Random.Range(-4f, 4f), 0, Random.Range(9f, 24f)), 4);
                z.Spawn(p, plan, false);
                Zs.Add(z);
            }
            PlayerRig.Pos = _camBase + _camRot * Vector3.back * 40;
        }

        public static void Tick(float dt)
        {
            if (_cam == null) return;
            _t += dt;
            _cam.transform.position = _camBase + _camRot * new Vector3(Mathf.Sin(_t * 0.07f) * 1.2f, Mathf.Sin(_t * 0.11f) * 0.15f, Mathf.Sin(_t * 0.05f) * 1.5f);
            _cam.transform.rotation = _camRot * Quaternion.Euler(Mathf.Sin(_t * 0.09f) * 2, Mathf.Sin(_t * 0.06f) * 6, 0);
            PlayerRig.Cam = _cam;
            Look.Tick(dt);
            Fx.Tick(dt);
            // Keep the infected in view.
            foreach (var z in Zs)
            {
                if (!z.IsAlive) continue;
                var local = Quaternion.Inverse(_camRot) * (z.transform.position - _camBase);
                if (local.z < 6 || local.z > 30 || Mathf.Abs(local.x) > 9)
                {
                    var p = Level.OnNav(_camBase + _camRot * new Vector3(Random.Range(-3f, 3f), 0, Random.Range(12f, 22f)), 4);
                    if (z.Agent.isOnNavMesh) z.Agent.SetDestination(p);
                }
            }
        }

        public static void Hide()
        {
            foreach (var z in Zs) if (z != null) Object.Destroy(z.gameObject);
            Zs.Clear();
            Zombie.Alive.Clear();
            if (_level != null) { _level.Destroy(); _level = null; }
            _cam = null;
        }
    }

    /// <summary>
    /// Starts the monetization backends compiled into this build. Backends live in their own assemblies
    /// (UA.Billing, UA.Ads) and register themselves into Iap.Backend / Ads.Backend before the first scene
    /// loads; they are started here, after the save and UI exist, so re-delivered purchases can be granted.
    /// </summary>
    public static class Monetization
    {
        public static void Init()
        {
            try { Iap.Backend?.Connect(); }
            catch (System.Exception e) { Log.E("Billing init failed: " + e.Message); }
            try { Ads.Backend?.Initialize(); }
            catch (System.Exception e) { Log.E("Ads init failed: " + e.Message); }
            Log.I("Monetization: billing " + (Iap.Backend != null ? "present" : "not in build") + ", ads " + (Ads.Backend != null ? "present" : "not in build"));
        }
    }
}
