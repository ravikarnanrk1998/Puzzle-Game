using System;
using UnityEngine;
using UnityEngine.UI;

namespace FRF
{
    /// <summary>Front-end screens, built in code over the live <see cref="MenuStage"/>.</summary>
    public static class Menus
    {
        public enum Page { Main, Career, Quick, Garage, Settings, HowTo, Loading }

        public static Page Current { get; private set; }
        static RectTransform Root => GameApp.I.ScreenRoot;

        static RectTransform Begin(Page s, bool live, float stageShift, float stageDist = 5.6f)
        {
            Current = s;
            GameApp.I.ClearScreens();
            GameApp.I.EnsureStage();
            GameApp.I.SetStageLive(live);
            GameApp.I.Stage?.Frame(stageShift, stageDist);
            Sfx.I?.Music(true, 0.32f);
            var root = Ui.Node(Root, s.ToString()).Fill();

            // Deep-space background overlay
            var shade = new Color(0.01f, 0.02f, 0.05f, 1);
            if (s == Page.Main || s == Page.Garage)
            {
                var fade = Ui.Img(root, Icons.FadeRight, shade.WithA(0.97f), "Fade");
                fade.preserveAspect = false;
                var fr = fade.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = new Vector2(0.72f, 1);
                fr.offsetMin = new Vector2(-130, -60);
                fr.offsetMax = new Vector2(0, 60);
            }
            else
            {
                var dim = Ui.Block(root, shade.WithA(0.76f), "Dim");
                dim.rectTransform.Fill(-300, -60, -300, -60);
            }

            // Top atmospheric fade (slightly taller for depth)
            var top = Ui.Img(root, Icons.FadeUp, shade.WithA(0.88f), "TopFade");
            top.preserveAspect = false;
            top.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            top.rectTransform.anchorMin = new Vector2(0, 1);
            top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            top.rectTransform.sizeDelta = new Vector2(600, 130);
            top.rectTransform.anchoredPosition = new Vector2(0, -28);
            return root;
        }

        public static void Back()
        {
            switch (Current)
            {
                case Page.Main:    ConfirmQuit(); break;
                case Page.Loading: break;
                default:           Main(); break;
            }
        }

        // ------------------------------------------------------------ shared pieces

        static void Header(RectTransform root, string title, string sub = null)
        {
            // Back button — ghost style
            var back = Ui.Ghost(root, "< BACK", Pal.Dim, Main, 19);
            ((RectTransform)back.transform).At(0, 1, 12, -10, 114, 40);

            // Neon accent stripe
            var stripe = Ui.Img(root, Ui.Slant, Pal.Orange, "Stripe");
            stripe.type = Image.Type.Sliced;
            stripe.preserveAspect = false;
            stripe.rectTransform.At(0, 1, 138, -14, 28, 36);

            // Neon glow behind stripe
            var stripeGlow = Ui.Img(root, Ui.NeonGlow, Pal.Orange.WithA(0.30f), "StripeGlow");
            stripeGlow.preserveAspect = false;
            stripeGlow.rectTransform.At(0, 1, 130, -18, 50, 50);

            var t = Ui.Label(root, title, 42, Color.white, TextAnchor.MiddleLeft, true, "Title");
            t.rectTransform.At(0, 1, 174, -8, 500, 48);
            Ui.Shadow(t, 2.5f, 0.8f);

            if (sub != null)
            {
                var s = Ui.Label(root, sub, 15, Pal.Dim, TextAnchor.MiddleLeft, false, "Sub");
                s.rectTransform.At(0, 1, 176, -52, 600, 18);
            }
            CashChip(root);
        }

        static void CashChip(RectTransform root)
        {
            // Yellow-bordered cash chip with neon glow
            var glowImg = Ui.Img(root, Ui.NeonGlow, Pal.Yellow.WithA(0.14f), "CashGlow");
            glowImg.preserveAspect = false;
            glowImg.rectTransform.At(1, 1, -10, -8, 190, 58);

            var chip = Ui.Panel(root, Pal.Card, 12, "Cash", Pal.Yellow.WithA(0.70f));
            chip.rectTransform.At(1, 1, -14, -12, 172, 40);
            var t = Ui.Label(chip.transform, Ui.Money(Profile.Cash), 26, Pal.Yellow, TextAnchor.MiddleCenter, true, "Value");
            t.rectTransform.Fill();
            Ui.Shadow(t, 2, 0.7f);
        }

        static Image Card(Transform parent, float x, float y, float w, float h, float ax = 0, float ay = 1, Color? border = null)
        {
            var c = Ui.Panel(parent, Pal.Card, 16, "Card", border ?? Pal.StrokeHi);
            c.rectTransform.At(ax, ay, x, y, w, h);
            return c;
        }

