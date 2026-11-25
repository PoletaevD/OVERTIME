using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class Stage05BPlayModeTests
    {
        private const string SceneName = "Security Route Stage 05B";
        private const double Tick = TemporalTimeline.TickDuration;
        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;

        [UnitySetUp]
        public IEnumerator OpenRoute()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _player.enabled = false;
            _player.CameraRig.ReducedMotion = true;
        }

        [UnityTest]
        public IEnumerator PhysicalRouteContinuesPastContainmentWithBuildAndSupplies()
        {
            Assert.That(_route.RoomCount, Is.EqualTo(6));
            Assert.That(_route.DebugLootWeapon(WeaponId.Compliance, WeaponRarity.Rare, 1 << (int)WeaponModifier.RicochetChamber, 611, true), Is.True);
            var weapon = _route.Weapon;
            var instance = weapon.Instance(weapon.ActiveSlot);
            for (var index = 0; index < _route.RoomCount - 1; index++)
            {
                var outgoing = _route.ActiveRoom;
                var room = _route.Current;
                var start = outgoing.transform.TransformPoint(new Vector3(0f, 0.89f, 4.15f));
                Place(start);
                _director.Advance(0d, default);
                Assert.That(room.State, Is.EqualTo(EncounterState.Active), room.RoomName);
                room.DebugReleaseAll();
                foreach (var enemy in room.Enemies)
                {
                    _director.DamageTarget(enemy, enemy.Body, 10000f, out _);
                }

                _director.Advance(0d, default);
                Assert.That(_route.Finished, Is.False);
                Assert.That(room.State, Is.EqualTo(EncounterState.RewardSelection));
                Assert.That(_route.Progression.Selection.Count, Is.EqualTo(3));
                Assert.That(outgoing.transform.Cast<Transform>().Count(child => child.name.StartsWith("ARC-", System.StringComparison.Ordinal)), Is.EqualTo(2));
                Assert.That(_route.ConfirmUpgrade(0), Is.True);

                var passage = _route.Passage;
                var branch = _route.RouteOptions.Count - 1;
                var door = passage.CrossingPoint(branch);
                var turn = passage.TurnPoint(branch);
                var destination = passage.DestinationSocket(branch);
                var exit = outgoing.Exit;
                Place(exit.position - exit.forward * 1.1f);
                var health = _player.Combat.Health.Current;
                var energy = _director.Energy.Current;
                var revision = _route.Progression.Build.Revision;
                var magazine = weapon.Magazine;
                var stock = weapon.Capture().Stock;
                WalkTo(new Vector3(exit.position.x, exit.position.y, door.z));
                WalkTo(door);
                WalkTo(turn);
                Assert.That(passage.Selected, Is.EqualTo(branch));
                WalkTo(destination.position + destination.rotation * new Vector3(0f, 0.89f, 4.45f));
                Assert.That(_route.RoomIndex, Is.EqualTo(index + 1));
                Assert.That(_route.InPassage || _route.Transferring, Is.False);
                Assert.That(_player.Combat.Health.Current, Is.EqualTo(health));
                Assert.That(_director.Energy.Current, Is.EqualTo(energy));
                Assert.That(_route.Progression.Build.Revision, Is.EqualTo(revision));
                Assert.That(weapon.Instance(weapon.ActiveSlot), Is.SameAs(instance));
                Assert.That(weapon.Magazine, Is.EqualTo(magazine));
                Assert.That(weapon.Capture().Stock, Is.EqualTo(stock));
                yield return null;
                Assert.That(outgoing == null, Is.True);
            }

            var boss = _route.Current.Boss;
            Assert.That(boss, Is.Not.Null);
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Waiting));
            Assert.That(_route.ActiveRoom.transform.Find("Architecture/Arrival Door").gameObject.activeSelf, Is.True);
            WalkTo(_route.ActiveRoom.transform.TransformPoint(new Vector3(0f, 0.89f, 8.2f)));
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Active));
            Assert.That(_route.Current.LivingEnemies, Is.EqualTo(1));
            Assert.That(_director.DynamicColliderCount, Is.GreaterThanOrEqualTo(boss.GetComponentsInChildren<Collider>().Length));
            Assert.That(_director.DamageTarget(boss, boss.Body, 10000f, out _).Killed, Is.True);
            var defeated = _route.EnemiesDefeated;
            _director.Advance(0d, default);
            Assert.That(_route.Finished, Is.True);
            Assert.That(_route.EnemiesDefeated, Is.EqualTo(defeated + 1));
            Assert.That(_route.EnemyProjectiles.ActiveCount, Is.Zero);
            _director.Advance(Tick, Input(TemporalMode.Reset));
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Completed));
            Assert.That(_route.EnemiesDefeated, Is.EqualTo(defeated + 1));
        }

        [UnityTest]
        public IEnumerator DamageUsesCoreArmorAndShieldAndPiercingOnlyHitsOnce()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            var upper = Hitbox(boss, "Upper Core");
            var rear = Hitbox(boss, "Rear Core");
            var shield = boss.GetComponentInChildren<CombatShield>().GetComponent<Collider>();
            Assert.That(upper.enabled, Is.False);
            Assert.That(_director.DamageTarget(boss, shield, 100f, out _).Amount, Is.Zero);
            var shielded = false;
            _director.ProjectileHit += impact => shielded |= impact.Shielded;
            var shieldShot = new Ray(shield.bounds.center + Vector3.up * 2f, Vector3.down);
            var healthBeforeShield = boss.Health.Current;
            Assert.That(_director.PlayerProjectiles.TryLaunchPlayer(shieldShot, Vector3.forward, Vector3.zero, 40f, 1f, 5f, 0.02f, 100, Physics.DefaultRaycastLayers), Is.True);
            Advance(0.12f);
            Assert.That(shielded, Is.True);
            Assert.That(boss.Health.Current, Is.EqualTo(healthBeforeShield));
            Assert.That(_director.DamageTarget(boss, boss.Body, 100f, out var weak).Amount, Is.EqualTo(70));
            Assert.That(weak, Is.False);
            Assert.That(_director.DamageTarget(boss, rear, 100f, out weak).Amount, Is.EqualTo(175));
            Assert.That(weak, Is.True);

            var start = rear.bounds.center + Vector3.forward * 3f;
            var shot = new Ray(start, Vector3.back);
            var ballistics = new ProjectileBallistics(WeaponId.Deadline, 3, 0, 1f, 0f, 0f, 0f, 0f, 0f);
            var before = boss.Health.Current;
            Assert.That(_director.PlayerProjectiles.TryLaunchVolley(shot, Vector3.up, Vector3.zero, 40f, 2f, 20f, 0.02f, 100f, Physics.DefaultRaycastLayers, false, false, ballistics, 1, 0f, 0f, 0f), Is.True);
            Advance(0.27f);
            Assert.That(boss.Health.Current, Is.EqualTo(before - 175));

            _director.DamageTarget(boss, boss.Body, 400f, out _);
            Assert.That(boss.Phase, Is.EqualTo(SupervisorPhase.CorrectiveAction));
            _director.DamageTarget(boss, boss.Body, 900f, out _);
            Assert.That(boss.Phase, Is.EqualTo(SupervisorPhase.EmergencyOverride));
            Advance(0.55f);
            Assert.That(upper.enabled, Is.True);
            Assert.That(shield.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator RicochetAndBlastReachTheBossThroughSharedBallistics()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            var rear = Hitbox(boss, "Rear Core").bounds.center;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = rear + Vector3.forward * 4.25f;
                wall.transform.localScale = new Vector3(1f, 1.25f, 0.16f);
                Physics.SyncTransforms();
                var pool = _director.PlayerProjectiles;
                var shot = new Ray(rear + Vector3.forward * 2.1f, Vector3.forward);
                var ricochet = new ProjectileBallistics(WeaponId.Compliance, 1, 1, 1f, 0.75f, 0f, 0f, 0f, 0f);
                var reflected = false;
                _director.ProjectileHit += impact => reflected |= impact.Ricochet;
                var health = boss.Health.Current;
                Assert.That(pool.TryLaunchVolley(shot, Vector3.up, Vector3.zero, 40f, 1f, 12f, 0.02f, 100f, Physics.DefaultRaycastLayers, false, false, ricochet, 1, 0f, 0f, 0f), Is.True);
                Advance(0.34f);
                Assert.That(reflected, Is.True);
                Assert.That(boss.Health.Current, Is.LessThan(health));

                health = boss.Health.Current;
                pool.Explode(rear + Vector3.forward, 2.1f, 95f, _player.transform, false);
                Assert.That(boss.Health.Current, Is.LessThan(health));
            }
            finally
            {
                Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator ResetRestoresPhaseAttackAndPlayer()
        {
            yield return RestoresWorld(TemporalMode.Reset);
        }

        [UnityTest]
        public IEnumerator OverrideRestoresPhaseAndAttackWhilePlayerStaysForward()
        {
            yield return RestoresWorld(TemporalMode.Override);
        }

        private IEnumerator RestoresWorld(TemporalMode mode)
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            Assert.That(_route.DebugSupervisor(1800, SupervisorAttack.RecordedViolation), Is.True);
            var cues = 0;
            _director.SupervisorSound += (_, _) => cues++;
            Frames(16);
            var before = boss.State;
            var playerStart = _player.transform.position;
            Place(playerStart + Vector3.right * 3f);
            _director.DamageTarget(boss, boss.Body, 1100f, out _);
            Frames(24);
            Assert.That(boss.Phase, Is.EqualTo(SupervisorPhase.CorrectiveAction));
            var cueCount = cues;
            var enemyGeneration = _route.EnemyProjectiles.Generation;
            _director.Energy.Recharge();
            Frames(12, Input(mode));
            Assert.That(_director.Mode, Is.EqualTo(mode));
            Assert.That(boss.Health.Current, Is.EqualTo(before.Health));
            Assert.That(boss.Phase, Is.EqualTo(before.Phase));
            Assert.That(boss.State.Attack.Remaining, Is.EqualTo(before.Attack.Remaining).Within(Tick * 1.1));
            Assert.That(boss.State.Attack.Point, Is.EqualTo(before.Attack.Point));
            Assert.That(cues, Is.EqualTo(cueCount));
            Assert.That(_route.EnemyProjectiles.Generation, Is.EqualTo(enemyGeneration));
            var offset = _player.transform.position.x - playerStart.x;
            Assert.That(offset, Is.EqualTo(mode == TemporalMode.Reset ? 0f : 3f).Within(0.12f));
            yield return null;
            var hud = Object.FindAnyObjectByType<SupervisorHud>();
            var panel = hud.GetComponentInParent<Canvas>().transform.Find("Supervisor HUD");
            Assert.That(hud.transform.Find("Sector Encounter").gameObject.activeSelf, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(panel.Find("Phase").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("SECURITY ASSESSMENT"));
            Assert.That(panel.Find("Hit Points").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("1800 / 1800"));
        }

        [UnityTest]
        public IEnumerator RecallPreservesDamageAndViolatedPointWhileBulletTimeSlowsAttack()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            Assert.That(_route.DebugSupervisor(1100, SupervisorAttack.RecordedViolation), Is.True);
            Frames(28);
            _director.DamageTarget(boss, boss.Body, 50f, out _);
            var initial = boss.State;
            var oldHealth = _player.Combat.Health.Current;
            var shot = new Ray(_player.AimRay.origin + Vector3.up * 25f, Vector3.forward);
            Assert.That(_director.PlayerProjectiles.TryLaunchPlayer(shot, Vector3.up, Vector3.zero, 12f, 2f, 30f, 0.04f, 35, Physics.DefaultRaycastLayers), Is.True);
            Assert.That(_director.PlayerProjectiles.TryReadFlight(0, out var before, out _, out _), Is.True);
            _director.Energy.Recharge();
            Frames(8, Input(TemporalMode.Recall));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Recall));
            Assert.That(boss.Health.Current, Is.EqualTo(initial.Health));
            Assert.That(boss.State.Attack.Point, Is.EqualTo(initial.Attack.Point));
            Assert.That(initial.Attack.Remaining - boss.State.Attack.Remaining, Is.EqualTo(Tick * 8).Within(0.003));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(oldHealth));
            Assert.That(_director.PlayerProjectiles.TryReadFlight(0, out var during, out _, out _), Is.True);
            Assert.That(during.origin.z, Is.GreaterThan(before.origin.z));

            var remaining = boss.State.Attack.Remaining;
            _director.Energy.Recharge();
            Frames(12, Input(TemporalMode.BulletTime));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
            Assert.That(remaining - boss.State.Attack.Remaining, Is.EqualTo(Tick * 3).Within(0.003));
        }

        [UnityTest]
        public IEnumerator CarbineAndScattergunUseTheirOwnTimelineDuringTheFight()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            var rear = Hitbox(boss, "Rear Core").bounds.center;
            var position = new Vector3(rear.x, 0.89f, rear.z + 2.15f);
            Place(position);
            var modifiers = (1 << (int)WeaponModifier.RicochetChamber) | (1 << (int)WeaponModifier.ExplosiveCore);
            Assert.That(_route.DebugLootWeapon(WeaponId.Compliance, WeaponRarity.Rare, modifiers, 611, true), Is.True);
            LookAt(rear);
            var health = boss.Health.Current;
            Frames(18, Input(TemporalMode.BulletTime, fire: true));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
            Assert.That(boss.Health.Current, Is.LessThan(health));

            Frames(1);
            Assert.That(_route.DebugLootWeapon(WeaponId.Termination, WeaponRarity.Rare, 1 << (int)WeaponModifier.SlugConversion, 512, true), Is.True);
            Frames(55);
            LookAt(rear);
            health = boss.Health.Current;
            Frames(1, new TemporalInput(default, default, false, false, true, default));
            _director.Energy.Recharge();
            Frames(25, Input(TemporalMode.Recall));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Recall));
            Assert.That(boss.Health.Current, Is.LessThan(health));
        }

        [UnityTest]
        public IEnumerator DeadlineHitsTheUpperCoreFromInvertedGravity()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            Assert.That(_route.DebugSupervisor(520, null), Is.True);
            Assert.That(_route.DebugLootWeapon(WeaponId.Deadline, WeaponRarity.Rare, 1 << (int)WeaponModifier.OverloadCell, 42, true), Is.True);
            var upper = Hitbox(boss, "Upper Core").bounds.center;
            var ceiling = _route.Current.CombatZone.bounds.max.y - 0.94f;
            _player.PlaceAt(new Vector3(upper.x + 1.6f, ceiling, upper.z - 1.4f), Quaternion.AngleAxis(180f, Vector3.forward));
            Physics.SyncTransforms();
            LookAt(upper);
            Assert.That(_player.AimUp.y, Is.LessThan(-0.99f));
            var health = boss.Health.Current;
            Advance(_route.Weapon.ChargeSeconds(_route.Weapon.ActiveSlot, TemporalMode.Normal) + 0.24f, new TemporalInput(default, default, false, false, false, default, fireHeld: true));
            Assert.That(boss.Health.Current, Is.LessThan(health));
        }

        [UnityTest]
        public IEnumerator PistolBuildCanStrikeWithKnifeAndArmBladeAndBullet()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            Assert.That(_route.Weapon.ActiveDefinition.Id, Is.EqualTo(WeaponId.Directive));
            var build = _route.Progression;
            Assert.That(build.DebugGrant(build.DebugDefinitions[(int)UpgradeId.BladeAndBullet]), Is.True);
            var rear = Hitbox(boss, "Rear Core").bounds.center;
            Place(new Vector3(rear.x, 0.89f, rear.z + 1.05f));
            LookAt(rear);
            var health = boss.Health.Current;
            Frames(1, new TemporalInput(default, default, false, false, false, default, meleePressed: true));
            Advance(0.56f);
            Assert.That(boss.Health.Current, Is.LessThan(health));
            Assert.That(build.PlayerEffects.State.BladeAndBullet, Is.Positive);
            health = boss.Health.Current;
            Frames(1, new TemporalInput(default, default, false, false, true, default));
            Advance(0.18f);
            Assert.That(boss.Health.Current, Is.LessThan(health));
            Assert.That(_route.Weapon.LastShotBoosted, Is.True);
        }

        [UnityTest]
        public IEnumerator SweepLocksBearingAndBurstEmitsFromTheWorldPool()
        {
            yield return EnterBoss();
            var boss = _route.Current.Boss;
            Assert.That(_route.DebugSupervisor(0, SupervisorAttack.SecuritySweep), Is.True);
            var locked = boss.State.Attack.Direction;
            Place(_player.transform.position + Vector3.right * 4f);
            Advance(0.55f);
            Assert.That(boss.State.Attack.Direction, Is.EqualTo(locked));
            Assert.That(boss.State.Attack.Stage, Is.EqualTo(SupervisorAttackStage.Warning));
            Assert.That(boss.State.Attack.BeamLength, Is.Positive);
            Advance(0.52f);
            Assert.That(boss.State.Attack.Stage, Is.EqualTo(SupervisorAttackStage.Active));

            Assert.That(_route.DebugSupervisor(0, SupervisorAttack.SuppressionBurst), Is.True);
            Advance(0.9f);
            Assert.That(_route.EnemyProjectiles.Generation, Is.Positive);
            Assert.That(_route.EnemyProjectiles.ActiveCount, Is.Positive);
        }

        [UnityTest]
        public IEnumerator RecordedViolationCanBeAvoidedAndInflictsOneHit()
        {
            yield return EnterBoss();
            Assert.That(_route.DebugSupervisor(1100, SupervisorAttack.RecordedViolation), Is.True);
            var health = _player.Combat.Health.Current;
            Advance(1.43f);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(health - 25));

            Assert.That(_route.DebugSupervisor(1100, SupervisorAttack.RecordedViolation), Is.True);
            Place(_player.transform.position + Vector3.right * 3.8f);
            Advance(1.45f);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(health - 25));
        }

        [UnityTest]
        public IEnumerator ReinforcementsRewindAndNeverClearTheLivingBoss()
        {
            yield return EnterBoss();
            var encounter = _route.Current;
            var boss = encounter.Boss;
            Assert.That(_route.DebugSupervisor(1100, SupervisorAttack.Reinforcements), Is.True);
            var roster = encounter.Enemies;
            var ids = roster.Select(enemy => ((ICombatDamageable)enemy).RewardId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(roster.Length));
            Assert.That(ids, Has.None.EqualTo(((ICombatDamageable)boss).RewardId));
            Advance(1.32f);
            Assert.That(roster.Count(enemy => enemy.EncounterReady), Is.EqualTo(4));
            Assert.That(boss.ReinforcementLimit, Is.EqualTo(4));

            _director.Energy.Recharge();
            Advance(0.61f, Input(TemporalMode.Override));
            Assert.That(roster.Count(enemy => enemy.EncounterReady), Is.Zero);
            Assert.That(boss.ReinforcementLimit, Is.Zero);
            Advance(1.25f);
            Assert.That(roster.Count(enemy => enemy.EncounterReady), Is.EqualTo(4));
            Assert.That(encounter.Enemies, Is.SameAs(roster));
            Assert.That(roster.Select(enemy => ((ICombatDamageable)enemy).RewardId), Is.EqualTo(ids));

            foreach (var enemy in roster)
            {
                if (enemy.EncounterReady)
                {
                    _director.DamageTarget(enemy, enemy.Body, 10000f, out _);
                }
            }

            _director.Advance(0d, default);
            Assert.That(encounter.LivingEnemies, Is.EqualTo(1));
            Assert.That(encounter.State, Is.EqualTo(EncounterState.Active));
            Assert.That(_director.CanDamagePlayer, Is.True);
            Assert.That(_route.Finished, Is.False);

            Assert.That(_route.DebugSupervisor(500, SupervisorAttack.Reinforcements), Is.True);
            Advance(1.50f);
            Assert.That(encounter.PendingReinforcements, Is.Zero);
            Assert.That(roster.Count(enemy => enemy.EncounterReady && enemy.Health.Alive), Is.EqualTo(4));
            Assert.That(boss.ReinforcementLimit, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator SimultaneousDeathAllowsRecallBeforeCommittingVictory()
        {
            yield return EnterBoss();
            Advance(0.5f);
            var boss = _route.Current.Boss;
            Assert.That(_director.DamageTarget(boss, boss.Body, 10000f, out _).Killed, Is.True);
            Assert.That(_director.DamagePlayer(10000), Is.True);
            _director.Advance(0d, default);
            Assert.That(_route.Finished, Is.False);
            Assert.That(_director.RecoveryAvailable, Is.True);
            Frames(3, Input(TemporalMode.Recall));
            Assert.That(_player.Combat.Health.Alive, Is.True);
            Assert.That(boss.Health.Alive, Is.False);
            Assert.That(_route.Finished, Is.True);
            Assert.That(_route.EnemiesDefeated, Is.EqualTo(1));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
        }

        private IEnumerator EnterBoss()
        {
            Assert.That(_route.DebugTravelToSupervisor(), Is.True);
            for (var frame = 0; frame < 300 && _route.Transferring; frame++)
            {
                yield return null;
            }

            Assert.That(_route.Transferring, Is.False);
            Assert.That(_route.RoomIndex, Is.EqualTo(5));
            Place(_route.ActiveRoom.transform.TransformPoint(new Vector3(0f, 0.89f, 8.2f)));
            _director.Advance(0d, default);
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Active));
        }

        private void Place(Vector3 point)
        {
            _player.PlaceAt(point, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private void WalkTo(Vector3 point)
        {
            var offset = Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up);
            for (var frame = 0; frame < 480 && offset.magnitude > 0.16f; frame++)
            {
                var before = _player.transform.position;
                var direction = Vector3.ClampMagnitude(offset * 3f, 1f);
                _director.Advance(Tick, new TemporalInput(new Vector2(direction.x, direction.z), default, false, false, false, default));
                Assert.That(Vector3.Distance(before, _player.transform.position), Is.LessThan(0.18f));
                offset = Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up);
            }

            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(0.16f), $"Could not reach {point} from {_player.transform.position}.");
        }

        private Collider Hitbox(SupervisorBoss boss, string label)
        {
            return boss.GetComponentsInChildren<Collider>().Single(collider => collider.name == label);
        }

        private void LookAt(Vector3 point)
        {
            var rig = _player.CameraRig;
            var up = _player.AimUp;
            var direction = (point - _player.AimRay.origin).normalized;
            var yaw = Vector3.SignedAngle(rig.Heading, Vector3.ProjectOnPlane(direction, up), up);
            var pitch = Mathf.Asin(Vector3.Dot(direction, up)) * Mathf.Rad2Deg + rig.Pitch;
            rig.Look(new Vector2(yaw, pitch) / 0.105f);
        }

        private void Advance(float seconds, in TemporalInput input = default)
        {
            Frames(Mathf.RoundToInt(seconds / (float)Tick), input);
        }

        private void Frames(int count, in TemporalInput input = default)
        {
            for (var index = 0; index < count; index++)
            {
                _director.Advance(Tick, input);
            }
        }

        private TemporalInput Input(TemporalMode mode, bool fire = false)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            return new TemporalInput(default, default, false, false, fire, selection);
        }
    }
}
