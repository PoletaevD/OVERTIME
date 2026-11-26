using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class Stage05CPresentationTests
    {
        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;

        [UnitySetUp]
        public IEnumerator OpenRoute()
        {
            yield return SceneManager.LoadSceneAsync("Security Route Stage 05B");
            yield return null;
            _route = Object.FindAnyObjectByType<SectorRoute>();
            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _player.enabled = false;
        }

        [UnityTest]
        public IEnumerator ReceiversUseTheEquippedInstanceAndTheMagazineMatchesTheWeapon()
        {
            foreach (var id in new[] { WeaponId.Directive, WeaponId.Compliance, WeaponId.Termination, WeaponId.Deadline })
            {
                Assert.That(_route.DebugLootWeapon(id, WeaponRarity.Rare, 1 << (int)WeaponModifier.RicochetChamber, 112, true), Is.True);
                yield return null;
                var visuals = _player.GetComponentInChildren<WeaponPresentation>();
                var assembly = visuals.GetComponentsInChildren<Transform>().Single(part => part.name == "Field Modifications");
                Assert.That(assembly.childCount, Is.EqualTo(3));
                var readout = visuals.GetComponentsInChildren<TextMeshPro>().Single(label => label.name == "Ammunition");
                readout.ForceMeshUpdate();
                Assert.That(readout.GetParsedText(), Does.StartWith(_route.Weapon.Magazine.ToString("00")));
                Assert.That(readout.gameObject.layer, Is.EqualTo(30));
                Assert.That(_route.DebugLootWeapon(id, WeaponRarity.Uncommon, 1 << (int)WeaponModifier.CryoPayload, 118, true), Is.True);
                yield return null;
                assembly = visuals.GetComponentsInChildren<Transform>().Single(part => part.name == "Field Modifications");
                Assert.That(assembly.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.transform.IsChildOf(assembly.Find("CryoPayload(Clone)"))), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ConfirmedHitsSumPerTargetAndRestoreDoesNotEmitDamage()
        {
            var room = _route.Current;
            _player.PlaceAt(_route.ActiveRoom.transform.TransformPoint(new Vector3(0f, 0.89f, 4.15f)), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
            var target = room.Enemies.First(enemy => enemy.EncounterReady && enemy.Health.Alive);
            var before = target.Health.Current;
            _director.DamageTarget(target, target.Body, 3f, out _);
            _director.DamageTarget(target, target.Body, 7f, out _);
            var label = _player.GetComponentsInChildren<TextMeshProUGUI>().Single(text => text.name == "Impact Readout");
            Assert.That(label.text, Is.EqualTo((before - target.Health.Current).ToString()));
            var confirmed = 0;
            _director.DamageConfirmed += (actor, point, damage, weak, color) => confirmed++;
            target.Capture(0);
            target.Restore(0);
            Assert.That(confirmed, Is.Zero);
            yield return null;
        }

        [Test]
        public void PresentationAssetsResolveAfterImport()
        {
            foreach (WeaponModifier modifier in System.Enum.GetValues(typeof(WeaponModifier)))
            {
                var model = Resources.Load<GameObject>("ARC Modules/" + modifier);
                Assert.That(model, Is.Not.Null, modifier.ToString());
                Assert.That(model.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(12));
            }

            foreach (FieldCue cue in System.Enum.GetValues(typeof(FieldCue)))
            {
                var clip = Resources.Load<AudioClip>("Meridian Audio/" + cue);
                Assert.That(clip, Is.Not.Null, cue.ToString());
                Assert.That(clip.samples, Is.GreaterThan(0), cue.ToString());
            }

            foreach (var name in new[] { "Meridian Spark Particles", "Bolt Jacket", "Player Muzzle Flash" })
            {
                var material = Resources.Load<Material>(name);
                Assert.That(material.shader.isSupported, Is.True, name);
                Assert.That(material.shader.name, Does.StartWith("MERIDIAN/"));
            }

            Assert.That(Resources.Load<Texture2D>("Module Plates"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MuzzleSparksFollowTheWeaponAfterTheFlashFades()
        {
            _player.CameraRig.ReducedMotion = false;
            Assert.That(_route.DebugLootWeapon(WeaponId.Compliance, WeaponRarity.Common, 0, 112, true), Is.True);
            var view = _player.GetComponentInChildren<WeaponPresentation>();
            var effect = view.GetComponentsInChildren<Transform>().Single(part => part.name == "Player Muzzle Flash VFX");
            var muzzle = effect.parent.Find("Muzzle Flash");
            var flare = effect.Find("Flash Core").GetComponent<MeshRenderer>();
            var sparks = effect.GetComponentInChildren<ParticleSystem>();
            var particle = new ParticleSystem.Particle[1];

            var shots = view.ConfirmedShots;
            _route.Weapon.Simulate(TemporalTimeline.TickDuration, true, false, true, _player.AimRay, _player.AimUp, _director.PlayerProjectiles);
            Assert.That(view.ConfirmedShots, Is.EqualTo(shots + 1));
            yield return new WaitForEndOfFrame();
            Assert.That(flare.enabled, Is.True);
            Assert.That(Vector3.Distance(muzzle.InverseTransformPoint(flare.transform.position), Vector3.forward * 0.055f), Is.LessThan(0.001f));

            sparks.Pause();
            var sparkAtShot = muzzle.InverseTransformPoint(FirstSparkPosition());
            var muzzleAtShot = muzzle.position;
            yield return new WaitForSecondsRealtime(0.09f);
            _player.PlaceAt(_player.Motor.transform.position + Vector3.right, Quaternion.Euler(0f, 32f, 0f));
            yield return new WaitForEndOfFrame();

            Assert.That(flare.enabled, Is.False);
            Assert.That(Vector3.Distance(muzzle.position, muzzleAtShot), Is.GreaterThan(0.5f));
            Assert.That(Vector3.Distance(muzzle.InverseTransformPoint(FirstSparkPosition()), sparkAtShot), Is.LessThan(0.001f));

            Vector3 FirstSparkPosition()
            {
                Assert.That(sparks.GetParticles(particle), Is.EqualTo(1));
                return sparks.main.simulationSpace == ParticleSystemSimulationSpace.Local
                    ? sparks.transform.TransformPoint(particle[0].position)
                    : particle[0].position;
            }
        }

        [UnityTest]
        public IEnumerator MaximumIsolationSupportsMixedVolleysDuringBulletTime()
        {
            var roomIndex = System.Array.FindIndex(_route.DebugLibrary, room => room.Id == "containment");
            Assert.That(_route.DebugTravel(roomIndex, 2), Is.True);
            for (var frame = 0; frame < 300 && _route.Transferring; frame++)
            {
                yield return null;
            }

            _player.PlaceAt(_route.ActiveRoom.transform.TransformPoint(new Vector3(0f, 0.89f, 4.15f)), Quaternion.identity);
            Physics.SyncTransforms();
            _director.Advance(0d, default);
            _route.Current.DebugReleaseAll();
            var target = _route.Current.Enemies.First(enemy => enemy.EncounterReady && enemy.Health.Alive);
            var origin = _player.AimRay.origin;
            var shot = new Ray(origin, (target.Body.bounds.center - origin).normalized);
            var pool = _director.PlayerProjectiles;
            var selection = new TemporalSelection();
            selection.Press(TemporalMode.BulletTime);
            var input = new TemporalInput(default, default, false, false, false, selection);
            var pellets = new ProjectileBallistics(WeaponId.Termination, 1, 1, 1f, 0.75f, 0f, 0f, 0f, 0f);
            var split = new ProjectileBallistics(WeaponId.Compliance, 1, 1, 1f, 0.75f, 0f, 0f, 0f, 0f, splitDistance: 2f);
            var explosive = new ProjectileBallistics(WeaponId.Directive, 1, 0, 1f, 0f, 0f, 0f, 0f, 0f, explosionRadius: 1.7f);
            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(system => system.name == "Discharge Particles").ToArray();
            var start = pool.Generation;
            Assert.That(particles.Length, Is.GreaterThanOrEqualTo(2));
            for (var tick = 0; tick < 120; tick++)
            {
                if (tick % 24 == 0)
                {
                    Assert.That(pool.TryLaunchVolley(shot, _player.AimUp, Vector3.zero, 45f, 1f, 45f, 0.04f, 5f, Physics.DefaultRaycastLayers, false, false, pellets, 8, 5f, 0f, 0f), Is.True);
                    Assert.That(pool.TryLaunchVolley(shot, _player.AimUp, Vector3.zero, 45f, 1f, 45f, 0.04f, 5f, Physics.DefaultRaycastLayers, false, false, split, 1, 0f, 0f, 0f), Is.True);
                    Assert.That(pool.TryLaunchVolley(shot, _player.AimUp, Vector3.zero, 45f, 1f, 45f, 0.04f, 5f, Physics.DefaultRaycastLayers, false, false, explosive, 1, 0f, 0f, 0f), Is.True);
                }

                _director.Advance(TemporalTimeline.TickDuration, input);
                Assert.That(pool.ActiveCount, Is.LessThanOrEqualTo(pool.PoolCapacity));
                if (tick % 12 == 0)
                {
                    yield return null;
                }
            }

            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
            Assert.That(pool.Generation - start, Is.GreaterThanOrEqualTo(50u));
            foreach (var system in particles)
            {
                Assert.That(system.particleCount, Is.LessThanOrEqualTo(system.main.maxParticles));
                Assert.That(system.GetComponent<Renderer>().sharedMaterial.shader.isSupported, Is.True);
            }
        }
    }
}
