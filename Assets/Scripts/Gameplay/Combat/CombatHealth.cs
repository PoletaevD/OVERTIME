using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maximum = 100;

        internal int Maximum => Mathf.Max(1, _maximum);
        internal int Current { get; private set; }
        internal bool Alive => Current > 0;
        internal event Action<DamageResult> Damaged;

        internal DamageResult ApplyDamage(int amount)
        {
            if (!Alive || amount <= 0)
            {
                return default;
            }

            var applied = Mathf.Min(Current, amount);
            Current -= applied;
            var result = new DamageResult(applied, !Alive);
            Damaged?.Invoke(result);
            return result;
        }

        internal void Restore(int value)
        {
            Current = Mathf.Clamp(value, 0, Maximum);
        }
    }

    internal readonly struct DamageResult
    {
        internal readonly int Amount;
        internal readonly bool Killed;

        internal DamageResult(int amount, bool killed)
        {
            Amount = amount;
            Killed = killed;
        }
    }
}
