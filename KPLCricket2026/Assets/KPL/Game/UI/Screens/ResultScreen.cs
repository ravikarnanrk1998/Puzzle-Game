using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    public static class ResultScreen
    {
        public static void Show(MatchContext ctx, MatchState m, RecordSummary sum)
        {
            var save = SaveSystem.Data;
            string user = ctx.UserId;
            bool practice = ctx.Practice;
            bool won = sum.Won;
            var page = Nav.Page("Result", () => Continue(ctx));
            ScreenKit.Scrim(page, 0.62f);
            Color accent = practice ? Pal.Sky : won ? Pal.Gold : sum.Tie ? Pal.Teal : Pal.Red;

            // Left: headline.
            var head = Ui.Card(page, "Head", accent.A(0.7f));
            head.rectTransform.At(0, 0.5f, 24, 0, 430, 640);
            var h = head.rectTransform;
            var glow = Ui.Img(h, Sprites.Glow, accent.A(0.25f), "Glow");
            glow.rectTransform.At(0.5f, 1, 0, 60, 460, 460);
            string title = practice ? "SESSION OVER" : ctx.Kind == MatchKind.Challenge ? (won ? "CHALLENGE CLEARED" : "NOT THIS TIME") : won ? "VICTORY" : sum.Tie ? "TIED" : "DEFEAT";
            var t = Ui.Label(h, title, title.Length > 10 ? 44 : 66, accent, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 1, 0, -20, 420, 74);
            Ui.Shadow(t, 3, 0.6f);
            if (ctx.Kind == MatchKind.Challenge)
            {
                for (int s = 0; s < 3; s++)
                {
                    bool on = s < ctx.Challenge.Evaluate(m, user);
                    var st = Ui.IconImg(h, on ? Icon.Star : Icon.StarEmpty, on ? Pal.Gold : Pal.Mute, 64);
                    st.rectTransform.At(0.5f, 1, (s - 1) * 76, -110, 64, 64);
                    if (on) Enter.Add(st, 0.2f + s * 0.15f, 30);
                }
            }
            else if (m.Result != null && m.Result.WinnerId != null)
            {
                var cr = Crest.Add(h, Teams.ById(m.Result.WinnerId), 110);
                cr.At(0.5f, 1, 0, -100, 110, 110);
            }
            var res = Ui.Para(h, m.Result?.Text ?? "", 20, Pal.Text, TextAnchor.UpperCenter, Font.Bold);
            res.rectTransform.At(0.5f, 1, 0, -224, 380, 56);
            if (!string.IsNullOrEmpty(sum.Note))
            {
                var note = Ui.Para(h, sum.Note, 18, Pal.Gold, TextAnchor.UpperCenter, Font.Semi);
                note.rectTransform.At(0.5f, 1, 0, -282, 380, 50);
            }
            if (m.Result != null && !m.IsScenario)
            {
                var potm = m.PlayerById(m.Result.PotmId);
                if (potm != null)
                {
                    var pc = Ui.Label(h, "PLAYER OF THE MATCH", 14, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                    pc.rectTransform.At(0.5f, 1, 0, -340, 380, 20);
                    var pn = Ui.Label(h, potm.Name.ToUpperInvariant(), 30, Color.white, TextAnchor.MiddleCenter, Font.Display);
                    pn.rectTransform.At(0.5f, 1, 0, -366, 400, 36);
                    var line = PotmLine(m, potm);
                    var pl = Ui.Label(h, line, 17, Pal.Gold, TextAnchor.MiddleCenter, Font.Semi);
                    pl.rectTransform.At(0.5f, 1, 0, -400, 400, 22);
                }
            }
            // Buttons.
            var cont = Ui.Btn(h, "CONTINUE", BtnStyle.Primary, () => Continue(ctx), 30, Icon.Play);
            cont.Rt().At(0.5f, 0, 0, 92, 380, 70);
            var sc = Ui.Btn(h, "SCORECARD", BtnStyle.Secondary, () =>
            {
                var c = Popup.Open("", 1180, 660);
                Scorecard.Build(c, m);
            }, 20, Icon.Chart);
            sc.Rt().At(0, 0, 24, 22, 186, 56);
            if (ctx.Kind == MatchKind.Quick || ctx.Kind == MatchKind.Challenge || practice)
            {
                var again = Ui.Btn(h, practice ? "AGAIN" : ctx.Kind == MatchKind.Challenge ? "RETRY" : "REMATCH", BtnStyle.Secondary, () => Again(ctx), 20, Icon.Refresh);
                again.Rt().At(1, 0, -24, 22, 186, 56);
            }

            // Right: performances and rewards.
            var right = Ui.Card(page, "Right");
            right.rectTransform.anchorMin = new Vector2(0, 0);
            right.rectTransform.anchorMax = new Vector2(1, 1);
            right.rectTransform.offsetMin = new Vector2(474, 40);
            right.rectTransform.offsetMax = new Vector2(-24, -40);
            var r = right.rectTransform;
            ScreenKit.Section(r, "TOP PERFORMERS", 22, -20);
            int row = 0;
            foreach (var inn in m.InningsList.Where(i => !i.SuperOver))
            {
                var team = inn.Bat.Team;
                var lineBg = Ui.Panel(r, Crest.Primary(team).A(0.25f), 12, "Inn");
                lineBg.rectTransform.TopStrip(56 + row * 120, 110, 22, 22);
                var cr = Crest.Add(lineBg.transform, team, 50, false);
                cr.At(0, 1, 12, -10, 50, 50);
                var sc2 = Ui.Label(lineBg.transform, team.Name.ToUpperInvariant() + "  " + inn.Score + " (" + inn.OversText + ")", 24, Color.white, TextAnchor.MiddleLeft, Font.Display);
                sc2.rectTransform.At(0, 1, 74, -12, 600, 30);
                var bats = inn.BatCards.Where(c => c.Batted).OrderByDescending(c => c.Runs).Take(2).Select(c => c.P.Short + " " + c.Runs + (c.Out ? "" : "*") + " (" + c.Balls + ")");
                var bowl = inn.BowlCards.OrderByDescending(b => b.Wkts).ThenBy(b => b.Runs).Take(2).Select(b => b.P.Short + " " + b.Wkts + "-" + b.Runs);
                var l1 = Ui.Label(lineBg.transform, string.Join("   ", bats), 17, Pal.Gold, TextAnchor.MiddleLeft, Font.Semi);
                l1.rectTransform.At(0, 1, 74, -48, 640, 22);
                var l2 = Ui.Label(lineBg.transform, "Bowling: " + string.Join("   ", bowl), 16, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                l2.rectTransform.At(0, 1, 74, -74, 640, 22);
                row++;
                if (row >= 2) break;
            }
            float y = 56 + row * 120 + 10;
            if (!practice)
            {
                ScreenKit.Section(r, "REWARDS", 22, -y);
                y += 34;
                foreach (var (label, coins) in sum.Coins)
                {
                    var rr = Ui.Node(r, "Reward").TopStrip(y, 28, 30, 30);
                    var lt = Ui.Label(rr, label, 18, Pal.Text, TextAnchor.MiddleLeft, Font.Semi);
                    lt.rectTransform.Fill();
                    var ct = Ui.Label(rr, "+" + Ui.Coins(coins), 18, Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
                    ct.rectTransform.Fill();
                    y += 28;
                }
                var total = Ui.Panel(r, Pal.Gold.A(0.18f), 14, "Total", Pal.Gold.A(0.6f));
                total.rectTransform.TopStrip(y + 8, 56, 22, 22);
                var ic = Ui.IconImg(total.transform, Icon.Coin, Pal.Gold, 36);
                ic.rectTransform.At(0, 0.5f, 14, 0, 36, 36);
                var tl = Ui.Label(total.transform, "TOTAL", 22, Color.white, TextAnchor.MiddleLeft, Font.Display);
                tl.rectTransform.Fill(62, 0, 0, 0);
                var tv = Ui.Label(total.transform, "0", 32, Pal.Gold, TextAnchor.MiddleRight, Font.Display);
                tv.rectTransform.Fill(0, 0, 18, 0);
                GameApp.I.StartCoroutine(ScreenKit.CountUp(tv, 0, sum.Total, 1.2f));
                if (sum.Total > 0) Audio.Play(Sfx.Coin);
                y += 74;
            }
            foreach (var a in sum.NewAchievements)
            {
                var ab = Ui.Panel(r, Pal.Violet.A(0.2f), 12, "Ach", Pal.Violet.A(0.6f));
                ab.rectTransform.TopStrip(y, 44, 22, 22);
                var ai = Ui.IconImg(ab.transform, Icon.Medal, Pal.Gold, 30);
                ai.rectTransform.At(0, 0.5f, 12, 0, 30, 30);
                var at = Ui.Label(ab.transform, "ACHIEVEMENT UNLOCKED: " + a.Title.ToUpperInvariant() + "  (claim +" + a.Reward + " in Stats)", 17, Color.white, TextAnchor.MiddleLeft, Font.Bold);
                at.rectTransform.Fill(52, 0, 12, 0);
                Enter.Add(ab, 0.5f);
                y += 50;
            }
            Audio.Play(won ? Sfx.Fanfare : Sfx.Groan, won ? 0.8f : 0.6f);
            Audio.Music(true);
            GameApp.I.Menu3D?.Celebrate(won);
        }

        static string PotmLine(MatchState m, Player p)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var inn in m.InningsList.Where(i => !i.SuperOver))
            {
                var b = inn.BatCards.FirstOrDefault(c => c.P.Id == p.Id && c.Batted);
                if (b != null) parts.Add(b.Runs + (b.Out ? "" : "*") + " (" + b.Balls + ")");
                var w = inn.BowlCards.FirstOrDefault(c => c.P.Id == p.Id);
                if (w != null) parts.Add(w.Wkts + "-" + w.Runs + " (" + w.OversText + ")");
            }
            return string.Join("  &  ", parts);
        }

        static void Continue(MatchContext ctx)
        {
            switch (ctx.Kind)
            {
                case MatchKind.Tournament: TournamentScreen.Show(); break;
                case MatchKind.Challenge: ChallengesScreen.Show(ctx.Challenge.Chapter); break;
                default: MainMenu.Show(); break;
            }
        }

        static void Again(MatchContext ctx)
        {
            switch (ctx.Kind)
            {
                case MatchKind.Challenge: GameApp.I.StartMatch(Matches.Challenge(ctx.Challenge)); break;
                case MatchKind.Practice: GameApp.I.StartMatch(Matches.Nets(ctx.NetsBat)); break;
                default:
                    var s = ctx.Setup;
                    GameApp.I.StartMatch(Matches.Quick(s.Home.Team, s.Away.Team, s.Overs, s.Night));
                    break;
            }
        }
    }

    /// <summary>3D life behind the menus: players at the crease and an orbiting camera.</summary>
    public sealed class MenuScene : MonoBehaviour
    {
        Actor _batter, _keeper, _umpire, _bowler, _fielder;
        CameraRig _cam;
        float _t;
        bool _celebrate;

        public Actor Batter => _batter;
        public Actor Keeper => _keeper;

        public static MenuScene Create(Transform world, Stadium s)
        {
            var go = new GameObject("MenuScene");
            go.transform.SetParent(world, false);
            var m = go.AddComponent<MenuScene>();
            var fav = s.Home;
            var opp = s.Away;
            var bat = TeamSheet.Default(fav).XI;
            var fld = TeamSheet.Default(opp).XI;
            var striker = bat.First(p => p.Role == Role.Batter);
            m._batter = Actor.Create(go.transform, Look.For(striker, fav, Outfit.Batter), "MenuBatter", new Vector3(striker.LeftBat ? 0.5f : -0.5f, 0, (float)Pitch.StrikerCreaseZ), striker.LeftBat ? -90 : 90);
            m._batter.Mirror = striker.LeftBat;
            m._batter.Proc(t => BatAnim.Stance(t), 0);
            var keeper = fld.FirstOrDefault(p => p.IsKeeper) ?? fld[0];
            m._keeper = Actor.Create(go.transform, Look.For(keeper, opp, Outfit.Keeper), "MenuKeeper", new Vector3(-0.3f, 0, (float)Pitch.StrikerStumpsZ - 1.2f), 0);
            m._keeper.Hold(Poses.KeeperCrouch(), 0);
            m._umpire = Actor.Create(go.transform, new Look { Outfit = Outfit.Umpire, Team = fav, Skin = 2, Build = 2, Number = 3 }, "MenuUmpire", new Vector3(-0.7f, 0, (float)Pitch.StumpsZ + 1.7f), 180);
            m._umpire.Hold(Poses.UmpireStand(), 0);
            var bowler = fld.First(p => p.IsPace);
            m._bowler = Actor.Create(go.transform, Look.For(bowler, opp, Outfit.Fielder), "MenuBowler", new Vector3(1.2f, 0, (float)Pitch.StumpsZ + 3), 180);
            m._bowler.Body.HeadGear?.gameObject.SetActive(false);
            m._bowler.Proc(t => Poses.Idle(t), 0);
            var f = fld.First(p => p.Role == Role.AllRounder);
            m._fielder = Actor.Create(go.transform, Look.For(f, opp, Outfit.Fielder), "MenuFielder", new Vector3(-3.4f, 0, (float)Pitch.StrikerStumpsZ - 9), 20);
            m._fielder.Hold(Poses.Ready(), 0);
            m._cam = CameraRig.Attach(SceneLook.MainCamera());
            m.Frame();
            return m;
        }

        /// <summary>Low camera tilted up a little: outfield below, stands, screens and sky above.</summary>
        void Frame()
        {
            _cam.OrbitCenter = new Vector3(0, 0, -6);
            _cam.OrbitRadius = 36;
            _cam.OrbitHeight = 4.5f;
            _cam.OrbitLookY = 8;
            _cam.OrbitFov = 48;
            _cam.Set(CamMode.Orbit, true);
        }

        public void Celebrate(bool won)
        {
            _celebrate = true;
            if (won) _batter.Proc(t => Poses.Celebrate(t), 0.3f);
            else _batter.Hold(Poses.Dejected(), 0.3f);
            _t = 0;
        }

        void OnEnable()
        {
            if (_cam != null) Frame();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t > 6 && _batter != null && _celebrate)
            {
                _celebrate = false;
                _batter.Proc(t => BatAnim.Stance(t), 0.5f);
            }
        }
    }
}
