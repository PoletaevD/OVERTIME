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
        private RunUpgradeState _upgrades;
        private PlayerUpgradeEffects _effects;

        internal int MagazineCapacity => _magazineSize + Mathf.RoundToInt(Bonus(UpgradeId.ExtendedMagazine));
        internal int EffectiveDamage => RoundDamage(BuildDamage);
        internal float ReloadSeconds => Bonus(UpgradeId.RapidService) > 0f ? Mathf.Max(0.05f, _reloadSeconds * (1f - Bonus(UpgradeId.RapidService))) : _reloadSeconds;
        private float BuildDamage => _damage * (1f + Bonus(UpgradeId.OverclockedReceiver));

        internal int Magazine => _state.Magazine;
        internal int Reserve => _state.Reserve;
        internal float ReloadProgress => IsReloading ? 1f - (float)(_state.ReloadRemaining / _state.ReloadDuration) : 0f;
        internal bool IsReloading => _state.ReloadRemaining > 0d;
        internal event Action ShotFired;
        internal event Action AutoFed;
        internal int ProjectileCapacity => _projectileCapacity;
        internal bool HasQueuedBoost => _effects?.Charged == true;

        internal void BindUpgrades(RunUpgradeState upgrades, PlayerUpgradeEffects effects)
        {
            _upgrades = upgrades;
            _effects = effects;
        }

        internal float RoundsPerMinute(TemporalMode mode)
        {
            return mode == TemporalMode.BulletTime ? _roundsPerMinute * (1f + Bonus(UpgradeId.AcceleratedProcessing)) : _roundsPerMinute;
        }

        internal float ProjectileSpeed(TemporalMode mode)
        {
            return mode == TemporalMode.BulletTime ? _projectileSpeed * (1f + Bonus(UpgradeId.Hypervelocity)) : _projectileSpeed;
        }

        internal bool TryAutoFeed(int rounds)
        {
            if (TransferReserve(rounds) == 0)
            {
                return false;
            }

            AutoFed?.Invoke();
            return true;
        }

        private int TransferReserve(int rounds)
        {
            var transferred = Mathf.Clamp(rounds, 0, Mathf.Max(0, Mathf.Min(MagazineCapacity - Magazine, Reserve)));
            _state.Magazine += transferred;
            _state.Reserve -= transferred;
            return transferred;
        }

        private float Bonus(UpgradeId id)
        {
            return _upgrades?.Bonus(id) ?? 0f;
        }

        private int RoundDamage(float damage)
        {
            return Mathf.Max(0, Mathf.FloorToInt(damage + 0.5f));
        }

        internal void AddReserve(int rounds)
        {
            _state.Reserve += rounds;
        }

        internal void ResetState()
        {
            _state = new Snapshot
            {
                Magazine = MagazineCapacity,
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

        internal void Simulate(double step, bool fire, bool reload, bool canShoot, Ray aim, Vector3 aimUp, TemporalProjectileEmitter projectiles, TemporalMode mode = TemporalMode.Normal)
        {
            const double tolerance = 0.0000001d;
            var cooling = _state.Cooldown > 0d;
            _state.Cooldown -= step;
            if (IsReloading)
            {
                _state.ReloadRemaining -= step;
                if (_state.ReloadRemaining <= tolerance)
                {
                    TransferReserve(Reserve);
                    _state.ReloadRemaining = 0d;
                }
            }
            else if (reload && Magazine < MagazineCapacity && Reserve > 0)
            {
                _state.ReloadDuration = ReloadSeconds;
                _state.ReloadRemaining = _state.ReloadDuration;
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

            var damage = RoundDamage(_effects?.ModifyShotDamage(BuildDamage) ?? BuildDamage);
            var hypervelocity = mode == TemporalMode.BulletTime && Bonus(UpgradeId.Hypervelocity) > 0f;
            Physics.SyncTransforms();
            if (!projectiles.TryLaunchPlayer(aim, aimUp, _launchOffset, ProjectileSpeed(mode), _projectileLifetime, _range, _projectileRadius, damage, _hitLayers, _effects?.Charged == true, hypervelocity))
            {
                _state.Cooldown = 0d;
                return;
            }

            var interval = 60d / RoundsPerMinute(mode);
            _state.Cooldown = interval + (cooling ? _state.Cooldown : 0d);
            _state.Magazine--;
            _effects?.ConfirmShot();
            ShotFired?.Invoke();
        }

        internal struct Snapshot
        {
            internal int Magazine;
            internal int Reserve;
            internal double Cooldown;
            internal double ReloadRemaining;
            internal float ReloadDuration;
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
