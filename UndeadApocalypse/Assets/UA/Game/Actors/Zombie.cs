using System;
using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.AI;

namespace UA.Game
{
    public enum ZState { Spawning, Idle, Chase, Search, Attack, Special, Stagger, Dead }

    /// <summary>
    /// A zombie: procedural animation over the skinned rig, NavMesh chasing with surround slots,
    /// vision / hearing perception, melee and special attacks, armor, hit zones, gore and death.
    /// Instances are pooled by the <see cref="Director"/>.
    /// </summary>
    public sealed class Zombie : MonoBehaviour, IDamageable
    {
        public static readonly List<Zombie> Alive = new List<Zombie>();
        public static event Action<Zombie, DamageInfo> Killed;
        public static event Action<Zombie, DamageInfo> Damaged;

        public ZombieType Type;
        public ZombieDef Def;
        public bool Elite, IsBossMinion;
        public float Hp, MaxHp, Damage, Speed, WalkSpeed;
        public ZState State;
        public ZombieRig Rig;
        public NavMeshAgent Agent;
        public float SurroundAngle;
        public bool Burning => _burn > 0;
        public bool IsAlive => State != ZState.Dead;
        public float DeadTime;
        public int VariantKey;
        public bool Aware, Frozen;

        readonly List<Hitbox> _hitboxes = new List<Hitbox>();
        Rigidbody _body;
        float _phase, _attackT = -1, _attackCd, _repath, _sense, _stagger, _specialT = -1, _specialCd, _spawnT, _lostSight, _growlT, _idleT, _burn, _burnTick, _gasT, _cloak, _deathT;
        int _attackKind, _deathKind;
        bool _hitThisAttack, _headGone, _armGone;
        Vector3 _lastSeen, _vel, _charge;
        Quaternion _baseRot;
        Spring _flinchX, _flinchZ, _headX;
        float _armReach, _limp, _headTilt, _armsAsym, _gaitSeed;
        StagePlan _plan;
        int _lodFrame;

        // --------------------------------------------------------------- setup

        public static Zombie Create(Transform parent, ZombieType type, bool elite, int variantSeed)
        {
            ZombieRig.Variant(type, BossType.None, elite, variantSeed, out var shape, out var look);
            // Walkers are the bulk of every horde, so they rotate through several imported civilians.
            string art = type == ZombieType.Walker ? ZombieRig.ArtVariant("Zombies/Walker", variantSeed, 5) : "Zombies/" + type;
            var rig = ZombieRig.Create(parent, shape, look, art);
            var z = rig.gameObject.AddComponent<Zombie>();
            z.Rig = rig;
            z.Type = type;
            z.Def = Bestiary.Get(type);
            z.Elite = elite;
            z.VariantKey = variantSeed;
            z.Setup();
            return z;
        }

        void Setup()
        {
            gameObject.layer = Layers.Enemy;
            _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.None;
            AddHit(ZB.Head, HitZone.Head, new Vector3(0, 0.12f, 0.02f), 0.12f, 0, false);
            AddHit(ZB.Chest, HitZone.Body, new Vector3(0, 0.03f, 0), 0.17f, 0.42f, true);
            AddHit(ZB.Hips, HitZone.Body, new Vector3(0, 0.02f, 0), 0.16f, 0.36f, true);
            AddHit(ZB.ArmL, HitZone.Limb, new Vector3(0, -0.14f, 0), 0.06f, 0.32f, true);
            AddHit(ZB.ArmR, HitZone.Limb, new Vector3(0, -0.14f, 0), 0.06f, 0.32f, true);
            AddHit(ZB.ForeL, HitZone.Limb, new Vector3(0, -0.13f, 0), 0.05f, 0.3f, true);
            AddHit(ZB.ForeR, HitZone.Limb, new Vector3(0, -0.13f, 0), 0.05f, 0.3f, true);
            AddHit(ZB.ThighL, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.08f, 0.44f, true);
            AddHit(ZB.ThighR, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.08f, 0.44f, true);
            AddHit(ZB.ShinL, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.06f, 0.42f, true);
            AddHit(ZB.ShinR, HitZone.Limb, new Vector3(0, -0.2f, 0), 0.06f, 0.42f, true);
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.enabled = false;
            Agent.updateRotation = false;
            Agent.autoBraking = false;
            Agent.acceleration = 14;
            Agent.angularSpeed = 360;
            Agent.radius = 0.32f;
            Agent.height = 1.75f;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            Agent.avoidancePriority = UnityEngine.Random.Range(30, 70);
            gameObject.SetActive(false);
        }

        void AddHit(ZB bone, HitZone zone, Vector3 center, float radius, float height, bool capsule)
        {
            var t = Rig[bone];
            var go = new GameObject("Hit" + bone);
            go.layer = Layers.Enemy;
            go.transform.SetParent(t, false);
            Collider c;
            if (capsule)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = center;
                cap.radius = radius;
                cap.height = height;
                cap.direction = 1;
                c = cap;
            }
            else
            {
                var sph = go.AddComponent<SphereCollider>();
                sph.center = center;
                sph.radius = radius;
                c = sph;
            }
            var hb = go.AddComponent<Hitbox>();
            hb.Zone = zone;
            hb.Owner = this;
            hb.Bone = t;
            _hitboxes.Add(hb);
        }