        // ------------------------------------------------------------ main menu

        public static void Main()
        {
            var root = Begin(Page.Main, true, 1.45f);

            // Logo area
            var logo = Ui.Node(root, "Logo");
            logo.At(0, 1, 24, -12, 480, 160);

            // Large orange parallelogram behind logo text
            var stripe = Ui.Img(logo, Ui.Slant, Pal.Orange, "Stripe");
            stripe.type = Image.Type.Sliced;
            stripe.preserveAspect = false;
            stripe.rectTransform.At(0, 1, -6, -16, 450, 70);

            // Neon orange glow behind stripe
            var stripeGlow = Ui.Img(logo, Ui.NeonGlow, Pal.Orange.WithA(0.28f), "StripeGlow");
            stripeGlow.preserveAspect = false;
            stripeGlow.rectTransform.At(0, 1, -20, -30, 480, 110);

            var t1 = Ui.Label(logo, "FREERIDE", 76, Color.white, TextAnchor.UpperLeft, true, "Free");
            t1.rectTransform.At(0, 1, 14, 6, 460, 92);
            Ui.Shadow(t1, 3, 0.55f);

            var t2 = Ui.Label(logo, "FURY", 96, Pal.Yellow, TextAnchor.UpperLeft, true, "Fury");
            t2.rectTransform.At(0, 1, 10, -52, 460, 108);
            Ui.Outline(t2, new Color(0.25f, 0.08f, 0, 0.95f), 3f);

            // Yellow neon glow behind FURY
            var furyGlow = Ui.Img(logo, Ui.NeonGlow, Pal.Yellow.WithA(0.20f), "FuryGlow");
            furyGlow.preserveAspect = false;
            furyGlow.rectTransform.At(0, 1, -10, -60, 300, 120);

            // Tagline
            var tag = Ui.Label(root, "DOWNHILL  ·  TRICKS  ·  TAKEDOWNS", 17, Pal.Dim, TextAnchor.UpperLeft, false, "Tag");
            tag.rectTransform.At(0, 1, 32, -160, 440, 20);
            Ui.Shadow(tag, 1.5f, 0.9f);

            // Navigation buttons
            float y = -192;

            var career = Ui.Primary(root, "CAREER", Career, 34, CareerSubtitle());
            ((RectTransform)career.transform).At(0, 1, 28, y, 328, 72);
            y -= 84;

            var quick = Ui.Button(root, "QUICK RACE", Pal.CyanDeep, new Color(0, 0.25f, 0.42f), QuickRace, 27);
            ((RectTransform)quick.transform).At(0, 1, 28, y, 328, 54);
            y -= 62;

            var garage = Ui.Button(root, "GARAGE", new Color(0.22f, 0.26f, 0.36f), new Color(0.08f, 0.10f, 0.16f), Garage, 23);
            ((RectTransform)garage.transform).At(0, 1, 28, y, 158, 46);

            var settings = Ui.Button(root, "SETTINGS", new Color(0.22f, 0.26f, 0.36f), new Color(0.08f, 0.10f, 0.16f), SettingsScreen, 23);
            ((RectTransform)settings.transform).At(0, 1, 196, y, 160, 46);
            y -= 54;

            var how = Ui.Ghost(root, "HOW TO PLAY", Pal.Dim, HowTo, 19);
            ((RectTransform)how.transform).At(0, 1, 28, y, 328, 38);

            CashChip(root);

            // Rider info chip (bottom-right)
            var r  = Profile.Rider;
            var b  = Profile.Bike;

            var infoGlow = Ui.Img(root, Ui.NeonGlow, Pal.Cyan.WithA(0.10f), "RiderGlow");
            infoGlow.preserveAspect = false;
            infoGlow.rectTransform.At(1, 0, -18, 12, 320, 80);

            var info = Ui.Panel(root, Pal.Card, 14, "RiderTag", Pal.Cyan.WithA(0.45f));
            info.rectTransform.At(1, 0, -22, 16, 306, 60);
            var n = Ui.Label(info.transform, r.Name + "  #" + r.Number, 22, Color.white, TextAnchor.UpperLeft, true, "Name");
            n.rectTransform.Fill(14, 4, 8, 0);
            Ui.Shadow(n, 1.5f, 0.6f);
            var bk = Ui.Label(info.transform, b.Name + "  ·  " + b.Kind.ToUpperInvariant(), 15, Pal.Dim, TextAnchor.LowerLeft, false, "Bike");
            bk.rectTransform.Fill(14, 0, 8, 8);

            // Medal count row
            var medals = Ui.Label(root, "<color=#FFCC44>" + Profile.MedalCount(3) + " GOLD</color>   <color=#D0DCF0>" + Profile.MedalCount(2) + " SILVER</color>   <color=#E08A48>" + Profile.MedalCount(1) + " BRONZE</color>",
                15, Pal.Text, TextAnchor.MiddleRight, false, "Medals");
            medals.rectTransform.At(1, 1, -16, -62, 370, 20);
            Ui.Shadow(medals, 1.5f, 0.9f);

            // Version
            var ver = Ui.Label(root, "v" + Application.version, 11, Pal.Mute, TextAnchor.LowerLeft, false, "Version");
            ver.rectTransform.At(0, 0, 10, 4, 80, 14);
        }

