using System;

namespace UA.Game
{
    /// <summary>Offline sound synthesis (thread-safe, pure math). All samples mono at <see cref="Rate"/>.</summary>
    public static class Synth
    {
        public const int Rate = 22050;
        const float TwoPi = (float)(Math.PI * 2);

        public sealed class Rand
        {
            uint _s;
            public Rand(int seed) { _s = (uint)(seed * 2654435761u + 12345); if (_s == 0) _s = 1; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / 16777215f; }
            public float Bi() => Next() * 2 - 1;
        }

        public static float[] Buf(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

        static float Exp(float t, float tau) => (float)Math.Exp(-t / Math.Max(1e-5f, tau));

        /// <summary>One-pole low-pass coefficient for a cutoff in Hz.</summary>
        static float Lp(float hz) => 1 - (float)Math.Exp(-TwoPi * hz / Rate);

        public static void Normalize(float[] b, float peak = 0.9f)
        {
            float m = 1e-6f;
            foreach (var v in b) m = Math.Max(m, Math.Abs(v));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        public static void Fade(float[] b, float inSec, float outSec)
        {
            int ni = (int)(inSec * Rate), no = (int)(outSec * Rate);
            for (int i = 0; i < ni && i < b.Length; i++) b[i] *= i / (float)ni;
            for (int i = 0; i < no && i < b.Length; i++) b[b.Length - 1 - i] *= i / (float)no;
        }

        public static void Drive(float[] b, float amount)
        {
            float norm = (float)Math.Tanh(amount);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * amount) / norm;
        }

        /// <summary>Small Schroeder-style reverb mixed in (comb + allpass network).</summary>
        public static float[] Reverb(float[] dry, float tail, float mix, float damp = 0.4f)
        {
            int extra = (int)(tail * Rate);
            var o = new float[dry.Length + extra];
            Array.Copy(dry, o, dry.Length);
            int[] combs = { 1116, 1188, 1277, 1356 };
            float fb = (float)Math.Pow(0.001, (combs[0] / (double)Rate) / Math.Max(0.1, tail));
            var wet = new float[o.Length];
            foreach (int d in combs)
            {
                var line = new float[d];
                int idx = 0;
                float store = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    float y = line[idx];
                    store = y * (1 - damp) + store * damp;
                    line[idx] = o[i] + store * fb;
                    idx = (idx + 1) % d;
                    wet[i] += y * 0.25f;
                }
            }
            int[] aps = { 556, 441, 341 };
            foreach (int d in aps)
            {
                var line = new float[d];
                int idx = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float bufout = line[idx];
                    float x = wet[i];
                    line[idx] = x + bufout * 0.5f;
                    wet[i] = bufout - x;
                    idx = (idx + 1) % d;
                }
            }
            for (int i = 0; i < o.Length; i++) o[i] = o[i] * (1 - mix * 0.5f) + wet[i] * mix;
            return o;
        }

        // ------------------------------------------------------------ gunshots

        public struct GunParams
        {
            public float Boom, BoomHz, Body, BodyHz, BodyDecay, Crack, Tail, Mech, Drive;
            public bool Suppressed;
        }