        /// <summary>Activates a pooled zombie at a position for the current stage.</summary>
        public void Spawn(Vector3 pos, StagePlan plan, bool aware, float extraHp = 1)
        {
            _plan = plan;
            float hpMul = plan.HpMul * (Elite ? 3f : 1f) * extraHp;
            MaxHp = Hp = Def.Hp * hpMul;
            Damage = Def.Damage * plan.DmgMul * (Elite ? 1.4f : 1f);
            Speed = Def.Speed * plan.SpeedMul * UnityEngine.Random.Range(0.9f, 1.12f);
            WalkSpeed = Def.WalkSpeed * plan.SpeedMul;
            float scale = Def.Scale * (Elite ? 1.15f : 1f) * UnityEngine.Random.Range(0.94f, 1.06f);
            transform.localScale = Vector3.one * scale;
            transform.position = pos;
            _baseRot = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
            transform.rotation = _baseRot;
            ResetBones();
            foreach (var hb in _hitboxes) hb.gameObject.SetActive(true);
            gameObject.SetActive(true);
            Agent.enabled = true;
            Agent.Warp(pos);
            Agent.radius = 0.32f * scale;
            Agent.height = 1.75f * scale;
            Agent.speed = WalkSpeed;
            Agent.isStopped = false;
            State = ZState.Spawning;
            Frozen = false;
            Aware = aware;
            _spawnT = 0.8f;
            _attackT = -1; _specialT = -1; _attackCd = UnityEngine.Random.Range(0.3f, 1f); _specialCd = UnityEngine.Random.Range(2f, 6f);
            _stagger = 0; _burn = 0; _cloak = 0; _deathT = 0; _repath = 0; _sense = UnityEngine.Random.value * 0.3f;
            _headGone = _armGone = false;
            _flinchX = default; _flinchZ = default; _headX = default;
            _phase = UnityEngine.Random.value * 6.28f;
            _gaitSeed = UnityEngine.Random.value;
            _armReach = UnityEngine.Random.value < 0.6f ? UnityEngine.Random.Range(0.6f, 1f) : UnityEngine.Random.Range(0, 0.3f);
            _armsAsym = UnityEngine.Random.Range(-0.4f, 0.4f);
            _limp = UnityEngine.Random.value < 0.35f ? UnityEngine.Random.Range(0.3f, 0.8f) : 0;
            _headTilt = UnityEngine.Random.Range(-18f, 18f);
            _growlT = UnityEngine.Random.Range(1f, 6f);
            _lastSeen = PlayerRig.Pos;
            Rig.ResetLook();
            if (!Alive.Contains(this)) Alive.Add(this);
        }

        void ResetBones()
        {
            for (int i = 1; i < Rig.Bones.Length; i++)
            {
                Rig.Bones[i].localRotation = Quaternion.identity;
                Rig.Bones[i].localScale = Vector3.one;
                Rig.Bones[i].localPosition = ZombieRig.BindPos[i] - ZombieRig.BindPos[(int)ParentOf((ZB)i)];
            }
        }

        static ZB ParentOf(ZB b)
        {
            switch (b)
            {
                case ZB.Hips: return ZB.Root;
                case ZB.Spine: return ZB.Hips;
                case ZB.Chest: return ZB.Spine;
                case ZB.Neck: return ZB.Chest;
                case ZB.Head: return ZB.Neck;
                case ZB.Jaw: return ZB.Head;
                case ZB.ClavL: case ZB.ClavR: return ZB.Chest;
                case ZB.ArmL: return ZB.ClavL;
                case ZB.ForeL: return ZB.ArmL;
                case ZB.HandL: return ZB.ForeL;
                case ZB.ArmR: return ZB.ClavR;
                case ZB.ForeR: return ZB.ArmR;
                case ZB.HandR: return ZB.ForeR;
                case ZB.ThighL: case ZB.ThighR: return ZB.Hips;
                case ZB.ShinL: return ZB.ThighL;
                case ZB.FootL: return ZB.ShinL;
                case ZB.ShinR: return ZB.ThighR;
                case ZB.FootR: return ZB.ShinR;
                default: return ZB.Root;
            }
        }

