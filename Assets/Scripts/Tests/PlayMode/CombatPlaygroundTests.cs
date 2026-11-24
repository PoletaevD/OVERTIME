using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class CombatPlaygroundTests
    {
        private const double Step = TemporalTimeline.TickDuration;

        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private PlayerCombat _combat;
        private CombatWeapon _weapon;
        private CombatTarget _dummy;
        private CombatTarget _armor;
        private TemporalProjectileEmitter _emitter;

        [UnitySetUp]
        public IEnumerator OpenCombatPlayground()
        {
            yield return SceneManager.LoadSceneAsync("Combat Playground", LoadSceneMode.Single);
            yield return null;

            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _combat = _player.Combat;
            _weapon = _combat.Weapon;
            _dummy = GameObject.Find("Training Dummy 01").GetComponent<CombatTarget>();
            _armor = GameObject.Find("Armored Dummy").GetComponent<CombatTarget>();
            _emitter = GameObject.Find("Projectile Lane Emitter").GetComponent<TemporalProjectileEmitter>();
            _player.enabled = false;
            ResetDrill();
        }

        [TestCase(30, false)]
        [TestCase(60, false)]
        [TestCase(120, false)]
        [TestCase(144, false)]
        [TestCase(30, true)]
        [TestCase(60, true)]
        [TestCase(120, true)]
        [TestCase(144, true)]
        public void AutomaticFireUsesPlayerTimeAtEveryRenderRate(int framesPerSecond, bool slow)
        {
            var presentation = _player.GetComponentInChildren<WeaponPresentation>();
            var before = presentation.ConfirmedShots;
            var input = Input(slow ? TemporalMode.BulletTime : TemporalMode.Normal, fire: true);
            for (var frame = 0; frame < framesPerSecond; frame++)
            {
                _director.Advance(1d / framesPerSecond, input);
            }

            Assert.That(_weapon.Magazine, Is.EqualTo(20));
            Assert.That(_weapon.Reserve, Is.EqualTo(180));
            Assert.That(presentation.ConfirmedShots - before, Is.EqualTo(10));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(120));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(slow ? 30 : 120));
            Assert.That(_dummy.Health.Alive, Is.False);

            var kicked = _player.CameraRig.Pitch;
            Assert.That(kicked, Is.InRange(-8f, -6f));
            Tick(24);
            Assert.That(_weapon.Magazine, Is.EqualTo(20), "Releasing the trigger stops automatic fire.");
            Assert.That(_player.CameraRig.Pitch, Is.EqualTo(kicked).Within(0.0001f));
        }

        [Test]
        public void EmptyMagazineTriggersReloadAndHeldFireResumesOnlyAfterCompletion()
        {
            var presentation = _player.GetComponentInChildren<WeaponPresentation>();
            Tick(360, Input(fire: true));
            var remaining = presentation.ConfirmedShots;
            Assert.That(_weapon.Magazine, Is.Zero);

            Assert.That(_weapon.IsReloading, Is.True);
            var reloadTicks = (int)Math.Round(_weapon.Capture().ReloadRemaining / TemporalTimeline.TickDuration);
            Tick(reloadTicks - 1, Input(fire: true));
            Assert.That(presentation.ConfirmedShots, Is.EqualTo(remaining));
            Assert.That(_weapon.Magazine, Is.Zero);
            Assert.That(_weapon.IsReloading, Is.True);

            Tick(1, Input(fire: true));
            Assert.That(_weapon.Magazine, Is.EqualTo(29));
            Assert.That(_weapon.Reserve, Is.EqualTo(150));
            Assert.That(_weapon.IsReloading, Is.False);
            Assert.That(presentation.ConfirmedShots, Is.EqualTo(remaining + 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReloadProgressAndCooldownFollowPlayerSnapshots(bool slow)
        {
            Tick(25, Input(fire: true));
            var mode = slow ? TemporalMode.BulletTime : TemporalMode.Normal;
            Tick(1, Input(mode, reload: true));
            Tick(60, Input(mode));
            var earlier = _weapon.Capture();
            Tick(40, Input(mode));
            Assert.That(_weapon.ReloadProgress, Is.EqualTo(100f / 180f).Within(0.00001f));

            Tick(20, Input(TemporalMode.Recall));
            Assert.That(_weapon.Capture().ReloadRemaining, Is.EqualTo(earlier.ReloadRemaining));
            Assert.That(_weapon.Capture().Cooldown, Is.EqualTo(earlier.Cooldown));
            Assert.That(_weapon.Magazine, Is.EqualTo(27));
            Assert.That(_weapon.Reserve, Is.EqualTo(180));

            Tick(119, Input(mode));
            Assert.That(_weapon.IsReloading, Is.True);
            Tick(1, Input(mode));
            Assert.That(_weapon.IsReloading, Is.False);
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Assert.That(_weapon.Reserve, Is.EqualTo(177));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Override)]
        [TestCase((int)TemporalMode.Recall)]
        public void RewindModesCannotConfirmNewShots(int modeValue)
        {
            var presentation = _player.GetComponentInChildren<WeaponPresentation>();
            Tick(80);
            var before = presentation.ConfirmedShots;
            Tick(20, Input((TemporalMode)modeValue, fire: true));
            Assert.That(presentation.ConfirmedShots, Is.EqualTo(before));
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void ProjectileUsesTheAimRayAndTheArmoredCoreDoublesDamage()
        {
            var torso = Body(_armor).bounds.center;
            MoveTo(new Vector3(torso.x + 0.35f, 0.89f, -3.6f));
            Tick(1, Input(fire: true));
            Assert.That(_armor.Health.Current, Is.EqualTo(160));
            Tick(40);
            Assert.That(_armor.Health.Current, Is.EqualTo(148));

            var weak = _armor.transform.Find("Live Target/Weak Point").GetComponent<Collider>();
            var verticalOffset = weak.bounds.center.y - _player.AimRay.origin.y;
            MoveTo(new Vector3(weak.bounds.center.x, _player.transform.position.y + verticalOffset, _player.transform.position.z));
            Tick(1, Input(fire: true));
            Tick(40);
            Assert.That(_armor.Health.Current, Is.EqualTo(124));
        }

        [Test]
        public void CeilingFireUsesTheGravityAlignedAimRay()
        {
            var ceiling = GameObject.Find("Ceiling Training Dummy").GetComponent<CombatTarget>();
            var rotation = Quaternion.AngleAxis(180f, Vector3.forward);
            _player.GetComponent<GravityMotor>().ResetAt(new Vector3(1f, 6.11f, 4f), rotation);
            _player.GetComponent<GravityCamera>().ResetView(rotation);
            Tick(1, Input(fire: true));
            Assert.That(ceiling.Health.Current, Is.EqualTo(60));
            Tick(24);
            Assert.That(ceiling.Health.Current, Is.EqualTo(48));
        }

        [Test]
        public void WallFireUsesTheGravityAlignedMuzzle()
        {
            var screen = new GameObject("Wall Shooting Target");
            try
            {
                var rotation = Quaternion.AngleAxis(90f, Vector3.forward);
                _player.GetComponent<GravityMotor>().ResetAt(new Vector3(-6.11f, 3.35f, -2f), rotation);
                _player.GetComponent<GravityCamera>().ResetView(rotation);
                var aim = _player.AimRay;
                screen.transform.position = aim.GetPoint(4f);
                screen.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.02f);
                var point = Vector3.zero;
                _director.ProjectileHit += impact =>
                {
                    if (impact.FromPlayer)
                    {
                        point = impact.Point;
                    }
                };

                Tick(1, Input(fire: true));
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
                Assert.That(point, Is.EqualTo(Vector3.zero));
                Tick(15);
                Assert.That(Vector3.Distance(point, screen.transform.position), Is.LessThan(0.12f));
            }
            finally
            {
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void AShotDuringGravityTransitionStopsOnTheAdjacentWall()
        {
            var motor = _player.GetComponent<GravityMotor>();
            var rig = _player.GetComponent<GravityCamera>();
            var facingWall = Quaternion.Euler(0f, 90f, 0f);
            motor.ResetAt(new Vector3(5.8f, 0.89f, -5f), facingWall);
            rig.ResetView(facingWall);
            Physics.SyncTransforms();
            var approach = new TemporalInput(Vector2.up, default, true, true, false, default(TemporalSelection));
            Tick(1, approach);
            var hold = new TemporalInput(Vector2.up, default, false, true, false, default(TemporalSelection));
            for (var step = 0; step < 20 && !motor.Transitioning; step++)
            {
                Tick(1, hold);
            }

            Assert.That(motor.Transitioning, Is.True);
            var impacts = 0;
            _director.ProjectileHit += impact => impacts += impact.FromPlayer ? 1 : 0;
            Tick(1, new TemporalInput(Vector2.up, default, false, true, true, default(TemporalSelection)));
            Assert.That(_director.PlayerProjectiles.Generation, Is.EqualTo(1));
            Assert.That(impacts, Is.Zero);
            Tick(30);
            Assert.That(impacts, Is.EqualTo(1));
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void RecallRestoresTheExactTimeUntilTheNextShot()
        {
            Tick(12);
            Tick(6, Input(fire: true));
            var cooldown = _weapon.Capture().Cooldown;
            Tick(8, Input(fire: true));
            Assert.That(_weapon.Magazine, Is.EqualTo(28));

            Tick(4, Input(TemporalMode.Recall));
            Assert.That(_weapon.Magazine, Is.EqualTo(29));
            Assert.That(_weapon.Capture().Cooldown, Is.EqualTo(cooldown));
            Tick(6, Input(fire: true));
            Assert.That(_weapon.Magazine, Is.EqualTo(29));
            Tick(1, Input(fire: true));
            Assert.That(_weapon.Magazine, Is.EqualTo(28));
        }

        [Test]
        public void DummyIsDestroyedAtZeroAndCanReceiveDamageAgainAfterWorldRewind()
        {
            Tick(12);
            var body = Body(_dummy);
            Tick(49, Input(fire: true));
            Tick(32);
            Assert.That(_dummy.Health.Current, Is.Zero);
            Assert.That(body.gameObject.activeInHierarchy, Is.False);
            Assert.That(_dummy.transform.Find("Wreck").gameObject.activeSelf, Is.True);
            Assert.That(_director.DamageTarget(_dummy, body, 12, out _).Amount, Is.Zero);

            Tick(40, Input(TemporalMode.Override));
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(_dummy.transform.Find("Wreck").gameObject.activeSelf, Is.False);
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
            Tick(25);
            Assert.That(_dummy.Health.Current, Is.EqualTo(48));
        }

        [TestCase((int)TemporalMode.Reset, 100, 30, 60)]
        [TestCase((int)TemporalMode.Override, 60, 25, 60)]
        [TestCase((int)TemporalMode.Recall, 100, 30, 0)]
        public void ModesRestoreOnlyTheirOwnCombatState(int modeValue, int health, int magazine, int targetHealth)
        {
            Tick(30);
            Tick(49, Input(fire: true));
            Tick(32);
            Assert.That(_dummy.Health.Current, Is.Zero);
            Assert.That(_director.DamagePlayer(40), Is.True);
            Tick(1);

            var world = _director.WorldTimeline.Cursor;
            Tick(50, Input((TemporalMode)modeValue));
            Assert.That(_combat.Health.Current, Is.EqualTo(health));
            Assert.That(_weapon.Magazine, Is.EqualTo(magazine));
            Assert.That(_dummy.Health.Current, Is.EqualTo(targetHealth));
            if ((TemporalMode)modeValue == TemporalMode.Recall)
            {
                Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world + 50));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorldRewindDiscardsAProjectileLaunchedBetweenWorldTicks(bool reset)
        {
            Tick(12);
            var world = _director.WorldTimeline.Cursor;
            Tick(1, Input(TemporalMode.BulletTime, fire: true));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world));

            _director.Advance(0d, Input(reset ? TemporalMode.Reset : TemporalMode.Override));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);
            Assert.That(_weapon.Magazine, Is.EqualTo(29), "At the instant rewind starts, the player tick containing the shot still exists.");
            Tick(1, Input(reset ? TemporalMode.Reset : TemporalMode.Override));
            Assert.That(_weapon.Magazine, Is.EqualTo(reset ? 30 : 29));
            Tick(35);
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void ImpactBetweenWorldTicksIsRolledBackWithoutPublishingItAgain()
        {
            Tick(12);
            Tick(1, Input(fire: true));
            Tick(24);
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
            var world = _director.WorldTimeline.Cursor;
            var impacts = 0;
            _director.ProjectileHit += impact => impacts += impact.FromPlayer ? 1 : 0;

            Tick(1, Input(TemporalMode.BulletTime));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world));
            Assert.That(_dummy.Health.Current, Is.EqualTo(48));
            Assert.That(impacts, Is.EqualTo(1));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);

            _director.Advance(0d, Input(TemporalMode.Override));
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
            Assert.That(impacts, Is.EqualTo(1));
            Tick(1);
            Assert.That(_dummy.Health.Current, Is.EqualTo(48));
            Assert.That(impacts, Is.EqualTo(2));
        }

        [Test]
        public void SequentialRewindsCommitIndependentBranches()
        {
            Tick(24);
            Tick(1, Input(fire: true));
            Tick(4);
            Assert.That(_director.DamagePlayer(15), Is.True);
            Tick(1);
            Tick(2, Input(TemporalMode.Reset));
            Assert.That(_weapon.Magazine, Is.EqualTo(29));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));

            Tick(2, Input(TemporalMode.Override));
            Assert.That(_weapon.Magazine, Is.EqualTo(29));
            Assert.That(_combat.Health.Current, Is.EqualTo(100));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);

            Tick(2, Input(TemporalMode.Recall));
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Tick(30);
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);
            Assert.That(_dummy.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void RecallLetsAnExistingBoltKillAndPayItsReward()
        {
            Tick(85);
            Assert.That(_director.DamageTarget(_dummy, Body(_dummy), 48, out _).Amount, Is.EqualTo(48));
            Tick(1);
            _director.Energy.Spend(50d);
            Tick(1, Input(fire: true));
            Assert.That(_dummy.Health.Current, Is.EqualTo(12));
            Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));

            Tick(30, Input(TemporalMode.Recall, fire: true));
            Assert.That(_dummy.Health.Alive, Is.False);
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Assert.That(_director.Energy.Current, Is.EqualTo(57f).Within(0.0001f));

            Tick(2);
            Tick(8, Input(TemporalMode.Override));
            Assert.That(_dummy.Health.Alive, Is.True);
            var energy = _director.Energy.Current;
            Tick(22);
            Assert.That(_dummy.Health.Alive, Is.False);
            Assert.That(_director.Energy.Current, Is.EqualTo(energy));
        }

        [Test]
        public void ExistingBoltCanKillDuringLethalRecovery()
        {
            Tick(85);
            Assert.That(_director.DamageTarget(_dummy, Body(_dummy), 48, out _).Amount, Is.EqualTo(48));
            Tick(1, Input(fire: true));
            Assert.That(_director.DamagePlayer(100), Is.True);
            Tick(30);
            Assert.That(_combat.GameOver, Is.False);
            Assert.That(_dummy.Health.Alive, Is.False);
        }

        [Test]
        public void BulletTimeAndNormalUseTheSameProjectileSpeedWithoutADuplicateStep()
        {
            Tick(1, Input(fire: true));
            var pool = _director.PlayerProjectiles;
            var bolt = pool.transform.GetChild(0);
            var start = bolt.position;
            Tick(4, Input(TemporalMode.BulletTime));
            Assert.That(Vector3.Distance(start, bolt.position), Is.EqualTo(1.5f).Within(0.001f));
            var middle = bolt.position;
            Tick(4);
            Assert.That(Vector3.Distance(middle, bolt.position), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(pool.Generation, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SweptCollisionBlocksThinCoverAndAnObstructedMuzzle(bool acrossMuzzle)
        {
            var wall = new GameObject("Thin Test Screen");
            try
            {
                var collider = wall.AddComponent<BoxCollider>();
                var eye = _player.AimRay.origin;
                wall.transform.position = eye + (acrossMuzzle ? new Vector3(0.19f, -0.13f, 0.58f) : Vector3.forward * 3f);
                collider.size = new Vector3(acrossMuzzle ? 0.13f : 2f, acrossMuzzle ? 0.22f : 2f, 0.015f);
                var shots = 0;
                var impacts = 0;
                _weapon.ShotFired += () => shots++;
                _director.ProjectileHit += impact => impacts += impact.FromPlayer ? 1 : 0;

                Tick(1, Input(fire: true));
                Assert.That(shots, Is.EqualTo(1));
                Assert.That(impacts, Is.Zero);
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));

                Tick(11);
                Assert.That(impacts, Is.EqualTo(1));
                Assert.That(_dummy.Health.Current, Is.EqualTo(60));
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void FullProjectilePoolDoesNotSpendAmmoOrEvictAProjectile()
        {
            var pool = _director.PlayerProjectiles;
            for (var index = 0; index < pool.PoolCapacity; index++)
            {
                Assert.That(pool.TryLaunchPlayer(_player.AimRay, _player.AimUp, Vector3.zero, 0.1f, 20f, 20f, 0.045f, 12, Physics.DefaultRaycastLayers), Is.True);
            }

            var generation = pool.Generation;
            var shots = 0;
            _weapon.ShotFired += () => shots++;
            Tick(1, Input(fire: true));
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Assert.That(shots, Is.Zero);
            Assert.That(pool.ActiveCount, Is.EqualTo(pool.PoolCapacity));
            Assert.That(pool.Generation, Is.EqualTo(generation));
        }

        [Test]
        public void SweptProjectileHitsTheCapsuleAndReappearsOnWorldRewind()
        {
            MoveTo(new Vector3(-5f, 0.89f, 9f));
            var slot = _emitter.transform.Find("Projectile Slot 1").gameObject;
            Tick(90);
            Assert.That(slot.activeSelf, Is.True);
            for (var tick = 0; tick < 45 && _combat.Health.Current == 100; tick++)
            {
                Tick(1);
            }

            Assert.That(_combat.Health.Current, Is.EqualTo(85));
            Assert.That(slot.activeSelf, Is.False);
            Tick(3, Input(TemporalMode.Override));
            Assert.That(slot.activeSelf, Is.True);
            Assert.That(_combat.Health.Current, Is.EqualTo(85));
            Assert.That(_director.DamagePlayer(15), Is.False);
            Tick(8);
            Assert.That(_combat.Health.Current, Is.EqualTo(70));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecallProtectsPlayerAndBulletTimeDelaysProjectileWithoutReducingItsDamage(bool slow)
        {
            MoveTo(new Vector3(-5f, 0.89f, 5f));
            Tick(150);
            Assert.That(_combat.Health.Current, Is.EqualTo(100));
            var world = _director.WorldTimeline.Cursor;
            Tick(slow ? 24 : 36, Input(slow ? TemporalMode.BulletTime : TemporalMode.Recall));
            Assert.That(_combat.Health.Current, Is.EqualTo(100));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world + (slow ? 6 : 36)));

            if (!slow)
            {
                Assert.That(_director.DamagePlayer(15), Is.False);
            }

            Tick(90);
            Assert.That(_combat.Health.Current, Is.EqualTo(85));
        }

        [TestCase((int)TemporalMode.Reset, 18f)]
        [TestCase((int)TemporalMode.Override, 20f)]
        [TestCase((int)TemporalMode.Recall, 26f)]
        [TestCase((int)TemporalMode.BulletTime, 8f)]
        public void PowersChargeOnlyTheirOwnTimeAndEnergyIsOutsideSnapshots(int modeValue, float cost)
        {
            Tick(180);
            Tick(60, Input((TemporalMode)modeValue));
            Assert.That(_director.Energy.Current, Is.EqualTo(100f - cost).Within(0.00001f));
            Tick(12);
            Assert.That(_director.Energy.Current, Is.EqualTo(100f - cost).Within(0.00001f));
        }

        [Test]
        public void BulletTimeChargesForRealTimeAcrossALongFrame()
        {
            _director.Advance(0.5d, Input(TemporalMode.BulletTime));
            Assert.That(_director.Energy.Current, Is.EqualTo(92f));
            Assert.That(_director.PlayerSteps, Is.EqualTo(12));
            Assert.That(_director.WorldSteps, Is.EqualTo(3));
        }

        [Test]
        public void ResetStopsChargingAtTheOldestSnapshot()
        {
            Tick(5);
            Tick(20, Input(TemporalMode.Reset));
            Assert.That(_director.PlayerTimeline.AtStart, Is.True);
            Assert.That(_director.WorldTimeline.AtStart, Is.True);
            Assert.That(_director.Energy.Current, Is.EqualTo(100f - 18f * 5f * (float)Step).Within(0.00001f));
            var balance = _director.Energy.Current;

            Tick(150, Input(TemporalMode.Reset));
            Assert.That(_director.Energy.Current, Is.EqualTo(balance));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Override)]
        [TestCase((int)TemporalMode.Recall)]
        [TestCase((int)TemporalMode.BulletTime)]
        public void DepletionCommitsTheBranchAndRequiresRelease(int modeValue)
        {
            var mode = (TemporalMode)modeValue;
            Tick(60);
            _director.Energy.Spend(99.5d);
            Tick(30, Input(mode));
            Assert.That(_director.Energy.Current, Is.Zero);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(_director.PlayerTimeline.Latest));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(_director.WorldTimeline.Latest));

            _director.Energy.Recharge();
            Tick(3, Input(mode));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            Tick(1, Input(mode, release: true));
            Tick(1, Input(mode));
            Assert.That(_director.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void StationRefillsAndKillRewardIsPaidOnceUntilDebugReset()
        {
            Tick(24);
            _director.Energy.Spend(50d);
            var body = Body(_dummy);
            Assert.That(_director.DamageTarget(_dummy, body, 60, out _).Killed, Is.True);
            Assert.That(_director.Energy.Current, Is.EqualTo(70f));
            Tick(1);
            Tick(1, Input(TemporalMode.Override));
            Assert.That(_dummy.Health.Alive, Is.True);
            Tick(1);

            var balance = _director.Energy.Current;
            Assert.That(_director.DamageTarget(_dummy, body, 60, out _).Killed, Is.True);
            Assert.That(_director.Energy.Current, Is.EqualTo(balance));

            MoveTo(new Vector3(3.60f, 0.89f, -5f));
            Assert.That(_director.CanRecharge, Is.True);
            Tick(1, Input(recharge: true));
            Assert.That(_director.Energy.Current, Is.EqualTo(100f));

            ResetDrill();
            Assert.That(_director.Energy.Current, Is.EqualTo(100f));
            _director.Energy.Spend(50d);
            Assert.That(_director.DamageTarget(_dummy, body, 60, out _).Killed, Is.True);
            Assert.That(_director.Energy.Current, Is.EqualTo(70f));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Recall)]
        public void LethalDamageStopsInputAndCanBeRescuedByPlayerRewind(int modeValue)
        {
            Tick(30);
            Assert.That(_director.DamagePlayer(100), Is.True);
            Tick(3, Input(fire: true));
            Assert.That(_combat.Alive, Is.False);
            Assert.That(_combat.GameOver, Is.False);
            Assert.That(_director.RecoveryAvailable, Is.True);
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            var deathPosition = _player.transform.position;
            Tick(10, Input(TemporalMode.Override, move: Vector2.right, fire: true));
            Assert.That(_player.transform.position, Is.EqualTo(deathPosition));
            Assert.That(_combat.Health.Current, Is.Zero);

            Tick(9, Input((TemporalMode)modeValue));
            Assert.That(_combat.Health.Current, Is.EqualTo(100));
            Assert.That(_combat.RecoveryRemaining, Is.Zero);
            Assert.That(_combat.GameOver, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GameOverIsFinalUntilTheSceneRetryAction(bool hasEnergy)
        {
            Tick(30);
            if (!hasEnergy)
            {
                _director.Energy.Spend(100d);
            }

            Assert.That(_director.DamagePlayer(100), Is.True);
            Tick(1);
            if (hasEnergy)
            {
                // The deadline uses actual time even though fixed stepping clamps long frames.
                _director.Advance(2.1d, Input());
            }

            Assert.That(_combat.GameOver, Is.True);
            Assert.That(_director.RecoveryAvailable, Is.False);
            Tick(3, Input(TemporalMode.Recall));
            Assert.That(_combat.Health.Current, Is.Zero);

            var button = _player.GetComponentInChildren<Button>(true);
            button.onClick.Invoke();
            Tick(1);
            Assert.That(_combat.GameOver, Is.False);
            Assert.That(_combat.Health.Current, Is.EqualTo(100));
            Assert.That(_weapon.Magazine, Is.EqualTo(30));
            Assert.That(_weapon.Reserve, Is.EqualTo(180));
            Assert.That(_director.Energy.Current, Is.EqualTo(100f));
            Assert.That(_director.PlayerTimeline.Cursor, Is.Zero);
            Assert.That(_director.WorldTimeline.Cursor, Is.Zero);
        }

        [Test]
        public void RecoveryDoesNotPromiseAnUnaffordableLivingSnapshot()
        {
            Tick(30);
            _director.Energy.Spend(99.99d);
            Assert.That(_director.DamagePlayer(100), Is.True);
            Tick(1);
            Assert.That(_director.Energy.Current, Is.GreaterThan(0f));
            Assert.That(_director.RecoveryAvailable, Is.False);
            Assert.That(_combat.GameOver, Is.True);
        }

        [UnityTest]
        public IEnumerator CombatKeyboardUsesF5ForDebugReset()
        {
            Tick(1, Input(fire: true));
            using var keyboard = new SyntheticKeyboard();
            var input = _player.GetComponent<PlaygroundInput>();
            input.SendMessage("OnApplicationFocus", true);
            _player.enabled = true;
            try
            {
                InputSystem.QueueStateEvent(keyboard.Device, new KeyboardState(Key.R));
                InputSystem.Update();
                Assert.That(input.ResetPressed, Is.False);
                yield return null;
                Assert.That(_weapon.Magazine, Is.EqualTo(29));

                InputSystem.QueueStateEvent(keyboard.Device, new KeyboardState(Key.F5));
                InputSystem.Update();
                Assert.That(input.ResetPressed, Is.True);
                yield return null;
                Assert.That(_weapon.Magazine, Is.EqualTo(30));
            }
            finally
            {
                _player.enabled = false;
            }
        }

        [Test]
        public void CombatSimulationUsesBoundedStorageAndDoesNotSpawnObjectsPerShot()
        {
            Tick(60);
            var input = Input();
            for (var index = 0; index < 20; index++)
            {
                Tick(24, input);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 20; index++)
            {
                Tick(24, input);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);

            var count = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Tick(360, Input(fire: true));
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(count));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(_director.PlayerTimeline.Capacity));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(_director.WorldTimeline.Capacity));
        }

        private void MoveTo(Vector3 position)
        {
            _player.GetComponent<GravityMotor>().ResetAt(position, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static Collider Body(CombatTarget target)
        {
            return target.transform.Find("Live Target/Hit Plate").GetComponent<Collider>();
        }

        private void ResetDrill()
        {
            _director.Advance(0d, new TemporalInput(default, default, false, false, false, default(TemporalSelection), debugReset: true));
        }

        private void Tick(int ticks, TemporalInput input = default)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                _director.Advance(Step, input);
            }
        }

        private static TemporalInput Input(TemporalMode mode = TemporalMode.Normal, bool fire = false, bool reload = false, bool recharge = false, bool release = false, Vector2 move = default)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            if (release)
            {
                selection.Release(mode);
            }

            return new TemporalInput(move, default, false, false, false, selection, fireHeld: fire, reloadPressed: reload, rechargePressed: recharge);
        }
    }
}
