using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal sealed class ArenaMechanism : TemporalActor<ArenaMechanism.Snapshot>
    {
        [SerializeField] private Vector3 _travel;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private float _rotationDegrees;
        [SerializeField, Min(1f)] private float _period = 6f;
        [SerializeField, Range(0f, 1f)] private float _phase;
        [SerializeField] private Collider[] _obstacles;

        private TemporalDirector _director;
        private EncounterController _encounter;
        private Vector3 _origin;
        private Quaternion _restRotation;
        private Snapshot _state;

        internal override bool AllowsGravityAttachment => false;
        internal float Phase => _state.Phase;

        internal void Bind(TemporalDirector director, EncounterController encounter)
        {
            _director = director;
            _encounter = encounter;
            _origin = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        internal override void CollectColliders(List<Collider> colliders)
        {
            colliders.AddRange(_obstacles);
        }

        internal override void ResetState()
        {
            _state = new Snapshot { Phase = _phase };
            UpdatePose();
        }

        internal override void Simulate(float step)
        {
            if (_encounter.State != EncounterState.Active || _encounter.Boss is { Phase: SupervisorPhase.SecurityAssessment })
            {
                return;
            }

            var previous = _state;
            _state.Phase = Mathf.Repeat(_state.Phase + step / _period, 1f);
            UpdatePose();
            if (!_director.IsPlayerProtected && TouchesPlayer())
            {
                RestoreState(previous);
            }
        }

        private void UpdatePose()
        {
            var travel = (1f - Mathf.Cos(_state.Phase * Mathf.PI * 2f)) * 0.5f;
            transform.SetLocalPositionAndRotation(_origin + _travel * travel, _restRotation * Quaternion.AngleAxis(_rotationDegrees * _state.Phase, _rotationAxis));
            _state.Position = transform.localPosition;
            _state.Rotation = transform.localRotation;
        }

        private bool TouchesPlayer()
        {
            var capsule = _director.PlayerCapsule;
            var player = capsule.transform;
            var center = player.TransformPoint(capsule.center);
            foreach (var obstacle in _obstacles)
            {
                var part = obstacle.transform;
                var radius = part.lossyScale.magnitude * 0.87f + capsule.height;
                if ((part.position - center).sqrMagnitude < radius * radius && Physics.ComputePenetration(capsule, player.position, player.rotation, obstacle, part.position, part.rotation, out _, out var depth) && depth > 0.004f)
                {
                    return true;
                }
            }

            return false;
        }

        private protected override Snapshot CaptureState() => _state;

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _state = snapshot;
            transform.SetLocalPositionAndRotation(snapshot.Position, snapshot.Rotation);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var start = Application.isPlaying ? transform.parent.TransformPoint(_origin) : transform.position;
            Gizmos.DrawLine(start, start + transform.parent.TransformVector(_travel));
            Gizmos.DrawWireSphere(start + transform.parent.TransformVector(_travel), 0.3f);
        }
#endif

        internal struct Snapshot
        {
            internal float Phase;
            internal Vector3 Position;
            internal Quaternion Rotation;
        }
    }
}
