using UnityEngine;

namespace Overtime
{
    internal interface ICombatDamageable
    {
        CombatHealth Health { get; }
        int RewardId { get; }
        DamageResult Hit(Collider collider, int amount, bool reducedMotion, out bool weakPoint);
    }
}
