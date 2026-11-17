using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    public enum CamMode { Broadcast, BehindBatter, Follow, Stumps, Orbit, Focus, Toss }

    /// <summary>
    /// Broadcast-style cameras: the classic view from behind the bowler, a low behind-the-batter
    /// view, side cameras that track the ball with zoom, a stumps close-up and orbits.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public CamMode Mode = CamMode.Broadcast;
        public Transform Ball;
        public Vector3 FocusPos, FocusLook;
        public float FocusFov = 35;
        float _orbit;
        Vector3 _pos, _vel;
        Vector3 _look, _lookVel;
        float _fov, _fovVel;
        float _shake;
        bool _snap = true;
        int _followSide = 1;
        public float OrbitRadius = 62, OrbitHeight = 18, OrbitLookY = 1.5f, OrbitFov = 38;
        public Vector3 OrbitCenter = Vector3.zero;

        public static CameraRig Attach(Camera cam)
        {
            var r = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            r.Cam = cam;
            return r;
        }

        public void Set(CamMode m, bool snap = false)
        {
            if (m == CamMode.Follow && Mode != CamMode.Follow && Ball != null) _followSide = Ball.position.x > 0 ? -1 : 1;
            Mode = m;
            if (snap) _snap = true;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 pos, look;
            float fov;
            switch (Mode)
            {
                case CamMode.BehindBatter:
                    pos = new Vector3(0.0f, 2.05f, -17.8f);
                    look = new Vector3(0, 1.05f, 6);
                    fov = 50;
                    break;
                case CamMode.Follow:
                {
                    var b = Ball != null ? Ball.position : Vector3.zero;
                    var anchor = new Vector3(_followSide * 74, 30, -6);
                    float d = Vector3.Distance(anchor, b);
                    // Pull towards the pitch for close action so we see the batters too.
                    float k = Mathf.Clamp01(b.magnitude / 70f);
                    pos = Vector3.Lerp(new Vector3(_followSide * 34, 22, -26), anchor, k);
                    look = Vector3.Lerp(new Vector3(b.x * 0.6f, 0.5f, b.z * 0.6f - 3), b, 0.65f + 0.35f * k);
                    fov = Mathf.Clamp(2 * Mathf.Atan(28f / Mathf.Max(10, d)) * Mathf.Rad2Deg, 16, 52);
                    break;
                }
                case CamMode.Stumps:
                    pos = new Vector3(2.6f, 0.75f, (float)Pitch.StrikerStumpsZ - 4.2f);
                    look = new Vector3(0, 0.45f, (float)Pitch.StrikerStumpsZ);
                    fov = 34;
                    break;
                case CamMode.Orbit:
                    _orbit += dt * 0.06f;
                    pos = OrbitCenter + new Vector3(Mathf.Sin(_orbit) * OrbitRadius, OrbitHeight, Mathf.Cos(_orbit) * OrbitRadius);
                    look = OrbitCenter + new Vector3(0, OrbitLookY, 0);
                    fov = OrbitFov;
                    break;
                case CamMode.Focus:
                case CamMode.Toss:
                    pos = FocusPos;
                    look = FocusLook;
                    fov = FocusFov;
                    break;
                default:
                    // Behind the bowler's arm: batter centred, giant screen and crowd above, bowler and umpire below.
                    pos = new Vector3(0.4f, 3.9f, 20.5f);
                    look = new Vector3(0, 1.35f, -8.0f);
                    fov = 26f;
                    break;
            }
            if (_snap)
            {
                _pos = pos;
                _look = look;
                _fov = fov;
                _vel = _lookVel = Vector3.zero;
                _fovVel = 0;
                _snap = false;
            }
            else
            {
                float smooth = Mode == CamMode.Follow ? 0.35f : Mode == CamMode.Orbit ? 0.6f : 0.45f;
                _pos = Vector3.SmoothDamp(_pos, pos, ref _vel, smooth, Mathf.Infinity, dt);
                _look = Vector3.SmoothDamp(_look, look, ref _lookVel, Mode == CamMode.Follow ? 0.12f : 0.3f, Mathf.Infinity, dt);
                _fov = Mathf.SmoothDamp(_fov, fov, ref _fovVel, 0.35f, Mathf.Infinity, dt);
            }
            var shake = Vector3.zero;
            if (_shake > 0.001f)
            {
                shake = Random.insideUnitSphere * _shake;
                _shake = Mathf.MoveTowards(_shake, 0, dt * 0.6f);
            }
            Cam.transform.position = _pos + shake;
            Cam.transform.rotation = Quaternion.LookRotation((_look - _pos).normalized, Vector3.up);
            Cam.fieldOfView = _fov;
        }
    }

    /// <summary>The ball: white T20 ball with a soft trail and a blob shadow.</summary>
    public sealed class BallView : MonoBehaviour
    {
        public Transform Ball, Shadow;
        TrailRenderer _trail;
        Vector3 _last;
        public bool Spin = true;

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("BallView");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallView>();
            v.Ball = Gear.MakeBall(go.transform, 0.036f * 1.25f);
            v._trail = v.Ball.gameObject.AddComponent<TrailRenderer>();
            v._trail.time = 0.12f;
            v._trail.minVertexDistance = 0.05f;
            v._trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.07f), new Keyframe(1, 0));
            v._trail.material = Mats.Additive(new Color(1, 1, 1, 0.55f));
            v._trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v._trail.emitting = SaveSystem.Data.Settings.BallTrail;
            var sh = MeshKit.Spawn("BallShadow", MeshKit.Quad(0.22f, 0.22f), go.transform, Mats.UnlitTransparent(new Color(0, 0, 0, 0.55f), Tex.Glow));
            sh.transform.rotation = Quaternion.Euler(90, 0, 0);
            MeshKit.NoShadows(sh);
            v.Shadow = sh.transform;
            v.Hide();
            return v;
        }

        public void Show(Vector3 p)
        {
            // Move first, then clear, so the trail never draws a streak from the last position.
            _trail.emitting = false;
            Ball.position = p;
            _last = p;
            Ball.gameObject.SetActive(true);
            Shadow.gameObject.SetActive(true);
            _trail.Clear();
            _trailDelay = 2;
        }

        int _trailDelay;

        void LateUpdate()
        {
            if (_trailDelay > 0 && --_trailDelay == 0) _trail.emitting = SaveSystem.Data.Settings.BallTrail;
        }

        public void Hide()
        {
            Ball.gameObject.SetActive(false);
            Shadow.gameObject.SetActive(false);
        }

        public bool Visible => Ball.gameObject.activeSelf;

        public void Move(Vector3 p)
        {
            var d = p - _last;
            if (Spin && d.sqrMagnitude > 1e-6f) Ball.Rotate(Vector3.Cross(Vector3.up, d.normalized), d.magnitude / 0.045f * Mathf.Rad2Deg, Space.World);
            Ball.position = p;
            _last = p;
            Shadow.position = new Vector3(p.x, 0.02f, p.z);
            float h = Mathf.Clamp01(p.y / 8f);
            Shadow.localScale = Vector3.one * (1 + h * 1.5f);
        }
    }
}
