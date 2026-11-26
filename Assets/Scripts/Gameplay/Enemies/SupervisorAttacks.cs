using UnityEngine;

namespace Overtime
{
    internal enum SupervisorAttack
    {
        SuppressionBurst,
        SecuritySweep,
        RecordedViolation,
        Reinforcements
    }

    internal enum SupervisorAttackStage
    {
        Recovery,
        Warning,
        Active
    }

    internal enum SupervisorCue
    {
        Charge,
        Discharge,
        Phase,
        Destruction
    }

    internal sealed class SupervisorAttacks : MonoBehaviour
    {
        [SerializeField] private Transform _left;
        [SerializeField] private Transform _right;
        [SerializeField] private Transform _laser;
        [SerializeField] private float _muzzleDistance = 1.12f;
        [SerializeField, Min(1f)] private float _projectileSpeed = 19f;
        [SerializeField, Min(1)] private int _projectileDamage = 8;
        [SerializeField, Min(0.1f)] private float _laserWarning = 1.02f;
        [SerializeField, Min(0.1f)] private float _laserDuration = 1.35f;
        [SerializeField, Range(5f, 150f)] private float _sweepAngle = 68f;
        [SerializeField, Min(1f)] private float _laserRange = 38f;
        [SerializeField, Min(0.1f)] private float _laserDamageInterval = 0.60f;
        [SerializeField, Min(1)] private int _laserDamage = 13;
        [SerializeField, Min(0.1f)] private float _violationDelay = 1.25f;
        [SerializeField, Min(0.5f)] private float _violationRadius = 2.5f;
        [SerializeField, Min(1)] private int _violationDamage = 25;

        private readonly RaycastHit[] _beamHits = new RaycastHit[32];
        private TemporalDirector _director;
        private TemporalProjectileEmitter _pool;
        private Quaternion _leftRest;
        private Quaternion _rightRest;
        private Quaternion _laserRest;

        internal float ViolationRadius => _violationRadius;
        internal Vector3 BeamOrigin => _laser.position + _laser.forward * 0.36f;

        internal void Bind(TemporalDirector director, TemporalProjectileEmitter pool)
        {
            _director = director;
            _pool = pool;
            _leftRest = _left.localRotation;
            _rightRest = _right.localRotation;
            _laserRest = _laser.localRotation;
        }

        internal Snapshot Rest(int seed)
        {
            return new Snapshot
            {
                Remaining = 1.35f,
                Pattern = Mathf.Abs(seed % 3),
                Left = _leftRest,
                Right = _rightRest,
                Laser = _laserRest
            };
        }

        internal bool Advance(ref Snapshot state, SupervisorPhase phase, bool reinforcementDue, float step)
        {
            state.Remaining -= step;
            state.DamageCooldown = Mathf.Max(0f, state.DamageCooldown - step);
            if (state.Stage == SupervisorAttackStage.Recovery)
            {
                if (state.Remaining <= 0f)
                {
                    BeginAttack(ref state, reinforcementDue ? SupervisorAttack.Reinforcements : Select(phase, state.Sequence++), phase);
                }

                return false;
            }

            switch (state.Attack)
            {
                case SupervisorAttack.SuppressionBurst:
                    AdvanceBurst(ref state, phase);
                    break;
                case SupervisorAttack.SecuritySweep:
                    AdvanceSweep(ref state, phase, step);
                    break;
                case SupervisorAttack.RecordedViolation:
                    AdvanceViolation(ref state, phase);
                    break;
                case SupervisorAttack.Reinforcements when state.Remaining <= 0f:
                    Recover(ref state, phase);
                    return true;
            }

            return false;
        }

        private SupervisorAttack Select(SupervisorPhase phase, int sequence)
        {
            if (phase == SupervisorPhase.SecurityAssessment)
            {
                return sequence % 2 == 0 ? SupervisorAttack.SuppressionBurst : SupervisorAttack.SecuritySweep;
            }

            return (sequence % 4) switch
            {
                1 => SupervisorAttack.RecordedViolation,
                3 => SupervisorAttack.SecuritySweep,
                _ => SupervisorAttack.SuppressionBurst
            };
        }

