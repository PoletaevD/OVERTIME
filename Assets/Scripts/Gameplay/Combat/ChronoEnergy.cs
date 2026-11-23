using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal sealed class ChronoEnergy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maximum = 100f;
        [SerializeField, Min(0.01f)] private float _resetCost = 18f;
        [SerializeField, Min(0.01f)] private float _overrideCost = 20f;
        [SerializeField, Min(0.01f)] private float _recallCost = 26f;
        [SerializeField, Min(0.01f)] private float _bulletTimeCost = 16f;
        [SerializeField, Min(0f)] private float _targetReward = 20f;

        private readonly HashSet<int> _paidTargets = new(8);
        private double _current;

        internal float Current => (float)_current;
        internal float Maximum => _maximum;
        internal bool Empty => _current <= 0d;

        internal void ResetRun()
        {
            _paidTargets.Clear();
            Recharge();
        }

        internal void Recharge()
        {
            _current = Maximum;
        }

        internal void Reward(int id)
        {
            if (id > 0 && _paidTargets.Add(id))
            {
                _current = Math.Min(Maximum, _current + _targetReward);
            }
        }

        internal double Affordable(double cost)
        {
            return cost > 0f ? _current / cost : double.PositiveInfinity;
        }

        internal void Spend(double amount)
        {
            _current = Math.Max(0d, _current - amount);
            if (_current < 0.0000001d)
            {
                _current = 0d;
            }
        }

        internal float Cost(TemporalMode mode)
        {
            return mode switch
            {
                TemporalMode.Reset => _resetCost,
                TemporalMode.Override => _overrideCost,
                TemporalMode.Recall => _recallCost,
                TemporalMode.BulletTime => _bulletTimeCost,
                _ => 0f
            };
        }
    }
}
