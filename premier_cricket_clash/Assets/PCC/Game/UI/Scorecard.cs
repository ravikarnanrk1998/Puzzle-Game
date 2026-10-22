using System;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>Full scorecard (batting and bowling per innings) and dismissal text.</summary>
    public static class Scorecard
    {
        static string Last(Player p)
        {
            int i = p.Name.LastIndexOf(' ');
            return i < 0 ? p.Name : p.Name.Substring(i + 1);
        }

        public static string DismissalLine(Dismissal d, Player fielder, Player bowler)
        {
            string b = bowler != null ? Last(bowler) : "";
            string f = fielder != null ? Last(fielder) : "";
            switch (d)
            {
                case Dismissal.Bowled: return "b " + b;
                case Dismissal.Lbw: return "lbw b " + b;
                case Dismissal.Caught:
                    if (fielder != null && bowler != null && fielder.Id == bowler.Id) return "c & b " + b;
                    return fielder != null ? "c " + f + " b " + b : "c b " + b;
                case Dismissal.CaughtBehind: return "c †" + f + " b " + b;
                case Dismissal.Stumped: return "st †" + f + " b " + b;
                case Dismissal.RunOut: return fielder != null ? "run out (" + f + ")" : "run out";
                default: return d.Label().ToLowerInvariant();
            }
        }

        static string InningsTab(MatchEngine m, int i)
        {
            var inn = m.InningsList[i];
            if (inn.SuperOver) return inn.Batting.Team.Id + " S/O";
            return inn.Batting.Team.Id + " " + inn.ScoreText;
        }

        /// <summary>Fills <paramref name="card"/> with a tabbed scorecard (one tab per innings).</summary>
        public static void Build(RectTransform card, MatchEngine m, Action onClose)
        {
            var title = Ui.Label(card, "SCORECARD", 30, Pal.Text, TextAnchor.MiddleLeft);
            title.rectTransform.At(0, 1, 18, -6, 200, 36);
            if (onClose != null)
            {
                var close = Ui.Ghost(card, "CLOSE", Pal.Cyan, onClose, 20);
                ((RectTransform)close.transform).At(1, 1, -12, -10, 100, 34);
            }
            var body = Ui.Node(card, "Body").Fill(14, 52, 14, 12);
            if (m.InningsList.Count == 0)
            {
                var none = Ui.Label(body, "The match has not started yet.", 20, Pal.Dim, TextAnchor.MiddleCenter, false);
                none.rectTransform.Fill();
                return;
            }
            int count = m.InningsList.Count;
            var tabs = Ui.Node(card, "Tabs").At(0, 1, 200, -10, Mathf.Min(onClose != null ? 360 : 440, 130 * count), 34);
            var labels = Enumerable.Range(0, count).Select(i => InningsTab(m, i)).ToArray();
            int start = m.Finished ? 0 : count - 1;
            Ui.Segments(tabs, labels, start, i => Fill(body, m.InningsList[i], m), 16);
            Fill(body, m.InningsList[start], m);
        }

        // Row geometry (reference px from the top of a table).
        const float Head = 22, BatPitch = 24, BowlPitch = 21;

        static void Fill(RectTransform body, Innings inn, MatchEngine m)
        {
            foreach (Transform c in body) UnityEngine.Object.Destroy(c.gameObject);

            // ---- Batting (left 60%).
            var bat = Panel(body, "Batting", 0, 0, 0.6f, 1, 0, 0, -6, 0);
            Header(bat, Pal.Gold, inn.Batting.Team.Name.ToUpperInvariant(), ("R", -126), ("B", -96), ("4s", -70), ("6s", -44), ("SR", -4));
            int row = 0;
            foreach (var c in inn.Bat.Values.OrderBy(c => c.Position))
            {
                var p = inn.Batting.ById(c.PlayerId);
                bool notOut = !c.Out;
                string how = c.Out && c.How != null
                    ? DismissalLine(c.How.Value, c.FielderId != null ? inn.Bowling.ById(c.FielderId.Value) : null, c.BowlerId != null ? inn.Bowling.ById(c.BowlerId.Value) : null)
                    : "not out";
                string name = p.Name + (p.Id == inn.Batting.CaptainId ? " (c)" : "") + (p.Id == inn.Batting.KeeperId ? " †" : "");
                float y = -Head - row * BatPitch;
                Stretch(Ui.Label(bat, name, 14, notOut ? Pal.GoldLight : Pal.Text, TextAnchor.UpperLeft, false), 10, 174, y, 15);
                Stretch(Ui.Label(bat, how, 11, notOut ? Pal.Green : Pal.Mute, TextAnchor.UpperLeft, false), 10, 174, y - 13, 12);
                Cell(bat, y, -126, c.Runs.ToString(), 18, notOut ? Pal.GoldLight : Pal.Text, true);
                Cell(bat, y, -96, c.Balls.ToString(), 13, Pal.Dim, false);
                Cell(bat, y, -70, c.Fours.ToString(), 13, Pal.Dim, false);
                Cell(bat, y, -44, c.Sixes.ToString(), 13, Pal.Dim, false);
                Cell(bat, y, -4, c.StrikeRate.ToString("F0"), 13, Pal.Dim, false);
                row++;
            }
            var dnb = inn.Batting.Order.Where(p => !inn.Bat.ContainsKey(p.Id)).Select(Last).ToList();
            if (dnb.Count > 0)
            {
                var t = Ui.Label(bat, "Did not bat: " + string.Join(", ", dnb), 11, Pal.Mute, TextAnchor.UpperLeft, false);
                Stretch(t, 10, 10, -Head - row * BatPitch - 2, 13);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            var extras = Ui.Label(bat, "Extras " + inn.Extras + " (wd " + inn.Wides + ", nb " + inn.NoBalls + ", lb " + inn.LegByes + ", b " + inn.Byes + ")", 11, Pal.Dim, TextAnchor.MiddleLeft, false);
            extras.rectTransform.At(0, 0, 10, 6, 220, 16);
            var total = Ui.Label(bat, inn.ScoreText + "  <size=12>(" + inn.OversText + " ov, RR " + inn.RunRate.ToString("F2") + ")</size>", 20, Pal.GoldLight, TextAnchor.MiddleRight);
            total.rectTransform.At(1, 0, -6, 4, 200, 22);

            // ---- Bowling (right 40%, top).
            var bowl = Panel(body, "Bowling", 0.6f, 0.36f, 1, 1, 6, 0, 0, 0);
            Header(bowl, Pal.Pink, "BOWLING", ("O", -126), ("M", -98), ("R", -70), ("W", -40), ("ECO", -4));
            row = 0;
            foreach (int id in inn.BowlerOrder)
            {
                if (!inn.Bowl.TryGetValue(id, out var c)) continue;
                var p = inn.Bowling.ById(id);
                float y = -Head - row * BowlPitch;
                Stretch(Ui.Label(bowl, Last(p), 14, Pal.Text, TextAnchor.UpperLeft, false), 10, 168, y - 2, 16);
                Cell(bowl, y, -126, Overs.Text(c.Balls), 13, Pal.Dim, false);
                Cell(bowl, y, -98, c.Maidens.ToString(), 13, Pal.Dim, false);
                Cell(bowl, y, -70, c.Runs.ToString(), 13, Pal.Dim, false);
                Cell(bowl, y, -40, c.Wickets.ToString(), 17, c.Wickets >= 3 ? Pal.GoldLight : Pal.Text, true);
                Cell(bowl, y, -4, c.Economy.ToString("F1"), 13, Pal.Dim, false);
                if (++row >= 6) break;
            }

            // ---- Fall of wickets and match situation (right 40%, bottom).
            var info = Panel(body, "Info", 0.6f, 0, 1, 0.36f, 6, 0, 0, -8);
            var fowCap = Ui.Label(info, "FALL OF WICKETS", 11, Pal.Cyan, TextAnchor.UpperLeft, false);
            fowCap.rectTransform.Fill(10, 6, 10, 0);
            string fow = inn.FallOfWickets.Count == 0
                ? "—"
                : string.Join("   ", inn.FallOfWickets.Select(f => f.Wicket + "-" + f.Score + " (" + Overs.Text(f.Balls) + ")"));
            var fowT = Ui.Para(info, fow, 11, Pal.Dim);
            fowT.rectTransform.Fill(10, 21, 10, 24);
            string status;
            if (m.Finished && m.Outcome != null) status = m.Outcome.Text;
            else if (inn.Target != null) status = "Target " + inn.Target + " • need " + inn.RunsNeeded + " off " + inn.BallsLeft;
            else status = "Projected " + Mathf.RoundToInt((float)(inn.Runs + inn.RunRate * inn.BallsLeft / 6.0));
            var st = Ui.Label(info, status, 13, Pal.GoldLight, TextAnchor.LowerLeft, false);
            st.rectTransform.Fill(10, 0, 10, 6);
            st.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static RectTransform Panel(RectTransform parent, string name, float x0, float y0, float x1, float y1, float l, float b, float r, float t)
        {
            var p = Ui.Panel(parent, new Color(1, 1, 1, 0.04f), 12, name);
            p.raycastTarget = false;
            var rt = p.rectTransform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(r, t);
            return rt;
        }

        static void Header(RectTransform parent, Color color, string title, params (string Text, float X)[] cols)
        {
            Stretch(Ui.Label(parent, title, 12, color, TextAnchor.MiddleLeft, false), 10, 170, -3, 16);
            foreach (var (text, x) in cols)
            {
                var t = Ui.Label(parent, text, 12, color, TextAnchor.MiddleRight, false);
                t.rectTransform.At(1, 1, x, -3, 30, 16);
            }
        }

        /// <summary>Spans the parent's width between left / right insets, at a fixed top offset and height.</summary>
        static void Stretch(Text t, float left, float right, float top, float height)
        {
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(left, top - height);
            rt.offsetMax = new Vector2(-right, top);
            // Overflow, not Truncate: UGUI hides a whole line whose height exceeds the rect.
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static void Cell(RectTransform parent, float top, float x, string text, float size, Color color, bool display)
        {
            var t = Ui.Label(parent, text, size, color, TextAnchor.MiddleRight, display);
            t.rectTransform.At(1, 1, x, top + 1, 30, 22);
        }
    }
}