        static string CareerSubtitle()
        {
            int next = 0;
            for (int i = 0; i < Catalog.Career.Length; i++)
                if (Profile.Unlocked(i)) next = i;
            var e = Catalog.Career[next];
            return "NEXT: EVENT " + (next + 1) + " · " + e.Track.Name;
        }

        static void ConfirmQuit()
        {
            var dim = Ui.Block(Root, new Color(0, 0, 0, 0.72f), "Quit");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();

            var card = Card(dim.transform, 0, 0, 430, 178, 0.5f, 0.5f, Pal.Orange.WithA(0.75f));
            var t = Ui.Label(card.transform, "LEAVE THE MOUNTAIN?", 34, Color.white, TextAnchor.UpperCenter, true, "T");
            t.rectTransform.Fill(0, 14, 0, 0);
            Ui.Shadow(t, 2, 0.7f);

            var no = Ui.Ghost(card.transform, "STAY", Pal.Text, () => UnityEngine.Object.Destroy(dim.gameObject), 22);
            ((RectTransform)no.transform).At(0.5f, 0, -94, 20, 158, 48);
            var yes = Ui.Primary(card.transform, "QUIT", Application.Quit, 22);
            ((RectTransform)yes.transform).At(0.5f, 0, 94, 20, 158, 48);
        }

        // ------------------------------------------------------------ career

        public static void Career()
        {
            var root = Begin(Page.Career, false, 3.2f, 7.5f);
            Header(root, "CAREER", "Medal in each event to unlock the next. 15 events across 5 mountains.");
            var view = Ui.ScrollList(root, out var content, true);
            view.Fill(12, 80, 12, 12);
            const float w = 220, h = 326, gap = 14;
            int focus = 0;
            for (int i = 0; i < Catalog.Career.Length; i++)
            {
                var e = Catalog.Career[i];
                bool open = Profile.Unlocked(i);
                if (open) focus = i;
                int medal = Profile.Medal(i);

                // Glow behind open/medal cards
                Color borderCol = !open ? Pal.Stroke
                    : medal > 0 ? Pal.Medal(medal).WithA(0.85f)
                    : Pal.Orange.WithA(0.75f);

                if (open)
                {
                    var cardGlow = Ui.Img(content, Ui.NeonGlow, borderCol.WithA(0.15f), "CardGlow" + i);
                    cardGlow.preserveAspect = false;
                    var cg = cardGlow.rectTransform;
                    cg.anchorMin = cg.anchorMax = new Vector2(0, 0.5f);
                    cg.pivot = new Vector2(0, 0.5f);
                    cg.anchoredPosition = new Vector2(i * (w + gap) - 10, 0);
                    cg.sizeDelta = new Vector2(w + 20, h + 20);
                }

                var card = Ui.Panel(content, open ? Pal.Card : new Color(0.04f, 0.05f, 0.08f, 0.92f), 16, "Event" + i, borderCol);
                var rt = card.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(i * (w + gap), 0);
                rt.sizeDelta = new Vector2(w, h);

                var th = e.Track.Theme;
                // Scenery thumbnail band
                var band = Ui.Node(rt, "Band");
                band.At(0.5f, 1, 0, -6, w - 12, 88);
                Ui.Scenery(band, th, w - 12, 88);

                // Event number over scenery
                var num = Ui.Label(band, (i + 1).ToString(), 46, Color.white, TextAnchor.UpperLeft, true, "Num");
                num.rectTransform.Fill(10, -4, 0, 0);
                Ui.Shadow(num, 2.5f, 0.8f);

                var trackName = Ui.Label(rt, e.Track.Name, 21, Pal.Text, TextAnchor.UpperLeft, true, "Track");
                trackName.rectTransform.Fill(14, 100, 8, 0);

                var kind = Ui.Label(rt, e.Title, 26, e.Kind == EventKind.Final ? Pal.Yellow : Pal.Orange, TextAnchor.UpperLeft, true, "Kind");
                kind.rectTransform.Fill(14, 124, 8, 0);

                var goal = Ui.Para(rt, e.Goal, 14.5f, Pal.Dim);
                goal.rectTransform.Fill(14, 158, 12, 0);

                // Medal dots
                for (int m = 3; m >= 1; m--)
                {
                    float yy = -204 - (3 - m) * 23;
                    bool earned = medal >= m;
                    var dot = Ui.Img(rt, Ui.Circle, Pal.Medal(m).WithA(earned ? 1 : 0.22f), "M" + m);
                    dot.rectTransform.At(0, 1, 14, yy, 18, 18);
                    if (earned)
                    {
                        var dg = Ui.Img(rt, Ui.NeonGlow, Pal.Medal(m).WithA(0.40f), "MG" + m);
                        dg.preserveAspect = false;
                        dg.rectTransform.At(0, 1, 8, yy - 6, 30, 30);
                    }
                    var rule = Ui.Label(rt, e.MedalRule(m), 14.5f, earned ? Pal.Text : Pal.Mute, TextAnchor.MiddleLeft, false, "Rule" + m);
                    rule.rectTransform.At(0, 1, 40, yy + 1, 174, 18);
                }

                if (open)
                {
                    int idx = i;
                    var go = Ui.Primary(rt, medal > 0 ? "RIDE AGAIN" : "RIDE", () => GameApp.I.StartEvent(Catalog.Career[idx]), 22);
                    ((RectTransform)go.transform).At(0.5f, 0, 0, 12, w - 28, 46);
                }
                else
                {
                    var lockT = Ui.Label(rt, "LOCKED", 22, Pal.Mute, TextAnchor.LowerCenter, true, "Lock");
                    lockT.rectTransform.Fill(0, 0, 0, 22);
                }
            }
            Ui.SetContentWidth(content, Catalog.Career.Length * (w + gap));
            var sr = view.GetComponent<ScrollRect>();
            float total = Catalog.Career.Length * (w + gap), viewW = 960 - 24;
            sr.horizontalNormalizedPosition = Mathf.Clamp01((focus * (w + gap) - viewW * 0.4f) / Mathf.Max(1, total - viewW));
        }

