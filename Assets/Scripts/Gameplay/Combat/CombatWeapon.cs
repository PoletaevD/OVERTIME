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

        private Snapshot _state;

        internal int Magazine => _state.Magazine;
        internal int Reserve => _state.Reserve;
        internal float ReloadProgress => IsReloading ? 1f - (float)(_state.ReloadRemaining / _reloadSeconds) : 0f;
        internal bool IsReloading => _state.ReloadRemaining > 0d;
        internal event Action<CombatShot> ShotFired;

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

        internal void Simulate(double step, bool fire, bool reload, bool canShoot, Ray aim, TemporalDirector director)
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

            var interval = 60d / _roundsPerMinute;
            _state.Cooldown = interval + (cooling ? _state.Cooldown : 0d);
            _state.Magazine--;
            Physics.SyncTransforms();
            var shot = new CombatShot();
            if (Physics.Raycast(aim, out var hit, _range, _hitLayers, QueryTriggerInteraction.Ignore))
            {
                shot.HasImpact = true;
                shot.Point = hit.point;
                shot.Normal = hit.normal;
                var target = hit.collider.GetComponentInParent<CombatTarget>();
                if (target != null)
                {
                    shot.Damage = director.DamageTarget(target, hit.collider, _damage, out shot.WeakPoint);
                }
            }

            ShotFired?.Invoke(shot);
        }

        internal struct Snapshot
        {
            internal int Magazine;
            internal int Reserve;
            internal double Cooldown;
            internal double ReloadRemaining;
        }
    }

    internal struct CombatShot
    {
        internal bool HasImpact;
        internal bool WeakPoint;
        internal Vector3 Point;
        internal Vector3 Normal;
        internal DamageResult Damage;
    }
}
