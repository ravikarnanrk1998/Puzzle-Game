using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Synthesised audio: wind and tyre loops that follow the player's speed, one-shots for
    /// every action and a generated rock loop for music. Built at startup, no audio files.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }
        const int Rate = 22050;

        AudioSource _wind, _tyre, _music, _oneShot, _crowd;
        AudioClip _click, _beep, _go, _swish, _hit, _crash, _land, _hop, _boost, _orb, _trick, _cheer, _finish, _bump, _windClip, _tyreClip, _musicClip, _crowdClip;
        float _musicTarget;
        System.Random _rnd = new System.Random(3);

        public static void Init(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Sfx>();
            I.Build();
        }

        float N() => (float)_rnd.NextDouble() * 2 - 1;

        static float[] Synth(float seconds, System.Func<float, float> f)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1);
            return d;
        }

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Makes a loop seamless with a crossfade of its tail into its head.</summary>
        static float[] Loop(float[] d, float fadeSec)
        {
            int fade = (int)(fadeSec * Rate), n = d.Length;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                d[i] = d[i] * t + d[n - fade + i] * (1 - t);
            }
            var o = new float[n - fade];
            System.Array.Copy(d, o, o.Length);
            return o;
        }

        AudioSource Src(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.ignoreListenerPause = !loop;
            return s;
        }

        void Build()
        {
            _wind = Src(true);
            _tyre = Src(true);
            _music = Src(true);
            _crowd = Src(true);
            _oneShot = Src(false);
            _music.ignoreListenerPause = true;

            // Wind: bright noise with slow gusts.
            float lp = 0, lp2 = 0;
            _windClip = Make("Wind", Loop(Synth(4f, t =>
            {
                lp += (N() - lp) * 0.35f;
                lp2 += (lp - lp2) * 0.5f;
                float gust = 0.7f + 0.3f * Mathf.Sin(t * 2.1f) * Mathf.Sin(t * 0.77f + 1);
                return (lp - lp2 * 0.6f) * 1.6f * gust;
            }), 0.4f));
            // Tyres on dirt: low rumble plus gravel crackle.
            float tl = 0;
            _tyreClip = Make("Tyre", Loop(Synth(3f, t =>
            {
                tl += (N() - tl) * 0.06f;
                float crackle = _rnd.NextDouble() < 0.012 ? N() * 0.9f : 0;
                return tl * 2.6f + crackle;
            }), 0.3f));
            float cl = 0;
            _crowdClip = Make("Crowd", Loop(Synth(5f, t =>
            {
                cl += (N() - cl) * 0.12f;
                return cl * 2.2f * (0.75f + 0.25f * Mathf.Sin(t * 1.4f));
            }), 0.5f));

            _click = Make("Click", Synth(0.05f, t => Mathf.Sin(t * 2 * Mathf.PI * 1800) * Mathf.Exp(-t * 90) * 0.5f));
            _beep = Make("Beep", Synth(0.22f, t => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 660)) * 0.35f * Mathf.Exp(-t * 9)));
            _go = Make("Go", Synth(0.6f, t => (Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 1320)) * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * 660) * 0.3f) * Mathf.Exp(-t * 3.5f)));
            _swish = Make("Swish", Synth(0.22f, t =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI);
                return N() * env * 0.5f;
            }));
            float hl = 0;
            _hit = Make("Hit", Synth(0.3f, t =>
            {
                hl += (N() - hl) * 0.2f;
                return (Mathf.Sin(t * 2 * Mathf.PI * (120 - t * 200)) * 0.9f + hl * 1.5f * Mathf.Exp(-t * 30)) * Mathf.Exp(-t * 13);
            }));
            float kl = 0;
            _crash = Make("Crash", Synth(0.9f, t =>
            {
                kl += (N() - kl) * 0.3f;
                float metal = Mathf.Sin(t * 2 * Mathf.PI * 1630) * 0.25f * Mathf.Exp(-t * 7) + Mathf.Sin(t * 2 * Mathf.PI * 2410) * 0.15f * Mathf.Exp(-t * 9);
                float thud = Mathf.Sin(t * 2 * Mathf.PI * (90 - t * 40)) * Mathf.Exp(-t * 6);
                return (kl * 1.4f * Mathf.Exp(-t * 4) + metal + thud * 0.8f) * 0.9f;
            }));
            _land = Make("Land", Synth(0.28f, t => (Mathf.Sin(t * 2 * Mathf.PI * (75 - t * 60)) + N() * 0.35f * Mathf.Exp(-t * 25)) * Mathf.Exp(-t * 14)));
            _bump = Make("Bump", Synth(0.16f, t => (Mathf.Sin(t * 2 * Mathf.PI * 150) + N() * 0.4f) * Mathf.Exp(-t * 26) * 0.7f));
            _hop = Make("Hop", Synth(0.14f, t => N() * 0.25f * Mathf.Exp(-t * 30) + Mathf.Sin(t * 2 * Mathf.PI * 220) * 0.2f * Mathf.Exp(-t * 20)));
            float bl = 0;
            _boost = Make("Boost", Synth(1.1f, t =>
            {
                bl += (N() - bl) * (0.1f + t * 0.4f);
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 2.2f);
                return (bl * 2.2f + Mathf.Sin(t * 2 * Mathf.PI * (180 + t * 420)) * 0.3f) * env;
            }));
            _orb = Make("Orb", Synth(0.35f, t =>
            {
                float f = t < 0.08f ? 1046 : t < 0.16f ? 1318 : 1568;
                return Mathf.Sin(t * 2 * Mathf.PI * f) * 0.3f * Mathf.Exp(-t * 6);
            }));
            _trick = Make("Trick", Synth(0.5f, t =>
            {
                float f = t < 0.12f ? 784 : 1175;
                return (Mathf.Sin(t * 2 * Mathf.PI * f) + 0.3f * Mathf.Sin(t * 2 * Mathf.PI * f * 2)) * 0.28f * Mathf.Exp(-t * 4);
            }));
            float ch = 0;
            _cheer = Make("Cheer", Synth(2.4f, t =>
            {
                ch += (N() - ch) * 0.25f;
                return ch * 2.4f * Mathf.Clamp01(t / 0.3f) * Mathf.Exp(-Mathf.Max(0, t - 0.6f) * 1.5f);
            }));
            _finish = Make("Finish", Synth(1.6f, t =>
            {
                float[] notes = { 523, 659, 784, 1046 };
                int i = Mathf.Min(3, (int)(t / 0.14f));
                float f = notes[i];
                float env = i < 3 ? 1 : Mathf.Exp(-(t - 0.42f) * 2.5f);
                return (Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * f)) * 0.18f + Mathf.Sin(t * 2 * Mathf.PI * f) * 0.2f) * env;
            }));
            _musicClip = Make("Music", Music());

            _wind.clip = _windClip;
            _tyre.clip = _tyreClip;
            _crowd.clip = _crowdClip;
            _music.clip = _musicClip;
        }

        /// <summary>Eight bars of driving rock at 128 BPM: drums, bass and a pentatonic lead.</summary>
        float[] Music()
        {
            const float bpm = 128;
            float beat = 60 / bpm;
            int bars = 8;
            int n = (int)(bars * 4 * beat * Rate);
            var d = new float[n];
            int[] roots = { 40, 40, 36, 36, 43, 43, 38, 38 }; // E E C C G G D D (MIDI)
            float Hz(int midi) => 440 * Mathf.Pow(2, (midi - 69) / 12f);
            int[] scale = { 0, 3, 5, 7, 10, 12, 15 };
            var rnd = new System.Random(11);
            float lowp = 0, hatlp = 0;
            var lead = new int[bars * 8];
            for (int i = 0; i < lead.Length; i++) lead[i] = rnd.NextDouble() < 0.7 ? scale[rnd.Next(scale.Length)] : -1;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float bt = t / beat;
                int b = (int)bt;
                float ph = bt - b;
                int bar = (b / 4) % bars;
                float v = 0;
                // Kick on 1 and 3 (plus an off-beat push), snare on 2 and 4.
                float kp = ph;
                if (b % 4 == 0 || b % 4 == 2 || (b % 4 == 3 && ph > 0.5f))
                {
                    float kt = (b % 4 == 3 ? ph - 0.5f : kp) * beat;
                    if (kt >= 0) v += Mathf.Sin(2 * Mathf.PI * (50 * kt + 60 * (1 - Mathf.Exp(-kt * 30)) / 30)) * Mathf.Exp(-kt * 9) * 0.55f;
                }
                if (b % 4 == 1 || b % 4 == 3)
                {
                    float st = ph * beat;
                    v += ((float)rnd.NextDouble() * 2 - 1) * Mathf.Exp(-st * 18) * 0.28f + Mathf.Sin(2 * Mathf.PI * 190 * st) * Mathf.Exp(-st * 22) * 0.2f;
                }
                // Hats on eighths.
                float eighth = bt * 2 - Mathf.Floor(bt * 2);
                float noise = (float)rnd.NextDouble() * 2 - 1;
                hatlp += (noise - hatlp) * 0.6f;
                v += (noise - hatlp) * Mathf.Exp(-eighth * beat * 2 * 40) * 0.12f;
                // Bass: eighth-note root pulses, gritty.
                float bf = Hz(roots[bar]);
                float saw = 2 * (t * bf - Mathf.Floor(t * bf + 0.5f));
                lowp += (saw - lowp) * 0.12f;
                v += lowp * 0.32f * (0.6f + 0.4f * Mathf.Exp(-eighth * beat * 2 * 6));
                // Power-chord pad (root + fifth), quiet.
                float pf = bf * 2;
                v += (Mathf.Sign(Mathf.Sin(2 * Mathf.PI * pf * t)) + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * pf * 1.5f * t))) * 0.025f;
                // Lead on eighths in the second half of the loop.
                if (bar >= 4)
                {
                    int li = (int)(bt * 2) % lead.Length;
                    if (lead[li] >= 0)
                    {
                        float lf = Hz(roots[bar] + 24 + lead[li]);
                        float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * lf * t));
                        v += sq * 0.06f * Mathf.Exp(-eighth * beat * 2 * 5);
                    }
                }
                d[i] = Mathf.Clamp(v * 0.8f, -1, 1);
            }
            return d;
        }

        // ------------------------------------------------------------ control

        public void Music(bool on, float volume = 0.35f)
        {
            _musicTarget = on && Settings.Music ? volume : 0;
            if (_musicTarget > 0 && !_music.isPlaying)
            {
                _music.volume = 0;
                _music.Play();
            }
        }

        public void StartRide()
        {
            if (!Settings.Sound) return;
            _wind.volume = 0;
            _tyre.volume = 0;
            _crowd.volume = 0;
            if (!_wind.isPlaying) _wind.Play();
            if (!_tyre.isPlaying) _tyre.Play();
            if (!_crowd.isPlaying) _crowd.Play();
        }

        public void StopRide()
        {
            if (_wind) _wind.Stop();
            if (_tyre) _tyre.Stop();
            if (_crowd) _crowd.Stop();
        }

        float _windT, _tyreT, _crowdT;

        /// <summary>Per-frame loop levels from the player's speed (0..1).</summary>
        public void Ride(float speed01, bool grounded, bool snow, float crowd)
        {
            _windT = Mathf.Pow(speed01, 1.4f) * 0.5f;
            _tyreT = grounded ? speed01 * (snow ? 0.32f : 0.5f) : 0;
            _crowdT = crowd * 0.35f;
            _wind.pitch = 0.75f + speed01 * 0.7f;
            _tyre.pitch = (snow ? 1.3f : 0.75f) + speed01 * 0.6f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_wind.isPlaying) _wind.volume = Mathf.MoveTowards(_wind.volume, _windT, dt * 2);
            if (_tyre.isPlaying) _tyre.volume = Mathf.MoveTowards(_tyre.volume, _tyreT, dt * 4);
            if (_crowd.isPlaying) _crowd.volume = Mathf.MoveTowards(_crowd.volume, _crowdT, dt * 1);
            _music.volume = Mathf.MoveTowards(_music.volume, _musicTarget, dt * 0.6f);
            if (_musicTarget <= 0 && _music.volume <= 0 && _music.isPlaying) _music.Stop();
        }

        void Play(AudioClip c, float vol, float pitch = 1)
        {
            if (!Settings.Sound || c == null) return;
            _oneShot.pitch = pitch;
            _oneShot.PlayOneShot(c, vol);
        }

        float Jit => 0.94f + Random.value * 0.12f;

        public void Click() => Play(_click, 0.4f);
        public void Beep() => Play(_beep, 0.6f);
        public void Go() => Play(_go, 0.7f);
        public void Swish() => Play(_swish, 0.45f, Jit);
        public void Hit(float vol = 1) => Play(_hit, 0.8f * vol, Jit);
        public void Crash(float vol = 1) => Play(_crash, 0.85f * vol, Jit);
        public void Land(float vol = 1) => Play(_land, 0.7f * vol, Jit);
        public void Bump() => Play(_bump, 0.5f, Jit);
        public void Hop() => Play(_hop, 0.5f, Jit);
        public void Boost() => Play(_boost, 0.75f);
        public void Orb() => Play(_orb, 0.55f, Jit);
        public void Trick() => Play(_trick, 0.6f);
        public void Cheer(float vol = 1) => Play(_cheer, 0.6f * vol);
        public void Finish() => Play(_finish, 0.7f);
    }
}