        // ------------------------------------------------------------ quick race

        static int _qTrack = -1, _qRivals = 1;

        public static void QuickRace()
        {
            var root = Begin(Page.Quick, false, 3.2f, 7.5f);
            Header(root, "QUICK RACE", "Any mountain, any time. Prize money still counts.");
            if (_qTrack < 0) _qTrack = PlayerPrefs.GetInt("frf.qtrack", 0);
            const float w = 178, h = 172;
            var cards = Ui.Node(root, "Tracks");
            cards.At(0.5f, 1, 0, -86, 5 * (w + 10), h);
            for (int i = 0; i < Catalog.Tracks.Length; i++)
            {
                var t = Catalog.Tracks[i];
                int idx = i;
                bool sel = i == _qTrack;

                if (sel)
                {
                    var cg = Ui.Img(cards, Ui.NeonGlow, Pal.Orange.WithA(0.20f), "SelGlow" + i);
                    cg.preserveAspect = false;
                    cg.rectTransform.At(0, 1, i * (w + 10) - 8, 8, w + 16, h + 16);
                }

                var c = Ui.Panel(cards, sel ? Pal.CardHi : Pal.Card, 16, "Track" + i, sel ? Pal.Orange : Pal.Stroke);
                c.rectTransform.At(0, 1, i * (w + 10), 0, w, h);
                var band = Ui.Node(c.transform, "Band");
                band.At(0.5f, 1, 0, -6, w - 12, 66);
                Ui.Scenery(band, t.Theme, w - 12, 66);
                var name = Ui.Label(c.transform, t.Name, 20, sel ? Color.white : Pal.Text, TextAnchor.UpperLeft, true, "Name");
                name.rectTransform.Fill(10, 76, 6, 0);
                var tag = Ui.Para(c.transform, t.Tagline, 13, Pal.Dim);
                tag.rectTransform.Fill(10, 100, 6, 0);
                float best = Profile.BestTime(t.Id);
                var bt = Ui.Label(c.transform, "BEST " + Ui.Time(best), 15, best > 0 ? Pal.Yellow : Pal.Mute, TextAnchor.LowerLeft, true, "Best");
                bt.rectTransform.Fill(10, 0, 6, 8);
                var len = Ui.Label(c.transform, (t.Length / 1000f).ToString("0.0") + " KM", 15, Pal.Dim, TextAnchor.LowerRight, true, "Len");
                len.rectTransform.Fill(0, 0, 10, 8);
                Ui.Tap(c.rectTransform, () =>
                {
                    _qTrack = idx;
                    PlayerPrefs.SetInt("frf.qtrack", idx);
                    QuickRace();
                });
            }

            float y = -278;
            void OptionRow(string label, string[] opts, int sel, Action<int> pick)
            {
                var l = Ui.Label(root, label, 18, Pal.Dim, TextAnchor.MiddleLeft, true, "L");
                l.rectTransform.At(0.5f, 1, -338, y, 155, 40);
                var segs = Ui.Node(root, "Seg");
                segs.At(0.5f, 1, -46, y, 424, 40);
                Ui.Segments(segs, opts, sel, pick);
                y -= 52;
            }
            OptionRow("DIFFICULTY", new[] { "EASY", "NORMAL", "HARD" }, (int)Settings.Difficulty, i =>
            {
                Settings.Difficulty = (Difficulty)i;
                Settings.Save();
            });
            OptionRow("RIVALS", new[] { "3", "5", "7" }, _qRivals, i => _qRivals = i);

            var start = Ui.Primary(root, "START RACE", () =>
            {
                int rivals = _qRivals == 0 ? 3 : _qRivals == 1 ? 5 : 7;
                GameApp.I.StartQuick(Catalog.Tracks[_qTrack], rivals, Settings.Difficulty);
            }, 30);
            ((RectTransform)start.transform).At(0.5f, 0, 296, 18, 268, 64);
        }

