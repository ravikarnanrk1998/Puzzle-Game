using System;
using System.Collections.Generic;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>Front-end screens drawn over the live stadium backdrop.</summary>
    public static class Menus
    {
        internal enum Page { Main, Quick, Settings, HowTo, Result, Auction, Names }

        static Page _current;
        static Sprite _scrim, _vscrim;

        static RectTransform Root => GameApp.I.ScreenRoot;

        internal static RectTransform Begin(Page s)
        {
            _current = s;
            GameApp.I.ClearScreens();
            GameApp.I.EnsureBackdrop();
            // Only the main menu and quick match show the live stadium; other screens use a still.
            GameApp.I.SetBackdropLive(s == Page.Main || s == Page.Quick);
            var page = Ui.Node(Root, s.ToString()).Fill();
            page.gameObject.AddComponent<FadeIn>();
            return page;
        }

        /// <summary>Android back: one level up, or quit from the main menu.</summary>
        public static void Back()
        {
            if (GameApp.I.InMatch || AuctionScreens.InRoom) return;
            switch (_current)
            {
                case Page.Main:
                    Application.Quit();
                    break;
                default:
                    Main();
                    break;
            }
        }

        // -------------------------------------------------------------- art

        /// <summary>Horizontal dark-to-clear gradient for text legibility over the 3D scene.</summary>
        static Sprite Scrim
        {
            get
            {
                if (_scrim != null) return _scrim;
                var px = new Px(256, 2, new Color32(0, 0, 0, 0));
                for (int x = 0; x < 256; x++)
                {
                    float a = Mathf.Pow(1 - x / 255f, 1.6f);
                    for (int y = 0; y < 2; y++) px.P[y * 256 + x] = new Color(0.01f, 0.02f, 0.07f, a * 0.92f);
                }
                _scrim = Sprite.Create(px.ToTexture("Scrim", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 256, 2), new Vector2(0.5f, 0.5f));
                return _scrim;
            }
        }

        static Sprite VScrim
        {
            get
            {
                if (_vscrim != null) return _vscrim;
                var px = new Px(2, 128, new Color32(0, 0, 0, 0));
                for (int y = 0; y < 128; y++)
                {
                    float a = Mathf.Pow(1 - y / 127f, 1.4f);
                    for (int x = 0; x < 2; x++) px.P[y * 2 + x] = new Color(0.01f, 0.02f, 0.07f, a * 0.9f);
                }
                _vscrim = Sprite.Create(px.ToTexture("VScrim", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f));
                return _vscrim;
            }
        }

        static void LeftScrim(RectTransform page, float width)
        {
            var s = Ui.Img(page, Scrim, Color.white, "Scrim");
            s.preserveAspect = false;
            s.rectTransform.anchorMin = Vector2.zero;
            s.rectTransform.anchorMax = new Vector2(width, 1);
            s.rectTransform.offsetMin = s.rectTransform.offsetMax = Vector2.zero;
        }

        static void BottomScrim(RectTransform page, float height)
        {
            var s = Ui.Img(page, VScrim, Color.white, "BottomScrim");
            s.preserveAspect = false;
            s.rectTransform.anchorMin = Vector2.zero;
            s.rectTransform.anchorMax = new Vector2(1, height);
            s.rectTransform.offsetMin = s.rectTransform.offsetMax = Vector2.zero;
        }

        internal static void Dim(RectTransform page, float a)
        {
            var d = Ui.Img(page, null, new Color(0.01f, 0.02f, 0.07f, a), "Dim");
            d.preserveAspect = false;
            d.rectTransform.Fill();
        }

        internal static void TopBar(RectTransform page, string title, string sub = null)
        {
            var back = Ui.Ghost(page, "‹  BACK", Pal.Text, Main, 20);
            ((RectTransform)back.transform).At(0, 1, 18, -12, 110, 38);
            var t = Ui.Label(page, title, 34, Pal.Text, TextAnchor.MiddleLeft);
            t.rectTransform.At(0, 1, 144, -10, 500, 42);
            Ui.Shadow(t, 2, 0.6f);
            if (sub != null)
            {
                var s = Ui.Label(page, sub, 14, Pal.Gold, TextAnchor.MiddleLeft, false);
                s.rectTransform.At(0, 1, 146, -46, 600, 18);
            }
        }

        internal static void Toast(string text)
        {
            var bg = Ui.Panel(Root, new Color(0, 0, 0, 0.82f), 14, "Toast", Pal.Gold.WithA(0.5f));
            bg.rectTransform.At(0.5f, 0, 0, 34, 520, 40);
            var t = Ui.Label(bg.transform, text, 20, Pal.GoldLight);
            t.rectTransform.Fill(8, 0, 8, 0);
            UnityEngine.Object.Destroy(bg.gameObject, 2.2f);
        }

        // -------------------------------------------------------------- main

        public static void Main()
        {
            var page = Begin(Page.Main);
            LeftScrim(page, 0.62f);
            BottomScrim(page, 0.22f);

            var logo = Ui.Node(page, "Logo").At(0, 0.5f, 44, 26, 440, 250);
            var season = Ui.Label(logo, "—  SEASON " + League.Year + "  —", 17, Pal.Gold, TextAnchor.UpperLeft, false);
            season.rectTransform.At(0, 1, 4, 0, 400, 22);
            var l1 = Ui.Label(logo, "PREMIER", 92, Pal.Text, TextAnchor.UpperLeft);
            l1.rectTransform.At(0, 1, 0, -12, 440, 92);
            Ui.Shadow(l1, 3, 0.7f);
            var l2 = Ui.Label(logo, "CRICKET CLASH", 60, Pal.Gold, TextAnchor.UpperLeft);
            l2.rectTransform.At(0, 1, 2, -96, 440, 62);
            Ui.Shadow(l2, 3, 0.7f);
            var glowLine = Ui.Img(logo, null, Pal.Gold, "Line");
            glowLine.preserveAspect = false;
            glowLine.rectTransform.At(0, 1, 4, -164, 330, 3);
            var line2 = Ui.Img(logo, null, Pal.Cyan, "Line2");
            line2.preserveAspect = false;
            line2.rectTransform.At(0, 1, 338, -164, 60, 3);
            var tag = Ui.Label(logo, "T20 FRANCHISE CRICKET  •  10 TEAMS  •  220 PLAYERS", 15, Pal.Dim, TextAnchor.UpperLeft, false);
            tag.rectTransform.At(0, 1, 4, -176, 440, 20);

            // Team crests ribbon.
            var ribbon = Ui.Node(page, "Ribbon").At(0, 0, 44, 46, 440, 40);
            for (int i = 0; i < Teams.All.Count; i++)
            {
                var c = Crest.Add(ribbon, Teams.All[i], 34);
                ((RectTransform)c.transform.parent).At(0, 0, i * 42, 0, 34, 38);
            }

            // Menu buttons (right).
            var col = Ui.Node(page, "Buttons").At(1, 0.5f, -40, 0, 300, 330);
            var quick = Ui.Button(col, "QUICK MATCH", Pal.Gold, Pal.GoldDeep, QuickMatch, 34, "Play a T20 now");
            ((RectTransform)quick.transform).At(0.5f, 1, 0, 0, 300, 72);
            var seasonBtn = Ui.Button(col, "SEASON " + League.Year, Pal.Violet, new Color(0.25f, 0.12f, 0.6f),
                () => Toast("Season mode is coming in the next update"), 26, "League + playoffs  •  coming soon");
            ((RectTransform)seasonBtn.transform).At(0.5f, 1, 0, -84, 300, 60);
            var auction = Ui.Button(col, "MEGA AUCTION", Pal.Pink, new Color(0.55f, 0.08f, 0.35f),
                AuctionScreens.Hub, 26, AuctionStore.HasLive ? "Auction in progress  •  tap to resume"
                    : AuctionStore.HasResult ? "See your results  •  or run a new one" : "Retain, bid and build your squad");
            ((RectTransform)auction.transform).At(0.5f, 1, 0, -156, 300, 60);
            var settings = Ui.Ghost(col, "SETTINGS", Pal.Text, SettingsScreen, 18);
            ((RectTransform)settings.transform).At(0, 1, 0, -230, 96, 48);
            var names = Ui.Ghost(col, "EDIT NAMES", Pal.Gold, NameEditor.Show, 18);
            ((RectTransform)names.transform).At(0.5f, 1, 0, -230, 96, 48);
            var how = Ui.Ghost(col, "HOW TO PLAY", Pal.Cyan, HowTo, 18);
            ((RectTransform)how.transform).At(1, 1, 0, -230, 96, 48);

            var foot = Ui.Label(page, "v" + Application.version + "  •  Unofficial fan game. Not affiliated with or endorsed by any league, board, team or player.", 12, Pal.Mute, TextAnchor.LowerRight, false);
            foot.rectTransform.At(1, 0, -16, 8, 600, 16);
        }

        // -------------------------------------------------------- quick match

        static int _userIdx = -1, _oppIdx = -1, _overs, _time;

        public static void QuickMatch()
        {
            if (_userIdx < 0)
            {
                _userIdx = Mathf.Max(0, Teams.IndexOf(Teams.ById(PlayerPrefs.GetString("pcc.team", "MUM"))));
                _oppIdx = Mathf.Max(0, Teams.IndexOf(Teams.ById(PlayerPrefs.GetString("pcc.opp", "CHC"))));
                if (_oppIdx == _userIdx) _oppIdx = (_userIdx + 1) % Teams.All.Count;
                _overs = PlayerPrefs.GetInt("pcc.overs", 5);
                _time = GameApp.ResolveDay(Settings.Lighting) ? 0 : 1;
            }
            var page = Begin(Page.Quick);
            Dim(page, 0.45f);
            TopBar(page, "QUICK MATCH", "Pick the teams, overs and time of play");
            if (AuctionStore.HasResult)
            {
                var sq = Ui.Node(page, "Squads").At(1, 1, -18, -10, 300, 30);
                Ui.Segments(sq, new[] { "AUCTION SQUADS", "ORIGINAL" }, AuctionStore.UseSquads ? 0 : 1, i =>
                {
                    AuctionStore.UseSquads = i == 0;
                    QuickMatch();
                }, 15);
            }

            var userCard = TeamCard(page, "YOUR TEAM", 0, () => _userIdx, i => _userIdx = i, () => _oppIdx);
            ((RectTransform)userCard).At(0.5f, 1, -235, -68, 400, 196);
            var oppCard = TeamCard(page, "OPPONENT", 1, () => _oppIdx, i => _oppIdx = i, () => _userIdx);
            ((RectTransform)oppCard).At(0.5f, 1, 235, -68, 400, 196);
            var vs = Ui.Label(page, "VS", 58, Pal.Gold);
            vs.rectTransform.At(0.5f, 1, 0, -128, 80, 60);
            Ui.Shadow(vs, 3, 0.7f);

            // Options.
            var opts = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.9f), 16, "Options", Pal.Stroke);
            opts.rectTransform.At(0.5f, 0, -120, 14, 680, 96);
            var o = opts.rectTransform;
            int[] overOptions = { 2, 5, 10, 20 };
            int oversIdx = Array.IndexOf(overOptions, _overs);
            OptionRow(o, "OVERS", 0, new[] { "2", "5", "10", "20" }, oversIdx >= 0 ? oversIdx : 1, i => _overs = overOptions[i], 250);
            OptionRow(o, "START", 1, new[] { "3:30 PM DAY", "7:30 PM NIGHT" }, _time, i => _time = i, 250);
            var diffCap = Ui.Label(o, "LEVEL", 13, Pal.Gold, TextAnchor.MiddleLeft, false);
            diffCap.rectTransform.At(0, 1, 360, -12, 60, 20);
            var diff = Ui.Node(o, "Difficulty").At(0, 1, 420, -6, 248, 34);
            Ui.Segments(diff, new[] { "EASY", "NORMAL", "HARD" }, (int)Settings.Difficulty, i => Settings.Difficulty = (Difficulty)i, 17);
            var ctrlCap = Ui.Label(o, "BATTING", 13, Pal.Gold, TextAnchor.MiddleLeft, false);
            ctrlCap.rectTransform.At(0, 1, 360, -54, 60, 20);
            var ctrl = Ui.Node(o, "Controls").At(0, 1, 420, -48, 248, 34);
            Ui.Segments(ctrl, new[] { "SWIPE", "BUTTONS" }, (int)Settings.Batting, i => Settings.Batting = (BattingControls)i, 17);

            var play = Ui.Button(page, "PLAY", Pal.Gold, Pal.GoldDeep, () =>
            {
                Settings.Save();
                GameApp.I.StartQuickMatch(Teams.All[_userIdx], Teams.All[_oppIdx], _overs, _time == 0);
            }, 40, "at " + Teams.All[_userIdx].Venue);
            ((RectTransform)play.transform).At(1, 0, -22, 14, 230, 96);
        }

        /// <summary>Forget the remembered quick-match teams (re-read from prefs next time).</summary>
        public static void ResetQuickMatch() => _userIdx = -1;

        static void OptionRow(RectTransform parent, string label, int row, string[] options, int selected, Action<int> pick, float width)
        {
            var cap = Ui.Label(parent, label, 13, Pal.Gold, TextAnchor.MiddleLeft, false);
            cap.rectTransform.At(0, 1, 14, -12 - row * 42, 60, 20);
            var seg = Ui.Node(parent, label).At(0, 1, 78, -6 - row * 42, width + 20, 34);
            Ui.Segments(seg, options, selected, pick, 17);
        }

        static Transform TeamCard(RectTransform page, string caption, int side, Func<int> get, Action<int> set, Func<int> other)
        {
            var team = Teams.All[get()];
            var card = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.88f), 18, caption, Mats.Hex(team.Glow).WithA(0.7f));
            var rt = card.rectTransform;
            var cap = Ui.Label(rt, caption, 14, side == 0 ? Pal.Gold : Pal.Pink, TextAnchor.UpperCenter, false);
            cap.rectTransform.At(0.5f, 1, 0, -8, 200, 18);
            var crest = Crest.Add(rt, team, 92, true);
            ((RectTransform)crest.transform.parent).At(0.5f, 1, 0, -30, 92, 103);
            var name = Ui.Label(rt, team.Name.ToUpperInvariant(), 28, Pal.Text);
            name.rectTransform.At(0.5f, 0, 0, 34, 360, 30);
            var sheet = TeamSheet.Auto(team, AuctionStore.SquadFor(team));
            int ovr = Lineup.TeamRating(sheet.Order), bat = Lineup.BatRating(sheet.Order), bowl = Lineup.BowlRating(sheet.Order);
            var stats = Ui.Label(rt, "OVR <color=#FFE9A3>" + ovr + "</color>   BAT " + bat + "   BOWL " + bowl + "   •   " + team.City.ToUpperInvariant(), 14, Pal.Dim, TextAnchor.MiddleCenter, false);
            stats.rectTransform.At(0.5f, 0, 0, 12, 380, 18);

            void Step(int dir)
            {
                int n = Teams.All.Count, i = get();
                do i = (i + dir + n) % n;
                while (i == other());
                set(i);
                QuickMatch();
            }
            var left = Ui.Ghost(rt, "‹", Pal.Text, () => Step(-1), 40);
            ((RectTransform)left.transform).At(0, 0.5f, 12, 12, 52, 64);
            var right = Ui.Ghost(rt, "›", Pal.Text, () => Step(1), 40);
            ((RectTransform)right.transform).At(1, 0.5f, -12, 12, 52, 64);
            return rt;
        }

        // ----------------------------------------------------------- settings

        public static void SettingsScreen()
        {
            var page = Begin(Page.Settings);
            Dim(page, 0.55f);
            TopBar(page, "SETTINGS");
            var card = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.92f), 18, "Card", Pal.Stroke);
            card.rectTransform.At(0.5f, 0, 0, 12, 820, 352);
            var c = card.rectTransform;
            int row = 0;

            void Row(string label, string help, string[] options, int selected, Action<int> pick)
            {
                float y = -10 - row * 42;
                var l = Ui.Label(c, label, 20, Pal.Text, TextAnchor.UpperLeft);
                l.rectTransform.At(0, 1, 20, y, 300, 24);
                var h = Ui.Label(c, help, 12, Pal.Dim, TextAnchor.UpperLeft, false);
                h.rectTransform.At(0, 1, 20, y - 22, 440, 16);
                var seg = Ui.Node(c, label).At(1, 1, -18, y, 360, 34);
                Ui.Segments(seg, options, selected, pick, 17);
                row++;
            }

            Row("GRAPHICS", "LOW runs coolest  •  HIGH adds soft shadows, bloom, floodlights",
                new[] { "LOW", "MEDIUM", "HIGH" }, (int)Settings.Graphics, i =>
                {
                    Settings.Graphics = (GraphicsQuality)i;
                    SceneLook.ApplyPipeline();
                });
            Row("FRAME RATE", "30 FPS saves battery and heat  •  60 FPS is smoother in matches", new[] { "30 BATTERY", "60 SMOOTH" },
                Settings.Smooth60 ? 1 : 0, i => Settings.Smooth60 = i == 1);
            Row("DIFFICULTY", "Timing windows and AI bowling accuracy", new[] { "EASY", "NORMAL", "HARD" }, (int)Settings.Difficulty,
                i => Settings.Difficulty = (Difficulty)i);
            Row("BATTING CONTROLS", "Swipe the shot direction  •  or stick + DEFEND / GROUND / LOFT", new[] { "SWIPE", "BUTTONS" }, (int)Settings.Batting,
                i => Settings.Batting = (BattingControls)i);
            Row("MATCH TIME", "Auto picks day or night from your clock", new[] { "AUTO", "DAY", "NIGHT" }, (int)Settings.Lighting, i =>
            {
                Settings.Lighting = (MatchTime)i;
                _userIdx = -1;
            });
            Row("TIMING HINT", "Ring and pitch marker that show when to play", new[] { "ON", "OFF" }, Settings.TimingHint ? 0 : 1, i => Settings.TimingHint = i == 0);
            Row("SOUND", "Crowd, bat and stumps effects", new[] { "ON", "OFF" }, Settings.Sound ? 0 : 1, i =>
            {
                Settings.Sound = i == 0;
                if (Settings.Sound) Sfx.I?.StartCrowd(0.16f);
                else Sfx.I?.StopCrowd();
            });
            Row("VIBRATION", "Buzz on wickets", new[] { "ON", "OFF" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0);
        }

        // ----------------------------------------------------------- how to

        public static void HowTo()
        {
            var page = Begin(Page.HowTo);
            Dim(page, 0.55f);
            TopBar(page, "HOW TO PLAY");
            var items = new[]
            {
                ("BATTING", Pal.Cyan, "SWIPE mode\n• Swipe in the direction you want to hit, just as the ball arrives.\n• A fast swipe or the LOFT toggle goes aerial.\n• Tap to defend.\n\nBUTTONS mode\n• Aim with the stick (off / leg / straight).\n• Press GROUND, LOFT or DEFEND on time."),
                ("BOWLING", Pal.Pink, "• Pick a delivery from the dropdown (stock, slower, swing, quicker, mystery).\n• Drag the gold target on the pitch: yorker, full, good length, short or bouncer.\n• Press BOWL, then stop the meter in the green zone for accuracy.\n• AUTO bowls for you."),
                ("TIPS", Pal.Gold, "• The shrinking ring shows when the ball arrives. Play when it turns green.\n• Late hits go square, early hits go straight.\n• Good-length balls outside off are hard to hit for six.\n• Pause to simulate an over, an innings or the whole match."),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var (title, color, body) = items[i];
                var card = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.92f), 18, title, color.WithA(0.6f));
                card.rectTransform.At(0.5f, 0, (i - 1) * 300, 16, 288, 320);
                var t = Ui.Label(card.transform, title, 30, color, TextAnchor.UpperLeft);
                t.rectTransform.Fill(16, 10, 16, 0);
                var p = Ui.Para(card.transform, body, 14, Pal.Text);
                p.rectTransform.Fill(16, 50, 14, 10);
            }
        }

        // ----------------------------------------------------------- result

        public static void Result(MatchConfig cfg)
        {
            var m = cfg.Engine;
            var page = Begin(Page.Result);
            Dim(page, 0.6f);
            var o = m.Outcome;
            bool won = o.WinnerId == cfg.UserTeamId;
            var winner = Teams.ById(o.WinnerId);

            var side = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.92f), 18, "Summary", (won ? Pal.Gold : Pal.Red).WithA(0.6f));
            side.rectTransform.At(0, 0.5f, 14, 0, 250, 400);
            var s = side.rectTransform;
            var title = Ui.Label(s, won ? "VICTORY" : "DEFEAT", 50, won ? Pal.Gold : Pal.Red);
            title.rectTransform.At(0.5f, 1, 0, -6, 240, 52);
            var crest = Crest.Add(s, winner, 80, true);
            ((RectTransform)crest.transform.parent).At(0.5f, 1, 0, -62, 80, 90);
            var text = Ui.Para(s, o.Text, 17, Pal.Text, TextAnchor.UpperCenter);
            text.rectTransform.At(0.5f, 1, 0, -160, 220, 46);
            var potm = m.PlayerById(o.PotmId);
            var pc = Ui.Label(s, "PLAYER OF THE MATCH", 12, Pal.Gold, TextAnchor.UpperCenter, false);
            pc.rectTransform.At(0.5f, 1, 0, -212, 220, 16);
            var pn = Ui.Label(s, potm.Name, 24, Pal.Text, TextAnchor.UpperCenter);
            pn.rectTransform.At(0.5f, 1, 0, -228, 230, 26);
            var again = Ui.Button(s, "REMATCH", Pal.Gold, Pal.GoldDeep, () =>
            {
                var user = Teams.ById(cfg.UserTeamId);
                var opp = m.Home.Team.Id == user.Id ? m.Away.Team : m.Home.Team;
                GameApp.I.StartQuickMatch(user, opp, m.Overs, cfg.Day);
            }, 26);
            ((RectTransform)again.transform).At(0.5f, 0, 0, 72, 220, 52);
            var menu = Ui.Ghost(s, "MAIN MENU", Pal.Text, Main, 20);
            ((RectTransform)menu.transform).At(0.5f, 0, 0, 14, 220, 46);

            var card = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.92f), 18, "Scorecard", Pal.Cyan.WithA(0.45f));
            card.rectTransform.anchorMin = new Vector2(0, 0);
            card.rectTransform.anchorMax = new Vector2(1, 1);
            card.rectTransform.offsetMin = new Vector2(276, 16);
            card.rectTransform.offsetMax = new Vector2(-14, -16);
            Scorecard.Build(card.rectTransform, m, null);
        }
    }

    /// <summary>Quick fade and rise when a screen opens.</summary>
    public sealed class FadeIn : MonoBehaviour
    {
        CanvasGroup _g;
        float _t;

        void Awake()
        {
            _g = gameObject.AddComponent<CanvasGroup>();
            _g.alpha = 0;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / 0.25f);
            _g.alpha = k;
            if (k >= 1) Destroy(this);
        }
    }
}
