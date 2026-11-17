using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UA.Core;
using UnityEngine;

namespace UA.Game
{
    public enum MusicKind { None, Menu, Combat, Boss }

    /// <summary>
    /// Audio manager. Every clip is synthesised at start-up on worker threads (no audio files ship
    /// with the game). Plays 3D one-shots from a pool with distance attenuation, 2D UI / player sounds,
    /// cross-faded music and looping ambience.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx _i;
        static readonly Dictionary<string, AudioClip[]> Clips = new Dictionary<string, AudioClip[]>();
        static Dictionary<string, List<float[]>> _pending;
        static Task _gen;
        public static bool Ready { get; private set; }
        public static float Progress;

        AudioSource[] _pool3d, _pool2d;
        AudioSource _musicA, _musicB, _amb, _amb2, _flame, _heart;
        int _next3d, _next2d;
        MusicKind _music;
        bool _musicOnA;
        float _flameT;
        readonly Dictionary<string, float> _lastPlay = new Dictionary<string, float>();

        public static void Init()
        {
            if (_i != null) return;
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Sfx>();
            _i.Setup();
            StartGeneration();
        }

        void Setup()
        {
            _pool3d = new AudioSource[20];
            for (int i = 0; i < _pool3d.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 2.5f;
                s.maxDistance = 60;
                s.dopplerLevel = 0;
                // Each 3D source lives on its own child so it can be positioned.
                var child = new GameObject("S3D" + i);
                child.transform.SetParent(transform, false);
                var cs = child.AddComponent<AudioSource>();
                cs.playOnAwake = false;
                cs.spatialBlend = 1;
                cs.rolloffMode = AudioRolloffMode.Logarithmic;
                cs.minDistance = 2.5f;
                cs.maxDistance = 60;
                cs.dopplerLevel = 0;
                Destroy(s);
                _pool3d[i] = cs;
            }
            _pool2d = new AudioSource[10];
            for (int i = 0; i < _pool2d.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                _pool2d[i] = s;
            }
            _musicA = Loop2D();
            _musicB = Loop2D();
            _amb = Loop2D();
            _amb2 = Loop2D();
            _flame = Loop2D();
            _heart = Loop2D();
        }