        // ------------------------------------------------------------ garage

        static int _gTab, _gRider = -1, _gBike = -1;

        public static void Garage()
        {
            var root = Begin(Page.Garage, true, 1.25f, 5.0f);
            Header(root, "GARAGE");
            var playable = Array.FindAll(Catalog.Riders, r => r.Playable);
            if (_gRider < 0) _gRider = Mathf.Max(0, Array.FindIndex(playable, r => r.Id == Profile.RiderId));
            if (_gBike  < 0) _gBike  = Mathf.Max(0, Array.FindIndex(Catalog.Bikes, b => b.Id == Profile.BikeId));

            var tabs = Ui.Node(root, "Tabs");
            tabs.At(0, 1, 16, -68, 424, 42);
            Ui.Segments(tabs, new[] { "RIDER", "BIKE", "PAINT" }, _gTab, i =>
            {
                _gTab = i;
                Garage();
            }, 20);

            var panel = Card(root, 16, -118, 424, 310);
            var p = panel.rectTransform;
            var stage = GameApp.I.Stage;

            if (_gTab == 0)
            {
                var r = playable[_gRider];
                stage?.Refresh(r, Profile.Bike, Profile.PaintColor);
                Cycler(p, r.Name, "\"" + r.Nick + "\"  #" + r.Number, d =>
                {
                    _gRider = (_gRider + d + playable.Length) % playable.Length;
                    Garage();
                });
                var bio = Ui.Para(p, r.Bio, 17, Pal.Text, TextAnchor.UpperCenter);
                bio.rectTransform.Fill(20, 86, 20, 0);

                string perk = r.SpeedBonus > 0 ? "+ SPEED" : r.HandlingBonus > 0 ? "+ HANDLING" : r.AccelBonus > 0 ? "+ ACCELERATION" : r.TrickBonus > 1 ? "+25% TRICK POINTS" : "";
                if (r.StrengthBonus > 0) perk += (perk.Length > 0 ? "   " : "") + "+ STRENGTH";
                if (perk.Length > 0)
                {
                    var pk = Ui.Label(p, perk, 20, Pal.Cyan, TextAnchor.UpperCenter, true, "Perk");
                    pk.rectTransform.Fill(10, 142, 10, 0);
                    Ui.Shadow(pk, 2, 0.6f);
                }

                bool current = r.Id == Profile.RiderId;
                var btn = current
                    ? Ui.Ghost(p, "✓  SELECTED", Pal.Green, null, 21)
                    : Ui.Primary(p, "RIDE AS " + r.Nick, () =>
                    {
                        Profile.RiderId = r.Id;
                        PlayerPrefs.Save();
                        Garage();
                    }, 22);
                ((RectTransform)btn.transform).At(0.5f, 0, 0, 14, 308, 52);
            }
            else if (_gTab == 1)
            {
                var b = Catalog.Bikes[_gBike];
                bool owned = Profile.Owns(b);
                stage?.Refresh(Profile.Rider, b, b.Id == Profile.BikeId ? Profile.PaintColor : Color.clear);
                Cycler(p, b.Name, b.Kind.ToUpperInvariant() + (owned ? "  ·  OWNED" : "  ·  " + Ui.Money(b.Price)), d =>
                {
                    _gBike = (_gBike + d + Catalog.Bikes.Length) % Catalog.Bikes.Length;
                    Garage();
                });
                var me = Profile.Rider;
                Ui.Pips(p, "SPEED",    b.Speed,    me.SpeedBonus,    Pal.Orange, 22, -96,  380);
                Ui.Pips(p, "ACCEL",    b.Accel,    me.AccelBonus,    Pal.Orange, 22, -122, 380);
                Ui.Pips(p, "HANDLING", b.Handling, me.HandlingBonus, Pal.Orange, 22, -148, 380);
                Ui.Pips(p, "STRENGTH", b.Strength, me.StrengthBonus, Pal.Orange, 22, -174, 380);
                var note = Ui.Label(p, "cyan pips = your rider's bonus", 13, Pal.Mute, TextAnchor.MiddleCenter, false, "Note");
                note.rectTransform.At(0.5f, 1, 0, -200, 310, 16);

                Button btn;
                if (b.Id == Profile.BikeId)
                    btn = Ui.Ghost(p, "✓  SELECTED", Pal.Green, null, 21);
                else if (owned)
                    btn = Ui.Primary(p, "RIDE THIS BIKE", () =>
                    {
                        Profile.BikeId = b.Id;
                        Profile.Paint = 0;
                        PlayerPrefs.Save();
                        Garage();
                    }, 22);
                else
                {
                    bool afford = Profile.Cash >= b.Price;
                    btn = Ui.Primary(p, "BUY " + Ui.Money(b.Price), () =>
                    {
                        if (!Profile.Buy(b)) return;
                        Profile.BikeId = b.Id;
                        Profile.Paint = 0;
                        PlayerPrefs.Save();
                        Sfx.I?.Finish();
                        Garage();
                    }, 22);
                    btn.interactable = afford;
                    if (!afford)
                    {
                        var need = Ui.Label(p, "NEED " + Ui.Money(b.Price - Profile.Cash) + " MORE", 14, Pal.Red, TextAnchor.MiddleCenter, true, "Need");
                        need.rectTransform.At(0.5f, 0, 0, 72, 310, 18);
                    }
                }
                ((RectTransform)btn.transform).At(0.5f, 0, 0, 14, 308, 52);
            }
            else
            {
                stage?.Refresh(Profile.Rider, Profile.Bike, Profile.PaintColor);
                var t = Ui.Label(p, Profile.Bike.Name + " FRAME PAINT", 24, Pal.Text, TextAnchor.UpperCenter, true, "T");
                t.rectTransform.Fill(0, 14, 0, 0);
                int cols = 5;
                for (int i = 0; i < Catalog.Paints.Length; i++)
                {
                    int idx = i;
                    var col = i == 0 ? Profile.Bike.Frame : Catalog.Paints[i];
                    bool sel = Profile.Paint == i;
                    var sw = Ui.Panel(p, col, 14, "Paint" + i, sel ? Pal.Yellow : Color.white.WithA(0.20f));
                    sw.rectTransform.At(0, 1, 32 + (i % cols) * 72, -64 - (i / cols) * 76, 64, 64);
                    if (sel)
                    {
                        var sg = Ui.Img(p, Ui.NeonGlow, Pal.Yellow.WithA(0.30f), "SelGlow" + i);
                        sg.preserveAspect = false;
                        sg.rectTransform.At(0, 1, 26 + (i % cols) * 72, -58 - (i / cols) * 76, 76, 76);
                    }
                    if (i == 0)
                    {
                        var f = Ui.Label(sw.transform, "STOCK", 13, col.Luma() > 0.6f ? Color.black : Color.white, TextAnchor.MiddleCenter, true, "Stock");
                        f.rectTransform.Fill();
                    }
                    var bt = sw.gameObject.AddComponent<Button>();
                    bt.onClick.AddListener(() =>
                    {
                        Sfx.I?.Click();
                        Profile.Paint = idx;
                        PlayerPrefs.Save();
                        Garage();
                    });
                    sw.gameObject.AddComponent<PressScale>();
                }
                var free = Ui.Label(p, "Paint jobs are free.", 15, Pal.Dim, TextAnchor.LowerCenter, false, "Free");
                free.rectTransform.Fill(0, 0, 0, 22);
            }
        }

