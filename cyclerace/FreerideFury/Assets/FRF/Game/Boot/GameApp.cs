using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FRF
{
    /// <summary>
    /// Entry point: builds the canvas, event system and audio, then runs the menus and races.
    /// Everything is created in code, so the boot scene can be empty.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public MenuStage Stage { get; private set; }
        public RaceDirector Race { get; private set; }

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
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;

            BuildCanvas();
            BuildEventSystem();
            Sfx.Init(transform);
            SceneLook.MainCamera();
            Menus.Main();
            AutoShots.StartIfRequested(this);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;
            // Layouts use a 960 x 432 grid; the scale keeps the safe area at least that big.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            ScreenRoot = Ui.Node(go.transform, "Screens").Fill();
            var fit = go.AddComponent<SafeAreaFitter>();
            fit.Target = ScreenRoot;
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

        // ------------------------------------------------------------ navigation

        public void ClearScreens()
        {
            foreach (Transform c in ScreenRoot) Destroy(c.gameObject);
        }

        public void EnsureStage()
        {
            if (Stage != null) return;
            Stage = MenuStage.Build(transform);
        }

        public void RebuildStage()
        {
            DropStage();
            EnsureStage();
        }

        public void SetStageLive(bool live)
        {
            if (Stage != null) Stage.SetLive(live, Canvas.transform);
        }

        void DropStage()
        {
            if (Stage == null) return;
            Stage.SetLive(true, Canvas.transform);
            Destroy(Stage.gameObject);
            Stage = null;
        }

        // ------------------------------------------------------------ races

        public void StartEvent(EventDef e)
        {
            var diff = e.Skill < 0.5f ? Difficulty.Easy : e.Skill < 0.75f ? Difficulty.Normal : Difficulty.Hard;
            StartRace(new RaceConfig { Track = e.Track, Event = e, Rivals = e.Rivals, Skill = e.Skill, Difficulty = diff });
        }

        public void StartQuick(TrackDef t, int rivals, Difficulty diff)
        {
            float skill = diff == Difficulty.Easy ? 0.4f : diff == Difficulty.Normal ? 0.62f : 0.85f;
            StartRace(new RaceConfig { Track = t, Rivals = rivals, Skill = skill, Difficulty = diff });
        }

        public bool Autopilot;

        public void StartRace(RaceConfig cfg)
        {
            cfg.Autopilot = Autopilot;
            cfg.OnExit = OnRaceExit;
            StartCoroutine(LoadRace(cfg));
        }

        IEnumerator LoadRace(RaceConfig cfg)
        {
            Menus.Loading(cfg.Track, cfg.Title);
            Sfx.I?.Music(false);
            // Two frames so the loading card is on screen before the heavy build.
            yield return null;
            yield return null;
            DropStage();
            if (Race != null) Destroy(Race.gameObject);
            yield return null;
            ClearScreens();
            Race = RaceDirector.Create(cfg, ScreenRoot);
            Application.targetFrameRate = Settings.Smooth60 ? 60 : 30;
        }

        void OnRaceExit(RaceConfig cfg, RaceResult res) => StartCoroutine(EndRace(cfg, res));

        IEnumerator EndRace(RaceConfig cfg, RaceResult res)
        {
            if (Race != null) Destroy(Race.gameObject);
            Race = null;
            Application.targetFrameRate = 30;
            yield return null;
            switch (res.Action)
            {
                case ExitAction.Retry:
                    StartRace(new RaceConfig { Track = cfg.Track, Event = cfg.Event, Rivals = cfg.Rivals, Skill = cfg.Skill, Difficulty = cfg.Difficulty });
                    break;
                case ExitAction.Next when cfg.Event != null && cfg.Event.Index + 1 < Catalog.Career.Length:
                    StartEvent(Catalog.Career[cfg.Event.Index + 1]);
                    break;
                default:
                    ClearScreens();
                    if (cfg.Event != null) Menus.Career();
                    else Menus.Main();
                    break;
            }
        }

        void Update()
        {
            Dbg.Tick();
            Palette.Flush();
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            // Android back: pause in a race, otherwise up one screen.
            if (Race != null)
            {
                if (Race.Phase == RacePhase.Results) Race.Exit(ExitAction.Menu);
                else Race.Hud.TogglePause();
            }
            else Menus.Back();
        }

        void OnApplicationPause(bool paused)
        {
            // Going to the background mid-race opens the pause menu.
            if (paused && Race != null && Race.Phase != RacePhase.Results && !Race.Paused) Race.Hud.ShowPause();
            if (paused) PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Keeps UI inside the phone's safe area (notches, cutouts, rounded corners) and scales the
    /// canvas so that area always holds the full 960 x 432 layout grid.
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        public const float RefWidth = 960, RefHeight = 432;
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
            if (Target == null || Screen.width == 0 || Screen.height == 0) return;
            _last = sa;
            _w = Screen.width;
            _h = Screen.height;
            Target.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            Target.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            Target.offsetMin = Target.offsetMax = Vector2.zero;
            if (Scaler != null) Scaler.scaleFactor = Mathf.Max(0.05f, Mathf.Min(sa.width / RefWidth, sa.height / RefHeight));
        }
    }
}
