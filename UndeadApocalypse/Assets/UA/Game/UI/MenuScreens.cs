using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    // ======================================================================= main menu

    public sealed class MainMenuScreen : UiScreen
    {
        public override string Title => "";
        public override float Backdrop => 0;
        Text _missionsBadge;
        UiButton _freeCoins;

        public override void Build(RectTransform root)
        {
            var d = SaveSystem.Data;
            // Title.
            // Left scrim: darkens the 3D backdrop behind the logo and mode cards so they read over bright lights.
            var scrim = Ui.Img(root, Sprites.HGradient, new Color(0, 0, 0, 0.7f), "LogoScrim");
            var sr = scrim.rectTransform;
            sr.anchorMin = new Vector2(0, 0);
            sr.anchorMax = new Vector2(0, 1);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(1150, 0);
            sr.anchoredPosition = new Vector2(575, 0);
            sr.localRotation = Quaternion.Euler(0, 0, 180); // HGradient is clear on the left; flip it so it is dark there.
            // Logo: stencil title face, red underline bar and spaced subtitle.
            var t1 = Ui.Title(root, "UNDEAD", 132, Pal.Text);
            t1.rectTransform.At(0, 1, 380, -86, 700, 150);
            Ui.Shadow(t1, 5, 0.9f);
            var t2 = Ui.Title(root, "APOCALYPSE", 74, Pal.Blood);
            t2.rectTransform.At(0, 1, 380, -186, 700, 90);
            Ui.Shadow(t2, 4, 0.9f);
            var bar = Ui.Img(root, Sprites.White, Pal.Blood.A(0.9f), "LogoBar");
            bar.rectTransform.At(0, 1, 380, -236, 700, 4);
            // Subtitle on a solid tag hanging from the bar: readable over any part of the 3D backdrop.
            var tag = Ui.Img(root, Sprites.White, Pal.Blood, "LogoTag");
            tag.rectTransform.At(0, 1, 200, -258, 340, 42);
            var t3 = Ui.Label(tag.transform, "I N F I N I T E   W A R", 30, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            t3.rectTransform.Fill();
            // Mode cards.
            var plan = StageGen.Survival(d.Stage, (ulong)d.WorldSeed);
            Card(root, 0, "INFINITE SURVIVAL", "STAGE " + d.Stage.ToString("#,0") + "  -  " + plan.BiomeDef.Name, Icon.Skull, Pal.Accent, () => Screens.Show(new SurvivalScreen()), true);
            int camp = Mathf.Min(d.CampaignCleared, Campaign.All.Length - 1);
            Card(root, 1, "CAMPAIGN", d.CampaignCleared >= Campaign.All.Length ? "OPERATION LAST LIGHT COMPLETE" : "MISSION " + (camp + 1) + ": " + Campaign.All[camp].Title, Icon.Map, Pal.Panel2, () => Screens.Show(new CampaignScreen()), false);
            bool dailyDone = d.DailyDone == Clock.Today;
            Card(root, 2, "DAILY CHALLENGE", dailyDone ? "COMPLETED - NEW IN " + Clock.Countdown(Clock.UntilTomorrow) : "x" + DailyChallenge.Plan(Clock.Today, d.HighestStage).RewardMul.ToString("0.#") + " REWARDS TODAY", Icon.Calendar, Pal.Panel2, () => Screens.Show(new DailyScreen()), false);
            Card(root, 3, "BOSS RUSH", "BEST ROUND " + d.BossRushBest, Icon.Boss, Pal.Panel2, () => Screens.Show(new BossRushScreen()), false);
            // Navigation (right).
            string[] names = { "WEAPONS", "UPGRADES", "STORE", "MISSIONS", "SETTINGS" };
            Icon[] icons = { Icon.Rifle, Icon.Skill, Icon.Cart, Icon.Trophy, Icon.Gear };
            Action[] acts =
            {
                () => Screens.Show(new ArmoryScreen()),
                () => Screens.Show(new UpgradesScreen()),
                () => Screens.Show(new StoreScreen()),
                () => Screens.Show(new MissionsScreen()),
                () => Screens.Show(new SettingsScreen()),
            };
            for (int i = 0; i < names.Length; i++)
            {
                var b = Ui.Button(root, names[i], acts[i], Pal.Panel.A(0.85f), 36, icons[i], null, true);
                b.Bg.rectTransform.At(1, 1, -230, -130 - i * 112, 380, 96);
                if (i == 3)
                {
                    var badge = Ui.Img(b.Bg.transform, Sprites.Circle, Pal.Bad, "Badge");
                    badge.rectTransform.At(1, 1, -6, -6, 44, 44);
                    _missionsBadge = Ui.Label(badge.transform, "", 26, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                    _missionsBadge.rectTransform.Fill();
                }
            }
            if (Ads.Configured)
            {
                _freeCoins = Ui.Button(root, "FREE 500 COINS", () => Ads.ShowRewarded("coins", ok =>
                {
                    if (!ok) { Screens.Toast("Ad not completed"); return; }
                    Ads.GrantFreeCoins();
                    Sfx.Buy();
                    Screens.RefreshTopBar();
                }), Pal.Good.Mul(0.8f), 30, Icon.Video);
                _freeCoins.Bg.rectTransform.At(1, 0, -230, 70, 380, 80);
            }
            // Bottom left: the owner's signature image (white on transparent) only, no text. Left edge lines up with
            // the mode cards; it fits in the space under the Boss Rush card. (The version is shown in Settings > About.)
            var sign = Resources.Load<Sprite>("UI/MenuSignature");
            if (sign != null)
            {
                const float h = 44;
                float signW = h * sign.rect.width / sign.rect.height;
                var img = Ui.Img(root, sign, new Color(1, 1, 1, 0.92f), "Signature");
                img.preserveAspect = true;
                img.rectTransform.At(0, 0, 30 + signW / 2, 22 + h / 2, signW, h);
                Ui.Shadow(img, 2, 0.6f);
            }
        }

        static void Card(RectTransform root, int i, string title, string sub, Icon icon, Color bg, Action onClick, bool hero)
        {
            float h = hero ? 150 : 112;
            float y = -350 - (hero ? 0 : 160 + (i - 1) * 126);
            var b = Ui.Button(root, "", onClick, bg.A(hero ? 0.92f : 0.85f), 30);
            b.Bg.rectTransform.At(0, 1, 380, y - h / 2 + 40, 700, h);
            if (!hero)
            {
                var o = Ui.Img(b.Bg.transform, Sprites.CutOutline(14, 2), Pal.Line, "Line");
                o.rectTransform.Fill();
            }
            var ic = Ui.Img(b.Bg.transform, Sprites.Of(icon), Color.white, "Icon");
            ic.rectTransform.At(0, 0.5f, hero ? 70 : 56, 0, hero ? 80 : 60, hero ? 80 : 60);
            var t = Ui.Label(b.Bg.transform, title, hero ? 58 : 40, Color.white, TextAnchor.UpperLeft, FontKind.Display);
            t.rectTransform.Fill(hero ? 130 : 108, hero ? 22 : 14, 60, 0);
            var s = Ui.Label(b.Bg.transform, sub, hero ? 28 : 24, hero ? Color.white.A(0.85f) : Pal.Dim, TextAnchor.LowerLeft, FontKind.Semi);
            s.rectTransform.Fill(hero ? 132 : 110, 0, 60, hero ? 22 : 14);
            var arrow = Ui.Img(b.Bg.transform, Sprites.Of(Icon.Play), Color.white.A(0.8f), "Arrow");
            arrow.rectTransform.At(1, 0.5f, -40, 0, 36, 36);
        }

        public override void OnShow()
        {
            Sfx.Music(MusicKind.Menu);
            var d = SaveSystem.Data;
            Missions.Refresh(d, Clock.Today);
            if (Missions.CanClaimLogin(d, Clock.Today) && !AutoPilot.Active) LoginPopup.Show();
        }

        public override void Tick(float dt)
        {
            if (_missionsBadge != null)
            {
                int n = Missions.Claimable(SaveSystem.Data) + (Missions.CanClaimLogin(SaveSystem.Data, Clock.Today) ? 1 : 0);
                _missionsBadge.transform.parent.gameObject.SetActive(n > 0);
                _missionsBadge.text = n.ToString();
            }
            if (_freeCoins != null) _freeCoins.SetInteractable(Ads.RewardedReady && Ads.FreeCoinsLeft > 0);
        }
    }

    // ======================================================================= survival

    public sealed class SurvivalScreen : UiScreen
    {
        public override string Title => "INFINITE SURVIVAL";
        long _stage;
        RectTransform _card;

        public override void Build(RectTransform root)
        {
            _stage = SaveSystem.Data.Stage;
            _card = Ui.Rect(root, "Card");
            _card.Fill();
            Rebuild();
        }

        void Rebuild()
        {
            Ui.Clear(_card);
            var d = SaveSystem.Data;
            var plan = StageGen.Survival(_stage, (ulong)d.WorldSeed);
            var left = Ui.Panel(_card, Pal.Panel.A(0.9f), "Left");
            left.rectTransform.At(0, 0.5f, 520, -10, 900, 820);
            var st = Ui.Title(left.transform, "STAGE " + _stage.ToString("#,0"), 110, Pal.Text);
            st.rectTransform.Fill(50, 30, 30, 0);
            var biome = Ui.Label(left.transform, plan.BiomeDef.Name, 44, Pal.Accent, TextAnchor.UpperLeft, FontKind.Display);
            biome.rectTransform.Fill(52, 160, 30, 0);
            var desc = Ui.Label(left.transform, plan.BiomeDef.Desc, 28, Pal.Dim, TextAnchor.UpperLeft, FontKind.Body);
            desc.rectTransform.Fill(52, 216, 30, 0);
            var lines = new List<string>
            {
                "<color=#FFB020>OBJECTIVE</color>   " + plan.ObjectiveText,
                "<color=#FFB020>CONDITIONS</color>   " + plan.Weather.ToString().ToUpper() + " / " + plan.Mood.ToString().ToUpper(),
            };
            if (plan.Event != StageEvent.None) lines.Add("<color=#FFB020>EVENT</color>   " + StageGen.EventName(plan.Event));
            var types = new HashSet<ZombieType>();
            foreach (var w in plan.Waves) foreach (var e in w.Spawns) types.Add(e.Type);
            var tn = new List<string>();
            foreach (var t in types) tn.Add(Bestiary.Get(t).Name);
            lines.Add("<color=#FFB020>INFECTED</color>   " + string.Join(", ", tn));
            lines.Add("<color=#FFB020>THREAT</color>   x" + plan.HpMul.ToString("0.0") + " health   x" + plan.DmgMul.ToString("0.0") + " damage");
            long nextElite = ((_stage / 25) + 1) * 25, nextBoss = ((_stage / 50) + 1) * 50;
            if (Difficulty.IsBoss(_stage)) lines.Add("<color=#E5322D>BOSS STAGE: " + Bestiary.Boss(plan.Boss).Name + "</color>");
            else if (Difficulty.IsElite(_stage)) lines.Add("<color=#E5322D>ELITE ENCOUNTER</color>");
            lines.Add("<color=#9AA3AD>Next elite: stage " + nextElite + "   Next boss: stage " + nextBoss + "</color>");
            var info = Ui.Label(left.transform, string.Join("\n", lines), 30, Pal.Text, TextAnchor.UpperLeft, FontKind.Semi);
            info.rectTransform.Fill(52, 290, 40, 120);
            info.lineSpacing = 1.25f;
            // Stage selector (replay any stage up to the highest reached).
            var prev10 = Ui.Button(left.transform, "-10", () => { _stage = Math.Max(1, _stage - 10); Rebuild(); }, Pal.Panel2, 30);
            prev10.Bg.rectTransform.At(0, 0, 100, 70, 110, 70);
            var prev = Ui.Button(left.transform, "-1", () => { _stage = Math.Max(1, _stage - 1); Rebuild(); }, Pal.Panel2, 30);
            prev.Bg.rectTransform.At(0, 0, 230, 70, 110, 70);
            var next = Ui.Button(left.transform, "+1", () => { _stage = Math.Min(d.HighestStage, _stage + 1); Rebuild(); }, Pal.Panel2, 30);
            next.Bg.rectTransform.At(0, 0, 360, 70, 110, 70);
            var cur = Ui.Button(left.transform, "LATEST", () => { _stage = d.HighestStage; Rebuild(); }, Pal.Panel2, 30);
            cur.Bg.rectTransform.At(0, 0, 520, 70, 170, 70);
            var hi = Ui.Label(left.transform, "HIGHEST " + d.HighestStage.ToString("#,0"), 26, Pal.Dim, TextAnchor.MiddleRight, FontKind.Semi);
            hi.rectTransform.At(1, 0, -150, 70, 260, 60);
            // Right: loadout and deploy.
            LoadoutPanel.Build(_card, new Vector2(-420, 120));
            var deploy = Ui.Button(_card, "DEPLOY", () => GameApp.Instance.PlaySurvival(_stage), Pal.Accent, 64, Icon.Play);
            deploy.Bg.rectTransform.At(1, 0, -420, 120, 600, 140);
        }
    }

    /// <summary>Shared loadout summary used on mode screens.</summary>
    public static class LoadoutPanel
    {
        public static void Build(RectTransform parent, Vector2 bottomRightOffset)
        {
            var d = SaveSystem.Data;
            var p = Ui.Panel(parent, Pal.Panel.A(0.9f), "Loadout");
            p.rectTransform.At(1, 0.5f, -420, 90, 600, 560);
            var t = Ui.Label(p.transform, "LOADOUT", 40, Pal.Accent2, TextAnchor.UpperLeft, FontKind.Display);
            t.rectTransform.Fill(36, 26, 20, 0);
            for (int i = 0; i < 3; i++)
            {
                bool locked = i >= d.LoadoutSlots;
                var row = Ui.Img(p.transform, Sprites.Cut(10), Pal.Panel2, "Slot");
                row.rectTransform.At(0.5f, 1, 0, -140 - i * 100, 530, 86);
                string name = locked ? "LOCKED SLOT" : d.Loadout[i] >= 0 ? Weapons.Get((WeaponId)d.Loadout[i]).Name.ToUpper() : "EMPTY";
                Image ic;
                if (!locked && d.Loadout[i] >= 0)
                {
                    ic = CardArt.WeaponIcon(row.transform, Weapons.Get((WeaponId)d.Loadout[i]), false);
                    ic.rectTransform.At(0, 0.5f, 62, 0, ic.preserveAspect ? 104 : 70, ic.preserveAspect ? 62 : 50);
                }
                else
                {
                    ic = Ui.Img(row.transform, Sprites.Of(locked ? Icon.Lock : Icon.Plus), locked ? Pal.Dim : Color.white, "Icon");
                    ic.rectTransform.At(0, 0.5f, 60, 0, 70, 50);
                }
                var n = Ui.Label(row.transform, name, 32, locked ? Pal.Dim : Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
                n.rectTransform.Fill(120, 0, 10, 0);
            }
            var boosts = new List<string>();
            if (d.BoostCoins > 0) boosts.Add("2x COINS (" + d.BoostCoins + ")");
            if (d.BoostDamage > 0) boosts.Add("+25% DMG (" + d.BoostDamage + ")");
            if (d.BoostXp > 0) boosts.Add("2x XP (" + d.BoostXp + ")");
            var b = Ui.Label(p.transform, boosts.Count > 0 ? "BOOSTERS: " + string.Join("  ", boosts) : "GRENADES: " + Mathf.Max(2, d.Grenades), 24, Pal.Good, TextAnchor.LowerLeft, FontKind.Semi);
            b.rectTransform.Fill(36, 0, 20, 110);
            var change = Ui.Button(p.transform, "CHANGE LOADOUT", () => Screens.Show(new ArmoryScreen()), Pal.Panel2, 30, Icon.Rifle, null, true);
            change.Bg.rectTransform.At(0.5f, 0, 0, 56, 530, 76);
        }
    }

    // ======================================================================= campaign

    public sealed class CampaignScreen : UiScreen
    {
        public override string Title => "OPERATION LAST LIGHT";
        int _chapter, _sel;
        RectTransform _body;

        public override void Build(RectTransform root)
        {
            var d = SaveSystem.Data;
            _sel = Mathf.Min(d.CampaignCleared, Campaign.All.Length - 1);
            _chapter = Campaign.All[_sel].Chapter;
            _body = Ui.Rect(root, "Body");
            _body.Fill();
            Rebuild();
        }

        void Rebuild()
        {
            Ui.Clear(_body);
            var d = SaveSystem.Data;
            // Chapter tabs.
            for (int c = 0; c < Campaign.Chapters.Length; c++)
            {
                int cc = c;
                bool unlocked = c * 6 <= d.CampaignCleared;
                var b = Ui.Button(_body, (c + 1) + ". " + Campaign.Chapters[c], () => { if (unlocked) { _chapter = cc; _sel = cc * 6; Rebuild(); } }, c == _chapter ? Pal.Accent : Pal.Panel2, 28, unlocked ? (Icon?)null : Icon.Lock);
                b.Bg.rectTransform.At(0, 1, 230 + c * 360, -40, 350, 70);
                b.SetInteractable(unlocked);
            }
            // Missions in the chapter.
            for (int i = 0; i < 6; i++)
            {
                int idx = _chapter * 6 + i;
                var m = Campaign.All[idx];
                bool unlocked = idx <= d.CampaignCleared;
                int stars = idx < d.CampaignStars.Length ? d.CampaignStars[idx] : 0;
                var b = Ui.Button(_body, "", () => { if (unlocked) { _sel = idx; Rebuild(); } }, idx == _sel ? Pal.Accent.A(0.85f) : Pal.Panel.A(0.9f), 26);
                b.Bg.rectTransform.At(0, 1, 360, -150 - i * 120, 660, 108);
                b.SetInteractable(unlocked);
                var num = Ui.Label(b.Bg.transform, (idx + 1).ToString("00"), 50, Color.white.A(0.5f), TextAnchor.MiddleLeft, FontKind.Display);
                num.rectTransform.Fill(24, 0, 0, 0);
                var t = Ui.Label(b.Bg.transform, m.Title, 36, Color.white, TextAnchor.UpperLeft, FontKind.Display);
                t.rectTransform.Fill(110, 12, 160, 0);
                var s = Ui.Label(b.Bg.transform, Biomes.Get(m.Biome).Name + (m.Boss != BossType.None ? "  -  BOSS" : ""), 22, Pal.Dim, TextAnchor.LowerLeft, FontKind.Semi);
                s.rectTransform.Fill(112, 0, 160, 14);
                for (int k = 0; k < 3; k++)
                {
                    var st = Ui.Img(b.Bg.transform, Sprites.Of(unlocked ? Icon.Star : Icon.Lock), unlocked ? (k < stars ? Pal.Accent2 : Pal.Line) : Pal.Dim, "Star");
                    st.rectTransform.At(1, 0.5f, -130 + k * 44, 0, unlocked ? 38 : 30, unlocked ? 38 : 30);
                    if (!unlocked) break;
                }
            }
            // Detail.
            var sel = Campaign.All[_sel];
            var p = Ui.Panel(_body, Pal.Panel.A(0.92f), "Detail");
            p.rectTransform.At(1, 0.5f, -520, -30, 920, 790);
            var ct = Ui.Label(p.transform, "CHAPTER " + (sel.Chapter + 1) + " - " + Campaign.Chapters[sel.Chapter], 28, Pal.Accent, TextAnchor.UpperLeft, FontKind.Display);
            ct.rectTransform.Fill(44, 34, 30, 0);
            var mt = Ui.Title(p.transform, "MISSION " + (sel.Index + 1) + ": " + sel.Title, 58, Pal.Text);
            mt.rectTransform.Fill(44, 76, 30, 0);
            string intro = sel.Index % 6 == 0 ? Campaign.ChapterIntro[sel.Chapter] + "\n\n" : "";
            var story = Ui.Label(p.transform, intro + sel.Story, 30, Pal.Dim, TextAnchor.UpperLeft, FontKind.Body);
            story.rectTransform.Fill(46, 170, 40, 260);
            var plan = Campaign.Plan(sel.Index);
            var obj = Ui.Label(p.transform, "<color=#FFB020>OBJECTIVE</color>  " + plan.ObjectiveText + "\n<color=#FFB020>REWARD</color>  " + sel.Coins.ToString("#,0") + " coins" + (sel.Gems > 0 ? "  +" + sel.Gems + " gems" : "") + (sel.Index < d.CampaignCleared ? "  (claimed)" : ""), 28, Pal.Text, TextAnchor.LowerLeft, FontKind.Semi);
            obj.rectTransform.Fill(46, 0, 30, 170);
            var deploy = Ui.Button(p.transform, "DEPLOY", () => GameApp.Instance.PlayCampaign(sel.Index), Pal.Accent, 54, Icon.Play);
            deploy.Bg.rectTransform.At(0.5f, 0, 0, 80, 520, 110);
        }
    }

    // ======================================================================= daily

    public sealed class DailyScreen : UiScreen
    {
        public override string Title => "DAILY CHALLENGE";
        Text _timer;

        public override void Build(RectTransform root)
        {
            var d = SaveSystem.Data;
            long day = Clock.Today;
            var plan = DailyChallenge.Plan(day, d.HighestStage);
            bool done = d.DailyDone == day;
            var p = Ui.Panel(root, Pal.Panel.A(0.92f), "Card");
            p.rectTransform.At(0.5f, 0.5f, -300, -20, 1100, 800);
            var date = Ui.Label(p.transform, DateTime.UtcNow.ToString("dddd, d MMMM").ToUpper(), 30, Pal.Accent, TextAnchor.UpperLeft, FontKind.Display);
            date.rectTransform.Fill(50, 36, 30, 0);
            var t = Ui.Title(p.transform, plan.BiomeDef.Name, 72, Pal.Text);
            t.rectTransform.Fill(50, 80, 30, 0);
            var rules = Ui.Label(p.transform, plan.ObjectiveText + "\n\n" + DailyChallenge.Describe(plan).Replace("  |  ", "\n"), 34, Pal.Text, TextAnchor.UpperLeft, FontKind.Semi);
            rules.rectTransform.Fill(52, 190, 40, 200);
            rules.lineSpacing = 1.2f;
            var rew = Ui.Label(p.transform, "COMPLETION BONUS:  " + DailyChallenge.CompletionCoins(plan).ToString("#,0") + " COINS  +  " + DailyChallenge.CompletionGems + " GEMS", 30, Pal.Coin, TextAnchor.LowerLeft, FontKind.Display);
            rew.rectTransform.Fill(52, 0, 30, 130);
            _timer = Ui.Label(p.transform, "", 28, Pal.Dim, TextAnchor.LowerLeft, FontKind.Semi);
            _timer.rectTransform.Fill(52, 0, 30, 60);
            if (done)
            {
                var c = Ui.Label(root, "COMPLETED TODAY", 54, Pal.Good, TextAnchor.MiddleCenter, FontKind.Display);
                c.rectTransform.At(1, 0, -420, 280, 620, 80);
                var again = Ui.Button(root, "PLAY AGAIN", () => GameApp.Instance.PlayDaily(), Pal.Panel2, 44, Icon.Restore);
                again.Bg.rectTransform.At(1, 0, -420, 140, 560, 120);
            }
            else
            {
                var deploy = Ui.Button(root, "DEPLOY", () => GameApp.Instance.PlayDaily(), Pal.Accent, 64, Icon.Play);
                deploy.Bg.rectTransform.At(1, 0, -420, 140, 600, 140);
            }
        }

        public override void Tick(float dt)
        {
            if (_timer != null) _timer.text = "NEW CHALLENGE IN " + Clock.Countdown(Clock.UntilTomorrow);
        }
    }

    // ======================================================================= boss rush

    public sealed class BossRushScreen : UiScreen
    {
        public override string Title => "BOSS RUSH";

        public override void Build(RectTransform root)
        {
            var d = SaveSystem.Data;
            for (int i = 1; i <= 4; i++)
            {
                var b = Bestiary.Boss((BossType)i);
                var card = Ui.Panel(root, Pal.Panel.A(0.92f), "Boss");
                card.rectTransform.At(0, 1, 260 + (i - 1) * 420, -320, 400, 520);
                var tint = i % 2 == 0 ? Pal.Toxic : Pal.Bad;
                var portrait = CardArt.Boss((BossType)i);
                if (portrait != null)
                {
                    var pic = Ui.Img(card.transform, portrait, Color.white, "Portrait");
                    pic.rectTransform.At(0.5f, 1, 0, -128, 376, 228);
                    var rule = Ui.Img(card.transform, Sprites.White, tint.A(0.9f), "Rule");
                    rule.rectTransform.At(0.5f, 1, 0, -243, 376, 3);
                }
                else
                {
                    var ic = Ui.Img(card.transform, Sprites.Of(Icon.Boss), tint, "Icon");
                    ic.rectTransform.At(0.5f, 1, 0, -130, 180, 180);
                }
                var n = Ui.Label(card.transform, b.Name, 40, Pal.Text, TextAnchor.UpperCenter, FontKind.Display);
                n.rectTransform.Fill(10, 250, 10, 0);
                var tt = Ui.Label(card.transform, b.Title.ToUpper(), 24, Pal.Accent, TextAnchor.UpperCenter, FontKind.Display);
                tt.rectTransform.Fill(10, 300, 10, 0);
                var ds = Ui.Label(card.transform, b.Desc, 24, Pal.Dim, TextAnchor.UpperCenter, FontKind.Body);
                ds.rectTransform.Fill(24, 345, 24, 10);
            }
            var info = Ui.Label(root, "Face every boss back to back. Each round is a full boss tier harder.\nBEST ROUND: " + d.BossRushBest + "     Round reward: " + BossRush.RoundCoins(1).ToString("#,0") + "+ coins", 32, Pal.Text, TextAnchor.MiddleLeft, FontKind.Semi);
            info.rectTransform.At(0, 0, 640, 150, 1100, 120);
            var start = Ui.Button(root, "START RUN", () => GameApp.Instance.PlayBossRush(1), Pal.Accent, 60, Icon.Play);
            start.Bg.rectTransform.At(1, 0, -380, 150, 560, 140);
        }
    }

    // ======================================================================= missions

    public sealed class MissionsScreen : UiScreen
    {
        public override string Title => "MISSIONS";
        int _tab;
        RectTransform _body;

        public override void Build(RectTransform root)
        {
            Missions.Refresh(SaveSystem.Data, Clock.Today);
            _body = Ui.Rect(root, "Body");
            _body.Fill();
            Rebuild();
        }

        void Rebuild()
        {
            Ui.Clear(_body);
            string[] tabs = { "DAILY", "WEEKLY", "ACHIEVEMENTS", "LOGIN REWARDS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                var b = Ui.Button(_body, tabs[i], () => { _tab = t; Rebuild(); }, i == _tab ? Pal.Accent : Pal.Panel2, 30);
                b.Bg.rectTransform.At(0, 1, 260 + i * 400, -40, 380, 70);
            }
            var d = SaveSystem.Data;
            if (_tab == 3) { LoginPopup.BuildCalendar(_body, false); return; }
            var content = Ui.Scroll(_body, out var scroll);
            scroll.GetComponent<RectTransform>().Fill(60, 110, 60, 20);
            float y = 0;
            if (_tab < 2)
            {
                foreach (var m in d.Missions)
                {
                    if (m.Weekly != (_tab == 1)) continue;
                    Row(content, y, Missions.Describe(m), m.Progress, m.Target, m.Coins, m.Gems, m.Claimed, Missions.CanClaim(m), () => { if (Missions.Claim(d, m.Id)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); } });
                    y += 140;
                }
                var reset = Ui.Label(content, (_tab == 0 ? "New daily missions in " : "Weekly missions reset on Monday (UTC)  ") + (_tab == 0 ? Clock.Countdown(Clock.UntilTomorrow) : ""), 26, Pal.Dim, TextAnchor.MiddleLeft, FontKind.Semi);
                reset.rectTransform.Row(y + 10, 50, 20, 20);
                y += 70;
            }
            else
            {
                for (int i = 0; i < Achievements.All.Length; i++)
                {
                    int idx = i;
                    var a = Achievements.All[i];
                    Row(content, y, a.Name + "  -  " + a.Desc, Math.Min(a.Value(d), a.Target), a.Target, a.Coins, a.Gems, Achievements.Claimed(d, i), Achievements.CanClaim(d, i), () => { if (Achievements.Claim(d, idx)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); } });
                    y += 140;
                }
            }
            content.sizeDelta = new Vector2(0, y);
        }

        static void Row(RectTransform parent, float y, string text, long progress, long target, long coins, long gems, bool claimed, bool canClaim, Action claim)
        {
            var bg = Ui.Panel(parent, Pal.Panel.A(0.9f), "Row", true, false);
            bg.rectTransform.Row(y, 124, 0, 0);
            var t = Ui.Label(bg.transform, text, 32, Pal.Text, TextAnchor.UpperLeft, FontKind.Display);
            t.rectTransform.Fill(36, 18, 520, 0);
            var fill = Ui.Bar(bg.transform, Pal.Panel2, claimed ? Pal.Dim : Pal.Accent2, out var bar, "Progress");
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0, 0);
            bar.offsetMin = new Vector2(36, 24);
            bar.offsetMax = new Vector2(-520, 44);
            Ui.SetFill(fill, target > 0 ? progress / (float)target : 0);
            var pt = Ui.Label(bg.transform, progress.ToString("#,0") + " / " + target.ToString("#,0"), 24, Pal.Dim, TextAnchor.LowerRight, FontKind.Semi);
            pt.rectTransform.Fill(0, 0, 360, 18);
            string rew = (coins > 0 ? coins.ToString("#,0") + " coins" : "") + (gems > 0 ? (coins > 0 ? " + " : "") + gems + " gems" : "");
            var r = Ui.Label(bg.transform, rew, 26, Pal.Coin, TextAnchor.MiddleRight, FontKind.Display);
            r.rectTransform.Fill(0, 0, 250, 0);
            var b = Ui.Button(bg.transform, claimed ? "CLAIMED" : canClaim ? "CLAIM" : "IN PROGRESS", claim, canClaim ? Pal.Good.Mul(0.85f) : Pal.Panel2, 26);
            b.Bg.rectTransform.At(1, 0.5f, -120, 0, 210, 76);
            b.SetInteractable(canClaim);
        }
    }

    /// <summary>7-day login calendar (popup on the first menu visit each day, and a Missions tab).</summary>
    public static class LoginPopup
    {
        public static void Show()
        {
            var p = Screens.Popup("DAILY REWARD", 1500, 700);
            BuildCalendar(p, true);
        }

        public static void BuildCalendar(RectTransform parent, bool popup)
        {
            var d = SaveSystem.Data;
            long today = Clock.Today;
            bool can = Missions.CanClaimLogin(d, today);
            int next = Missions.NextLoginDay(d, today);
            for (int i = 0; i < 7; i++)
            {
                int day = i + 1;
                bool claimed = can ? day < next : day <= next;
                bool isNext = can && day == next;
                var card = Ui.Panel(parent, isNext ? Pal.Accent.A(0.9f) : Pal.Panel2.A(0.95f), "Day", true, false);
                card.rectTransform.At(0.5f, 0.5f, (i - 3) * 196, popup ? 10 : 60, 180, 280);
                var t = Ui.Label(card.transform, "DAY " + day, 30, Color.white, TextAnchor.UpperCenter, FontKind.Display);
                t.rectTransform.Fill(0, 16, 0, 0);
                bool gem = Missions.LoginGems[i] > 0 && Missions.LoginCoins[i] == 0;
                var ic = Ui.Img(card.transform, Sprites.Of(gem ? Icon.Gem : Icon.Coin), gem ? Pal.Gem : Pal.Coin, "Icon");
                ic.rectTransform.At(0.5f, 0.5f, 0, 10, 90, 90);
                var v = Ui.Label(card.transform, gem ? Missions.LoginGems[i] + "" : Missions.LoginCoins[i].ToString("#,0"), 32, Color.white, TextAnchor.LowerCenter, FontKind.Display);
                v.rectTransform.Fill(0, 0, 0, 20);
                if (claimed)
                {
                    var c = Ui.Img(card.transform, Sprites.Of(Icon.Check), Pal.Good, "Check");
                    c.rectTransform.At(0.5f, 0.5f, 0, 10, 110, 110);
                }
            }
            var streak = Ui.Label(parent, "LOGIN STREAK: " + d.LoginStreak + " DAY" + (d.LoginStreak == 1 ? "" : "S") + "   (every full week adds +10% to coin rewards)", 28, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Semi);
            streak.rectTransform.At(0.5f, 0.5f, 0, popup ? -190 : -150, 1400, 50);
            var b = Ui.Button(parent, can ? "CLAIM DAY " + next : "COME BACK TOMORROW", () =>
            {
                int got = Missions.ClaimLogin(d, Clock.Today, out var coins, out var gems);
                if (got > 0)
                {
                    Sfx.Buy();
                    SaveSystem.MarkDirty();
                    Screens.Toast("+" + (coins > 0 ? coins.ToString("#,0") + " COINS" : "") + (gems > 0 ? " +" + gems + " GEMS" : ""));
                    Screens.ClosePopups();
                    Screens.RefreshTopBar();
                    if (!popup) Screens.Show(new MissionsScreen(), false);
                }
            }, can ? Pal.Good.Mul(0.85f) : Pal.Panel2, 38);
            b.Bg.rectTransform.At(0.5f, 0.5f, 0, popup ? -280 : -240, 560, 96);
            b.SetInteractable(can);
        }
    }

    // ======================================================================= settings

    public sealed class SettingsScreen : UiScreen
    {
        public override string Title => "SETTINGS";
        public bool InGame;
        public override bool ShowTopBar => !InGame;
        // Long text rows: a stronger dim than other menus (the in-game pause has its own panel).
        public override float Backdrop => InGame ? 0 : 0.75f;

        public static void OpenInGame()
        {
            Screens.Show(new SettingsScreen { InGame = true }, false);
        }

        public override void Build(RectTransform root)
        {
            var s = SaveSystem.Data.Settings;
            if (InGame)
            {
                var dim = Ui.Img(root, Sprites.White, new Color(0, 0, 0, Ui.Dim(0.85f)), "Dim", true);
                dim.rectTransform.Fill();
                var done = Ui.Button(root, "DONE", () => { SaveSystem.Save(); Screens.Hide(); Screens.ShowPause(); }, Pal.Accent, 36);
                done.Bg.rectTransform.At(1, 1, -170, -60, 260, 86);
            }
            var content = Ui.Scroll(root, out var scroll);
            scroll.GetComponent<RectTransform>().Fill(140, InGame ? 130 : 20, 140, 20);
            float y = 0;
            void Header(string h)
            {
                var t = Ui.Label(content, h, 40, Pal.Accent, TextAnchor.LowerLeft, FontKind.Display);
                t.rectTransform.Row(y, 70, 10, 10);
                var line = Ui.Img(content, Sprites.White, Pal.Line, "Line");
                line.rectTransform.Row(y + 72, 2, 10, 10);
                y += 90;
            }
            RectTransform Row(string label, string hint = null)
            {
                var r = Ui.Rect(content, "Row");
                r.Row(y, 90, 10, 10);
                // Backing panel so the label and hint never sit directly on the bright 3D backdrop.
                var bgp = Ui.Panel(r, Pal.Panel.A(0.82f), "RowBg", true, false);
                bgp.rectTransform.Fill(0, 2, 0, 2);
                var t = Ui.Label(r, label, 32, Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
                t.rectTransform.Fill(24, 0, 900, hint != null ? 30 : 0);
                if (hint != null)
                {
                    var h = Ui.Label(r, hint, 23, Pal.Dim, TextAnchor.LowerLeft, FontKind.Body);
                    h.rectTransform.Fill(24, 50, 700, 6);
                }
                y += 100;
                return r;
            }
            UiButton Toggle(RectTransform row, bool v, Action<bool> set)
            {
                var b = Ui.Toggle(row, v, x => { set(x); SaveSystem.MarkDirty(); });
                b.Bg.rectTransform.At(1, 0.5f, -80, 0, 150, 66);
                return b;
            }
            void SliderRow(RectTransform row, float v, float min, float max, Action<float> set)
            {
                var sl = Ui.Slider(row, v, min, max, x => { set(x); SaveSystem.MarkDirty(); });
                sl.GetComponent<RectTransform>().At(1, 0.5f, -320, 0, 600, 60);
            }

            RectTransform RightBox(RectTransform row, float w)
            {
                var r = Ui.Rect(row, "Seg");
                r.At(1, 0.5f, -w / 2 - 10, 0, w, 70);
                return r;
            }

            Header("GRAPHICS");
            var g = Row("QUALITY", "LOW for older phones, ULTRA for flagships");
            Ui.Segments(RightBox(g, 600), Quality.Names, (int)Quality.Current, i => { s.Graphics = i; SaveSystem.MarkDirty(); Quality.Apply((Gfx)i); Look.RefreshQuality(); }, 600, 66, 28);
            var fps = Row("FRAME RATE", "60 FPS is smoother but uses more battery");
            Ui.Segments(RightBox(fps, 400), new[] { "30 FPS", "60 FPS" }, s.Fps60 ? 1 : 0, i => { s.Fps60 = i == 1; SaveSystem.MarkDirty(); Quality.ApplyFrameRate(StageRunner.Instance != null); }, 400, 66, 28);
            Toggle(Row("ADAPTIVE QUALITY", "Lowers the preset automatically if the frame rate drops"), s.AdaptiveQuality, v => s.AdaptiveQuality = v);
            Toggle(Row("SHOW FPS"), s.ShowFps, v => s.ShowFps = v);

            Header("CONTROLS");
            SliderRow(Row("LOOK SENSITIVITY"), s.Sensitivity, 0.2f, 3f, v => s.Sensitivity = v);
            SliderRow(Row("AIM SENSITIVITY", "Applied while aiming down sights"), s.AdsSensitivity, 0.2f, 2f, v => s.AdsSensitivity = v);
            var aa = Row("AIM ASSIST", "Slows the crosshair over targets and nudges bullets");
            Ui.Segments(RightBox(aa, 480), new[] { "OFF", "LOW", "HIGH" }, s.AimAssist, i => { s.AimAssist = i; SaveSystem.MarkDirty(); }, 480, 66, 28);
            Toggle(Row("AUTO FIRE", "Fires automatically when the crosshair is on an enemy"), s.AutoFire, v => s.AutoFire = v);
            Toggle(Row("GYROSCOPE AIMING", "Tilt the phone to fine-tune your aim"), s.Gyro, v => s.Gyro = v);
            SliderRow(Row("GYRO SENSITIVITY"), s.GyroSensitivity, 0.2f, 3f, v => s.GyroSensitivity = v);
            Toggle(Row("INVERT LOOK"), s.InvertY, v => s.InvertY = v);
            Toggle(Row("LEFT FIRE BUTTON"), s.LeftFire, v => s.LeftFire = v);
            SliderRow(Row("BUTTON VISIBILITY", "Also in Pause > Customize Controls, with a live preview"), s.ButtonAlpha, 0.2f, 1f, v => { s.ButtonAlpha = v; if (Hud.Instance != null && Hud.Instance.Touch != null) Hud.Instance.Touch.ApplyAlpha(); });
            var lay = Row("CUSTOMIZE BUTTON LAYOUT", "Drag buttons and resize them");
            var edit = Ui.Button(lay, "EDIT LAYOUT", () => LayoutEditor.Open(), Pal.Panel2, 28, Icon.Gear, null, true);
            edit.Bg.rectTransform.At(1, 0.5f, -170, 0, 320, 70);

            Header("AUDIO");
            SliderRow(Row("MUSIC"), s.Music, 0, 1, v => s.Music = v);
            SliderRow(Row("SOUND EFFECTS"), s.Sfx, 0, 1, v => s.Sfx = v);
            Toggle(Row("VIBRATION"), s.Vibration, v => s.Vibration = v);

            if (!InGame)
            {
                Header("ACCOUNT");
                var cloud = Row("CLOUD SAVE", CloudSave.Status);
                var sync = Ui.Button(cloud, CloudSave.Configured ? "SYNC NOW" : "NOT CONFIGURED", () => CloudSave.SyncNow(msg => Screens.Toast(msg)), Pal.Panel2, 26);
                sync.Bg.rectTransform.At(1, 0.5f, -170, 0, 320, 70);
                sync.SetInteractable(CloudSave.Configured);
                var restore = Row("RESTORE PURCHASES", "Re-applies items you bought on this Google account");
                var rb = Ui.Button(restore, "RESTORE", () => Iap.Restore(), Pal.Panel2, 26, Icon.Restore);
                rb.Bg.rectTransform.At(1, 0.5f, -170, 0, 320, 70);
                var priv = Row("PRIVACY POLICY");
                var pb = Ui.Button(priv, "OPEN", () => Application.OpenURL(GameApp.PrivacyUrl), Pal.Panel2, 26);
                pb.Bg.rectTransform.At(1, 0.5f, -170, 0, 320, 70);
                var reset = Row("RESET PROGRESS", "Deletes all progress on this device. Purchases are kept.");
                var resb = Ui.Button(reset, "RESET", () => Screens.Confirm("RESET PROGRESS?", "All stages, coins, weapons and upgrades on this device will be deleted. Purchases stay.", "RESET", () => { SaveSystem.ResetAll(); Screens.Home(); }), Pal.Bad.Mul(0.7f), 26);
                resb.Bg.rectTransform.At(1, 0.5f, -170, 0, 320, 70);
                Header("ABOUT");
                var about = Ui.Label(content, "Undead Apocalypse: Infinite War  v" + Application.version + "\nAn original game by RK Games. All characters, places and weapons are fictional.", 26, Pal.Dim, TextAnchor.UpperLeft, FontKind.Body);
                about.rectTransform.Row(y, 80, 10, 10);
                y += 100;
                // Credits and third-party licence notices (the MIT licence requires its notice to ship with the game).
                var credits = Resources.Load<TextAsset>("UACredits");
                if (credits != null)
                {
                    int lines = 0;
                    foreach (var ln in credits.text.Split('\n')) lines += 1 + ln.Length / 110;   // allow for wrapped long lines
                    int ch = lines * 27 + 40;
                    // Dark panel so the notice stays readable over the 3D menu backdrop.
                    var panel = Ui.Img(content, Sprites.Rounded(), new Color(0.04f, 0.05f, 0.07f, 0.88f), "CreditsPanel");
                    panel.raycastTarget = false;
                    panel.rectTransform.Row(y, ch, 0, 0);
                    var cl = Ui.Label(content, credits.text, 20, new Color(0.8f, 0.83f, 0.87f), TextAnchor.UpperLeft, FontKind.Body);
                    cl.rectTransform.Row(y + 16, ch - 24, 26, 26);
                    y += ch + 20;
                }
            }
            content.sizeDelta = new Vector2(0, y + 40);
        }

        public override void OnHide() => SaveSystem.Save();
    }

    /// <summary>Full-screen editor for the touch button layout.</summary>
    public static class LayoutEditor
    {
        public static void Open()
        {
            var canvas = Ui.Canvas("LayoutEditor", 40);
            var root = (RectTransform)canvas.transform;
            // Opaque backing: the gradient sprite alone is transparent at the bottom, which let the Settings screen and
            // toasts show through behind the buttons being edited.
            var solid = Ui.Img(root, Sprites.White, new Color(0.035f, 0.045f, 0.06f, 1), "Solid", true);
            solid.rectTransform.Fill();
            var bg = Ui.Img(root, Sprites.VGradient, new Color(0.09f, 0.12f, 0.15f, 1), "Bg");
            bg.rectTransform.Fill();
            var safe = Ui.Safe(root);
            // Guides for the fixed HUD parts (the same places as in game), so buttons are not dropped on top of them.
            Guide(safe, "MAP", new Vector2(0, 1), new Vector2(0, 1), Hud.MapPos, Vector2.one * Hud.MapSize, true);
            Guide(safe, "HEALTH", new Vector2(0, 1), new Vector2(0, 1), Hud.VitalsPos, Hud.VitalsSize, false);
            Guide(safe, "KILLS / COINS / PAUSE", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(560, 80), false);
            Guide(safe, "WEAPONS", new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-190, 62), new Vector2(420, 90), false);
            Guide(safe, "AMMO", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 22), new Vector2(330, 110), false);
            var tc = TouchControls.Create(safe);
            // Toolbar (top centre, where the objective panel sits in game).
            var bar = Ui.Panel(safe, new Color(0.06f, 0.08f, 0.1f, 0.94f), "Toolbar");
            bar.rectTransform.At(0.5f, 1, 0, -176, 800, 328);
            var title = Ui.Label(bar.transform, "CUSTOMIZE CONTROLS", 40, Pal.Accent, TextAnchor.UpperCenter, FontKind.Display);
            title.rectTransform.Fill(0, 14, 0, 0);
            var hint = Ui.Label(bar.transform, "Drag any button (or the joystick) to move it. Tap one to select it, then set its size.", 24, Pal.Dim, TextAnchor.UpperCenter, FontKind.Semi);
            hint.rectTransform.Fill(20, 62, 20, 0);
            var sizeLabel = Ui.Label(bar.transform, "SELECT A BUTTON", 26, Pal.Text, TextAnchor.MiddleRight, FontKind.Display);
            sizeLabel.rectTransform.At(0, 1, 195, -130, 330, 50);
            Slider slider = null;
            slider = Ui.Slider(bar.transform, 1, 0.6f, 1.6f, v => tc.ScaleSelected(v));
            slider.GetComponent<RectTransform>().At(0, 1, 575, -130, 370, 56);
            tc.BeginEdit(id =>
            {
                slider.SetValueWithoutNotify(tc.SelectedScale);
                sizeLabel.text = TouchControls.Label(id) + " SIZE";
            });
            // Button visibility for all controls, previewed live; kept only with SAVE.
            float alpha = SaveSystem.Data.Settings.ButtonAlpha;
            var alphaLabel = Ui.Label(bar.transform, "", 26, Pal.Text, TextAnchor.MiddleRight, FontKind.Display);
            alphaLabel.rectTransform.At(0, 1, 195, -192, 330, 50);
            void ShowAlpha() => alphaLabel.text = "BUTTON VISIBILITY " + Mathf.RoundToInt(alpha * 100) + "%";
            ShowAlpha();
            tc.PreviewAlpha(alpha);
            var alphaSlider = Ui.Slider(bar.transform, alpha, 0.2f, 1f, v => { alpha = v; tc.PreviewAlpha(v); ShowAlpha(); });
            alphaSlider.GetComponent<RectTransform>().At(0, 1, 575, -192, 370, 56);
            void Close() => UnityEngine.Object.Destroy(canvas.gameObject);
            var save = Ui.Button(bar.transform, "SAVE", () =>
            {
                SaveSystem.Data.Settings.ButtonAlpha = alpha;
                tc.EndEdit(true);
                // In game: the stage's controls take the new layout straight away.
                if (Hud.Instance != null && Hud.Instance.Touch != null) Hud.Instance.Touch.ReloadLayout();
                Close();
                Screens.Toast("Layout saved");
            }, Pal.Good.Mul(0.85f), 32);
            save.Bg.rectTransform.At(0.5f, 0, 270, 52, 240, 74);
            var reset = Ui.Button(bar.transform, "RESET", () =>
            {
                tc.ResetLayout();
                // Default visibility too (UA.Core.Settings.ButtonAlpha's default).
                alpha = 0.85f;
                alphaSlider.SetValueWithoutNotify(alpha);
                tc.PreviewAlpha(alpha);
                ShowAlpha();
                sizeLabel.text = "SELECT A BUTTON";
                Screens.Toast("Default layout - press SAVE to keep it");
            }, Pal.Panel2, 32);
            reset.Bg.rectTransform.At(0.5f, 0, 0, 52, 240, 74);
            var cancel = Ui.Button(bar.transform, "CANCEL", Close, Pal.Panel2, 32);
            cancel.Bg.rectTransform.At(0.5f, 0, -270, 52, 240, 74);
        }

        /// <summary>Outline of a fixed HUD part in the layout editor.</summary>
        static void Guide(RectTransform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool round)
        {
            var g = Ui.Img(parent, round ? Sprites.Circle : Sprites.Rounded(12), new Color(1, 1, 1, 0.06f), "Guide_" + label);
            var rt = g.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var line = Ui.Img(rt, round ? Sprites.Ring(2.5f) : Sprites.RoundedOutline(12, 2), new Color(1, 1, 1, 0.3f), "Line");
            line.rectTransform.Fill();
            var t = Ui.Label(rt, label, 22, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, FontKind.Display);
            t.rectTransform.Fill(6, 0, 6, 0);
        }
    }
}