        static void Cycler(RectTransform p, string title, string sub, Action<int> step)
        {
            var prev = Ui.Ghost(p, "<", Pal.Dim, () => step(-1), 30);
            ((RectTransform)prev.transform).At(0, 1, 12, -14, 54, 58);
            var next = Ui.Ghost(p, ">", Pal.Dim, () => step(1), 30);
            ((RectTransform)next.transform).At(1, 1, -12, -14, 54, 58);
            var t = Ui.Label(p, title, 32, Color.white, TextAnchor.UpperCenter, true, "Name");
            t.rectTransform.Fill(72, 10, 72, 0);
            Ui.Shadow(t, 2, 0.7f);
            var s = Ui.Label(p, sub, 16, Pal.Dim, TextAnchor.UpperCenter, false, "Sub");
            s.rectTransform.Fill(72, 50, 72, 0);
        }

        // ------------------------------------------------------------ settings

        public static void SettingsScreen()
        {
            var root = Begin(Page.Settings, false, 3.2f, 7.5f);
            Header(root, "SETTINGS");
            var card = Card(root, 0, -72, 780, 360, 0.5f, 1);
            var c = card.rectTransform;
            float y = -18;
            void Row(string label, string[] opts, int sel, Action<int> pick, string hint = null)
            {
                var l = Ui.Label(c, label, 19, Pal.Text, TextAnchor.MiddleLeft, true, "L");
                l.rectTransform.At(0, 1, 26, y, 224, 34);
                if (hint != null)
                {
                    var h = Ui.Label(c, hint, 12, Pal.Mute, TextAnchor.MiddleLeft, false, "H");
                    h.rectTransform.At(0, 1, 26, y - 22, 244, 14);
                }
                var segs = Ui.Node(c, "Seg");
                segs.At(1, 1, -26, y, 460, 34);
                Ui.Segments(segs, opts, sel, pick, 17);
                y -= 42;
            }
            Row("STEERING",        new[] { "BUTTONS", "TILT" },          (int)Settings.Steer,                 i => Settings.Steer = (SteerMode)i, "left-thumb arrows or tilt the phone");
            Row("TILT SENSITIVITY",new[] { "LOW", "MEDIUM", "HIGH" },    Settings.TiltSensitivity,            i => Settings.TiltSensitivity = i);
            Row("GRAPHICS",        new[] { "LOW", "MEDIUM", "HIGH" },    (int)Settings.Graphics,              i => { Settings.Graphics = (GraphicsQuality)i; SceneLook.ApplyPipeline(); }, "LOW keeps older phones cool");
            Row("FRAME RATE",      new[] { "30 SAVER", "60 SMOOTH" },    Settings.Smooth60 ? 1 : 0,          i => Settings.Smooth60 = i == 1);
            Row("CAMERA",          new[] { "NEAR", "FAR" },              Settings.FarCamera ? 1 : 0,         i => Settings.FarCamera = i == 1);
            Row("SOUND",           new[] { "ON", "OFF" },                Settings.Sound ? 0 : 1,             i => Settings.Sound = i == 0);
            Row("MUSIC",           new[] { "ON", "OFF" },                Settings.Music ? 0 : 1,             i => { Settings.Music = i == 0; Sfx.I?.Music(Settings.Music, 0.32f); });
            Row("VIBRATION",       new[] { "ON", "OFF" },                Settings.Vibration ? 0 : 1,         i => Settings.Vibration = i == 0);

            var reset = Ui.Ghost(root, "RESET CAREER", Pal.Red, ConfirmReset, 18);
            ((RectTransform)reset.transform).At(1, 0, -16, 12, 178, 38);
        }

