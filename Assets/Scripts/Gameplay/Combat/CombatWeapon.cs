using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatWeapon : MonoBehaviour
    {
        [SerializeField, Range(1f, 7200f)] private float _roundsPerMinute = 600f;
        [SerializeField, Min(1)] private int _magazineSize = 30;
        [SerializeField, Min(0)] private int _startingReserve = 180;
        [SerializeField, Min(1)] private int _damage = 12;
        [SerializeField, Min(0.01f)] private float _reloadSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float _range = 80f;
        [SerializeField] private LayerMask _hitLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 45f;
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.045f;
        [SerializeField, Min(0.01f)] private float _projectileLifetime = 1.8f;
        [SerializeField, Min(1)] private int _projectileCapacity = 48;
        [SerializeField] private Vector3 _launchOffset = new(0.31f, -0.23f, 0.99f);

        private Snapshot _state;

        internal int Magazine => _state.Magazine;
        internal int Reserve => _state.Reserve;
        internal float ReloadProgress => IsReloading ? 1f - (float)(_state.ReloadRemaining / _reloadSeconds) : 0f;
        internal bool IsReloading => _state.ReloadRemaining > 0d;
        internal event Action ShotFired;
        internal int ProjectileCapacity => _projectileCapacity;

        internal void AddReserve(int rounds)
        {
            _state.Reserve += rounds;
        }

        internal void ResetState()
        {
            _state = new Snapshot
            {
                Magazine = _magazineSize,
                Reserve = _startingReserve
            };
        }

        internal Snapshot Capture()
        {
            return _state;
        }

        internal void Restore(in Snapshot snapshot)
        {
            _state = snapshot;
        }

        internal void Simulate(double step, bool fire, bool reload, bool canShoot, Ray aim, Vector3 aimUp, TemporalProjectileEmitter projectiles)
        {
            const double tolerance = 0.0000001d;
            var cooling = _state.Cooldown > 0d;
            _state.Cooldown -= step;
            if (IsReloading)
            {
                _state.ReloadRemaining -= step;
                if (_state.ReloadRemaining <= tolerance)
                {
                    var transferred = Mathf.Min(_magazineSize - Magazine, Reserve);
                    _state.Magazine += transferred;
                    _state.Reserve -= transferred;
                    _state.ReloadRemaining = 0d;
                }
            }
            else if (reload && Magazine < _magazineSize && Reserve > 0)
            {
                _state.ReloadRemaining = _reloadSeconds;
            }

            if (!canShoot || !fire || IsReloading || Magazine <= 0)
            {
                _state.Cooldown = Math.Max(0d, _state.Cooldown);
                return;
            }

            if (_state.Cooldown > tolerance)
            {
                return;
            }

            Physics.SyncTransforms();
            if (!projectiles.TryLaunchPlayer(aim, aimUp, _launchOffset, _projectileSpeed, _projectileLifetime, _range, _projectileRadius, _damage, _hitLayers))
            {
                _state.Cooldown = 0d;
                return;
            }

            var interval = 60d / _roundsPerMinute;
            _state.Cooldown = interval + (cooling ? _state.Cooldown : 0d);
            _state.Magazine--;
            ShotFired?.Invoke();
        }

        internal struct Snapshot
        {
            internal int Magazine;
            internal int Reserve;
            internal double Cooldown;
            internal double ReloadRemaining;
        }
    }

    internal readonly struct ProjectileImpact
    {
        internal readonly Vector3 Point;
        internal readonly Vector3 Normal;
        internal readonly DamageResult Damage;
        internal readonly bool WeakPoint;
        internal readonly bool Shielded;
        internal readonly bool FromPlayer;

        internal ProjectileImpact(Vector3 point, Vector3 normal, DamageResult damage, bool weakPoint, bool shielded, bool fromPlayer)
        {
            Point = point;
            Normal = normal;
            Damage = damage;
            WeakPoint = weakPoint;
            Shielded = shielded;
            FromPlayer = fromPlayer;
        }
    }
}
