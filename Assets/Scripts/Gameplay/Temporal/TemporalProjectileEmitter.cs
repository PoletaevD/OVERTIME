using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalProjectileEmitter : TemporalActor
    {
        private const int TraceSamples = 6;
        private const int TraceSpacing = 4;

        [SerializeField] private Transform[] _projectiles;
        [SerializeField, Min(0.05f)] private float _interval = 0.48f;
        [SerializeField, Min(0.05f)] private float _lifetime = 1.45f;
        [SerializeField, Min(0.1f)] private float _speed = 8f;

        private LineRenderer[] _traces;
        private Projectile[] _live;
        private Projectile[] _history;
        private EmitterFrame[] _emitterHistory;
        private float _untilEmission;
        private uint _generation;

        internal override void Initialize(int capacity)
        {
            var slots = _projectiles.Length;
            _traces = new LineRenderer[slots];
            _live = new Projectile[slots];
            _history = new Projectile[capacity * slots];
            _emitterHistory = new EmitterFrame[capacity];
            for (var slot = 0; slot < slots; slot++)
            {
                _traces[slot] = _projectiles[slot].GetComponent<LineRenderer>();
            }

            ResetState();
        }

        internal override void ResetState()
        {
            _untilEmission = _interval;
            _generation = 0;
            for (var slot = 0; slot < _live.Length; slot++)
            {
                _live[slot] = default;
                Apply(slot);
            }
        }

        internal override void Simulate(float step)
        {
            var displacement = transform.forward * (_speed * step);
            for (var slot = 0; slot < _live.Length; slot++)
            {
                ref var projectile = ref _live[slot];
                if (projectile.Active)
                {
                    projectile.Remaining = Mathf.Max(0f, projectile.Remaining - step);
                    projectile.Position += displacement;
                    Apply(slot);
                }
            }

            _untilEmission -= step;
            if (_untilEmission <= 0.000001f)
            {
                _untilEmission += _interval;
                for (var slot = 0; slot < _live.Length; slot++)
                {
                    if (!_live[slot].Active)
                    {
                        _live[slot] = new Projectile { Position = transform.position, Remaining = _lifetime, Generation = ++_generation };
                        Apply(slot);
                        break;
                    }
                }
            }
        }

        internal override void Capture(int slot)
        {
            _emitterHistory[slot] = new EmitterFrame { UntilEmission = _untilEmission, Generation = _generation };
            System.Array.Copy(_live, 0, _history, slot * _live.Length, _live.Length);
        }

        internal override void Restore(int slot)
        {
            var frame = _emitterHistory[slot];
            _untilEmission = frame.UntilEmission;
            _generation = frame.Generation;
            System.Array.Copy(_history, slot * _live.Length, _live, 0, _live.Length);
            for (var projectile = 0; projectile < _live.Length; projectile++)
            {
                Apply(projectile);
            }
        }

        internal override void Present(TemporalTimeline timeline, bool showTraces)
        {
            for (var slot = 0; slot < _live.Length; slot++)
            {
                var current = _live[slot];
                var trace = _traces[slot];
                if (!current.Active)
                {
                    continue;
                }

                var position = current.Position;
                if (timeline.PreviewFraction > 0f)
                {
                    var older = Read(timeline, timeline.Cursor - 1, slot);
                    if (SameProjectile(older, current))
                    {
                        position = Vector3.Lerp(position, older.Position, timeline.PreviewFraction);
                    }
                }

                _projectiles[slot].position = position;
                if (trace != null)
                {
                    PresentTrace(timeline, showTraces, slot, current, position, trace);
                }
            }
        }

        private void PresentTrace(TemporalTimeline timeline, bool showTraces, int slot, in Projectile current, Vector3 position, LineRenderer trace)
        {
            if (timeline.Direction != TemporalDirection.Reverse || !showTraces)
            {
                trace.enabled = false;
                return;
            }

            var limit = (int)System.Math.Min(TraceSamples, 1 + (timeline.Cursor - timeline.Oldest) / TraceSpacing);
            var samples = 1;
            while (samples < limit && SameProjectile(Read(timeline, timeline.Cursor - samples * TraceSpacing, slot), current))
            {
                samples++;
            }

            trace.enabled = samples > 1;
            if (trace.enabled)
            {
                trace.positionCount = samples;
                trace.SetPosition(0, position);
                for (var sample = 1; sample < samples; sample++)
                {
                    trace.SetPosition(sample, Read(timeline, timeline.Cursor - sample * TraceSpacing, slot).Position);
                }
            }
        }

        private Projectile Read(TemporalTimeline timeline, long tick, int slot)
        {
            return _history[timeline.Slot(tick) * _live.Length + slot];
        }

        private bool SameProjectile(in Projectile first, in Projectile second)
        {
            return first.Active && first.Generation == second.Generation;
        }

        private void Apply(int slot)
        {
            var projectile = _live[slot];
            var visual = _projectiles[slot];
            if (visual.gameObject.activeSelf != projectile.Active)
            {
                if (!projectile.Active && _traces[slot] != null)
                {
                    _traces[slot].enabled = false;
                }

                visual.gameObject.SetActive(projectile.Active);
            }

            if (projectile.Active)
            {
                visual.position = projectile.Position;
            }
        }

        private struct Projectile
        {
            internal Vector3 Position;
            internal float Remaining;
            internal uint Generation;

            internal bool Active => Remaining > 0f;
        }

        private struct EmitterFrame
        {
            internal float UntilEmission;
            internal uint Generation;
        }
    }
}