        static void ConfirmReset()
        {
            var dim = Ui.Block(Root, new Color(0, 0, 0, 0.72f), "Reset");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var card = Card(dim.transform, 0, 0, 470, 196, 0.5f, 0.5f, Pal.Red.WithA(0.75f));
            var t = Ui.Label(card.transform, "RESET CAREER?", 34, Color.white, TextAnchor.UpperCenter, true, "T");
            t.rectTransform.Fill(0, 12, 0, 0);
            Ui.Shadow(t, 2, 0.7f);
            var s = Ui.Para(card.transform, "Medals, money and bikes go back to the start. Settings stay.", 16, Pal.Dim, TextAnchor.UpperCenter);
            s.rectTransform.Fill(24, 56, 24, 0);
            var no = Ui.Ghost(card.transform, "CANCEL", Pal.Text, () => UnityEngine.Object.Destroy(dim.gameObject), 22);
            ((RectTransform)no.transform).At(0.5f, 0, -104, 20, 178, 48);
            var yes = Ui.Button(card.transform, "RESET", Pal.Red, Pal.Red.Darken(0.4f), () =>
            {
                Profile.Reset();
                _gRider = _gBike = -1;
                GameApp.I.RebuildStage();
                Main();
            }, 22);
            ((RectTransform)yes.transform).At(0.5f, 0, 104, 20, 178, 48);
        }

        // ------------------------------------------------------------ how to play

