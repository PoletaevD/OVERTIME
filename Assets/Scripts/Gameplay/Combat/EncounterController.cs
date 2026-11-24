using UnityEngine;

namespace Overtime
{
    internal enum EncounterState
    {
        Waiting,
        Active,
        Cleared,
        Completed,
        RewardSelection,
        RouteSelection
    }

    internal sealed class EncounterController : MonoBehaviour
    {
        [SerializeField] private string _roomName;
        [SerializeField] private SecurityEnemy[] _enemies;
        [SerializeField] private BoxCollider _combatZone;
        [SerializeField] private BoxCollider _exitZone;
        [SerializeField] private EncounterGate _gate;
        [SerializeField] private int _reserveReward = 30;

        private EncounterReinforcements _reinforcements;

        internal int PendingReinforcements => _reinforcements?.Pending ?? 0;
        internal int ActiveEnemies
        {
            get
            {
                var count = 0;
                foreach (var enemy in _enemies)
                {
                    count += enemy.EncounterReady && enemy.Health.Alive ? 1 : 0;
                }

                return count;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugReleaseAll()
        {
            _reinforcements?.DebugReleaseAll();
        }
#endif

        internal void SetReinforcements(EncounterReinforcements reinforcements)
        {
            _reinforcements = reinforcements;
        }

        internal string RoomName => _roomName;
        internal Vector3 Exit => _gate.transform.position;
        internal EncounterState State { get; private set; }
        internal SecurityEnemy[] Enemies => _enemies;
        internal int ReserveReward => _reserveReward;
        internal BoxCollider CombatZone => _combatZone;
        internal BoxCollider ExitZone => _exitZone;
        internal EncounterGate Gate => _gate;

        internal void AssignEnemies(SecurityEnemy[] enemies)
        {
            _enemies = enemies;
        }

        internal void RequestRoute()
        {
            State = EncounterState.RouteSelection;
        }

        internal void Complete()
        {
            State = EncounterState.Completed;
        }

        internal int LivingEnemies
        {
            get
            {
                var count = 0;
                foreach (var enemy in _enemies)
                {
                    if (enemy.Health.Alive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal void Prepare()
        {
            foreach (var enemy in _enemies)
            {
                enemy.SetEncounterReady(false);
                enemy.SetNeighbors(_enemies);
            }

            _gate.SetOpen(false, true);
        }

        internal void UnlockExit(PlaygroundPlayer player)
        {
            State = EncounterState.Cleared;
            _gate.SetOpen(true, player.ReducedMotion);
        }

        internal void Advance(TemporalDirector director, PlaygroundPlayer player)
        {
            var capsule = player.Capsule;
            var position = capsule.transform.TransformPoint(capsule.center);
            switch (State)
            {
                case EncounterState.Waiting when director.HasLivingPlayer && _combatZone.bounds.Contains(position):
                    if (_reinforcements != null)
                    {
                        _reinforcements.Begin();
                    }
                    else
                    {
                        foreach (var enemy in _enemies)
                        {
                            enemy.SetEncounterReady(true);
                        }
                    }

                    State = EncounterState.Active;
                    director.SealHistory();
                    break;
                case EncounterState.Active when director.HasLivingPlayer && LivingEnemies == 0 && PendingReinforcements == 0:
                    State = EncounterState.RewardSelection;
                    break;
                case EncounterState.Cleared when _exitZone.bounds.Contains(position):
                    _gate.SetOpen(false, player.ReducedMotion);
                    State = EncounterState.Completed;
                    break;
            }
        }
    }
}