        AudioSource Loop2D()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0;
            s.volume = 0;
            return s;
        }

        // --------------------------------------------------------------- generation

        static readonly Dictionary<WeaponId, Synth.GunParams> GunDefs = new Dictionary<WeaponId, Synth.GunParams>
        {
            { WeaponId.StandardPistol, new Synth.GunParams { Boom = 0.5f, BoomHz = 120, Body = 0.8f, BodyHz = 3200, BodyDecay = 0.045f, Crack = 0.9f, Tail = 0.5f, Mech = 0.3f, Drive = 2.2f } },
            { WeaponId.HeavyPistol, new Synth.GunParams { Boom = 1.1f, BoomHz = 90, Body = 1f, BodyHz = 2400, BodyDecay = 0.08f, Crack = 1f, Tail = 0.9f, Mech = 0.4f, Drive = 2.8f } },
            { WeaponId.TacticalPistol, new Synth.GunParams { Boom = 0.4f, BoomHz = 130, Body = 0.7f, BodyHz = 3600, BodyDecay = 0.035f, Crack = 0.8f, Tail = 0.4f, Mech = 0.3f, Drive = 2f } },
            { WeaponId.VK74, new Synth.GunParams { Boom = 0.9f, BoomHz = 95, Body = 1f, BodyHz = 2600, BodyDecay = 0.065f, Crack = 1.1f, Tail = 0.8f, Mech = 0.4f, Drive = 2.8f } },
            { WeaponId.R4Carbine, new Synth.GunParams { Boom = 0.7f, BoomHz = 110, Body = 0.9f, BodyHz = 3400, BodyDecay = 0.05f, Crack = 1.2f, Tail = 0.7f, Mech = 0.35f, Drive = 2.5f } },
            { WeaponId.RavenTR, new Synth.GunParams { Boom = 0.6f, BoomHz = 115, Body = 0.85f, BodyHz = 3600, BodyDecay = 0.045f, Crack = 1.1f, Tail = 0.6f, Mech = 0.3f, Drive = 2.4f } },
            { WeaponId.BastionBurst, new Synth.GunParams { Boom = 0.8f, BoomHz = 100, Body = 0.9f, BodyHz = 3000, BodyDecay = 0.055f, Crack = 1.2f, Tail = 0.7f, Mech = 0.4f, Drive = 2.6f } },
            { WeaponId.PumpShotgun, new Synth.GunParams { Boom = 1.4f, BoomHz = 75, Body = 1.2f, BodyHz = 2000, BodyDecay = 0.12f, Crack = 0.9f, Tail = 1.1f, Mech = 0.3f, Drive = 3f } },
            { WeaponId.AutoShotgun, new Synth.GunParams { Boom = 1.2f, BoomHz = 80, Body = 1.1f, BodyHz = 2200, BodyDecay = 0.1f, Crack = 0.9f, Tail = 0.9f, Mech = 0.3f, Drive = 3f } },
            { WeaponId.HeavyShotgun, new Synth.GunParams { Boom = 1.6f, BoomHz = 65, Body = 1.3f, BodyHz = 1800, BodyDecay = 0.14f, Crack = 1f, Tail = 1.2f, Mech = 0.3f, Drive = 3.4f } },
            { WeaponId.TacticalSniper, new Synth.GunParams { Boom = 1.2f, BoomHz = 80, Body = 1.1f, BodyHz = 2800, BodyDecay = 0.09f, Crack = 1.6f, Tail = 1.8f, Mech = 0.3f, Drive = 3f } },
            { WeaponId.HeavySniper, new Synth.GunParams { Boom = 1.8f, BoomHz = 55, Body = 1.3f, BodyHz = 2200, BodyDecay = 0.13f, Crack = 1.6f, Tail = 2.2f, Mech = 0.3f, Drive = 3.6f } },
            { WeaponId.SemiSniper, new Synth.GunParams { Boom = 1f, BoomHz = 90, Body = 1f, BodyHz = 3000, BodyDecay = 0.07f, Crack = 1.4f, Tail = 1.3f, Mech = 0.35f, Drive = 2.8f } },
            { WeaponId.LightMG, new Synth.GunParams { Boom = 0.85f, BoomHz = 100, Body = 1f, BodyHz = 2800, BodyDecay = 0.06f, Crack = 1.1f, Tail = 0.7f, Mech = 0.45f, Drive = 2.8f } },
            { WeaponId.HeavyMG, new Synth.GunParams { Boom = 1.4f, BoomHz = 70, Body = 1.2f, BodyHz = 2200, BodyDecay = 0.09f, Crack = 1.2f, Tail = 1f, Mech = 0.5f, Drive = 3.2f } },
            { WeaponId.Launcher, new Synth.GunParams { Boom = 1.4f, BoomHz = 60, Body = 0.6f, BodyHz = 900, BodyDecay = 0.1f, Crack = 0.2f, Tail = 0.6f, Mech = 0.6f, Drive = 1.8f } },
            { WeaponId.Smg, new Synth.GunParams { Boom = 0.45f, BoomHz = 125, Body = 0.75f, BodyHz = 3400, BodyDecay = 0.04f, Crack = 0.85f, Tail = 0.45f, Mech = 0.35f, Drive = 2.1f } },
            { WeaponId.RocketLauncher, new Synth.GunParams { Boom = 1.8f, BoomHz = 50, Body = 0.9f, BodyHz = 700, BodyDecay = 0.2f, Crack = 0.3f, Tail = 1.6f, Mech = 0.2f, Drive = 2f } },
        };

        static void StartGeneration()
        {
            _pending = new Dictionary<string, List<float[]>>();
            _gen = Task.Run(() =>
            {
                try
                {
                    var jobs = new List<(string key, Func<int, float[]> make, int variants)>();
                    foreach (var kv in GunDefs)
                    {
                        var g = kv.Value;
                        jobs.Add(("gun" + (int)kv.Key, s => Synth.Gunshot(g, s), 2));
                        var sup = g;
                        sup.Suppressed = true;
                        sup.Tail *= 0.4f;
                        jobs.Add(("gunS" + (int)kv.Key, s => Synth.Gunshot(sup, s + 50), 1));
                    }
                    jobs.Add(("gun" + (int)WeaponId.ArcRifle, s => Synth.Zap(s, 0.35f, 700), 2));
                    jobs.Add(("flame", s => Synth.FlameLoop(s), 1));
                    jobs.Add(("explosion", s => Synth.Explosion(s, 1f), 3));
                    jobs.Add(("zap", s => Synth.Zap(s, 0.25f, 400), 2));
                    jobs.Add(("dry", s => Synth.Click(s, 1800, 0.06f, 0.7f), 1));
                    jobs.Add(("magout", s => Synth.Rack(s, 0.05f, 900), 2));
                    jobs.Add(("magin", s => Synth.Rack(s, 0.07f, 1300), 2));
                    jobs.Add(("charge", s => Synth.Rack(s, 0.12f, 1100), 2));
                    jobs.Add(("rack", s => Synth.Rack(s, 0.16f, 700), 2));
                    jobs.Add(("bolt", s => Synth.Rack(s, 0.22f, 1500), 2));
                    jobs.Add(("shell", s => Synth.Click(s, 1200, 0.1f, 0.6f), 2));
                    jobs.Add(("equip", s => Synth.Rack(s, 0.09f, 800), 1));
                    jobs.Add(("pin", s => Synth.Click(s, 2600, 0.12f, 0.3f), 1));
                    jobs.Add(("throw", s => Synth.Thud(s, 120, 0.2f), 1));
                    jobs.Add(("clink", s => Synth.Click(s, 3000, 0.12f, 0.2f), 2));
                    jobs.Add(("step", s => Synth.Footstep(s), 5));
                    jobs.Add(("land", s => Synth.Thud(s, 70, 0.25f), 1));
                    jobs.Add(("flesh", s => Synth.Splat(s, 0.18f), 4));
                    jobs.Add(("splat", s => Synth.Splat(s, 0.5f), 2));
                    jobs.Add(("ricochet", s => Synth.Ricochet(s), 3));
                    jobs.Add(("thud", s => Synth.Thud(s, 55, 0.5f), 2));
                    jobs.Add(("glass", s => Synth.Glass(s), 1));
                    jobs.Add(("swoosh", s => { var b = Synth.Click(s, 300, 0.3f, 0.95f); return b; }, 2));
                    // Zombie voices.
                    jobs.Add(("growl", s => Synth.Voice(s, 1.1f + (s % 3) * 0.2f, 85 + (s % 4) * 12, 70, 0.7f, 0.25f, 3f, s % 3, (s + 1) % 4), 6));
                    jobs.Add(("alert", s => Synth.Voice(s + 100, 0.7f, 130 + (s % 3) * 15, 100, 0.5f, 0.3f, 3.5f, 0, 3, 0.02f), 3));
                    jobs.Add(("attack", s => Synth.Voice(s + 200, 0.45f, 150, 110, 0.4f, 0.35f, 4f, 3, 0, 0.01f), 4));
                    jobs.Add(("scream", s => Synth.Voice(s + 300, 0.9f, 260 + (s % 3) * 30, 200, 0.3f, 0.4f, 4f, 3, 0, 0.02f), 3));
                    jobs.Add(("death", s => Synth.Voice(s + 400, 1.0f, 110, 55, 0.8f, 0.3f, 2.5f, 1, 2), 4));
                    jobs.Add(("bloater", s => Synth.Voice(s + 500, 1.3f, 60, 45, 0.9f, 0.5f, 2f, 2, 1), 2));
                    jobs.Add(("roar", s => Synth.Voice(s + 600, 1.6f, 58, 45, 0.85f, 0.35f, 5f, 0, 4, 0.05f), 2));
                    jobs.Add(("shriek", s => Synth.Voice(s + 700, 0.7f, 380, 300, 0.2f, 0.45f, 4f, 3, 3, 0.01f), 2));
                    jobs.Add(("boss", s => Synth.Voice(s + 800, 2.2f, 42, 36, 0.9f, 0.3f, 6f, 4, 0, 0.08f), 2));
                    jobs.Add(("spit", s => Synth.Splat(s + 9, 0.35f), 1));
                    // Ambience.
                    jobs.Add(("rain", s => Synth.Rain(s, 6f), 1));
                    jobs.Add(("wind", s => Synth.Wind(s, 8f), 1));
                    jobs.Add(("drone", s => Synth.Drone(s, 10f), 1));
                    jobs.Add(("thunder", s => Synth.Thunder(s), 2));
                    // UI / feedback.
                    jobs.Add(("click", s => Synth.Tone(new[] { 1400f }, 0.03f, 0.02f, 0.1f), 1));
                    jobs.Add(("buy", s => Synth.Tone(new[] { 988f, 1319f, 1760f }, 0.06f, 0.12f, 0.3f), 1));
                    jobs.Add(("error", s => Synth.Tone(new[] { 180f, 150f }, 0.09f, 0.08f, 0.8f), 1));
                    jobs.Add(("levelup", s => Synth.Reverb(Synth.Tone(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.35f, 0.4f), 1f, 0.4f), 1));
                    jobs.Add(("clear", s => Synth.Reverb(Synth.Tone(new[] { 392f, 523f, 659f, 784f }, 0.12f, 0.6f, 0.5f), 1.5f, 0.45f), 1));
                    jobs.Add(("fail", s => Synth.Reverb(Synth.Tone(new[] { 330f, 262f, 196f }, 0.25f, 0.7f, 0.6f), 1.5f, 0.45f), 1));
                    jobs.Add(("wave", s => Synth.Reverb(Synth.Thud(s, 45, 1.2f), 1.5f, 0.4f), 1));
                    jobs.Add(("elite", s => Synth.Reverb(Synth.Tone(new[] { 147f, 139f }, 0.35f, 0.6f, 1f), 1.2f, 0.4f), 1));
                    jobs.Add(("hit", s => Synth.Tone(new[] { 2200f }, 0.02f, 0.015f, 0.2f), 1));
                    jobs.Add(("kill", s => Synth.Tone(new[] { 1500f, 1100f }, 0.03f, 0.04f, 0.4f), 1));
                    jobs.Add(("head", s => Synth.Tone(new[] { 2600f }, 0.02f, 0.12f, 0.6f), 1));
                    jobs.Add(("coin", s => Synth.Tone(new[] { 1568f, 2093f }, 0.04f, 0.08f, 0.3f), 1));
                    jobs.Add(("pickup", s => Synth.Tone(new[] { 880f, 1320f }, 0.05f, 0.1f, 0.2f), 1));
                    jobs.Add(("heart", s => Synth.Heartbeat(), 1));
                    jobs.Add(("hurt", s => Synth.Thud(s + 3, 90, 0.25f), 2));
                    // Music last (biggest).
                    jobs.Add(("mMenu", s => Synth.MenuMusic(), 1));
                    jobs.Add(("mCombat", s => Synth.CombatMusic(0), 1));
                    jobs.Add(("mBoss", s => Synth.CombatMusic(1), 1));
                    int done = 0;
                    var results = new List<float[]>[jobs.Count];
                    Parallel.For(0, jobs.Count, j =>
                    {
                        var list = new List<float[]>();
                        for (int v = 0; v < jobs[j].variants; v++) list.Add(jobs[j].make(v + 1));
                        results[j] = list;
                        lock (_pending) { done++; Progress = done / (float)jobs.Count; }
                    });
                    lock (_pending)
                        for (int j = 0; j < jobs.Count; j++) _pending[jobs[j].key] = results[j];
                }
                catch (Exception e)
                {
                    Debug.LogError("[UA] Audio synthesis failed: " + e);
                }
            });
        }

        void Update()
        {
            if (!Ready && _gen != null && _gen.IsCompleted)
            {
                lock (_pending)
                    foreach (var kv in _pending)
                    {
                        var arr = new AudioClip[kv.Value.Count];
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var data = kv.Value[i];
                            var c = AudioClip.Create(kv.Key + i, data.Length, 1, Synth.Rate, false);
                            c.SetData(data, 0);
                            arr[i] = c;
                        }
                        Clips[kv.Key] = arr;
                    }
                _pending = null;
                Ready = true;
                Progress = 1;
                if (_music != MusicKind.None) { var m = _music; _music = MusicKind.None; Music(m); }
            }
            ApplyVolumes();
            if (_flameT > 0)
            {
                _flameT -= Time.deltaTime;
                if (_flameT <= 0) _flame.Stop();
            }
        }

        void ApplyVolumes()
        {
            var s = SaveSystem.Data.Settings;
            float target = s.Music * 0.55f;
            float fade = Time.unscaledDeltaTime * 0.6f;
            var on = _musicOnA ? _musicA : _musicB;
            var off = _musicOnA ? _musicB : _musicA;
            on.volume = Mathf.MoveTowards(on.volume, target, fade);
            off.volume = Mathf.MoveTowards(off.volume, 0, fade);
            if (off.volume <= 0 && off.isPlaying) off.Stop();
            AudioListener.volume = 1;
        }

        static AudioClip Clip(string key)
        {
            if (!Ready || !Clips.TryGetValue(key, out var arr) || arr.Length == 0) return null;
            return arr[UnityEngine.Random.Range(0, arr.Length)];
        }

        bool Throttle(string key, float minGap)
        {
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(key, out var last) && now - last < minGap) return true;
            _lastPlay[key] = now;
            return false;
        }

        static float SfxVol => SaveSystem.Data.Settings.Sfx;

        public static void Play3D(string key, Vector3 pos, float vol = 1, float pitch = 1, float minGap = 0.03f, float maxDist = 60)
        {
            if (_i == null) return;
            var c = Clip(key);
            if (c == null || _i.Throttle(key + (int)(pos.x / 3) + (int)(pos.z / 3), minGap)) return;
            var s = _i._pool3d[_i._next3d];
            _i._next3d = (_i._next3d + 1) % _i._pool3d.Length;
            s.transform.position = pos;
            s.clip = c;
            s.volume = vol * SfxVol;
            s.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            s.maxDistance = maxDist;
            s.Play();
        }

        public static void Play2D(string key, float vol = 1, float pitch = 1, float minGap = 0.02f)
        {
            if (_i == null) return;
            var c = Clip(key);
            if (c == null || _i.Throttle(key, minGap)) return;
            var s = _i._pool2d[_i._next2d];
            _i._next2d = (_i._next2d + 1) % _i._pool2d.Length;
            s.clip = c;
            s.volume = vol * SfxVol;
            s.pitch = pitch;
            s.Play();
        }

        // --------------------------------------------------------------- music & ambience

        public static void Music(MusicKind k)
        {
            if (_i == null || k == _i._music) return;
            _i._music = k;
            if (!Ready) return;
            string key = k == MusicKind.Menu ? "mMenu" : k == MusicKind.Combat ? "mCombat" : k == MusicKind.Boss ? "mBoss" : null;
            _i._musicOnA = !_i._musicOnA;
            var src = _i._musicOnA ? _i._musicA : _i._musicB;
            if (key == null) return;
            src.clip = Clip(key);
            src.volume = 0;
            src.Play();
        }

        /// <summary>A generated music track (promo-video export); null until generation has finished.</summary>
        public static AudioClip MusicClip(MusicKind k) => Clip(k == MusicKind.Menu ? "mMenu" : k == MusicKind.Boss ? "mBoss" : "mCombat");

        public static void Ambience(Weather w, bool indoor)
        {
            if (_i == null) return;
            _i.StartCoroutine(_i.AmbienceWhenReady(w, indoor));
        }

        System.Collections.IEnumerator AmbienceWhenReady(Weather w, bool indoor)
        {
            while (!Ready) yield return null;
            var s = SaveSystem.Data.Settings;
            _amb.Stop();
            _amb2.Stop();
            if (indoor) { _amb.clip = Clip("drone"); _amb.volume = 0.5f * s.Sfx; _amb.Play(); }
            else { _amb.clip = Clip("wind"); _amb.volume = 0.35f * s.Sfx; _amb.Play(); }
            if (!indoor && (w == Weather.Rain || w == Weather.Storm)) { _amb2.clip = Clip("rain"); _amb2.volume = 0.55f * s.Sfx; _amb2.Play(); }
        }

        public static void StopAmbience()
        {
            if (_i == null) return;
            _i._amb.Stop();
            _i._amb2.Stop();
            _i._heart.Stop();
            _i._flame.Stop();
        }

        // --------------------------------------------------------------- game events

        public static void Gunshot(WeaponId id, bool suppressed)
        {
            string key = (suppressed && id != WeaponId.ArcRifle ? "gunS" : "gun") + (int)id;
            var w = Weapons.Get(id);
            Play2D(key, w.Class == WeaponClass.Pistol ? 0.7f : 0.85f, UnityEngine.Random.Range(0.96f, 1.04f), 0.01f);
        }

        public static void FlameLoop()
        {
            if (_i == null || !Ready) return;
            if (!_i._flame.isPlaying) { _i._flame.clip = Clip("flame"); _i._flame.volume = 0.7f * SfxVol; _i._flame.Play(); }
            _i._flameT = 0.15f;
        }

        public static void FlameStop()
        {
            if (_i != null) _i._flameT = Mathf.Min(_i._flameT, 0.05f);
        }

        public static void Heartbeat(float low)
        {
            if (_i == null || !Ready) return;
            var h = _i._heart;
            if (low > 0.05f)
            {
                if (!h.isPlaying) { h.clip = Clip("heart"); h.Play(); }
                h.volume = low * 0.8f * SfxVol;
                h.pitch = 0.9f + low * 0.4f;
            }
            else if (h.isPlaying) h.Stop();
        }

        public static void Explosion(Vector3 p, float radius) => Play3D("explosion", p, Mathf.Clamp(radius / 4f, 0.6f, 1.2f), UnityEngine.Random.Range(0.85f, 1.05f), 0.05f, 120);
        public static void Impact(Vector3 p, SurfaceKind k) => Play3D(k == SurfaceKind.Metal ? "ricochet" : k == SurfaceKind.Glass ? "glass" : k == SurfaceKind.Flesh ? "flesh" : "thud", p, k == SurfaceKind.Metal ? 0.5f : 0.35f, k == SurfaceKind.Concrete ? 2.2f : 1, 0.06f, 40);
        public static void Zap(Vector3 p) => Play3D("zap", p, 0.6f, 1, 0.1f, 25);
        public static void Clink(Vector3 p) => Play3D("clink", p, 0.4f, 1, 0.08f, 20);
        public static void Splat(Vector3 p) => Play3D("splat", p, 0.8f, 1, 0.05f, 30);
        public static void Thud(Vector3 p) => Play3D("thud", p, 1f, 0.7f, 0.05f, 60);
        public static void Swoosh(Vector3 p) => Play3D("swoosh", p, 0.8f, 0.7f, 0.05f, 25);
        public static void Spit(Vector3 p) => Play3D("spit", p, 0.8f, 1.3f, 0.05f, 30);

        public static void ZombieGrowl(Zombie z) => Play3D(z.Type == ZombieType.Runner ? "scream" : z.Type == ZombieType.Explosive ? "bloater" : z.Type == ZombieType.Hunter ? "shriek" : z.Type == ZombieType.Mutant ? "roar" : "growl", z.transform.position + Vector3.up * 1.5f, z.Type == ZombieType.Hunter ? 0.35f : 0.65f, 1f / Mathf.Sqrt(z.transform.localScale.x), 0.4f, 35);
        public static void ZombieAlert(Zombie z) => Play3D(z.Type == ZombieType.Runner ? "scream" : "alert", z.transform.position + Vector3.up * 1.5f, 0.8f, 1, 0.3f, 40);
        public static void ZombieAttack(Zombie z) => Play3D(z.Type == ZombieType.Hunter ? "shriek" : "attack", z.transform.position + Vector3.up * 1.5f, 0.8f, z.Type == ZombieType.Mutant ? 0.6f : 1f, 0.15f, 25);
        public static void ZombieDeath(Zombie z) => Play3D("death", z.transform.position + Vector3.up, 0.7f, 1f / Mathf.Sqrt(z.transform.localScale.x), 0.08f, 30);
        public static void Bloater(Zombie z) => Play3D("bloater", z.transform.position + Vector3.up, 1f, 1.2f, 0.2f, 30);
        public static void MutantRoar(Zombie z) => Play3D("roar", z.transform.position + Vector3.up * 2, 1f, 0.9f, 0.3f, 60);
        public static void BossRoar(Vector3 p) => Play3D("boss", p + Vector3.up * 2, 1f, 1f, 0.5f, 120);
        public static void BossGrowl(Vector3 p) => Play3D("roar", p + Vector3.up * 2, 0.8f, 0.6f, 0.5f, 80);
        public static void BossDeath(Vector3 p) => Play3D("boss", p + Vector3.up, 1f, 0.7f, 0.5f, 120);

        public static void DryFire() => Play2D("dry", 0.6f, 1, 0.15f);
        public static void ReloadStart(WeaponClass c) => Play2D("equip", 0.4f, 1.2f);
        public static void MagOut() => Play2D("magout", 0.7f);
        public static void MagIn() => Play2D("magin", 0.8f);
        public static void Charge(WeaponClass c) => Play2D("charge", 0.7f, c == WeaponClass.Pistol ? 1.3f : 1f);
        public static void Rack(bool bolt) => Play2D(bolt ? "bolt" : "rack", 0.8f);
        public static void ShellIn() => Play2D("shell", 0.6f, UnityEngine.Random.Range(0.95f, 1.05f));
        public static void Equip() => Play2D("equip", 0.6f);
        public static void Pin() => Play2D("pin", 0.6f);
        public static void Throw() => Play2D("throw", 0.6f, 1.4f);
        public static void Footstep() => Play2D("step", 0.25f, UnityEngine.Random.Range(0.9f, 1.1f), 0.1f);
        public static void Jump() => Play2D("step", 0.35f, 0.8f);
        public static void Land() => Play2D("land", 0.5f);
        public static void PlayerHurt(HurtKind k) => Play2D("hurt", 0.8f, k == HurtKind.Explosion ? 0.7f : 1f, 0.2f);
        public static void PlayerDeath() => Play2D("fail", 0.8f, 0.8f);
        public static void Hitmarker(bool kill, bool head) => Play2D(head ? "head" : kill ? "kill" : "hit", head ? 0.55f : kill ? 0.5f : 0.3f, 1, 0.03f);
        public static void Pickup() => Play2D("pickup", 0.6f);
        public static void Coin() => Play2D("coin", 0.5f, UnityEngine.Random.Range(0.95f, 1.1f), 0.05f);
        public static void Click() => Play2D("click", 0.5f, 1, 0.03f);
        public static void Buy() => Play2D("buy", 0.7f);
        public static void Error() => Play2D("error", 0.6f);
        public static void LevelUp() => Play2D("levelup", 0.8f);
        public static void StageClear() => Play2D("clear", 0.8f);
        public static void StageFail() => Play2D("fail", 0.8f);
        public static void WaveStart() => Play2D("wave", 0.9f);
        public static void EliteSting() => Play2D("elite", 0.9f);
        public static void Thunder() => Play2D("thunder", 0.9f, UnityEngine.Random.Range(0.85f, 1.1f), 1f);
    }
}
