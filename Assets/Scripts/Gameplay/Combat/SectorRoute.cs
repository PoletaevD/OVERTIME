using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overtime
{
    internal sealed class SectorRoute : MonoBehaviour
    {
        [SerializeField] private EncounterController[] _rooms;
        [SerializeField] private TemporalProjectileEmitter _enemyProjectiles;

        internal int RoomIndex { get; private set; }
        internal EncounterController Current => _rooms[RoomIndex];
        internal int EnemiesDefeated { get; private set; }
        internal int PowersUsed { get; private set; }
        internal double Elapsed { get; private set; }
        internal bool Finished { get; private set; }
        internal TemporalProjectileEmitter EnemyProjectiles => _enemyProjectiles;

        internal TemporalActor[] Prepare()
        {
            foreach (var room in _rooms)
            {
                room.Prepare();
            }

            return RoomActors();
        }

        internal void Advance(double elapsed, TemporalDirector director, PlaygroundPlayer player)
        {
            Elapsed += elapsed;
            var before = Current.State;
            Current.Advance(director, player);
            if (before == Current.State)
            {
                return;
            }

            if (Current.State == EncounterState.Cleared)
            {
                EnemiesDefeated += Current.Enemies.Length;
            }
            else if (Current.State == EncounterState.Completed)
            {
                if (RoomIndex + 1 == _rooms.Length)
                {
                    director.SealHistory();
                    Finished = true;
                    player.SetGameOver(true);
                    return;
                }

                RoomIndex++;
                director.ReplaceEncounter(RoomActors());
            }
        }

        internal void CountMode(TemporalMode mode)
        {
            if (mode != TemporalMode.Normal)
            {
                PowersUsed++;
            }
        }

        internal void Restart()
        {
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        private TemporalActor[] RoomActors()
        {
            var enemies = Current.Enemies;
            var actors = new TemporalActor[enemies.Length + 1];
            enemies.CopyTo(actors, 1);
            actors[0] = _enemyProjectiles;
            return actors;
        }
    }
}
