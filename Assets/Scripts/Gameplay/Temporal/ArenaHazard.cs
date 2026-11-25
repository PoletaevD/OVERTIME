using UnityEngine;

namespace Overtime
{
    internal enum ArenaHazardKind
    {
        SweepingLaser,
        ElectricalPanel,
        SecurityBarrier
    }

    internal enum HazardPhase
    {
        Dormant,
        Warning,
        Active,
        Recovery
    }

    internal sealed class ArenaHazard : TemporalActor<ArenaHazard.Snapshot>
    {
        [SerializeField] private ArenaHazardKind _kind;
        [SerializeField] private GameObject _warning;
        [SerializeField] private GameObject _active;
        [SerializeField] private LineRenderer _beam;
        [SerializeField] private Vector3 _halfExtents = new(2f, 0.5f, 2f);
        [SerializeField] private float _length = 12f;
        [SerializeField] private float _sweepDegrees = 115f;
        [SerializeField] private float _delay = 2f;
        [SerializeField] private float _warningSeconds = 0.8f;
        [SerializeField] private float _activeSeconds = 1.2f;
        [SerializeField] private float _recoverySeconds = 2.8f;
        [SerializeField] private float _damageInterval = 0.65f;
        [SerializeField] private int _damage = 12;

        private TemporalDirector _director;
        private EncounterController _encounter;
        private bool _selected;
        private Snapshot _state;

        internal ArenaHazardKind Kind => _kind;
        internal HazardPhase Phase => _state.Phase;
        internal float DamageCooldown => _state.DamageCooldown;

        internal void Bind(TemporalDirector director, EncounterController encounter, bool selected)
        {
            _director = director;
            _encounter = encounter;
            _selected = selected;
        }

        internal override void ResetState()
        {
            _state = new Snapshot { Remaining = _delay, Armed = _selected };
            ApplyPresentation();
        }

        internal void Disarm()
        {
            _state = default;
            ApplyPresentation();
        }

        internal override void Simulate(float step)
        {
            if (!_state.Armed || _encounter.State != EncounterState.Active || _encounter.Boss is { Phase: SupervisorPhase.SecurityAssessment })
            {
                return;
            }

            _state.Remaining -= step;
            _state.DamageCooldown = Mathf.Max(0f, _state.DamageCooldown - step);
            if (_state.Remaining <= 0f)
            {
                _state.Phase = _state.Phase switch
                {
                    HazardPhase.Dormant or HazardPhase.Recovery => HazardPhase.Warning,
                    HazardPhase.Warning => HazardPhase.Active,
                    _ => HazardPhase.Recovery
                };

                _state.Remaining += _state.Phase switch
                {
                    HazardPhase.Warning => _warningSeconds,
                    HazardPhase.Active => _activeSeconds,
                    _ => _recoverySeconds
                };
                ApplyPresentation();
            }

            if (_kind == ArenaHazardKind.SweepingLaser && _state.Phase is HazardPhase.Warning or HazardPhase.Active)
            {
                UpdateBeam();
            }

            if (_state.Phase != HazardPhase.Active || _state.DamageCooldown > 0f || !_director.CanDamagePlayer)
            {
                return;
            }

            var capsule = _director.PlayerCapsule;
            var center = capsule.transform.TransformPoint(capsule.center);
            var offset = capsule.transform.up * (capsule.height * 0.5f - capsule.radius);
            var start = center + offset;
            var end = center - offset;
            var hit = _kind == ArenaHazardKind.SweepingLaser ? BeamTouchesPlayer(start, end, capsule.radius) : InHazardVolume(start, end, capsule.radius);
            if (hit && _director.DamagePlayer(_damage, transform.position))
            {
                _state.DamageCooldown = _damageInterval;
            }
        }

        private void UpdateBeam()
        {
            var direction = BeamDirection();
            _state.BeamLength = Physics.Raycast(transform.position, direction, out var wall, _length, 1, QueryTriggerInteraction.Ignore) ? wall.distance : _length;
            _beam.SetPosition(0, transform.position);
            _beam.SetPosition(1, transform.position + direction * _state.BeamLength);
        }

        private Vector3 BeamDirection()
        {
            var progress = _state.Phase == HazardPhase.Active ? 1f - _state.Remaining / _activeSeconds : 0f;
            return transform.rotation * Quaternion.AngleAxis((progress - 0.5f) * _sweepDegrees, Vector3.up) * Vector3.forward;
        }

        private bool BeamTouchesPlayer(Vector3 start, Vector3 end, float radius)
        {
            var origin = transform.position;
            var direction = BeamDirection();
            return BeamNear(start, origin, direction, radius) || BeamNear((start + end) * 0.5f, origin, direction, radius) || BeamNear(end, origin, direction, radius);
        }

        private bool BeamNear(Vector3 point, Vector3 origin, Vector3 direction, float radius)
        {
            var delta = point - origin;
            return (delta - direction * Mathf.Clamp(Vector3.Dot(delta, direction), 0f, _state.BeamLength)).sqrMagnitude <= radius * radius;
        }

        private bool InHazardVolume(Vector3 start, Vector3 end, float radius)
        {
            var a = transform.InverseTransformPoint(start);
            var b = transform.InverseTransformPoint(end);
            var segment = b - a;
            var low = 0f;
            var high = 1f;
            for (var axis = 0; axis < 3; axis++)
            {
                var limit = _halfExtents[axis] + radius;
                if (Mathf.Abs(segment[axis]) <= 0.00001f)
                {
                    if (Mathf.Abs(a[axis]) > limit)
                    {
                        return false;
                    }

                    continue;
                }

                var near = (-limit - a[axis]) / segment[axis];
                var far = (limit - a[axis]) / segment[axis];
                low = Mathf.Max(low, Mathf.Min(near, far));
                high = Mathf.Min(high, Mathf.Max(near, far));
                if (low > high)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyPresentation()
        {
            var warning = _state.Phase == HazardPhase.Warning;
            var active = _state.Phase == HazardPhase.Active;
            _warning.SetActive(warning);
            _active.SetActive(active);
            if (_beam != null)
            {
                _beam.enabled = warning || active;
                _beam.startColor = _beam.endColor = active ? new Color(1f, 0.16f, 0.06f) : new Color(1f, 0.62f, 0.18f, 0.65f);
                _beam.widthMultiplier = active ? 0.115f : 0.04f;
                _beam.SetPosition(0, transform.position);
                _beam.SetPosition(1, transform.position + BeamDirection() * _state.BeamLength);
            }
        }

        private protected override Snapshot CaptureState() => _state;

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _state = snapshot;
            ApplyPresentation();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            if (_kind == ArenaHazardKind.SweepingLaser)
            {
                Gizmos.DrawRay(Vector3.zero, Quaternion.AngleAxis(-_sweepDegrees * 0.5f, Vector3.up) * Vector3.forward * _length);
                Gizmos.DrawRay(Vector3.zero, Quaternion.AngleAxis(_sweepDegrees * 0.5f, Vector3.up) * Vector3.forward * _length);
            }
            else
            {
                Gizmos.DrawWireCube(Vector3.zero, _halfExtents * 2f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        internal struct Snapshot
        {
            internal bool Armed;
            internal HazardPhase Phase;
            internal float Remaining;
            internal float DamageCooldown;
            internal float BeamLength;
        }
    }
}
