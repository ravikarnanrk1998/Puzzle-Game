using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// Mobile FPS controls: floating joystick (left), look pad (right), fire buttons (right and an
    /// optional left), aim, reload, jump, crouch, grenade and swap. Every button can be moved and
    /// resized in the layout editor; positions are saved in Settings.Layout.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public sealed class Def
        {
            public string Id;
            public Icon Icon;
            public float X, Y, Size;   // anchor in the safe area (0..1) and size in reference pixels
            public TouchBtn? Press;
            public bool Fire;
            /// <summary>Press-and-hold button (grenade: aim while held, throw on release).</summary>
            public bool Hold;
        }

        public static readonly Def[] Defaults =
        {
            new Def { Id = "fire", Icon = Icon.Fire, X = 0.86f, Y = 0.3f, Size = 210, Fire = true },
            // Left fire sits under the minimap (top-left).
            new Def { Id = "fireL", Icon = Icon.Fire, X = 0.1f, Y = 0.58f, Size = 140, Fire = true },
            new Def { Id = "aim", Icon = Icon.Aim, X = 0.955f, Y = 0.53f, Size = 130, Press = TouchBtn.Aim },
            new Def { Id = "reload", Icon = Icon.Reload, X = 0.745f, Y = 0.14f, Size = 116, Press = TouchBtn.Reload },
            new Def { Id = "jump", Icon = Icon.Jump, X = 0.955f, Y = 0.15f, Size = 120, Press = TouchBtn.Jump },
            new Def { Id = "crouch", Icon = Icon.Crouch, X = 0.845f, Y = 0.56f, Size = 110, Press = TouchBtn.Crouch },
            new Def { Id = "grenade", Icon = Icon.Grenade, X = 0.735f, Y = 0.38f, Size = 110, Hold = true },
            new Def { Id = "swap", Icon = Icon.Swap, X = 0.955f, Y = 0.76f, Size = 104, Press = TouchBtn.Swap },
        };

        readonly Dictionary<string, RectTransform> _buttons = new Dictionary<string, RectTransform>();
        RectTransform _area, _joyBase, _joyKnob;
        Image _joyBaseImg, _joyKnobImg;
        int _joyPointer = -99, _lookPointer = -99, _grenadePointer = -99;
        Vector2 _joyOrigin;
        readonly HashSet<int> _firePointers = new HashSet<int>();
        public bool Editing;
        string _selected;
        System.Action<string> _onSelect;

        public static TouchControls Create(RectTransform parent)
        {
            var area = Ui.Rect(parent, "Touch");
            area.Fill();
            var tc = area.gameObject.AddComponent<TouchControls>();
            tc._area = area;
            tc.Build();
            return tc;
        }

        public static Def Find(string id)
        {
            foreach (var d in Defaults) if (d.Id == id) return d;
            return null;
        }

        /// <summary>Layout editor "reset": build from the defaults (saved only when the player presses SAVE).</summary>
        bool _useDefaults;

        (float x, float y, float size) Placement(Def d)
        {
            if (!_useDefaults)
                foreach (var l in SaveSystem.Data.Settings.Layout)
                    if (l.Id == d.Id) return (l.X, l.Y, d.Size * l.Scale);
            return (d.X, d.Y, d.Size);
        }

        // Joystick resting place (normalised in the touch area) and size; layout id "joy".
        const string JoyId = "joy";
        const float JoySize = 250, KnobSize = 112, JoyTravel = 110;
        static readonly Vector2 JoyDefault = new Vector2(0.135f, 0.24f);
        Vector2 _joyHome = JoyDefault;
        float _joyScale = 1;

        void LoadJoyLayout()
        {
            _joyHome = JoyDefault;
            _joyScale = 1;
            if (_useDefaults) return;
            foreach (var l in SaveSystem.Data.Settings.Layout)
                if (l.Id == JoyId) { _joyHome = new Vector2(l.X, l.Y); _joyScale = Mathf.Clamp(l.Scale, 0.6f, 1.6f); }
        }

        /// <summary>Puts the joystick at its resting place and size (idle; while held it follows the thumb).</summary>
        void PlaceJoyIdle()
        {
            _joyBase.anchorMin = _joyBase.anchorMax = _joyHome;
            _joyBase.anchoredPosition = Vector2.zero;
            _joyBase.sizeDelta = Vector2.one * JoySize * _joyScale;
            _joyKnob.sizeDelta = Vector2.one * KnobSize * _joyScale;
            _joyKnob.anchoredPosition = Vector2.zero;
        }

        void Build()
        {
            // Look pad: right 58% of the screen.
            var look = Ui.Img(_area, Sprites.White, new Color(0, 0, 0, 0.001f), "LookPad", true);
            look.rectTransform.anchorMin = new Vector2(0.42f, 0);
            look.rectTransform.anchorMax = new Vector2(1, 1);
            look.rectTransform.offsetMin = look.rectTransform.offsetMax = Vector2.zero;
            var lp = look.gameObject.AddComponent<PadHandler>();
            lp.Down = (e) => { if (_lookPointer == -99) _lookPointer = e.pointerId; };
            lp.Drag = (e) => { if (e.pointerId == _lookPointer) Look(e.delta); };
            lp.Up = (e) => { if (e.pointerId == _lookPointer) _lookPointer = -99; };
            // Joystick zone: left 42%.
            var joy = Ui.Img(_area, Sprites.White, new Color(0, 0, 0, 0.001f), "JoyZone", true);
            joy.rectTransform.anchorMin = new Vector2(0, 0);
            joy.rectTransform.anchorMax = new Vector2(0.42f, 0.85f);
            joy.rectTransform.offsetMin = joy.rectTransform.offsetMax = Vector2.zero;
            // Joystick: dark glass base with a ring and direction chevrons, and a lit knob. The base takes touches too:
            // in play it starts the joystick like the zone around it; in the layout editor it is dragged and resized.
            _joyBaseImg = Ui.Img(_area, Sprites.ButtonFace, JoyIdle, "JoyBase", true);
            _joyBase = _joyBaseImg.rectTransform;
            var bh = _joyBaseImg.gameObject.AddComponent<PadHandler>();
            bh.Down = e => { if (Editing) { Select(JoyId); _grab = Norm(e.position) - _joyBase.anchorMin; } else JoyDown(e); };
            bh.Drag = e => { if (Editing) MoveTo(_joyBase, Norm(e.position) - _grab); else JoyDrag(e); };
            bh.Up = e => { if (Editing) _joyHome = _joyBase.anchorMin; else JoyUp(e); };
            var ringArt = Ui.Img(_joyBase, Sprites.JoyBase, new Color(1, 1, 1, 0.5f), "Ring");
            ringArt.rectTransform.Fill();
            _joyKnobImg = Ui.Img(_joyBase, Sprites.ButtonFace, new Color(0.92f, 0.94f, 0.96f, 0.62f), "Knob");
            _joyKnob = _joyKnobImg.rectTransform;
            _joyKnob.At(0.5f, 0.5f, 0, 0, 112, 112);
            var knobSheen = Ui.Img(_joyKnob, Sprites.ButtonSheen, Color.white, "Sheen");
            knobSheen.rectTransform.Fill();
            var knobRing = Ui.Img(_joyKnob, Sprites.Ring(4), new Color(1, 1, 1, 0.6f), "Ring");
            knobRing.rectTransform.Fill();
            var jh = joy.gameObject.AddComponent<PadHandler>();
            jh.Down = JoyDown;
            jh.Drag = JoyDrag;
            jh.Up = JoyUp;
            LoadJoyLayout();
            PlaceJoyIdle();
            // Buttons.
            foreach (var d in Defaults)
            {
                if (d.Id == "fireL" && !SaveSystem.Data.Settings.LeftFire) continue;
                MakeButton(d);
            }
            ApplyAlpha();
        }

        // Button look: domed face (tinted), glass sheen, thin rim, icon with a drop shadow. Fire buttons are
        // orange with a soft glow behind them; the others are dark glass.
        static readonly Color FaceDark = new Color(0.07f, 0.09f, 0.11f, 0.66f);
        static readonly Color FaceFire = new Color(1f, 0.4f, 0.12f, 0.9f);

        sealed class ButtonView
        {
            public Image Face, Ring, Icon, Progress;
            public Text Badge;
            public Color Base;
            public bool Pressed;
        }

        readonly Dictionary<string, ButtonView> _views = new Dictionary<string, ButtonView>();

        void MakeButton(Def d)
        {
            var (x, y, size) = Placement(d);
            bool big = d.Fire;
            var img = Ui.Img(_area, Sprites.ButtonFace, big ? FaceFire : FaceDark, "B_" + d.Id, true);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.sizeDelta = Vector2.one * size;
            rt.anchoredPosition = Vector2.zero;
            var v = new ButtonView { Face = img, Base = img.color };
            if (big)
            {
                var glow = Ui.Img(rt, Sprites.OuterGlow, Pal.Accent.A(0.55f), "Glow");
                glow.rectTransform.Fill(-size * 0.22f, -size * 0.22f, -size * 0.22f, -size * 0.22f);
            }
            var sheen = Ui.Img(rt, Sprites.ButtonSheen, new Color(1, 1, 1, big ? 0.45f : 0.3f), "Sheen");
            sheen.rectTransform.Fill();
            v.Ring = Ui.Img(rt, Sprites.Ring(big ? 4 : 3.5f), big ? new Color(1, 0.85f, 0.7f, 0.75f) : new Color(1, 1, 1, 0.42f), "Ring");
            v.Ring.rectTransform.Fill();
            float pad = size * (big ? 0.19f : 0.21f);
            v.Icon = Ui.Img(rt, Sprites.Control(d.Icon), Color.white, "Icon");
            v.Icon.rectTransform.Fill(pad, pad, pad, pad);
            Ui.Shadow(v.Icon, Mathf.Max(1.5f, size * 0.014f), 0.55f);
            if (d.Id == "reload")
            {
                // Reload progress around the rim (shown while reloading).
                v.Progress = Ui.Img(rt, Sprites.Ring(7), Pal.Accent2, "Progress");
                v.Progress.type = Image.Type.Filled;
                v.Progress.fillMethod = Image.FillMethod.Radial360;
                v.Progress.fillOrigin = (int)Image.Origin360.Top;
                v.Progress.fillClockwise = true;
                v.Progress.rectTransform.Fill();
                v.Progress.enabled = false;
            }
            if (d.Id == "grenade")
            {
                // Grenades left.
                var bb = Ui.Img(rt, Sprites.Circle, Pal.Accent, "Badge");
                bb.rectTransform.At(1, 1, -size * 0.12f, -size * 0.12f, size * 0.36f, size * 0.36f);
                v.Badge = Ui.Label(bb.transform, "", Mathf.RoundToInt(size * 0.24f), Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                v.Badge.rectTransform.Fill();
            }
            _views[d.Id] = v;
            var h = img.gameObject.AddComponent<PadHandler>();
            string id = d.Id;
            h.Down = e =>
            {
                if (Editing) { Select(id); _grab = Norm(e.position) - rt.anchorMin; return; }
                if (d.Fire) { _firePointers.Add(e.pointerId); Controls.SetFire(true); if (_lookPointer == -99 && d.Id == "fire") _lookPointer = e.pointerId; }
                else if (d.Hold)
                {
                    // Grenade: held = aim (red arc), dragging the thumb turns the view, release = throw.
                    _grenadePointer = e.pointerId;
                    Controls.SetGrenade(true);
                    if (_lookPointer == -99) _lookPointer = e.pointerId;
                }
                else if (d.Press.HasValue) Controls.Press(d.Press.Value);
                img.transform.localScale = Vector3.one * 0.92f;
                v.Pressed = true;
            };
            h.Drag = e =>
            {
                if (Editing)
                {
                    MoveTo(rt, Norm(e.position) - _grab);
                    return;
                }
                // Dragging off the fire (or held grenade) button keeps aiming (standard mobile FPS behaviour).
                if ((d.Fire || d.Hold) && e.pointerId == _lookPointer) Look(e.delta);
            };
            h.Up = e =>
            {
                img.transform.localScale = Vector3.one;
                v.Pressed = false;
                if (Editing) return;
                if (d.Fire)
                {
                    _firePointers.Remove(e.pointerId);
                    if (_firePointers.Count == 0) Controls.SetFire(false);
                    if (e.pointerId == _lookPointer) _lookPointer = -99;
                }
                else if (d.Hold && e.pointerId == _grenadePointer)
                {
                    _grenadePointer = -99;
                    Controls.SetGrenade(false);   // release: the grenade is thrown along the arc
                    if (e.pointerId == _lookPointer) _lookPointer = -99;
                }
            };
            _buttons[d.Id] = rt;
        }

        static readonly Color JoyIdle = new Color(0.05f, 0.06f, 0.08f, 0.32f);
        static readonly Color JoyActive = new Color(0.05f, 0.06f, 0.08f, 0.5f);

        /// <summary>
        /// Button state from the player (display only): aim lights up while aiming, reload shows its progress
        /// and pulses when the magazine runs low, grenade shows how many are left.
        /// </summary>
        void Update()
        {
            if (Editing) return;
            var p = PlayerRig.Instance;
            var g = p != null ? p.Guns : null;
            foreach (var kv in _views)
            {
                var v = kv.Value;
                var c = v.Base;
                if (g != null)
                {
                    if (kv.Key == "aim" && g.AimAmount > 0.5f) c = FaceFire;
                    else if (kv.Key == "reload")
                    {
                        bool rel = g.Reloading;
                        v.Progress.enabled = rel;
                        if (rel) v.Progress.fillAmount = g.ReloadProgress;
                        var w = g.W;
                        bool low = !rel && !w.Infinite && w.Reserve > 0 && w.Mag <= Mathf.Max(1, w.St.Mag / 5);
                        if (low) c = Color.Lerp(c, FaceFire, Mathf.PingPong(Time.unscaledTime * 2.5f, 1));
                    }
                    else if (kv.Key == "grenade")
                    {
                        int n = g.Grenades;
                        string s = n.ToString();
                        if (v.Badge.text != s) v.Badge.text = s;
                        v.Badge.transform.parent.GetComponent<Image>().color = n > 0 ? Pal.Accent : Pal.Bad;
                        v.Icon.color = n > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
                        if (g.GrenadeAiming) c = FaceFire;
                    }
                }
                if (v.Pressed) c = new Color(Mathf.Lerp(c.r, 1, 0.25f), Mathf.Lerp(c.g, 1, 0.25f), Mathf.Lerp(c.b, 1, 0.25f), Mathf.Min(1, c.a + 0.1f));
                if (v.Face.color != c) v.Face.color = c;
            }
        }

        void Look(Vector2 deltaPx)
        {
            // A drag across the full screen height turns about 180 degrees at sensitivity 1.
            float k = 180f / Mathf.Max(400, Screen.height);
            Controls.AddLook(new Vector2(deltaPx.x * k, -deltaPx.y * k));
        }

        void JoyDown(PointerEventData e)
        {
            if (_joyPointer != -99 || Editing) return;
            _joyPointer = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, null, out var local);
            var r = _area.rect;
            _joyOrigin = local - r.min;
            // Floating: the stick comes to the thumb.
            _joyBase.anchorMin = _joyBase.anchorMax = Vector2.zero;
            _joyBase.anchoredPosition = _joyOrigin;
            _joyBaseImg.color = JoyActive;
            JoyDrag(e);
        }

        void JoyDrag(PointerEventData e)
        {
            if (e.pointerId != _joyPointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, null, out var local);
            var d = local - _area.rect.min - _joyOrigin;
            float max = JoyTravel * _joyScale;
            var v = Vector2.ClampMagnitude(d, max);
            _joyKnob.anchoredPosition = v;
            var move = v / max;
            if (move.magnitude < 0.12f) move = Vector2.zero;
            Controls.SetMove(move);
        }

        void JoyUp(PointerEventData e)
        {
            if (e.pointerId != _joyPointer) return;
            _joyPointer = -99;
            _joyBaseImg.color = JoyIdle;
            PlaceJoyIdle();
            Controls.SetMove(Vector2.zero);
        }

        public void ApplyAlpha() => PreviewAlpha(SaveSystem.Data.Settings.ButtonAlpha);

        /// <summary>
        /// Sets how visible the buttons and joystick are (the layout editor previews before saving). The setting is
        /// what the player sees, so it goes through <see cref="Ui.Fade"/>: in linear colour a raw 0.25 alpha still
        /// looks half-strength on the light icons.
        /// </summary>
        public void PreviewAlpha(float a)
        {
            float g = Ui.Fade(a);
            foreach (var kv in _buttons) kv.Value.gameObject.Get<CanvasGroup>().alpha = g;
            _joyBase.gameObject.Get<CanvasGroup>().alpha = g;
        }

        void OnDisable()
        {
            Controls.ResetTouch();   // also cancels a held grenade (no throw)
            _joyPointer = _lookPointer = _grenadePointer = -99;
            _firePointers.Clear();
            if (_joyBase != null && _joyKnob != null && !Editing) { _joyBaseImg.color = JoyIdle; PlaceJoyIdle(); }
        }

        public void SetVisible(bool on)
        {
            gameObject.SetActive(on);
        }

        // --------------------------------------------------------------- layout editor

        Vector2 _grab;
        readonly List<GameObject> _editLabels = new List<GameObject>();
        static readonly Color EditFace = new Color(1f, 0.69f, 0.13f, 0.45f), EditSelected = new Color(0.36f, 0.84f, 0.42f, 0.75f);

        /// <summary>Display name of a control in the layout editor.</summary>
        public static string Label(string id)
        {
            switch (id)
            {
                case "fire": return "FIRE";
                case "fireL": return "FIRE (LEFT)";
                case "aim": return "AIM";
                case "reload": return "RELOAD";
                case "jump": return "JUMP";
                case "crouch": return "CROUCH";
                case "grenade": return "GRENADE";
                case "swap": return "SWAP WEAPON";
                case JoyId: return "MOVE";
                default: return id.ToUpperInvariant();
            }
        }

        /// <summary>Screen point to normalised touch-area coordinates.</summary>
        Vector2 Norm(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var local);
            var r = _area.rect;
            return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
        }

        /// <summary>Moves a control (anchored at its centre), kept fully on screen.</summary>
        void MoveTo(RectTransform rt, Vector2 n)
        {
            var r = _area.rect;
            float hw = Mathf.Min(0.5f, rt.sizeDelta.x * 0.5f / Mathf.Max(1, r.width));
            float hh = Mathf.Min(0.5f, rt.sizeDelta.y * 0.5f / Mathf.Max(1, r.height));
            n.x = Mathf.Clamp(n.x, hw, 1 - hw);
            n.y = Mathf.Clamp(n.y, hh, 1 - hh);
            rt.anchorMin = rt.anchorMax = n;
            rt.anchoredPosition = Vector2.zero;
            if (rt == _joyBase) _joyHome = n;
        }

        public void BeginEdit(System.Action<string> onSelect)
        {
            Editing = true;
            _onSelect = onSelect;
            foreach (var l in _editLabels) if (l != null) Destroy(l);
            _editLabels.Clear();
            foreach (var kv in _buttons) AddEditLabel(kv.Value, kv.Key);
            AddEditLabel(_joyBase, JoyId);
            Paint();
        }

        void AddEditLabel(RectTransform rt, string id)
        {
            var t = Ui.Label(rt, Label(id), 24, Color.white, TextAnchor.UpperCenter, FontKind.Display, "EditLabel");
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.anchoredPosition = new Vector2(0, -6);
            t.rectTransform.sizeDelta = new Vector2(260, 30);
            Ui.Shadow(t, 2, 0.9f);
            _editLabels.Add(t.gameObject);
        }

        /// <summary>Edit-mode colours: every control amber, the selected one green.</summary>
        void Paint()
        {
            foreach (var kv in _buttons) kv.Value.GetComponent<Image>().color = kv.Key == _selected ? EditSelected : EditFace;
            _joyBaseImg.color = _selected == JoyId ? EditSelected : EditFace;
        }

        void Select(string id)
        {
            _selected = id;
            Paint();
            _onSelect?.Invoke(id);
        }

        public string Selected => _selected;

        public void ScaleSelected(float scale)
        {
            if (_selected == JoyId)
            {
                _joyScale = scale;
                _joyBase.sizeDelta = Vector2.one * JoySize * scale;
                _joyKnob.sizeDelta = Vector2.one * KnobSize * scale;
                MoveTo(_joyBase, _joyBase.anchorMin);
                return;
            }
            if (_selected == null || !_buttons.TryGetValue(_selected, out var rt)) return;
            rt.sizeDelta = Vector2.one * Find(_selected).Size * scale;
            MoveTo(rt, rt.anchorMin);
        }

        public float SelectedScale => _selected == JoyId ? _joyScale : _selected != null && _buttons.TryGetValue(_selected, out var rt) ? rt.sizeDelta.x / Find(_selected).Size : 1;

        public void EndEdit(bool save)
        {
            Editing = false;
            foreach (var l in _editLabels) if (l != null) Destroy(l);
            _editLabels.Clear();
            _selected = null;
            if (save)
            {
                var list = SaveSystem.Data.Settings.Layout;
                list.Clear();
                foreach (var kv in _buttons)
                {
                    var d = Find(kv.Key);
                    list.Add(new ButtonLayout { Id = kv.Key, X = kv.Value.anchorMin.x, Y = kv.Value.anchorMin.y, Scale = kv.Value.sizeDelta.x / d.Size });
                }
                list.Add(new ButtonLayout { Id = JoyId, X = _joyHome.x, Y = _joyHome.y, Scale = _joyScale });
                SaveSystem.MarkDirty();
                SaveSystem.Save();
            }
            _useDefaults = false;
            Rebuild();
        }

        /// <summary>Back to the default layout in the editor (saved only with SAVE; CANCEL keeps the old one).</summary>
        public void ResetLayout()
        {
            _useDefaults = true;
            _selected = null;
            Rebuild();
            if (Editing) BeginEdit(_onSelect);
        }

        /// <summary>Re-reads the saved layout (the in-game controls after the layout editor saved).</summary>
        public void ReloadLayout()
        {
            _useDefaults = false;
            Rebuild();
        }

        void Rebuild()
        {
            foreach (var kv in _buttons) Destroy(kv.Value.gameObject);
            _buttons.Clear();
            _views.Clear();
            foreach (var d in Defaults)
            {
                if (d.Id == "fireL" && !SaveSystem.Data.Settings.LeftFire) continue;
                MakeButton(d);
            }
            LoadJoyLayout();
            _joyBaseImg.color = JoyIdle;
            PlaceJoyIdle();
            ApplyAlpha();
        }
    }

    /// <summary>Forwards pointer events (multi-touch aware) to delegates.</summary>
    public sealed class PadHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IBeginDragHandler
    {
        public System.Action<PointerEventData> Down, Drag, Up;
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(e);
    }
}
