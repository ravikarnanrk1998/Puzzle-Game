using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace KPL.Game
{
    public enum Sfx { Bat, BatBig, Edge, Defend, Bounce, Stumps, Glove, Whoosh, Click, Confirm, Back, Coin, Fanfare, Horn, Cheer, Groan, Applause, Dhol, Error, Pop }

    /// <summary>
    /// All sound is synthesised at start-up: bat cracks, stumps, crowd, UI and an original music
    /// loop, so the game ships with no audio assets at all.
    /// </summary>
    public sealed class Audio : MonoBehaviour
    {
        public static Audio I { get; private set; }
        const int Rate = 22050;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] _voices;
        int _next;
        AudioSource _music, _crowd, _crowd2;
        float _crowdTarget, _crowdLevel;
        AudioClip _musicClip;
        float[] _musicData;
        bool _musicWanted;

        public static void Init(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Audio>();
            I.Build();
        }

        void Build()
        {
            _voices = new AudioSource[10];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _crowd = gameObject.AddComponent<AudioSource>();
            _crowd.loop = true;
            _crowd2 = gameObject.AddComponent<AudioSource>();
            _crowd2.loop = true;

            _clips[Sfx.Bat] = Make("bat", 0.32f, BatHit(1.0f, false));
            _clips[Sfx.BatBig] = Make("batbig", 0.4f, BatHit(1.25f, false));
            _clips[Sfx.Edge] = Make("edge", 0.2f, BatHit(1.6f, true));
            _clips[Sfx.Defend] = Make("defend", 0.25f, Defend);
            _clips[Sfx.Bounce] = Make("bounce", 0.16f, Bounce);
            _clips[Sfx.Stumps] = Make("stumps", 0.7f, Stumps);
            _clips[Sfx.Glove] = Make("glove", 0.16f, Glove);
            _clips[Sfx.Whoosh] = Make("whoosh", 0.45f, Whoosh);
            _clips[Sfx.Click] = Make("click", 0.06f, t => Tone(t, 2300, 0.012f) * 0.6f + Noise(t) * Env(t, 0.003f) * 0.3f);
            _clips[Sfx.Confirm] = Make("confirm", 0.22f, t => (t < 0.08f ? Tone(t, 660, 0.06f) : Tone(t - 0.08f, 990, 0.09f)) * 0.55f);
            _clips[Sfx.Back] = Make("back", 0.18f, t => (t < 0.07f ? Tone(t, 700, 0.05f) : Tone(t - 0.07f, 470, 0.08f)) * 0.5f);
            _clips[Sfx.Error] = Make("error", 0.25f, t => Saw(t, 140) * Env(t, 0.12f) * 0.35f);
            _clips[Sfx.Pop] = Make("pop", 0.12f, t => Mathf.Sin(2 * Mathf.PI * (300 + 900 * t) * t) * Env(t, 0.04f) * 0.6f);
            _clips[Sfx.Coin] = Make("coin", 0.6f, Coin);
            _clips[Sfx.Fanfare] = Make("fanfare", 1.8f, Fanfare);
            _clips[Sfx.Horn] = Make("horn", 1.5f, Horn);
            _clips[Sfx.Cheer] = Make("cheer", 3.2f, Cheer());
            _clips[Sfx.Groan] = Make("groan", 1.6f, Groan());
            _clips[Sfx.Applause] = Make("applause", 2.8f, Applause());
            _clips[Sfx.Dhol] = Make("dhol", 2.4f, Dhol);
            _crowd.clip = MakeLoop("crowd", 6f, CrowdBed(0.6f));
            _crowd2.clip = MakeLoop("crowd2", 5f, CrowdBed(1.0f));
            ApplyVolumes();
            // Music takes a moment to synthesise; do it off the main thread.
            Task.Run(() => _musicData = MusicLoop());
        }

        void Update()
        {
            if (_musicData != null && _musicClip == null)
            {
                _musicClip = AudioClip.Create("music", _musicData.Length, 1, Rate, false);
                _musicClip.SetData(_musicData, 0);
                _music.clip = _musicClip;
                if (_musicWanted) _music.Play();
            }
            float sfx = SaveSystem.Data.Settings.Sfx;
            _crowdLevel = Mathf.MoveTowards(_crowdLevel, _crowdTarget, Time.unscaledDeltaTime * 0.35f);
            _crowd.volume = _crowdLevel * sfx * 0.7f;
            _crowd2.volume = Mathf.Max(0, _crowdLevel - 0.45f) * sfx;
            if (_crowdTarget > 0 && !_crowd.isPlaying)
            {
                _crowd.Play();
                _crowd2.Play();
            }
            if (_crowdTarget <= 0 && _crowdLevel <= 0.001f && _crowd.isPlaying)
            {
                _crowd.Stop();
                _crowd2.Stop();
            }
        }

        public void ApplyVolumes()
        {
            var s = SaveSystem.Data.Settings;
            _music.volume = s.Music * 0.6f;
        }

        public static void Play(Sfx s, float vol = 1, float pitch = 1)
        {
            if (I == null) return;
            float v = SaveSystem.Data.Settings.Sfx * vol;
            if (v <= 0.001f) return;
            var src = I._voices[I._next];
            I._next = (I._next + 1) % I._voices.Length;
            src.pitch = pitch;
            src.PlayOneShot(I._clips[s], v);
        }

        public static void Music(bool on)
        {
            if (I == null) return;
            I._musicWanted = on;
            I.ApplyVolumes();
            if (on && I._music.clip != null && !I._music.isPlaying) I._music.Play();
            if (!on) I._music.Stop();
        }

        /// <summary>Crowd ambience level: 0 = off, 0.4 = calm, 1 = roaring.</summary>
        public static void Crowd(float level)
        {
            if (I != null) I._crowdTarget = level;
        }

        public static void CrowdSwell(float peak, float hold = 2.5f)
        {
            if (I == null) return;
            I.StopCoroutine("SwellBack");
            float baseLevel = Mathf.Min(I._crowdTarget, 0.5f);
            I._crowdLevel = Mathf.Max(I._crowdLevel, peak);
            I._crowdTarget = peak;
            I.StartCoroutine(I.SwellBackRoutine(baseLevel, hold));
        }

        System.Collections.IEnumerator SwellBackRoutine(float baseLevel, float hold)
        {
            yield return new WaitForSecondsRealtime(hold);
            _crowdTarget = baseLevel;
        }

        // ------------------------------------------------------------ synthesis

        static readonly System.Random R = new System.Random(2026);
        static float Noise(float t) => (float)(R.NextDouble() * 2 - 1);
        static float Env(float t, float tau) => Mathf.Exp(-t / tau);
        static float Tone(float t, float f, float tau) => Mathf.Sin(2 * Mathf.PI * f * t) * Env(t, tau);
        static float Saw(float t, float f) => 2 * (t * f - Mathf.Floor(t * f + 0.5f));

        AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = f(i / (float)Rate);
            Normalize(d, 0.9f);
            // Short fade-out avoids clicks.
            int fade = Mathf.Min(n, Rate / 100);
            for (int i = 0; i < fade; i++) d[n - 1 - i] *= i / (float)fade;
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        AudioClip MakeLoop(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            int x = Rate / 2;
            var d = new float[n + x];
            for (int i = 0; i < d.Length; i++) d[i] = f(i / (float)Rate);
            // Crossfade the tail into the head for a seamless loop.
            for (int i = 0; i < x; i++)
            {
                float k = i / (float)x;
                d[i] = d[i] * k + d[n + i] * (1 - k);
            }
            var o = new float[n];
            Array.Copy(d, o, n);
            Normalize(o, 0.8f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(o, 0);
            return c;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 1e-6f;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static Func<float, float> BatHit(float bright, bool thin)
        {
            float lp = 0;
            return t =>
            {
                float n = Noise(t);
                lp += (n - lp) * 0.5f;
                float click = (n - lp) * Env(t, 0.0025f) * 1.2f;
                float body = Tone(t, 980 * bright, 0.07f) * 0.8f + Tone(t, 1730 * bright, 0.05f) * 0.55f + Tone(t, 2650 * bright, 0.035f) * 0.35f;
                if (!thin) body += Tone(t, 420, 0.09f) * 0.5f * bright;
                float noise = lp * Env(t, 0.025f) * 0.6f;
                return click + body * (thin ? 0.5f : 1) + noise;
            };
        }

        static float Defend(float t)
        {
            return Tone(t, 520, 0.05f) * 0.7f + Tone(t, 260, 0.07f) * 0.6f + Noise(t) * Env(t, 0.008f) * 0.4f;
        }

        static float Bounce(float t)
        {
            float f = 160 - 60 * t / 0.16f;
            return Mathf.Sin(2 * Mathf.PI * f * t) * Env(t, 0.035f) + Noise(t) * Env(t, 0.01f) * 0.35f;
        }

        static float Stumps(float t)
        {
            float s = 0;
            float[] at = { 0, 0.045f, 0.11f, 0.19f, 0.31f };
            float[] fq = { 610, 780, 540, 900, 700 };
            for (int i = 0; i < at.Length; i++)
            {
                float u = t - at[i];
                if (u < 0) continue;
                float a = 1f / (1 + i * 0.6f);
                s += (Tone(u, fq[i], 0.04f) + Tone(u, fq[i] * 2.3f, 0.02f) * 0.5f + Noise(u) * Env(u, 0.006f) * 0.6f) * a;
            }
            return s;
        }

        static float Glove(float t) => Noise(t) * Env(t, 0.012f) * 0.8f + Tone(t, 300, 0.03f) * 0.6f;

        static float Whoosh(float t)
        {
            float x = t / 0.45f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(x));
            return Noise(t) * env * env * 0.5f * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * (300 + 800 * x) * t));
        }

        static float Coin(float t)
        {
            float s = 0;
            float[] f = { 1318.5f, 1760f, 2637f };
            for (int i = 0; i < 3; i++)
            {
                float u = t - i * 0.07f;
                if (u < 0) continue;
                s += (Tone(u, f[i], 0.18f) + Tone(u, f[i] * 2.01f, 0.08f) * 0.3f) * 0.5f;
            }
            return s;
        }

        static float Fanfare(float t)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float s = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float u = t - i * 0.14f;
                if (u < 0) continue;
                float dur = i == notes.Length - 1 ? 1.2f : 0.5f;
                float env = Mathf.Min(1, u / 0.02f) * Mathf.Exp(-u / dur);
                s += (Saw(u, notes[i]) * 0.5f + Mathf.Sin(2 * Mathf.PI * notes[i] * u)) * env * 0.35f;
            }
            return s;
        }

        static float Horn(float t)
        {
            float trem = 1 + 0.06f * Mathf.Sin(2 * Mathf.PI * 5.5f * t);
            float env = Mathf.Min(1, t / 0.05f) * (t > 1.2f ? Mathf.Max(0, 1 - (t - 1.2f) / 0.3f) : 1);
            return (Saw(t, 233 * trem) + Saw(t, 294 * trem) * 0.8f + Saw(t, 349 * trem) * 0.5f) * env * 0.3f;
        }

        static Func<float, float> CrowdBed(float bright)
        {
            float a = 0, b = 0, c = 0;
            var rnd = new System.Random(77);
            return t =>
            {
                float n = (float)(rnd.NextDouble() * 2 - 1);
                a += (n - a) * 0.08f * bright;
                b += (a - b) * 0.15f;
                c += (n - c) * 0.35f;
                float mod = 0.75f + 0.15f * Mathf.Sin(t * 1.3f) + 0.1f * Mathf.Sin(t * 3.7f + 1) + 0.08f * Mathf.Sin(t * 0.37f);
                return (b * 1.6f + (c - a) * 0.12f) * mod;
            };
        }

        static Func<float, float> Cheer()
        {
            var bed = CrowdBed(1.2f);
            return t =>
            {
                float env = Mathf.Min(1, t / 0.25f) * Mathf.Exp(-Mathf.Max(0, t - 0.6f) / 1.1f);
                float voice = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 3.1f * t);
                return bed(t) * env * (0.8f + 0.4f * voice);
            };
        }

        static Func<float, float> Groan()
        {
            float lp = 0;
            return t =>
            {
                lp += (Noise(t) - lp) * 0.05f;
                float env = Mathf.Min(1, t / 0.15f) * Mathf.Exp(-t / 0.8f);
                float f = 220 - 60 * t;
                return lp * env * 2.2f * (0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * f * t));
            };
        }

        static Func<float, float> Applause()
        {
            var rnd = new System.Random(5);
            var claps = new List<float>();
            for (int i = 0; i < 260; i++) claps.Add((float)(rnd.NextDouble() * 2.6));
            claps.Sort();
            float hp = 0, prev = 0;
            return t =>
            {
                float s = 0;
                foreach (var c in claps)
                {
                    float u = t - c;
                    if (u < 0) break;
                    if (u > 0.03f) continue;
                    s += Noise(t) * Env(u, 0.006f);
                }
                float y = s - prev;
                prev = s;
                hp = y * 0.6f + hp * 0.4f;
                float env = Mathf.Min(1, t / 0.3f) * Mathf.Exp(-Mathf.Max(0, t - 1.4f) / 0.6f);
                return hp * env;
            };
        }

        static float Drum(float u, float f0, float f1, float tau)
        {
            if (u < 0) return 0;
            float f = f1 + (f0 - f1) * Mathf.Exp(-u / 0.03f);
            return Mathf.Sin(2 * Mathf.PI * f * u) * Env(u, tau);
        }

        static float Dhol(float t)
        {
            const float beat = 60f / 150f / 2f;
            int[] pattern = { 1, 0, 2, 1, 0, 2, 1, 2, 1, 0, 2, 1, 2, 0, 2, 2 };
            float s = 0;
            for (int i = 0; i < pattern.Length; i++)
            {
                float u = t - i * beat;
                if (u < 0) break;
                if (pattern[i] == 1) s += Drum(u, 130, 55, 0.18f);
                else if (pattern[i] == 2) s += Drum(u, 420, 260, 0.06f) * 0.6f + Noise(u) * Env(u, 0.01f) * 0.2f;
            }
            return s;
        }

        /// <summary>Original menu loop: Am-F-C-G at 104 bpm with pad, bass, drums, dhol and a pluck melody.</summary>
        static float[] MusicLoop()
        {
            const float bpm = 104f;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            int n = (int)(len * Rate);
            var d = new float[n];
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f }, new[] { 261.63f, 329.63f, 392f }, new[] { 196f, 246.94f, 293.66f },
            };
            float[] bass = { 110f, 87.31f, 130.81f, 98f };
            var rnd = new System.Random(9);
            float lp = 0, hpPrev = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = (int)(t / (4 * beat)) % bars;
                int ci = bar % 4;
                float tb = t - (int)(t / (4 * beat)) * 4 * beat;
                float tBeat = t % beat;
                int beatIdx = (int)(t / beat);
                float s = 0;
                // Pad: detuned saws through a soft low-pass.
                float pad = 0;
                foreach (var f in chords[ci]) pad += Saw(t, f) + Saw(t, f * 1.004f);
                float padEnv = Mathf.Min(1, tb / 0.4f) * (0.75f + 0.25f * Mathf.Cos(tb / (4 * beat) * Mathf.PI * 2));
                lp += (pad * padEnv - lp) * 0.06f;
                s += lp * 0.09f;
                // Bass on 1, the "and" of 2, and 3.
                float[] bassHits = { 0, 1.5f, 2f, 3.5f };
                foreach (var h in bassHits)
                {
                    float u = tb - h * beat;
                    if (u >= 0 && u < beat) s += (Mathf.Sin(2 * Mathf.PI * bass[ci] * u) + 0.3f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * bass[ci] * u))) * Env(u, 0.22f) * 0.22f;
                }
                // Drums.
                int inBar = beatIdx % 4;
                if (inBar == 0 || inBar == 2) s += Drum(tBeat, 120, 45, 0.14f) * 0.5f;
                if (inBar == 1 || inBar == 3)
                {
                    float nz = (float)(rnd.NextDouble() * 2 - 1);
                    s += (nz * Env(tBeat, 0.05f) * 0.25f + Drum(tBeat, 220, 170, 0.05f) * 0.15f);
                }
                float half = t % (beat / 2);
                {
                    float nz = (float)(rnd.NextDouble() * 2 - 1);
                    hp = 0.7f * (hp + nz - hpPrev);
                    hpPrev = nz;
                    s += hp * Env(half, 0.012f) * 0.07f;
                }
                // Dhol fills on the off-beats in the second half.
                if (bar >= 4)
                {
                    float q = t % (beat / 2);
                    int qi = (int)(t / (beat / 2)) % 8;
                    if (qi == 3 || qi == 6 || qi == 7) s += Drum(q, 400, 250, 0.05f) * 0.16f;
                }
                // Pluck arpeggio in bars 4-7.
                if (bar >= 4)
                {
                    float sixteenth = beat / 4;
                    int si = (int)(tb / sixteenth);
                    float u = tb - si * sixteenth;
                    float f = chords[ci][si % 3] * (si % 6 >= 3 ? 2 : 1);
                    float tri = 1 - 4 * Mathf.Abs(Mathf.Repeat(f * u, 1) - 0.5f);
                    s += tri * Env(u, 0.08f) * 0.09f;
                }
                d[i] = s;
            }
            // Smooth the loop point.
            int x = Rate / 20;
            for (int i = 0; i < x; i++)
            {
                float k = i / (float)x;
                d[i] *= k;
                d[n - 1 - i] *= k;
            }
            Normalize(d, 0.8f);
            return d;
        }
    }

    public static class Haptics
    {
        public static void Pulse(long ms = 40)
        {
            if (!SaveSystem.Data.Settings.Vibrate) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vib?.Call("vibrate", ms);
                }
            }
            catch (Exception)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
