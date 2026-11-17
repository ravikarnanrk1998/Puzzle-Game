using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Full scorecard: each innings' batting and bowling with extras and fall of wickets.</summary>
    public static class Scorecard
    {
        public static void Build(RectTransform parent, MatchState m)
        {
            var title = Ui.Label(parent, "SCORECARD", 32, Color.white, TextAnchor.MiddleLeft, Font.Display);
            title.rectTransform.TopStrip(12, 40, 24, 80);
            var list = Ui.ScrollList(parent, 2, new RectOffset(0, 0, 0, 20));
            ((RectTransform)list.parent).Fill(20, 60, 20, 14);
            foreach (var inn in m.InningsList)
            {
                var head = Ui.Row(list, 46);
                var hb = Ui.Panel(head, Crest.Primary(inn.Bat.Team).A(0.5f), 10, "Head");
                hb.rectTransform.Fill();
                var ht = Ui.Label(head, inn.Bat.Team.Name.ToUpperInvariant() + (inn.SuperOver ? "  (SUPER OVER)" : ""), 24, Color.white, TextAnchor.MiddleLeft, Font.Display);
                ht.rectTransform.Fill(14, 0, 200, 0);
                var hs = Ui.Label(head, inn.Score + "  (" + inn.OversText + ")", 24, Pal.Gold, TextAnchor.MiddleRight, Font.Display);
                hs.rectTransform.Fill(200, 0, 14, 0);
                Header(list, new[] { "BATTER", "", "R", "B", "4s", "6s", "SR" });
                foreach (var c in inn.BatCards.Where(c => c.Batted))
                {
                    Line(list, new[] { c.P.Name + (c.P.Id == inn.Bat.CaptainId ? " (c)" : "") + (c.P.Id == inn.Bat.KeeperId ? " †" : ""), c.DismissalText,
                        c.Runs.ToString(), c.Balls.ToString(), c.Fours.ToString(), c.Sixes.ToString(), c.StrikeRate.ToString("0.0") }, !c.Out);
                }
                var dnb = inn.BatCards.Where(c => !c.Batted).Select(c => c.P.Short).ToList();
                var extras = Ui.Row(list, 26);
                var et = Ui.Label(extras, "Extras " + inn.Extras + "  (wd " + inn.Wides + ", nb " + inn.NoBalls + ", b " + inn.Byes + ", lb " + inn.LegByes + ")" + (dnb.Count > 0 ? "     Did not bat: " + string.Join(", ", dnb) : ""), 15, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                et.rectTransform.Fill(14, 0, 14, 0);
                et.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (inn.Fow.Count > 0)
                {
                    var f = Ui.Row(list, 26);
                    var ft = Ui.Label(f, "FoW: " + string.Join(",  ", inn.Fow.Select(x => x.Score + "-" + x.Wkt + " (" + x.Name + ", " + Overs.Text(x.Balls) + ")")), 14, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                    ft.rectTransform.Fill(14, 0, 14, 0);
                    ft.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
                Header(list, new[] { "BOWLER", "", "O", "M", "R", "W", "ECON" });
                foreach (var b in inn.BowlCards)
                {
                    Line(list, new[] { b.P.Name, (b.Wides > 0 ? b.Wides + "wd " : "") + (b.NoBalls > 0 ? b.NoBalls + "nb" : ""), b.OversText, b.Maidens.ToString(), b.Runs.ToString(), b.Wkts.ToString(), b.Economy.ToString("0.00") }, false);
                }
                Ui.Row(list, 12);
            }
            if (m.Result != null)
            {
                var r = Ui.Row(list, 40);
                var rt = Ui.Label(r, m.Result.Text, 22, Pal.Gold, TextAnchor.MiddleCenter, Font.Display);
                rt.rectTransform.Fill();
            }
        }

        static readonly float[] Cols = { 0f, 0.3f, 0.66f, 0.73f, 0.8f, 0.86f, 0.92f };

        static void Header(RectTransform list, string[] cells) => Cells(list, cells, Pal.Dim, 15, 26, false, true);

        static void Line(RectTransform list, string[] cells, bool highlight) => Cells(list, cells, highlight ? Pal.Gold : Color.white, 17, 30, highlight, false);

        static void Cells(RectTransform list, string[] cells, Color c, int size, float h, bool bold, bool header)
        {
            var row = Ui.Row(list, h);
            if (header)
            {
                var line = Ui.Img(row, null, new Color(1, 1, 1, 0.08f), "Line");
                line.rectTransform.anchorMin = new Vector2(0, 0);
                line.rectTransform.anchorMax = new Vector2(1, 0);
                line.rectTransform.sizeDelta = new Vector2(0, 1);
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var t = Ui.Label(row, cells[i], i == 1 ? size - 3 : size, i == 1 ? Pal.Dim : c, i < 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, header ? Font.Bold : Font.Semi);
                t.rectTransform.anchorMin = new Vector2(Cols[i], 0);
                t.rectTransform.anchorMax = new Vector2(i + 1 < Cols.Length ? Cols[i + 1] : 1, 1);
                t.rectTransform.offsetMin = new Vector2(i == 0 ? 14 : 4, 0);
                t.rectTransform.offsetMax = new Vector2(i == cells.Length - 1 ? -14 : -4, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (i < 2) t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }
    }
}