        public static void HowTo()
        {
            var root = Begin(Page.HowTo, false, 3.2f, 7.5f);
            Header(root, "HOW TO PLAY");
            (string T, string B, Color C)[] tips =
            {
                ("RIDE",   "You pedal automatically and gravity does the rest. Steer with the left-thumb arrows (or tilt). Too fast into a bend and you drift wide, so tap BRAKE before tight corners and use the banked berms.", Pal.Orange),
                ("JUMP",   "Hold JUMP to crouch and release to hop logs. Hold it into a kicker and you POP off the lip for big air. Land straight or you will crash.", Pal.Yellow),
                ("TRICKS", "In the air the buttons become tricks: SUPERMAN, NO HANDS and CAN-CAN. Hold one for points, then let go before you land. Steer in the air to spin 360s. Mix them for a combo multiplier.", Pal.Violet),
                ("FIGHT",  "Ride up beside a rival and hit PUNCH. Knock their balance bar to zero for a TAKEDOWN. They will fight back, so watch your own balance bar.", Pal.Red),
                ("BOOST",  "Tricks, hits and the glowing orbs fill your ADRENALINE. Each full bar is one BOOST: a burst of speed.", Pal.Cyan),
                ("CAREER", "Win medals to unlock the next event and earn cash for faster bikes in the GARAGE. Each rider has a perk.", Pal.Green),
            };
            for (int i = 0; i < tips.Length; i++)
            {
                int col = i % 3, row = i / 3;
                Color tipColor = tips[i].C;

                // Neon glow behind each card
                var glow = Ui.Img(root, Ui.NeonGlow, tipColor.WithA(0.12f), "TipGlow" + i);
                glow.preserveAspect = false;
                glow.rectTransform.At(0, 1, 10 + col * 322, -60 - row * 184, 318, 192);

                var card = Card(root, 16 + col * 322, -72 - row * 184, 306, 174, 0, 1, tipColor.WithA(0.70f));
                var t = Ui.Label(card.transform, tips[i].T, 27, tipColor, TextAnchor.UpperLeft, true, "T");
                t.rectTransform.Fill(14, 6, 10, 0);
                Ui.Shadow(t, 2, 0.7f);
                var b = Ui.Para(card.transform, tips[i].B, 14.5f, Pal.Text);
                b.rectTransform.Fill(14, 42, 12, 8);
            }
        }

        // ------------------------------------------------------------ loading

        public static void Loading(TrackDef track, string title)
        {
            Current = Page.Loading;
            GameApp.I.ClearScreens();
            var root = Ui.Node(Root, "Loading").Fill();
            var bg = Ui.Block(root.parent, Pal.Bg0, "LoadingBg");
            bg.rectTransform.Fill();
            bg.transform.SetParent(root, true);
            bg.rectTransform.Fill(-400, -400, -400, -400);

            var th = track.Theme;

            // Large radial sky glow
            var skyGlow = Ui.Img(root, Ui.Glow, th.SkyTint.WithA(0.55f), "SkyGlow");
            skyGlow.preserveAspect = false;
            skyGlow.rectTransform.At(0.5f, 0.5f, 0, 50, 960, 480);

            // Neon accent line under the title
            var line = Ui.Block(root, Pal.Orange.WithA(0.80f), "AccentLine");
            line.rectTransform.At(0.5f, 0.5f, 0, 16, 420, 2);

            // Track name
            var t = Ui.Label(root, track.Name, 68, Color.white, TextAnchor.MiddleCenter, true, "Track");
            t.rectTransform.At(0.5f, 0.5f, 0, 58, 900, 82);
            Ui.Shadow(t, 3.5f, 0.7f);

            // Orange neon glow behind the name
            var nameGlow = Ui.Img(root, Ui.NeonGlow, Pal.Orange.WithA(0.18f), "NameGlow");
            nameGlow.preserveAspect = false;
            nameGlow.rectTransform.At(0.5f, 0.5f, 0, 60, 700, 120);

            var s = Ui.Label(root, title + "  ·  " + track.Tagline.ToUpperInvariant(), 20, Pal.Orange, TextAnchor.MiddleCenter, true, "Sub");
            s.rectTransform.At(0.5f, 0.5f, 0, 0, 900, 30);

            string[] hints =
            {
                "Hold JUMP into a kicker for a bigger pop.", "Let go of a trick before you land.", "Brake before tight bends; berms let you carry speed.",
                "A full adrenaline bar is one boost.", "Steer in the air to spin a 360.", "Punch rivals beside you to knock them off.",
            };
            var hint = Ui.Label(root, "TIP: " + hints[UnityEngine.Random.Range(0, hints.Length)], 18, Pal.Dim, TextAnchor.MiddleCenter, false, "Hint");
            hint.rectTransform.At(0.5f, 0.5f, 0, -56, 900, 24);

            var l = Ui.Label(root, "LOADING...", 22, Pal.Orange.WithA(0.75f), TextAnchor.MiddleCenter, true, "Dots");
            l.rectTransform.At(0.5f, 0, 0, 32, 320, 30);
        }
    }
}
