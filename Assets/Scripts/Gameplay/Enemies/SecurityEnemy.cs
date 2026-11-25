using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal enum SecurityEnemyKind
    {
        Guard,
        Drone,
        Auditor,
        Enforcer
    }

    internal enum SecurityEnemyState
    {
        Idle,
        Alert,
        Moving,
        Aiming,
        Attacking,
        Recovering,
        Dead
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    internal sealed class SecurityEnemy : TemporalActor<SecurityEnemy.Snapshot>, ICombatDamageable
    {
        private const float AimTolerance = 3f;

        [SerializeField] private SecurityEnemyKind _kind;
        [SerializeField] private CombatHealth _health;
        [SerializeField] private Collider _body;
        [SerializeField] private Collider _weakPoint;
        [SerializeField] private Collider _shield;
        [SerializeField] private CombatShield _shieldFeedback;
        [SerializeField, Min(1f)] private float _weakPointMultiplier = 1.5f;
        [SerializeField, Min(1)] private int _rewardId = 1;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private TemporalProjectileEmitter _projectiles;
        [SerializeField] private SecurityEnemyPresentation _presentation;
        [SerializeField] private LayerMask _geometry = 1;
        [SerializeField] private Transform[] _patrol = Array.Empty<Transform>();
        [SerializeField] private Vector3 _flightHalfExtents = new(5f, 2f, 7f);
        [SerializeField] private Vector3 _attackOrigin = new(0f, 0.35f, 0f);
        [SerializeField, Min(0.1f)] private float _moveRadius = 0.48f;
        [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float _detectionRange = 18f;
        [SerializeField, Min(0f)] private float _reactionSeconds = 0.28f;
        [SerializeField, Min(1f)] private float _yawSpeed = 320f;
        [SerializeField, Min(1f)] private float _pitchSpeed = 120f;
        [SerializeField, Min(0.1f)] private float _preferredRange = 7f;
        [SerializeField, Min(0.05f)] private float _cooldownSeconds = 1.3f;
        [SerializeField, Min(1)] private int _burstSize = 3;
        [SerializeField, Min(0.01f)] private float _burstSpacing = 0.15f;
        [SerializeField, Min(0.05f)] private float _aimSeconds = 0.9f;
        [SerializeField, Min(0.01f)] private float _lockedSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float _beamSeconds = 0.12f;
        [SerializeField, Min(1)] private int _damage = 8;
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 16f;
        [SerializeField, Min(0.05f)] private float _projectileLifetime = 2.2f;
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.13f;

        private SecurityEnemy[] _neighbors;
        private bool _encounterReady = true;
        private bool _stagedEncounter;
        private AiState _state;
        private Vector3 _home;
        private Quaternion _homeRotation;

        internal SecurityEnemyKind Kind => _kind;
        internal bool EncounterReady => _encounterReady;
        internal float InstabilityRemaining => _state.InstabilityRemaining;
        internal float ChillRemaining => _state.ChillRemaining;

        internal bool BlockKnife(Vector3 origin, bool reducedMotion)
        {
            if (_shieldFeedback == null || _shield == null || !_shield.enabled)
            {
                return false;
            }

            var direction = Vector3.ProjectOnPlane(origin - transform.position, transform.up).normalized;
            if (Vector3.Dot(transform.forward, direction) < 0.55f)
            {
                return false;
            }

            _shieldFeedback.React(reducedMotion);
            return true;
        }

        internal void ApplyChill(float seconds)
        {
            _state.ChillRemaining = Mathf.Max(_state.ChillRemaining, seconds);
        }

        internal void ApplyInstability(float magnitude, float seconds)
        {
            if (_encounterReady && _health.Alive)
            {
                _state.InstabilityRemaining = seconds;
                _state.InstabilityMagnitude = magnitude;
            }
        }
        internal CombatHealth Health => _health;
        internal Collider Body => _body;
        internal SecurityEnemyState State => _state.State;
        internal float PhaseRemaining => _state.PhaseRemaining;
        internal float Cooldown => _state.Cooldown;
        internal bool SeesPlayer => _state.SeesPlayer;
        internal bool DirectionLocked => _state.DirectionLocked;
        internal uint Attacks => _state.Attacks;
        internal Vector3 Aim => _state.Aim;
        internal Vector3 AttackOrigin => transform.TransformPoint(_attackOrigin);

        CombatHealth ICombatDamageable.Health => _health;
        int ICombatDamageable.RewardId => _rewardId;

        internal override void Initialize(int capacity)
        {
            _home = transform.position;
            _homeRotation = transform.rotation;
            base.Initialize(capacity);
        }

        internal override void ResetState()
        {
            RestoreState(new Snapshot(_home, _homeRotation, _health.Maximum, new AiState
            {
                Aim = _homeRotation * Vector3.forward,
                Cooldown = _stagedEncounter ? 0.10f + (_rewardId * 37 % 19) * 0.048f : 0f,
                SightRemaining = _stagedEncounter ? (_rewardId * 11 % 7) * 0.009f : 0f
            }));
        }

        internal void BindEncounter(TemporalDirector director, TemporalProjectileEmitter projectiles, int rewardId, Transform[] patrol, Vector3 flightHalfExtents)
        {
            _director = director;
            _projectiles = projectiles;
            _rewardId = rewardId;
            _stagedEncounter = true;
            _patrol = patrol;
            _flightHalfExtents = flightHalfExtents;
        }

        internal void SetEncounterReady(bool ready)
        {
            if (_encounterReady == ready)
            {
                return;
            }

            _encounterReady = ready;
            SetHitboxes(ready && _health.Alive);
            if (_stagedEncounter)
            {
                _presentation.SetDeployed(ready);
            }
        }

        internal bool HasClearDeployment(Vector3 player)
        {
            if ((transform.position - player).sqrMagnitude < 2.3f * 2.3f)
            {
                return false;
            }

            var body = (BoxCollider)_body;
            var box = body.transform;
            var extent = body.size * 0.5f - Vector3.one * 0.026f;
            return !Physics.CheckBox(box.TransformPoint(body.center), extent, box.rotation, _geometry, QueryTriggerInteraction.Ignore);
        }

        internal void SetNeighbors(SecurityEnemy[] neighbors)
        {
            _neighbors = neighbors;
        }

        internal override void CollectColliders(List<Collider> colliders)
        {
            colliders.Add(_body);
            if (_weakPoint != null)
            {
                colliders.Add(_weakPoint);
            }

            if (_shield != null)
            {
                colliders.Add(_shield);
            }
        }

        DamageResult ICombatDamageable.Hit(Collider collider, float amount, bool reducedMotion, out bool weakPoint)
        {
            weakPoint = _weakPoint != null && collider == _weakPoint;
            if ((_stagedEncounter && !_encounterReady) || (!weakPoint && collider != _body))
            {
                return default;
            }

            var vulnerability = _state.InstabilityRemaining > 0f ? 1f + _state.InstabilityMagnitude : 1f;
            var result = _health.ApplyDamage(Mathf.RoundToInt(amount * vulnerability * (weakPoint ? _weakPointMultiplier : 1f)));
            if (result.Amount == 0)
            {
                return result;
            }

            if (result.Killed)
            {
                _state.State = SecurityEnemyState.Dead;
                _state.DeathTime = 0f;
                _state.DirectionLocked = false;
                _state.SeesPlayer = false;
                _state.Recoil = 0f;
                SetHitboxes(false);
            }

            _presentation.ReactToHit(reducedMotion);
            return result;
        }

        internal override void Simulate(float step)
        {
            if (!_health.Alive)
            {
                _state.DeathTime = Mathf.Min(1.3f, _state.DeathTime + step);
                return;
            }

            if (!_encounterReady)
            {
                return;
            }

            _state.InstabilityRemaining = Mathf.Max(0f, _state.InstabilityRemaining - step);
            var chilled = _state.ChillRemaining > 0f;
            _state.ChillRemaining = Mathf.Max(0f, _state.ChillRemaining - step);
            if (chilled)
            {
                step *= 0.52f;
            }

            _state.PhaseRemaining = Mathf.Max(0f, _state.PhaseRemaining - step);
            _state.Cooldown = Mathf.Max(0f, _state.Cooldown - step);
            _state.Recoil = Mathf.MoveTowards(_state.Recoil, 0f, 7f * step);

            var capsule = _director.PlayerCapsule;
            var target = capsule.transform.TransformPoint(capsule.center);
            var toPlayer = target - AttackOrigin;
            var sawPlayer = _state.SeesPlayer;
            _state.SightRemaining -= step;
            if (_state.SightRemaining <= 0f)
            {
                _state.SightRemaining = _stagedEncounter ? 0.052f : 0f;
                var range = _stagedEncounter ? _detectionRange * 1.35f : _detectionRange;
                _state.SeesPlayer = _director.HasLivingPlayer && toPlayer.sqrMagnitude <= range * range && !Physics.Linecast(AttackOrigin, target, _geometry, QueryTriggerInteraction.Ignore);
            }

            if (_kind == SecurityEnemyKind.Auditor)
            {
                SimulateAuditor(toPlayer, step);
                return;
            }

            SimulateMobile(target, toPlayer, sawPlayer, step);
        }

        private void SimulateMobile(Vector3 target, Vector3 toPlayer, bool sawPlayer, float step)
        {
            if (!_state.SeesPlayer)
            {
                _state.ShotsRemaining = 0;
                _state.State = sawPlayer || _state.State == SecurityEnemyState.Moving ? SecurityEnemyState.Moving : SecurityEnemyState.Idle;
                var heading = MoveOnRoute(step);
                if (heading.sqrMagnitude > 0.0001f)
                {
                    TurnToward(heading, step);
                }

                return;
            }

            TurnToward(toPlayer, step);
            if (_kind == SecurityEnemyKind.Enforcer)
            {
                AdvanceEnforcer(toPlayer, step);
                toPlayer = target - AttackOrigin;
            }

            if (!sawPlayer)
            {
                _state.State = SecurityEnemyState.Alert;
                _state.PhaseRemaining = _stagedEncounter ? _reactionSeconds * (0.52f + (_rewardId % 5) * 0.085f) : _reactionSeconds;
            }

            if (_state.State == SecurityEnemyState.Alert && _state.PhaseRemaining > 0f)
            {
                return;
            }

            if (_state.Cooldown > 0f)
            {
                _state.State = SecurityEnemyState.Recovering;
                if (_kind == SecurityEnemyKind.Guard)
                {
                    if (_stagedEncounter)
                    {
                        RepositionGuard(toPlayer, step);
                    }
                    else
                    {
                        MoveOnRoute(step);
                    }
                }
                else if (_kind == SecurityEnemyKind.Drone)
                {
                    MoveDrone(target, step);
                }

                return;
            }

            if (_state.ShotsRemaining == 0)
            {
                _state.State = SecurityEnemyState.Aiming;
                if (_kind == SecurityEnemyKind.Drone)
                {
                    MoveDrone(target, step);
                    toPlayer = target - AttackOrigin;
                }

                if (Vector3.Angle(_state.Aim, toPlayer) > AimTolerance)
                {
                    return;
                }

                _state.ShotsRemaining = _burstSize;
            }

            _state.State = SecurityEnemyState.Attacking;
            if (_state.PhaseRemaining > 0f || Vector3.Angle(_state.Aim, toPlayer) > AimTolerance || Physics.Raycast(AttackOrigin, _state.Aim, toPlayer.magnitude, _geometry, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var origin = AttackOrigin;
            if (_kind == SecurityEnemyKind.Enforcer)
            {
                if (Vector3.Dot(transform.forward, _state.Aim) <= 0.2f)
                {
                    return;
                }

                origin += _state.Aim * _projectileRadius;
                if (Physics.Linecast(AttackOrigin, origin, _geometry, QueryTriggerInteraction.Ignore))
                {
                    return;
                }
            }

            if (_projectiles.TryLaunch(origin, _state.Aim, _projectileSpeed, _projectileLifetime, _projectileRadius, _damage, _kind == SecurityEnemyKind.Drone, transform))
            {
                _state.Attacks++;
                _state.Recoil = 1f;
                _state.ShotsRemaining--;
                _state.PhaseRemaining = _burstSpacing;
            }

            if (_state.ShotsRemaining == 0)
            {
                _state.Cooldown = _cooldownSeconds;
            }
        }

        private void AdvanceEnforcer(Vector3 toPlayer, float step)
        {
            var horizontal = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
            if (horizontal.sqrMagnitude <= _preferredRange * _preferredRange)
            {
                return;
            }

            var direction = horizontal.normalized;
            if (Vector3.Dot(direction, transform.forward) <= 0.5f || TryMove(direction, step))
            {
                return;
            }

            var sidestep = Vector3.Cross(Vector3.up, direction);
            if (!TryMove((direction + sidestep * 1.5f).normalized, step))
            {
                TryMove((direction - sidestep * 1.5f).normalized, step);
            }
        }

        private void SimulateAuditor(Vector3 toPlayer, float step)
        {
            if (_state.State == SecurityEnemyState.Attacking && _state.PhaseRemaining > 0f)
            {
                return;
            }

            if (_state.Cooldown > 0f)
            {
                _state.State = SecurityEnemyState.Recovering;
                _state.DirectionLocked = false;
                if (_state.SeesPlayer)
                {
                    TurnToward(toPlayer, step);
                }

                return;
            }

            if (!_state.DirectionLocked && !_state.SeesPlayer)
            {
                _state.State = SecurityEnemyState.Idle;
                return;
            }

            var lockedTime = Mathf.Min(_lockedSeconds, _aimSeconds);
            if (_state.State != SecurityEnemyState.Aiming)
            {
                _state.State = SecurityEnemyState.Aiming;
                _state.PhaseRemaining = _aimSeconds;
                _director.StartAuditorCharge(AttackOrigin);
            }

            if (!_state.DirectionLocked)
            {
                TurnToward(toPlayer, step);
                if (_state.PhaseRemaining <= lockedTime && Vector3.Angle(_state.Aim, toPlayer) <= AimTolerance)
                {
                    _state.DirectionLocked = true;
                }

                if (!_encounterReady)
            {
                return;
            }

            _state.PhaseRemaining = Mathf.Max(_state.PhaseRemaining, lockedTime);
                _state.BeamDistance = BeamDistance(AttackOrigin, false, out _);
                return;
            }

            if (_state.PhaseRemaining <= 0f)
            {
                var origin = AttackOrigin;
                _state.BeamDistance = BeamDistance(origin, _director.CanDamagePlayer, out var hitsPlayer);
                _state.State = SecurityEnemyState.Attacking;
                _state.PhaseRemaining = _beamSeconds;
                _state.Cooldown = _cooldownSeconds;
                _state.Recoil = 1f;
                _state.Attacks++;
                if (hitsPlayer)
                {
                    _director.DamagePlayer(_damage, origin);
                }
            }
            else
            {
                _state.BeamDistance = BeamDistance(AttackOrigin, false, out _);
            }
        }

        private float BeamDistance(Vector3 origin, bool includePlayer, out bool hitsPlayer)
        {
            var capsule = _director.PlayerCapsule;
            var layers = includePlayer ? _geometry.value | (1 << capsule.gameObject.layer) : _geometry.value;
            if (Physics.Raycast(origin, _state.Aim, out var hit, _detectionRange, layers, QueryTriggerInteraction.Ignore))
            {
                hitsPlayer = includePlayer && hit.collider == capsule;
                return hit.distance;
            }

            hitsPlayer = false;
            return _detectionRange;
        }

        private void TurnToward(Vector3 direction, float step)
        {
            var horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(horizontal), _yawSpeed * step);
            }

            var targetPitch = Mathf.Atan2(direction.y, horizontal.magnitude) * Mathf.Rad2Deg;
            _state.Pitch = Mathf.MoveTowards(_state.Pitch, targetPitch, _pitchSpeed * step);
            _state.Aim = transform.rotation * Quaternion.Euler(-_state.Pitch, 0f, 0f) * Vector3.forward;
        }

        private Vector3 MoveOnRoute(float step)
        {
            if (_patrol.Length == 0)
            {
                return Vector3.zero;
            }

            var delta = _patrol[_state.Waypoint].position - transform.position;
            if (_kind is SecurityEnemyKind.Guard or SecurityEnemyKind.Enforcer)
            {
                delta.y = 0f;
            }
            else
            {
                delta = LimitFlight(delta);
            }

            if (delta.sqrMagnitude < 0.04f || !TryMove(delta, step))
            {
                _state.Waypoint = (_state.Waypoint + 1) % _patrol.Length;
            }

            return delta;
        }

        private void RepositionGuard(Vector3 toTarget, float step)
        {
            toTarget.y = 0f;
            var radial = toTarget.normalized;
            var tangent = Vector3.Cross(Vector3.up, radial) * ((_rewardId + _state.Attacks / 3) % 2 == 0 ? 1f : -1f);
            var steer = radial * Mathf.Clamp((toTarget.magnitude - _preferredRange) * 0.2f, -0.65f, 0.75f);
            if (!TryMove((steer + tangent).normalized, step) && !TryMove((steer - tangent).normalized, step))
            {
                MoveOnRoute(step);
            }
        }

        private void MoveDrone(Vector3 target, float step)
        {
            var toTarget = target - transform.position;
            var distance = toTarget.magnitude;
            if (distance < 0.01f)
            {
                return;
            }

            var radial = toTarget / distance;
            var tangent = Vector3.Cross(Vector3.up, radial) * (_stagedEncounter && _rewardId % 2 == 0 ? -1f : 1f);
            var steer = radial * Mathf.Clamp((distance - _preferredRange) / 2f, -1f, 1f);
            steer.y += Mathf.Clamp(toTarget.y + 0.45f, -1f, 1f) * 0.6f;
            if (TryMove(LimitFlight((steer + tangent * 0.8f).normalized), step))
            {
                return;
            }

            if (TryMove(LimitFlight((steer - tangent).normalized), step))
            {
                return;
            }

            if (!TryMove(LimitFlight(Vector3.up), step))
            {
                TryMove(LimitFlight(Vector3.down), step);
            }
        }

        private Vector3 LimitFlight(Vector3 delta)
        {
            var candidate = transform.position + delta - _home;
            candidate.x = Mathf.Clamp(candidate.x, -_flightHalfExtents.x, _flightHalfExtents.x);
            candidate.y = Mathf.Clamp(candidate.y, -_flightHalfExtents.y, _flightHalfExtents.y);
            candidate.z = Mathf.Clamp(candidate.z, -_flightHalfExtents.z, _flightHalfExtents.z);
            return _home + candidate - transform.position;
        }

        private bool TryMove(Vector3 delta, float step)
        {
            var length = delta.magnitude;
            if (length <= 0.0001f)
            {
                return false;
            }

            var position = transform.position;
            var direction = delta / length;
            var distance = Mathf.Min(length, step * _moveSpeed);
            var halfSegment = _kind is SecurityEnemyKind.Guard or SecurityEnemyKind.Enforcer ? Vector3.up * 0.40f : Vector3.zero;
            var blocked = halfSegment == Vector3.zero
                ? Physics.SphereCast(position, _moveRadius, direction, out _, distance + 0.03f, _geometry, QueryTriggerInteraction.Ignore)
                : Physics.CapsuleCast(position - halfSegment, position + halfSegment, _moveRadius, direction, out _, distance + 0.03f, _geometry, QueryTriggerInteraction.Ignore);

            var candidate = position + direction * distance;
            if (blocked || MovesIntoNeighbor(position, candidate))
            {
                return false;
            }

            transform.position = candidate;
            return true;
        }

        private bool MovesIntoNeighbor(Vector3 position, Vector3 candidate)
        {
            if (_neighbors == null)
            {
                return false;
            }

            foreach (var neighbor in _neighbors)
            {
                if (neighbor == this || !neighbor.EncounterReady || !neighbor.Health.Alive)
                {
                    continue;
                }

                var minimum = _moveRadius + neighbor._moveRadius;
                var other = neighbor.transform.position;
                var offset = candidate - other;
                if (offset.sqrMagnitude < minimum * minimum && offset.sqrMagnitude < (position - other).sqrMagnitude)
                {
                    return true;
                }
            }

            return false;
        }

        internal override void Present(TemporalTimeline timeline, TemporalMode mode, bool showTraces)
        {
            if (_stagedEncounter && !_encounterReady)
            {
                return;
            }

            var aiming = _kind == SecurityEnemyKind.Auditor && _state.State == SecurityEnemyState.Aiming;
            var firing = _kind == SecurityEnemyKind.Auditor && _state.State == SecurityEnemyState.Attacking;
            var charge = aiming ? 1f - _state.PhaseRemaining / _aimSeconds : 0f;
            var time = (timeline.Cursor + timeline.ForwardFraction - timeline.PreviewFraction) * (float)TemporalTimeline.TickDuration;
            _presentation.Present(time, _state.Aim, AttackOrigin, _state.Recoil, _state.DeathTime, _state.State, aiming, firing, charge, _state.DirectionLocked, _state.BeamDistance, _state.InstabilityRemaining > 0f);
        }

        private protected override Snapshot CaptureState()
        {
            return new Snapshot(transform.position, transform.rotation, _health.Current, _state);
        }

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _state = snapshot.Ai;
            transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            _health.Restore(snapshot.HitPoints);
            SetHitboxes(_encounterReady && _health.Alive);
            _presentation.ClearHit();
            _shieldFeedback?.Clear();
        }

        private void SetHitboxes(bool alive)
        {
            _body.enabled = alive;
            if (_weakPoint != null)
            {
                _weakPoint.enabled = alive;
            }

            if (_shield != null)
            {
                _shield.enabled = alive;
                _shield.gameObject.SetActive(_health.Alive && (!_stagedEncounter || _encounterReady));
            }
        }

        internal readonly struct Snapshot
        {
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal readonly int HitPoints;
            internal readonly AiState Ai;

            internal Snapshot(Vector3 position, Quaternion rotation, int hitPoints, in AiState ai)
            {
                Position = position;
                Rotation = rotation;
                HitPoints = hitPoints;
                Ai = ai;
            }
        }

        internal struct AiState
        {
            internal Vector3 Aim;
            internal SecurityEnemyState State;
            internal bool SeesPlayer;
            internal bool DirectionLocked;
            internal int Waypoint;
            internal int ShotsRemaining;
            internal float PhaseRemaining;
            internal float SightRemaining;
            internal float InstabilityRemaining;
            internal float ChillRemaining;
            internal float InstabilityMagnitude;
            internal float Cooldown;
            internal float Pitch;
            internal float BeamDistance;
            internal float Recoil;
            internal float DeathTime;
            internal uint Attacks;
        }
    }
}
