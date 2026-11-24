using System;
using System.Collections.Generic;

namespace Overtime
{
    internal sealed class UpgradeSelection
    {
        private readonly RunUpgradeCatalog _catalog;
        private readonly RunUpgradeState _state;
        private readonly Random _random;
        private readonly HashSet<int> _claimed = new(4);
        private readonly RunUpgradeDefinition[] _offers = new RunUpgradeDefinition[3];
        private int _reward;

        internal UpgradeSelection(RunUpgradeCatalog catalog, RunUpgradeState state, int seed)
        {
            _catalog = catalog;
            _state = state;
            _random = new Random(seed);
        }

        internal int Count { get; private set; }

        internal RunUpgradeDefinition Offer(int index)
        {
            return _offers[index];
        }

        internal bool Open(int reward)
        {
            if (Count != 0 || _claimed.Contains(reward))
            {
                return false;
            }

            _reward = reward;
            for (var index = 0; index < _offers.Length; index++)
            {
                var definition = Draw();
                if (definition == null)
                {
                    break;
                }

                _offers[Count++] = definition;
            }

            if (Count == 0)
            {
                _claimed.Add(reward);
            }

            return Count != 0;
        }

        internal bool Confirm(int index, out RunUpgradeDefinition definition)
        {
            definition = null;
            if (index < 0 || index >= Count || !_state.Acquire(_offers[index]))
            {
                return false;
            }

            definition = _offers[index];
            _claimed.Add(_reward);
            Array.Clear(_offers, 0, Count);
            Count = 0;
            return true;
        }

        internal float CategoryWeight(UpgradeCategory category)
        {
            return _state.HasCategory(category) ? _catalog.SynergyWeight : 1f;
        }

        private RunUpgradeDefinition Draw()
        {
            Span<int> counts = stackalloc int[3];
            counts.Clear();
            foreach (var definition in _catalog.Definitions)
            {
                if (Available(definition))
                {
                    counts[(int)definition.Rarity]++;
                }
            }

            var total = 0d;
            foreach (var definition in _catalog.Definitions)
            {
                if (Available(definition))
                {
                    total += Weight(definition, counts);
                }
            }

            var value = _random.NextDouble() * total;
            RunUpgradeDefinition fallback = null;
            foreach (var definition in _catalog.Definitions)
            {
                if (!Available(definition))
                {
                    continue;
                }

                var weight = Weight(definition, counts);
                value -= weight;
                if (value < 0d)
                {
                    return definition;
                }

                if (fallback == null || weight > 0f)
                {
                    fallback = definition;
                }
            }

            return fallback;
        }

        private float Weight(RunUpgradeDefinition definition, ReadOnlySpan<int> counts)
        {
            return _catalog.RarityWeight(definition.Rarity) * CategoryWeight(definition.Category) / counts[(int)definition.Rarity];
        }

        private bool Available(RunUpgradeDefinition definition)
        {
            if (_state.Level(definition.Id) >= definition.MaxLevel)
            {
                return false;
            }

            for (var index = 0; index < Count; index++)
            {
                if (_offers[index].Id == definition.Id)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
