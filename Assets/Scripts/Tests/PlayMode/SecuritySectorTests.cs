using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class SecuritySectorTests
    {
        private const double Step = TemporalTimeline.TickDuration;

        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;

        [UnitySetUp]
        public IEnumerator LoadSector()
        {
            yield return SceneManager.LoadSceneAsync("Security Sector", LoadSceneMode.Single);
            yield return null;

            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _player.enabled = false;
        }

        [UnityTest]
        public IEnumerator CompletingTheRouteWithTerminationSealsEachRoomAndShowsSuccess()
        {
            yield return CompleteRoute(0);
        }

        [UnityTest]
        public IEnumerator CompletingTheRouteWithDeadlineSealsEachRoomAndShowsSuccess()
        {
            yield return CompleteRoute(1);
        }

        private IEnumerator CompleteRoute(int choice)
        {
            var scaler = _player.GetComponentInChildren<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));

            var rewardIds = new HashSet<int>();
            var sizes = new[] { 3, 4, 4 };
            var entry = new[] { 4f, 34f, 64f };
            var exits = new[] { 28f, 58f, 92f };
            var combat = _player.Combat;
            Assert.That(_director.RewardSelection, Is.True);
            Assert.That(_route.Current.State, Is.EqualTo(EncounterState.Waiting));
            Assert.That(_route.SelectingSecondary, Is.True);
            Assert.That(_route.Progression.Selection.Count, Is.Zero);
            Assert.That(_route.Progression.Build.Revision, Is.Zero);
            Tick(60, TemporalMode.BulletTime, true);
            Assert.That(_director.SimulationTick, Is.Zero);
            Assert.That(_route.Elapsed, Is.Zero);
            Assert.That(_director.Energy.Current, Is.EqualTo(100f));
            Assert.That(combat.Weapon.Magazine, Is.EqualTo(30));
            Assert.That(_route.ConfirmUpgrade(0), Is.False);
            Assert.That(_route.ConfirmSecondary(choice), Is.True);
            Assert.That(_route.ConfirmSecondary(1 - choice), Is.False);
            Assert.That(_route.Progression.Selection.Count, Is.EqualTo(3));
            Assert.That(combat.Weapon.State(1).Reserve, Is.EqualTo(choice == 0 ? 36 : 16));
            var secondaryReward = choice == 0 ? 8 : 4;
            Assert.That(combat.Weapon.State(1).Magazine, Is.EqualTo(choice == 0 ? 6 : 4));
            ConfirmReward(1);
            _director.Energy.Spend(35d);
            Assert.That(_director.DamagePlayer(21), Is.True);

            for (var index = 0; index < sizes.Length; index++)
            {
                var room = _route.Current;
                var gate = room.transform.Find("Encounter Interlock").GetComponent<BoxCollider>();
                Assert.That(room.State, Is.EqualTo(EncounterState.Waiting));
                Assert.That(room.LivingEnemies, Is.EqualTo(sizes[index]));
                Assert.That(gate.enabled, Is.True);
                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.Body.enabled, Is.False);
                    Assert.That(rewardIds.Add(((ICombatDamageable)enemy).RewardId), Is.True);
                }

                Tick(40);
                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.Attacks, Is.Zero);
                }

                MoveTo(entry[index]);
                Assert.That(room.State, Is.EqualTo(EncounterState.Active));
                Assert.That(gate.enabled, Is.True);
                Assert.That(_director.WorldActorCount, Is.EqualTo(sizes[index] + 2));
                Tick(12, TemporalMode.BulletTime);
                Tick(1, fire: true);
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.EqualTo(1));
                var enemyPool = _route.EnemyProjectiles;
                Assert.That(enemyPool.TryLaunch(_player.Capsule.bounds.center, Vector3.back, 4f, 10f, 0.12f, 8, false), Is.True);

                var ammo = combat.Weapon.Magazine;
                var reserve = combat.Weapon.Reserve;
                var secondary = combat.Weapon.State(1);
                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.Body.enabled, Is.True);
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 200, out _).Killed, Is.True);
                }

                Tick(1);
                if (index < sizes.Length - 1)
                {
                    Assert.That(room.State, Is.EqualTo(EncounterState.RewardSelection));
                    Assert.That(gate.enabled, Is.True);
                    Assert.That(combat.Weapon.Reserve, Is.EqualTo(reserve + 30));
                    var simulationTick = _director.SimulationTick;
                    var elapsed = _route.Elapsed;
                    Tick(30, TemporalMode.Reset, true);
                    Assert.That(_director.SimulationTick, Is.EqualTo(simulationTick));
                    Assert.That(_route.Elapsed, Is.EqualTo(elapsed));
                    Assert.That(room.State, Is.EqualTo(EncounterState.RewardSelection));
                    Assert.That(combat.Weapon.Reserve, Is.EqualTo(reserve + 30));
                    ConfirmReward(index + 2);
                }
                else
                {
                    Assert.That(_director.RewardSelection, Is.False);
                }

                Assert.That(room.State, Is.EqualTo(EncounterState.Cleared));
                Assert.That(gate.enabled, Is.False);
                Assert.That(combat.Weapon.Reserve, Is.EqualTo(reserve + 30));
                Assert.That(combat.Weapon.Magazine, Is.EqualTo(ammo));
                Assert.That(combat.Health.Current, Is.EqualTo(79));
                Assert.That(enemyPool.ActiveCount, Is.Zero);
                Assert.That(_director.PlayerProjectiles.ActiveCount, Is.Zero);
                Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
                Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));

                _director.Energy.Spend(17d);
                Tick(8, TemporalMode.Reset);
                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.Health.Alive, Is.False);
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 200, out _).Amount, Is.Zero);
                }

                Assert.That(combat.Weapon.Reserve, Is.EqualTo(reserve + 30));
                var energy = _director.Energy.Current;
                MoveTo(exits[index]);
                Assert.That(room.State, Is.EqualTo(EncounterState.Completed));
                Assert.That(combat.Weapon.State(1).Reserve, Is.EqualTo(secondary.Reserve + secondaryReward));
                Assert.That(combat.Weapon.State(1).Magazine, Is.EqualTo(secondary.Magazine));
                Assert.That(gate.enabled, Is.True);
                Assert.That(combat.Health.Current, Is.EqualTo(79));
                Assert.That(combat.Weapon.Reserve, Is.EqualTo(reserve + 30));
                Assert.That(combat.Weapon.Magazine, Is.EqualTo(ammo));
                Assert.That(_director.Energy.Current, Is.EqualTo(energy));
                Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
                Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));

                foreach (var enemy in room.Enemies)
                {
                    _director.Energy.Reward(((ICombatDamageable)enemy).RewardId);
                }

                Assert.That(_director.Energy.Current, Is.EqualTo(energy));
                Assert.That(_route.Progression.Build.Revision, Is.EqualTo(Mathf.Min(index + 2, 3)));
            }

            Assert.That(_route.Finished, Is.True);
            Assert.That(_route.EnemiesDefeated, Is.EqualTo(11));
            Assert.That(_route.PowersUsed, Is.GreaterThanOrEqualTo(3));
            yield return new WaitForSecondsRealtime(0.6f);
            var text = GameObject.Find("Sector Clear Title").GetComponent<TMP_Text>();
            Assert.That(text.text, Is.EqualTo("SECURITY SECTOR CLEARED"));
            Assert.That(text.GetComponentInParent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.01f));
            Assert.That(GameObject.Find("Sector Statistics").GetComponent<TMP_Text>().text, Does.Contain("HOSTILES DOWN  11"));
        }

        [Test]
        public void FrontShieldBlocksAndTheRearCellDoublesDamageWithRewindableColliders()
        {
            Assert.That(_route.ConfirmSecondary(0), Is.True);
            ConfirmReward(1);
            MoveTo(4f);
            ClearAndLeave(28f);
            MoveTo(34f);
            ClearAndLeave(58f);
            MoveTo(64f);
            var enforcer = _route.Current.Enemies[0];
            enforcer.transform.position += Vector3.right * 3.5f;
            Physics.SyncTransforms();
            var shield = enforcer.transform.Find("Front Shield").GetComponent<Collider>();
            var cell = enforcer.transform.Find("Exposed Rear Power Cell").GetComponent<Collider>();
            var blocked = 0;
            var weak = 0;
            _director.ProjectileHit += impact =>
            {
                blocked += impact.Shielded ? 1 : 0;
                weak += impact.WeakPoint ? 1 : 0;
            };

            var silhouette = new[]
            {
                new Vector3(0f, 0.92f, 0f),
                new Vector3(0f, 0.52f, 0f),
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0f, -0.4f, 0f),
                new Vector3(0f, -0.87f, 0f),
                new Vector3(-0.55f, 0.36f, 0f),
                new Vector3(0.55f, 0.36f, 0f)
            };
            foreach (var angle in new[] { 0f, -12f, 12f })
            {
                foreach (var local in silhouette)
                {
                    var target = enforcer.transform.TransformPoint(local);
                    var approach = Quaternion.AngleAxis(angle, Vector3.up) * shield.transform.forward;
                    FireAt(target + approach * 1.7f, target);
                    Assert.That(enforcer.Health.Current, Is.EqualTo(160), $"Front silhouette {local} at {angle} degrees");
                }
            }

            var frontCount = silhouette.Length * 3;
            Assert.That(blocked, Is.EqualTo(frontCount));

            var body = enforcer.Body.bounds.center;
            FireAt(body + enforcer.transform.right * 1.55f, body);
            Assert.That(enforcer.Health.Current, Is.EqualTo(148));
            var rearBody = enforcer.transform.TransformPoint(new Vector3(0.33f, -0.26f, 0f));
            FireAt(rearBody - enforcer.transform.forward * 1.55f, rearBody);
            Assert.That(enforcer.Health.Current, Is.EqualTo(136));
            FireAt(cell.bounds.center - enforcer.transform.forward * 1.55f, cell.bounds.center);
            Assert.That(enforcer.Health.Current, Is.EqualTo(112));
            Assert.That(weak, Is.EqualTo(1));

            var state = enforcer.State;
            var direction = shield.transform.forward;
            Assert.That(_director.DamageTarget(enforcer, enforcer.Body, 200, out _).Killed, Is.True);
            Assert.That(shield.enabled, Is.False);
            Assert.That(cell.enabled, Is.False);
            Tick(1);
            Tick(1, TemporalMode.Override);
            Assert.That(enforcer.Health.Alive, Is.True);
            Assert.That(shield.enabled, Is.True);
            Assert.That(cell.enabled, Is.True);
            Assert.That(enforcer.State, Is.EqualTo(state));
            Assert.That(Vector3.Angle(direction, shield.transform.forward), Is.LessThan(1.3f));
            Assert.That(blocked, Is.EqualTo(frontCount));
            Assert.That(weak, Is.EqualTo(1));
        }

        [Test]
        public void ShieldStopsRailEvenWithRicochetAndRearTakesShotgunDamage()
        {
            Assert.That(_route.ConfirmSecondary(0), Is.True);
            ConfirmReward(1);
            MoveTo(4f);
            ClearAndLeave(28f);
            MoveTo(34f);
            ClearAndLeave(58f);
            MoveTo(64f);
            var enforcer = _route.Current.Enemies[0];
            enforcer.transform.position += Vector3.right * 3.5f;
            Physics.SyncTransforms();
            var point = enforcer.Body.bounds.center;
            var forward = enforcer.transform.forward;
            var from = point + forward * 1.7f;
            var rail = new ProjectileBallistics(WeaponId.Deadline, 4, 1, 0.85f, 0.8f, 0f, 0f, 0f, 0f);
            var shielded = false;
            var reflected = false;
            _director.ProjectileHit += impact =>
            {
                shielded |= impact.Shielded;
                reflected |= impact.Ricochet;
            };
            var pool = _director.PlayerProjectiles;
            Assert.That(pool.TryLaunchVolley(new Ray(from, -forward), Vector3.up, Vector3.zero, 100f, 1f, 100f, 0.045f, 90f, Physics.DefaultRaycastLayers, false, false, rail, 1, 0f, 0f, 0f), Is.True);
            pool.Simulate(0.025f);
            Assert.That(shielded, Is.True);
            Assert.That(reflected, Is.False);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(enforcer.Health.Current, Is.EqualTo(160));

            var rear = enforcer.transform.Find("Exposed Rear Power Cell").GetComponent<Collider>().bounds.center;
            var scatter = _player.Combat.Weapon.Tuning(1);
            Assert.That(pool.TryLaunchVolley(new Ray(rear - forward * 1.6f, forward), Vector3.up, Vector3.zero, scatter.Speed, scatter.Lifetime, scatter.Range, scatter.Radius, scatter.Damage, Physics.DefaultRaycastLayers, false, false, default, scatter.Pellets, scatter.Spread, 0f, 0f), Is.True);
            pool.Simulate(0.075f);
            Assert.That(enforcer.Health.Current, Is.LessThanOrEqualTo(160 - 8 * 9));
        }

        [Test]
        public void DeadlineProjectileCanCatchASurveillanceDrone()
        {
            Assert.That(_route.ConfirmSecondary(1), Is.True);
            ConfirmReward(1);
            MoveTo(4f);
            ClearAndLeave(28f);
            MoveTo(34f);
            var drone = _route.Current.Enemies[0];
            Assert.That(drone.Kind, Is.EqualTo(SecurityEnemyKind.Drone));
            var target = drone.Body.bounds.center;
            var origin = target + Vector3.up * 2f + Vector3.back;
            var rail = _player.Combat.Weapon.Tuning(1);
            var flight = new ProjectileBallistics(WeaponId.Deadline, rail.Targets, 0, rail.PenetrationRetention, 0f, 0f, 0f, 0f, 0f);
            var pool = _director.PlayerProjectiles;
            Assert.That(pool.TryLaunchVolley(new Ray(origin, (target - origin).normalized), Vector3.up, Vector3.zero, rail.Speed, rail.Lifetime, rail.Range, rail.Radius, rail.Damage, Physics.DefaultRaycastLayers, false, false, flight, 1, 0f, 0f, 0f), Is.True);
            Assert.That(drone.Health.Alive, Is.True);
            Tick(4, TemporalMode.BulletTime);
            Assert.That(drone.Health.Alive, Is.False);
        }

        private void ClearAndLeave(float exit)
        {
            foreach (var enemy in _route.Current.Enemies)
            {
                _director.DamageTarget(enemy, enemy.Body, 200, out _);
            }

            Tick(1);
            ConfirmReward(_route.RoomIndex + 2);
            MoveTo(exit);
        }

        private void ConfirmReward(int expectedRevision)
        {
            Assert.That(_route.ConfirmUpgrade(0), Is.True);
            Assert.That(_route.ConfirmUpgrade(0), Is.False);
            Assert.That(_director.RewardSelection, Is.False);
            Assert.That(_route.Progression.Build.Revision, Is.EqualTo(expectedRevision));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
        }

        private void FireAt(Vector3 origin, Vector3 target)
        {
            var ray = new Ray(origin, (target - origin).normalized);
            Assert.That(_director.PlayerProjectiles.TryLaunchPlayer(ray, Vector3.up, Vector3.zero, 45f, 1.8f, 80f, 0.045f, 12, Physics.DefaultRaycastLayers), Is.True);
            Tick(5);
        }

        private void MoveTo(float z)
        {
            _player.GetComponent<GravityMotor>().ResetAt(new Vector3(0f, 0.89f, z), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
        }

        private void Tick(int count, TemporalMode mode = TemporalMode.Normal, bool fire = false)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            var input = new TemporalInput(default, default, false, false, false, selection, fireHeld: fire);
            for (var index = 0; index < count; index++)
            {
                _director.Advance(Step, input);
            }
        }
    }
}
