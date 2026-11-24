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

        internal SecurityEnemy Enemy => _enemy;
        internal Transform Point => _point;
        internal Transform[] Patrol => _patrol;
        internal Vector3 FlightHalfExtents => _flightHalfExtents;
    }

    internal sealed class RoomRuntime : MonoBehaviour
    {
        [SerializeField] private EncounterController _encounter;
        [SerializeField] private Transform _entry;
        [SerializeField] private Transform _exit;
        [SerializeField] private Vector2 _socketOpening = new(4.8f, 3.6f);
        [SerializeField] private EnemySpawn[] _markers;
        [SerializeField] private TemporalActor[] _worldActors = Array.Empty<TemporalActor>();

        internal EncounterController Encounter => _encounter;
        internal Transform Entry => _entry;
        internal Transform Exit => _exit;
        internal Vector2 SocketOpening => _socketOpening;
        internal EnemySpawn[] Markers => _markers;

        internal TemporalActor[] Prepare(RouteNode node, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            var markers = node.Room.Variants[node.Variant].Markers;
            var enemies = new SecurityEnemy[markers.Length];
            var actors = new TemporalActor[1 + markers.Length + _worldActors.Length];
            actors[0] = pool;
            _worldActors.CopyTo(actors, 1 + markers.Length);

            for (var index = 0; index < markers.Length; index++)
            {
                var markerIndex = markers[index];
                var marker = _markers[markerIndex];
                var enemy = Instantiate(marker.Enemy, marker.Point.position, marker.Point.rotation, transform);
                var rewardId = node.Id * 10000 + node.Variant * 100 + markerIndex + 1;
                enemy.BindEncounter(director, pool, rewardId, marker.Patrol, marker.FlightHalfExtents);
                enemies[index] = enemy;
                actors[index + 1] = enemy;
            }

            _encounter.AssignEnemies(enemies);
            _encounter.Prepare();
            return actors;
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
            Gizmos.DrawWireSphere(_exit.position, 0.42f);
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
