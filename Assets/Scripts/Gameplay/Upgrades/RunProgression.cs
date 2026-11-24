using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class RunProgression : MonoBehaviour
    {
        [SerializeField] private RunUpgradeCatalog _catalog;
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private int _seed;
        [SerializeField] private LayerMask _blastObstacles = 1;

        private TemporalDirector _director;
        private PlayerUpgradeEffects _playerEffects;

        internal RunUpgradeState Build { get; private set; }
        internal UpgradeSelection Selection { get; private set; }
        internal int Seed { get; private set; }
        internal PlayerUpgradeEffects PlayerEffects => _playerEffects;
        internal RecallEchoPresentation.Snapshot Echo { get; private set; }

        internal event Action<RunUpgradeDefinition> Acquired;

        internal void Initialize(TemporalDirector director, PlaygroundPlayer player)
        {
            _director = director;
            Seed = _useFixedSeed ? _seed : Guid.NewGuid().GetHashCode();
            Build = new RunUpgradeState();
            Selection = new UpgradeSelection(_catalog, Build, Seed);
            _playerEffects = new PlayerUpgradeEffects(Build, player.Motor, player.Combat.Weapon);
            player.Motor.BindUpgrades(Build);
            player.Combat.BindUpgrades(Build, _playerEffects);
            _director.Energy.BindUpgrades(Build);
            _director.AbilityCompleted += OnAbilityCompleted;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<UpgradeDebugHud>().Initialize(GetComponent<SectorRoute>(), director, player);
#endif
        }

        internal bool Confirm(int index)
        {
            if (!Selection.Confirm(index, out var definition))
            {
                return false;
            }

            ApplyAcquisition(definition);
            return true;
        }

        internal void SimulateWorld(float step)
        {
            Echo = Echo.Advance(step);
        }

        internal void RestoreWorld(in RecallEchoPresentation.Snapshot echo)
        {
            Echo = echo;
        }

        private void OnDestroy()
        {
            if (_director != null)
            {
                _director.AbilityCompleted -= OnAbilityCompleted;
            }

            _playerEffects?.Unbind();
        }

        private void ApplyAcquisition(RunUpgradeDefinition definition)
        {
            if (definition.Id == UpgradeId.ExtendedBattery)
            {
                _director.Energy.AddEnergy(definition.Magnitude);
            }

            Acquired?.Invoke(definition);
        }

        private void OnAbilityCompleted(TemporalMode mode, Vector3 origin)
        {
            if (_playerEffects.Complete(mode))
            {
                _director.InvalidatePlayerFrame();
            }
            if (mode == TemporalMode.Override && Build.Definition(UpgradeId.ResidualMemory) is { } residual)
            {
                _director.InvalidateWorldFrame();
                foreach (var enemy in _director.CurrentEnemies)
                {
                    enemy.ApplyInstability(Build.Bonus(UpgradeId.ResidualMemory), residual.Seconds);
                }
            }
            else if (mode == TemporalMode.Recall && Build.Definition(UpgradeId.BadTake) is { } badTake)
            {
                Detonate(origin, badTake);
            }
        }

        private void Detonate(Vector3 origin, RunUpgradeDefinition definition)
        {
            var radius = definition.Radius;
            var damage = Mathf.RoundToInt(Build.Bonus(UpgradeId.BadTake));
            _director.InvalidateWorldFrame();
            Echo = new RecallEchoPresentation.Snapshot(origin, radius, RecallEchoPresentation.Duration);
            foreach (var enemy in _director.CurrentEnemies)
            {
                if (!enemy.EncounterReady || !enemy.Health.Alive)
                {
                    continue;
                }

                var target = enemy.Body.ClosestPoint(origin);
                if ((target - origin).sqrMagnitude <= radius * radius && !Physics.Linecast(origin, target, _blastObstacles, QueryTriggerInteraction.Ignore))
                {
                    _director.DamageTarget(enemy, enemy.Body, damage, out _);
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal RunUpgradeDefinition[] DebugDefinitions => _catalog.Definitions;

        internal bool DebugGrant(RunUpgradeDefinition definition)
        {
            if (Selection.Count > 0 || !_director.HasLivingPlayer || !Build.Acquire(definition))
            {
                return false;
            }

            ApplyAcquisition(definition);
            _director.SealHistory();
            return true;
        }
#endif
    }
}