        public void Despawn()
        {
            Alive.Remove(this);
            if (Agent.enabled) Agent.enabled = false;
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var player = PlayerRig.Instance;
            var ppos = PlayerRig.Pos;
            if (State == ZState.Dead)
            {
                DeadUpdate(dt);
                return;
            }
            var toP = ppos - transform.position;
            float dist = toP.Flat().magnitude;
            if (Frozen)
            {
                // Posed for showcase screenshots: idle animation only.
                _vel = Vector3.zero;
                if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
                Animate(dt, dist);
                return;
            }
            // Level of detail for animation updates.
            _lodFrame++;
            int lodStep = dist < 22 ? 1 : dist < 40 ? 2 : 4;
            if (!Rig.IsVisible) lodStep = 6;

            if (_burn > 0) BurnTick(dt);
            if (Type == ZombieType.Toxic) ToxicTick(dt, dist);
            if (Type == ZombieType.Hunter) CloakTick(dt, dist);
            if (_attackCd > 0) _attackCd -= dt;
            if (_specialCd > 0) _specialCd -= dt;
            Growl(dt, dist);

            switch (State)
            {
                case ZState.Spawning:
                    _spawnT -= dt;
                    Agent.isStopped = true;
                    if (_spawnT <= 0) { State = Aware ? ZState.Chase : ZState.Idle; Agent.isStopped = false; }
                    break;
                case ZState.Idle:
                    Perceive(dt, dist, toP);
                    Wander(dt);
                    break;
                case ZState.Chase:
                    Chase(dt, dist, toP, player);
                    break;
                case ZState.Search:
                    Perceive(dt, dist, toP);
                    if (State == ZState.Search && Agent.enabled && Agent.isOnNavMesh && !Agent.pathPending && Agent.remainingDistance < 1.5f)
                    {
                        _lostSight += dt;
                        if (_lostSight > 4) { State = ZState.Chase; _lostSight = 0; }
                    }
                    break;
                case ZState.Attack:
                    AttackUpdate(dt, dist, toP);
                    break;
                case ZState.Special:
                    SpecialUpdate(dt, dist, toP);
                    break;
                case ZState.Stagger:
                    _stagger -= dt;
                    if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
                    if (_stagger <= 0) { State = ZState.Chase; if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false; }
                    break;
            }

            // Face the movement / the player.
            var v = Agent.enabled ? Agent.velocity : Vector3.zero;
            _vel = Vector3.Lerp(_vel, v, 1 - Mathf.Exp(-dt * 8));
            Vector3 look = State == ZState.Attack || dist < 3f ? toP.Flat() : _vel.Flat();
            if (look.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(look);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, (State == ZState.Attack ? 240 : 300) * dt);
            }
            if (_lodFrame % lodStep == 0) Animate(dt * lodStep, dist);
        }

        void Perceive(float dt, float dist, Vector3 toP)
        {
            _sense -= dt;
            if (_sense > 0) return;
            _sense = 0.3f;
            if (PlayerRig.Instance == null || !PlayerRig.Instance.Alive) return;
            float range = Elite ? 40 : 30;
            if (Aware || dist < 4) { State = ZState.Chase; Aware = true; return; }
            if (dist > range) return;
            float ang = Vector3.Angle(transform.forward, toP.Flat());
            bool lit = PlayerRig.Instance.FlashlightOn;
            if (ang < (lit ? 75 : 60) || dist < 7)
            {
                if (!Physics.Linecast(transform.position + Vector3.up * 1.6f, PlayerRig.Pos + Vector3.up * 1.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    State = ZState.Chase;
                    Aware = true;
                    _lastSeen = PlayerRig.Pos;
                    if (UnityEngine.Random.value < 0.5f) Sfx.ZombieAlert(this);
                }
            }
        }

        /// <summary>Gunfire and explosions alert zombies within earshot.</summary>
        public static void Noise(Vector3 p, float radius)
        {
            float r2 = radius * radius;
            foreach (var z in Alive)
            {
                if (z.State == ZState.Idle || z.State == ZState.Search)
                    if ((z.transform.position - p).sqrMagnitude < r2) { z.Aware = true; z._lastSeen = p; z.State = ZState.Chase; }
            }
        }

        void Wander(float dt)
        {
            _idleT -= dt;
            if (_idleT > 0 || !Agent.isOnNavMesh) return;
            _idleT = UnityEngine.Random.Range(3f, 7f);
            var p = transform.position + new Vector3(UnityEngine.Random.Range(-5f, 5f), 0, UnityEngine.Random.Range(-5f, 5f));
            Agent.speed = WalkSpeed * 0.7f;
            Agent.SetDestination(Level.OnNav(p, 2));
        }

        void Chase(float dt, float dist, Vector3 toP, PlayerRig player)
        {
            if (player == null || !player.Alive) { State = ZState.Idle; return; }
            if (!Agent.isOnNavMesh) return;
            // Pace: shamblers hurry while far away (keeps waves flowing), then lurch in at their own speed.
            float sp = Speed;
            if (Type == ZombieType.Walker || Type == ZombieType.Armored || Type == ZombieType.Toxic || Type == ZombieType.Explosive)
                sp = Mathf.Lerp(Speed, Speed * 1.55f, Mathf.Clamp01((dist - 10) / 14));
            if (Type == ZombieType.Hunter && dist > 10) sp = Speed * 0.75f;
            Agent.speed = sp * (_headGone ? 0.5f : 1) * (_burn > 0 ? 1.15f : 1);
            // Special attacks.
            if (TrySpecial(dist, toP)) return;
            // Melee range.
            float range = Def.Range * transform.localScale.x;
            if (dist < range && _attackCd <= 0)
            {
                StartAttack();
                return;
            }
            _repath -= dt;
            if (_repath <= 0)
            {
                _repath = dist < 8 ? 0.15f : dist < 20 ? 0.4f : 0.8f;
                Vector3 target = PlayerRig.Pos;
                // Surround: approach to an assigned slot around the player when close.
                if (dist < 9 && dist > range * 1.3f)
                {
                    var off = Quaternion.Euler(0, SurroundAngle, 0) * Vector3.forward * Mathf.Min(dist * 0.6f, 2.2f);
                    target = PlayerRig.Pos + off;
                }
                Agent.SetDestination(target);
            }
        }

        // --------------------------------------------------------------- attacks

        void StartAttack()
        {
            State = ZState.Attack;
            _attackT = 0;
            _hitThisAttack = false;
            _attackKind = UnityEngine.Random.Range(0, 2);
            if (Agent.isOnNavMesh) Agent.isStopped = true;
            Sfx.ZombieAttack(this);
        }

        float AttackDuration => Type == ZombieType.Runner || Type == ZombieType.Hunter ? 0.7f : Type == ZombieType.Mutant ? 1.3f : 1.0f;

        void AttackUpdate(float dt, float dist, Vector3 toP)
        {
            _attackT += dt / AttackDuration;
            float hitAt = 0.5f;
            if (!_hitThisAttack && _attackT >= hitAt)
            {
                _hitThisAttack = true;
                float range = Def.Range * transform.localScale.x * 1.25f;
                if (dist < range && Vector3.Angle(transform.forward, toP.Flat()) < 70 && PlayerRig.Instance != null)
                {
                    if (Type == ZombieType.Mutant)
                    {
                        // Ground slam: area hit with knockback.
                        Fx.Impact(transform.position + transform.forward * 1.2f, Vector3.up, SurfaceKind.Concrete, 2.5f);
                        CameraShake.Add(0.6f);
                    }
                    PlayerRig.Instance.Hurt(Damage, transform.position, HurtKind.Melee);
                }
            }
            if (_attackT >= 1)
            {
                _attackT = -1;
                _attackCd = Def.AttackInterval * UnityEngine.Random.Range(0.8f, 1.2f);
                State = ZState.Chase;
                if (Agent.isOnNavMesh) Agent.isStopped = false;
            }
        }

        bool TrySpecial(float dist, Vector3 toP)
        {
            if (_specialCd > 0) return false;
            switch (Type)
            {
                case ZombieType.Explosive:
                    if (dist < Def.Range) { BeginSpecial(0.9f); Sfx.Bloater(this); return true; }
                    break;
                case ZombieType.Toxic:
                    if (dist > 5 && dist < 15 && HasLos()) { BeginSpecial(0.8f); return true; }
                    break;
                case ZombieType.Mutant:
                    if (dist > 6 && dist < 16 && HasLos()) { BeginSpecial(1.6f); _charge = toP.Flat().normalized; Sfx.MutantRoar(this); return true; }
                    break;
                case ZombieType.Hunter:
                case ZombieType.Crawler:
                    if (dist > 2.2f && dist < 6.5f && HasLos()) { BeginSpecial(0.65f); _charge = toP.Flat().normalized; Sfx.ZombieAttack(this); return true; }
                    break;
            }
            return false;
        }

        bool HasLos() => !Physics.Linecast(transform.position + Vector3.up * 1.4f, PlayerRig.Pos + Vector3.up * 1.4f, Layers.WorldMask, QueryTriggerInteraction.Ignore);

        void BeginSpecial(float duration)
        {
            State = ZState.Special;
            _specialT = 0;
            _specialDur = duration;
            _hitThisAttack = false;
            if (Agent.isOnNavMesh) Agent.isStopped = Type == ZombieType.Explosive || Type == ZombieType.Toxic;
        }

        float _specialDur = 1;

        void SpecialUpdate(float dt, float dist, Vector3 toP)
        {
            _specialT += dt / _specialDur;
            switch (Type)
            {
                case ZombieType.Explosive:
                    // Swell, glow, then burst.
                    if (_specialT >= 1) { Explode(); return; }
                    break;
                case ZombieType.Toxic:
                    if (!_hitThisAttack && _specialT > 0.55f)
                    {
                        _hitThisAttack = true;
                        var from = Rig[ZB.Head].position + transform.forward * 0.2f;
                        Projectile.Acid(from, PlayerRig.Pos + Vector3.up * 0.9f, Damage * 0.9f);
                    }
                    break;
                case ZombieType.Mutant:
                case ZombieType.Hunter:
                case ZombieType.Crawler:
                {
                    // Charge / pounce: drive along the charge direction.
                    float spd = Type == ZombieType.Mutant ? 11f : 9f;
                    if (_specialT > 0.25f && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = true;
                        Agent.Move(_charge * spd * dt);
                    }
                    if (!_hitThisAttack && dist < 1.6f * transform.localScale.x && PlayerRig.Instance != null)
                    {
                        _hitThisAttack = true;
                        PlayerRig.Instance.Hurt(Damage * (Type == ZombieType.Mutant ? 1.3f : 1.1f), transform.position, HurtKind.Melee);
                        CameraShake.Add(Type == ZombieType.Mutant ? 0.8f : 0.4f);
                        _specialT = Mathf.Max(_specialT, 0.85f);
                    }
                    break;
                }
            }
            if (_specialT >= 1)
            {
                State = ZState.Chase;
                _specialCd = Type == ZombieType.Toxic ? UnityEngine.Random.Range(4f, 7f) : UnityEngine.Random.Range(5f, 9f);
                if (Agent.isOnNavMesh) Agent.isStopped = false;
            }
        }

        void Explode()
        {
            if (State == ZState.Dead && _deathT > 0.4f) return;
            var p = transform.position + Vector3.up * 1f;
            Hp = 0;
            var info = new DamageInfo { Amount = 0, Point = p, Dir = Vector3.up, Explosive = true, FromPlayer = _killedByPlayer };
            Die(info, false);
            Rig.SetVisible(false);
            Blast.Explode(p, 4.5f, Damage * 3.5f, _killedByPlayer, false, Damage);
            Fx.Gore(p, Vector3.up, 2f);
            Zombie.Noise(p, 30);
        }

        bool _killedByPlayer;

        // --------------------------------------------------------------- ticks

        void BurnTick(float dt)
        {
            _burn -= dt;
            _burnTick -= dt;
            if (_burnTick <= 0)
            {
                _burnTick = 0.25f;
                Fx.Burn(Rig[ZB.Chest].position, transform.localScale.x);
                if (UnityEngine.Random.value < 0.5f) Fx.Burn(Rig[ZB.Head].position, transform.localScale.x * 0.7f);
                TakeDamage(new DamageInfo { Amount = _burnDps * 0.25f, Point = Rig[ZB.Chest].position, Dir = Vector3.up, Zone = HitZone.Body, Fire = true, ArmorPierce = 1, FromPlayer = true, Class = WeaponClass.Special });
            }
        }

        float _burnDps;

        public void Ignite(float dps, float duration)
        {
            _burnDps = Mathf.Max(_burnDps, dps);
            _burn = Mathf.Max(_burn, duration);
        }

        void ToxicTick(float dt, float dist)
        {
            _gasT -= dt;
            if (_gasT > 0) return;
            _gasT = 0.35f;
            if (dist < 30) Fx.Gas(Rig[ZB.Chest].position, 0.9f, new Color(0.35f, 0.6f, 0.15f, 0.25f));
            if (dist < 2.2f && PlayerRig.Instance != null) PlayerRig.Instance.Hurt(Damage * 0.12f, transform.position, HurtKind.Toxic);
        }

        /// <summary>A cloaked Hunter (shimmering, nearly invisible): the minimap leaves it out.</summary>
        public bool Cloaked => Type == ZombieType.Hunter && _cloak > 0.5f;

        void CloakTick(float dt, float dist)
        {
            float target = dist > 7.5f && State != ZState.Special && State != ZState.Attack && State != ZState.Stagger ? 1 : 0;
            _cloak = Mathf.MoveTowards(_cloak, target, dt * 1.5f);
            Rig.SetCloak(_cloak > 0.5f);
        }

        void Growl(float dt, float dist)
        {
            _growlT -= dt;
            if (_growlT > 0) return;
            _growlT = UnityEngine.Random.Range(3f, 8f);
            if (dist < 28) Sfx.ZombieGrowl(this);
        }

        // --------------------------------------------------------------- damage

        public void TakeDamage(DamageInfo d)
        {
            if (State == ZState.Dead) return;
            float amt = d.Amount;
            if (d.Zone == HitZone.Head)
            {
                float headMul = d.HeadMul > 0 ? d.HeadMul : 2.5f;
                // Helmets cut the headshot bonus.
                amt *= 1 + (headMul - 1) * Def.HeadMul;
            }
            else if (d.Zone == HitZone.Limb) amt *= 0.8f;
            if (!d.Fire && d.Zone != HitZone.Head) amt *= 1 - Def.BodyArmor * (1 - d.ArmorPierce);
            if (_plan != null && (_plan.Mutators & Mutator.HeadshotFocus) != 0 && d.Zone != HitZone.Head && !d.Explosive && !d.Fire) amt *= 0.35f;
            if (Elite && d.FromPlayer) amt *= PlayerRig.Stats.EliteMul;
            Hp -= amt;
            _killedByPlayer = d.FromPlayer;
            Aware = true;
            if (State == ZState.Idle || State == ZState.Search) State = ZState.Chase;
            Damaged?.Invoke(this, d);
            // Armor sparks vs blood.
            bool armored = (Type == ZombieType.Armored || Type == ZombieType.Military) && d.Zone != HitZone.Head && !d.Fire && !d.Explosive;
            if (!d.Fire && !d.Explosive)
            {
                if (armored && UnityEngine.Random.value < 0.6f) Fx.Impact(d.Point, -d.Dir, SurfaceKind.Metal);
                else Fx.Blood(d.Point, d.Dir, d.Zone == HitZone.Head ? 1f : 0.55f);
            }
            // Flinch springs.
            var local = transform.InverseTransformDirection(d.Dir);
            float k = Mathf.Clamp(amt / Mathf.Max(30, MaxHp * 0.25f), 0.2f, 1.6f);
            _flinchX.Velocity += local.z * 220 * k;
            _flinchZ.Velocity += -local.x * 180 * k;
            if (d.Zone == HitZone.Head) _headX.Velocity -= 500 * k;
            if (Hp <= 0)
            {
                if (Type == ZombieType.Explosive && !d.Fire) { Explode(); Killed?.Invoke(this, d); return; }
                Die(d, true);
                return;
            }
            // Stagger on heavy hits.
            float stagger = d.Stagger + (amt > MaxHp * 0.35f ? 0.5f : 0);
            if (stagger > 0.3f && Type != ZombieType.Mutant && State != ZState.Special)
            {
                State = ZState.Stagger;
                _stagger = Mathf.Clamp(stagger * 0.5f, 0.25f, 0.9f);
                _attackT = -1;
            }
            // Lose a forearm to heavy limb hits.
            if (d.Zone == HitZone.Limb && amt > 70 && !_armGone && d.Bone != null && (d.Bone == Rig[ZB.ForeL] || d.Bone == Rig[ZB.ForeR]))
            {
                _armGone = true;
                d.Bone.localScale = Vector3.one * 0.001f;
                Fx.Gore(d.Bone.position, d.Dir, 0.6f);
            }
        }

        void Die(DamageInfo d, bool notify)
        {
            if (State == ZState.Dead) return;
            State = ZState.Dead;
            DeadTime = Time.time;
            _deathT = 0;
            _deathKind = Vector3.Dot(d.Dir, transform.forward) > 0 ? 1 : 0; // shot from behind falls forward
            if (UnityEngine.Random.value < 0.3f) _deathKind = 2;          // collapse to the knees
            if (Agent.enabled) Agent.enabled = false;
            foreach (var hb in _hitboxes) hb.gameObject.SetActive(false);
            Alive.Remove(this);
            // Headshot gore: big hits pop the head.
            if (d.Zone == HitZone.Head && (d.Crit || d.Amount > 70 || d.Explosive))
            {
                _headGone = true;
                Rig[ZB.Head].localScale = Vector3.one * 0.001f;
                Fx.Gore(Rig[ZB.Head].position, d.Dir, 1.2f);
            }
            if (d.Explosive && UnityEngine.Random.value < 0.4f)
            {
                var limb = UnityEngine.Random.value < 0.5f ? ZB.ForeL : ZB.ShinR;
                Rig[limb].localScale = Vector3.one * 0.001f;
                Fx.Gore(Rig[limb].position, d.Dir, 0.8f);
            }
            Sfx.ZombieDeath(this);
            if (Type == ZombieType.Toxic) Hazards.SpawnToxicPool(transform.position, 6f, Damage * 0.6f);
            if (notify) Killed?.Invoke(this, d);
        }

        void DeadUpdate(float dt)
        {
            _deathT += dt;
            float k = X.Smooth01(_deathT / 0.75f);
            // Fall about the feet: pitch back / forward, or knees first.
            float pitch = _deathKind == 0 ? -88 : _deathKind == 1 ? 86 : 86;
            float delay = _deathKind == 2 ? 0.35f : 0;
            float fk = X.Smooth01((_deathT - delay) / 0.6f);
            transform.rotation = _baseRotAtDeath() * Quaternion.Euler(pitch * fk, 0, 0);
            float buckle = _deathKind == 2 ? X.Smooth01(_deathT / 0.35f) : k * 0.5f;
            Rig[ZB.ThighL].localRotation = Quaternion.Euler(-40 * buckle, 0, -5);
            Rig[ZB.ThighR].localRotation = Quaternion.Euler(-30 * buckle, 0, 5);
            Rig[ZB.ShinL].localRotation = Quaternion.Euler(70 * buckle, 0, 0);
            Rig[ZB.ShinR].localRotation = Quaternion.Euler(60 * buckle, 0, 0);
            Rig[ZB.ArmL].localRotation = Quaternion.Slerp(Rig[ZB.ArmL].localRotation, Quaternion.Euler(-30 - 90 * fk, 0, -50 * fk), k);
            Rig[ZB.ArmR].localRotation = Quaternion.Slerp(Rig[ZB.ArmR].localRotation, Quaternion.Euler(-20 - 80 * fk, 0, 60 * fk), k);
            Rig[ZB.Head].localRotation = Quaternion.Slerp(Rig[ZB.Head].localRotation, Quaternion.Euler(0, 40, 20), k);
            Rig[ZB.Jaw].localRotation = Quaternion.Euler(18, 0, 0);
            if (_deathT > 0.8f && _deathT - dt <= 0.8f) Fx.BloodPool(transform.position + transform.up * 0.6f, UnityEngine.Random.Range(1f, 1.8f));
            // Sink away after a while (the Director recycles the body).
            float stay = 6f;
            if (_deathT > stay) transform.position += Vector3.down * dt * 0.25f;
        }

        Quaternion _deathBase;
        bool _deathBaseSet;

        Quaternion _baseRotAtDeath()
        {
            if (!_deathBaseSet || _deathT < 0.02f)
            {
                _deathBase = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                _deathBaseSet = true;
            }
            return _deathBase;
        }

        public bool ReadyToRecycle => State == ZState.Dead && _deathT > 7.5f;

        // --------------------------------------------------------------- animation

        void Animate(float dt, float dist)
        {
            float speed = _vel.Flat().magnitude / Mathf.Max(0.5f, transform.localScale.x);
            int gait = GaitFor(speed);
            float stride = gait == 1 ? 1.9f : gait == 2 ? 0.9f : gait == 3 ? 2.2f : gait == 4 ? 1.6f : 1.05f;
            _phase += speed / stride * Mathf.PI * dt * (1 - _limp * 0.15f);
            if (speed < 0.1f) _phase += dt * 1.2f;
            float ph = _phase;
            float move = Mathf.Clamp01(speed / 0.6f);

            _flinchX.Step(0, 120, 14, dt);
            _flinchZ.Step(0, 120, 14, dt);
            _headX.Step(0, 160, 16, dt);

            float lean = gait == 1 ? 26 : gait == 3 ? 38 : gait == 4 ? 18 : 14;
            float bob = 0, sway = Mathf.Sin(ph) * 5 * move;
            float thighA = gait == 1 ? 42 : gait == 3 ? 46 : gait == 4 ? 28 : 22;
            float kneeA = gait == 1 ? 75 : gait == 3 ? 80 : 45;
            var hips = Rig[ZB.Hips];
            if (gait == 2)
            {
                // Crawl: body near the ground, arms pulling.
                hips.localPosition = new Vector3(0, 0.36f, 0.25f);
                hips.localRotation = Quaternion.Euler(78, Mathf.Sin(ph) * 8, Mathf.Sin(ph) * 6);
                Rig[ZB.Spine].localRotation = Quaternion.Euler(-8, 0, 0);
                Rig[ZB.Chest].localRotation = Quaternion.Euler(-12 + _flinchX.Value * 0.3f, Mathf.Sin(ph) * 10, 0);
                Rig[ZB.Neck].localRotation = Quaternion.Euler(-45, 0, 0);
                Rig[ZB.Head].localRotation = Quaternion.Euler(-25 + _headX.Value, Mathf.Sin(ph * 0.5f) * 15, _headTilt * 0.5f);
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(-150 + Mathf.Sin(ph) * 35, 0, -12);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-150 - Mathf.Sin(ph) * 35, 0, 12);
                Rig[ZB.ForeL].localRotation = Quaternion.Euler(-20 - Mathf.Max(0, Mathf.Cos(ph)) * 50, 0, 0);
                Rig[ZB.ForeR].localRotation = Quaternion.Euler(-20 - Mathf.Max(0, -Mathf.Cos(ph)) * 50, 0, 0);
                Rig[ZB.ThighL].localRotation = Quaternion.Euler(10 + Mathf.Sin(ph) * 8, 0, -6);
                Rig[ZB.ThighR].localRotation = Quaternion.Euler(10 - Mathf.Sin(ph) * 8, 0, 6);
                Rig[ZB.ShinL].localRotation = Quaternion.Euler(20, 0, 0);
                Rig[ZB.ShinR].localRotation = Quaternion.Euler(25, 0, 0);
                Jaw(dt);
                AttackOverlay();
                return;
            }
            bob = -Mathf.Abs(Mathf.Cos(ph)) * (gait == 1 ? 0.06f : 0.035f) * move;
            hips.localPosition = new Vector3(0, ZombieRig.BindPos[(int)ZB.Hips].y + bob - (gait == 3 ? 0.18f : 0) - _limp * 0.02f * Mathf.Max(0, Mathf.Sin(ph)), 0);
            hips.localRotation = Quaternion.Euler(gait == 3 ? 12 : 4, Mathf.Sin(ph) * 7 * move, sway + _limp * 4 * Mathf.Sin(ph));
            Rig[ZB.Spine].localRotation = Quaternion.Euler(lean * 0.45f + _flinchX.Value * 0.5f, -Mathf.Sin(ph) * 5 * move, _flinchZ.Value * 0.4f);
            Rig[ZB.Chest].localRotation = Quaternion.Euler(lean * 0.55f + _flinchX.Value * 0.6f + (Type == ZombieType.Mutant ? 10 : 0), -Mathf.Sin(ph) * 8 * move, -sway * 0.6f + _flinchZ.Value * 0.6f);
            Rig[ZB.Neck].localRotation = Quaternion.Euler(-lean * 0.5f, 0, 0);
            float twitch = Mathf.PerlinNoise(Time.time * 3 + _gaitSeed * 10, 0) > 0.82f ? UnityEngine.Random.Range(-10f, 10f) : 0;
            Rig[ZB.Head].localRotation = Quaternion.Euler(-lean * 0.35f + 8 + _headX.Value + (gait == 0 ? Mathf.Sin(ph * 2) * 4 : 0), twitch + Mathf.Sin(Time.time * 0.7f + _gaitSeed * 6) * 12, _headTilt + twitch * 0.5f);
            // Legs.
            float limpL = 1 - _limp * 0.5f;
            LegPose(ZB.ThighL, ZB.ShinL, ZB.FootL, ph, thighA * limpL, kneeA * limpL, move, -1);
            LegPose(ZB.ThighR, ZB.ShinR, ZB.FootR, ph + Mathf.PI, thighA, kneeA, move, 1);
            // Arms.
            if (gait == 1 || gait == 3)
            {
                float swing = gait == 3 ? 20 : 55;
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(Mathf.Sin(ph) * swing * move - (gait == 3 ? -30 : 0), 0, -14);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * swing * move - (gait == 3 ? -30 : 0), 0, 14);
                Rig[ZB.ForeL].localRotation = Quaternion.Euler(-70 - Mathf.Max(0, Mathf.Sin(ph)) * 20, 0, 0);
                Rig[ZB.ForeR].localRotation = Quaternion.Euler(-70 - Mathf.Max(0, -Mathf.Sin(ph)) * 20, 0, 0);
            }
            else if (Type == ZombieType.Explosive)
            {
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 10, 0, -38);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * 10, 0, 38);
                Rig[ZB.ForeL].localRotation = Quaternion.Euler(-25, 0, 0);
                Rig[ZB.ForeR].localRotation = Quaternion.Euler(-25, 0, 0);
            }
            else
            {
                // Classic reach: one or both arms raised toward the target, swaying with the gait.
                float reachL = Mathf.Clamp01(_armReach - _armsAsym) * (dist < 12 ? 1 : 0.6f);
                float reachR = Mathf.Clamp01(_armReach + _armsAsym) * (dist < 12 ? 1 : 0.6f);
                float hangSwingL = Mathf.Sin(ph) * 18 * move, hangSwingR = -Mathf.Sin(ph) * 18 * move;
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(Mathf.Lerp(hangSwingL, -78 + Mathf.Sin(ph * 2 + 1) * 6, reachL), 0, Mathf.Lerp(-8, -4, reachL));
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(Mathf.Lerp(hangSwingR, -74 + Mathf.Sin(ph * 2) * 6, reachR), 0, Mathf.Lerp(8, 4, reachR));
                Rig[ZB.ForeL].localRotation = Quaternion.Euler(Mathf.Lerp(-12, -22, reachL), 0, 0);
                Rig[ZB.ForeR].localRotation = Quaternion.Euler(Mathf.Lerp(-12, -18, reachR), 0, 0);
                Rig[ZB.HandL].localRotation = Quaternion.Euler(Mathf.Lerp(0, 15, reachL), 0, 0);
                Rig[ZB.HandR].localRotation = Quaternion.Euler(Mathf.Lerp(0, 15, reachR), 0, 0);
            }
            if (Type == ZombieType.Mutant)
            {
                Rig[ZB.ClavL].localRotation = Quaternion.Euler(0, 0, 12);
                Rig[ZB.ClavR].localRotation = Quaternion.Euler(0, 0, -12);
            }
            Jaw(dt);
            AttackOverlay();
            // Bloater swelling before it bursts.
            if (Type == ZombieType.Explosive)
            {
                float swell = State == ZState.Special ? 1 + _specialT * 0.25f + Mathf.Sin(Time.time * 30) * 0.02f : 1;
                Rig[ZB.Spine].localScale = new Vector3(swell, 1, swell);
            }
        }

        int GaitFor(float speed)
        {
            if (Type == ZombieType.Crawler) return 2;
            if (Type == ZombieType.Hunter) return speed > 2 ? 3 : 0;
            if (Type == ZombieType.Mutant) return 4;
            if (speed > 2.6f || (Type == ZombieType.Runner && speed > 1.5f) || (Type == ZombieType.Military && speed > 1.8f)) return 1;
            return 0;
        }

        void LegPose(ZB thigh, ZB shin, ZB foot, float p, float thighA, float kneeA, float move, int side)
        {
            float t = -Mathf.Sin(p) * thighA * move;
            float k = Mathf.Max(0, Mathf.Cos(p)) * kneeA * move + 6;
            Rig[thigh].localRotation = Quaternion.Euler(t, side * 4, side * 2);
            Rig[shin].localRotation = Quaternion.Euler(k, 0, 0);
            Rig[foot].localRotation = Quaternion.Euler(-(t + k) * 0.5f, side * 6, 0);
        }

        void Jaw(float dt)
        {
            float open = State == ZState.Attack || State == ZState.Special ? 24 : 6 + Mathf.Max(0, Mathf.Sin(Time.time * 2.2f + _gaitSeed * 9)) * 10;
            Rig[ZB.Jaw].localRotation = Quaternion.Euler(open, 0, 0);
        }

        /// <summary>Layers the swipe / lunge on top of the gait pose.</summary>
        void AttackOverlay()
        {
            if (State == ZState.Attack && _attackT >= 0)
            {
                float t = _attackT;
                float wind = X.Bump(t, 0f, 0.55f), strike = X.Bump(t, 0.35f, 0.8f);
                float armWind = -160 * wind, armStrike = 60 * strike;
                if (_attackKind == 0 || Type == ZombieType.Mutant)
                {
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-40 + armWind + armStrike, 0, -10);
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-40 + armWind + armStrike, 0, 10);
                }
                else
                {
                    // Single swipe across.
                    Rig[ZB.ArmR].localRotation = Quaternion.Euler(-70 - 40 * wind, -60 * wind + 90 * strike, 40 * wind);
                    Rig[ZB.ArmL].localRotation = Quaternion.Euler(-80, 0, -10);
                }
                Rig[ZB.ForeL].localRotation = Quaternion.Euler(-30 - 30 * wind, 0, 0);
                Rig[ZB.ForeR].localRotation = Quaternion.Euler(-30 - 30 * wind, 0, 0);
                Rig[ZB.Chest].localRotation *= Quaternion.Euler(-15 * wind + 30 * strike, 0, 0);
            }
            else if (State == ZState.Special && Type == ZombieType.Toxic)
            {
                float t = _specialT;
                Rig[ZB.Chest].localRotation *= Quaternion.Euler(-25 * X.Bump(t, 0, 0.55f) + 35 * X.Bump(t, 0.45f, 0.9f), 0, 0);
                Rig[ZB.Jaw].localRotation = Quaternion.Euler(30 * X.Bump(t, 0.3f, 0.9f), 0, 0);
            }
            else if (State == ZState.Special && (Type == ZombieType.Hunter || Type == ZombieType.Crawler || Type == ZombieType.Mutant))
            {
                float t = _specialT;
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(-120, 0, -20);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-120, 0, 20);
                Rig[ZB.Chest].localRotation *= Quaternion.Euler(25, 0, 0);
            }
            else if (State == ZState.Stagger)
            {
                Rig[ZB.Spine].localRotation *= Quaternion.Euler(-18, 0, 8);
                Rig[ZB.ArmL].localRotation = Quaternion.Euler(-20, 0, -40);
                Rig[ZB.ArmR].localRotation = Quaternion.Euler(-10, 0, 45);
            }
            else if (State == ZState.Spawning)
            {
                float k = Mathf.Clamp01(_spawnT / 0.8f);
                Rig[ZB.Chest].localRotation *= Quaternion.Euler(40 * k, 0, 0);
                Rig[ZB.Head].localRotation *= Quaternion.Euler(30 * k, 0, 0);
            }
        }
    }
}
