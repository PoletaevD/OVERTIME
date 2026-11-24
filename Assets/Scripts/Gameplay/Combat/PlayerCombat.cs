using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private CombatHealth _health;
        [SerializeField] private CombatWeapon _weapon;
        [SerializeField, Min(0.1f)] private float _recoverySeconds = 2f;

        private double _recoveryRemaining;
        private PlayerUpgradeEffects _effects;

        internal CombatHealth Health => _health;
        internal CombatWeapon Weapon => _weapon;
        internal bool Alive => _health.Alive;
        internal bool GameOver { get; private set; }
        internal double RecoveryRemaining => _recoveryRemaining;

        internal void BindUpgrades(RunUpgradeState build, PlayerUpgradeEffects effects)
        {
            _effects = effects;
            _weapon.BindUpgrades(build, effects);
        }

        internal bool TakeDamage(int amount)
        {
            if (GameOver)
            {
                return false;
            }

            var result = _health.ApplyDamage(Mathf.RoundToInt(amount * (_effects?.IncomingDamageMultiplier ?? 1f)));
            if (result.Amount == 0)
            {
                return false;
            }

            if (result.Killed)
            {
                _recoveryRemaining = _recoverySeconds;
            }

            return true;
        }

        internal void AdvanceRecoveryClock(double elapsed)
        {
            _recoveryRemaining = Math.Max(0d, _recoveryRemaining - elapsed);
        }

        internal void EndRecovery()
        {
            GameOver = true;
        }

        internal void ResetState()
        {
            GameOver = false;
            _recoveryRemaining = 0d;
            _health.Restore(_health.Maximum);
            _weapon.ResetState();
        }

        internal Snapshot Capture()
        {
            return new Snapshot(_health.Current, _weapon.Capture(), _effects?.State ?? default);
        }

        internal void Restore(in Snapshot snapshot)
        {
            _health.Restore(snapshot.HitPoints);
            _weapon.Restore(snapshot.Weapon);
            _effects?.Restore(snapshot.Effects);
            if (Alive)
            {
                _recoveryRemaining = 0d;
            }
        }

        internal readonly struct Snapshot
        {
            internal readonly int HitPoints;
            internal readonly CombatWeapon.Snapshot Weapon;

            internal readonly PlayerUpgradeEffects.Snapshot Effects;

            internal Snapshot(int hitPoints, CombatWeapon.Snapshot weapon, PlayerUpgradeEffects.Snapshot effects)
            {
                HitPoints = hitPoints;
                Weapon = weapon;
                Effects = effects;
            }
        }
    }
}
