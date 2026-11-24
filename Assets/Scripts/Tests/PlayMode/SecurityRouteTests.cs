using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
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
#if UNITY_EDITOR
            const string preview = "Assets/Scenes/Security Route Stage 05A.1.unity";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(preview) != null)
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(preview, new LoadSceneParameters(LoadSceneMode.Single));
            }
            else
#endif
            {
                yield return SceneManager.LoadSceneAsync("Security Route", LoadSceneMode.Single);
            }
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

            Assert.That(roomCount, Is.EqualTo(16));
            Assert.That(variantCount, Is.EqualTo(47));
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

                var routeHud = Object.FindAnyObjectByType<RouteSelectionHud>();
                var overlay = routeHud.transform.Find("Route Selection");
                var labels = overlay.GetComponentsInChildren<TMP_Text>(true);
                foreach (var option in _route.RouteOptions)
                {
                    foreach (var label in labels)
                    {
                        Assert.That(label.text, Does.Not.Contain(option.Room.Title.ToUpperInvariant()));
                    }
                }

                Assert.That(overlay.Find("Assignment 1/Arena Survey"), Is.Null);
                Assert.That(overlay.Find("Assignment 2/Threat"), Is.Null);
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

        [UnityTest]
        public IEnumerator DirectiveRequiresFireEdgesAndSwitchCancelsAutomaticReload()
        {
            StartRun(2);
            var gun = _player.Combat.Weapon;
            gun.DebugActivate(1);
            var specification = gun.Tuning(1);
            Assert.That(gun.ActiveDefinition.Id, Is.EqualTo(WeaponId.Directive));
            Assert.That(specification.Magazine, Is.EqualTo(12));
            Assert.That(specification.Reserve, Is.EqualTo(96));
            var held = new TemporalInput(default, default, false, false, false, default, fireHeld: true);
            var press = new TemporalInput(default, default, false, false, true, default, fireHeld: true);
            var rig = _player.CameraRig;
            var initialAim = rig.AimRay.direction;
            var shots = 0;
            gun.ShotFired += () => shots++;

            _director.Advance(TemporalTimeline.TickDuration, press);
            Assert.That(rig.PendingRecoil.y, Is.GreaterThan(0f));
            AdvanceFrames(0.28, held);
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(gun.Magazine, Is.EqualTo(11));
            Assert.That(Vector3.Angle(initialAim, rig.AimRay.direction), Is.GreaterThan(1.4f));

            for (var index = 0; index < 11; index++)
            {
                _director.Advance(TemporalTimeline.TickDuration, press);
                AdvanceFrames(index == 10 ? 0.06 : 0.27, held);
            }

            Assert.That(shots, Is.EqualTo(12));
            Assert.That(gun.Magazine, Is.Zero);
            Assert.That(gun.IsReloading, Is.True);
            Assert.That(gun.Reserve, Is.EqualTo(96));
            var lastAim = rig.AimRay.direction;
            var swap = new TemporalInput(default, default, false, false, false, default, weaponSelection: WeaponSlotInput.Primary);
            _director.Advance(TemporalTimeline.TickDuration, swap);
            Assert.That(gun.IsReloading, Is.False);
            Assert.That(gun.IsSwitching, Is.True);
            AdvanceFrames(0.42, default);
            Assert.That(gun.State(1).Magazine, Is.Zero);
            Assert.That(gun.State(1).Reserve, Is.EqualTo(96));

            swap = new TemporalInput(default, default, false, false, false, default, weaponSelection: WeaponSlotInput.Secondary);
            _director.Advance(TemporalTimeline.TickDuration, swap);
            AdvanceFrames(1.3, held);
            Assert.That(gun.ActiveSlot, Is.EqualTo(1));
            Assert.That(gun.Magazine, Is.EqualTo(12));
            Assert.That(gun.Reserve, Is.EqualTo(84));
            Assert.That(shots, Is.EqualTo(12));
            Assert.That(Vector3.Distance(lastAim, rig.AimRay.direction), Is.LessThan(0.00001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReinforcementsRewindAndHazardsFollowTheWorldClock()
        {
            StartRun(2);
            var library = _route.DebugLibrary;
            var index = Array.FindIndex(library, definition => definition.Id == "containment");
            Assert.That(_route.DebugTravel(index, 2), Is.True);
            yield return WaitForTransfer();
            StartFight();
            var room = _route.Current;
            Assert.That(room.Enemies, Has.Length.EqualTo(24));
            Assert.That(room.ActiveEnemies, Is.EqualTo(16));
            Assert.That(room.PendingReinforcements, Is.EqualTo(8));
            Assert.That(_route.EnemyProjectiles.PoolCapacity, Is.GreaterThanOrEqualTo(96));
            var actors = _route.ActiveRoom.WorldActors;
            var rotor = Array.Find(actors, actor => actor is ArenaMechanism) as ArenaMechanism;
            var hazard = Array.Find(actors, actor => actor is ArenaHazard) as ArenaHazard;

            MoveTo(_route.ActiveRoom.Entry.position);
            foreach (var enemy in room.Enemies)
            {
                if (enemy.EncounterReady)
                {
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 1000f, out _).Killed, Is.True);
                }
            }

            _director.Advance(0d, default);
            Assert.That(room.State, Is.EqualTo(EncounterState.Active));
            Assert.That(room.LivingEnemies, Is.EqualTo(8));
            Tick(108);
            Assert.That(room.PendingReinforcements, Is.EqualTo(8));
            Tick(28);
            Assert.That(room.PendingReinforcements, Is.EqualTo(4));
            var position = rotor.transform.position;
            var phase = rotor.Phase;

            _director.Energy.Recharge();
            Tick(26, TemporalMode.Override);
            Assert.That(room.PendingReinforcements, Is.EqualTo(8));
            Assert.That(rotor.Phase, Is.LessThan(phase));
            Assert.That(Vector3.Distance(rotor.transform.position, position), Is.GreaterThan(0.001f));
            var health = _player.Combat.Health.Current;

            phase = rotor.Phase;
            Tick(60, TemporalMode.Recall);
            Assert.That(rotor.Phase, Is.GreaterThan(phase));
            Assert.That(room.PendingReinforcements, Is.LessThan(8));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(health));
            phase = rotor.Phase;
            Tick(60, TemporalMode.BulletTime);
            var slowed = rotor.Phase - phase;
            phase = rotor.Phase;
            Tick(60);
            Assert.That(slowed, Is.InRange(0.00001f, rotor.Phase - phase));
            Tick(96);
            Assert.That(hazard.Phase, Is.Not.EqualTo(HazardPhase.Dormant));

            _director.Advance(0d, default);
            ClearFight();
            Assert.That(hazard.Phase, Is.EqualTo(HazardPhase.Dormant));
        }

        [UnityTest]
        public IEnumerator CameraRecoilPersistsAndRestoresWithPlayerHistory()
        {
            StartRun(2);
            var rig = _player.CameraRig;
            var initialPitch = rig.Pitch;
            Tick(24);
            var press = new TemporalInput(default, default, false, false, true, default);
            var startingTick = _director.PlayerTimeline.Cursor;
            _director.Advance(TemporalTimeline.TickDuration, press);
            Assert.That(rig.PendingRecoil.y, Is.GreaterThan(0f));
            Tick(12);
            var kicked = rig.Pitch;
            Assert.That(kicked, Is.LessThan(initialPitch - 0.5f));
            Tick(80);
            Assert.That(rig.Pitch, Is.EqualTo(kicked).Within(0.00002f));
            var compensation = new TemporalInput(default, new Vector2(0f, (kicked - initialPitch) / 0.105f), false, false, false, default);
            _director.Advance(TemporalTimeline.TickDuration, compensation);
            Assert.That(rig.Pitch, Is.EqualTo(initialPitch).Within(0.001f));

            _director.Energy.Recharge();
            Tick(8, TemporalMode.Recall);
            Assert.That(rig.Pitch, Is.EqualTo(kicked).Within(0.00002f));
            Tick(100, TemporalMode.Reset);
            Assert.That(_director.PlayerTimeline.Cursor, Is.LessThanOrEqualTo(startingTick));
            Assert.That(rig.Pitch, Is.EqualTo(initialPitch).Within(0.00002f));
            Assert.That(rig.PendingRecoil, Is.EqualTo(Vector2.zero));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageArcsTrackSeveralWorldDirectionsAndClearOnRecall()
        {
            StartRun(2);
            var rig = _player.CameraRig;
            var ray = rig.AimRay;
            var right = Vector3.Cross(rig.AimUp, ray.direction);
            var hud = _player.GetComponentInChildren<PlaygroundHud>();
            var root = hud.transform.Find("Directional Damage Arcs");
            Assert.That(root.childCount, Is.EqualTo(10));
            var sources = new[] { ray.direction, right, -ray.direction, -right };
            var expected = new[] { 0f, -90f, 180f, 90f };
            Tick(40);
            for (var index = 0; index < sources.Length; index++)
            {
                Assert.That(_director.DamagePlayer(2, ray.origin + sources[index] * 8f), Is.True);
                var arc = root.GetChild(index).GetComponent<Image>();
                Assert.That(arc.enabled, Is.True);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(arc.rectTransform.localEulerAngles.z, expected[index])), Is.LessThan(0.05f));
            }

            rig.Look(new Vector2(90f / 0.105f, 0f));
            yield return null;
            var frontArc = (RectTransform)root.GetChild(0);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(frontArc.localEulerAngles.z, 90f)), Is.LessThan(0.1f));
            Tick(8, TemporalMode.Recall);
            yield return null;
            foreach (Transform child in root)
            {
                Assert.That(child.GetComponent<Image>().enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LargestEncounterRecordsAt120HzDuringSustainedCombat()
        {
            StartRun(2);
            var index = Array.FindIndex(_route.DebugLibrary, definition => definition.Id == "containment");
            Assert.That(_route.DebugTravel(index, 2), Is.True);
            yield return WaitForTransfer();
            StartFight();
            MoveTo(new Vector3(0f, 0.89f, 10f));
            var pool = _route.EnemyProjectiles;
            var peak = 0;
            var warmUp = new TemporalInput(Vector2.right, Vector2.zero, false, false, false, default);
            AdvanceFrames(0.18, warmUp);
            var tick = _director.WorldTimeline.Cursor;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var managed = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 480; frame++)
            {
                var move = frame % 120 < 60 ? Vector2.left : Vector2.right;
                var input = new TemporalInput(move, Vector2.zero, frame % 144 == 0, false, false, default);
                _player.Combat.Health.Restore(_player.Combat.Health.Maximum);
                _director.Advance(TemporalTimeline.TickDuration, input);
                peak = Mathf.Max(peak, pool.ActiveCount);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - managed;
            watch.Stop();
            Assert.That(_director.WorldTimeline.Cursor - tick, Is.EqualTo(480));
            Assert.That(peak, Is.GreaterThanOrEqualTo(2));
            Assert.That(peak, Is.LessThan(pool.PoolCapacity));
            Assert.That(_director.WorldTimeline.Count, Is.GreaterThanOrEqualTo(480));
            TestContext.Out.WriteLine($"24 planned, 16 deployed. 480 world ticks in {watch.Elapsed.TotalMilliseconds:0.0} ms; {allocated} managed bytes; hostile projectile peak {peak}/{pool.PoolCapacity}; history {_director.WorldTimeline.Count}/{_director.WorldTimeline.Capacity}.");
            while (pool.ActiveCount < pool.PoolCapacity)
            {
                Assert.That(pool.TryLaunch(Vector3.zero, Vector3.forward, 11f, 2f, 0.13f, 4, false), Is.True);
            }

            var generation = pool.Generation;
            Assert.That(pool.TryLaunch(Vector3.zero, Vector3.forward, 11f, 2f, 0.13f, 4, false), Is.False);
            Assert.That(pool.Generation, Is.EqualTo(generation));
            Assert.That(pool.ActiveCount, Is.EqualTo(pool.PoolCapacity));
        }

        private void AdvanceFrames(double seconds, in TemporalInput input)
        {
            var frames = Mathf.RoundToInt((float)(seconds * 120d));
            for (var index = 0; index < frames; index++)
            {
                _director.Advance(TemporalTimeline.TickDuration, input);
            }
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
                    var first = _route.RouteOptions[0];
                    var second = _route.RouteOptions[1];
                    var safer = first.Room.Threat(first.Variant) < second.Room.Threat(second.Variant) ? 0 : 1;
                    var choice = branch == 0 ? safer : 1 - safer;
                    expected = _route.RouteOptions[choice];
                    Assert.That(_route.ConfirmRoute(choice), Is.True);
                    Assert.That(_route.ConfirmRoute(1 - choice), Is.False);
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
                var actors = _route.ActiveRoom.WorldActors;
                var colliders = new List<Collider>();
                foreach (var actor in actors)
                {
                    actor.CollectColliders(colliders);
                }

                Assert.That(_director.DynamicColliderCount, Is.InRange(_route.Current.Enemies.Length + colliders.Count, _route.Current.Enemies.Length * 3 + colliders.Count));
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
            var previous = _route;
            _route.Progression.DebugRestartWithSeed(seed);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (previous != null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(previous == null, Is.True, "The previous scene must unload before checking the seeded run.");
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
            room.DebugReleaseAll();
            foreach (var enemy in room.Enemies)
            {
                if (enemy.Health.Alive)
                {
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 1000f, out _).Killed, Is.True);
                }
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
