using System;
using System.Collections.Generic;
using UA.Core;
using UA.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UA.EditorTools
{
    /// <summary>
    /// Launcher icon and Play feature graphic rendered from the imported zombie models (Microsoft Rocketbox, MIT):
    /// orange-red key light and teal rim light, glowing eyes on the model's eye bones, claw slashes behind the head,
    /// all over a dark red-lit backdrop. The model is cut out with a black / white two-pass matte (no reliance on
    /// render-target alpha). Returns null when the prefabs are missing, so the procedural <see cref="IconArt"/> is used.
    /// </summary>
    public static class IconArt3D
    {
        const string Prefabs = "Assets/UA/Resources/UAArt/";
        const string FontDir = "Assets/UA/Resources/Fonts/";

        // ------------------------------------------------------------------ icon

        public static Texture2D Icon(int n)
        {
            // The hooded Hunter: the hood gives a strong silhouette at launcher size.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Zombies/Hunter.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Zombies/Walker.prefab");
            if (prefab == null) return null;
            var made = new List<Object>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var body = Spawn(prefab, Vector3.zero, 0, made);
                var head = body.Bones[(int)ZB.Head];
                // Head lowered and turned slightly: a stare, not a passport photo.
                head.rotation = Quaternion.Euler(9, -12, 6) * head.rotation;
                var eyes = (body.EyeL.position + body.EyeR.position) * 0.5f;
                Lighting(made, true);
                var cam = MakeCamera(made, 26);
                // Head and shoulders, eyes a little above centre.
                cam.transform.position = eyes + new Vector3(0.08f, -0.03f, 0.95f);
                cam.transform.LookAt(eyes + new Vector3(0, -0.075f, 0));
                var layer = Matte(cam, n, n);
                var eyePx = new[] { (Vector2)cam.WorldToViewportPoint(body.EyeL.position), (Vector2)cam.WorldToViewportPoint(body.EyeR.position) };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                        var bg = Backdrop(u, v, new Vector2(0.5f, 0.38f));
                        bg = Slashes(bg, u, v, 0.62f, 0.6f, 1f);
                        px[y * n + x] = Over(bg, layer[y * n + x]);
                    }
                Glow(px, n, n, eyePx, n * 0.02f, 1.6f);
                Finish(px, n, n, new Vector2(0.5f, 0.45f), 1.45f);
                return ToTexture(px, n, n);
            }
            finally
            {
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                ShaderUtil.allowAsyncCompilation = asyncShaders;
            }
        }

        // ------------------------------------------------------------------ feature graphic

        public static Texture2D Feature(int w, int h)
        {
            var walker = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Zombies/Walker.prefab");
            if (walker == null) return null;
            var made = new List<Object>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                // A small horde on the right: lead walker in front, two more behind it.
                // The camera looks down -Z, so -X is screen right: the horde stands right of the title.
                var lineup = new (string key, Vector3 pos, float yaw)[]
                {
                    ("Zombies/Walker", new Vector3(-0.6f, 0, 0.6f), 12),
                    ("Zombies/Hunter", new Vector3(-1.5f, 0, -0.7f), 24),
                    ("Zombies/Walker_2", new Vector3(-0.15f, 0, -1.6f), -8),
                    ("Zombies/Explosive", new Vector3(-2.4f, 0, -2.0f), 30),
                };
                var eyes = new List<Transform>();
                foreach (var (key, pos, yaw) in lineup)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + key + ".prefab");
                    if (prefab == null) continue;
                    var b = Spawn(prefab, pos, yaw, made);
                    Reach(b);
                    eyes.Add(b.EyeL);
                    eyes.Add(b.EyeR);
                }
                Lighting(made, false);
                var cam = MakeCamera(made, 30);
                cam.transform.position = new Vector3(0.5f, 1.45f, 4.6f);
                cam.transform.LookAt(new Vector3(0.4f, 1.2f, 0));
                var layer = Matte(cam, w, h);
                var eyePx = new List<Vector2>();
                foreach (var e in eyes) if (e != null) eyePx.Add(cam.WorldToViewportPoint(e.position));
                var px = new Color[w * h];
                float aspect = w / (float)h;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                        var bg = Backdrop(u * aspect / 2.2f + 0.05f, v, new Vector2(0.85f, 0.3f));
                        px[y * w + x] = Over(bg, layer[y * w + x]);
                    }
                Glow(px, w, h, eyePx.ToArray(), h * 0.009f, 1.1f);
                // Darken the left third behind the title.
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w;
                        px[y * w + x] *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0.22f, 0.55f, u));
                    }
                Finish(px, w, h, new Vector2(0.62f, 0.45f), 1.1f);
                var tex = ToTexture(px, w, h);
                return Title(tex, w, h);
            }
            finally
            {
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                ShaderUtil.allowAsyncCompilation = asyncShaders;
            }
        }

        // ------------------------------------------------------------------ weapon thumbnails

        /// <summary>
        /// Factory-finish weapon seen from its right side (muzzle pointing right), a little from above and ahead,
        /// on a transparent background: the store, armory and loadout card art. Null if the model cannot be built.
        /// </summary>
        public static Texture2D WeaponThumb(WeaponId id, int w, int h)
        {
            var made = new List<Object>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            try
            {
                var holder = new GameObject("ThumbGun");
                made.Add(holder);
                var gm = GunModels.Build(id, null, holder.transform, 0, false);
                if (gm == null) return null;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.22f, 0.23f, 0.26f);
                // Key: warm white from above the visible side. Fill: cool, low. Edge: orange from behind and above,
                // which picks out the top line of the weapon against dark cards.
                AddLight(made, "Key", new Color(1f, 0.96f, 0.9f), 2.5f, new Vector3(-0.55f, -0.7f, -0.35f));
                AddLight(made, "Fill", new Color(0.62f, 0.72f, 0.88f), 0.9f, new Vector3(-0.6f, 0.35f, 0.5f));
                AddLight(made, "Edge", new Color(1f, 0.58f, 0.3f), 2.4f, new Vector3(0.55f, -0.75f, 0.1f));
                var cam = MakeCamera(made, 20);
                cam.orthographic = true;
                var dir = new Vector3(1f, 0.3f, 0.2f).normalized;
                cam.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                // Tight fit: the model's vertices in camera space (renderer bounds corners when a mesh is not readable).
                var pts = new List<Vector3>();
                foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    if (mesh.isReadable) foreach (var v in mesh.vertices) pts.Add(mf.transform.TransformPoint(v));
                    else
                    {
                        var b = mf.GetComponent<Renderer>().bounds;
                        for (int i = 0; i < 8; i++)
                            pts.Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    }
                }
                if (pts.Count == 0) return null;
                var centre = Vector3.zero;
                foreach (var p in pts) centre += p;
                centre /= pts.Count;
                cam.transform.position = centre + dir * 5;
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                foreach (var p in pts)
                {
                    var l = (Vector2)cam.transform.InverseTransformPoint(p);
                    min = Vector2.Min(min, l);
                    max = Vector2.Max(max, l);
                }
                var mid = (min + max) * 0.5f;
                cam.transform.position += cam.transform.right * mid.x + cam.transform.up * mid.y;
                float aspect = w / (float)h;
                cam.orthographicSize = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspect) * 1.06f;
                var px = Matte(cam, w, h);
                return ToTexture(px, w, h);
            }
            finally
            {
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
            }
        }

        // ------------------------------------------------------------------ boss portraits

        /// <summary>
        /// Head-and-shoulders portrait of an imported boss over the smoky backdrop (glow colour per boss), with glowing
        /// eyes: the Boss Rush card art. Null when the prefab is missing.
        /// </summary>
        public static Texture2D BossPortrait(string key, Color glow, int w, int h)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + key + ".prefab");
            if (prefab == null) return null;
            var made = new List<Object>();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var body = Spawn(prefab, Vector3.zero, -14, made);
                if (body == null) return null;
                var head = body.Bones[(int)ZB.Head];
                if (head != null) head.rotation = Quaternion.Euler(10, 4, -5) * head.rotation;
                var eyes = (body.EyeL.position + body.EyeR.position) * 0.5f;
                Lighting(made, true);
                var cam = MakeCamera(made, 30);
                // Head and shoulders filling the card: eyes on the upper third.
                cam.transform.position = eyes + new Vector3(0.1f, -0.05f, 0.82f);
                cam.transform.LookAt(eyes + new Vector3(0, -0.1f, 0));
                var layer = Matte(cam, w, h);
                var eyePx = new[] { (Vector2)cam.WorldToViewportPoint(body.EyeL.position), (Vector2)cam.WorldToViewportPoint(body.EyeR.position) };
                var px = new Color[w * h];
                float aspect = w / (float)h;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                        var bg = Backdrop((u - 0.5f) * aspect * 0.75f + 0.5f, v, new Vector2(0.5f, 0.6f), glow);
                        px[y * w + x] = Over(bg, layer[y * w + x]);
                    }
                Glow(px, w, h, eyePx, h * 0.008f, 1.2f);
                Finish(px, w, h, new Vector2(0.5f, 0.55f), 1.3f);
                return ToTexture(px, w, h);
            }
            finally
            {
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                ShaderUtil.allowAsyncCompilation = asyncShaders;
            }
        }

        // ------------------------------------------------------------------ scene

        static ArtBody Spawn(GameObject prefab, Vector3 pos, float yaw, List<Object> made)
        {
            var go = Object.Instantiate(prefab);
            made.Add(go);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            return go.GetComponent<ArtBody>();
        }

        /// <summary>Classic shamble: arms raised forward, head lolled.</summary>
        static void Reach(ArtBody b)
        {
            var fwd = b.transform.forward;
            var right = b.transform.right;
            foreach (var (bone, lift) in new[] { (ZB.ArmL, 72f), (ZB.ArmR, 62f), (ZB.ForeL, 12f), (ZB.ForeR, 22f) })
            {
                var t = b.Bones[(int)bone];
                if (t != null) t.rotation = Quaternion.AngleAxis(-lift, right) * t.rotation;
            }
            var head = b.Bones[(int)ZB.Head];
            if (head != null) head.rotation = Quaternion.AngleAxis(14, right) * Quaternion.AngleAxis(9, fwd) * head.rotation;
        }

        static void Lighting(List<Object> made, bool portrait)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.09f);
            // Key: warm, from the side (portrait) or low front-right (group). Rim: cold teal from behind-left.
            // Fill: dim red from below. The portrait key is paler so the infected skin keeps its colour.
            if (portrait) AddLight(made, "Key", new Color(1f, 0.66f, 0.5f), 1.9f, new Vector3(-0.85f, 0.12f, -0.5f));
            else AddLight(made, "Key", new Color(1f, 0.46f, 0.26f), 2.3f, new Vector3(-0.62f, 0.3f, -0.72f));
            AddLight(made, "Rim", new Color(0.32f, 0.78f, 0.95f), 3.2f, new Vector3(0.65f, -0.35f, 0.68f));
            AddLight(made, "Under", new Color(0.7f, 0.08f, 0.05f), 0.8f, new Vector3(0.1f, 1f, -0.3f));
        }

        static void AddLight(List<Object> made, string name, Color c, float intensity, Vector3 travel)
        {
            var go = new GameObject("Icon" + name);
            made.Add(go);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderingLayerMask = -1; // weapons render on the World light layer
            l.cullingMask = -1;
            go.transform.rotation = Quaternion.LookRotation(travel.normalized);
        }

        static Camera MakeCamera(List<Object> made, float fov)
        {
            var go = new GameObject("IconCam");
            made.Add(go);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            return cam;
        }

        /// <summary>
        /// Renders twice (black and white backgrounds); alpha = 1 - (white - black), colour = black pass / alpha.
        /// Returns premultiplied-free colour with alpha, bottom row first.
        /// </summary>
        static Color[] Matte(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            // Fixed aspect: WorldToViewportPoint is used after the target texture is released.
            cam.aspect = w / (float)h;
            Color[] Pass(Color bg)
            {
                cam.backgroundColor = bg;
                cam.Render();
                cam.Render();  // second render: the first can miss objects created this editor frame
                RenderTexture.active = rt;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = null;
                var p = t.GetPixels();
                Object.DestroyImmediate(t);
                return p;
            }
            var black = Pass(Color.black);
            var white = Pass(Color.white);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            var o = new Color[w * h];
            for (int i = 0; i < o.Length; i++)
            {
                float a = 1 - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f;
                a = Mathf.Clamp01(a);
                var c = a > 0.004f ? black[i] / a : Color.black;
                c.a = a;
                o[i] = c;
            }
            return o;
        }

        // ------------------------------------------------------------------ compositing

        static Color Over(Color bg, Color fg) => new Color(Mathf.Lerp(bg.r, fg.r, fg.a), Mathf.Lerp(bg.g, fg.g, fg.a), Mathf.Lerp(bg.b, fg.b, fg.a), 1);

        static readonly Color BloodGlow = new Color(0.42f, 0.045f, 0.035f);

        /// <summary>Dark smoky backdrop with a red glow behind the subject and a teal haze at the top left.</summary>
        static Color Backdrop(float u, float v, Vector2 glowAt) => Backdrop(u, v, glowAt, BloodGlow);

        static Color Backdrop(float u, float v, Vector2 glowAt, Color glow)
        {
            float d = Vector2.Distance(new Vector2(u, v), glowAt);
            var c = Color.Lerp(glow, new Color(0.016f, 0.018f, 0.026f), Mathf.Clamp01(d * 1.75f));
            c += new Color(0.035f, 0.12f, 0.16f) * Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(0.1f, 0.95f)) * 1.7f) * 0.8f;
            float fog = Noise.Fbm(u, v, 3, 5, 11);
            c *= 0.7f + fog * 0.6f;
            return c;
        }

        /// <summary>Three claw slashes from top right to bottom left, glowing red at their cores.</summary>
        static Color Slashes(Color c, float u, float v, float cu, float cv, float strength)
        {
            for (int i = 0; i < 3; i++)
            {
                float off = (i - 1) * 0.075f;
                // Signed distance to a line along (1, 0.85) through the slash centre, with tapered ends.
                var p = new Vector2(u - cu - off, v - cv + off * 0.4f);
                var dir = new Vector2(0.76f, 0.65f);
                float along = Vector2.Dot(p, dir);
                float across = Mathf.Abs(p.x * dir.y - p.y * dir.x);
                float len = 0.34f;
                float taper = Mathf.Clamp01(1 - Mathf.Abs(along) / len);
                if (taper <= 0) continue;
                float width = 0.011f * taper;
                float core = Mathf.Clamp01(1 - across / Mathf.Max(0.0005f, width));
                float glow = Mathf.Exp(-across * 70) * taper;
                c = Color.Lerp(c, new Color(0.85f, 0.05f, 0.03f), core * strength);
                c += new Color(0.55f, 0.03f, 0.01f) * glow * 0.55f * strength;
            }
            return c;
        }

        static void Glow(Color[] px, int w, int h, Vector2[] at, float sigma, float strength)
        {
            int r = Mathf.CeilToInt(sigma * 4);
            foreach (var vp in at)
            {
                int cx = Mathf.RoundToInt(vp.x * w), cy = Mathf.RoundToInt(vp.y * h);
                for (int y = Mathf.Max(0, cy - r); y < Mathf.Min(h, cy + r); y++)
                    for (int x = Mathf.Max(0, cx - r); x < Mathf.Min(w, cx + r); x++)
                    {
                        float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        float g = Mathf.Exp(-d2 / (2 * sigma * sigma));
                        float core = Mathf.Exp(-d2 / (2 * sigma * sigma * 0.08f));
                        var c = px[y * w + x] + new Color(1f, 0.36f, 0.07f) * g * strength;
                        px[y * w + x] = Color.Lerp(c, new Color(1f, 0.88f, 0.55f), core * 0.9f);
                    }
            }
        }

        /// <summary>Vignette, gentle contrast and a little grain.</summary>
        static void Finish(Color[] px, int w, int h, Vector2 centre, float vignette)
        {
            var rnd = new System.Random(7);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float d = Vector2.Distance(new Vector2(u, v), centre);
                    var c = px[y * w + x] * Mathf.Lerp(1.08f, 0.38f, Mathf.Clamp01(d * vignette - 0.2f));
                    c.r = Mathf.Pow(Mathf.Clamp01(c.r), 0.95f);
                    float grain = ((float)rnd.NextDouble() - 0.5f) * 0.02f;
                    px[y * w + x] = new Color(Mathf.Clamp01(c.r + grain), Mathf.Clamp01(c.g + grain), Mathf.Clamp01(c.b + grain), 1);
                }
        }

        static Texture2D ToTexture(Color[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ title (feature graphic)

        /// <summary>Draws the game title with the game's own fonts (a UI canvas rendered over the image).</summary>
        static Texture2D Title(Texture2D image, int w, int h)
        {
            var made = new List<Object>();
            try
            {
                var camGo = new GameObject("TitleCam");
                made.Add(camGo);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 1 << 5;
                cam.orthographic = true;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.targetTexture = rt;
                var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
                made.Add(canvasGo);
                canvasGo.layer = 5;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1;
                var bgImg = new GameObject("Bg", typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
                bgImg.transform.SetParent(canvasGo.transform, false);
                bgImg.gameObject.layer = 5;
                bgImg.texture = image;
                var brt = bgImg.rectTransform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
                var stencil = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "SairaStencilOne-Regular.ttf");
                var condensed = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "BarlowCondensed-ExtraBold.ttf");
                AddText(canvasGo.transform, stencil, "UNDEAD", 132, new Color(0.93f, 0.91f, 0.87f), 54, 300);
                AddText(canvasGo.transform, stencil, "APOCALYPSE", 70, new Color(0.86f, 0.1f, 0.08f), 58, 196);
                var bar = new GameObject("Bar", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                bar.transform.SetParent(canvasGo.transform, false);
                bar.gameObject.layer = 5;
                bar.color = new Color(0.86f, 0.1f, 0.08f, 0.9f);
                var r = bar.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
                r.anchoredPosition = new Vector2(60, 150);
                r.sizeDelta = new Vector2(430, 5);
                AddText(canvasGo.transform, condensed, "I N F I N I T E   W A R", 40, new Color(1f, 0.56f, 0.2f), 60, 92);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
                o.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                o.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                return o;
            }
            finally
            {
                foreach (var m in made) if (m != null) Object.DestroyImmediate(m);
            }
        }

        static void AddText(Transform canvas, Font font, string s, int size, Color c, float x, float y)
        {
            var t = new GameObject("T_" + s, typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
            t.transform.SetParent(canvas, false);
            t.gameObject.layer = 5;
            t.font = font;
            t.fontSize = size;
            t.color = c;
            t.text = s;
            t.alignment = TextAnchor.LowerLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.85f);
            sh.effectDistance = new Vector2(3, -3);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(900, size * 1.3f);
        }
    }
}
