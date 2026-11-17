using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// In-game HUD: vitals, objective and stage info, objective marker, boss bar, ammo and weapons,
    /// crosshair with hitmarkers, damage direction, announcements, floating numbers and scope overlay.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public static Hud Instance;
        public Canvas Canvas;
        RectTransform _root, _safe;
        Image _hp, _armor, _hpGlow, _bossFill, _bossBack, _reloadRing, _holdRing, _scope, _vignette, _hitmark, _headIcon, _killSkull;
        Text _hpText, _armorText, _stage, _objective, _objSub, _kills, _coins, _ammo, _reserve, _weaponName, _grenades, _bossName, _announce, _announceSub, _fps, _marker, _reloadHint;
        RectTransform _bossRoot, _crossRoot, _markerRoot, _announceRoot;
        Image[] _cross = new Image[4];
        Image[] _dmgArcs = new Image[4];
        float[] _dmgT = new float[4];
        Vector3[] _dmgFrom = new Vector3[4];
        int _dmgNext;
        readonly List<Image> _slotIcons = new List<Image>();
        readonly List<Image> _slotBgs = new List<Image>();
        readonly List<bool> _slotArt = new List<bool>();
        float _hitT, _killT, _headT, _announceT, _bossFlash, _coinsPulse;
        long _shownCoins;
        Image _markerIcon;
        RectTransform _markerArrow;
        readonly List<(Text t, Vector3 world, float life)> _floats = new List<(Text, Vector3, float)>();
        public TouchControls Touch;

        /// <summary>HUD placement (top-left anchored, reference pixels); the button-layout editor draws these as guides.</summary>
        public static readonly Vector2 MapPos = new Vector2(28, -22);
        public const float MapSize = 200;
        /// <summary>Health / armour panel: to the right of the minimap, top-aligned with it.</summary>
        public static Vector2 VitalsPos => new Vector2(MapPos.x + MapSize + 22, MapPos.y);
        /// <summary>
        /// Up to 460 wide, narrower on 16:9 screens so it never reaches the objective panel (top centre, 760 wide).
        /// The canvas is 1080 high, so its width is 1080 x the screen's aspect ratio.
        /// </summary>
        public static Vector2 VitalsSize
        {
            get
            {
                float canvasW = 1080f * Screen.width / Mathf.Max(1, Screen.height);
                float room = canvasW / 2 - 380 - 16 - VitalsPos.x;
                return new Vector2(Mathf.Clamp(room, 300, 460), 100);
            }
        }

        public static Hud Create()
        {
            var c = Ui.Canvas("HUD", 10);
            var h = c.gameObject.AddComponent<Hud>();
            h.Canvas = c;
            h.Build();
            Instance = h;
            return h;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            _root = (RectTransform)Canvas.transform;
            // Vignette and scope overlays (bottom of the stack).
            _vignette = Ui.Img(_root, Sprites.Vignette, new Color(0, 0, 0, 0.55f), "Vignette");
            _vignette.rectTransform.Fill();
            _scope = Ui.Img(_root, Sprites.Scope, Color.black, "Scope");
            _scope.preserveAspect = true;
            _scope.rectTransform.At(0.5f, 0.5f, 0, 0, 1080, 1080);
            var scopeL = Ui.Img(_scope.transform, Sprites.White, Color.black, "L");
            scopeL.rectTransform.At(0, 0.5f, -1000, 0, 2000, 1100);
            var scopeR = Ui.Img(_scope.transform, Sprites.White, Color.black, "R");
            scopeR.rectTransform.At(1, 0.5f, 1000, 0, 2000, 1100);
            _scope.gameObject.SetActive(false);
            _safe = Ui.Safe(_root);
            Touch = TouchControls.Create(_safe);

            // ---- minimap (top-left corner) and the health / armour panel to its right
            Minimap.Create(_safe, MapPos, MapSize);
            var vit = Ui.Rect(_safe, "Vitals");
            vit.anchorMin = vit.anchorMax = new Vector2(0, 1);
            vit.pivot = new Vector2(0, 1);
            vit.anchoredPosition = VitalsPos;
            var vs = VitalsSize;
            vit.sizeDelta = vs;
            var vbg = Ui.Img(vit, Sprites.Cut(16), Pal.Panel.A(0.6f), "Bg");
            vbg.rectTransform.Fill();
            // Icon | bar | number, the bar taking whatever width the panel has.
            float barL = 72, barW = vs.x - barL - 100, barC = barL + barW / 2, numC = vs.x - 48;
            var hic = Ui.Img(vit, Sprites.Of(Icon.Health), Pal.Health, "HIcon");
            hic.rectTransform.At(0, 1, 36, -31, 34, 34);
            _hp = Ui.Bar(vit, new Color(0, 0, 0, 0.6f), Pal.Health, out var hpRoot, "HP");
            hpRoot.At(0, 1, barC, -31, barW, 22);
            _hpText = Ui.Label(vit, "100", 30, Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
            _hpText.rectTransform.At(0, 1, numC, -31, 80, 40);
            var aic = Ui.Img(vit, Sprites.Of(Icon.Armor), Pal.Armor, "AIcon");
            aic.rectTransform.At(0, 1, 36, -71, 30, 30);
            _armor = Ui.Bar(vit, new Color(0, 0, 0, 0.6f), Pal.Armor, out var arRoot, "Armor");
            arRoot.At(0, 1, barC, -71, barW, 16);
            _armorText = Ui.Label(vit, "0", 26, Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
            _armorText.rectTransform.At(0, 1, numC, -71, 80, 36);

            // ---- stage & objective (top-centre)
            var top = Ui.Rect(_safe, "Objective");
            top.At(0.5f, 1, 0, -60, 760, 110);
            var tbg = Ui.Img(top, Sprites.Cut(16), Pal.Panel.A(0.6f), "Bg");
            tbg.rectTransform.Fill();
            var accent = Ui.Img(top, Sprites.White, Pal.Accent, "Accent");
            accent.rectTransform.At(0.5f, 1, 0, -2, 740, 3);
            _stage = Ui.Label(top, "STAGE 1", 26, Pal.Accent2, TextAnchor.UpperCenter, FontKind.Display);
            _stage.rectTransform.Fill(10, 8, 10, 0);
            _objective = Ui.Label(top, "", 34, Pal.Text, TextAnchor.MiddleCenter, FontKind.Display);
            _objective.rectTransform.Fill(10, 30, 10, 26);
            _objSub = Ui.Label(top, "", 22, Pal.Dim, TextAnchor.LowerCenter, FontKind.Semi);
            _objSub.rectTransform.Fill(10, 0, 10, 8);

            // ---- boss bar
            _bossRoot = Ui.Rect(_safe, "Boss");
            _bossRoot.At(0.5f, 1, 0, -200, 900, 70);
            _bossName = Ui.Label(_bossRoot, "", 30, Pal.Bad, TextAnchor.UpperCenter, FontKind.Display);
            _bossName.rectTransform.Fill();
            Ui.Shadow(_bossName, 2, 0.8f);
            _bossBack = Ui.Img(_bossRoot, Sprites.Rounded(6), new Color(1, 1, 1, 0.25f), "Flash");
            _bossBack.rectTransform.At(0.5f, 0, 0, 14, 900, 24);
            _bossFill = Ui.Bar(_bossRoot, new Color(0, 0, 0, 0.7f), Pal.Bad, out var bossBar, "BossHP");
            bossBar.At(0.5f, 0, 0, 14, 900, 24);
            for (int i = 1; i < 3; i++)
            {
                var tick = Ui.Img(bossBar, Sprites.White, new Color(1, 1, 1, 0.6f), "Tick");
                tick.rectTransform.At(i / 3f, 0.5f, 0, 0, 3, 24);
            }
            _bossRoot.gameObject.SetActive(false);

            // ---- top-right: kills, coins, pause, flashlight
            var tr = Ui.Rect(_safe, "TopRight");
            tr.anchorMin = tr.anchorMax = new Vector2(1, 1);
            tr.pivot = new Vector2(1, 1);
            tr.anchoredPosition = new Vector2(-24, -24);
            tr.sizeDelta = new Vector2(560, 80);
            _kills = Ui.Chip(tr, Icon.Skull, Pal.Text, "0", 30);
            _kills.transform.parent.GetComponent<RectTransform>().At(1, 0.5f, -470, 0, 150, 60);
            _coins = Ui.Chip(tr, Icon.Coin, Pal.Coin, "0", 30);
            _coins.transform.parent.GetComponent<RectTransform>().At(1, 0.5f, -290, 0, 190, 60);
            var fl = Ui.IconButton(tr, Icon.Flashlight, () => Controls.Press(TouchBtn.Flashlight), 64, Pal.Panel2.A(0.75f));
            fl.Bg.rectTransform.At(1, 0.5f, -140, 0, 64, 64);
            var pause = Ui.IconButton(tr, Icon.Pause, () => StageRunner.Instance?.Pause(), 64, Pal.Panel2.A(0.75f));
            pause.Bg.rectTransform.At(1, 0.5f, -38, 0, 64, 64);
            _fps = Ui.Label(_safe, "", 22, Pal.Dim, TextAnchor.UpperRight, FontKind.Semi);
            _fps.rectTransform.At(1, 1, -40, -112, 200, 30);

            // ---- ammo (bottom centre, right of the weapon slots)
            var ammo = Ui.Rect(_safe, "Ammo");
            ammo.anchorMin = ammo.anchorMax = new Vector2(0.5f, 0);
            ammo.pivot = new Vector2(0.5f, 0);
            ammo.anchoredPosition = new Vector2(240, 22);
            ammo.sizeDelta = new Vector2(330, 110);
            var abg = Ui.Img(ammo, Sprites.Cut(14), Pal.Panel.A(0.55f), "Bg");
            abg.rectTransform.Fill();
            _ammo = Ui.Label(ammo, "30", 64, Pal.Text, TextAnchor.MiddleRight, FontKind.Display);
            _ammo.rectTransform.At(0, 0.5f, 120, 6, 200, 80);
            _reserve = Ui.Label(ammo, "/ 210", 30, Pal.Dim, TextAnchor.MiddleLeft, FontKind.Display);
            _reserve.rectTransform.At(0, 0.5f, 262, -4, 120, 50);
            _weaponName = Ui.Label(ammo, "", 22, Pal.Accent2, TextAnchor.UpperRight, FontKind.Semi);
            _weaponName.rectTransform.Fill(10, 4, 14, 0);
            _grenades = Ui.Label(ammo, "", 24, Pal.Text, TextAnchor.LowerRight, FontKind.Display);
            _grenades.rectTransform.Fill(10, 0, 14, 6);
            _reloadRing = Ui.Img(_root, Sprites.Ring(10), Pal.Accent2, "ReloadRing");
            _reloadRing.type = Image.Type.Filled;
            _reloadRing.fillMethod = Image.FillMethod.Radial360;
            _reloadRing.rectTransform.At(0.5f, 0.5f, 0, 0, 90, 90);
            _reloadHint = Ui.Label(_root, "RELOAD", 28, Pal.Accent2, TextAnchor.MiddleCenter, FontKind.Display);
            _reloadHint.rectTransform.At(0.5f, 0.5f, 0, -90, 300, 40);

            // ---- weapon slots (bottom-centre)
            var slots = Ui.Rect(_safe, "Slots");
            slots.At(0.5f, 0, -190, 62, 420, 90);
            var guns = PlayerRig.Instance != null ? PlayerRig.Instance.Guns : null;
            int n = guns != null ? guns.Slots.Count : 1;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                float w = 130;
                var b = Ui.IconButton(slots, Sprites.ClassIcon(guns.Slots[i].Def.Class) == Icon.Pistol ? Icon.Pistol : Sprites.ClassIcon(guns.Slots[i].Def.Class), () => Controls.Select(idx), 80, Pal.Panel.A(0.6f));
                b.Bg.rectTransform.At(0.5f, 0.5f, (i - (n - 1) / 2f) * (w + 10), 0, w, 76);
                var ic = b.transform.Find("Icon").GetComponent<Image>();
                ic.rectTransform.Fill(18, 8, 18, 8);
                // Rendered gun picture when baked (Resources/Thumbs), else the class icon.
                var art = guns != null ? CardArt.Weapon(guns.Slots[i].Def.Id) : null;
                if (art != null)
                {
                    ic.sprite = art;
                    ic.preserveAspect = true;
                    ic.rectTransform.Fill(8, 2, 8, 2);
                    _slotArt.Add(true);
                }
                else _slotArt.Add(false);
                _slotIcons.Add(ic);
                _slotBgs.Add(b.Bg);
            }

            // ---- crosshair, hit marker, damage arcs
            _crossRoot = Ui.Rect(_root, "Crosshair");
            _crossRoot.At(0.5f, 0.5f, 0, 0, 10, 10);
            for (int i = 0; i < 4; i++)
            {
                var l = Ui.Img(_crossRoot, Sprites.White, new Color(1, 1, 1, 0.9f), "C" + i);
                l.rectTransform.sizeDelta = i % 2 == 0 ? new Vector2(3, 16) : new Vector2(16, 3);
                Ui.Outline(l, new Color(0, 0, 0, 0.5f), 1);
                _cross[i] = l;
            }
            var dot = Ui.Img(_crossRoot, Sprites.Circle, new Color(1, 1, 1, 0.9f), "Dot");
            dot.rectTransform.At(0.5f, 0.5f, 0, 0, 5, 5);
            _hitmark = Ui.Img(_root, Sprites.Of(Icon.Close), Color.white, "Hitmark");
            _hitmark.rectTransform.At(0.5f, 0.5f, 0, 0, 40, 40);
            _headIcon = Ui.Img(_root, Sprites.Of(Icon.Skull), Pal.Accent2, "Headshot");
            _headIcon.rectTransform.At(0.5f, 0.5f, 0, 56, 44, 44);
            _killSkull = Ui.Img(_root, Sprites.Of(Icon.Skull), Pal.Bad, "Kill");
            _killSkull.rectTransform.At(0.5f, 0.5f, 0, -60, 36, 36);
            for (int i = 0; i < 4; i++)
            {
                var a = Ui.Img(_root, Sprites.Glow, new Color(1, 0, 0, 0), "DmgArc");
                a.rectTransform.At(0.5f, 0.5f, 0, 0, 260, 90);
                _dmgArcs[i] = a;
            }

            // ---- objective marker
            _markerRoot = Ui.Rect(_root, "Marker");
            _markerRoot.At(0.5f, 0.5f, 0, 0, 60, 60);
            _markerIcon = Ui.Img(_markerRoot, Sprites.Of(Icon.Pin), Pal.Accent2, "Icon");
            _markerIcon.rectTransform.At(0.5f, 0.5f, 0, 0, 46, 46);
            Ui.Shadow(_markerIcon, 2, 0.7f);
            var arrow = Ui.Img(_markerRoot, Sprites.Of(Icon.Play), Pal.Accent2, "Arrow");
            _markerArrow = arrow.rectTransform;
            _markerArrow.At(0.5f, 0.5f, 0, 0, 30, 30);
            _marker = Ui.Label(_markerRoot, "", 22, Pal.Text, TextAnchor.UpperCenter, FontKind.Display);
            _marker.rectTransform.At(0.5f, 0.5f, 0, -46, 160, 30);
            Ui.Shadow(_marker, 2, 0.8f);
            _holdRing = Ui.Img(_root, Sprites.Ring(12), Pal.Good, "Hold");
            _holdRing.type = Image.Type.Filled;
            _holdRing.fillMethod = Image.FillMethod.Radial360;
            _holdRing.rectTransform.At(0.5f, 0.5f, 0, 0, 160, 160);
            _holdRing.gameObject.SetActive(false);

            // ---- announcements
            _announceRoot = Ui.Rect(_root, "Announce");
            _announceRoot.At(0.5f, 0.5f, 0, 210, 1400, 160);
            _announce = Ui.Title(_announceRoot, "", 86, Pal.Text, TextAnchor.MiddleCenter);
            _announce.rectTransform.Fill(0, 0, 0, 40);
            Ui.Shadow(_announce, 4, 0.8f);
            _announceSub = Ui.Label(_announceRoot, "", 32, Pal.Dim, TextAnchor.LowerCenter, FontKind.Semi);
            _announceSub.rectTransform.Fill();
            Ui.Shadow(_announceSub, 2, 0.8f);
            _announceRoot.gameObject.SetActive(false);
            foreach (var g in Canvas.GetComponentsInChildren<Graphic>(true)) if (g.GetComponent<UiButton>() == null && g.transform.parent?.GetComponent<UiButton>() == null && !g.transform.IsChildOf(Touch.transform)) g.raycastTarget = false;
        }

        // --------------------------------------------------------------- static API

        public static void Announce(string title, string sub, Color c)
        {
            var h = Instance;
            if (h == null) { Screens.Toast(title + (string.IsNullOrEmpty(sub) ? "" : " - " + sub)); return; }
            h._announce.text = title;
            h._announce.color = c;
            h._announceSub.text = sub;
            h._announceT = 2.6f;
            h._announceRoot.gameObject.SetActive(true);
        }

        public static void Toast(string msg) => Screens.Toast(msg);

        public static void BossHit()
        {
            if (Instance != null) Instance._bossFlash = 0.15f;
        }

        public static void Damage(Vector3 world, float dmg, bool head, bool crit)
        {
            var h = Instance;
            if (h == null) return;
            var t = Ui.Label(h._root, Mathf.RoundToInt(dmg).ToString(), head ? 38 : 32, head ? Pal.Accent2 : crit ? Pal.Accent : Color.white, TextAnchor.MiddleCenter, FontKind.Display, "Dmg");
            t.rectTransform.sizeDelta = new Vector2(160, 50);
            Ui.Shadow(t, 2, 0.8f);
            h._floats.Add((t, world, 0.7f));
            if (h._floats.Count > 12) { Destroy(h._floats[0].t.gameObject); h._floats.RemoveAt(0); }
        }

        public void OnHit(bool kill, bool head)
        {
            _hitT = 0.18f;
            if (kill) _killT = 0.5f;
            if (head) _headT = 0.5f;
            _hitmark.color = kill ? Pal.Bad : Color.white;
            Sfx.Hitmarker(kill, head);
        }

        public void OnHurt(float amount, Vector3 from, HurtKind kind)
        {
            _dmgFrom[_dmgNext] = from;
            _dmgT[_dmgNext] = 1.2f;
            _dmgNext = (_dmgNext + 1) % 4;
        }

        public void CoinsPulse() => _coinsPulse = 0.3f;

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var p = PlayerRig.Instance;
            var run = StageRunner.Instance;
            if (p == null || run == null) return;
            // Vitals.
            Ui.SetFill(_hp, p.Hp / p.MaxHp);
            _hp.color = p.Hp < p.MaxHp * 0.3f ? Color.Lerp(Pal.Bad, Pal.Health, Mathf.PingPong(Time.time * 3, 1) * 0.3f) : Pal.Health;
            _hpText.text = Mathf.CeilToInt(p.Hp).ToString();
            float maxA = Mathf.Max(p.MaxArmor, 50);
            Ui.SetFill(_armor, p.Armor / maxA);
            _armorText.text = Mathf.CeilToInt(p.Armor).ToString();
            // Objective.
            _stage.text = run.Plan.Title + "  -  " + run.Plan.Subtitle;
            _objective.text = run.ObjectiveLine;
            _objSub.text = run.ObjectiveSub;
            // Kills / coins.
            _kills.text = run.Kills.ToString();
            if (_shownCoins != run.CoinsEarned) _shownCoins = (long)Mathf.MoveTowards(_shownCoins, run.CoinsEarned, Mathf.Max(1, Mathf.Abs(run.CoinsEarned - _shownCoins)) * dt * 8 + 1);
            _coins.text = X.Money(_shownCoins);
            _coinsPulse = Mathf.Max(0, _coinsPulse - dt);
            _coins.transform.parent.localScale = Vector3.one * (1 + _coinsPulse * 0.4f);
            _fps.text = SaveSystem.Data.Settings.ShowFps ? Mathf.RoundToInt(Quality.SmoothedFps) + " FPS" : "";
            // Ammo / weapon.
            var g = p.Guns;
            var w = g.W;
            _ammo.text = w.Mag.ToString();
            _ammo.color = w.Mag <= Mathf.Max(1, w.St.Mag / 5) ? Pal.Bad : Pal.Text;
            _reserve.text = w.Infinite ? "/ ∞" : "/ " + w.Reserve;
            _weaponName.text = w.Def.Name.ToUpper();
            _grenades.text = "GRENADES  " + g.Grenades;
            _reloadRing.gameObject.SetActive(g.Reloading);
            _reloadRing.fillAmount = g.ReloadProgress;
            bool noAmmo = !g.Reloading && w.Mag == 0 && !w.Infinite && w.Reserve == 0;
            _reloadHint.gameObject.SetActive(g.GrenadeAiming || noAmmo);
            if (g.GrenadeAiming) _reloadHint.text = "RELEASE TO THROW";
            else if (noAmmo) _reloadHint.text = "NO AMMO - FIND SUPPLIES";
            for (int i = 0; i < _slotBgs.Count; i++)
            {
                bool cur = i == g.Current;
                _slotBgs[i].color = cur ? Pal.Accent.A(0.85f) : Pal.Panel.A(0.6f);
                _slotIcons[i].color = cur ? Color.white : _slotArt[i] ? new Color(0.62f, 0.64f, 0.68f, 0.85f) : Pal.Dim;
            }
            // Crosshair.
            bool scoped = g.Scoped;
            _scope.gameObject.SetActive(scoped);
            _crossRoot.gameObject.SetActive(!scoped && g.AimAmount < 0.85f);
            float spreadPx = 8 + g.Spread * 9;
            _cross[0].rectTransform.anchoredPosition = new Vector2(0, spreadPx + 8);
            _cross[1].rectTransform.anchoredPosition = new Vector2(spreadPx + 8, 0);
            _cross[2].rectTransform.anchoredPosition = new Vector2(0, -spreadPx - 8);
            _cross[3].rectTransform.anchoredPosition = new Vector2(-spreadPx - 8, 0);
            var cc = g.AssistTarget != null ? Pal.Bad : Color.white;
            foreach (var c in _cross) c.color = cc.A(0.9f);
            _hitT -= dt; _killT -= dt; _headT -= dt;
            _hitmark.gameObject.SetActive(_hitT > 0);
            _hitmark.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, _hitT) * 2);
            _headIcon.gameObject.SetActive(_headT > 0);
            _killSkull.gameObject.SetActive(_killT > 0);
            // Damage direction arcs.
            var cam = PlayerRig.Cam;
            for (int i = 0; i < 4; i++)
            {
                _dmgT[i] -= dt;
                var a = _dmgArcs[i];
                if (_dmgT[i] <= 0 || cam == null) { a.color = new Color(1, 0, 0, 0); continue; }
                var to = (_dmgFrom[i] - cam.transform.position).Flat();
                float ang = Vector3.SignedAngle(cam.transform.forward.Flat(), to, Vector3.up);
                a.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, -ang) * new Vector3(0, 230, 0);
                a.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
                a.color = new Color(1, 0.1f, 0.05f, Mathf.Clamp01(_dmgT[i]) * 0.85f);
            }
            // Boss.
            var boss = Boss.Current;
            bool showBoss = boss != null && boss.IsAlive;
            _bossRoot.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                _bossName.text = boss.Name + "   " + Mathf.CeilToInt(boss.Hp).ToString("#,0");
                Ui.SetFill(_bossFill, boss.Hp / boss.MaxHp);
                _bossFlash = Mathf.Max(0, _bossFlash - dt);
                _bossBack.color = new Color(1, 1, 1, _bossFlash * 3);
            }
            // Objective marker.
            UpdateMarker(run, cam);
            // Hold ring.
            var hold = run.HoldTarget;
            _holdRing.gameObject.SetActive(hold != null && hold.Hold > 0 && !hold.Done);
            if (hold != null) _holdRing.fillAmount = hold.Hold / hold.HoldNeeded;
            // Announcement.
            if (_announceT > 0)
            {
                _announceT -= dt;
                float k = Mathf.Clamp01(_announceT / 0.4f) * Mathf.Clamp01((2.6f - _announceT) / 0.25f);
                _announce.color = _announce.color.A(k);
                _announceSub.color = Pal.Dim.A(k);
                _announceRoot.localScale = Vector3.one * (1 + (1 - Mathf.Clamp01((2.6f - _announceT) / 0.25f)) * 0.3f);
                if (_announceT <= 0) _announceRoot.gameObject.SetActive(false);
            }
            // Floating numbers.
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var f = _floats[i];
                f.life -= dt;
                if (f.life <= 0 || cam == null) { Destroy(f.t.gameObject); _floats.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(f.world + Vector3.up * (0.7f - f.life));
                if (sp.z < 0) { f.t.enabled = false; }
                else
                {
                    f.t.enabled = true;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var lp);
                    f.t.rectTransform.anchorMin = f.t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    f.t.rectTransform.anchoredPosition = lp;
                    f.t.color = f.t.color.A(Mathf.Clamp01(f.life * 2));
                }
                _floats[i] = f;
            }
            _vignette.color = new Color(0, 0, 0, run.Level != null && run.Level.Atmosphere.Darkness > 0.5f ? 0.65f : 0.45f);
        }

        void UpdateMarker(StageRunner run, Camera cam)
        {
            var target = run.MarkerTarget;
            if (target == null || cam == null)
            {
                _markerRoot.gameObject.SetActive(false);
                return;
            }
            _markerRoot.gameObject.SetActive(true);
            _markerIcon.sprite = Sprites.Of(run.MarkerIcon);
            var world = target.Value + Vector3.up * 1.6f;
            var sp = cam.WorldToScreenPoint(world);
            var size = new Vector2(Screen.width, Screen.height);
            bool behind = sp.z < 0;
            if (behind) sp = new Vector3(size.x - sp.x, size.y - sp.y, -sp.z);
            var center = size / 2;
            var dir = new Vector2(sp.x, sp.y) - center;
            bool onScreen = !behind && sp.x > 60 && sp.x < size.x - 60 && sp.y > 120 && sp.y < size.y - 200;
            Vector2 screen;
            if (onScreen) screen = sp;
            else
            {
                if (dir.sqrMagnitude < 1) dir = Vector2.down;
                float sx = (size.x / 2 - 90) / Mathf.Abs(dir.x + 1e-4f), sy = (size.y / 2 - 160) / Mathf.Abs(dir.y + 1e-4f);
                screen = center + dir * Mathf.Min(sx, sy);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var lp);
            _markerRoot.anchoredPosition = lp;
            _markerArrow.gameObject.SetActive(!onScreen);
            if (!onScreen)
            {
                float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _markerArrow.localRotation = Quaternion.Euler(0, 0, a);
                _markerArrow.anchoredPosition = dir.normalized * 38;
            }
            float dist = (target.Value - PlayerRig.Pos).Flat().magnitude;
            _marker.text = Mathf.RoundToInt(dist) + "m";
        }
    }
}
