using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FRF
{
    /// <summary>In-race HUD, touch controls, pause menu and results card.</summary>
    public sealed class Hud : MonoBehaviour
    {
        RaceDirector _d;
        RectTransform _root, _overlay;
        Text _place, _placeOf, _time, _speed, _goal, _bigText, _bigSub, _trickName, _trickPts, _boostCount;
        Image _health, _flash, _progress, _gaugeFill, _adrRing, _tiltDot;
        readonly List<Image> _dots = new List<Image>();
        RectTransform _bar, _big, _trick, _intro, _boostRing, _boostBadge, _tilt, _tiltTrack;
        HoldButton _btnA, _btnB, _btnC, _btnBoost;
        SteerPad _pad;
        float _steerShown;

        // Deep-space glass tint for HUD panels
        static readonly Color Glass     = new Color(0.02f, 0.03f, 0.07f, 0.70f);
        // Slightly lighter for bordered panels
        static readonly Color GlassBord = new Color(0.03f, 0.04f, 0.09f, 0.72f);

        float _bigT, _bigDur, _trickT, _flashT;
        bool _air;
        readonly List<(Text T, float Life)> _toasts = new List<(Text, float)>();
        RectTransform _toastRoot;
        int _lastCount = -1;

        public static Hud Create(RectTransform parent, RaceDirector d)
        {
            var root = Ui.Node(parent, "Hud").Fill();
            var h = root.gameObject.AddComponent<Hud>();
            h._d = d;
            h._root = root;
            h.Build();
            return h;
        }

        // ------------------------------------------------------------ vignette

        static Sprite _vignette;
        static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int s = 64;
                var px = new Px(s, s, new Color32(255, 255, 255, 0));
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy) / 1.41f;
                    px.P[y * s + x] = new Color(1, 1, 1, Tex.SmoothStep(0.40f, 1f, d));
                }
                var t = px.ToTexture("Vignette", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
                return _vignette = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            }
        }

        // ------------------------------------------------------------ build

        void Build()
        {
            var r = _root;

            // Full-screen colour flash vignette
            _flash = Ui.Img(r, Vignette, new Color(1, 0, 0, 0), "Flash");
            _flash.preserveAspect = false;
            _flash.rectTransform.Fill();

            // ---- POSITION PANEL (top-left) ----------------------------------
            // Slanted glass chip
            var posBg = Ui.Img(r, Ui.Slant, GlassBord, "PosPanel");
            posBg.type = Image.Type.Sliced;
            posBg.preserveAspect = false;
            posBg.rectTransform.At(0, 1, -30, -6, 248, 64);

            // Orange neon underline
            var posBar = Ui.Img(r, Ui.Round(2), Pal.Orange, "PosBar");
            posBar.type = Image.Type.Sliced;
            posBar.preserveAspect = false;
            posBar.rectTransform.At(0, 1, 0, -68, 208, 3);

            // Neon glow behind the bar
            var posBarGlow = Ui.Img(r, Ui.NeonGlow, Pal.Orange.WithA(0.35f), "PosBarGlow");
            posBarGlow.preserveAspect = false;
            posBarGlow.rectTransform.At(0, 1, 0, -70, 200, 14);

            _place = Ui.Label(r, "1ST", 58, Color.white, TextAnchor.MiddleLeft, true, "Place");
            _place.rectTransform.At(0, 1, 14, -4, 160, 64);
            Ui.Shadow(_place, 2.5f, 0.7f);
            _placeOf = Ui.Label(r, "/6", 24, Pal.Dim, TextAnchor.MiddleLeft, true, "PlaceOf");
            _placeOf.rectTransform.At(0, 1, 106, -16, 64, 34);

            // Goal pill
            var goalBg = Ui.Panel(r, Glass, 10, "GoalPill");
            goalBg.raycastTarget = false;
            goalBg.rectTransform.At(0, 1, 8, -76, 310, 24);
            _goal = Ui.Label(goalBg.transform, "", 15, Pal.Text, TextAnchor.MiddleLeft, false, "Goal");
            _goal.rectTransform.Fill(10, 0, 8, 0);

            // ---- TIMER & PROGRESS BAR (top-centre) -------------------------
            var timeBg = Ui.Panel(r, GlassBord, 18, "TimePill", Color.white.WithA(0.18f));
            timeBg.raycastTarget = false;
            timeBg.rectTransform.At(0.5f, 1, 0, -6, 164, 38);
            _time = Ui.Label(timeBg.transform, "0:00.00", 29, Color.white, TextAnchor.MiddleCenter, true, "Time");
            _time.rectTransform.Fill();
            Ui.Shadow(_time, 2, 0.7f);

            // Course progress bar
            var barBg = Ui.Panel(r, new Color(0, 0, 0, 0.55f), 5, "Progress", Color.white.WithA(0.20f));
            barBg.raycastTarget = false;
            _bar = barBg.rectTransform;
            _bar.At(0.5f, 1, -10, -54, 392, 10);

            _progress = Ui.Img(_bar, Ui.Round(4), Pal.Orange, "Fill");
            _progress.type = Image.Type.Sliced;
            _progress.preserveAspect = false;
            _progress.rectTransform.Fill(1.5f, 1.5f, 1.5f, 1.5f);

            // Progress glow
            var progGlow = Ui.Img(_bar, Ui.NeonGlow, Pal.Orange.WithA(0.30f), "FillGlow");
            progGlow.preserveAspect = false;
            progGlow.rectTransform.Fill(-4, -6, -4, -6);

            var flag = Ui.Img(_bar, Icons.Flag, Color.white, "Flag");
            flag.rectTransform.At(1, 0.5f, 28, 4, 28, 28);

            // Rider dots on the progress bar
            foreach (var rider in _d.Riders)
            {
                var dot = Ui.Img(_bar, Ui.Circle, rider.Def.Jersey, "Dot");
                float size = rider.IsPlayer ? 20 : 12;
                dot.rectTransform.At(0, 0.5f, 0, 0, size, size);
                var ring = Ui.Img(dot.transform, Ui.Ring, rider.IsPlayer ? Color.white : new Color(0, 0, 0, 0.55f), "Ring");
                ring.rectTransform.Fill(-2, -2, -2, -2);
                _dots.Add(dot);
            }

            // Toasts (left side, below goal pill)
            _toastRoot = Ui.Node(r, "Toasts");
            _toastRoot.At(0, 1, 14, -108, 420, 130);

            // ---- PAUSE BUTTON (top-right) -----------------------------------
            var pause = Ui.Node(r, "Pause");
            pause.At(1, 1, -10, -8, 52, 52);
            var pBg = pause.gameObject.AddComponent<Image>();
            pBg.sprite = Ui.Circle;
            pBg.color = GlassBord;
            var pRing = Ui.Img(pause, Icons.Ring(0.86f), Pal.Cyan.WithA(0.40f), "Ring");
            pRing.rectTransform.Fill();
            var pIcon = Ui.Img(pause, Icons.Pause, Color.white, "Icon");
            pIcon.rectTransform.At(0.5f, 0.5f, 0, 0, 26, 26);
            Ui.Tap(pause, () => ShowPause());

            // ---- SPEED GAUGE (bottom-centre) --------------------------------
            var g = Ui.Node(r, "Gauge");
            g.At(0.5f, 0, 0, 14, 132, 132);

            // Outer glow ring
            var gaugeGlow = Ui.Img(g, Ui.Circle, Pal.Green.WithA(0.08f), "GaugeGlow");
            gaugeGlow.preserveAspect = true;
            gaugeGlow.rectTransform.Fill(-8, -8, -8, -8);

            var disk = Ui.Img(g, Ui.Circle, new Color(0.01f, 0.02f, 0.05f, 0.65f), "Disk");
            disk.rectTransform.Fill(7, 7, 7, 7);

            var arcBg = Ui.Img(g, Icons.Ring(0.80f), Color.white.WithA(0.12f), "ArcBg");
            Arc(arcBg, 0.75f);

            _gaugeFill = Ui.Img(g, Icons.Ring(0.80f), Pal.Green, "Arc");
            Arc(_gaugeFill, 0);

            _speed = Ui.Label(g, "0", 42, Color.white, TextAnchor.MiddleCenter, true, "Speed");
            _speed.rectTransform.At(0.5f, 0.5f, 0, 10, 120, 46);
            Ui.Shadow(_speed, 2.5f, 0.7f);

            var kmh = Ui.Label(g, "KM/H", 12, Pal.Dim, TextAnchor.MiddleCenter, true, "Kmh");
            kmh.rectTransform.At(0.5f, 0.5f, 0, -14, 80, 14);

            // Balance bar inside gauge
            var hpBg = Ui.Img(g, Ui.Round(3), new Color(0, 0, 0, 0.65f), "BalanceBg");
            hpBg.type = Image.Type.Sliced;
            hpBg.preserveAspect = false;
            hpBg.rectTransform.At(0.5f, 0.5f, 0, -34, 64, 8);

            _health = Ui.Img(hpBg.transform, Ui.Round(3), Pal.Green, "Balance");
            _health.type = Image.Type.Sliced;
            _health.preserveAspect = false;
            _health.rectTransform.Fill();

            var hl = Ui.Label(r, "BALANCE", 10, Pal.Mute, TextAnchor.MiddleCenter, false, "BalanceLabel");
            hl.rectTransform.At(0.5f, 0, 0, 2, 120, 11);

            // ---- STEERING (bottom-left) ------------------------------------
            _pad = SteerPad.Create(r, 280, 126);
            ((RectTransform)_pad.transform).At(0, 0, 12, 14, 280, 126);

            _tilt = Ui.Panel(r, GlassBord, 14, "Tilt", Color.white.WithA(0.18f)).rectTransform;
            _tilt.At(0, 0, 12, 14, 238, 60);
            var tl = Ui.Label(_tilt, "TILT TO STEER", 14, Pal.Dim, TextAnchor.UpperCenter, true, "Label");
            tl.rectTransform.Fill(0, 4, 0, 0);
            var track = Ui.Img(_tilt, Ui.Round(4), Color.white.WithA(0.16f), "Track");
            track.type = Image.Type.Sliced;
            track.preserveAspect = false;
            _tiltTrack = track.rectTransform;
            _tiltTrack.At(0.5f, 0, 0, 14, 188, 8);
            var mid = Ui.Block(_tiltTrack, Color.white.WithA(0.45f), "Mid");
            mid.rectTransform.At(0.5f, 0.5f, 0, 0, 2, 14);
            _tiltDot = Ui.Img(_tiltTrack, Ui.Circle, Pal.Orange, "Dot");
            _tiltDot.rectTransform.At(0.5f, 0.5f, 0, 0, 20, 20);

            // ---- ACTION BUTTONS (bottom-right) ------------------------------
            // JUMP (largest)
            _btnB = HoldButton.Create(r, "JUMP",  Pal.Orange,                             126, Icons.Jump);
            ((RectTransform)_btnB.transform).At(1, 0, -14, 14, 126, 126);
            // PUNCH
            _btnA = HoldButton.Create(r, "PUNCH", Pal.Red,                                100, Icons.Punch);
            ((RectTransform)_btnA.transform).At(1, 0, -154, 12, 100, 100);
            // BRAKE
            _btnC = HoldButton.Create(r, "BRAKE", new Color(0.30f, 0.34f, 0.45f),          84, Icons.Brake);
            ((RectTransform)_btnC.transform).At(1, 0, -32, 154, 84, 84);

            // Adrenaline ring behind BOOST
            _boostRing = Ui.Node(r, "AdrenalineRing");
            _boostRing.At(1, 0, -138, 118, 120, 120);
            var ringBg = Ui.Img(_boostRing, Icons.Ring(0.78f), new Color(0, 0, 0, 0.50f), "Bg");
            ringBg.rectTransform.Fill();
            _adrRing = Ui.Img(_boostRing, Icons.Ring(0.78f), Pal.Cyan, "Fill");
            _adrRing.rectTransform.Fill();
            _adrRing.type = Image.Type.Filled;
            _adrRing.fillMethod = Image.FillMethod.Radial360;
            _adrRing.fillOrigin = (int)Image.Origin360.Top;
            _adrRing.fillClockwise = true;
            _adrRing.fillAmount = 0;
            for (int i = 0; i < 4; i++)
            {
                var tick = Ui.Block(_boostRing, new Color(0.01f, 0.02f, 0.05f, 0.95f), "Tick");
                float a = i * Mathf.PI / 2;
                tick.rectTransform.At(0.5f, 0.5f, Mathf.Sin(a) * 53, Mathf.Cos(a) * 53, 3, 16);
                tick.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 90);
            }

            _btnBoost = HoldButton.Create(r, "BOOST", Pal.CyanDeep, 90, Icons.Bolt);
            ((RectTransform)_btnBoost.transform).At(1, 0, -152, 132, 90, 90);

            var badge = Ui.Img(r, Ui.Circle, Pal.Cyan, "BoostBadge");
            _boostBadge = badge.rectTransform;
            _boostBadge.At(1, 0, -130, 208, 30, 30);
            _boostCount = Ui.Label(_boostBadge, "1", 18, new Color(0.01f, 0.08f, 0.12f), TextAnchor.MiddleCenter, true, "Count");
            _boostCount.rectTransform.Fill();

            // ---- CENTRE BIG MESSAGE ----------------------------------------
            _big = Ui.Node(r, "Big");
            _big.At(0.5f, 0.5f, 0, 52, 720, 138);
            _bigText = Ui.Label(_big, "", 88, Color.white, TextAnchor.MiddleCenter, true, "Text");
            _bigText.rectTransform.Fill(0, 0, 0, 34);
            Ui.Outline(_bigText, new Color(0, 0, 0, 0.7f), 3.5f);
            _bigSub = Ui.Label(_big, "", 22, Color.white, TextAnchor.LowerCenter, false, "Sub");
            _bigSub.rectTransform.Fill();
            Ui.Shadow(_bigSub, 2, 0.85f);
            _big.gameObject.SetActive(false);

            // Trick display
            _trick = Ui.Node(r, "Trick");
            _trick.At(0.5f, 0.5f, 0, 124, 720, 84);
            _trickName = Ui.Label(_trick, "", 30, Pal.Yellow, TextAnchor.UpperCenter, true, "Name");
            _trickName.rectTransform.Fill();
            Ui.Outline(_trickName, new Color(0, 0, 0, 0.65f), 2);
            _trickPts = Ui.Label(_trick, "", 26, Color.white, TextAnchor.LowerCenter, true, "Pts");
            _trickPts.rectTransform.Fill();
            Ui.Outline(_trickPts, new Color(0, 0, 0, 0.65f), 2);
            _trick.gameObject.SetActive(false);

            BuildTags();
            Intro();
            ApplyControlMode();
        }

        // ------------------------------------------------------------ rival tags

        sealed class Tag
        {
            public RectTransform Rt;
            public Text Name;
            public Image Bar;
        }

        readonly List<Tag> _tags = new List<Tag>();

        void BuildTags()
        {
            var layer = Ui.Node(_root, "Tags").Fill();
            layer.SetAsFirstSibling();
            for (int i = 0; i < 5; i++)
            {
                var rt = Ui.Node(layer, "Tag");
                rt.sizeDelta = new Vector2(94, 28);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var name = Ui.Label(rt, "", 15, Color.white, TextAnchor.UpperCenter, true, "Name");
                name.rectTransform.Fill();
                Ui.Outline(name, new Color(0, 0, 0, 0.75f), 1.5f);
                var bg = Ui.Img(rt, Ui.Round(4), new Color(0, 0, 0, 0.60f), "BarBg");
                bg.type = Image.Type.Sliced;
                bg.preserveAspect = false;
                bg.rectTransform.At(0.5f, 0, 0, 0, 60, 6);
                var bar = Ui.Img(bg.transform, Ui.Round(4), Pal.Green, "Bar");
                bar.type = Image.Type.Sliced;
                bar.preserveAspect = false;
                bar.rectTransform.Fill();
                rt.gameObject.SetActive(false);
                _tags.Add(new Tag { Rt = rt, Name = name, Bar = bar });
            }
        }

        /// <summary>Names and balance bars over rivals close to the camera.</summary>
        void UpdateTags()
        {
            var cam = Camera.main;
            int used = 0;
            if (cam != null && (_d.Phase == RacePhase.Racing || _d.Phase == RacePhase.Countdown))
            {
                var parentRt = (RectTransform)_tags[0].Rt.parent;
                foreach (var r in _d.Riders)
                {
                    if (used >= _tags.Count) break;
                    if (r.IsPlayer || r.Crashed) continue;
                    var wp   = r.WorldPos + Vector3.up * 2.1f;
                    float dist = Vector3.Distance(wp, cam.transform.position);
                    if (dist > 32) continue;
                    var sp = cam.WorldToScreenPoint(wp);
                    if (sp.z <= 0.5f) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, sp, null, out var local)) continue;
                    var tag = _tags[used++];
                    tag.Rt.gameObject.SetActive(true);
                    tag.Rt.anchoredPosition = local;
                    float s = Mathf.Lerp(1.1f, 0.7f, dist / 32);
                    tag.Rt.localScale = new Vector3(s, s, 1);
                    tag.Name.text  = r.Def.Nick;
                    tag.Name.color = r.HitFlash > 0 ? Pal.Red : Color.Lerp(r.Def.Jersey, Color.white, 0.45f);
                    float hp = Mathf.Clamp01(r.Health / 100f);
                    tag.Bar.rectTransform.anchorMax = new Vector2(hp, 1);
                    tag.Bar.color = hp > 0.6f ? Pal.Green : hp > 0.3f ? Pal.Yellow : Pal.Red;
                }
            }
            for (int i = used; i < _tags.Count; i++)
                if (_tags[i].Rt.gameObject.activeSelf) _tags[i].Rt.gameObject.SetActive(false);
        }

        void Intro()
        {
            var cfg  = _d.Cfg;
            var card = Ui.Panel(_root, Pal.Card, 14, "Intro", Pal.Orange.WithA(0.80f));
            _intro = card.rectTransform;
            _intro.At(0.5f, 1, 0, -68, 450, cfg.Event != null ? 90 : 66);

            // Orange glow behind intro card
            var introGlow = Ui.Img(_root, Ui.NeonGlow, Pal.Orange.WithA(0.16f), "IntroGlow");
            introGlow.preserveAspect = false;
            introGlow.rectTransform.At(0.5f, 1, 0, -56, 480, 120);

            var t = Ui.Label(_intro, cfg.Title, 24, Pal.Orange, TextAnchor.UpperCenter, true, "Title");
            t.rectTransform.Fill(8, 4, 8, 0);
            Ui.Shadow(t, 1.5f, 0.6f);
            var s = Ui.Label(_intro, cfg.Track.Name + "  ·  " + cfg.Track.Tagline, 16, Pal.Text, TextAnchor.UpperCenter, false, "Track");
            s.rectTransform.Fill(8, 32, 8, 0);
            if (cfg.Event != null)
            {
                var gl = Ui.Label(_intro, "GOAL: " + cfg.Event.Goal.ToUpperInvariant(), 16, Pal.Yellow, TextAnchor.UpperCenter, false, "Goal");
                gl.rectTransform.Fill(8, 58, 8, 0);
            }
        }

        public void ApplyControlMode()
        {
            bool tilt = Settings.Steer == SteerMode.Tilt;
            _pad.gameObject.SetActive(!tilt && _controlsOn);
            _tilt.gameObject.SetActive(tilt && _controlsOn);
            _pad.Release();
        }

        /// <summary>270-degree gauge arc, open at the bottom.</summary>
        static void Arc(Image img, float fill)
        {
            img.rectTransform.Fill();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Bottom;
            img.fillClockwise = true;
            img.fillAmount = fill;
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
        }

        // ------------------------------------------------------------ input

        static float TiltValue()
        {
            var a     = Input.acceleration;
            float raw = Screen.orientation == ScreenOrientation.LandscapeRight ? a.y : -a.y;
            float full = Settings.TiltSensitivity == 0 ? 0.45f : Settings.TiltSensitivity == 1 ? 0.32f : 0.22f;
            float v   = raw / full;
            if (Mathf.Abs(v) < 0.08f) v = 0;
            return Mathf.Clamp(v, -1, 1);
        }

        public RiderInput ReadInput()
        {
            var inp = new RiderInput();
            float k = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) k -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) k += 1;
            _pad.KeyValue = k;
            Key(_btnA, KeyCode.J);
            Key(_btnB, KeyCode.Space);
            Key(_btnC, KeyCode.K);
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.LeftShift)) _btnBoost.PressFromKey();

            float steer = Settings.Steer == SteerMode.Tilt ? Mathf.Clamp(TiltValue() + k, -1, 1) : _pad.Value;
            inp.Steer  = steer;
            _steerShown = steer;
            inp.A      = _btnA.Held;
            inp.B      = _btnB.Held;
            inp.C      = _btnC.Held;
            inp.ADown  = _btnA.TakeDown();
            inp.BDown  = _btnB.TakeDown();
            inp.CDown  = _btnC.TakeDown();
            inp.BoostDown = _btnBoost.TakeDown();
            return inp;
        }

        static void Key(HoldButton b, KeyCode key)
        {
            b.KeyHeld = Input.GetKey(key);
            if (Input.GetKeyDown(key)) b.PressFromKey();
        }

        // ------------------------------------------------------------ messages

        public void Big(string text, Color c, float duration, string sub = null)
        {
            _big.gameObject.SetActive(true);
            _bigText.text = text;
            _bigText.color = c;
            _bigSub.text = sub ?? "";
            _bigT = 0;
            _bigDur = duration;
        }

        public void Trick(string names, int points)
        {
            _trick.gameObject.SetActive(true);
            _trickName.text = names;
            _trickPts.text  = "+" + points.ToString("N0");
            _trickT = 0;
        }

        public void Toast(string text, Color c)
        {
            var t = Ui.Label(_toastRoot, text, 20, c, TextAnchor.MiddleLeft, true, "Toast");
            Ui.Outline(t, new Color(0, 0, 0, 0.65f), 1.5f);
            t.rectTransform.At(0, 1, 0, 0, 420, 24);
            _toasts.Insert(0, (t, 1.8f));
            if (_toasts.Count > 4)
            {
                Destroy(_toasts[4].T.gameObject);
                _toasts.RemoveAt(4);
            }
        }

        Text _coach;
        float _coachT;

        /// <summary>Tutorial hint in a banner above the action buttons.</summary>
        public void Coach(string text, Color c)
        {
            if (_coach == null)
            {
                var bg = Ui.Panel(_root, Glass, 10, "Coach", Color.white.WithA(0.18f));
                bg.raycastTarget = false;
                bg.rectTransform.At(0.5f, 0, -10, 154, 480, 34);
                _coach = Ui.Label(bg.transform, "", 16, c, TextAnchor.MiddleCenter, true, "Text");
                _coach.rectTransform.Fill(8, 0, 8, 0);
            }
            _coach.text  = text;
            _coach.color = c;
            _coach.transform.parent.gameObject.SetActive(true);
            _coachT = 3.4f;
        }

        public void Flash(Color c)
        {
            _flash.color = c.WithA(0.60f);
            _flashT = 0.45f;
        }

        // ------------------------------------------------------------ per frame

        public void Tick(float dt)
        {
            var d = _d;
            var p = d.Player;

            // Countdown
            if (d.Phase == RacePhase.Countdown)
            {
                int n = Mathf.CeilToInt(d.Countdown);
                if (n != _lastCount && n <= 3 && n >= 1) Big(n.ToString(), Pal.Yellow, 0.95f);
                _lastCount = n;
            }
            if (_intro != null && d.Phase == RacePhase.Racing && d.Clock > 1.2f)
            {
                Destroy(_intro.gameObject);
                _intro = null;
            }

            // Place
            _place.text  = Ui.Ord(p.Place);
            _place.color = p.Place == 1 ? Pal.Gold : Color.white;
            _placeOf.text = "/" + d.Riders.Count;
            _placeOf.rectTransform.anchoredPosition = new Vector2(14 + _place.preferredWidth + 2, -18);

            // Timer
            _time.text = Ui.Time(d.Clock);

            // Speed
            _speed.text  = Mathf.RoundToInt(p.Kmh).ToString();
            _speed.color = p.Boosting ? Pal.Yellow : Color.white;

            // Gauge colour: green → yellow → orange → cyan on boost
            float sp = Mathf.Clamp01(p.Kmh / 90f);
            _gaugeFill.fillAmount = Mathf.Lerp(_gaugeFill.fillAmount, 0.75f * sp, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
            _gaugeFill.color = p.Boosting ? Pal.Cyan
                : sp < 0.55f ? Color.Lerp(Pal.Green, Pal.Yellow, sp / 0.55f)
                : Color.Lerp(Pal.Yellow, Pal.Orange, (sp - 0.55f) / 0.45f);

            _progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p.S / d.Track.FinishS), 1);

            // Tilt indicator
            if (_tilt.gameObject.activeSelf)
            {
                float half = _tiltTrack.rect.width * 0.5f - 6;
                var dp = _tiltDot.rectTransform;
                dp.anchoredPosition = new Vector2(Mathf.Lerp(dp.anchoredPosition.x, _steerShown * half, Time.unscaledDeltaTime * 14), 0);
                _tiltDot.color = Mathf.Abs(_steerShown) > 0.1f ? Pal.Orange : Color.white;
            }

            // Goal text
            var ev = d.Cfg.Event;
            if (ev != null && ev.Kind == EventKind.Tricks)   _goal.text = "TRICKS  " + p.TrickScore.ToString("N0") + " / " + ev.Bronze.ToString("N0");
            else if (ev != null && ev.Kind == EventKind.Takedown) _goal.text = "TAKEDOWNS  " + p.Takedowns + " / " + ev.Bronze;
            else _goal.text = "TRICKS " + p.TrickScore.ToString("N0") + "   ·   TAKEDOWNS " + p.Takedowns;

            // Progress dots
            float w = _bar.rect.width;
            for (int i = 0; i < d.Riders.Count; i++)
            {
                var rr = d.Riders[i];
                float f = Mathf.Clamp01(rr.S / d.Track.FinishS);
                _dots[i].rectTransform.anchoredPosition = new Vector2(f * w - _dots[i].rectTransform.sizeDelta.x * 0.5f, 0);
                _dots[i].color = rr.Crashed ? Color.gray : rr.Def.Jersey;
                if (rr.IsPlayer) _dots[i].transform.SetAsLastSibling();
            }

            // Balance bar
            float hp = Mathf.Clamp01(p.Health / 100f);
            _health.rectTransform.anchorMax = new Vector2(hp, 1);
            _health.color = hp > 0.6f ? Pal.Green : hp > 0.3f ? Pal.Yellow : Pal.Red;

            // Adrenaline ring — smoother lerp, pulsing when charged
            bool ready = p.Adrenaline >= 25;
            _adrRing.fillAmount = Mathf.Lerp(_adrRing.fillAmount, p.Adrenaline / 100f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 9));
            float pulse = ready ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f) : 0.52f;
            _adrRing.color = (p.Boosting ? Pal.Yellow : Pal.Cyan).WithA(pulse);

            int boosts = Mathf.FloorToInt(p.Adrenaline / 25f);
            _boostBadge.gameObject.SetActive(_controlsOn && boosts > 0);
            _boostCount.text = boosts.ToString();
            _btnBoost.SetEnabledLook(ready || p.Boosting);

            // Ground ↔ air button swap
            bool air = !p.Grounded && !p.Crashed;
            if (air != _air)
            {
                _air = air;
                if (air)
                {
                    _btnA.Set("SUPER\nMAN", Pal.Violet);
                    _btnB.Set("NO\nHANDS",  Pal.Pink);
                    _btnC.Set("CAN\nCAN",   new Color(0.55f, 0.28f, 0.95f));
                }
                else
                {
                    _btnA.Set("PUNCH", Pal.Red,                           Icons.Punch);
                    _btnB.Set("JUMP",  Pal.Orange,                        Icons.Jump);
                    _btnC.Set("BRAKE", new Color(0.30f, 0.34f, 0.45f),   Icons.Brake);
                }
            }

            // Big message animation
            if (_big.gameObject.activeSelf)
            {
                _bigT += Time.unscaledDeltaTime;
                float s = _bigT < 0.14f ? Mathf.Lerp(1.65f, 1, _bigT / 0.14f) : 1;
                _big.localScale = new Vector3(s, s, 1);
                float a = Mathf.Clamp01((_bigDur - _bigT) / 0.3f);
                _bigText.color = _bigText.color.WithA(a);
                _bigSub.color  = _bigSub.color.WithA(a);
                if (_bigT > _bigDur && d.Phase != RacePhase.Results) _big.gameObject.SetActive(false);
            }

            // Trick animation
            if (_trick.gameObject.activeSelf)
            {
                _trickT += Time.unscaledDeltaTime;
                float s = _trickT < 0.11f ? Mathf.Lerp(0.55f, 1.1f, _trickT / 0.11f) : Mathf.Lerp(1.1f, 1, (_trickT - 0.11f) / 0.22f);
                _trick.localScale = new Vector3(s, s, 1);
                float a = Mathf.Clamp01((1.9f - _trickT) / 0.45f);
                _trickName.color = Pal.Yellow.WithA(a);
                _trickPts.color  = Color.white.WithA(a);
                if (_trickT > 1.9f) _trick.gameObject.SetActive(false);
            }

            // Coach banner
            if (_coachT > 0)
            {
                _coachT -= Time.unscaledDeltaTime;
                if (_coachT <= 0) _coach.transform.parent.gameObject.SetActive(false);
            }

            // Flash vignette
            if (_flashT > 0)
            {
                _flashT -= Time.unscaledDeltaTime;
                _flash.color = _flash.color.WithA(Mathf.Clamp01(_flashT / 0.45f) * 0.60f);
            }

            // Toasts
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var (t, life) = _toasts[i];
                life -= Time.unscaledDeltaTime;
                if (life <= 0)
                {
                    Destroy(t.gameObject);
                    _toasts.RemoveAt(i);
                    continue;
                }
                _toasts[i] = (t, life);
                t.rectTransform.anchoredPosition = Vector2.Lerp(t.rectTransform.anchoredPosition, new Vector2(0, -i * 26), Time.unscaledDeltaTime * 14);
                t.color = t.color.WithA(Mathf.Clamp01(life / 0.45f));
            }

            bool controls = d.Phase == RacePhase.Countdown || d.Phase == RacePhase.Racing;
            SetControls(controls);
            UpdateTags();
        }

        bool _controlsOn = true;

        void SetControls(bool on)
        {
            if (on == _controlsOn) return;
            _controlsOn = on;
            foreach (var b in new[] { _btnA, _btnB, _btnC, _btnBoost })
            {
                b.gameObject.SetActive(on);
                b.Release();
            }
            _boostRing.gameObject.SetActive(on);
            if (!on) _boostBadge.gameObject.SetActive(false);
            ApplyControlMode();
        }

        // ------------------------------------------------------------ overlays

        RectTransform Overlay(string name)
        {
            CloseOverlay();
            var dim = Ui.Block(_root.parent, new Color(0, 0, 0, 0.68f), name);
            dim.raycastTarget = true;
            _overlay = dim.rectTransform;
            _overlay.Fill();
            return _overlay;
        }

        public void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay.gameObject);
            _overlay = null;
        }

        public bool OverlayOpen => _overlay != null;

        public void TogglePause()
        {
            if (_d.Phase == RacePhase.Results) return;
            if (_d.Paused) { CloseOverlay(); _d.SetPaused(false); }
            else ShowPause();
        }

        public void ShowPause()
        {
            if (_d.Phase == RacePhase.Results) return;
            _d.SetPaused(true);
            foreach (var b in new[] { _btnA, _btnB, _btnC, _btnBoost }) b.Release();
            _pad.Release();
            var o    = Overlay("Pause");
            var card = Ui.Panel(o, Pal.Card, 18, "Card", Pal.Orange.WithA(0.55f));
            card.rectTransform.At(0.5f, 0.5f, 0, 0, 540, 376);
            var c = card.rectTransform;

            // Neon glow behind the card
            var cardGlow = Ui.Img(o, Ui.NeonGlow, Pal.Orange.WithA(0.12f), "CardGlow");
            cardGlow.preserveAspect = false;
            cardGlow.rectTransform.At(0.5f, 0.5f, 0, 0, 600, 430);

            var title = Ui.Label(c, "PAUSED", 46, Pal.Orange, TextAnchor.UpperCenter, true, "Title");
            title.rectTransform.Fill(0, 8, 0, 0);
            Ui.Shadow(title, 2.5f, 0.8f);

            var sub = Ui.Label(c, _d.Cfg.Track.Name + "  ·  " + Ui.Time(_d.Clock), 17, Pal.Dim, TextAnchor.UpperCenter, false, "Sub");
            sub.rectTransform.Fill(0, 60, 0, 0);

            var resume = Ui.Primary(c, "RESUME", () => { CloseOverlay(); _d.SetPaused(false); }, 28);
            ((RectTransform)resume.transform).At(0.5f, 1, 0, -96, 310, 58);

            var restart = Ui.Ghost(c, "RESTART RACE", Pal.Text,  () => _d.Exit(ExitAction.Retry), 22);
            ((RectTransform)restart.transform).At(0.5f, 1, -82, -166, 158, 46);
            var quit = Ui.Ghost(c, "QUIT", Pal.Red, () => _d.Exit(ExitAction.Menu), 22);
            ((RectTransform)quit.transform).At(0.5f, 1, 82, -166, 158, 46);

            // Settings rows inside pause
            float y = -230;
            void PauseRow(string label, string[] opts, int sel, Action<int> pick)
            {
                var l = Ui.Label(c, label, 15, Pal.Dim, TextAnchor.MiddleLeft, false, "L" + label);
                l.rectTransform.At(0, 1, 30, y, 120, 30);
                var segs = Ui.Node(c, "S" + label);
                segs.At(0, 1, 154, y, 340, 34);
                Ui.Segments(segs, opts, sel, pick, 16);
                y -= 44;
            }

            PauseRow("STEERING", new[] { "BUTTONS", "TILT" }, (int)Settings.Steer, i =>
            {
                Settings.Steer = (SteerMode)i;
                Settings.Save();
                ApplyControlMode();
            });
            PauseRow("CAMERA", new[] { "NEAR", "FAR" }, Settings.FarCamera ? 1 : 0, i =>
            {
                Settings.FarCamera = i == 1;
                Settings.Save();
            });
            PauseRow("SOUND", new[] { "ON", "OFF" }, Settings.Sound ? 0 : 1, i =>
            {
                Settings.Sound = i == 0;
                Settings.Save();
                if (!Settings.Sound) Sfx.I?.StopRide();
                else Sfx.I?.StartRide();
            });
        }

        public void ShowResults(RaceResult res)
        {
            SetControls(false);
            _big.gameObject.SetActive(false);
            var o   = Overlay("Results");
            o.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            var cfg = _d.Cfg;
            var card = Ui.Panel(o, Pal.Card, 18, "Card", Pal.StrokeHi);
            card.rectTransform.At(0.5f, 0.5f, 0, 0, 920, 426);
            var c = card.rectTransform;

            // Glow behind results card
            Color glowCol = res.Medal > 0 ? Pal.Medal(res.Medal) : (res.Place <= 3 ? Pal.Yellow : Pal.Cyan);
            var rGlow = Ui.Img(o, Ui.NeonGlow, glowCol.WithA(0.10f), "ResGlow");
            rGlow.preserveAspect = false;
            rGlow.rectTransform.At(0.5f, 0.5f, 0, 0, 1000, 500);

            // Header
            string head = cfg.Event != null ? "EVENT " + (cfg.Event.Index + 1) + " · " + cfg.Event.Title : "QUICK RACE";
            var title = Ui.Label(c, head + "  ·  " + cfg.Track.Name, 20, Pal.Dim, TextAnchor.UpperLeft, true, "Head");
            title.rectTransform.Fill(24, 10, 0, 0);

            var place = Ui.Label(c, Ui.Ord(res.Place), 72, res.Place <= 3 ? Pal.Gold : Color.white, TextAnchor.UpperLeft, true, "Place");
            place.rectTransform.Fill(22, 30, 0, 0);
            Ui.Shadow(place, 3, 0.8f);

            // Neon glow under place if top 3
            if (res.Place <= 3)
            {
                var placeGlow = Ui.Img(c, Ui.NeonGlow, Pal.Gold.WithA(0.25f), "PlaceGlow");
                placeGlow.preserveAspect = false;
                placeGlow.rectTransform.At(0, 1, 14, -36, 160, 100);
            }

            string verdict;
            if (cfg.Event != null)
            {
                verdict = res.Medal == 3 ? "GOLD MEDAL!" : res.Medal == 2 ? "SILVER MEDAL" : res.Medal == 1 ? "BRONZE MEDAL" : "NO MEDAL · " + cfg.Event.Goal;
                var medal = Ui.Img(c, Ui.Circle, Pal.Medal(res.Medal), "Medal");
                medal.rectTransform.At(0, 1, 160, -48, 46, 46);
                if (res.Medal > 0)
                {
                    // Medal glow
                    var mg = Ui.Img(c, Ui.NeonGlow, Pal.Medal(res.Medal).WithA(0.45f), "MedalGlow");
                    mg.preserveAspect = false;
                    mg.rectTransform.At(0, 1, 148, -60, 70, 70);

                    var star = Ui.Label(medal.transform, res.Medal == 3 ? "1" : res.Medal == 2 ? "2" : "3", 26, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, true, "N");
                    star.rectTransform.Fill();
                }
            }
            else verdict = res.Place == 1 ? "WINNER!" : res.Place <= 3 ? "PODIUM FINISH" : "FINISHED";

            var v = Ui.Label(c, verdict, 26, res.Medal > 0 || (cfg.Event == null && res.Place <= 3) ? Pal.Yellow : Pal.Text, TextAnchor.UpperLeft, true, "Verdict");
            v.rectTransform.Fill(cfg.Event != null ? 216 : 162, 50, 0, 0);
            Ui.Shadow(v, 2, 0.7f);

            // Standings list
            var list = Ui.Node(c, "Standings");
            list.At(0, 1, 24, -120, 430, 240);
            for (int i = 0; i < res.Standings.Count && i < 8; i++)
            {
                var st  = res.Standings[i];
                bool alt = i % 2 == 0;
                var row = Ui.Panel(list, st.Player ? Pal.Orange.WithA(0.28f) : new Color(1, 1, 1, alt ? 0.05f : 0.02f), 6, "Row");
                row.raycastTarget = false;
                row.rectTransform.At(0, 1, 0, -i * 29, 430, 27);
                var pos = Ui.Label(row.transform, (i + 1).ToString(), 18, Pal.Dim, TextAnchor.MiddleLeft, true, "Pos");
                pos.rectTransform.Fill(10, 0, 0, 0);
                var sw = Ui.Img(row.transform, Ui.Round(3), st.Rider.Jersey, "Swatch");
                sw.type = Image.Type.Sliced;
                sw.preserveAspect = false;
                sw.rectTransform.At(0, 0.5f, 36, 0, 8, 18);
                var name = Ui.Label(row.transform, st.Rider.Name, 17, st.Player ? Color.white : Pal.Text, TextAnchor.MiddleLeft, false, "Name");
                name.rectTransform.Fill(52, 0, 0, 0);
                if (st.Player) Ui.Shadow(name, 1.5f, 0.6f);
                var time = Ui.Label(row.transform, Ui.Time(st.Time), 18, Pal.Text, TextAnchor.MiddleRight, true, "Time");
                time.rectTransform.Fill(0, 0, 10, 0);
            }

            // Stats & money
            var right = Ui.Node(c, "Stats");
            right.At(1, 1, -24, -40, 390, 300);
            void Line(float y, string label, string value, Color vc, float size = 18)
            {
                var l = Ui.Label(right, label, size - 2, Pal.Dim, TextAnchor.MiddleLeft, false, "L");
                l.rectTransform.At(0, 1, 0, -y, 244, 24);
                var val = Ui.Label(right, value, size, vc, TextAnchor.MiddleRight, true, "V");
                val.rectTransform.At(1, 1, 0, -y, 164, 24);
            }
            float best = Profile.BestTime(cfg.Track.Id);
            Line(0,   "TIME",             Ui.Time(res.Time) + (res.NewBest ? "  NEW BEST!" : ""), res.NewBest ? Pal.Yellow : Pal.Text);
            Line(24,  "COURSE BEST",      Ui.Time(best),          Pal.Text);
            Line(48,  "TRICK POINTS",     res.Tricks.ToString("N0"),  Pal.Text);
            Line(72,  "TAKEDOWNS",        res.Takedowns.ToString(),   Pal.Text);
            Line(96,  "CRASHES",          res.Crashes.ToString(),     Pal.Text);
            var sep = Ui.Block(right, Pal.StrokeHi, "Sep");
            sep.rectTransform.At(0.5f, 1, 0, -126, 390, 1);
            Line(136, "PLACE PRIZE",      Ui.Money(res.PrizePlace),       Pal.Green);
            Line(160, "TRICK BONUS",      Ui.Money(res.PrizeTricks),      Pal.Green);
            Line(184, "TAKEDOWN BONUS",   Ui.Money(res.PrizeTakedowns),   Pal.Green);
            if (cfg.Event != null)
                Line(208, "MEDAL BONUS",  Ui.Money(res.PrizeMedal),       Pal.Green);
            Line(238, "TOTAL EARNED",     Ui.Money(res.Total),            Pal.Yellow, 24);
            Line(266, "BALANCE",          Ui.Money(Profile.Cash),         Pal.Text);

            // Action buttons
            bool hasNext = cfg.Event != null && res.Medal > 0 && cfg.Event.Index + 1 < Catalog.Career.Length;
            var retry = Ui.Ghost(c, "RETRY",  Pal.Text, () => _d.Exit(ExitAction.Retry), 22);
            ((RectTransform)retry.transform).At(0.5f, 0, -234, 16, 178, 50);
            var menu  = Ui.Ghost(c, "MENU",   Pal.Text, () => _d.Exit(ExitAction.Menu), 22);
            ((RectTransform)menu.transform).At(0.5f, 0, -50, 16, 178, 50);
            var next  = Ui.Primary(c, hasNext ? "NEXT EVENT" : "CONTINUE", () => _d.Exit(hasNext ? ExitAction.Next : ExitAction.Menu), 24);
            ((RectTransform)next.transform).At(0.5f, 0, 186, 16, 268, 54);

            if (res.Place == 1 || res.Medal == 3)
            {
                var conf = Confetti.Create(o);
                conf.Burst(new[] { Pal.Orange, Pal.Yellow, Pal.Cyan, Pal.Pink, Color.white });
            }
        }
    }
}
