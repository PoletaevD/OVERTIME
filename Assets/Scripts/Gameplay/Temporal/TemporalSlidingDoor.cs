using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalSlidingDoor : TemporalActor<TemporalSlidingDoor.Snapshot>
    {
        internal enum DoorPhase
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        [SerializeField] private Vector3 _travel = Vector3.up * 2.6f;
        [SerializeField, Min(0.05f)] private float _closedTime = 1.2f;
        [SerializeField, Min(0.05f)] private float _travelTime = 0.85f;
        [SerializeField, Min(0.05f)] private float _openTime = 1.5f;

        private Vector3 _closedPosition;
        private DoorPhase _phase;
        private float _remaining;

        internal override void Initialize(int capacity)
        {
            _closedPosition = transform.localPosition;
            base.Initialize(capacity);
        }

        internal override void CollectColliders(List<Collider> colliders)
        {
            colliders.AddRange(GetComponentsInChildren<Collider>(true));
        }

        internal override void ResetState()
        {
            _phase = DoorPhase.Closed;
            _remaining = _closedTime;
            transform.localPosition = _closedPosition;
        }

        internal override void Simulate(float step)
        {
            _remaining -= step;
            while (_remaining <= 0f)
            {
                _phase = _phase == DoorPhase.Closing ? DoorPhase.Closed : _phase + 1;
                _remaining += _phase switch
                {
                    DoorPhase.Closed => _closedTime,
                    DoorPhase.Open => _openTime,
                    _ => _travelTime
                };
            }

            var openness = _phase switch
            {
                DoorPhase.Closed => 0f,
                DoorPhase.Open => 1f,
                DoorPhase.Opening => 1f - _remaining / _travelTime,
                _ => _remaining / _travelTime
            };
            transform.localPosition = _closedPosition + _travel * Mathf.SmoothStep(0f, 1f, openness);
        }

        private protected override Snapshot CaptureState()
        {
            return new Snapshot(transform.localPosition, _phase, _remaining);
        }

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _phase = snapshot.Phase;
            _remaining = snapshot.Remaining;
            transform.localPosition = snapshot.Position;
        }

        internal readonly struct Snapshot
        {
            internal readonly Vector3 Position;
            internal readonly DoorPhase Phase;
            internal readonly float Remaining;

            internal Snapshot(Vector3 position, DoorPhase phase, float remaining)
            {
                Position = position;
                Phase = phase;
                Remaining = remaining;
            }
        }
    }
}
