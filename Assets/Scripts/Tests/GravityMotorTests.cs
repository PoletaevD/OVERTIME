using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class GravityMotorTests
    {
        private const float StepTime = 1f / 120f;
        private readonly List<GameObject> _objects = new();
        private GravityMotor _motor;
        private Vector3 _heading;
        private Vector3 _origin;

        [SetUp]
        public void SetUp()
        {
            _origin = new Vector3(1000f, 500f, 1000f);
            _heading = Vector3.forward;
            CreateBox("Test floor", new Vector3(0f, -0.5f, 0f), new Vector3(28f, 1f, 28f));
            CreateBox("North wall", new Vector3(0f, 4f, 5.5f), new Vector3(28f, 8f, 1f));
            CreateBox("South wall", new Vector3(0f, 4f, -5.5f), new Vector3(28f, 8f, 1f));
            CreateBox("Ceiling", new Vector3(0f, 8.5f, 0f), new Vector3(28f, 1f, 28f));

            var player = new GameObject("Test capsule");
            _objects.Add(player);
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.7f;
            capsule.radius = 0.37f;
            _motor = player.AddComponent<GravityMotor>();
            InvokeAwake();
            _motor.ResetAt(_origin + new Vector3(0f, 0.88f, 0f), Quaternion.identity);
            _motor.GravityChanged += transport => _heading = transport * _heading;
            Physics.SyncTransforms();
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
            Step(Vector2.up, false, 64);
            _motor.QueueJump();
            Step(Vector2.up, true, 4);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Step(Vector2.up, false, 18);
            Assert.That(_motor.Grounded, Is.False);
            Assert.That(_motor.transform.position.y - _origin.y, Is.GreaterThan(1.4f));

            Step(Vector2.up, false, 130);
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.transform.position.z - _origin.z, Is.LessThan(4.65f));
        }

        [Test]
        public void HoldTransitionsFloorWallCeilingAndOppositeWall()
        {
            Step(Vector2.up, true, 82);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.back));
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.transform.position.y - _origin.y, Is.GreaterThan(2f));

            Step(Vector2.up, true, 75);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.Velocity.z, Is.LessThan(-3f));

            var beforeStrafe = _motor.transform.position;
            Step(Vector2.right, false, 35);
            Assert.That(Mathf.Abs(_motor.transform.position.x - beforeStrafe.x), Is.GreaterThan(0.8f));
            Step(Vector2.up, true, 123);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void CeilingJumpUnlocksMagnetAndReturnsToWorldGravity()
        {
            Step(Vector2.up, true, 154);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.down));
            Step(Vector2.zero, false, 35);
            _motor.QueueJump();
            Step(Vector2.zero, true, 22);
            Assert.That(_motor.Grounded, Is.False);
            Assert.That(_motor.Velocity.y, Is.LessThan(0f));

            Step(Vector2.zero, false, 165);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.up));
            Assert.That(_motor.Grounded, Is.True);
            Assert.That(_motor.Magnetic, Is.False);
        }

        [Test]
        public void HoldRoundsAConvexEdgeAndMagnetLocksToItsSide()
        {
            CreateBox("Convex ledge", new Vector3(0f, 1.9f, 0f), new Vector3(5f, 3.8f, 4f));
            _motor.ResetAt(_origin + new Vector3(0f, 4.68f, 0f), Quaternion.identity);
            Physics.SyncTransforms();

            Step(Vector2.up, true, 80);
            Assert.That(_motor.Up, Is.EqualTo(Vector3.forward));
            Assert.That(_motor.Magnetic, Is.True);
            Assert.That(_motor.Surface?.name, Is.EqualTo("Convex ledge"), $"Capsule {_motor.transform.position - _origin:F3}, velocity {_motor.Velocity:F3}, rotating {_motor.Transitioning}");
        }

        private void CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            _objects.Add(box);
            box.transform.position = _origin + position;
            box.AddComponent<BoxCollider>().size = size;
        }

        private void InvokeAwake()
        {
            var awake = typeof(GravityMotor).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            awake.Invoke(_motor, null);
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
