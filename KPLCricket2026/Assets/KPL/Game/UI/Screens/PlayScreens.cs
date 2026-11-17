using System;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    public static class QuickMatchScreen
    {
        static int _user = -1, _opp = -1, _overs = 5, _time;

        public static void Show()
        {
            var save = SaveSystem.Data;
            if (_user < 0)
            {
                _user = Mathf.Max(0, Teams.IndexOf(save.Fav));
                _opp = (_user + 1) % Teams.All.Count;
                _time = save.Settings.MatchTime == 2 ? 1 : 0;
            }
            var page = ScreenKit.Page("Quick", "QUICK MATCH", "Pick the teams, overs and time of play", MainMenu.Show);
            TeamCard(page, "YOUR TEAM", -300, () => _user, i => _user = i, () => _opp, true);
            TeamCard(page, "OPPONENT", 300, () => _opp, i => _opp = i, () => _user, false);
            var vs = Ui.Label(page, "VS", 70, Pal.Accent, TextAnchor.MiddleCenter, Font.Display);
            vs.rectTransform.At(0.5f, 1, 0, -230, 120, 80);
            Ui.Shadow(vs, 3, 0.6f);
            var opts = Ui.Card(page, "Options");
            opts.rectTransform.At(0, 0, 30, 24, 860, 150);
            var o = opts.rectTransform;
            Row(o, "OVERS", 0, new[] { "2", "5", "10", "20" }, Array.IndexOf(new[] { 2, 5, 10, 20 }, _overs), i => _overs = new[] { 2, 5, 10, 20 }[i]);
            Row(o, "TIME", 1, new[] { "DAY", "NIGHT" }, _time, i => _time = i);
            Row(o, "LEVEL", 2, new[] { "EASY", "MEDIUM", "HARD", "PRO" }, save.Settings.Difficulty, i =>
            {
                save.Settings.Difficulty = i;
                SaveSystem.MarkDirty();
            });
            var play = Ui.Btn(page, "PLAY", BtnStyle.Primary, () =>
            {
                var ctx = Matches.Quick(Teams.All[_user], Teams.All[_opp], _overs, _time == 1);
                GameApp.I.StartMatch(ctx);
            }, 44, Icon.Play, "at " + Teams.All[_user].GroundShort);
            play.Rt().At(1, 0, -30, 30, 330, 120);
            play.gameObject.AddComponent<Pulse>().Amount = 0.02f;
        }

        static void Row(RectTransform parent, string label, int row, string[] options, int selected, Action<int> pick)
        {
            var l = Ui.Label(parent, label, 18, Pal.Gold, TextAnchor.MiddleLeft, Font.Bold);
            l.rectTransform.At(0, 1, 22, -14 - row * 44, 100, 36);
            var seg = Ui.Segments(parent, options, Mathf.Max(0, selected), pick, 18);
            seg.At(0, 1, 120, -14 - row * 44, 470, 38);
        }

        static void TeamCard(RectTransform page, string caption, float x, Func<int> get, Action<int> set, Func<int> other, bool user)
        {
            var t = Teams.All[get()];
            var card = Ui.Card(page, caption, Crest.Primary(t).A(0.8f));
            card.rectTransform.At(0.5f, 1, x, -100, 500, 260);
            var c = card.rectTransform;
            var cap = Ui.Label(c, caption, 16, user ? Pal.Gold : Pal.Accent, TextAnchor.MiddleCenter, Font.Bold);
            cap.rectTransform.At(0.5f, 1, 0, -10, 300, 22);
            var cr = Crest.Add(c, t, 110);
            cr.At(0.5f, 1, 0, -34, 110, 110);
            var n = Ui.Label(c, t.Name.ToUpperInvariant(), 32, Color.white, TextAnchor.MiddleCenter, Font.Display);
            n.rectTransform.At(0.5f, 0, 0, 52, 480, 40);
            var sheet = user ? Matches.UserSheet(t) : TeamSheet.Default(t);
            var s = Ui.Label(c, "OVR <color=#FFC94A>" + sheet.Overall + "</color>   BAT " + sheet.BattingStrength + "   BOWL " + sheet.BowlingStrength, 17, Pal.Dim, TextAnchor.MiddleCenter, Font.Semi);
            s.rectTransform.At(0.5f, 0, 0, 22, 480, 22);
            void Step(int d)
            {
                int n2 = Teams.All.Count, i = get();
                do i = (i + d + n2) % n2;
                while (i == other());
                set(i);
                Show();
            }
            var l = Ui.IconBtn(c, Icon.Back, () => Step(-1), 60);
            l.Rt().At(0, 0.5f, 14, 16, 60, 60);
            var r = Ui.IconBtn(c, Icon.Arrow, () => Step(1), 60);
            r.Rt().At(1, 0.5f, -14, 16, 60, 60);
        }
    }

    public static class TournamentScreen
    {
        static int _tab;
        static TourFormat _fmt = TourFormat.Express;
        static int _overs = 5;

        public static void Show()
        {
            var save = SaveSystem.Data;
            if (!save.HasTour) Setup();
            else Hub();
        }

        static void Setup()
        {
            var save = SaveSystem.Data;
            var fav = Teams.ById(save.Fav);
            var page = ScreenKit.Page("TourSetup", "KPL " + League.Year, "Ten franchises. One trophy.", MainMenu.Show);
            var hero = Ui.Card(page, "Hero", Pal.Accent.A(0.7f));
            hero.rectTransform.At(0, 1, 30, -100, 600, 400);
            var trophy = Ui.IconImg(hero.transform, Icon.Trophy, Pal.Gold, 120);
            trophy.rectTransform.At(0, 1, 30, -30, 120, 120);
            var glow = Ui.Img(hero.transform, Sprites.Glow, Pal.Gold.A(0.25f), "Glow");
            glow.rectTransform.At(0, 1, -20, 20, 240, 240);
            glow.transform.SetSiblingIndex(1);
            var t = Ui.Label(hero.transform, "LEAD " + fav.Nick.ToUpperInvariant() + "\nTO GLORY", 46, Color.white, TextAnchor.UpperLeft, Font.Display);
            t.rectTransform.At(0, 1, 170, -30, 420, 120);
            var body = Ui.Para(hero.transform,
                "• League stage: play every franchise. Win = 2 points, ties go to a Super Over.\n• Top four reach the playoffs: Qualifier 1, Eliminator, Qualifier 2 and the Final.\n• Net run rate separates teams on equal points.\n• Play each of your matches or simulate them; all other games are simulated ball by ball.",
                17, Pal.Text);
            body.rectTransform.Fill(30, 170, 30, 20);

            var opts = Ui.Card(page, "Options");
            opts.rectTransform.At(1, 1, -30, -100, 580, 300);
            var o = opts.rectTransform;
            ScreenKit.Section(o, "FORMAT", 22, -20);
            var f = Ui.Segments(o, new[] { "EXPRESS • 9 GAMES", "FULL • 14 GAMES" }, (int)_fmt, i => { _fmt = (TourFormat)i; }, 18);
            f.At(0, 1, 22, -52, 536, 46);
            ScreenKit.Section(o, "OVERS PER INNINGS", 22, -116);
            var ov = Ui.Segments(o, new[] { "5", "10", "20" }, Array.IndexOf(new[] { 5, 10, 20 }, _overs), i => _overs = new[] { 5, 10, 20 }[i], 20);
            ov.At(0, 1, 22, -148, 536, 46);
            ScreenKit.Section(o, "DIFFICULTY", 22, -212);
            var d = Ui.Segments(o, new[] { "EASY", "MEDIUM", "HARD", "PRO" }, save.Settings.Difficulty, i => { save.Settings.Difficulty = i; SaveSystem.MarkDirty(); }, 18);
            d.At(0, 1, 22, -244, 536, 46);
            var go = Ui.Btn(page, "START SEASON", BtnStyle.Primary, () =>
            {
                save.Tour = Tournament.Create(_fmt, _overs, save.Fav, DateTime.Now.Ticks & 0x7FFFFFFF, (Difficulty)save.Settings.Difficulty);
                save.HasTour = true;
                Progression.SimAhead(save.Tour);
                SaveSystem.Save();
                Audio.Play(Sfx.Fanfare, 0.7f);
                _tab = 0;
                Hub();
            }, 34, Icon.Trophy);
            go.Rt().At(1, 0, -30, 30, 380, 100);
        }

        static void Hub()
        {
            var save = SaveSystem.Data;
            var t = save.Tour;
            var page = ScreenKit.Page("TourHub", "KPL " + League.Year, t.StageEnum.Label() + "  •  " + (t.Format == TourFormat.Express ? "Express" : "Full") + " format  •  " + t.Overs + " overs", MainMenu.Show);
            var tabs = Ui.Segments(page, new[] { "OVERVIEW", "TABLE", "FIXTURES", "STATS" }, _tab, i => { _tab = i; Hub(); }, 20);
            tabs.At(0.5f, 1, 0, -92, 720, 50);
            var body = Ui.Node(page, "Body");
            body.anchorMin = new Vector2(0, 0);
            body.anchorMax = new Vector2(1, 1);
            body.offsetMin = new Vector2(30, 20);
            body.offsetMax = new Vector2(-30, -156);
            switch (_tab)
            {
                case 1: Table(body, t); break;
                case 2: Fixtures(body, t); break;
                case 3: Stats(body, t); break;
                default: Overview(body, t); break;
            }
        }

        static void Overview(RectTransform body, Tournament t)
        {
            var save = SaveSystem.Data;
            var user = Teams.ById(t.UserTeam);
            var next = t.NextUserFixture();
            var card = Ui.Card(body, "Next", Pal.Accent.A(0.6f));
            card.rectTransform.anchorMin = new Vector2(0, 0);
            card.rectTransform.anchorMax = new Vector2(0.58f, 1);
            card.rectTransform.offsetMin = card.rectTransform.offsetMax = Vector2.zero;
            var c = card.rectTransform;
            if (t.Complete || next == null)
            {
                bool champ = t.Champion == t.UserTeam;
                var tr = Ui.IconImg(c, Icon.Trophy, champ ? Pal.Gold : Pal.Dim, 110);
                tr.rectTransform.At(0.5f, 1, 0, -30, 110, 110);
                string head = t.Complete ? (champ ? "CHAMPIONS!" : "SEASON OVER") : "ELIMINATED";
                var h = Ui.Label(c, head, 48, champ ? Pal.Gold : Color.white, TextAnchor.MiddleCenter, Font.Display);
                h.rectTransform.At(0.5f, 1, 0, -150, 600, 56);
                string line = t.Complete ? Teams.ById(t.Champion).Name + " won KPL " + League.Year + (t.RunnerUp != "" ? ", beating " + Teams.ById(t.RunnerUp).Name + " in the final." : ".")
                    : user.Name + " are out. Simulate the rest of the season to see who wins.";
                var l = Ui.Para(c, line, 19, Pal.Text, TextAnchor.UpperCenter);
                l.rectTransform.At(0.5f, 1, 0, -214, 600, 60);
                if (!t.Complete)
                {
                    var sim = Ui.Btn(c, "SIMULATE REST", BtnStyle.Primary, () =>
                    {
                        var sum = new RecordSummary();
                        var before = t.StageEnum;
                        Progression.SimAhead(t);
                        Progression.AfterTournamentProgress(t, save, sum, before);
                        Economy.Grant(save, sum.Coins.Sum(x => x.Coins));
                        Achievements.Check(save);
                        SaveSystem.Save();
                        Hub();
                    }, 26, Icon.Refresh);
                    sim.Rt().At(0.5f, 0, 0, 30, 320, 70);
                }
                else
                {
                    var again = Ui.Btn(c, "NEW SEASON", BtnStyle.Primary, () =>
                    {
                        save.HasTour = false;
                        SaveSystem.Save();
                        Setup();
                    }, 26, Icon.Trophy);
                    again.Rt().At(0.5f, 0, 0, 30, 320, 70);
                }
            }
            else
            {
                var stage = (TourStage)next.Stage;
                var lbl = Ui.Label(c, stage == TourStage.League ? "NEXT MATCH  •  GAME " + (t.UserGamesPlayed + 1) + " OF " + t.UserGamesTotal : stage.Label(), 20, Pal.Gold, TextAnchor.MiddleCenter, Font.Bold);
                lbl.rectTransform.At(0.5f, 1, 0, -18, 600, 26);
                var home = Teams.ById(next.Home);
                var away = Teams.ById(next.Away);
                var c1 = Crest.Add(c, home, 120);
                c1.At(0.5f, 1, -170, -56, 120, 120);
                var c2 = Crest.Add(c, away, 120);
                c2.At(0.5f, 1, 170, -56, 120, 120);
                var vs = Ui.Label(c, "VS", 48, Pal.Accent, TextAnchor.MiddleCenter, Font.Display);
                vs.rectTransform.At(0.5f, 1, 0, -90, 100, 56);
                var n1 = Ui.Label(c, home.Nick.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, Font.Display);
                n1.rectTransform.At(0.5f, 1, -170, -182, 260, 32);
                var n2 = Ui.Label(c, away.Nick.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, Font.Display);
                n2.rectTransform.At(0.5f, 1, 170, -182, 260, 32);
                var venue = Ui.Label(c, (stage == TourStage.Final ? Teams.ById("GUJ").Ground : home.Ground) + "  •  " + t.Overs + " overs", 16, Pal.Dim, TextAnchor.MiddleCenter, Font.Semi);
                venue.rectTransform.At(0.5f, 1, 0, -218, 600, 22);
                var play = Ui.Btn(c, "PLAY", BtnStyle.Primary, () => GameApp.I.StartMatch(Matches.Tournament(t, next)), 34, Icon.Play);
                play.Rt().At(0.5f, 0, -110, 26, 280, 80);
                play.gameObject.AddComponent<Pulse>().Amount = 0.02f;
                var sim = Ui.Btn(c, "SIMULATE", BtnStyle.Secondary, () =>
                {
                    var m = Progression.SimUserFixture(t, next);
                    var ctx = Matches.Tournament(t, next);
                    var sum = Progression.Record(ctx, m, 0);
                    ResultScreen.Show(ctx, m, sum);
                }, 22, Icon.Refresh);
                sim.Rt().At(0.5f, 0, 160, 32, 220, 66);
            }
            // Mini table.
            var side = Ui.Card(body, "Mini");
            side.rectTransform.anchorMin = new Vector2(0.6f, 0);
            side.rectTransform.anchorMax = new Vector2(1, 1);
            side.rectTransform.offsetMin = side.rectTransform.offsetMax = Vector2.zero;
            ScreenKit.Section(side.transform, "STANDINGS", 18, -16);
            var st = t.Standings();
            for (int i = 0; i < st.Count; i++)
            {
                var r = st[i];
                bool me = r.Team == t.UserTeam;
                var row = Ui.Node(side.transform, "R" + i).TopStrip(48 + i * 34, 32, 12, 12);
                if (me)
                {
                    var hl = Ui.Panel(row, Pal.Accent.A(0.22f), 8, "Hl");
                    hl.rectTransform.Fill();
                }
                if (i == 3)
                {
                    var line = Ui.Img(row, null, Pal.Gold.A(0.5f), "Cut");
                    line.rectTransform.anchorMin = new Vector2(0, 0);
                    line.rectTransform.anchorMax = new Vector2(1, 0);
                    line.rectTransform.sizeDelta = new Vector2(0, 2);
                }
                var pos = Ui.Label(row, (i + 1).ToString(), 18, i < 4 ? Pal.Gold : Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                pos.rectTransform.At(0, 0.5f, 0, 0, 30, 30);
                var cr = Crest.Add(row, Teams.ById(r.Team), 26, false);
                cr.At(0, 0.5f, 34, 0, 26, 26);
                var n = Ui.Label(row, Teams.ById(r.Team).Nick, 18, me ? Pal.Gold : Color.white, TextAnchor.MiddleLeft, Font.Bold);
                n.rectTransform.At(0, 0.5f, 68, 0, 180, 30);
                var p = Ui.Label(row, r.P + "    " + r.Pts + "    " + r.NrrText, 17, Pal.Text, TextAnchor.MiddleRight, Font.Semi);
                p.rectTransform.At(1, 0.5f, -6, 0, 200, 30);
            }
            var quit = Ui.Btn(body, "ABANDON SEASON", BtnStyle.Ghost, () => Popup.Confirm("Abandon this season?", "All progress in this KPL season will be lost.", "ABANDON", () =>
            {
                save.HasTour = false;
                SaveSystem.Save();
                Setup();
            }), 15);
            quit.Rt().At(1, 1, 0, 50, 190, 38);
        }

        static void Table(RectTransform body, Tournament t)
        {
            var card = Ui.Card(body, "Table");
            card.rectTransform.Fill();
            string[] heads = { "#", "TEAM", "P", "W", "L", "NR", "PTS", "NRR", "FORM" };
            float[] xs = { 20, 70, 520, 600, 680, 760, 840, 930, 1060 };
            for (int i = 0; i < heads.Length; i++)
            {
                var h = Ui.Label(card.transform, heads[i], 16, Pal.Dim, i == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Font.Bold);
                h.rectTransform.At(0, 1, xs[i] - (i == 1 ? 0 : 30), -14, i == 1 ? 300 : 80, 24);
            }
            var st = t.Standings();
            for (int r = 0; r < st.Count; r++)
            {
                var row = st[r];
                bool me = row.Team == t.UserTeam;
                float y = -48 - r * 42;
                var bg = Ui.Panel(card.transform, me ? Pal.Accent.A(0.22f) : r % 2 == 0 ? new Color(1, 1, 1, 0.03f) : new Color(0, 0, 0, 0), 8, "Row");
                bg.rectTransform.TopStrip(-y - 4, 40, 10, 10);
                var team = Teams.ById(row.Team);
                var cells = new[] { (r + 1).ToString(), team.Name, row.P.ToString(), row.W.ToString(), row.L.ToString(), row.NR.ToString(), row.Pts.ToString(), row.NrrText, row.Form };
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = Ui.Label(card.transform, cells[i], i == 6 ? 22 : 19, i == 6 ? Pal.Gold : me ? Pal.Gold : Color.white, i == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, i == 6 ? Font.Display : Font.Semi);
                    c.rectTransform.At(0, 1, xs[i] - (i == 1 ? -36 : 30), y, i == 1 ? 340 : i == 8 ? 140 : 80, 34);
                }
                var cr = Crest.Add(card.transform, team, 30, false);
                cr.At(0, 1, xs[1], y - 2, 30, 30);
                if (r == 3)
                {
                    var cut = Ui.Img(card.transform, null, Pal.Gold.A(0.6f), "Cut");
                    cut.rectTransform.TopStrip(-y + 38, 2, 14, 14);
                }
            }
            var note = Ui.Label(card.transform, "Top four qualify for the playoffs  •  NRR = runs per over scored minus conceded (all-out counts the full quota)", 15, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
            note.rectTransform.At(0, 0, 20, 12, 1100, 20);
        }

        static void Fixtures(RectTransform body, Tournament t)
        {
            var card = Ui.Card(body, "Fixtures");
            card.rectTransform.Fill();
            var list = Ui.ScrollList(card.transform, 6);
            ((RectTransform)list.parent).Fill(14, 14, 14, 14);
            foreach (var f in t.Fixtures)
            {
                bool me = f.Involves(t.UserTeam);
                var row = Ui.Row(list, 50);
                var bg = Ui.Panel(row, me ? Pal.Accent.A(0.16f) : new Color(1, 1, 1, 0.04f), 10, "Bg");
                bg.rectTransform.Fill();
                var no = Ui.Label(row, f.Stage == 0 ? "#" + f.No : ((TourStage)f.Stage).Label(), 16, f.Stage == 0 ? Pal.Dim : Pal.Gold, TextAnchor.MiddleLeft, Font.Bold);
                no.rectTransform.At(0, 0.5f, 14, 0, 140, 30);
                var h = Teams.ById(f.Home);
                var a = Teams.ById(f.Away);
                var c1 = Crest.Add(row, h, 32, false);
                c1.At(0, 0.5f, 150, 0, 32, 32);
                var tx = Ui.Label(row, h.Id + (f.Played ? "  " + f.HomeScore : "") + "   v   " + a.Id + (f.Played ? "  " + f.AwayScore : ""), 18, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                tx.rectTransform.At(0, 0.5f, 190, 0, 520, 30);
                var c2 = Crest.Add(row, a, 32, false);
                c2.At(0, 0.5f, 690, 0, 32, 32);
                var res = Ui.Label(row, f.Played ? f.Result : me ? "YOUR MATCH" : "Upcoming", 15, f.Played ? (f.Winner == t.UserTeam ? Pal.Green : Pal.Dim) : me ? Pal.Gold : Pal.Mute, TextAnchor.MiddleRight, Font.Semi);
                res.rectTransform.At(1, 0.5f, -14, 0, 420, 30);
            }
        }

        static void Stats(RectTransform body, Tournament t)
        {
            void Column(string title, Color c, float x0, float x1, List<SeasonStat> rows, Func<SeasonStat, string> value)
            {
                var card = Ui.Card(body, title, c.A(0.5f));
                card.rectTransform.anchorMin = new Vector2(x0, 0);
                card.rectTransform.anchorMax = new Vector2(x1, 1);
                card.rectTransform.offsetMin = new Vector2(6, 0);
                card.rectTransform.offsetMax = new Vector2(-6, 0);
                var head = Ui.Label(card.transform, title, 26, c, TextAnchor.MiddleLeft, Font.Display);
                head.rectTransform.TopStrip(12, 32, 18, 18);
                for (int i = 0; i < rows.Count; i++)
                {
                    var s = rows[i];
                    var p = PlayerDb.ById(s.PlayerId);
                    if (p == null) continue;
                    var row = Ui.Node(card.transform, "R").TopStrip(56 + i * 42, 40, 14, 14);
                    if (i == 0)
                    {
                        var hl = Ui.Panel(row, c.A(0.2f), 8, "Hl");
                        hl.rectTransform.Fill();
                    }
                    var cr = Crest.Add(row, Teams.ById(s.Team), 28, false);
                    cr.At(0, 0.5f, 6, 0, 28, 28);
                    var n = Ui.Label(row, p.Name, 18, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                    n.rectTransform.At(0, 0.5f, 42, 0, 230, 30);
                    var v = Ui.Label(row, value(s), 20, Pal.Gold, TextAnchor.MiddleRight, Font.Display);
                    v.rectTransform.At(1, 0.5f, -6, 0, 120, 30);
                }
            }
            Column("GOLD BAT • RUNS", Pal.Gold, 0, 0.334f, t.TopRuns(8), s => s.Runs.ToString());
            Column("GOLD BALL • WICKETS", Pal.Violet, 0.334f, 0.667f, t.TopWickets(8), s => s.Wkts.ToString());
            Column("MOST SIXES", Pal.Sky, 0.667f, 1, t.TopSixes(8), s => s.Sixes.ToString());
        }
    }

    public static class ChallengesScreen
    {
        static int _chapter = -1;

        public static void Show(int chapter)
        {
            var save = SaveSystem.Data;
            if (chapter >= 0) _chapter = chapter;
            if (_chapter < 0)
            {
                // Open on the first chapter with an unfinished level.
                int first = save.ChallengeStars.FindIndex(s => s == 0);
                _chapter = first < 0 ? 4 : Challenges.All[first].Chapter;
            }
            var page = ScreenKit.Page("Challenges", "CHALLENGES", save.TotalStars + " / 150 stars  •  50 scenarios in 5 chapters", MainMenu.Show, 0.7f);
            var tabs = Ui.Node(page, "Chapters").At(0, 1, 30, -96, 1220, 64);
            var lay = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 8;
            lay.childForceExpandWidth = true;
            lay.childControlWidth = true;
            lay.childControlHeight = true;
            for (int c = 0; c < 5; c++)
            {
                int cc = c;
                int stars = Challenges.All.Where(x => x.Chapter == c).Sum(x => save.ChallengeStars[x.Id - 1]);
                bool unlocked = ChapterUnlocked(c);
                var b = Ui.Btn(tabs, (c + 1) + ". " + Challenges.ChapterNames[c], c == _chapter ? BtnStyle.Primary : BtnStyle.Secondary, () =>
                {
                    if (!unlocked)
                    {
                        Popup.Toast("Clear the previous chapter's final to unlock", Icon.Lock, Pal.Red);
                        return;
                    }
                    Show(cc);
                }, 16, unlocked ? (Icon?)null : Icon.Lock, stars + " / 30 ★");
                if (!unlocked) Ui.SetInteractable(b, true);
            }
            var blurb = Ui.Label(page, Challenges.ChapterBlurbs[_chapter], 18, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
            blurb.rectTransform.At(0, 1, 34, -170, 1100, 24);
            var grid = Ui.Node(page, "Grid").At(0.5f, 0, 0, 24, 1220, 420);
            var levels = Challenges.All.Where(x => x.Chapter == _chapter).ToList();
            for (int i = 0; i < levels.Count; i++)
            {
                var def = levels[i];
                int col = i % 5, row = i / 5;
                var tile = Ui.Node(grid, "L" + def.Id).At(0, 1, col * 244, -row * 206, 232, 196);
                int stars = save.ChallengeStars[def.Id - 1];
                bool open = LevelUnlocked(def);
                bool final = def.Goal == ChallengeGoal.WinMatch;
                var bg = Ui.Panel(tile, open ? Pal.Hex(0x0B1426, 0.92f) : Pal.Hex(0x0B1426, 0.6f), 18, "Bg", stars > 0 ? Pal.Gold.A(0.8f) : open ? (final ? Pal.Accent : Pal.Stroke) : new Color(1, 1, 1, 0.05f));
                bg.rectTransform.Fill();
                var num = Ui.Label(tile, def.Id.ToString(), 46, open ? Color.white : Pal.Mute, TextAnchor.UpperLeft, Font.Display);
                num.rectTransform.At(0, 1, 16, -6, 100, 52);
                var icon = Ui.IconImg(tile, def.UserBats ? Icon.Bat : Icon.Ball, open ? (def.UserBats ? Pal.Teal : Pal.Accent) : Pal.Mute, 30);
                icon.rectTransform.At(1, 1, -16, -16, 30, 30);
                var title = Ui.Para(tile, def.Title.ToUpperInvariant(), 18, open ? Color.white : Pal.Mute, TextAnchor.UpperLeft, Font.Bold);
                title.rectTransform.At(0, 1, 16, -64, 200, 50);
                if (open)
                {
                    for (int s = 0; s < 3; s++)
                    {
                        var st = Ui.IconImg(tile, s < stars ? Icon.Star : Icon.StarEmpty, s < stars ? Pal.Gold : Pal.Mute, 30);
                        st.rectTransform.At(0, 0, 16 + s * 34, 14, 30, 30);
                    }
                    var b = tile.gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    tile.gameObject.AddComponent<Pressable>();
                    b.onClick.AddListener(() =>
                    {
                        Audio.Play(Sfx.Click);
                        Detail(def);
                    });
                }
                else
                {
                    var lk = Ui.IconImg(tile, Icon.Lock, Pal.Mute, 34);
                    lk.rectTransform.At(0, 0, 16, 14, 34, 34);
                }
                Enter.Add(tile, i * 0.025f, 12);
            }
        }

        public static bool ChapterUnlocked(int c)
        {
            if (c == 0) return true;
            var prevFinal = Challenges.All.Last(x => x.Chapter == c - 1);
            return SaveSystem.Data.ChallengeStars[prevFinal.Id - 1] > 0;
        }

        public static bool LevelUnlocked(ChallengeDef d)
        {
            if (!ChapterUnlocked(d.Chapter)) return false;
            if (d.Id == 1) return true;
            var prev = Challenges.ById(d.Id - 1);
            return prev.Chapter != d.Chapter || SaveSystem.Data.ChallengeStars[d.Id - 2] > 0;
        }

        public static void Detail(ChallengeDef def)
        {
            var save = SaveSystem.Data;
            int stars = save.ChallengeStars[def.Id - 1];
            var c = Popup.Open("LEVEL " + def.Id + "  •  " + def.Title.ToUpperInvariant(), 760, 470);
            var opp = Challenges.Opponent(def, save.Fav);
            var brief = Ui.Para(c, def.Brief, 20, Pal.Text);
            brief.rectTransform.TopStrip(70, 54, 28, 28);
            var goal = Ui.Panel(c, Pal.Accent.A(0.18f), 14, "Goal", Pal.Accent.A(0.5f));
            goal.rectTransform.TopStrip(128, 56, 28, 28);
            var gi = Ui.IconImg(goal.transform, Icon.Target, Pal.Accent, 30);
            gi.rectTransform.At(0, 0.5f, 14, 0, 30, 30);
            var gt = Ui.Label(goal.transform, def.GoalText.ToUpperInvariant(), 20, Color.white, TextAnchor.MiddleLeft, Font.Bold);
            gt.rectTransform.Fill(56, 0, 12, 0);
            for (int s = 1; s <= 3; s++)
            {
                var row = Ui.Node(c, "Star" + s).TopStrip(198 + (s - 1) * 40, 36, 28, 28);
                var st = Ui.IconImg(row, s <= stars ? Icon.Star : Icon.StarEmpty, s <= stars ? Pal.Gold : Pal.Dim, 28);
                st.rectTransform.At(0, 0.5f, 0, 0, 28, 28);
                var tx = Ui.Label(row, def.StarText(s), 19, s <= stars ? Pal.Gold : Pal.Text, TextAnchor.MiddleLeft, Font.Semi);
                tx.rectTransform.At(0, 0.5f, 40, 0, 500, 30);
                var rw = Ui.Label(row, "+" + (def.Reward * (s == 3 ? 2 : 1)), 18, Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
                rw.rectTransform.At(1, 0.5f, -40, 0, 120, 30);
                var ci = Ui.IconImg(row, Icon.Coin, Pal.Gold, 24);
                ci.rectTransform.At(1, 0.5f, -6, 0, 24, 24);
            }
            var vs = Ui.Label(c, "vs " + opp.Name + (def.Night ? "  •  under lights" : "") + (def.MinDifficulty > Difficulty.Easy ? "  •  min. difficulty " + def.MinDifficulty.ToString().ToUpperInvariant() : ""), 16, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
            vs.rectTransform.At(0, 0, 28, 104, 600, 22);
            var root = c.parent.gameObject;
            var play = Ui.Btn(c, stars > 0 ? "REPLAY" : "PLAY", BtnStyle.Primary, () =>
            {
                Popup.Close(root);
                GameApp.I.StartMatch(Matches.Challenge(def));
            }, 30, Icon.Play);
            play.Rt().At(1, 0, -28, 24, 280, 70);
        }
    }
}
