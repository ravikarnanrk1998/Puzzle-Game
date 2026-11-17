using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>
    /// Imported character body (e.g. a Rocketbox avatar) that follows a <see cref="ZombieRig"/>'s procedural
    /// skeleton. The game keeps animating, hit-testing and dismembering its own 21-bone rig exactly as before;
    /// that rig's mesh is hidden and this body copies its pose every frame:
    ///  - rotations: model bone = driver bone (world) * rest offset recorded in the matching "arms down" pose;
    ///  - pins: model bones whose skeletal parent differs from the game's (Biped thighs hang off the spine,
    ///    clavicles off the neck) are held at their rest offset from the game's parent;
    ///  - hips follow the driver hips; bone scale is copied so a shot-off head or arm disappears.
    /// Built by ArtPipeline (editor); arrays are indexed by <see cref="ZB"/>.
    /// </summary>
    public sealed class ArtBody : MonoBehaviour
    {
        public Transform[] Bones;
        public Quaternion[] Rest;
        public bool[] Pinned;
        public Vector3[] PinLocal;
        public Vector3 HipOffset;
        public Transform EyeL, EyeR;
        public float EyeSize = 0.011f;
        public SkinnedMeshRenderer[] Renderers;

        /// <summary>Cheaper bodies (2-bone skinning, no shadow casting) at any preset; Low always uses this.</summary>
        public static bool Lite;

        ZombieRig _rig;
        Material[][] _materials;
        Material[][] _cloaked;
        GameObject[] _eyes;
        bool _visible = true;
        Transform[] _sockets;
        Vector3[] _scaleMul;
        readonly List<MeshRenderer> _extras = new List<MeshRenderer>();
        readonly List<Material[]> _extraMats = new List<Material[]>();
        readonly List<int> _extraGlow = new List<int>();

        /// <summary>
        /// A child of the imported bone that turns with the matching game bone (model bone = game bone * Rest),
        /// so offsets authored against the game skeleton's rest pose land on the imported body.
        /// </summary>
        public Transform Socket(ZB b)
        {
            int i = (int)b;
            if (Bones == null || i <= 0 || i >= Bones.Length || Bones[i] == null) return null;
            if (_sockets == null) _sockets = new Transform[Bones.Length];
            if (_sockets[i] != null) return _sockets[i];
            var t = new GameObject("Socket_" + b).transform;
            t.SetParent(Bones[i], false);
            t.localRotation = Quaternion.Inverse(Rest[i]);
            t.gameObject.layer = gameObject.layer;
            _sockets[i] = t;
            return t;
        }

        /// <summary>Adds a gear renderer that follows visibility and cloaking (its glow submesh keeps glowing).</summary>
        public void AddExtra(MeshRenderer r, int glowSubmesh)
        {
            _extras.Add(r);
            _extraMats.Add(r.sharedMaterials);
            _extraGlow.Add(glowSubmesh);
            r.enabled = _visible;
        }

        /// <summary>Scales one imported bone on top of the game bone's own scale (e.g. 0 below a crawler's knees).</summary>
        public void SetBoneScale(ZB b, Vector3 mul)
        {
            if (_scaleMul == null)
            {
                _scaleMul = new Vector3[Bones.Length];
                for (int i = 0; i < _scaleMul.Length; i++) _scaleMul[i] = Vector3.one;
            }
            _scaleMul[(int)b] = mul;
        }

        public bool IsVisible
        {
            get
            {
                if (!_visible) return false;
                foreach (var r in Renderers) if (r != null && r.isVisible) return true;
                return false;
            }
        }

        public void Bind(ZombieRig rig, int layer)
        {
            _rig = rig;
            _materials = new Material[Renderers.Length][];
            bool lite = Lite || Quality.Current == Gfx.Low;
            for (int i = 0; i < Renderers.Length; i++)
            {
                var r = Renderers[i];
                _materials[i] = r.sharedMaterials;
                r.quality = lite ? SkinQuality.Bone2 : SkinQuality.Bone4;
                r.shadowCastingMode = lite ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.updateWhenOffscreen = false;
                r.localBounds = new Bounds(Vector3.zero, new Vector3(2.6f, 2.6f, 2.6f));
            }
            // Glowing eyes on the model's eye bones (the game's eye colour encodes type / elite).
            var glow = rig.Materials != null && rig.Materials.Length > 6 ? rig.Materials[6] : null;
            var eyeCol = rig.LookDef != null ? rig.LookDef.EyeGlow : Color.black;
            if (glow != null && eyeCol.maxColorComponent > 0.05f && EyeL != null && EyeR != null)
            {
                var mesh = EyeMesh();
                _eyes = new GameObject[2];
                int k = 0;
                foreach (var e in new[] { EyeL, EyeR })
                {
                    var go = new GameObject("Glow");
                    go.transform.SetParent(e, false);
                    go.transform.localScale = Vector3.one * EyeSize * 2f / Mathf.Max(0.0001f, e.lossyScale.x / rig.transform.lossyScale.x);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = glow;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    _eyes[k++] = go;
                }
            }
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            LateUpdate();
        }

        static Mesh _eyeMesh;

        static Mesh EyeMesh()
        {
            if (_eyeMesh != null) return _eyeMesh;
            var mb = new MeshBuilder();
            mb.Sphere(0, Vector3.zero, Vector3.one * 0.5f, 8, 6);
            _eyeMesh = mb.ToMesh("artEye", false);
            return _eyeMesh;
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            foreach (var r in Renderers) if (r != null) r.enabled = v;
            if (_eyes != null) foreach (var e in _eyes) if (e != null) e.SetActive(v);
            foreach (var r in _extras) if (r != null) r.enabled = v;
        }

        public void SetRenderingLayer(uint mask)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = mask;
        }

        /// <summary>Swaps every submesh to the shimmer material (the eyes keep glowing), or back.</summary>
        public void SetCloak(bool on, Material shimmer)
        {
            if (on && _cloaked == null)
            {
                _cloaked = new Material[Renderers.Length][];
                for (int i = 0; i < Renderers.Length; i++)
                {
                    _cloaked[i] = new Material[_materials[i].Length];
                    for (int s = 0; s < _cloaked[i].Length; s++) _cloaked[i][s] = shimmer;
                }
            }
            for (int i = 0; i < Renderers.Length; i++)
            {
                var want = on ? _cloaked[i] : _materials[i];
                if (Renderers[i].sharedMaterials[0] != want[0]) Renderers[i].sharedMaterials = want;
            }
            for (int i = 0; i < _extras.Count; i++)
            {
                var r = _extras[i];
                if (r == null) continue;
                var orig = _extraMats[i];
                var want = orig;
                if (on)
                {
                    want = new Material[orig.Length];
                    for (int s = 0; s < want.Length; s++) want[s] = s == _extraGlow[i] ? orig[s] : shimmer;
                }
                if (r.sharedMaterials[0] != want[0]) r.sharedMaterials = want;
            }
        }

        void LateUpdate()
        {
            if (_rig == null || !_visible) return;
            var drv = _rig.Bones;
            var root = _rig.transform;
            for (int i = 1; i < Bones.Length && i < drv.Length; i++)
            {
                var b = Bones[i];
                if (b == null) continue;
                var d = drv[i];
                if (i == (int)ZB.Hips) b.position = root.TransformPoint(d.localPosition + HipOffset);
                else if (Pinned[i]) b.position = Bones[(int)ZombieRig.ParentOf((ZB)i)].TransformPoint(PinLocal[i]);
                b.rotation = d.rotation * Rest[i];
                var s = d.localScale;
                if (_scaleMul != null) s = Vector3.Scale(s, _scaleMul[i]);
                if (b.localScale != s) b.localScale = s;
            }
        }
    }
}
