using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Synthesised sound: stadium crowd bed, bat on ball, stumps, cheers and
    /// UI clicks. Generated at startup, so no audio files are needed.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        AudioSource _crowd, _oneShot;
        AudioClip _crowdClip, _bat, _stumps, _cheer, _groan, _click, _hammer;
        float _hype;
        const int Rate = 22050;

        public static void Init(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Sfx>();
            I.Build();
        }

        void Build()
        {
            _crowd = gameObject.AddComponent<AudioSource>();
            _crowd.loop = true;
            _crowd.playOnAwake = false;
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            // Menu clicks still sound while the match (and AudioListener) is paused.
            _oneShot.ignoreListenerPause = true;
            var rnd = new System.Random(5);
            float N() => (float)rnd.NextDouble() * 2 - 1;

            // Crowd: brown-ish noise with slow swells, 6 s loop with a crossfade.
            int n = Rate * 6;
            var crowd = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                lp += (N() - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.3f;
                float swell = 0.75f + 0.25f * Mathf.Sin(i / (float)Rate * 1.3f) * Mathf.Sin(i / (float)Rate * 0.47f + 1);
                crowd[i] = lp2 * 2.2f * swell;
            }
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                crowd[i] = crowd[i] * t + crowd[n - fade + i] * (1 - t);
            }
            var loop = new float[n - fade];
            System.Array.Copy(crowd, loop, loop.Length);
            _crowdClip = Make("Crowd", loop);

            _bat = Make("Bat", Synth(0.16f, (t, i) => (Mathf.Sin(t * 2 * Mathf.PI * 820) * 0.7f + Mathf.Sin(t * 2 * Mathf.PI * 1430) * 0.35f + N() * Mathf.Exp(-t * 120) * 0.6f) * Mathf.Exp(-t * 34)));
            _stumps = Make("Stumps", Synth(0.35f, (t, i) => (Mathf.Sin(t * 2 * Mathf.PI * 520) * 0.6f + Mathf.Sin(t * 2 * Mathf.PI * 1180) * 0.4f + N() * 0.3f) * Mathf.Exp(-t * 14)));
            _hammer = Make("Hammer", Synth(0.3f, (t, i) => (Mathf.Sin(t * 2 * Mathf.PI * 180) + N() * Mathf.Exp(-t * 60) * 0.8f) * Mathf.Exp(-t * 18)));
            _click = Make("Click", Synth(0.05f, (t, i) => Mathf.Sin(t * 2 * Mathf.PI * 1800) * Mathf.Exp(-t * 90) * 0.5f));
            float cl = 0;
            _cheer = Make("Cheer", Synth(2.8f, (t, i) =>
            {
                cl += (N() - cl) * 0.25f;
                float env = Mathf.Clamp01(t / 0.35f) * Mathf.Exp(-Mathf.Max(0, t - 0.6f) * 1.4f);
                return cl * 2.6f * env;
            }));
            float gl = 0;
            _groan = Make("Groan", Synth(1.6f, (t, i) =>
            {
                gl += (N() - gl) * 0.05f;
                return gl * 3f * Mathf.Clamp01(t / 0.2f) * Mathf.Exp(-t * 1.6f);
            }));
        }

        static float[] Synth(float seconds, System.Func<float, int, float> f)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate, i), -1, 1);
            return d;
        }

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        public void StartCrowd(float volume = 0.35f)
        {
            if (!Settings.Sound) return;
            _crowd.clip = _crowdClip;
            _crowd.volume = volume;
            _crowdBase = volume;
            _hype = 0.15f;
            if (!_crowd.isPlaying) _crowd.Play();
        }

        public void StopCrowd()
        {
            // The source can already be gone during app shutdown.
            if (_crowd) _crowd.Stop();
        }

        public void SetHype(float h) => _hype = h;

        float _crowdBase = 0.35f;

        void Update()
        {
            if (_crowd.isPlaying) _crowd.volume = Mathf.Lerp(_crowd.volume, _crowdBase * (0.8f + _hype), Time.deltaTime * 2);
        }

        void Play(AudioClip c, float vol, float pitch = 1)
        {
            if (!Settings.Sound || c == null) return;
            _oneShot.pitch = pitch;
            _oneShot.PlayOneShot(c, vol);
        }

        public void Bat(float power = 1) => Play(_bat, 0.6f + 0.4f * power, 0.92f + Random.value * 0.12f);
        public void Stumps() => Play(_stumps, 0.9f);
        public void Cheer(float big = 1) => Play(_cheer, 0.5f + 0.5f * big);
        public void Groan() => Play(_groan, 0.6f);
        public void Click() => Play(_click, 0.4f);
        public void Hammer() => Play(_hammer, 0.9f);
    }
}
