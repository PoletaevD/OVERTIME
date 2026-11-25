using System;
using UnityEngine;

namespace Overtime
{
    [Serializable]
    internal sealed class KnifeTuning
    {
        [SerializeField, Min(1f)] private float _damage = 45f;
        [SerializeField, Min(0.1f)] private float _reach = 2.2f;
        [SerializeField, Range(1f, 180f)] private float _angle = 100f;
        [SerializeField, Min(0.01f)] private float _windup = 0.08f;
        [SerializeField, Min(0.01f)] private float _recovery = 0.45f;

        internal float Damage => _damage;
        internal float Reach => _reach;
        internal float Angle => _angle;
        internal float Windup => _windup;
        internal float Recovery => _recovery;
        internal float Duration => _windup + _recovery;
    }

    internal sealed class KnifeAttack
    {
        private readonly KnifeTuning _tuning;
        private readonly Collider[] _contacts = new Collider[96];
        private readonly RaycastHit[] _occlusion = new RaycastHit[64];
        private Snapshot _state;

        internal event Action Started;
        internal KnifeTuning Tuning => _tuning;
        internal Snapshot State => _state;
        internal bool Active => _state.Active;
        internal float Progress => _state.Elapsed / _tuning.Duration;

        internal KnifeAttack(KnifeTuning tuning)
        {
            _tuning = tuning;
        }

        internal void Restore(in Snapshot snapshot)
        {
            _state = snapshot;
        }

        internal bool Begin(bool pressed, bool allowed)
        {
            if (!pressed || !allowed || Active)
            {
                return false;
            }

            _state = new Snapshot { Active = true };
            Started?.Invoke();
            return true;
        }

        internal void Advance(float step, bool canHit, Ray aim, TemporalDirector director, PlayerUpgradeEffects effects)
        {
            if (!Active)
            {
                return;
            }

            _state.Elapsed += step;
            if (!_state.Resolved && _state.Elapsed >= _tuning.Windup)
            {
                _state.Resolved = true;
                if (canHit)
                {
                    Strike(aim, director, effects);
                }
            }

            if (_state.Elapsed >= _tuning.Duration)
            {
                _state = default;
            }
        }

        private void Strike(Ray aim, TemporalDirector director, PlayerUpgradeEffects effects)
        {
            Physics.SyncTransforms();
            var player = director.PlayerCapsule.transform;
            var range = _tuning.Reach + (effects?.MeleeExtraReach ?? 0f);
            var damage = _tuning.Damage * (effects?.MeleeDamageMultiplier ?? 1f);
            var cosine = Mathf.Cos(_tuning.Angle * 0.5f * Mathf.Deg2Rad);
            var count = Physics.OverlapSphereNonAlloc(aim.origin, range, _contacts, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var hits = default(RoomTargetMask);
            for (var index = 0; index < count; index++)
            {
                var collider = _contacts[index];
                if (collider.GetComponentInParent<ICombatDamageable>() is not { } target || !target.Health.Alive || hits.Contains(target.RewardId))
                {
                    continue;
                }

                var point = collider.ClosestPoint(aim.origin);
                var offset = point - aim.origin;
                if (offset.sqrMagnitude <= 0.00001f)
                {
                    offset = collider.bounds.center - aim.origin;
                }

                if (offset.sqrMagnitude <= 0.00001f || Vector3.Dot(aim.direction, offset.normalized) < cosine)
                {
                    continue;
                }

                var obstacle = Closest(aim.origin, offset, player);
                if (obstacle != null && obstacle.TryGetComponent<CombatShield>(out var shield))
                {
                    shield.React(director.ReducedMotion);
                    director.PublishImpact(new ProjectileImpact(point, -offset.normalized, default, false, true, true, melee: true));
                    continue;
                }

                if (obstacle != null && obstacle.GetComponentInParent<ICombatDamageable>() != target)
                {
                    continue;
                }

                if (target is SecurityEnemy enemy && enemy.BlockKnife(aim.origin, director.ReducedMotion))
                {
                    director.PublishImpact(new ProjectileImpact(point, -offset.normalized, default, false, true, true, melee: true));
                    continue;
                }

                var hit = obstacle != null ? obstacle : collider;
                var result = director.DamageTarget(target, hit, damage, out var weak, true);
                if (result.Amount <= 0)
                {
                    continue;
                }

                hits.Add(target.RewardId);
                director.PublishImpact(new ProjectileImpact(point, -offset.normalized, result, weak, false, true, melee: true));
            }

            effects?.ResolveMelee(aim, hits, director);
        }

        private Collider Closest(Vector3 origin, Vector3 offset, Transform owner)
        {
            var distance = offset.magnitude + 0.025f;
            var closest = default(Collider);
            var count = Physics.RaycastNonAlloc(origin, offset.normalized, _occlusion, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == _occlusion.Length)
            {
                return owner.GetComponent<CapsuleCollider>();
            }

            for (var index = 0; index < count; index++)
            {
                ref var hit = ref _occlusion[index];
                if (!hit.collider.transform.IsChildOf(owner) && hit.distance <= distance)
                {
                    closest = hit.collider;
                    distance = hit.distance;
                }
            }

            return closest;
        }

        internal struct Snapshot
        {
            internal bool Active;
            internal bool Resolved;
            internal float Elapsed;
        }
    }

    internal struct RoomTargetMask
    {
        // Arena reward IDs reserve their last two decimal digits for the authored spawn marker.
        private ulong _low;
        private ulong _high;

        internal bool Contains(int id)
        {
            var slot = Mathf.Abs(id % 100);
            return ((slot < 64 ? _low : _high) & (1UL << (slot & 63))) != 0;
        }

        internal void Add(int id)
        {
            var slot = Mathf.Abs(id % 100);
            var bit = 1UL << (slot & 63);
            if (slot < 64)
            {
                _low |= bit;
            }
            else
            {
                _high |= bit;
            }
        }
    }
}
