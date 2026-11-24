#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class RunProgressionTests
    {
        private const double Step = TemporalTimeline.TickDuration;

        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private RunProgression _progression;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Security Sector", LoadSceneMode.Single);
            yield return null;
            LocateScene();
        }

        [UnityTest]
        public IEnumerator KeyboardConfirmsOnceAndRestartClearsBuildAndPaidKills()
        {
            var priorRun = _progression.Build;
            using (var keyboard = new SyntheticKeyboard())
            {
                InputSystem.QueueStateEvent(keyboard.Device, new KeyboardState(Key.Digit2, Key.C));
                yield return null;
                yield return null;
            }

            Assert.That(_director.RewardSelection, Is.False);
            Assert.That(priorRun.Revision, Is.EqualTo(1));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            _director.Energy.Spend(50d);
            var rewardId = ((ICombatDamageable)_route.Current.Enemies[0]).RewardId;
            _director.Energy.Reward(rewardId);
            _director.RequestRestart();
            Tick(1);
            yield return null;
            yield return null;
            LocateScene();

            Assert.That(_progression.Build, Is.Not.SameAs(priorRun));
            Assert.That(_progression.Build.Revision, Is.Zero);
            Assert.That(_progression.PlayerEffects.State.Kinetic, Is.Zero);
            Assert.That(_director.Energy.Current, Is.EqualTo(100f));
            Assert.That(_director.RewardSelection, Is.True);
            Assert.That(_progression.Selection.Count, Is.EqualTo(3));
            ConfirmStart();
            _director.Energy.Spend(60d);
            var before = _director.Energy.Current;
            _director.Energy.Reward(rewardId);
            Assert.That(_director.Energy.Current, Is.EqualTo(before + 20f + _progression.Build.Bonus(UpgradeId.PerformanceBonus)));
        }

        [Test]
        public void BatteryCreditsOnceAndPerformanceBonusSharesTheKillLedger()
        {
            ConfirmStart();
            var energy = _director.Energy;
            energy.Spend(55d);
            var before = energy.Current;
            var maximum = energy.Maximum;
            Assert.That(_progression.DebugGrant(Definition(UpgradeId.ExtendedBattery)), Is.True);
            Assert.That(energy.Maximum, Is.EqualTo(maximum + 25f));
            Assert.That(energy.Current, Is.EqualTo(before + 25f));

            _director.SealHistory();
            Assert.That(energy.Current, Is.EqualTo(before + 25f));
            Grant(UpgradeId.PerformanceBonus);
            Tick(30);
            MoveTo(new Vector3(0f, 0.89f, 4f));
            Tick(21);
            var enemy = _route.Current.Enemies[0];
            before = energy.Current;
            Assert.That(_director.DamageTarget(enemy, enemy.Body, 500, out _).Killed, Is.True);
            Assert.That(energy.Current, Is.EqualTo(before + 25f));
            Tick(2);
            Tick(3, TemporalMode.Override);
            CompleteAbility();
            Assert.That(enemy.Health.Alive, Is.True);
            before = energy.Current;
            Assert.That(_director.DamageTarget(enemy, enemy.Body, 500, out _).Killed, Is.True);
            Assert.That(energy.Current, Is.EqualTo(before));
        }

        [Test]
        public void TemporalAssassinUsesRecallCostAndLeavesOneRewindableBlast()
        {
            ConfirmStart();
            Grant(UpgradeId.BadTake, UpgradeId.EfficientRecall, UpgradeId.SafeReturn);
            var echoOrigin = new Vector3(0f, 0.89f, 4f);
            MoveTo(echoOrigin);
            var exposed = _route.Current.Enemies[0];
            var covered = _route.Current.Enemies[1];
            exposed.transform.position = new Vector3(2f, 0.92f, 4f);
            covered.transform.position = new Vector3(-2f, 0.92f, 4f);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(-1.1f, 0.9f, 4f);
            blocker.transform.localScale = new Vector3(0.2f, 1.8f, 2f);
            Physics.SyncTransforms();
            _director.SealHistory();
            Tick(65);

            var enemyHealth = exposed.Health.Current;
            var coveredHealth = covered.Health.Current;
            Tick(1, TemporalMode.Recall);
            CompleteAbility();
            Assert.That(exposed.Health.Current, Is.EqualTo(enemyHealth));
            Assert.That(_progression.Echo.Remaining, Is.Zero);
            Assert.That(_progression.PlayerEffects.State.SafeReturn, Is.Zero);

            var energy = _director.Energy.Current;
            Assert.That(_director.Energy.Cost(TemporalMode.Recall), Is.EqualTo(19.5f));
            Tick(18, TemporalMode.Recall);
            var restoredHealth = _player.Combat.Health.Current;
            CompleteAbility();
            Assert.That(_director.Energy.Current, Is.EqualTo(energy - 19.5f * 0.3f).Within(0.001f));
            Assert.That(_progression.Echo.Position, Is.EqualTo(echoOrigin).Using(Vector3Comparer(0.08f)));
            Assert.That(exposed.Health.Current, Is.EqualTo(enemyHealth - 35));
            Assert.That(covered.Health.Current, Is.EqualTo(coveredHealth));
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(restoredHealth));
            Assert.That(_progression.PlayerEffects.State.SafeReturn, Is.EqualTo(1.5f));
            Assert.That(_director.DamagePlayer(20), Is.True);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(restoredHealth - 12));

            Tick(12);
            var earlierProtection = _progression.PlayerEffects.State.SafeReturn;
            Tick(12);
            var laterProtection = _progression.PlayerEffects.State.SafeReturn;
            Tick(3, TemporalMode.Recall);
            CompleteAbility();
            Assert.That(_progression.PlayerEffects.State.SafeReturn, Is.InRange(laterProtection + 0.03f, earlierProtection));
            Assert.That(exposed.Health.Current, Is.EqualTo(enemyHealth - 35));
            Assert.That(_progression.Build.Level(UpgradeId.BadTake), Is.EqualTo(1));

            Tick(25, TemporalMode.Override);
            CompleteAbility();
            Assert.That(exposed.Health.Current, Is.EqualTo(enemyHealth));
            Assert.That(_progression.Echo.Remaining, Is.Zero);
        }

        [Test]
        public void ParadoxEngineerRestoresDebuffDurationAndCombinesDamageBonuses()
        {
            ConfirmStart();
            Grant(UpgradeId.ResidualMemory, UpgradeId.ExploitWindow, UpgradeId.OverclockedReceiver);
            MoveTo(new Vector3(0f, 0.89f, 4f));
            var enemy = _route.Current.Enemies[0];
            Tick(75);
            Tick(18, TemporalMode.Override);
            CompleteAbility();
            Assert.That(enemy.InstabilityRemaining, Is.EqualTo(3f));
            Assert.That(_progression.PlayerEffects.State.ExploitWindow, Is.EqualTo(1.5f));
            Assert.That(_progression.PlayerEffects.ModifyShotDamage(12f * 1.2f), Is.EqualTo(18.72f).Within(0.001f));
            Assert.That(_director.DamageTarget(enemy, enemy.Body, 20, out _).Amount, Is.EqualTo(27));

            Tick(18);
            var remaining = enemy.InstabilityRemaining;
            Tick(4, TemporalMode.Override);
            CompleteAbility();
            Assert.That(enemy.InstabilityRemaining, Is.EqualTo(remaining + Step * 8).Within(0.001f));
            Assert.That(_progression.PlayerEffects.State.ExploitWindow, Is.LessThan(1.5f));

            Tick(40, TemporalMode.Reset);
            CompleteAbility();
            Assert.That(enemy.InstabilityRemaining, Is.Zero);
            Assert.That(_progression.Build.Level(UpgradeId.OverclockedReceiver), Is.EqualTo(1));
            Assert.That(_progression.Build.Level(UpgradeId.ResidualMemory), Is.EqualTo(1));
        }

        [Test]
        public void BulletStormKeepsTheBoltVelocityAndTheExistingCooldown()
        {
            ConfirmStart();
            Grant(UpgradeId.AcceleratedProcessing, UpgradeId.Hypervelocity, UpgradeId.ExtendedMagazine);
            var gun = _player.Combat.Weapon;
            Assert.That(gun.MagazineCapacity, Is.EqualTo(40));
            Assert.That(gun.Magazine, Is.EqualTo(30));
            Assert.That(gun.RoundsPerMinute(TemporalMode.Normal), Is.EqualTo(600f));
            Assert.That(gun.RoundsPerMinute(TemporalMode.BulletTime), Is.EqualTo(810f));

            Tick(1, TemporalMode.BulletTime, true);
            Assert.That(gun.Magazine, Is.EqualTo(29));
            var cooldown = gun.Capture().Cooldown;
            Assert.That(cooldown, Is.EqualTo(60d / 810d).Within(0.00001d));
            var projectile = _director.PlayerProjectiles.transform.GetChild(0);
            var start = projectile.position;
            CompleteAbility();
            Assert.That(gun.Capture().Cooldown, Is.EqualTo(cooldown));
            Tick(1, fire: true);
            Assert.That(gun.Magazine, Is.EqualTo(29));
            Assert.That(Vector3.Distance(start, projectile.position), Is.EqualTo(67.5d * Step).Within(0.001d));
            Assert.That(gun.ProjectileSpeed(TemporalMode.Normal), Is.EqualTo(45f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void ResetQualificationUsesRestoredHistoryAcrossRenderRates(int fps)
        {
            ConfirmStart();
            Grant(UpgradeId.SecondDraft, UpgradeId.QualityAssurance);
            FrameAdvance(fps, fps, TemporalMode.Normal);
            FrameAdvance(fps, Mathf.RoundToInt(0.04f * fps), TemporalMode.Reset);
            CompleteAbility();
            Assert.That(_progression.PlayerEffects.State.SecondDraft, Is.Zero);

            FrameAdvance(fps, Mathf.RoundToInt(0.17f * fps), TemporalMode.Reset);
            CompleteAbility();
            Assert.That(_progression.PlayerEffects.State.SecondDraft, Is.EqualTo(2f));
            Assert.That(_progression.PlayerEffects.State.QualityAssurance, Is.EqualTo(1.5f));
            Assert.That(_director.DamagePlayer(20), Is.True);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(87));
            var gun = _player.Combat.Weapon;
            gun.Simulate(Step, true, false, false, _player.AimRay, _player.AimUp, _director.PlayerProjectiles);
            Assert.That(_progression.PlayerEffects.State.SecondDraft, Is.EqualTo(2f));
            Tick(1, fire: true);
            Assert.That(_progression.PlayerEffects.State.SecondDraft, Is.Zero);
            Assert.That(_progression.Build.Level(UpgradeId.QualityAssurance), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void QualifiedOverrideCompletesAtAutomaticBoundaries(bool emptyEnergy)
        {
            ConfirmStart();
            Grant(UpgradeId.ExploitWindow);
            Tick(38);
            if (emptyEnergy)
            {
                var energy = _director.Energy;
                energy.Spend(energy.Current - 6d);
            }

            Tick(20, TemporalMode.Override);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            Assert.That(_director.Energy.Empty, Is.EqualTo(emptyEnergy));
            Assert.That(_progression.PlayerEffects.State.ExploitWindow, Is.InRange(1.45f, 1.5f));
        }

        [Test]
        public void SwitchingRewindsAndSealingHistoryDoesNotDetonateRecall()
        {
            ConfirmStart();
            Grant(UpgradeId.BadTake, UpgradeId.SafeReturn);
            Tick(70);
            Tick(19, TemporalMode.Recall);
            Tick(1, TemporalMode.Override);
            Assert.That(_progression.Echo.Remaining, Is.Zero);
            Assert.That(_progression.PlayerEffects.State.SafeReturn, Is.Zero);
            CompleteAbility();

            Tick(60);
            Tick(18, TemporalMode.Recall);
            _director.SealHistory();
            Assert.That(_progression.Echo.Remaining, Is.Zero);
            Assert.That(_progression.PlayerEffects.State.SafeReturn, Is.Zero);
        }

        [Test]
        public void WarmSchedulerAndEffectiveStatsStayAllocationFree()
        {
            ConfirmStart();
            Grant(UpgradeId.OverclockedReceiver, UpgradeId.AcceleratedProcessing, UpgradeId.Hypervelocity);
            Tick(140);
            var gun = _player.Combat.Weapon;
            var energy = _director.Energy;
            var checksum = 0f;
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 240; index++)
            {
                checksum += gun.EffectiveDamage + gun.RoundsPerMinute(TemporalMode.BulletTime) + gun.ProjectileSpeed(TemporalMode.BulletTime) + energy.Cost(TemporalMode.Recall);
                _director.Advance(Step, default);
            }

            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(checksum, Is.GreaterThan(0f));
            Assert.That(allocated, Is.Zero);
            Assert.That(_director.PlayerTimeline.Count, Is.LessThanOrEqualTo(_director.PlayerTimeline.Capacity));
            Assert.That(_director.WorldTimeline.Count, Is.LessThanOrEqualTo(_director.WorldTimeline.Capacity));
        }

        private void LocateScene()
        {
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _progression = _route.Progression;
            _player.enabled = false;
        }

        private void ConfirmStart()
        {
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
        }

        private RunUpgradeDefinition Definition(UpgradeId id)
        {
            return _progression.DebugDefinitions[(int)id];
        }

        private void Grant(params UpgradeId[] ids)
        {
            foreach (var id in ids)
            {
                if (_progression.Build.Level(id) == 0)
                {
                    Assert.That(_progression.DebugGrant(Definition(id)), Is.True);
                }
            }
        }

        private void MoveTo(Vector3 position)
        {
            _player.Motor.ResetAt(position, Quaternion.identity);
            Physics.SyncTransforms();
            CompleteAbility();
        }

        private void Tick(int count, TemporalMode mode = TemporalMode.Normal, bool fire = false)
        {
            Advance(count, Step, mode, fire);
        }

        private void FrameAdvance(int fps, int frames, TemporalMode mode)
        {
            Advance(frames, 1d / fps, mode, false);
        }

        private void Advance(int count, double seconds, TemporalMode mode, bool fire)
        {
            var keys = new TemporalSelection();
            keys.Press(mode);
            var input = new TemporalInput(default, default, false, false, false, keys, fireHeld: fire);
            for (var index = 0; index < count; index++)
            {
                _director.Advance(seconds, input);
            }
        }

        private void CompleteAbility()
        {
            _director.Advance(0d, default);
        }

        private System.Collections.Generic.IEqualityComparer<Vector3> Vector3Comparer(float distance)
        {
            return new UnityEngine.TestTools.Utils.Vector3EqualityComparer(distance);
        }
    }
}
#endif
