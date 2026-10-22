using System;
using System.Collections.Generic;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>Saved mega-auction result, and whether quick matches use those squads.</summary>
    public static class AuctionStore
    {
        const string Key = "pcc.auction", UseKey = "pcc.auction.use";

        public static (string UserId, Dictionary<string, Franchise> Teams)? Load() => AuctionResult.Parse(PlayerPrefs.GetString(Key, ""));

        public static void Save(AuctionEngine a)
        {
            PlayerPrefs.SetString(Key, AuctionResult.Serialize(a.UserId, a.Franchises.Values));
            PlayerPrefs.SetInt(UseKey, 1);
            PlayerPrefs.Save();
        }

        public static bool HasResult => Load() != null;

        public static void Clear() => PlayerPrefs.DeleteKey(Key);

        // ---- auction in progress (pause / save & exit / resume later)

        const string LiveKey = "pcc.auction.live";

        public static void SaveLive(AuctionEngine a)
        {
            PlayerPrefs.SetString(LiveKey, AuctionProgress.Serialize(a));
            PlayerPrefs.Save();
        }

        public static AuctionEngine LoadLive() => AuctionProgress.Deserialize(PlayerPrefs.GetString(LiveKey, ""), Environment.TickCount & 0x7fffffff);

        public static bool HasLive => PlayerPrefs.HasKey(LiveKey) && LoadLive() != null;

        public static void ClearLive()
        {
            PlayerPrefs.DeleteKey(LiveKey);
            PlayerPrefs.Save();
        }

        /// <summary>Quick matches use the auction squads (when an auction has been completed).</summary>
        public static bool UseSquads
        {
            get => PlayerPrefs.GetInt(UseKey, 1) == 1 && HasResult;
            set => PlayerPrefs.SetInt(UseKey, value ? 1 : 0);
        }

        /// <summary>The squad a team plays with: its auction squad when enabled, else its original squad.</summary>
        public static IReadOnlyList<Player> SquadFor(TeamInfo team)
        {
            if (UseSquads)
            {
                var saved = Load();
                if (saved != null && saved.Value.Teams.TryGetValue(team.Id, out var f) && f.Size >= 11) return f.Players;
            }
            return PlayerDb.Instance.Squad(team.Id);
        }
    }

    /// <summary>Mega auction flow: franchise select, retentions, live auction room, summary.</summary>
    public static class AuctionScreens
    {
        static int _teamIdx = -1;
        static readonly List<Player> Keep = new List<Player>();

        /// <summary>True while the live auction room is open (Android back is ignored there).</summary>
        public static bool InRoom { get; set; }

        // ---------------------------------------------------------- helpers

        public static string RoleCode(Player p) =>
            p.Role == Role.Wk ? "WK" : p.Role == Role.Bat ? "BAT" : p.Role == Role.Ar ? "AR" : (p.IsSpin ? "SPIN" : "PACE");

        public static Color RoleColor(Player p) =>
            p.Role == Role.Wk ? Pal.Cyan : p.Role == Role.Bat ? Pal.Gold : p.Role == Role.Ar ? Pal.Green : (p.IsSpin ? Pal.Violet : Pal.Pink);

        public static Color TierColor(Player p)
        {
            switch (p.Tier)
            {
                case "ELITE": return new Color(1f, 0.55f, 0.2f);
                case "GOLD": return Pal.Gold;
                case "SILVER": return new Color(0.75f, 0.82f, 0.95f);
                default: return new Color(0.82f, 0.58f, 0.38f);
            }
        }

        internal static void Badge(Transform parent, string text, Color color, float x, float y, float w = 46, float h = 18, float ax = 0)
        {
            // Dark fill: the border image is a full shape underneath, so a see-through fill would hide the text.
            var b = Ui.Panel(parent, new Color(0.04f, 0.05f, 0.14f, 0.96f), 6, "Badge", color.WithA(0.85f));
            b.raycastTarget = false;
            b.rectTransform.At(ax, 1, x, y, w, h);
            var t = Ui.Label(b.transform, text, 12, color, TextAnchor.MiddleCenter, false);
            t.rectTransform.Fill();
        }

        static Image Card(RectTransform page, float ax, float ay, float x, float y, float w, float h, Color? border = null)
        {
            var c = Ui.Panel(page, new Color(0.03f, 0.05f, 0.15f, 0.93f), 16, "Card", border ?? Pal.Stroke);
            c.rectTransform.At(ax, ay, x, y, w, h);
            return c;
        }

        // ---------------------------------------------------------- hub

        /// <summary>Main-menu entry: resume an auction in progress, else the last result, else a new auction.</summary>
        public static void Hub()
        {
            var live = AuctionStore.LoadLive();
            if (live != null)
            {
                ResumeChoice(live);
                return;
            }
            var saved = AuctionStore.Load();
            if (saved != null) Summary(saved.Value.UserId, saved.Value.Teams, null);
            else TeamSelect();
        }

        static float _newArmed = -10;

        /// <summary>An auction is in progress: resume it or start again.</summary>
        static void ResumeChoice(AuctionEngine live)
        {
            var page = Menus.Begin(Menus.Page.Auction);
            Menus.Dim(page, 0.7f);
            Menus.TopBar(page, "MEGA AUCTION " + League.Year, "Your auction is saved - carry on where you left off");
            var me = live.User;
            var card = Card(page, 0.5f, 0.5f, 0, 10, 560, 230, Mats.Hex(me.Info.Glow).WithA(0.7f));
            var c = card.rectTransform;
            var crest = Crest.Add(c, me.Info, 80, true);
            ((RectTransform)crest.transform.parent).At(0, 1, 26, -24, 80, 90);
            var name = Ui.Label(c, me.Info.Name.ToUpperInvariant(), 30, Pal.Text, TextAnchor.MiddleLeft);
            name.rectTransform.At(0, 1, 128, -22, 410, 34);
            int lot = Math.Min(live.Index + 1, live.Lots.Count);
            var lines = Ui.Label(c, "Lot <color=#FFE9A3>" + lot + "</color> of " + live.Lots.Count + "   •   " + live.SoldCount + " sold\n"
                + "Squad " + me.Size + "/" + AuctionRules.MaxSquad + "   •   Overseas " + me.OverseasCount + "/" + AuctionRules.MaxOverseas + "   •   RTM " + me.RtmCards + "\n"
                + "Purse left  <color=#2EE59D>" + AuctionRules.Money(me.Purse) + "</color>", 17, Pal.Dim, TextAnchor.UpperLeft, false);
            lines.rectTransform.At(0, 1, 130, -62, 410, 80);
            lines.lineSpacing = 1.1f;
            var resume = Ui.Button(c, "RESUME AUCTION", Pal.Gold, Pal.GoldDeep, () => Room(live), 28);
            ((RectTransform)resume.transform).At(1, 0, -20, 16, 300, 56);
            var fresh = Ui.Ghost(c, "NEW AUCTION", Pal.Red, () =>
            {
                if (Time.unscaledTime - _newArmed < 2.5f)
                {
                    AuctionStore.ClearLive();
                    TeamSelect();
                    return;
                }
                _newArmed = Time.unscaledTime;
                Menus.Toast("Tap NEW AUCTION again - the saved auction will be lost");
            }, 18);
            ((RectTransform)fresh.transform).At(0, 0, 20, 16, 190, 56);
            if (AuctionStore.HasResult)
            {
                var last = Ui.Ghost(page, "LAST RESULTS", Pal.Cyan, () =>
                {
                    var saved = AuctionStore.Load();
                    if (saved != null) Summary(saved.Value.UserId, saved.Value.Teams, null);
                }, 18);
                ((RectTransform)last.transform).At(1, 0, -18, 12, 190, 44);
            }
        }

        // ---------------------------------------------------------- team select

        public static void TeamSelect()
        {
            if (_teamIdx < 0) _teamIdx = Mathf.Max(0, Teams.IndexOf(Teams.ById(PlayerPrefs.GetString("pcc.team", "MUM"))));
            var page = Menus.Begin(Menus.Page.Auction);
            Menus.Dim(page, 0.6f);
            Menus.TopBar(page, "MEGA AUCTION " + League.Year, "Pick the franchise you will run  •  ₹120 Cr purse  •  up to 6 retentions");
            var grid = Ui.Node(page, "Grid").At(0.5f, 1, 0, -66, 930, 300);
            for (int i = 0; i < Teams.All.Count; i++)
            {
                int idx = i;
                var t = Teams.All[i];
                bool sel = i == _teamIdx;
                var cell = Ui.Panel(grid, new Color(0.03f, 0.05f, 0.15f, sel ? 0.97f : 0.85f), 16, t.Id, sel ? Pal.Gold : Mats.Hex(t.Glow).WithA(0.35f));
                cell.rectTransform.At(0, 1, (i % 5) * 186, -(i / 5) * 152, 178, 144);
                var crest = Crest.Add(cell.transform, t, 58, sel);
                ((RectTransform)crest.transform.parent).At(0.5f, 1, 0, -8, 58, 65);
                var name = Ui.Label(cell.transform, t.Name.ToUpperInvariant(), 17, sel ? Pal.GoldLight : Pal.Text);
                name.rectTransform.At(0.5f, 0, 0, 30, 172, 22);
                int ovr = Lineup.TeamRating(Lineup.PickBestXI(PlayerDb.Instance.Squad(t.Id)));
                var info = Ui.Label(cell.transform, "OVR " + ovr + "  •  " + t.Titles + " title" + (t.Titles == 1 ? "" : "s"), 12, Pal.Dim, TextAnchor.MiddleCenter, false);
                info.rectTransform.At(0.5f, 0, 0, 10, 172, 16);
                var b = cell.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() =>
                {
                    Sfx.I?.Click();
                    _teamIdx = idx;
                    Keep.Clear();
                    TeamSelect();
                });
                cell.gameObject.AddComponent<PressScale>();
            }
            var team = Teams.All[_teamIdx];
            var next = Ui.Button(page, "RETENTIONS  ›", Pal.Gold, Pal.GoldDeep, () => Retention(team), 28, "Next: choose who to keep");
            ((RectTransform)next.transform).At(1, 0, -18, 10, 280, 58);
            var you = Ui.Label(page, "YOU RUN  <color=#FFE9A3>" + team.Name.ToUpperInvariant() + "</color>", 22, Pal.Text, TextAnchor.MiddleLeft);
            you.rectTransform.At(0, 0, 22, 22, 500, 30);
        }

        // ---------------------------------------------------------- retention

        public static void Retention(TeamInfo team)
        {
            var page = Menus.Begin(Menus.Page.Auction);
            Menus.Dim(page, 0.72f);
            Menus.TopBar(page, "RETENTIONS", team.Name.ToUpperInvariant() + "  •  keep up to 6 (max 5 capped, 2 uncapped)  •  unused slots become RTM cards");
            var squad = PlayerDb.Instance.Squad(team.Id).OrderByDescending(p => p.Overall).ToList();

            // Squad list (left).
            var listCard = Card(page, 0, 1, 14, -64, 596, 354);
            var head = Ui.Label(listCard.transform, "PLAYER", 12, Pal.Gold, TextAnchor.MiddleLeft, false);
            head.rectTransform.At(0, 1, 16, -6, 200, 16);
            foreach (var (txt, x) in new[] { ("ROLE", 248f), ("OVR", 300f), ("VALUE", 352f), ("SLAB", 432f) })
            {
                var h = Ui.Label(listCard.transform, txt, 12, Pal.Gold, TextAnchor.MiddleLeft, false);
                h.rectTransform.At(0, 1, x, -6, 80, 16);
            }
            var view = Ui.ScrollList(listCard.transform, out var content);
            view.Fill(6, 26, 6, 6);
            const float rowH = 36;
            var costs = global::PCC.Core.Retention.Costs(Keep);
            for (int i = 0; i < squad.Count; i++)
            {
                var p = squad[i];
                int keptAt = Keep.IndexOf(p);
                var row = Ui.Row(content, i * rowH, rowH - 3);
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = Ui.Round(8);
                bg.type = Image.Type.Sliced;
                bg.color = keptAt >= 0 ? Pal.Gold.WithA(0.2f) : new Color(1, 1, 1, i % 2 == 0 ? 0.05f : 0.02f);
                var name = Ui.Label(row, p.Name, 16, keptAt >= 0 ? Pal.GoldLight : Pal.Text, TextAnchor.MiddleLeft, false);
                name.rectTransform.At(0, 0.5f, 10, 1, 170, 22);
                if (p.Overseas) Badge(row, p.Country, Pal.Cyan, 184, -9, 38, 16);
                Badge(row, RoleCode(p), RoleColor(p), 236, -8, 46, 18);
                var ovr = Ui.Label(row, p.Overall.ToString(), 20, TierColor(p), TextAnchor.MiddleLeft);
                ovr.rectTransform.At(0, 0.5f, 296, 1, 40, 24);
                var val = Ui.Label(row, AuctionRules.Money(p.MarketValue()), 14, Pal.Dim, TextAnchor.MiddleLeft, false);
                val.rectTransform.At(0, 0.5f, 342, 0, 90, 20);
                var capTag = Ui.Label(row, p.Capped() ? "capped" : "uncapped", 11, Pal.Mute, TextAnchor.MiddleLeft, false);
                capTag.rectTransform.At(0, 0.5f, 424, -1, 60, 16);
                var toggle = Ui.Ghost(row, keptAt >= 0 ? "#" + (keptAt + 1) + " " + AuctionRules.Money(costs[keptAt]) : "KEEP",
                    keptAt >= 0 ? Pal.Gold : Pal.Text, () => ToggleKeep(team, p), 15);
                ((RectTransform)toggle.transform).At(1, 0.5f, -10, 0, 92, 28);
            }
            Ui.SetContentHeight(content, squad.Count * rowH);

            // Retained summary (right).
            var side = Card(page, 1, 1, -14, -64, 322, 354, Pal.Gold.WithA(0.5f));
            var s = side.rectTransform;
            var title = Ui.Label(s, "RETAINED  " + Keep.Count + "/" + AuctionRules.MaxRetain, 22, Pal.Gold, TextAnchor.MiddleLeft);
            title.rectTransform.At(0, 1, 16, -8, 290, 26);
            for (int i = 0; i < AuctionRules.MaxRetain; i++)
            {
                string line = i < Keep.Count ? "#" + (i + 1) + "  " + Keep[i].ShortName + "  <color=#B4BCE0>" + AuctionRules.Money(costs[i]) + "</color>" : "#" + (i + 1) + "  —";
                var l = Ui.Label(s, line, 15, i < Keep.Count ? Pal.Text : Pal.Mute, TextAnchor.MiddleLeft, false);
                l.rectTransform.At(0, 1, 18, -38 - i * 20, 290, 20);
            }
            int spend = costs.Sum();
            var money = Ui.Label(s, "Retention cost  <color=#FFE9A3>" + AuctionRules.Money(spend) + "</color>\nAuction purse  <color=#2EE59D>" + AuctionRules.Money(AuctionRules.Purse - spend)
                + "</color>\nRTM cards  <color=#38E8FF>" + (AuctionRules.MaxRetain - Keep.Count) + "</color>", 15, Pal.Text, TextAnchor.UpperLeft, false);
            money.rectTransform.At(0, 1, 18, -162, 290, 64);
            var auto = Ui.Ghost(s, "AUTO PICK", Pal.Cyan, () =>
            {
                Keep.Clear();
                Keep.AddRange(global::PCC.Core.Retention.AiPick(squad));
                Retention(team);
            }, 18);
            ((RectTransform)auto.transform).At(0, 0, 14, 74, 140, 40);
            var clear = Ui.Ghost(s, "CLEAR", Pal.Text, () =>
            {
                Keep.Clear();
                Retention(team);
            }, 18);
            ((RectTransform)clear.transform).At(1, 0, -14, 74, 140, 40);
            var start = Ui.Button(s, "START AUCTION", Pal.Gold, Pal.GoldDeep, () => StartAuction(team), 26);
            ((RectTransform)start.transform).At(0.5f, 0, 0, 12, 294, 52);
        }

        static void ToggleKeep(TeamInfo team, Player p)
        {
            if (Keep.Contains(p))
            {
                Keep.Remove(p);
            }
            else
            {
                var trial = new List<Player>(Keep) { p };
                string problem = global::PCC.Core.Retention.Problem(trial);
                if (problem != null)
                {
                    Menus.Toast(problem);
                    return;
                }
                Keep.Add(p);
            }
            Retention(team);
        }

        /// <summary>Test hook: fill the retention list with the AI's choice for <paramref name="team"/>.</summary>
        internal static void AutoKeep(TeamInfo team)
        {
            Keep.Clear();
            Keep.AddRange(global::PCC.Core.Retention.AiPick(PlayerDb.Instance.Squad(team.Id)));
        }

        internal static void StartAuction(TeamInfo team)
        {
            PlayerPrefs.SetString("pcc.team", team.Id);
            var engine = MegaAuction.Create(team.Id, new List<Player>(Keep), Environment.TickCount & 0x7fffffff);
            Keep.Clear();
            Room(engine);
        }

        // ---------------------------------------------------------- room

        public static void Room(AuctionEngine engine)
        {
            var page = Menus.Begin(Menus.Page.Auction);
            Menus.Dim(page, 0.8f);
            AuctionRoom.Create(page, engine, a =>
            {
                var notes = a.FillSquads();
                AuctionStore.Save(a);
                AuctionStore.ClearLive();
                Summary(a.UserId, a.Franchises, notes);
            });
        }

        // ---------------------------------------------------------- summary

        public static void Summary(string userId, Dictionary<string, Franchise> teams, List<string> notes)
        {
            InRoom = false;
            var page = Menus.Begin(Menus.Page.Auction);
            Menus.Dim(page, 0.72f);
            var user = teams[userId];
            Menus.TopBar(page, "AUCTION RESULTS", "Your squad, the biggest buys and every franchise's purse");

            // Your squad (left).
            var left = Card(page, 0, 1, 14, -64, 430, 354, Mats.Hex(user.Info.Glow).WithA(0.6f));
            var l = left.rectTransform;
            var crest = Crest.Add(l, user.Info, 46, true);
            ((RectTransform)crest.transform.parent).At(0, 1, 14, -10, 46, 52);
            var tn = Ui.Label(l, user.Info.Name.ToUpperInvariant(), 24, Pal.Text, TextAnchor.MiddleLeft);
            tn.rectTransform.At(0, 1, 70, -8, 340, 28);
            var ts = Ui.Label(l, "OVR <color=#FFE9A3>" + user.Strength + "</color>   •   " + user.Size + " players (" + user.OverseasCount + " overseas)   •   " + AuctionRules.Money(user.Purse) + " left",
                14, Pal.Dim, TextAnchor.MiddleLeft, false);
            ts.rectTransform.At(0, 1, 72, -38, 350, 18);
            var view = Ui.ScrollList(l, out var content);
            view.Fill(8, 70, 8, 8);
            float y = 0;
            string[] groups = { "WK", "BAT", "AR", "PACE", "SPIN" };
            string[] titles = { "WICKETKEEPERS", "BATTERS", "ALL-ROUNDERS", "FAST BOWLERS", "SPINNERS" };
            for (int g = 0; g < groups.Length; g++)
            {
                var list = user.Squad.Where(c => RoleCode(c.Player) == groups[g]).OrderByDescending(c => c.Player.Overall).ToList();
                if (list.Count == 0) continue;
                var hr = Ui.Row(content, y, 22);
                var ht = Ui.Label(hr, titles[g] + "  " + list.Count, 13, RoleColor(list[0].Player), TextAnchor.MiddleLeft, false);
                ht.rectTransform.Fill(8, 0, 8, 0);
                y += 24;
                foreach (var c in list)
                {
                    var row = Ui.Row(content, y, 28);
                    var nm = Ui.Label(row, c.Player.Name + (c.Player.Overseas ? "  <size=11><color=#38E8FF>" + c.Player.Country + "</color></size>" : ""), 15, Pal.Text, TextAnchor.MiddleLeft, false);
                    nm.rectTransform.At(0, 0.5f, 10, 0, 220, 20);
                    var ov = Ui.Label(row, c.Player.Overall.ToString(), 18, TierColor(c.Player), TextAnchor.MiddleLeft);
                    ov.rectTransform.At(0, 0.5f, 236, 1, 34, 22);
                    string how = c.How == Acquired.Retained ? "RET" : c.How == Acquired.Rtm ? "RTM" : c.How == Acquired.Supplementary ? "SUP" : "BUY";
                    Color hc = c.How == Acquired.Retained ? Pal.Gold : c.How == Acquired.Rtm ? Pal.Cyan : c.How == Acquired.Supplementary ? Pal.Mute : Pal.Green;
                    Badge(row, how, hc, 274, -5, 40, 18);
                    var pr = Ui.Label(row, AuctionRules.Money(c.Price), 15, Pal.GoldLight, TextAnchor.MiddleRight, false);
                    pr.rectTransform.At(1, 0.5f, -10, 0, 100, 20);
                    y += 29;
                }
                y += 4;
            }
            Ui.SetContentHeight(content, y);

            // Top buys + all teams (right).
            var right = Card(page, 1, 1, -14, -64, 494, 290);
            var r = right.rectTransform;
            var tb = Ui.Label(r, "TOP BUYS", 13, Pal.Gold, TextAnchor.MiddleLeft, false);
            tb.rectTransform.At(0, 1, 14, -8, 200, 16);
            var buys = teams.Values.SelectMany(f => f.Squad.Where(c => c.How == Acquired.Auction || c.How == Acquired.Rtm).Select(c => (f, c)))
                .OrderByDescending(x => x.c.Price).Take(5).ToList();
            for (int i = 0; i < buys.Count; i++)
            {
                var (f, c) = buys[i];
                var cr = Crest.Add(r, f.Info, 20);
                ((RectTransform)cr.transform.parent).At(0, 1, 14, -28 - i * 22, 20, 22);
                var bl = Ui.Label(r, c.Player.Name + "  <color=#6E77A0>" + f.Id + (c.How == Acquired.Rtm ? " • RTM" : "") + "</color>", 15, Pal.Text, TextAnchor.MiddleLeft, false);
                bl.rectTransform.At(0, 1, 40, -29 - i * 22, 330, 20);
                var bp = Ui.Label(r, AuctionRules.Money(c.Price), 16, Pal.GoldLight, TextAnchor.MiddleRight);
                bp.rectTransform.At(1, 1, -14, -28 - i * 22, 110, 20);
            }
            var tt = Ui.Label(r, "FRANCHISES", 13, Pal.Cyan, TextAnchor.MiddleLeft, false);
            tt.rectTransform.At(0, 1, 14, -146, 200, 16);
            var ordered = teams.Values.OrderByDescending(f => f.Strength).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var f = ordered[i];
                int col = i / 5, rowI = i % 5;
                float x = 14 + col * 240, yy = -166 - rowI * 23;
                var cr = Crest.Add(r, f.Info, 18);
                ((RectTransform)cr.transform.parent).At(0, 1, x, yy, 18, 20);
                var fl = Ui.Label(r, (f.Id == userId ? "<color=#FFE9A3>" + f.Id + "</color>" : f.Id) + "   OVR " + f.Strength + "   " + f.Size + "p   " + AuctionRules.Money(f.Purse),
                    14, Pal.Text, TextAnchor.MiddleLeft, false);
                fl.rectTransform.At(0, 1, x + 24, yy - 1, 220, 20);
            }
            if (notes != null && notes.Count > 0)
            {
                var nt = Ui.Label(r, notes.Count + " player" + (notes.Count == 1 ? "" : "s") + " signed at base price to complete your squad", 12, Pal.Mute, TextAnchor.MiddleLeft, false);
                nt.rectTransform.At(0, 0, 14, 6, 460, 16);
            }

            var play = Ui.Button(page, "PLAY QUICK MATCH", Pal.Gold, Pal.GoldDeep, () =>
            {
                AuctionStore.UseSquads = true;
                PlayerPrefs.SetString("pcc.team", userId);
                Menus.ResetQuickMatch();
                Menus.QuickMatch();
            }, 24, "with your auction squad");
            ((RectTransform)play.transform).At(1, 0, -14, 12, 280, 54);
            var again = Ui.Ghost(page, "NEW AUCTION", Pal.Cyan, () =>
            {
                _teamIdx = Teams.IndexOf(Teams.ById(userId));
                Keep.Clear();
                TeamSelect();
            }, 20);
            ((RectTransform)again.transform).At(1, 0, -306, 12, 200, 54);
        }
    }

    /// <summary>The live auction room: drives bidding pace, the hammer and RTM decisions.</summary>
    public sealed class AuctionRoom : MonoBehaviour
    {
        enum State { Reveal, Bidding, Decision, Result }

        static float _speed = 1;

        AuctionEngine A;
        Action<AuctionEngine> _onDone;
        State _state;
        float _t, _quiet, _nextAi;
        int _idle;
        float _quitArmed = -10;
        bool _paused;

        /// <summary>The open auction room (for the Android back button).</summary>
        public static AuctionRoom Current { get; private set; }

        RectTransform _root, _card, _bidHistory, _strip, _overlay, _pauseLayer;
        Text _setName, _lotNo, _bidAmount, _leaderName, _status, _purse, _counts, _bidText, _bidSub;
        Image _leaderCrest;
        Button _bidButton;
        readonly List<(Text Purse, Text Count, Image Bg, string Id)> _chips = new List<(Text, Text, Image, string)>();

        public static AuctionRoom Create(RectTransform page, AuctionEngine a, Action<AuctionEngine> onDone)
        {
            var room = page.gameObject.AddComponent<AuctionRoom>();
            room.A = a;
            room._onDone = onDone;
            room._root = page;
            room.Build();
            AuctionScreens.InRoom = true;
            Current = room;
            AuctionStore.SaveLive(a);
            room.NextLot();
            return room;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------------------------------------------------------- layout

        void Build()
        {
            var title = Ui.Label(_root, "MEGA AUCTION " + League.Year, 28, Pal.Text, TextAnchor.MiddleLeft);
            title.rectTransform.At(0, 1, 18, -6, 360, 32);
            _setName = Ui.Label(_root, "", 15, Pal.Gold, TextAnchor.MiddleLeft, false);
            _setName.rectTransform.At(0, 1, 20, -38, 400, 18);
            _lotNo = Ui.Label(_root, "", 15, Pal.Dim, TextAnchor.MiddleRight, false);
            _lotNo.rectTransform.At(1, 1, -18, -8, 200, 18);
            var speed = Ui.Node(_root, "Speed").At(1, 1, -18, -30, 168, 26);
            float[] speeds = { 1, 2, 4 };
            Ui.Segments(speed, new[] { "1x", "2x", "4x" }, Array.IndexOf(speeds, _speed) is int si && si >= 0 ? si : 0, i => _speed = speeds[i], 15);
            var pause = Ui.Ghost(_root, "II  PAUSE", Pal.Gold, TogglePause, 16);
            ((RectTransform)pause.transform).At(1, 1, -196, -28, 96, 28);

            // Player card (left).
            _card = Ui.Node(_root, "PlayerCard").At(0, 1, 14, -62, 300, 290);

            // Bid centre.
            var centre = Ui.Panel(_root, new Color(0.03f, 0.05f, 0.15f, 0.93f), 16, "Centre", Pal.Gold.WithA(0.5f));
            centre.rectTransform.At(0.5f, 1, -3, -62, 306, 290);
            var c = centre.rectTransform;
            var cap = Ui.Label(c, "CURRENT BID", 13, Pal.Gold, TextAnchor.MiddleCenter, false);
            cap.rectTransform.At(0.5f, 1, 0, -8, 280, 16);
            _bidAmount = Ui.Label(c, "", 52, Pal.GoldLight);
            _bidAmount.rectTransform.At(0.5f, 1, 0, -24, 300, 56);
            Ui.Shadow(_bidAmount, 3, 0.6f);
            var crestRoot = Ui.Node(c, "LeaderCrest").At(0.5f, 1, -96, -88, 44, 50);
            _leaderCrest = Ui.Img(crestRoot, null, Color.white, "Crest");
            _leaderCrest.rectTransform.Fill();
            _leaderName = Ui.Label(c, "", 20, Pal.Text, TextAnchor.MiddleLeft);
            _leaderName.rectTransform.At(0.5f, 1, -66, -98, 200, 28);
            _leaderName.rectTransform.pivot = new Vector2(0, 1);
            _status = Ui.Label(c, "", 17, Pal.Cyan, TextAnchor.MiddleCenter, false);
            _status.rectTransform.At(0.5f, 1, 0, -146, 290, 22);
            _bidHistory = Ui.Node(c, "History").At(0.5f, 1, 0, -174, 280, 110);

            // User panel (right).
            var mine = Ui.Panel(_root, new Color(0.03f, 0.05f, 0.15f, 0.93f), 16, "Mine", Mats.Hex(A.User.Info.Glow).WithA(0.6f));
            mine.rectTransform.At(1, 1, -14, -62, 300, 290);
            var m = mine.rectTransform;
            var myCrest = Crest.Add(m, A.User.Info, 36, true);
            ((RectTransform)myCrest.transform.parent).At(0, 1, 12, -10, 36, 40);
            var myName = Ui.Label(m, A.User.Info.Name.ToUpperInvariant(), 18, Pal.Text, TextAnchor.MiddleLeft);
            myName.rectTransform.At(0, 1, 56, -10, 236, 22);
            _purse = Ui.Label(m, "", 26, Pal.Green, TextAnchor.MiddleLeft);
            _purse.rectTransform.At(0, 1, 56, -30, 236, 28);
            _counts = Ui.Label(m, "", 13, Pal.Dim, TextAnchor.MiddleLeft, false);
            _counts.rectTransform.At(0, 1, 14, -64, 280, 18);
            _bidButton = Ui.Button(m, "BID", Pal.Gold, Pal.GoldDeep, OnBid, 30, " ");
            ((RectTransform)_bidButton.transform).At(0.5f, 1, 0, -90, 276, 72);
            _bidText = _bidButton.transform.Find("Text").GetComponent<Text>();
            _bidSub = _bidButton.transform.Find("Sub").GetComponent<Text>();
            var pass = Ui.Ghost(m, "PASS", Pal.Text, OnPass, 18);
            ((RectTransform)pass.transform).At(0, 1, 12, -172, 134, 40);
            var skip = Ui.Ghost(m, "SKIP LOT", Pal.Cyan, OnSkipLot, 18);
            ((RectTransform)skip.transform).At(1, 1, -12, -172, 134, 40);
            var skipSet = Ui.Ghost(m, "SKIP SET", Pal.Cyan, OnSkipSet, 18);
            ((RectTransform)skipSet.transform).At(0, 1, 12, -220, 134, 40);
            var simRest = Ui.Ghost(m, "SIM REST", Pal.Gold, OnSimRest, 18);
            ((RectTransform)simRest.transform).At(1, 1, -12, -220, 134, 40);
            var hint = Ui.Label(m, "SIM REST: the AI bids for you until the end", 11, Pal.Mute, TextAnchor.MiddleCenter, false);
            hint.rectTransform.At(0.5f, 0, 0, 8, 280, 14);

            // Franchise strip (bottom).
            _strip = Ui.Node(_root, "Strip").Band(0, 1, 0, 8, 62, 14);
            int n = Teams.All.Count;
            for (int i = 0; i < n; i++)
            {
                var t = Teams.All[i];
                var cell = Ui.Panel(_strip, new Color(0.03f, 0.05f, 0.15f, 0.9f), 10, t.Id, Pal.Stroke);
                cell.raycastTarget = false;
                var rt = cell.rectTransform;
                rt.anchorMin = new Vector2(i / (float)n, 0);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                rt.offsetMin = new Vector2(2, 0);
                rt.offsetMax = new Vector2(-2, 0);
                var cr = Crest.Add(rt, t, 26);
                ((RectTransform)cr.transform.parent).At(0, 0.5f, 6, 0, 26, 30);
                var p = Ui.Label(rt, "", 14, Pal.Text, TextAnchor.UpperLeft);
                p.rectTransform.Fill(36, 6, 2, 0);
                var cnt = Ui.Label(rt, "", 11, Pal.Dim, TextAnchor.LowerLeft, false);
                cnt.rectTransform.Fill(36, 0, 2, 8);
                _chips.Add((p, cnt, cell, t.Id));
            }

            _overlay = Ui.Node(_root, "Overlay").Fill();
            _pauseLayer = Ui.Node(_root, "PauseLayer").Fill();
        }

        // ---------------------------------------------------------- lot flow

        void NextLot()
        {
            ClearOverlay();
            if (A.Finished)
            {
                Finish();
                return;
            }
            A.OpenLot();
            _state = State.Reveal;
            _t = 0;
            _quiet = 0;
            _idle = 0;
            _nextAi = UnityEngine.Random.Range(0.7f, 1.2f);
            BuildCard(A.Lot);
            Refresh();
        }

        void Update()
        {
            if (_paused) return;
            float dt = Time.unscaledDeltaTime * _speed;
            _t += dt;
            switch (_state)
            {
                case State.Reveal:
                    if (_t > 0.9f)
                    {
                        _state = State.Bidding;
                        _t = 0;
                    }
                    break;
                case State.Bidding:
                    TickBidding(dt);
                    break;
                case State.Result:
                    if (_t > 1.7f)
                    {
                        A.Advance();
                        // Auto-save after every lot, so leaving the app never loses the auction.
                        AuctionStore.SaveLive(A);
                        NextLot();
                    }
                    break;
            }
        }

        void TickBidding(float dt)
        {
            bool fast = A.UserPassed;
            _quiet += dt * (fast ? 3 : 1);
            _nextAi -= dt * (fast ? 4 : 1);
            if (_nextAi <= 0)
            {
                _nextAi = UnityEngine.Random.Range(0.45f, 1.1f);
                if (A.AiTick() != null)
                {
                    _quiet = 0;
                    _idle = 0;
                    Sfx.I?.Click();
                    Refresh();
                }
                else
                {
                    _idle++;
                }
            }
            const float hammerAt = 2.7f;
            bool aiDone = !A.AiStillInterested || _idle >= 3;
            string going = _quiet > hammerAt * 0.72f ? "GOING TWICE…" : _quiet > hammerAt * 0.42f ? "GOING ONCE…" : null;
            SetStatus(going);
            if (_quiet > hammerAt && aiDone) DoHammer();
        }

        void DoHammer()
        {
            bool needUser = A.Hammer();
            if (needUser) ShowDecision();
            else ShowResult();
        }

        void SetStatus(string going)
        {
            string s;
            if (_state == State.Reveal) s = "NEW LOT";
            else if (going != null) s = going;
            else if (A.Leader == null) s = A.InterestedCount > 0 ? "Bidding opens at base price" : "Waiting for a bid…";
            else s = A.InterestedCount + " team" + (A.InterestedCount == 1 ? "" : "s") + " still in";
            if (_status.text != s)
            {
                _status.text = s;
                _status.color = going != null ? Pal.Pink : Pal.Cyan;
            }
        }

        // ---------------------------------------------------------- actions

        internal bool ShowingResult => _state == State.Result;

        internal void OnBid()
        {
            if (_state != State.Bidding && _state != State.Reveal) return;
            if (_state == State.Reveal) _state = State.Bidding;
            if (A.UserBid())
            {
                _quiet = 0;
                _idle = 0;
                _nextAi = UnityEngine.Random.Range(0.6f, 1.2f);
                Refresh();
            }
        }

        void OnPass()
        {
            if (_state != State.Bidding && _state != State.Reveal) return;
            _state = State.Bidding;
            A.UserPassed = true;
            Refresh();
        }

        void OnSkipLot()
        {
            if (_state == State.Decision || _state == State.Result) return;
            A.ResolveInstantly();
            ShowResult();
        }

        internal void OnSkipSet()
        {
            if (_state == State.Decision || A.Finished) return;
            string set = A.Lot.SetCode;
            int sold = 0, count = 0;
            if (_state == State.Result) A.Advance();
            while (!A.Finished && A.Lot.SetCode == set)
            {
                A.ResolveInstantly();
                count++;
                if (A.LastResult != null && A.LastResult.Status == LotStatus.Sold) sold++;
                A.Advance();
            }
            Menus.Toast("Set skipped: " + sold + " of " + count + " players sold");
            AuctionStore.SaveLive(A);
            NextLot();
        }

        internal void OnSimRest()
        {
            if (_state == State.Decision) return;
            A.AiManagesUser = true;
            if (_state == State.Result) A.Advance();
            int guard = 0;
            while (!A.Finished && guard++ < 3000)
            {
                A.ResolveInstantly();
                A.Advance();
            }
            Finish();
        }

        // ---------------------------------------------------------- pause

        internal bool Paused => _paused;

        /// <summary>Snapshot of the live bidding (test hook: must not change while paused).</summary>
        internal string DebugState => A.Index + ":" + A.CurrentBid + ":" + A.Bids.Count + ":" + _state + ":" + _t.ToString("F2");

        /// <summary>Pauses (freezing bidding and saving progress) or resumes the auction.</summary>
        public void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        void Pause()
        {
            if (_paused || !enabled) return;
            _paused = true;
            AuctionStore.SaveLive(A);
            foreach (Transform t in _pauseLayer) Destroy(t.gameObject);
            var dim = Ui.Img(_pauseLayer, null, new Color(0, 0, 0, 0.65f), "Dim");
            dim.preserveAspect = false;
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var card = Ui.Panel(dim.transform, new Color(0.04f, 0.06f, 0.17f, 0.99f), 20, "Card", Pal.Gold.WithA(0.6f));
            card.rectTransform.At(0.5f, 0.5f, 0, 0, 540, 230);
            var c = card.rectTransform;
            var title = Ui.Label(c, "AUCTION PAUSED", 34, Pal.Text);
            title.rectTransform.At(0.5f, 1, 0, -10, 440, 40);
            var me = A.User;
            var info = Ui.Label(c, "Lot " + Math.Min(A.Index + 1, A.Lots.Count) + " of " + A.Lots.Count + "   •   Squad " + me.Size + "/" + AuctionRules.MaxSquad
                + "   •   " + AuctionRules.Money(me.Purse) + " left\nProgress is saved - you can leave and resume later from MEGA AUCTION.", 15, Pal.Dim, TextAnchor.UpperCenter, false);
            info.rectTransform.At(0.5f, 1, 0, -54, 500, 44);
            var resume = Ui.Button(c, "RESUME", Pal.Gold, Pal.GoldDeep, Resume, 26);
            ((RectTransform)resume.transform).At(0.5f, 0, 0, 72, 500, 52);
            var exit = Ui.Ghost(c, "SAVE & EXIT", Pal.Cyan, SaveAndExit, 19);
            ((RectTransform)exit.transform).At(0, 0, 20, 14, 245, 46);
            var quit = Ui.Ghost(c, "QUIT AUCTION", Pal.Red, QuitAuction, 19);
            ((RectTransform)quit.transform).At(1, 0, -20, 14, 245, 46);
        }

        void Resume()
        {
            if (!_paused) return;
            _paused = false;
            foreach (Transform t in _pauseLayer) Destroy(t.gameObject);
        }

        internal void SaveAndExit()
        {
            AuctionStore.SaveLive(A);
            AuctionScreens.InRoom = false;
            enabled = false;
            Menus.Main();
        }

        void QuitAuction()
        {
            if (Time.unscaledTime - _quitArmed < 2.5f)
            {
                AuctionStore.ClearLive();
                AuctionScreens.InRoom = false;
                enabled = false;
                Menus.Main();
                return;
            }
            _quitArmed = Time.unscaledTime;
            Menus.Toast("Tap QUIT AUCTION again - this auction will be deleted");
        }

        /// <summary>Leaving the app (home button, a call) pauses and saves the auction.</summary>
        void OnApplicationPause(bool appPaused)
        {
            if (appPaused && enabled && !_paused) Pause();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) OnApplicationPause(true);
        }

        void Finish()
        {
            AuctionScreens.InRoom = false;
            enabled = false;
            _onDone(A);
        }

        // ---------------------------------------------------------- RTM

        void ShowDecision()
        {
            _state = State.Decision;
            ClearOverlay();
            var l = A.Lot;
            var leader = A.Franchises[A.Leader].Info;
            string text, yes, no;
            Action onYes, onNo;
            switch (A.RtmStep)
            {
                case RtmStep.UserDecide:
                    text = l.Player.Name + " played for you.\n" + leader.Name + " bid " + AuctionRules.Money(A.CurrentBid) + ". Use an RTM card to match?";
                    yes = "USE RTM";
                    no = "LET HIM GO";
                    onYes = () => { A.UserUseRtm(true); AfterDecision(); };
                    onNo = () => { A.UserUseRtm(false); AfterDecision(); };
                    break;
                case RtmStep.UserMatch:
                    text = leader.Name + " raised to " + AuctionRules.Money(A.RtmRaisedTo) + ".\nMatch the final price?";
                    yes = "MATCH";
                    no = "DECLINE";
                    onYes = () => { A.UserMatchRaise(true); AfterDecision(); };
                    onNo = () => { A.UserMatchRaise(false); AfterDecision(); };
                    break;
                default:
                    ShowRaise();
                    return;
            }
            Modal(text, (yes, Pal.Gold, onYes), (no, Pal.Text, onNo));
        }

        void ShowRaise()
        {
            var holder = A.Franchises[A.RtmTeam].Info;
            int max = A.MaxAllowed(A.User);
            var options = new List<(string, Color, Action)>();
            foreach (int add in new[] { 0, 50, 100, 200 })
            {
                int price = A.CurrentBid + add;
                if (add > 0 && price > max) continue;
                int p = price;
                options.Add((add == 0 ? "STAY " + AuctionRules.Money(p) : "+" + AuctionRules.Money(add), add == 0 ? Pal.Text : Pal.Gold, () =>
                {
                    A.UserFinalRaise(p);
                    AfterDecision();
                }));
            }
            Modal(holder.Name + " used an RTM card on your winning bid.\nMake a final raise - they get one chance to match it.", options.ToArray());
        }

        void AfterDecision()
        {
            if (A.RtmStep == RtmStep.UserMatch) ShowDecision();
            else ShowResult();
        }

        void Modal(string text, params (string Label, Color Color, Action Act)[] buttons)
        {
            ClearOverlay();
            var dim = Ui.Img(_overlay, null, new Color(0, 0, 0, 0.6f), "Dim");
            dim.preserveAspect = false;
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var card = Ui.Panel(dim.transform, new Color(0.04f, 0.06f, 0.17f, 0.99f), 20, "Card", Pal.Cyan.WithA(0.6f));
            card.rectTransform.At(0.5f, 0.5f, 0, 0, 560, 210);
            var cap = Ui.Label(card.transform, "RIGHT TO MATCH", 26, Pal.Cyan);
            cap.rectTransform.At(0.5f, 1, 0, -10, 400, 30);
            var t = Ui.Para(card.transform, text, 17, Pal.Text, TextAnchor.UpperCenter);
            t.rectTransform.At(0.5f, 1, 0, -48, 520, 70);
            float w = (520 - (buttons.Length - 1) * 10f) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, color, act) = buttons[i];
                var b = Ui.Ghost(card.transform, label, color, () => act(), 18);
                ((RectTransform)b.transform).At(0, 0, 20 + i * (w + 10), 16, w, 46);
            }
        }

        // ---------------------------------------------------------- result

        void ShowResult()
        {
            _state = State.Result;
            _t = 0;
            ClearOverlay();
            Refresh();
            var l = A.LastResult ?? A.Lot;
            bool sold = l.Status == LotStatus.Sold;
            var banner = Ui.Panel(_overlay, new Color(0.02f, 0.03f, 0.1f, 0.94f), 20, "Hammer", (sold ? Pal.Gold : Pal.Mute).WithA(0.8f));
            banner.raycastTarget = false;
            banner.rectTransform.At(0.5f, 0.5f, 0, 20, 520, 150);
            var b = banner.rectTransform;
            var word = Ui.Label(b, sold ? "SOLD!" : "UNSOLD", 62, sold ? Pal.Gold : Pal.Dim);
            word.rectTransform.At(0.5f, 1, sold ? 40 : 0, -6, 300, 66);
            Ui.Shadow(word, 3, 0.7f);
            if (sold)
            {
                var team = A.Franchises[l.SoldTo].Info;
                var cr = Crest.Add(b, team, 70, true);
                ((RectTransform)cr.transform.parent).At(0, 0.5f, 26, 6, 70, 78);
                var to = Ui.Label(b, l.Player.Name + "  →  " + team.Name.ToUpperInvariant() + (l.ViaRtm ? "  (RTM)" : ""), 18, Pal.Text, TextAnchor.MiddleCenter, false);
                to.rectTransform.At(0.5f, 0, 40, 46, 400, 22);
                var price = Ui.Label(b, AuctionRules.Money(l.Price), 30, Pal.GoldLight);
                price.rectTransform.At(0.5f, 0, 40, 10, 300, 34);
                if (l.SoldTo == A.UserId) Sfx.I?.Cheer(0.6f);
            }
            else
            {
                var sub = Ui.Label(b, l.Player.Name + " goes unsold", 18, Pal.Dim, TextAnchor.MiddleCenter, false);
                sub.rectTransform.At(0.5f, 0, 0, 30, 400, 22);
            }
            Sfx.I?.Hammer();
        }

        void ClearOverlay()
        {
            foreach (Transform c in _overlay) Destroy(c.gameObject);
        }

        // ---------------------------------------------------------- views

        void BuildCard(AuctionLot l)
        {
            foreach (Transform old in _card) Destroy(old.gameObject);
            var p = l.Player;
            var tier = AuctionScreens.TierColor(p);
            var card = Ui.Panel(_card, new Color(0.03f, 0.05f, 0.15f, 0.95f), 16, "Card", tier.WithA(0.8f));
            card.rectTransform.Fill();
            var c = card.rectTransform;
            var band = Ui.Img(c, Ui.Round(14), tier.WithA(0.25f), "Band");
            band.type = Image.Type.Sliced;
            band.preserveAspect = false;
            band.rectTransform.anchorMin = new Vector2(0, 1);
            band.rectTransform.anchorMax = new Vector2(1, 1);
            band.rectTransform.pivot = new Vector2(0.5f, 1);
            band.rectTransform.offsetMin = new Vector2(3, -96);
            band.rectTransform.offsetMax = new Vector2(-3, -3);
            var ovr = Ui.Label(c, p.Overall.ToString(), 58, tier, TextAnchor.MiddleLeft);
            ovr.rectTransform.At(0, 1, 14, -4, 90, 62);
            Ui.Shadow(ovr, 2, 0.6f);
            var tierT = Ui.Label(c, p.Tier, 12, tier, TextAnchor.MiddleLeft, false);
            tierT.rectTransform.At(0, 1, 18, -64, 80, 14);
            var name = Ui.Label(c, p.Name.ToUpperInvariant(), 26, Pal.Text, TextAnchor.MiddleLeft);
            name.rectTransform.At(0, 1, 100, -12, 192, 30);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 26;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            AuctionScreens.Badge(c, AuctionScreens.RoleCode(p), AuctionScreens.RoleColor(p), 100, -46, 50, 18);
            AuctionScreens.Badge(c, p.Overseas ? p.Country : "IND", p.Overseas ? Pal.Cyan : Pal.GoldLight, 156, -46, 46, 18);
            AuctionScreens.Badge(c, p.Capped() ? "CAPPED" : "UNCAPPED", Pal.Dim, 208, -46, 78, 18);
            var style = Ui.Label(c, p.StyleLine, 12, Pal.Dim, TextAnchor.MiddleLeft, false);
            style.rectTransform.At(0, 1, 100, -70, 192, 16);
            Ui.Bar(c, "BAT", p.Bat, Pal.Gold, 14, -108, 272);
            Ui.Bar(c, "BOWL", p.BowlStyle == BowlStyle.None ? 0 : p.Bowl, Pal.Pink, 14, -132, 272);
            Ui.Bar(c, "POWER", p.Power, Pal.Cyan, 14, -156, 272);
            var basePrice = Ui.Label(c, "BASE PRICE  <color=#FFE9A3>" + AuctionRules.Money(l.Base) + "</color>", 17, Pal.Text, TextAnchor.MiddleLeft, false);
            basePrice.rectTransform.At(0, 0, 16, 48, 270, 22);
            A.RtmTeamOf.TryGetValue(p.Id, out string former);
            string formerText = former == null ? "Free agent" : "Former team  " + former + (A.Franchises[former].RtmCards > 0 ? "  •  RTM available" : "");
            var fl = Ui.Label(c, formerText, 13, former == A.UserId ? Pal.Gold : Pal.Dim, TextAnchor.MiddleLeft, false);
            fl.rectTransform.At(0, 0, 16, 24, 270, 18);
            var mv = Ui.Label(c, "Market value  " + AuctionRules.Money(p.MarketValue()), 12, Pal.Mute, TextAnchor.MiddleLeft, false);
            mv.rectTransform.At(0, 0, 16, 6, 270, 16);
        }

        void Refresh()
        {
            var l = A.Lot;
            if (l == null) return;
            _setName.text = l.SetName.ToUpperInvariant();
            _lotNo.text = "LOT " + (A.Index + 1) + " / " + A.Lots.Count + "   •   SOLD " + A.SoldCount;
            if (A.Leader == null)
            {
                _bidAmount.text = AuctionRules.Money(l.Base);
                _bidAmount.color = Pal.Dim;
                _leaderCrest.enabled = false;
                _leaderName.text = "no bids yet";
                _leaderName.color = Pal.Mute;
            }
            else
            {
                var leader = A.Franchises[A.Leader].Info;
                _bidAmount.text = AuctionRules.Money(A.CurrentBid);
                _bidAmount.color = Pal.GoldLight;
                _leaderCrest.enabled = true;
                _leaderCrest.sprite = Crest.Of(leader);
                _leaderName.text = leader.Name.ToUpperInvariant();
                _leaderName.color = A.Leader == A.UserId ? Pal.Green : Pal.Text;
            }

            // Last bids.
            foreach (Transform c in _bidHistory) Destroy(c.gameObject);
            var recent = A.Bids.Skip(Math.Max(0, A.Bids.Count - 4)).Reverse().ToList();
            for (int i = 0; i < recent.Count; i++)
            {
                var info = A.Franchises[recent[i].TeamId].Info;
                var cr = Crest.Add(_bidHistory, info, 20);
                ((RectTransform)cr.transform.parent).At(0, 1, 20, -i * 26, 20, 22);
                var t = Ui.Label(_bidHistory, info.Id + "  " + AuctionRules.Money(recent[i].Amount), 16, i == 0 ? Pal.Text : Pal.Dim, TextAnchor.MiddleLeft, false);
                t.rectTransform.At(0, 1, 48, -i * 26, 220, 22);
            }

            // User panel.
            var me = A.User;
            _purse.text = AuctionRules.Money(me.Purse);
            _counts.text = "Squad " + me.Size + "/" + AuctionRules.MaxSquad + "   •   Overseas " + me.OverseasCount + "/" + AuctionRules.MaxOverseas + "   •   RTM " + me.RtmCards;
            string blocker = A.BidBlocker(me);
            bool canBid = blocker == null && (_state == State.Bidding || _state == State.Reveal);
            _bidButton.interactable = canBid;
            _bidText.text = "BID " + AuctionRules.Money(A.NextAmount);
            _bidSub.text = blocker ?? (A.UserPassed ? "You passed - bid to come back in" : "Max you can bid " + AuctionRules.Money(A.MaxAllowed(me)));

            // Franchise strip.
            foreach (var (purse, count, bg, id) in _chips)
            {
                var f = A.Franchises[id];
                purse.text = AuctionRules.Money(f.Purse).Replace(" Cr", "Cr");
                purse.color = id == A.UserId ? Pal.Green : Pal.Text;
                count.text = f.Size + "/" + AuctionRules.MaxSquad + "  OS " + f.OverseasCount;
                var border = id == A.Leader ? Pal.Gold : (id == A.UserId ? Pal.Green.WithA(0.6f) : Pal.Stroke);
                bg.color = border;
            }
            SetStatus(null);
        }
    }
}
