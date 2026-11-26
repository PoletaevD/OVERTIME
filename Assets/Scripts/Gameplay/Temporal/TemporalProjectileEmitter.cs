using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalProjectileEmitter : TemporalActor
    {
        private const int MaxCollisionIterations = 6;
        private const float SurfaceSkin = 0.004f;
        private const float GoldenAngle = 2.39996323f;
        private const float ReticleStartAdvance = 0.015f;

        [SerializeField] private Transform[] _projectiles;
        [SerializeField] private bool _automatic = true;
        [SerializeField, Min(0.05f)] private float _interval = 0.48f;
        [SerializeField, Min(0.05f)] private float _lifetime = 1.45f;
        [SerializeField, Min(0.1f)] private float _speed = 8f;
        [SerializeField] private TemporalDirector _combatDirector;
        [SerializeField, Min(1)] private int _damage = 15;
        [SerializeField, Min(0.01f)] private float _radius = 0.13f;
        [SerializeField] private LayerMask _impactLayers = 1;

        private readonly RaycastHit[] _sweepHits = new RaycastHit[128];
        private readonly Collider[] _overlaps = new Collider[64];
        private bool _playerPool;
        private ProjectileFlightVisual[] _visuals;
        private Projectile[] _live;
        private Projectile[] _history;
        private int[] _splitQueue;
        private EmitterFrame[] _emitterHistory;
        private float _untilEmission;
        private uint _generation;
        private int _activeCount;

        internal uint Generation => _generation;
        internal int PoolCapacity => _projectiles.Length;

        internal void ConfigurePlayerPool(TemporalDirector director, int capacity)
        {
            _playerPool = true;
            _automatic = false;
            _combatDirector = director;
            _radius = 0.045f;
            _projectiles = new Transform[Mathf.Max(1, capacity)];
            for (var slot = 0; slot < _projectiles.Length; slot++)
            {
                var host = new GameObject($"Player Projectile {slot + 1:00}");
                host.transform.SetParent(transform, false);
                host.SetActive(false);
                _projectiles[slot] = host.transform;
            }
        }

        internal int ActiveCount => _activeCount;

        internal override void Initialize(int capacity)
        {
            var slots = _projectiles.Length;
            if (_visuals == null)
            {
                var flightAssets = new ProjectileFlightVisual.Assets();
                _visuals = new ProjectileFlightVisual[slots];
                for (var slot = 0; slot < slots; slot++)
                {
                    _visuals[slot] = new ProjectileFlightVisual(_projectiles[slot], flightAssets, _playerPool);
                }
            }

            _live = new Projectile[slots];
            _history = new Projectile[capacity * slots];
            _splitQueue = new int[slots];
            _emitterHistory = new EmitterFrame[capacity];
            ResetState();
        }

        internal override void ReleaseHistory()
        {
            _history = null;
            _emitterHistory = null;
        }

        internal void ClearProjectiles(bool releaseRecordedOwners = true)
        {
            ResetState();
            if (releaseRecordedOwners)
            {
                Array.Clear(_history, 0, _history.Length);
            }
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
            var pendingSplits = 0;
            for (var slot = 0; slot < _live.Length; slot++)
            {
                ref var projectile = ref _live[slot];
                if (!projectile.Active)
                {
                    continue;
                }

                var direction = projectile.Direction;
                var blastSpent = projectile.BlastSpent;
                if (projectile.Ballistics.Acceleration > 0f)
                {
                    projectile.Speed = Mathf.Min(projectile.InitialSpeed * 2.35f, projectile.Speed + projectile.InitialSpeed * projectile.Ballistics.Acceleration * step);
                }

                var distance = Mathf.Min(projectile.Range, projectile.Speed * Mathf.Min(step, projectile.Remaining));
                var split = projectile.Split ? 0f : projectile.Ballistics.SplitDistance;
                if (split > projectile.Travelled)
                {
                    distance = Mathf.Min(distance, split - projectile.Travelled);
                }
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

                if (projectile.Active && split > 0f && projectile.Travelled + 0.0001f >= split)
                {
                    _splitQueue[pendingSplits++] = slot;
                }

                Apply(slot, projectile.BlastSpent != blastSpent, projectile.Direction != direction);
            }

            for (var index = 0; index < pendingSplits; index++)
            {
                Split(_splitQueue[index]);
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

        internal bool TryLaunchVolley(Ray aim, Vector3 aimUp, Vector3 muzzleOffset, float speed, float lifetime, float range, float radius, float damage, int layers, bool charged, bool hypervelocity, in ProjectileBallistics ballistics, int pellets, float spread, float twinDamage, float twinOffset, float fanSpread = 0f)
        {
            var twinCount = twinDamage > 0f ? 1 : 0;
            var count = pellets + twinCount + (fanSpread > 0f ? 2 : 0);
            if (PoolCapacity - ActiveCount < count)
            {
                return false;
            }

            Physics.SyncTransforms();
            var owner = _combatDirector.PlayerCapsule.transform;
            // Keep the launch point on the reticle. A blocked launch remains at the eye
            // and resolves on the first swept physics step.
            var origin = aim.origin;
            if (!FindOverlap(origin, radius, layers, owner, out _, out _, out var overlapFull) && !overlapFull && !FindHit(origin, radius, aim.direction, ReticleStartAdvance, layers, owner, out _, out var castFull) && !castFull)
            {
                origin = aim.GetPoint(ReticleStartAdvance);
            }

            var pattern = Quaternion.LookRotation(aim.direction, aimUp);
            var projectile = new Projectile
            {
                Position = origin,
                Speed = speed,
                InitialSpeed = speed,
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

                if (next < pellets)
                {
                    projectile.Direction = PelletDirection(pattern, next, pellets, spread);
                    projectile.Damage = damage;
                }
                else
                {
                    var duplicate = next == pellets && twinCount > 0;
                    var yaw = duplicate ? twinOffset : next == count - 1 ? fanSpread : -fanSpread;
                    projectile.Direction = pattern * (Quaternion.Euler(duplicate ? -twinOffset * 0.5f : 0f, yaw, 0f) * Vector3.forward);
                    projectile.Damage = damage * (duplicate ? twinDamage : 0.55f);
                }
                LaunchAt(slot, projectile);
                next++;
            }

            return true;
        }

        internal void LaunchKnifeWave(Ray aim, float range, float damage, in RoomTargetMask excluded)
        {
            var owner = _combatDirector.PlayerCapsule.transform;
            TryLaunch(new Projectile
            {
                Position = aim.origin,
                Direction = aim.direction,
                Speed = 27f,
                InitialSpeed = 27f,
                Remaining = range / 27f + 0.01f,
                Range = range,
                Radius = 0.23f,
                Damage = damage,
                Owner = owner,
                Layers = Physics.DefaultRaycastLayers,
                RoomExclusions = excluded,
                Ballistics = new ProjectileBallistics(WeaponId.Directive, 1, 0, 0f, 0f, 0f, 0f, 0f, 0f, melee: true),
                Retention = 1f
            });
        }

        internal void Explode(Vector3 center, float radius, float damage, Transform owner, bool melee, ICombatDamageable excluded = null, ProjectileTargets targets = default)
        {
            _combatDirector.PublishImpact(new ProjectileImpact(center, Vector3.up, default, false, false, true, melee: melee, explosion: true));
            foreach (var actor in _combatDirector.WorldActors)
            {
                var body = actor switch
                {
                    SecurityEnemy enemy when enemy.Health.Alive && enemy.EncounterReady => enemy.Body,
                    SupervisorBoss boss when boss.Health.Alive && boss.State.Started => boss.BlastHitbox(center),
                    _ => null
                };
                if (body == null || actor is not ICombatDamageable target || ReferenceEquals(target, excluded) || (actor is SupervisorBoss && targets.Contains(target.RewardId)))
                {
                    continue;
                }

                var sample = actor is SupervisorBoss ? body.ClosestPoint(center) : body.bounds.center;
                var delta = sample - center;
                var distance = delta.magnitude;
                if (distance > radius || distance < 0.0001f)
                {
                    continue;
                }

                var blocked = FindHit(center, 0f, delta / distance, distance, Physics.DefaultRaycastLayers, owner, out var hit, out var full);
                if (full || (blocked && (hit.collider.TryGetComponent<CombatShield>(out _) || !ReferenceEquals(hit.collider.GetComponentInParent<ICombatDamageable>(), target))))
                {
                    continue;
                }

                var amount = damage * Mathf.Lerp(1f, 0.35f, distance / radius);
                var result = _combatDirector.DamageTarget(target, actor is SupervisorBoss && blocked ? hit.collider : body, amount, out var weak, melee, sample, new Color(1f, 0.52f, 0.19f));
                if (result.Amount > 0)
                {
                    _combatDirector.PublishImpact(new ProjectileImpact(sample, -delta.normalized, result, weak, false, true, melee: melee));
                }
            }
        }

        private void Split(int slot)
        {
            ref var parent = ref _live[slot];
            parent.Split = true;
            if (PoolCapacity - ActiveCount < 2)
            {
                return;
            }

            parent.Damage *= 0.55f;
            parent.Radius *= 0.72f;
            parent.BlastSpent = true;
            var frame = Quaternion.LookRotation(parent.Direction);
            var child = parent;
            child.Direction = frame * (Quaternion.Euler(0f, -10f, 0f) * Vector3.forward);
            TryLaunch(child);
            child.Direction = frame * (Quaternion.Euler(0f, 10f, 0f) * Vector3.forward);
            TryLaunch(child);
            Apply(slot, true);
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
            projectile.SegmentDistance = 0f;
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
            projectile.SegmentDistance += distance;
        }

        private bool FindImpact(in Projectile projectile, float distance, int layers, out Collider collider, out Vector3 point, out Vector3 normal, out float travelled)
        {
            travelled = 0f;
            if (FindOverlap(projectile.Position, projectile.Radius, layers, projectile.Owner, out collider, out point, out var overlapFull, projectile.Targets, projectile.RoomExclusions) || overlapFull)
            {
                normal = (projectile.Position - point).normalized;
                return true;
            }

            if (FindHit(projectile.Position, projectile.Radius, projectile.Direction, distance, layers, projectile.Owner, out var hit, out var castFull, projectile.Targets, projectile.RoomExclusions) || castFull)
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

            var incoming = projectile.Direction;
            var damage = default(DamageResult);
            var weakPoint = false;
            var shielded = false;
            var pierced = false;
            var reflected = false;
            ICombatDamageable directTarget = null;
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
                    directTarget = target;
                    damage = _combatDirector.DamageTarget(target, collider, amount, out weakPoint, projectile.Ballistics.Melee, point);
                    if (damage.Amount > 0 && projectile.Ballistics.ChillSeconds > 0f)
                    {
                        if (target is SecurityEnemy enemy)
                        {
                            enemy.ApplyChill(projectile.Ballistics.ChillSeconds);
                        }
                        else if (target is SupervisorBoss boss)
                        {
                            boss.ApplyChill(projectile.Ballistics.ChillSeconds);
                        }
                    }
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
                    projectile.SegmentDistance = 0f;
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

            if (!shielded && !projectile.BlastSpent && projectile.Ballistics.ExplosionRadius > 0f)
            {
                projectile.BlastSpent = true;
                Explode(point + normal * 0.045f, projectile.Ballistics.ExplosionRadius, projectile.Damage * projectile.Retention * 0.65f, projectile.Owner, false, directTarget, projectile.Targets);
            }

            _combatDirector.PublishImpact(new ProjectileImpact(point, normal, damage, weakPoint, shielded, _playerPool, reflected, projectile.Ballistics.TargetLimit > 1 && damage.Amount > 0, melee: projectile.Ballistics.Melee, incoming: incoming, chilled: damage.Amount > 0 && projectile.Ballistics.ChillSeconds > 0f));
            return pierced || reflected;
        }

        private bool FindHit(Vector3 origin, float radius, Vector3 direction, float distance, int layers, Transform owner, out RaycastHit closest, out bool full, ProjectileTargets targets = default, RoomTargetMask exclusions = default)
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
                if (candidate.distance <= best && IsValid(candidate.collider, owner, targets, exclusions) && (candidate.distance < best || candidate.collider.TryGetComponent<CombatShield>(out _)))
                {
                    closest = candidate;
                    best = candidate.distance;
                }
            }

            return closest.collider != null;
        }

        private bool FindOverlap(Vector3 origin, float radius, int layers, Transform owner, out Collider closest, out Vector3 point, out bool full, ProjectileTargets targets = default, RoomTargetMask exclusions = default)
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
                if (!IsValid(candidate, owner, targets, exclusions))
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

        private bool IsValid(Collider collider, Transform owner, ProjectileTargets targets, RoomTargetMask exclusions)
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

            var target = collider.GetComponentInParent<ICombatDamageable>();
            if (_playerPool && target is not SupervisorBoss && collider.TryGetComponent<CombatShield>(out _))
            {
                return true;
            }

            return target == null || (_playerPool && !targets.Contains(target.RewardId) && !exclusions.Contains(target.RewardId));
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
            var view = _playerPool ? _combatDirector.PlayerAim : default;
            var rewind = timeline.Direction == TemporalDirection.Reverse;
            for (var slot = 0; slot < _live.Length; slot++)
            {
                ref readonly var current = ref _live[slot];
                if (!current.Active)
                {
                    continue;
                }

                var position = current.Position;
                var segment = current.SegmentDistance;
                if (timeline.PreviewFraction > 0f)
                {
                    ref readonly var older = ref _history[timeline.Slot(timeline.Cursor - 1) * _live.Length + slot];
                    if (older.Active && older.Generation == current.Generation &&
                        Vector3.Dot(older.Direction, current.Direction) > 0.999f)
                    {
                        position = Vector3.Lerp(position, older.Position, timeline.PreviewFraction);
                        segment = Mathf.Lerp(segment, older.SegmentDistance, timeline.PreviewFraction);
                    }
                }

                _visuals[slot].Present(position, current.Direction, segment, current.Speed, view, showTraces, rewind, mode == TemporalMode.BulletTime);
            }
        }

        private void Apply(int slot, bool refreshStyle = false, bool redirect = false)
        {
            ref readonly var projectile = ref _live[slot];
            var root = _projectiles[slot];
            if (root.gameObject.activeSelf != projectile.Active)
            {
                root.gameObject.SetActive(projectile.Active);
            }

            if (!projectile.Active)
            {
                return;
            }

            var visual = _visuals[slot];
            if (refreshStyle)
            {
                var form = FlightForm(projectile);
                var radius = form == ProjectileFlightVisual.Form.KnifeWave ? 0.23f : _radius;
                var hot = projectile.Charged || projectile.Hypervelocity || projectile.Ballistics.Overload || form == ProjectileFlightVisual.Form.Blast;
                visual.SetStyle(form, projectile.Radius, radius, hot);
            }

            if (refreshStyle || redirect)
            {
                visual.SetPose(projectile.Position, projectile.Direction);
            }
            else
            {
                root.position = projectile.Position;
            }
        }

        private ProjectileFlightVisual.Form FlightForm(in Projectile projectile)
        {
            if (!_playerPool)
            {
                return projectile.Drone ? ProjectileFlightVisual.Form.Drone : ProjectileFlightVisual.Form.Sentinel;
            }

            var ballistics = projectile.Ballistics;
            if (ballistics.Melee)
            {
                return ProjectileFlightVisual.Form.KnifeWave;
            }

            if (ballistics.ExplosionRadius > 0f && !projectile.BlastSpent)
            {
                return ProjectileFlightVisual.Form.Blast;
            }

            return ballistics.Weapon switch
            {
                WeaponId.Termination => ballistics.Slug ? ProjectileFlightVisual.Form.Slug : ProjectileFlightVisual.Form.Pellet,
                WeaponId.Deadline => ProjectileFlightVisual.Form.Deadline,
                WeaponId.Directive => ProjectileFlightVisual.Form.Directive,
                _ => ProjectileFlightVisual.Form.Compliance
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

        internal bool TryReadFlight(int slot, out Ray ray, out float speed, out bool split)
        {
            ref var projectile = ref _live[slot];
            ray = new Ray(projectile.Position, projectile.Direction);
            speed = projectile.Speed;
            split = projectile.Split;
            return projectile.Active;
        }

        private struct Projectile
        {
            internal Vector3 Position;
            internal Vector3 Direction;
            internal float Speed;
            internal float InitialSpeed;
            internal float Remaining;
            internal float Range;
            internal float Travelled;
            internal float SegmentDistance;
            internal int Layers;
            internal Transform Owner;
            internal float Radius;
            internal float Damage;
            internal ProjectileBallistics Ballistics;
            internal ProjectileTargets Targets;
            internal RoomTargetMask RoomExclusions;
            internal float Retention;
            internal int Penetrations;
            internal int Ricochets;
            internal uint Generation;
            internal bool Drone;
            internal bool Charged;
            internal bool Hypervelocity;
            internal bool Hit;
            internal bool Split;
            internal bool BlastSpent;
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
