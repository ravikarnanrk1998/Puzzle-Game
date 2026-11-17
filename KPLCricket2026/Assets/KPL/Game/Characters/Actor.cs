using System;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Drives a <see cref="Body"/>: holds a pose, plays a clip with root motion, runs to a point
    /// with a procedural gait, or follows a custom pose function. Changes cross-fade.
    /// </summary>
    public sealed class Actor : MonoBehaviour
    {
        public Body Body;
        public Vector3 Pos;
        public float Yaw;
        public bool Mirror;
        public bool CarryBat;

        enum Mode { Hold, Clip, Move, Proc }

        Mode _mode = Mode.Hold;
        Pose _hold = new Pose();
        Pose _last = new Pose();
        Pose _from;
        float _blend = 1, _blendDur = 0.2f;
        Clip _clip;
        float _t, _speed = 1;
        Vector3 _clipOrigin;
        float _clipYaw;
        Action _done;
        Vector3 _dest;
        float _moveSpeed, _curSpeed, _phase;
        float? _endYaw;
        Pose _arrivePose;
        Func<float, Pose> _proc;
        float _procT;
        public float TimeScale = 1;

        public static Actor Create(Transform parent, Look look, string name, Vector3 pos, float yaw)
        {
            var body = Body.Create(parent, look, name);
            var a = body.gameObject.AddComponent<Actor>();
            a.Body = body;
            a.Pos = pos;
            a.Yaw = yaw;
            a.Hold(Poses.Stand(), 0);
            return a;
        }

        Pose M(Pose p) => Mirror ? p.Mirror() : p;

        void StartBlend(float dur)
        {
            _from = _last.Clone();
            _blendDur = Mathf.Max(0.0001f, dur);
            _blend = dur <= 0 ? 1 : 0;
        }

        public void Hold(Pose p, float blend = 0.2f)
        {
            StartBlend(blend);
            _mode = Mode.Hold;
            _hold = p;
            _done = null;
        }

        public void Play(Clip c, float speed = 1, Action done = null, float blend = 0.12f)
        {
            StartBlend(blend);
            _mode = Mode.Clip;
            _clip = c;
            _t = 0;
            _speed = speed;
            _clipOrigin = Pos;
            _clipYaw = Yaw;
            _done = done;
        }

        public void Proc(Func<float, Pose> f, float blend = 0.15f)
        {
            StartBlend(blend);
            _mode = Mode.Proc;
            _proc = f;
            _procT = 0;
        }

        public void MoveTo(Vector3 dest, float speed, float? endYaw = null, Action arrived = null, Pose arrivePose = null)
        {
            if (_mode != Mode.Move) StartBlend(0.18f);
            _mode = Mode.Move;
            _dest = new Vector3(dest.x, 0, dest.z);
            _moveSpeed = speed;
            _endYaw = endYaw;
            _done = arrived;
            _arrivePose = arrivePose;
        }

        public bool Moving => _mode == Mode.Move;
        public float ClipTime => _t;

        void LateUpdate()
        {
            float dt = Time.deltaTime * TimeScale;
            Pose target;
            switch (_mode)
            {
                case Mode.Clip:
                {
                    _t += dt * _speed;
                    var p = _clip.Sample(_t);
                    target = M(p);
                    // Root motion: clip offsets are relative to where the clip started.
                    var off = Quaternion.Euler(0, _clipYaw, 0) * new Vector3(target.RootOffset.x, 0, target.RootOffset.z) * Body.Scale;
                    Pos = _clipOrigin + off;
                    target = target.Clone();
                    target.RootOffset = new Vector3(0, target.RootOffset.y, 0);
                    if (_t >= _clip.Length)
                    {
                        var d = _done;
                        _done = null;
                        _mode = Mode.Hold;
                        _hold = M(_clip.Sample(_clip.Length));
                        _hold = _hold.Clone();
                        _hold.RootOffset = new Vector3(0, _hold.RootOffset.y, 0);
                        d?.Invoke();
                    }
                    break;
                }
                case Mode.Move:
                {
                    var to = _dest - Pos;
                    to.y = 0;
                    float dist = to.magnitude;
                    float want = dist < 0.05f ? 0 : Mathf.Min(_moveSpeed, dist * 3.2f + 0.6f);
                    _curSpeed = Mathf.MoveTowards(_curSpeed, want, dt * 9f);
                    if (dist > 0.05f)
                    {
                        float yawTo = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        Yaw = Mathf.MoveTowardsAngle(Yaw, yawTo, dt * 540);
                        float step = Mathf.Min(dist, _curSpeed * dt);
                        Pos += to / dist * step;
                    }
                    float stride = 2 * (0.32f + Mathf.Max(0.5f, _curSpeed) * 0.12f);
                    _phase = Mathf.Repeat(_phase + _curSpeed * dt / stride, 1);
                    target = Poses.Gait(_phase, Mathf.Max(0.01f, _curSpeed), CarryBat);
                    if (dist <= 0.05f && _curSpeed < 0.3f)
                    {
                        if (_endYaw != null) Yaw = Mathf.MoveTowardsAngle(Yaw, _endYaw.Value, dt * 360);
                        if (_endYaw == null || Mathf.Abs(Mathf.DeltaAngle(Yaw, _endYaw.Value)) < 2)
                        {
                            var d = _done;
                            _done = null;
                            Hold(_arrivePose ?? Poses.Stand(), 0.25f);
                            d?.Invoke();
                            target = Poses.Gait(_phase, 0.01f, CarryBat);
                        }
                    }
                    break;
                }
                case Mode.Proc:
                    _procT += dt;
                    target = M(_proc(_procT));
                    break;
                default:
                    target = _hold;
                    break;
            }
            if (_blend < 1)
            {
                _blend = Mathf.Min(1, _blend + dt / _blendDur);
                float k = _blend * _blend * (3 - 2 * _blend);
                target = Pose.Lerp(_from, target, k);
            }
            _last = target;
            Rig.Apply(Body, target, Pos, Yaw);
        }

        /// <summary>World position of the right (bowling / throwing) hand.</summary>
        public Vector3 HandPos(bool right = true) => Body[right ? B.HandR : B.HandL].position + Body[right ? B.HandR : B.HandL].rotation * new Vector3(0, -0.08f, 0.02f);

        public Vector3 HeadPos => Body[B.Head].position;
    }
}
