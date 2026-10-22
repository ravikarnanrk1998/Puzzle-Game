using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FRF
{
    /// <summary>Premium dark-neon colour palette.</summary>
    public static class Pal
    {
        // Backgrounds — near-black navy, layered
        public static readonly Color Bg0    = new Color32(0x03, 0x04, 0x09, 255);
        public static readonly Color Bg1    = new Color32(0x09, 0x0C, 0x16, 255);
        // Cards — dark navy with slight blue tint
        public static readonly Color Card   = new Color32(0x0B, 0x0F, 0x1C, 0xF0);
        public static readonly Color CardHi = new Color32(0x14, 0x1B, 0x2E, 0xF8);
        public static readonly Color Stroke = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color StrokeHi = new Color(1f, 1f, 1f, 0.28f);

        // Primary neon orange — race energy
        public static readonly Color Orange     = new Color32(0xFF, 0x5C, 0x1A, 255);
        public static readonly Color OrangeDeep = new Color32(0xB5, 0x28, 0x00, 255);
        public static readonly Color OrangeGlow = new Color32(0xFF, 0x7A, 0x40, 0x55);

        // Accent colours
        public static readonly Color Yellow     = new Color32(0xFF, 0xC8, 0x00, 255);
        public static readonly Color YellowGlow = new Color32(0xFF, 0xD0, 0x30, 0x55);
        public static readonly Color Cyan       = new Color32(0x00, 0xF0, 0xFF, 255);
        public static readonly Color CyanDeep   = new Color32(0x00, 0x78, 0xB8, 255);
        public static readonly Color CyanGlow   = new Color32(0x00, 0xE8, 0xFF, 0x44);
        public static readonly Color Violet     = new Color32(0xA0, 0x60, 0xFF, 255);
        public static readonly Color VioletGlow = new Color32(0xAA, 0x70, 0xFF, 0x44);
        public static readonly Color Pink       = new Color32(0xFF, 0x30, 0x9A, 255);
        public static readonly Color PinkGlow   = new Color32(0xFF, 0x40, 0xAA, 0x44);
        public static readonly Color Green      = new Color32(0x1E, 0xFF, 0xA0, 255);
        public static readonly Color GreenGlow  = new Color32(0x30, 0xFF, 0xA8, 0x44);
        public static readonly Color Red        = new Color32(0xFF, 0x38, 0x50, 255);
        public static readonly Color RedGlow    = new Color32(0xFF, 0x40, 0x58, 0x44);

        // Text hierarchy
        public static readonly Color Text  = new Color32(0xF0, 0xF4, 0xFF, 255);
        public static readonly Color Dim   = new Color32(0x9A, 0xA4, 0xBC, 255);
        public static readonly Color Mute  = new Color32(0x56, 0x60, 0x78, 255);

        // Medal tiers
        public static readonly Color Gold   = new Color32(0xFF, 0xCC, 0x44, 255);
        public static readonly Color Silver = new Color32(0xD0, 0xDC, 0xF0, 255);
        public static readonly Color Bronze = new Color32(0xE0, 0x8A, 0x48, 255);

        public static Color Medal(int m) => m == 3 ? Gold : m == 2 ? Silver : m == 1 ? Bronze : new Color(1, 1, 1, 0.10f);

        /// <summary>Neon glow colour (low alpha, used as soft back-light behind glowing elements).</summary>
        public static Color GlowOf(Color c) => c.WithA(0.35f);
    }

    /// <summary>Code-built UGUI helpers (no prefabs needed).</summary>
    public static class Ui
    {
        public static Font Display => TextBaker.Font("Teko-Bold");
        public static Font Body    => TextBaker.Font("Rajdhani-Bold");

        static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
        static Sprite _gloss, _circle, _glow, _ring, _slant, _neonRect;

        // ------------------------------------------------------------ sprites

        /// <summary>9-sliced rounded rectangle sprite.</summary>
        public static Sprite Round(int radius)
        {
            if (Rounded.TryGetValue(radius, out var s)) return s;
            int size = radius * 2 + 4;
            var px = new Px(size, size, new Color32(255, 255, 255, 0));
            px.RoundRect(0, 0, size - 1, size - 1, radius, Color.white, Color.white);
            var tex = px.ToTexture("Round" + radius, false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            Rounded[radius] = s;
            return s;
        }

        /// <summary>Top-half gloss highlight for buttons — now stronger and more defined.</summary>
        public static Sprite Gloss
        {
            get
            {
                if (_gloss != null) return _gloss;
                var px = new Px(4, 64, new Color32(255, 255, 255, 0));
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 4; x++)
                {
                    float t = (y - 32) / 32f;
                    float a = Mathf.Clamp01(t) * 0.42f;          // top half only, brighter
                    px.P[y * 4 + x] = new Color(1, 1, 1, a);
                }
                var t2 = px.ToTexture("Gloss", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
                _gloss = Sprite.Create(t2, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
                return _gloss;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                var px = new Px(128, 128, new Color32(255, 255, 255, 0));
                px.Circle(64, 64, 62, Color.white);
                _circle = Sprite.Create(px.ToTexture("Circle", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
                return _circle;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                var px = new Px(128, 128, new Color32(255, 255, 255, 0));
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - 64) * (x + 0.5f - 64) + (y + 0.5f - 64) * (y + 0.5f - 64));
                    float a = Mathf.Clamp01(62 - d + 0.5f) * Mathf.Clamp01(d - 55 + 0.5f);
                    px.P[y * 128 + x] = new Color(1, 1, 1, a);
                }
                _ring = Sprite.Create(px.ToTexture("Ring", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
                return _ring;
            }
        }

        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                var t = Tex.GlowTex;
                _glow = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                return _glow;
            }
        }

        static Sprite _peak;

        /// <summary>Mountain silhouette for track cards.</summary>
        public static Sprite Peak
        {
            get
            {
                if (_peak != null) return _peak;
                const int w = 128, h = 64;
                var px = new Px(w, h, new Color32(255, 255, 255, 0));
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float ridge = 1 - Mathf.Abs(u - 0.5f) * 2 + (Tex.Noise(u * 14, 3) - 0.5f) * 0.08f;
                    float a = Mathf.Clamp01((ridge - v) * h * 0.8f);
                    px.P[y * w + x] = new Color(1, 1, 1, a);
                }
                return _peak = Sprite.Create(px.ToTexture("Peak", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, w, h), new Vector2(0.5f, 0));
            }
        }

        /// <summary>Little landscape for a track card: sky, two peaks with caps, ground band.</summary>
        public static void Scenery(RectTransform band, Theme th, float w, float h)
        {
            var sky = Img(band, Round(10), th.SkyTint.Lighten(0.28f), "Sky");
            sky.type = Image.Type.Sliced;
            sky.preserveAspect = false;
            sky.rectTransform.Fill();
            var far = Img(band, Peak, th.MountainFar, "Far");
            far.preserveAspect = false;
            far.rectTransform.At(0.5f, 0, -w * 0.18f, h * 0.18f, w * 0.75f, h * 0.72f);
            var cap = Img(far.transform, Peak, th.MountainCap, "Cap");
            cap.preserveAspect = false;
            cap.rectTransform.At(0.5f, 1, 0, 0, w * 0.75f * 0.3f, h * 0.72f * 0.3f);
            var near = Img(band, Peak, th.MountainNear, "Near");
            near.preserveAspect = false;
            near.rectTransform.At(0.5f, 0, w * 0.22f, h * 0.18f, w * 0.6f, h * 0.55f);
            var ground = Img(band, Round(10), th.Grass, "Ground");
            ground.type = Image.Type.Sliced;
            ground.preserveAspect = false;
            ground.rectTransform.At(0.5f, 0, 0, 0, w, h * 0.24f);
            var trail = Img(band, Round(4), th.Dirt, "Trail");
            trail.type = Image.Type.Sliced;
            trail.preserveAspect = false;
            trail.rectTransform.At(0.5f, 0, 0, h * 0.06f, w * 0.5f, h * 0.06f);
        }

        /// <summary>Parallelogram stripe — sporty slanted header accent.</summary>
        public static Sprite Slant
        {
            get
            {
                if (_slant != null) return _slant;
                const int w = 128, h = 32;
                var px = new Px(w, h, new Color32(255, 255, 255, 0));
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float off = y * 0.5f;
                    float a = Mathf.Clamp01(x - off + 0.5f) * Mathf.Clamp01(w - h * 0.5f + off - x + 0.5f);
                    px.P[y * w + x] = new Color(1, 1, 1, a);
                }
                _slant = Sprite.Create(px.ToTexture("Slant", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(20, 0, 20, 0));
                return _slant;
            }
        }

        /// <summary>Soft radial glow sprite — used behind neon elements.</summary>
        public static Sprite NeonGlow
        {
            get
            {
                if (_neonRect != null) return _neonRect;
                const int s = 64;
                var px = new Px(s, s, new Color32(255, 255, 255, 0));
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d);
                    px.P[y * s + x] = new Color(1, 1, 1, a * 0.9f);
                }
                var t = px.ToTexture("NeonGlow", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
                _neonRect = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _neonRect;
            }
        }

        // ------------------------------------------------------------ layout

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Fills the parent with insets (left, top, right, bottom).</summary>
        public static RectTransform Fill(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchors at (ax, ay) of the parent, pivot the same, offset and size in reference px.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // ------------------------------------------------------------ widgets

        public static Image Panel(Transform parent, Color color, int radius = 14, string name = "Panel", Color? border = null)
        {
            var rt = Node(parent, name);
            if (border != null)
            {
                var b = rt.gameObject.AddComponent<Image>();
                b.sprite = Round(radius);
                b.type = Image.Type.Sliced;
                b.color = border.Value;
                var inner = Node(rt, "Fill").Fill(1.5f, 1.5f, 1.5f, 1.5f);
                var img = inner.gameObject.AddComponent<Image>();
                img.sprite = Round(radius);
                img.type = Image.Type.Sliced;
                img.color = color;
                img.raycastTarget = false;
                return b;
            }
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = Round(radius);
            i.type = Image.Type.Sliced;
            i.color = color;
            return i;
        }

        /// <summary>Card panel with a neon-tinted top-edge glow — premium glass feel.</summary>
        public static Image GlassPanel(Transform parent, Color color, Color glowColor, int radius = 16, string name = "GlassPanel")
        {
            // Outer glow layer (slightly larger, blurred look)
            var glow = Node(parent, name + "Glow");
            var gi = glow.gameObject.AddComponent<Image>();
            gi.sprite = Round(radius + 2);
            gi.type = Image.Type.Sliced;
            gi.color = glowColor.WithA(0.22f);
            gi.raycastTarget = false;
            glow.Fill(-4, -4, -4, -4);

            var rt = Node(parent, name);
            // Border stroke
            var border = rt.gameObject.AddComponent<Image>();
            border.sprite = Round(radius);
            border.type = Image.Type.Sliced;
            border.color = glowColor.WithA(0.35f);
            // Fill
            var inner = Node(rt, "Fill").Fill(1f, 1f, 1f, 1f);
            var img = inner.gameObject.AddComponent<Image>();
            img.sprite = Round(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            // Top-edge shimmer
            var shimmer = Node(inner, "Shimmer");
            shimmer.anchorMin = new Vector2(0, 0.85f);
            shimmer.anchorMax = Vector2.one;
            shimmer.offsetMin = shimmer.offsetMax = Vector2.zero;
            var si = shimmer.gameObject.AddComponent<Image>();
            si.sprite = Gloss;
            si.color = Color.white.WithA(0.18f);
            si.raycastTarget = false;
            return border;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            var rt = Node(parent, name);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.preserveAspect = true;
            i.raycastTarget = false;
            return i;
        }

        /// <summary>Plain solid rectangle (no sprite).</summary>
        public static Image Block(Transform parent, Color color, string name = "Block")
        {
            var rt = Node(parent, name);
            var i = rt.gameObject.AddComponent<Image>();
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        public static Text Label(Transform parent, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool display = true, string name = "Label")
        {
            var rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = display ? Display : Body;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Wrapping paragraph text.</summary>
        public static Text Para(Transform parent, string text, float size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = Label(parent, text, size, color, anchor, false, "Para");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.1f;
            return t;
        }

        public static void Shadow(Graphic g, float d = 2.5f, float a = 0.75f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectDistance = new Vector2(d, -d);
            s.effectColor = new Color(0, 0, 0, a);
        }

        public static void Outline(Graphic g, Color c, float d = 1.5f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
        }

        /// <summary>
        /// Premium glossy gradient button: neon border, gloss top, dark shade bottom, press-scale.
        /// </summary>
        public static Button Button(Transform parent, string label, Color a, Color b, Action onClick, float fontSize = 24, string subtitle = null, int radius = 14)
        {
            // Outer glow behind the button
            var glowNode = Node(parent, "BtnGlow");
            var glowImg = glowNode.gameObject.AddComponent<Image>();
            glowImg.sprite = Round(radius + 4);
            glowImg.type = Image.Type.Sliced;
            glowImg.color = a.WithA(0.18f);
            glowImg.raycastTarget = false;

            var bg = Panel(parent, a, radius, "Button", Color.Lerp(a, Color.white, 0.45f));
            var rt = (RectTransform)bg.transform;
            var fill = rt.Find("Fill").GetComponent<Image>();

            // Richer gradient: mid-point blend of a→b
            fill.color = Color.Lerp(a, b, 0.35f);

            // Top gloss highlight
            var grad = Node(rt.Find("Fill"), "Grad").Fill();
            var gi = grad.gameObject.AddComponent<Image>();
            gi.sprite = Gloss;
            gi.color = Color.white.WithA(0.55f);
            gi.raycastTarget = false;

            // Bottom shadow shade
            var bottom = Node(rt.Find("Fill"), "Shade").Fill();
            bottom.anchorMax = new Vector2(1, 0.45f);
            var bi = bottom.gameObject.AddComponent<Image>();
            bi.color = b.WithA(0.65f);
            bi.raycastTarget = false;

            // Text — always white on dark buttons
            var fg = a.Luma() > 0.62f ? new Color(0.06f, 0.04f, 0.02f) : Color.white;
            var txt = Label(rt, label, fontSize, fg, TextAnchor.MiddleCenter, true, "Text");
            txt.rectTransform.Fill(6, subtitle != null ? 2 : 0, 6, subtitle != null ? fontSize * 0.45f : 0);
            Shadow(txt, 2, 0.8f);

            if (subtitle != null)
            {
                var st = Label(rt, subtitle, fontSize * 0.40f, fg.WithA(0.80f), TextAnchor.LowerCenter, false, "Sub");
                st.rectTransform.Fill(6, 0, 6, 4);
            }

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.35f, 0.38f, 0.45f, 0.55f);
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                Sfx.I?.Click();
                onClick?.Invoke();
            });
            bg.gameObject.AddComponent<PressScale>();
            glowNode.SetSiblingIndex(rt.GetSiblingIndex());   // keep glow behind button
            return btn;
        }

        public static Button Primary(Transform parent, string label, Action onClick, float fontSize = 26, string subtitle = null) =>
            Button(parent, label, Pal.Orange, Pal.OrangeDeep, onClick, fontSize, subtitle);

        /// <summary>Ghost button: dark glass fill, neon border, glows on hover.</summary>
        public static Button Ghost(Transform parent, string label, Color color, Action onClick, float fontSize = 20)
        {
            var bg = Panel(parent, new Color(0.04f, 0.06f, 0.10f, 0.96f), 10, "Ghost", color.WithA(0.55f));
            var txt = Label(bg.transform, label, fontSize, color, TextAnchor.MiddleCenter, true, "Text");
            txt.rectTransform.Fill(6, 0, 6, 0);
            Shadow(txt, 1.5f, 0.6f);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                Sfx.I?.Click();
                onClick?.Invoke();
            });
            bg.gameObject.AddComponent<PressScale>();
            return btn;
        }

        /// <summary>Invisible tap target over an existing node.</summary>
        public static Button Tap(RectTransform target, Action onClick)
        {
            var img = target.GetComponent<Image>();
            if (img == null)
            {
                img = target.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.001f);
            }
            img.raycastTarget = true;
            var btn = target.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                Sfx.I?.Click();
                onClick?.Invoke();
            });
            target.gameObject.AddComponent<PressScale>();
            return btn;
        }

        /// <summary>Segmented selector — pill-style with neon active segment.</summary>
        public static List<Image> Segments(Transform parent, string[] labels, int selected, Action<int> onPick, float fontSize = 19)
        {
            var list = new List<Image>();
            int n = labels.Length;
            // Track background
            var trackBg = Panel(parent, new Color(0.04f, 0.05f, 0.09f, 0.98f), 10, "SegTrack", Pal.Stroke);
            trackBg.raycastTarget = false;
            ((RectTransform)trackBg.transform).Fill();

            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var p = Panel(parent, Color.white, 9, "Seg" + i, Pal.Stroke);
                var rt = (RectTransform)p.transform;
                rt.anchorMin = new Vector2(i / (float)n, 0);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                rt.offsetMin = new Vector2(3, 2);
                rt.offsetMax = new Vector2(-3, -2);
                var t = Label(rt, labels[i], fontSize, Pal.Text, TextAnchor.MiddleCenter, true, "Text");
                t.rectTransform.Fill();
                var b = p.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() =>
                {
                    Sfx.I?.Click();
                    onPick(idx);
                    Highlight(list, idx);
                });
                p.gameObject.AddComponent<PressScale>();
                list.Add(p);
            }
            Highlight(list, selected);
            return list;
        }

        static void Highlight(List<Image> segs, int selected)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                bool on = i == selected;
                var fill = segs[i].transform.Find("Fill").GetComponent<Image>();
                fill.color = on ? Pal.Orange : new Color(0.06f, 0.08f, 0.13f, 0.0f);
                segs[i].color = on ? Color.Lerp(Pal.Orange, Color.white, 0.4f) : Pal.Stroke;
                segs[i].transform.Find("Text").GetComponent<Text>().color = on ? Color.white : Pal.Dim;
            }
        }

        /// <summary>
        /// Scrolling list (drag or wheel).
        /// </summary>
        public static RectTransform ScrollList(Transform parent, out RectTransform content, bool horizontal = false, string name = "Scroll")
        {
            var view = Node(parent, name);
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            content = Node(view, "Content");
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
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = horizontal;
            sr.vertical = !horizontal;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 35;
            sr.inertia = true;
            sr.decelerationRate = 0.15f;
            return view;
        }

        public static RectTransform Row(RectTransform content, float top, float height, string name = "Row")
        {
            var r = Node(content, name);
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -top);
            r.sizeDelta = new Vector2(0, height);
            return r;
        }

        public static void SetContentHeight(RectTransform content, float height) => content.sizeDelta = new Vector2(0, height);
        public static void SetContentWidth(RectTransform content, float width)  => content.sizeDelta = new Vector2(width, 0);

        /// <summary>Stat bar of 10 pips (bike and rider stats) — now with neon active pips.</summary>
        public static void Pips(Transform parent, string label, int value, int bonus, Color color, float x, float y, float width)
        {
            var l = Label(parent, label, 15, Pal.Dim, TextAnchor.MiddleLeft, false);
            l.rectTransform.At(0, 1, x, y, 96, 18);
            float px0 = x + 100, pw = (width - 104) / 10f;
            for (int i = 0; i < 10; i++)
            {
                Color c;
                if      (i < value)         c = color;
                else if (i < value + bonus) c = Pal.Cyan;
                else                        c = new Color(1, 1, 1, 0.08f);

                var pip = Img(parent, Round(3), c, "Pip");
                pip.type = Image.Type.Sliced;
                pip.preserveAspect = false;
                pip.rectTransform.At(0, 1, px0 + i * pw, y - 4, pw - 3, 11);

                // Neon glow behind active pips
                if (i < value)
                {
                    var glow = Img(parent, NeonGlow, c.WithA(0.35f), "PipGlow");
                    glow.preserveAspect = false;
                    glow.rectTransform.At(0, 1, px0 + i * pw - 2, y - 6, pw + 2, 15);
                }
            }
        }

        public static string Ord(int n)
        {
            int m = n % 100;
            if (m >= 11 && m <= 13) return n + "TH";
            switch (n % 10)
            {
                case 1: return n + "ST";
                case 2: return n + "ND";
                case 3: return n + "RD";
                default: return n + "TH";
            }
        }

        public static string Time(float t)
        {
            if (t <= 0) return "--:--.--";
            int m = (int)(t / 60);
            float s = t - m * 60;
            return m + ":" + s.ToString("00.00");
        }

        public static string Money(int v) => "$" + v.ToString("N0");
    }

    /// <summary>Shrinks a control slightly while pressed, with a snappy spring-back.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1;
        float _vel;

        public void OnPointerDown(PointerEventData e) => _target = 0.93f;
        public void OnPointerUp(PointerEventData e)   => _target = 1;
        public void OnPointerExit(PointerEventData e) => _target = 1;

        void Update()
        {
            // Critically-damped spring for a premium bounce feel
            float current = transform.localScale.x;
            _vel += (_target - current) * Time.unscaledDeltaTime * 900f;
            _vel *= Mathf.Exp(-26f * Time.unscaledDeltaTime);
            float s = current + _vel * Time.unscaledDeltaTime;
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
