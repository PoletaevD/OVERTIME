using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class Stage052PlayModeTests
    {
        private const string SceneName = "Security Route Stage 05A.1";
        private const double Step = TemporalTimeline.TickDuration;
        private SectorRoute _route;
        private PlaygroundPlayer _player;
        private TemporalDirector _director;

        [UnitySetUp]
        public IEnumerator OpenPlayableRoute()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player.enabled = false;
            _player.CameraRig.ReducedMotion = true;
        }

        [UnityTest]
        public IEnumerator WalksLeftDoorsAndCompletesAllFiveEncounters()
        {
            yield return TraverseSector(0);
        }

        [UnityTest]
        public IEnumerator WalksRightDoorsAndCompletesAllFiveEncounters()
        {
            yield return TraverseSector(1);
        }

        [Test]
        public void DirectiveStartsAloneAndClearDoesNotGrantReserveAmmo()
        {
            var gun = _route.Weapon;
            Assert.That(_route.Seamless, Is.True);
            Assert.That(_route.SelectingSecondary || _director.RewardSelection, Is.False);
            Assert.That(gun.ActiveDefinition.Id, Is.EqualTo(WeaponId.Directive));
            Assert.That(gun.HasSecondary, Is.False);
            Assert.That(gun.Instance(0).Affixes, Is.Empty);
            Assert.That(gun.Reserve, Is.EqualTo(gun.ActiveDefinition.Tuning.Reserve));
            Assert.That(_route.Progression.Build.Revision, Is.Zero);
            var reserve = gun.Reserve;
            ClearFight();
            Assert.That(gun.Reserve, Is.EqualTo(reserve));
            Assert.That(_director.RewardSelection, Is.True);
            Assert.That(_route.Progression.Selection.Count, Is.EqualTo(3));
        }

        [Test]
        public void ContactCrateIsUniqueThroughPlayerAndWorldRewind()
        {
            var gun = _route.Weapon;
            var before = gun.Capture();
            var reserve = gun.Reserve;
            Assert.That(_route.DebugLootAmmo(WeaponId.Directive, 12), Is.True);
            var crate = ChildNamed("QA Ammunition");
            StepFrames(24);
            var position = crate.position;
            var cover = new GameObject("Ammo Contact Occlusion");
            try
            {
                cover.transform.position = position - Vector3.forward * 0.29f;
                cover.AddComponent<BoxCollider>().size = new Vector3(1.1f, 1.6f, 0.08f);
                _player.PlaceAt(new Vector3(position.x, 0.89f, position.z - 0.75f), Quaternion.identity);
                Physics.SyncTransforms();
                _director.Advance(Step, default);
                Assert.That(crate.gameObject.activeSelf, Is.True);
                Assert.That(gun.Reserve, Is.EqualTo(reserve));
            }
            finally
            {
                Object.DestroyImmediate(cover);
            }

            _player.PlaceAt(new Vector3(position.x, 0.89f, position.z), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(Step, default);
            Assert.That(crate.gameObject.activeSelf, Is.False);
            Assert.That(gun.Reserve, Is.EqualTo(reserve + 12));
            StepFrames(24);
            foreach (var mode in new[] { TemporalMode.Recall, TemporalMode.Reset, TemporalMode.Override })
            {
                _director.Energy.Recharge();
                StepFrames(10, new TemporalInput(default, default, false, false, false, Selection(mode)));
                StepFrames(10);
                Assert.That(gun.Reserve, Is.EqualTo(reserve + 12), mode.ToString());
                Assert.That(crate.gameObject.activeSelf, Is.False, mode.ToString());
            }

            gun.Restore(before);
            gun.Restore(before);
            Assert.That(gun.Reserve, Is.EqualTo(reserve + 12));
        }

        [Test]
        public void PhysicalSwapPreservesMagazineAndInstanceIdentity()
        {
            var gun = _route.Weapon;
            var original = gun.Instance(0);
            StepFrames(1, new TemporalInput(default, default, false, false, true, default));
            var rounds = gun.Magazine;
            ClearFight();
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            Assert.That(_route.DebugLootWeapon(WeaponId.Termination, WeaponRarity.Rare, 1 << (int)WeaponModifier.SlugConversion, 44, false), Is.True);
            var shotgun = ChildNamed("ARC-02  /  Termination", true);
            LookAt(shotgun.position);
            Interact();
            Assert.That(gun.HasSecondary, Is.True);
            Assert.That(gun.Instance(1).Modifier(WeaponModifier.SlugConversion), Is.Positive);
            Assert.That(gun.Instance(0), Is.SameAs(original));

            var reserve = gun.Ammo(WeaponId.Directive);
            Assert.That(_route.DebugLootWeapon(WeaponId.Deadline, WeaponRarity.Common, 0, 38, false), Is.True);
            var rail = ChildNamed("ARC-03  /  Deadline", true);
            LookAt(rail.position);
            Interact();
            Assert.That(gun.Instance(0).Definition.Id, Is.EqualTo(WeaponId.Deadline));

            var dropped = ChildNamed("ARC-00  /  Directive", true);
            LookAt(dropped.position);
            Interact();
            Assert.That(gun.Instance(0), Is.SameAs(original));
            Assert.That(gun.Magazine, Is.EqualTo(rounds));
            Assert.That(gun.Ammo(WeaponId.Directive), Is.EqualTo(reserve));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoomWeaponRewardRetiresTheOtherChoiceAndKeepsAmmo(bool replace)
        {
            if (replace)
            {
                Assert.That(_route.DebugLootWeapon(WeaponId.Termination, WeaponRarity.Rare, 1 << (int)WeaponModifier.SlugConversion, 44, true), Is.True);
                StepFrames(1, new TemporalInput(default, default, false, false, true, default));
            }

            var gun = _route.Weapon;
            var original = gun.Instance(gun.ActiveSlot);
            var magazine = gun.Magazine;
            ClearFight();
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            var room = _route.ActiveRoom;
            var node = _route.CurrentNode;
            var rewards = new WeaponLootRoll().Roll(_route.Progression.Seed, node, new[] { gun.AuthoredPrimary, _route.SecondaryOptions[0], _route.SecondaryOptions[1], _route.SecondaryOptions[2] });
            var chosen = ChildNamed(rewards[0].Definition.DisplayName, true);
            var rejected = ChildNamed(rewards[1].Definition.DisplayName, true);
            var crates = room.transform.Cast<Transform>().Where(child => child.name.StartsWith("Ammunition /", System.StringComparison.Ordinal) && child.gameObject.activeSelf).ToArray();
            Assert.That(crates, Is.Not.Empty);

            _player.PlaceAt(new Vector3(chosen.position.x + 0.78f, 0.89f, chosen.position.z - 0.6f), room.Exit.rotation);
            Physics.SyncTransforms();
            LookAt(chosen.position);
            Interact();
            var slot = replace ? gun.ActiveSlot : 1;
            var acquired = gun.Instance(slot);
            Assert.That(acquired.InstanceId, Is.EqualTo(rewards[0].InstanceId));
            Assert.That(rejected.gameObject.activeSelf, Is.False);
            Assert.That(_route.LootHint, Is.Empty);
            Assert.That(crates.All(crate => crate.gameObject.activeSelf), Is.True);
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));

            StepFrames(20);
            StepFrames(16, new TemporalInput(default, default, false, false, false, Selection(TemporalMode.Reset)));
            _player.PlaceAt(new Vector3(rejected.position.x - 0.78f, 0.89f, rejected.position.z - 0.6f), room.Exit.rotation);
            Physics.SyncTransforms();
            LookAt(rejected.position);
            Interact();
            room.GetComponent<EncounterLoot>().DropWeapons(node.Id, rewards, true);
            Assert.That(gun.Instance(slot), Is.SameAs(acquired));
            Assert.That(rejected.gameObject.activeSelf, Is.False);

            if (replace)
            {
                var oldModel = ChildNamed(original.Definition.DisplayName, true);
                _player.PlaceAt(new Vector3(oldModel.position.x + 0.78f, 0.89f, oldModel.position.z - 0.6f), room.Exit.rotation);
                Physics.SyncTransforms();
                LookAt(oldModel.position);
                Interact();
                Assert.That(gun.Instance(slot), Is.SameAs(original));
                Assert.That(gun.Magazine, Is.EqualTo(magazine));
                Assert.That(gun.Instance(slot).Modifier(WeaponModifier.SlugConversion), Is.Positive);
            }
        }

        [Test]
        public void AdsKeepsRecoilAndTheBoltStartsOnTheReticle()
        {
            var gun = _route.Weapon;
            var held = new TemporalInput(default, default, false, false, false, default, aimHeld: true);
            StepFrames(38, held);
            Assert.That(gun.Aiming, Is.True);
            Assert.That(gun.AimBlend, Is.EqualTo(1f));
            var aim = _player.AimRay;
            StepFrames(1, new TemporalInput(default, default, false, false, true, default, aimHeld: true));
            Assert.That(_director.PlayerProjectiles.TryReadFlight(0, out var ray, out _, out _), Is.True);
            Assert.That(Vector3.Distance(ray.origin, aim.origin), Is.LessThan(0.02f));
            Assert.That(Vector3.Angle(ray.direction, aim.direction), Is.LessThan(0.01f));
            Assert.That(_player.CameraRig.PendingRecoil.y, Is.Positive);
            StepFrames(38);
            Assert.That(gun.Aiming, Is.False);
            Assert.That(gun.AimBlend, Is.Zero);
            Assert.That(Vector3.Angle(_player.AimRay.direction, aim.direction), Is.GreaterThan(0.5f));
        }

        [Test]
        public void KnifeWindupAndRestoreDoNotPublishAnotherSwing()
        {
            var knife = _player.Combat.Knife;
            var starts = 0;
            knife.Started += () => starts++;
            var before = _route.Weapon.Magazine;
            StepFrames(1, new TemporalInput(default, default, false, false, true, default, meleePressed: true));
            var windup = knife.State;
            Assert.That(windup.Active, Is.True);
            Assert.That(windup.Resolved, Is.False);
            Assert.That(_route.Weapon.Magazine, Is.EqualTo(before));
            StepFrames(16);
            Assert.That(knife.State.Resolved, Is.True);
            knife.Restore(windup);
            Assert.That(knife.State.Resolved, Is.False);
            Assert.That(starts, Is.EqualTo(1));

            StepFrames(85);
            _director.Energy.Recharge();
            StepFrames(1, new TemporalInput(default, default, false, false, false, Selection(TemporalMode.Override), meleePressed: true));
            Assert.That(knife.Active, Is.False);
            Assert.That(starts, Is.EqualTo(1));
        }

        [Test]
        public void AcceleratorAndSplitterKeepBoundedWorldState()
        {
            var pool = _director.PlayerProjectiles;
            var shot = new Ray(new Vector3(100f, 100f, 100f), Vector3.forward);
            var effects = new ProjectileBallistics(WeaponId.Compliance, 1, 0, 1f, 0f, 0f, 0f, 0f, 0f, splitDistance: 3f, acceleration: 1f);
            Assert.That(pool.TryLaunchVolley(shot, Vector3.up, Vector3.zero, 24f, 2f, 40f, 0.04f, 25f, Physics.DefaultRaycastLayers, false, false, effects, 1, 0f, 0f, 0f), Is.True);
            StepFrames(32);
            Assert.That(pool.ActiveCount, Is.EqualTo(3));
            Assert.That(pool.TryReadFlight(0, out _, out var speed, out var split), Is.True);
            Assert.That(speed, Is.GreaterThan(24f));
            Assert.That(split, Is.True);
            StepFrames(45);
            Assert.That(pool.Generation, Is.EqualTo(3));

            _director.Energy.Recharge();
            StepFrames(40, new TemporalInput(default, default, false, false, false, Selection(TemporalMode.Override)));
            Assert.That(pool.Generation, Is.LessThan(3));
        }

        [Test]
        public void QuickChamberUsesPartOfOneGrantAndAutoReloadUsesStock()
        {
            Grant(UpgradeId.QuickChamber);
            var gun = _route.Weapon;
            var old = gun.Capture();
            old.Primary.Magazine = 2;
            gun.Restore(old);
            var reserve = gun.Reserve;
            Assert.That(gun.CollectAmmo(-101, WeaponId.Directive, 12), Is.True);
            Assert.That(gun.Magazine, Is.EqualTo(5));
            Assert.That(gun.Reserve, Is.EqualTo(reserve + 9));
            Assert.That(gun.CollectAmmo(-101, WeaponId.Directive, 12), Is.False);
            gun.Restore(old);
            Assert.That(gun.Magazine + gun.Reserve, Is.EqualTo(reserve + 14));

            var empty = gun.Capture();
            empty.Primary.Magazine = 0;
            gun.Restore(empty);
            gun.DebugSetAmmo(WeaponId.Directive, 0);
            StepFrames(1);
            Assert.That(gun.IsReloading, Is.False);
            Assert.That(gun.CollectAmmo(-102, WeaponId.Directive, 16), Is.True);
            var loaded = gun.Capture();
            loaded.Primary.Magazine = 0;
            gun.Restore(loaded);
            StepFrames(1);
            Assert.That(gun.IsReloading, Is.True);
            StepFrames(Mathf.CeilToInt(gun.ReloadSeconds / (float)Step) + 2);
            Assert.That(gun.Magazine, Is.EqualTo(gun.MagazineCapacity));
        }

        [Test]
        public void AdsProtocolsCombineWithPenetrationWithoutRemovingCameraRecoil()
        {
            Grant(UpgradeId.FocusProtocol);
            Grant(UpgradeId.PrecisionLock);
            Grant(UpgradeId.SteadyProcessing);
            Grant(UpgradeId.PenetrationProtocol);
            var gun = _route.Weapon;
            var aim = new TemporalInput(default, default, false, false, false, default, aimHeld: true);
            StepFrames(124, aim);
            Assert.That(gun.AimSeconds, Is.EqualTo(1f));
            var slow = Selection(TemporalMode.BulletTime);
            StepFrames(1, new TemporalInput(default, default, false, false, true, slow, aimHeld: true));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
            Assert.That(_director.EnergyCost, Is.EqualTo(_director.Energy.Cost(TemporalMode.BulletTime) * 0.75f));
            Assert.That(gun.AimSeconds, Is.Zero);
            Assert.That(_director.PlayerProjectiles.TryReadProjectile(0, out var penetration, out _, out _, out _), Is.True);
            Assert.That(penetration, Is.EqualTo(2));
            var expected = gun.ActiveDefinition.GameplayRecoil.Impulse(0).y * gun.ActiveDefinition.Aim.Recoil * 0.70f;
            Assert.That(_player.CameraRig.PendingRecoil.y, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void KnifeKillRewardsPayOnceAndArmBladeAndBullet()
        {
            Grant(UpgradeId.SurgicalStrike);
            Grant(UpgradeId.BloodBattery);
            Grant(UpgradeId.EmergencySupply);
            Grant(UpgradeId.PerformanceBonus);
            Grant(UpgradeId.BladeAndBullet);
            Grant(UpgradeId.ExtendedEdge);
            var room = _route.Current;
            _player.PlaceAt(room.CombatZone.bounds.center + Vector3.up * (0.89f - room.CombatZone.bounds.center.y), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
            room.DebugReleaseAll();
            _player.PlaceAt(_route.ActiveRoom.Entry.position, Quaternion.identity);
            SecurityEnemy enemy = null;
            foreach (var candidate in room.Enemies)
            {
                if (candidate.Kind == SecurityEnemyKind.Guard)
                {
                    enemy = candidate;
                    break;
                }
            }

            Assert.That(enemy, Is.Not.Null);
            var mark = _player.transform.position + Vector3.forward * 1.62f;
            enemy.transform.position = mark;
            enemy.Health.Restore(41);
            Physics.SyncTransforms();
            _director.Energy.Spend(_director.Energy.Current - 20f);
            _player.Combat.Health.Restore(50);
            var gun = _route.Weapon;
            var empty = gun.Capture();
            empty.Primary.Magazine = 0;
            gun.Restore(empty);
            gun.DebugSetAmmo(WeaponId.Directive, 0);
            StepFrames(1, new TemporalInput(default, default, false, false, false, default, meleePressed: true));
            StepFrames(12);
            Assert.That(enemy.Health.Alive, Is.False);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(58));
            Assert.That(_director.Energy.Current, Is.EqualTo(45f).Within(0.01f));
            Assert.That(ChildNamed("Emergency Supply"), Is.Not.Null);
            Assert.That(_route.Progression.PlayerEffects.State.BladeAndBullet, Is.Positive);
            Assert.That(_director.PlayerProjectiles.Generation, Is.EqualTo(1));

            enemy.ResetState();
            Assert.That(_director.DamageTarget(enemy, enemy.Body, 1000f, out _, true).Killed, Is.True);
            Assert.That(_player.Combat.Health.Current, Is.EqualTo(58));
            Assert.That(_director.Energy.Current, Is.EqualTo(45f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CalibratedAdsAlignsBothSightsAndOnlyDeadlineShowsTheOpticalReticle()
        {
            var gun = _route.Weapon;
            var hud = _player.GetComponentInChildren<PlaygroundHud>();
            var hip = hud.transform.Find("Reticle");
            var header = hud.transform.Find("Sector Encounter");
            var scope = hud.GetComponentInChildren<DeadlineScope>();
            var scopeImage = scope.GetComponent<UnityEngine.UI.RawImage>();
            var presentation = _player.GetComponentInChildren<WeaponPresentation>();
            var view = presentation.GetComponentInParent<Camera>();
            var types = new[] { WeaponId.Directive, WeaponId.Compliance, WeaponId.Termination, WeaponId.Deadline };
            foreach (var id in types)
            {
                Assert.That(_route.DebugLootWeapon(id, WeaponRarity.Common, 0, 52, true), Is.True);
                _player.PlaceAt(_route.ActiveRoom.Entry.position + Vector3.up, Quaternion.AngleAxis(90f, Vector3.forward));
                gun.AdvanceAim(1f, true, false);
                yield return null;
                yield return null;

                Assert.That(hip.gameObject.activeSelf, Is.False);
                Assert.That(scopeImage.enabled, Is.EqualTo(id == WeaponId.Deadline));
                Assert.That(header.gameObject.activeSelf, Is.EqualTo(id != WeaponId.Deadline));
                Assert.That(gun.ActiveDefinition.Aim.HasSight, Is.True);
                presentation.SightPoints(gun.ActiveDefinition.Aim, out var rear, out var front);
                AssertCenteredOn(view, rear);
                AssertCenteredOn(view, front);
                var actualCamera = _player.GetComponentInChildren<Camera>();
                var expectedFov = id == WeaponId.Deadline ? 34f : gun.ActiveDefinition.Aim.FieldOfView(gun.ActiveType);
                Assert.That(actualCamera.fieldOfView, Is.EqualTo(expectedFov).Within(0.08f));
            }

            gun.AdvanceAim(1f, false, true);
            yield return null;
            yield return null;
            Assert.That(scopeImage.enabled, Is.False);
            Assert.That(hip.gameObject.activeSelf, Is.True);
            Assert.That(header.gameObject.activeSelf, Is.True);
        }

        private void AssertCenteredOn(Camera camera, Vector3 position)
        {
            var viewport = camera.WorldToViewportPoint(position);
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(Vector2.Distance(new Vector2(viewport.x, viewport.y), Vector2.one * 0.5f), Is.LessThan(0.0005f));
        }

        [UnityTest]
        public IEnumerator RenderedCameraAndAimRayShareTheirCenterUnderGravity()
        {
            var camera = _player.GetComponentInChildren<Camera>();
            foreach (var roll in new[] { 0f, 90f, 180f })
            {
                _player.PlaceAt(_route.ActiveRoom.Entry.position + Vector3.up * 2f, Quaternion.AngleAxis(roll, Vector3.forward));
                StepFrames(25, new TemporalInput(default, default, false, false, false, default, aimHeld: true));
                yield return null;
                yield return null;
                var aim = _player.AimRay;
                var center = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Assert.That(Vector3.Angle(aim.direction, center.direction), Is.LessThan(0.02f));
                Assert.That(Vector3.Cross(center.origin - aim.origin, aim.direction).magnitude, Is.LessThan(0.015f));
            }
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void ATriggerProducesExactlyThreeRoundsAtEveryRenderRate(int fps)
        {
            var mask = (1 << (int)WeaponModifier.BurstModule) | (1 << (int)WeaponModifier.GyroscopicStabilizer);
            Assert.That(_route.DebugLootWeapon(WeaponId.Directive, WeaponRarity.Rare, mask, 81, true), Is.True);
            var gun = _route.Weapon;
            var initial = gun.Magazine;
            var shots = 0;
            gun.ShotFired += () => shots++;
            _director.Advance(1d / fps, new TemporalInput(default, default, false, false, true, default, aimHeld: true));
            for (var frame = 1; frame < fps / 2; frame++)
            {
                _director.Advance(1d / fps, new TemporalInput(default, default, false, false, false, default, aimHeld: true));
            }

            Assert.That(shots, Is.EqualTo(3));
            Assert.That(gun.Magazine, Is.EqualTo(initial - 3));
            Assert.That(gun.Capture().Active.BurstLeft, Is.Zero);
            Assert.That(gun.AimBlend, Is.EqualTo(1f));
            Assert.That(_player.CameraRig.Pitch, Is.LessThan(-1f));
        }

        [Test]
        public void QualifiedRecallArmsTimeSeverAndAServiceExitDoesNot()
        {
            Grant(UpgradeId.TimeSever);
            var effects = _route.Progression.PlayerEffects;
            StepFrames(80);
            var recall = new TemporalInput(default, default, false, false, false, Selection(TemporalMode.Recall));
            StepFrames(25, recall);
            StepFrames(1);
            Assert.That(effects.State.TimeSever, Is.GreaterThan(2.9f));
            StepFrames(1, new TemporalInput(default, default, false, false, false, default, meleePressed: true));
            StepFrames(12);
            Assert.That(effects.State.TimeSever, Is.Zero);

            StepFrames(80);
            _director.Energy.Recharge();
            StepFrames(25, recall);
            _director.SealHistory();
            Assert.That(effects.State.TimeSever, Is.Zero);
        }

        [Test]
        public void KnifeRespectsWallsAndOnlyBypassesAShieldFromBehind()
        {
            SecurityEnemy prefab = null;
            foreach (var room in _route.DebugLibrary)
            {
                foreach (var marker in room.Prefab.Markers)
                {
                    if (marker.Enemy.Kind == SecurityEnemyKind.Enforcer)
                    {
                        prefab = marker.Enemy;
                        break;
                    }
                }
            }

            Assert.That(prefab, Is.Not.Null);
            var targetPosition = _player.transform.position + Vector3.forward * 1.63f;
            var cover = new GameObject("Knife Occlusion");
            var enemy = Object.Instantiate(prefab, targetPosition, Quaternion.identity);
            try
            {
                enemy.BindEncounter(_director, _route.EnemyProjectiles, 9902, System.Array.Empty<Transform>(), Vector3.one);
                enemy.Initialize(8);
                cover.transform.position = _player.AimRay.GetPoint(0.9f);
                var wall = cover.AddComponent<BoxCollider>();
                wall.size = new Vector3(2.2f, 2.3f, 0.05f);
                Physics.SyncTransforms();
                var hitPoints = enemy.Health.Current;

                SwingAndRecover();
                Assert.That(enemy.Health.Current, Is.EqualTo(hitPoints));

                wall.enabled = false;
                enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                var blocks = 0;
                _director.ProjectileHit += hit => blocks += hit.Melee && hit.Shielded ? 1 : 0;
                SwingAndRecover();
                Assert.That(enemy.Health.Current, Is.EqualTo(hitPoints));
                Assert.That(blocks, Is.GreaterThan(0));

                enemy.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                SwingAndRecover();
                Assert.That(enemy.Health.Current, Is.InRange(hitPoints - 70, hitPoints - 45));
            }
            finally
            {
                Object.DestroyImmediate(enemy.gameObject);
                Object.DestroyImmediate(cover);
            }
        }

        private void SwingAndRecover()
        {
            StepFrames(1, new TemporalInput(default, default, false, false, false, default, meleePressed: true));
            StepFrames(69);
        }

        [Test]
        public void OriginOverlapBlocksWithoutDeflectingTheBolt()
        {
            var cover = new GameObject("Initial Overlap");
            try
            {
                var aim = _player.AimRay;
                cover.transform.position = aim.origin + aim.direction * 0.025f;
                cover.AddComponent<BoxCollider>().size = Vector3.one * 0.07f;
                var pool = _director.PlayerProjectiles;
                var impacts = 0;
                _director.ProjectileHit += hit => impacts += hit.FromPlayer ? 1 : 0;
                Assert.That(pool.TryLaunchPlayer(aim, _player.AimUp, Vector3.zero, 40f, 1f, 40f, 0.045f, 10, Physics.DefaultRaycastLayers), Is.True);
                Assert.That(pool.TryReadFlight(0, out var shot, out _, out _), Is.True);
                Assert.That(Vector3.Distance(aim.origin, shot.origin), Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(aim.direction, shot.direction), Is.LessThan(0.01f));
                StepFrames(1);
                Assert.That(impacts, Is.EqualTo(1));
                Assert.That(pool.ActiveCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(cover);
            }
        }

        [Test]
        public void ActiveArenaKeepsHotLoopAllocationsBounded()
        {
            var bounds = _route.Current.CombatZone.bounds;
            _player.PlaceAt(new Vector3(bounds.center.x, 0.89f, bounds.min.z + 0.65f), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
            _player.PlaceAt(_route.ActiveRoom.Entry.position, Quaternion.identity);
            StepFrames(30);
            var start = _director.WorldTimeline.Cursor;
            var timer = new System.Diagnostics.Stopwatch();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            StepFrames(480);
            timer.Stop();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(_director.WorldTimeline.Cursor - start, Is.EqualTo(480));
            Assert.That(allocated, Is.LessThan(100000));
            Debug.Log($"OVERTIME stage 05A.2 profile: 480 fixed world ticks, {timer.Elapsed.TotalMilliseconds:0.0} ms, {allocated} managed bytes, {_director.WorldActorCount} world actors, player bolts {_director.PlayerProjectiles.PoolCapacity}, enemy bolts {_route.EnemyProjectiles.PoolCapacity}.");
        }

        private void Grant(UpgradeId id)
        {
            var definition = _route.Progression.DebugDefinitions[(int)id];
            Assert.That(_route.Progression.DebugGrant(definition), Is.True, definition.Title);
        }

        private IEnumerator TraverseSector(int preferred)
        {
            var playerPool = _director.PlayerProjectiles;
            var instance = _route.Weapon.Instance(0);
            for (var roomIndex = 0; roomIndex < _route.RoomCount - 1; roomIndex++)
            {
                var outgoing = _route.ActiveRoom;
                var oldBounds = outgoing.Encounter.CombatZone.bounds;
                ClearFight();
                Assert.That(_route.ConfirmUpgrade(0), Is.True);
                var passage = _route.Passage;
                var branch = Mathf.Min(preferred, _route.RouteOptions.Count - 1);
                var chosen = _route.RouteOptions[branch];
                var exit = outgoing.Exit;
                _player.PlaceAt(exit.position - exit.forward * 1.15f, exit.rotation);
                Physics.SyncTransforms();

                var turn = passage.TurnPoint(branch);
                var door = passage.CrossingPoint(branch);
                var end = passage.DestinationSocket(branch);
                Assert.That(Object.FindObjectsByType<RoomRuntime>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                WalkTo(new Vector3(exit.position.x, exit.position.y, door.z));
                Assert.That(passage.Selected, Is.EqualTo(-1));
                var residentBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                WalkTo(door);
                WalkTo(turn);
                var residentDuring = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                Assert.That(passage.Selected, Is.EqualTo(branch));
                Assert.That(Object.FindObjectsByType<RoomRuntime>(FindObjectsSortMode.None), Has.Length.EqualTo(2));
                Assert.That(passage.Select(1 - branch, true), Is.False);
                Assert.That(_director.WorldActors.Count, Is.EqualTo(1));
                var health = _player.Combat.Health.Current;
                var energy = _director.Energy.Current;
                var revision = _route.Progression.Build.Revision;

                WalkTo(end.position + end.rotation * (Vector3.forward * 4.4f + Vector3.up * 0.89f));
                Assert.That(_route.CurrentNode, Is.SameAs(chosen));
                Assert.That(_route.RoomIndex, Is.EqualTo(roomIndex + 1));
                Assert.That(_route.ActiveRoom.Encounter.CombatZone.bounds.Intersects(oldBounds), Is.False);
                Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Active));
                Assert.That(_player.Combat.Health.Current, Is.EqualTo(health));
                Assert.That(_director.Energy.Current, Is.EqualTo(energy));
                Assert.That(_route.Progression.Build.Revision, Is.EqualTo(revision));
                Assert.That(_route.Weapon.Instance(0), Is.SameAs(instance));
                Assert.That(_director.PlayerProjectiles, Is.SameAs(playerPool));
                Assert.That(_route.InPassage || _route.Transferring, Is.False);

                StepFrames(200, new TemporalInput(default, default, false, false, false, Selection(TemporalMode.Reset)));
                Assert.That(_player.transform.position.z, Is.GreaterThan(end.position.z));
                yield return null;
                Assert.That(outgoing == null, Is.True);
                if (roomIndex == 0)
                {
                    var residentAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    TestContext.Out.WriteLine($"Stage 05A.2 branch {branch}: Unity allocated memory before staging {residentBefore}, with two rooms {residentDuring}, after release {residentAfter} bytes.");
                }
            }

            ClearFight();
            Assert.That(_route.Finished, Is.True);
            Assert.That(_route.Passage, Is.Null);
        }

        private void WalkTo(Vector3 point)
        {
            var planar = Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up);
            for (var tick = 0; tick < 360 && planar.magnitude > 0.16f; tick++)
            {
                var before = _player.transform.position;
                var heading = _player.CameraRig.Heading.normalized;
                var right = Vector3.Cross(Vector3.up, heading);
                var direction = Vector3.ClampMagnitude(planar * 3f, 1f);
                var move = new Vector2(Vector3.Dot(right, direction), Vector3.Dot(heading, direction));
                _director.Advance(Step, new TemporalInput(move, default, false, false, false, default));
                Assert.That(Vector3.Distance(before, _player.transform.position), Is.LessThan(0.18f), "Movement must stay continuous.");
                planar = Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up);
            }

            Assert.That(planar.magnitude, Is.LessThanOrEqualTo(0.16f), $"Could not reach {point} from {_player.transform.position}.");
        }

        private void ClearFight()
        {
            _director.Advance(0d, default);
            var room = _route.Current;
            if (room.State == EncounterState.Waiting)
            {
                var bounds = room.CombatZone.bounds;
                _player.PlaceAt(new Vector3(bounds.center.x, 0.89f, bounds.min.z + 0.55f), Quaternion.identity);
                Physics.SyncTransforms();
                _director.Advance(0d, default);
            }

            Assert.That(room.State, Is.EqualTo(EncounterState.Active));
            room.DebugReleaseAll();
            _player.PlaceAt(_route.ActiveRoom.Entry.position, _route.ActiveRoom.Entry.rotation);
            Physics.SyncTransforms();
            foreach (var enemy in room.Enemies)
            {
                if (enemy.Health.Alive)
                {
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 1000f, out _).Killed, Is.True, $"{room.RoomName} / {enemy.Kind} / {_director.Mode}");
                }
            }

            _director.Advance(0d, default);
        }

        private Transform ChildNamed(string name, bool exact = false)
        {
            var room = _route.ActiveRoom.transform;
            for (var index = room.childCount - 1; index >= 0; index--)
            {
                var child = room.GetChild(index);
                if (child.gameObject.activeSelf && (exact ? child.name == name : child.name.StartsWith(name, System.StringComparison.Ordinal)))
                {
                    return child;
                }
            }

            Assert.Fail($"Could not find {name} in {room.name}.");
            return null;
        }

        private void LookAt(Vector3 point)
        {
            var rig = _player.CameraRig;
            var direction = (point - _player.AimRay.origin).normalized;
            var yaw = Vector3.SignedAngle(rig.Heading, Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
            var pitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg + rig.Pitch;
            rig.Look(new Vector2(yaw, pitch) / 0.105f);
        }

        private void Interact()
        {
            _director.Advance(Step, new TemporalInput(default, default, false, false, false, default, interactPressed: true));
        }

        private void StepFrames(int count, in TemporalInput input = default)
        {
            for (var frame = 0; frame < count; frame++)
            {
                _director.Advance(Step, input);
            }
        }

        private TemporalSelection Selection(TemporalMode mode)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            return selection;
        }
    }
}
