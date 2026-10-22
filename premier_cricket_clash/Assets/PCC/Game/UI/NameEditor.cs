using System.Collections.Generic;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>EDIT NAMES: rename any player (team squads and free agents); saved on the device.</summary>
    public static class NameEditor
    {
        static int _tab;
        static float _resetArmed = -10;
        const int FreeAgentsTab = 10;

        public static void Show()
        {
            var page = Menus.Begin(Menus.Page.Names);
            Menus.Dim(page, 0.78f);
            Menus.TopBar(page, "EDIT PLAYER NAMES", "Tap a name to change it  •  saved on this phone  •  used in matches, scorecards and the auction");

            // Team tabs + free agents.
            var tabs = Ui.Node(page, "Tabs").At(0.5f, 1, 0, -62, 932, 40);
            int n = Teams.All.Count + 1;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool sel = i == _tab;
                var cell = Ui.Panel(tabs, new Color(0.03f, 0.05f, 0.15f, sel ? 0.98f : 0.85f), 10, "Tab" + i, sel ? Pal.Gold : Pal.Stroke);
                var rt = cell.rectTransform;
                rt.anchorMin = new Vector2(i / (float)n, 0);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                rt.offsetMin = new Vector2(2, 0);
                rt.offsetMax = new Vector2(-2, 0);
                if (i < Teams.All.Count)
                {
                    var t = Teams.All[i];
                    var cr = Crest.Add(rt, t, 24);
                    ((RectTransform)cr.transform.parent).At(0, 0.5f, 6, 0, 24, 27);
                    var id = Ui.Label(rt, t.Id, 17, sel ? Pal.GoldLight : Pal.Text, TextAnchor.MiddleLeft);
                    id.rectTransform.Fill(34, 0, 2, 0);
                }
                else
                {
                    var fa = Ui.Label(rt, "FREE", 16, sel ? Pal.GoldLight : Pal.Cyan);
                    fa.rectTransform.Fill();
                }
                var b = cell.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() =>
                {
                    Sfx.I?.Click();
                    _tab = idx;
                    Show();
                });
            }

            // Player list.
            var card = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.94f), 16, "List", Pal.Stroke);
            card.rectTransform.At(0.5f, 1, 0, -108, 932, 262);
            var c = card.rectTransform;
            foreach (var (txt, x) in new[] { ("NAME (TAP TO EDIT)", 14f), ("ORIGINAL", 352f), ("ROLE", 590f), ("OVR", 652f) })
            {
                var h = Ui.Label(c, txt, 12, Pal.Gold, TextAnchor.MiddleLeft, false);
                h.rectTransform.At(0, 1, x, -6, 220, 16);
            }
            var view = Ui.ScrollList(c, out var content);
            view.Fill(6, 26, 6, 6);
            IReadOnlyList<Player> players = _tab < Teams.All.Count ? PlayerDb.Instance.Squad(Teams.All[_tab].Id) : PlayerDb.Instance.FreeAgents;
            var list = players.OrderByDescending(p => p.Overall).ToList();
            const float rowH = 40;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                bool renamed = PlayerNames.IsRenamed(p.Id);
                var row = Ui.Row(content, i * rowH, rowH - 4);
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = Ui.Round(8);
                bg.type = Image.Type.Sliced;
                bg.color = renamed ? Pal.Gold.WithA(0.14f) : new Color(1, 1, 1, i % 2 == 0 ? 0.05f : 0.02f);
                var field = Ui.TextField(row, p.Name, p.OriginalName, 17, PlayerNames.MaxLength, v =>
                {
                    if (v == p.Name) return;
                    PlayerNames.Set(p.Id, v);
                    Menus.Toast(string.IsNullOrWhiteSpace(v) ? "Name reset" : "Saved: " + p.Name);
                    Show();
                });
                ((RectTransform)field.transform).At(0, 0.5f, 6, 0, 330, 30);
                var orig = Ui.Label(row, p.OriginalName + (p.Overseas ? "  <color=#38E8FF>" + p.Country + "</color>" : ""), 14, Pal.Dim, TextAnchor.MiddleLeft, false);
                orig.rectTransform.At(0, 0.5f, 346, 0, 236, 20);
                AuctionScreens.Badge(row, AuctionScreens.RoleCode(p), AuctionScreens.RoleColor(p), 584, -9, 50, 18);
                var ovr = Ui.Label(row, p.Overall.ToString(), 19, AuctionScreens.TierColor(p), TextAnchor.MiddleLeft);
                ovr.rectTransform.At(0, 0.5f, 650, 1, 40, 24);
                if (renamed)
                {
                    var reset = Ui.Ghost(row, "RESET", Pal.Text, () =>
                    {
                        PlayerNames.Set(p.Id, "");
                        Menus.Toast("Back to " + p.OriginalName);
                        Show();
                    }, 15);
                    ((RectTransform)reset.transform).At(1, 0.5f, -10, 0, 84, 28);
                }
            }
            Ui.SetContentHeight(content, list.Count * rowH);

            // Footer.
            var info = Ui.Label(page, PlayerNames.Count + " player" + (PlayerNames.Count == 1 ? "" : "s") + " renamed", 15, Pal.Dim, TextAnchor.MiddleLeft, false);
            info.rectTransform.At(0, 0, 24, 22, 300, 20);
            var resetAll = Ui.Ghost(page, "RESET ALL NAMES", Pal.Red, () =>
            {
                if (Time.unscaledTime - _resetArmed < 2.5f)
                {
                    PlayerNames.ResetAll();
                    Menus.Toast("All names reset");
                    Show();
                    return;
                }
                _resetArmed = Time.unscaledTime;
                Menus.Toast("Tap RESET ALL NAMES again to confirm");
            }, 18);
            ((RectTransform)resetAll.transform).At(1, 0, -240, 10, 210, 44);
            var done = Ui.Button(page, "DONE", Pal.Gold, Pal.GoldDeep, Menus.Main, 24);
            ((RectTransform)done.transform).At(1, 0, -18, 10, 210, 44);
        }
    }
}
