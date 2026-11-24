using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class GravityMotorTests
    {
        private const float StepTime = 1f / 120f;
        private const int HoldSteps = 15;
        private const int ReleaseSteps = 42;
        private const int MovingTapSteps = 22;
        private const int EdgeGraceSteps = 12;
        private const int LocalFlightSteps = 42;

        private readonly List<GameObject> _objects = new();
        private GravityMotor _motor;
        private BoxCollider _northWall;
        private Vector3 _heading;
        private Vector3 _origin;

        [SetUp]
        public void SetUp()
        {
            _origin = new Vector3(1000f, 500f, 1000f);
            CreateBox("Test floor", new Vector3(0f, -0.5f, 0f), new Vector3(28f, 1f, 28f));
            _northWall = CreateBox("North wall", new Vector3(0f, 4f, 5.5f), new Vector3(28f, 8f, 1f));
            CreateBox("South wall", new Vector3(0f, 4f, -5.5f), new Vector3(28f, 8f, 1f));
            CreateBox("Ceiling", new Vector3(0f, 8.5f, 0f), new Vector3(28f, 1f, 28f));

            var player = new GameObject("Test capsule");
            _objects.Add(player);
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.7f;
            capsule.radius = 0.37f;
            _motor = player.AddComponent<GravityMotor>();
            typeof(GravityMotor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_motor, null);
            _motor.GravityChanged += transport => _heading = transport * _heading;
            Reset(new Vector3(0f, 0.88f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects)
            {
                Object.DestroyImmediate(item);
            }

            _objects.Clear();
        }

        [Test]
        public void TapJumpNearWallDoesNotAttachAndLands()
        {
            Reset(new Vector3(0f, 0.88f, 3.8f));
            _motor.QueueJump();
            Step(Vector2.up, true, 4);
            Assert.That(_motor.ManualAttachmentProgress, Is.GreaterThan(0f));

            Step(Vector2.up, false, 22);
            Assert.That(_motor.JumpInFlight, Is.True);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.transform.position.y - _origin.y, Is.GreaterThan(1.4f));

            Step(Vector2.up, false, 100);
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.transform.position.z - _origin.z, Is.LessThan(4.65f));
        }

        [Test]
        public void ValidWallMustBeHeldForConfiguredDelay()
        {
            Reset(new Vector3(0f, 0.88f, 3.8f));
            var jumps = 0;
            _motor.Jumped += () => jumps++;
            _motor.QueueJump();

            Step(Vector2.up, true, HoldSteps - 1);
            Assert.That(_motor.ManualCandidate, Is.EqualTo(_northWall));
            Assert.That(_motor.ManualAttachmentProgress, Is.InRange(0.95f, 0.99f));
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Step(Vector2.up, true, 1);

            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.ManualAttachment));
            Assert.That(jumps, Is.Zero);
        }

        [Test]
        public void HeldSpaceBeforeDetectionDoesNotCountTowardDelay()
        {
            Step(Vector2.zero, true, 60);
            Assert.That(_motor.ManualAttachmentProgress, Is.Zero);

            StepUntil(() => _motor.ManualCandidate != null, Vector2.up, true, 90);
            Assert.That(_motor.ManualAttachmentProgress, Is.LessThan(0.08f));
            Step(Vector2.up, true, HoldSteps - 2);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Step(Vector2.up, true, 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void LosingManualCandidateRestartsDelay()
        {
            Reset(new Vector3(0f, 0.88f, 3.8f));
            Step(Vector2.zero, true, HoldSteps - 2);

            _northWall.enabled = false;
            Step(Vector2.zero, true, 1);
            Assert.That(_motor.ManualAttachmentProgress, Is.Zero);

            _northWall.enabled = true;
            Step(Vector2.zero, true, HoldSteps - 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Step(Vector2.zero, true, 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void HeldSpaceCanAttachLateInAnOrdinaryJump()
        {
            Reset(new Vector3(0f, 0.88f, 1.5f));
            var jumps = 0;
            _motor.Jumped += () => jumps++;
            _motor.QueueJump();
            Step(Vector2.up, true, 1);
            Assert.That(_motor.JumpInFlight, Is.True);
            Assert.That(_motor.Velocity.y, Is.GreaterThan(8f));

            Step(Vector2.up, true, 42);
            Assert.That(_motor.ManualAttachmentProgress, Is.Zero);
            StepUntil(() => _motor.ManualCandidate != null, Vector2.up, true, 25);
            Assert.That(_motor.JumpInFlight, Is.True);
            Assert.That(_motor.ManualAttachmentProgress, Is.LessThan(0.08f));

            Step(Vector2.up, true, HoldSteps - 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(jumps, Is.EqualTo(1));
        }

        [Test]
        public void WallToCeilingTurnsBeforeImpactWithoutSpace()
        {
            AttachNorthWall();
            StepUntil(() => _motor.Up == Vector3.down, Vector2.up, false, 150);

            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.InnerCorner));
            Assert.That(_motor.AutomaticCandidate?.name, Is.EqualTo("Ceiling"));
            Assert.That(_motor.Speed, Is.GreaterThan(8f));
            Assert.That(_motor.Obstructed, Is.False);
            Assert.That(_motor.Velocity.z, Is.LessThan(-8f));
            StepUntil(() => _motor.Grounded && !_motor.Transitioning, Vector2.up, false, 45);
            Assert.That(_motor.Surface?.name, Is.EqualTo("Ceiling"));

            var beforeStrafe = _motor.transform.position;
            Step(Vector2.right, false, 35);
            Assert.That(Mathf.Abs(_motor.transform.position.x - beforeStrafe.x), Is.GreaterThan(0.8f));
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
        }

        [Test]
        public void OuterCornerOntoHorizontalFaceEndsMagneticMode()
        {
            var ledge = CreateBox("Convex ledge", new Vector3(0f, 0.8f, -1f), new Vector3(5f, 1.6f, 4f));
            Reset(new Vector3(0f, 0.88f, -4.2f));
            Step(Vector2.up, true, HoldSteps);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));

            StepUntil(() => _motor.Up == Vector3.up, Vector2.up, false, 130);
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.OuterCorner));
            Assert.That(_motor.AutomaticCandidate, Is.EqualTo(ledge));
            Assert.That(_motor.Magnetic, Is.False);
            Assert.That(_motor.Speed, Is.GreaterThan(7f));

            Step(Vector2.up, false, 150);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.Surface?.name, Is.EqualTo("Test floor"));
            Assert.That(_motor.AutomaticCandidate, Is.Null);
        }

        [TestCase(1f)]
        [TestCase(0.28f)]
        public void OuterCornerBetweenWallsKeepsMagnetAndSpeed(float input)
        {
            var ledge = CreateBox("Convex ledge", new Vector3(0f, 2f, -1f), new Vector3(5f, 4f, 4f));
            Reset(new Vector3(0f, 0.88f, -4.2f));
            Step(Vector2.up, true, HoldSteps);
            Step(Vector2.zero, false, 42);
            var strafe = Vector2.right * input;
            StepUntil(() => _motor.Up == Vector3.right, strafe, false, 190);

            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.OuterCorner));
            Assert.That(_motor.AutomaticCandidate, Is.EqualTo(ledge));
            Assert.That(_motor.Magnetic, Is.True);
            Assert.That(_motor.Speed, Is.EqualTo(9f * input).Within(0.15f));
            StepUntil(() => _motor.Surface == ledge, strafe, false, 38);
        }

        [Test]
        public void UnsupportedEdgeFallsBackAfterCornerGrace()
        {
            CreateBox("Thin wall", new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 0.06f));
            Reset(new Vector3(0f, 0.88f, -1.2f));
            Step(Vector2.up, true, HoldSteps);
            StepUntil(() => !_motor.Grounded && !_motor.Transitioning, Vector2.up, false, 130);

            Step(Vector2.up, false, EdgeGraceSteps / 2);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.Magnetic, Is.True);
            StepUntil(() => !_motor.Magnetic, Vector2.up, false, EdgeGraceSteps / 2 + 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.Velocity.y, Is.LessThan(0f));
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.EdgeRelease));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CeilingJumpKeepsLocalGravityBeyondCornerGraceAndLands(bool moving)
        {
            AttachNorthWall();
            StepUntil(() => _motor.Up == Vector3.down, Vector2.up, false, 150);
            Step(Vector2.zero, false, 38);
            AssertLocalJump(Vector3.down, "Ceiling", moving);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WallJumpKeepsLocalGravityAndLandsOnSameWall(bool moving)
        {
            AttachNorthWall();
            Step(Vector2.zero, false, 40);
            AssertLocalJump(Vector3.back, "North wall", moving);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void HoldingSpaceWithoutContinuationLetsGoOfWallOrCeiling(bool ceiling, bool moving)
        {
            AttachNorthWall();
            if (ceiling)
            {
                StepUntil(() => _motor.Up == Vector3.down, Vector2.up, false, 150);
            }

            var input = moving ? Vector2.right : Vector2.zero;
            Step(input, false, 42);
            Assert.That(_motor.Grounded, Is.True);
            var oldUp = _motor.Up;
            var jumps = 0;
            _motor.Jumped += () => jumps++;
            _motor.QueueJump();
            Step(input, true, 1);
            Assert.That(Vector3.Dot(_motor.Velocity, oldUp), Is.GreaterThan(8f));
            Step(input, true, ReleaseSteps - 2);

            Assert.That(_motor.Up, Is.EqualTo(oldUp));
            Assert.That(jumps, Is.EqualTo(1));
            Step(input, true, 1);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.Magnetic, Is.False);
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.ManualRelease));
            Assert.That(_motor.Velocity.y, Is.LessThan(0f));
            Assert.That(jumps, Is.EqualTo(1));
        }

        [Test]
        public void HoldingSpaceAcrossReleaseRequiresNewPressToAttach()
        {
            AttachNorthWall();
            Step(Vector2.zero, false, 42);
            _motor.QueueJump();
            Step(Vector2.zero, true, ReleaseSteps);
            Step(Vector2.up, true, 120);

            Assert.That(_motor.Surface?.name, Is.EqualTo("Test floor"));
            Assert.That(_motor.ManualCandidate, Is.Null);
            Assert.That(_motor.Magnetic, Is.False);

            Step(Vector2.up, false, 1);
            _motor.QueueJump();
            Step(Vector2.up, true, HoldSteps);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void HoldingSpaceTowardReachableCornerKeepsMagneticGravity()
        {
            AttachNorthWall();
            StepUntil(() => _motor.transform.position.y - _origin.y > 6.55f, Vector2.up, false, 130);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            _motor.QueueJump();
            Step(Vector2.up, true, ReleaseSteps + 5);

            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.InnerCorner));
            Assert.That(_motor.Magnetic, Is.True);
            Assert.That(_motor.JumpInFlight, Is.False);
        }

        [Test]
        public void ChangingDirectionBeforeReleaseHoldInvalidatesTheCorner()
        {
            AttachNorthWall();
            StepUntil(() => _motor.transform.position.y - _origin.y > 6.55f, Vector2.up, false, 130);
            _motor.QueueJump();
            StepUntil(() => _motor.AutomaticCandidate != null, Vector2.up, true, MovingTapSteps);
            Step(Vector2.right, true, ReleaseSteps);

            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.ManualRelease));
        }

        [Test]
        public void TapJumpTowardReachableCornerKeepsTheTakeoffGravity()
        {
            AttachNorthWall();
            StepUntil(() => _motor.transform.position.y - _origin.y > 6.55f, Vector2.up, false, 130);
            _motor.QueueJump();
            Step(Vector2.up, true, MovingTapSteps);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.JumpInFlight, Is.True);

            StepUntil(() => _motor.Grounded, Vector2.up, false, 90);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.Surface, Is.EqualTo(_northWall));
        }

        [Test]
        public void SideWallDoesNotTriggerAutomaticCorner()
        {
            CreateBox("Side wall", new Vector3(0.92f, 3.4f, 4.25f), new Vector3(1f, 3f, 1.5f));
            AttachNorthWall();
            Step(Vector2.up, false, 62);

            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.Surface, Is.EqualTo(_northWall));
            Assert.That(_motor.AutomaticCandidate, Is.Null);
        }

        [Test]
        public void WorldGravityRunDoesNotAutomaticallyClimb()
        {
            Step(Vector2.up, false, 140);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.Magnetic, Is.False);
            Assert.That(_motor.Surface?.name, Is.EqualTo("Test floor"));
        }

        [Test]
        public void ConsecutiveCornersKeepSpeedAndStopAutoTurningOnTheFloor()
        {
            AttachNorthWall();
            Step(Vector2.up, false, 65);
            var normals = new List<Vector3>();
            _motor.GravityChanged += _ => normals.Add(_motor.Up);
            var minimumSpeed = _motor.Speed;
            var largestStep = 0f;
            for (var i = 0; i < 480 && normals.Count < 3; i++)
            {
                var position = _motor.transform.position;
                Step(Vector2.up, false, 1);
                minimumSpeed = Mathf.Min(minimumSpeed, _motor.Speed);
                largestStep = Mathf.Max(largestStep, Vector3.Distance(position, _motor.transform.position));
            }

            Assert.That(normals, Is.EqualTo(new[] { Vector3.down, Vector3.forward, Vector3.up }));
            Assert.That(minimumSpeed, Is.GreaterThan(8f));
            Assert.That(largestStep, Is.LessThan(0.15f));
            Step(Vector2.up, false, 220);
            Assert.That(normals.Count, Is.EqualTo(3));
            Assert.That(_motor.Magnetic, Is.False);
            Assert.That(_motor.Surface?.name, Is.EqualTo("Test floor"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestoredMagneticJumpContinuesAtTheRecordedVelocityWithoutReplayingEvents(bool ceiling)
        {
            AttachNorthWall();
            if (ceiling)
            {
                StepUntil(() => _motor.Up == Vector3.down, Vector2.up, false, 150);
            }

            Step(Vector2.zero, false, 30);
            Assert.That(_motor.Grounded, Is.True);
            var up = _motor.Up;
            var position = _motor.transform.position;
            var onSurface = _motor.Capture();
            var jumps = 0;
            var landings = 0;
            var gravityChanges = 0;
            _motor.Jumped += () => jumps++;
            _motor.Landed += _ => landings++;
            _motor.GravityChanged += _ => gravityChanges++;

            _motor.QueueJump();
            Step(Vector2.right, true, 10);
            Step(Vector2.right, false, 3);
            var inFlight = _motor.Capture();
            var airbornePosition = _motor.transform.position;
            var airborneVelocity = _motor.Velocity;
            Step(Vector2.right, false, 12);
            var nextPosition = _motor.transform.position;
            var nextVelocity = _motor.Velocity;
            StepUntil(() => _motor.Grounded, Vector2.zero, false, 120);
            Assert.That(landings, Is.EqualTo(1));

            _motor.Restore(onSurface);
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.transform.position, Is.EqualTo(position));
            _motor.Restore(inFlight);
            Physics.SyncTransforms();
            Assert.That(_motor.transform.position, Is.EqualTo(airbornePosition));
            Assert.That(_motor.Velocity, Is.EqualTo(airborneVelocity));
            Assert.That(_motor.Up, Is.EqualTo(up));
            Assert.That(_motor.Magnetic && _motor.JumpInFlight && !_motor.Grounded, Is.True);

            Step(Vector2.right, false, 12);
            Assert.That(_motor.transform.position, Is.EqualTo(nextPosition));
            Assert.That(_motor.Velocity, Is.EqualTo(nextVelocity));
            Assert.That(jumps, Is.EqualTo(1));
            Assert.That(landings, Is.EqualTo(1));
            Assert.That(gravityChanges, Is.Zero);
        }

        [Test]
        public void RestoresManualAttachmentDelayAndPartialCapsuleRotation()
        {
            Reset(new Vector3(0f, 0.88f, 3.8f));
            _motor.QueueJump();
            Step(Vector2.up, true, 8);
            var hold = _motor.Capture();
            var progress = _motor.ManualAttachmentProgress;
            var approach = _heading;
            Step(Vector2.up, true, HoldSteps - 8);

            var rotation = _motor.Capture();
            var rotatingHeading = _heading;
            var partialRotation = _motor.transform.rotation;
            Assert.That(_motor.Transitioning, Is.True);

            Step(Vector2.up, false, 7);
            var expectedRotation = _motor.transform.rotation;
            var expectedVelocity = _motor.Velocity;
            _motor.Restore(hold);
            _heading = approach;
            Physics.SyncTransforms();
            Assert.That(_motor.ManualCandidate, Is.EqualTo(_northWall));
            Assert.That(_motor.ManualAttachmentProgress, Is.EqualTo(progress));
            Step(Vector2.up, true, HoldSteps - 8);
            Assert.That(Quaternion.Angle(partialRotation, _motor.transform.rotation), Is.LessThan(0.01f));

            _motor.Restore(rotation);
            _heading = rotatingHeading;
            Physics.SyncTransforms();
            Assert.That(_motor.Transitioning, Is.True);
            Step(Vector2.up, false, 7);
            Assert.That(_motor.Velocity, Is.EqualTo(expectedVelocity));
            Assert.That(Quaternion.Angle(expectedRotation, _motor.transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void RestoresMagneticReleaseHoldAndReplaysTheSameDetachment()
        {
            AttachNorthWall();
            Step(Vector2.zero, false, 30);
            _motor.QueueJump();
            Step(Vector2.zero, true, ReleaseSteps - 8);
            var held = _motor.Capture();
            var heading = _heading;
            Assert.That(_motor.Magnetic, Is.True);
            Step(Vector2.zero, true, 10);
            var velocity = _motor.Velocity;
            var position = _motor.transform.position;
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.ManualRelease));

            _motor.Restore(held);
            _heading = heading;
            Physics.SyncTransforms();
            Assert.That(_motor.Magnetic, Is.True);
            Step(Vector2.zero, true, 10);
            Assert.That(_motor.CurrentTransitionReason, Is.EqualTo(GravityMotor.TransitionReason.ManualRelease));
            Assert.That(_motor.Velocity, Is.EqualTo(velocity));
            Assert.That(_motor.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void RestoredOuterCornerRetainsItsSupportAndClearanceState()
        {
            CreateBox("Convex ledge", new Vector3(0f, 0.8f, -1f), new Vector3(5f, 1.6f, 4f));
            Reset(new Vector3(0f, 0.88f, -4.2f));
            Step(Vector2.up, true, HoldSteps);
            StepUntil(() => _motor.CurrentTransitionReason == GravityMotor.TransitionReason.OuterCorner, Vector2.up, false, 130);
            var corner = _motor.Capture();
            var heading = _heading;
            Assert.That(_motor.Transitioning, Is.True);
            Step(Vector2.up, false, 22);
            var position = _motor.transform.position;
            var velocity = _motor.Velocity;

            _motor.Restore(corner);
            _heading = heading;
            Physics.SyncTransforms();
            Step(Vector2.up, false, 22);
            Assert.That(_motor.transform.position, Is.EqualTo(position));
            Assert.That(_motor.Velocity, Is.EqualTo(velocity));
        }

        private void AssertLocalJump(Vector3 up, string surface, bool moving)
        {
            var input = moving ? Vector2.right : Vector2.zero;
            Step(input, false, 24);
            Assert.That(_motor.Up, Is.EqualTo(up));
            Assert.That(_motor.Surface?.name, Is.EqualTo(surface));
            var orientation = _motor.transform.rotation;
            var gravityChanges = 0;
            _motor.GravityChanged += _ => gravityChanges++;

            _motor.QueueJump();
            Step(input, true, 1);
            Assert.That(Vector3.Dot(_motor.Velocity, up), Is.GreaterThan(8f));
            if (moving)
            {
                Assert.That(_motor.Speed, Is.GreaterThan(8f));
            }

            Step(input, true, MovingTapSteps - 1);
            Step(input, false, LocalFlightSteps - MovingTapSteps + 1);
            Assert.That(_motor.Grounded, Is.False);
            Assert.That(_motor.JumpInFlight, Is.True);
            Assert.That(Vector3.Dot(_motor.Velocity, up), Is.LessThan(0f));
            Assert.That(_motor.Up, Is.EqualTo(up));
            Assert.That(_motor.Magnetic, Is.True);
            Assert.That(Quaternion.Angle(_motor.transform.rotation, orientation), Is.LessThan(0.01f));

            StepUntil(() => _motor.Grounded, input, false, 90);
            Assert.That(_motor.JumpInFlight, Is.False);
            Assert.That(_motor.Surface?.name, Is.EqualTo(surface));
            Assert.That(gravityChanges, Is.Zero);
        }

        [Test]
        public void GravityProtocolsUseCompletedTransitionsAndActualSurfaceSupport()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            var build = new RunUpgradeState();
            foreach (var id in new[] { UpgradeId.KineticCharge, UpgradeId.MagneticAcceleration, UpgradeId.AutoFeed, UpgradeId.Freefall })
            {
                build.Acquire(catalog.Definitions[(int)id]);
            }

            var gun = _motor.gameObject.AddComponent<CombatWeapon>();
            gun.BindUpgrades(build, null);
            var startingAmmo = new CombatWeapon.Snapshot
            {
                Primary = new WeaponState
                {
                    Magazine = 20,
                    Reserve = 4
                }
            };
            gun.Restore(startingAmmo);
            var effects = new PlayerUpgradeEffects(build, _motor, gun);
            _motor.BindUpgrades(build);
            try
            {
                Reset(new Vector3(0f, 0.88f, 3.8f));
                Assert.That(_motor.MovementSpeed, Is.EqualTo(9f));
                Step(Vector2.up, true, HoldSteps - 1);
                Assert.That(effects.Charged, Is.False);
                Assert.That(gun.Magazine, Is.EqualTo(20));

                StepUntil(() => effects.Charged, Vector2.up, true, 100);
                Assert.That(_motor.Transitioning, Is.False);
                Assert.That(_motor.MovementSpeed, Is.EqualTo(10.35f).Within(0.001f));
                Assert.That(gun.Magazine, Is.EqualTo(23));
                Assert.That(gun.Reserve, Is.EqualTo(1));
                Assert.That(effects.State.AutoFeed, Is.EqualTo(1f));
                StepUntil(() => _motor.Grounded, Vector2.up, false, 35);
                effects.ConfirmShot();
                Assert.That(effects.ModifyShotDamage(12f), Is.EqualTo(12f));

                StepUntil(() => _motor.Up == Vector3.down && !_motor.Transitioning, Vector2.up, false, 170);
                Assert.That(effects.Charged, Is.True);
                Assert.That(gun.Magazine, Is.EqualTo(23));
                Assert.That(gun.Reserve, Is.EqualTo(1));

                effects.Simulate(1.1f);
                AttachNorthWall();
                StepUntil(() => !_motor.Transitioning, Vector2.up, true, 90);
                Assert.That(gun.Magazine, Is.EqualTo(24));
                Assert.That(gun.Reserve, Is.Zero);
            }
            finally
            {
                effects.Unbind();
            }
        }

        [Test]
        public void VerticalAdvantageRequiresMagneticSupportAwayFromTheFloor()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            var build = new RunUpgradeState();
            build.Acquire(catalog.Definitions[(int)UpgradeId.VerticalAdvantage]);
            var gun = _motor.gameObject.AddComponent<CombatWeapon>();
            var effects = new PlayerUpgradeEffects(build, _motor, gun);
            try
            {
                Reset(new Vector3(0f, 0.88f, 3.8f));
                Step(Vector2.zero, false, 4);
                Assert.That(_motor.Grounded, Is.True);
                Assert.That(effects.ModifyShotDamage(12f), Is.EqualTo(12f));
                AttachNorthWall();
                StepUntil(() => _motor.Grounded && !_motor.Transitioning, Vector2.up, false, 95);
                Assert.That(_motor.Magnetic, Is.True);
                Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
                Assert.That(effects.ModifyShotDamage(12f), Is.EqualTo(15f));
            }
            finally
            {
                effects.Unbind();
            }
        }

        [Test]
        public void DeadlineChargesWhileTheMotorTravelsAcrossTheMagneticCeiling()
        {
            AttachNorthWall();
            StepUntil(() => _motor.Up == Vector3.down, Vector2.up, false, 150);
            StepUntil(() => _motor.Grounded && !_motor.Transitioning, Vector2.up, false, 45);
            Assert.That(_motor.Surface?.name, Is.EqualTo("Ceiling"));

            var gun = _motor.gameObject.AddComponent<CombatWeapon>();
            var rail = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Weapons/ARC-03 Deadline.asset");
            Assert.That(gun.EquipSecondary(rail), Is.True);
            var aim = new Ray(_motor.transform.position, _heading);
            var playerStep = TemporalTimeline.TickDuration;
            gun.Simulate(playerStep, false, false, true, aim, _motor.Up, null, selection: WeaponSlotInput.Secondary);
            for (var tick = 0; tick < 36; tick++)
            {
                gun.Simulate(playerStep, false, false, true, aim, _motor.Up, null);
            }

            var origin = _motor.transform.position;
            for (var tick = 0; tick < 40; tick++)
            {
                Step(Vector2.right, false, 1);
                var ceilingAim = new Ray(_motor.transform.position, _heading);
                gun.Simulate(playerStep, true, false, true, ceilingAim, _motor.Up, null, TemporalMode.BulletTime, fireHeld: true);
            }

            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.Surface?.name, Is.EqualTo("Ceiling"));
            Assert.That(Vector3.Distance(_motor.transform.position, origin), Is.GreaterThan(1f));
            Assert.That(gun.ChargeProgress, Is.EqualTo(40f / 84f).Within(0.0001f));
            Assert.That(gun.Magazine, Is.EqualTo(4));
        }

        private void AttachNorthWall()
        {
            Reset(new Vector3(0f, 0.88f, 3.8f));
            Step(Vector2.up, true, HoldSteps);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
        }

        private void Reset(Vector3 position)
        {
            _heading = Vector3.forward;
            _motor.ResetAt(_origin + position, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private BoxCollider CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            _objects.Add(box);
            box.transform.position = _origin + position;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private void StepUntil(System.Func<bool> condition, Vector2 input, bool held, int limit)
        {
            for (var i = 0; i < limit && !condition(); i++)
            {
                Step(input, held, 1);
            }

            Assert.That(condition(), Is.True, $"Position {_motor.transform.position - _origin:F2}, velocity {_motor.Velocity:F2}, up {_motor.Up}, reason {_motor.CurrentTransitionReason}");
        }

        private void Step(Vector2 input, bool held, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                _motor.Step(input, _heading, held, StepTime);
            }
        }
    }
}
