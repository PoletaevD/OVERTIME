using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal enum SupervisorPhase
    {
        SecurityAssessment,
        CorrectiveAction,
        EmergencyOverride
    }

    [RequireComponent(typeof(CombatHealth), typeof(SupervisorAttacks), typeof(SupervisorPresentation))]
    internal sealed class SupervisorBoss : TemporalActor<SupervisorBoss.Snapshot>, ICombatDamageable
    {
        internal const float CorrectiveThreshold = 0.65f;
        internal const float EmergencyThreshold = 0.30f;

        [SerializeField] private CombatHealth _health;
        [SerializeField] private SupervisorAttacks _attacks;
        [SerializeField] private SupervisorPresentation _presentation;
        [SerializeField] private Collider _body;
        [SerializeField] private Collider[] _armor;
        [SerializeField] private Collider[] _cores;
        [SerializeField] private Collider[] _shields;
        [SerializeField] private Transform[] _panels;
        [SerializeField, Min(0f)] private float _coreMultiplier = 1.75f;
        [SerializeField, Range(0.1f, 1f)] private float _armorMultiplier = 0.7f;
        [SerializeField, Min(0f)] private float _killEnergy = 25f;
        [SerializeField, Min(1)] private int _reinforcementBatch = 4;

        private TemporalDirector _director;
        private Pose _rest;
        private int _seed;
        private int _rewardId;
        private Snapshot _state;

        CombatHealth ICombatDamageable.Health => _health;
        int ICombatDamageable.RewardId => _rewardId;
        internal CombatHealth Health => _health;
        internal Collider Body => _body;
        internal SupervisorPhase Phase => _state.Phase;
        internal Snapshot State => CaptureState();
        internal int ReinforcementLimit => _state.ReinforcementWaves * _reinforcementBatch;
        internal float KillEnergy => _killEnergy;
        internal override bool AllowsGravityAttachment => false;

        internal void Bind(TemporalDirector director, TemporalProjectileEmitter projectiles, int rewardId, int seed)
        {
            _director = director;
            _rewardId = rewardId;
            _seed = seed;
            _rest = new Pose(transform.localPosition, transform.localRotation);
            _attacks.Bind(director, projectiles);
        }

        internal override void ResetState()
        {
            var rest = new Snapshot
            {
                Health = _health.Maximum,
                Position = _rest.position,
                Rotation = _rest.rotation,
                Attack = _attacks.Rest(_seed)
            };

            RestoreState(rest);
        }

        internal void Begin()
        {
            _state.Started = true;
            _director.PlaySupervisorCue(SupervisorCue.Phase, transform.position);
        }

        internal override void CollectColliders(List<Collider> colliders)
        {
            colliders.Add(_body);
            colliders.AddRange(_armor);
            colliders.AddRange(_shields);
            colliders.AddRange(_cores);
        }

        internal override void Simulate(float step)
        {
            if (!_state.Started || !_health.Alive)
            {
                return;
            }

            var waves = (int)_state.Phase;
            var reinforcementDue = waves > _state.ReinforcementWaves;
            if (_attacks.Advance(ref _state.Attack, _state.Phase, reinforcementDue, step * (_state.Chill > 0f ? 0.75f : 1f)))
            {
                _state.ReinforcementWaves = Mathf.Min(2, _state.ReinforcementWaves + 1);
            }

            _state.Chill = Mathf.Max(0f, _state.Chill - step);
            _state.Instability = Mathf.Max(0f, _state.Instability - step);
            var open = _state.Phase == SupervisorPhase.EmergencyOverride || (_state.Phase == SupervisorPhase.CorrectiveAction && _state.Attack.Stage == SupervisorAttackStage.Recovery);
            var opening = Mathf.MoveTowards(_state.PanelOpening, open ? 1f : 0f, step * 1.85f);
            if (opening != _state.PanelOpening)
            {
                _state.PanelOpening = opening;
                _state.TopExposed = opening >= 0.65f;
                ApplyHitboxes();
            }
        }

        internal override void Present(TemporalTimeline timeline, TemporalMode mode, bool showTraces)
        {
            _presentation.Present(_state, _attacks, _health.Alive, !showTraces);
        }

        DamageResult ICombatDamageable.Hit(Collider collider, float amount, bool reducedMotion, out bool weakPoint)
        {
            weakPoint = false;
            if (!_state.Started || !_health.Alive || collider == null || !collider.enabled || collider.TryGetComponent<CombatShield>(out _))
            {
                return default;
            }

            weakPoint = System.Array.IndexOf(_cores, collider) >= 0;
            var result = _health.ApplyDamage(Mathf.FloorToInt(amount * (weakPoint ? _coreMultiplier : _armorMultiplier) * (1f + (_state.Instability > 0f ? _state.InstabilityBonus : 0f)) + 0.5f));
            if (result.Amount <= 0)
            {
                return result;
            }

            _presentation.Impact(weakPoint, reducedMotion);
            UpdatePhase(true);
            if (result.Killed)
            {
                ApplyHitboxes();
            }

            return result;
        }

        internal void ApplyChill(float duration)
        {
            _state.Chill = Mathf.Max(_state.Chill, duration);
        }

        internal void ApplyInstability(float bonus, float duration)
        {
            if (_state.Started && _health.Alive)
            {
                _state.Instability = Mathf.Max(_state.Instability, duration);
                _state.InstabilityBonus = bonus;
            }
        }

        internal Collider BlastHitbox(Vector3 origin)
        {
            var best = _body;
            var closest = (_body.ClosestPoint(origin) - origin).sqrMagnitude;
            foreach (var collider in _armor)
            {
                var distance = (collider.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < closest)
                {
                    closest = distance;
                    best = collider;
                }
            }

            foreach (var collider in _cores)
            {
                if (!collider.enabled)
                {
                    continue;
                }

                var distance = (collider.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < closest)
                {
                    closest = distance;
                    best = collider;
                }
            }

            return best;
        }

        internal void CommitDestruction(bool reduced)
        {
            _director.PlaySupervisorCue(SupervisorCue.Destruction, transform.position);
            _presentation.DestroyChassis(reduced);
        }

        private void UpdatePhase(bool feedback)
        {
            var ratio = (float)_health.Current / _health.Maximum;
            var phase = ratio <= EmergencyThreshold ? SupervisorPhase.EmergencyOverride : ratio <= CorrectiveThreshold ? SupervisorPhase.CorrectiveAction : SupervisorPhase.SecurityAssessment;
            if (_state.Phase != phase)
            {
                _state.Phase = phase;
                if (feedback && _health.Alive)
                {
                    _director.PlaySupervisorCue(SupervisorCue.Phase, transform.position);
                }
            }
        }

        private void ApplyHitboxes()
        {
            var alive = _health.Alive;
            _body.enabled = alive;
            foreach (var armor in _armor)
            {
                armor.enabled = alive;
            }

            for (var index = 0; index < _cores.Length; index++)
            {
                _cores[index].enabled = alive && (index < _cores.Length - 1 || _state.TopExposed);
            }

            for (var index = 0; index < _shields.Length; index++)
            {
                _shields[index].enabled = alive && !_state.TopExposed;
                var sign = index == 0 ? 1f : -1f;
                _panels[index].localRotation = Quaternion.Euler(0f, 0f, sign * _state.PanelOpening * 76f);
            }
        }

        private protected override Snapshot CaptureState()
        {
            var state = _state;
            state.Health = _health.Current;
            state.Position = transform.localPosition;
            state.Rotation = transform.localRotation;
            return state;
        }

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _state = snapshot;
            _health.Restore(snapshot.Health);
            transform.SetLocalPositionAndRotation(snapshot.Position, snapshot.Rotation);
            _attacks.Restore(snapshot.Attack);
            ApplyHitboxes();
            _presentation.ClearImpact();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal Snapshot DebugSnapshot(TemporalTimeline timeline)
        {
            return Read(timeline, timeline.Cursor);
        }

        internal void DebugSetHealth(int value)
        {
            _health.Restore(Mathf.Clamp(value, 1, _health.Maximum));
            UpdatePhase(false);
            _state.PanelOpening = Phase == SupervisorPhase.EmergencyOverride ? 1f : 0f;
            _state.TopExposed = _state.PanelOpening >= 0.65f;
            ApplyHitboxes();
        }

        internal void DebugForce(SupervisorAttack attack)
        {
            _attacks.Start(ref _state.Attack, attack, _state.Phase);
        }

        internal void DebugDraw()
        {
            foreach (var core in _cores)
            {
                Debug.DrawLine(core.transform.position - Vector3.up * 0.45f, core.transform.position + Vector3.up * 0.45f, core.enabled ? Color.cyan : Color.gray);
                Debug.DrawLine(core.transform.position - Vector3.right * 0.45f, core.transform.position + Vector3.right * 0.45f, core.enabled ? Color.cyan : Color.gray);
            }

            var attack = _state.Attack;
            if (attack.Stage == SupervisorAttackStage.Recovery)
            {
                return;
            }

            if (attack.Attack == SupervisorAttack.SecuritySweep)
            {
                Debug.DrawLine(_attacks.BeamOrigin, _attacks.BeamEnd(attack), Color.red);
            }
            else if (attack.Attack == SupervisorAttack.RecordedViolation)
            {
                var radius = _attacks.ViolationRadius;
                Debug.DrawLine(attack.Point - Vector3.up * radius, attack.Point + Vector3.up * radius, Color.yellow);
                Debug.DrawLine(attack.Point - Vector3.right * radius, attack.Point + Vector3.right * radius, Color.yellow);
                Debug.DrawLine(attack.Point - Vector3.forward * radius, attack.Point + Vector3.forward * radius, Color.yellow);
            }
        }
#endif

        internal struct Snapshot
        {
            internal bool Started;
            internal int Health;
            internal SupervisorPhase Phase;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float PanelOpening;
            internal bool TopExposed;
            internal int ReinforcementWaves;
            internal float Chill;
            internal float Instability;
            internal float InstabilityBonus;
            internal SupervisorAttacks.Snapshot Attack;
        }
    }
}
