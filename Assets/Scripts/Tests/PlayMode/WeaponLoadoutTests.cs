#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class WeaponLoadoutTests
    {
        private const double Step = TemporalTimeline.TickDuration;
        private const int SwitchTicks = 37;

        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private CombatWeapon _gun;
        private TemporalProjectileEmitter _pool;
        private RunUpgradeState _build;
        private PlayerUpgradeEffects _effects;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Security Sector", LoadSceneMode.Single);
            yield return null;
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _player.enabled = false;
            _gun = _player.Combat.Weapon;
            _pool = _director.PlayerProjectiles;
        }

        [TearDown]
        public void RemoveTestEffects()
        {
            _effects?.Unbind();
        }

        [Test]
        public void ShotgunVolleyIsAtomicAndUsesOneRoundAndOneBoost()
        {
            Equip(0);
            Grant(UpgradeId.KineticCharge, UpgradeId.SecondDraft, UpgradeId.LastWord);
            Switch(WeaponSlotInput.Secondary);
            var shotState = _gun.Capture();
            shotState.Secondary.Magazine = 1;
            _gun.Restore(shotState);
            _effects.Restore(new PlayerUpgradeEffects.Snapshot { Kinetic = 2f, SecondDraft = 2f });

            var shots = 0;
            _gun.ShotFired += () => shots++;
            for (var slot = 0; slot < _pool.PoolCapacity - 7; slot++)
            {
                Assert.That(_pool.TryLaunchPlayer(TestRay, Vector3.up, Vector3.zero, 0.1f, 5f, 10f, 0.03f, 1, Physics.DefaultRaycastLayers), Is.True);
            }

            Fire();
            Assert.That(_gun.Magazine, Is.EqualTo(1));
            Assert.That(_pool.Generation, Is.EqualTo(_pool.PoolCapacity - 7));
            Assert.That(_effects.Charged, Is.True);
            Assert.That(shots, Is.Zero);
            _pool.ClearProjectiles();

            Fire();
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(_gun.State(0).Magazine, Is.EqualTo(30));
            Assert.That(_gun.Magazine, Is.Zero);
            Assert.That(_effects.Charged, Is.False);
            Assert.That(_pool.ActiveCount, Is.EqualTo(8));
            for (var slot = 0; slot < 8; slot++)
            {
                Assert.That(_pool.TryReadProjectile(slot, out _, out _, out var damage, out _), Is.True);
                Assert.That(damage, Is.EqualTo(9f * 1.35f * 1.75f * 1.5f).Within(0.001f));
            }
        }

        [Test]
        public void TwinCountsConfirmedVolleysAndFiresOneExtraBolt()
        {
            Equip(0);
            Grant(UpgradeId.TwinOutput);
            Switch(WeaponSlotInput.Secondary);
            for (var shot = 1; shot <= 6; shot++)
            {
                Fire();
                Assert.That(_pool.ActiveCount, Is.EqualTo(shot == 5 ? 9 : 8));
                Assert.That(_gun.Magazine, Is.EqualTo(6 - shot));
                if (shot == 5)
                {
                    Assert.That(_pool.TryReadProjectile(8, out _, out _, out var damage, out _), Is.True);
                    Assert.That(damage, Is.EqualTo(5.4f).Within(0.0001f));
                }

                _pool.ClearProjectiles();
                for (var tick = 0; tick < 72; tick++)
                {
                    Fire(trigger: false);
                }
            }

            Assert.That(_gun.Capture().TwinCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeadlineRequiresHeldChargeAndUsesPlayerTime(bool bulletTime)
        {
            Equip(1);
            Switch(WeaponSlotInput.Secondary);
            var mode = bulletTime ? TemporalMode.BulletTime : TemporalMode.Normal;
            var shots = 0;
            _gun.ShotFired += () => shots++;
            Tick(40, mode, hold: true);
            Assert.That(_gun.ChargeProgress, Is.EqualTo(40f / 84f).Within(0.0001f));
            Assert.That(_gun.Magazine, Is.EqualTo(4));
            Assert.That(shots, Is.Zero);
            Tick(1, mode);
            Assert.That(_gun.ChargeProgress, Is.Zero);

            Tick(83, mode, hold: true);
            Assert.That(_gun.Magazine, Is.EqualTo(4));
            Tick(1, mode, hold: true);
            Assert.That(_gun.Magazine, Is.EqualTo(3));
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(_gun.Capture().Cooldown, Is.EqualTo(0.55d).Within(0.00001d));
            Assert.That(_pool.ActiveCount, Is.EqualTo(1));
        }

        [TestCase(30, 0)]
        [TestCase(60, 0)]
        [TestCase(120, 0)]
        [TestCase(144, 0)]
        [TestCase(30, 1)]
        [TestCase(60, 1)]
        [TestCase(120, 1)]
        [TestCase(144, 1)]
        public void SecondaryWeaponsKeepTheirCadenceAcrossRenderRates(int framesPerSecond, int choice)
        {
            Equip(choice);
            Switch(WeaponSlotInput.Secondary);
            var shots = 0;
            _gun.ShotFired += () => shots++;
            var input = new TemporalInput(default, default, false, false, false, default, fireHeld: true);
            for (var frame = 0; frame < framesPerSecond * 2; frame++)
            {
                _director.Advance(1d / framesPerSecond, input);
            }

            Assert.That(shots, Is.EqualTo(choice == 0 ? 4 : 2));
            Assert.That(_gun.Magazine, Is.EqualTo(2));
            Assert.That(_pool.Generation, Is.EqualTo(choice == 0 ? 32 : 2));
        }

        [UnityTest]
        public IEnumerator ViewmodelFollowsARecalledMidpointWithoutAnExtraShot()
        {
            Equip(1);
            var presentation = _player.GetComponentInChildren<WeaponPresentation>();
            var before = presentation.ConfirmedShots;
            Tick(1, selection: WeaponSlotInput.Secondary);
            Tick(20);
            yield return null;
            Assert.That(GameObject.Find("ARC-03 Deadline Viewmodel"), Is.Not.Null);
            Assert.That(_gun.IsSwitching, Is.True);

            Tick(3, TemporalMode.Recall);
            yield return null;
            Assert.That(GameObject.Find("ARC-03 Deadline Viewmodel"), Is.Null);
            Assert.That(_gun.IsSwitching, Is.True);

            Tick(7);
            yield return null;
            Assert.That(GameObject.Find("ARC-03 Deadline Viewmodel"), Is.Not.Null);
            Assert.That(presentation.ConfirmedShots, Is.EqualTo(before));
        }

        [Test]
        public void ExchangeCancelsActionsAndDoesNotResetWeaponCooldown()
        {
            Equip(1);
            Fire();
            var cooldown = _gun.Capture().Cooldown;
            Tick(1, reload: true);
            Assert.That(_gun.IsReloading, Is.True);
            Tick(1, hold: true, selection: WeaponSlotInput.Secondary);
            Assert.That(_gun.IsReloading, Is.False);
            Assert.That(_gun.IsSwitching, Is.True);
            Assert.That(_gun.State(0).Cooldown, Is.EqualTo(cooldown - 2d * Step).Within(0.0001d));
            Tick(19, hold: true);
            Assert.That(_gun.ActiveSlot, Is.EqualTo(1));
            Assert.That(_gun.Magazine, Is.EqualTo(4));
            Assert.That(_gun.ChargeProgress, Is.Zero);
            Tick(17);
            Tick(35, hold: true);
            Assert.That(_gun.IsCharging, Is.True);
            Tick(1, selection: WeaponSlotInput.Primary);
            Assert.That(_gun.IsCharging, Is.False);
            Tick(36);
            Assert.That(_gun.ActiveSlot, Is.Zero);
            Assert.That(_gun.Magazine, Is.EqualTo(29));
            Assert.That(_gun.Reserve, Is.EqualTo(180));
            Assert.That(_gun.State(1).Magazine, Is.EqualTo(4));
        }

        [TestCase((int)TemporalMode.Reset)]
        [TestCase((int)TemporalMode.Recall)]
        public void PlayerRewindRestoresSlotsChargeAndSwitchPhaseWithoutPublishingEvents(int mode)
        {
            Equip(1);
            Grant(UpgradeId.TacticalExchange);
            Tick(1, hold: true);
            Tick(1, hold: true, selection: WeaponSlotInput.Secondary);
            Tick(6);
            var inSwitch = _gun.Capture();
            Tick(30);
            Tick(20, hold: true);
            Assert.That(_gun.ActiveSlot, Is.EqualTo(1));
            var charging = _gun.ChargeProgress;
            Assert.That(_effects.State.Exchange, Is.EqualTo(2f));
            Tick(20, hold: true);
            var shots = 0;
            var switches = 0;
            _gun.ShotFired += () => shots++;
            _gun.SwitchStarted += () => switches++;
            Tick(10, (TemporalMode)mode);
            Assert.That(_gun.ChargeProgress, Is.EqualTo(charging).Within(0.000001f));
            Tick(25, (TemporalMode)mode);
            Assert.That(_gun.ActiveSlot, Is.Zero);
            Assert.That(_gun.Capture().SwitchRemaining, Is.EqualTo(inSwitch.SwitchRemaining));
            Assert.That(_gun.State(0).Magazine, Is.EqualTo(29));
            Assert.That(_gun.State(1).Magazine, Is.EqualTo(4));
            Assert.That(_effects.State.Exchange, Is.Zero);
            Assert.That(shots, Is.Zero);
            Assert.That(switches, Is.Zero);
        }

        [Test]
        public void OverridePreservesTheCurrentLoadoutAndExactCharge()
        {
            Equip(1);
            Switch(WeaponSlotInput.Secondary);
            Tick(28, hold: true);
            var prior = _gun.Capture();
            Tick(9, TemporalMode.Override, hold: true, selection: WeaponSlotInput.Primary);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Override));
            Assert.That(_gun.ActiveSlot, Is.EqualTo(1));
            Assert.That(_gun.State(0).Magazine, Is.EqualTo(prior.Primary.Magazine));
            Assert.That(_gun.State(1).Magazine, Is.EqualTo(prior.Secondary.Magazine));
            Assert.That(_gun.State(1).ChargeElapsed, Is.EqualTo(prior.Secondary.ChargeElapsed));
        }

        [Test]
        public void RailBoltPiercesDistinctTargetsAndRewindRestoresItsHitLedger()
        {
            Equip(1);
            var first = Target(1, 7f, doubleCollider: true);
            var second = Target(2, 10f);
            var third = Target(3, 12f);
            var fourth = Target(4, 14f);
            Launch(new ProjectileBallistics(WeaponId.Deadline, 3, 0, 0.85f, 0f, 0f, 0f, 0f, 0f), 90f);
            _pool.Simulate(0.022f);
            Assert.That(first.Hits, Is.EqualTo(1));
            Assert.That(_pool.TryReadProjectile(0, out var left, out _, out _, out _), Is.True);
            Assert.That(left, Is.EqualTo(1));
            _pool.Capture(1);
            _pool.Simulate(0.095f);
            Assert.That(first.Hits, Is.EqualTo(1));
            Assert.That(second.EffectiveDamage, Is.EqualTo(76.5f).Within(0.01f));
            Assert.That(third.EffectiveDamage, Is.EqualTo(65.025f).Within(0.01f));
            Assert.That(fourth.Hits, Is.Zero);
            Assert.That(_pool.ActiveCount, Is.Zero);

            _pool.Restore(1);
            Assert.That(first.Hits, Is.EqualTo(1));
            Assert.That(second.Hits, Is.EqualTo(1));
            Assert.That(third.Hits, Is.EqualTo(1));
            Assert.That(_pool.TryReadProjectile(0, out left, out _, out _, out _), Is.True);
            Assert.That(left, Is.EqualTo(1));
            _pool.Simulate(0.095f);
            Assert.That(first.Hits, Is.EqualTo(1));
            Assert.That(second.Hits, Is.EqualTo(2));
            Assert.That(third.Hits, Is.EqualTo(2));
        }

        [Test]
        public void RicochetLeavesTheSurfaceAndSnapshotsDistanceAndRemainingBounce()
        {
            Equip(1);
            var wall = new GameObject("Reflection test wall");
            wall.transform.position = TestRay.GetPoint(3f);
            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 2f, 0.2f);
            var flight = new ProjectileBallistics(WeaponId.Deadline, 3, 1, 0.85f, 0.8f, 0.015f, 0.45f, 0.30f, 5f);
            Physics.SyncTransforms();
            Launch(flight, 60f);
            _pool.Capture(1);
            _pool.Simulate(0.04f);
            var bolt = _pool.transform.GetChild(0);
            Assert.That(bolt.forward.z, Is.LessThan(-0.99f));
            Assert.That(bolt.position.z, Is.LessThan(wall.transform.position.z - 0.13f));
            Assert.That(_pool.TryReadProjectile(0, out _, out var ricochets, out var damage, out var distance), Is.True);
            Assert.That(ricochets, Is.Zero);
            Assert.That(damage, Is.EqualTo(48f).Within(0.001f));
            Assert.That(distance, Is.EqualTo(4f).Within(0.0001f));
            _pool.Simulate(0.005f);
            Assert.That(_pool.ActiveCount, Is.EqualTo(1));
            _pool.Restore(1);
            Assert.That(bolt.forward.z, Is.GreaterThan(0.99f));
            Assert.That(_pool.TryReadProjectile(0, out _, out ricochets, out _, out distance), Is.True);
            Assert.That(ricochets, Is.EqualTo(1));
            Assert.That(distance, Is.Zero);
        }

        [Test]
        public void CloseAndTerminalBallisticsUseProjectileDistanceDuringRecall()
        {
            Equip(1);
            var near = Target(1, 8f);
            var far = Target(2, 13f);
            Tick(25);
            Launch(new ProjectileBallistics(WeaponId.Deadline, 2, 0, 1f, 0f, 0.015f, 0.45f, 0.30f, 5f), 30f);
            Tick(12, TemporalMode.Recall);
            Assert.That(near.Hits, Is.EqualTo(1));
            Assert.That(far.Hits, Is.EqualTo(1));
            Assert.That(near.EffectiveDamage, Is.GreaterThan(39f));
            Assert.That(far.EffectiveDamage, Is.EqualTo(30f * (1f + 0.015f * 7.85f)).Within(0.12f));
        }

        [Test]
        public void ExtendedMagazinesAndFeedUsePerSlotCapacityAndAmmo()
        {
            Equip(0);
            Grant(UpgradeId.ExtendedMagazine);
            Assert.That(_gun.Capacity(0), Is.EqualTo(40));
            Assert.That(_gun.Capacity(1), Is.EqualTo(8));
            Assert.That(_gun.State(0).Magazine, Is.EqualTo(30));
            Assert.That(_gun.State(1).Magazine, Is.EqualTo(6));

            Switch(WeaponSlotInput.Secondary);
            Assert.That(_gun.TryAutoFeed(3), Is.True);
            Assert.That(_gun.State(1).Magazine, Is.EqualTo(8));
            Assert.That(_gun.State(1).Reserve, Is.EqualTo(34));
            Assert.That(_gun.State(0).Magazine, Is.EqualTo(30));
            Assert.That(_gun.State(0).Reserve, Is.EqualTo(180));
        }

        [Test]
        public void DeadlinePreservesLaunchModifiersAcrossAWeaponRestore()
        {
            Equip(1);
            Grant(UpgradeId.AcceleratedProcessing, UpgradeId.Hypervelocity, UpgradeId.PenetrationProtocol);
            var holstered = _gun.Capture();
            Switch(WeaponSlotInput.Secondary);
            Tick(62, TemporalMode.BulletTime, hold: true);
            Assert.That(_gun.Magazine, Is.EqualTo(4));
            Tick(1, TemporalMode.BulletTime, hold: true);
            Assert.That(_gun.Magazine, Is.EqualTo(3));
            Assert.That(_gun.Capture().Cooldown, Is.EqualTo(0.55d / 1.35d - (63d * Step - 0.7d / 1.35d)).Within(0.00001d));
            Assert.That(_pool.TryReadProjectile(0, out var left, out _, out var damage, out _), Is.True);
            Assert.That(left, Is.EqualTo(3));
            Assert.That(damage, Is.EqualTo(90f));

            _gun.Restore(holstered);
            _director.Advance(0d, default);
            var projectile = _pool.transform.GetChild(0);
            var start = projectile.position;
            Tick(1);
            Assert.That(_gun.ActiveType, Is.EqualTo(WeaponType.Carbine));
            Assert.That(Vector3.Distance(start, projectile.position), Is.EqualTo(150d * Step).Within(0.001d));
        }

        [Test]
        public void StaticWallStopsPiercingRoundBeforeTarget()
        {
            Equip(1);
            var target = Target(1, 11f);
            var wall = new GameObject("Piercing obstruction");
            wall.transform.position = TestRay.GetPoint(2f);
            wall.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, 0.2f);
            Launch(new ProjectileBallistics(WeaponId.Deadline, 4, 0, 0.85f, 0f, 0f, 0f, 0f, 0f), 90f);
            _pool.Simulate(0.10f);
            Assert.That(_pool.ActiveCount, Is.Zero);
            Assert.That(target.Hits, Is.Zero);
        }

        [Test]
        public void WarmProjectileMovementWritesAndRestoresHistoryWithoutManagedAllocations()
        {
            Equip(1);
            var flight = new ProjectileBallistics(WeaponId.Deadline, 3, 1, 0.85f, 0.8f, 0f, 0f, 0f, 0f);
            Assert.That(_pool.TryLaunchVolley(TestRay, Vector3.up, Vector3.zero, 0.2f, 5f, 10f, 0.05f, 20f, Physics.DefaultRaycastLayers, false, false, flight, 8, 7f, 0f, 0f), Is.True);
            for (var tick = 0; tick < 16; tick++)
            {
                _pool.Simulate((float)Step);
                _pool.Capture(1);
                _pool.Restore(1);
            }

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 120; tick++)
            {
                _pool.Simulate((float)Step);
                _pool.Capture(1);
                _pool.Restore(1);
            }

            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(_pool.ActiveCount, Is.EqualTo(8));
        }

        private Ray TestRay => new(new Vector3(0f, 3f, 5f), Vector3.forward);

        private void Equip(int choice)
        {
            Assert.That(_route.ConfirmSecondary(choice), Is.True);
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            _build = new RunUpgradeState();
            _effects = new PlayerUpgradeEffects(_build, _player.Motor, _gun);
            _player.Combat.BindUpgrades(_build, _effects);
            _director.SealHistory();
        }

        private void Grant(params UpgradeId[] ids)
        {
            foreach (var id in ids)
            {
                Assert.That(_build.Acquire(_route.Progression.DebugDefinitions[(int)id]), Is.True);
            }
        }

        private void Fire(bool trigger = true)
        {
            _gun.Simulate(Step, trigger, false, true, TestRay, Vector3.up, _pool);
        }

        private void Switch(WeaponSlotInput selection)
        {
            Tick(1, selection: selection);
            Tick(SwitchTicks - 1);
        }

        private void Tick(int count, TemporalMode mode = TemporalMode.Normal, bool hold = false, bool reload = false, WeaponSlotInput selection = WeaponSlotInput.None)
        {
            var keys = new TemporalSelection();
            keys.Press(mode);
            var input = new TemporalInput(default, default, false, false, false, keys, fireHeld: hold, reloadPressed: reload, weaponSelection: selection);
            for (var index = 0; index < count; index++)
            {
                _director.Advance(Step, input);
            }
        }

        private WeaponTestTarget Target(int id, float distance, bool doubleCollider = false)
        {
            var host = new GameObject($"Weapon Target {id}");
            host.transform.position = new Vector3(0f, 3f, distance);
            var target = host.AddComponent<WeaponTestTarget>();
            target.Initialize(id);
            host.AddComponent<BoxCollider>().size = new Vector3(0.7f, 0.7f, 0.2f);
            if (doubleCollider)
            {
                var rear = new GameObject("Second hitbox");
                rear.transform.SetParent(host.transform, false);
                rear.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                rear.AddComponent<BoxCollider>().size = new Vector3(0.7f, 0.7f, 0.2f);
            }

            return target;
        }

        private void Launch(in ProjectileBallistics flight, float damage)
        {
            Assert.That(_pool.TryLaunchVolley(TestRay, Vector3.up, Vector3.zero, 100f, 1.1f, 100f, 0.05f, damage, Physics.DefaultRaycastLayers, false, false, flight, 1, 0f, 0f, 0f), Is.True);
        }
    }

    internal sealed class WeaponTestTarget : MonoBehaviour, ICombatDamageable
    {
        public CombatHealth Health { get; private set; }
        public int RewardId { get; private set; }
        internal int Hits { get; private set; }
        internal float EffectiveDamage { get; private set; }

        internal void Initialize(int id)
        {
            RewardId = id;
            Health = gameObject.AddComponent<CombatHealth>();
            Health.Restore(Health.Maximum);
        }

        DamageResult ICombatDamageable.Hit(Collider collider, float amount, bool reducedMotion, out bool weakPoint)
        {
            weakPoint = false;
            EffectiveDamage = amount;
            Hits++;
            return new DamageResult(Mathf.RoundToInt(amount), false);
        }
    }
}
#endif
