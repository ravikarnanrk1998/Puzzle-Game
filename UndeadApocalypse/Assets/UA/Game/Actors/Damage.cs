using UA.Core;
using UnityEngine;

namespace UA.Game
{
    public enum HitZone { Body, Head, Limb, WeakPoint }

    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point, Dir;
        public HitZone Zone;
        public bool Crit, Explosive, Fire, Melee, FromPlayer;
        public float ArmorPierce, Stagger, HeadMul;
        public WeaponClass Class;
        public int WeaponId;
        public Transform Bone;
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo d);
    }

    /// <summary>Collider part of a damageable (zombie bone, nest, barrel, boss weak point).</summary>
    public sealed class Hitbox : MonoBehaviour
    {
        public HitZone Zone;
        public IDamageable Owner;
        public Transform Bone;
    }

    /// <summary>Explosion damage helper shared by grenades, launchers, barrels and bloaters.</summary>
    public static class Blast
    {
        static readonly Collider[] Hits = new Collider[96];
        static readonly System.Collections.Generic.HashSet<IDamageable> Done = new System.Collections.Generic.HashSet<IDamageable>();

        public static int Explode(Vector3 p, float radius, float damage, bool fromPlayer, bool toxic = false, float playerDamage = -1)
        {
            Fx.Explosion(p, radius, toxic);
            Sfx.Explosion(p, radius);
            CameraShake.Add(Mathf.Clamp01(1.4f - (PlayerRig.Pos - p).magnitude / (radius * 4)) * 0.9f);
            int kills = 0;
            Done.Clear();
            int n = Physics.OverlapSphereNonAlloc(p, radius, Hits, (1 << Layers.Enemy) | (1 << Layers.World) | (1 << Layers.Default), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hb = Hits[i].GetComponent<Hitbox>();
                IDamageable target = hb != null ? hb.Owner : Hits[i].GetComponent<IDamageable>();
                if (target == null || !target.IsAlive || Done.Contains(target)) continue;
                Done.Add(target);
                var c = Hits[i].bounds.center;
                float d = (c - p).magnitude;
                float fall = Mathf.Clamp01(1 - d / radius);
                // Line of sight check so walls block blasts.
                if (Physics.Linecast(p + Vector3.up * 0.3f, c, out var wall, Layers.WorldMask, QueryTriggerInteraction.Ignore) && wall.collider != Hits[i]) fall *= 0.25f;
                bool wasAlive = target.IsAlive;
                target.TakeDamage(new DamageInfo
                {
                    Amount = damage * (0.35f + 0.65f * fall),
                    Point = c,
                    Dir = (c - p).normalized,
                    Zone = HitZone.Body,
                    Explosive = true,
                    ArmorPierce = 0.6f,
                    Stagger = 1.5f * fall,
                    FromPlayer = fromPlayer,
                    Class = WeaponClass.Special,
                });
                if (wasAlive && !target.IsAlive) kills++;
            }
            // Player.
            float pd = playerDamage >= 0 ? playerDamage : damage * (fromPlayer ? 0.35f : 1f);
            float dist = (PlayerRig.Pos + Vector3.up - p).magnitude;
            if (dist < radius && PlayerRig.Instance != null)
            {
                bool blocked = Physics.Linecast(p + Vector3.up * 0.3f, PlayerRig.Pos + Vector3.up, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                float k = Mathf.Clamp01(1 - dist / radius) * (blocked ? 0.3f : 1);
                if (k > 0.02f) PlayerRig.Instance.Hurt(pd * k, p, toxic ? HurtKind.Toxic : HurtKind.Explosion);
            }
            return kills;
        }
    }
}
