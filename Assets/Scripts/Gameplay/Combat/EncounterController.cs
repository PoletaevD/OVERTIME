using UnityEngine;

namespace Overtime
{
    internal enum EncounterState
    {
        Waiting,
        Active,
        Cleared,
        Completed,
        RewardSelection
    }

    internal sealed class EncounterController : MonoBehaviour
    {
        [SerializeField] private string _roomName;
        [SerializeField] private SecurityEnemy[] _enemies;
        [SerializeField] private BoxCollider _combatZone;
        [SerializeField] private BoxCollider _exitZone;
        [SerializeField] private EncounterGate _gate;
        [SerializeField] private int _reserveReward = 30;

        internal string RoomName => _roomName;
        internal Vector3 Exit => _gate.transform.position;
        internal EncounterState State { get; private set; }
        internal SecurityEnemy[] Enemies => _enemies;

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

        internal void Advance(TemporalDirector director, PlaygroundPlayer player, SectorRoute route)
        {
            var capsule = player.Capsule;
            var position = capsule.transform.TransformPoint(capsule.center);
            switch (State)
            {
                case EncounterState.Waiting when director.HasLivingPlayer && _combatZone.bounds.Contains(position):
                    foreach (var enemy in _enemies)
                    {
                        enemy.SetEncounterReady(true);
                    }

                    director.SealHistory();
                    State = EncounterState.Active;
                    break;
                case EncounterState.Active when director.HasLivingPlayer && LivingEnemies == 0:
                    State = EncounterState.RewardSelection;
                    director.SealHistory();
                    player.Combat.Weapon.AddReserve(_reserveReward);
                    if (!route.OpenRoomReward())
                    {
                        director.SealHistory();
                        UnlockExit(player);
                    }
                    break;
                case EncounterState.Cleared when _exitZone.bounds.Contains(position):
                    _gate.SetOpen(false, player.ReducedMotion);
                    State = EncounterState.Completed;
                    break;
            }
        }
    }
}
