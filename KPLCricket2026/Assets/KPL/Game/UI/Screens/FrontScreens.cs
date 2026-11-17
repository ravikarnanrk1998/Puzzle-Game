using System;
using System.Collections;
using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Studio card then the KPL logo reveal.</summary>
    public static class SplashScreen
    {
        public static IEnumerator Run(RectTransform parent)
        {
            var root = Ui.Node(parent, "Splash").Fill();
            var bg = Ui.Img(root, null, Pal.Hex(0x03060D), "Bg");
            bg.raycastTarget = true;
            bg.rectTransform.Fill();
            bool tapped = false;
            bg.gameObject.AddComponent<TapCatcher>().Tapped = () => tapped = true;
            var glow = Ui.Img(root, Sprites.Glow, Pal.Accent.A(0.0f), "Glow");
            glow.rectTransform.At(0.5f, 0.5f, 0, 0, 1100, 1100);
            var studio = Ui.Label(root, "RK GAMES", 46, Color.white, TextAnchor.MiddleCenter, Font.Display);
            studio.rectTransform.At(0.5f, 0.5f, 0, 0, 800, 60);
            var presents = Ui.Label(root, "PRESENTS", 18, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
            presents.rectTransform.At(0.5f, 0.5f, 0, -44, 800, 24);
            var cg1 = studio.gameObject.AddComponent<CanvasGroup>();
            var cg2 = presents.gameObject.AddComponent<CanvasGroup>();
            float t = 0;
            while (t < 1.6f && !tapped)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((1.6f - t) / 0.4f);
                cg1.alpha = cg2.alpha = a;
                yield return null;
            }
            studio.gameObject.SetActive(false);
            presents.gameObject.SetActive(false);
            var logo = Logo(root, 1.0f);
            logo.At(0.5f, 0.5f, 0, 20, 900, 360);
            var cg = logo.gameObject.AddComponent<CanvasGroup>();
            Audio.Play(Sfx.Whoosh, 0.8f);
            t = 0;
            while (t < 2.0f && !tapped)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.5f);
                float s = Mathf.Lerp(1.25f, 1f, 1 - Mathf.Pow(1 - k, 3));
                logo.localScale = new Vector3(s, s, 1);
                cg.alpha = k * Mathf.Clamp01((2.0f - t) / 0.35f);
                glow.color = Pal.Accent.A(0.35f * k * Mathf.Clamp01((2.0f - t) / 0.35f));
                yield return null;
            }
            UnityEngine.Object.Destroy(root.gameObject);
        }

        /// <summary>The KPL CRICKET 2026 wordmark.</summary>
        public static RectTransform Logo(Transform parent, float scale)
        {
            var root = Ui.Node(parent, "Logo");
            var mark = Brand.Logo(root, 210 * scale, false);
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.anchoredPosition = new Vector2(-10 * scale, 66 * scale);
            var band = Ui.Panel(root, Pal.Accent, (int)(14 * scale), "Band");
            band.rectTransform.At(0.5f, 0.5f, 0, -62 * scale, 520 * scale, 70 * scale);
            var shine = Ui.Img(band.transform, Sprites.VGradient(0, 0.35f), Color.white, "Shine");
            shine.rectTransform.Fill();
            var cr = Ui.Label(band.transform, "CRICKET 2026", (int)(54 * scale), Color.white, TextAnchor.MiddleCenter, Font.Display);
            cr.rectTransform.Fill();
            var tag = Ui.Label(root, "THE FRANCHISE T20 LEAGUE", (int)(18 * scale), Pal.Gold, TextAnchor.MiddleCenter, Font.Bold);
            tag.rectTransform.At(0.5f, 0.5f, 0, -120 * scale, 700 * scale, 26 * scale);
            return root;
        }
    }

    /// <summary>Full-screen loading card with progress bar and rotating tips.</summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        RectTransform _fill;
        Text _status, _tip;
        float _target, _shown;
        float _tipT;
        int _tipIdx;
        CanvasGroup _group;
        bool _closing;

        static readonly string[] Tips =
        {
            "Centre the stick and the batter plays the natural shot for that ball.",
            "Short balls outside off are made for the cut. Short on the body? Pull it.",
            "Against spin, the sweep (leg side, along the ground) is a safe way to score.",
            "Lofted shots need cleaner timing - a mistimed loft is a catch.",
            "Bowling: vary your deliveries. The same ball every time becomes easy to hit.",
            "Yorkers at the death are hard to score from - but miss and it's a full toss.",
            "Train your players in the Squad screen to raise their ratings.",
            "Buy a better bat in the Shop for a bigger timing window.",
            "Win the KPL: top four after the league stage reach the playoffs.",
            "Claim your daily reward every day - day 7 pays 1,000 coins.",
        };

        public static LoadingScreen Show(RectTransform parent, string title)
        {
            var root = Ui.Node(parent, "Loading").Fill();
            var l = root.gameObject.AddComponent<LoadingScreen>();
            l._group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = Ui.Img(root, null, Pal.Hex(0x050A16), "Bg");
            bg.raycastTarget = true;
            bg.rectTransform.Fill();
            var grad = Ui.Img(root, Sprites.VGradient(0.0f, 1f), Pal.Hex(0x13285C), "Grad");
            grad.rectTransform.Fill();
            var stripes = Ui.Img(root, Sprites.Stripes, new Color(1, 1, 1, 0.35f), "Stripes");
            stripes.type = Image.Type.Tiled;
            stripes.rectTransform.Fill();
            var glow = Ui.Img(root, Sprites.Glow, Pal.Accent.A(0.25f), "Glow");
            glow.rectTransform.At(0.5f, 0.5f, 0, 80, 1000, 900);
            var logo = SplashScreen.Logo(root, 0.62f);
            logo.At(0.5f, 0.5f, 0, 110, 600, 260);
            var t = Ui.Label(root, title.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 0, 0, 210, 1100, 34);
            var barBg = Ui.Panel(root, new Color(1, 1, 1, 0.1f), 10, "BarBg");
            barBg.rectTransform.At(0.5f, 0, 0, 160, 640, 16);
            l._fill = Ui.Img(barBg.transform, Sprites.Round(8), Pal.Accent, "Fill").rectTransform;
            l._fill.anchorMin = new Vector2(0, 0);
            l._fill.anchorMax = new Vector2(0, 1);
            l._fill.pivot = new Vector2(0, 0.5f);
            l._fill.offsetMin = l._fill.offsetMax = Vector2.zero;
            l._status = Ui.Label(root, "Loading", 18, Pal.Dim, TextAnchor.MiddleCenter, Font.Semi);
            l._status.rectTransform.At(0.5f, 0, 0, 126, 640, 24);
            var tipBg = Ui.Panel(root, Pal.Hex(0x0B1426, 0.85f), 16, "Tip", Pal.Gold.A(0.3f));
            tipBg.rectTransform.At(0.5f, 0, 0, 40, 900, 58);
            var ti = Ui.IconImg(tipBg.transform, Icon.Info, Pal.Gold, 28);
            ti.rectTransform.At(0, 0.5f, 18, 0, 28, 28);
            l._tip = Ui.Label(tipBg.transform, "", 18, Pal.Text, TextAnchor.MiddleLeft, Font.Semi);
            l._tip.rectTransform.Fill(60, 0, 18, 0);
            l._tipIdx = UnityEngine.Random.Range(0, Tips.Length);
            l._tip.text = "TIP: " + Tips[l._tipIdx];
            var spin = Ui.Img(root, Sprites.Ring(8), Pal.Gold, "Spin");
            spin.rectTransform.At(1, 0, -40, 40, 48, 48);
            spin.fillMethod = Image.FillMethod.Radial360;
            spin.type = Image.Type.Filled;
            spin.fillAmount = 0.7f;
            spin.gameObject.AddComponent<Spin>();
            return l;
        }

        public void SetProgress(float p, string status)
        {
            _target = Mathf.Max(_target, p);
            if (!string.IsNullOrEmpty(status)) _status.text = status + "...";
        }

        public void Close()
        {
            _closing = true;
        }

        void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime * 1.6f);
            _fill.anchorMax = new Vector2(_shown, 1);
            _tipT += Time.unscaledDeltaTime;
            if (_tipT > 4)
            {
                _tipT = 0;
                _tipIdx = (_tipIdx + 1) % Tips.Length;
                _tip.text = "TIP: " + Tips[_tipIdx];
            }
            if (_closing)
            {
                _group.alpha -= Time.unscaledDeltaTime * 3.5f;
                if (_group.alpha <= 0) Destroy(gameObject);
            }
        }
    }

    public static class MainMenu
    {
        static bool _dailyShown;

        public static void Show()
        {
            GameApp.I.EnsureMenuScene();
            var save = SaveSystem.Data;
            var fav = Teams.ById(string.IsNullOrEmpty(save.Fav) ? "MUM" : save.Fav);
            var page = Nav.Page("Main", null);
            var left = Ui.Img(page, Sprites.HGradient(0.88f, 0), Pal.Hex(0x040811), "LeftScrim");
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(0.62f, 1);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
            var bottom = Ui.Img(page, Sprites.VGradient(0.85f, 0), Pal.Hex(0x040811), "BottomScrim");
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1, 0.3f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;

            var logo = SplashScreen.Logo(page, 0.55f);
            logo.At(0, 1, 40, -24, 500, 240);
            Enter.Add(logo, 0.05f, 20);

            // Profile chip.
            var chip = Ui.Panel(page, Pal.Hex(0x0B1426, 0.9f), 18, "Profile", Crest.Primary(fav).A(0.7f));
            chip.rectTransform.At(0, 1, 40, -270, 420, 76);
            var cr = Crest.Add(chip.transform, fav, 60);
            cr.At(0, 0.5f, 10, 0, 60, 60);
            var tn = Ui.Label(chip.transform, fav.Name.ToUpperInvariant(), 26, Color.white, TextAnchor.UpperLeft, Font.Display);
            tn.rectTransform.At(0, 1, 82, -8, 320, 32);
            var st = save.Stats;
            var ts = Ui.Label(chip.transform, "P " + st.Matches + "  •  W " + st.Wins + "  •  ★ " + save.TotalStars + "/150  •  🏆 " + st.TourWon, 16, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
            ts.text = "P " + st.Matches + "   W " + st.Wins + "   STARS " + save.TotalStars + "/150   TITLES " + st.TourWon;
            ts.rectTransform.At(0, 1, 82, -44, 330, 22);
            var chg = chip.gameObject.AddComponent<Button>();
            chg.transition = Selectable.Transition.None;
            chg.onClick.AddListener(() => TeamSelectScreen.Show(false));
            chip.gameObject.AddComponent<Pressable>();
            Enter.Add(chip, 0.1f);

            ScreenKit.CoinPill(page);
            var gift = Ui.IconBtn(page, Icon.Gift, () => DailyPopup.Show(), 54, Pal.Gold);
            gift.Rt().At(1, 1, -224, -16, 54, 54);
            if (Economy.DailyAvailable(save, DateTime.Now))
            {
                var dot = Ui.Img(gift.transform, Sprites.Circle, Pal.Red, "Dot");
                dot.rectTransform.At(1, 1, 4, 4, 16, 16);
                gift.gameObject.AddComponent<Pulse>().Amount = 0.06f;
            }
            var cog = Ui.IconBtn(page, Icon.Gear, SettingsScreen.Show, 54);
            cog.Rt().At(1, 1, -290, -16, 54, 54);

            // Mode tiles.
            var col = Ui.Node(page, "Modes").At(1, 0.5f, -28, -10, 520, 470);
            float y = 0;
            var tour = save.HasTour && !save.Tour.Complete ? save.Tour : null;
            string tourSub = tour == null ? "League + playoffs. Lift the trophy." : tour.StageEnum == TourStage.League ? "Season in progress  •  " + tour.UserGamesPlayed + "/" + tour.UserGamesTotal + " played" : tour.StageEnum.Label() + "  •  playoffs!";
            Tile(col, ref y, 150, "KPL TOURNAMENT", tourSub, Icon.Trophy, Pal.Accent, () => TournamentScreen.Show(), true, 0.12f);
            Tile(col, ref y, 96, "QUICK MATCH", "Pick teams, overs and play now", Icon.Play, Pal.Teal, () => QuickMatchScreen.Show(), false, 0.17f);
            Tile(col, ref y, 96, "CHALLENGES", save.TotalStars + " / 150 stars  •  " + save.ChallengeStars.Count(x => x > 0) + " of 50 cleared", Icon.Star, Pal.Gold, () => ChallengesScreen.Show(-1), false, 0.22f);
            Tile(col, ref y, 96, "NETS PRACTICE", "Learn the controls - no pressure", Icon.Target, Pal.Sky, () => NetsPopup(), false, 0.27f);

            // Bottom nav.
            var nav = Ui.Node(page, "Nav").At(0, 0, 40, 26, 720, 70);
            var lay = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 10;
            lay.childForceExpandWidth = true;
            lay.childControlWidth = true;
            lay.childControlHeight = true;
            Ui.Btn(nav, "SQUAD", BtnStyle.Secondary, () => SquadScreen.Show(), 20, Icon.Users);
            var shopBtn = Ui.Btn(nav, "SHOP", BtnStyle.Secondary, () => ShopScreen.Show(0), 20, Icon.Cart);
            var statsBtn = Ui.Btn(nav, "STATS", BtnStyle.Secondary, () => StatsScreen.Show(0), 20, Icon.Chart);
            if (Achievements.Claimable(save) > 0)
            {
                var dot = Ui.Img(statsBtn.transform, Sprites.Circle, Pal.Red, "Dot");
                dot.rectTransform.At(1, 1, 4, 4, 18, 18);
            }
            Ui.Btn(nav, "HOW TO", BtnStyle.Secondary, () => HowToScreen.Show(), 20, Icon.Info);
            Enter.Add(nav, 0.3f);
            _ = shopBtn;

            var foot = Ui.Label(page, "v" + Application.version + "  •  Fictional teams, players and league. Not affiliated with any real league, board, team or player.", 12, Pal.Mute, TextAnchor.LowerRight, Font.Semi);
            foot.rectTransform.At(1, 0, -24, 8, 760, 16);

            if (!_dailyShown && Economy.DailyAvailable(save, DateTime.Now) && !AutoPilot.Requested)
            {
                _dailyShown = true;
                DailyPopup.Show();
            }
        }

        static void Tile(RectTransform col, ref float y, float h, string title, string sub, Icon icon, Color c, Action click, bool hero, float delay)
        {
            var b = Ui.Node(col, title).At(0, 1, 0, -y, 520, h);
            var bg = Ui.Panel(b, Pal.Hex(0x0B1426, 0.9f), 20, "Bg", c.A(hero ? 0.9f : 0.45f));
            bg.rectTransform.Fill();
            var side = Ui.Img(b, Sprites.HGradient(0.55f, 0), c, "Accent");
            side.rectTransform.Fill(2, 2, 160, 2);
            if (hero)
            {
                var stripes = Ui.Img(b, Sprites.Stripes, new Color(1, 1, 1, 0.5f), "Stripes");
                stripes.type = Image.Type.Tiled;
                stripes.rectTransform.Fill(2, 2, 2, 2);
            }
            var ib = Ui.Img(b, Sprites.Circle, c, "IconBg");
            float isz = hero ? 92 : 66;
            ib.rectTransform.At(0, 0.5f, 18, 0, isz, isz);
            var ic = Ui.IconImg(ib.transform, icon, c.Luma() > 0.6f ? Pal.Ink : Color.white, isz * 0.55f);
            ic.rectTransform.At(0.5f, 0.5f, 0, 0, isz * 0.55f, isz * 0.55f);
            var t = Ui.Label(b, title, hero ? 44 : 32, Color.white, TextAnchor.UpperLeft, Font.Display);
            t.rectTransform.At(0, 1, isz + 36, hero ? -26 : -14, 380, hero ? 50 : 38);
            var s = Ui.Label(b, sub, 17, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
            s.rectTransform.At(0, 1, isz + 38, hero ? -80 : -54, 380, 22);
            var arrow = Ui.IconImg(b, Icon.Arrow, c, 26);
            arrow.rectTransform.At(1, 0.5f, -20, 0, 26, 26);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Audio.Play(Sfx.Confirm);
                click();
            });
            b.gameObject.AddComponent<Pressable>().Depth = 0.97f;
            if (hero) b.gameObject.AddComponent<Pulse>().Amount = 0.012f;
            Enter.Add(b, delay, 24);
            y += h + 14;
        }

        static void NetsPopup()
        {
            var c = Popup.Open("NETS PRACTICE", 640, 330);
            var p = Ui.Para(c, "Practise against the bowlers and batters of a rival franchise. No score pressure, no rewards - just you, the ball and your timing. Pause any time to end the session.", 19, Pal.Text);
            p.rectTransform.Fill(28, 70, 28, 110);
            var root = c.parent.gameObject;
            var bat = Ui.Btn(c, "BATTING", BtnStyle.Teal, () => { Popup.Close(root); GameApp.I.StartMatch(Matches.Nets(true)); }, 26, Icon.Bat);
            bat.Rt().At(0.5f, 0, -150, 26, 270, 66);
            var bowl = Ui.Btn(c, "BOWLING", BtnStyle.Primary, () => { Popup.Close(root); GameApp.I.StartMatch(Matches.Nets(false)); }, 26, Icon.Ball);
            bowl.Rt().At(0.5f, 0, 150, 26, 270, 66);
        }
    }

    /// <summary>Pick a favourite franchise (first launch, or from the profile chip).</summary>
    public static class TeamSelectScreen
    {
        static int _sel;

        public static void Show(bool first)
        {
            var save = SaveSystem.Data;
            if (!string.IsNullOrEmpty(save.Fav)) _sel = Teams.IndexOf(save.Fav);
            var page = ScreenKit.Page("TeamSelect", first ? "CHOOSE YOUR FRANCHISE" : "CHANGE YOUR TEAM", "Ten original KPL franchises  •  pick the one you'll lead", first ? null : (Action)MainMenu.Show, 0.6f);
            var grid = Ui.Node(page, "Grid").At(0.5f, 1, 0, -96, 1220, 400);
            for (int i = 0; i < Teams.All.Count; i++)
            {
                int idx = i;
                var t = Teams.All[i];
                int col = i % 5, row = i / 5;
                var card = Ui.Node(grid, t.Id).At(0, 1, col * 246, -row * 200, 234, 188);
                bool on = i == _sel;
                var bg = Ui.Panel(card, on ? Crest.Primary(t).A(0.55f) : Pal.Hex(0x0B1426, 0.92f), 18, "Bg", on ? Pal.Gold : Crest.Primary(t).A(0.6f));
                bg.rectTransform.Fill();
                var cr = Crest.Add(card, t, 92);
                cr.At(0.5f, 1, 0, -12, 92, 92);
                var n = Ui.Label(card, t.City.ToUpperInvariant(), 16, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                n.rectTransform.At(0.5f, 0, 0, 58, 220, 20);
                var nm = Ui.Label(card, t.Nick.ToUpperInvariant(), 30, Color.white, TextAnchor.MiddleCenter, Font.Display);
                nm.rectTransform.At(0.5f, 0, 0, 24, 220, 34);
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                card.gameObject.AddComponent<Pressable>();
                b.onClick.AddListener(() =>
                {
                    Audio.Play(Sfx.Click);
                    _sel = idx;
                    Show(first);
                });
                Enter.Add(card, i * 0.02f, 14);
            }
            var team = Teams.All[Mathf.Clamp(_sel, 0, Teams.All.Count - 1)];
            var sheet = TeamSheet.Default(team);
            var info = Ui.Panel(page, Pal.Hex(0x0B1426, 0.92f), 18, "Info", Crest.Primary(team).A(0.7f));
            info.rectTransform.At(0, 0, 30, 24, 860, 110);
            var it = Ui.Label(info.transform, team.Name.ToUpperInvariant(), 34, Color.white, TextAnchor.UpperLeft, Font.Display);
            it.rectTransform.At(0, 1, 22, -10, 560, 40);
            var meta = Ui.Label(info.transform, "Home: " + team.Ground + "  •  Titles: " + team.Titles + "  •  " + (team.PitchSpin > 1.06 ? "Spin-friendly pitch" : team.PitchPace > 1.05 ? "Pace and bounce" : "Batting paradise") + "  •  Boundary " + team.BoundarySquare + "-" + team.BoundaryStraight + " m", 17, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
            meta.rectTransform.At(0, 1, 22, -54, 820, 22);
            var ratings = Ui.Label(info.transform, "OVR <color=#FFC94A>" + sheet.Overall + "</color>    BAT " + sheet.BattingStrength + "    BOWL " + sheet.BowlingStrength + "    Star: " + sheet.XI.OrderByDescending(p => p.Overall).First().Name, 18, Color.white, TextAnchor.UpperLeft, Font.Semi);
            ratings.rectTransform.At(0, 1, 22, -78, 820, 24);
            var go = Ui.Btn(page, first ? "LEAD " + team.Nick.ToUpperInvariant() : "CONFIRM", BtnStyle.Primary, () =>
            {
                save.Fav = team.Id;
                SaveSystem.Save();
                Audio.Play(Sfx.Fanfare, 0.7f);
                if (GameApp.I.Stadium != null && GameApp.I.Stadium.Venue.Id != team.Id && GameApp.I.Match == null)
                {
                    GameApp.I.StartCoroutine(Rebuild(first));
                }
                else if (first) HowToScreen.Show(true);
                else MainMenu.Show();
            }, 30, Icon.Check);
            go.Rt().At(1, 0, -30, 30, 330, 92);
        }

        static IEnumerator Rebuild(bool first)
        {
            Nav.Clear();
            var fav = Teams.ById(SaveSystem.Data.Fav);
            var away = Teams.All[(Teams.IndexOf(fav.Id) + 3) % Teams.All.Count];
            yield return GameApp.I.BuildStadium(fav, fav, away, SaveSystem.Data.Settings.MatchTime == 2, true, "Welcome to " + fav.Ground);
            GameApp.I.EnsureMenuScene();
            if (first) HowToScreen.Show(true);
            else MainMenu.Show();
        }
    }

    public static class HowToScreen
    {
        public static void Show(bool onboarding = false)
        {
            var page = ScreenKit.Page("HowTo", "HOW TO PLAY", onboarding ? "A quick guide before your first game" : "Controls and tips", MainMenu.Show, 0.7f);
            var items = new (string Title, Icon Icon, Color C, string Body)[]
            {
                ("BATTING", Icon.Bat, Pal.Teal, "STICK + BUTTONS (default)\n• Aim with the stick: up = straight, sideways = off / leg side. Leave it centred and the batter picks the natural shot.\n• Press GROUND, LOFT or DEFEND as the ball reaches the bat. The ring on the pitch shows where it will bounce.\n\nSWIPE (Settings)\n• Swipe in the shot direction as the ball arrives; a long or fast swipe goes in the air; tap to defend."),
                ("BOWLING", Icon.Ball, Pal.Accent, "• Choose a delivery: stock, swing, yorker, bouncer or slower ball for pacers; turn, flight, arm ball or wrong'un for spinners.\n• Drag on the pitch to set line and length.\n• Press BOWL, then tap when the meter is in the green zone for accuracy.\n• AUTO bowls a smart delivery for you. FIELD changes the field setting."),
                ("FIELDING & TIPS", Icon.Users, Pal.Gold, "• When the ball goes up and your side is fielding, TAP TO CATCH as the ring turns green.\n• Late timing sends the ball square; early timing drags it to the leg side.\n• Good-length balls are hard to hit for six - pick the full and short ones.\n• Tap the screen during a play to skip ahead. Pause to simulate an over, an innings or the match."),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var (title, icon, c, body) = items[i];
                var card = Ui.Card(page, title, c.A(0.6f));
                card.rectTransform.At(0.5f, 0.5f, (i - 1) * 410, -30, 396, 520);
                var ic = Ui.IconImg(card.transform, icon, c, 44);
                ic.rectTransform.At(0, 1, 22, -20, 44, 44);
                var t = Ui.Label(card.transform, title, 32, c, TextAnchor.MiddleLeft, Font.Display);
                t.rectTransform.At(0, 1, 76, -22, 300, 40);
                var p = Ui.Para(card.transform, body, 17, Pal.Text);
                p.rectTransform.Fill(22, 82, 20, 18);
                Enter.Add(card, i * 0.06f);
            }
            if (onboarding)
            {
                var go = Ui.Btn(page, "TRY THE NETS", BtnStyle.Primary, () =>
                {
                    SaveSystem.Data.TutorialDone = true;
                    SaveSystem.MarkDirty();
                    GameApp.I.StartMatch(Matches.Nets(true));
                }, 26, Icon.Target);
                go.Rt().At(1, 0, -30, 24, 300, 64);
                var skip = Ui.Btn(page, "MAIN MENU", BtnStyle.Ghost, () =>
                {
                    SaveSystem.Data.TutorialDone = true;
                    SaveSystem.MarkDirty();
                    MainMenu.Show();
                }, 22);
                skip.Rt().At(1, 0, -350, 24, 220, 64);
            }
        }
    }
}
