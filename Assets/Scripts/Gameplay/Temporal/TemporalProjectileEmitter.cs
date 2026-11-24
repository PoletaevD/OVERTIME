using System;
using UnityEngine;
using UnityEngine.Rendering;

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

        private readonly RaycastHit[] _sweepHits = new RaycastHit[64];
        private readonly Collider[] _overlaps = new Collider[64];
        private bool _playerPool;
        private Material _fallbackMaterial;
        private Visual[] _visuals;
        private Projectile[] _live;
        private Projectile[] _history;
        private EmitterFrame[] _emitterHistory;
        private float _untilEmission;
        private uint _generation;
        private int _activeCount;

        internal uint Generation => _generation;
        internal int PoolCapacity => _projectiles.Length;

        internal void ConfigurePlayerPool(TemporalDirector director, int capacity, Material material)
        {
            _playerPool = true;
            _automatic = false;
            _combatDirector = director;
            _radius = 0.045f;
            _projectiles = new Transform[Mathf.Max(1, capacity)];
            if (material == null)
            {
                _fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _fallbackMaterial.color = new Color(0.08f, 1.45f, 1.8f);
                material = _fallbackMaterial;
            }

            for (var slot = 0; slot < _projectiles.Length; slot++)
            {
                var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bolt.name = $"ARC-01 Bolt {slot + 1:00}";
                bolt.transform.SetParent(transform, false);
                bolt.transform.localScale = new Vector3(0.09f, 0.09f, 0.24f);
                var collider = bolt.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var mesh = bolt.GetComponent<MeshRenderer>();
                mesh.sharedMaterial = material;
                mesh.shadowCastingMode = ShadowCastingMode.Off;
                mesh.receiveShadows = false;
                var trace = bolt.AddComponent<LineRenderer>();
                trace.sharedMaterial = material;
                trace.useWorldSpace = true;
                trace.positionCount = 2;
                trace.startWidth = 0.052f;
                trace.endWidth = 0.006f;
                trace.numCapVertices = 2;
                trace.shadowCastingMode = ShadowCastingMode.Off;
                trace.receiveShadows = false;
                trace.enabled = false;
                bolt.SetActive(false);
                _projectiles[slot] = bolt.transform;
            }
        }

        private void OnDestroy()
        {
            if (_fallbackMaterial != null)
            {
                Destroy(_fallbackMaterial);
            }
        }

        internal int ActiveCount => _activeCount;

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

        internal void ClearProjectiles()
        {
            ResetState();
            Array.Clear(_history, 0, _history.Length);
        }

        internal override void ResetState()
        {
            _untilEmission = _interval;
            _generation = 0;
            _activeCount = 0;
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

                var distance = Mathf.Min(projectile.Range, projectile.Speed * Mathf.Min(step, projectile.Remaining));
                if (_combatDirector != null && CheckImpact(projectile, distance, out var travelled))
                {
                    projectile.Position += projectile.Direction * travelled;
                    projectile.Hit = true;
                }
                else
                {
                    projectile.Position += projectile.Direction * distance;
                    projectile.Range -= distance;
                    projectile.Travelled += distance;
                }

                projectile.Remaining = Mathf.Max(0f, projectile.Remaining - step);
                if (!projectile.Active)
                {
                    _activeCount--;
                }

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
                TryLaunch(transform.position, transform.forward, _speed, _lifetime, _radius, _damage, false, transform);
            }
        }

        internal bool TryLaunch(Vector3 origin, Vector3 direction, float speed, float lifetime, float radius, int damage, bool drone, Transform owner = null)
        {
            var layers = _automatic ? Physics.DefaultRaycastLayers : _impactLayers.value;
            return TryLaunch(new Projectile
            {
                Position = origin,
                Direction = direction.normalized,
                Speed = speed,
                Remaining = lifetime,
                Range = speed * lifetime,
                Radius = radius,
                Damage = damage,
                Drone = drone,
                Owner = owner,
                Layers = layers
            });
        }

        internal bool TryLaunchPlayer(Ray aim, Vector3 aimUp, Vector3 muzzleOffset, float speed, float lifetime, float range, float radius, int damage, int layers)
        {
            var owner = _combatDirector.PlayerCapsule.transform;
            var aimDistance = FindHit(aim.origin, 0f, aim.direction, range, layers, owner, out var aimHit) ? aimHit.distance : range;
            var point = aim.GetPoint(aimDistance);
            var frame = Quaternion.LookRotation(aim.direction, aimUp);
            var offset = frame * muzzleOffset;
            var muzzleDepth = Vector3.Dot(offset, aim.direction);
            if (muzzleDepth > 0f && aimDistance < muzzleDepth + radius)
            {
                offset *= Mathf.Clamp01((aimDistance - radius * 2f) / muzzleDepth);
            }

            var origin = aim.origin + offset;
            var direction = point - origin;
            if (FindOverlap(aim.origin, radius, layers, owner, out _, out _) || FindHit(aim.origin, radius, offset.normalized, offset.magnitude, layers, owner, out _))
            {
                // Keep the bolt on the eye side of cover and send it along the obstructed muzzle path.
                origin = aim.origin;
                direction = offset.sqrMagnitude > 0f ? offset : aim.direction;
            }
            else if (Vector3.Dot(direction, aim.direction) <= 0f)
            {
                direction = aim.direction;
            }

            return TryLaunch(new Projectile
            {
                Position = origin,
                Direction = direction.normalized,
                Speed = speed,
                Remaining = lifetime,
                Range = range,
                Radius = radius,
                Damage = damage,
                Owner = owner,
                Layers = layers
            });
        }

        private bool TryLaunch(Projectile projectile)
        {
            for (var slot = 0; slot < _live.Length; slot++)
            {
                if (_live[slot].Active)
                {
                    continue;
                }

                projectile.Generation = ++_generation;
                _live[slot] = projectile;
                _activeCount++;
                Apply(slot, true);
                return true;
            }

            return false;
        }

        private bool CheckImpact(in Projectile projectile, float distance, out float travelled)
        {
            var layers = projectile.Layers;
            var capsule = _combatDirector.PlayerCapsule;
            if (!_playerPool && _combatDirector.CanDamagePlayer)
            {
                layers |= 1 << capsule.gameObject.layer;
            }

            travelled = 0f;
            var normal = -projectile.Direction;
            if (!FindOverlap(projectile.Position, projectile.Radius, layers, projectile.Owner, out var collider, out var point))
            {
                if (!FindHit(projectile.Position, projectile.Radius, projectile.Direction, distance, layers, projectile.Owner, out var hit))
                {
                    return false;
                }

                collider = hit.collider;
                point = hit.point;
                normal = hit.normal;
                travelled = hit.distance;
            }

            var damage = default(DamageResult);
            var weakPoint = false;
            var shield = _playerPool ? collider.GetComponent<CombatShield>() : null;
            var shielded = shield != null;
            if (_playerPool)
            {
                if (shielded)
                {
                    shield.React(_combatDirector.ReducedMotion);
                }
                else if (collider.GetComponentInParent<ICombatDamageable>() is { } target)
                {
                    damage = _combatDirector.DamageTarget(target, collider, projectile.Damage, out weakPoint);
                }
            }
            else if (collider == capsule)
            {
                _combatDirector.DamagePlayer(projectile.Damage, projectile.Position - projectile.Direction);
            }

            _combatDirector.PublishImpact(new ProjectileImpact(point, normal, damage, weakPoint, shielded, _playerPool));
            return true;
        }

        private bool FindHit(Vector3 origin, float radius, Vector3 direction, float distance, int layers, Transform owner, out RaycastHit closest)
        {
            closest = default;
            if (distance <= 0f)
            {
                return false;
            }

            var count = radius > 0f ? Physics.SphereCastNonAlloc(origin, radius, direction, _sweepHits, distance, layers, QueryTriggerInteraction.Ignore) : Physics.RaycastNonAlloc(origin, direction, _sweepHits, distance, layers, QueryTriggerInteraction.Ignore);
            var hits = _sweepHits;
            if (count == hits.Length)
            {
                hits = radius > 0f ? Physics.SphereCastAll(origin, radius, direction, distance, layers, QueryTriggerInteraction.Ignore) : Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            var best = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidate = hits[index];
                if (candidate.distance <= best && IsValid(candidate.collider, owner) && (candidate.distance < best || candidate.collider.TryGetComponent<CombatShield>(out _)))
                {
                    closest = candidate;
                    best = candidate.distance;
                }
            }

            return closest.collider != null;
        }

        private bool FindOverlap(Vector3 origin, float radius, int layers, Transform owner, out Collider closest, out Vector3 point)
        {
            var overlaps = _overlaps;
            var count = Physics.OverlapSphereNonAlloc(origin, radius, overlaps, layers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
            {
                overlaps = Physics.OverlapSphere(origin, radius, layers, QueryTriggerInteraction.Ignore);
                count = overlaps.Length;
            }

            closest = null;
            point = origin;
            var best = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidate = overlaps[index];
                if (!IsValid(candidate, owner))
                {
                    continue;
                }

                var surface = candidate is MeshCollider { convex: false } ? origin : candidate.ClosestPoint(origin);
                var distance = (surface - origin).sqrMagnitude;
                if (distance < best || (distance == best && candidate.TryGetComponent<CombatShield>(out _)))
                {
                    closest = candidate;
                    point = surface;
                    best = distance;
                }
            }

            return closest != null;
        }

        private bool IsValid(Collider collider, Transform owner)
        {
            var capsule = _combatDirector.PlayerCapsule;
            if (collider == capsule)
            {
                return !_playerPool && _combatDirector.CanDamagePlayer;
            }

            if (owner != null && collider.transform.IsChildOf(owner))
            {
                return false;
            }

            return _playerPool || collider.GetComponentInParent<ICombatDamageable>() == null;
        }

        internal override void Capture(int slot)
        {
            _emitterHistory[slot] = new EmitterFrame(_untilEmission, _generation, _activeCount);
            Array.Copy(_live, 0, _history, slot * _live.Length, _live.Length);
        }

        internal override void Restore(int slot)
        {
            var frame = _emitterHistory[slot];
            _untilEmission = frame.UntilEmission;
            _generation = frame.Generation;
            _activeCount = frame.ActiveCount;
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
                _projectiles[slot].position = position;
                var trace = _visuals[slot].Trace;
                if (trace != null)
                {
                    if (_playerPool)
                    {
                        trace.enabled = current.Travelled > 0f;
                        trace.SetPosition(0, position);
                        trace.SetPosition(1, position - current.Direction * Mathf.Min(current.Travelled, 0.34f));
                    }
                    else
                    {
                        PresentTrace(timeline, mode, showTraces, slot, current, position, trace);
                    }
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
            internal float Range;
            internal float Travelled;
            internal int Layers;
            internal Transform Owner;
            internal float Radius;
            internal int Damage;
            internal uint Generation;
            internal bool Drone;
            internal bool Hit;
            internal bool Active => !Hit && Remaining > 0f && Range > 0f;
        }

        private readonly struct EmitterFrame
        {
            internal readonly float UntilEmission;
            internal readonly uint Generation;

            internal readonly int ActiveCount;

            internal EmitterFrame(float untilEmission, uint generation, int activeCount)
            {
                UntilEmission = untilEmission;
                Generation = generation;
                ActiveCount = activeCount;
            }
        }
    }
}
