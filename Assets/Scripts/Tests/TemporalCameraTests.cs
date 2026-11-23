using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    public sealed class TemporalCameraTests
    {
        [Test]
        public void RestoresYawPitchAndContinuesTheExactGravityTransition()
        {
            var host = new GameObject("Snapshot camera");
            host.SetActive(false);
            var camera = host.AddComponent<GravityCamera>();

            try
            {
                var reference = Quaternion.Euler(0f, 42f, 90f);
                var start = Quaternion.Euler(0f, 42f, 0f);
                const float progress = 0.37f;
                var display = Quaternion.Slerp(start, reference, Mathf.SmoothStep(0f, 1f, progress));
                camera.Restore(new GravityCamera.Snapshot(reference, display, start, progress, 358f, 21f));
                camera.Look(new Vector2(35f, -40f));
                var captured = camera.Capture();
                var heading = camera.Heading;
                camera.AdvanceView(1f / 120f);
                var after = camera.Capture();

                camera.ResetView(Quaternion.identity);
                camera.Look(new Vector2(-400f, 300f));
                camera.Restore(captured);
                var restored = camera.Capture();
                Assert.That(restored.Yaw, Is.EqualTo(captured.Yaw));
                Assert.That(restored.Pitch, Is.EqualTo(captured.Pitch));
                Assert.That(restored.Display, Is.EqualTo(captured.Display));
                Assert.That(restored.Reference, Is.EqualTo(reference));
                Assert.That(camera.Heading, Is.EqualTo(heading));
                Assert.That(camera.LookDelta, Is.EqualTo(Vector2.zero));

                camera.AdvanceView(1f / 120f);
                Assert.That(camera.Capture().Display, Is.EqualTo(after.Display));
                Assert.That(camera.Capture().Transition, Is.EqualTo(after.Transition));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
