using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalCollision
    {
        private readonly CapsuleCollider _player;
        private readonly Entry[] _entries;

        internal int OverlapCount { get; private set; }
        internal int PhasedCount { get; private set; }

        internal TemporalCollision(CapsuleCollider player, TemporalActor[] actors)
        {
            _player = player;
            var colliders = new List<Collider>();
            foreach (var actor in actors)
            {
                actor.CollectColliders(colliders);
            }

            _entries = new Entry[colliders.Count];
            for (var index = 0; index < _entries.Length; index++)
            {
                _entries[index].Collider = colliders[index];
            }
        }

        internal void Refresh(bool worldReversing)
        {
            OverlapCount = 0;
            PhasedCount = 0;
            var playerTransform = _player.transform;

            for (var index = 0; index < _entries.Length; index++)
            {
                ref var entry = ref _entries[index];
                var other = entry.Collider;
                if (other == null || !other.enabled || !other.gameObject.activeInHierarchy)
                {
                    entry.Phased = false;
                    continue;
                }

                var otherTransform = other.transform;
                var overlapping = Physics.ComputePenetration(_player, playerTransform.position, playerTransform.rotation, other, otherTransform.position, otherTransform.rotation, out _, out var depth) && depth > 0f;
                // Keep only the interpenetrating pair phased after a rewind. The player can
                // leave along any free route, and the pair becomes solid as soon as it clears.
                entry.Phased = worldReversing || overlapping;
                OverlapCount += overlapping ? 1 : 0;
                PhasedCount += entry.Phased ? 1 : 0;

                if (!_player.isTrigger && _player.enabled && Physics.GetIgnoreCollision(_player, other) != entry.Phased)
                {
                    Physics.IgnoreCollision(_player, other, entry.Phased);
                }
            }
        }

        internal bool Ignores(Collider collider)
        {
            if (PhasedCount > 0)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Phased && entry.Collider == collider)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private struct Entry
        {
            internal Collider Collider;
            internal bool Phased;
        }
    }
}
