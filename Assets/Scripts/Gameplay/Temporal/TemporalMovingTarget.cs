using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalMovingTarget : TemporalActor<float>
    {
        private const int TraceSamples = 7;
        private const int TraceSpacing = 5;

        [SerializeField] private Transform _visual;
        [SerializeField] private LineRenderer _trace;
        [SerializeField] private Vector3 _travel = Vector3.right * 2f;
        [SerializeField, Min(0.1f)] private float _period = 3f;
        [SerializeField, Range(0f, 1f)] private float _startPhase;

        private float _phase;

        internal override void ResetState()
        {
            RestoreState(_startPhase);
        }

        internal override void Simulate(float step)
        {
            _phase = Mathf.Repeat(_phase + step / _period, 1f);
            _visual.position = PositionAt(_phase);
        }

        private protected override float CaptureState()
        {
            return _phase;
        }

        private protected override void RestoreState(in float snapshot)
        {
            _phase = snapshot;
            _visual.position = PositionAt(_phase);
        }

        internal override void Present(TemporalTimeline timeline, TemporalMode mode, bool showTraces)
        {
            var position = PositionAt(_phase);
            if (timeline.PreviewFraction > 0f)
            {
                position = Vector3.Lerp(position, PositionAt(Read(timeline, timeline.Cursor - 1)), timeline.PreviewFraction);
            }
            else if (timeline.Direction == TemporalDirection.Forward)
            {
                position = PositionAt(_phase + timeline.ForwardFraction * (float)TemporalTimeline.TickDuration / _period);
            }

            _visual.position = position;
            if (_trace == null)
            {
                return;
            }

            var samples = (int)System.Math.Min(TraceSamples, 1 + (timeline.Cursor - timeline.Oldest) / TraceSpacing);
            _trace.enabled = timeline.Direction == TemporalDirection.Reverse && showTraces && samples > 1;
            if (_trace.enabled)
            {
                var color = TemporalEffects.ModeColor(mode);
                _trace.startColor = new Color(color.r, color.g, color.b, 0.73f);
                _trace.endColor = new Color(color.r, color.g, color.b, 0f);
                _trace.positionCount = samples;
                _trace.SetPosition(0, position);
                for (var sample = 1; sample < samples; sample++)
                {
                    _trace.SetPosition(sample, PositionAt(Read(timeline, timeline.Cursor - sample * TraceSpacing)));
                }
            }
        }

        private Vector3 PositionAt(float phase)
        {
            return transform.TransformPoint(_travel * Mathf.Sin(phase * (2f * Mathf.PI)));
        }
    }
}
