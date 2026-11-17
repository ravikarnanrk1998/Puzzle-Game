using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    public sealed class WeaponSlot
    {
        public WeaponId Id;
        public WeaponDef Def;
        public WeaponStats St;
        public WeaponSave Save;
        public int Mag, Reserve;
        public GunModel Model;
        public bool Infinite;
        public float Cycle;          // pump / bolt cycle still running
        public int Fired;
    }

    /// <summary>
    /// Weapons: loadout, firing (hitscan with pierce and falloff, pellets, burst, pump, bolt, flame,
    /// launcher, arc), reloading, switching, grenades, aim-down-sights, aim assist and auto-fire.
    /// Also animates the first-person viewmodel and the gloved arms.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class Gunplay : MonoBehaviour
    {
        public readonly List<WeaponSlot> Slots = new List<WeaponSlot>();
        public int Current;
        public float AimAmount;
        public bool Reloading;
        public int Grenades;
        public int ShotsFired, ShotsHit;
        public WeaponSlot W => Slots[Mathf.Clamp(Current, 0, Slots.Count - 1)];
        public float Zoom => W.Model != null && W.Model.HasScope ? W.St.AdsZoom : W.St.AdsZoom;
        public float MoveMul => W.Def.MoveMul;
        public bool Scoped => AimAmount > 0.92f && W.Model != null && W.Model.HasScope;
        public float Spread { get; private set; }
        public System.Action<bool, bool> OnHit;       // (kill, headshot)
        public Zombie AssistTarget;

        PlayerRig _p;
        Transform _vmRoot, _holder;
        Arms _arms;
        float _cool, _reloadT, _reloadDur, _switchT = 1, _bloom, _flashT, _idleT, _inspectT = -1, _grenadeT = -1, _flameTick, _burstGap;
        int _burstLeft, _shellsToLoad;
        bool _holstered, _triggerHeld;
        Spring3 _swayPos, _swayRot, _kickPos, _kickRot;
        Vector2 _lastLook;
        GameObject _flash;
        Light _muzzleLight;
        static readonly RaycastHit[] Hits = new RaycastHit[24];
        static readonly HashSet<IDamageable> HitSet = new HashSet<IDamageable>();

        public void Init(PlayerRig p)
        {
            _p = p;
            _vmRoot = X.Child(p.ViewCam.transform, "Viewmodel").transform;
            _holder = X.Child(_vmRoot, "Holder").transform;
            var d = SaveSystem.Data;
            var plan = StageRunner.Instance != null ? StageRunner.Instance.Plan : null;
            var ids = new List<WeaponId>();
            for (int i = 0; i < d.LoadoutSlots; i++)
                if (d.Loadout[i] >= 0 && !ids.Contains((WeaponId)d.Loadout[i])) ids.Add((WeaponId)d.Loadout[i]);
            if (plan != null && plan.AllowedClass >= 0)
            {
                // Daily challenge restriction: keep only allowed weapons; lend one if none owned.
                var cls = (WeaponClass)plan.AllowedClass;
                ids.RemoveAll(i => Weapons.Get(i).Class != cls);
                if (ids.Count == 0)
                {
                    foreach (var w in d.Weapons) if (Weapons.Get((WeaponId)w.Id).Class == cls) { ids.Add((WeaponId)w.Id); break; }
                    if (ids.Count == 0) ids.Add(cls == WeaponClass.Rifle ? WeaponId.VK74 : cls == WeaponClass.Shotgun ? WeaponId.PumpShotgun : WeaponId.StandardPistol);
                }
            }
            if (ids.Count == 0) ids.Add(WeaponId.StandardPistol);
            var stats = PlayerRig.Stats;
            foreach (var id in ids)
            {
                var ws = d.Weapon(id) ?? new WeaponSave { Id = (int)id };
                var st = PlayerProgress.Weapon(d, id, stats);
                var slot = new WeaponSlot { Id = id, Def = Weapons.Get(id), Save = ws, St = st, Mag = st.Mag, Reserve = st.Reserve, Infinite = st.Reserve >= 9999 };
                slot.Model = GunModels.Build(id, ws, _holder, Layers.Viewmodel);
                slot.Model.gameObject.SetActive(false);
                Slots.Add(slot);
            }
            Grenades = Mathf.Max(2, d.Grenades);
            _arc = GrenadeArc.Create(p.transform);
            _arms = new Arms(_holder.parent, (Outfit)d.Outfit, (Gloves)d.Gloves);
            BuildFlash();
            Current = 0;
            W.Model.gameObject.SetActive(true);
            _switchT = 0;
        }

        void BuildFlash()
        {
            var mesh = WorldKit.Get("muzzleflash", mb =>
            {
                for (int i = 0; i < 3; i++)
                {
                    mb.M = Matrix4x4.Rotate(Quaternion.Euler(0, 0, i * 60));
                    mb.Plane(0, new Vector3(-0.5f, 0, 0), Vector3.right, Vector3.forward * 1.6f, Vector2.zero, Vector2.one);
                    mb.Plane(0, new Vector3(0.5f, 0, 0), Vector3.left, Vector3.forward * 1.6f, Vector2.zero, Vector2.one);
                }
                mb.M = Matrix4x4.identity;
                mb.Plane(0, new Vector3(-0.6f, -0.6f, 0.05f), Vector3.right * 1.2f, Vector3.up * 1.2f, Vector2.zero, Vector2.one);
            });
            _flash = new GameObject("MuzzleFlash");
            _flash.layer = Layers.Viewmodel;
            _flash.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = _flash.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Cached("muzzleFlashMat", () => Mats.Additive(new Color(1f, 0.85f, 0.6f, 1f), Tex.Flash));
            r.shadowCastingMode = ShadowCastingMode.Off;
            _flash.SetActive(false);
            var lg = new GameObject("MuzzleLight");
            lg.transform.SetParent(transform, false);
            _muzzleLight = lg.AddComponent<Light>();
            _muzzleLight.type = LightType.Point;
            _muzzleLight.color = new Color(1f, 0.7f, 0.35f);
            _muzzleLight.range = 9;
            _muzzleLight.intensity = 0;
            _muzzleLight.shadows = LightShadows.None;
            LightLayers.Set(_muzzleLight, LightLayers.World);
            _muzzleLight.enabled = false;
            // A small flash that lights the weapon and hands themselves.
            var vg = new GameObject("VmFlash");
            vg.transform.SetParent(_vmRoot, false);
            vg.transform.localPosition = new Vector3(0.1f, -0.05f, 0.55f);
            _vmFlash = vg.AddComponent<Light>();
            _vmFlash.type = LightType.Point;
            _vmFlash.color = new Color(1f, 0.7f, 0.35f);
            _vmFlash.range = 0.9f;
            _vmFlash.shadows = LightShadows.None;
            LightLayers.Set(_vmFlash, LightLayers.Viewmodel);
            _vmFlash.enabled = false;
        }

        Light _vmFlash;

        /// <summary>Swaps the current slot's weapon (autopilot weapon showcase).</summary>
        public void DebugEquip(WeaponId id)
        {
            var d = SaveSystem.Data;
            var ws = d.Weapon(id) ?? new WeaponSave { Id = (int)id, Optic = Weapons.Get(id).BuiltInScope ? (int)Attachment.Scope : -1 };
            var st = PlayerProgress.Weapon(d, id, PlayerRig.Stats);
            var old = Slots[Current];
            if (old.Model != null)
            {
                if (_flash.transform.IsChildOf(old.Model.transform)) _flash.transform.SetParent(_vmRoot, false);
                Destroy(old.Model.gameObject);
            }
            var slot = new WeaponSlot { Id = id, Def = Weapons.Get(id), Save = ws, St = st, Mag = st.Mag, Reserve = st.Reserve, Infinite = st.Reserve >= 9999 };
            slot.Model = GunModels.Build(id, ws, _holder, Layers.Viewmodel);
            Slots[Current] = slot;
            _switchT = 1;
            AimAmount = 0;
            Reloading = false;
        }

        public void Holster()
        {
            _holstered = true;
            if (_vmRoot != null) _vmRoot.gameObject.SetActive(false);
        }

        public void Draw()
        {
            _holstered = false;
            _vmRoot.gameObject.SetActive(true);
            _switchT = 0;
        }

        // --------------------------------------------------------------- update

        // Grenade throw (shared by the throw and the aiming arc so the arc is exact).
        const float GrenadeDamage = 240, GrenadeRadius = 5.5f, GrenadeFuse = 2.2f;
        bool _nadeAiming, _nadeThrown;
        GrenadeArc _arc;

        /// <summary>The grenade button is held and the arc is showing.</summary>
        public bool GrenadeAiming => _nadeAiming;

        void ThrowStart(out Vector3 from, out Vector3 vel)
        {
            var cam = _p.WorldCam.transform;
            from = cam.position + cam.forward * 0.5f + cam.right * 0.1f;
            vel = cam.forward * 17f + Vector3.up * 3.5f + _p.Cc.velocity * 0.5f;
        }

        /// <summary>Drops the aim without throwing (the grenade is kept); the arm lowers.</summary>
        void CancelGrenade()
        {
            _nadeAiming = false;
            if (_arc != null) _arc.Hide();
            if (_grenadeT >= 0) _grenadeT = 0.88f;   // the arm lowers; past the release point, so nothing is thrown
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_nadeAiming && (_holstered || !_p.Alive)) CancelGrenade();
            if (dt <= 0 || _holstered || !_p.Alive) return;
            var input = Controls.State;
            var w = W;
            // Aim.
            bool wantAim = input.Aim && !Reloading && _switchT >= 1 && _grenadeT < 0;
            AimAmount = Mathf.MoveTowards(AimAmount, wantAim ? 1 : 0, dt / 0.17f);
            if (AimAmount > 0.1f) _inspectT = -1;
            // Switching.
            if (input.SelectSlot >= 0 && input.SelectSlot < Slots.Count && input.SelectSlot != Current) Switch(input.SelectSlot);
            else if (input.SwapPressed && Slots.Count > 1) Switch((Current + 1) % Slots.Count);
            _switchT = Mathf.Min(1, _switchT + dt / 0.38f);
            // Grenade. Holding the button pulls the pin and shows the red arc (exact predicted path and blast area);
            // releasing throws along that arc. A one-shot press (autopilot) throws straight away as before.
            bool canNade = Grenades > 0 && !Reloading;
            if (_nadeAiming)
            {
                if (input.GrenadeCancel || Grenades <= 0) CancelGrenade();
                else
                {
                    // Hold low (gun dipped, hand just in view) so the arm does not cover the arc.
                    _grenadeT = Mathf.Min(_grenadeT + dt / 0.8f, 0.12f);
                    ThrowStart(out var from, out var vel);
                    _arc.Show(from, vel, GrenadeFuse, GrenadeRadius, _p.WorldCam.transform.position);
                    if (!input.GrenadeHeld)
                    {
                        _nadeAiming = false;
                        _arc.Hide();
                        Grenades--;
                        Projectile.Grenade(from, vel, GrenadeDamage, GrenadeRadius);
                        Sfx.Throw();
                        _nadeThrown = true;   // the arm swing continues smoothly from the hold pose, without a second throw
                    }
                }
            }
            else if (input.GrenadeHeld && canNade && _grenadeT < 0) { _nadeAiming = true; _grenadeT = 0; Sfx.Pin(); }
            else if (input.GrenadePressed && canNade && _grenadeT < 0) { _grenadeT = 0; Grenades--; Sfx.Pin(); }
            if (_grenadeT >= 0 && !_nadeAiming)
            {
                float prev = _grenadeT;
                _grenadeT += dt / 0.8f;
                if (prev < 0.45f && _grenadeT >= 0.45f && !_nadeThrown)
                {
                    ThrowStart(out var from, out var vel);
                    Projectile.Grenade(from, vel, GrenadeDamage, GrenadeRadius);
                    Sfx.Throw();
                }
                if (_grenadeT >= 1) { _grenadeT = -1; _nadeThrown = false; }
            }
            // Reload.
            if (input.ReloadPressed) TryReload();
            if (Reloading) ReloadTick(dt);
            if (w.Cycle > 0) w.Cycle -= dt;
            _cool -= dt;
            _bloom = Mathf.Max(0, _bloom - dt * 3f);
            // Fire (with optional auto-fire on target).
            bool fire = input.Fire;
            UpdateAssist();
            if (!fire && SaveSystem.Data.Settings.AutoFire && AssistTarget != null && _assistAngle < 2.2f) fire = true;
            if (fire && _grenadeT < 0 && _switchT >= 0.9f) TryFire(dt);
            else if (!fire) { _triggerHeld = false; if (w.Def.Mode == FireMode.Flame) Sfx.FlameStop(); }
            if (fire) { _idleT = 0; _inspectT = -1; }
            // Auto reload.
            if (!Reloading && w.Mag <= 0 && (w.Reserve > 0 || w.Infinite) && _cool <= 0 && _burstLeft <= 0) TryReload();
            // Idle inspect.
            _idleT += dt;
            if (_idleT > 14 && _inspectT < 0 && !Reloading && AimAmount < 0.1f) { _inspectT = 0; _idleT = 0; }
            if (_inspectT >= 0) { _inspectT += dt / 2.6f; if (_inspectT >= 1) _inspectT = -1; }
            // Effects timers.
            _flashT -= dt;
            if (_flashT <= 0 && _flash.activeSelf) _flash.SetActive(false);
            if (_muzzleLight.enabled) { _muzzleLight.intensity = Mathf.Max(0, _muzzleLight.intensity - dt * 360); if (_muzzleLight.intensity <= 0.05f) _muzzleLight.enabled = false; }
            if (_vmFlash.enabled) { _vmFlash.intensity = Mathf.Max(0, _vmFlash.intensity - dt * 4.5f); if (_vmFlash.intensity <= 0.003f) _vmFlash.enabled = false; }
            // Spread for the crosshair.
            float moving = new Vector2(_p.Cc.velocity.x, _p.Cc.velocity.z).magnitude / 5f;
            Spread = Mathf.Lerp(w.St.HipSpread * (1 + moving * 0.6f) * (_p.Crouched ? 0.8f : 1f) + _bloom, w.St.AdsSpread + _bloom * 0.3f, AimAmount);
        }

        void LateUpdate()
        {
            if (_holstered || _p == null) return;
            Animate(Time.deltaTime);
        }

        void Switch(int slot)
        {
            if (Reloading) CancelReload();
            W.Model.gameObject.SetActive(false);
            Current = slot;
            W.Model.gameObject.SetActive(true);
            _switchT = 0;
            _burstLeft = 0;
            AimAmount = 0;
            Sfx.Equip();
        }

        // --------------------------------------------------------------- firing

        void TryFire(float dt)
        {
            var w = W;
            if (Reloading)
            {
                // Shell-by-shell reloads can be interrupted by firing.
                if (w.Def.ShellReload && w.Mag > 0) CancelReload();
                else return;
            }
            if (w.Mag <= 0)
            {
                if (!_triggerHeld) Sfx.DryFire();
                _triggerHeld = true;
                return;
            }
            if (w.Cycle > 0 || _cool > 0) return;
            switch (w.Def.Mode)
            {
                case FireMode.Burst:
                    if (_burstLeft <= 0 && !_triggerHeld) _burstLeft = w.Def.BurstCount;
                    if (_burstLeft > 0)
                    {
                        Shoot(w);
                        _burstLeft--;
                        _cool = _burstLeft > 0 ? w.St.Interval : 0.28f;
                    }
                    break;
                case FireMode.Flame:
                    Shoot(w);
                    _cool = 0.05f;
                    break;
                default:
                    Shoot(w);
                    _cool = Mathf.Max(w.St.Interval, w.Def.Mode == FireMode.Semi ? 0.085f : 0);
                    if (w.Def.Mode == FireMode.Pump || w.Def.Mode == FireMode.Bolt) w.Cycle = Mathf.Min(w.St.Interval * 0.9f, w.Def.Mode == FireMode.Bolt ? 0.9f : 0.5f);
                    break;
            }
            _triggerHeld = true;
        }

        Vector3 MuzzleWorld()
        {
            var m = W.Model.Muzzle;
            var vp = _p.ViewCam.WorldToViewportPoint(m.position);
            return _p.WorldCam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, Mathf.Max(0.3f, vp.z)));
        }

        void Shoot(WeaponSlot w)
        {
            w.Mag--;
            w.Fired++;
            ShotsFired++;
            var cam = _p.WorldCam.transform;
            var origin = cam.position;
            var muzzle = MuzzleWorld();
            bool suppressed = w.St.Suppressed;
            switch (w.Def.Mode)
            {
                case FireMode.Flame:
                    FlameTick(w, origin, cam.forward, muzzle);
                    break;
                case FireMode.Launcher:
                {
                    var target = origin + cam.forward * 60;
                    if (Physics.Raycast(origin, cam.forward, out var hit, 80, Layers.ShotMask, QueryTriggerInteraction.Ignore)) target = hit.point;
                    var dir = (target - muzzle).normalized;
                    bool rocket = w.Def.Id == WeaponId.RocketLauncher;
                    Projectile.LauncherShell(muzzle, dir * w.Def.ProjectileSpeed + (rocket ? Vector3.zero : Vector3.up * 1.2f), w.Def.ExplosionDamage * (w.St.Damage / w.Def.Damage), w.Def.ExplosionRadius, rocket);
                    Fx.Smoke(muzzle, 0.6f, new Color(0.7f, 0.7f, 0.7f, 0.5f), dir * 2);
                    break;
                }
                default:
                {
                    int pellets = Mathf.Max(1, w.St.Pellets);
                    bool any = false, kill = false, head = false;
                    for (int i = 0; i < pellets; i++)
                    {
                        var dir = SpreadDir(cam, i, pellets);
                        var r = Trace(w, origin, dir, muzzle, pellets > 1 ? (i % 3 == 0) : true);
                        any |= r.hit;
                        kill |= r.kill;
                        head |= r.head;
                    }
                    if (any) { ShotsHit++; OnHit?.Invoke(kill, head); }
                    break;
                }
            }
            // Feedback.
            float kick = w.Def.RecoilV * (1 - AimAmount * 0.35f) * (_p.Crouched ? 0.8f : 1f);
            _p.Recoil(kick * 0.6f, Random.Range(-1f, 1f) * w.Def.RecoilH * 0.5f);
            _kickPos.Velocity += new Vector3(0, 0.15f, -1.4f) * kick * (w.Def.Class == WeaponClass.Pistol ? 0.6f : 1f);
            _kickRot.Velocity += new Vector3(-kick * 55, Random.Range(-1f, 1f) * w.Def.RecoilH * 20, Random.Range(-1f, 1f) * 15);
            _bloom = Mathf.Min(_bloom + w.Def.RecoilV * 0.45f, 6);
            if (w.Def.Mode != FireMode.Flame)
            {
                _flash.transform.SetParent(w.Model.Muzzle, false);
                _flash.transform.localPosition = Vector3.zero;
                _flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
                float fs = (suppressed ? 0.025f : w.Def.Class == WeaponClass.Shotgun || w.Def.Class == WeaponClass.Sniper ? 0.11f : w.Def.Class == WeaponClass.Pistol ? 0.06f : 0.08f) * Random.Range(0.8f, 1.25f);
                if (w.Def.Mode == FireMode.Beam) fs = 0.05f;
                _flash.transform.localScale = Vector3.one * fs;
                _flash.SetActive(true);
                _flashT = 0.045f;
                _muzzleLight.transform.position = muzzle;
                _muzzleLight.color = w.Def.Mode == FireMode.Beam ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.7f, 0.35f);
                _muzzleLight.intensity = suppressed ? 5f : 18f;
                _muzzleLight.enabled = true;
                _vmFlash.transform.position = w.Model.Muzzle.position - w.Model.Muzzle.forward * 0.15f + w.Model.Muzzle.up * 0.22f;
                _vmFlash.color = _muzzleLight.color;
                _vmFlash.intensity = suppressed ? 0.05f : 0.22f;
                _vmFlash.enabled = true;
                // Brass.
                if (w.Model.Eject != null && w.Def.Mode != FireMode.Pump && w.Def.Mode != FireMode.Bolt && w.Def.Mode != FireMode.Launcher && w.Def.Mode != FireMode.Beam)
                    EjectShell(w);
            }
            Sfx.Gunshot(w.Id, suppressed);
            Zombie.Noise(_p.transform.position, suppressed ? 9 : w.Def.Mode == FireMode.Flame ? 14 : 38);
            Haptics.Pulse(w.Def.Class == WeaponClass.Shotgun || w.Def.Class == WeaponClass.Sniper ? 30 : 10);
            if (w.Def.Mode == FireMode.Pump || w.Def.Mode == FireMode.Bolt) Invoke(nameof(CycleSound), 0.18f);
        }

        void CycleSound() => Sfx.Rack(W.Def.Mode == FireMode.Bolt);

        void EjectShell(WeaponSlot w)
        {
            var cam = _p.WorldCam.transform;
            var p = cam.position + cam.right * 0.25f + cam.forward * 0.5f - cam.up * 0.12f;
            var v = cam.right * Random.Range(2.5f, 3.5f) + cam.up * Random.Range(1.5f, 2.5f) - cam.forward * 0.5f + _p.Cc.velocity;
            Fx.Shell(p, v, w.Def.Class == WeaponClass.Shotgun, w.Def.Class == WeaponClass.Pistol ? 0.01f : 0.013f);
        }

        Vector3 SpreadDir(Transform cam, int pellet, int pellets)
        {
            float deg = Spread;
            Vector2 r;
            if (pellets > 1)
            {
                // Even-ish pellet pattern with jitter.
                float a = pellet / (float)pellets * Mathf.PI * 2 + Random.Range(-0.3f, 0.3f);
                float rad = (pellet == 0 ? 0 : Mathf.Sqrt((pellet % 4 + 1) / 4f)) * deg;
                r = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            }
            else r = Random.insideUnitCircle * deg * 0.5f;
            var dir = Quaternion.AngleAxis(r.x, cam.up) * Quaternion.AngleAxis(r.y, cam.right) * cam.forward;
            // Bullet magnetism toward the assist target.
            if (AssistTarget != null && SaveSystem.Data.Settings.AimAssist > 0 && pellets == 1)
            {
                float lim = SaveSystem.Data.Settings.AimAssist == 2 ? 4f : 2.5f;
                var to = (_assistPoint - cam.position).normalized;
                if (Vector3.Angle(dir, to) < lim) dir = Vector3.Slerp(dir, to, 0.65f);
            }
            return dir;
        }

        struct TraceResult { public bool hit, kill, head; }

        TraceResult Trace(WeaponSlot w, Vector3 origin, Vector3 dir, Vector3 muzzle, bool tracer)
        {
            var res = new TraceResult();
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, w.St.Range, Layers.ShotMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(Hits, 0, n, HitCmp);
            int pierce = w.St.Pierce;
            HitSet.Clear();
            Vector3 end = origin + dir * w.St.Range;
            var chained = new List<Zombie>();
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                var hb = h.collider.GetComponent<Hitbox>();
                if (hb != null)
                {
                    if (hb.Owner == null || !hb.Owner.IsAlive || HitSet.Contains(hb.Owner)) continue;
                    HitSet.Add(hb.Owner);
                    float dmg = Damage(w, h.distance, out bool crit);
                    bool head = hb.Zone == HitZone.Head;
                    bool was = hb.Owner.IsAlive;
                    hb.Owner.TakeDamage(new DamageInfo
                    {
                        Amount = dmg, Point = h.point, Dir = dir, Zone = hb.Zone, Crit = crit, ArmorPierce = w.Def.ArmorPierce,
                        HeadMul = w.St.HeadMul, Stagger = w.Def.Class == WeaponClass.Shotgun || w.Def.Class == WeaponClass.Sniper ? 0.6f : 0.1f,
                        FromPlayer = true, Class = w.Def.Class, WeaponId = (int)w.Id, Bone = hb.Bone,
                    });
                    res.hit = true;
                    res.head |= head;
                    res.kill |= was && !hb.Owner.IsAlive;
                    if (w.Def.Mode == FireMode.Beam && hb.Owner is Zombie z) chained.Add(z);
                    if (crit || head) Hud.Damage(h.point, dmg, head, crit);
                    end = h.point;
                    if (--pierce < 0) break;
                    continue;
                }
                // World: barrels and other damageables, then the impact.
                var dmgable = h.collider.GetComponent<IDamageable>();
                if (dmgable != null && dmgable.IsAlive) dmgable.TakeDamage(new DamageInfo { Amount = Damage(w, h.distance, out _), Point = h.point, Dir = dir, FromPlayer = true });
                var tag = h.collider.GetComponent<SurfaceTag>();
                Fx.Impact(h.point, h.normal, tag != null ? tag.Kind : SurfaceKind.Concrete, w.Def.Class == WeaponClass.Sniper ? 2f : 1f);
                if (Random.value < 0.35f) Sfx.Impact(h.point, tag != null ? tag.Kind : SurfaceKind.Concrete);
                end = h.point;
                break;
            }
            if (w.Def.Mode == FireMode.Beam)
            {
                Fx.Arc(muzzle, end);
                // Chain to nearby zombies.
                int chains = w.St.Pierce;
                var last = chained.Count > 0 ? chained[chained.Count - 1].transform.position + Vector3.up * 1.2f : end;
                foreach (var z in Zombie.Alive.ToArray())
                {
                    if (chains <= 0) break;
                    if (chained.Contains(z) || !z.IsAlive) continue;
                    var zp = z.transform.position + Vector3.up * 1.2f;
                    if ((zp - last).sqrMagnitude > 36) continue;
                    Fx.Arc(last, zp);
                    bool was = z.IsAlive;
                    z.TakeDamage(new DamageInfo { Amount = Damage(w, 10, out _) * 0.6f, Point = zp, Dir = (zp - last).normalized, Zone = HitZone.Body, ArmorPierce = 1, FromPlayer = true, Class = w.Def.Class, Stagger = 0.4f });
                    res.hit = true;
                    res.kill |= was && !z.IsAlive;
                    last = zp;
                    chains--;
                }
            }
            else if (tracer && (w.Fired % (w.Def.Class == WeaponClass.MachineGun ? 1 : 2) == 0 || w.Def.Class == WeaponClass.Sniper))
                Fx.Tracer(muzzle, end, new Color(1f, 0.78f, 0.45f, 0.85f), w.Def.Class == WeaponClass.Sniper ? 0.045f : 0.025f, 0.05f);
            return res;
        }

        static readonly IComparer<RaycastHit> HitCmp = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        float Damage(WeaponSlot w, float dist, out bool crit)
        {
            float d = w.St.Damage;
            float r = w.St.Range;
            if (dist > r * 0.5f) d *= Mathf.Lerp(1f, 0.45f, (dist - r * 0.5f) / (r * 0.5f));
            crit = Random.value < w.St.CritChance;
            if (crit) d *= w.St.CritMul;
            return d;
        }

        void FlameTick(WeaponSlot w, Vector3 origin, Vector3 fwd, Vector3 muzzle)
        {
            Fx.Flame(muzzle, fwd, w.St.Range, _p.Cc.velocity);
            Sfx.FlameLoop();
            float range = w.St.Range;
            int burned = 0;
            foreach (var z in Zombie.Alive.ToArray())
            {
                var to = z.transform.position + Vector3.up - origin;
                float dist = to.magnitude;
                if (dist > range || Vector3.Angle(fwd, to) > 14 + 10 * (1 - dist / range)) continue;
                if (Physics.Linecast(origin, z.transform.position + Vector3.up, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                z.TakeDamage(new DamageInfo { Amount = w.St.Damage, Point = z.transform.position + Vector3.up, Dir = to.normalized, Zone = HitZone.Body, Fire = true, ArmorPierce = 1, FromPlayer = true, Class = w.Def.Class, WeaponId = (int)w.Id });
                z.Ignite(w.Def.BurnDps * (w.St.Damage / w.Def.Damage), 4f);
                burned++;
            }
            var boss = Boss.Current;
            if (boss != null && boss.IsAlive && (boss.transform.position - origin).magnitude < range + 1.5f && Vector3.Angle(fwd, boss.transform.position + Vector3.up - origin) < 20)
                boss.TakeDamage(new DamageInfo { Amount = w.St.Damage * 1.5f, Point = boss.transform.position + Vector3.up, Dir = fwd, Fire = true, FromPlayer = true, ArmorPierce = 1 });
            foreach (var o in ObjectiveThing.All)
                if (o.Kind == ObjKind.Nest && !o.Done && (o.transform.position - origin).magnitude < range + 1 && Vector3.Angle(fwd, o.transform.position - origin) < 25)
                    o.TakeDamage(new DamageInfo { Amount = w.St.Damage, Point = o.transform.position, Dir = fwd, Fire = true, FromPlayer = true });
            if (burned > 0) { ShotsHit++; OnHit?.Invoke(false, false); }
        }

        // --------------------------------------------------------------- aim assist

        Vector3 _assistPoint;
        float _assistAngle = 99;

        void UpdateAssist()
        {
            AssistTarget = null;
            _assistAngle = 99;
            int level = SaveSystem.Data.Settings.AimAssist;
            bool auto = SaveSystem.Data.Settings.AutoFire;
            if (level == 0 && !auto) return;
            var cam = _p.WorldCam.transform;
            float best = level == 2 ? 9 : 6;
            if (auto) best = Mathf.Max(best, 3);
            foreach (var z in Zombie.Alive)
            {
                if (z.State == ZState.Spawning) continue;
                var head = z.Rig[ZB.Head].position + Vector3.up * 0.06f;
                var chest = z.Rig[ZB.Chest].position;
                var target = AimAmount > 0.5f ? head : Vector3.Lerp(chest, head, 0.35f);
                var to = target - cam.position;
                float dist = to.magnitude;
                if (dist > W.St.Range) continue;
                float ang = Vector3.Angle(cam.forward, to);
                float allowance = best * Mathf.Clamp(12f / Mathf.Max(4, dist), 0.4f, 1.4f);
                if (ang < allowance && ang < _assistAngle)
                {
                    if (Physics.Linecast(cam.position, target, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    _assistAngle = ang;
                    AssistTarget = z;
                    _assistPoint = target;
                }
            }
        }

        /// <summary>Look-speed multiplier while the crosshair is over a target (aim friction).</summary>
        public float AimAssistFriction()
        {
            int level = SaveSystem.Data.Settings.AimAssist;
            if (level == 0 || AssistTarget == null || _assistAngle > 3.5f) return 1;
            return level == 2 ? 0.45f : 0.62f;
        }

        // --------------------------------------------------------------- reload

        void TryReload()
        {
            var w = W;
            if (Reloading || w.Mag >= w.St.Mag || (!w.Infinite && w.Reserve <= 0) || w.Def.Mode == FireMode.Flame && w.Mag > w.St.Mag * 0.9f) return;
            Reloading = true;
            _reloadT = 0;
            _burstLeft = 0;
            if (w.Def.ShellReload)
            {
                _shellsToLoad = w.St.Mag - w.Mag;
                if (!w.Infinite) _shellsToLoad = Mathf.Min(_shellsToLoad, w.Reserve);
                _reloadDur = w.St.Reload * _shellsToLoad + 0.5f;
            }
            else _reloadDur = w.St.Reload;
            Sfx.ReloadStart(w.Def.Class);
        }

        void ReloadTick(float dt)
        {
            var w = W;
            float prev = _reloadT;
            _reloadT += dt;
            if (w.Def.ShellReload)
            {
                // Insert one shell per Reload seconds after a short lead-in.
                float lead = 0.25f;
                int loadedBefore = Mathf.FloorToInt(Mathf.Max(0, prev - lead) / w.St.Reload);
                int loadedNow = Mathf.FloorToInt(Mathf.Max(0, _reloadT - lead) / w.St.Reload);
                for (int i = loadedBefore; i < loadedNow && _shellsToLoad > 0; i++)
                {
                    w.Mag++;
                    if (!w.Infinite) w.Reserve--;
                    _shellsToLoad--;
                    Sfx.ShellIn();
                }
                if (_shellsToLoad <= 0 && _reloadT > lead + 0.1f) { FinishReload(); Sfx.Rack(false); }
                return;
            }
            if (prev < _reloadDur * 0.3f && _reloadT >= _reloadDur * 0.3f) Sfx.MagOut();
            if (prev < _reloadDur * 0.65f && _reloadT >= _reloadDur * 0.65f) Sfx.MagIn();
            if (prev < _reloadDur * 0.82f && _reloadT >= _reloadDur * 0.82f && w.Def.Class != WeaponClass.Special) Sfx.Charge(w.Def.Class);
            if (_reloadT >= _reloadDur)
            {
                int need = w.St.Mag - w.Mag;
                int take = w.Infinite ? need : Mathf.Min(need, w.Reserve);
                w.Mag += take;
                if (!w.Infinite) w.Reserve -= take;
                FinishReload();
            }
        }

        void FinishReload()
        {
            Reloading = false;
            _reloadT = 0;
        }

        void CancelReload()
        {
            Reloading = false;
            _reloadT = 0;
        }

        public float ReloadProgress => Reloading ? Mathf.Clamp01(_reloadT / Mathf.Max(0.01f, _reloadDur)) : 0;

        /// <summary>Refills ammo from a pickup (one magazine's worth per box, more for big guns).</summary>
        public void AddAmmo(float mags)
        {
            foreach (var s in Slots)
            {
                if (s.Infinite) continue;
                int add = Mathf.Max(1, Mathf.RoundToInt(s.St.Mag * mags));
                s.Reserve = Mathf.Min(s.St.Reserve, s.Reserve + add);
            }
        }

        public void RefillAll()
        {
            foreach (var s in Slots)
            {
                s.Mag = s.St.Mag;
                if (!s.Infinite) s.Reserve = s.St.Reserve;
            }
        }

        // --------------------------------------------------------------- viewmodel

        Vector3 HipPos(WeaponSlot w)
        {
            // Shoulder-fired tube sits out on the right so it doesn't cover the view.
            if (w.Def.Id == WeaponId.RocketLauncher) return new Vector3(0.245f, -0.135f, 0.1f);
            switch (w.Def.Class)
            {
                case WeaponClass.Pistol: return new Vector3(0.12f, -0.1f, 0.32f);
                case WeaponClass.Sniper: return new Vector3(0.14f, -0.15f, 0.22f);
                case WeaponClass.MachineGun: return new Vector3(0.15f, -0.17f, 0.2f);
                case WeaponClass.Special: return new Vector3(0.15f, -0.16f, 0.22f);
                default: return new Vector3(0.14f, -0.15f, 0.24f);
            }
        }

        void Animate(float dt)
        {
            var w = W;
            var m = w.Model;
            var input = Controls.State;
            // Sway from look input.
            var look = input.Look;
            _swayPos.Step(new Vector3(-look.x, -look.y, 0) * 0.0018f * (1 - AimAmount * 0.8f), 160, 18, dt);
            _swayRot.Step(new Vector3(look.y * 0.6f, -look.x * 0.6f, -look.x * 0.9f) * (1 - AimAmount * 0.8f), 160, 18, dt);
            _swayPos.Value = Vector3.ClampMagnitude(_swayPos.Value, 0.05f);
            _kickPos.Step(Vector3.zero, 260, 22, dt);
            _kickRot.Step(Vector3.zero, 220, 20, dt);
            // Bob / sprint.
            float speed = new Vector2(_p.Cc.velocity.x, _p.Cc.velocity.z).magnitude;
            float bob = _p.Grounded ? Mathf.Clamp01(speed / 5) * (1 - AimAmount * 0.85f) : 0;
            float ph = _p.StepPhase;
            bool sprint = speed > 5.6f && AimAmount < 0.1f && !Reloading;
            _sprint = Mathf.MoveTowards(_sprint, sprint ? 1 : 0, dt * 5);
            var hip = HipPos(w);
            // Iron sights: the procedural receivers are as tall as their sights, so with the eye exactly on the sight line
            // the receiver covered the aim point and the stock filled the bottom of the screen. The eye sits a little
            // above the sight line instead (visual only: shots still go to the screen centre). Optics stay exact.
            bool pistol = w.Def.Class == WeaponClass.Pistol;
            float lift = m.HasScope || m.HasOptic ? 0 : pistol ? 0.006f : 0.014f;
            var ads = new Vector3(0, -m.Sight.localPosition.y - lift, pistol ? 0.24f : m.HasScope ? 0.14f : m.HasOptic ? 0.17f : 0.2f);
            var pos = Vector3.Lerp(hip, ads, X.Smooth01(AimAmount));
            var rot = Quaternion.Slerp(Quaternion.Euler(0, -1.5f, 0), Quaternion.identity, AimAmount);
            pos += new Vector3(Mathf.Cos(ph) * 0.012f, -Mathf.Abs(Mathf.Sin(ph)) * 0.012f, 0) * bob;
            pos += _swayPos.Value + _kickPos.Value * 0.012f;
            rot *= Quaternion.Euler(_swayRot.Value + _kickRot.Value * 0.02f);
            // Sprint pose.
            pos += new Vector3(-0.02f, -0.05f, -0.02f) * _sprint;
            rot *= Quaternion.Euler(14 * _sprint, -28 * _sprint, 12 * _sprint);
            // Draw (switch) animation.
            float draw = 1 - X.Smooth01(_switchT);
            pos += new Vector3(0, -0.22f, -0.05f) * draw;
            rot *= Quaternion.Euler(45 * draw, 0, -20 * draw);
            // Reload choreography.
            float rk = 0;
            if (Reloading) rk = ReloadPose(w, m, ref pos, ref rot);
            else ResetParts(m);
            // Grenade throw dips the weapon.
            if (_grenadeT >= 0)
            {
                float g = X.Bump(_grenadeT, 0, 1);
                pos += new Vector3(0, -0.15f, 0) * g;
                rot *= Quaternion.Euler(30 * g, 0, 0);
            }
            // Inspect.
            if (_inspectT >= 0)
            {
                float k = X.Bump(_inspectT, 0, 1);
                pos += new Vector3(-0.06f, 0.03f, 0.02f) * k;
                rot *= Quaternion.Euler(-10 * k, -55 * k * Mathf.Sign(Mathf.Sin(_inspectT * Mathf.PI * 2) + 0.01f), -35 * k);
            }
            // Moving parts: slide / bolt / pump / drum.
            AnimateParts(w, m, dt);
            _holder.localPosition = pos;
            _holder.localRotation = rot;
            // Scope view hides the weapon.
            bool hide = Scoped;
            foreach (var r in m.Renderers) r.enabled = !hide;
            _arms.SetVisible(!hide);
            _arms.Pose(m, _holder, _p.ViewCam.transform, Reloading ? _reloadT / Mathf.Max(0.01f, _reloadDur) : -1, w, _grenadeT);
        }

        float _sprint;

        float ReloadPose(WeaponSlot w, GunModel m, ref Vector3 pos, ref Quaternion rot)
        {
            float t = Mathf.Clamp01(_reloadT / Mathf.Max(0.01f, _reloadDur));
            if (w.Def.ShellReload)
            {
                float k = X.Bump(t, 0, 1) > 0 ? 1 : 0;
                k = Mathf.Min(1, Mathf.Min(t * 8, (1 - t) * 8));
                pos += new Vector3(-0.03f, 0.02f, 0) * k;
                rot *= Quaternion.Euler(-8 * k, 0, 32 * k);
                return k;
            }
            float tilt = Mathf.Min(1, Mathf.Min(t * 6, (1 - t) * 5));
            pos += new Vector3(-0.02f, 0.01f, 0) * tilt;
            rot *= Quaternion.Euler(-10 * tilt, 10 * tilt, 28 * tilt);
            if (m.Lid != null) m.Lid.localRotation = m.LidRest * Quaternion.Euler(-70 * X.Bump(t, 0.1f, 0.95f), 0, 0);
            if (m.Mag != null && m.Mag != m.Drum)
            {
                // Out: 0.15-0.35, gone: 0.35-0.55, in: 0.55-0.72.
                float outK = X.Smooth01(X.Ramp(t, 0.15f, 0.35f));
                float inK = X.Smooth01(X.Ramp(t, 0.55f, 0.72f));
                float off = outK * (1 - inK);
                m.Mag.localPosition = m.MagRest + new Vector3(0, -0.25f * off, -0.03f * off);
                m.Mag.localRotation = Quaternion.Euler(-20 * off, 0, 15 * off);
                m.Mag.gameObject.SetActive(!(t > 0.36f && t < 0.54f));
            }
            if (m.Drum != null) m.Drum.localRotation = m.DrumRest * Quaternion.Euler(0, 0, t * 360);
            // Charging handle / slide at the end.
            float charge = X.Bump(t, 0.78f, 0.95f);
            if (m.Bolt != null) m.Bolt.localPosition = m.BoltRest + new Vector3(0, 0, -0.06f * charge);
            if (m.Slide != null) m.Slide.localPosition = m.SlideRest + new Vector3(0, 0, -0.035f * charge);
            return tilt;
        }

        void ResetParts(GunModel m)
        {
            if (m.Mag != null && m.Mag != m.Drum)
            {
                m.Mag.localPosition = m.MagRest;
                m.Mag.localRotation = Quaternion.identity;
                if (!m.Mag.gameObject.activeSelf) m.Mag.gameObject.SetActive(true);
            }
            if (m.Lid != null) m.Lid.localRotation = m.LidRest;
        }

        void AnimateParts(WeaponSlot w, GunModel m, float dt)
        {
            if (Reloading) return;
            float sinceShot = _cool > 0 ? 1 - Mathf.Clamp01(_cool / Mathf.Max(0.05f, w.St.Interval)) : 1;
            if (m.Slide != null)
            {
                // Slide snaps back on each shot; locks back on empty.
                float back = w.Mag <= 0 ? 1 : X.Bump(sinceShot, 0, 0.6f);
                m.Slide.localPosition = m.SlideRest + new Vector3(0, 0, -0.03f * back);
            }
            if (m.Pump != null)
            {
                float c = w.Cycle > 0 ? 1 - w.Cycle / 0.5f : 0;
                m.Pump.localPosition = m.PumpRest + new Vector3(0, 0, -0.08f * X.Bump(c, 0, 1));
            }
            if (m.Bolt != null && w.Def.Mode == FireMode.Bolt)
            {
                float c = w.Cycle > 0 ? 1 - w.Cycle / 0.9f : 0;
                float lift = X.Bump(c, 0, 1);
                float pull = X.Bump(c, 0.2f, 0.8f);
                m.Bolt.localRotation = m.BoltRotRest * Quaternion.Euler(0, 0, 70 * lift);
                m.Bolt.localPosition = m.BoltRest + new Vector3(0, 0, -0.07f * pull);
            }
            else if (m.Bolt != null)
            {
                m.Bolt.localPosition = m.BoltRest + new Vector3(0, 0, -0.025f * X.Bump(sinceShot, 0, 0.5f));
            }
            if (m.Drum != null && w.Def.Mode == FireMode.Launcher)
                m.Drum.localRotation = Quaternion.Slerp(m.Drum.localRotation, m.DrumRest * Quaternion.Euler(0, 0, w.Fired * 60), 1 - Mathf.Exp(-dt * 18));
        }
    }

    /// <summary>First-person gloved hands and sleeved forearms, posed to the weapon each frame.</summary>
    public sealed class Arms
    {
        readonly Transform _handR, _handL, _armR, _armL;
        readonly List<Renderer> _rs = new List<Renderer>();

        public Arms(Transform parent, Outfit outfit, Gloves gloves)
        {
            var gloveMat = GloveMat(gloves);
            var knuckle = gloves == Gloves.RedKnuckle ? Mats.Cached("knuckleRed", () => Mats.Lit(new Color(0.5f, 0.05f, 0.04f), 0.4f)) : gloveMat;
            var sleeve = Mats.Cached("sleeve" + outfit, () => Mats.Lit(Color.white, 0.12f, 0, Tex.Camo(OutfitCamo(outfit)), new Vector2(2.5f, 2.5f)));
            var skin = Mats.Cached("armskin", () => Mats.Lit(new Color(0.72f, 0.56f, 0.45f), 0.35f));
            var hand = WorldKit.Get("fpHand", mb =>
            {
                // Fist around a grip: palm block, four curled fingers, thumb (local +Z = fingers forward, +Y = back of hand).
                mb.RoundBox(0, new Vector3(0, 0, 0.045f), new Vector3(0.085f, 0.034f, 0.09f), 0.014f, 2);
                for (int f = 0; f < 4; f++)
                {
                    float x = -0.03f + f * 0.02f;
                    mb.Cylinder(0, new Vector3(x, 0.0f, 0.09f), new Vector3(x, -0.022f, 0.112f), 0.0095f, 0.009f, 7);
                    mb.Cylinder(0, new Vector3(x, -0.022f, 0.112f), new Vector3(x, -0.05f, 0.098f), 0.009f, 0.0085f, 7);
                    mb.Cylinder(0, new Vector3(x, -0.05f, 0.098f), new Vector3(x, -0.06f, 0.072f), 0.0085f, 0.008f, 7);
                    mb.RoundBox(1, new Vector3(x, 0.014f, 0.09f), new Vector3(0.018f, 0.012f, 0.022f), 0.005f, 1);
                }
                mb.Cylinder(0, new Vector3(-0.045f, -0.005f, 0.03f), new Vector3(-0.055f, -0.02f, 0.08f), 0.011f, 0.01f, 7);
                mb.Cylinder(0, new Vector3(-0.055f, -0.02f, 0.08f), new Vector3(-0.045f, -0.035f, 0.105f), 0.01f, 0.009f, 7);
                mb.Cylinder(0, new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.005f), 0.036f, 0.04f, 12, false, false);
            });
            var forearm = WorldKit.Get("fpForearm", mb =>
            {
                mb.Cylinder(0, Vector3.zero, Vector3.forward * 0.34f, 0.038f, 0.052f, 14, false, false);
                mb.Cylinder(1, new Vector3(0, 0, -0.005f), new Vector3(0, 0, 0.05f), 0.04f, 0.042f, 14, false, false);
                mb.Cylinder(0, new Vector3(0, 0, 0.12f), new Vector3(0, 0, 0.14f), 0.047f, 0.048f, 14, false, false);
            });
            _handR = Make(parent, "HandR", hand, new[] { gloveMat, knuckle });
            _handL = Make(parent, "HandL", hand, new[] { gloveMat, knuckle });
            _armR = Make(parent, "ArmR", forearm, new[] { sleeve, gloveMat });
            _armL = Make(parent, "ArmL", forearm, new[] { sleeve, gloveMat });
        }

        static int OutfitCamo(Outfit o)
        {
            switch (o)
            {
                case Outfit.UrbanOps: return 3;
                case Outfit.DesertStorm: return 2;
                case Outfit.BlackOps: return 11;
                case Outfit.Arctic: return 4;
                case Outfit.Hazmat: return 12;
                default: return 10;
            }
        }

        static Material GloveMat(Gloves g)
        {
            switch (g)
            {
                case Gloves.Tan: return Mats.Cached("gloveTan", () => Mats.Lit(new Color(0.42f, 0.32f, 0.2f), 0.3f));
                case Gloves.Carbon: return Mats.Cached("gloveCarbon", () => Mats.Lit(Color.white, 0.6f, 0.1f, Tex.Camo(6), new Vector2(4, 4)));
                case Gloves.Gold: return Mats.Cached("gloveGold", () => Mats.Lit(Color.white, 0.85f, 1f, Tex.Camo(8)));
                default: return Mats.Cached("gloveBlack", () => Mats.Lit(new Color(0.15f, 0.15f, 0.16f), 0.35f));
            }
        }

        Transform Make(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.layer = Layers.Viewmodel;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            LightLayers.Set(r, LightLayers.Viewmodel);
            _rs.Add(r);
            return go.transform;
        }

        public void SetVisible(bool on)
        {
            foreach (var r in _rs) r.enabled = on;
        }

        /// <summary>Places hands on the grips and forearms toward fixed elbow anchors below the screen.</summary>
        public void Pose(GunModel m, Transform holder, Transform cam, float reload, WeaponSlot w, float grenade)
        {
            var gunRot = holder.rotation;
            // Right hand: fist around the pistol grip, fingers forward/down.
            _handR.position = m.GripR.position + gunRot * new Vector3(0.012f, 0.02f, -0.025f);
            _handR.rotation = gunRot * Quaternion.Euler(70, 0, -80);
            // Left hand: under the handguard, or helping on the pistol grip.
            Vector3 lp;
            Quaternion lr;
            if (w.Def.Class == WeaponClass.Pistol)
            {
                lp = m.GripR.position + gunRot * new Vector3(-0.035f, -0.01f, 0.005f);
                lr = gunRot * Quaternion.Euler(60, 0, 75);
            }
            else
            {
                lp = m.GripL.position + gunRot * new Vector3(-0.01f, -0.03f, 0);
                lr = gunRot * Quaternion.Euler(10, 0, 100);
            }
            // During magazine reloads the support hand goes to the mag and back.
            if (reload >= 0 && m.MagWell != null && !w.Def.ShellReload)
            {
                float toMag = X.Smooth01(X.Ramp(reload, 0.05f, 0.18f)) * (1 - X.Smooth01(X.Ramp(reload, 0.75f, 0.9f)));
                var magPos = (m.Mag != null && m.Mag.gameObject.activeSelf ? m.Mag.position : m.MagWell.position) + gunRot * new Vector3(-0.02f, -0.04f, 0);
                float away = X.Bump(reload, 0.33f, 0.58f);
                magPos += cam.TransformDirection(new Vector3(-0.08f, -0.25f, -0.05f)) * away;
                lp = Vector3.Lerp(lp, magPos, toMag);
                lr = Quaternion.Slerp(lr, gunRot * Quaternion.Euler(60, 0, 90), toMag);
            }
            else if (reload >= 0 && w.Def.ShellReload)
            {
                float cyc = Mathf.Repeat(reload * 6, 1);
                lp = m.MagWell.position + gunRot * new Vector3(-0.02f, -0.05f - X.Bump(cyc, 0, 1) * 0.06f, -0.02f);
                lr = gunRot * Quaternion.Euler(70, 0, 90);
            }
            if (grenade >= 0)
            {
                float g = X.Bump(grenade, 0, 1);
                lp = Vector3.Lerp(lp, cam.TransformPoint(new Vector3(-0.15f, 0.05f + 0.1f * g, 0.25f)), g);
            }
            _handL.position = lp;
            _handL.rotation = lr;
            PoseArm(_armR, _handR, cam.TransformPoint(new Vector3(0.26f, -0.42f, -0.06f)));
            PoseArm(_armL, _handL, cam.TransformPoint(new Vector3(-0.24f, -0.44f, 0.0f)));
        }

        static void PoseArm(Transform arm, Transform hand, Vector3 elbow)
        {
            var wrist = hand.position + hand.rotation * new Vector3(0, 0, -0.02f);
            var dir = elbow - wrist;
            // Degenerate frames (camera or hand collapsed onto the elbow anchor, or NaN) keep the last pose
            // instead of logging "Look rotation viewing vector is zero" every frame.
            if (!(dir.sqrMagnitude > 1e-8f)) return;
            arm.position = wrist;
            arm.rotation = Quaternion.LookRotation(dir, hand.rotation * Vector3.up);
            arm.localScale = new Vector3(1, 1, Mathf.Max(0.5f, dir.magnitude / 0.34f));
        }
    }
}
