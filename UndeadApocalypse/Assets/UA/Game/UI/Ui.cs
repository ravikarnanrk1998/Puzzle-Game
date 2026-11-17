using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>Colour palette of the interface.</summary>
    public static class Pal
    {
        public static readonly Color Bg = X.Hex(0x07090C, 0.86f);
        public static readonly Color Panel = X.Hex(0x10141A, 0.88f);
        public static readonly Color Panel2 = X.Hex(0x1A2028, 0.92f);
        public static readonly Color Line = X.Hex(0x3A4450, 0.9f);
        public static readonly Color Accent = X.Hex(0xFF5A1F);
        public static readonly Color Accent2 = X.Hex(0xFFB020);
        public static readonly Color Blood = X.Hex(0xC4161C);
        public static readonly Color Toxic = X.Hex(0x9BE31C);
        public static readonly Color Gem = X.Hex(0x3FD6FF);
        public static readonly Color Coin = X.Hex(0xFFC233);
        public static readonly Color Health = X.Hex(0x7EDC4A);
        public static readonly Color Armor = X.Hex(0x4FA3FF);
        public static readonly Color Text = X.Hex(0xEDEAE4);
        // Secondary text: brighter than before so hints and stats stay readable over the 3D backdrop.
        public static readonly Color Dim = X.Hex(0xB4BCC6);
        public static readonly Color Dark = X.Hex(0x05070A);
        public static readonly Color Good = X.Hex(0x5BD66B);
        public static readonly Color Bad = X.Hex(0xE5322D);
    }

    /// <summary>
    /// Display / Bold / Semi: Barlow Condensed (buttons, numbers, labels). Body: Barlow SemiBold (descriptions and
    /// hints). Title: Saira Stencil One, for the logo and hero titles only. All SIL Open Font License.
    /// </summary>
    public enum FontKind { Display, Bold, Semi, Body, Title }

    public static class Fonts
    {
        static readonly Font[] F = new Font[5];

        public static Font Get(FontKind k)
        {
            int i = (int)k;
            if (F[i] != null) return F[i];
            string[] names = { "Fonts/BarlowCondensed-ExtraBold", "Fonts/BarlowCondensed-Bold", "Fonts/BarlowCondensed-SemiBold", "Fonts/Barlow-SemiBold", "Fonts/SairaStencilOne-Regular" };
            F[i] = Resources.Load<Font>(names[i]);
            if (F[i] == null) F[i] = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return F[i];
        }
    }

    /// <summary>Button with press scale, hover tint, click sound and an optional disabled state.</summary>
    public sealed class UiButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Action OnClick;
        public Image Bg;
        public Text Label;
        public bool Interactable = true;
        public bool Silent;
        Vector3 _scale = Vector3.one;
        float _pressed;
        bool _down;

        public void OnPointerDown(PointerEventData e)
        {
            if (!Interactable) return;
            _down = true;
        }

        public void OnPointerUp(PointerEventData e) => _down = false;
        public void OnPointerExit(PointerEventData e) => _down = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) { Sfx.Error(); return; }
            if (!Silent) Sfx.Click();
            Click();
        }

        public void Click()
        {
            try { OnClick?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        void Update()
        {
            _pressed = Mathf.MoveTowards(_pressed, _down ? 1 : 0, Time.unscaledDeltaTime * 12);
            transform.localScale = _scale * (1 - _pressed * 0.05f);
        }

        public void SetInteractable(bool on)
        {
            Interactable = on;
            var cg = gameObject.Get<CanvasGroup>();
            cg.alpha = on ? 1 : 0.45f;
        }
    }

    /// <summary>Keeps a single-line label within its rect's width by lowering the font size (never below half).</summary>
    public sealed class FitWidth : MonoBehaviour
    {
        public int MaxSize = 60;
        Text _t;
        string _lastText;
        float _lastWidth = -1;

        void LateUpdate()
        {
            if (_t == null) _t = GetComponent<Text>();
            float w = _t.rectTransform.rect.width;
            if (_t.text == _lastText && Mathf.Abs(w - _lastWidth) < 0.5f) return;
            _lastText = _t.text;
            _lastWidth = w;
            _t.fontSize = MaxSize;
            float pw = _t.preferredWidth;
            if (w > 0 && pw > w) _t.fontSize = Mathf.Max(MaxSize / 2, Mathf.FloorToInt(MaxSize * w / pw));
        }
    }

    /// <summary>Keeps content inside the device safe area (notches, rounded corners).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last || Screen.width == 0) return;
            _last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>UI construction helpers (everything is built in code).</summary>
    public static class Ui
    {
        public static EventSystem Events;

        public static void EnsureEventSystem()
        {
            if (Events != null) return;
            var go = new GameObject("EventSystem");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Events = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else UnityEngine.Object.DontDestroyOnLoad(go);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Safe(Transform parent)
        {
            var r = Rect(parent, "Safe");
            r.Fill();
            r.gameObject.AddComponent<SafeArea>();
            return r;
        }

        public static Image Img(Transform parent, Sprite sprite, Color c, string name = "Img", bool raycast = false)
        {
            var r = Rect(parent, name);
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = c;
            i.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) i.type = Image.Type.Sliced;
            return i;
        }

        public static Image Panel(Transform parent, Color c, string name = "Panel", bool cut = true, bool raycast = true)
        {
            return Img(parent, cut ? Sprites.Cut(18) : Sprites.Rounded(14), Backing(c), name, raycast);
        }

        /// <summary>
        /// Dark translucent panel and button colours are meant to darken the 3D scene behind them; in linear colour
        /// space they let bright lights and posters show through, so their alpha is corrected with <see cref="Dim"/>.
        /// Coloured and light fills keep their alpha.
        /// </summary>
        public static Color Backing(Color c) => c.a < 1 && c.Luma() < 0.25f ? c.A(Dim(c.a)) : c;

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft, FontKind font = FontKind.Bold, string name = "Label")
        {
            var r = Rect(parent, name);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Fonts.Get(font);
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            // Soft drop shadow on all text: keeps labels readable over the 3D menu backdrop and bright panels.
            Shadow(t, Mathf.Clamp(size * 0.05f, 1.2f, 2.5f), 0.55f);
            return t;
        }

        /// <summary>
        /// Hero title in the stencil title face: always one line, scaled down only when it is wider than its rect
        /// (the stencil face is wider than Barlow Condensed, and a wrapped title would run into the text below).
        /// </summary>
        public static Text Title(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.UpperLeft, string name = "Title")
        {
            var t = Label(parent, text, size, c, align, FontKind.Title, name);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var fit = t.gameObject.AddComponent<FitWidth>();
            fit.MaxSize = size;
            return t;
        }

        /// <summary>
        /// Alpha for a black overlay that should darken what is behind it by <paramref name="a"/> as the eye sees it.
        /// The project renders in linear colour space, where UI blends in linear light: a 0.78 black overlay then only
        /// darkens the screen by about half and a 0.97 panel lets bright text ghost through. This returns the alpha
        /// that gives the intended look (1 - (1 - a)^2.2); in gamma colour space it returns <paramref name="a"/>.
        /// </summary>
        public static float Dim(float a)
        {
            a = Mathf.Clamp01(a);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 - Mathf.Pow(1 - a, 2.2f) : a;
        }

        /// <summary>
        /// Alpha for light UI (icons, outlines) over a dark scene that should show at <paramref name="a"/> of full
        /// strength as the eye sees it. In linear colour space a white icon at alpha 0.25 over black still looks about
        /// 53% bright, so a fade slider barely seems to work; this returns a^2.2 (in gamma colour space, a itself).
        /// </summary>
        public static float Fade(float a)
        {
            a = Mathf.Clamp01(a);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.Pow(a, 2.2f) : a;
        }

        /// <summary>Drop shadow (replaces an existing plain shadow rather than stacking a second one).</summary>
        public static void Shadow(Graphic g, float dist = 2, float alpha = 0.6f)
        {
            Shadow s = null;
            foreach (var x in g.GetComponents<Shadow>()) if (x.GetType() == typeof(Shadow)) { s = x; break; }
            if (s == null) s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(dist, -dist);
        }

        public static void Outline(Graphic g, Color c, float dist = 1.5f)
        {
            var s = g.gameObject.AddComponent<Outline>();
            s.effectColor = c;
            s.effectDistance = new Vector2(dist, -dist);
        }

        /// <summary>Standard button: cut-corner panel, optional icon, label.</summary>
        public static UiButton Button(Transform parent, string label, Action onClick, Color? bg = null, int fontSize = 34, Icon? icon = null, Color? fg = null, bool outline = false)
        {
            var img = Img(parent, Sprites.Cut(14), Backing(bg ?? Pal.Accent), "Btn_" + label, true);
            var b = img.gameObject.AddComponent<UiButton>();
            b.Bg = img;
            b.OnClick = onClick;
            if (outline)
            {
                var o = Img(img.transform, Sprites.CutOutline(14, 2.5f), Pal.Accent.A(0.9f), "Outline");
                o.rectTransform.Fill();
            }
            float iconW = 0;
            if (icon != null)
            {
                var ic = Img(img.transform, Sprites.Of(icon.Value), fg ?? Color.white, "Icon");
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0, 0.5f);
                ic.rectTransform.sizeDelta = Vector2.one * fontSize * 1.2f;
                ic.rectTransform.anchoredPosition = new Vector2(fontSize * 0.9f, 0);
                iconW = fontSize * 1.5f;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(img.transform, label, fontSize, fg ?? Color.white, icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontKind.Display);
                t.rectTransform.Fill(icon != null ? iconW + 8 : 10, 0, 10, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                b.Label = t;
            }
            return b;
        }

        /// <summary>Square icon button (HUD and toolbars).</summary>
        public static UiButton IconButton(Transform parent, Icon icon, Action onClick, float size, Color? bg = null, Color? fg = null, bool circle = false)
        {
            var img = Img(parent, circle ? Sprites.Circle : Sprites.Cut(10), Backing(bg ?? Pal.Panel2), "IconBtn_" + icon, true);
            img.rectTransform.sizeDelta = Vector2.one * size;
            var b = img.gameObject.AddComponent<UiButton>();
            b.Bg = img;
            b.OnClick = onClick;
            var ic = Img(img.transform, Sprites.Of(icon), fg ?? Color.white, "Icon");
            ic.rectTransform.Fill(size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            return b;
        }

        /// <summary>Horizontal bar with a fill image (health, XP, stats).</summary>
        public static Image Bar(Transform parent, Color back, Color fill, out RectTransform root, string name = "Bar")
        {
            var bg = Img(parent, Sprites.Rounded(6), back, name);
            root = bg.rectTransform;
            var f = Img(bg.transform, Sprites.Rounded(6), fill, "Fill");
            f.type = Image.Type.Sliced;
            f.rectTransform.anchorMin = new Vector2(0, 0);
            f.rectTransform.anchorMax = new Vector2(1, 1);
            f.rectTransform.offsetMin = new Vector2(2, 2);
            f.rectTransform.offsetMax = new Vector2(-2, -2);
            f.rectTransform.pivot = new Vector2(0, 0.5f);
            return f;
        }

        public static void SetFill(Image fill, float k)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(k), 1);
        }

        /// <summary>Currency chip: icon + value.</summary>
        public static Text Chip(Transform parent, Icon icon, Color tint, string value, int size = 30)
        {
            var bg = Img(parent, Sprites.Rounded(14), Pal.Panel2, "Chip");
            var ic = Img(bg.transform, Sprites.Of(icon), tint, "Icon");
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0, 0.5f);
            ic.rectTransform.sizeDelta = Vector2.one * size * 1.1f;
            ic.rectTransform.anchoredPosition = new Vector2(size * 0.8f, 0);
            var t = Label(bg.transform, value, size, Pal.Text, TextAnchor.MiddleLeft, FontKind.Display);
            t.rectTransform.Fill(size * 1.6f, 0, 8, 0);
            return t;
        }

        /// <summary>Scroll view with a vertical content container; returns the content rect.</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll, bool horizontal = false)
        {
            var view = Rect(parent, "Scroll");
            var img = view.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30;
            scroll.inertia = true;
            var content = Rect(view, "Content");
            if (horizontal)
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
            }
            else
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
            }
            content.offsetMin = content.offsetMax = Vector2.zero;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        public static Slider Slider(Transform parent, float value, float min, float max, Action<float> onChange)
        {
            var root = Rect(parent, "Slider");
            // Touch area: the slider's whole rect (the bar is 12 px and the handle 38 px, too small for a thumb).
            var hit = Img(root, Sprites.White, new Color(0, 0, 0, 0.001f), "Hit", true);
            hit.rectTransform.Fill();
            var bg = Img(root, Sprites.Rounded(6), Pal.Line, "Bg");
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 12);
            var fillArea = Rect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-20, 12);
            var fill = Img(fillArea, Sprites.Rounded(6), Pal.Accent, "Fill");
            fill.rectTransform.sizeDelta = new Vector2(10, 0);
            var handleArea = Rect(root, "HandleArea");
            handleArea.Fill(10, 0, 10, 0);
            var handle = Img(handleArea, Sprites.Circle, Color.white, "Handle", true);
            handle.rectTransform.sizeDelta = new Vector2(38, 38);
            var s = root.gameObject.AddComponent<TouchSlider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        /// <summary>Segmented selector (e.g. LOW / MEDIUM / ULTRA).</summary>
        public static List<UiButton> Segments(Transform parent, string[] options, int selected, Action<int> onPick, float width, float height, int fontSize = 26)
        {
            var list = new List<UiButton>();
            float w = width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = Button(parent, options[i], null, i == selected ? Pal.Accent : Pal.Panel2, fontSize);
                b.Bg.rectTransform.At(0, 0.5f, w * i + w / 2, 0, w - 6, height);
                list.Add(b);
                b.OnClick = () =>
                {
                    for (int k = 0; k < list.Count; k++) list[k].Bg.color = k == idx ? Pal.Accent : Pal.Panel2;
                    onPick(idx);
                };
            }
            return list;
        }

        public static UiButton Toggle(Transform parent, bool value, Action<bool> onChange, float width = 120, float height = 54)
        {
            bool v = value;
            var b = Button(parent, v ? "ON" : "OFF", null, v ? Pal.Good.Mul(0.75f) : Pal.Panel2, 26);
            b.Bg.rectTransform.sizeDelta = new Vector2(width, height);
            b.OnClick = () =>
            {
                v = !v;
                b.Label.text = v ? "ON" : "OFF";
                b.Bg.color = v ? Pal.Good.Mul(0.75f) : Pal.Panel2;
                onChange(v);
            };
            return b;
        }

        // ------------------------------------------------------------------ rect helpers

        /// <summary>Anchors to a point (ax, ay) of the parent with centred pivot, position and size.</summary>
        public static RectTransform At(this RectTransform r, float ax, float ay, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        public static RectTransform Fill(this RectTransform r, float l = 0, float t = 0, float rr = 0, float b = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(-rr, -t);
            return r;
        }

        /// <summary>Top-anchored row inside a vertical list (y from the top, full width minus margins).</summary>
        public static RectTransform Row(this RectTransform r, float top, float height, float marginL = 0, float marginR = 0)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(marginL, -top - height);
            r.offsetMax = new Vector2(-marginR, -top);
            return r;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
