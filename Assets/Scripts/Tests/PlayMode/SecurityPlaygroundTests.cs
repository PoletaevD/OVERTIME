using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class SecurityPlaygroundTests
    {
        private const double Step = TemporalTimeline.TickDuration;
        private static readonly Vector3 s_guardLane = new(-4.5f, 0.89f, -2.8f);
        private static readonly Vector3 s_auditorLane = new(3.15f, 6.11f, 7.5f);
        private static readonly Quaternion s_ceiling = Quaternion.AngleAxis(180f, Vector3.forward);

        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private SecurityEnemy _guard;
        private SecurityEnemy _secondGuard;
        private SecurityEnemy _drone;
        private SecurityEnemy _auditor;
        private SecurityEnemy[] _roster;
        private TemporalProjectileEmitter _pool;

        [UnitySetUp]
        public IEnumerator LoadSecurity()
        {
            yield return SceneManager.LoadSceneAsync("Security Playground", LoadSceneMode.Single);
            yield return null;

            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _guard = FindEnemy("Security Unit 01");
            _secondGuard = FindEnemy("Security Unit 02");
            _drone = FindEnemy("Surveillance Drone");
            _auditor = FindEnemy("Laser Auditor");
            _roster = new[] { _guard, _secondGuard, _drone, _auditor };
            _pool = GameObject.Find("Enemy Projectile Pool").GetComponent<TemporalProjectileEmitter>();
            _player.enabled = false;
            Restart();
        }

        [Test]
        public void SceneWiresFourWorldActorsWithUniqueRewardsAndOneHealthEach()
        {
            var ids = new HashSet<int>();
            foreach (var enemy in _roster)
            {
                Assert.That(enemy.GetComponents<TemporalActor>(), Has.Length.EqualTo(1));
                Assert.That(enemy.GetComponents<CombatHealth>(), Has.Length.EqualTo(1));
                Assert.That(enemy.Body.enabled, Is.True);
                Assert.That(ids.Add(((ICombatDamageable)enemy).RewardId), Is.True);
            }

            Assert.That(_guard.Health.Current, Is.EqualTo(70));
            Assert.That(_secondGuard.Health.Current, Is.EqualTo(70));
            Assert.That(_drone.Health.Current, Is.EqualTo(45));
            Assert.That(_auditor.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void SecurityUnitReactsThenFiresExactlyThreeVisibleProjectiles()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(1);
            Assert.That(_guard.SeesPlayer, Is.True);
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Alert));
            Assert.That(_pool.Generation, Is.Zero);

            Tick(22);
            Assert.That(_pool.Generation, Is.Zero);
            TickUntil(() => _guard.Attacks == 3, 120);
            Assert.That(_pool.Generation, Is.EqualTo(3));
            Assert.That(_pool.ActiveCount, Is.GreaterThan(0));
            Tick(10);
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Recovering));
            Assert.That(_guard.Attacks, Is.EqualTo(3));
            Assert.That(_player.Combat.Health.Current, Is.LessThan(100));
        }

        [Test]
        public void SecurityUnitCannotFireThroughCoverAndReturnsToItsPatrol()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            var wall = Block(new Vector3(-4.6f, 1.5f, -0.8f), new Vector3(4.4f, 3f, 0.6f));
            Tick(250);
            Assert.That(_guard.SeesPlayer, Is.False);
            Assert.That(_pool.Generation, Is.Zero);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));

            Object.DestroyImmediate(wall);
            TickUntil(() => _guard.Attacks > 0, 155);
            var attacks = _guard.Attacks;
            Block(new Vector3(-4.6f, 1.5f, -0.8f), new Vector3(4.4f, 3f, 0.6f));
            Tick(100);
            Assert.That(_guard.SeesPlayer, Is.False);
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Moving));
            Assert.That(_guard.Attacks, Is.EqualTo(attacks));
        }

        [Test]
        public void GroundPatrolRemainsInsideItsAuthoredArea()
        {
            var start = _guard.transform.position;
            Isolate(_guard);
            MovePlayer(new Vector3(0f, 0.89f, -8.4f));
            for (var checkpoint = 0; checkpoint < 22; checkpoint++)
            {
                Tick(24);
                var position = _guard.transform.position;
                Assert.That(position.x, Is.InRange(-5.26f, -3.04f));
                Assert.That(position.y, Is.EqualTo(start.y));
                Assert.That(position.z, Is.InRange(0.49f, 5.81f));
            }

            Assert.That(Vector3.Distance(_guard.transform.position, start), Is.GreaterThan(0.1f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DroneAcquiresAWallOrCeilingTargetAndFires(bool ceiling)
        {
            Isolate(_drone);
            var wall = Quaternion.FromToRotation(Vector3.up, Vector3.right);
            MovePlayer(ceiling ? new Vector3(-0.5f, 6.10f, 7.5f) : new Vector3(-6.11f, 3.91f, 4.0f), ceiling ? s_ceiling : wall);
            TickUntil(() => _drone.Attacks > 0, 150);
            Assert.That(_drone.SeesPlayer, Is.True);
            Assert.That(Vector3.Angle(_drone.Aim, _player.transform.position - _drone.AttackOrigin), Is.LessThan(6f));
            Assert.That(ceiling ? _drone.Aim.y : -_drone.Aim.x, Is.GreaterThan(0.2f));
            Assert.That(_pool.Generation, Is.EqualTo(1));
        }

        [Test]
        public void GuardCanAimVerticallyAtTheCeiling()
        {
            Isolate(_guard);
            MovePlayer(new Vector3(-4.25f, 6.11f, 0.5f), s_ceiling);
            TickUntil(() => _guard.Attacks > 0, 155);
            Assert.That(_guard.SeesPlayer, Is.True);
            Assert.That(_guard.Aim.y, Is.GreaterThan(0.65f));
        }

        [Test]
        public void DroneSweepAvoidsStaticGeometry()
        {
            Isolate(_drone);
            var start = _drone.transform.position;
            Block(start + new Vector3(-0.65f, 0.20f, -1.75f), new Vector3(0.7f, 1f, 0.6f));
            Physics.SyncTransforms();
            var farthest = 0f;
            for (var tick = 0; tick < 280; tick++)
            {
                Tick(1);
                var position = _drone.transform.position;
                farthest = Mathf.Max(farthest, Vector3.Distance(position, start));
                Assert.That(Physics.CheckSphere(position, 0.78f, 1, QueryTriggerInteraction.Ignore), Is.False);
            }

            Assert.That(farthest, Is.GreaterThan(1.5f));
        }

        [Test]
        public void AuditorTelegraphLocksAndAllowsAVisibleDodge()
        {
            StartAuditorAttack();
            var laser = _auditor.GetComponentInChildren<LineRenderer>(true);
            Assert.That(_auditor.State, Is.EqualTo(SecurityEnemyState.Aiming));
            Assert.That(laser.enabled, Is.True);
            Assert.That(_auditor.Attacks, Is.Zero);

            var scanWidth = laser.startWidth;
            TickUntil(() => _auditor.DirectionLocked, 110);
            var lockedAim = _auditor.Aim;
            Assert.That(laser.startWidth, Is.GreaterThan(scanWidth));
            MovePlayer(new Vector3(0.5f, 6.11f, 7.5f), s_ceiling);
            TickUntil(() => _auditor.Attacks == 1, 75);
            Assert.That(_auditor.Aim, Is.EqualTo(lockedAim));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
            Assert.That(laser.startWidth, Is.GreaterThan(0.2f));
        }

        [Test]
        public void LockedAuditorBeamStopsAtInterposedCover()
        {
            StartAuditorAttack();
            TickUntil(() => _auditor.DirectionLocked, 110);
            var origin = _auditor.AttackOrigin;
            Block(origin + _auditor.Aim * 1.5f, Vector3.one * 0.55f);
            TickUntil(() => _auditor.Attacks == 1, 75);

            var laser = _auditor.GetComponentInChildren<LineRenderer>(true);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
            Assert.That(Vector3.Distance(origin, laser.GetPosition(1)), Is.LessThan(1.5f));
        }

        [Test]
        public void AuditorDamageOccursOnceAndIsNeverReplayedByRestore()
        {
            StartAuditorAttack();
            TickUntil(() => _auditor.Attacks == 1, 150);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(70));
            Tick(9);
            Tick(3, TemporalMode.Override);
            Assert.That(_auditor.State, Is.EqualTo(SecurityEnemyState.Attacking));
            Assert.That(_auditor.Attacks, Is.EqualTo(1));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(70));
            Tick(24);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(70));
            Assert.That(_auditor.State, Is.EqualTo(SecurityEnemyState.Recovering));
            Assert.That(_auditor.Cooldown, Is.GreaterThan(1.8f));
        }

        [Test]
        public void OverrideRestoresExactAuditorChargeAndItsLockedAim()
        {
            StartAuditorAttack();
            TickUntil(() => _auditor.DirectionLocked, 110);
            Tick(6);
            var aim = _auditor.Aim;
            var time = _auditor.PhaseRemaining;
            Tick(18);
            MovePlayer(new Vector3(0.5f, 6.11f, 7.5f), s_ceiling);
            Tick(9, TemporalMode.Override);
            Assert.That(_auditor.State, Is.EqualTo(SecurityEnemyState.Aiming));
            Assert.That(_auditor.DirectionLocked, Is.True);
            Assert.That(_auditor.PhaseRemaining, Is.EqualTo(time));
            Assert.That(_auditor.Aim, Is.EqualTo(aim));
            Assert.That(_auditor.Attacks, Is.Zero);
            TickUntil(() => _auditor.Attacks == 1, 75);
            Assert.That(_auditor.Aim, Is.EqualTo(aim));
        }

        [Test]
        public void ResetRestoresEnemyHealthPositionAndAlertTimerWithPlayerHealth()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(12);
            var position = _guard.transform.position;
            var remaining = _guard.PhaseRemaining;
            TickUntil(() => _guard.Attacks == 3, 130);
            _director.DamageTarget(_guard, _guard.Body, 19, out _);
            Tick(34);
            Assert.That(_guard.Health.Current, Is.EqualTo(51));
            Assert.That(_guard.transform.position, Is.Not.EqualTo(position));
            Assert.That(_player.Combat.Health.Current, Is.LessThan(100));

            if (_director.WorldTimeline.Cursor % 2 != 0)
            {
                Tick(1);
            }

            var ticks = (int)(_director.WorldTimeline.Cursor - 12);
            Tick(ticks / 2, TemporalMode.Reset);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
            Assert.That(_guard.Health.Current, Is.EqualTo(70));
            Assert.That(_guard.transform.position, Is.EqualTo(position));
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Alert));
            Assert.That(_guard.PhaseRemaining, Is.EqualTo(remaining));
            Assert.That(_pool.ActiveCount, Is.Zero);
        }

        [Test]
        public void OverrideUsesSavedPerceptionAndPositionUntilForwardSimulationResumes()
        {
            Isolate(_guard);
            var position = _guard.transform.position;
            Tick(90);
            Assert.That(_guard.transform.position, Is.Not.EqualTo(position));
            MovePlayer(s_guardLane);
            Tick(35, TemporalMode.Override);
            Assert.That(Vector3.Distance(_guard.transform.position, position), Is.LessThan(0.65f));
            Assert.That(_guard.SeesPlayer, Is.False);
            Assert.That(_pool.Generation, Is.Zero);
            MovePlayer(s_guardLane);
            Tick(1);
            Assert.That(_guard.SeesPlayer, Is.True);
            TickUntil(() => _guard.Attacks > 0, 110);
        }

        [Test]
        public void RecallKeepsKillsAndWorldAttacksButUsesCurrentPlayerPosition()
        {
            Isolate(_guard);
            Tick(24);
            MovePlayer(s_guardLane);
            Tick(86);
            Assert.That(_guard.SeesPlayer, Is.True);
            Assert.That(_guard.Attacks, Is.GreaterThan(0));
            var attacks = _guard.Attacks;
            var world = _director.WorldTimeline.Cursor;
            Tick(52, TemporalMode.Recall);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world + 52));
            Assert.That(_guard.SeesPlayer, Is.False);
            Assert.That(_guard.Health.Alive, Is.True);
            Assert.That(_guard.Attacks, Is.GreaterThanOrEqualTo(attacks));
            Tick(1);
            Kill(_guard);
            Tick(8);
            Tick(6, TemporalMode.Recall);
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Dead));
            Assert.That(_guard.Body.enabled, Is.False);
        }

        [Test]
        public void BulletTimeScalesMovementAndTheEntireBurstSchedule()
        {
            Isolate(_guard);
            Tick(48);
            var patrolPosition = _guard.transform.position;
            Restart();
            Isolate(_guard);
            Tick(192, TemporalMode.BulletTime);
            Assert.That(_guard.transform.position, Is.EqualTo(patrolPosition));

            Restart();
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(50);
            var attacks = _guard.Attacks;
            var phase = _guard.PhaseRemaining;
            Assert.That(attacks, Is.GreaterThan(0));
            Restart();
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(200, TemporalMode.BulletTime);
            Assert.That(_guard.Attacks, Is.EqualTo(attacks));
            Assert.That(_guard.PhaseRemaining, Is.EqualTo(phase));
        }

        [Test]
        public void BulletTimeLetsThePlayerRunFromFloorToWallToCeiling()
        {
            MovePlayer(new Vector3(5.8f, 0.89f, -5f), Quaternion.Euler(0f, 90f, 0f));
            var motor = _player.GetComponent<GravityMotor>();
            var selection = new TemporalSelection();
            selection.Press(TemporalMode.BulletTime);
            var jump = new TemporalInput(Vector2.up, default, true, true, false, selection);
            var runUp = new TemporalInput(Vector2.up, default, false, true, false, selection);
            _director.Advance(Step, jump);
            for (var tick = 0; tick < 14; tick++)
            {
                _director.Advance(Step, runUp);
            }

            Assert.That(motor.Up, Is.EqualTo(Vector3.left));
            for (var tick = 0; tick < 155 && motor.Up != Vector3.down; tick++)
            {
                _director.Advance(Step, runUp);
            }

            Assert.That(motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(_director.PlayerTimeline.Cursor / 4));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Override)]
        public void WorldRewindRestoresProjectileGenerationAndDoesNotApplyTheImpact(int rawMode)
        {
            Isolate(null);
            MovePlayer(new Vector3(-4.5f, 0.89f, -5.5f));
            Tick(20);
            LaunchAtPlayer(25, 10f, 2f);
            Tick(8);
            var position = _pool.transform.GetChild(0).position;
            TickUntil(() => _player.Combat.Health.Current < 100, 40);
            Assert.That(_pool.ActiveCount, Is.Zero);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(75));

            var delta = (int)(_director.WorldTimeline.Cursor - 28);
            Tick((delta + 1) / 2, (TemporalMode)rawMode);
            Assert.That(_pool.Generation, Is.EqualTo(1));
            Assert.That(_pool.ActiveCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(_pool.transform.GetChild(0).position, position), Is.LessThan(0.09f));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(rawMode == (int)TemporalMode.Reset ? 100 : 75));
        }

        [Test]
        public void RecallLetsAProjectileCrossTheProtectedPlayerWithoutHitting()
        {
            Isolate(null);
            MovePlayer(new Vector3(-4.5f, 0.89f, -5.5f));
            Tick(70);
            LaunchAtPlayer(25, 12f, 2f);
            var slot = _pool.transform.GetChild(0);
            var before = slot.position;
            Tick(28, TemporalMode.Recall);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
            Assert.That(slot.position.z - before.z, Is.EqualTo(2.8f).Within(0.01f));
            Assert.That(_pool.ActiveCount, Is.EqualTo(1));
            Tick(10);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
        }

        [TestCase("Security Unit 01")]
        [TestCase("Surveillance Drone")]
        [TestCase("Laser Auditor")]
        public void Arc01HitsAllEnemyTypes(string enemyName)
        {
            var enemy = FindEnemy(enemyName);
            var target = enemy.Body.bounds.center;
            var origin = target + Vector3.forward * -1.3f;
            FireAt(origin, target);
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Maximum - 12));
        }

        [Test]
        public void Arc01CanHitTheAuditorRearPowerCell()
        {
            var core = _auditor.transform.Find("Exposed Rear Power Cell").position;
            FireAt(core + Vector3.forward * 1.1f, core);
            Assert.That(_auditor.Health.Current, Is.EqualTo(72));
        }

        [Test]
        public void ADeadEnemyStopsAttackingAndItsRewardRemainsPaidAfterResurrection()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            TickUntil(() => _guard.Attacks == 1, 120);
            Tick(6);
            _director.Energy.Spend(50d);
            Kill(_guard);
            Assert.That(_director.Energy.Current, Is.EqualTo(70f));
            var attacks = _guard.Attacks;
            var generation = _pool.Generation;

            Tick(20);
            Assert.That(_guard.State, Is.EqualTo(SecurityEnemyState.Dead));
            Assert.That(_guard.Body.enabled, Is.False);
            Assert.That(_guard.Attacks, Is.EqualTo(attacks));
            Assert.That(_pool.Generation, Is.EqualTo(generation));
            Tick(11, TemporalMode.Override);
            Assert.That(_guard.Health.Alive, Is.True);
            Tick(1);
            var balance = _director.Energy.Current;
            Kill(_guard);
            Assert.That(_director.Energy.Current, Is.EqualTo(balance));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Recall)]
        public void LethalEnemyProjectileCanBeSurvivedThroughPlayerRewind(int rawMode)
        {
            Isolate(null);
            MovePlayer(new Vector3(-4.5f, 0.89f, -5.5f));
            Tick(32);
            LaunchAtPlayer(100, 20f, 2f);
            TickUntil(() => !_player.Combat.Alive, 30);
            Assert.That(_director.RecoveryAvailable, Is.True);
            Tick(8, (TemporalMode)rawMode);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(100));
            Assert.That(_player.Combat.GameOver, Is.False);
        }

        [Test]
        public void SequentialRewindsKeepAiTimersAndAllowFreshAttacks()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(25);
            var phase = _guard.PhaseRemaining;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                Tick(12);
                Tick(6, TemporalMode.Override);
                Assert.That(_guard.PhaseRemaining, Is.EqualTo(phase));
                Assert.That(_guard.Attacks, Is.Zero);
                Assert.That(_pool.Generation, Is.Zero);
            }

            TickUntil(() => _guard.Attacks == 3, 132);
            Assert.That(_pool.Generation, Is.EqualTo(3));
        }

        [Test]
        public void RestartResetsEveryEnemyAndClearsAllProjectiles()
        {
            var positions = new Vector3[_roster.Length];
            for (var index = 0; index < _roster.Length; index++)
            {
                positions[index] = _roster[index].transform.position;
                _director.DamageTarget(_roster[index], _roster[index].Body, index == 0 ? 300 : 17, out _);
            }

            Tick(44);
            LaunchAtPlayer(8, 8f, 3f);
            Assert.That(_pool.ActiveCount, Is.GreaterThan(0));
            Restart();
            for (var index = 0; index < _roster.Length; index++)
            {
                var enemy = _roster[index];
                Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Maximum));
                Assert.That(enemy.State, Is.EqualTo(SecurityEnemyState.Idle));
                Assert.That(enemy.Body.enabled, Is.True);
                Assert.That(enemy.transform.position, Is.EqualTo(positions[index]));
            }

            Assert.That(_pool.ActiveCount, Is.Zero);
            Assert.That(_pool.Generation, Is.Zero);
            Assert.That(_director.WorldTimeline.Cursor, Is.Zero);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void RenderFrameRateDoesNotChangeEnemyOrProjectileSimulation(int fps)
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(112);
            var aim = _guard.Aim;
            var position = _guard.transform.position;
            var attacks = _guard.Attacks;
            var health = _player.Combat.Health.Current;
            var projectileCount = _pool.ActiveCount;

            Restart();
            Isolate(_guard);
            MovePlayer(s_guardLane);
            for (var frame = 0; frame < fps; frame++)
            {
                _director.Advance(1d / fps * 112d * Step, default);
            }

            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(112));
            Assert.That(_guard.Aim, Is.EqualTo(aim));
            Assert.That(_guard.transform.position, Is.EqualTo(position));
            Assert.That(_guard.Attacks, Is.EqualTo(attacks));
            Assert.That(_pool.ActiveCount, Is.EqualTo(projectileCount));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(health));
        }

        [Test]
        public void WarmedCombatSimulationIsAllocationFreeAndKeepsFixedStorageAndObjectCount()
        {
            Isolate(_guard);
            MovePlayer(s_guardLane);
            Tick(94);
            var generation = _pool.Generation;
            var count = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var before = GC.GetAllocatedBytesForCurrentThread();
            Tick(205);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(_pool.Generation, Is.GreaterThan(generation));
            Assert.That(allocated, Is.Zero);
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(count));

            MovePlayer(new Vector3(0f, 0.89f, -8.2f));
            Tick(600);
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(_director.WorldTimeline.Capacity));
        }

        private void StartAuditorAttack()
        {
            Isolate(_auditor);
            MovePlayer(s_auditorLane, s_ceiling);
            Tick(1);
        }

        private void FireAt(Vector3 origin, Vector3 target)
        {
            var ray = new Ray(origin, (target - origin).normalized);
            _player.Combat.Weapon.Simulate(Step, true, false, true, ray, Vector3.up, _director.PlayerProjectiles);
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
            Tick(Mathf.CeilToInt(Vector3.Distance(origin, target) / (45f * (float)Step)) + 1);
        }

        private void LaunchAtPlayer(int damage, float speed, float distance)
        {
            var target = _player.Capsule.bounds.center;
            Assert.That(_pool.TryLaunch(target - Vector3.forward * distance, Vector3.forward, speed, 2f, 0.13f, damage, false), Is.True);
        }

        private void Isolate(SecurityEnemy keep)
        {
            foreach (var enemy in _roster)
            {
                if (enemy != keep)
                {
                    Kill(enemy);
                }
            }
        }

        private void Kill(SecurityEnemy enemy)
        {
            Assert.That(_director.DamageTarget(enemy, enemy.Body, 300, out _).Killed, Is.True);
        }

        private void TickUntil(Func<bool> condition, int limit)
        {
            for (var tick = 0; tick < limit && !condition(); tick++)
            {
                Tick(1);
            }

            Assert.That(condition(), Is.True, $"Condition unmet after {limit} ticks.");
        }

        private void Tick(int count, TemporalMode mode = TemporalMode.Normal)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            var input = new TemporalInput(default, default, false, false, false, selection);
            for (var tick = 0; tick < count; tick++)
            {
                _director.Advance(Step, input);
            }
        }

        private void Restart()
        {
            _director.RequestRestart();
            _director.Advance(0d, default);
        }

        private void MovePlayer(Vector3 position, Quaternion? rotation = null)
        {
            var orientation = rotation ?? Quaternion.identity;
            _player.GetComponent<GravityMotor>().ResetAt(position, orientation);
            _player.GetComponent<GravityCamera>().ResetView(orientation);
            Physics.SyncTransforms();
        }

        private static SecurityEnemy FindEnemy(string name)
        {
            return GameObject.Find(name).GetComponent<SecurityEnemy>();
        }

        private static GameObject Block(Vector3 position, Vector3 scale)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Test Obstruction";
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            return obstacle;
        }
    }
}
