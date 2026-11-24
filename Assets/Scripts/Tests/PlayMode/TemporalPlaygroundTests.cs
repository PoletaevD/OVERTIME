using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
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
            using var synthetic = new SyntheticKeyboard();
            var keyboard = synthetic.Device;
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

        [Test]
        public void OverrideMovesWorldBackWhilePlayerRunsTheWallAndCeiling()
        {
            Tick(480);
            var playerHistory = _director.PlayerTimeline.Cursor;
            var worldHistory = _director.WorldTimeline.Cursor;
            var facingWall = Quaternion.Euler(0f, 90f, 0f);
            _motor.ResetAt(new Vector3(5.8f, 0.89f, -5f), facingWall);
            _rig.ResetView(facingWall);
            Physics.SyncTransforms();

            Tick(15, WithMode(TemporalMode.Override, Vector2.up, jumpPressed: true, jumpHeld: true));
            Assert.That(_motor.Up, Is.EqualTo(Vector3.left));
            var wallPose = _motor.transform.position;
            var run = WithMode(TemporalMode.Override, Vector2.up);
            for (var tick = 0; tick < 155 && _motor.Up != Vector3.down; tick++)
            {
                Tick(1, run);
            }

            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(Vector3.Distance(_motor.transform.position, wallPose), Is.GreaterThan(1f));
            Tick(36, run);
            Tick(1, WithMode(TemporalMode.Override, Vector2.right, jumpPressed: true, jumpHeld: true));
            Assert.That(_motor.JumpInFlight, Is.True);

            var playerCursor = _director.PlayerTimeline.Cursor;
            var worldCursor = _director.WorldTimeline.Cursor;
            var playerCount = _director.PlayerTimeline.Count;
            Assert.That(playerCursor, Is.GreaterThan(playerHistory));
            Assert.That(worldCursor, Is.LessThan(worldHistory));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(481));
            Assert.That(_director.WorldTimeline.Direction, Is.EqualTo(TemporalDirection.Reverse));
            Assert.That(_director.PlayerTimeline.Direction, Is.EqualTo(TemporalDirection.Forward));
            var position = _motor.transform.position;
            Release();
            Assert.That(_motor.transform.position, Is.EqualTo(position));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(playerCursor));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(playerCount));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(worldCursor + 1));
            Tick(1);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(worldCursor + 1));
        }

        [Test]
        public void OverrideCannotDamageAWorldSnapshot()
        {
            Tick(12);
            var intact = _destructible.transform.Find("Intact Target").gameObject;
            Tick(1, WithMode(TemporalMode.Override, fire: true));
            Assert.That(intact.activeSelf, Is.True);
            Release();
            Tick(1);
            Assert.That(intact.activeSelf, Is.True);
            Tick(1, WithMode(TemporalMode.Normal, fire: true));
            Assert.That(intact.activeSelf, Is.False);
        }

        [TestCase((int)TemporalMode.Override)]
        [TestCase((int)TemporalMode.Recall)]
        public void DoorOverlapPhasesOnlyThatColliderUntilPlayerClearsIt(int modeValue)
        {
            var mode = (TemporalMode)modeValue;
            var probePosition = new Vector3(_door.transform.position.x, 0.89f, _door.transform.position.z);
            var movingDoor = _door.GetComponentInChildren<Collider>();
            var capsule = _player.GetComponent<CapsuleCollider>();
            var early = mode == TemporalMode.Override ? 228 : 459;
            Tick(early);
            _motor.ResetAt(probePosition, Quaternion.identity);
            _rig.ResetView(Quaternion.identity);
            Physics.SyncTransforms();

            if (mode == TemporalMode.Recall)
            {
                Tick(1);
                _motor.ResetAt(probePosition + Vector3.back, Quaternion.identity);
                Physics.SyncTransforms();
                Tick(10);
            }

            Tick(mode == TemporalMode.Override ? 50 : 5, WithMode(mode));
            Assert.That(Vector3.Distance(_motor.transform.position, probePosition), Is.LessThan(0.05f));
            Assert.That(_director.DynamicOverlaps, Is.EqualTo(1), $"Door height {_door.transform.localPosition.y:F2}, world tick {_director.WorldTimeline.Cursor}");
            Release();
            Assert.That(capsule.isTrigger, Is.False);
            Assert.That(Physics.GetIgnoreCollision(capsule, movingDoor), Is.True);
            Assert.That(_director.PhasedColliders, Is.EqualTo(1));

            Tick(26, WithMode(TemporalMode.Normal, Vector2.down));
            Assert.That(_motor.transform.position.z, Is.LessThan(probePosition.z - 0.55f));
            Assert.That(Physics.GetIgnoreCollision(capsule, movingDoor), Is.False);
            Assert.That(_director.PhasedColliders, Is.Zero);
        }

        [Test]
        public void RecallPreservesWorldDamageAndContinuesProjectileEmission()
        {
            Tick(20);
            var saved = _rig.Capture();
            var savedPosition = _motor.transform.position;
            var target = _destructible.transform.Find("Intact Target").gameObject;
            var projectile = _emitter.transform.Find("Projectile Slot 1");
            Tick(1, WithMode(TemporalMode.Normal, fire: true));
            Tick(29, WithMode(TemporalMode.Normal, Vector2.right, new Vector2(2f, 1f)));
            Assert.That(target.activeSelf, Is.False);
            Assert.That(projectile.gameObject.activeSelf, Is.False);

            Tick(15, WithMode(TemporalMode.Recall, Vector2.left, new Vector2(40f, -40f), fire: true));
            Assert.That(_director.IsPlayerProtected, Is.True);
            Assert.That(_player.GetComponent<CapsuleCollider>().isTrigger, Is.True);
            Assert.That(_motor.transform.position, Is.EqualTo(savedPosition));
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(saved.Yaw));
            Assert.That(_rig.Capture().Pitch, Is.EqualTo(saved.Pitch));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(20));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(65));
            Assert.That(target.activeSelf, Is.False);
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(projectile.position.z, Is.GreaterThan(_emitter.transform.position.z));

            Release();
            Assert.That(_director.IsPlayerProtected, Is.False);
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(21));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(66));
            Tick(1);
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(saved.Yaw));
            Assert.That(_rig.Capture().Pitch, Is.EqualTo(saved.Pitch));
        }

        [Test]
        public void RecallRestoresAnAirborneCeilingJumpWithoutReplayingMotorEvents()
        {
            var facingWall = Quaternion.Euler(0f, 90f, 0f);
            _motor.ResetAt(new Vector3(5.8f, 0.89f, -5f), facingWall);
            _rig.ResetView(facingWall);
            Physics.SyncTransforms();
            Tick(15, WithMode(TemporalMode.Normal, Vector2.up, jumpPressed: true, jumpHeld: true));
            for (var tick = 0; tick < 155 && _motor.Up != Vector3.down; tick++)
            {
                Tick(1, WithMode(TemporalMode.Normal, Vector2.up));
            }

            Tick(36, WithMode(TemporalMode.Normal, Vector2.up));
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.Grounded, Is.True);
            var jumps = 0;
            var landings = 0;
            _motor.Jumped += () => jumps++;
            _motor.Landed += _ => landings++;
            Tick(1, WithMode(TemporalMode.Normal, Vector2.right, new Vector2(13f, 25f), jumpPressed: true, jumpHeld: true));
            var coast = WithMode(TemporalMode.Normal, Vector2.right);
            Tick(8, coast);
            var saved = _player.Capture();
            Tick(1, coast);
            var next = _motor.transform.position;
            Tick(11, coast);
            Tick(6, WithMode(TemporalMode.Recall, jumpPressed: true, jumpHeld: true));
            Assert.That(_motor.transform.position, Is.EqualTo(saved.Motor.Position));
            Assert.That(_motor.Magnetic && _motor.JumpInFlight, Is.True);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(saved.Camera.Yaw));
            Assert.That(_rig.Capture().Pitch, Is.EqualTo(saved.Camera.Pitch));
            Release();
            Tick(1, coast);
            Assert.That(_motor.transform.position, Is.EqualTo(next));
            Assert.That(jumps, Is.EqualTo(1));
            Assert.That(landings, Is.Zero);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void BulletTimeRuns120PlayerStepsAnd30WorldStepsPerSecond(int frameRate)
        {
            var input = WithMode(TemporalMode.BulletTime, Vector2.right);
            for (var frame = 0; frame < frameRate; frame++)
            {
                _director.Advance(1f / frameRate, input);
            }

            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(120));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(30));
            Assert.That(_director.PlayerTimeline.Rate, Is.EqualTo(1f));
            Assert.That(_director.WorldTimeline.Rate, Is.EqualTo(0.25f));
            Assert.That(_motor.transform.position.x, Is.GreaterThan(4f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void BulletTimeHitsBetweenWorldStepsAndKeepsFractionWhenReleased()
        {
            Tick(10);
            var moving = _movingTarget.transform.Find("Moving Target");
            var intact = _destructible.transform.Find("Intact Target").gameObject;
            Tick(2, WithMode(TemporalMode.BulletTime));
            Tick(1, WithMode(TemporalMode.BulletTime, fire: true));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(13));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(10));
            Assert.That(_director.WorldTimeline.ForwardFraction, Is.EqualTo(0.75f));
            Assert.That(intact.activeSelf, Is.False);

            var beforeRelease = moving.position;
            Release();
            Assert.That(_director.WorldTimeline.Rate, Is.EqualTo(1f));
            Assert.That(_director.WorldTimeline.ForwardFraction, Is.EqualTo(0.75f));
            Assert.That(moving.position, Is.EqualTo(beforeRelease));
            _director.Advance(Step / 4d, default);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(11));
            Tick(1, WithMode(TemporalMode.Recall));
            Assert.That(intact.activeSelf, Is.False);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(12));

            Rewind(2);
            Assert.That(intact.activeSelf, Is.True);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(10));
        }

        [Test]
        public void WorldRewindCanUndoAHitBeforeTheNextSlowStep()
        {
            Tick(10);
            var intact = _destructible.transform.Find("Intact Target").gameObject;
            Tick(1, WithMode(TemporalMode.BulletTime, fire: true));
            Assert.That(intact.activeSelf, Is.False);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(10));

            Tick(1, WithMode(TemporalMode.Override));
            Assert.That(intact.activeSelf, Is.True);
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(8));
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(12));
        }

        [Test]
        public void ModeSwitchesCommitOnlyDepartingReverseTracksAndKeepCursorsIndependent()
        {
            Tick(180);
            Tick(5, WithMode(TemporalMode.Reset));
            AssertCursors(170, 170);

            Tick(5, WithMode(TemporalMode.Override));
            AssertCursors(175, 160);
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(171));
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(176));

            Tick(5, WithMode(TemporalMode.Recall));
            AssertCursors(165, 165);
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(166));
            Tick(40, WithMode(TemporalMode.BulletTime));
            AssertCursors(205, 175);
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(206));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(176));

            Rewind(10);
            AssertCursors(195, 165);
            Tick(4, WithMode(TemporalMode.Override));
            AssertCursors(199, 157);
            Rewind(6);
            AssertCursors(193, 151);
            Tick(3, WithMode(TemporalMode.Recall));
            AssertCursors(187, 154);
            Release();
            Assert.That(_director.PlayerTimeline.Count, Is.EqualTo(188));
            Assert.That(_director.WorldTimeline.Count, Is.EqualTo(155));
        }

        [TestCase((int)TemporalMode.Override)]
        [TestCase((int)TemporalMode.Recall)]
        public void ExhaustedPowerWaitsForAReleaseAndNewPress(int modeValue)
        {
            var mode = (TemporalMode)modeValue;
            var input = WithMode(mode);
            Tick(3, input);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            AssertCursors(3, 3);
            Release();
            Tick(2, input);
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            Assert.That(_director.IsPlayerProtected, Is.False);
            var player = _director.PlayerTimeline.Cursor;
            var world = _director.WorldTimeline.Cursor;
            Tick(3, input);
            AssertCursors(player + 3, world + 3);

            var pressedAgain = input.Selection;
            pressedAgain.Release(mode);
            pressedAgain.Press(mode);
            _director.Advance(0d, new TemporalInput(default, default, false, false, false, pressedAgain));
            Assert.That(_director.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void FocusLossCommitsAndDiscardsPendingInputAndLargeFramesAreBounded()
        {
            Tick(120);
            Tick(1, WithMode(TemporalMode.Recall));
            var player = _director.PlayerTimeline.Cursor;
            var world = _director.WorldTimeline.Cursor;
            _director.Advance(33d, new TemporalInput(default, new Vector2(500f, 500f), true, true, true, default, focused: false));
            Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
            AssertCursors(player, world);
            _director.Advance(33d, WithMode(TemporalMode.BulletTime));
            AssertCursors(player + 12, world + 3);
            Assert.That(_rig.Capture().Yaw, Is.Zero);
            Assert.That(_motor.JumpInFlight, Is.False);
            Assert.That(_destructible.transform.Find("Intact Target").gameObject.activeSelf, Is.True);

            _director.Advance(33d, WithMode(TemporalMode.Reset));
            Assert.That(_director.PlayerSteps, Is.EqualTo(24));
            Assert.That(_director.WorldSteps, Is.EqualTo(24));
        }

        [Test]
        public void SubTickInputIsConsumedOnceAndSurvivesForwardModeChanges()
        {
            Tick(12);
            var jumps = 0;
            _motor.Jumped += () => jumps++;
            _director.Advance(Step / 3d, WithMode(TemporalMode.Normal, look: new Vector2(12f, 0f), jumpPressed: true));
            _director.Advance(Step / 3d, WithMode(TemporalMode.BulletTime, look: new Vector2(8f, 0f)));
            _director.Advance(Step / 3d, WithMode(TemporalMode.BulletTime, look: new Vector2(10f, 0f)));
            Tick(30, WithMode(TemporalMode.BulletTime));
            Assert.That(_rig.Capture().Yaw, Is.EqualTo(30f * 0.105f).Within(0.0001f));
            Assert.That(jumps, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewInputSystemSwitchesToMostRecentHeldPowerAndFallsBackOnRelease()
        {
            Tick(360);
            using var synthetic = new SyntheticKeyboard();
            var keyboard = synthetic.Device;
            var input = _player.GetComponent<PlaygroundInput>();
            input.SendMessage("OnApplicationFocus", true);
            _player.enabled = true;

            try
            {
                SetKeys(keyboard, Key.Q);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Reset));
                SetKeys(keyboard, Key.Q, Key.E);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Override));
                SetKeys(keyboard, Key.Q, Key.E, Key.C);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Recall));
                SetKeys(keyboard, Key.Q, Key.E, Key.C, Key.LeftShift);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.BulletTime));
                SetKeys(keyboard, Key.Q, Key.E);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Override));
                SetKeys(keyboard, Key.Q);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Reset));
                input.SendMessage("OnApplicationFocus", false);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
                input.SendMessage("OnApplicationFocus", true);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Normal));
                SetKeys(keyboard);
                SetKeys(keyboard, Key.Q);
                yield return null;
                Assert.That(_director.Mode, Is.EqualTo(TemporalMode.Reset));
            }
            finally
            {
                _player.enabled = false;
            }
        }

        [TestCase((int)TemporalMode.Override)]
        [TestCase((int)TemporalMode.Recall)]
        [TestCase((int)TemporalMode.BulletTime)]
        public void IndependentSchedulesDoNotAllocateDuringSteadyPlayback(int modeValue)
        {
            Tick(480);
            var input = WithMode((TemporalMode)modeValue);
            Tick(16, input);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Tick(160, input);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(_director.PlayerTimeline.Count, Is.LessThanOrEqualTo(481));
            Assert.That(_director.WorldTimeline.Count, Is.LessThanOrEqualTo(481));
        }

        [UnityTest]
        public IEnumerator SceneHudReflectsEachModeAndReducedMotionSuppressesRecallTrace()
        {
            var hud = _player.transform.Find("MERIDIAN HUD");
            var panel = hud.Find("Temporal Timeline");
            var status = panel.Find("Playback Status").GetComponent<TMP_Text>();
            var player = panel.Find("Rewind Binding").GetComponent<TMP_Text>();
            var world = panel.Find("World Readout").GetComponent<TMP_Text>();
            var trace = _director.transform.Find("Recall Path").GetComponent<LineRenderer>();
            Tick(180);
            Tick(4, WithMode(TemporalMode.Reset));
            yield return null;
            Assert.That(status.text, Is.EqualTo("RESET"));
            Assert.That(player.text, Does.Contain("BACK"));
            Assert.That(world.text, Does.Contain("BACK"));

            Tick(4, WithMode(TemporalMode.Override));
            yield return null;
            Assert.That(status.text, Is.EqualTo("OVERRIDE"));
            Assert.That(player.text, Does.Contain("FWD"));
            Assert.That(world.text, Does.Contain("BACK"));

            Tick(4, WithMode(TemporalMode.Recall));
            yield return null;
            Assert.That(status.text, Is.EqualTo("RECALL"));
            Assert.That(player.text, Does.Contain("BACK"));
            Assert.That(world.text, Does.Contain("FWD"));
            Assert.That(trace.enabled, Is.True);
            _rig.ReducedMotion = true;
            yield return null;
            Assert.That(trace.enabled, Is.False);
            Assert.That(hud.Find("RESET Screen Tint").GetComponent<CanvasGroup>().alpha, Is.Zero);

            Tick(8, WithMode(TemporalMode.BulletTime));
            var diagnostics = hud.Find("Debug Overlay");
            diagnostics.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(status.text, Is.EqualTo("BULLET TIME"));
            Assert.That(player.text, Does.Contain("FWD"));
            Assert.That(world.text, Does.Contain("0.25x"));
            var telemetry = diagnostics.Find("Telemetry").GetComponent<TMP_Text>();
            Assert.That(telemetry.text, Does.Contain("Steps/frame"));
            Assert.That(telemetry.text, Does.Contain("Dynamic overlap"));
            Assert.That(telemetry.isTextOverflowing, Is.False);
        }

        private void SetKeys(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private void AssertCursors(long player, long world)
        {
            Assert.That(_director.PlayerTimeline.Cursor, Is.EqualTo(player));
            Assert.That(_director.WorldTimeline.Cursor, Is.EqualTo(world));
        }

        private TemporalInput WithMode(TemporalMode mode, Vector2 move = default, Vector2 look = default, bool fire = false, bool jumpPressed = false, bool jumpHeld = false)
        {
            var selection = new TemporalSelection();
            selection.Press(mode);
            return new TemporalInput(move, look, jumpPressed, jumpHeld, fire, selection);
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
            var input = WithMode(TemporalMode.Reset);
            while (ticks > 0)
            {
                var frame = Math.Min(ticks, 20);
                _director.Advance(frame * Step / 2d, input);
                ticks -= frame;
            }
        }

        private void Release()
        {
            _director.Advance(0d, default);
        }
    }
}
