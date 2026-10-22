using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    public enum Trick { None, Superman, NoHander, CanCan }

    /// <summary>
    /// One frame of controls. A / B / C are the three action buttons: on the ground they are
    /// PUNCH / JUMP (hold to preload, release to hop) / BRAKE; in the air they start the
    /// SUPERMAN / NO-HANDER / CAN-CAN tricks. Steering in the air spins the bike.
    /// </summary>
    public struct RiderInput
    {
        public float Steer;
        public bool A, B, C, ADown, BDown, CDown, BoostDown;
    }

    public enum RiderEventKind { Crash, Respawn, Hop, Pop, Takeoff, Land, TrickLanded, Punch, Hit, Takedown, Boost, Orb, Finish, Barrier, HardLanding }

    public struct RiderEvent
    {
        public RiderEventKind Kind;
        public Rider Rider, Other;
        public string Text;
        public int Points;
    }

    /// <summary>
    /// Rider + bike simulation in track space (see <see cref="Track"/>): speed from gravity,
    /// pedalling and drag; steering with cornering grip and drift; ground following that
    /// launches off lips and crests; air tricks, spins and landings; punches; boost; crashes.
    /// </summary>
    public sealed class Rider
    {
        public const float Gravity = 9.81f * 1.12f;
        public const float MaxYaw = 0.5f;
        public const float SpinRate = 7.2f;

        public readonly RiderDef Def;
        public readonly BikeDef Bike;
        public readonly Color Paint;
        public readonly bool IsPlayer;
        public readonly int Slot;
        public readonly Track Tr;
        public readonly List<RiderEvent> Events;

        // Stats.
        public readonly float TopSpeed, PedalForce, PedalMax, Grip, SteerRate, Power;
        public float SpeedMul = 1;

        // Motion.
        public float S, X, Y, Vy, V, Yaw, DriftV, Spin;
        public bool Grounded = true;
        public float AirTime, Pitch, Lean, PedalPhase, Pedalling, Speed01;

        // Combat, boost, health.
        public float Health = 100, Adrenaline, BoostTime, Invuln, HitFlash, SinceHit = 10;
        public float PunchTimer = -1, PunchCooldown;
        public int PunchSide = 1;
        public bool PunchStrikeNow;

        // Jumping and tricks.
        public float Preload;
        public Trick CurTrick;
        public float TrickBlend;
        bool _trickHeld;
        float _trickPts;
        readonly List<string> _combo = new List<string>();
        bool _prevB;

        // Crash.
        public bool Crashed;
        public float CrashTime;
        public Vector3 BodyPos, BodyVel, BodySpin, BikePos, BikeVel, BikeSpin;
        public Quaternion BodyRot, BikeRot;

        // Race.
        public bool Finished;
        public float FinishTime;
        public int Takedowns, TrickScore, Crashes, Place;
        public bool Started;

        public Rider(RiderDef def, BikeDef bike, Color paint, bool player, int slot, Track track, List<RiderEvent> events)
        {
            Def = def;
            Bike = bike;
            Paint = paint;
            IsPlayer = player;
            Slot = slot;
            Tr = track;
            Events = events;
            int spd = bike.Speed + def.SpeedBonus, acc = bike.Accel + def.AccelBonus, hdl = bike.Handling + def.HandlingBonus, str = bike.Strength + def.StrengthBonus;
            TopSpeed = 15.6f + spd * 0.62f;
            PedalForce = 1.5f + acc * 0.2f;
            PedalMax = 10.5f + acc * 0.25f;
            Grip = (7.2f + hdl * 0.42f) * track.Theme.Grip;
            SteerRate = 2.8f + hdl * 0.16f;
            Power = 0.72f + str * 0.055f;
        }

        public float Half => Tr.WidthAt(S) * 0.5f;
        public float Barrier => Half + 2.35f;
        public bool Boosting => BoostTime > 0;
        public bool CanAct => !Crashed && Started && !Finished;
        public float Kmh => V * 3.6f;
        public string Name => Def.Name;

        void Emit(RiderEventKind k, string text = null, int pts = 0, Rider other = null) =>
            Events.Add(new RiderEvent { Kind = k, Rider = this, Other = other, Text = text, Points = pts });

        public void PlaceAt(float s, float x)
        {
            S = s;
            X = x;
            Y = Tr.GroundY(s, x);
            V = 0;
            Vy = 0;
            Grounded = true;
        }

        // ------------------------------------------------------------ step

        public void Step(float dt, RiderInput inp)
        {
            PunchStrikeNow = false;
            if (Crashed)
            {
                CrashStep(dt);
                return;
            }
            Invuln -= dt;
            PunchCooldown -= dt;
            HitFlash = Mathf.Max(0, HitFlash - dt);
            SinceHit += dt;
            if (SinceHit > 2.0f) Health = Mathf.Min(100, Health + 16 * dt);
            if (BoostTime > 0) BoostTime -= dt;

            if (!Started)
            {
                Y = Tr.GroundY(S, X);
                Preload = Mathf.MoveTowards(Preload, inp.B ? 1 : 0, dt * 3);
                return;
            }
            if (Finished)
            {
                // Coast and brake to a stop in the run-out.
                inp = new RiderInput { Steer = Mathf.Clamp(-X * 0.15f, -1, 1), C = V > 4 };
            }

            float k = Tr.CurvAt(S);
            float grade = Tr.GradeAt(S);
            bool offTrail = Mathf.Abs(X) > Half;

            // Speed.
            float top = TopSpeed * SpeedMul + (Boosting ? 5.5f : 0);
            float a = 0;
            if (Grounded)
            {
                a += 9.81f * grade * 0.95f;
                bool braking = inp.C && CanAct || (Finished && V > 4);
                Pedalling = !braking && V < PedalMax ? 1 : 0;
                if (Pedalling > 0) a += PedalForce * (1 - V / PedalMax);
                if (braking) a -= 7f;
                if (offTrail) a -= 2.4f;
                a -= 0.22f;
            }
            else Pedalling = 0;
            float cd = 9.81f * 0.135f * 0.95f / (TopSpeed * TopSpeed);
            a -= cd * V * V;
            if (V > top) a -= (V - top) * 1.3f;
            if (Boosting) a += 4.5f;
            V = Mathf.Max(0, V + a * dt);
            if (Finished && V < 0.3f) V = 0;
            Speed01 = Mathf.Clamp01(V / 22f);

            // Steering: relative yaw on the ground, spins in the air.
            if (Grounded)
            {
                // Less steering lock at speed keeps fast sections controllable.
                float target = (CanAct || Finished ? inp.Steer : 0) * MaxYaw * (1 - 0.35f * Speed01);
                if (PunchTimer >= 0) target *= 0.6f;
                Yaw = Mathf.MoveTowards(Yaw, target, SteerRate * dt);
                // Cornering grip: too fast for the bend and the bike drifts wide.
                float bankHelp = Mathf.Abs(Tr.BankAt(S)) * 9.81f * 1.4f;
                float need = V * V * Mathf.Abs(k);
                float excess = need - (Grip + bankHelp);
                if (excess > 0)
                {
                    DriftV += -Mathf.Sign(k) * excess * dt * 1.1f;
                    V -= excess * 0.12f * dt;
                }
                else DriftV = Mathf.MoveTowards(DriftV, 0, 7 * dt);
                DriftV = Mathf.Clamp(DriftV, -9, 9);
            }
            else
            {
                float ramp = Mathf.Clamp01((AirTime - 0.18f) / 0.15f);
                if (CanAct) Spin += inp.Steer * SpinRate * ramp * dt;
            }

            float vx = V * Mathf.Sin(Yaw) + DriftV;
            float stretch = Mathf.Clamp(1 - k * X, 0.6f, 1.6f);
            float ds = V * Mathf.Cos(Yaw) * dt / stretch;
            float nx = X + vx * dt;
            float ns = Mathf.Min(S + ds, Tr.Length - 8);
            if (ns >= Tr.Length - 8) V = Mathf.Min(V, 1);

            // Tape and hay bales.
            float bar = Tr.WidthAt(ns) * 0.5f + 2.35f;
            if (Mathf.Abs(nx) > bar)
            {
                float into = vx * Mathf.Sign(nx);
                nx = Mathf.Sign(nx) * bar;
                if (into > 6.5f && Invuln <= 0 && CanAct)
                {
                    S = ns;
                    X = nx;
                    Crash("INTO THE TAPE");
                    return;
                }
                V *= 1 - Mathf.Clamp01(into * 0.04f);
                DriftV = -Mathf.Sign(nx) * 1.2f;
                Yaw = -Mathf.Sign(nx) * 0.08f;
                if (into > 2) Emit(RiderEventKind.Barrier);
            }
            S = ns;
            X = nx;

            // Vertical: follow the ground until it falls away faster than gravity.
            float gy = Tr.GroundY(S, X);
            if (Grounded)
            {
                // Leave the ground when the ballistic path clears it by a few cm (lips, drops,
                // crests taken fast). A gap test, not an acceleration test, so the 2 m kinks of
                // the height profile do not cause micro-hops.
                float yBall = Y + Vy * dt - 0.5f * Gravity * dt * dt;
                if (yBall > gy + 0.035f && V > 3)
                {
                    Grounded = false;
                    AirTime = 0;
                    Spin = 0;
                    _combo.Clear();
                    _trickPts = 0;
                    if (Preload > 0.2f && CanAct)
                    {
                        Vy += 1.0f + 2.6f * Preload;
                        Emit(RiderEventKind.Pop);
                    }
                    Preload = 0;
                    Vy -= Gravity * dt;
                    Y = Mathf.Max(yBall, gy);
                    Emit(RiderEventKind.Takeoff);
                }
                else
                {
                    Vy = (gy - Y) / dt;
                    Y = gy;
                }
            }
            else
            {
                AirTime += dt;
                Vy -= Gravity * dt;
                Y += Vy * dt;
                if (Y <= gy)
                {
                    Land(gy);
                    if (Crashed) return;
                }
            }

            // Jump button: preload while held, hop on release.
            if (Grounded && CanAct)
            {
                if (inp.B) Preload = Mathf.MoveTowards(Preload, 1, dt * 3.2f);
                else if (_prevB && Preload > 0.05f)
                {
                    Vy = Mathf.Max(Vy, 0) + 2.4f + 2.6f * Preload;
                    Grounded = false;
                    AirTime = 0;
                    Spin = 0;
                    _combo.Clear();
                    _trickPts = 0;
                    Preload = 0;
                    Y += Vy * dt;
                    Emit(RiderEventKind.Hop);
                }
                else Preload = Mathf.MoveTowards(Preload, 0, dt * 4);
            }
            _prevB = inp.B;

            // Tricks.
            if (!Grounded && CanAct)
            {
                Trick pressed = inp.ADown ? Trick.Superman : inp.BDown ? Trick.NoHander : inp.CDown ? Trick.CanCan : Trick.None;
                if (pressed != Trick.None)
                {
                    CurTrick = pressed;
                    _trickHeld = true;
                }
                bool held = CurTrick == Trick.Superman ? inp.A : CurTrick == Trick.NoHander ? inp.B : CurTrick == Trick.CanCan && inp.C;
                if (!held) _trickHeld = false;
            }
            else if (Grounded) _trickHeld = false;
            TrickBlend = Mathf.MoveTowards(TrickBlend, _trickHeld ? 1 : 0, dt * (_trickHeld ? 5.5f : 4.2f));
            if (TrickBlend <= 0 && !_trickHeld) CurTrick = Trick.None;
            if (!Grounded && TrickBlend > 0.75f && CurTrick != Trick.None)
            {
                string name = TrickName(CurTrick);
                if (!_combo.Contains(name)) _combo.Add(name);
                float rate = CurTrick == Trick.Superman ? 950 : CurTrick == Trick.CanCan ? 760 : 640;
                _trickPts += rate * Def.TrickBonus * dt;
            }

            // Punch.
            if (CanAct && Grounded && inp.ADown && PunchCooldown <= 0 && PunchTimer < 0)
            {
                PunchTimer = 0;
                PunchCooldown = 0.7f;
                Emit(RiderEventKind.Punch);
            }
            if (PunchTimer >= 0)
            {
                float before = PunchTimer;
                PunchTimer += dt;
                if (before < 0.15f && PunchTimer >= 0.15f) PunchStrikeNow = true;
                if (PunchTimer > 0.42f) PunchTimer = -1;
            }

            // Boost.
            if (CanAct && inp.BoostDown && Adrenaline >= 25 && BoostTime <= 0)
            {
                Adrenaline -= 25;
                BoostTime = 2.6f;
                Emit(RiderEventKind.Boost);
            }

            // Pose helpers.
            PedalPhase += Pedalling * dt * (2.5f + V * 0.45f);
            // Euler Z: positive rolls left, so right turns and right steering lean negative.
            float leanTarget = Grounded ? Mathf.Clamp(-(Yaw / MaxYaw) * 0.32f - k * V * V / 9.81f * 0.55f, -0.6f, 0.6f) : 0;
            Lean = Mathf.Lerp(Lean, leanTarget, dt * 6);
            float pitchTarget;
            if (Grounded)
            {
                float yb = Tr.GroundY(S - 0.55f, X), yf = Tr.GroundY(S + 0.55f, X);
                pitchTarget = Mathf.Atan2(yb - yf, 1.1f);
            }
            else pitchTarget = Mathf.Clamp(Mathf.Atan2(-Vy, Mathf.Max(4, V)) * 0.6f, -0.5f, 0.6f);
            Pitch = Mathf.Lerp(Pitch, pitchTarget, dt * (Grounded ? 14 : 2.5f));

            if (!Finished && S >= Tr.FinishS)
            {
                Finished = true;
                Emit(RiderEventKind.Finish);
            }
        }

        public static string TrickName(Trick t) => t switch
        {
            Trick.Superman => "SUPERMAN",
            Trick.NoHander => "NO-HANDER",
            Trick.CanCan => "CAN-CAN",
            _ => "",
        };

        void Land(float gy)
        {
            Y = gy;
            bool wasTrick = TrickBlend > 0.4f;
            float rem = Spin - Mathf.Round(Spin / (Mathf.PI * 2)) * Mathf.PI * 2;
            int fullSpins = Mathf.RoundToInt(Mathf.Abs(Spin) / (Mathf.PI * 2));
            float impact = -Vy;
            Grounded = true;
            Vy = 0;
            float air = AirTime;
            AirTime = 0;
            Spin = 0;
            if (!CanAct)
            {
                TrickBlend = 0;
                return;
            }
            if (wasTrick)
            {
                Crash("BAILED THE TRICK");
                return;
            }
            if (Mathf.Abs(rem) > 0.8f)
            {
                Crash("SIDEWAYS LANDING");
                return;
            }
            if (impact > 16.5f)
            {
                Crash("CASED IT");
                return;
            }
            if (impact > 11)
            {
                V *= 0.85f;
                Emit(RiderEventKind.HardLanding);
            }
            Emit(RiderEventKind.Land, null, Mathf.RoundToInt(impact * 10));

            int pts = Mathf.RoundToInt(_trickPts);
            if (fullSpins > 0)
            {
                _combo.Add(fullSpins == 1 ? "360" : fullSpins == 2 ? "720" : (fullSpins * 360) + "");
                pts += fullSpins == 1 ? 900 : fullSpins * 1100;
            }
            if (air > 0.75f && (_combo.Count > 0 || air > 1.1f))
            {
                pts += Mathf.RoundToInt((air - 0.6f) * 420);
                if (_combo.Count == 0) _combo.Add("BIG AIR");
            }
            if (_combo.Count > 0 && pts > 0)
            {
                float mult = _combo.Count >= 3 ? 2f : _combo.Count == 2 ? 1.5f : 1f;
                pts = Mathf.RoundToInt(pts * mult / 10f) * 10;
                TrickScore += pts;
                Adrenaline = Mathf.Min(100, Adrenaline + pts / 45f);
                Emit(RiderEventKind.TrickLanded, string.Join(" + ", _combo) + (mult > 1 ? "  x" + mult.ToString("0.#") : ""), pts);
            }
            _combo.Clear();
            _trickPts = 0;
        }

        // ------------------------------------------------------------ combat

        public void TakeHit(Rider from, int side)
        {
            if (Crashed || Invuln > 0) return;
            // The player takes a little less from AI punches than the AI does from the player.
            float dmg = (IsPlayer ? 26 : 34) * from.Power / Mathf.Sqrt(Power) * (from.Boosting ? 1.3f : 1);
            Health -= dmg;
            SinceHit = 0;
            HitFlash = 0.35f;
            DriftV += side * 3.4f;
            V *= 0.94f;
            from.Adrenaline = Mathf.Min(100, from.Adrenaline + 7);
            Emit(RiderEventKind.Hit, null, Mathf.RoundToInt(dmg), from);
            if (Health <= 0)
            {
                from.Takedowns++;
                from.Adrenaline = Mathf.Min(100, from.Adrenaline + 25);
                Events.Add(new RiderEvent { Kind = RiderEventKind.Takedown, Rider = from, Other = this });
                Crash("KNOCKED DOWN");
            }
        }

        // ------------------------------------------------------------ crash

        public void Crash(string why)
        {
            if (Crashed) return;
            Crashed = true;
            CrashTime = 0;
            Crashes++;
            var fwd = Tr.Forward(S);
            var right = Tr.Right(S);
            float head = Tr.HeadAt(S) + Yaw;
            var dir = new Vector3(Mathf.Sin(head), 0, Mathf.Cos(head));
            var pos = Tr.World(S, X, Y);
            BodyPos = pos + Vector3.up * 1.0f;
            BikePos = pos + Vector3.up * 0.4f;
            BodyVel = dir * V * 0.75f + Vector3.up * (Vy * 0.5f + 3.2f) + right * DriftV * 0.5f;
            BikeVel = dir * V * 0.55f + Vector3.up * (Vy * 0.4f + 2f) - right * Mathf.Sign(DriftV + 0.01f) * 1.5f;
            BodyRot = Quaternion.LookRotation(dir);
            BikeRot = Quaternion.LookRotation(dir);
            BodySpin = new Vector3(Random.Range(5f, 9f), Random.Range(-3f, 3f), Random.Range(-4f, 4f));
            BikeSpin = new Vector3(Random.Range(-6f, 6f), Random.Range(-5f, 5f), Random.Range(5f, 9f));
            TrickBlend = 0;
            CurTrick = Trick.None;
            _trickHeld = false;
            PunchTimer = -1;
            BoostTime = 0;
            Preload = 0;
            _ = fwd;
            Emit(RiderEventKind.Crash, why);
        }

        void Tumble(ref Vector3 pos, ref Vector3 vel, ref Quaternion rot, ref Vector3 spin, float dt, float bounce)
        {
            vel += Vector3.down * Gravity * dt;
            pos += vel * dt;
            // Ground under the piece (track space approximation around the crash spot).
            float s = S + Vector3.Dot(pos - Tr.Center(S), Tr.Forward(S));
            float x = Vector3.Dot(pos - Tr.Center(S), Tr.Right(S));
            float bar = Tr.WidthAt(s) * 0.5f + 2.3f;
            if (Mathf.Abs(x) > bar)
            {
                var c = Tr.World(s, Mathf.Sign(x) * bar, pos.y);
                pos.x = c.x;
                pos.z = c.z;
                vel -= Tr.Right(s) * Mathf.Sign(x) * Vector3.Dot(vel, Tr.Right(s)) * Mathf.Sign(x) * 1.5f;
            }
            float gy = Tr.GroundY(s, x) + 0.18f;
            if (pos.y < gy)
            {
                pos.y = gy;
                if (vel.y < 0) vel.y = -vel.y * bounce;
                vel.x *= 1 - dt * 3.5f;
                vel.z *= 1 - dt * 3.5f;
                spin *= 1 - dt * 3f;
            }
            rot = Quaternion.Euler(spin * Mathf.Rad2Deg * dt) * rot;
        }

        void CrashStep(float dt)
        {
            CrashTime += dt;
            Tumble(ref BodyPos, ref BodyVel, ref BodyRot, ref BodySpin, dt, 0.32f);
            Tumble(ref BikePos, ref BikeVel, ref BikeRot, ref BikeSpin, dt, 0.25f);
            V = Mathf.MoveTowards(V, 0, dt * 12);
            if (CrashTime > 2.1f) Respawn();
        }

        void Respawn()
        {
            float s = S + Vector3.Dot(BodyPos - Tr.Center(S), Tr.Forward(S));
            s = Mathf.Clamp(s, S - 4, S + 25);
            // Not on a ramp, a drop or inside an obstacle.
            foreach (var r in Tr.Ramps)
                if (s > r.S0 - 3 && s < r.Lip + 16) s = r.Lip + 16;
            foreach (float d in Tr.Drops)
                if (s > d - 2 && s < d + 8) s = d + 8;
            float half = Tr.WidthAt(s) * 0.5f;
            float x = Mathf.Clamp(Vector3.Dot(BodyPos - Tr.Center(S), Tr.Right(S)), -half + 1.5f, half - 1.5f);
            foreach (var o in Tr.Obstacles)
            {
                if (Mathf.Abs(o.S - s) < 4 && Mathf.Abs(o.X - x) < o.HalfX + 1.2f)
                    x = o.X > 0 ? o.X - o.HalfX - 1.5f : o.X + o.HalfX + 1.5f;
            }
            s = Mathf.Min(s, Tr.Length - 10);
            Crashed = false;
            S = s;
            X = Mathf.Clamp(x, -half + 1, half - 1);
            Y = Tr.GroundY(S, X);
            Vy = 0;
            V = Finished ? 0 : 6;
            Yaw = 0;
            DriftV = 0;
            Spin = 0;
            Grounded = true;
            Health = 100;
            Invuln = 2.2f;
            Lean = 0;
            Emit(RiderEventKind.Respawn);
            if (!Finished && S >= Tr.FinishS)
            {
                Finished = true;
                Emit(RiderEventKind.Finish);
            }
        }

        // ------------------------------------------------------------ view helpers

        public float Heading => Tr.HeadAt(S) + Yaw + (Grounded ? 0 : Spin);

        public Vector3 WorldPos => Tr.World(S, X, Y);

        /// <summary>Bike orientation: heading, pitch with the ground or the flight, lean into turns.</summary>
        public Quaternion Rotation => Quaternion.Euler(Pitch * Mathf.Rad2Deg, Heading * Mathf.Rad2Deg, Lean * Mathf.Rad2Deg);

        /// <summary>Seconds until the rider lands, by stepping the flight (AI trick timing).</summary>
        public float TimeToLand()
        {
            if (Grounded) return 0;
            float y = Y, vy = Vy, s = S, t = 0;
            float ds = V * Mathf.Cos(Yaw);
            while (t < 4)
            {
                t += 0.04f;
                vy -= Gravity * 0.04f;
                y += vy * 0.04f;
                s += ds * 0.04f;
                if (y <= Tr.GroundY(s, X)) return t;
            }
            return t;
        }
    }
}
