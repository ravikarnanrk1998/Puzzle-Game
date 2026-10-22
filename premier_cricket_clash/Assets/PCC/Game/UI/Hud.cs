using System;
using System.Collections.Generic;
using System.Linq;
using PCC.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>In-match HUD and overlays (toss, bowler picker, pause, innings break, result).</summary>
    public sealed class Hud : MonoBehaviour
    {
        MatchDirector D;
        RectTransform _root, _overlay;
        Canvas _canvas;

        // Score bug and strips.
        Image _crest;
        Text _scoreTeam, _scoreRuns, _scoreOvers, _scoreSub, _matchup, _speed, _bat1, _bat2, _bowlLine;
        readonly List<(Image Bg, Text T)> _chips = new List<(Image, Text)>();
        GameObject _speedGo;

        // Batting.
        GameObject _swipeGroup, _buttonsGroup, _timingGo;
        Text _swipeTitle, _timing;
        Image _swipePanel, _loftSwipe;
        RectTransform _chev;
        Joystick _stick;
        Text _stickHint;
        Image _ring, _ringBase;
        readonly List<Image> _swipeDots = new List<Image>();

        // Bowling.
        GameObject _bowlGroup, _meterGo, _bowlBtnGo, _hintGo, _deliveryList;
        Text _deliveryName, _deliverySummary;
        RectTransform _meterKnob;
        Image _autoBg;
        readonly List<Text> _zoneLabels = new List<Text>();
        Text _markerLabel;
        string[] _listFor;

        // Banner, toast, confetti.
        RectTransform _banner;
        Text _bannerText, _bannerSub;
        float _bannerT = 99;
        Text _toast;
        float _toastT = 99;
        Confetti _confetti;
        Text _tossCoin;
        RectTransform _tossCoinRt;

        public static Hud Build(Transform canvas, MatchDirector d)
        {
            var root = Ui.Node(canvas, "HUD").Fill();
            var h = root.gameObject.AddComponent<Hud>();
            h.D = d;
            h._root = root;
            h._canvas = canvas.GetComponentInParent<Canvas>();
            h.Construct();
            return h;
        }

        float UiScale => _canvas != null ? _canvas.scaleFactor : 1;

        void Construct()
        {
            // Touch surface for the 3D scene.
            var touch = Ui.Node(_root, "Touch").Fill();
            var ti = touch.gameObject.AddComponent<Image>();
            ti.color = new Color(0, 0, 0, 0);
            var tl = touch.gameObject.AddComponent<TouchLayer>();
            tl.Down = p => D.PointerDown(p, UiScale);
            tl.Drag = p => D.PointerDrag(p, UiScale);
            tl.Up = (p, tap) => D.PointerUp(p, tap);

            var safe = Ui.Node(_root, "Safe").Fill(8, 6, 8, 6);
            BuildScoreBug(safe);
            BuildMatchup(safe);
            BuildTopRight(safe);
            BuildBatterPanel(safe);
            BuildBatting(safe);
            BuildBowling(safe);

            for (int i = 0; i < 26; i++)
            {
                var dot = Ui.Img(_root, Ui.Circle, Color.white, "SwipeDot");
                dot.rectTransform.sizeDelta = new Vector2(12, 12);
                dot.gameObject.SetActive(false);
                _swipeDots.Add(dot);
            }
            _ringBase = Ui.Img(_root, RingSprite(), new Color(1, 1, 1, 0.5f), "RingBase");
            _ring = Ui.Img(_root, RingSprite(), Pal.Gold, "Ring");
            foreach (var z in MatchProps.Zones)
            {
                var t = Ui.Label(_root, z.Label, 18, z.Color, TextAnchor.MiddleLeft);
                Ui.Shadow(t, 1.5f, 0.9f);
                t.rectTransform.sizeDelta = new Vector2(120, 22);
                _zoneLabels.Add(t);
            }
            _markerLabel = Ui.Label(_root, "", 22, Color.white, TextAnchor.MiddleLeft);
            Ui.Shadow(_markerLabel, 2, 0.9f);
            _markerLabel.rectTransform.sizeDelta = new Vector2(160, 26);
            _markerLabel.rectTransform.anchorMin = _markerLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _markerLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            _markerLabel.gameObject.SetActive(false);

            // Banner.
            _banner = Ui.Node(_root, "Banner").At(0.5f, 0.62f, 0, 0, 700, 140);
            _bannerText = Ui.Label(_banner, "", 96, Pal.Gold);
            _bannerText.rectTransform.Fill(0, 0, 0, 30);
            Ui.Shadow(_bannerText, 4, 0.7f);
            var outline = _bannerText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            outline.effectDistance = new Vector2(2, -2);
            var subBg = Ui.Panel(_banner, new Color(0, 0, 0, 0.55f), 14, "SubBg");
            subBg.rectTransform.At(0.5f, 0, 0, 0, 420, 28);
            subBg.raycastTarget = false;
            _bannerSub = Ui.Label(subBg.transform, "", 17, Pal.Text, TextAnchor.MiddleCenter, false);
            _bannerSub.rectTransform.Fill(8, 0, 8, 0);
            _banner.gameObject.SetActive(false);

            var toastBg = Ui.Panel(_root, new Color(0, 0, 0, 0.75f), 14, "Toast", Pal.Gold.WithA(0.5f));
            // High on screen so it never covers the pitch.
            toastBg.rectTransform.At(0.5f, 0.8f, 0, 0, 560, 38);
            toastBg.raycastTarget = false;
            _toast = Ui.Label(toastBg.transform, "", 21, Pal.GoldLight);
            _toast.rectTransform.Fill(8, 0, 8, 0);
            toastBg.gameObject.SetActive(false);

            _confetti = PCC.Game.Confetti.Create(_root);
            _overlay = Ui.Node(_root, "Overlay").Fill();
            ShowToss();
        }

        static Sprite _ringSprite;

        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            var px = new Px(128, 128, new Color32(255, 255, 255, 0));
            for (int j = 0; j < 128; j++)
            for (int i = 0; i < 128; i++)
            {
                float dx = (i + 0.5f) / 64f - 1, dy = (j + 0.5f) / 64f - 1, d = Mathf.Sqrt(dx * dx + dy * dy);
                px.P[j * 128 + i] = new Color(1, 1, 1, Mathf.Clamp01(1 - Mathf.Abs(d - 0.9f) / 0.07f));
            }
            _ringSprite = Sprite.Create(px.ToTexture("RingUI", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        // ------------------------------------------------------------ build

        void BuildScoreBug(RectTransform parent)
        {
            var bug = Ui.Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.88f), 16, "ScoreBug", Pal.Gold.WithA(0.6f));
            bug.rectTransform.At(0, 1, 0, 0, 250, 58);
            bug.raycastTarget = false;
            _crest = Crest.Add(bug.transform, D.M.Home.Team, 38);
            ((RectTransform)_crest.transform.parent).At(0, 0.5f, 10, 0, 38, 43);
            _scoreTeam = Ui.Label(bug.transform, "", 26, Pal.Text, TextAnchor.MiddleLeft);
            _scoreTeam.rectTransform.At(0, 1, 56, -6, 60, 28);
            _scoreRuns = Ui.Label(bug.transform, "", 34, Pal.GoldLight, TextAnchor.MiddleLeft);
            _scoreRuns.rectTransform.At(0, 1, 104, -3, 90, 32);
            _scoreOvers = Ui.Label(bug.transform, "", 18, Pal.Dim, TextAnchor.MiddleLeft, false);
            _scoreOvers.rectTransform.At(0, 1, 184, -9, 64, 22);
            _scoreSub = Ui.Label(bug.transform, "", 12.5f, Pal.Dim, TextAnchor.MiddleLeft, false);
            _scoreSub.rectTransform.At(0, 0, 56, 5, 190, 16);
        }

        void BuildMatchup(RectTransform parent)
        {
            var strip = Ui.Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.85f), 18, "Matchup", Pal.Stroke);
            strip.rectTransform.At(0.5f, 1, 0, 0, 430, 40);
            strip.raycastTarget = false;
            _matchup = Ui.Label(strip.transform, "", 22, Pal.Text, TextAnchor.MiddleLeft);
            _matchup.rectTransform.At(0, 0.5f, 14, 0, 230, 32);
            for (int i = 0; i < 8; i++)
            {
                var bg = Ui.Img(strip.transform, Ui.Circle, new Color(1, 1, 1, 0.12f), "Chip");
                bg.rectTransform.At(1, 0.5f, -12 - (7 - i) * 25, 0, 23, 23);
                var t = Ui.Label(bg.transform, "", 13, Pal.Text);
                t.rectTransform.Fill();
                _chips.Add((bg, t));
            }
        }

        void BuildTopRight(RectTransform parent)
        {
            var speed = Ui.Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.85f), 14, "Speed", Pal.Gold.WithA(0.5f));
            speed.rectTransform.At(1, 1, -54, -2, 118, 34);
            speed.raycastTarget = false;
            _speed = Ui.Label(speed.transform, "", 21, Pal.GoldLight);
            _speed.rectTransform.Fill();
            _speedGo = speed.gameObject;
            var pause = Ui.Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.8f), 20, "Pause", Pal.Stroke);
            pause.rectTransform.At(1, 1, 0, 0, 44, 44);
            var p = Ui.Label(pause.transform, "II", 26, Pal.Text);
            p.rectTransform.Fill();
            var b = pause.gameObject.AddComponent<Button>();
            b.onClick.AddListener(TogglePause);
            pause.gameObject.AddComponent<PressScale>();
        }

        void BuildBatterPanel(RectTransform parent)
        {
            var panel = Ui.Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.85f), 14, "Batters", Pal.Stroke);
            panel.rectTransform.At(1, 0.62f, 0, 0, 220, 76);
            panel.raycastTarget = false;
            _bat1 = Ui.Label(panel.transform, "", 17, Pal.Text, TextAnchor.MiddleLeft, false);
            _bat1.rectTransform.At(0, 1, 10, -4, 200, 22);
            _bat2 = Ui.Label(panel.transform, "", 17, Pal.Dim, TextAnchor.MiddleLeft, false);
            _bat2.rectTransform.At(0, 1, 10, -26, 200, 22);
            _bowlLine = Ui.Label(panel.transform, "", 15, Pal.Pink, TextAnchor.MiddleLeft, false);
            _bowlLine.rectTransform.At(0, 0, 10, 4, 200, 20);
        }

        void BuildBatting(RectTransform parent)
        {
            // Swipe mode.
            _swipeGroup = Ui.Node(parent, "SwipeGroup").Fill().gameObject;
            _swipePanel = Ui.Panel(_swipeGroup.transform, new Color(0.02f, 0.04f, 0.12f, 0.85f), 16, "SwipeHint", Pal.Cyan.WithA(0.5f));
            _swipePanel.rectTransform.At(0, 0, 0, 0, 220, 92);
            // Swipes may start on the hint card.
            _swipePanel.raycastTarget = false;
            _swipeTitle = Ui.Label(_swipePanel.transform, "SWIPE TO PLAY SHOT", 21, Pal.Text);
            _swipeTitle.rectTransform.At(0.5f, 1, 0, -6, 210, 24);
            _chev = Ui.Node(_swipePanel.transform, "Chevrons").At(0.5f, 0.5f, 0, -4, 200, 30);
            var chevText = Ui.Label(_chev, "‹‹   •   ››", 30, Pal.Cyan);
            chevText.rectTransform.Fill();
            var tapHint = Ui.Label(_swipePanel.transform, "TAP = DEFEND", 12, Pal.Dim, TextAnchor.MiddleCenter, false);
            tapHint.rectTransform.At(0.5f, 0, 0, 6, 200, 14);
            var loft = Ui.Panel(_swipeGroup.transform, new Color(0.02f, 0.04f, 0.12f, 0.85f), 30, "Loft", Pal.Stroke);
            loft.rectTransform.At(0, 0, 232, 8, 64, 64);
            _loftSwipe = loft.transform.Find("Fill").GetComponent<Image>();
            var lt = Ui.Label(loft.transform, "LOFT", 20, Pal.Text);
            lt.rectTransform.Fill();
            var lb = loft.gameObject.AddComponent<Button>();
            lb.onClick.AddListener(() => D.LoftToggle = !D.LoftToggle);
            loft.gameObject.AddComponent<PressScale>();

            // Joystick + buttons mode (DEFEND / GROUND / LOFT).
            _buttonsGroup = Ui.Node(parent, "ButtonsGroup").Fill().gameObject;
            _stick = Joystick.Create(_buttonsGroup.transform, 150);
            ((RectTransform)_stick.transform).At(0, 0, 6, 4, 150, 150);
            _stickHint = Ui.Label(_buttonsGroup.transform, "AIM", 13, Pal.Dim, TextAnchor.MiddleCenter, false);
            _stickHint.rectTransform.At(0, 0, 6, 156, 150, 16);
            ShotButton(_buttonsGroup.transform, "DEFEND", new Color(0.2f, 0.75f, 0.55f), new Color(0.05f, 0.4f, 0.3f), -186, 14, 78, () => D.PlayShot(null, false, true));
            ShotButton(_buttonsGroup.transform, "GROUND", new Color(0.22f, 0.6f, 1f), new Color(0.12f, 0.25f, 0.7f), -96, 52, 84, () => D.PlayShot(_stick.Angle ?? 0, false, false));
            ShotButton(_buttonsGroup.transform, "LOFT", new Color(1f, 0.62f, 0.15f), new Color(0.85f, 0.3f, 0.05f), -6, 14, 84, () => D.PlayShot(_stick.Angle ?? 0, true, false));

            var timing = Ui.Panel(parent, new Color(0, 0, 0, 0.55f), 12, "Timing", Pal.Stroke);
            timing.rectTransform.At(1, 0, 0, 146, 120, 54);
            timing.raycastTarget = false;
            var tl = Ui.Label(timing.transform, "TIMING", 11, Pal.Dim, TextAnchor.MiddleCenter, false);
            tl.rectTransform.At(0.5f, 1, 0, -3, 120, 14);
            _timing = Ui.Label(timing.transform, "—", 24, Pal.Mute, TextAnchor.MiddleCenter);
            _timing.rectTransform.At(0.5f, 0, 0, 2, 120, 34);
            _timingGo = timing.gameObject;
        }

        void ShotButton(Transform parent, string label, Color a, Color b, float x, float y, float size, Action onPress)
        {
            var bg = Ui.Img(parent, Ui.Circle, Color.Lerp(a, b, 0.4f), label);
            bg.raycastTarget = true;
            bg.rectTransform.At(1, 0, x, y, size, size);
            var inner = Ui.Img(bg.transform, Ui.Circle, a, "Inner");
            inner.rectTransform.Fill(4, 4, 4, 4);
            var gloss = Ui.Img(inner.transform, Ui.Glow, new Color(1, 1, 1, 0.35f), "Gloss");
            gloss.rectTransform.At(0.5f, 0.75f, 0, 0, size * 0.7f, size * 0.4f);
            var t = Ui.Label(bg.transform, label, size * 0.24f, Color.white);
            Ui.Shadow(t, 1.5f, 0.5f);
            t.rectTransform.Fill();
            // Fire on press (timing matters), not on release.
            var trigger = bg.gameObject.AddComponent<PressTrigger>();
            trigger.OnPress = onPress;
            bg.gameObject.AddComponent<PressScale>();
        }

        void BuildBowling(RectTransform parent)
        {
            _bowlGroup = Ui.Node(parent, "BowlGroup").Fill().gameObject;
            // Delivery selector (dropdown).
            var sel = Ui.Panel(_bowlGroup.transform, new Color(0.02f, 0.04f, 0.12f, 0.88f), 14, "Delivery", Pal.Gold.WithA(0.6f));
            sel.rectTransform.At(0, 0, 0, 0, 230, 62);
            var cap = Ui.Label(sel.transform, "DELIVERY", 11, Pal.Gold, TextAnchor.UpperLeft, false);
            cap.rectTransform.Fill(12, 5, 12, 0);
            _deliveryName = Ui.Label(sel.transform, "", 26, Pal.Text, TextAnchor.MiddleLeft);
            _deliveryName.rectTransform.Fill(12, 14, 30, 14);
            _deliverySummary = Ui.Label(sel.transform, "", 13, Pal.Cyan, TextAnchor.LowerLeft, false);
            _deliverySummary.rectTransform.Fill(12, 0, 12, 5);
            var arrow = Ui.Label(sel.transform, "‹ ›", 18, Pal.Gold);
            arrow.rectTransform.At(1, 0.5f, -8, 0, 26, 20);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            var sb = sel.gameObject.AddComponent<Button>();
            sb.onClick.AddListener(() => _deliveryList.SetActive(!_deliveryList.activeSelf));
            sel.gameObject.AddComponent<PressScale>();
            _deliveryList = Ui.Node(_bowlGroup.transform, "List").At(0, 0, 0, 68, 230, 160).gameObject;
            _deliveryList.SetActive(false);

            // AUTO + BOWL / meter.
            var auto = Ui.Panel(_bowlGroup.transform, new Color(0.02f, 0.04f, 0.12f, 0.85f), 30, "Auto", Pal.Stroke);
            auto.rectTransform.At(1, 0, -232, 4, 58, 58);
            _autoBg = auto.transform.Find("Fill").GetComponent<Image>();
            var at = Ui.Label(auto.transform, "AUTO", 15, Pal.Text);
            at.rectTransform.Fill();
            var ab = auto.gameObject.AddComponent<Button>();
            ab.onClick.AddListener(() => D.AutoBowl = !D.AutoBowl);
            auto.gameObject.AddComponent<PressScale>();

            var bowl = Ui.Button(_bowlGroup.transform, "BOWL", Pal.Pink, Pal.Violet, D.OnBowlButton, 30, "Then stop the meter");
            ((RectTransform)bowl.transform).At(1, 0, 0, 0, 216, 64);
            _bowlBtnGo = bowl.gameObject;

            var meter = Ui.Panel(_bowlGroup.transform, new Color(0.03f, 0.05f, 0.15f, 0.95f), 14, "Meter", Pal.Gold);
            meter.rectTransform.At(1, 0, 0, 0, 216, 64);
            var mt = Ui.Label(meter.transform, "TAP TO RELEASE", 12, Pal.Gold, TextAnchor.UpperCenter, false);
            mt.rectTransform.Fill(0, 5, 0, 0);
            var bar = Ui.Node(meter.transform, "Bar").Fill(12, 24, 12, 10);
            int segs = 5;
            var cols = new[] { Pal.Red, Pal.Gold, Pal.Green, Pal.Gold, Pal.Red };
            for (int i = 0; i < segs; i++)
            {
                var s = Ui.Img(bar, null, cols[i], "Seg");
                s.rectTransform.anchorMin = new Vector2(i / (float)segs, 0);
                s.rectTransform.anchorMax = new Vector2((i + 1) / (float)segs, 1);
                s.rectTransform.offsetMin = s.rectTransform.offsetMax = Vector2.zero;
            }
            var knob = Ui.Img(bar, null, Color.white, "Knob");
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(6, 38);
            _meterKnob = knob.rectTransform;
            var mb = meter.gameObject.AddComponent<PressTrigger>();
            mb.OnPress = D.OnBowlButton;
            _meterGo = meter.gameObject;

            var hint = Ui.Panel(_bowlGroup.transform, new Color(0, 0, 0, 0.55f), 14, "Hint");
            hint.rectTransform.At(0.5f, 0, -20, 6, 330, 26);
            var ht = Ui.Label(hint.transform, "DRAG THE TARGET ON THE PITCH  •  THEN BOWL", 13, Pal.GoldLight, TextAnchor.MiddleCenter, false);
            ht.rectTransform.Fill();
            hint.raycastTarget = false;
            _hintGo = hint.gameObject;
        }

        void RebuildDeliveryList(string[] options)
        {
            if (_listFor == options) return;
            _listFor = options;
            foreach (Transform c in _deliveryList.transform) Destroy(c.gameObject);
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = Ui.Panel(_deliveryList.transform, new Color(0.04f, 0.06f, 0.18f, 0.95f), 10, "Opt" + i, Pal.Stroke);
                b.rectTransform.At(0, 1, 0, -i * 38, 230, 34);
                var t = Ui.Label(b.transform, options[i], 19, Pal.Text, TextAnchor.MiddleLeft);
                t.rectTransform.Fill(12, 0, 8, 0);
                var btn = b.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    D.Variation = idx;
                    _deliveryList.SetActive(false);
                    Sfx.I?.Click();
                });
            }
            ((RectTransform)_deliveryList.transform).sizeDelta = new Vector2(230, options.Length * 38);
        }

        // ------------------------------------------------------------ update

        void LateUpdate()
        {
            Dbg.Mark("Hud.score");
            var m = D.M;
            bool started = m.Started && !m.Finished;
            var inn = m.Started ? (m.Finished ? m.InningsList[m.InningsList.Count - 1] : m.Current) : null;

            // Score bug.
            if (inn == null)
            {
                foreach (var (bg, _) in _chips) bg.gameObject.SetActive(false);
            }
            else
            {
                var team = inn.Batting.Team;
                if (_crest.sprite != Crest.Of(team)) _crest.sprite = Crest.Of(team);
                _scoreTeam.text = team.Id;
                _scoreRuns.text = inn.ScoreText;
                _scoreOvers.text = "(" + inn.OversText + ")";
                // Flow the three labels left to right so wide team codes never overlap the score.
                float x = 56 + _scoreTeam.preferredWidth + 8;
                _scoreRuns.rectTransform.anchoredPosition = new Vector2(x, -3);
                _scoreOvers.rectTransform.anchoredPosition = new Vector2(x + _scoreRuns.preferredWidth + 6, -9);
                _scoreSub.text = inn.Target != null
                    ? "TARGET " + inn.Target + " • NEED " + inn.RunsNeeded + " OFF " + inn.BallsLeft
                    : "CRR " + inn.RunRate.ToString("F2") + (inn.SuperOver ? " • SUPER OVER" : "");
                var bowlerId = inn.BowlerId ?? inn.LastOverBowler;
                var striker = inn.Batting.ById(inn.Striker);
                var bowler = bowlerId != null ? inn.Bowling.ById(bowlerId.Value) : null;
                _matchup.text = (bowler != null ? bowler.ShortName.ToUpperInvariant() : "—") + "  to  " + striker.ShortName.ToUpperInvariant();
                var over = inn.RecentOver;
                for (int i = 0; i < _chips.Count; i++)
                {
                    int k = over.Count - _chips.Count + i;
                    var (bg, t) = _chips[i];
                    if (k < 0)
                    {
                        bg.gameObject.SetActive(false);
                        continue;
                    }
                    bg.gameObject.SetActive(true);
                    var r = over[k].Result;
                    t.text = r.Chip;
                    bg.color = r.Wicket ? Pal.Red : r.IsSix ? Pal.Violet : r.IsFour ? Pal.Cyan : r.Extra != ExtraType.None ? Pal.Gold.WithA(0.85f) : new Color(1, 1, 1, 0.14f);
                }
                var s1 = inn.Bat[inn.Striker];
                var s2 = inn.Bat[inn.NonStriker];
                _bat1.text = "<color=#FFC84A>›</color> " + striker.ShortName + "   <b>" + s1.Runs + "</b> (" + s1.Balls + ")";
                _bat2.text = "   " + inn.Batting.ById(inn.NonStriker).ShortName + "   " + s2.Runs + " (" + s2.Balls + ")";
                _bowlLine.text = bowler != null ? bowler.ShortName + "  " + Fig(inn, bowler.Id) : "";
            }
            _speedGo.SetActive(D.SpeedGun != null);
            _speed.text = D.SpeedGun ?? "";

            // Controls.
            Dbg.Mark("Hud.controls");
            bool batting = started && D.UserBatting;
            bool bowling = started && D.UserBowling;
            bool swipeMode = Settings.Batting == BattingControls.Swipe;
            _swipeGroup.SetActive(batting && swipeMode);
            _buttonsGroup.SetActive(batting && !swipeMode);
            _timingGo.SetActive(batting);
            _bowlGroup.SetActive(bowling);
            if (batting)
            {
                bool active = D.AwaitingShot;
                _swipeTitle.text = active ? "SWIPE NOW!" : "SWIPE TO PLAY SHOT";
                _swipePanel.color = active ? Pal.Cyan.WithA(0.8f) : Pal.Cyan.WithA(0.35f);
                _chev.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 6) * 6, -4);
                _loftSwipe.color = D.LoftToggle ? Pal.Gold.WithA(0.6f) : new Color(0.02f, 0.04f, 0.12f, 0.85f);
                _stick.SetLeftHanded(m.Striker.LeftBat);
                var a = _stick.Angle;
                _stickHint.text = a == null ? "AIM: STRAIGHT" : "AIM: " + DeliveryX.Nearest(-a.Value * (m.Striker.LeftBat ? -1 : 1)).Label();
                _timing.text = D.Grade?.Label() ?? "—";
                _timing.color = D.Grade switch
                {
                    TimingGrade.Perfect => Pal.Green,
                    TimingGrade.Good => Pal.Cyan,
                    TimingGrade.Early => Pal.Gold,
                    TimingGrade.Late => Pal.Gold,
                    null => Pal.Mute,
                    _ => Pal.Red,
                };
            }
            if (bowling)
            {
                var bowler = m.Current.BowlerId != null ? m.Bowler : null;
                var options = bowler != null && bowler.IsSpin ? MatchDirector.SpinVariations : MatchDirector.PaceVariations;
                RebuildDeliveryList(options);
                _deliveryName.text = options[Mathf.Clamp(D.Variation, 0, options.Length - 1)];
                if (bowler != null)
                {
                    var (type, line) = D.AimToDelivery();
                    _deliverySummary.text = type.Label() + " • " + line.Label();
                    _deliverySummary.color = MatchProps.Zones[MatchProps.ZoneIndex(Pitch.Length - D.Aim.y)].Color;
                }
                bool ready = D.Phase == MatchPhase.Ready && !D.PickingBowler;
                _bowlBtnGo.SetActive(!D.MeterOn);
                _bowlBtnGo.GetComponent<Button>().interactable = ready && !D.AutoBowl;
                _meterGo.SetActive(D.MeterOn);
                if (D.MeterOn)
                {
                    float w = ((RectTransform)_meterKnob.parent).rect.width;
                    _meterKnob.anchoredPosition = new Vector2((float)(D.MeterValue - 0.5) * w, 0);
                }
                _autoBg.color = D.AutoBowl ? Pal.Green.WithA(0.45f) : new Color(0.02f, 0.04f, 0.12f, 0.85f);
                _hintGo.SetActive(ready && !D.AutoBowl);
            }

            Dbg.Mark("Hud.labels");
            UpdateZoneLabels(bowling && D.Phase == MatchPhase.Ready && !D.AutoBowl && !D.PickingBowler);
            UpdateMarkerLabel();
            Dbg.Mark("Hud.ring");
            UpdateRing();
            Dbg.Mark("Hud.swipe");
            UpdateSwipe();
            Dbg.Mark("Hud.banner");
            UpdateBanner();
            Dbg.Mark("Hud.toss");
            UpdateToss();
            Dbg.Mark("Hud.end");
        }

        /// <summary>Broadcast-style bowling figures: wickets-runs (overs).</summary>
        static string Fig(Innings inn, int bowlerId) =>
            inn.Bowl.TryGetValue(bowlerId, out var bc) ? bc.Wickets + "-" + bc.Runs + " (" + Overs.Text(bc.Balls) + ")" : "0-0 (0)";

        Vector2 ToLocal(Vector3 world, out bool visible)
        {
            var cam = Camera.main;
            visible = false;
            if (cam == null) return Vector2.zero;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.5f) return Vector2.zero;
            visible = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            return local;
        }

        void UpdateZoneLabels(bool show)
        {
            // The zone under the bowler's target is enlarged; the others fade back.
            int selected = MatchProps.ZoneIndex(Pitch.Length - D.Aim.y);
            for (int i = 0; i < _zoneLabels.Count; i++)
            {
                var z = MatchProps.Zones[i];
                var lp = ToLocal(new V3(1.7, Pitch.Length - (z.From + z.To) / 2, 0.02).U, out bool vis);
                bool on = show && vis;
                _zoneLabels[i].gameObject.SetActive(on);
                if (!on) continue;
                var rt = _zoneLabels[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = lp;
                bool sel = i == selected;
                rt.localScale = Vector3.one * (sel ? 1.35f : 1f);
                _zoneLabels[i].color = z.Color.WithA(sel ? 1f : 0.55f);
            }
        }

        /// <summary>Length name (YORKER, GOOD...) beside the pitch marker.</summary>
        void UpdateMarkerLabel()
        {
            var pm = D.PitchMarker;
            bool on = false;
            if (pm != null)
            {
                var lp = ToLocal(pm.Value.Pos.U, out bool vis);
                on = vis;
                if (vis)
                {
                    _markerLabel.rectTransform.anchoredPosition = lp + new Vector2(46, 0);
                    _markerLabel.text = pm.Value.Label;
                    _markerLabel.color = pm.Value.Color.WithA(pm.Value.Alpha);
                }
            }
            if (_markerLabel.gameObject.activeSelf != on) _markerLabel.gameObject.SetActive(on);
        }

        void UpdateRing()
        {
            var p = D.RingProgress;
            bool hand = D.M.Started && !D.M.Finished && D.M.Striker.LeftBat;
            var lp = ToLocal(new V3(hand ? -0.1 : 0.1, 19.05, 0.7).U, out bool vis);
            bool on = p != null && vis;
            _ring.gameObject.SetActive(on);
            _ringBase.gameObject.SetActive(on);
            if (!on) return;
            var cam = Camera.main;
            float pxPerM = cam != null ? Screen.height / (2 * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Vector3.Distance(cam.transform.position, new V3(0, 19.05, 0.7).U)) / UiScale : 30;
            float baseR = 0.3f * pxPerM;
            float r = baseR + (float)p.Value * 1.7f * pxPerM;
            bool good = p.Value < 0.12;
            _ringBase.rectTransform.anchorMin = _ringBase.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ringBase.rectTransform.anchoredPosition = lp;
            _ringBase.rectTransform.sizeDelta = new Vector2(baseR * 2, baseR * 2);
            _ring.rectTransform.anchorMin = _ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ring.rectTransform.anchoredPosition = lp;
            _ring.rectTransform.sizeDelta = new Vector2(r * 2, r * 2);
            _ring.color = good ? Pal.Green : Color.Lerp(Pal.Gold, Pal.Cyan, (float)p.Value);
        }

        void UpdateSwipe()
        {
            var pts = D.Swipe;
            for (int i = 0; i < _swipeDots.Count; i++)
            {
                var dot = _swipeDots[i];
                if (i >= pts.Count)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }
                float age = (float)(D.Clock - pts[i].T);
                float a = Mathf.Clamp01(1 - age / 0.45f);
                dot.gameObject.SetActive(a > 0);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, pts[i].P, null, out var lp);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.anchoredPosition = lp;
                float s = 6 + 14 * a * (i / (float)Mathf.Max(1, pts.Count));
                dot.rectTransform.sizeDelta = new Vector2(s, s);
                dot.color = Color.Lerp(Pal.Cyan, Color.white, 0.5f).WithA(0.85f * a);
            }
        }

        void UpdateBanner()
        {
            _bannerT += Time.deltaTime;
            float life = 1.9f;
            bool on = _bannerT < life;
            if (_banner.gameObject.activeSelf != on) _banner.gameObject.SetActive(on);
            if (on)
            {
                float t = Mathf.Clamp01(_bannerT / 0.5f);
                float back = 1 + 2.7f * Mathf.Pow(t - 1, 3) + 1.7f * Mathf.Pow(t - 1, 2);
                float fade = Mathf.Clamp01((life - _bannerT) / 0.3f);
                _banner.localScale = Vector3.one * (0.4f + 0.6f * back);
                _bannerText.color = _bannerText.color.WithA(fade);
                _bannerSub.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_bannerSub.text));
            }
            _toastT += Time.deltaTime;
            bool toastOn = _toastT < 1.6f;
            if (_toast.transform.parent.gameObject.activeSelf != toastOn) _toast.transform.parent.gameObject.SetActive(toastOn);
        }

        public void ShowBanner(string text, Color color, string sub)
        {
            _bannerText.text = text;
            _bannerText.color = color;
            _bannerSub.text = sub ?? "";
            _bannerT = 0;
        }

        public void ShowToast(string text)
        {
            _toast.text = text;
            _toastT = 0;
        }

        public void ShowTiming(TimingGrade? g) { }

        public void Confetti(TeamInfo team)
        {
            _confetti.Burst(new[] { Mats.Hex(team.Primary), Mats.Hex(team.Glow), Pal.Gold, Color.white, Pal.Violet });
        }

        // ------------------------------------------------------------ overlays

        public void HideOverlay()
        {
            foreach (Transform c in _overlay) Destroy(c.gameObject);
            _tossCoin = null;
            if (Paused) SetPaused(false);
        }

        // ------------------------------------------------------------ pause

        /// <summary>True while the pause menu (or the scorecard opened from it) is up.</summary>
        public bool Paused { get; private set; }

        /// <summary>Freezes the match clock, animations and crowd audio (UI keeps working: it runs on unscaled time).</summary>
        void SetPaused(bool on)
        {
            Paused = on;
            Time.timeScale = on ? 0 : 1;
            AudioListener.pause = on;
            // The frozen scene behind the pause menu does not need 60 FPS.
            Application.targetFrameRate = on || !Settings.Smooth60 ? 30 : 60;
        }

        public void TogglePause()
        {
            if (Paused) HideOverlay();
            else ShowPause();
        }

        /// <summary>Leaving the app (home button, a call) pauses a live match instead of letting it run.</summary>
        void OnApplicationPause(bool appPaused)
        {
            if (appPaused && !Paused && D.M.Started && !D.M.Finished && _overlay.childCount == 0) ShowPause();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) OnApplicationPause(true);
        }

        void OnDestroy()
        {
            if (Paused) SetPaused(false);
        }

        RectTransform Card(float w, float h, Color? border = null)
        {
            HideOverlay();
            var dim = Ui.Node(_overlay, "Dim").Fill();
            var di = dim.gameObject.AddComponent<Image>();
            di.color = new Color(0, 0, 0, 0.55f);
            var card = Ui.Panel(dim, new Color(0.04f, 0.06f, 0.17f, 0.99f), 20, "Card", border ?? Pal.Gold.WithA(0.55f));
            card.rectTransform.At(0.5f, 0.5f, 0, 0, w, h);
            return card.rectTransform;
        }

        void ShowToss()
        {
            var card = Card(620, 250);
            var m = D.M;
            var title = Ui.Label(card, D.Cfg.Title, 15, Pal.Gold, TextAnchor.MiddleCenter, false);
            title.rectTransform.At(0.5f, 1, 0, -10, 560, 20);
            var h = Crest.Add(card, m.Home.Team, 64, true);
            ((RectTransform)h.transform.parent).At(0, 1, 70, -34, 64, 72);
            var a = Crest.Add(card, m.Away.Team, 64, true);
            ((RectTransform)a.transform.parent).At(1, 1, -70, -34, 64, 72);
            var hn = Ui.Label(card, m.Home.Team.Id, 24, Pal.Text);
            hn.rectTransform.At(0, 1, 70, -108, 64, 26);
            var an = Ui.Label(card, m.Away.Team.Id, 24, Pal.Text);
            an.rectTransform.At(1, 1, -70, -108, 64, 26);
            var coin = Ui.Img(card, Ui.Circle, Pal.Gold, "Coin");
            coin.rectTransform.At(0.5f, 1, 0, -38, 80, 80);
            var coinIn = Ui.Img(coin.transform, Ui.Circle, Pal.GoldLight, "Face");
            coinIn.rectTransform.Fill(6, 6, 6, 6);
            _tossCoin = Ui.Label(coin.transform, "H", 46, new Color(0.42f, 0.29f, 0));
            _tossCoin.rectTransform.Fill();
            _tossCoinRt = coin.rectTransform;
            var venue = Ui.Label(card, m.Venue.Venue + " • " + m.Overs + " overs" + (D.Cfg.Day ? " • 3:30 PM" : " • 7:30 PM"), 15, Pal.Dim, TextAnchor.MiddleCenter, false);
            venue.rectTransform.At(0.5f, 1, 0, -124, 560, 18);
            var info = Ui.Label(card, "", 22, Pal.GoldLight);
            info.rectTransform.At(0.5f, 0, 0, 70, 560, 26);
            info.name = "TossInfo";
            var row = Ui.Node(card, "Row").Band(0, 1, 0, 14, 48, 22);
            var heads = Ui.Button(row, "HEADS", Pal.Gold, Pal.GoldDeep, () => D.CallToss(true), 26);
            ((RectTransform)heads.transform).anchorMin = new Vector2(0, 0);
            ((RectTransform)heads.transform).anchorMax = new Vector2(0.49f, 1);
            ((RectTransform)heads.transform).offsetMin = ((RectTransform)heads.transform).offsetMax = Vector2.zero;
            var tails = Ui.Button(row, "TAILS", Pal.Cyan, Pal.Violet, () => D.CallToss(false), 26);
            ((RectTransform)tails.transform).anchorMin = new Vector2(0.51f, 0);
            ((RectTransform)tails.transform).anchorMax = new Vector2(1, 1);
            ((RectTransform)tails.transform).offsetMin = ((RectTransform)tails.transform).offsetMax = Vector2.zero;
            _tossRow = row.gameObject;
            _tossInfo = info;
        }

        GameObject _tossRow;
        Text _tossInfo;
        bool _choiceShown;

        void UpdateToss()
        {
            if (_tossCoin == null) return;
            if (D.M.Started)
            {
                // Either side's decision has started the match.
                HideOverlay();
                return;
            }
            if (D.Flipping)
            {
                float ang = (float)D.FlipT * 22;
                _tossCoinRt.localScale = new Vector3(1, Mathf.Abs(Mathf.Cos(ang)) * 0.9f + 0.1f, 1);
                _tossCoin.text = Mathf.Cos(ang) >= 0 ? "H" : "T";
                _tossInfo.text = "The coin is in the air…";
            }
            else
            {
                _tossCoinRt.localScale = Vector3.one;
            }
            if (D.TossCall != null && _tossRow.activeSelf && !_choiceShown) _tossRow.SetActive(false);
            if (D.TossText != null) _tossInfo.text = D.TossText;
            if (D.AwaitTossChoice && !_choiceShown)
            {
                _choiceShown = true;
                var card = (RectTransform)_tossRow.transform.parent;
                var row = Ui.Node(card, "Choice").Band(0, 1, 0, 14, 48, 22);
                var bat = Ui.Button(row, "BAT FIRST", Pal.Gold, Pal.GoldDeep, () => { D.ChooseBat(true); HideOverlay(); }, 26);
                var rb = (RectTransform)bat.transform;
                rb.anchorMin = Vector2.zero;
                rb.anchorMax = new Vector2(0.49f, 1);
                rb.offsetMin = rb.offsetMax = Vector2.zero;
                var bowl = Ui.Button(row, "BOWL FIRST", Pal.Pink, Pal.Violet, () => { D.ChooseBat(false); HideOverlay(); }, 26);
                var rw = (RectTransform)bowl.transform;
                rw.anchorMin = new Vector2(0.51f, 0);
                rw.anchorMax = Vector2.one;
                rw.offsetMin = rw.offsetMax = Vector2.zero;
            }
        }

        public void ShowBowlerPicker()
        {
            var m = D.M;
            var inn = m.Current;
            var suggested = m.SuggestBowler();
            var opts = m.EligibleBowlers().OrderByDescending(p => p.Id == suggested.Id ? 1000 : p.Bowl).ToList();
            var card = Card(700, Mathf.Min(380, 70 + ((opts.Count + 1) / 2) * 50));
            var t = Ui.Label(card, "OVER " + (inn.OverIndex + 1) + " • PICK YOUR BOWLER", 26, Pal.Text);
            t.rectTransform.At(0.5f, 1, 0, -8, 600, 32);
            for (int i = 0; i < opts.Count && i < 12; i++)
            {
                var p = opts[i];
                int col = i % 2, row = i / 2;
                var cell = Ui.Panel(card, new Color(0.05f, 0.07f, 0.2f, 0.95f), 12, "Bowler", p.Id == suggested.Id ? Pal.Gold : Pal.Stroke);
                cell.rectTransform.At(col == 0 ? 0 : 1, 1, col == 0 ? 14 : -14, -50 - row * 50, 330, 44);
                var name = Ui.Label(cell.transform, p.Name, 17, Pal.Text, TextAnchor.UpperLeft, false);
                name.rectTransform.Fill(12, 4, 90, 0);
                var style = Ui.Label(cell.transform, (p.Id == suggested.Id ? "SUGGESTED • " : "") + p.BowlStyle.Label(), 12, p.Id == suggested.Id ? Pal.Gold : Pal.Dim, TextAnchor.LowerLeft, false);
                style.rectTransform.Fill(12, 0, 90, 4);
                var fig = Ui.Label(cell.transform, Fig(inn, p.Id), 20, Pal.Text, TextAnchor.UpperRight);
                fig.rectTransform.Fill(0, 3, 10, 0);
                var sk = Ui.Label(cell.transform, "BOWL " + p.Bowl, 11, p.Bowl >= 70 ? Pal.Green : Pal.Mute, TextAnchor.LowerRight, false);
                sk.rectTransform.Fill(0, 0, 10, 4);
                var b = cell.gameObject.AddComponent<Button>();
                int id = p.Id;
                b.onClick.AddListener(() =>
                {
                    Sfx.I?.Click();
                    HideOverlay();
                    D.PickBowler(id);
                });
                cell.gameObject.AddComponent<PressScale>();
            }
        }

        public void ShowPause()
        {
            var card = Card(560, 200);
            SetPaused(true);
            var t = Ui.Label(card, "MATCH PAUSED", 30, Pal.Text);
            t.rectTransform.At(0.5f, 1, 0, -10, 400, 36);
            var items = new List<(string, Color, Action)>
            {
                ("RESUME", Pal.Green, HideOverlay),
            };
            if (D.SimOffered)
            {
                items.Add(("SIM OVER", Pal.Text, () => D.Sim(D.M.SimulateOver)));
                items.Add(("SIM INNINGS", Pal.Text, () => D.Sim(D.M.SimulateInnings)));
                items.Add(("SIM MATCH", Pal.Gold, () => D.Sim(D.M.SimulateToEnd)));
            }
            items.Add(("SCORECARD", Pal.Cyan, () => ShowScorecard(true)));
            items.Add(("QUIT", Pal.Red, Quit));
            for (int i = 0; i < items.Count; i++)
            {
                var (label, col, act) = items[i];
                int c = i % 3, r = i / 3;
                var b = Ui.Ghost(card, label, col, () =>
                {
                    if (label != "SCORECARD") HideOverlay();
                    act();
                }, 22);
                ((RectTransform)b.transform).At(0, 1, 16 + c * 178, -58 - r * 62, 170, 50);
            }
        }

        void Quit()
        {
            if (D.Cfg.OnFinished == null)
            {
                D.Cfg.OnExit?.Invoke();
                return;
            }
            // Season matches must be finished: simulate the rest.
            D.QuitAndSimulate();
        }

        /// <summary>Full scorecard. Opened from the pause menu it stays paused and CLOSE goes back to that menu.</summary>
        public void ShowScorecard(bool fromPause = false)
        {
            var card = Card(820, 400, Pal.Cyan.WithA(0.5f));
            if (fromPause) SetPaused(true);
            Scorecard.Build(card, D.M, () =>
            {
                if (fromPause) ShowPause();
                else HideOverlay();
            });
        }

        public void ShowInningsBreak()
        {
            var m = D.M;
            var prev = m.InningsList[m.InningsList.Count - 2];
            var next = m.Current;
            bool so = next.SuperOver;
            var card = Card(700, 240);
            var t = Ui.Label(card, so ? "SUPER OVER!" : "INNINGS BREAK", 32, so ? Pal.Pink : Pal.Gold, TextAnchor.UpperLeft);
            t.rectTransform.At(0, 1, 20, -12, 380, 36);
            var c = Crest.Add(card, prev.Batting.Team, 40);
            ((RectTransform)c.transform.parent).At(0, 1, 20, -52, 40, 45);
            var s = Ui.Label(card, prev.Batting.Team.Id + " " + prev.ScoreText + "  <size=16>(" + prev.OversText + ")</size>", 34, Pal.Text, TextAnchor.MiddleLeft);
            s.rectTransform.At(0, 1, 70, -54, 320, 40);
            var bats = prev.Bat.Values.OrderByDescending(x => x.Runs).Take(2).ToList();
            var bowls = prev.Bowl.Values.OrderByDescending(x => x.Wickets).ThenBy(x => x.Runs).Take(2).ToList();
            int row = 0;
            foreach (var b in bats)
            {
                var l = Ui.Label(card, prev.Batting.ById(b.PlayerId).Name + "   <b>" + b.Runs + "</b> (" + b.Balls + ")", 16, Pal.Cyan, TextAnchor.MiddleLeft, false);
                l.rectTransform.At(0, 1, 22, -104 - row++ * 22, 380, 20);
            }
            foreach (var b in bowls)
            {
                var l = Ui.Label(card, prev.Bowling.ById(b.PlayerId).Name + "   <b>" + b.Wickets + "/" + b.Runs + "</b> (" + Overs.Text(b.Balls) + ")", 16, Pal.Pink, TextAnchor.MiddleLeft, false);
                l.rectTransform.At(0, 1, 22, -104 - row++ * 22, 380, 20);
            }
            var need = Ui.Label(card, next.Batting.Team.Id + " NEED", 15, Pal.Dim);
            need.rectTransform.At(1, 1, -130, -30, 220, 20);
            var big = Ui.Label(card, next.Target.ToString(), 70, Pal.GoldLight);
            big.rectTransform.At(1, 1, -130, -54, 220, 70);
            var off = Ui.Label(card, "off " + next.MaxOvers + " over" + (next.MaxOvers == 1 ? "" : "s"), 15, Pal.Dim, TextAnchor.MiddleCenter, false);
            off.rectTransform.At(1, 1, -130, -128, 220, 20);
            bool userChasing = next.Batting.Team.Id == D.Cfg.UserTeamId;
            var go = Ui.Button(card, userChasing ? "START CHASE" : "DEFEND " + (next.Target - 1), Pal.Gold, Pal.GoldDeep, () =>
            {
                HideOverlay();
                D.ContinueAfterInnings();
            }, 26);
            ((RectTransform)go.transform).At(1, 0, -20, 18, 220, 52);
        }

        public void ShowDone()
        {
            var m = D.M;
            var o = m.Outcome;
            var winner = Teams.ById(o.WinnerId);
            bool won = o.WinnerId == D.Cfg.UserTeamId;
            var card = Card(720, 250, (won ? Pal.Gold : Pal.Red).WithA(0.6f));
            var title = Ui.Label(card, won ? "VICTORY!" : "DEFEAT", 50, won ? Pal.Gold : Pal.Red);
            title.rectTransform.At(0, 1, 30, -8, 360, 56);
            var c = Crest.Add(card, winner, 64, true);
            ((RectTransform)c.transform.parent).At(0, 1, 178, -70, 64, 72);
            var text = Ui.Label(card, o.Text, 22, Pal.Text);
            text.rectTransform.At(0, 0, 30, 58, 360, 26);
            string lines = "";
            foreach (var inn in m.InningsList.Take(2)) lines += inn.Batting.Team.Id + "  " + inn.ScoreText + " (" + inn.OversText + ")\n";
            var sc = Ui.Label(card, lines.TrimEnd(), 16, Pal.Dim, TextAnchor.UpperCenter, false);
            sc.rectTransform.At(0, 0, 30, 10, 360, 44);
            var potm = m.PlayerById(o.PotmId);
            var pb = Ui.Panel(card, new Color(1, 1, 1, 0.06f), 14, "Potm", Pal.Stroke);
            pb.rectTransform.At(1, 1, -24, -40, 290, 64);
            var pl = Ui.Label(pb.transform, "PLAYER OF THE MATCH", 12, Pal.Gold, TextAnchor.UpperLeft, false);
            pl.rectTransform.Fill(14, 8, 8, 0);
            var pn = Ui.Label(pb.transform, potm.Name, 24, Pal.Text, TextAnchor.LowerLeft);
            pn.rectTransform.Fill(14, 0, 8, 6);
            var b = Ui.Button(card, "CONTINUE", Pal.Gold, Pal.GoldDeep, () => D.Cfg.OnExit?.Invoke(), 28);
            ((RectTransform)b.transform).At(1, 0, -24, 26, 290, 56);
        }
    }

    /// <summary>Invokes on pointer down (for timing-critical buttons).</summary>
    public sealed class PressTrigger : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
    {
        public Action OnPress;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            Sfx.I?.Click();
            OnPress?.Invoke();
        }
    }
}
