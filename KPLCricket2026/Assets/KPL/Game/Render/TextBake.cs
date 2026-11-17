using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// Renders a small uGUI composition (text, shapes, crests) into a Texture2D once. Used for
    /// shirt names and numbers, advertising boards, flags and the stadium screens.
    /// </summary>
    public static class TextBake
    {
        const int Layer = 30;

        public static Texture2D Render(int w, int h, Color bg, Action<RectTransform> build, bool mips = true)
        {
            var root = new GameObject("TextBake");
            root.hideFlags = HideFlags.HideAndDontSave;
            var camGo = new GameObject("BakeCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;

            var canvasGo = new GameObject("BakeCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            var area = Ui.Node(canvasGo.transform, "Area").Fill();
            build(area);
            SetLayer(canvasGo.transform);
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, mips);
            tex.Apply(mips, false);
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(root);
            return tex;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform c in t) SetLayer(c);
        }
    }
}
