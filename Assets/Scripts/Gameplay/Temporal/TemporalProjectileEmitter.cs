using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class TemporalProjectileEmitter : TemporalActor
    {
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");
        private const int TraceSamples = 6;
        private const int TraceSpacing = 4;
        private const int MaxCollisionIterations = 6;
        private const float SurfaceSkin = 0.004f;
        private const float GoldenAngle = 2.39996323f;

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

        private readonly RaycastHit[] _sweepHits = new RaycastHit[128];
        private readonly Collider[] _overlaps = new Collider[64];
        private bool _playerPool;
        private Material _fallbackMaterial;
        private MaterialPropertyBlock _boltProperties;
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
            _boltProperties = new MaterialPropertyBlock();
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
                bolt.name = $"Player Bolt {slot + 1:00}";
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

                var direction = projectile.Direction;
                var distance = Mathf.Min(projectile.Range, projectile.Speed * Mathf.Min(step, projectile.Remaining));
                if (_combatDirector != null)
                {
                    AdvanceProjectile(ref projectile, distance);
                }
                else
                {
                    Travel(ref projectile, distance);
                }

                projectile.Remaining = Mathf.Max(0f, projectile.Remaining - step);
                if (!projectile.Active)
                {
                    _activeCount--;
                }

                Apply(slot, _playerPool && projectile.Direction != direction);
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

        internal bool TryLaunchPlayer(Ray aim, Vector3 aimUp, Vector3 muzzleOffset, float speed, float lifetime, float range, float radius, int damage, int layers, bool charged = false, bool hypervelocity = false)
        {
            return TryLaunchVolley(aim, aimUp, muzzleOffset, speed, lifetime, range, radius, damage, layers, charged, hypervelocity, default, 1, 0f, 0f, 0f);
        }

        internal bool TryLaunchVolley(Ray aim, Vector3 aimUp, Vector3 muzzleOffset, float speed, float lifetime, float range, float radius, float damage, int layers, bool charged, bool hypervelocity, in ProjectileBallistics ballistics, int pellets, float spread, float twinDamage, float twinOffset)
        {
            var count = pellets + (twinDamage > 0f ? 1 : 0);
            if (PoolCapacity - ActiveCount < count)
            {
                return false;
            }

            Physics.SyncTransforms();
            var owner = _combatDirector.PlayerCapsule.transform;
            var aimDistance = FindHit(aim.origin, 0f, aim.direction, range, layers, owner, out var aimHit, out _) ? aimHit.distance : range;
            var frame = Quaternion.LookRotation(aim.direction, aimUp);
            var offset = frame * muzzleOffset;
            var muzzleDepth = Vector3.Dot(offset, aim.direction);
            if (muzzleDepth > 0f && aimDistance < muzzleDepth + radius)
            {
                offset *= Mathf.Clamp01((aimDistance - radius * 2f) / muzzleDepth);
            }

            var origin = aim.origin + offset;
            var direction = aim.GetPoint(aimDistance) - origin;
            if (FindOverlap(aim.origin, radius, layers, owner, out _, out _, out var overlapFull) || overlapFull || FindHit(aim.origin, radius, offset.normalized, offset.magnitude, layers, owner, out _, out var castFull) || castFull)
            {
                origin = aim.origin;
                direction = offset.sqrMagnitude > 0f ? offset : aim.direction;
            }
            else if (Vector3.Dot(direction, aim.direction) <= 0f)
            {
                direction = aim.direction;
            }

            var pattern = Quaternion.LookRotation(direction, aimUp);
            var projectile = new Projectile
            {
                Position = origin,
                Speed = speed,
                Remaining = lifetime,
                Range = range,
                Radius = radius,
                Owner = owner,
                Layers = layers,
                Charged = charged,
                Hypervelocity = hypervelocity,
                Ballistics = ballistics,
                Retention = 1f,
                Penetrations = Mathf.Max(0, ballistics.TargetLimit - 1),
                Ricochets = ballistics.RicochetLimit
            };

            var next = 0;
            for (var slot = 0; slot < _live.Length && next < count; slot++)
            {
                if (_live[slot].Active)
                {
                    continue;
                }

                var duplicate = next >= pellets;
                projectile.Direction = duplicate ? pattern * (Quaternion.Euler(-twinOffset * 0.5f, twinOffset, 0f) * Vector3.forward) : PelletDirection(pattern, next, pellets, spread);
                projectile.Damage = duplicate ? damage * twinDamage : damage;
                LaunchAt(slot, projectile);
                next++;
            }

            return true;
        }

        private Vector3 PelletDirection(Quaternion frame, int index, int count, float spread)
        {
            if (index == 0 || spread <= 0f)
            {
                return frame * Vector3.forward;
            }

            var radius = Mathf.Sqrt((float)index / (count - 1)) * Mathf.Tan(spread * Mathf.Deg2Rad);
            var angle = index * GoldenAngle;
            return frame * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 1f).normalized;
        }

        private bool TryLaunch(Projectile projectile)
        {
            for (var slot = 0; slot < _live.Length; slot++)
            {
                if (_live[slot].Active)
                {
                    continue;
                }

                LaunchAt(slot, projectile);
                return true;
            }

            return false;
        }

        private void LaunchAt(int slot, Projectile projectile)
        {
            projectile.Generation = ++_generation;
            _live[slot] = projectile;
            _activeCount++;
            Apply(slot, true);
        }

        private void AdvanceProjectile(ref Projectile projectile, float distance)
        {
            var layers = projectile.Layers;
            var capsule = _combatDirector.PlayerCapsule;
            if (!_playerPool && _combatDirector.CanDamagePlayer)
            {
                layers |= 1 << capsule.gameObject.layer;
            }

            for (var iteration = 0; iteration < MaxCollisionIterations && distance > 0f; iteration++)
            {
                if (!FindImpact(projectile, distance, layers, out var collider, out var point, out var normal, out var travelled))
                {
                    Travel(ref projectile, distance);
                    return;
                }

                Travel(ref projectile, travelled);
                distance -= travelled;
                if (!ResolveImpact(ref projectile, capsule, collider, point, normal))
                {
                    projectile.Hit = true;
                    return;
                }

                if (projectile.Range <= 0f)
                {
                    return;
                }

                distance = Mathf.Min(distance, projectile.Range);
            }
        }

        private void Travel(ref Projectile projectile, float distance)
        {
            projectile.Position += projectile.Direction * distance;
            projectile.Range -= distance;
            projectile.Travelled += distance;
        }

        private bool FindImpact(in Projectile projectile, float distance, int layers, out Collider collider, out Vector3 point, out Vector3 normal, out float travelled)
        {
            travelled = 0f;
            if (FindOverlap(projectile.Position, projectile.Radius, layers, projectile.Owner, out collider, out point, out var overlapFull, projectile.Targets) || overlapFull)
            {
                normal = (projectile.Position - point).normalized;
                return true;
            }

            if (FindHit(projectile.Position, projectile.Radius, projectile.Direction, distance, layers, projectile.Owner, out var hit, out var castFull, projectile.Targets) || castFull)
            {
                collider = hit.collider;
                point = hit.point;
                normal = hit.normal;
                travelled = hit.distance;
                return true;
            }

            normal = Vector3.zero;
            return false;
        }

        private bool ResolveImpact(ref Projectile projectile, Collider capsule, Collider collider, Vector3 point, Vector3 normal)
        {
            if (collider == null)
            {
                return false;
            }

            var damage = default(DamageResult);
            var weakPoint = false;
            var shielded = false;
            var pierced = false;
            var reflected = false;
            if (_playerPool)
            {
                if (collider.TryGetComponent<CombatShield>(out var shield))
                {
                    shielded = true;
                    shield.React(_combatDirector.ReducedMotion);
                }
                else if (collider.GetComponentInParent<ICombatDamageable>() is { } target)
                {
                    var amount = projectile.Ballistics.DamageAtDistance(projectile.Damage, projectile.Travelled) * projectile.Retention;
                    damage = _combatDirector.DamageTarget(target, collider, amount, out weakPoint);
                    if (projectile.Penetrations > 0 && damage.Amount > 0)
                    {
                        projectile.Targets.Add(target.RewardId);
                        projectile.Penetrations--;
                        projectile.Retention *= projectile.Ballistics.PenetrationRetention;
                        pierced = true;
                    }
                }
                else if (projectile.Ricochets > 0 && collider.attachedRigidbody == null && collider.GetComponentInParent<EncounterGate>() == null && collider.GetComponentInParent<TemporalSlidingDoor>() == null && Vector3.Dot(projectile.Direction, normal) < -0.0001f)
                {
                    projectile.Direction = Vector3.Reflect(projectile.Direction, normal).normalized;
                    projectile.Position = point + normal * (projectile.Radius + SurfaceSkin);
                    projectile.Ricochets--;
                    projectile.Retention *= projectile.Ballistics.RicochetRetention;
                    reflected = true;
                }
            }
            else if (collider == capsule)
            {
                _combatDirector.DamagePlayer(Mathf.RoundToInt(projectile.Damage), projectile.Position - projectile.Direction);
            }

            if (normal == Vector3.zero)
            {
                normal = -projectile.Direction;
            }

            _combatDirector.PublishImpact(new ProjectileImpact(point, normal, damage, weakPoint, shielded, _playerPool, reflected, projectile.Ballistics.TargetLimit > 1 && damage.Amount > 0));
            return pierced || reflected;
        }

        private bool FindHit(Vector3 origin, float radius, Vector3 direction, float distance, int layers, Transform owner, out RaycastHit closest, out bool full, ProjectileTargets targets = default)
        {
            closest = default;
            full = false;
            if (distance <= 0f)
            {
                return false;
            }

            var count = radius > 0f ? Physics.SphereCastNonAlloc(origin, radius, direction, _sweepHits, distance, layers, QueryTriggerInteraction.Ignore) : Physics.RaycastNonAlloc(origin, direction, _sweepHits, distance, layers, QueryTriggerInteraction.Ignore);
            full = count == _sweepHits.Length;
            if (full)
            {
                // An incomplete query cannot establish whether a shield or a wall is nearer.
                return false;
            }

            var best = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidate = _sweepHits[index];
                if (candidate.distance <= best && IsValid(candidate.collider, owner, targets) && (candidate.distance < best || candidate.collider.TryGetComponent<CombatShield>(out _)))
                {
                    closest = candidate;
                    best = candidate.distance;
                }
            }

            return closest.collider != null;
        }

        private bool FindOverlap(Vector3 origin, float radius, int layers, Transform owner, out Collider closest, out Vector3 point, out bool full, ProjectileTargets targets = default)
        {
            var count = Physics.OverlapSphereNonAlloc(origin, radius, _overlaps, layers, QueryTriggerInteraction.Ignore);
            closest = null;
            point = origin;
            full = count == _overlaps.Length;
            if (full)
            {
                return false;
            }
            var best = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var candidate = _overlaps[index];
                if (!IsValid(candidate, owner, targets))
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

        private bool IsValid(Collider collider, Transform owner, ProjectileTargets targets)
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

            if (_playerPool && collider.TryGetComponent<CombatShield>(out _))
            {
                return true;
            }

            var target = collider.GetComponentInParent<ICombatDamageable>();
            return target == null || (_playerPool && !targets.Contains(target.RewardId));
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
                        trace.SetPosition(1, position - current.Direction * Mathf.Min(current.Travelled, current.Ballistics.Weapon == WeaponId.Deadline ? 1.8f : current.Hypervelocity ? 0.85f : 0.34f));
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
                if (_playerPool && style.Trace != null)
                {
                    var color = BoltColor(projectile);
                    style.Trace.startColor = color;
                    style.Trace.endColor = new Color(color.r, color.g, color.b, 0f);
                }

                if (style.Mesh != null)
                {
                    style.Mesh.sharedMaterial = projectile.Drone && _droneBolt != null ? _droneBolt : style.Material;
                    if (_playerPool)
                    {
                        _boltProperties.Clear();
                        _boltProperties.SetColor(s_baseColor, BoltColor(projectile));

                        style.Mesh.SetPropertyBlock(_boltProperties);
                    }
                }
            }
        }

        private Color BoltColor(in Projectile projectile)
        {
            if (projectile.Charged)
            {
                return new Color(2.6f, 0.83f, 0.24f);
            }

            return projectile.Ballistics.Weapon switch
            {
                WeaponId.Termination => new Color(2.2f, 0.7f, 0.25f),
                WeaponId.Deadline => new Color(0.65f, 1.6f, 3f),
                _ => new Color(0.08f, 1.45f, 1.8f)
            };
        }

        internal bool TryReadProjectile(int slot, out int penetrations, out int ricochets, out float damage, out float distance)
        {
            ref var projectile = ref _live[slot];
            penetrations = projectile.Penetrations;
            ricochets = projectile.Ricochets;
            damage = projectile.Damage * projectile.Retention;
            distance = projectile.Travelled;
            return projectile.Active;
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
            internal float Damage;
            internal ProjectileBallistics Ballistics;
            internal ProjectileTargets Targets;
            internal float Retention;
            internal int Penetrations;
            internal int Ricochets;
            internal uint Generation;
            internal bool Drone;
            internal bool Charged;
            internal bool Hypervelocity;
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