        internal void BeginAttack(ref Snapshot state, SupervisorAttack attack, SupervisorPhase phase)
        {
            var target = PlayerCenter();
            state.Attack = attack;
            state.Stage = SupervisorAttackStage.Warning;
            state.Shots = 0;
            state.DamageCooldown = 0f;
            state.Point = target;
            state.Direction = (target - _laser.position).normalized;
            if (state.Direction.sqrMagnitude < 0.0001f)
            {
                state.Direction = transform.forward;
            }

            state.SweepSign = state.Sequence % 4 < 2 ? -1f : 1f;
            state.Duration = attack switch
            {
                SupervisorAttack.SecuritySweep => _laserWarning,
                SupervisorAttack.RecordedViolation => _violationDelay,
                SupervisorAttack.Reinforcements => 0.4f,
                _ => phase == SupervisorPhase.EmergencyOverride ? 0.58f : 0.76f
            };
            state.Remaining = state.Duration;

            state.Left = Aim(_left, target - _left.position);
            state.Right = Aim(_right, target - _right.position);
            if (attack == SupervisorAttack.SecuritySweep)
            {
                state.BeamLength = BeamDistance();
            }

            _director.PlaySupervisorCue(SupervisorCue.Charge, transform.position);
        }

        private void AdvanceBurst(ref Snapshot state, SupervisorPhase phase)
        {
            if (state.Remaining > 0f)
            {
                return;
            }

            state.Stage = SupervisorAttackStage.Active;
            var precise = state.Pattern == 0;
            var fan = state.Pattern == 1;
            var turret = precise || fan ? state.Sequence % 4 < 2 ? _left : _right : state.Shots % 2 == 0 ? _left : _right;
            var focus = precise ? Vector3.Lerp(state.Point, PlayerCenter(), 0.45f) : state.Point;
            var direction = (focus - turret.position).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = turret.forward;
            }

            var rotation = Aim(turret, direction);
            if (turret == _left)
            {
                state.Left = rotation;
            }
            else
            {
                state.Right = rotation;
            }

            var count = fan ? phase == SupervisorPhase.EmergencyOverride ? 7 : 5 : precise ? 1 : 3;
            for (var index = 0; index < count; index++)
            {
                var offset = (index - (count - 1) * 0.5f) * (fan ? 10f : 6f);
                var aimed = Quaternion.AngleAxis(offset, turret.up) * direction;
                _pool.TryLaunch(turret.position + direction * _muzzleDistance, aimed, _projectileSpeed, 2.8f, 0.13f, _projectileDamage, false, transform);
            }

            _director.PlaySupervisorCue(SupervisorCue.Discharge, turret.position);
            state.Shots++;
            var volleys = precise ? 5 : fan ? 2 : 4;
            if (state.Shots >= volleys + (phase == SupervisorPhase.EmergencyOverride && !fan ? 1 : 0))
            {
                state.Pattern = (state.Pattern + 1) % 3;
                Recover(ref state, phase);
                return;
            }

            state.Remaining = precise ? 0.17f : fan ? 0.42f : 0.27f;
        }

        private void AdvanceSweep(ref Snapshot state, SupervisorPhase phase, float step)
        {
            if (state.Stage == SupervisorAttackStage.Warning && state.Remaining <= 0f)
            {
                state.Stage = SupervisorAttackStage.Active;
                state.Remaining += _laserDuration;
                _director.PlaySupervisorCue(SupervisorCue.Discharge, _laser.position);
            }

            if (state.Remaining <= 0f)
            {
                Recover(ref state, phase);
                return;
            }

            var active = state.Stage == SupervisorAttackStage.Active;
            var progress = active ? Mathf.Clamp01(1f - state.Remaining / _laserDuration) : 0f;
                SetBeam(ref state, progress, step);
            if (active && state.DamageCooldown <= 0f && _director.CanDamagePlayer && BeamTouchesPlayer(state.BeamLength) && _director.DamagePlayer(_laserDamage, _laser.position))
            {
                state.DamageCooldown = _laserDamageInterval;
            }
        }

