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

        internal CombatHealth Health => _health;
        internal CombatWeapon Weapon => _weapon;
        internal bool Alive => _health.Alive;
        internal bool GameOver { get; private set; }
        internal double RecoveryRemaining => _recoveryRemaining;

        internal bool TakeDamage(int amount)
        {
            if (GameOver)
            {
                return false;
            }

            var result = _health.ApplyDamage(amount);
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
            return new Snapshot(_health.Current, _weapon.Capture());
        }

        internal void Restore(in Snapshot snapshot)
        {
            _health.Restore(snapshot.HitPoints);
            _weapon.Restore(snapshot.Weapon);
            if (Alive)
            {
                _recoveryRemaining = 0d;
            }
        }

        internal readonly struct Snapshot
        {
            internal readonly int HitPoints;
            internal readonly CombatWeapon.Snapshot Weapon;

            internal Snapshot(int hitPoints, CombatWeapon.Snapshot weapon)
            {
                HitPoints = hitPoints;
                Weapon = weapon;
            }
        }
    }
}
