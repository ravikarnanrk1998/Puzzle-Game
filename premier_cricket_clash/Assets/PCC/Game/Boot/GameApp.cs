using System;
using System.Collections;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>
    /// Entry point: builds the canvas, event system and audio, then runs the menus.
    /// Everything is created in code, so the boot scene can be empty.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform ScreenRoot { get; private set; }

        MenuBackdrop _backdrop;
        MatchDirector _match;

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
            // Menus and the auction run at 30 FPS; matches use 30 or 60 (Settings > FRAME RATE).
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Landscape only, either way up.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            BuildCanvas();
            BuildEventSystem();
            Sfx.Init(transform);
            ResetSavesIfDatabaseChanged();
            PlayerNames.Init();
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
            // Layouts use a 960 x 432 grid. The scale is chosen so the SAFE AREA (inside notches
            // and camera cutouts) always has at least that much room on every phone and tablet.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            ScreenRoot = Ui.Node(go.transform, "Screens").Fill();
            var fit = go.AddComponent<SafeAreaFitter>();
            fit.Target = ScreenRoot;
            fit.Scaler = scaler;
            fit.Apply();
        }

        /// <summary>
        /// Saved auctions and renames store player ids; when the player database changes (fictional
        /// to real squads) those ids point at other players, so the old saves are cleared once.
        /// </summary>
        static void ResetSavesIfDatabaseChanged()
        {
            string version = PlayerDb.UseRealNames ? "real-2026-v1" : "fictional-v1";
            if (PlayerPrefs.GetString("pcc.dbver", "") == version) return;
            foreach (var k in new[] { "pcc.auction", "pcc.auction.live", "pcc.auction.use", "pcc.names" }) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.SetString("pcc.dbver", version);
            PlayerPrefs.Save();
        }

        void BuildEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && PCC_INPUTSYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------ navigation

        public void ClearScreens()
        {
            foreach (Transform c in ScreenRoot) Destroy(c.gameObject);
        }

        /// <summary>Live stadium behind the menus (built once, reused between screens).</summary>
        public void EnsureBackdrop()
        {
            if (_backdrop != null) return;
            var team = Teams.ById(PlayerPrefs.GetString("pcc.team", "MUM"));
            bool day = Settings.Lighting == MatchTime.Day;
            _backdrop = MenuBackdrop.Build(team, day);
            Sfx.I?.StartCrowd(0.16f);
        }

        /// <summary>
        /// Live 3D background (main menu, quick match) or a frozen still (every other menu and
        /// the auction) so the GPU rests while the player reads and taps.
        /// </summary>
        public void SetBackdropLive(bool live)
        {
            if (_backdrop != null) _backdrop.SetLive(live, Canvas.transform);
        }

        public void DropBackdrop()
        {
            if (_backdrop == null) return;
            // Unfreeze first, synchronously: the match reuses the same main camera this frame.
            _backdrop.SetLive(true, Canvas.transform);
            Destroy(_backdrop.gameObject);
            _backdrop = null;
            Sfx.I?.StopCrowd();
        }

        public static bool ResolveDay(MatchTime t)
        {
            if (t == MatchTime.Day) return true;
            if (t == MatchTime.Night) return false;
            int h = DateTime.Now.Hour;
            return h >= 7 && h < 17;
        }

        /// <summary>Starts a one-off T20 between two franchises at the user's home ground.</summary>
        public void StartQuickMatch(TeamInfo user, TeamInfo opp, int overs, bool day)
        {
            PlayerPrefs.SetString("pcc.team", user.Id);
            PlayerPrefs.SetString("pcc.opp", opp.Id);
            PlayerPrefs.SetInt("pcc.overs", overs);
            Settings.Save();
            // Auction squads when the player has run a mega auction (and chose to use them).
            var home = TeamSheet.Auto(user, AuctionStore.SquadFor(user));
            var away = TeamSheet.Auto(opp, AuctionStore.SquadFor(opp));
            var engine = new MatchEngine(home, away, user, Environment.TickCount & 0x7fffffff, overs, Settings.Difficulty);
            MatchConfig cfg = null;
            cfg = new MatchConfig
            {
                Engine = engine,
                UserTeamId = user.Id,
                Title = "QUICK MATCH • " + overs + " OVERS • " + user.VenueShort.ToUpperInvariant(),
                Day = day,
                OnExit = () => EndMatch(cfg),
            };
            ClearScreens();
            DropBackdrop();
            Application.targetFrameRate = Settings.Smooth60 ? 60 : 30;
            // The HUD lives in the safe area too, so nothing sits under a camera cutout.
            _match = MatchDirector.Create(cfg, ScreenRoot);
        }

        void EndMatch(MatchConfig cfg) => StartCoroutine(EndMatchNextFrame(cfg));

        IEnumerator EndMatchNextFrame(MatchConfig cfg)
        {
            if (_match != null) Destroy(_match.gameObject);
            _match = null;
            Application.targetFrameRate = 30;
            // Let the match (and its OnDestroy crowd stop) go before the menus start their own audio.
            yield return null;
            if (cfg.Engine.Finished) Menus.Result(cfg);
            else Menus.Main();
        }

        void Update()
        {
            Dbg.Tick();
            Dbg.Mark("GameApp.Update");
            // Android back button / Escape: pause in a match, otherwise up one screen.
            if (!BackPressed()) return;
            if (_match != null) _match.Hud?.TogglePause();
            else if (AuctionScreens.InRoom && AuctionRoom.Current != null) AuctionRoom.Current.TogglePause();
            else Menus.Back();
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM && PCC_INPUTSYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        public bool InMatch => _match != null;
        public MatchDirector Match => _match;
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

    /// <summary>Floodlit stadium with a few players, orbited slowly behind the menus.</summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        Camera _cam;
        double _a = 0.4;
        Cricketer[] _people;
        (Pose Pose, V3 Foot, double Facing)[] _places;

        public static MenuBackdrop Build(TeamInfo home, bool day)
        {
            var go = new GameObject("MenuBackdrop");
            var b = go.AddComponent<MenuBackdrop>();
            var away = Teams.All.First(t => t.Id != home.Id);
            Stadium.Build(home, away, day, go.transform);
            SceneLook.Apply(day, go.transform);
            b._cam = SceneLook.MainCamera();
            b.AddPlayers(home, away);
            b.Step(0);
            return b;
        }

        void AddPlayers(TeamInfo bat, TeamInfo bowl)
        {
            var db = PlayerDb.Instance;
            var batters = db.Squad(bat.Id);
            var fielders = db.Squad(bowl.Id);
            Color Trim(TeamInfo t) => Mats.Hex(t.Secondary).Luma() < 0.2f ? Pal.Gold : Mats.Hex(t.Secondary);
            Look BatterLook(Player p) => Look.Batter(Mats.Hex(bat.Primary), Trim(bat), p.Surname, p.Jersey, Look.SkinFor(p.OriginalName));
            Look FielderLook(Player p) => Look.Fielder(Mats.Hex(bowl.Primary), Trim(bowl), p.Surname, p.Jersey, Look.SkinFor(p.OriginalName));
            var keeper = fielders.First(p => p.IsKeeper);
            var looks = new[]
            {
                BatterLook(batters[0]),
                BatterLook(batters[3]),
                Look.Keeper(Mats.Hex(bowl.Primary), Trim(bowl), keeper.Surname, keeper.Jersey, Look.SkinFor(keeper.OriginalName)),
                Look.Umpire(Look.SkinTones[1]),
                FielderLook(fielders[12]),
                FielderLook(fielders[5]),
                FielderLook(fielders[9]),
            };
            var striker = new V3(0.18, 19.32, 0);
            double Facing(V3 at) => Math.Atan2(striker.x - at.x, striker.y - at.y);
            var slip = new V3(-1.9, 22.9, 0);
            var point = new V3(-17, 17, 0);
            _places = new[]
            {
                (Poses.BatStance, striker, -Math.PI / 2),
                (Poses.NonStriker, new V3(2.1, 1.5, 0), 0.0),
                (Poses.KeeperCrouch, new V3(0.1, 21.7, 0), Math.PI),
                (Poses.Umpire, new V3(1.15, -2.4, 0), 0.0),
                (Poses.Stand, new V3(-1.2, -6.2, 0), 0.0),
                (Poses.Slip, slip, Facing(slip)),
                (Poses.FieldReady, point, Facing(point)),
            };
            _people = new Cricketer[looks.Length];
            for (int i = 0; i < looks.Length; i++) _people[i] = Cricketer.Create(transform, looks[i], "Menu" + i);
        }

        RenderTexture _still;
        GameObject _stillImage;
        bool _frozen;

        /// <summary>Live orbit, or a still frame shown as a UI image with the 3D camera switched off.</summary>
        public void SetLive(bool live, Transform canvas)
        {
            if (live != _frozen) return;
            if (live)
            {
                Unfreeze();
                return;
            }
            if (_cam == null) return;
            _still = new RenderTexture(Mathf.Max(64, Screen.width / 2), Mathf.Max(64, Screen.height / 2), 24, RenderTextureFormat.ARGB32);
            var prev = _cam.targetTexture;
            _cam.targetTexture = _still;
            _cam.Render();
            _cam.targetTexture = prev;
            _cam.enabled = false;
            foreach (var s in GetComponentsInChildren<Stadium>()) s.enabled = false;
            _stillImage = new GameObject("FrozenBackdrop", typeof(RectTransform));
            _stillImage.transform.SetParent(canvas, false);
            _stillImage.transform.SetAsFirstSibling();
            var raw = _stillImage.AddComponent<RawImage>();
            raw.texture = _still;
            raw.raycastTarget = false;
            ((RectTransform)_stillImage.transform).Fill();
            _frozen = true;
        }

        void Unfreeze()
        {
            if (!_frozen) return;
            _frozen = false;
            if (_cam != null) _cam.enabled = true;
            foreach (var s in GetComponentsInChildren<Stadium>()) s.enabled = true;
            if (_stillImage != null) Destroy(_stillImage);
            if (_still != null)
            {
                _still.Release();
                Destroy(_still);
            }
        }

        void OnDestroy() => Unfreeze();

        void Update()
        {
            if (!_frozen) Step(Time.deltaTime);
        }

        /// <summary>Advances the orbit and poses the players (also called once at build for a correct first frame).</summary>
        void Step(float dt)
        {
            _a += dt * 0.045;
            if (_cam != null) Rigs.Orbit(_a, 58, 15).ApplyTo(_cam);
            if (_people == null) return;
            double t = Time.time;
            for (int i = 0; i < _people.Length; i++)
            {
                var (pose, foot, facing) = _places[i];
                var p = i == 0 ? Pose.Lerp(Poses.BatStance, Poses.BatBacklift, 0.06 + 0.06 * Math.Sin(t * 2.2)) : pose;
                _people[i].Apply(p, foot, facing);
            }
        }
    }
}
