using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>Renders a slowly rotating weapon into a texture for the armory and store.</summary>
    public static class WeaponPreview
    {
        static GameObject _stage;
        static Camera _cam;
        static RenderTexture _rt;
        static GunModel _gun;
        static Transform _spin;
        static float _yaw;

        public static RenderTexture Show(WeaponId id, WeaponSave ws)
        {
            if (_stage == null)
            {
                _stage = new GameObject("WeaponPreview");
                UnityEngine.Object.DontDestroyOnLoad(_stage);
                _stage.transform.position = new Vector3(0, -2000, 0);
                var camGo = new GameObject("Cam");
                camGo.transform.SetParent(_stage.transform, false);
                camGo.transform.localPosition = new Vector3(0, 0.05f, -1.15f);
                _cam = camGo.AddComponent<Camera>();
                _cam.fieldOfView = 30;
                _cam.nearClipPlane = 0.05f;
                _cam.farClipPlane = 6;
                _cam.cullingMask = 1 << Layers.Viewmodel;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0, 0, 0, 0);
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                _rt = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
                _cam.targetTexture = _rt;
                foreach (var (rot, col, inten) in new[] { (new Vector3(30, -40, 0), new Color(1f, 0.95f, 0.9f), 2.4f), (new Vector3(-10, 150, 0), new Color(1f, 0.5f, 0.2f), 2.6f), (new Vector3(10, 60, 0), new Color(0.4f, 0.6f, 1f), 1.6f) })
                {
                    var lg = new GameObject("Light");
                    lg.transform.SetParent(_stage.transform, false);
                    lg.transform.rotation = Quaternion.Euler(rot);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 60;
                    l.range = 10;
                    l.color = col;
                    l.intensity = inten * 6;
                    l.transform.position = _stage.transform.position - l.transform.forward * 2.5f;
                    LightLayers.Set(l, LightLayers.Viewmodel);
                }
                _spin = X.Child(_stage.transform, "Spin").transform;
            }
            _stage.SetActive(true);
            if (_gun != null) UnityEngine.Object.Destroy(_gun.gameObject);
            _gun = GunModels.Build(id, ws, _spin, Layers.Viewmodel);
            // Centre and frame the weapon.
            var b = new Bounds(_gun.transform.position, Vector3.zero);
            foreach (var r in _gun.Renderers) b.Encapsulate(r.bounds);
            _gun.transform.localPosition = -_spin.InverseTransformPoint(b.center);
            float size = Mathf.Max(b.size.z, b.size.y * 1.8f);
            _cam.transform.localPosition = new Vector3(0, 0.02f, -size * 1.9f - 0.1f);
            return _rt;
        }

        public static void Tick(float dt)
        {
            if (_spin == null || _stage == null || !_stage.activeSelf) return;
            _yaw += dt * 22;
            _spin.localRotation = Quaternion.Euler(8, 90 + Mathf.Sin(_yaw * Mathf.Deg2Rad) * 35, 0);
        }

        public static void Hide()
        {
            if (_stage != null) _stage.SetActive(false);
        }
    }

    // ======================================================================= armory

    public sealed class ArmoryScreen : UiScreen
    {
        public override string Title => "WEAPONS";
        WeaponId _sel;
        int _filter = -1, _tab;
        RectTransform _list, _center, _right;

        public override void Build(RectTransform root)
        {
            var d = SaveSystem.Data;
            _sel = (WeaponId)d.Loadout[0];
            _list = Ui.Rect(root, "List");
            _list.anchorMin = new Vector2(0, 0); _list.anchorMax = new Vector2(0, 1); _list.pivot = new Vector2(0, 0.5f);
            _list.offsetMin = new Vector2(30, 20); _list.offsetMax = new Vector2(520, -10);
            _center = Ui.Rect(root, "Center");
            _center.anchorMin = new Vector2(0, 0); _center.anchorMax = new Vector2(1, 1);
            _center.offsetMin = new Vector2(540, 20); _center.offsetMax = new Vector2(-700, -10);
            _right = Ui.Rect(root, "Right");
            _right.anchorMin = new Vector2(1, 0); _right.anchorMax = new Vector2(1, 1); _right.pivot = new Vector2(1, 0.5f);
            _right.offsetMin = new Vector2(-680, 20); _right.offsetMax = new Vector2(-30, -10);
            Rebuild();
        }

        public override void OnHide() => WeaponPreview.Hide();
        public override void Tick(float dt) => WeaponPreview.Tick(dt);

        void Rebuild()
        {
            BuildList();
            BuildCenter();
            BuildRight();
            Screens.RefreshTopBar();
        }

        void BuildList()
        {
            Ui.Clear(_list);
            var d = SaveSystem.Data;
            Icon[] icons = { Icon.Star, Icon.Pistol, Icon.Rifle, Icon.Shotgun, Icon.Sniper, Icon.MachineGun, Icon.Special };
            for (int i = 0; i < icons.Length; i++)
            {
                int f = i - 1;
                var b = Ui.IconButton(_list, icons[i], () => { _filter = f; BuildList(); }, 62, f == _filter ? Pal.Accent : Pal.Panel2);
                b.Bg.rectTransform.At(0, 1, 36 + i * 69, -36, 62, 62);
            }
            var content = Ui.Scroll(_list, out var scroll);
            scroll.GetComponent<RectTransform>().Fill(0, 80, 0, 0);
            float y = 0;
            foreach (var w in Weapons.All)
            {
                if (_filter >= 0 && (int)w.Class != _filter) continue;
                var id = w.Id;
                bool owned = d.Owns(id);
                bool locked = d.Level < w.UnlockLevel;
                var card = Ui.Button(content, "", () => { _sel = id; Rebuild(); }, id == _sel ? Pal.Accent.A(0.9f) : Pal.Panel.A(0.92f), 26);
                card.Bg.rectTransform.Row(y, 104, 0, 6);
                var ic = CardArt.WeaponIcon(card.Bg.transform, w, !owned);
                if (ic.preserveAspect) ic.rectTransform.At(0, 0.5f, 60, 0, 104, 64);
                else ic.rectTransform.At(0, 0.5f, 60, 0, 86, 64);
                var n = Ui.Label(card.Bg.transform, w.Name.ToUpper(), 30, owned ? Color.white : Pal.Dim, TextAnchor.UpperLeft, FontKind.Display);
                n.rectTransform.Fill(116, 14, 10, 0);
                string status = owned ? (Array.IndexOf(d.Loadout, (int)id) >= 0 ? "EQUIPPED" : "OWNED") : locked ? "UNLOCKS AT LEVEL " + w.UnlockLevel : (w.PriceCurrency == Currency.Gems ? w.Price + " GEMS" : w.Price.ToString("#,0") + " COINS");
                var s = Ui.Label(card.Bg.transform, status, 22, owned ? Pal.Good : locked ? Pal.Bad : w.PriceCurrency == Currency.Gems ? Pal.Gem : Pal.Coin, TextAnchor.LowerLeft, FontKind.Semi);
                s.rectTransform.Fill(118, 0, 10, 12);
                for (int r = 0; r < w.Rarity; r++)
                {
                    var st = Ui.Img(card.Bg.transform, Sprites.Of(Icon.Star), Pal.Accent2, "R");
                    st.rectTransform.At(1, 1, -26 - r * 26, -24, 24, 24);
                }
                y += 112;
            }
            content.sizeDelta = new Vector2(0, y);
        }

        void BuildCenter()
        {
            Ui.Clear(_center);
            var d = SaveSystem.Data;
            var w = Weapons.Get(_sel);
            var ws = d.Weapon(_sel);
            var bg = Ui.Panel(_center, Pal.Panel.A(0.85f), "Bg", true, false);
            bg.rectTransform.Fill();
            var glow = Ui.Img(_center, Sprites.Glow, Pal.Accent.A(0.18f), "Glow");
            glow.rectTransform.At(0.5f, 1, 0, -280, 900, 500);
            var raw = Ui.Rect(_center, "Preview").gameObject.AddComponent<RawImage>();
            raw.texture = WeaponPreview.Show(_sel, ws ?? new WeaponSave { Id = (int)_sel, Optic = w.BuiltInScope ? (int)Attachment.Scope : -1 });
            raw.raycastTarget = false;
            raw.rectTransform.At(0.5f, 1, 0, -270, 680, 382);
            var n = Ui.Label(_center, w.Name.ToUpper(), 54, Pal.Text, TextAnchor.UpperLeft, FontKind.Display);
            n.rectTransform.Fill(30, 470, 20, 0);
            var desc = Ui.Label(_center, w.Desc, 26, Pal.Dim, TextAnchor.UpperLeft, FontKind.Body);
            desc.rectTransform.Fill(32, 534, 20, 0);
            // Stat bars.
            var st = PlayerProgress.Weapon(d, _sel, PlayerProgress.Compute(d));
            float dps = st.Damage * st.Pellets / st.Interval;
            (string, float, string)[] bars =
            {
                ("DAMAGE", Mathf.Clamp01(st.Damage * st.Pellets / 420f), Mathf.RoundToInt(st.Damage) + (st.Pellets > 1 ? " x" + st.Pellets : "")),
                ("FIRE RATE", Mathf.Clamp01(60f / st.Interval / 1200f), Mathf.RoundToInt(60f / st.Interval) + " RPM"),
                ("ACCURACY", Mathf.Clamp01(1 - st.HipSpread / 8f), (100 - st.HipSpread * 10).ToString("0")),
                ("MAGAZINE", Mathf.Clamp01(st.Mag / 150f), st.Mag.ToString()),
                ("RELOAD", Mathf.Clamp01(1 - st.Reload / 5f), st.Reload.ToString("0.0") + "s"),
                ("DPS", Mathf.Clamp01(dps / 1500f), Mathf.RoundToInt(dps).ToString()),
            };
            for (int i = 0; i < bars.Length; i++)
            {
                var row = Ui.Rect(_center, "Stat");
                row.Row(600 + i * 46, 40, 34, 30);
                var l = Ui.Label(row, bars[i].Item1, 24, Pal.Dim, TextAnchor.MiddleLeft, FontKind.Semi);
                l.rectTransform.Fill(0, 0, 0, 0);
                var fill = Ui.Bar(row, Pal.Panel2, Pal.Accent2, out var br, "Bar");
                br.At(0, 0.5f, 380, 0, 360, 14);
                Ui.SetFill(fill, bars[i].Item2);
                var v = Ui.Label(row, bars[i].Item3, 26, Pal.Text, TextAnchor.MiddleRight, FontKind.Display);
                v.rectTransform.Fill(0, 0, 0, 0);
            }
            // Buy / equip.
            bool owned = ws != null;
            if (!owned)
            {
                bool locked = d.Level < w.UnlockLevel;
                string price = w.PriceCurrency == Currency.Gems ? w.Price + " GEMS" : w.Price.ToString("#,0") + " COINS";
                var b = Ui.Button(_center, locked ? "LEVEL " + w.UnlockLevel + " REQUIRED" : "BUY  " + price, () =>
                {
                    if (Wallet.BuyWeapon(d, _sel)) { Sfx.Buy(); SaveSystem.MarkDirty(); Screens.Toast(w.Name + " unlocked"); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast(locked ? "Reach level " + w.UnlockLevel : "Not enough " + (w.PriceCurrency == Currency.Gems ? "gems" : "coins")); }
                }, locked ? Pal.Panel2 : w.PriceCurrency == Currency.Gems ? Pal.Gem.Mul(0.6f) : Pal.Accent, 36, w.PriceCurrency == Currency.Gems ? Icon.Gem : Icon.Coin);
                b.Bg.rectTransform.At(0.5f, 0, 0, 70, 560, 96);
            }
            else
            {
                int slots = d.LoadoutSlots;
                for (int i = 0; i < 3; i++)
                {
                    int slot = i;
                    bool lockedSlot = i >= slots;
                    bool here = d.Loadout[i] == (int)_sel;
                    var b = Ui.Button(_center, lockedSlot ? "LOCKED" : here ? "SLOT " + (i + 1) + " ✓" : "SLOT " + (i + 1), () =>
                    {
                        if (lockedSlot) { Screens.Show(new StoreScreen { Section = 5 }); return; }
                        // Move the weapon into this slot (swap if it was elsewhere).
                        int prev = Array.IndexOf(d.Loadout, (int)_sel);
                        if (prev >= 0 && prev != slot) d.Loadout[prev] = d.Loadout[slot];
                        d.Loadout[slot] = (int)_sel;
                        if (d.Loadout[0] < 0) d.Loadout[0] = (int)WeaponId.StandardPistol;
                        SaveSystem.MarkDirty();
                        Sfx.Equip();
                        Rebuild();
                    }, here ? Pal.Good.Mul(0.8f) : lockedSlot ? Pal.Panel2 : Pal.Accent, 32, lockedSlot ? Icon.Lock : (Icon?)null);
                    b.Bg.rectTransform.At(0.5f, 0, (i - 1) * 260, 70, 240, 90);
                }
            }
        }

        void BuildRight()
        {
            Ui.Clear(_right);
            var d = SaveSystem.Data;
            var w = Weapons.Get(_sel);
            var ws = d.Weapon(_sel);
            string[] tabs = { "UPGRADES", "ATTACHMENTS", "SKINS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                var b = Ui.Button(_right, tabs[i], () => { _tab = t; BuildRight(); }, i == _tab ? Pal.Accent : Pal.Panel2, 26);
                b.Bg.rectTransform.At(0, 1, 110 + i * 218, -36, 210, 64);
            }
            var panel = Ui.Panel(_right, Pal.Panel.A(0.9f), "Panel", true, false);
            panel.rectTransform.Fill(0, 84, 0, 0);
            if (ws == null)
            {
                var t = Ui.Label(panel.transform, "Buy this weapon to upgrade it, fit attachments and apply skins.", 30, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Body);
                t.rectTransform.Fill(40, 40, 40, 40);
                return;
            }
            var p = panel.rectTransform;
            if (_tab == 0)
            {
                for (int s = 0; s < 6; s++)
                {
                    var stat = (WStat)s;
                    int lv = ws.Levels[s];
                    var row = Ui.Rect(p, "Up");
                    row.Row(26 + s * 132, 120, 26, 26);
                    var n = Ui.Label(row, Weapons.StatNames[s], 30, Pal.Text, TextAnchor.UpperLeft, FontKind.Display);
                    n.rectTransform.Fill(0, 6, 0, 0);
                    for (int k = 0; k < Weapons.MaxLevel; k++)
                    {
                        var pip = Ui.Img(row, Sprites.Cut(4), k < lv ? Pal.Accent2 : Pal.Panel2, "Pip");
                        pip.rectTransform.At(0, 0, 18 + k * 34, 30, 28, 18);
                    }
                    long cost = Weapons.UpgradeCost(w, stat, lv);
                    var b = Ui.Button(row, lv >= Weapons.MaxLevel ? "MAX" : X.Money(cost), () =>
                    {
                        if (Wallet.UpgradeWeapon(d, _sel, stat)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                        else { Sfx.Error(); Screens.Toast("Not enough coins"); }
                    }, lv >= Weapons.MaxLevel ? Pal.Panel2 : Pal.Coin.Mul(0.75f), 30, lv >= Weapons.MaxLevel ? (Icon?)null : Icon.Coin);
                    b.Bg.rectTransform.At(1, 0.5f, -110, 0, 210, 80);
                    b.SetInteractable(lv < Weapons.MaxLevel);
                }
            }
            else if (_tab == 1)
            {
                for (int a = 0; a < 6; a++)
                {
                    var att = (Attachment)a;
                    var row = Ui.Rect(p, "Att");
                    row.Row(20 + a * 128, 116, 26, 26);
                    bool fits = Weapons.AttachmentFits(w, att) && !(w.BuiltInScope && Weapons.IsOptic(att));
                    var n = Ui.Label(row, Weapons.AttachmentNames[a], 30, fits ? Pal.Text : Pal.Dim, TextAnchor.UpperLeft, FontKind.Display);
                    n.rectTransform.Fill(0, 8, 230, 0);
                    var ds = Ui.Label(row, fits ? Weapons.AttachmentDesc[a] : (w.BuiltInScope && Weapons.IsOptic(att) ? "Built-in scope" : "Not compatible"), 22, Pal.Dim, TextAnchor.LowerLeft, FontKind.Body);
                    ds.rectTransform.Fill(0, 0, 230, 14);
                    if (!fits) continue;
                    bool own = Wallet.OwnsAttachment(ws, att);
                    bool on = Wallet.IsEquipped(ws, att);
                    var b = Ui.Button(row, own ? (on ? "EQUIPPED" : "EQUIP") : X.Money(Weapons.AttachmentPrice[a]), () =>
                    {
                        if (!own)
                        {
                            if (Wallet.BuyAttachment(d, _sel, att)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                            else { Sfx.Error(); Screens.Toast("Not enough coins"); }
                        }
                        else { Wallet.Equip(d, _sel, att, !on); Sfx.Equip(); SaveSystem.MarkDirty(); Rebuild(); }
                    }, own ? (on ? Pal.Good.Mul(0.8f) : Pal.Panel2) : Pal.Coin.Mul(0.75f), 28, own ? (Icon?)null : Icon.Coin);
                    b.Bg.rectTransform.At(1, 0.5f, -110, 0, 210, 76);
                }
            }
            else
            {
                for (int s = 0; s < Weapons.SkinNames.Length; s++)
                {
                    var skin = (Skin)s;
                    if (skin == Skin.Dragon && !Wallet.OwnsSkin(ws, skin)) { }
                    int col = s % 2, row = s / 2;
                    bool own = Wallet.OwnsSkin(ws, skin);
                    bool on = ws.Skin == s;
                    var card = Ui.Button(p, "", () =>
                    {
                        if (Wallet.BuySkin(d, _sel, skin)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                        else { Sfx.Error(); Screens.Toast("Not enough gems"); }
                    }, on ? Pal.Good.Mul(0.7f) : Pal.Panel2, 24);
                    card.Bg.rectTransform.At(0, 1, 165 + col * 312, -76 - row * 148, 300, 136);
                    var sw = Ui.Rect(card.Bg.transform, "Swatch").gameObject.AddComponent<RawImage>();
                    sw.texture = Tex.Camo(SkinTex(skin));
                    sw.raycastTarget = false;
                    sw.rectTransform.At(0, 0.5f, 62, 0, 100, 100);
                    var n = Ui.Label(card.Bg.transform, Weapons.SkinNames[s], 26, Color.white, TextAnchor.UpperLeft, FontKind.Display);
                    n.rectTransform.Fill(124, 18, 8, 0);
                    var pr = Ui.Label(card.Bg.transform, own ? (on ? "EQUIPPED" : "OWNED") : Weapons.SkinGems[s] + " GEMS", 22, own ? Pal.Good : Pal.Gem, TextAnchor.LowerLeft, FontKind.Semi);
                    pr.rectTransform.Fill(126, 0, 8, 18);
                }
            }
        }

        static int SkinTex(Skin s)
        {
            switch (s)
            {
                case Skin.Woodland: return 1;
                case Skin.Desert: return 2;
                case Skin.UrbanDigital: return 3;
                case Skin.Arctic: return 4;
                case Skin.BloodRed: return 5;
                case Skin.Carbon: return 6;
                case Skin.Toxic: return 7;
                case Skin.Gold: return 8;
                case Skin.Dragon: return 9;
                default: return 0;
            }
        }
    }

    // ======================================================================= upgrades / skills / outfit

    public sealed class UpgradesScreen : UiScreen
    {
        public override string Title => "UPGRADES";
        /// <summary>Tab to open on (0 player, 1 skill tree, 2 characters).</summary>
        public int Tab;
        /// <summary>Character to preview first on the Characters tab (-1 = the equipped one).</summary>
        public int PreviewCharacter = -1;
        int _tab;
        int _previewChar = -1;
        RectTransform _body;

        public override void Build(RectTransform root)
        {
            _body = Ui.Rect(root, "Body");
            _body.Fill();
            _tab = Tab;
            _previewChar = PreviewCharacter;
            Rebuild();
        }

        public override void OnHide() => CharacterPreview.Hide();
        public override void Tick(float dt) => CharacterPreview.Tick(dt);

        void Rebuild()
        {
            Ui.Clear(_body);
            Screens.RefreshTopBar();
            if (_tab != 2) CharacterPreview.Hide();
            string[] tabs = { "PLAYER", "SKILL TREE", "CHARACTERS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                var b = Ui.Button(_body, tabs[i], () => { _tab = t; Rebuild(); }, i == _tab ? Pal.Accent : Pal.Panel2, 30);
                b.Bg.rectTransform.At(0, 1, 240 + i * 420, -40, 400, 70);
            }
            if (_tab == 0) Player();
            else if (_tab == 1) Skills();
            else Outfit();
        }

        void Player()
        {
            var d = SaveSystem.Data;
            var ps = PlayerProgress.Compute(d);
            string[] current =
            {
                Mathf.RoundToInt(ps.MaxHealth) + " HP", Mathf.RoundToInt(ps.MaxArmor) + " ARMOR", "x" + ps.MoveMul.ToString("0.00"), "x" + ps.ReloadMul.ToString("0.00"),
                "x" + ps.CritMul.ToString("0.00"), "x" + ps.DamageMul.ToString("0.00"), "x" + ps.AmmoMul.ToString("0.00"),
            };
            for (int i = 0; i < 7; i++)
            {
                var stat = (PStat)i;
                int lv = d.PlayerUpgrades[i];
                var row = Ui.Panel(_body, Pal.Panel.A(0.9f), "Row", true, false);
                row.rectTransform.Row(110 + i * 118, 108, 120, 120);
                var n = Ui.Label(row.transform, PlayerProgress.StatNames[i], 34, Pal.Text, TextAnchor.UpperLeft, FontKind.Display);
                n.rectTransform.Fill(36, 14, 0, 0);
                var ds = Ui.Label(row.transform, PlayerProgress.StatDesc[i] + " per level   (now " + current[i] + ")", 24, Pal.Dim, TextAnchor.LowerLeft, FontKind.Body);
                ds.rectTransform.Fill(38, 0, 0, 14);
                for (int k = 0; k < PlayerProgress.MaxUpgrade; k++)
                {
                    var pip = Ui.Img(row.transform, Sprites.Cut(4), k < lv ? Pal.Accent2 : Pal.Panel2, "Pip");
                    pip.rectTransform.At(1, 0.5f, -980 + k * 34, 0, 28, 20);
                }
                long cost = PlayerProgress.UpgradeCost(stat, lv);
                var b = Ui.Button(row.transform, lv >= PlayerProgress.MaxUpgrade ? "MAX" : X.Money(cost), () =>
                {
                    if (Wallet.UpgradePlayer(d, stat)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast("Not enough coins"); }
                }, lv >= PlayerProgress.MaxUpgrade ? Pal.Panel2 : Pal.Coin.Mul(0.75f), 30, lv >= PlayerProgress.MaxUpgrade ? (Icon?)null : Icon.Coin);
                b.Bg.rectTransform.At(1, 0.5f, -150, 0, 240, 80);
                b.SetInteractable(lv < PlayerProgress.MaxUpgrade);
            }
        }

        void Skills()
        {
            var d = SaveSystem.Data;
            var pts = Ui.Label(_body, "SKILL POINTS: " + d.SkillPoints + "     (earn 1 per level)", 36, Pal.Accent2, TextAnchor.MiddleLeft, FontKind.Display);
            pts.rectTransform.At(0, 1, 520, -130, 900, 60);
            var respec = Ui.Button(_body, "RESPEC (50 GEMS)", () => Screens.Confirm("RESET SKILLS?", "Refund every skill point for 50 gems.", "RESPEC", () =>
            {
                if (Wallet.Spend(d, Currency.Gems, 50)) { Skill.Respec(d); SaveSystem.MarkDirty(); Rebuild(); }
                else { Sfx.Error(); Screens.Toast("Not enough gems"); }
            }), Pal.Panel2, 26, Icon.Gem);
            respec.Bg.rectTransform.At(1, 1, -220, -130, 360, 70);
            Color[] branchCol = { Pal.Health, Pal.Bad, Pal.Gem };
            for (int b = 0; b < 3; b++)
            {
                float x = 330 + b * 620;
                var h = Ui.Label(_body, Skill.Branches[b], 40, branchCol[b], TextAnchor.MiddleCenter, FontKind.Display);
                h.rectTransform.At(0, 1, x, -200, 560, 60);
                var line = Ui.Img(_body, Sprites.White, branchCol[b].A(0.35f), "Line");
                line.rectTransform.At(0, 1, x, -540, 6, 600);
                for (int t = 0; t < 4; t++)
                {
                    SkillDef def = null;
                    foreach (var s in Skill.All) if (s.Branch == b && s.Tier == t) def = s;
                    if (def == null) continue;
                    int rank = Skill.Rank(d, def.Id);
                    bool can = Skill.CanUnlock(d, def.Id, out var why);
                    var node = Ui.Panel(_body, rank > 0 ? branchCol[b].Mul(0.45f).A(0.95f) : Pal.Panel.A(0.95f), "Node", true, false);
                    node.rectTransform.At(0, 1, x, -290 - t * 165, 560, 150);
                    var n = Ui.Label(node.transform, def.Name + "   " + rank + "/" + def.MaxRank, 32, Pal.Text, TextAnchor.UpperLeft, FontKind.Display);
                    n.rectTransform.Fill(26, 14, 180, 0);
                    var ds = Ui.Label(node.transform, def.Desc, 22, Pal.Dim, TextAnchor.LowerLeft, FontKind.Body);
                    ds.rectTransform.Fill(28, 0, 180, 16);
                    var id = def.Id;
                    var btn = Ui.Button(node.transform, rank >= def.MaxRank ? "MAX" : can ? "+1 (" + def.Cost + ")" : why.Length > 14 ? "LOCKED" : why, () =>
                    {
                        if (Skill.Unlock(d, id)) { Sfx.LevelUp(); SaveSystem.MarkDirty(); Rebuild(); }
                        else { Sfx.Error(); Screens.Toast(why); }
                    }, can ? branchCol[b].Mul(0.8f) : Pal.Panel2, 24);
                    btn.Bg.rectTransform.At(1, 0.5f, -96, 0, 168, 70);
                    btn.SetInteractable(can);
                }
            }
        }

        void Outfit()
        {
            var d = SaveSystem.Data;
            if (_previewChar < 0) _previewChar = d.Outfit;
            // Full-body 3D preview of the selected character.
            var stage = Ui.Img(_body, Sprites.Rounded(), new Color(0.05f, 0.07f, 0.1f, 0.85f), "Stage");
            stage.rectTransform.At(0, 1, 300, -560, 500, 820);
            var view = Ui.Rect(stage.transform, "View").gameObject.AddComponent<RawImage>();
            view.texture = CharacterPreview.Show(_previewChar);
            view.raycastTarget = false;
            view.rectTransform.Fill(10, 70, 10, 10);
            var nm = Ui.Label(stage.transform, Weapons.OutfitNames[_previewChar], 40, Color.white, TextAnchor.LowerCenter, FontKind.Display);
            nm.rectTransform.Fill(10, 0, 10, 26);
            var role = Ui.Label(stage.transform, Characters.Roles[_previewChar], 20, Pal.Dim, TextAnchor.LowerCenter, FontKind.Semi);
            role.rectTransform.Fill(10, 0, 10, 4);

            var h1 = Ui.Label(_body, "CHARACTERS", 36, Pal.Accent2, TextAnchor.MiddleLeft, FontKind.Display);
            h1.rectTransform.At(0, 1, 880, -130, 600, 60);
            for (int i = 0; i < Weapons.OutfitNames.Length; i++)
            {
                int idx = i;
                bool own = Array.IndexOf(d.OwnedOutfits, i) >= 0;
                bool on = d.Outfit == i;
                var card = Ui.Button(_body, "", () =>
                {
                    // First tap previews; tapping the previewed character buys / equips it.
                    if (_previewChar != idx) { _previewChar = idx; Rebuild(); return; }
                    if (Wallet.BuyOutfit(d, idx)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast("Not enough gems"); }
                }, on ? Pal.Good.Mul(0.7f) : idx == _previewChar ? Pal.Accent.Mul(0.55f) : Pal.Panel2, 24);
                card.Bg.rectTransform.At(0, 1, 740 + (i % 3) * 330, -270 - (i / 3) * 210, 310, 190);
                var n = Ui.Label(card.Bg.transform, Weapons.OutfitNames[i], 30, Color.white, TextAnchor.UpperCenter, FontKind.Display);
                n.rectTransform.Fill(8, 18, 8, 0);
                var wn = Ui.Label(card.Bg.transform, Weapons.Get(Characters.Weapon[i]).Name, 20, Pal.Dim, TextAnchor.MiddleCenter, FontKind.Semi);
                wn.rectTransform.Fill(8, 0, 8, 0);
                string state = own ? (on ? "EQUIPPED" : idx == _previewChar ? "TAP TO EQUIP" : "OWNED") : Weapons.OutfitGems[i] + " GEMS" + (idx == _previewChar ? "  -  TAP TO BUY" : "");
                var pr = Ui.Label(card.Bg.transform, state, 22, own ? Pal.Good : Pal.Gem, TextAnchor.LowerCenter, FontKind.Semi);
                pr.rectTransform.Fill(6, 0, 6, 14);
            }
            var h2 = Ui.Label(_body, "GLOVES", 36, Pal.Accent2, TextAnchor.MiddleLeft, FontKind.Display);
            h2.rectTransform.At(0, 1, 880, -720, 600, 60);
            Color[] glove = { X.Hex(0x111214), X.Hex(0x6B5133), X.Hex(0x7A1010), X.Hex(0x2A2D33), X.Hex(0xD4A52A) };
            for (int i = 0; i < Weapons.GloveNames.Length; i++)
            {
                int idx = i;
                bool own = Array.IndexOf(d.OwnedGloves, i) >= 0;
                bool on = d.Gloves == i;
                var card = Ui.Button(_body, "", () =>
                {
                    if (Wallet.BuyGloves(d, idx)) { Sfx.Buy(); SaveSystem.MarkDirty(); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast(idx == 4 && !own ? "Gold gloves: 250 gems or the Legendary Pack" : "Not enough gems"); }
                }, on ? Pal.Good.Mul(0.7f) : Pal.Panel2, 24);
                card.Bg.rectTransform.At(0, 1, 690 + i * 230, -850, 210, 170);
                var sw = Ui.Img(card.Bg.transform, Sprites.Circle, glove[i], "Swatch");
                sw.rectTransform.At(0.5f, 1, 0, -50, 70, 70);
                var n = Ui.Label(card.Bg.transform, Weapons.GloveNames[i], 28, Color.white, TextAnchor.LowerCenter, FontKind.Display);
                n.rectTransform.Fill(6, 0, 6, 46);
                var pr = Ui.Label(card.Bg.transform, own ? (on ? "EQUIPPED" : "OWNED") : Weapons.GloveGems[i] + " GEMS", 22, own ? Pal.Good : Pal.Gem, TextAnchor.LowerCenter, FontKind.Semi);
                pr.rectTransform.Fill(6, 0, 6, 14);
            }
        }
    }
}
