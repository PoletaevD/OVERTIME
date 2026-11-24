using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class SecurityRouteTests
    {
        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;

        [UnitySetUp]
        public IEnumerator LoadRoute()
        {
            yield return SceneManager.LoadSceneAsync("Security Route", LoadSceneMode.Single);
            yield return null;
            Bind();
        }

        [UnityTest]
        public IEnumerator BothSafeAssignmentsRetainTerminationAndFinishTheSector()
        {
            yield return NewRun(0x51A);
            yield return CompleteRoute(0);
        }

        [UnityTest]
        public IEnumerator BothHighThreatAssignmentsRetainDeadlineAndFinishTheSector()
        {
            yield return NewRun(0x54B);
            yield return CompleteRoute(1);
        }

        [UnityTest]
        public IEnumerator AllAuthoredVariantsSpawnClearOfGeometryAndReleaseTheirPredecessors()
        {
            StartRun(0);
            var roomCount = 0;
            var variantCount = 0;
            var pool = _director.PlayerProjectiles;
            for (var roomIndex = 0; roomIndex < _route.DebugLibrary.Length; roomIndex++)
            {
                var room = _route.DebugLibrary[roomIndex];
                roomCount++;
                for (var variantIndex = 0; variantIndex < room.Variants.Length; variantIndex++)
                {
                    var previous = _route.Current;
                    var previousEnemy = previous.Enemies[0];
                    Assert.That(_route.DebugTravel(roomIndex, variantIndex), Is.True);
                    yield return WaitForTransfer();
                    variantCount++;
                    Assert.That(previous == null, Is.True);
                    Assert.That(previousEnemy.HistoryCapacity, Is.Zero);
                    Assert.That(_route.CurrentNode.Room, Is.SameAs(room));
                    Assert.That(_director.PlayerProjectiles, Is.SameAs(pool));
                    Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
                    Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));

                    ValidateGeometry(room, variantIndex);
                    Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Waiting));
                    var walk = new TemporalInput(Vector2.up, Vector2.zero, false, false, false, default);
                    for (var step = 0; step < 90 && _route.Current.State == EncounterState.Waiting; step++)
                    {
                        _director.Advance(TemporalTimeline.TickDuration, walk);
                    }

                    Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Active), room.Title);
                    Assert.That(_route.Current.LivingEnemies, Is.EqualTo(room.Variants[variantIndex].Markers.Length));
                    Assert.That(Object.FindObjectsByType<RoomRuntime>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                }
            }

            Assert.That(roomCount, Is.EqualTo(8));
            Assert.That(variantCount, Is.EqualTo(23));
        }

        [UnityTest]
        public IEnumerator RouteUiAcceptsKeyboardAndPointerWithoutSwitchingWeaponsOrFiring()
        {
            using (var keyboard = new SyntheticKeyboard())
            {
                StartRun(1);
                StartFight();
                ClearFight();
                Assert.That(_route.ConfirmUpgrade(0), Is.True);
                yield return null;

                var before = _director.SimulationTick;
                var magazine = _player.Combat.Weapon.Magazine;
                var energy = _director.Energy.Current;
                _director.Advance(3d, new TemporalInput(Vector2.one, Vector2.one, true, true, true, default, fireHeld: true, weaponSelection: WeaponSlotInput.Secondary));
                Assert.That(_director.SimulationTick, Is.EqualTo(before));
                Assert.That(_director.Energy.Current, Is.EqualTo(energy));

                InputSystem.QueueStateEvent(keyboard.Device, new KeyboardState(Key.Digit2));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard.Device, new KeyboardState());
                Assert.That(_route.SelectingRoute, Is.False);
                Assert.That(_player.Combat.Weapon.ActiveSlot, Is.Zero);
                Assert.That(_player.Input.WeaponSelection, Is.EqualTo(WeaponSlotInput.None));
                Assert.That(_player.Combat.Weapon.Magazine, Is.EqualTo(magazine));
                _director.Advance(TemporalTimeline.TickDuration, default);
                Assert.That(_director.SimulationTick - before, Is.LessThanOrEqualTo(1));
                yield return ExitRoom();
                Assert.That(_route.CurrentNode, Is.SameAs(_route.Plan.At(1)[1]));

                StartFight();
                ClearFight();
                Assert.That(_route.ConfirmUpgrade(0), Is.True);
                yield return ExitRoom();
                StartFight();
                ClearFight();
                Assert.That(_route.ConfirmUpgrade(0), Is.True);
                yield return null;

                var hud = Object.FindAnyObjectByType<RouteSelectionHud>();
                var button = hud.transform.Find("Route Selection/Assignment 1/Confirm Assignment").GetComponent<Button>();
                Assert.That(button.isActiveAndEnabled, Is.True);
                button.onClick.Invoke();
                Assert.That(_route.SelectingRoute, Is.False);
                Assert.That(_player.Input.FireHeld, Is.False);
                Assert.That(_player.Input.FirePressed, Is.False);
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);
                yield return ExitRoom();
                Assert.That(_route.CurrentNode, Is.SameAs(_route.Plan.At(3)[0]));
            }
        }

        [UnityTest]
        public IEnumerator LastHostileDuringRecallCommitsClearAndCannotBeRestored()
        {
            StartRun(0);
            StartFight();
            var room = _route.Current;
            Tick(54);
            var selection = new TemporalSelection();
            selection.Press(TemporalMode.Recall);
            var frame = new TemporalInput(default, default, false, false, false, selection);
            _director.Advance(TemporalTimeline.TickDuration, frame);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Recall));
            KillEnemies(room);
            _director.Advance(0d, frame);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            Assert.That(room.State, Is.EqualTo(EncounterState.RewardSelection));
            Assert.That(room.LivingEnemies, Is.Zero);
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));

            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            yield return null;
            var overlay = Object.FindAnyObjectByType<RouteSelectionHud>().transform.Find("Route Selection").gameObject;
            Assert.That(overlay.activeSelf, Is.True);
            Assert.That(_route.ConfirmRoute(0), Is.True);
            yield return null;
            Assert.That(overlay.activeSelf, Is.False);
            Tick(20, TemporalMode.Reset);
            Assert.That(room.LivingEnemies, Is.Zero);
            Assert.That(_route.RewardClaimed(_route.CurrentNode.Id), Is.True);
            yield return ExitRoom();
        }

        [UnityTest]
        public IEnumerator RetryingAClearedRoomKeepsResourcesAndItsRewardLedger()
        {
            StartRun(0);
            StartFight();
            ClearFight();
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            Assert.That(_route.ConfirmRoute(0), Is.True);
            var id = _route.CurrentNode.Id;
            var route = _route.CurrentNode;
            var hp = _player.Combat.Health.Current;
            var gun = _player.Combat.Weapon;
            var primary = gun.State(0).Reserve;
            var secondary = gun.State(1).Reserve;
            var defeats = _route.EnemiesDefeated;
            _director.Energy.Spend(30d);
            var energy = _director.Energy.Current;
            Assert.That(_route.DebugRestartRoom(), Is.True);
            yield return WaitForTransfer();
            Assert.That(_route.CurrentNode, Is.SameAs(route));
            Assert.That(_route.RewardClaimed(id), Is.True);
            StartFight();
            ClearFight();
            Assert.That(_route.SelectingRoute, Is.True);
            Assert.That(_route.EnemiesDefeated, Is.EqualTo(defeats));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(hp));
            Assert.That(gun.State(0).Reserve, Is.EqualTo(primary));
            Assert.That(gun.State(1).Reserve, Is.EqualTo(secondary));
            Assert.That(_director.Energy.Current, Is.EqualTo(energy));

            yield return NewRun(101);
            Assert.That(_route.RewardClaimed(id), Is.False);
            Assert.That(_route.Progression.Build.Revision, Is.Zero);
            Assert.That(_player.Combat.Weapon.HasSecondary, Is.False);
            Assert.That(_director.Energy.Current, Is.EqualTo(_director.Energy.Maximum));
        }

        private IEnumerator CompleteRoute(int branch)
        {
            StartRun(branch);
            var gun = _player.Combat.Weapon;
            var pool = _director.PlayerProjectiles;
            var enemyPool = _route.EnemyProjectiles;
            var ids = new HashSet<int>();
            var expected = _route.Plan.At(0)[0];
            var managedStart = GC.GetTotalMemory(false);
            var nativeStart = Profiler.GetTotalAllocatedMemoryLong();

            var battery = _route.Progression.DebugDefinitions[(int)UpgradeId.ExtendedBattery];
            if (_route.Progression.Build.Level(battery.Id) == 0)
            {
                Assert.That(_route.Progression.DebugGrant(battery), Is.True);
            }

            var batteryLevel = _route.Progression.Build.Level(battery.Id);
            var switchToSecondary = new TemporalInput(default, default, false, false, false, default, weaponSelection: WeaponSlotInput.Secondary);
            _director.Advance(TemporalTimeline.TickDuration, switchToSecondary);
            Tick(45);
            Assert.That(gun.ActiveSlot, Is.EqualTo(1));

            for (var index = 0; index < RoutePlan.RoomCount; index++)
            {
                var room = _route.Current;
                Assert.That(_route.CurrentNode, Is.SameAs(expected));
                Assert.That(room.State, Is.EqualTo(EncounterState.Waiting));

                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.EncounterReady, Is.False);
                    Assert.That(enemy.Body.enabled, Is.False);
                    Assert.That(enemy.Attacks, Is.Zero);
                    Assert.That(ids.Add(((ICombatDamageable)enemy).RewardId), Is.True);
                }

                StartFight();
                Assert.That(room.Gate.GetComponent<Collider>().enabled, Is.True);
                ExerciseTime(room);
                var expectedSecondary = branch == 0 ? WeaponId.Termination : WeaponId.Deadline;
                Assert.That(gun.Definition(1).Id, Is.EqualTo(expectedSecondary));
                Assert.That(gun.ActiveSlot, Is.EqualTo(1));
                Assert.That(_route.Progression.Build.Level(battery.Id), Is.EqualTo(batteryLevel));

                _player.Combat.Health.Restore(90);
                _director.Energy.Spend(_director.Energy.Current);
                var reserve0 = gun.State(0).Reserve;
                var reserve1 = gun.State(1).Reserve;
                Assert.That(enemyPool.TryLaunch(new Vector3(0f, 5f, 4f), Vector3.forward, 4f, 10f, 0.12f, 8, false), Is.True);
                KillEnemies(room);
                var energy = _director.Energy.Current;
                _director.Advance(0d, default);

                var ammo = expected.Reward == RoomReward.AmmunitionCache;
                Assert.That(_route.RewardClaimed(expected.Id), Is.True);
                Assert.That(gun.State(0).Reserve, Is.EqualTo(reserve0 + room.ReserveReward + (ammo ? 45 : 0)));
                Assert.That(gun.State(1).Reserve, Is.EqualTo(reserve1 + gun.Tuning(1).RoomReserve + (ammo ? branch == 0 ? 12 : 6 : 0)));
                Assert.That(_player.Combat.Health.Current, Is.EqualTo(expected.Reward == RoomReward.MedicalSupplies ? 100 : 90));
                Assert.That(_director.Energy.Current, Is.EqualTo(Mathf.Min(_director.Energy.Maximum, energy + (expected.Reward == RoomReward.ChronoCache ? 35f : 0f))));
                Assert.That(enemyPool.ActiveCount, Is.Zero);
                Assert.That(pool.ActiveCount, Is.Zero);
                Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
                Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));
                if (index == RoutePlan.RoomCount - 1)
                {
                    Assert.That(_route.Finished, Is.True);
                    Assert.That(room.RoomName, Is.EqualTo("Containment Chamber"));
                    Assert.That(_route.EnemiesDefeated, Is.EqualTo(ids.Count));
                    Assert.That(Object.FindObjectsByType<RoomRuntime>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                    var managedEnd = GC.GetTotalMemory(false);
                    var nativeEnd = Profiler.GetTotalAllocatedMemoryLong();
                    TestContext.Out.WriteLine($"Route {branch}: managed {managedStart} -> {managedEnd} bytes, Unity allocated {nativeStart} -> {nativeEnd} bytes.");
                    break;
                }

                Assert.That(room.State, Is.EqualTo(EncounterState.RewardSelection));
                Assert.That(_route.ConfirmRoute(0), Is.False);
                MoveTo(room.ExitZone.bounds.center);
                Assert.That(_route.Current, Is.SameAs(room));
                Assert.That(_route.ConfirmUpgrade(0), Is.True);
                Assert.That(_route.ConfirmUpgrade(0), Is.False);

                if (index is 0 or 2)
                {
                    Assert.That(_route.SelectingRoute, Is.True);
                    expected = _route.RouteOptions[branch];
                    Assert.That(_route.ConfirmRoute(branch), Is.True);
                    Assert.That(_route.ConfirmRoute(1 - branch), Is.False);
                }
                else
                {
                    expected = _route.RouteOptions[0];
                    Assert.That(_route.SelectingRoute, Is.False);
                }

                var health = _player.Combat.Health.Current;
                var currentEnergy = _director.Energy.Current;
                var enemyBefore = room.Enemies[0];
                var mag0 = gun.State(0).Magazine;
                var mag1 = gun.State(1).Magazine;
                var build = _route.Progression.Build;
                yield return ExitRoom();
                Assert.That(room == null, Is.True);
                Assert.That(enemyBefore.HistoryCapacity, Is.Zero);
                CollectionAssert.DoesNotContain(_director.WorldActors, enemyBefore);
                Assert.That(_director.DynamicColliderCount, Is.InRange(_route.Current.Enemies.Length, _route.Current.Enemies.Length * 3));
                Assert.That(_route.EnemyProjectiles, Is.SameAs(enemyPool));
                Assert.That(_director.PlayerProjectiles, Is.SameAs(pool));
                Assert.That(_player.Combat.Health.Current, Is.EqualTo(health));
                Assert.That(_director.Energy.Current, Is.EqualTo(currentEnergy));
                Assert.That(gun.State(0).Magazine, Is.EqualTo(mag0));
                Assert.That(gun.State(1).Magazine, Is.EqualTo(mag1));
                Assert.That(_route.Progression.Build, Is.SameAs(build));
                Assert.That(_player.Motor.Up, Is.EqualTo(Vector3.up));
                Assert.That(_player.Motor.Velocity, Is.EqualTo(Vector3.zero));

                var entry = _player.transform.position;
                Tick(6, TemporalMode.Reset);
                Tick(6, TemporalMode.Recall);
                Assert.That(Vector3.Distance(entry, _player.transform.position), Is.LessThan(0.2f));
                Assert.That(_route.CurrentNode, Is.SameAs(expected));
            }
        }

        private void ValidateGeometry(RoomDefinition room, int variantIndex)
        {
            var instance = _route.Current.GetComponent<RoomRuntime>();
            var variant = room.Variants[variantIndex];
            var entry = instance.Entry.position;
            Assert.That(Physics.CheckCapsule(entry - Vector3.up * 0.45f, entry + Vector3.up * 0.45f, 0.36f, 1, QueryTriggerInteraction.Ignore), Is.False, room.Title);
            var crossSection = new Vector3(0f, 1.05f, 4f);
            Assert.That(Physics.Raycast(crossSection, Vector3.left, 24f, 1, QueryTriggerInteraction.Ignore), Is.True, room.Title);
            Assert.That(Physics.Raycast(crossSection, Vector3.right, 24f, 1, QueryTriggerInteraction.Ignore), Is.True, room.Title);
            Assert.That(Physics.Raycast(crossSection, Vector3.up, 16f, 1, QueryTriggerInteraction.Ignore), Is.True, room.Title);

            for (var index = 0; index < variant.Markers.Length; index++)
            {
                var enemy = _route.Current.Enemies[index];
                var body = (BoxCollider)enemy.Body;
                var center = body.transform.TransformPoint(body.center);
                var nearby = Physics.OverlapBox(center, body.size * 0.5f - Vector3.one * 0.025f, body.transform.rotation, 1, QueryTriggerInteraction.Ignore);
                Assert.That(nearby, Is.Empty, $"{room.Title} / {variant.Title} / marker {variant.Markers[index]} at {center}");
                if (enemy.Kind != SecurityEnemyKind.Drone)
                {
                    Assert.That(Physics.Raycast(center, Vector3.down, body.size.y * 0.5f + 0.2f, 1, QueryTriggerInteraction.Ignore), Is.True, $"{room.Title} / {variant.Title} / unsupported marker {variant.Markers[index]}");
                }
            }
        }

        private void ExerciseTime(EncounterController room)
        {
            _director.Energy.Recharge();
            foreach (var mode in new[] { TemporalMode.Reset, TemporalMode.Recall, TemporalMode.Override, TemporalMode.BulletTime })
            {
                Tick(24);
                Tick(2, mode);
                Assert.That(_director.Mode, Is.EqualTo(mode));
                Assert.That(_route.Current, Is.SameAs(room));
            }

            _director.Advance(0d, default);
        }

        private IEnumerator NewRun(int seed)
        {
            _route.Progression.DebugRestartWithSeed(seed);
            yield return null;
            yield return null;
            Bind();
            Assert.That(_route.Progression.Seed, Is.EqualTo(seed));
        }

        private void Bind()
        {
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _player.enabled = false;
            _player.GetComponent<GravityCamera>().ReducedMotion = true;
        }

        private void StartRun(int secondary)
        {
            Assert.That(_route.ConfirmSecondary(secondary), Is.True);
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
        }

        private void StartFight()
        {
            var zone = _route.Current.CombatZone;
            MoveTo(new Vector3(0f, 0.89f, zone.bounds.min.z + 0.5f));
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Active));
        }

        private void ClearFight()
        {
            KillEnemies(_route.Current);
            _director.Advance(0d, default);
        }

        private void KillEnemies(EncounterController room)
        {
            foreach (var enemy in room.Enemies)
            {
                Assert.That(_director.DamageTarget(enemy, enemy.Body, 1000f, out _).Killed, Is.True);
            }
        }

        private IEnumerator ExitRoom()
        {
            MoveTo(_route.Current.ExitZone.bounds.center);
            Assert.That(_route.Transferring, Is.True);
            yield return WaitForTransfer();
        }

        private IEnumerator WaitForTransfer()
        {
            var end = Time.realtimeSinceStartup + 2f;
            while (_route.Transferring && Time.realtimeSinceStartup < end)
            {
                _director.Advance(0.05d, default);
                yield return null;
            }

            Assert.That(_route.Transferring, Is.False);
        }

        private void MoveTo(Vector3 position)
        {
            _player.Motor.ResetAt(position, Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
        }

        private void Tick(int count, TemporalMode mode = TemporalMode.Normal)
        {
            var input = new TemporalSelection();
            input.Press(mode);
            var frame = new TemporalInput(default, default, false, false, false, input);
            for (var index = 0; index < count; index++)
            {
                _director.Advance(TemporalTimeline.TickDuration, frame);
            }
        }
    }
}
