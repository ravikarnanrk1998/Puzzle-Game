using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>Capture format shared with the editor encoder (UA / 7. Make Promo Video).</summary>
    [Serializable]
    public sealed class TrailerInfo
    {
        public int fps, width, height, frames, rate, musicRate, musicChannels;
    }

    /// <summary>
    /// Promo-video capture for the autopilot "trailer" plan (Windows build). Game time advances a fixed 1/30 s per
    /// frame (Time.captureDeltaTime), so slow capture never drops frames. Every frame is saved as a JPEG and the game's
    /// audio is rendered in step (AudioRenderer). An overlay draws the title / end cards, the captions and the dips to
    /// black between clips. The editor tool then encodes the folder to an MP4 with the music bed mixed in.
    /// </summary>
    public sealed class TrailerKit
    {
        public const int Fps = 30;
        const int Dip = 8;                 // frames of fade to / from black at every cut
        static readonly Vector2 CaptionPos = new Vector2(790, 175);

        readonly string _dir, _frameDir;
        readonly int _scale, _spf, _srcChannels;
        readonly TrailerInfo _info = new TrailerInfo { fps = Fps };
        readonly List<float> _audio = new List<float>(48000 * 2 * 50);
        bool _audioOk, _showCaption, _showCard;
        float _gain, _lastGain;

        readonly Canvas _canvas;
        readonly Image _black;
        readonly CanvasGroup _caption, _card;
        readonly RectTransform _captionBox, _logo;
        readonly Text _captionText, _cardSub;

        public TrailerKit(string dir, int scale)
        {
            _dir = dir;
            _scale = Mathf.Max(1, scale);
            _frameDir = Path.Combine(dir, "frames");
            // Output of a previous capture of this same tool.
            if (Directory.Exists(_frameDir)) Directory.Delete(_frameDir, true);
            Directory.CreateDirectory(_frameDir);
            _info.rate = AudioSettings.outputSampleRate;
            _spf = _info.rate / Fps;
            _srcChannels = Channels(AudioSettings.GetConfiguration().speakerMode);
            Log.I("TRAILER capture to " + _frameDir + " audio " + _info.rate + " Hz x" + _srcChannels);

            _canvas = Ui.Canvas("Trailer", 60);
            var root = (RectTransform)_canvas.transform;

            // Title / end card: the menu logo, large, over a dark red-lit backdrop.
            var card = Ui.Rect(root, "Card");
            card.Fill();
            _card = card.gameObject.AddComponent<CanvasGroup>();
            var bg = Ui.Img(card, Sprites.White, Pal.Dark, "Bg");
            bg.rectTransform.Fill();
            var glow = Ui.Img(card, Sprites.Glow, Pal.Blood.A(0.45f), "Glow");
            glow.rectTransform.At(0.5f, 0.5f, 0, 20, 2400, 1300);
            var vig = Ui.Img(card, Sprites.Vignette, Color.black, "Vignette");
            vig.rectTransform.Fill();
            _logo = Ui.Rect(card, "Logo");
            _logo.At(0.5f, 0.5f, 0, 60, 1300, 560);
            var t1 = Ui.Title(_logo, "UNDEAD", 190, Pal.Text, TextAnchor.MiddleCenter);
            t1.rectTransform.At(0.5f, 0.5f, 0, 130, 1300, 220);
            Ui.Shadow(t1, 6, 0.9f);
            var t2 = Ui.Title(_logo, "APOCALYPSE", 104, Pal.Blood, TextAnchor.MiddleCenter);
            t2.rectTransform.At(0.5f, 0.5f, 0, 0, 1300, 130);
            Ui.Shadow(t2, 5, 0.9f);
            var bar = Ui.Img(_logo, Sprites.White, Pal.Blood, "Bar");
            bar.rectTransform.At(0.5f, 0.5f, 0, -76, 780, 6);
            var tag = Ui.Img(_logo, Sprites.White, Pal.Blood, "Tag");
            tag.rectTransform.At(0.5f, 0.5f, 0, -122, 480, 60);
            var tt = Ui.Label(tag.transform, "I N F I N I T E   W A R", 40, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            tt.rectTransform.Fill();
            _cardSub = Ui.Label(card, "", 54, Pal.Accent2, TextAnchor.MiddleCenter, FontKind.Display);
            _cardSub.rectTransform.At(0.5f, 0.5f, 0, -300, 1600, 90);

            // Caption: bottom left (where the joystick sits in play; touch controls are hidden while recording).
            var cap = Ui.Rect(root, "Caption");
            cap.Fill();
            _caption = cap.gameObject.AddComponent<CanvasGroup>();
            var shade = Ui.Img(cap, Sprites.Glow, new Color(0, 0, 0, 0.8f), "Shade");
            shade.rectTransform.At(0, 0, 420, 130, 1800, 640);
            _captionBox = Ui.Rect(cap, "Box");
            _captionBox.At(0, 0, CaptionPos.x, CaptionPos.y, 1400, 130);
            _captionText = Ui.Title(_captionBox, "", 96, Pal.Text, TextAnchor.LowerLeft);
            _captionText.rectTransform.Fill();
            Ui.Shadow(_captionText, 5, 0.9f);
            var cbar = Ui.Img(_captionBox, Sprites.White, Pal.Blood, "Bar");
            cbar.rectTransform.At(0, 0, 230, -16, 460, 7);

            _black = Ui.Img(root, Sprites.White, new Color(0, 0, 0, 0), "Black");
            _black.rectTransform.Fill();
            _card.alpha = 0;
            _caption.alpha = 0;
        }

        static int Channels(AudioSpeakerMode m)
        {
            switch (m)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2;
            }
        }

        public int Frames => _info.frames;

        /// <summary>Starts a clip: fixed-step time, offline audio rendering and the clip's caption or card.</summary>
        public void Begin(string accent, string rest, bool card, string sub)
        {
            _showCaption = accent != null || rest != null;
            _captionText.text = (accent != null ? "<color=#FF6A22>" + accent + "</color>" : "") + (rest ?? "");
            _showCard = card;
            _card.alpha = card ? 1 : 0;
            _cardSub.text = sub ?? "";
            _caption.alpha = 0;
            _gain = _lastGain = 0;
            Time.captureDeltaTime = 1f / Fps;
            _audioOk = false;
            try { _audioOk = AudioRenderer.Start(); }
            catch (Exception e) { Log.W("TRAILER: audio capture unavailable: " + e.Message); }
        }

        /// <summary>Overlay state for frame <paramref name="i"/> of <paramref name="n"/> (set before the frame renders).</summary>
        public void Pose(int i, int n)
        {
            float vis = Mathf.Clamp01(Mathf.Min(i, n - 1 - i) / (float)Dip);
            _black.color = new Color(0, 0, 0, Ui.Dim(1 - vis));
            _gain = vis;
            if (_showCaption)
            {
                float a = Mathf.Clamp01((i - Dip) / 8f) * Mathf.Clamp01((n - 1 - i - Dip) / 8f);
                _caption.alpha = a;
                float slide = 1 - Mathf.Clamp01((i - Dip) / 10f);
                _captionBox.anchoredPosition = CaptionPos + new Vector2(-70 * slide * slide, 0);
            }
            else _caption.alpha = 0;
            if (_showCard) _logo.localScale = Vector3.one * (1 + 0.04f * i / Mathf.Max(1, n - 1));
        }

        /// <summary>Saves the frame that has just been rendered (call after WaitForEndOfFrame) and its audio.</summary>
        public void Capture()
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture(_scale);
            if (_info.frames == 0) { _info.width = tex.width; _info.height = tex.height; }
            if (tex.width == _info.width && tex.height == _info.height)
            {
                File.WriteAllBytes(Path.Combine(_frameDir, _info.frames.ToString("00000") + ".jpg"), tex.EncodeToJPG(92));
                _info.frames++;
                AddAudio();
            }
            else Log.E("TRAILER: frame size changed to " + tex.width + "x" + tex.height);
            UnityEngine.Object.Destroy(tex);
        }

        /// <summary>Exactly one frame of stereo audio per video frame (padded or trimmed), faded with the picture.</summary>
        void AddAudio()
        {
            float g0 = _lastGain, g1 = _gain;
            _lastGain = _gain;
            if (_audioOk)
            {
                int n = AudioRenderer.GetSampleCountForCaptureFrame();
                if (n > 0)
                {
                    using (var buf = new NativeArray<float>(n * _srcChannels, Allocator.Temp))
                    {
                        AudioRenderer.Render(buf);
                        int right = _srcChannels > 1 ? 1 : 0;
                        for (int s = 0; s < _spf; s++)
                        {
                            float g = Mathf.Lerp(g0, g1, s / (float)_spf);
                            int k = Mathf.Min(s, n - 1) * _srcChannels;
                            _audio.Add(s < n ? buf[k] * g : 0);
                            _audio.Add(s < n ? buf[k + right] * g : 0);
                        }
                        return;
                    }
                }
            }
            for (int s = 0; s < _spf * 2; s++) _audio.Add(0);
        }

        public void End()
        {
            if (_audioOk)
            {
                try { AudioRenderer.Stop(); }
                catch (Exception e) { Log.W("TRAILER: " + e.Message); }
            }
            _audioOk = false;
            Time.captureDeltaTime = 0;
            _caption.alpha = 0;
            _card.alpha = 0;
            _black.color = new Color(0, 0, 0, 0);
        }

        /// <summary>Writes the audio, the music track and trailer.json next to the frames.</summary>
        public void Finish()
        {
            WriteFloats(Path.Combine(_dir, "audio.raw"), _audio);
            var clip = Sfx.MusicClip(MusicKind.Combat);
            if (clip != null)
            {
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                WriteFloats(Path.Combine(_dir, "music.raw"), data);
                _info.musicRate = clip.frequency;
                _info.musicChannels = clip.channels;
            }
            else Log.W("TRAILER: music track not ready");
            File.WriteAllText(Path.Combine(_dir, "trailer.json"), JsonUtility.ToJson(_info, true));
            Log.I("TRAILER done: " + _info.frames + " frames " + _info.width + "x" + _info.height + ", " + (_audio.Count / 2) + " audio frames");
            UnityEngine.Object.Destroy(_canvas.gameObject);
        }

        static void WriteFloats(string path, IList<float> f)
        {
            using (var w = new BinaryWriter(File.Create(path)))
                for (int i = 0; i < f.Count; i++) w.Write(f[i]);
        }
    }
}
