using System;
using System.Collections;
using System.Collections.Generic;
using KPL.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// Entry point. Builds the UI canvas, loads the save, applies graphics settings and runs the
    /// splash → loading → menus flow. Owns the 3D stadium shown behind menus and in matches.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public RectTransform HudRoot { get; private set; }
        public RectTransform PopupRoot { get; private set; }
        public RectTransform TopRoot { get; private set; }
        public Transform World { get; private set; }
        public Stadium Stadium { get; private set; }
        public MatchDirector Match { get; private set; }
        public MenuScene Menu3D { get; private set; }

        public static bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            I = this;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;

            SaveSystem.Load();
            Quality.Apply(Quality.FromSave());
            Quality.ApplyFrameRate(false);
            var w = new GameObject("World");
            DontDestroyOnLoad(w);
            World = w.transform;
            BuildCanvas();
            BuildEventSystem();
            Audio.Init(transform);
            SceneLook.MainCamera();
            Debug.Log("[KPL] Boot: save " + SaveSystem.LastLoadNote + ", graphics " + Quality.Current + ", " + SystemInfo.deviceModel + " " + SystemInfo.graphicsDeviceName + " RAM " + SystemInfo.systemMemorySize);
            StartCoroutine(Flow());
        }

        IEnumerator Flow()
        {
            if (!AutoPilot.Requested) yield return SplashScreen.Run(TopRoot);
            yield return LoadMenuStadium(true);
            Ready = true;
            if (string.IsNullOrEmpty(SaveSystem.Data.Fav)) TeamSelectScreen.Show(true);
            else MainMenu.Show();
            AutoPilot.StartIfRequested(this);
        }

        // ------------------------------------------------------------- canvas

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            var safe = Ui.Node(go.transform, "Safe").Fill();
            ScreenRoot = Ui.Node(safe, "Screens").Fill();
            HudRoot = Ui.Node(safe, "Hud").Fill();
            PopupRoot = Ui.Node(safe, "Popups").Fill();
            TopRoot = Ui.Node(go.transform, "Top").Fill();
            var fit = go.AddComponent<SafeArea>();
            fit.Target = safe;
            fit.Scaler = scaler;
            fit.Apply();
        }

        void BuildEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ------------------------------------------------------------ stadium

        public static bool ResolveNight(int setting)
        {
            if (setting == 1) return false;
            if (setting == 2) return true;
            int h = DateTime.Now.Hour;
            return h < 7 || h >= 18;
        }

        /// <summary>Builds (or keeps) the stadium of the favourite team for the menus.</summary>
        public IEnumerator LoadMenuStadium(bool showLoading)
        {
            var fav = Teams.ById(string.IsNullOrEmpty(SaveSystem.Data.Fav) ? "MUM" : SaveSystem.Data.Fav);
            // Menus show the ground on a bright match day unless the player has chosen night.
            bool night = SaveSystem.Data.Settings.MatchTime == 2;
            if (Stadium == null)
            {
                var away = Teams.All[(Teams.IndexOf(fav.Id) + 3) % Teams.All.Count];
                yield return BuildStadium(fav, fav, away, night, showLoading, "Preparing " + fav.Ground + "...");
            }
            EnsureMenuScene();
        }

        public void EnsureMenuScene()
        {
            if (Menu3D == null && Stadium != null) Menu3D = MenuScene.Create(World, Stadium);
            if (Menu3D != null) Menu3D.gameObject.SetActive(true);
            Audio.Crowd(0.3f);
            Audio.Music(true);
        }

        public IEnumerator BuildStadium(TeamDef venue, TeamDef home, TeamDef away, bool night, bool showLoading, string title)
        {
            LoadingScreen loading = showLoading ? LoadingScreen.Show(TopRoot, title) : null;
            if (Menu3D != null)
            {
                Destroy(Menu3D.gameObject);
                Menu3D = null;
            }
            if (Stadium != null)
            {
                Destroy(Stadium.gameObject);
                Stadium = null;
                Tex.ClearMatchTextures();
                Kits.Clear();
                Body.ClearCaches();
                yield return null;
                yield return Resources.UnloadUnusedAssets();
            }
            loading?.SetProgress(0.05f, "Lighting the ground");
            SceneLook.Apply(night, World);
            yield return null;
            Stadium built = null;
            yield return Stadium.Build(venue, home, away, night, World, s => built = s, p => loading?.SetProgress(0.08f + p * 0.82f, StepText(p)));
            Stadium = built;
            loading?.SetProgress(0.95f, "Final touches");
            yield return null;
            SceneLook.CaptureReflections(World);
            loading?.SetProgress(1f, "Ready");
            yield return null;
            if (loading != null) loading.Close();
        }

        static string StepText(float p)
        {
            if (p < 0.25f) return "Rolling the pitch";
            if (p < 0.5f) return "Painting the creases";
            if (p < 0.7f) return "Filling the stands";
            if (p < 0.9f) return "Switching on the floodlights";
            return "Warming up the players";
        }

        // ------------------------------------------------------------- matches

        public void StartMatch(MatchContext ctx) => StartCoroutine(StartMatchRoutine(ctx));

        IEnumerator StartMatchRoutine(MatchContext ctx)
        {
            Nav.Clear();
            Audio.Music(false);
            var s = ctx.Setup;
            bool rebuild = Stadium == null || Stadium.Venue.Id != s.Venue.Id || Stadium.Night != s.Night || Stadium.Home.Id != s.Home.Team.Id || Stadium.Away.Id != s.Away.Team.Id;
            if (rebuild) yield return BuildStadium(s.Venue, s.Home.Team, s.Away.Team, s.Night, true, s.Title);
            if (Menu3D != null)
            {
                Destroy(Menu3D.gameObject);
                Menu3D = null;
            }
            Quality.ApplyFrameRate(true);
            Match = MatchDirector.Create(ctx, World, HudRoot, Stadium);
        }

        /// <summary>Called by the match when it is over (or abandoned).</summary>
        public void MatchEnded(MatchContext ctx, MatchState m, int userCatches, bool abandoned)
        {
            if (Match != null)
            {
                Destroy(Match.gameObject);
                Match = null;
            }
            Time.timeScale = 1;
            Quality.ApplyFrameRate(false);
            foreach (Transform c in HudRoot) Destroy(c.gameObject);
            Stadium?.ResetStumps();
            EnsureMenuScene();
            if (abandoned)
            {
                MainMenu.Show();
                return;
            }
            var summary = Progression.Record(ctx, m, userCatches);
            ResultScreen.Show(ctx, m, summary);
        }

        // --------------------------------------------------------------- loop

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Popup.CloseTop()) { }
                else if (Match != null) Match.OnBack();
                else Nav.Back();
            }
        }

        void LateUpdate() => SaveSystem.FlushIfDirty();

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveSystem.Save();
                if (Match != null) Match.OnAppPaused();
            }
        }

        void OnApplicationQuit() => SaveSystem.Save();
    }

    /// <summary>Keeps UI inside the safe area and scales it so the area holds a 1280 x 720 layout.</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        public const float RefW = 1280, RefH = 720;
        public RectTransform Target;
        public CanvasScaler Scaler;
        Rect _last;
        int _w, _h;

        void Update()
        {
            if (Screen.safeArea != _last || Screen.width != _w || Screen.height != _h) Apply();
        }

        public void Apply()
        {
            var sa = Screen.safeArea;
            if (Target == null || Screen.width == 0) return;
            _last = sa;
            _w = Screen.width;
            _h = Screen.height;
            Target.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            Target.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            Target.offsetMin = Target.offsetMax = Vector2.zero;
            if (Scaler != null) Scaler.scaleFactor = Mathf.Max(0.05f, Mathf.Min(sa.width / RefW, sa.height / RefH));
        }
    }

    /// <summary>Simple navigation: each screen registers what Back does.</summary>
    public static class Nav
    {
        static Action _back;
        public static string Current { get; private set; } = "";

        public static RectTransform Page(string name, Action back)
        {
            Clear();
            Current = name;
            _back = back;
            var page = Ui.Node(GameApp.I.ScreenRoot, name).Fill();
            page.gameObject.AddComponent<CanvasGroup>();
            Enter.Add(page, 0, 10);
            return page;
        }

        public static void Clear()
        {
            Popup.CloseAll();
            foreach (Transform c in GameApp.I.ScreenRoot) UnityEngine.Object.Destroy(c.gameObject);
            Current = "";
            _back = null;
        }

        public static void Back()
        {
            if (_back != null)
            {
                Audio.Play(Sfx.Back);
                _back();
            }
            else if (Current == "Main") Popup.Confirm("Quit KPL Cricket?", "Your progress is saved.", "QUIT", Application.Quit);
        }
    }
}
