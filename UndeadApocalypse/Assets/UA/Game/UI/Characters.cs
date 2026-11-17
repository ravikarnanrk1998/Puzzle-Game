using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UA.Game
{
    /// <summary>
    /// The six playable characters. Each one is an outfit slot (same index, price and save data as the old
    /// outfits), so owned and equipped outfits carry over. In first person the outfit still drives the
    /// sleeves; in the menus the character is shown full-body by <see cref="CharacterPreview"/>.
    /// </summary>
    public static class Characters
    {
        public static readonly string[] Roles =
        {
            "Frontline rifleman. Steady under fire.",
            "Elite operator. Masked, armoured, silent.",
            "Scavenger who made it through day one.",
            "Hooded tracker who stalks the dead.",
            "Mechanic and demolitions expert.",
            "Field medic. Keeps the squad standing.",
        };

        /// <summary>Signature weapon shown in the character's hands.</summary>
        public static readonly WeaponId[] Weapon =
        {
            WeaponId.R4Carbine, WeaponId.RavenTR, WeaponId.VK74, WeaponId.SemiSniper, WeaponId.PumpShotgun, WeaponId.Smg,
        };

        public static void Look(int i, out ZShape s, out ZLook l)
        {
            s = new ZShape { Build = 1.12f, Muscle = 0.4f, Shirt = 2, Pants = 0, Hair = 1, Shoes = 1 };
            l = new ZLook
            {
                Human = true, Seed = 11 + i * 7, Decay = 0, Tatter = 0.05f, EyeGlow = Color.black,
                HairCol = X.Hex(0x2A1E16), ShoeCol = X.Hex(0x24211C), GearCol = X.Hex(0x3E4230), Gear2Col = X.Hex(0x15171A),
            };
            switch (i)
            {
                case 0: // Soldier: woodland fatigues, plate carrier, beard.
                    s.Vest = true; l.HumanTone = 1; l.Beard = true;
                    l.ShirtKind = 2; l.ShirtCol = Color.white; l.PantsKind = 2; l.PantsCol = Color.white; l.GearCol = X.Hex(0x4A4632);
                    break;
                case 1: // Special forces: black kit, helmet, mask.
                    s.Helmet = true; s.Vest = true; s.GasMask = true; s.Hair = 0; l.HumanTone = 1;
                    l.ShirtKind = 5; l.ShirtCol = X.Hex(0x2A2E36); l.PantsKind = 5; l.PantsCol = X.Hex(0x22262C); l.GearCol = X.Hex(0x1E2226);
                    break;
                case 2: // Survivor: tank top, cargo pants.
                    s.Shirt = 1; s.Hair = 2; s.Build = 1.0f; s.Muscle = 0.2f; l.HumanTone = 0; l.HairCol = X.Hex(0x1A120C);
                    l.ShirtKind = 0; l.ShirtCol = X.Hex(0x3A3F44); l.PantsKind = 0; l.PantsCol = X.Hex(0x4A4A30);
                    break;
                case 3: // Hunter: hood and cloak over a vest, masked.
                    s.Hood = true; s.Cloak = true; s.Vest = true; s.GasMask = true; s.Hair = 0; l.HumanTone = 2;
                    l.ShirtKind = 4; l.ShirtCol = X.Hex(0x1C2028); l.PantsKind = 0; l.PantsCol = X.Hex(0x22252A); l.GearCol = X.Hex(0x2E3226);
                    break;
                case 4: // Engineer: work jacket, jeans, beard.
                    s.Hair = 1; l.HumanTone = 2; l.Beard = true; l.HairCol = X.Hex(0x16100C);
                    l.ShirtKind = 4; l.ShirtCol = X.Hex(0x6A4A2A); l.PantsKind = 1; l.PantsCol = X.Hex(0x2A3A55);
                    break;
                default: // Medic: light scrubs.
                    s.Hair = 2; s.Build = 1.0f; s.Muscle = 0.2f; l.HumanTone = 0; l.HairCol = X.Hex(0x3A2414);
                    l.ShirtKind = 0; l.ShirtCol = X.Hex(0xD6D4CF); l.PantsKind = 0; l.PantsCol = X.Hex(0xC4C2BD);
                    break;
            }
        }

        /// <summary>
        /// Port-arms carry: the rifle diagonally across the chest (grip at the right hip, muzzle up past the
        /// left shoulder) with both hands solved onto it by two-bone IK.
        /// </summary>
        public static void Pose(ZombieRig r, GunModel gun)
        {
            var root = r.transform;
            r[ZB.Head].localRotation = Quaternion.Euler(3, -8, 0);
            r[ZB.ThighL].localRotation = Quaternion.Euler(2, 0, -5);
            r[ZB.ThighR].localRotation = Quaternion.Euler(-4, 0, 5);
            r[ZB.Spine].localRotation = Quaternion.Euler(0, 6, 0);
            if (gun == null) return;
            var gripR = root.TransformPoint(new Vector3(0.13f, 1.06f, 0.2f));
            var rot = Quaternion.LookRotation(root.TransformDirection(new Vector3(-0.62f, 0.55f, 0.56f)), root.TransformDirection(new Vector3(0.25f, 0.35f, 0.9f)));
            gun.transform.rotation = rot;
            gun.transform.position = gripR - rot * (gun.GripR != null ? gun.GripR.localPosition : Vector3.zero);
            Ik(r[ZB.ArmR], r[ZB.ForeR], r[ZB.HandR], gripR, root.TransformPoint(new Vector3(0.55f, 1.0f, -0.35f)));
            if (gun.GripL != null) Ik(r[ZB.ArmL], r[ZB.ForeL], r[ZB.HandL], gun.GripL.position, root.TransformPoint(new Vector3(-0.55f, 1.1f, -0.1f)));
        }

        /// <summary>Analytic two-bone IK: bends upper/lower so the end bone reaches the target, elbow toward the pole.</summary>
        static void Ik(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            var a = upper.position;
            float l1 = (lower.position - a).magnitude, l2 = (end.position - lower.position).magnitude;
            var to = target - a;
            float d = Mathf.Clamp(to.magnitude, 0.05f, l1 + l2 - 0.002f);
            var dir = to.normalized;
            var p = pole - a;
            p -= Vector3.Dot(p, dir) * dir;
            var perp = p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1 - cosA * cosA);
            var elbow = a + (dir * cosA + perp * sinA) * l1;
            upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
        }
    }

    /// <summary>Renders a slowly turning, lit full-body character into a texture for the Characters tab.</summary>
    public static class CharacterPreview
    {
        static GameObject _stage;
        static Camera _cam;
        static RenderTexture _rt;
        static Transform _spin;
        static GameObject _current;
        static int _shown = -1;
        static float _yaw;

        public static RenderTexture Show(int character)
        {
            if (_stage == null) BuildStage();
            _stage.SetActive(true);
            if (_shown != character || _current == null)
            {
                if (_current != null) Object.Destroy(_current);
                _current = new GameObject("Character");
                _current.transform.SetParent(_spin, false);
                Characters.Look(character, out var shape, out var look);
                var rig = ZombieRig.Create(_current.transform, shape, look, "Characters/Character" + character);
                var gun = GunModels.Build(Characters.Weapon[character], null, rig.transform, Layers.Viewmodel);
                Characters.Pose(rig, gun);
                foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Viewmodel;
                rig.SetRenderingLayer(LightLayers.Viewmodel);
                rig.Skin.updateWhenOffscreen = true;
                _shown = character;
            }
            return _rt;
        }

        static void BuildStage()
        {
            _stage = new GameObject("CharacterPreview");
            Object.DontDestroyOnLoad(_stage);
            _stage.transform.position = new Vector3(0, -3000, 0);
            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(_stage.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0.95f, 4.7f);
            camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.04f, -1));
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 30;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 12;
            _cam.cullingMask = 1 << Layers.Viewmodel;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0, 0, 0, 0);
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            _rt = new RenderTexture(560, 840, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            _cam.targetTexture = _rt;
            // Key (warm, front-left), rim (cool, behind) and fill.
            foreach (var (pos, col, inten) in new[]
            {
                (new Vector3(-2.2f, 3.0f, 2.6f), new Color(1f, 0.92f, 0.82f), 30f),
                (new Vector3(1.8f, 2.4f, -2.4f), new Color(0.45f, 0.65f, 1f), 26f),
                (new Vector3(2.4f, 1.2f, 2.2f), new Color(0.75f, 0.8f, 0.9f), 9f),
            })
            {
                var lg = new GameObject("Light");
                lg.transform.SetParent(_stage.transform, false);
                lg.transform.localPosition = pos;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9;
                l.color = col;
                l.intensity = inten;
                l.shadows = LightShadows.None;
                LightLayers.Set(l, LightLayers.Viewmodel);
            }
            // Pedestal disc with a soft glowing rim.
            var mb = new MeshBuilder();
            mb.Cylinder(0, Vector3.zero, Vector3.up * 0.05f, 0.75f, 0.75f, 40);
            mb.Cylinder(1, Vector3.up * 0.01f, Vector3.up * 0.045f, 0.765f, 0.765f, 40, false, false);
            var ped = new GameObject("Pedestal");
            ped.transform.SetParent(_stage.transform, false);
            ped.transform.localPosition = new Vector3(0, -0.06f, 0);
            ped.layer = Layers.Viewmodel;
            ped.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("pedestal");
            var pr = ped.AddComponent<MeshRenderer>();
            pr.sharedMaterials = new[] { Mats.Lit(new Color(0.08f, 0.09f, 0.1f), 0.6f, 0.6f), Mats.Emissive(new Color(1f, 0.45f, 0.15f), new Color(2.2f, 0.9f, 0.25f)) };
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.renderingLayerMask = LightLayers.Viewmodel;
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_stage.transform, false);
        }

        public static void Tick(float dt)
        {
            if (_spin == null || _stage == null || !_stage.activeSelf) return;
            _yaw += dt * 18;
            _spin.localRotation = Quaternion.Euler(0, Mathf.Sin(_yaw * Mathf.Deg2Rad) * 30, 0);
        }

        public static void Hide()
        {
            if (_stage != null) _stage.SetActive(false);
        }

        /// <summary>Forces the next Show to rebuild the character (e.g. after toggling imported art).</summary>
        public static void Invalidate() => _shown = -1;
    }
}
