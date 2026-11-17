using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.AI;

namespace UA.Game
{
    /// <summary>
    /// Boss zombie with three health phases and an attack set per boss type. Complexity (rising every
    /// 50 stages) unlocks extra attacks earlier and shortens cooldowns.
    /// </summary>
    public sealed class Boss : MonoBehaviour, IDamageable
    {
        public static Boss Current;
        public static event Action<Boss> Defeated;

        public BossType Type;
        public BossDef Def;
        public float Hp, MaxHp, Damage;
        public int Phase = 1, Complexity;
        public ZombieRig Rig;
        public bool IsAlive => _state != S.Dead;
        public string Name => Def.Name;

        enum S { Intro, Chase, Attack, Recover, Dead }
        enum A { None, Swing, Charge, Slam, Throw, Spew, Brood, Burst, Dash, Combo, Summon, Howl }

        S _state;
        A _attack;
        float _t, _dur, _cool, _repath, _phaseAnim, _cloak, _deathT, _roarT;
        bool _hit;
        Vector3 _chargeDir, _vel;
        NavMeshAgent _agent;
        readonly List<Hitbox> _boxes = new List<Hitbox>();
        Spring _flinch;
        StagePlan _plan;
        readonly Dictionary<A, float> _cooldowns = new Dictionary<A, float>();

