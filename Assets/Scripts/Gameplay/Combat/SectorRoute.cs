using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overtime
{
    internal sealed class SectorRoute : MonoBehaviour
    {
        [SerializeField] private EncounterController[] _rooms;
        [SerializeField] private RoomDefinition[] _library;
        [SerializeField] private RouteTransfer _transfer;
        [SerializeField] private TemporalProjectileEmitter _enemyProjectiles;
        [SerializeField] private RunProgression _progression;
        [SerializeField] private WeaponDefinition[] _secondaryOptions;
        [SerializeField, Min(0f)] private float _lightEnemyEnergy = 10f;
        [SerializeField, Min(0f)] private float _heavyEnemyEnergy = 17f;

        private readonly HashSet<int> _paidRooms = new();
        private readonly RouteNode[] _visited = new RouteNode[RoutePlan.RoomCount];
        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private RoomRuntime _activeRoom;
        private RouteNode _next;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _debugReward;
#endif

        internal RoomRuntime ActiveRoom => _activeRoom;
        internal CombatWeapon Weapon => _player.Combat.Weapon;

        internal float KillEnergy(SecurityEnemyKind kind)
        {
            return kind is SecurityEnemyKind.Auditor or SecurityEnemyKind.Enforcer ? _heavyEnemyEnergy : _lightEnemyEnergy;
        }

        internal bool SelectingSecondary { get; private set; }
        internal WeaponDefinition[] SecondaryOptions => _secondaryOptions;
        internal int RoomIndex { get; private set; }
        internal int RoomCount => Generated ? RoutePlan.RoomCount : _rooms.Length;
        internal RunProgression Progression => _progression;
        internal EncounterController Current => Generated ? _activeRoom.Encounter : _rooms[RoomIndex];
        internal RoutePlan Plan { get; private set; }
        internal RouteNode CurrentNode => _visited[RoomIndex];
        internal IReadOnlyList<RouteNode> Visited => _visited;
        internal bool Generated => _library is { Length: > 0 };
        internal bool SelectingRoute => Generated && Current.State == EncounterState.RouteSelection;
        internal bool Transferring { get; private set; }
        internal IReadOnlyList<RouteNode> RouteOptions => Plan.At(RoomIndex + 1);
        internal int EnemiesDefeated { get; private set; }
        internal int PowersUsed { get; private set; }
        internal double Elapsed { get; private set; }
        internal bool Finished { get; private set; }
        internal TemporalProjectileEmitter EnemyProjectiles => _enemyProjectiles;

        internal TemporalActor[] Prepare(TemporalDirector director, PlaygroundPlayer player)
        {
            _director = director;
            _player = player;
            if (Generated)
            {
                Plan = new RouteGenerator(_progression.Seed, _library).Generate();
                return PrepareRoom(Plan.At(0)[0]);
            }

            foreach (var room in _rooms)
            {
                room.Prepare();
            }

            return FixedRoomActors();
        }

        internal void Advance(double elapsed)
        {
            Elapsed += elapsed;
            var room = Current;
            var before = room.State;
            room.Advance(_director, _player);
            if (before == room.State)
            {
                return;
            }

            if (room.State == EncounterState.RewardSelection)
            {
                ClearRoom(room);
            }
            else if (room.State == EncounterState.Completed)
            {
                if (RoomIndex + 1 == RoomCount)
                {
                    _director.SealHistory();
                    CompleteSector();
                    return;
                }

                if (Generated)
                {
                    BeginTransfer(_next, RoomIndex + 1);
                }
                else
                {
                    RoomIndex++;
                    _director.ReplaceEncounter(FixedRoomActors());
                }
            }
        }

        internal void StartRun()
        {
            if (_secondaryOptions is { Length: > 0 })
            {
                SelectingSecondary = true;
                _director.SetRewardSelection(true);
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

        internal bool ConfirmUpgrade(int card)
        {
            if (SelectingSecondary || SelectingRoute || !_director.RewardSelection || !_progression.Confirm(card))
            {
                return false;
            }

            _director.SealHistory();
            if (Current.State == EncounterState.RewardSelection)
            {
                ResolveReward();
            }
            else
            {
                _director.SetRewardSelection(false);
            }

            return true;
        }

        internal bool ConfirmRoute(int choice)
        {
            if (!SelectingRoute || choice < 0 || choice >= RouteOptions.Count)
            {
                return false;
            }

            _next = RouteOptions[choice];
            Current.UnlockExit(_player);
            _director.SetRewardSelection(false);
            return true;
        }

        private void ClearRoom(EncounterController room)
        {
            _activeRoom?.DisableHazards();
            _director.SealHistory();
            var id = Generated ? CurrentNode.Id : RoomIndex + 1;
            var reward = Generated ? CurrentNode.Reward : RoomReward.None;
            if (_paidRooms.Add(id))
            {
                EnemiesDefeated += room.Enemies.Length;
                _player.Combat.Weapon.ReplenishRoom(room.ReserveReward);
                GrantSupplies(reward);
                _director.CommitPlayerBaseline();
            }

            if (Generated && RoomIndex + 1 == RoomCount)
            {
                room.Complete();
                CompleteSector();
                return;
            }

            if (RoomIndex + 1 == RoomCount || !OpenReward(id, reward == RoomReward.PriorityProtocol))
            {
                ResolveReward();
            }
        }

        private void GrantSupplies(RoomReward reward)
        {
            switch (reward)
            {
                case RoomReward.MedicalSupplies:
                    var health = _player.Combat.Health;
                    health.Restore(health.Current + 25);
                    break;
                case RoomReward.ChronoCache:
                    _director.Energy.AddEnergy(35f);
                    break;
                case RoomReward.AmmunitionCache:
                    _player.Combat.Weapon.GrantAmmoCache();
                    break;
            }
        }

        private void ResolveReward()
        {
            if (Generated && RouteOptions.Count > 1)
            {
                Current.RequestRoute();
                _director.SetRewardSelection(true);
                return;
            }

            if (Generated)
            {
                _next = RouteOptions[0];
            }

            Current.UnlockExit(_player);
            _director.SetRewardSelection(false);
        }

        private bool OpenReward(int reward, bool priority = false)
        {
            if (_progression == null || !_progression.Selection.Open(reward, priority))
            {
                return false;
            }

            _director.SetRewardSelection(true);
            return true;
        }

        private TemporalActor[] PrepareRoom(RouteNode node)
        {
            _activeRoom = Instantiate(node.Room.Prefab, transform.position, Quaternion.identity, transform);
            _activeRoom.name = node.Room.Title;
            _visited[RoomIndex] = node;
            var actors = _activeRoom.Prepare(node, _director, _enemyProjectiles);
            _player.PlaceAt(_activeRoom.Entry.position, _activeRoom.Entry.rotation);
            return actors;
        }

        private void BeginTransfer(RouteNode destination, int roomIndex)
        {
            Transferring = true;
            _director.SetTransfer(true);
            StartCoroutine(Transfer(destination, roomIndex));
        }

        private IEnumerator Transfer(RouteNode destination, int roomIndex)
        {
            var reduced = _player.ReducedMotion;
            _transfer.Fade(true, reduced);
            yield return new WaitForSecondsRealtime(reduced ? 0.05f : RouteTransfer.CloseSeconds);

            _director.DetachEncounter();
            _activeRoom.gameObject.SetActive(false);
            Destroy(_activeRoom.gameObject);
            RoomIndex = roomIndex;
            _next = null;
            _director.AttachEncounter(PrepareRoom(destination));

            _transfer.Fade(false, reduced);
            if (reduced)
            {
                yield return null;
            }
            else
            {
                yield return new WaitForSecondsRealtime(RouteTransfer.OpenSeconds);
            }

            Transferring = false;
            _director.SetTransfer(false);
        }

        private void CompleteSector()
        {
            Finished = true;
            _player.SetGameOver(true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal RoomDefinition[] DebugLibrary => _library;
        internal bool RewardClaimed(int id) => _paidRooms.Contains(id);

        internal bool DebugOffer()
        {
            if (!_director.HasLivingPlayer || Finished || _director.RewardSelection || Transferring)
            {
                return false;
            }

            _director.SealHistory();
            return OpenReward(--_debugReward);
        }

        internal bool DebugTravel(int libraryIndex, int variant)
        {
            if (!CanDebugTravel() || libraryIndex < 0 || libraryIndex >= _library.Length)
            {
                return false;
            }

            var room = _library[libraryIndex];
            if (variant < 0 || variant >= room.Variants.Length)
            {
                return false;
            }

            BeginTransfer(new RouteNode(100 + libraryIndex, RoomIndex + 1, room, variant, RoomReward.None), RoomIndex);
            return true;
        }

        internal bool DebugRestartRoom()
        {
            if (!CanDebugTravel())
            {
                return false;
            }

            BeginTransfer(CurrentNode, RoomIndex);
            return true;
        }

        internal bool DebugClearRoom()
        {
            if (!CanDebugTravel() || Current.State != EncounterState.Active || _director.Mode != TemporalMode.Normal)
            {
                return false;
            }

            Current.DebugReleaseAll();
            foreach (var enemy in Current.Enemies)
            {
                if (enemy.Health.Alive)
                {
                    _director.DamageTarget(enemy, enemy.Body, 1000f, out _);
                }
            }

            _director.Advance(0d, default);
            return true;
        }

        internal bool DebugNextRoom()
        {
            if (!CanDebugTravel() || RoomIndex + 1 >= RoomCount)
            {
                return false;
            }

            BeginTransfer(_next ?? RouteOptions[0], RoomIndex + 1);
            return true;
        }

        private bool CanDebugTravel()
        {
            return Generated && !Transferring && !Finished && !_director.RewardSelection && _director.HasLivingPlayer;
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
            var scene = gameObject.scene;
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        private TemporalActor[] FixedRoomActors()
        {
            var enemies = Current.Enemies;
            var actors = new TemporalActor[enemies.Length + 1];
            enemies.CopyTo(actors, 1);
            actors[0] = _enemyProjectiles;
            return actors;
        }
    }
}
