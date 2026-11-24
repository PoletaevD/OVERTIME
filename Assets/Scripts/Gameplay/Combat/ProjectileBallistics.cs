using UnityEngine;

namespace Overtime
{
    internal readonly struct ProjectileBallistics
    {
        internal readonly WeaponId Weapon;
        internal readonly int TargetLimit;
        internal readonly int RicochetLimit;
        internal readonly float PenetrationRetention;
        internal readonly float RicochetRetention;
        internal readonly float DistanceBonus;
        internal readonly float DistanceBonusLimit;
        internal readonly float CloseBonus;
        internal readonly float CloseDistance;

        internal ProjectileBallistics(WeaponId weapon, int targetLimit, int ricochetLimit, float penetrationRetention, float ricochetRetention, float distanceBonus, float distanceBonusLimit, float closeBonus, float closeDistance)
        {
            Weapon = weapon;
            TargetLimit = targetLimit;
            RicochetLimit = ricochetLimit;
            PenetrationRetention = penetrationRetention;
            RicochetRetention = ricochetRetention;
            DistanceBonus = distanceBonus;
            DistanceBonusLimit = distanceBonusLimit;
            CloseBonus = closeBonus;
            CloseDistance = closeDistance;
        }

        internal float DamageAtDistance(float damage, float distance)
        {
            return damage * (1f + Mathf.Min(DistanceBonus * distance, DistanceBonusLimit)) * (distance <= CloseDistance ? 1f + CloseBonus : 1f);
        }
    }

    internal struct ProjectileTargets
    {
        private int _first;
        private int _second;
        private int _third;
        private int _count;

        internal bool Contains(int id)
        {
            return (_count >= 1 && _first == id) || (_count >= 2 && _second == id) || (_count >= 3 && _third == id);
        }

        internal void Add(int id)
        {
            switch (_count)
            {
                case 0:
                    _first = id;
                    break;
                case 1:
                    _second = id;
                    break;
                case 2:
                    _third = id;
                    break;
            }

            _count++;
        }
    }
}
