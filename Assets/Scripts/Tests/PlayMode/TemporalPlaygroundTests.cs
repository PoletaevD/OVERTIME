using System;
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
    public sealed class TemporalPlaygroundTests
    {
        private const double Step = TemporalTimeline.TickDuration;

        private TemporalDirector _director;
        private PlaygroundPlayer _player;
        private GravityMotor _motor;
        private GravityCamera _rig;
        private TemporalMovingTarget _movingTarget;
        private TemporalSlidingDoor _door;
        private TemporalProjectileEmitter _emitter;
        private TemporalDestructible _destructible;

        [UnitySetUp]
        public IEnumerator OpenTemporalPlayground()
        {
            yield return SceneManager.LoadSceneAsync("Temporal Playground", LoadSceneMode.Single);
            yield return null;

            _director = Object.FindAnyObjectByType<TemporalDirector>();
            _player = Object.FindAnyObjectByType<PlaygroundPlayer>();
            _motor = _player.GetComponent<GravityMotor>();
            _rig = _player.GetComponent<GravityCamera>();
            _movingTarget = Object.FindAnyObjectByType<TemporalMovingTarget>();
            _door = Object.FindAnyObjectByType<TemporalSlidingDoor>();
            _emitter = Object.FindAnyObjectByType<TemporalProjectileEmitter>();
            _destructible = Object.FindAnyObjectByType<TemporalDestructible>();
            _player.enabled = false;
            _director.Advance(0d, new TemporalInput(default, default, false, false, false, false, true));
        }

        [Test]
        public void PlayerAndMovingWorldRewindTogetherAndReplaceTheirFuture()
        {
            var target = _movingTarget.transform.Find("Moving Target");
            var run = new TemporalInput(Vector2.right, new Vector2(0.5f, -0.4f), false, false, false, false, false);
            Tick(45, run);
            var playerPose = _motor.transform.position;
            var targetPose = target.position;
            var camera = _rig.Capture();
            Tick(60, run);

            Rewind(60);
            Assert.That(_motor.transform.position, Is.EqualTo(playerPose));
            Assert.That(target.position, Is.EqualTo(targetPose));
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(camera.Yaw));
            Assert.That(_rig.Capture().Pitch, Is.EqualTo(camera.Pitch));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(45));
            Assert.That(_director.PlayerTimeline.Position, Is.EqualTo(_director.WorldTimeline.Position));
            Assert.That(_director.SimulationTick, Is.EqualTo(105));

            Release();
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(46));
            var newRoute = new TemporalInput(Vector2.left, new Vector2(-1f, 0.7f), false, false, false, false, false);
            Tick(15, newRoute);
            var branchPose = _motor.transform.position;
            var branchTarget = target.position;
            var branchYaw = _rig.Capture().Yaw;
            Tick(32, newRoute);
            Rewind(32);

            Assert.That(_motor.transform.position, Is.EqualTo(branchPose));
            Assert.That(target.position, Is.EqualTo(branchTarget));
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(branchYaw));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(60));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(60));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(93));
        }

        [Test]
        public void DoorResumesFromTheRestoredPositionAndPhaseTimer()
        {
            Tick(174);
            var midway = _door.transform.localPosition;
            Assert.That(midway.y, Is.InRange(0.1f, 2.5f));
            Tick(29);
            var later = _door.transform.localPosition;
            Tick(130);
            Rewind(159);

            Assert.That(_door.transform.localPosition, Is.EqualTo(midway));
            Release();
            Tick(29);
            Assert.That(_door.transform.localPosition, Is.EqualTo(later));
            Tick(130);
            Assert.That(_door.transform.localPosition.y, Is.EqualTo(2.6f).Within(0.001f));
        }

        [Test]
        public void HitRestoresTheSameTargetAndRewindSuppressesShots()
        {
            var intact = _destructible.transform.Find("Intact Target").gameObject;
            var damaged = _destructible.transform.Find("Broken Target").gameObject;
            var originalInstance = intact.GetInstanceID();
            Tick(10);
            var fire = new TemporalInput(default, default, false, false, true, false, false);
            _director.Advance(Step, fire);
            Assert.That(intact.activeSelf, Is.False);
            Assert.That(damaged.activeSelf, Is.True);

            var rewindAndFire = new TemporalInput(default, default, false, false, true, true, false);
            Assert.That(_director.Advance(Step / 2d, rewindAndFire), Is.False);
            Assert.That(intact.activeSelf, Is.True);
            Assert.That(damaged.activeSelf, Is.False);
            Assert.That(intact.GetInstanceID(), Is.EqualTo(originalInstance));

            Assert.That(_director.Advance(Step, fire), Is.False);
            Assert.That(intact.activeSelf, Is.True);
            _director.Advance(Step, fire);
            Assert.That(intact.activeSelf, Is.False);
        }

        [Test]
        public void PooledProjectileReappearsBeforeExpiryAndDisappearsBeforeSpawn()
        {
            var slot = _emitter.transform.Find("Projectile Slot 1");
            var originalInstance = slot.GetInstanceID();
            Tick(220);
            Assert.That(slot.gameObject.activeSelf, Is.True);
            var beforeExpiry = slot.position;
            Tick(20);
            Assert.That(slot.gameObject.activeSelf, Is.False);
            Rewind(20);
            Assert.That(slot.gameObject.activeSelf, Is.True);
            Assert.That(slot.position, Is.EqualTo(beforeExpiry));
            Assert.That(slot.GetInstanceID(), Is.EqualTo(originalInstance));

            var trace = slot.GetComponent<LineRenderer>();
            Assert.That(trace.enabled, Is.True);
            Rewind(170);
            Assert.That(slot.gameObject.activeSelf, Is.False);
            Release();
            Tick(8);
            Assert.That(slot.gameObject.activeSelf, Is.True);
            Assert.That(trace.enabled, Is.False);
            Tick(15);
            Assert.That(slot.position.z, Is.GreaterThan(_emitter.transform.position.z + 0.5f));
        }

        [Test]
        public void DebugResetClearsBothTracksAndRestoresWorldBaseline()
        {
            var start = _motor.transform.position;
            var projectile = _emitter.transform.Find("Projectile Slot 1");
            var target = _movingTarget.transform.Find("Moving Target");
            var targetStart = target.position;
            var emptyRewind = new TemporalInput(default, default, false, false, false, true, false);
            _director.Advance(0d, emptyRewind);
            Assert.That(_director.IsRewinding, Is.False);
            Tick(160, new TemporalInput(Vector2.up, default, false, false, false, false, false));
            Rewind(1);
            Assert.That(_director.IsRewinding, Is.True);

            _director.Advance(0d, new TemporalInput(default, default, false, false, false, true, true));
            Assert.That(_director.IsRewinding, Is.False);
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(1));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(1));
            Assert.That(_director.SimulationTick, Is.Zero);
            Assert.That(_motor.transform.position, Is.EqualTo(start));
            Assert.That(_door.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(target.position, Is.EqualTo(targetStart));
            Assert.That(projectile.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator CeilingJumpRewindsThroughCinemachineAndContinuesTheSameAirborneStep()
        {
            _rig.ReducedMotion = true;
            var facingWall = Quaternion.Euler(0f, 90f, 0f);
            _motor.ResetAt(new Vector3(5.8f, 0.89f, -5f), facingWall);
            _rig.ResetView(facingWall);
            Physics.SyncTransforms();
            Tick(15, new TemporalInput(Vector2.up, default, true, true, false, false, false));
            var runUp = new TemporalInput(Vector2.up, default, false, false, false, false, false);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.left));
            for (var tick = 0; tick < 155 && _motor.Up != Vector3.down; tick++)
            {
                Tick(1, runUp);
            }

            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.InnerCorner));
            Assert.That(_motor.Transitioning, Is.True);
            var cornerTick = _director.PlayerTimeline.Cursor;
            var cornerCamera = _rig.Capture();
            var cornerPose = _motor.transform.rotation;

            Tick(36, runUp);
            Assert.That(_motor.Grounded, Is.True);
            var jumps = 0;
            var landings = 0;
            _motor.Jumped += () => jumps++;
            _motor.Landed += _ => landings++;
            Tick(1, new TemporalInput(Vector2.right, new Vector2(13f, 25f), true, true, false, false, false));
            var coast = new TemporalInput(Vector2.right, default, false, false, false, false, false);
            Tick(8, coast);
            Assert.That(_motor.JumpInFlight, Is.True);
            var position = _motor.transform.position;
            var velocity = _motor.Velocity;
            var view = _rig.Capture();
            Tick(1, coast);
            var nextPosition = _motor.transform.position;
            var nextVelocity = _motor.Velocity;
            Tick(11, coast);
            Rewind(12);

            Assert.That(_motor.transform.position, Is.EqualTo(position));
            Assert.That(_motor.Velocity, Is.EqualTo(velocity));
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.Magnetic && _motor.JumpInFlight, Is.True);
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(view.Yaw));
            Assert.That(_rig.Capture().Pitch, Is.EqualTo(view.Pitch));
            yield return null;
            Assert.That(Vector3.Angle(UnityEngine.Camera.main.transform.forward, _rig.AimRay.direction), Is.LessThan(0.1f));
            Release();
            Tick(1, coast);
            Assert.That(_motor.transform.position, Is.EqualTo(nextPosition));
            Assert.That(_motor.Velocity, Is.EqualTo(nextVelocity));

            Rewind((int)(_director.PlayerTimeline.Cursor - cornerTick));
            Assert.That(_motor.Transitioning && _motor.Magnetic, Is.True);
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.InnerCorner));
            Assert.That(_motor.transform.rotation, Is.EqualTo(cornerPose));
            Assert.That(_rig.Capture().Display, Is.EqualTo(cornerCamera.Display));
            Assert.That(_rig.Capture().Transition, Is.EqualTo(cornerCamera.Transition));
            Assert.That(jumps, Is.EqualTo(1));
            Assert.That(landings, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NewInputSystemQStartsAndReleasesReset()
        {
            Tick(120);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = _player.GetComponent<PlaygroundInput>();
            input.SendMessage("OnApplicationFocus", true);
            _player.enabled = true;

            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.Update();
                yield return null;
                Assert.That(_director.IsRewinding, Is.True);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                Assert.That(_director.IsRewinding, Is.False);
            }
            finally
            {
                _player.enabled = false;
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void ForwardSimulationAndRewindAgreeAcrossRenderFrameRates()
        {
            var frameRates = new[] { 30, 59, 144, 240 };
            var moving = _movingTarget.transform.Find("Moving Target");
            var advance = new TemporalInput(Vector2.right * 0.35f, default, false, false, false, false, false);
            var rewind = new TemporalInput(default, default, false, false, false, true, false);
            var reset = new TemporalInput(default, default, false, false, false, false, true);
            var position = Vector3.zero;
            var velocity = Vector3.zero;
            var target = Vector3.zero;

            foreach (var frameRate in frameRates)
            {
                _director.Advance(0d, reset);
                var elapsed = 1f / frameRate;
                for (var frame = 0; frame < frameRate * 3; frame++)
                {
                    _director.Advance(elapsed, advance);
                }

                Assert.That(_director.SimulationTick, Is.EqualTo(360));
                for (var frame = 0; frame < frameRate; frame++)
                {
                    _director.Advance(elapsed, rewind);
                }

                Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(120));
                Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(120));
                Assert.That(_director.PlayerTimeline.Depth, Is.EqualTo(2d).Within(0.00001d));
                if (frameRate == frameRates[0])
                {
                    position = _motor.transform.position;
                    velocity = _motor.Velocity;
                    target = moving.position;
                }
                else
                {
                    Assert.That(_motor.transform.position, Is.EqualTo(position));
                    Assert.That(_motor.Velocity, Is.EqualTo(velocity));
                    Assert.That(Vector3.Distance(moving.position, target), Is.LessThan(0.00001f));
                }
            }
        }

        [Test]
        public void RepeatedWorldSimulationAndRestoreUseBoundedStorageWithoutManagedAllocations()
        {
            Tick(700);
            _director.Advance(0d, new TemporalInput(default, default, false, false, false, true, false));
            Rewind(240);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 210; tick++)
            {
                Rewind(1);
            }

            var restoreBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Release();
            Tick(500);
            before = GC.GetAllocatedBytesForCurrentThread();
            Tick(2400);
            var recordBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(restoreBytes, Is.Zero);
            Assert.That(recordBytes, Is.Zero);
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(481));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(481));
        }

        private void Tick(int count, in TemporalInput input = default)
        {
            for (var tick = 0; tick < count; tick++)
            {
                _director.Advance(Step, input);
            }
        }

        private void Rewind(int ticks)
        {
            _director.Advance(ticks * Step / 2d, new TemporalInput(default, default, false, false, false, true, false));
        }

        private void Release()
        {
            _director.Advance(0d, default);
        }
    }
}
