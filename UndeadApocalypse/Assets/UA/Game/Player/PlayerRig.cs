using System;
using UA.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UA.Game
{
    public enum HurtKind { Melee, Explosion, Toxic, Fire }

    /// <summary>Per-frame player intent, written by touch controls, keyboard/mouse, gyro or the autopilot.</summary>
    public struct PlayerInput
    {
        public Vector2 Move;      // -1..1
        public Vector2 Look;      // degrees this frame (yaw, pitch)
        public bool Fire, Aim, Sprint;
        public bool ReloadPressed, JumpPressed, CrouchPressed, SwapPressed, GrenadePressed, FlashlightPressed, AimPressed;
        /// <summary>Grenade button held (aim with the arc; the throw happens on release).</summary>
        public bool GrenadeHeld;
        /// <summary>Touch input was reset (pause, death, controls hidden): drop an aimed grenade without throwing.</summary>
        public bool GrenadeCancel;
        public int SelectSlot;    // -1 none
    }

    public enum TouchBtn { Reload, Jump, Crouch, Swap, Grenade, Flashlight, Aim }

    public static class Controls
    {
        public static PlayerInput State;
        public static bool AimToggled;
        // Touch accumulators, filled by UI event handlers (which run before the player update).
        static Vector2 _touchMove, _touchLook;
        static bool _touchFire, _touchGrenade, _grenadeCancel;
        static int _pressed;
        static int _select = -1;

        public static void SetMove(Vector2 v) => _touchMove = v;
        public static void AddLook(Vector2 deg) => _touchLook += deg;
        public static void SetFire(bool on) => _touchFire = on;
        public static void SetGrenade(bool held) => _touchGrenade = held;
        public static void Press(TouchBtn b) => _pressed |= 1 << (int)b;
        public static void Select(int slot) => _select = slot;

        public static void ResetTouch()
        {
            _touchMove = _touchLook = Vector2.zero;
            _touchFire = false;
            if (_touchGrenade) _grenadeCancel = true;
            _touchGrenade = false;
            _pressed = 0;
            _select = -1;
            AimToggled = false;
        }

        static bool P(TouchBtn b) => (_pressed & (1 << (int)b)) != 0;

        public static void Gather(float dt)
        {
            var s = new PlayerInput { SelectSlot = -1 };
            if (AutoPilot.Active)
            {
                State = AutoPilot.Input;
                _touchLook = Vector2.zero;
                _pressed = 0;
                return;
            }
            // Touch.
            s.Move = _touchMove;
            s.Look = _touchLook;
            s.Fire = _touchFire;
            s.ReloadPressed = P(TouchBtn.Reload);
            s.JumpPressed = P(TouchBtn.Jump);
            s.CrouchPressed = P(TouchBtn.Crouch);
            s.SwapPressed = P(TouchBtn.Swap);
            s.GrenadePressed = P(TouchBtn.Grenade);
            s.GrenadeHeld = _touchGrenade;
            s.GrenadeCancel = _grenadeCancel;
            _grenadeCancel = false;
            s.FlashlightPressed = P(TouchBtn.Flashlight);
            s.AimPressed = P(TouchBtn.Aim);
            s.SelectSlot = _select;
            _touchLook = Vector2.zero;
            _pressed = 0;
            _select = -1;
            if (s.AimPressed) AimToggled = !AimToggled;
            // Gyro.
            var set = SaveSystem.Data.Settings;
            if (set.Gyro)
            {
                var g = UnityEngine.InputSystem.Gyroscope.current;
                if (g != null)
                {
                    if (!g.enabled) InputSystem.EnableDevice(g);
                    var w = g.angularVelocity.ReadValue() * Mathf.Rad2Deg * dt * set.GyroSensitivity;
                    bool left = Screen.orientation != ScreenOrientation.LandscapeRight;
                    s.Look += left ? new Vector2(-w.x, w.y) : new Vector2(w.x, -w.y);
                }
            }
            // Keyboard / mouse (desktop testing and Chromebooks).
            var kb = Keyboard.current;
            if (kb != null)
            {
                var mv = Vector2.zero;
                if (kb.wKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed) mv.y -= 1;
                if (kb.aKey.isPressed) mv.x -= 1;
                if (kb.dKey.isPressed) mv.x += 1;
                if (mv != Vector2.zero) s.Move = Vector2.ClampMagnitude(mv, 1);
                s.Sprint |= kb.leftShiftKey.isPressed;
                s.ReloadPressed |= kb.rKey.wasPressedThisFrame;
                s.JumpPressed |= kb.spaceKey.wasPressedThisFrame;
                s.CrouchPressed |= kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
                s.SwapPressed |= kb.qKey.wasPressedThisFrame;
                s.GrenadeHeld |= kb.gKey.isPressed;   // hold G to aim, release to throw
                s.FlashlightPressed |= kb.fKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) s.SelectSlot = 0;
                if (kb.digit2Key.wasPressedThisFrame) s.SelectSlot = 1;
                if (kb.digit3Key.wasPressedThisFrame) s.SelectSlot = 2;
            }
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue();
                s.Look += new Vector2(d.x, -d.y) * 0.09f * set.Sensitivity;
                s.Fire |= mouse.leftButton.isPressed;
                if (mouse.rightButton.wasPressedThisFrame) AimToggled = true;
                if (mouse.rightButton.wasReleasedThisFrame) AimToggled = false;
            }
            if (set.InvertY) s.Look.y = -s.Look.y;
            s.Aim = AimToggled;
            State = s;
        }
    }

    /// <summary>Screen shake from explosions, hits and boss attacks (trauma model).</summary>
    public static class CameraShake
    {
        public static float Trauma;
        public static void Add(float t) => Trauma = Mathf.Clamp01(Trauma + t);

        public static Vector3 Offset(float dt)
        {
            Trauma = Mathf.Max(0, Trauma - dt * 1.4f);
            float k = Trauma * Trauma;
            float t = Time.time * 22;
            return new Vector3((Mathf.PerlinNoise(t, 0) - 0.5f) * 6 * k, (Mathf.PerlinNoise(0, t) - 0.5f) * 6 * k, (Mathf.PerlinNoise(t, t) - 0.5f) * 5 * k);
        }
    }

    /// <summary>
    /// The player: CharacterController movement, first-person cameras (world + viewmodel overlay),
    /// health and armor, flashlight, head bob, recoil and shake. Owns the <see cref="Gunplay"/>.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Instance;
        public static Vector3 Pos;
        public static Camera Cam;
        public static PlayerStats Stats;

        public CharacterController Cc;
        public Transform Head;
        public Camera WorldCam, ViewCam;
        public Light Flashlight;
        public Gunplay Guns;
        public float Hp, MaxHp, Armor, MaxArmor;
        public bool Alive = true, Grounded, Crouched, FlashlightOn, Invulnerable;
        public float Yaw, Pitch;
        public float LastHurtTime = -99;
        public Vector3 LastHurtFrom;
        public float StepPhase;
        public float BaseFov = 70;
        public event Action<float, Vector3, HurtKind> Hurted;
        public event Action Died;
        bool _secondWindUsed;
        float _vy, _eye = 1.62f, _bob, _landDip, _stun, _fovKick, _regenWait, _footDist;
        Vector3 _knock;
        Spring _recoilPitch, _recoilYaw;

        public static PlayerRig Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.layer = Layers.Player;
            go.transform.position = pos + Vector3.up * 0.05f;
            var p = go.AddComponent<PlayerRig>();
            p.Yaw = yaw;
            p.Setup();
            Instance = p;
            return p;
        }

        void Setup()
        {
            Cc = gameObject.AddComponent<CharacterController>();
            Cc.height = 1.8f;
            Cc.radius = 0.33f;
            Cc.center = new Vector3(0, 0.9f, 0);
            Cc.stepOffset = 0.42f;
            Cc.slopeLimit = 50;
            Cc.skinWidth = 0.04f;
            Cc.minMoveDistance = 0;
            Head = X.Child(transform, "Head", new Vector3(0, _eye, 0)).transform;
            // World camera.
            var camGo = new GameObject("WorldCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(Head, false);
            WorldCam = camGo.AddComponent<Camera>();
            WorldCam.nearClipPlane = 0.06f;
            WorldCam.farClipPlane = 140;
            WorldCam.fieldOfView = BaseFov;
            WorldCam.cullingMask = ~(1 << Layers.Viewmodel);
            WorldCam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var wd = camGo.AddComponent<UniversalAdditionalCameraData>();
            wd.renderPostProcessing = true;
            wd.renderType = CameraRenderType.Base;
            // Viewmodel overlay camera (weapon never clips into walls).
            var vmGo = new GameObject("ViewCamera");
            vmGo.transform.SetParent(camGo.transform, false);
            ViewCam = vmGo.AddComponent<Camera>();
            ViewCam.nearClipPlane = 0.01f;
            ViewCam.farClipPlane = 4;
            ViewCam.fieldOfView = 54;
            ViewCam.cullingMask = 1 << Layers.Viewmodel;
            var vd = vmGo.AddComponent<UniversalAdditionalCameraData>();
            vd.renderType = CameraRenderType.Overlay;
            vd.renderShadows = false;
            vd.renderPostProcessing = true;
            wd.cameraStack.Add(ViewCam);
            ApplyCameraQuality();
            Cam = WorldCam;
            // Flashlight.
            var fl = new GameObject("Flashlight");
            fl.transform.SetParent(camGo.transform, false);
            fl.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f);
            fl.transform.localRotation = Quaternion.Euler(-3f, -1.5f, 0);
            Flashlight = fl.AddComponent<Light>();
            Flashlight.type = LightType.Spot;
            Flashlight.color = new Color(1f, 0.94f, 0.82f);
            Flashlight.range = 34;
            Flashlight.spotAngle = 50;
            Flashlight.innerSpotAngle = 18;
            // With the source set back 0.8 m (PlaceFlashlight) this lights 5-15 m like before, without close-range glare.
            Flashlight.intensity = FlashlightIntensity;
            Flashlight.renderMode = LightRenderMode.ForcePixel;
            LightLayers.Set(Flashlight, LightLayers.World);
            Flashlight.shadows = Quality.FlashlightShadows ? LightShadows.Soft : LightShadows.None;
            Flashlight.shadowStrength = 0.85f;
            Flashlight.shadowBias = 0.03f;
            // Viewmodel light rig: soft key from the upper left and a cool rim from the right, only on the weapon/hands.
            foreach (var (pos, col, inten, range) in new[] { (new Vector3(-0.35f, 0.35f, 0.05f), new Color(1f, 0.92f, 0.82f), 1.5f, 2.2f), (new Vector3(0.45f, 0.1f, 0.5f), new Color(0.55f, 0.7f, 1f), 0.45f, 1.4f) })
            {
                var vl = new GameObject("VmLight");
                vl.transform.SetParent(vmGo.transform, false);
                vl.transform.localPosition = pos;
                var l = vl.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = col;
                l.intensity = inten;
                l.range = range;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                LightLayers.Set(l, LightLayers.Viewmodel);
            }
            // Viewmodel reflections. Gun metal is lit almost only by reflections, and the level's probe is near black in
            // night and dusk stages, which rendered steel parts pure black. A small studio cubemap travels with the
            // weapon and outranks the level probe inside its box (about 1.5 m around the gun).
            var vr = new GameObject("VmReflection");
            vr.transform.SetParent(vmGo.transform, false);
            vr.transform.localPosition = new Vector3(0, 0, 0.4f);
            var probe = vr.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = ViewmodelCubemap();
            probe.size = new Vector3(3, 3, 3);
            probe.importance = 100;
            probe.intensity = 1;
            probe.blendDistance = 0;
            Stats = PlayerProgress.Compute(SaveSystem.Data);
            MaxHp = Hp = Stats.MaxHealth;
            MaxArmor = Armor = Stats.MaxArmor;
            Guns = gameObject.AddComponent<Gunplay>();
            Guns.Init(this);
            Quality.Changed += ApplyCameraQuality;
        }

        static Cubemap _vmCube;

        /// <summary>
        /// Neutral studio environment for the weapon: dark floor and ceiling with a bright horizontal band, which gives
        /// metal long highlight streaks. Nearly symmetric top to bottom, so face orientation does not matter.
        /// </summary>
        static Cubemap ViewmodelCubemap()
        {
            if (_vmCube != null) return _vmCube;
            const int n = 32;
            var cube = new Cubemap(n, TextureFormat.RGBA32, true) { name = "ViewmodelEnv", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[n * n];
            for (int f = 0; f < 6; f++)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                        Vector3 d;
                        switch ((CubemapFace)f)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                            default: d = new Vector3(-u, -v, -1); break;
                        }
                        d.Normalize();
                        float band = Mathf.Exp(-(d.y * d.y) / 0.05f);
                        float k = 0.2f + band * 0.6f + Mathf.Clamp01(d.y) * 0.25f;
                        px[y * n + x] = new Color(k, k * 0.97f, k * 0.93f, 1);
                    }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(true, false);
            _vmCube = cube;
            return cube;
        }

        void OnDestroy()
        {
            Quality.Changed -= ApplyCameraQuality;
            if (Instance == this) Instance = null;
        }

        void ApplyCameraQuality()
        {
            if (WorldCam == null) return;
            var wd = WorldCam.GetComponent<UniversalAdditionalCameraData>();
            var g = Quality.Current;
            wd.antialiasing = g == Gfx.Ultra ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : g == Gfx.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            wd.renderShadows = g != Gfx.Low || Look.Key == null || Look.Key.shadows != LightShadows.None;
            WorldCam.allowHDR = true;
            WorldCam.farClipPlane = g == Gfx.Low ? 90 : 140;
            if (Flashlight != null) Flashlight.shadows = Quality.FlashlightShadows ? LightShadows.Soft : LightShadows.None;
        }

        public void SetFlashlight(bool on)
        {
            FlashlightOn = on;
            if (Flashlight != null) Flashlight.enabled = on;
            if (on) PlaceFlashlight();
        }

        /// <summary>
        /// The beam's source sits up to 0.8 m behind the camera: inverse-square falloff then no longer blows out
        /// enemies at arm's length while 5-15 m stays as bright. A wall right behind the player pulls it forward
        /// so it never ends up behind (and shadowed by) geometry.
        /// </summary>
        void PlaceFlashlight()
        {
            if (Flashlight == null || WorldCam == null) return;
            var cam = WorldCam.transform;
            float back = 0.8f;
            if (Physics.Raycast(cam.position, -cam.forward, out var hit, back + 0.1f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) back = Mathf.Max(0, hit.distance - 0.1f);
            Flashlight.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f - back);
            // Close-range exposure: when something fills the beam within ~3 m (a zombie at arm's length, a wall),
            // the light dims smoothly, the way a camera stops down, instead of blowing faces and clothes out to
            // white. Beyond ~3 m it is unchanged. Visual only.
            // The nearest surface anywhere in the inner beam (centre plus four rays 14 degrees out): a wall to one side
            // blew out into a white disc when only the centre was checked.
            const float range = 3.6f;
            float near = range;
            for (int i = 0; i < 5; i++)
            {
                var dir = i == 0 ? cam.forward : Quaternion.AngleAxis(14, i < 3 ? (i == 1 ? cam.up : -cam.up) : (i == 3 ? cam.right : -cam.right)) * cam.forward;
                if (Physics.SphereCast(cam.position, 0.2f, dir, out var fh, near, Layers.ShotMask, QueryTriggerInteraction.Ignore)) near = Mathf.Min(near, fh.distance);
            }
            // Inverse-square compensation, like a camera stopping down: a surface at distance d gets roughly the light it
            // would get at the 3.6 m reference (slightly brighter, exponent 1.6 not 2). Beyond 3.6 m nothing changes.
            float d = near + back, dRef = range + back;
            float target = Mathf.Clamp(Mathf.Pow(d / dRef, 1.6f), 0.12f, 1f);
            // Stops down fast (a wall appears suddenly), opens up more slowly.
            _flashExposure = Mathf.MoveTowards(_flashExposure, target, Time.deltaTime * (target < _flashExposure ? 4f : 1.5f));
            Flashlight.intensity = FlashlightIntensity * _flashExposure;
        }

        const float FlashlightIntensity = 26f;
        float _flashExposure = 1;

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Controls.Gather(dt);
            Pos = transform.position;
            if (!Alive)
            {
                DeathCam(dt);
                return;
            }
            var input = Controls.State;
            if (_stun > 0) { _stun -= dt; input.Move *= 0.3f; input.Look *= 0.4f; }
            // Look.
            var set = SaveSystem.Data.Settings;
            float sens = set.Sensitivity * (Guns.AimAmount > 0.5f ? set.AdsSensitivity * Mathf.Lerp(1, Guns.Zoom, 0.6f) : 1);
            var look = input.Look;
            if (!AutoPilot.Active) look *= sens;
            look *= Guns.AimAssistFriction();
            Yaw += look.x;
            Pitch = Mathf.Clamp(Pitch + look.y, -82, 84);
            // Recoil springs.
            _recoilPitch.Step(0, 90, 13, dt);
            _recoilYaw.Step(0, 90, 13, dt);
            if (input.FlashlightPressed) SetFlashlight(!FlashlightOn);
            if (FlashlightOn) PlaceFlashlight();
            if (input.CrouchPressed) Crouched = !Crouched;
            // Movement.
            Grounded = Cc.isGrounded;
            var move = input.Move;
            bool sprint = (input.Sprint || move.y > 0.92f) && !input.Fire && Guns.AimAmount < 0.3f && !Crouched && !Guns.Reloading;
            float speed = 4.6f * Stats.MoveMul * Guns.MoveMul * (sprint ? 1.42f : 1f) * (Crouched ? 0.55f : 1f) * Mathf.Lerp(1, 0.6f, Guns.AimAmount);
            var fwd = Quaternion.Euler(0, Yaw, 0);
            var wish = fwd * new Vector3(move.x, 0, move.y) * speed;
            if (Grounded)
            {
                if (_vy < -6) { _landDip = Mathf.Clamp(-_vy * 0.012f, 0, 0.12f); Sfx.Land(); }
                _vy = -2f;
                if (input.JumpPressed && !Crouched) { _vy = 6.8f; Sfx.Jump(); }
                if (input.JumpPressed && Crouched) Crouched = false;
            }
            else _vy -= 22f * dt;
            _knock = Vector3.Lerp(_knock, Vector3.zero, 1 - Mathf.Exp(-dt * 5));
            var vel = wish + Vector3.up * _vy + _knock;
            Cc.Move(vel * dt);
            Pos = transform.position;
            // Crouch / eye height.
            float eyeTarget = Crouched ? 1.05f : 1.62f;
            _eye = Mathf.Lerp(_eye, eyeTarget, 1 - Mathf.Exp(-dt * 12));
            Cc.height = Crouched ? 1.2f : 1.8f;
            Cc.center = new Vector3(0, Cc.height / 2, 0);
            // Head bob and footsteps.
            float hspeed = new Vector3(Cc.velocity.x, 0, Cc.velocity.z).magnitude;
            if (Grounded && hspeed > 0.5f)
            {
                StepPhase += hspeed * dt * 1.9f;
                _footDist += hspeed * dt;
                if (_footDist > (sprint ? 2.2f : 1.7f)) { _footDist = 0; Sfx.Footstep(); }
            }
            float bobAmt = Grounded ? Mathf.Clamp01(hspeed / 5) * (1 - Guns.AimAmount * 0.8f) : 0;
            _bob = Mathf.Lerp(_bob, bobAmt, 1 - Mathf.Exp(-dt * 8));
            _landDip = Mathf.Lerp(_landDip, 0, 1 - Mathf.Exp(-dt * 7));
            var bobOff = new Vector3(Mathf.Cos(StepPhase) * 0.025f, -Mathf.Abs(Mathf.Sin(StepPhase)) * 0.04f, 0) * _bob;
            Head.localPosition = new Vector3(0, _eye - _landDip, 0) + bobOff;
            var shake = CameraShake.Offset(dt);
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            Head.localRotation = Quaternion.Euler(Pitch + _recoilPitch.Value + shake.x, _recoilYaw.Value + shake.y, shake.z + Mathf.Sin(StepPhase) * 0.6f * _bob);
            // FOV: aim zoom and sprint kick.
            _fovKick = Mathf.Lerp(_fovKick, sprint && hspeed > 3 ? 6 : 0, 1 - Mathf.Exp(-dt * 6));
            WorldCam.fieldOfView = Mathf.Lerp(BaseFov + _fovKick, BaseFov * Guns.Zoom, Guns.AimAmount);
            // Regeneration.
            _regenWait -= dt;
            bool noRegen = StageRunner.Instance != null && StageRunner.Instance.Plan != null && (StageRunner.Instance.Plan.Mutators & Mutator.NoRegen) != 0;
            if (_regenWait <= 0 && Stats.Regen > 0 && Hp < MaxHp && !noRegen) Hp = Mathf.Min(MaxHp, Hp + Stats.Regen * dt);
            // Low health feedback.
            float low = 1 - Mathf.Clamp01(Hp / (MaxHp * 0.35f));
            float recent = Mathf.Clamp01(1 - (Time.time - LastHurtTime) * 2.5f);
            Look.SetHurt(Mathf.Max(low * 0.7f, recent * 0.55f));
            Sfx.Heartbeat(low);
            // Fall out of the world safety. Every floor is at y = 0, so below -1.5 m the player has left the level:
            // put them back where they last stood on the floor plan (the level start only if there is no such spot).
            var here = transform.position;
            if (Grounded && here.y > -0.3f && OnFloorPlan(here)) { _lastSafe = here; _haveSafe = true; }
            if (here.y < -1.5f && _haveSafe) { Cc.enabled = false; transform.position = _lastSafe + Vector3.up * 0.1f; Cc.enabled = true; _vy = 0; _knock = Vector3.zero; }
            else if (here.y < -20) { Cc.enabled = false; transform.position = StageRunner.Instance != null ? StageRunner.Instance.Level.Start + Vector3.up : Vector3.up; Cc.enabled = true; _vy = 0; }
        }

        Vector3 _lastSafe;
        bool _haveSafe;

        static bool OnFloorPlan(Vector3 p)
        {
            var run = StageRunner.Instance;
            var g = run != null && run.Level != null && run.Level.Plan != null ? run.Level.Plan.Grid : null;
            return g == null || g.IsOpen(Mathf.FloorToInt(p.x / Level.Cell), Mathf.FloorToInt(p.z / Level.Cell));
        }

        public void Recoil(float pitch, float yaw)
        {
            _recoilPitch.Velocity -= pitch * 22;
            _recoilYaw.Velocity += yaw * 22;
            Pitch -= pitch * 0.35f;
            Yaw += yaw * 0.2f;
        }

        // --------------------------------------------------------------- health

        public void Hurt(float amount, Vector3 from, HurtKind kind, bool continuous = false)
        {
            if (!Alive || amount <= 0 || Invulnerable) return;
            amount *= Stats.DamageTaken;
            if (Armor > 0 && kind != HurtKind.Toxic)
            {
                float absorbed = Mathf.Min(Armor, amount * 0.6f);
                Armor -= absorbed;
                amount -= absorbed;
            }
            Hp -= amount;
            _regenWait = 4f;
            if (!continuous || Time.time - LastHurtTime > 0.5f)
            {
                LastHurtTime = Time.time;
                LastHurtFrom = from;
                Hurted?.Invoke(amount, from, kind);
                CameraShake.Add(kind == HurtKind.Explosion ? 0.45f : 0.22f);
                Sfx.PlayerHurt(kind);
                Haptics.Pulse(kind == HurtKind.Explosion ? 60 : 25);
            }
            if (Hp <= 0)
            {
                if (Stats.SecondWind && !_secondWindUsed)
                {
                    _secondWindUsed = true;
                    Hp = MaxHp * 0.35f;
                    Hud.Announce("SECOND WIND", "You refuse to die", new Color(0.5f, 1f, 0.5f));
                    return;
                }
                Hp = 0;
                Die();
            }
        }

        public void Heal(float amount) => Hp = Mathf.Min(MaxHp, Hp + amount);
        public void AddArmor(float amount) => Armor = Mathf.Min(Mathf.Max(MaxArmor, 50), Armor + amount);
        public void Knockback(Vector3 v) => _knock += v;
        public void Stun(float t) => _stun = Mathf.Max(_stun, t);

        void Die()
        {
            Alive = false;
            _deathT = 0;
            Guns.Holster();
            Sfx.PlayerDeath();
            Died?.Invoke();
        }

        float _deathT;

        void DeathCam(float dt)
        {
            _deathT += dt;
            float k = X.Smooth01(_deathT / 1.2f);
            Head.localPosition = new Vector3(0, Mathf.Lerp(_eye, 0.25f, k), 0);
            Head.localRotation = Quaternion.Euler(Pitch * (1 - k) - 10 * k, 0, 70 * k);
            Look.SetHurt(1);
        }

        /// <summary>Brings the player back (revive after an ad or gems).</summary>
        public void Revive()
        {
            Alive = true;
            Hp = MaxHp * 0.6f;
            Armor = MaxArmor;
            Head.localRotation = Quaternion.identity;
            Guns.Draw();
            Invulnerable = true;
            Invoke(nameof(EndInvuln), 2.5f);
            Look.SetHurt(0);
        }

        void EndInvuln() => Invulnerable = false;
    }

    public static class Haptics
    {
        static float _last;

        public static void Pulse(int ms)
        {
            if (!SaveSystem.Data.Settings.Vibration || Time.unscaledTime - _last < 0.15f) return;
            _last = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            // Referencing Handheld.Vibrate makes the build add the VIBRATE permission.
            if (ms > 100000) Handheld.Vibrate();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vib?.Call("vibrate", (long)ms);
                }
            }
            catch (Exception) { }
#endif
        }
    }
}
