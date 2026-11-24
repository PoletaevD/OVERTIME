using NUnit.Framework;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class RecoilTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void RecoilImpulseIsPersistentAndRenderRateIndependent(int framesPerSecond)
        {
            var owner = new GameObject("Camera Recoil Test");
            owner.SetActive(false);
            try
            {
                var camera = owner.AddComponent<GravityCamera>();
                camera.ResetView(Quaternion.AngleAxis(90f, Vector3.forward));
                camera.AddRecoil(new Vector2(0.25f, 1.85f), 0.05f);
                for (var index = 0; index < framesPerSecond; index++)
                {
                    camera.AdvanceView(1f / framesPerSecond);
                }

                Assert.That(camera.Yaw, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(camera.Pitch, Is.EqualTo(-1.85f).Within(0.0001f));
                Assert.That(camera.PendingRecoil.sqrMagnitude, Is.LessThan(0.000001f));
                camera.Look(new Vector2(0f, -1.85f / 0.105f));
                Assert.That(camera.Pitch, Is.Zero.Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void SnapshotPreservesUnfinishedImpulseAndPitchStopsAtLimit(float roll)
        {
            var owner = new GameObject("Camera Snapshot Test");
            owner.SetActive(false);
            try
            {
                var camera = owner.AddComponent<GravityCamera>();
                camera.ResetView(Quaternion.AngleAxis(roll, Vector3.forward));
                camera.Look(new Vector2(0f, 87f / 0.105f));
                camera.AddRecoil(new Vector2(0.5f, 4.2f), 0.05f);
                camera.AdvanceView(0.008f);
                var snapshot = camera.Capture();
                camera.AdvanceView(0.1f);
                var yaw = camera.Yaw;
                Assert.That(camera.Pitch, Is.EqualTo(-88f));
                camera.Restore(snapshot);
                Assert.That(camera.PendingRecoil.y, Is.GreaterThan(0f));
                Assert.That(camera.Pitch, Is.EqualTo(snapshot.Pitch));
                camera.AdvanceView(0.1f);
                Assert.That(camera.Pitch, Is.EqualTo(-88f));
                Assert.That(camera.Yaw, Is.EqualTo(yaw).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void ViewmodelSpringAddsImpulsesAndSettles(int framesPerSecond)
        {
            var recoil = new ViewmodelRecoil();
            var position = Vector3.zero;
            var velocity = recoil.PositionImpulse;
            var angles = Vector3.zero;
            var angularVelocity = recoil.RotationImpulse;
            var step = 1f / (4 * framesPerSecond);
            var peak = 0f;
            for (var index = 0; index < framesPerSecond * 8; index++)
            {
                if (index == Mathf.RoundToInt(0.06f / step))
                {
                    velocity += recoil.PositionImpulse;
                    angularVelocity += recoil.RotationImpulse;
                }

                recoil.Advance(step, ref position, ref velocity, ref angles, ref angularVelocity);
                peak = Mathf.Max(peak, -position.z);
                Assert.That(position.magnitude, Is.LessThanOrEqualTo(0.18f));
                Assert.That(angles.magnitude, Is.LessThanOrEqualTo(22f));
            }

            Assert.That(peak, Is.GreaterThan(0.05f));
            Assert.That(position.magnitude, Is.LessThan(0.0002f));
            Assert.That(angles.magnitude, Is.LessThan(0.002f));
        }
    }
}
