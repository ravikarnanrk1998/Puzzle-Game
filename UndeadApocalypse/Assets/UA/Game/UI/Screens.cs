using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>A full-screen menu page.</summary>
    public abstract class UiScreen
    {
        public RectTransform Root;
        public virtual bool ShowTopBar => true;
        public virtual string Title => "";
        /// <summary>Darkening over the 3D menu backdrop behind this screen (0 = none), so its text stays readable.</summary>
        public virtual float Backdrop => 0.45f;
        public abstract void Build(RectTransform root);
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }
    }

    /// <summary>Screen navigation, the currency top bar, popups, toasts, loading, results, pause and revive.</summary>
    public static class Screens
    {
        static Canvas _canvas;
        static RectTransform _safe, _screenLayer, _popupLayer, _toastLayer, _loading, _topBar, _back;
        static UiScreen _current;
        static readonly Stack<UiScreen> History = new Stack<UiScreen>();
        static Text _coins, _gems, _level, _title;
        static Image _xpFill;
        static Image _loadingFill;
        static Text _loadingText, _loadingTitle, _loadingSub, _loadingBrief, _loadingTip;
        static readonly List<(RectTransform rt, float t)> Toasts = new List<(RectTransform, float)>();
        static Ticker _ticker;
        public static UiScreen Current => _current;
        public static bool PopupOpen => _popupLayer != null && _popupLayer.childCount > 0;

        sealed class Ticker : MonoBehaviour
        {
            void Update() => Tick(Time.unscaledDeltaTime);
        }

        public static void Init()
        {
            if (_canvas != null) return;
            Ui.EnsureEventSystem();
            _canvas = Ui.Canvas("UI", 20);
            _ticker = _canvas.gameObject.AddComponent<Ticker>();
            var root = (RectTransform)_canvas.transform;
            _safe = Ui.Safe(root);
            _screenLayer = Ui.Rect(_safe, "Screens");
            _screenLayer.Fill();
            BuildTopBar();
            _popupLayer = Ui.Rect(root, "Popups");
            _popupLayer.Fill();
            _toastLayer = Ui.Rect(root, "Toasts");
            _toastLayer.Fill();
            BuildLoading(root);
        }

        // --------------------------------------------------------------- navigation

        public static void Show(UiScreen s, bool push = true)
        {
            if (_current != null)
            {
                _current.OnHide();
                if (push) History.Push(_current);
                UnityEngine.Object.Destroy(_current.Root.gameObject);
            }
            _current = s;
            if (s == null) { _topBar.gameObject.SetActive(false); return; }
            s.Root = Ui.Rect(_screenLayer, s.GetType().Name);
            s.Root.Fill(0, s.ShowTopBar ? 110 : 0, 0, 0);
            if (s.Backdrop > 0)
            {
                var dim = Ui.Img(s.Root, Sprites.White, new Color(0, 0, 0, s.Backdrop * 0.6f), "BackdropDim");
                dim.rectTransform.Fill();
                var vig = Ui.Img(s.Root, Sprites.Vignette, new Color(0, 0, 0, Mathf.Min(1, s.Backdrop * 1.6f)), "BackdropVignette");
                vig.rectTransform.Fill();
            }
            s.Build(s.Root);
            _topBar.gameObject.SetActive(s.ShowTopBar);
            _title.text = s.Title;
            _back.gameObject.SetActive(!(s is MainMenuScreen));
            RefreshTopBar();
            s.OnShow();
        }

        public static void Back()
        {
            if (PopupOpen) { ClosePopups(); return; }
            if (History.Count == 0) return;
            var prev = History.Pop();
            // Rebuild a fresh instance of the previous screen type.
            var fresh = (UiScreen)Activator.CreateInstance(prev.GetType());
            Show(fresh, false);
        }

        public static void Home()
        {
            History.Clear();
            Show(new MainMenuScreen(), false);
        }

        public static void Hide()
        {
            if (_current != null)
            {
                _current.OnHide();
                UnityEngine.Object.Destroy(_current.Root.gameObject);
                _current = null;
            }
            History.Clear();
            _topBar.gameObject.SetActive(false);
        }

        static void Tick(float dt)
        {
            _current?.Tick(dt);
            for (int i = Toasts.Count - 1; i >= 0; i--)
            {
                var (rt, t) = Toasts[i];
                t -= dt;
                if (rt == null) { Toasts.RemoveAt(i); continue; }
                if (t <= 0) { UnityEngine.Object.Destroy(rt.gameObject); Toasts.RemoveAt(i); continue; }
                rt.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(t / 0.4f);
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0, -150 - i * 70), dt * 10);
                Toasts[i] = (rt, t);
            }
            if (_topBar.gameObject.activeSelf) RefreshTopBar();
        }

        // --------------------------------------------------------------- top bar

        static void BuildTopBar()
        {
            _topBar = Ui.Rect(_safe, "TopBar");
            _topBar.anchorMin = new Vector2(0, 1);
            _topBar.anchorMax = new Vector2(1, 1);
            _topBar.pivot = new Vector2(0.5f, 1);
            _topBar.offsetMin = new Vector2(0, -104);
            _topBar.offsetMax = Vector2.zero;
            var bg = Ui.Img(_topBar, Sprites.VGradient, new Color(0, 0, 0, 0.75f), "Bg");
            bg.rectTransform.Fill();
            bg.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            var back = Ui.IconButton(_topBar, Icon.Back, Back, 76, Pal.Panel2);
            back.Bg.rectTransform.At(0, 0.5f, 60, 0, 76, 76);
            _back = back.Bg.rectTransform;
            _title = Ui.Label(_topBar, "", 44, Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
            _title.rectTransform.At(0, 0.5f, 380, 0, 560, 80);
            // Level badge + XP bar.
            var lvl = Ui.Img(_topBar, Sprites.Circle, Pal.Accent, "Level");
            lvl.rectTransform.At(1, 0.5f, -870, 0, 70, 70);
            _level = Ui.Label(lvl.transform, "1", 32, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            _level.rectTransform.Fill();
            _xpFill = Ui.Bar(_topBar, Pal.Panel2, Pal.Accent2, out var xpRoot, "XP");
            xpRoot.At(1, 0.5f, -740, 0, 170, 16);
            _coins = Ui.Chip(_topBar, Icon.Coin, Pal.Coin, "0", 32);
            _coins.transform.parent.GetComponent<RectTransform>().At(1, 0.5f, -500, 0, 250, 66);
            _gems = Ui.Chip(_topBar, Icon.Gem, Pal.Gem, "0", 32);
            _gems.transform.parent.GetComponent<RectTransform>().At(1, 0.5f, -220, 0, 230, 66);
            var plus = Ui.IconButton(_topBar, Icon.Plus, () => Show(new StoreScreen { Section = 1 }), 58, Pal.Good.Mul(0.8f));
            plus.Bg.rectTransform.At(1, 0.5f, -60, 0, 58, 58);
            _topBar.gameObject.SetActive(false);
        }

        public static void RefreshTopBar()
        {
            var d = SaveSystem.Data;
            _coins.text = X.Money(d.Coins);
            _gems.text = X.Money(d.Gems);
            _level.text = d.Level.ToString();
            Ui.SetFill(_xpFill, d.Xp / (float)PlayerProgress.XpToNext(d.Level));
        }

        // --------------------------------------------------------------- toasts

        public static void Toast(string msg)
        {
            if (_toastLayer == null) { Log.I(msg); return; }
            var bg = Ui.Img(_toastLayer, Sprites.Cut(12), Pal.Panel.A(0.92f), "Toast");
            var rt = bg.rectTransform;
            rt.At(0.5f, 1, 0, -150, Mathf.Clamp(msg.Length * 18 + 80, 300, 1100), 60);
            bg.gameObject.AddComponent<CanvasGroup>();
            var t = Ui.Label(rt, msg, 30, Pal.Text, TextAnchor.MiddleCenter, FontKind.Display);
            t.rectTransform.Fill(16, 0, 16, 0);
            Toasts.Insert(0, (rt, 2.2f));
            if (Toasts.Count > 4) { UnityEngine.Object.Destroy(Toasts[4].rt.gameObject); Toasts.RemoveAt(4); }
        }

        // --------------------------------------------------------------- popups

        public static RectTransform Popup(string title, float w, float h, bool closeButton = true)
        {
            var dim = Ui.Img(_popupLayer, Sprites.White, new Color(0, 0, 0, Ui.Dim(0.78f)), "Dim", true);
            dim.rectTransform.Fill();
            // Opaque: in linear colour space even a 0.97 panel lets the screen behind ghost through.
            var panel = Ui.Panel(dim.transform, Pal.Panel.A(1f), "Popup");
            panel.rectTransform.At(0.5f, 0.5f, 0, 0, w, h);
            var line = Ui.Img(panel.transform, Sprites.CutOutline(18, 2.5f), Pal.Accent.A(0.8f), "Line");
            line.rectTransform.Fill();
            var t = Ui.Label(panel.transform, title, 52, Pal.Text, TextAnchor.UpperCenter, FontKind.Display, "Title");
            t.rectTransform.Fill(20, 26, 20, 0);
            if (closeButton)
            {
                var c = Ui.IconButton(panel.transform, Icon.Close, ClosePopups, 64, Pal.Panel2);
                c.Bg.rectTransform.At(1, 1, -50, -50, 64, 64);
            }
            return panel.rectTransform;
        }

        public static void ClosePopups()
        {
            if (_popupLayer == null) return;
            Ui.Clear(_popupLayer);
        }

        public static void Message(string title, string body, Action ok = null)
        {
            var p = Popup(title, 900, 480, false);
            var t = Ui.Label(p, body, 32, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Body);
            t.rectTransform.Fill(50, 110, 50, 130);
            var b = Ui.Button(p, "OK", () => { ClosePopups(); ok?.Invoke(); }, Pal.Accent, 38);
            b.Bg.rectTransform.At(0.5f, 0, 0, 70, 320, 86);
        }

        public static void Confirm(string title, string body, string yes, Action onYes, string no = "CANCEL")
        {
            var p = Popup(title, 940, 500, false);
            var t = Ui.Label(p, body, 32, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Body);
            t.rectTransform.Fill(50, 110, 50, 140);
            var y = Ui.Button(p, yes, () => { ClosePopups(); onYes?.Invoke(); }, Pal.Accent, 36);
            y.Bg.rectTransform.At(0.5f, 0, 190, 74, 330, 86);
            var n = Ui.Button(p, no, ClosePopups, Pal.Panel2, 36);
            n.Bg.rectTransform.At(0.5f, 0, -190, 74, 330, 86);
        }

        // --------------------------------------------------------------- loading

        static readonly string[] Tips =
        {
            "Headshots deal massive bonus damage. Helmets on soldiers and riot zombies reduce it.",
            "Bloaters explode when they get close. Shoot them from range - the blast hurts other zombies too.",
            "Explosive barrels are your friends. Lure a crowd, then shoot the barrel.",
            "Every 25 stages an elite encounter appears. Every 50 stages, a boss.",
            "Stalkers are almost invisible until they are close. Listen for their shriek.",
            "Fire and explosives deal extra damage to infection nests.",
            "Upgrade your favourite weapon's damage and magazine first.",
            "The Juggernaut is armored everywhere except its glowing weak points.",
            "Daily challenges reset at midnight UTC and pay up to 3x rewards.",
            "Skill points come from levelling up. Regeneration keeps you alive in long stages.",
            "Hold the fire button and drag to aim - you never have to lift your thumb.",
            "Hold the grenade button: a red arc shows exactly where it will land. Release to throw.",
            "Drag your buttons where you like: Pause > Customize Controls (or Settings > Controls).",
            "The minimap shows zombies as red dots. Dots on its rim point to zombies further away.",
        };

        static void BuildLoading(RectTransform root)
        {
            _loading = Ui.Rect(root, "Loading");
            _loading.Fill();
            var bg = Ui.Img(_loading, Sprites.White, Pal.Dark, "Bg", true);
            bg.rectTransform.Fill();
            var glow = Ui.Img(_loading, Sprites.Glow, Pal.Blood.A(0.35f), "Glow");
            glow.rectTransform.At(0.5f, 0.3f, 0, 0, 2200, 1200);
            var vig = Ui.Img(_loading, Sprites.Vignette, Color.black, "Vignette");
            vig.rectTransform.Fill();
            _loadingTitle = Ui.Title(_loading, "", 92, Pal.Text, TextAnchor.MiddleCenter);
            _loadingTitle.rectTransform.At(0.5f, 0.66f, 0, 0, 1600, 120);
            Ui.Shadow(_loadingTitle, 4, 0.8f);
            _loadingSub = Ui.Label(_loading, "", 40, Pal.Accent, TextAnchor.MiddleCenter, FontKind.Display);
            _loadingSub.rectTransform.At(0.5f, 0.56f, 0, 0, 1600, 60);
            _loadingBrief = Ui.Label(_loading, "", 32, Pal.Dim, TextAnchor.UpperCenter, FontKind.Body);
            _loadingBrief.rectTransform.At(0.5f, 0.4f, 0, 0, 1300, 220);
            _loadingFill = Ui.Bar(_loading, Pal.Panel2, Pal.Accent, out var bar, "Progress");
            bar.At(0.5f, 0.16f, 0, 0, 900, 14);
            _loadingText = Ui.Label(_loading, "", 26, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Semi);
            _loadingText.rectTransform.At(0.5f, 0.16f, 0, 36, 900, 40);
            _loadingTip = Ui.Label(_loading, "", 28, Pal.Text.A(0.75f), TextAnchor.MiddleCenter, FontKind.Body);
            _loadingTip.rectTransform.At(0.5f, 0.07f, 0, 0, 1500, 70);
            _loading.gameObject.SetActive(false);
        }

        public static void ShowLoading(string title, string sub, string brief)
        {
            _loading.gameObject.SetActive(true);
            _loading.SetAsLastSibling();
            _loadingTitle.text = title;
            _loadingSub.text = sub;
            _loadingBrief.text = brief;
            _loadingTip.text = "TIP: " + Tips[UnityEngine.Random.Range(0, Tips.Length)];
            SetLoading(0, "");
        }

        public static void SetLoading(float k, string what)
        {
            Ui.SetFill(_loadingFill, k);
            _loadingText.text = what.ToUpperInvariant();
        }

        public static void HideLoading() => _loading.gameObject.SetActive(false);

        // --------------------------------------------------------------- in-game dialogs

        public static void ShowPause()
        {
            var p = Popup("PAUSED", 760, 800, false);
            var run = StageRunner.Instance;
            string[] labels = { "RESUME", "CUSTOMIZE CONTROLS", "SETTINGS", "RESTART", "QUIT TO MENU" };
            Action[] acts =
            {
                () => { ClosePopups(); run?.Resume(); },
                // Opens over the pause menu; the stage's controls take the new layout when it is saved.
                () => LayoutEditor.Open(),
                () => { ClosePopups(); SettingsScreen.OpenInGame(); },
                () => { ClosePopups(); run?.Resume(); GameApp.Instance.Retry(); },
                () => Confirm("QUIT STAGE?", "You keep half of the coins from kills so far.", "QUIT", () => run?.Forfeit()),
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = Ui.Button(p, labels[i], acts[i], i == 0 ? Pal.Accent : Pal.Panel2, 38);
                b.Bg.rectTransform.At(0.5f, 1, 0, -170 - i * 110, 560, 92);
            }
            var plan = run?.Plan;
            if (plan != null)
            {
                var t = Ui.Label(p, plan.ObjectiveText, 26, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Body);
                t.rectTransform.At(0.5f, 0, 0, 50, 680, 60);
            }
        }

        public static void ShowRevive(bool canRevive, Action revive, Action giveUp)
        {
            var p = Popup("YOU DIED", 900, 620, false);
            var t = Ui.Label(p, canRevive ? "Get back up and finish the job?" : "You can only revive once per stage.", 32, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Body);
            t.rectTransform.At(0.5f, 1, 0, -160, 800, 60);
            float y = -260;
            if (canRevive && Ads.RewardedReady)
            {
                var a = Ui.Button(p, "REVIVE  (WATCH AD)", () => Ads.ShowRewarded("revive", ok => { if (ok) { ClosePopups(); revive(); } else Toast("Ad not completed"); }), Pal.Good.Mul(0.85f), 36, Icon.Video);
                a.Bg.rectTransform.At(0.5f, 1, 0, y, 640, 92);
                y -= 112;
            }
            if (canRevive)
            {
                var g = Ui.Button(p, "REVIVE  (20 GEMS)", () =>
                {
                    if (Wallet.Spend(SaveSystem.Data, Currency.Gems, 20)) { SaveSystem.MarkDirty(); ClosePopups(); revive(); }
                    else { Sfx.Error(); Toast("Not enough gems"); }
                }, Pal.Gem.Mul(0.6f), 36, Icon.Gem);
                g.Bg.rectTransform.At(0.5f, 1, 0, y, 640, 92);
                y -= 112;
            }
            var no = Ui.Button(p, "GIVE UP", () => { ClosePopups(); giveUp(); }, Pal.Panel2, 36);
            no.Bg.rectTransform.At(0.5f, 1, 0, y, 640, 92);
        }

        // --------------------------------------------------------------- results

        public static void ShowResult(StageResult r)
        {
            Show(new ResultScreen(r), false);
        }
    }

    /// <summary>Stage complete / failed: stats, rewards, ad bonus, continue / retry / menu.</summary>
    public sealed class ResultScreen : UiScreen
    {
        readonly StageResult _r;
        public ResultScreen() { }
        public ResultScreen(StageResult r) { _r = r; }
        public override bool ShowTopBar => false;
        public override float Backdrop => 0;

        public override void Build(RectTransform root)
        {
            var r = _r ?? StageRunner.Instance?.Result;
            if (r == null) return;
            var dim = Ui.Img(root, Sprites.White, new Color(0, 0, 0, Ui.Dim(0.72f)), "Dim", true);
            dim.rectTransform.Fill();
            // Mode-specific wording: a failed Boss Rush round or campaign mission is not a "stage".
            string noun = r.Plan.Mode == GameMode.BossRush ? "ROUND" : r.Plan.Mode == GameMode.Campaign ? "MISSION" : r.Plan.Mode == GameMode.Daily ? "CHALLENGE" : "STAGE";
            string verdict = r.Cleared ? (r.Plan.Mode == GameMode.Campaign || r.Plan.Mode == GameMode.Daily ? "COMPLETE" : "CLEAR") : "FAILED";
            var title = Ui.Title(root, noun + " " + verdict, 110, r.Cleared ? Pal.Good : Pal.Bad, TextAnchor.MiddleCenter);
            title.rectTransform.At(0.5f, 1, 0, -110, 1400, 140);
            Ui.Shadow(title, 5, 0.9f);
            var sub = Ui.Label(root, r.Plan.Title + "  -  " + r.Plan.Subtitle, 38, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Display);
            sub.rectTransform.At(0.5f, 1, 0, -200, 1400, 60);
            if (r.Plan.Mode == GameMode.Campaign && r.Cleared)
            {
                for (int i = 0; i < 3; i++)
                {
                    var s = Ui.Img(root, Sprites.Of(Icon.Star), i < r.Stars ? Pal.Accent2 : Pal.Line, "Star");
                    s.rectTransform.At(0.5f, 1, (i - 1) * 90, -270, 80, 80);
                }
            }
            // Stats panel.
            var stats = Ui.Panel(root, Pal.Panel.A(0.92f), "Stats");
            stats.rectTransform.At(0.5f, 0.5f, -360, -40, 620, 470);
            string[] names = { "ZOMBIES KILLED", "HEADSHOTS", "ACCURACY", "TIME" };
            string[] vals = { r.Kills.ToString(), r.Headshots.ToString(), Mathf.RoundToInt(r.Accuracy * 100) + "%", StageGen.Clock(r.Time) };
            Icon[] icons = { Icon.Skull, Icon.Aim, Icon.Fire, Icon.Timer };
            for (int i = 0; i < names.Length; i++)
            {
                var row = Ui.Rect(stats.transform, "Row");
                row.Row(40 + i * 100, 90, 40, 40);
                var ic = Ui.Img(row, Sprites.Of(icons[i]), Pal.Dim, "Icon");
                ic.rectTransform.At(0, 0.5f, 30, 0, 50, 50);
                var n = Ui.Label(row, names[i], 32, Pal.Dim, TextAnchor.MiddleLeft, FontKind.Semi);
                n.rectTransform.Fill(80, 0, 0, 0);
                var v = Ui.Label(row, vals[i], 48, Pal.Text, TextAnchor.MiddleRight, FontKind.Display);
                v.rectTransform.Fill(0, 0, 10, 0);
            }
            // Rewards panel.
            var rew = Ui.Panel(root, Pal.Panel.A(0.92f), "Rewards");
            rew.rectTransform.At(0.5f, 0.5f, 360, -40, 620, 470);
            var rt = Ui.Label(rew.transform, "REWARDS", 40, Pal.Accent2, TextAnchor.UpperCenter, FontKind.Display);
            rt.rectTransform.Fill(0, 26, 0, 0);
            long coins = r.Reward.Coins + r.DailyCoins + r.RushCoins + r.CampaignCoins;
            long gems = r.Reward.Gems + r.DailyGems + r.RushGems + r.CampaignGems;
            var coinsT = Ui.Chip(rew.transform, Icon.Coin, Pal.Coin, "+" + coins.ToString("#,0"), 46);
            coinsT.transform.parent.GetComponent<RectTransform>().At(0.5f, 1, 0, -130, 460, 84);
            var xpT = Ui.Chip(rew.transform, Icon.Star, Pal.Accent, "+" + r.Reward.Xp.ToString("#,0") + " XP", 40);
            xpT.transform.parent.GetComponent<RectTransform>().At(0.5f, 1, 0, -230, 460, 76);
            if (gems > 0)
            {
                var g = Ui.Chip(rew.transform, Icon.Gem, Pal.Gem, "+" + gems, 40);
                g.transform.parent.GetComponent<RectTransform>().At(0.5f, 1, 0, -322, 460, 76);
            }
            if (r.LevelUps > 0)
            {
                var lu = Ui.Label(rew.transform, "LEVEL UP!  LEVEL " + SaveSystem.Data.Level + "  (+" + r.LevelUps + " SKILL POINT" + (r.LevelUps > 1 ? "S" : "") + ")", 30, Pal.Good, TextAnchor.LowerCenter, FontKind.Display);
                lu.rectTransform.Fill(10, 0, 10, 26);
                Sfx.LevelUp();
            }
            // Ad: double the coins (once).
            if (Ads.RewardedReady && r.Reward.Coins > 0 && !r.Doubled)
            {
                UiButton dbl = null;
                dbl = Ui.Button(root, "DOUBLE COINS  (WATCH AD)", () => Ads.ShowRewarded("double", ok =>
                {
                    if (!ok) { Screens.Toast("Ad not completed"); return; }
                    StageRunner.Instance?.DoubleReward();
                    coinsT.text = "+" + (coins + r.Reward.Coins).ToString("#,0");
                    dbl.SetInteractable(false);
                    Sfx.Buy();
                }), Pal.Good.Mul(0.85f), 32, Icon.Video);
                dbl.Bg.rectTransform.At(0.5f, 0, 0, 220, 640, 84);
            }
            // Actions.
            var plan = r.Plan;
            bool canContinue = r.Cleared && (plan.Mode == GameMode.Survival || plan.Mode == GameMode.BossRush || (plan.Mode == GameMode.Campaign && plan.CampaignMission + 1 < Campaign.All.Length));
            var menu = Ui.Button(root, "MENU", () => GameApp.Instance.ToMenu(), Pal.Panel2, 38, Icon.Map);
            menu.Bg.rectTransform.At(0.5f, 0, -470, 90, 360, 96);
            var retry = Ui.Button(root, r.Cleared ? "REPLAY" : "RETRY", () => GameApp.Instance.Retry(), Pal.Panel2, 38, Icon.Restore);
            retry.Bg.rectTransform.At(0.5f, 0, -40, 90, 360, 96);
            if (canContinue)
            {
                var next = Ui.Button(root, plan.Mode == GameMode.BossRush ? "NEXT ROUND" : "CONTINUE", () => GameApp.Instance.Next(), Pal.Accent, 40, Icon.Play);
                next.Bg.rectTransform.At(0.5f, 0, 420, 90, 420, 96);
            }
            else
            {
                var up = Ui.Button(root, "UPGRADE", () => { GameApp.Instance.ToMenu(); Screens.Show(new ArmoryScreen()); }, Pal.Accent, 40, Icon.Gear);
                up.Bg.rectTransform.At(0.5f, 0, 420, 90, 420, 96);
            }
        }
    }
}