        public static float[] Gunshot(GunParams g, int seed)
        {
            var r = new Rand(seed);
            float len = 0.35f + g.BodyDecay * 3;
            var b = Buf(len);
            float lpBody = Lp(g.Suppressed ? Math.Min(g.BodyHz, 900) : g.BodyHz), lpHi = Lp(5000);
            float body = 0, hi = 0, phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                body += (n - body) * lpBody;
                hi += (n - hi) * lpHi;
                float crack = (n - hi) * Exp(t, 0.0035f) * g.Crack * (g.Suppressed ? 0.05f : 1f);
                float bod = body * Exp(t, g.BodyDecay) * g.Body * 3f;
                float f = g.BoomHz * (0.55f + 0.45f * Exp(t, 0.03f));
                phase += TwoPi * f / Rate;
                float boom = (float)Math.Sin(phase) * Exp(t, 0.07f + g.BodyDecay * 0.5f) * g.Boom;
                float mech = t < 0.012f ? r.Bi() * g.Mech * Exp(t, 0.003f) : 0;
                b[i] = crack + bod + boom + mech;
            }
            Drive(b, g.Drive);
            var o = g.Tail > 0 ? Reverb(b, g.Tail, g.Suppressed ? 0.15f : 0.45f, 0.5f) : b;
            Fade(o, 0, 0.05f);
            Normalize(o, g.Suppressed ? 0.6f : 0.95f);
            return o;
        }

        public static float[] Explosion(int seed, float size)
        {
            var r = new Rand(seed);
            var b = Buf(2.2f * size);
            float lp = 0, lp2 = 0, ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                lp += (n - lp) * Lp(900 * Exp(t, 0.4f) + 80);
                lp2 += (lp - lp2) * Lp(300);
                ph += TwoPi * (60 * Exp(t, 0.25f) + 28) / Rate;
                float crackle = r.Next() > 0.996f - t * 0.001f ? r.Bi() * Exp(t, 0.6f) * 0.6f : 0;
                b[i] = lp * Exp(t, 0.5f * size) * 2.2f + lp2 * Exp(t, 0.9f * size) * 2 + (float)Math.Sin(ph) * Exp(t, 0.35f) * 1.2f + crackle + (t < 0.02f ? r.Bi() * Exp(t, 0.005f) * 1.5f : 0);
            }
            Drive(b, 2.5f);
            var o = Reverb(b, 1.4f, 0.35f, 0.6f);
            Fade(o, 0, 0.3f);
            Normalize(o, 0.95f);
            return o;
        }

        /// <summary>Electric zap for the arc rifle and sparking boxes.</summary>
        public static float[] Zap(int seed, float len, float pitch)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float ph = 0, mph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = pitch * (1.5f - t / len);
                mph += TwoPi * f * 3.1f / Rate;
                ph += TwoPi * (f + (float)Math.Sin(mph) * f * 2) / Rate;
                float crackle = r.Next() > 0.9f ? r.Bi() * 0.6f : 0;
                b[i] = ((float)Math.Sin(ph) * 0.7f + crackle) * Exp(t, len * 0.4f);
            }
            Drive(b, 2f);
            Normalize(b, 0.8f);
            return b;
        }

        public static float[] FlameLoop(int seed)
        {
            var r = new Rand(seed);
            var b = Buf(1.5f);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                lp += (n - lp) * Lp(1400);
                lp2 += (lp - lp2) * Lp(400);
                float lfo = 0.7f + 0.3f * (float)Math.Sin(t * TwoPi * 6.7f);
                float crackle = r.Next() > 0.985f ? r.Bi() * 0.5f : 0;
                b[i] = (lp * 1.5f + lp2 * 2f) * lfo + crackle;
            }
            LoopSeam(b, 0.2f);
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Makes a buffer loop seamlessly by crossfading its end into its start.</summary>
        public static void LoopSeam(float[] b, float sec)
        {
            int n = Math.Min(b.Length / 3, (int)(sec * Rate));
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                b[i] = b[i] * k + b[b.Length - n + i] * (1 - k);
            }
            Array.Resize(ref b, b.Length);
        }

        // ------------------------------------------------------------ foley

        public static float[] Click(int seed, float hz, float len, float noise = 0.6f)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float ph = 0, hp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                hp += (n - hp) * Lp(2000);
                ph += TwoPi * hz / Rate;
                b[i] = ((n - hp) * noise + (float)Math.Sin(ph) * (1 - noise)) * Exp(t, len * 0.18f);
            }
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Two-part mechanical action (e.g. slide back + forward, pump rack).</summary>
        public static float[] Rack(int seed, float gap, float pitch)
        {
            var a = Click(seed, pitch, 0.08f, 0.8f);
            var c = Click(seed + 1, pitch * 1.3f, 0.07f, 0.7f);
            var b = Buf(gap + 0.12f);
            Mix(b, a, 0, 0.9f);
            Mix(b, c, (int)(gap * Rate), 1f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += (float)Math.Sin(t * TwoPi * 90) * Exp(t, 0.05f) * 0.15f;
            }
            Normalize(b, 0.8f);
            return b;
        }

        public static void Mix(float[] dst, float[] src, int at, float gain)
        {
            for (int i = 0; i < src.Length && at + i < dst.Length; i++) if (at + i >= 0) dst[at + i] += src[i] * gain;
        }

        public static float[] Thud(int seed, float hz, float len)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float ph = 0, lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += TwoPi * hz * (0.6f + 0.4f * Exp(t, 0.02f)) / Rate;
                lp += (r.Bi() - lp) * Lp(500);
                b[i] = ((float)Math.Sin(ph) + lp * 1.5f) * Exp(t, len * 0.25f);
            }
            Normalize(b, 0.85f);
            return b;
        }

        public static float[] Footstep(int seed)
        {
            var r = new Rand(seed);
            var b = Buf(0.16f);
            float lp = 0, bp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                lp += (n - lp) * Lp(900);
                bp += (lp - bp) * Lp(160);
                float grit = (n - lp) * 0.25f * Exp(t, 0.02f);
                b[i] = (lp - bp) * Exp(t, 0.035f) * 2 + grit + (float)Math.Sin(t * TwoPi * 70) * Exp(t, 0.03f) * 0.5f;
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Wet flesh impact.</summary>
        public static float[] Splat(int seed, float len)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (r.Bi() - lp) * Lp(1600 * Exp(t, 0.05f) + 200);
                float bubble = (float)Math.Sin(t * TwoPi * (300 + 200 * (float)Math.Sin(t * 90))) * Exp(t, 0.06f) * 0.3f;
                b[i] = lp * Exp(t, len * 0.3f) * 2 + bubble;
            }
            Normalize(b, 0.8f);
            return b;
        }

        public static float[] Ricochet(int seed)
        {
            var r = new Rand(seed);
            var b = Buf(0.35f);
            float ph = 0, hp = 0;
            float f0 = 2200 + r.Next() * 1600;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = r.Bi();
                hp += (n - hp) * Lp(3000);
                ph += TwoPi * f0 * (1 - t * 1.2f) / Rate;
                b[i] = (n - hp) * Exp(t, 0.01f) + (float)Math.Sin(ph) * Exp(t, 0.09f) * 0.35f;
            }
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Glass(int seed)
        {
            var r = new Rand(seed);
            var b = Buf(0.9f);
            for (int k = 0; k < 18; k++)
            {
                int at = (int)(r.Next() * 0.4f * Rate);
                float f = 2500 + r.Next() * 4500;
                for (int i = 0; i < 2500 && at + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[at + i] += (float)Math.Sin(t * TwoPi * f) * Exp(t, 0.02f + r.Next() * 0.02f) * 0.4f;
                }
            }
            Normalize(b, 0.7f);
            return b;
        }

        // ------------------------------------------------------------ voices

        /// <summary>
        /// Formant-synthesised zombie vocal: a jittery glottal saw with vocal fry, shaped by three
        /// moving formant resonators, breath noise and distortion.
        /// </summary>
        public static float[] Voice(int seed, float len, float f0, float f0End, float fry, float breath, float drive, float vowel, float vowelEnd, float attack = 0.05f)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float ph = 0, jitter = 0;
            // Formant resonator states (2-pole bandpass).
            var y1 = new float[3];
            var y2 = new float[3];
            float[,] vowels =
            {
                { 750, 1150, 2500 },  // ah
                { 550, 900, 2400 },   // oh
                { 350, 650, 2300 },   // oo
                { 500, 1700, 2500 },  // eh
                { 650, 1050, 2900 },  // aw
            };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                if (i % 220 == 0) jitter = r.Bi() * 0.08f;
                float f = (f0 + (f0End - f0) * k) * (1 + jitter);
                ph += f / Rate;
                if (ph > 1) ph -= 1;
                float saw = ph * 2 - 1;
                // Vocal fry: amplitude pulses.
                float fryMod = 1 - fry * (0.5f + 0.5f * (float)Math.Sin(t * TwoPi * (28 + 10 * jitter)));
                float src = saw * fryMod + r.Bi() * breath;
                float v = vowel + (vowelEnd - vowel) * k;
                int vi = Math.Min(3, (int)v);
                float vf = v - vi;
                float outv = 0;
                for (int fI = 0; fI < 3; fI++)
                {
                    float fc = vowels[vi, fI] * (1 - vf) + vowels[vi + 1, fI] * vf;
                    fc *= 0.85f + 0.1f * (float)Math.Sin(t * 3 + fI);
                    float bw = 80 + fI * 60;
                    float rr = (float)Math.Exp(-Math.PI * bw / Rate);
                    float a1 = -2 * rr * (float)Math.Cos(TwoPi * fc / Rate), a2 = rr * rr;
                    float y = src * (1 - rr) - a1 * y1[fI] - a2 * y2[fI];
                    y2[fI] = y1[fI];
                    y1[fI] = y;
                    outv += y * (fI == 0 ? 1f : fI == 1 ? 0.7f : 0.35f);
                }
                float env = Math.Min(1, t / attack) * (k > 0.7f ? (1 - k) / 0.3f : 1);
                b[i] = outv * env;
            }
            Drive(b, drive);
            Fade(b, 0.005f, 0.05f);
            Normalize(b, 0.85f);
            return b;
        }

        // ------------------------------------------------------------ ambience

        public static float[] Rain(int seed, float len)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float lp = 0, hp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float n = r.Bi();
                lp += (n - lp) * Lp(5000);
                hp += (lp - hp) * Lp(400);
                float drop = r.Next() > 0.9985f ? r.Bi() * 0.8f : 0;
                b[i] = (lp - hp) * 0.6f + drop;
            }
            LoopSeam(b, 0.4f);
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] Wind(int seed, float len)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.5f + 0.5f * (float)Math.Sin(t * TwoPi * 0.13f) * (float)Math.Sin(t * TwoPi * 0.07f + 1);
                lp += (r.Bi() - lp) * Lp(200 + 500 * gust);
                b[i] = lp * (0.4f + gust);
            }
            LoopSeam(b, 0.8f);
            Normalize(b, 0.5f);
            return b;
        }

        /// <summary>Eerie indoor drone with distant drips and creaks.</summary>
        public static float[] Drone(int seed, float len)
        {
            var r = new Rand(seed);
            var b = Buf(len);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (r.Bi() - lp) * Lp(120);
                float hum = (float)Math.Sin(t * TwoPi * 50) * 0.08f + (float)Math.Sin(t * TwoPi * 100.5f) * 0.04f;
                float pad = (float)Math.Sin(t * TwoPi * 55 + Math.Sin(t * 0.3) * 2) * 0.12f * (0.6f + 0.4f * (float)Math.Sin(t * 0.21f));
                b[i] = lp * 1.2f + hum + pad;
            }
            for (int k = 0; k < (int)(len / 2.5f); k++)
            {
                int at = (int)(r.Next() * (len - 0.4f) * Rate);
                float f = 900 + r.Next() * 900;
                for (int i = 0; i < 4000 && at + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[at + i] += (float)Math.Sin(t * TwoPi * f * (1 - t * 1.5f)) * Exp(t, 0.03f) * 0.25f;
                }
            }
            var o = Reverb(b, 1.5f, 0.4f, 0.5f);
            Array.Resize(ref o, b.Length);
            LoopSeam(o, 1f);
            Normalize(o, 0.5f);
            return o;
        }

        public static float[] Thunder(int seed)
        {
            var r = new Rand(seed);
            var b = Buf(4.5f);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (r.Bi() - lp) * Lp(600 * Exp(t, 0.3f) + 60);
                lp2 += (lp - lp2) * Lp(120);
                float rumble = 0.5f + 0.5f * (float)Math.Sin(t * 7 + Math.Sin(t * 2.3) * 3);
                b[i] = (lp * Exp(t, 0.25f) * 2 + lp2 * 3 * rumble) * Exp(t, 1.6f);
            }
            Drive(b, 1.8f);
            Normalize(b, 0.9f);
            return b;
        }

        // ------------------------------------------------------------ UI

        public static float[] Tone(float[] freqs, float noteLen, float decay, float harm = 0.3f, float gap = 0)
        {
            var b = Buf(freqs.Length * (noteLen + gap) + decay * 3);
            for (int n = 0; n < freqs.Length; n++)
            {
                int at = (int)(n * (noteLen + gap) * Rate);
                float f = freqs[n];
                for (int i = 0; at + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float e = Exp(t, decay);
                    if (e < 0.001f) break;
                    b[at + i] += ((float)Math.Sin(t * TwoPi * f) + (float)Math.Sin(t * TwoPi * f * 2) * harm + (float)Math.Sin(t * TwoPi * f * 3) * harm * 0.4f) * e * Math.Min(1, t * 400);
                }
            }
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Heartbeat()
        {
            var b = Buf(0.9f);
            foreach (var (at, g) in new[] { (0f, 1f), (0.22f, 0.7f) })
            {
                int s = (int)(at * Rate);
                for (int i = 0; s + i < b.Length && i < 5000; i++)
                {
                    float t = i / (float)Rate;
                    b[s + i] += (float)Math.Sin(t * TwoPi * (55 - t * 60)) * Exp(t, 0.05f) * g;
                }
            }
            Normalize(b, 0.8f);
            return b;
        }

        // ------------------------------------------------------------ music

        static float Note(int semis) => 220f * (float)Math.Pow(2, semis / 12.0);   // relative to A3

        /// <summary>Dark ambient menu theme: detuned pad chords, a sparse piano-like motif and a sub drone.</summary>
        public static float[] MenuMusic()
        {
            float bpm = 68, beat = 60 / bpm;
            int bars = 16;
            float len = bars * 4 * beat;
            var b = Buf(len);
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -5, -1, 2 } };   // Am, F, G, Em
            var r = new Rand(7);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                int bar = (int)(t / (4 * beat));
                var ch = chords[bar % 4];
                float pad = 0;
                foreach (int s in ch)
                {
                    float f = Note(s - 12);
                    for (int d = -1; d <= 1; d++) pad += Saw(t * f * (1 + d * 0.004f)) * 0.06f;
                }
                lp += (pad - lp) * Lp(500 + 300 * (float)Math.Sin(t * 0.3f));
                float sub = (float)Math.Sin(t * TwoPi * Note(ch[0] - 24)) * 0.18f;
                b[i] = lp + sub;
            }
            // Piano-ish motif: notes from the chord on off-beats.
            int[] motif = { 12, 15, 19, 15, 12, 10, 7, 10, 12, 15, 17, 15, 14, 12, 10, 7 };
            for (int n = 0; n < bars * 2; n++)
            {
                if (r.Next() < 0.25f) continue;
                float at = n * 2 * beat + beat;
                int bar = (int)(at / (4 * beat));
                int s = motif[n % motif.Length] + chords[bar % 4][0];
                float f = Note(s - 12);
                int st = (int)(at * Rate);
                for (int i = 0; st + i < b.Length && i < Rate * 3; i++)
                {
                    float t = i / (float)Rate;
                    float e = Exp(t, 0.9f) * Math.Min(1, t * 300);
                    b[st + i] += ((float)Math.Sin(t * TwoPi * f) + (float)Math.Sin(t * TwoPi * f * 2) * 0.35f + (float)Math.Sin(t * TwoPi * f * 3.01f) * 0.12f) * e * 0.16f;
                }
            }
            var o = Reverb(b, 2.5f, 0.5f, 0.45f);
            Array.Resize(ref o, b.Length);
            LoopSeam(o, 1.5f);
            Normalize(o, 0.6f);
            return o;
        }

        static float Saw(float x) => 2 * (x - (float)Math.Floor(x + 0.5f));

        /// <summary>Combat loop: driving drums, a gritty bass ostinato and a tense pad. intensity 0 = combat, 1 = boss.</summary>
        public static float[] CombatMusic(float intensity)
        {
            float bpm = 100 + intensity * 20, beat = 60 / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            var b = Buf(len);
            var r = new Rand(11 + (int)(intensity * 10));
            int[] bass = { 0, 0, 3, 0, -2, 0, 5, 3, 0, 0, 3, 0, -2, -4, -5, -2 };
            float bl = 0, padLp = 0;
            int steps = bars * 16;
            float step = beat / 4;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                int s16 = (int)(t / step);
                float ts = t - s16 * step;
                // Bass: 8th notes.
                int bi = (s16 / 2) % bass.Length;
                float bf = Note(bass[bi] - 24);
                float bassv = Saw(t * bf) * Exp(ts + (s16 % 2) * step, 0.25f);
                bl += (bassv - bl) * Lp(300 + 900 * intensity);
                // Pad.
                float pad = Saw(t * Note(-12) * 1.002f) * 0.04f + Saw(t * Note(-9)) * 0.04f + Saw(t * Note(-5) * 0.998f) * 0.03f;
                padLp += (pad - padLp) * Lp(800);
                b[i] = bl * 0.35f + padLp;
            }
            // Drums.
            for (int s = 0; s < steps; s++)
            {
                float at = s * step;
                int pos = s % 16;
                bool kick = pos == 0 || pos == 8 || pos == 10 || (intensity > 0.5f && pos == 6);
                bool snare = pos == 4 || pos == 12;
                bool hat = s % 2 == 0;
                bool tom = intensity > 0.5f && (s % 64) >= 60;
                int st = (int)(at * Rate);
                if (kick)
                    for (int i = 0; st + i < b.Length && i < 8000; i++)
                    {
                        float t = i / (float)Rate;
                        b[st + i] += (float)Math.Sin(TwoPi * (45 * t + 90 * 0.04f * (1 - Exp(t, 0.04f)))) * Exp(t, 0.18f) * 0.8f;
                    }
                if (snare)
                    for (int i = 0; st + i < b.Length && i < 6000; i++)
                    {
                        float t = i / (float)Rate;
                        b[st + i] += (r.Bi() * 0.5f + (float)Math.Sin(t * TwoPi * 190) * 0.4f) * Exp(t, 0.08f) * 0.45f;
                    }
                if (hat)
                {
                    float hp = 0;
                    for (int i = 0; st + i < b.Length && i < 1500; i++)
                    {
                        float t = i / (float)Rate;
                        float n = r.Bi();
                        hp += (n - hp) * 0.5f;
                        b[st + i] += (n - hp) * Exp(t, 0.015f) * 0.12f;
                    }
                }
                if (tom)
                    for (int i = 0; st + i < b.Length && i < 6000; i++)
                    {
                        float t = i / (float)Rate;
                        b[st + i] += (float)Math.Sin(t * TwoPi * (110 - (s % 4) * 15)) * Exp(t, 0.12f) * 0.5f;
                    }
            }
            // Boss stabs.
            if (intensity > 0.5f)
                for (int bar = 0; bar < bars; bar++)
                {
                    int st = (int)(bar * 4 * beat * Rate);
                    float sl = 0;
                    for (int i = 0; st + i < b.Length && i < (int)(beat * 2 * Rate); i++)
                    {
                        float t = i / (float)Rate;
                        float v = (Saw(t * Note(-12)) + Saw(t * Note(-9) * 1.003f) + Saw(t * Note(-5))) * Exp(t, 0.35f);
                        sl += (v - sl) * Lp(600 + 2000 * Exp(t, 0.1f));
                        b[st + i] += sl * 0.12f;
                    }
                }
            Drive(b, 1.3f);
            var o = Reverb(b, 1.2f, 0.2f, 0.5f);
            Array.Resize(ref o, b.Length);
            Normalize(o, 0.6f);
            return o;
        }
    }
}
