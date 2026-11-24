using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overtime
{
    internal sealed class SectorRoute : MonoBehaviour
    {
        [SerializeField] private EncounterController[] _rooms;
        [SerializeField] private TemporalProjectileEmitter _enemyProjectiles;
        [SerializeField] private RunProgression _progression;
        [SerializeField] private WeaponDefinition[] _secondaryOptions;

        private TemporalDirector _director;
        private PlaygroundPlayer _player;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _debugReward;
#endif

        internal bool SelectingSecondary { get; private set; }
        internal WeaponDefinition[] SecondaryOptions => _secondaryOptions;

        internal int RoomIndex { get; private set; }
        internal RunProgression Progression => _progression;
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
            Current.Advance(director, player, this);
            if (before == Current.State)
            {
                return;
            }

            if (Current.State is EncounterState.Cleared or EncounterState.RewardSelection)
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

        internal void StartRun(TemporalDirector director, PlaygroundPlayer player)
        {
            _director = director;
            _player = player;
            if (_secondaryOptions is { Length: > 0 })
            {
                SelectingSecondary = true;
                director.SetRewardSelection(true);
            }
            else
            {
                OpenReward(0);
            }
        }

        internal bool ConfirmSecondary(int choice)
        {
            if (!SelectingSecondary || choice < 0 || choice >= _secondaryOptions.Length || !_player.Combat.Weapon.EquipSecondary(_secondaryOptions[choice]))
            {
                return false;
            }

            SelectingSecondary = false;
            _director.SealHistory();
            if (!OpenReward(0))
            {
                _director.SetRewardSelection(false);
            }

            return true;
        }

        internal bool OpenRoomReward()
        {
            return RoomIndex < _rooms.Length - 1 && OpenReward(RoomIndex + 1);
        }

        internal bool ConfirmUpgrade(int card)
        {
            if (SelectingSecondary || !_director.RewardSelection || !_progression.Confirm(card))
            {
                return false;
            }

            _director.SealHistory();
            if (Current.State == EncounterState.RewardSelection)
            {
                Current.UnlockExit(_player);
            }

            _director.SetRewardSelection(false);
            return true;
        }

        private bool OpenReward(int reward)
        {
            if (_progression == null || !_progression.Selection.Open(reward))
            {
                return false;
            }

            _director.SetRewardSelection(true);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool DebugOffer()
        {
            if (!_director.HasLivingPlayer || Finished || _director.RewardSelection)
            {
                return false;
            }

            _director.SealHistory();
            return OpenReward(--_debugReward);
        }
#endif

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