        public static Boss Spawn(BossType type, StagePlan plan, Vector3 pos, Transform parent)
        {
            ZombieRig.Variant(ZombieType.Walker, type, false, 7, out var shape, out var look);
            var rig = ZombieRig.Create(parent, shape, look, "Bosses/" + type);
            var b = rig.gameObject.AddComponent<Boss>();
            b.Rig = rig;
            b.Type = type;
            b.Def = Bestiary.Boss(type);
            b._plan = plan;
            b.Complexity = plan.BossComplexity;
            b.MaxHp = b.Hp = b.Def.Hp * plan.BossHpMul;
            b.Damage = b.Def.Damage * plan.DmgMul;
            rig.transform.position = pos;
            rig.transform.localScale = Vector3.one * b.Def.Scale;
            rig.gameObject.layer = Layers.Enemy;
            var body = rig.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            b.AddBox(ZB.Head, HitZone.Head, new Vector3(0, 0.12f, 0.02f), 0.13f, 0);
            b.AddBox(ZB.Chest, HitZone.Body, Vector3.zero, 0.2f, 0.45f);
            b.AddBox(ZB.Hips, HitZone.Body, Vector3.zero, 0.18f, 0.4f);
            b.AddBox(ZB.ThighL, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.09f, 0.45f);
            b.AddBox(ZB.ThighR, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.09f, 0.45f);
            b.AddBox(ZB.ArmL, HitZone.Limb, new Vector3(0, -0.15f, 0), 0.08f, 0.35f);
            b.AddBox(ZB.ArmR, HitZone.Limb, new Vector3(0, -0.15f, 0), 0.08f, 0.35f);
            if (type == BossType.Juggernaut)
            {
                b.AddBox(ZB.Chest, HitZone.WeakPoint, new Vector3(0, 0f, -0.2f), 0.07f, 0);
                b.AddBox(ZB.Chest, HitZone.WeakPoint, new Vector3(0.12f, 0.12f, -0.17f), 0.05f, 0);
                b.AddBox(ZB.Chest, HitZone.WeakPoint, new Vector3(-0.12f, 0.12f, -0.17f), 0.05f, 0);
                // The procedural body carries glowing weak points; an imported body gets matching glows on the same hitboxes.
                if (rig.Art != null)
                    foreach (var (c, r) in new[] { (new Vector3(0, 0f, -0.2f), 0.07f), (new Vector3(0.12f, 0.12f, -0.17f), 0.05f), (new Vector3(-0.12f, 0.12f, -0.17f), 0.05f) })
                    {
                        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        UnityEngine.Object.Destroy(g.GetComponent<Collider>());
                        g.name = "WeakPointGlow";
                        g.transform.SetParent(rig[ZB.Chest], false);
                        g.transform.localPosition = c;
                        g.transform.localScale = Vector3.one * r * 1.6f;
                        var mr = g.GetComponent<MeshRenderer>();
                        mr.sharedMaterial = rig.Materials[6];
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
            }
            b._agent = rig.gameObject.AddComponent<NavMeshAgent>();
            b._agent.updateRotation = false;
            b._agent.radius = 0.6f;
            b._agent.height = 1.75f * b.Def.Scale;
            b._agent.speed = b.Def.Speed;
            b._agent.acceleration = 12;
            b._agent.autoBraking = false;
            b._agent.Warp(pos);
            b._state = S.Intro;
            b._t = 0;
            Current = b;
            Sfx.BossRoar(rig.transform.position);
            CameraShake.Add(0.7f);
            return b;
        }

        void AddBox(ZB bone, HitZone zone, Vector3 c, float r, float h)
        {
            var go = new GameObject("Hit" + bone + zone);
            go.layer = Layers.Enemy;
            go.transform.SetParent(Rig[bone], false);
            if (h > 0)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = c; cap.radius = r; cap.height = h; cap.direction = 1;
            }
            else
            {
                var s = go.AddComponent<SphereCollider>();
                s.center = c; s.radius = r;
            }
            var hb = go.AddComponent<Hitbox>();
            hb.Zone = zone;
            hb.Owner = this;
            hb.Bone = Rig[bone];
            _boxes.Add(hb);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        float Cd(A a) => _cooldowns.TryGetValue(a, out var v) ? v : 0;

        void SetCd(A a, float seconds) => _cooldowns[a] = seconds * (1 - Complexity * 0.08f) * (Phase == 3 ? 0.75f : 1);

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var keys = new List<A>(_cooldowns.Keys);
            foreach (var k in keys) _cooldowns[k] = Mathf.Max(0, _cooldowns[k] - dt);
            var toP = PlayerRig.Pos - transform.position;
            float dist = toP.Flat().magnitude;
            _flinch.Step(0, 90, 12, dt);
            switch (_state)
            {
                case S.Intro:
                    _t += dt;
                    _agent.isStopped = true;
                    if (_t > 2.2f) { _state = S.Chase; _agent.isStopped = false; }
                    break;
                case S.Chase:
                    ChaseUpdate(dt, dist, toP);
                    break;
                case S.Attack:
                    AttackUpdate(dt, dist, toP);
                    break;
                case S.Recover:
                    _t += dt;
                    if (_t > _dur) { _state = S.Chase; if (_agent.isOnNavMesh) _agent.isStopped = false; }
                    break;
                case S.Dead:
                    DeadUpdate(dt);
                    return;
            }
            var v = _agent.enabled ? _agent.velocity : Vector3.zero;
            _vel = Vector3.Lerp(_vel, v, 1 - Mathf.Exp(-dt * 6));
            var look = _state == S.Attack && _attack == A.Charge ? _chargeDir : toP.Flat();
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), (_attack == A.Charge ? 30 : 140) * dt);
            if (Type == BossType.Wraith) CloakTick(dt, dist);
            _roarT -= dt;
            if (_roarT <= 0) { _roarT = UnityEngine.Random.Range(6f, 11f); Sfx.BossGrowl(transform.position); }
            Animate(dt);
        }

        void ChaseUpdate(float dt, float dist, Vector3 toP)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.speed = Def.Speed * (Phase == 3 ? 1.25f : 1f) * (1 + Complexity * 0.05f);
            _repath -= dt;
            if (_repath <= 0)
            {
                _repath = 0.25f;
                _agent.SetDestination(PlayerRig.Pos);
            }
            var a = Choose(dist);
            if (a != A.None) Begin(a, toP);
        }

        A Choose(float dist)
        {
            bool p2 = Phase >= 2 || Complexity >= 1, p3 = Phase >= 3 || Complexity >= 2;
            float melee = 2.6f * Def.Scale / 2.2f + 0.8f;
            switch (Type)
            {
                case BossType.Butcher:
                    if (dist < melee && Cd(A.Swing) <= 0) return A.Swing;
                    if (dist > 7 && dist < 22 && Cd(A.Charge) <= 0) return A.Charge;
                    if (dist < 7 && Cd(A.Slam) <= 0 && p2) return A.Slam;
                    if (dist > 9 && Cd(A.Throw) <= 0 && p2) return A.Throw;
                    break;
                case BossType.BroodMother:
                    if (dist < melee && Cd(A.Swing) <= 0) return A.Swing;
                    if (dist > 5 && dist < 20 && Cd(A.Spew) <= 0) return A.Spew;
                    if (Cd(A.Brood) <= 0) return A.Brood;
                    if (dist < 8 && Cd(A.Burst) <= 0 && p2) return A.Burst;
                    break;
                case BossType.Juggernaut:
                    if (dist < melee && Cd(A.Swing) <= 0) return A.Swing;
                    if (dist > 8 && dist < 24 && Cd(A.Charge) <= 0) return A.Charge;
                    if (dist < 8 && Cd(A.Slam) <= 0) return A.Slam;
                    if (dist > 10 && Cd(A.Throw) <= 0 && p2) return A.Throw;
                    break;
                case BossType.Wraith:
                    if (dist < melee + 0.6f && Cd(A.Combo) <= 0) return A.Combo;
                    if (dist > 6 && Cd(A.Dash) <= 0) return A.Dash;
                    if (Cd(A.Summon) <= 0 && p2) return A.Summon;
                    if (Cd(A.Howl) <= 0 && p3) return A.Howl;
                    break;
            }
            return A.None;
        }

        void Begin(A a, Vector3 toP)
        {
            _state = S.Attack;
            _attack = a;
            _t = 0;
            _hit = false;
            _chargeDir = toP.Flat().normalized;
            switch (a)
            {
                case A.Swing: _dur = Type == BossType.Juggernaut ? 1.5f : 1.2f; SetCd(a, 1.8f); break;
                case A.Charge: _dur = 2.2f; SetCd(a, 7f); Sfx.BossRoar(transform.position); break;
                case A.Slam: _dur = 1.6f; SetCd(a, 6f); break;
                case A.Throw: _dur = 1.4f; SetCd(a, 6.5f); break;
                case A.Spew: _dur = 1.3f; SetCd(a, 4.5f); break;
                case A.Brood: _dur = 1.6f; SetCd(a, 11f); break;
                case A.Burst: _dur = 1.5f; SetCd(a, 9f); break;
                case A.Dash: _dur = 0.9f; SetCd(a, 3.5f); break;
                case A.Combo: _dur = 1.6f; SetCd(a, 2.2f); break;
                case A.Summon: _dur = 1.5f; SetCd(a, 14f); Sfx.BossRoar(transform.position); break;
                case A.Howl: _dur = 1.2f; SetCd(a, 12f); break;
            }
            if (_agent.isOnNavMesh) _agent.isStopped = a != A.Charge && a != A.Dash;
        }

        void AttackUpdate(float dt, float dist, Vector3 toP)
        {
            float prev = _t;
            _t += dt / _dur;
            bool Cross(float at) => prev < at && _t >= at;
            var center = transform.position + Vector3.up;
            switch (_attack)
            {
                case A.Swing:
                case A.Combo:
                {
                    int hits = _attack == A.Combo ? 3 : (Phase >= 2 ? 2 : 1);
                    for (int i = 0; i < hits; i++)
                    {
                        float at = 0.45f + i * (0.45f / hits);
                        if (Cross(at) && dist < 3.2f * Def.Scale / 2f + 1f && Vector3.Angle(transform.forward, toP.Flat()) < 80)
                        {
                            PlayerRig.Instance?.Hurt(Damage * (_attack == A.Combo ? 0.6f : 1f), transform.position, HurtKind.Melee);
                            CameraShake.Add(0.5f);
                        }
                        if (Cross(at)) Sfx.Swoosh(transform.position);
                    }
                    break;
                }
                case A.Charge:
                    if (_t > 0.25f && _agent.isOnNavMesh)
                    {
                        _agent.isStopped = true;
                        _agent.Move(_chargeDir * (13f + Complexity) * dt);
                        if (UnityEngine.Random.value < 0.4f) Fx.Impact(transform.position + Vector3.up * 0.1f, Vector3.up, SurfaceKind.Concrete, 0.6f);
                    }
                    if (!_hit && dist < 2.6f)
                    {
                        _hit = true;
                        PlayerRig.Instance?.Hurt(Damage * 1.5f, transform.position, HurtKind.Melee);
                        PlayerRig.Instance?.Knockback(_chargeDir * 9 + Vector3.up * 3);
                        CameraShake.Add(1f);
                        _t = Mathf.Max(_t, 0.85f);
                    }
                    break;
                case A.Slam:
                    if (Cross(0.55f))
                    {
                        Fx.Explosion(transform.position + transform.forward * 1.2f, 3.5f);
                        CameraShake.Add(1f);
                        Sfx.Explosion(transform.position, 6);
                        float r = 7 + Complexity;
                        if (dist < r && PlayerRig.Instance != null && PlayerRig.Instance.Grounded) PlayerRig.Instance.Hurt(Damage * 1.2f * Mathf.Clamp01(1.2f - dist / r), transform.position, HurtKind.Explosion);
                    }
                    break;
                case A.Throw:
                    if (Cross(0.6f))
                    {
                        var from = Rig[ZB.HandR].position + Vector3.up * 0.5f;
                        Projectile.Debris(from, PlayerRig.Pos + Vector3.up * 0.8f, Damage * 1.1f, Type == BossType.Juggernaut ? 1.6f : 1f);
                        if (Phase == 3 || Complexity >= 3) Projectile.Debris(from, PlayerRig.Pos + Vector3.up * 0.8f + UnityEngine.Random.insideUnitSphere * 3, Damage * 0.8f, 0.8f);
                    }
                    break;
                case A.Spew:
                    if (Cross(0.5f))
                    {
                        int n = 3 + Complexity + (Phase - 1);
                        var from = Rig[ZB.Head].position + transform.forward * 0.4f;
                        for (int i = 0; i < n; i++)
                        {
                            var off = Quaternion.Euler(0, (i - (n - 1) / 2f) * 9, 0) * (PlayerRig.Pos - transform.position).Flat();
                            Projectile.Acid(from, transform.position + off + Vector3.up * 0.5f, Damage * 0.7f);
                        }
                        Sfx.Spit(from);
                    }
                    break;
                case A.Brood:
                    if (Cross(0.6f))
                    {
                        int n = 2 + Complexity / 2 + (Phase - 1);
                        Director.Instance?.SpawnMinions(ZombieType.Crawler, n, transform.position - transform.forward * 1.5f);
                        Fx.Gore(transform.position + Vector3.up * 1.2f - transform.forward * 0.6f, -transform.forward, 1.2f);
                    }
                    break;
                case A.Burst:
                    if (Cross(0.5f))
                    {
                        Blast.Explode(transform.position + Vector3.up * 0.8f, 6f, 0, false, true, Damage * 0.8f);
                        Hazards.SpawnToxicPool(transform.position, 8f, Damage * 0.5f);
                    }
                    break;
                case A.Dash:
                {
                    // Flank: dash to the side of the player, cloaked.
                    if (_t < 0.15f) _cloak = 1;
                    if (_agent.isOnNavMesh)
                    {
                        var side = Vector3.Cross(Vector3.up, toP.Flat().normalized) * ((int)(Time.time * 10) % 2 == 0 ? 1 : -1);
                        var dir = (toP.Flat().normalized * 0.6f + side * 0.8f).normalized;
                        _agent.isStopped = true;
                        _agent.Move(dir * 16f * dt);
                    }
                    break;
                }
                case A.Summon:
                    if (Cross(0.6f))
                    {
                        int n = 1 + Phase + Complexity / 2;
                        Director.Instance?.SpawnMinions(ZombieType.Hunter, n, transform.position);
                    }
                    break;
                case A.Howl:
                    if (Cross(0.3f))
                    {
                        CameraShake.Add(0.8f);
                        Sfx.BossRoar(transform.position);
                        PlayerRig.Instance?.Stun(0.6f);
                    }
                    break;
            }
            if (_t >= 1)
            {
                _state = S.Recover;
                _t = 0;
                _dur = _attack == A.Charge ? 1.1f : _attack == A.Slam ? 0.8f : 0.35f;
                _attack = A.None;
                if (_agent.isOnNavMesh) _agent.isStopped = true;
            }
        }

        void CloakTick(float dt, float dist)
        {
            float target = (_attack == A.Dash || (dist > 9 && _state == S.Chase)) ? 1 : 0;
            _cloak = Mathf.MoveTowards(_cloak, target, dt * 2f);
            Rig.SetCloak(_cloak > 0.5f);
        }

        // --------------------------------------------------------------- damage

        public void TakeDamage(DamageInfo d)
        {
            if (_state == S.Dead || _state == S.Intro && _t < 0.5f) return;
            float amt = d.Amount;
            if (d.Zone == HitZone.WeakPoint) amt *= 3f;
            else if (d.Zone == HitZone.Head) amt *= 1 + ((d.HeadMul > 0 ? d.HeadMul : 2f) - 1) * 0.6f;
            else if (!d.Fire) amt *= 1 - Def.BodyArmor * (1 - d.ArmorPierce);
            if (_cloak > 0.5f) amt *= 0.6f;
            if (d.FromPlayer) amt *= PlayerRig.Stats.EliteMul;
            Hp -= amt;
            if (d.Zone == HitZone.WeakPoint) { Fx.Sparks(d.Point, -d.Dir, 14, 6); Fx.Glow(d.Point, 0.6f, new Color(1f, 0.5f, 0.1f, 1)); }
            else if (Type == BossType.Juggernaut && d.Zone == HitZone.Body && !d.Fire) Fx.Impact(d.Point, -d.Dir, SurfaceKind.Metal);
            else if (!d.Fire && !d.Explosive) Fx.Blood(d.Point, d.Dir, 0.8f);
            _flinch.Velocity += 60;
            Hud.BossHit();
            int phase = Hp > MaxHp * 0.66f ? 1 : Hp > MaxHp * 0.33f ? 2 : 3;
            if (phase != Phase)
            {
                Phase = phase;
                Sfx.BossRoar(transform.position);
                CameraShake.Add(0.6f);
                Hud.Announce(Def.Name + " ENRAGED", "PHASE " + Phase, new Color(1f, 0.3f, 0.2f));
            }
            if (Hp <= 0) Die(d);
        }

        void Die(DamageInfo d)
        {
            _state = S.Dead;
            _deathT = 0;
            if (_agent.enabled) _agent.enabled = false;
            foreach (var b in _boxes) b.gameObject.SetActive(false);
            Rig.SetCloak(false);
            Fx.Gore(Rig[ZB.Chest].position, Vector3.up, 2.5f);
            Sfx.BossDeath(transform.position);
            CameraShake.Add(1f);
            Defeated?.Invoke(this);
        }

        void DeadUpdate(float dt)
        {
            _deathT += dt;
            float k = X.Smooth01((_deathT - 0.6f) / 1.2f);
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Quaternion.Euler(-80 * k, 0, 0);
            Rig[ZB.ShinL].localRotation = Quaternion.Euler(70 * X.Smooth01(_deathT / 0.6f), 0, 0);
            Rig[ZB.ShinR].localRotation = Quaternion.Euler(70 * X.Smooth01(_deathT / 0.6f), 0, 0);
            if (_deathT < 2f && UnityEngine.Random.value < 0.2f) Fx.Blood(Rig[ZB.Chest].position, UnityEngine.Random.onUnitSphere, 1f);
        }

        // --------------------------------------------------------------- animation

        float _phase;

        void Animate(float dt)
        {
            float speed = _vel.Flat().magnitude / Def.Scale;
            _phase += speed * Mathf.PI * dt / 1.4f;
            float ph = _phase, move = Mathf.Clamp01(speed / 0.5f);
            bool thin = Type == BossType.Wraith;
            float lean = thin ? 30 : 16;
            var hips = Rig[ZB.Hips];
            hips.localPosition = new Vector3(0, ZombieRig.BindPos[(int)ZB.Hips].y - Mathf.Abs(Mathf.Cos(ph)) * 0.05f * move - (thin ? 0.1f : 0), 0);
            hips.localRotation = Quaternion.Euler(6, Mathf.Sin(ph) * 8 * move, Mathf.Sin(ph) * 7 * move);
            Rig[ZB.Spine].localRotation = Quaternion.Euler(lean * 0.4f + _flinch.Value * 0.2f, 0, 0);
            Rig[ZB.Chest].localRotation = Quaternion.Euler(lean * 0.6f, -Mathf.Sin(ph) * 10 * move, 0);
            Rig[ZB.Neck].localRotation = Quaternion.Euler(-lean * 0.5f, 0, 0);
            Rig[ZB.Head].localRotation = Quaternion.Euler(-10, Mathf.Sin(Time.time * 0.8f) * 15, 0);
            float tA = 26, kA = 50;
            Rig[ZB.ThighL].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * tA * move, 0, -6);
            Rig[ZB.ThighR].localRotation = Quaternion.Euler(Mathf.Sin(ph) * tA * move, 0, 6);
            Rig[ZB.ShinL].localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Cos(ph)) * kA * move + 8, 0, 0);
            Rig[ZB.ShinR].localRotation = Quaternion.Euler(Mathf.Max(0, -Mathf.Cos(ph)) * kA * move + 8, 0, 0);
            Rig[ZB.ArmL].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 22 * move + (thin ? -20 : 0), 0, -16);
            Rig[ZB.ArmR].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * 22 * move + (thin ? -20 : 0), 0, 16);
            Rig[ZB.ForeL].localRotation = Quaternion.Euler(-25, 0, 0);
            Rig[ZB.ForeR].localRotation = Quaternion.Euler(-25, 0, 0);
            Rig[ZB.ClavL].localRotation = Quaternion.Euler(0, 0, 10);
            Rig[ZB.ClavR].localRotation = Quaternion.Euler(0, 0, -10);
            Rig[ZB.Jaw].localRotation = Quaternion.Euler(8 + Mathf.Max(0, Mathf.Sin(Time.time * 1.7f)) * 12, 0, 0);
            if (_state == S.Intro)
            {
                float k = X.Bump(_t / 2.2f, 0.2f, 1f);
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(-60 * k, 0, -60 * k);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-60 * k, 0, 60 * k);
                Rig[ZB.Chest].localRotation = Quaternion.Euler(-20 * k, 0, 0);
                Rig[ZB.Head].localRotation = Quaternion.Euler(-30 * k, 0, 0);
                Rig[ZB.Jaw].localRotation = Quaternion.Euler(35 * k, 0, 0);
            }
            if (_state != S.Attack) return;
            float t = _t;
            float wind = X.Bump(t, 0, 0.55f), strike = X.Bump(t, 0.35f, 0.85f);
            switch (_attack)
            {
                case A.Swing:
                case A.Combo:
                {
                    float c = _attack == A.Combo ? Mathf.Sin(t * Mathf.PI * 6) : 0;
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-60 - 100 * wind + 120 * strike, -40 * wind + 60 * strike + c * 30, 30 * wind);
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-60 - 60 * wind + 60 * strike - c * 30, 0, -20);
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(lean * 0.6f - 20 * wind + 35 * strike, 30 * wind - 40 * strike, 0);
                    Rig[ZB.Jaw].localRotation = Quaternion.Euler(30, 0, 0);
                    break;
                }
                case A.Charge:
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(40, 0, 0);
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(30, 0, -30);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(30, 0, 30);
                    _phase += dt * 10;
                    break;
                case A.Slam:
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-170 * wind + 150 * strike, 0, -10);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-170 * wind + 150 * strike, 0, 10);
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(-20 * wind + 50 * strike, 0, 0);
                    hips.localPosition += Vector3.up * (0.25f * wind - 0.15f * strike);
                    break;
                case A.Throw:
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-170 * wind + 120 * strike, 0, 20);
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(-15 * wind + 30 * strike, 25 * wind - 30 * strike, 0);
                    break;
                case A.Spew:
                case A.Burst:
                case A.Howl:
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(-25 * wind + 30 * strike, 0, 0);
                    Rig[ZB.Head].localRotation = Quaternion.Euler(-35 * wind + 10 * strike, 0, 0);
                    Rig[ZB.Jaw].localRotation = Quaternion.Euler(40 * Mathf.Max(wind, strike), 0, 0);
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-30, 0, -50 * wind);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-30, 0, 50 * wind);
                    break;
                case A.Brood:
                case A.Summon:
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-140 * wind, 0, -40);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-140 * wind, 0, 40);
                    Rig[ZB.Head].localRotation = Quaternion.Euler(-40 * wind, 0, 0);
                    Rig[ZB.Jaw].localRotation = Quaternion.Euler(40 * wind, 0, 0);
                    break;
                case A.Dash:
                    Rig[ZB.Chest].localRotation = Quaternion.Euler(45, 0, 0);
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(40, 0, -20);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(40, 0, 20);
                    break;
            }
        }
    }

    /// <summary>Simple ballistic projectiles (grenades, launcher shells, acid spit, thrown debris).</summary>
    public sealed class Projectile : MonoBehaviour
    {
        public enum Kind { Grenade, Shell, Acid, Debris }
        Kind _kind;
        Vector3 _vel;
        float _life, _damage, _radius, _fuse, _size;
        bool _fromPlayer;
        static Mesh _sphere, _rock, _shell;
        static Material _acidMat, _grenadeMat, _debrisMat;

        static GameObject Make(Kind k, Vector3 pos, Vector3 vel, float damage)
        {
            var go = new GameObject(k.ToString());
            go.transform.position = pos;
            var p = go.AddComponent<Projectile>();
            p._kind = k;
            p._vel = vel;
            p._damage = damage;
            p._life = 8;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_sphere == null)
            {
                var mb = new MeshBuilder();
                mb.Sphere(0, Vector3.zero, Vector3.one * 0.5f, 8, 6);
                _sphere = mb.ToMesh("proj", false);
                var rb = new MeshBuilder();
                rb.Sphere(0, Vector3.zero, new Vector3(0.6f, 0.4f, 0.5f), 6, 4);
                rb.Box(0, new Vector3(0.2f, 0.1f, 0), new Vector3(0.5f, 0.3f, 0.6f));
                _rock = rb.ToMesh("debris", false);
                var sb = new MeshBuilder();
                sb.Cylinder(0, new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.3f), 0.3f, 0.3f, 8);
                sb.Sphere(0, new Vector3(0, 0, 0.3f), new Vector3(0.3f, 0.3f, 0.3f), 8, 4);
                _shell = sb.ToMesh("shell", false);
                _acidMat = Mats.Emissive(new Color(0.4f, 0.9f, 0.2f), new Color(0.6f, 2.5f, 0.3f));
                _grenadeMat = Mats.Lit(new Color(0.18f, 0.22f, 0.15f), 0.5f, 0.4f);
                _debrisMat = Mats.Lit(new Color(0.3f, 0.28f, 0.26f), 0.2f);
            }
            switch (k)
            {
                case Kind.Acid: mf.sharedMesh = _sphere; mr.sharedMaterial = _acidMat; go.transform.localScale = Vector3.one * 0.25f; break;
                case Kind.Debris: mf.sharedMesh = _rock; mr.sharedMaterial = _debrisMat; break;
                case Kind.Shell: mf.sharedMesh = _shell; mr.sharedMaterial = _grenadeMat; go.transform.localScale = Vector3.one * 0.12f; break;
                default: mf.sharedMesh = _sphere; mr.sharedMaterial = _grenadeMat; go.transform.localScale = Vector3.one * 0.11f; break;
            }
            return go;
        }

        static Vector3 Lob(Vector3 from, Vector3 to, float speed, float gravity)
        {
            var d = to - from;
            float t = Mathf.Clamp(d.Flat().magnitude / speed, 0.3f, 2.5f);
            var v = d / t;
            v.y += 0.5f * gravity * t;
            return v;
        }

        public static void Acid(Vector3 from, Vector3 to, float damage)
        {
            var go = Make(Kind.Acid, from, Lob(from, to, 14f, 9.8f), damage);
            go.GetComponent<Projectile>()._radius = 2f;
        }

        public static void Debris(Vector3 from, Vector3 to, float damage, float size)
        {
            var go = Make(Kind.Debris, from, Lob(from, to, 18f, 9.8f), damage);
            var p = go.GetComponent<Projectile>();
            p._size = size;
            p._radius = 2.2f * size;
            go.transform.localScale = Vector3.one * size;
        }

        public static void Grenade(Vector3 from, Vector3 vel, float damage, float radius)
        {
            var go = Make(Kind.Grenade, from, vel, damage);
            var p = go.GetComponent<Projectile>();
            p._fromPlayer = true;
            p._radius = radius;
            p._fuse = 2.2f;
        }

        public static void LauncherShell(Vector3 from, Vector3 vel, float damage, float radius, bool rocket = false)
        {
            var go = Make(Kind.Shell, from, vel, damage);
            var p = go.GetComponent<Projectile>();
            p._fromPlayer = true;
            p._radius = radius;
            p._fuse = 6f;
            if (rocket)
            {
                // Self-propelled: flies straight, bigger warhead, thick smoke trail.
                p._rocket = true;
                go.transform.localScale = new Vector3(0.16f, 0.16f, 0.32f);
            }
        }

        bool _rocket;

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            float g = _rocket ? 0f : _kind == Kind.Shell ? 4f : 9.8f;
            _vel.y -= g * dt;
            var p = transform.position;
            var step = _vel * dt;
            int mask = Layers.WorldMask | (_fromPlayer ? Layers.EnemyMask : 0);
            if (_kind == Kind.Grenade)
            {
                _fuse -= dt;
                if (Physics.SphereCast(p, 0.06f, step.normalized, out var gh, step.magnitude + 0.01f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    _vel = Vector3.Reflect(_vel, gh.normal) * 0.45f;
                    transform.position = gh.point + gh.normal * 0.07f;
                    if (_vel.magnitude > 1.5f) Sfx.Clink(gh.point);
                }
                else transform.position = p + step;
                transform.Rotate(_vel.magnitude * 200 * dt, 0, 0);
                if (_fuse <= 0) Detonate(transform.position);
                return;
            }
            if (Physics.SphereCast(p, _kind == Kind.Debris ? 0.3f * _size : 0.08f, step.normalized, out var hit, step.magnitude + 0.01f, mask, QueryTriggerInteraction.Ignore))
            {
                Detonate(hit.point + hit.normal * 0.1f, hit.normal);
                return;
            }
            // Enemy projectiles hit the player capsule.
            if (!_fromPlayer && PlayerRig.Instance != null && (PlayerRig.Pos + Vector3.up * 1f - p).sqrMagnitude < 0.7f * 0.7f)
            {
                Detonate(p, -_vel.normalized);
                return;
            }
            transform.position = p + step;
            if (_kind == Kind.Shell)
            {
                transform.rotation = Quaternion.LookRotation(_vel);
                if (_rocket)
                {
                    Fx.Smoke(p, 0.45f, new Color(0.75f, 0.75f, 0.75f, 0.5f), -_vel.normalized * 0.5f);
                    if (UnityEngine.Random.value < 0.5f) Fx.Smoke(p, 0.2f, new Color(1f, 0.6f, 0.2f, 0.6f), Vector3.zero);
                }
                else if (UnityEngine.Random.value < 0.6f) Fx.Smoke(p, 0.25f, new Color(0.6f, 0.6f, 0.6f, 0.4f), Vector3.zero);
            }
            else if (_kind == Kind.Acid && UnityEngine.Random.value < 0.5f) Fx.Gas(p, 0.25f, new Color(0.4f, 0.9f, 0.2f, 0.35f));
            else if (_kind == Kind.Debris) transform.Rotate(180 * dt, 90 * dt, 0);
            if (_life <= 0) Destroy(gameObject);
        }

        void Detonate(Vector3 p, Vector3 n = default)
        {
            switch (_kind)
            {
                case Kind.Grenade:
                case Kind.Shell:
                    if (_kind == Kind.Grenade && _fromPlayer && AutoPilot.Active)
                        Log.I("GRENADE exploded at " + p.ToString("F2") + ", arc predicted " + GrenadeArc.LastPredicted.ToString("F2") + ", miss " + (p - GrenadeArc.LastPredicted).magnitude.ToString("0.00") + " m");
                    Blast.Explode(p, _radius, _damage, true);
                    Zombie.Noise(p, 40);
                    break;
                case Kind.Acid:
                    Fx.AcidSplash(p, n == default ? Vector3.up : n);
                    Sfx.Splat(p);
                    if (PlayerRig.Instance != null && (PlayerRig.Pos + Vector3.up - p).magnitude < _radius) PlayerRig.Instance.Hurt(_damage, p, HurtKind.Toxic);
                    Hazards.SpawnToxicPool(p, 3.5f, _damage * 0.25f);
                    break;
                case Kind.Debris:
                    Fx.Impact(p, n == default ? Vector3.up : n, SurfaceKind.Concrete, 3f);
                    Fx.Explosion(p, 1.5f * _size);
                    CameraShake.Add(0.5f);
                    Sfx.Thud(p);
                    if (PlayerRig.Instance != null && (PlayerRig.Pos + Vector3.up - p).magnitude < _radius) PlayerRig.Instance.Hurt(_damage, p, HurtKind.Explosion);
                    break;
            }
            Destroy(gameObject);
        }
    }
}
