using System;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>A camera shot in cricket space: position, look-at point and lens (k = focal / screen height).</summary>
    public struct Shot
    {
        public V3 Pos;
        public V3 Look;
        public double K;

        public Shot(V3 pos, V3 look, double k)
        {
            Pos = pos;
            Look = look;
            K = k;
        }

        public static Shot Lerp(Shot a, Shot b, double t) => new Shot(V3.Lerp(a.Pos, b.Pos, t), V3.Lerp(a.Look, b.Look, t), a.K + (b.K - a.K) * t);

        public void ApplyTo(Camera cam, double shake = 0, double time = 0)
        {
            var look = Look;
            if (shake > 0) look = look + new V3(Math.Sin(time * 61) * shake * 0.08, 0, Math.Cos(time * 53) * shake * 0.06);
            cam.transform.position = Pos.U;
            cam.transform.LookAt(look.U, Vector3.up);
            float vfov = (float)(2 * Math.Atan(0.5 / Math.Max(0.2, K)) * Mathf.Rad2Deg);
            // Narrow portrait-like aspect ratios get a wider lens so the action still fits.
            if (cam.aspect < 1.6f) vfov = Mathf.Min(90, vfov * 1.6f / cam.aspect);
            cam.fieldOfView = vfov;
        }
    }

    /// <summary>Broadcast camera positions used by the game.</summary>
    public static class Rigs
    {
        /// <summary>Low third-person view behind the bowler's end (user bowling / AI batting).</summary>
        public static Shot Bowling => new Shot(new V3(0, -9.2, 6.2), new V3(0, 20.5, -0.2), 1.1);

        /// <summary>Close-up of the pitch while the bowler drags the target.</summary>
        public static Shot Aim => new Shot(new V3(0, -5.5, 9.5), new V3(0, 15.6, 0), 2.15);

        /// <summary>
        /// TV broadcast view for user batting: long lens from high behind the bowler, so the
        /// batter faces the camera and the bowler runs in at the bottom of the screen.
        /// </summary>
        public static Shot Batting(bool leftHanded) =>
            new Shot(new V3(leftHanded ? -0.25 : 0.25, -16.0, 7.2), new V3(0, 16.2, 0.55), 3.4);

        /// <summary>Slow orbit for menus.</summary>
        public static Shot Orbit(double angle, double radius = 62, double height = 17) =>
            new Shot(new V3(Math.Sin(angle) * radius, Pitch.Mid - Math.Cos(angle) * radius, height), new V3(0, Pitch.Mid, 5), 1.15);
    }
}