        private void SetBeam(ref Snapshot state, float progress, float step)
        {
            var direction = Quaternion.AngleAxis((progress - 0.5f) * state.SweepSign * _sweepAngle, transform.up) * state.Direction;
            var target = Rotation(direction);
            _laser.rotation = state.Stage == SupervisorAttackStage.Warning ? Quaternion.RotateTowards(_laser.rotation, target, 420f * step) : target;
            state.Laser = _laser.localRotation;
            state.BeamLength = BeamDistance();
        }

        private float BeamDistance()
        {
            var capsule = _director.PlayerCapsule;
            var count = Physics.RaycastNonAlloc(BeamOrigin, _laser.forward, _beamHits, _laserRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var length = _laserRange;
            for (var index = 0; index < count; index++)
            {
                var hit = _beamHits[index];
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(capsule.transform))
                {
                    length = Mathf.Min(length, hit.distance);
                }
            }

            return length;
        }

        private bool BeamTouchesPlayer(float length)
        {
            var capsule = _director.PlayerCapsule;
            var center = PlayerCenter();
            var offset = capsule.transform.up * (capsule.height * 0.5f - capsule.radius);
            var radius = capsule.radius + 0.10f;
            return BeamNear(center, length, radius) || BeamNear(center + offset, length, radius) || BeamNear(center - offset, length, radius);
        }

        private bool BeamNear(Vector3 point, float length, float radius)
        {
            var delta = point - BeamOrigin;
            var direction = _laser.forward;
            return (delta - direction * Mathf.Clamp(Vector3.Dot(delta, direction), 0f, length)).sqrMagnitude <= radius * radius;
        }

        private void AdvanceViolation(ref Snapshot state, SupervisorPhase phase)
        {
            if (state.Remaining > 0f)
            {
                return;
            }

            if (state.Stage == SupervisorAttackStage.Warning)
            {
                state.Stage = SupervisorAttackStage.Active;
                state.Duration = state.Remaining = 0.22f;
                var capsule = _director.PlayerCapsule;
                if (_director.CanDamagePlayer && (capsule.ClosestPoint(state.Point) - state.Point).sqrMagnitude <= _violationRadius * _violationRadius)
                {
                    _director.DamagePlayer(_violationDamage, state.Point);
                }

                _director.PlaySupervisorCue(SupervisorCue.Discharge, state.Point);
                return;
            }

            Recover(ref state, phase);
        }

        private void Recover(ref Snapshot state, SupervisorPhase phase)
        {
            state.Stage = SupervisorAttackStage.Recovery;
            state.Duration = state.Remaining = phase switch
            {
                SupervisorPhase.EmergencyOverride => 0.78f,
                SupervisorPhase.CorrectiveAction => 1.05f,
                _ => 1.28f
            };
        }

        internal void Restore(in Snapshot state)
        {
            _left.localRotation = state.Left;
            _right.localRotation = state.Right;
            _laser.localRotation = state.Laser;
        }

        internal Vector3 BeamEnd(in Snapshot state)
        {
            return BeamOrigin + _laser.forward * state.BeamLength;
        }

        private Vector3 PlayerCenter()
        {
            var capsule = _director.PlayerCapsule;
            return capsule.transform.TransformPoint(capsule.center);
        }

        private Quaternion Aim(Transform pivot, Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                pivot.rotation = Rotation(direction.normalized);
            }

            return pivot.localRotation;
        }

        private Quaternion Rotation(Vector3 direction)
        {
            var up = Mathf.Abs(Vector3.Dot(direction, transform.up)) > 0.95f ? transform.forward : transform.up;
            return Quaternion.LookRotation(direction, up);
        }

        internal struct Snapshot
        {
            internal SupervisorAttack Attack;
            internal SupervisorAttackStage Stage;
            internal float Duration;
            internal float Remaining;
            internal float DamageCooldown;
            internal int Sequence;
            internal int Pattern;
            internal int Shots;
            internal Vector3 Point;
            internal Vector3 Direction;
            internal float BeamLength;
            internal float SweepSign;
            internal Quaternion Left;
            internal Quaternion Right;
            internal Quaternion Laser;
        }
    }
}
