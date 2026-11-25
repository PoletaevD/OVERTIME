using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal enum WeaponRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    internal enum WeaponModifier
    {
        BurstModule,
        SlugConversion,
        Splitter,
        ExplosiveCore,
        RicochetChamber,
        CryoPayload,
        FanOutput,
        GyroscopicStabilizer,
        PhaseAccelerator,
        OverloadCell
    }

    internal readonly struct WeaponAffix
    {
        internal WeaponModifier Kind { get; }
        internal float Magnitude { get; }

        internal WeaponAffix(WeaponModifier kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
        }
    }

    internal sealed class WeaponInstance
    {
        private readonly WeaponAffix[] _affixes;

        internal int InstanceId { get; }
        internal WeaponDefinition Definition { get; }
        internal WeaponRarity Rarity { get; }
        internal IReadOnlyList<WeaponAffix> Affixes => _affixes;
        internal WeaponState Stored { get; set; }

        internal WeaponInstance(int instanceId, WeaponDefinition definition, WeaponRarity rarity, params WeaponAffix[] affixes)
        {
            InstanceId = instanceId;
            Definition = definition;
            Rarity = rarity;
            _affixes = (WeaponAffix[])affixes.Clone();
            Stored = new WeaponState
            {
                Magazine = definition.Tuning.Magazine
            };
        }

        internal float Modifier(WeaponModifier kind)
        {
            foreach (var affix in _affixes)
            {
                if (affix.Kind == kind)
                {
                    return affix.Magnitude;
                }
            }

            return 0f;
        }
    }

    internal sealed class WeaponLootRoll
    {
        internal WeaponInstance[] Roll(int seed, RouteNode room, WeaponDefinition[] definitions)
        {
            var random = new System.Random(unchecked(seed * 397 ^ room.Id * 7919 ^ room.Variant * 8191 ^ 0x5745504E));
            var first = random.Next(definitions.Length);
            var second = (first + random.Next(1, definitions.Length)) % definitions.Length;
            return new[]
            {
                Create(random, room.Id * 10 + 1, definitions[first], RollRarity(random, room.Depth)),
                Create(random, room.Id * 10 + 2, definitions[second], RollRarity(random, room.Depth))
            };
        }

        internal WeaponInstance Create(System.Random random, int id, WeaponDefinition definition, WeaponRarity rarity)
        {
            var candidates = new List<WeaponModifier>(10);
            foreach (WeaponModifier modifier in Enum.GetValues(typeof(WeaponModifier)))
            {
                if (Compatible(definition.Type, modifier))
                {
                    candidates.Add(modifier);
                }
            }

            var count = rarity == WeaponRarity.Common ? random.Next(2) : (int)rarity;
            var affixes = new WeaponAffix[count];
            for (var index = 0; index < count; index++)
            {
                var chosen = random.Next(candidates.Count);
                var modifier = candidates[chosen];
                candidates.RemoveAt(chosen);
                if (modifier == WeaponModifier.SlugConversion)
                {
                    candidates.Remove(WeaponModifier.FanOutput);
                }
                else if (modifier == WeaponModifier.FanOutput)
                {
                    candidates.Remove(WeaponModifier.SlugConversion);
                }
                else if (modifier == WeaponModifier.Splitter)
                {
                    candidates.Remove(WeaponModifier.ExplosiveCore);
                }
                else if (modifier == WeaponModifier.ExplosiveCore)
                {
                    candidates.Remove(WeaponModifier.Splitter);
                }

                affixes[index] = new WeaponAffix(modifier, Magnitude(modifier, (float)random.NextDouble()));
            }

            return new WeaponInstance(id, definition, rarity, affixes);
        }

        internal WeaponInstance CreateSpecified(System.Random random, int id, WeaponDefinition definition, WeaponRarity rarity, int modifiers)
        {
            var selected = new List<WeaponAffix>(3);
            foreach (WeaponModifier modifier in Enum.GetValues(typeof(WeaponModifier)))
            {
                if ((modifiers & (1 << (int)modifier)) == 0 || !Compatible(definition.Type, modifier))
                {
                    continue;
                }

                var compatible = true;
                foreach (var affix in selected)
                {
                    compatible &= CompatibleTogether(modifier, affix.Kind);
                }

                if (compatible)
                {
                    selected.Add(new WeaponAffix(modifier, Magnitude(modifier, (float)random.NextDouble())));
                }
            }

            return new WeaponInstance(id, definition, rarity, selected.ToArray());
        }

        private bool CompatibleTogether(WeaponModifier first, WeaponModifier second)
        {
            return (first, second) switch
            {
                (WeaponModifier.SlugConversion, WeaponModifier.FanOutput) or (WeaponModifier.FanOutput, WeaponModifier.SlugConversion) => false,
                (WeaponModifier.Splitter, WeaponModifier.ExplosiveCore) or (WeaponModifier.ExplosiveCore, WeaponModifier.Splitter) => false,
                _ => true
            };
        }

        internal bool Compatible(WeaponType type, WeaponModifier modifier)
        {
            return modifier switch
            {
                WeaponModifier.BurstModule => type is WeaponType.SemiAutomatic or WeaponType.Carbine,
                WeaponModifier.SlugConversion => type == WeaponType.Scatter,
                WeaponModifier.Splitter => type != WeaponType.Scatter,
                WeaponModifier.ExplosiveCore => type != WeaponType.Scatter,
                WeaponModifier.FanOutput => type != WeaponType.Charged,
                _ => true
            };
        }

        private float Magnitude(WeaponModifier modifier, float roll)
        {
            return modifier switch
            {
                WeaponModifier.BurstModule => Mathf.Lerp(0.052f, 0.075f, roll),
                WeaponModifier.SlugConversion => Mathf.Lerp(4.3f, 5.2f, roll),
                WeaponModifier.Splitter => Mathf.Lerp(4f, 7f, roll),
                WeaponModifier.ExplosiveCore => Mathf.Lerp(1.4f, 2.1f, roll),
                WeaponModifier.RicochetChamber => Mathf.Lerp(0.65f, 0.86f, roll),
                WeaponModifier.CryoPayload => Mathf.Lerp(0.9f, 1.6f, roll),
                WeaponModifier.FanOutput => Mathf.Lerp(5f, 9f, roll),
                WeaponModifier.GyroscopicStabilizer => Mathf.Lerp(0.31f, 0.52f, roll),
                WeaponModifier.PhaseAccelerator => Mathf.Lerp(0.55f, 0.95f, roll),
                _ => Mathf.Lerp(1.35f, 1.6f, roll)
            };
        }

        private WeaponRarity RollRarity(System.Random random, int depth)
        {
            var roll = random.Next(100) + depth * 3;
            return roll >= 98 ? WeaponRarity.Epic : roll >= 82 ? WeaponRarity.Rare : roll >= 48 ? WeaponRarity.Uncommon : WeaponRarity.Common;
        }
    }

    internal struct AmmoStock
    {
        private int _carbine;
        private int _shells;
        private int _rail;
        private int _pistol;

        internal int Get(WeaponId type)
        {
            return type switch
            {
                WeaponId.Compliance => _carbine,
                WeaponId.Termination => _shells,
                WeaponId.Deadline => _rail,
                WeaponId.Directive => _pistol,
                _ => 0
            };
        }

        internal void Add(WeaponId type, int amount)
        {
            switch (type)
            {
                case WeaponId.Compliance:
                    _carbine += amount;
                    break;
                case WeaponId.Termination:
                    _shells += amount;
                    break;
                case WeaponId.Deadline:
                    _rail += amount;
                    break;
                case WeaponId.Directive:
                    _pistol += amount;
                    break;
            }
        }

        internal void Reconcile(in AmmoStock latest, in AmmoStock watermark)
        {
            _carbine += latest._carbine - watermark._carbine;
            _shells += latest._shells - watermark._shells;
            _rail += latest._rail - watermark._rail;
            _pistol += latest._pistol - watermark._pistol;
        }
    }
}
