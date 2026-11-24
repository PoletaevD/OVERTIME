using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalProjectileEmitter : TemporalActor
    {
        private const int TraceSamples = 6;
        private const int TraceSpacing = 4;

        [SerializeField] private Transform[] _projectiles;
        [SerializeField] private bool _automatic = true;
        [SerializeField, Min(0.05f)] private float _interval = 0.48f;
        [SerializeField, Min(0.05f)] private float _lifetime = 1.45f;
        [SerializeField, Min(0.1f)] private float _speed = 8f;
        [SerializeField] private TemporalDirector _combatDirector;
        [SerializeField, Min(1)] private int _damage = 15;
        [SerializeField, Min(0.01f)] private float _radius = 0.13f;
        [SerializeField] private LayerMask _impactLayers = 1;
        [SerializeField] private Material _droneBolt;

        private Visual[] _visuals;
        private Projectile[] _live;
        private Projectile[] _history;
        private EmitterFrame[] _emitterHistory;
        private float _untilEmission;
        private uint _generation;

        internal uint Generation => _generation;

        internal int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var projectile in _live)
                {
                    count += projectile.Active ? 1 : 0;
                }

                return count;
            }
        }

        internal override void Initialize(int capacity)
        {
            var slots = _projectiles.Length;
            _visuals = new Visual[slots];
            _live = new Projectile[slots];
            _history = new Projectile[capacity * slots];
            _emitterHistory = new EmitterFrame[capacity];
            for (var slot = 0; slot < slots; slot++)
            {
                var transform = _projectiles[slot];
                var mesh = transform.GetComponent<MeshRenderer>();
                _visuals[slot] = new Visual(transform.GetComponent<LineRenderer>(), mesh, mesh != null ? mesh.sharedMaterial : null, transform.localScale);
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
            for (var slot = 0; slot < _live.Length; slot++)
            {
                ref var projectile = ref _live[slot];
                if (!projectile.Active)
                {
                    continue;
                }

                var distance = projectile.Speed * Mathf.Min(step, projectile.Remaining);
                if (_combatDirector != null && Sweep(projectile, distance, out var travelled))
                {
                    projectile.Position += projectile.Direction * travelled;
                    projectile.Hit = true;
                }
                else
                {
                    projectile.Position += projectile.Direction * distance;
                }

                projectile.Remaining = Mathf.Max(0f, projectile.Remaining - step);
                Apply(slot);
            }

            if (!_automatic)
            {
                return;
            }

            _untilEmission -= step;
            if (_untilEmission <= 0.000001f)
            {
                _untilEmission += _interval;
                TryLaunch(transform.position, transform.forward, _speed, _lifetime, _radius, _damage, false);
            }
        }

        internal bool TryLaunch(Vector3 origin, Vector3 direction, float speed, float lifetime, float radius, int damage, bool drone)
        {
            for (var slot = 0; slot < _live.Length; slot++)
            {
                if (_live[slot].Active)
                {
                    continue;
                }

                _live[slot] = new Projectile
                {
                    Position = origin,
                    Direction = direction,
                    Speed = speed,
                    Remaining = lifetime,
                    Radius = radius,
                    Damage = damage,
                    Generation = ++_generation,
                    Drone = drone
                };
                Apply(slot, true);
                return true;
            }

            return false;
        }

        private bool Sweep(in Projectile projectile, float distance, out float travelled)
        {
            travelled = 0f;
            var origin = projectile.Position;
            var radius = projectile.Radius;
            var canDamagePlayer = _combatDirector.CanDamagePlayer;
            var capsule = _combatDirector.PlayerCapsule;
            // Enemy bolts use the geometry mask. The separate hitbox layer keeps shooters out of their own sweeps.
            var mask = _automatic ? Physics.DefaultRaycastLayers : _impactLayers.value;
            if (canDamagePlayer)
            {
                mask = _automatic ? Physics.AllLayers : mask | (1 << capsule.gameObject.layer);
                if ((capsule.ClosestPoint(origin) - origin).sqrMagnitude <= radius * radius)
                {
                    return _combatDirector.DamagePlayer(projectile.Damage, origin - projectile.Direction);
                }
            }

            if (!Physics.SphereCast(origin, radius, projectile.Direction, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            travelled = hit.distance;
            return hit.collider != capsule || _combatDirector.DamagePlayer(projectile.Damage, origin - projectile.Direction);
        }

        internal override void Capture(int slot)
        {
            _emitterHistory[slot] = new EmitterFrame(_untilEmission, _generation);
            Array.Copy(_live, 0, _history, slot * _live.Length, _live.Length);
        }

        internal override void Restore(int slot)
        {
            var frame = _emitterHistory[slot];
            _untilEmission = frame.UntilEmission;
            _generation = frame.Generation;
            Array.Copy(_history, slot * _live.Length, _live, 0, _live.Length);
            for (var projectile = 0; projectile < _live.Length; projectile++)
            {
                Apply(projectile, true);
            }
        }

        internal override void Present(TemporalTimeline timeline, TemporalMode mode, bool showTraces)
        {
            for (var slot = 0; slot < _live.Length; slot++)
            {
                var current = _live[slot];
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
                else if (timeline.Direction == TemporalDirection.Forward)
                {
                    var fraction = timeline.ForwardFraction * (float)TemporalTimeline.TickDuration;
                    position += current.Direction * (current.Speed * Mathf.Min(fraction, current.Remaining));
                }

                _projectiles[slot].position = position;
                var trace = _visuals[slot].Trace;
                if (trace != null)
                {
                    PresentTrace(timeline, mode, showTraces, slot, current, position, trace);
                }
            }
        }

        private void PresentTrace(TemporalTimeline timeline, TemporalMode mode, bool showTraces, int slot, in Projectile current, Vector3 position, LineRenderer trace)
        {
            if (!showTraces || (timeline.Direction != TemporalDirection.Reverse && mode != TemporalMode.BulletTime))
            {
                trace.enabled = false;
                return;
            }

            var limit = (int)Math.Min(TraceSamples, 1 + (timeline.Cursor - timeline.Oldest) / TraceSpacing);
            var samples = 1;
            while (samples < limit && SameProjectile(Read(timeline, timeline.Cursor - samples * TraceSpacing, slot), current))
            {
                samples++;
            }

            trace.enabled = samples > 1;
            if (trace.enabled)
            {
                var color = TemporalEffects.ModeColor(mode);
                trace.startColor = new Color(color.r, color.g, color.b, 0.78f);
                trace.endColor = new Color(color.r, color.g, color.b, 0f);
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

        private void Apply(int slot, bool refreshStyle = false)
        {
            var projectile = _live[slot];
            var visual = _projectiles[slot];
            ref readonly var style = ref _visuals[slot];
            if (visual.gameObject.activeSelf != projectile.Active)
            {
                if (!projectile.Active && style.Trace != null)
                {
                    style.Trace.enabled = false;
                }

                visual.gameObject.SetActive(projectile.Active);
            }

            if (!projectile.Active)
            {
                return;
            }

            visual.position = projectile.Position;
            if (refreshStyle && !_automatic)
            {
                visual.rotation = Quaternion.LookRotation(projectile.Direction);
                visual.localScale = style.Scale * (projectile.Radius / _radius);
                if (style.Mesh != null)
                {
                    style.Mesh.sharedMaterial = projectile.Drone && _droneBolt != null ? _droneBolt : style.Material;
                }
            }
        }

        private readonly struct Visual
        {
            internal readonly LineRenderer Trace;
            internal readonly MeshRenderer Mesh;
            internal readonly Material Material;
            internal readonly Vector3 Scale;

            internal Visual(LineRenderer trace, MeshRenderer mesh, Material material, Vector3 scale)
            {
                Trace = trace;
                Mesh = mesh;
                Material = material;
                Scale = scale;
            }
        }

        private struct Projectile
        {
            internal Vector3 Position;
            internal Vector3 Direction;
            internal float Speed;
            internal float Remaining;
            internal float Radius;
            internal int Damage;
            internal uint Generation;
            internal bool Drone;
            internal bool Hit;
            internal bool Active => !Hit && Remaining > 0f;
        }

        private readonly struct EmitterFrame
        {
            internal readonly float UntilEmission;
            internal readonly uint Generation;

            internal EmitterFrame(float untilEmission, uint generation)
            {
                UntilEmission = untilEmission;
                Generation = generation;
            }
        }
    }
}
