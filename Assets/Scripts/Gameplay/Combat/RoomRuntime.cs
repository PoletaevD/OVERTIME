using System;
using UnityEngine;

namespace Overtime
{
    [Serializable]
    internal sealed class EnemySpawn
    {
        [SerializeField] private SecurityEnemy _enemy;
        [SerializeField] private Transform _point;
        [SerializeField] private Transform[] _patrol;
        [SerializeField] private Vector3 _flightHalfExtents;
        [SerializeField] private GameObject _warning;

        internal SecurityEnemy Enemy => _enemy;
        internal Transform Point => _point;
        internal Transform[] Patrol => _patrol;
        internal Vector3 FlightHalfExtents => _flightHalfExtents;
        internal GameObject Warning => _warning;
    }

    internal sealed class RoomRuntime : MonoBehaviour
    {
        [SerializeField] private EncounterController _encounter;
        [SerializeField] private Transform _entry;
        [SerializeField] private Transform _exit;
        [SerializeField] private Vector2 _socketOpening = new(4.8f, 3.6f);
        [SerializeField] private EnemySpawn[] _markers;
        [SerializeField] private Transform[] _ammoSockets = Array.Empty<Transform>();
        [SerializeField] private TemporalActor[] _worldActors = Array.Empty<TemporalActor>();

        internal EncounterController Encounter => _encounter;
        internal Transform Entry => _entry;
        internal Transform Exit => _exit;
        internal Vector2 SocketOpening => _socketOpening;
        internal EnemySpawn[] Markers => _markers;
        internal Transform[] AmmoSockets => _ammoSockets;
        internal float EntryHeight => transform.InverseTransformPoint(_entry.position).y;

        internal Pose ConnectEntrance(Pose socket)
        {
            var localRotation = Quaternion.Inverse(transform.rotation) * _entry.rotation;
            var localPoint = transform.InverseTransformPoint(_entry.position);
            var exposedOpening = new Vector3(localPoint.x, 0f, 0f);
            var rotation = socket.rotation * Quaternion.Inverse(localRotation);
            return new Pose(socket.position - rotation * exposedOpening, rotation);
        }

        internal Bounds ArenaBounds(Pose placement)
        {
            var zone = _encounter.CombatZone;
            var center = transform.InverseTransformPoint(zone.transform.TransformPoint(zone.center));
            var extents = zone.size * 0.5f;
            var entry = transform.InverseTransformPoint(_entry.position);
            var exit = transform.InverseTransformPoint(_exit.position);
            var minimum = new Vector3(center.x - extents.x - 0.62f, -0.78f, entry.z - 2.2f);
            var maximum = new Vector3(center.x + extents.x + 0.62f, center.y + extents.y + 0.72f, exit.z + 0.28f);
            var bounds = new Bounds(placement.position + placement.rotation * minimum, Vector3.zero);
            for (var corner = 1; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) != 0 ? maximum.x : minimum.x, (corner & 2) != 0 ? maximum.y : minimum.y, (corner & 4) != 0 ? maximum.z : minimum.z);
                bounds.Encapsulate(placement.position + placement.rotation * point);
            }

            return bounds;
        }

        internal bool HasEntered(Vector3 position)
        {
            var point = transform.InverseTransformPoint(position);
            return point.z >= 3.75f && _encounter.CombatZone.bounds.Contains(position);
        }

        internal void SetArrivalSeal(bool closed)
        {
            transform.Find("Architecture/Arrival Door").gameObject.SetActive(closed);
        }

        internal TemporalActor[] Prepare(RouteNode node, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            var variant = node.Room.Variants[node.Variant];
            var markers = variant.Markers;
            var enemies = new SecurityEnemy[markers.Length];
            var signals = new GameObject[markers.Length];
            var reinforcement = gameObject.AddComponent<EncounterReinforcements>();
            var actors = new TemporalActor[2 + markers.Length + _worldActors.Length];
            actors[0] = pool;
            actors[1] = reinforcement;
            _worldActors.CopyTo(actors, 2);
            var hazardIndex = 0;
            foreach (var actor in _worldActors)
            {
                if (actor is ArenaMechanism mechanism)
                {
                    mechanism.Bind(director, _encounter);
                }
                else if (actor is ArenaHazard hazard)
                {
                    hazard.Bind(director, _encounter, (variant.HazardMask & (1 << hazardIndex)) != 0);
                    hazardIndex++;
                }
            }

            for (var index = 0; index < markers.Length; index++)
            {
                var markerIndex = markers[index];
                var marker = _markers[markerIndex];
                var enemy = Instantiate(marker.Enemy, marker.Point.position, marker.Point.rotation, transform);
                var rewardId = node.Id * 10000 + node.Variant * 100 + markerIndex + 1;
                enemy.BindEncounter(director, pool, rewardId, marker.Patrol, marker.FlightHalfExtents);
                enemies[index] = enemy;
                signals[index] = marker.Warning;
                actors[2 + _worldActors.Length + index] = enemy;
            }

            reinforcement.Configure(enemies, signals, variant, director);
            _encounter.AssignEnemies(enemies);
            _encounter.SetReinforcements(reinforcement);
            _encounter.Prepare();
            return actors;
        }

        internal TemporalActor[] WorldActors => _worldActors;

        internal int HazardCount(int mask)
        {
            var count = 0;
            var index = 0;
            foreach (var actor in _worldActors)
            {
                if (actor is ArenaHazard)
                {
                    count += (mask >> index) & 1;
                    index++;
                }
            }

            return count;
        }

        internal void DisableHazards()
        {
            foreach (var actor in _worldActors)
            {
                if (actor is ArenaHazard hazard)
                {
                    hazard.Disarm();
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_markers == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_entry.position, 0.42f);
            Gizmos.DrawRay(_entry.position, _entry.forward * 1.8f);
            Gizmos.DrawWireSphere(_exit.position, 0.42f);
            Gizmos.DrawRay(_exit.position, _exit.forward * 1.8f);
            var zone = _encounter.CombatZone;
            var matrix = Gizmos.matrix;
            Gizmos.matrix = zone.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(zone.center, zone.size);
            Gizmos.matrix = matrix;
            Gizmos.color = Color.yellow;
            foreach (var socket in _ammoSockets)
            {
                Gizmos.DrawWireSphere(socket.position, 0.27f);
            }

            Gizmos.DrawWireSphere(_exit.position - _exit.forward * 1.7f + _exit.right * 1.3f, 0.46f);
            Gizmos.DrawWireSphere(_exit.position - _exit.forward * 1.7f - _exit.right * 1.3f, 0.46f);
            foreach (var marker in _markers)
            {
                if (marker.Point == null || marker.Enemy == null)
                {
                    continue;
                }

                Gizmos.color = marker.Enemy.Kind == SecurityEnemyKind.Drone ? Color.cyan : new Color(1f, 0.5f, 0.2f);
                var position = marker.Point.position;
                Gizmos.DrawWireSphere(position, 0.35f);
                Gizmos.DrawRay(position, marker.Point.forward * 0.9f);
                if (marker.Enemy.Kind == SecurityEnemyKind.Drone)
                {
                    Gizmos.DrawWireCube(position, marker.FlightHalfExtents * 2f);
                }

                foreach (var waypoint in marker.Patrol)
                {
                    Gizmos.DrawLine(position, waypoint.position);
                    position = waypoint.position;
                }
            }
        }
#endif
    }
}
