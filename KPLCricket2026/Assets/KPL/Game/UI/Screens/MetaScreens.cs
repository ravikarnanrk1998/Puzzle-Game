using System;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Pick the playing XI and batting order, choose a captain and train players.</summary>
    public static class SquadScreen
    {
        static List<int> _order;
        static int _captain;
        static string _team;

        public static void Show()
        {
            var save = SaveSystem.Data;
            var team = Teams.ById(save.Fav);
            var squad = PlayerDb.Squad(team.Id);
            if (_team != team.Id || _order == null)
            {
                var sheet = Matches.UserSheet(team);
                _order = sheet.XI.Select(p => p.Id).ToList();
                _captain = sheet.CaptainId;
                _team = team.Id;
            }
            var page = ScreenKit.Page("Squad", "SQUAD", team.Name + "  •  pick 11, set the order, train your players", MainMenu.Show);
            var xi = _order.Select(id => squad.First(p => p.Id == id)).ToList();
            var boosted = Economy.ApplyUserBoosts(TeamSheet.Create(team, xi), save);
            int overseas = xi.Count(p => p.Overseas), bowlers = xi.Count(p => p.CanBowl), keepers = xi.Count(p => p.IsKeeper);

            // Left: playing XI in batting order.
            var left = Ui.Card(page, "XI", Pal.Accent.A(0.5f));
            left.rectTransform.At(0, 0, 24, 20, 600, 560);
            ScreenKit.Section(left.transform, "PLAYING XI  •  BATTING ORDER", 18, -16, 400);
            var status = Ui.Label(left.transform, "OVR " + boosted.Overall + "  •  OVERSEAS " + overseas + "/4  •  BOWLERS " + bowlers, 15, overseas > 4 || bowlers < 5 || keepers == 0 ? Pal.Red : Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
            status.rectTransform.At(1, 1, -18, -16, 330, 24);
            var list = Ui.ScrollList(left.transform, 4);
            ((RectTransform)list.parent).Fill(12, 50, 12, 12);
            for (int i = 0; i < xi.Count; i++)
            {
                var p = xi[i];
                int idx = i;
                var row = Ui.Row(list, 42);
                var bg = Ui.Panel(row, new Color(1, 1, 1, 0.04f), 10, "Bg");
                bg.rectTransform.Fill();
                var n = Ui.Label(row, (i + 1) + ".  " + p.Name + (p.Id == _captain ? "  (c)" : "") + (p.IsKeeper ? "  †" : "") + (p.Overseas ? "  ✈" : ""), 18, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                n.rectTransform.At(0, 0.5f, 12, 0, 330, 30);
                var r = Ui.Label(row, p.Role.Label().ToUpperInvariant() + "  " + Rating(p, save), 14, Pal.Dim, TextAnchor.MiddleRight, Font.Bold);
                r.rectTransform.At(1, 0.5f, -150, 0, 160, 30);
                // Chevrons rotate about their centre, so anchor them by the centre too.
                var up = Ui.IconBtn(row, Icon.Back, () => Move(idx, -1), 36);
                up.Rt().At(1, 0.5f, -104, 0, 36, 36);
                up.Rt().pivot = new Vector2(0.5f, 0.5f);
                up.Rt().anchoredPosition = new Vector2(-122, 0);
                up.Rt().localRotation = Quaternion.Euler(0, 0, -90);
                var dn = Ui.IconBtn(row, Icon.Back, () => Move(idx, 1), 36);
                dn.Rt().At(1, 0.5f, -62, 0, 36, 36);
                dn.Rt().pivot = new Vector2(0.5f, 0.5f);
                dn.Rt().anchoredPosition = new Vector2(-80, 0);
                dn.Rt().localRotation = Quaternion.Euler(0, 0, 90);
                var cap = Ui.IconBtn(row, Icon.Shield, () => { _captain = p.Id; Commit(); }, 36, p.Id == _captain ? Pal.Gold : Pal.Dim);
                cap.Rt().At(1, 0.5f, -20, 0, 36, 36);
            }

            // Right: full squad with in/out toggles and training.
            var right = Ui.Card(page, "SquadList");
            right.rectTransform.At(1, 0, -24, 20, 620, 560);
            ScreenKit.Section(right.transform, "FULL SQUAD (16)  •  TAP + / - TO PICK", 18, -16, 400);
            var reset = Ui.Btn(right.transform, "AUTO PICK", BtnStyle.Ghost, () =>
            {
                _order = TeamSheet.AutoXI(squad).Select(p => p.Id).ToList();
                Commit();
            }, 15);
            reset.Rt().At(1, 1, -16, -12, 130, 34);
            var list2 = Ui.ScrollList(right.transform, 4);
            ((RectTransform)list2.parent).Fill(12, 50, 12, 12);
            foreach (var p in squad.OrderBy(p => p.Role).ThenByDescending(p => p.Overall))
            {
                bool inXi = _order.Contains(p.Id);
                int lvl = save.Level(p.Id);
                var row = Ui.Row(list2, 56);
                var bg = Ui.Panel(row, inXi ? Pal.Accent.A(0.14f) : new Color(1, 1, 1, 0.04f), 10, "Bg");
                bg.rectTransform.Fill();
                var tog = Ui.IconBtn(row, inXi ? Icon.Cross : Icon.Plus, () => Toggle(p), 38, inXi ? Pal.Red : Pal.Green);
                tog.Rt().At(0, 0.5f, 8, 0, 38, 38);
                var n = Ui.Label(row, p.Name + (p.Overseas ? "  ✈" : ""), 18, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                n.rectTransform.At(0, 0.5f, 54, 9, 300, 24);
                var meta = Ui.Label(row, p.Role.Label() + (p.CanBowl ? " • " + p.Bowl.Label() : "") + (p.LeftBat ? " • LHB" : "") + "  •  BAT " + (p.Bat + Economy.TrainingBoost(p, lvl).bat) + "  BOWL " + (p.BowlSkill + Economy.TrainingBoost(p, lvl).bowl), 13, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                meta.rectTransform.At(0, 0.5f, 54, -12, 360, 20);
                for (int s = 0; s < Economy.MaxTraining; s++)
                {
                    var pip = Ui.Img(row, Sprites.Round(3), s < lvl ? Pal.Gold : new Color(1, 1, 1, 0.12f), "Pip");
                    pip.rectTransform.At(1, 0.5f, -150 + s * 12 - 60, 16, 9, 9);
                }
                int cost = Economy.TrainingCost(lvl);
                if (lvl < Economy.MaxTraining)
                {
                    var tr = Ui.Btn(row, Ui.Coins(cost), save.Coins >= cost ? BtnStyle.Gold : BtnStyle.Secondary, () =>
                    {
                        if (Economy.Train(save, p))
                        {
                            Audio.Play(Sfx.Coin);
                            Popup.Toast(p.Name + " trained to level " + save.Level(p.Id), Icon.Bolt, Pal.Green);
                            Achievements.Check(save);
                            SaveSystem.Save();
                            Show();
                        }
                        else
                        {
                            Audio.Play(Sfx.Error);
                            Popup.Toast("Not enough coins", Icon.Coin, Pal.Red);
                        }
                    }, 15, Icon.Bolt, "TRAIN");
                    tr.Rt().At(1, 0.5f, -8, 0, 132, 46);
                }
                else
                {
                    var mx = Ui.Label(row, "MAXED", 15, Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
                    mx.rectTransform.At(1, 0.5f, -14, 0, 100, 30);
                }
            }
        }

        static string Rating(Player p, SaveData s)
        {
            var b = Economy.TrainingBoost(p, s.Level(p.Id));
            int ovr = p.Overall + (b.bat + b.bowl) / 2;
            return ovr.ToString();
        }

        static void Move(int idx, int d)
        {
            int j = idx + d;
            if (j < 0 || j >= _order.Count) return;
            (_order[idx], _order[j]) = (_order[j], _order[idx]);
            Commit();
        }

        static void Toggle(Player p)
        {
            if (_order.Contains(p.Id))
            {
                if (_order.Count <= 1) return;
                _order.Remove(p.Id);
                Popup.Toast("Pick one more player for the XI", Icon.Users);
                Show();
                return;
            }
            if (_order.Count >= 11)
            {
                Popup.Toast("The XI is full - remove someone first", Icon.Users, Pal.Red);
                return;
            }
            var squad = PlayerDb.Squad(_team);
            if (p.Overseas && _order.Count(id => squad.First(x => x.Id == id).Overseas) >= League.MaxOverseasXI)
            {
                Popup.Toast("Maximum four overseas players", Icon.Users, Pal.Red);
                return;
            }
            _order.Add(p.Id);
            Commit();
        }

        static void Commit()
        {
            var save = SaveSystem.Data;
            if (_order.Count == 11)
            {
                var squad = PlayerDb.Squad(_team);
                var xi = _order.Select(id => squad.First(p => p.Id == id)).ToList();
                if (!xi.Any(p => p.IsKeeper)) Popup.Toast("No wicketkeeper picked - someone else will keep", Icon.Info, Pal.Gold);
                if (!_order.Contains(_captain)) _captain = _order[0];
                var pick = save.Squad(_team);
                if (pick == null)
                {
                    pick = new SquadPick { Team = _team };
                    save.Squads.Add(pick);
                }
                pick.Order = _order.ToList();
                pick.Captain = _captain;
                SaveSystem.Save();
            }
            Show();
        }
    }

    public static class ShopScreen
    {
        static int _tab;

        public static void Show(int tab)
        {
            _tab = tab;
            var save = SaveSystem.Data;
            var page = ScreenKit.Page("Shop", "SHOP", "Equipment and coaching to sharpen your game", MainMenu.Show);
            var tabs = Ui.Segments(page, new[] { "BATS", "SPIKES", "FIELDING", "COACHES" }, _tab, i => Show(i), 20);
            tabs.At(0.5f, 1, 0, -92, 760, 50);
            if (_tab == 3)
            {
                Coaches(page, save);
                return;
            }
            var kind = _tab == 0 ? ItemKind.Bat : _tab == 1 ? ItemKind.Spikes : ItemKind.Gloves;
            var items = Shop.Items.Where(i => i.Kind == kind).ToList();
            var strip = Ui.HScroll(page, 16);
            ((RectTransform)strip.parent).Fill(30, 160, 30, 30);
            foreach (var it in items)
            {
                var item = it;
                bool owned = save.Owned.Contains(it.Id);
                bool equipped = save.Bat == it.Id || save.Spikes == it.Id || save.Gloves == it.Id;
                var card = Ui.Col(strip, 270);
                var bg = Ui.Card(card, "Bg", equipped ? Pal.Gold : owned ? Pal.Teal.A(0.6f) : Pal.Stroke);
                bg.rectTransform.Fill();
                var tier = Ui.Label(card, "TIER " + it.Tier, 14, Pal.Dim, TextAnchor.MiddleLeft, Font.Bold);
                tier.rectTransform.At(0, 1, 18, -14, 120, 20);
                var art = Ui.Img(card, Sprites.Circle, Color.Lerp(Pal.Panel3, Pal.Gold, it.Tier / 5f).A(0.6f), "Art");
                art.rectTransform.At(0.5f, 1, 0, -40, 130, 130);
                var ic = Ui.IconImg(art.transform, kind == ItemKind.Bat ? Icon.Bat : kind == ItemKind.Spikes ? Icon.Bolt : Icon.Shield, Color.white, 80);
                ic.rectTransform.At(0.5f, 0.5f, 0, 0, 80, 80);
                var n = Ui.Label(card, it.Name.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, Font.Display);
                n.rectTransform.At(0.5f, 1, 0, -182, 260, 32);
                var b = Ui.Para(card, it.Blurb, 15, Pal.Dim, TextAnchor.UpperCenter, Font.Semi);
                b.rectTransform.At(0.5f, 1, 0, -216, 240, 44);
                string stat = kind == ItemKind.Bat ? "+" + (it.Timing * 100).ToString("0") + "% timing  •  +" + it.Power + " power"
                    : kind == ItemKind.Spikes ? "+" + (it.Accuracy * 100).ToString("0") + " accuracy" : "+" + (it.Catch * 100).ToString("0") + "% catching";
                var s = Ui.Label(card, it.Tier == 0 ? "Starter" : stat, 16, Pal.Gold, TextAnchor.MiddleCenter, Font.Bold);
                s.rectTransform.At(0.5f, 0, 0, 92, 260, 22);
                Button btn;
                if (equipped)
                {
                    btn = Ui.Btn(card, "EQUIPPED", BtnStyle.Ghost, () => { }, 20, Icon.Check);
                }
                else if (owned)
                {
                    btn = Ui.Btn(card, "EQUIP", BtnStyle.Teal, () =>
                    {
                        Shop.Equip(save, item.Id);
                        SaveSystem.Save();
                        Audio.Play(Sfx.Confirm);
                        Show(_tab);
                    }, 22);
                }
                else
                {
                    btn = Ui.Btn(card, Ui.Coins(it.Price), save.Coins >= it.Price ? BtnStyle.Gold : BtnStyle.Secondary, () => Buy(item), 22, Icon.Coin, "BUY");
                }
                btn.Rt().At(0.5f, 0, 0, 20, 230, 62);
            }
        }

        static void Buy(ShopItem it)
        {
            var save = SaveSystem.Data;
            if (save.Coins < it.Price)
            {
                Audio.Play(Sfx.Error);
                Popup.Message("NOT ENOUGH COINS", "You need " + Ui.Coins(it.Price - save.Coins) + " more coins. Win matches, clear challenges and claim daily rewards to earn more.");
                return;
            }
            Popup.Confirm("BUY " + it.Name.ToUpperInvariant() + "?", "Spend " + Ui.Coins(it.Price) + " coins. It will be equipped straight away.", "BUY", () =>
            {
                var r = Shop.Buy(save, it.Id);
                if (r == Shop.BuyResult.Ok)
                {
                    Audio.Play(Sfx.Coin);
                    Achievements.Check(save);
                    SaveSystem.Save();
                    Popup.Toast(it.Name + " purchased and equipped", Icon.Check, Pal.Green);
                }
                else Popup.Toast("Purchase failed: " + r, Icon.Cross, Pal.Red);
                Show(_tab);
            });
        }

        static void Coaches(RectTransform page, SaveData save)
        {
            var names = new[] { ("BATTING COACH", "+1 batting and power per level for your whole squad", Icon.Bat, Pal.Teal), ("BOWLING COACH", "+1 bowling skill and sharper accuracy per level", Icon.Ball, Pal.Accent), ("FIELDING COACH", "+2 fielding and better catching per level", Icon.Users, Pal.Gold) };
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                var (title, desc, icon, c) = names[i];
                int lvl = i == 0 ? save.CoachBat : i == 1 ? save.CoachBowl : save.CoachField;
                var card = Ui.Card(page, title, c.A(0.6f));
                card.rectTransform.At(0.5f, 0.5f, (i - 1) * 410, -60, 390, 420);
                var art = Ui.Img(card.transform, Sprites.Circle, c, "Art");
                art.rectTransform.At(0.5f, 1, 0, -28, 110, 110);
                var ic = Ui.IconImg(art.transform, icon, Color.white, 64);
                ic.rectTransform.At(0.5f, 0.5f, 0, 0, 64, 64);
                var t = Ui.Label(card.transform, title, 30, Color.white, TextAnchor.MiddleCenter, Font.Display);
                t.rectTransform.At(0.5f, 1, 0, -150, 380, 36);
                var d = Ui.Para(card.transform, desc, 17, Pal.Dim, TextAnchor.UpperCenter, Font.Semi);
                d.rectTransform.At(0.5f, 1, 0, -192, 330, 50);
                for (int s = 0; s < Economy.MaxCoach; s++)
                {
                    var pip = Ui.Img(card.transform, Sprites.Round(6), s < lvl ? c : new Color(1, 1, 1, 0.12f), "Pip");
                    pip.rectTransform.At(0.5f, 1, (s - 2) * 44, -260, 36, 14);
                }
                var lv = Ui.Label(card.transform, "LEVEL " + lvl + " / " + Economy.MaxCoach, 18, Pal.Gold, TextAnchor.MiddleCenter, Font.Bold);
                lv.rectTransform.At(0.5f, 1, 0, -284, 300, 24);
                if (lvl < Economy.MaxCoach)
                {
                    int cost = Economy.CoachCost(lvl);
                    var b = Ui.Btn(card.transform, Ui.Coins(cost), save.Coins >= cost ? BtnStyle.Gold : BtnStyle.Secondary, () =>
                    {
                        if (Economy.BuyCoach(save, which))
                        {
                            Audio.Play(Sfx.Coin);
                            Achievements.Check(save);
                            SaveSystem.Save();
                            Popup.Toast(title + " upgraded to level " + (lvl + 1), Icon.Check, Pal.Green);
                        }
                        else
                        {
                            Audio.Play(Sfx.Error);
                            Popup.Toast("Not enough coins", Icon.Coin, Pal.Red);
                        }
                        Show(3);
                    }, 22, Icon.Coin, "UPGRADE");
                    b.Rt().At(0.5f, 0, 0, 22, 260, 64);
                }
                else
                {
                    var mx = Ui.Label(card.transform, "MAXED OUT", 26, Pal.Gold, TextAnchor.MiddleCenter, Font.Display);
                    mx.rectTransform.At(0.5f, 0, 0, 36, 300, 36);
                }
            }
        }
    }

    public static class SettingsScreen
    {
        public static void Show()
        {
            var save = SaveSystem.Data;
            var s = save.Settings;
            var page = ScreenKit.Page("Settings", "SETTINGS", "Graphics, controls, audio and data", MainMenu.Show, 0.65f);
            var card = Ui.Card(page, "Card");
            card.rectTransform.Fill(30, 96, 30, 20);
            var list = Ui.ScrollList(card.transform, 6, new RectOffset(0, 0, 8, 20));
            ((RectTransform)list.parent).Fill(16, 10, 16, 10);
            void Row(string label, string help, string[] options, int sel, Action<int> pick)
            {
                var row = Ui.Row(list, 70);
                var l = Ui.Label(row, label, 22, Color.white, TextAnchor.UpperLeft, Font.Bold);
                l.rectTransform.At(0, 1, 14, -8, 420, 28);
                var h = Ui.Label(row, help, 14, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
                h.rectTransform.At(0, 1, 14, -38, 560, 20);
                var seg = Ui.Segments(row, options, sel, i =>
                {
                    pick(i);
                    SaveSystem.MarkDirty();
                }, 17);
                seg.At(1, 0.5f, -14, 0, Mathf.Min(560, 140 * options.Length), 44);
            }
            void Slider(string label, string help, float v, Action<float> set)
            {
                var row = Ui.Row(list, 70);
                var l = Ui.Label(row, label, 22, Color.white, TextAnchor.UpperLeft, Font.Bold);
                l.rectTransform.At(0, 1, 14, -8, 420, 28);
                var h = Ui.Label(row, help, 14, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
                h.rectTransform.At(0, 1, 14, -38, 560, 20);
                var sl = Ui.SliderRow(row, v, x =>
                {
                    set(x);
                    SaveSystem.MarkDirty();
                });
                sl.GetComponent<RectTransform>().At(1, 0.5f, -14, 0, 420, 40);
            }
            void Header(string text)
            {
                var row = Ui.Row(list, 40);
                ScreenKit.Section(row, text, 6, -10, 600);
            }
            Header("GRAPHICS");
            Row("GRAPHICS QUALITY", "Auto-detected: " + Quality.Names[(int)Quality.Detect()] + "  •  ULTRA adds 4x MSAA, 4K shadows and ambient occlusion", Quality.Names, (int)Quality.Current, i =>
            {
                s.Graphics = i;
                Quality.Apply((Gfx)i);
                Popup.Toast("Graphics set to " + Quality.Names[i] + " - stadium textures update on the next load", Icon.Eye);
            });
            Row("FRAME RATE", "30 FPS saves battery and heat  •  60 FPS is smoother", new[] { "30 FPS", "60 FPS" }, s.Fps60 ? 1 : 0, i =>
            {
                s.Fps60 = i == 1;
                Quality.ApplyFrameRate(false);
            });
            Row("BALL TRAIL", "A faint streak behind the ball", new[] { "ON", "OFF" }, s.BallTrail ? 0 : 1, i => s.BallTrail = i == 0);
            Row("MATCH TIME", "Auto picks day or night from your clock", new[] { "AUTO", "DAY", "NIGHT" }, s.MatchTime, i => s.MatchTime = i);
            Header("CONTROLS & GAMEPLAY");
            Row("BATTING CONTROLS", "Stick + shot buttons, or swipe the shot direction", new[] { "BUTTONS", "SWIPE" }, s.Controls, i => s.Controls = i);
            Row("LAYOUT", "Left-handed puts the stick on the right", new[] { "RIGHT-HANDED", "LEFT-HANDED" }, s.LeftHanded ? 1 : 0, i => s.LeftHanded = i == 1);
            Row("DIFFICULTY", "Timing windows, ball speed and AI skill", new[] { "EASY", "MEDIUM", "HARD", "PRO" }, s.Difficulty, i => s.Difficulty = i);
            Row("CAMERA", "Broadcast view from behind the bowler, or low behind the batter", new[] { "BROADCAST", "BATTER" }, s.Camera, i => s.Camera = i);
            Row("TIMING ASSIST", "Bounce marker and timing meter when batting", new[] { "ON", "OFF" }, s.TimingAssist ? 0 : 1, i => s.TimingAssist = i == 0);
            Row("CATCH PROMPTS", "Tap to catch when your side is fielding", new[] { "ON", "OFF" }, s.CatchPrompts ? 0 : 1, i => s.CatchPrompts = i == 0);
            Slider("TIMING CALIBRATION  (" + s.TimingOffset + " ms)", "If your shots always feel late, slide left; early, slide right", (s.TimingOffset + 120) / 240f, v =>
            {
                s.TimingOffset = Mathf.RoundToInt(v * 240 - 120);
            });
            Header("AUDIO");
            Slider("MUSIC", "Menu music volume", s.Music, v =>
            {
                s.Music = v;
                Audio.I?.ApplyVolumes();
            });
            Slider("SOUND EFFECTS", "Bat, crowd and interface sounds", s.Sfx, v => s.Sfx = v);
            Row("COMMENTARY", "Ball-by-ball commentary text", new[] { "ON", "OFF" }, s.Commentary ? 0 : 1, i => s.Commentary = i == 0);
            Row("VIBRATION", "Buzz on shots and wickets", new[] { "ON", "OFF" }, s.Vibrate ? 0 : 1, i => s.Vibrate = i == 0);
            Header("DATA");
            var data = Ui.Row(list, 80);
            var team = Ui.Btn(data, "CHANGE TEAM", BtnStyle.Secondary, () => TeamSelectScreen.Show(false), 20, Icon.Shield);
            team.Rt().At(0, 0.5f, 14, 0, 260, 60);
            var reset = Ui.Btn(data, "RESET PROGRESS", BtnStyle.Danger, () => Popup.Confirm("Reset all progress?", "Coins, stats, challenges, tournaments and purchases will be deleted. Settings are kept. This cannot be undone.", "RESET", () =>
            {
                SaveSystem.ResetAll();
                TeamSelectScreen.Show(true);
            }), 20, Icon.Refresh);
            reset.Rt().At(0, 0.5f, 290, 0, 280, 60);
            var about = Ui.Row(list, 90);
            var at = Ui.Para(about, "KPL Cricket 2026 v" + Application.version + "  •  Fictional teams, players and league - not affiliated with any real league, board, team or player.\nAll progress is stored only on this device. No account, ads or data collection.", 14, Pal.Dim);
            at.rectTransform.Fill(14, 6, 14, 6);
        }
    }

    public static class StatsScreen
    {
        public static void Show(int tab)
        {
            var save = SaveSystem.Data;
            var page = ScreenKit.Page("Stats", "STATS", "Your career across every mode", MainMenu.Show);
            var tabs = Ui.Segments(page, new[] { "CAREER", "ACHIEVEMENTS (" + Achievements.Claimable(save) + ")" }, tab, i => Show(i), 20);
            tabs.At(0.5f, 1, 0, -92, 640, 50);
            if (tab == 1)
            {
                AchievementsList(page, save);
                return;
            }
            var st = save.Stats;
            void Block(string title, Color c, float x, (string, string)[] rows)
            {
                var card = Ui.Card(page, title, c.A(0.5f));
                card.rectTransform.At(0.5f, 1, x, -160, 395, 460);
                var t = Ui.Label(card.transform, title, 28, c, TextAnchor.MiddleLeft, Font.Display);
                t.rectTransform.TopStrip(14, 34, 20, 20);
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = Ui.Node(card.transform, "R").TopStrip(60 + i * 38, 34, 20, 20);
                    if (i % 2 == 0)
                    {
                        var bg = Ui.Panel(row, new Color(1, 1, 1, 0.04f), 8, "Bg");
                        bg.rectTransform.Fill();
                    }
                    var k = Ui.Label(row, rows[i].Item1, 18, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                    k.rectTransform.Fill(10, 0, 120, 0);
                    var v = Ui.Label(row, rows[i].Item2, 22, Color.white, TextAnchor.MiddleRight, Font.Display);
                    v.rectTransform.Fill(160, 0, 10, 0);
                }
            }
            Block("RESULTS", Pal.Accent, -410, new[]
            {
                ("Matches", st.Matches.ToString()), ("Won", st.Wins.ToString()), ("Lost", st.Losses.ToString()), ("Tied", st.Ties.ToString()), ("Win %", st.WinPct.ToString("0")),
                ("Chases won", st.ChasesWon.ToString()), ("Totals defended", st.DefendsWon.ToString()), ("KPL titles", st.TourWon.ToString()), ("Playoffs", st.Playoffs.ToString()), ("Challenge stars", save.TotalStars + " / 150"),
            });
            Block("BATTING", Pal.Teal, 0, new[]
            {
                ("Runs", st.Runs.ToString("N0")), ("Average", st.BatAverage.ToString("0.0")), ("Strike rate", st.StrikeRate.ToString("0.0")), ("Highest score", st.HighScore.ToString()), ("50s / 100s", st.Fifties + " / " + st.Hundreds),
                ("Fours", st.Fours.ToString()), ("Sixes", st.Sixes.ToString()), ("Biggest six", st.BiggestSix > 0 ? st.BiggestSix + " m" : "-"), ("Highest total", st.HighestTotal.ToString()), ("Ducks", st.Ducks.ToString()),
            });
            Block("BOWLING & FIELDING", Pal.Gold, 410, new[]
            {
                ("Wickets", st.Wkts.ToString()), ("Economy", st.Economy.ToString("0.00")), ("Best figures", st.Best), ("Five-fors", st.FiveFors.ToString()), ("Hat-tricks", st.HatTricks.ToString()),
                ("Maidens", st.Maidens.ToString()), ("Overs bowled", Overs.Text(st.BallsBowled)), ("Catches (prompts)", st.Catches.ToString()), ("Run outs", st.RunOuts.ToString()), ("Coins earned", save.CoinsEarned.ToString("N0")),
            });
        }

        static void AchievementsList(RectTransform page, SaveData save)
        {
            var card = Ui.Card(page, "Ach");
            card.rectTransform.Fill(30, 160, 30, 20);
            var list = Ui.ScrollList(card.transform, 6);
            ((RectTransform)list.parent).Fill(14, 14, 14, 14);
            foreach (var a in Achievements.All.OrderBy(a => save.Ach.First(x => x.Id == a.Id).Claimed).ThenByDescending(a => save.Ach.First(x => x.Id == a.Id).Unlocked))
            {
                var st = save.Ach.First(x => x.Id == a.Id);
                int prog = Mathf.Min(a.Target, a.Progress(save));
                var row = Ui.Row(list, 70);
                var bg = Ui.Panel(row, st.Unlocked && !st.Claimed ? Pal.Gold.A(0.16f) : new Color(1, 1, 1, 0.04f), 12, "Bg", st.Unlocked && !st.Claimed ? Pal.Gold.A(0.6f) : (Color?)null);
                bg.rectTransform.Fill();
                var ic = Ui.IconImg(row, st.Unlocked ? Icon.Medal : Icon.Lock, st.Unlocked ? Pal.Gold : Pal.Mute, 40);
                ic.rectTransform.At(0, 0.5f, 16, 0, 40, 40);
                var t = Ui.Label(row, a.Title.ToUpperInvariant(), 22, Color.white, TextAnchor.MiddleLeft, Font.Display);
                t.rectTransform.At(0, 0.5f, 72, 12, 500, 28);
                var d = Ui.Label(row, a.Desc + "   (" + prog + " / " + a.Target + ")", 15, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                d.rectTransform.At(0, 0.5f, 72, -14, 600, 22);
                var barBg = Ui.Img(row, Sprites.Round(4), new Color(1, 1, 1, 0.1f), "Bar");
                barBg.rectTransform.At(1, 0.5f, -230, -18, 200, 8);
                var fill = Ui.Img(barBg.transform, Sprites.Round(4), Pal.Gold, "Fill");
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(prog / (float)a.Target, 1);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                if (st.Unlocked && !st.Claimed)
                {
                    var claim = Ui.Btn(row, "+" + Ui.Coins(a.Reward), BtnStyle.Gold, () =>
                    {
                        if (Achievements.Claim(save, a.Id))
                        {
                            Audio.Play(Sfx.Coin);
                            SaveSystem.Save();
                            Popup.Toast("+" + a.Reward + " coins", Icon.Coin, Pal.Gold);
                        }
                        Show(1);
                    }, 18, Icon.Coin, "CLAIM");
                    claim.Rt().At(1, 0.5f, -14, 8, 180, 52);
                }
                else
                {
                    var r = Ui.Label(row, st.Claimed ? "CLAIMED" : "+" + Ui.Coins(a.Reward), 18, st.Claimed ? Pal.Green : Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
                    r.rectTransform.At(1, 0.5f, -20, 12, 180, 26);
                }
            }
        }
    }

    public static class DailyPopup
    {
        public static void Show()
        {
            var save = SaveSystem.Data;
            var now = DateTime.Now;
            bool avail = Economy.DailyAvailable(save, now);
            int slot = Economy.DailySlot(save, now);
            int claimedToday = avail ? -1 : (save.DailyStreak - 1) % 7;
            var c = Popup.Open("DAILY REWARDS", 900, 420, true, Pal.Gold.A(0.6f));
            var sub = Ui.Label(c, avail ? "Come back every day - the streak resets if you miss one." : "Claimed today. See you tomorrow!", 18, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
            sub.rectTransform.TopStrip(60, 24, 28, 28);
            for (int i = 0; i < 7; i++)
            {
                bool done = avail ? i < slot : i <= claimedToday;
                bool today = avail && i == slot;
                var d = Ui.Panel(c, today ? Pal.Gold.A(0.25f) : done ? Pal.Green.A(0.12f) : new Color(1, 1, 1, 0.05f), 16, "Day" + i, today ? Pal.Gold : done ? Pal.Green.A(0.6f) : (Color?)null);
                d.rectTransform.At(0, 1, 28 + i * 122, -100, 112, 170);
                var t = Ui.Label(d.transform, "DAY " + (i + 1), 18, today ? Pal.Gold : Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                t.rectTransform.At(0.5f, 1, 0, -10, 110, 22);
                var ic = Ui.IconImg(d.transform, i == 6 ? Icon.Gift : Icon.Coin, done ? Pal.Green : Pal.Gold, i == 6 ? 56 : 46);
                ic.rectTransform.At(0.5f, 0.5f, 0, 6, 56, 56);
                var v = Ui.Label(d.transform, done ? "✓" : Ui.Coins(Economy.DailyRewards[i]), 22, done ? Pal.Green : Color.white, TextAnchor.MiddleCenter, Font.Display);
                v.rectTransform.At(0.5f, 0, 0, 12, 110, 28);
            }
            var root = c.parent.gameObject;
            if (avail)
            {
                var claim = Ui.Btn(c, "CLAIM " + Ui.Coins(Economy.DailyRewards[slot]), BtnStyle.Gold, () =>
                {
                    int got = Economy.ClaimDaily(save, DateTime.Now);
                    Audio.Play(Sfx.Coin);
                    Achievements.Check(save);
                    SaveSystem.Save();
                    Popup.Close(root);
                    Popup.Toast("+" + got + " coins  •  streak " + save.DailyStreak + " day" + (save.DailyStreak > 1 ? "s" : ""), Icon.Coin, Pal.Gold);
                    if (Nav.Current == "Main") MainMenu.Show();
                }, 26, Icon.Coin);
                claim.Rt().At(0.5f, 0, 0, 26, 340, 70);
                claim.gameObject.AddComponent<Pulse>().Amount = 0.03f;
            }
        }
    }
}
