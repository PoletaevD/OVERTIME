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
        public IEnumerator CompletingTheRouteSealsEachRoomAndShowsSuccess()
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
                foreach (var enemy in room.Enemies)
                {
                    Assert.That(enemy.Body.enabled, Is.True);
                    Assert.That(_director.DamageTarget(enemy, enemy.Body, 200, out _).Killed, Is.True);
                }

                Tick(1);
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

            var facing = shield.transform.forward;
            FireAt(shield.bounds.center + enforcer.transform.forward * 1.55f, shield.bounds.center);
            Assert.That(enforcer.Health.Current, Is.EqualTo(160));
            Assert.That(blocked, Is.EqualTo(1));
            Assert.That(Vector3.Angle(facing, shield.transform.forward), Is.LessThanOrEqualTo(3.01f));

            var body = enforcer.Body.bounds.center;
            FireAt(body + enforcer.transform.right * 1.55f, body);
            Assert.That(enforcer.Health.Current, Is.EqualTo(148));
            FireAt(cell.bounds.center - enforcer.transform.forward * 1.55f, cell.bounds.center);
            Assert.That(enforcer.Health.Current, Is.EqualTo(124));
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
            Assert.That(blocked, Is.EqualTo(1));
            Assert.That(weak, Is.EqualTo(1));
        }

        private void ClearAndLeave(float exit)
        {
            foreach (var enemy in _route.Current.Enemies)
            {
                _director.DamageTarget(enemy, enemy.Body, 200, out _);
            }

            Tick(1);
            MoveTo(exit);
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
