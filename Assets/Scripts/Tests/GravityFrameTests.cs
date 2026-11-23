using NUnit.Framework;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class GravityFrameTests
    {
        [Test]
        public void FloorWallCeilingTransportPreservesSpeedAndTangentMotion()
        {
            var up = Vector3.up;
            var heading = Vector3.forward;
            var velocity = heading * 8.4f;
            var floorToWall = GravityFrame.Transport(up, Vector3.back, heading);
            velocity = floorToWall * velocity;
            heading = floorToWall * heading;
            Assert.That(Vector3.Distance(floorToWall * up, Vector3.back), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(heading, Vector3.up), Is.LessThan(0.0001f));

            var wallToCeiling = GravityFrame.Transport(Vector3.back, Vector3.down, heading);
            velocity = wallToCeiling * velocity;
            Assert.That(Vector3.Distance(wallToCeiling * Vector3.back, Vector3.down), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(velocity, Vector3.back * 8.4f), Is.LessThan(0.0001f));
        }

        [Test]
        public void AntiparallelNormalsRetainHeadingAndFlipRight()
        {
            var heading = new Vector3(1f, 0f, 2f).normalized;
            var turn = GravityFrame.Transport(Vector3.up, Vector3.down, heading);
            Assert.That(Vector3.Distance(turn * heading, heading), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(turn * Vector3.up, Vector3.down), Is.LessThan(0.0001f));
            var initialRight = GravityFrame.MoveDirection(Vector2.right, heading, Vector3.up);
            var afterRight = GravityFrame.MoveDirection(Vector2.right, turn * heading, Vector3.down);
            Assert.That(Vector3.Distance(afterRight, -initialRight), Is.LessThan(0.0001f));
        }

        [Test]
        public void DirectionWithDegenerateHeadingStaysOnArbitraryPlane()
        {
            var up = new Vector3(0.6f, 0.2f, -0.9f).normalized;
            var move = GravityFrame.MoveDirection(new Vector2(0.6f, 0.8f), up, up);
            Assert.That(move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Vector3.Dot(move, up), Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
