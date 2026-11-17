using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// In-match interface: broadcast scorebug, commentary, banners, batting controls (stick + shot
    /// buttons or swipe), timing meter, bowling controls with accuracy meter, catch prompts,
    /// toss, bowler picker, innings break and pause menu.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        MatchDirector _md;
        RectTransform _root, _layer, _modalLayer;
        // Scorebug.
        RectTransform _bug, _chips, _crestSlot;
        Text _bugTeam, _bugScore, _bugOvers, _bugInfo, _bugBat1, _bugBat2, _bugBowl, _freeHit;
        string _crestTeam = "";
        // Info.
        Text _speed, _comment, _skipHint;
        CanvasGroup _commentGroup, _speedGroup;
        float _commentT, _speedT;
        RectTransform _banner;
        Text _bannerBig, _bannerSub;
        Image _bannerBar;
        float _bannerT, _bannerDur;
        // Batting.
        RectTransform _batUi, _timing, _timingMark, _timingPerfect, _timingGood;
        Joystick _stick;
        Text _grade;
        float _gradeT;
        bool _armed;
        ShotInput? _pending;
        bool _swipeMode, _loft;
        Image _loftToggleImg;
        // Bowling.
        RectTransform _bowlUi, _meter, _meterInd, _meterZone;
        Text _targetInfo, _bowlerName, _fieldLabel;
        readonly List<(DeliveryKind Kind, Image Bg, Text Label)> _kindChips = new List<(DeliveryKind, Image, Text)>();
        Action<float> _meterCb;
        bool _meterOn;
        float _meterT, _meterPos, _zoneHalf = 0.12f;
        // Modals.
        RectTransform _modal, _pause;
        Action<float> _catchCb;
        RectTransform _catchUi;
        float _catchT, _catchDur;
        bool _catchDone;

        public static Hud Create(RectTransform parent, MatchDirector md)
        {
            var root = Ui.Node(parent, "Hud").Fill();
            var h = root.gameObject.AddComponent<Hud>();
            h._md = md;
            h._root = root;
            h.Build();
            return h;
        }

        // ------------------------------------------------------------ layout

        void Build()
        {
            // Tap anywhere (not on a control) skips the current play.
            var catcher = Ui.Img(_root, null, new Color(0, 0, 0, 0), "TapCatcher");
            catcher.raycastTarget = true;
            catcher.rectTransform.Fill();
            catcher.gameObject.AddComponent<TapCatcher>().Tapped = () => _md.Skip();
            _layer = Ui.Node(_root, "Layer").Fill();
            BuildBug();
            BuildInfo();
            BuildBatting();
            BuildBowling();
            _modalLayer = Ui.Node(_root, "Modals").Fill();
            var pause = Ui.IconBtn(_layer, Icon.Pause, () => _md.SetPaused(true), 58);
            pause.Rt().At(1, 1, -14, -12, 58, 58);
        }

        void BuildBug()
        {
            _bug = Ui.Node(_layer, "Scorebug").At(0, 1, 14, -12, 560, 104);
            var bg = Ui.Panel(_bug, Pal.Hex(0x081022, 0.9f), 16, "Bg", new Color(1, 1, 1, 0.1f));
            bg.rectTransform.Fill();
            var stripe = Ui.Img(_bug, Sprites.Round(4), Pal.Accent, "Stripe");
            stripe.rectTransform.At(0, 1, 0, 0, 6, 104);
            _crestSlot = Ui.Node(_bug, "Crest").At(0, 1, 14, -10, 54, 54);
            _bugTeam = Ui.Label(_bug, "", 22, Pal.Dim, TextAnchor.UpperLeft, Font.Bold);
            _bugTeam.rectTransform.At(0, 1, 76, -6, 80, 26);
            _bugScore = Ui.Label(_bug, "0/0", 46, Color.white, TextAnchor.UpperLeft, Font.Display);
            _bugScore.rectTransform.At(0, 1, 74, -22, 160, 48);
            _bugOvers = Ui.Label(_bug, "", 22, Pal.Gold, TextAnchor.UpperLeft, Font.Bold);
            _bugOvers.rectTransform.At(0, 1, 200, -38, 110, 26);
            _bugInfo = Ui.Label(_bug, "", 17, Pal.Text, TextAnchor.UpperLeft, Font.Semi);
            _bugInfo.rectTransform.At(0, 1, 286, -10, 270, 22);
            _bugBat1 = Ui.Label(_bug, "", 17, Color.white, TextAnchor.UpperLeft, Font.Semi);
            _bugBat1.rectTransform.At(0, 1, 286, -34, 270, 22);
            _bugBat2 = Ui.Label(_bug, "", 17, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
            _bugBat2.rectTransform.At(0, 1, 286, -56, 270, 22);
            _bugBowl = Ui.Label(_bug, "", 16, Pal.Dim, TextAnchor.UpperLeft, Font.Semi);
            _bugBowl.rectTransform.At(0, 1, 14, -74, 260, 22);
            _chips = Ui.Node(_bug, "ThisOver").At(0, 1, 286, -78, 270, 22);
            var lay = _chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 4;
            lay.childForceExpandWidth = false;
            lay.childControlWidth = false;
            lay.childControlHeight = false;
            _freeHit = Ui.Label(_layer, "FREE HIT", 26, Pal.Accent, TextAnchor.MiddleCenter, Font.Display);
            _freeHit.rectTransform.At(0, 1, 590, -14, 140, 34);
            _freeHit.gameObject.SetActive(false);
        }

        void BuildInfo()
        {
            _speed = Ui.Label(_layer, "", 22, Color.white, TextAnchor.MiddleRight, Font.Bold);
            _speed.rectTransform.At(1, 1, -84, -18, 340, 30);
            Ui.Shadow(_speed, 2, 0.6f);
            _speedGroup = _speed.gameObject.AddComponent<CanvasGroup>();
            _speedGroup.alpha = 0;

            var cbg = Ui.Panel(_layer, Pal.Hex(0x081022, 0.82f), 14, "Commentary");
            cbg.raycastTarget = false;
            cbg.rectTransform.At(0.5f, 1, 70, -126, 700, 38);
            _comment = Ui.Label(cbg.transform, "", 18, Pal.Text, TextAnchor.MiddleCenter, Font.Semi);
            _comment.rectTransform.Fill(14, 0, 14, 0);
            _commentGroup = cbg.gameObject.AddComponent<CanvasGroup>();
            _commentGroup.alpha = 0;
            _commentGroup.blocksRaycasts = false;

            _skipHint = Ui.Label(_layer, "TAP TO SKIP", 14, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
            _skipHint.rectTransform.At(0.5f, 0, 0, 54, 200, 18);
            _skipHint.gameObject.SetActive(false);

            _banner = Ui.Node(_layer, "Banner").At(0.5f, 0.5f, 0, 120, 820, 150);
            _bannerBar = Ui.Img(_banner, Sprites.HGradient(0, 0), Pal.Accent, "Bar");
            _bannerBar.rectTransform.At(0.5f, 0.5f, 0, -42, 640, 6);
            _bannerBig = Ui.Label(_banner, "", 96, Color.white, TextAnchor.MiddleCenter, Font.Display);
            _bannerBig.rectTransform.At(0.5f, 0.5f, 0, 18, 820, 100);
            Ui.Outline(_bannerBig, 3, 0.6f);
            _bannerSub = Ui.Label(_banner, "", 24, Pal.Text, TextAnchor.MiddleCenter, Font.Bold);
            _bannerSub.rectTransform.At(0.5f, 0.5f, 0, -64, 820, 30);
            Ui.Shadow(_bannerSub, 2, 0.7f);
            _banner.gameObject.SetActive(false);
        }

        void BuildBatting()
        {
            _batUi = Ui.Node(_layer, "Batting").Fill();
            bool lefty = SaveSystem.Data.Settings.LeftHanded;
            _swipeMode = SaveSystem.Data.Settings.Controls == 1;
            float sx = lefty ? 1 : 0, bx = lefty ? 0 : 1, sgn = lefty ? -1 : 1;

            if (_swipeMode)
            {
                var pad = Ui.Img(_batUi, null, new Color(1, 1, 1, 0.0f), "SwipePad");
                pad.raycastTarget = true;
                pad.rectTransform.anchorMin = new Vector2(0.18f, 0.12f);
                pad.rectTransform.anchorMax = new Vector2(0.98f, 0.82f);
                pad.rectTransform.offsetMin = pad.rectTransform.offsetMax = Vector2.zero;
                var sp = pad.gameObject.AddComponent<SwipePad>();
                sp.Swiped = (d, speed) => Shot(_loft || speed > 2600 || d.magnitude > 320 ? ShotKind.Loft : ShotKind.Ground, AzFromScreen(d.normalized));
                sp.Tapped = () => Shot(ShotKind.Defend, 0);
                var hint = Ui.Label(_batUi, "SWIPE IN THE SHOT DIRECTION  •  TAP TO DEFEND", 15, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                hint.rectTransform.At(0.5f, 0, 0, 92, 600, 20);
                var lt = Ui.Btn(_batUi, "LOFT", BtnStyle.Ghost, () =>
                {
                    _loft = !_loft;
                    _loftToggleImg.color = _loft ? Pal.Gold : new Color(1, 1, 1, 0.06f);
                }, 22, Icon.Arrow);
                lt.Rt().At(sx, 0, 24 * sgn, 24, 140, 56);
                _loftToggleImg = lt.GetComponent<Image>();
            }
            else
            {
                var stickRoot = Ui.Node(_batUi, "Stick").At(sx, 0, 40 * sgn, 30, 210, 210);
                var baseImg = Ui.Img(stickRoot, Sprites.Circle, new Color(1, 1, 1, 0.08f), "Base");
                baseImg.raycastTarget = true;
                baseImg.rectTransform.Fill();
                var ring = Ui.Img(stickRoot, Sprites.Ring(4), new Color(1, 1, 1, 0.3f), "Ring");
                ring.rectTransform.Fill();
                foreach (var (label, x, y) in new[] { ("STRAIGHT", 0f, 92f), ("OFF", -88f * sgn, 0f), ("LEG", 88f * sgn, 0f), ("FINE", 0f, -92f) })
                {
                    var l = Ui.Label(stickRoot, label, 12, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, Font.Bold);
                    l.rectTransform.At(0.5f, 0.5f, x, y, 80, 16);
                }
                var knob = Ui.Img(stickRoot, Sprites.Circle, Pal.Accent.A(0.92f), "Knob");
                knob.rectTransform.At(0.5f, 0.5f, 0, 0, 84, 84);
                var kicon = Ui.IconImg(knob.transform, Icon.Bat, Color.white, 40);
                kicon.rectTransform.At(0.5f, 0.5f, 0, 0, 40, 40);
                _stick = stickRoot.gameObject.AddComponent<Joystick>();
                _stick.Knob = knob.rectTransform;
                _stick.Radius = 70;
                var help = Ui.Label(_batUi, "AIM (optional)", 13, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                help.rectTransform.At(sx, 0, (40 + 105) * sgn - (lefty ? 0 : 0), 246, 210, 16);
                help.rectTransform.anchoredPosition = new Vector2(lefty ? -145 : 145, 246);

                ShotButton("GROUND", "Along the floor", Pal.Teal, bx, -118 * sgn, 112, 150, () => Shot(ShotKind.Ground, StickAz()));
                ShotButton("LOFT", "In the air", Pal.Gold, bx, -280 * sgn, 74, 124, () => Shot(ShotKind.Loft, StickAz()));
                ShotButton("DEFEND", "Block it", Pal.Sky, bx, -120 * sgn, 276, 108, () => Shot(ShotKind.Defend, 0));
                var leave = Ui.Btn(_batUi, "LEAVE", BtnStyle.Ghost, () => { }, 18);
                leave.onClick.RemoveAllListeners();
                leave.gameObject.AddComponent<TouchButton>().Down = () => Shot(ShotKind.Leave, 0);
                leave.Rt().At(bx, 0, -282 * sgn, 222, 110, 44);
            }

            _timing = Ui.Node(_batUi, "Timing").At(0.5f, 0, 0, 62, 420, 26);
            var tbg = Ui.Panel(_timing, Pal.Hex(0x081022, 0.75f), 12, "Bg", new Color(1, 1, 1, 0.12f));
            tbg.rectTransform.Fill();
            _timingGood = Ui.Img(_timing, Sprites.Round(8), Pal.Gold.A(0.55f), "Good").rectTransform;
            _timingPerfect = Ui.Img(_timing, Sprites.Round(8), Pal.Green.A(0.9f), "Perfect").rectTransform;
            _timingMark = Ui.Img(_timing, Sprites.Circle, Color.white, "Ball").rectTransform;
            _timingMark.sizeDelta = new Vector2(22, 22);
            _timing.gameObject.SetActive(false);

            _grade = Ui.Label(_layer, "", 40, Pal.Green, TextAnchor.MiddleCenter, Font.Display);
            _grade.rectTransform.At(0.5f, 0.5f, 0, -40, 600, 90);
            Ui.Outline(_grade, 2.5f, 0.7f);
            _grade.gameObject.SetActive(false);
            _batUi.gameObject.SetActive(false);
        }

        void ShotButton(string label, string sub, Color c, float ax, float x, float y, float size, Action down)
        {
            var rt = Ui.Node(_batUi, label).At(ax, 0, x, y, size, size);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y + size / 2);
            var bg = Ui.Img(rt, Sprites.Circle, c.A(0.9f), "Bg");
            bg.raycastTarget = true;
            bg.rectTransform.Fill();
            var sheen = Ui.Img(rt, Sprites.Glow, new Color(1, 1, 1, 0.25f), "Sheen");
            sheen.rectTransform.At(0.5f, 0.5f, -size * 0.12f, size * 0.12f, size * 0.7f, size * 0.7f);
            var ring = Ui.Img(rt, Sprites.Ring(5), new Color(1, 1, 1, 0.75f), "Ring");
            ring.rectTransform.Fill();
            var t = Ui.Label(rt, label, (int)(size * 0.2f), c.Luma() > 0.6f ? Pal.Ink : Color.white, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 0.5f, 0, 6, size, size * 0.3f);
            var s = Ui.Label(rt, sub, (int)Mathf.Max(11, size * 0.1f), (c.Luma() > 0.6f ? Pal.Ink : Color.white).A(0.75f), TextAnchor.MiddleCenter, Font.Semi);
            s.rectTransform.At(0.5f, 0.5f, 0, -size * 0.17f, size, size * 0.16f);
            rt.gameObject.AddComponent<TouchButton>().Down = down;
        }

        void BuildBowling()
        {
            _bowlUi = Ui.Node(_layer, "Bowling").Fill();
            var bar = Ui.Panel(_bowlUi, Pal.Hex(0x081022, 0.85f), 16, "Kinds", new Color(1, 1, 1, 0.1f));
            bar.rectTransform.At(0, 0, 14, 14, 640, 112);
            _bowlerName = Ui.Label(bar.transform, "", 18, Pal.Gold, TextAnchor.UpperLeft, Font.Bold);
            _bowlerName.rectTransform.At(0, 1, 14, -8, 600, 22);
            var row = Ui.Node(bar.transform, "Row").At(0, 0, 10, 10, 620, 64);
            var lay = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 6;
            lay.childForceExpandWidth = true;
            lay.childControlWidth = true;
            lay.childControlHeight = true;
            _targetInfo = Ui.Label(_bowlUi, "", 18, Pal.Text, TextAnchor.MiddleCenter, Font.Bold);
            _targetInfo.rectTransform.At(0.5f, 0, 70, 136, 520, 24);
            Ui.Shadow(_targetInfo, 2, 0.7f);
            var hint = Ui.Label(_bowlUi, "DRAG ON THE PITCH TO AIM", 14, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
            hint.rectTransform.At(0.5f, 0, 70, 160, 520, 18);
            var bowl = Ui.Btn(_bowlUi, "BOWL", BtnStyle.Primary, () => _bowlCb?.Invoke(false), 40, Icon.Ball);
            bowl.Rt().At(1, 0, -18, 18, 210, 92);
            bowl.gameObject.AddComponent<Pulse>().Amount = 0.025f;
            var auto = Ui.Btn(_bowlUi, "AUTO", BtnStyle.Secondary, () => _bowlCb?.Invoke(true), 20, Icon.Refresh);
            auto.Rt().At(1, 0, -240, 18, 130, 50);
            var field = Ui.Btn(_bowlUi, "FIELD", BtnStyle.Secondary, CycleField, 18, Icon.Users);
            field.Rt().At(1, 0, -240, 74, 130, 44);
            _fieldLabel = Ui.Label(_bowlUi, "AUTO FIELD", 13, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
            _fieldLabel.rectTransform.At(1, 0, -240, 120, 130, 16);
            _kindRow = row;

            _meter = Ui.Node(_layer, "Meter").At(1, 0.5f, -40, 30, 56, 330);
            var mbg = Ui.Panel(_meter, Pal.Hex(0x081022, 0.85f), 18, "Bg", new Color(1, 1, 1, 0.2f));
            mbg.rectTransform.Fill();
            var grad = Ui.Img(_meter, Sprites.VGradient(1, 1), Pal.Red.A(0.5f), "Red");
            grad.rectTransform.Fill(8, 8, 8, 8);
            _meterZone = Ui.Img(_meter, Sprites.Round(8), Pal.Green.A(0.9f), "Zone").rectTransform;
            _meterInd = Ui.Img(_meter, Sprites.Round(6), Color.white, "Ind").rectTransform;
            _meterInd.sizeDelta = new Vector2(70, 10);
            var ml = Ui.Label(_meter, "TAP!", 16, Pal.Gold, TextAnchor.MiddleCenter, Font.Display);
            ml.rectTransform.At(0.5f, 1, 0, 26, 80, 22);
            _meter.gameObject.SetActive(false);
            _bowlUi.gameObject.SetActive(false);
        }

        RectTransform _kindRow;
        Action<bool> _bowlCb;
        int _fieldIdx = -1;

        void CycleField()
        {
            _fieldIdx = (_fieldIdx + 1) % 3;
            var f = (FieldSetting)_fieldIdx;
            _md.SetField(f);
            _fieldLabel.text = f == FieldSetting.Attacking ? "ATTACKING" : f == FieldSetting.Balanced ? "BALANCED" : "DEFENSIVE";
        }

        // ------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_commentT > 0)
            {
                _commentT -= dt;
                _commentGroup.alpha = Mathf.Clamp01(_commentT / 0.4f);
            }
            if (_speedT > 0)
            {
                _speedT -= dt;
                _speedGroup.alpha = Mathf.Clamp01(_speedT / 0.5f);
            }
            if (_banner.gameObject.activeSelf)
            {
                _bannerT += dt;
                float k = Mathf.Clamp01(_bannerT / 0.18f);
                float s = Mathf.Lerp(1.4f, 1, 1 - Mathf.Pow(1 - k, 3));
                _banner.localScale = new Vector3(s, s, 1);
                var g = _banner.GetComponent<CanvasGroup>() ?? _banner.gameObject.AddComponent<CanvasGroup>();
                g.alpha = Mathf.Clamp01(Mathf.Min(_bannerT / 0.12f, (_bannerDur - _bannerT) / 0.3f));
                g.blocksRaycasts = false;
                if (_bannerT > _bannerDur) _banner.gameObject.SetActive(false);
            }
            if (_gradeT > 0)
            {
                _gradeT -= dt;
                var c = _grade.color;
                c.a = Mathf.Clamp01(_gradeT / 0.3f);
                _grade.color = c;
                if (_gradeT <= 0) _grade.gameObject.SetActive(false);
            }
            if (_meterOn)
            {
                _meterT += dt;
                _meterPos = Mathf.PingPong(_meterT * 1.35f, 1);
                float h = _meter.rect.height - 20;
                _meterInd.anchorMin = _meterInd.anchorMax = new Vector2(0.5f, 0);
                _meterInd.anchoredPosition = new Vector2(0, 10 + _meterPos * h);
                if (Input.GetMouseButtonDown(0) && !(_bowlUi.gameObject.activeSelf && PointerOverControl())) StopMeterNow();
            }
            if (_catchUi != null && !_catchDone)
            {
                _catchT += dt;
                float k = Mathf.Clamp01(_catchT / _catchDur);
                var inner = _catchUi.Find("Inner") as RectTransform;
                if (inner != null)
                {
                    float sz = Mathf.Lerp(330, 120, k);
                    inner.sizeDelta = new Vector2(sz, sz);
                    inner.GetComponent<Image>().color = k > 0.66f ? Pal.Green : Pal.Gold;
                }
                if (k >= 1.15f || _catchT > _catchDur * 1.15f) FinishCatch(-1);
            }
            _skipHint.gameObject.SetActive(_md.InPlay && !_armed);
        }

        public bool PointerOverControl()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
            {
                if (h.gameObject.name != "TapCatcher") return true;
            }
            return false;
        }

        // ------------------------------------------------------------ scorebug

        public void Refresh(MatchState m)
        {
            if (!m.Started) return;
            var inn = m.Finished ? m.InningsList.Last() : m.Current;
            var team = inn.Bat.Team;
            if (_crestTeam != team.Id)
            {
                foreach (Transform c in _crestSlot) Destroy(c.gameObject);
                var crest = Crest.Add(_crestSlot, team, 54, false);
                crest.anchorMin = crest.anchorMax = new Vector2(0.5f, 0.5f);
                crest.anchoredPosition = Vector2.zero;
                _crestTeam = team.Id;
            }
            _bugTeam.text = team.Id + (inn.SuperOver ? "  SUPER OVER" : "");
            _bugScore.text = inn.Score;
            _bugOvers.text = "(" + inn.OversText + (m.IsScenario ? "/" + Overs.Text(inn.MaxBalls) : "/" + inn.MaxOvers) + ")";
            if (inn.Target != null) _bugInfo.text = "<color=#FFC94A>NEED " + inn.RunsNeeded + "</color> OFF " + inn.BallsLeft + "  •  RRR " + inn.RequiredRate.ToString("0.00");
            else if (m.IsScenario && _md.Ctx.Challenge != null) _bugInfo.text = "<color=#FFC94A>" + _md.Ctx.Challenge.GoalText.ToUpperInvariant() + "</color>";
            else _bugInfo.text = "CRR " + inn.RunRate.ToString("0.00") + (inn.Balls > 6 ? "  •  PROJ " + Mathf.RoundToInt((float)(inn.Runs + inn.RunRate * inn.BallsLeft / 6)) : "");
            if (!inn.Complete)
            {
                var s = inn.StrikerCard;
                var n = inn.NonStrikerCard;
                _bugBat1.text = "▶ " + s.P.Short + "  <b>" + s.Runs + "</b> (" + s.Balls + ")";
                _bugBat2.text = "   " + n.P.Short + "  " + n.Runs + " (" + n.Balls + ")";
                if (inn.Bowler != null)
                {
                    var bc = inn.CardFor(inn.Bowler);
                    _bugBowl.text = inn.Bowler.Short + "  " + bc.Wkts + "-" + bc.Runs + " (" + bc.OversText + ")";
                }
            }
            foreach (Transform c in _chips) Destroy(c.gameObject);
            foreach (var b in inn.ThisOver)
            {
                Color bg = b.Wicket ? Pal.Red : b.Six ? Pal.Violet : b.Four ? Pal.Sky : b.Extras > 0 && !b.Legal ? Pal.Accent : new Color(1, 1, 1, 0.14f);
                var chip = Ui.Panel(_chips, bg, 10, "Chip");
                chip.rectTransform.sizeDelta = new Vector2(b.Chip.Length > 2 ? 36 : 24, 22);
                var t = Ui.Label(chip.transform, b.Chip, 14, Color.white, TextAnchor.MiddleCenter, Font.Bold);
                t.rectTransform.Fill();
            }
            _freeHit.gameObject.SetActive(!inn.Complete && inn.FreeHit);
        }

        public void Commentary(string text)
        {
            if (!SaveSystem.Data.Settings.Commentary || string.IsNullOrEmpty(text)) return;
            _comment.text = text;
            _commentT = 4.5f;
            _commentGroup.alpha = 1;
        }

        public void Speed(double kph, string label)
        {
            _speed.text = kph.ToString("0.0") + " KM/H  •  <color=#FFC94A>" + label + "</color>";
            _speedT = 3.5f;
            _speedGroup.alpha = 1;
        }

        public void Banner(string big, string sub, Color c, float seconds)
        {
            _bannerBig.text = big;
            _bannerBig.color = c;
            _bannerSub.text = sub;
            _bannerBar.color = c;
            _bannerT = 0;
            _bannerDur = seconds;
            _banner.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ batting

        public void SetBatting(bool on, MatchState m)
        {
            _batUi.gameObject.SetActive(on);
            _bowlUi.gameObject.SetActive(false);
            _stick?.ResetStick();
            _pending = null;
            _armed = false;
        }

        public void ArmShot(bool on)
        {
            _armed = on;
            if (on) _pending = null;
            _timing.gameObject.SetActive(on && SaveSystem.Data.Settings.TimingAssist);
            if (on)
            {
                var w = _md.E.UserWindows;
                float contact = 0.8f;
                float scale = 1 / 1.25f / 0.55f;
                float perfect = (float)w.Perfect / 1000f * scale, good = (float)w.Good / 1000f * scale;
                float width = _timing.rect.width;
                _timingPerfect.anchorMin = _timingPerfect.anchorMax = new Vector2(contact, 0.5f);
                _timingPerfect.sizeDelta = new Vector2(Mathf.Max(10, perfect * 2 * width), 18);
                _timingGood.anchorMin = _timingGood.anchorMax = new Vector2(contact, 0.5f);
                _timingGood.sizeDelta = new Vector2(Mathf.Max(16, good * 2 * width), 18);
            }
        }

        public void Timing(float progress, bool show)
        {
            if (!show)
            {
                _timing.gameObject.SetActive(false);
                return;
            }
            float x = Mathf.Clamp01(progress * 0.8f);
            _timingMark.anchorMin = _timingMark.anchorMax = new Vector2(x, 0.5f);
            _timingMark.anchoredPosition = Vector2.zero;
        }

        void Shot(ShotKind kind, double az)
        {
            if (!_armed || _pending != null) return;
            _pending = new ShotInput { Kind = kind, Azimuth = az };
            Haptics.Pulse(15);
        }

        public ShotInput? ConsumeShot()
        {
            if (_pending == null) return null;
            var p = _pending;
            _pending = null;
            _armed = false;
            return p;
        }

        /// <summary>Stick direction to a batter-relative angle; neutral = play the natural shot for the ball.</summary>
        double StickAz()
        {
            if (_stick == null || _stick.Value.sqrMagnitude < 0.05f) return double.NaN;
            return AzFromScreen(_stick.Value);
        }

        double AzFromScreen(Vector2 v)
        {
            var striker = _md.M.Current.StrikerCard.P;
            float side = striker.LeftBat ? -1 : 1;
            bool behind = _md.Cam.Mode == CamMode.BehindBatter;
            float x = behind ? v.x : -v.x;
            return Mathf.Atan2(x * side, v.y) * Mathf.Rad2Deg;
        }

        public void ShowGrade(TimingGrade g, string shot)
        {
            string text;
            Color c;
            switch (g)
            {
                case TimingGrade.Perfect: text = "PERFECT"; c = Pal.Green; break;
                case TimingGrade.Good: text = "GOOD"; c = Pal.Teal; break;
                case TimingGrade.Early: text = "EARLY"; c = Pal.Gold; break;
                case TimingGrade.Late: text = "LATE"; c = Pal.Gold; break;
                case TimingGrade.VeryEarly: text = "TOO EARLY"; c = Pal.Accent; break;
                case TimingGrade.VeryLate: text = "TOO LATE"; c = Pal.Accent; break;
                case TimingGrade.Miss: text = "MISSED"; c = Pal.Red; break;
                default: return;
            }
            _grade.text = text + "\n<size=22>" + shot + "</size>";
            _grade.color = c;
            _gradeT = 1.4f;
            _grade.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ bowling

        public void ShowBowling(Player bowler, DeliveryKind[] kinds, DeliveryKind current, Action<DeliveryKind> onKind, Action<bool> onBowl)
        {
            _batUi.gameObject.SetActive(false);
            _bowlUi.gameObject.SetActive(true);
            _bowlCb = auto =>
            {
                _bowlCb = null;
                onBowl(auto);
            };
            var bt = bowler.CanBowl ? bowler.Bowl : BowlType.FastMedium;
            var inn = _md.M.Current;
            var card = inn.CardFor(bowler);
            _bowlerName.text = bowler.Name.ToUpperInvariant() + "  •  " + bt.Label().ToUpperInvariant() + "  •  " + card.Wkts + "-" + card.Runs + " (" + card.OversText + ")";
            foreach (Transform c in _kindRow) Destroy(c.gameObject);
            _kindChips.Clear();
            foreach (var k in kinds)
            {
                var kk = k;
                var rt = Ui.Node(_kindRow, k.ToString());
                var bg = rt.gameObject.AddComponent<Image>();
                bg.sprite = Sprites.Round(12);
                bg.type = Image.Type.Sliced;
                var t = Ui.Label(rt, k.Label(bt), 16, Color.white, TextAnchor.MiddleCenter, Font.Bold);
                t.rectTransform.Fill(4, 0, 4, 0);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                rt.gameObject.AddComponent<Pressable>();
                b.onClick.AddListener(() =>
                {
                    Audio.Play(Sfx.Click);
                    onKind(kk);
                    HighlightKind(kk);
                });
                _kindChips.Add((k, bg, t));
            }
            HighlightKind(current);
        }

        void HighlightKind(DeliveryKind k)
        {
            foreach (var (kind, bg, t) in _kindChips)
            {
                bool on = kind == k;
                bg.color = on ? Pal.Accent : new Color(1, 1, 1, 0.08f);
                t.color = on ? Color.white : Pal.Dim;
            }
        }

        public void TargetInfo(LengthClass len, double line)
        {
            string l = line > 0.5 ? "WIDE OUTSIDE OFF" : line > 0.15 ? "OUTSIDE OFF" : line > -0.12 ? "ON THE STUMPS" : line > -0.35 ? "LEG STUMP" : "DOWN LEG";
            string n = len == LengthClass.Yorker ? "YORKER" : len == LengthClass.Full ? "FULL" : len == LengthClass.Good ? "GOOD LENGTH" : len == LengthClass.Short ? "SHORT" : len == LengthClass.Bouncer ? "BOUNCER" : "FULL TOSS";
            _targetInfo.text = n + "  •  " + l;
        }

        public void HideBowling() => _bowlUi.gameObject.SetActive(false);

        public void StartMeter(Action<float> done)
        {
            _meterCb = done;
            _meterOn = true;
            _meterT = UnityEngine.Random.value;
            float acc = (float)(Shop.ById(SaveSystem.Data.Spikes)?.Accuracy ?? 0);
            _zoneHalf = 0.09f + acc * 0.6f + (SaveSystem.Data.Settings.Difficulty == 0 ? 0.04f : 0);
            float h = 310;
            _meterZone.anchorMin = _meterZone.anchorMax = new Vector2(0.5f, 0);
            _meterZone.anchoredPosition = new Vector2(0, 10 + 0.5f * h);
            _meterZone.sizeDelta = new Vector2(44, _zoneHalf * 2 * h);
            _meter.gameObject.SetActive(true);
            _bowlUi.gameObject.SetActive(false);
        }

        void StopMeterNow()
        {
            if (!_meterOn) return;
            _meterOn = false;
            float d = Mathf.Abs(_meterPos - 0.5f);
            float acc = d <= _zoneHalf ? 1 - d / _zoneHalf * 0.15f : Mathf.Clamp01(0.85f - (d - _zoneHalf) * 2.2f);
            _meterCb?.Invoke(acc);
            _meterCb = null;
            StartCoroutine(HideMeterSoon());
        }

        IEnumerator HideMeterSoon()
        {
            yield return new WaitForSeconds(0.5f);
            _meter.gameObject.SetActive(false);
        }

        /// <summary>Run-up finished without a tap: take the meter where it is.</summary>
        public float StopMeter()
        {
            float before = -1;
            _meterCb = a => before = a;
            StopMeterNow();
            return Mathf.Max(0.2f, before);
        }

        public void ShowAccuracy(float acc)
        {
            string t = acc >= 0.9f ? "PINPOINT" : acc >= 0.7f ? "ACCURATE" : acc >= 0.45f ? "WAYWARD" : "LOOSE";
            ShowGradeColor(t, acc >= 0.7f ? Pal.Green : acc >= 0.45f ? Pal.Gold : Pal.Red);
        }

        void ShowGradeColor(string text, Color c)
        {
            _grade.text = text;
            _grade.color = c;
            _gradeT = 1.0f;
            _grade.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ catches

        public void CatchPrompt(float seconds, Action<float> result)
        {
            _catchCb = result;
            _catchDur = seconds;
            _catchT = 0;
            _catchDone = false;
            _catchUi = Ui.Node(_modalLayer, "Catch").Fill();
            var dim = Ui.Img(_catchUi, null, new Color(0, 0, 0, 0.25f), "Dim");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var outer = Ui.Img(_catchUi, Sprites.Ring(6), Color.white, "Outer");
            outer.rectTransform.At(0.5f, 0.5f, 0, 0, 130, 130);
            var inner = Ui.Img(_catchUi, Sprites.Ring(10), Pal.Gold, "Inner");
            inner.rectTransform.At(0.5f, 0.5f, 0, 0, 330, 330);
            var t = Ui.Label(_catchUi, "TAP TO CATCH!", 40, Color.white, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 0.5f, 0, -120, 600, 50);
            Ui.Outline(t, 2, 0.7f);
            dim.gameObject.AddComponent<TapCatcher>().Tapped = () => FinishCatch(Mathf.Clamp01(_catchT / _catchDur));
            Audio.Play(Sfx.Pop);
            if (AutoPilot.Active) StartCoroutine(AutoCatch(seconds));
        }

        IEnumerator AutoCatch(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds * 0.4f);
            AutoPilot.Tag("catch_prompt");
            yield return new WaitForSecondsRealtime(seconds * 0.35f);
            FinishCatch(Mathf.Clamp01(_catchT / _catchDur));
        }

        void FinishCatch(float k)
        {
            if (_catchDone) return;
            _catchDone = true;
            var cb = _catchCb;
            _catchCb = null;
            if (_catchUi != null) Destroy(_catchUi.gameObject);
            _catchUi = null;
            cb?.Invoke(k);
        }

        // -------------------------------------------------------------- modals

        RectTransform Modal(string name, float w, float h, bool dim = true)
        {
            CloseModal();
            var root = Ui.Node(_modalLayer, name).Fill();
            if (dim)
            {
                var d = Ui.Img(root, null, new Color(0.01f, 0.02f, 0.06f, 0.6f), "Dim");
                d.raycastTarget = true;
                d.rectTransform.Fill();
            }
            var card = Ui.Card(root, "Card", Pal.Accent.A(0.35f));
            card.rectTransform.At(0.5f, 0.5f, 0, 0, w, h);
            Enter.Add(card, 0, 16);
            _modal = root;
            return card.rectTransform;
        }

        public bool CloseModal()
        {
            if (_modal == null) return false;
            Destroy(_modal.gameObject);
            _modal = null;
            return true;
        }

        public IEnumerator Intro(MatchDirector md)
        {
            var m = md.M;
            var root = Ui.Node(_modalLayer, "Intro").Fill();
            var band = Ui.Img(root, Sprites.HGradient(0.0f, 0.0f), Color.white, "Band");
            var bandBg = Ui.Img(root, null, Pal.Hex(0x060A14, 0.82f), "BandBg");
            bandBg.rectTransform.At(0.5f, 0.5f, 0, 0, 1400, 220);
            band.gameObject.SetActive(false);
            var title = Ui.Label(root, m.Setup.Title, 22, Pal.Gold, TextAnchor.MiddleCenter, Font.Bold);
            title.rectTransform.At(0.5f, 0.5f, 0, 84, 1000, 28);
            var home = m.Setup.Home.Team;
            var away = m.Setup.Away.Team;
            var c1 = Crest.Add(root, home, 120);
            c1.At(0.5f, 0.5f, -380, 0, 120, 120);
            var c2 = Crest.Add(root, away, 120);
            c2.At(0.5f, 0.5f, 380, 0, 120, 120);
            var n1 = Ui.Label(root, home.Name.ToUpperInvariant(), 40, Color.white, TextAnchor.MiddleCenter, Font.Display);
            n1.rectTransform.At(0.5f, 0.5f, -170, 10, 300, 50);
            var n2 = Ui.Label(root, away.Name.ToUpperInvariant(), 40, Color.white, TextAnchor.MiddleCenter, Font.Display);
            n2.rectTransform.At(0.5f, 0.5f, 170, 10, 300, 50);
            var vs = Ui.Label(root, "VS", 54, Pal.Accent, TextAnchor.MiddleCenter, Font.Display);
            vs.rectTransform.At(0.5f, 0.5f, 0, 6, 120, 60);
            var venue = Ui.Label(root, m.Setup.Venue.Ground.ToUpperInvariant() + "  •  " + (m.Setup.Night ? "NIGHT" : "DAY") + "  •  " + (m.IsScenario ? "CHALLENGE" : m.Setup.Overs + " OVERS"), 18, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
            venue.rectTransform.At(0.5f, 0.5f, 0, -70, 1000, 24);
            Enter.Add(bandBg, 0, 30);
            bool tapped = false;
            var hit = Ui.Img(root, null, new Color(0, 0, 0, 0), "Hit");
            hit.raycastTarget = true;
            hit.rectTransform.Fill();
            hit.gameObject.AddComponent<TapCatcher>().Tapped = () => tapped = true;
            Audio.Play(Sfx.Whoosh, 0.6f);
            Audio.CrowdSwell(0.8f, 2);
            float t = 0;
            while (t < (AutoPilot.Active ? 0.8f : 3.2f) && !tapped)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(root.gameObject);
        }

        public IEnumerator Message(string big, string small, float seconds)
        {
            Banner(big, small, Pal.Gold, seconds);
            float t = 0;
            while (t < (AutoPilot.Active ? Mathf.Min(1f, seconds) : seconds))
            {
                t += Time.unscaledDeltaTime;
                if (t > 0.6f && Input.GetMouseButtonDown(0)) break;
                yield return null;
            }
            _banner.gameObject.SetActive(false);
        }

        public void TossCall(Action<bool> call)
        {
            var c = Modal("Toss", 560, 300);
            var t = Ui.Label(c, "CALL THE TOSS", 40, Color.white, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 1, 0, -22, 500, 50);
            var sub = Ui.Label(c, "Your captain calls as the coin goes up", 18, Pal.Dim, TextAnchor.MiddleCenter, Font.Semi);
            sub.rectTransform.At(0.5f, 1, 0, -74, 500, 24);
            var coin = Ui.Img(c, Sprites.Circle, Pal.Gold, "Coin");
            coin.rectTransform.At(0.5f, 0.5f, 0, 6, 70, 70);
            var h = Ui.Btn(c, "HEADS", BtnStyle.Gold, () => { CloseModal(); call(true); }, 26);
            h.Rt().At(0.5f, 0, -120, 26, 210, 64);
            var tl = Ui.Btn(c, "TAILS", BtnStyle.Primary, () => { CloseModal(); call(false); }, 26);
            tl.Rt().At(0.5f, 0, 120, 26, 210, 64);
            if (AutoPilot.Active) StartCoroutine(AutoPress(() => { CloseModal(); call(true); }));
        }

        IEnumerator AutoPress(Action a)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            a();
        }

        public IEnumerator TossFlip(bool heads)
        {
            var root = Ui.Node(_modalLayer, "Flip").Fill();
            var coin = Ui.Img(root, Sprites.Circle, Pal.Gold, "Coin");
            coin.rectTransform.At(0.5f, 0.5f, 0, 0, 140, 140);
            var face = Ui.Label(coin.transform, "", 46, Pal.Hex(0x5A3A00), TextAnchor.MiddleCenter, Font.Display);
            face.rectTransform.Fill();
            Audio.Play(Sfx.Whoosh);
            float t = 0;
            while (t < 1.3f)
            {
                t += Time.unscaledDeltaTime;
                float y = Mathf.Sin(t / 1.3f * Mathf.PI) * 180;
                coin.rectTransform.anchoredPosition = new Vector2(0, y);
                float s = Mathf.Abs(Mathf.Cos(t * 18));
                coin.rectTransform.localScale = new Vector3(1, s, 1);
                face.text = Mathf.Cos(t * 18) > 0 ? "H" : "T";
                yield return null;
            }
            coin.rectTransform.localScale = Vector3.one;
            face.text = heads ? "H" : "T";
            Audio.Play(Sfx.Coin);
            var res = Ui.Label(root, heads ? "HEADS" : "TAILS", 54, Color.white, TextAnchor.MiddleCenter, Font.Display);
            res.rectTransform.At(0.5f, 0.5f, 0, -130, 400, 60);
            Ui.Outline(res, 2, 0.7f);
            yield return new WaitForSecondsRealtime(AutoPilot.Active ? 0.3f : 1.0f);
            Destroy(root.gameObject);
        }

        public void TossChoose(TeamDef user, Action<bool> bat)
        {
            var c = Modal("TossChoose", 560, 280);
            var t = Ui.Label(c, "YOU WON THE TOSS", 40, Pal.Gold, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 1, 0, -24, 500, 50);
            var sub = Ui.Label(c, "What will " + user.Name + " do?", 18, Pal.Dim, TextAnchor.MiddleCenter, Font.Semi);
            sub.rectTransform.At(0.5f, 1, 0, -76, 500, 24);
            var b1 = Ui.Btn(c, "BAT", BtnStyle.Teal, () => { CloseModal(); bat(true); }, 28, Icon.Bat);
            b1.Rt().At(0.5f, 0, -120, 34, 210, 70);
            var b2 = Ui.Btn(c, "BOWL", BtnStyle.Primary, () => { CloseModal(); bat(false); }, 28, Icon.Ball);
            b2.Rt().At(0.5f, 0, 120, 34, 210, 70);
            if (AutoPilot.Active) StartCoroutine(AutoPress(() => { CloseModal(); bat(AutoPilot.TossBat); }));
        }

        public void PickBowler(MatchState m, List<Player> options, Player suggested, Action<Player> picked)
        {
            var inn = m.Current;
            var c = Modal("Bowler", 760, 470);
            var t = Ui.Label(c, "CHOOSE YOUR BOWLER", 34, Color.white, TextAnchor.MiddleLeft, Font.Display);
            t.rectTransform.At(0, 1, 24, -18, 500, 40);
            var sub = Ui.Label(c, "Over " + (inn.OverIndex + 1) + " of " + inn.MaxOvers + "  •  max " + inn.QuotaPerBowler + " over" + (inn.QuotaPerBowler > 1 ? "s" : "") + " each", 17, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
            sub.rectTransform.At(0, 1, 26, -58, 500, 22);
            var list = Ui.ScrollList(c, 6);
            list.parent.GetComponent<RectTransform>().Fill(18, 90, 18, 18);
            foreach (var p in options.OrderByDescending(p => p == suggested).ThenByDescending(p => p.BowlSkill))
            {
                var pp = p;
                var row = Ui.Row(list, 62);
                var card = inn.BowlCards.FirstOrDefault(b => b.P.Id == p.Id);
                var bg = Ui.Panel(row, p == suggested ? Pal.Accent.A(0.22f) : new Color(1, 1, 1, 0.05f), 12, "Bg", p == suggested ? Pal.Accent.A(0.6f) : (Color?)null);
                bg.rectTransform.Fill();
                var name = Ui.Label(row, p.Name.ToUpperInvariant() + (p == suggested ? "   <color=#FFC94A>SUGGESTED</color>" : ""), 22, Color.white, TextAnchor.MiddleLeft, Font.Bold);
                name.rectTransform.At(0, 0.5f, 16, 10, 460, 26);
                var meta = Ui.Label(row, (p.CanBowl ? p.Bowl.Label() : "Part-time") + "  •  Skill " + p.BowlSkill, 15, Pal.Dim, TextAnchor.MiddleLeft, Font.Semi);
                meta.rectTransform.At(0, 0.5f, 16, -14, 460, 20);
                var fig = Ui.Label(row, card == null ? "—" : card.OversText + " ov  " + card.Wkts + "-" + card.Runs, 20, Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
                fig.rectTransform.At(1, 0.5f, -16, 0, 200, 26);
                var b = row.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                row.gameObject.AddComponent<Pressable>();
                b.onClick.AddListener(() =>
                {
                    Audio.Play(Sfx.Confirm);
                    CloseModal();
                    picked(pp);
                });
            }
            if (AutoPilot.Active) StartCoroutine(AutoPress(() => { CloseModal(); picked(suggested); }));
        }

        public void InningsBreak(MatchState m, Innings done, Action cont)
        {
            var c = Modal("Break", 820, 470);
            var t = Ui.Label(c, done.SuperOver ? "SUPER OVER" : "INNINGS BREAK", 40, Pal.Gold, TextAnchor.MiddleLeft, Font.Display);
            t.rectTransform.At(0, 1, 26, -18, 600, 48);
            var crest = Crest.Add(c, done.Bat.Team, 70);
            crest.At(0, 1, 26, -80, 70, 70);
            var sc = Ui.Label(c, done.Bat.Team.Name.ToUpperInvariant() + "  " + done.Score + "  (" + done.OversText + ")", 32, Color.white, TextAnchor.MiddleLeft, Font.Display);
            sc.rectTransform.At(0, 1, 110, -88, 680, 40);
            var nxt = m.Current;
            var tg = Ui.Label(c, nxt.Target != null ? nxt.Bat.Team.Name + " need " + nxt.Target + " to win" + (nxt.SuperOver ? " in the Super Over" : " from " + nxt.MaxOvers + " overs") : "", 20, Pal.Text, TextAnchor.MiddleLeft, Font.Semi);
            tg.rectTransform.At(0, 1, 110, -126, 680, 26);
            float y = -176;
            var lbl = Ui.Label(c, "TOP BATTERS", 16, Pal.Dim, TextAnchor.MiddleLeft, Font.Bold);
            lbl.rectTransform.At(0, 1, 30, y, 300, 20);
            var lbl2 = Ui.Label(c, "TOP BOWLERS", 16, Pal.Dim, TextAnchor.MiddleLeft, Font.Bold);
            lbl2.rectTransform.At(0, 1, 420, y, 300, 20);
            int i = 0;
            foreach (var b in done.BatCards.Where(b => b.Batted).OrderByDescending(b => b.Runs).Take(3))
            {
                var l = Ui.Label(c, b.P.Name + "  <color=#FFC94A>" + b.Runs + (b.Out ? "" : "*") + "</color> (" + b.Balls + ")", 20, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                l.rectTransform.At(0, 1, 30, y - 30 - i * 30, 380, 26);
                i++;
            }
            i = 0;
            foreach (var b in done.BowlCards.OrderByDescending(b => b.Wkts).ThenBy(b => b.Runs).Take(3))
            {
                var l = Ui.Label(c, b.P.Name + "  <color=#FFC94A>" + b.Wkts + "-" + b.Runs + "</color> (" + b.OversText + ")", 20, Color.white, TextAnchor.MiddleLeft, Font.Semi);
                l.rectTransform.At(0, 1, 420, y - 30 - i * 30, 380, 26);
                i++;
            }
            var sc2 = Ui.Btn(c, "SCORECARD", BtnStyle.Secondary, () => ScorecardModal(m), 20, Icon.Chart);
            sc2.Rt().At(0, 0, 26, 24, 220, 58);
            var go = Ui.Btn(c, "CONTINUE", BtnStyle.Primary, () => cont(), 28, Icon.Play);
            go.Rt().At(1, 0, -26, 24, 260, 64);
        }

        void ScorecardModal(MatchState m)
        {
            var root = Ui.Node(_modalLayer, "ScorecardView").Fill();
            var dim = Ui.Img(root, null, new Color(0.01f, 0.02f, 0.06f, 0.75f), "Dim");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var card = Ui.Card(root, "Card", Pal.Sky.A(0.4f));
            card.rectTransform.Fill(40, 30, 40, 30);
            Scorecard.Build(card.rectTransform, m);
            var close = Ui.IconBtn(card.transform, Icon.Cross, () => Destroy(root.gameObject), 50);
            close.Rt().At(1, 1, -10, -10, 50, 50);
        }

        public void ShowPause(bool on)
        {
            if (_pause != null)
            {
                Destroy(_pause.gameObject);
                _pause = null;
            }
            if (!on) return;
            _pause = Ui.Node(_modalLayer, "Pause").Fill();
            var dim = Ui.Img(_pause, null, new Color(0.01f, 0.02f, 0.06f, 0.72f), "Dim");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var c = Ui.Card(_pause, "Card", Pal.Accent.A(0.4f));
            c.rectTransform.At(0.5f, 0.5f, 0, 0, 520, 560);
            var t = Ui.Label(c.transform, "PAUSED", 48, Color.white, TextAnchor.MiddleCenter, Font.Display);
            t.rectTransform.At(0.5f, 1, 0, -18, 400, 56);
            var col = Ui.Node(c.transform, "Col").Fill(40, 90, 40, 24);
            var lay = col.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.spacing = 10;
            lay.childForceExpandHeight = false;
            lay.childControlHeight = true;
            lay.childControlWidth = true;
            void Item(string label, BtnStyle s, Icon i, Action a)
            {
                var b = Ui.Btn(col, label, s, a, 24, i);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            }
            Item("RESUME", BtnStyle.Primary, Icon.Play, () => _md.SetPaused(false));
            Item("SCORECARD", BtnStyle.Secondary, Icon.Chart, () => ScorecardModal(_md.M));
            bool canSim = !_md.Ctx.Practice && _md.Ctx.Challenge == null;
            if (canSim)
            {
                Item("SIMULATE OVER", BtnStyle.Secondary, Icon.Refresh, () => _md.RequestSim(0));
                Item("SIMULATE INNINGS", BtnStyle.Secondary, Icon.Refresh, () => _md.RequestSim(1));
                Item("SIMULATE MATCH", BtnStyle.Secondary, Icon.Refresh, () => _md.RequestSim(2));
            }
            var s = SaveSystem.Data.Settings;
            Item("CAMERA: " + (s.Camera == 0 ? "BROADCAST" : "BEHIND BATTER"), BtnStyle.Ghost, Icon.Eye, () =>
            {
                s.Camera = 1 - s.Camera;
                SaveSystem.MarkDirty();
                ShowPause(true);
            });
            if (_md.Ctx.Practice) Item("END SESSION", BtnStyle.Danger, Icon.Cross, () => _md.EndPractice());
            else Item(_md.Ctx.Kind == MatchKind.Challenge ? "QUIT CHALLENGE" : "QUIT MATCH", BtnStyle.Danger, Icon.Cross, () =>
                Popup.Confirm(_md.Ctx.Kind == MatchKind.Tournament ? "Leave this match?" : "Quit this match?",
                    _md.Ctx.Kind == MatchKind.Tournament ? "League fixtures must be completed, so the rest of this match will be simulated." : "Progress in this match will be lost.", "QUIT", () =>
                {
                    if (_md.Ctx.Kind == MatchKind.Tournament)
                    {
                        _md.RequestSim(2);
                    }
                    else _md.Quit();
                }));
        }
    }
}
