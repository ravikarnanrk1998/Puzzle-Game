using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Computer rider: picks a line (inside of bends, around obstacles and riders, onto
    /// kickers), brakes for corners, preloads jumps, tricks with skill-based timing, spins,
    /// fights neighbours and fires boost on open trail. Also drives the player in autopilot tests.
    /// </summary>
    public sealed class RiderAI
    {
        readonly Rider _r;
        readonly System.Random _rnd;
        public float Skill, Aggro, Style;
        float _lineOffset, _think, _lineX, _trickUntil, _boostChance;
        bool _spinning, _wantTrick;
        int _spinDir;
        Trick _trick;
        bool _trickPressed;

        public RiderAI(Rider r, float skill, int seed)
        {
            _r = r;
            _rnd = new System.Random(seed);
            Skill = Mathf.Clamp01(skill);
            Aggro = 0.25f + (float)_rnd.NextDouble() * 0.6f + (r.Def.StrengthBonus > 0 ? 0.2f : 0);
            Style = 0.3f + (float)_rnd.NextDouble() * 0.5f + (r.Def.TrickBonus > 1 ? 0.25f : 0);
            _lineOffset = (float)(_rnd.NextDouble() * 2 - 1) * 1.8f;
            _boostChance = 0.3f + (float)_rnd.NextDouble() * 0.5f;
        }

        float Rand => (float)_rnd.NextDouble();

        public RiderInput Think(float dt, IReadOnlyList<Rider> riders, Rider player)
        {
            var r = _r;
            var tr = r.Tr;
            var inp = new RiderInput();
            if (r.Crashed || !r.Started) return inp;

            if (!r.Grounded) return Air(dt, inp);
            _spinning = false;
            _wantTrick = false;
            _trickPressed = false;

            float s = r.S, half = r.Half;
            float look = 10 + r.V * 0.9f;

            // Line decisions refresh with a reaction time that shrinks with skill.
            _think -= dt;
            if (_think <= 0)
            {
                _think = Mathf.Lerp(0.42f, 0.12f, Skill) * (0.7f + Rand * 0.6f);
                if (Rand < 0.08f) _lineOffset = (Rand * 2 - 1) * 1.8f;
                float kAhead = 0;
                for (int i = 1; i <= 5; i++) kAhead += tr.CurvAt(s + 5 + i * 4);
                kAhead /= 5;
                float line = Mathf.Clamp(kAhead * 110, -(half - 1.6f), half - 1.6f) + _lineOffset;

                // Kickers: stylish riders line up with the ramp.
                var ramp = tr.NextRamp(s, look + 12);
                if (ramp != null && ramp.S0 > s + 2 && Style > 0.35f) line = Mathf.Clamp(line, ramp.X0 + 0.9f, ramp.X1 - 0.9f);

                // Obstacles: move to the roomier side.
                foreach (var o in tr.Obstacles)
                {
                    if (o.S < s || o.S > s + look + 8) continue;
                    float gap = o.HalfX + 1.35f;
                    if (Mathf.Abs(line - o.X) > gap) continue;
                    float left = o.X - gap, right = o.X + gap;
                    bool leftOk = left > -half + 0.7f, rightOk = right < half - 0.7f;
                    if (leftOk && (!rightOk || Mathf.Abs(r.X - left) < Mathf.Abs(r.X - right))) line = left;
                    else if (rightOk) line = right;
                }

                // Riders just ahead: pass beside them; the aggressive ones close in to fight.
                foreach (var o in riders)
                {
                    if (o == r || o.Crashed) continue;
                    float ds = o.S - s, dx = o.X - line;
                    if (ds > 0 && ds < 7 && Mathf.Abs(dx) < 1.2f) line = o.X + (dx > 0 ? -1.7f : 1.7f);
                    bool target = o == player ? Aggro > 0.55f : Aggro > 0.75f;
                    if (target && ds > -3 && ds < 9 && Mathf.Abs(o.X - r.X) < 4) line = o.X + (r.X > o.X ? 1.35f : -1.35f);
                }
                _lineX = Mathf.Clamp(line, -half + 0.8f, half - 0.8f);
            }

            // Steering towards the line, countering drift.
            float desired = Mathf.Atan2(_lineX - r.X, Mathf.Max(5, r.V * 0.75f));
            float steer = desired / Rider.MaxYaw * 1.5f - r.DriftV * 0.12f;
            inp.Steer = Mathf.Clamp(steer, -1, 1);

            // Speed for the bends ahead.
            float safe = 40;
            for (float q = 0; q < 10 + r.V * 1.6f; q += 3) safe = Mathf.Min(safe, tr.SafeAt(s + q) + q * 0.25f);
            safe *= 0.86f + 0.16f * Skill;
            inp.C = r.V > safe + 0.4f;

            // Jumps: hold to preload just before the lip, so the take-off pops.
            var next = tr.NextRamp(s, 12);
            bool onRamp = next != null && r.X > next.X0 && r.X < next.X1;
            if (onRamp && next.Lip - s < r.V * 0.45f + 1.5f) inp.B = true;
            // Hop logs when skilled.
            foreach (var o in tr.Obstacles)
            {
                if (o.Kind != ObstacleKind.Log || o.S < s) continue;
                float ds = o.S - s;
                if (ds < 6 && Mathf.Abs(o.X - r.X) < o.HalfX + 0.6f && Skill > 0.5f) inp.B = ds > 2.2f;
            }

            // Punch a neighbour.
            foreach (var o in riders)
            {
                if (o == r || o.Crashed || !o.Grounded) continue;
                if (Mathf.Abs(o.S - s) < 2.2f && Mathf.Abs(o.X - r.X) < 2.2f && r.PunchCooldown <= 0)
                {
                    float chance = Aggro * (o == player ? 1.1f : 1.0f) * dt;
                    if (Rand < chance)
                    {
                        inp.ADown = true;
                        inp.A = true;
                    }
                }
            }

            // Boost on an open stretch.
            if (r.Adrenaline >= 25 && r.BoostTime <= 0 && safe > r.V + 5 && Rand < _boostChance * dt) inp.BoostDown = true;
            return inp;
        }

        RiderInput Air(float dt, RiderInput inp)
        {
            var r = _r;
            float land = r.TimeToLand();
            float margin = Mathf.Lerp(0.42f, 0.22f, Skill);
            if (r.AirTime < 0.05f)
            {
                _wantTrick = land > 0.55f && Rand < Style * 0.9f;
                _spinning = land > 1.25f && Skill > 0.55f && Rand < Style * 0.5f;
                _spinDir = Rand < 0.5f ? -1 : 1;
                _trick = (Trick)(1 + _rnd.Next(3));
                _trickPressed = false;
                _trickUntil = 0;
            }
            if (_spinning)
            {
                // A clean 360: stop steering once the remaining turn will finish on its own.
                float done = Mathf.Abs(r.Spin);
                if (done < Mathf.PI * 2 - 0.35f && land > 0.12f) inp.Steer = _spinDir;
                else _spinning = false;
            }
            if (_wantTrick && !_spinning)
            {
                bool hold = land > margin + 0.12f;
                if (!_trickPressed && hold)
                {
                    _trickPressed = true;
                    if (_trick == Trick.Superman) inp.ADown = true;
                    else if (_trick == Trick.NoHander) inp.BDown = true;
                    else inp.CDown = true;
                }
                if (hold)
                {
                    inp.A = _trick == Trick.Superman;
                    inp.B = _trick == Trick.NoHander;
                    inp.C = _trick == Trick.CanCan;
                }
                // Occasional misjudged hold for low skill riders.
                if (!hold && Skill < 0.5f && Rand < 0.02f) _trickUntil = 0.12f;
                if (_trickUntil > 0)
                {
                    _trickUntil -= dt;
                    inp.A = _trick == Trick.Superman;
                    inp.B = _trick == Trick.NoHander;
                    inp.C = _trick == Trick.CanCan;
                }
            }
            return inp;
        }
    }
}
