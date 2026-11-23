using UnityEngine;

namespace Overtime
{
    internal sealed class GravitySurfaceProbe
    {
        private const float CornerInset = 0.12f;
        private const float SeamTolerance = 0.08f;

        private readonly LayerMask _layers;
        private readonly RaycastHit[] _hits = new RaycastHit[24];

        internal TemporalCollision TemporalCollision { get; set; }

        internal GravitySurfaceProbe(LayerMask layers)
        {
            _layers = layers;
        }

        internal bool FindManual(Vector3 center, Vector3 up, Vector3 direction, float distance, float sensitivity, float hysteresis, out RaycastHit best)
        {
            best = default;
            var score = float.NegativeInfinity;
            var side = Vector3.Cross(up, direction);
            ConsiderManual(center, direction, direction, up, distance, sensitivity, hysteresis, ref best, ref score);
            ConsiderManual(center, (direction + side * 0.45f).normalized, direction, up, distance, sensitivity, hysteresis, ref best, ref score);
            ConsiderManual(center, (direction - side * 0.45f).normalized, direction, up, distance, sensitivity, hysteresis, ref best, ref score);
            ConsiderManual(center - up * 0.35f, direction, direction, up, distance, sensitivity, hysteresis, ref best, ref score);
            return best.collider != null;
        }

        internal bool HasFooting(Vector3 center, Vector3 up, Vector3 supportPoint, float hysteresis)
        {
            var foot = center - up * Vector3.Dot(center - supportPoint, up);
            return HasSeam(foot, up, supportPoint, hysteresis);
        }

        internal bool FindCorner(Vector3 center, Vector3 up, Vector3 travel, Vector3 wish, Vector3 supportPoint, float distance, float hysteresis, bool hasFooting, out RaycastHit candidate, out bool outer)
        {
            candidate = default;
            outer = !hasFooting;
            var foot = center - up * Vector3.Dot(center - supportPoint, up);
            var approach = outer ? travel : -travel;
            var direction = outer ? -travel : travel;
            var start = foot + up * (outer ? -CornerInset : CornerInset);
            if (!Raycast(start, direction, distance, out var hit))
            {
                return false;
            }

            if (Mathf.Abs(Vector3.Dot(hit.normal, up)) > 1f - hysteresis || Vector3.Dot(hit.normal, approach) < 0.7f || Vector3.Dot(hit.normal, outer ? wish : -wish) < 0.7f)
            {
                return false;
            }

            var seam = hit.point + hit.normal * (outer ? -SeamTolerance : SeamTolerance);
            if (!HasSeam(seam, up, supportPoint, hysteresis))
            {
                return false;
            }

            candidate = hit;
            return true;
        }

        private bool HasSeam(Vector3 point, Vector3 up, Vector3 supportPoint, float hysteresis)
        {
            var start = point + up * (CornerInset + 2f * SeamTolerance);
            return Raycast(start, -up, CornerInset * 2f + 3f * SeamTolerance, out var hit) && Vector3.Dot(hit.normal, up) >= hysteresis && Mathf.Abs(Vector3.Dot(hit.point - supportPoint, up)) <= SeamTolerance;
        }

        private void ConsiderManual(Vector3 origin, Vector3 ray, Vector3 direction, Vector3 up, float distance, float sensitivity, float hysteresis, ref RaycastHit best, ref float score)
        {
            if (!Raycast(origin, ray, distance, out var hit))
            {
                return;
            }

            var alignment = Vector3.Dot(-hit.normal, direction);
            if (alignment < sensitivity || Vector3.Dot(hit.normal, up) >= hysteresis)
            {
                return;
            }

            var candidateScore = alignment - hit.distance / distance * 0.45f;
            if (candidateScore > score)
            {
                score = candidateScore;
                best = hit;
            }
        }

        private bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            if (TemporalCollision == null || TemporalCollision.PhasedCount == 0)
            {
                return Physics.Raycast(origin, direction, out nearest, distance, _layers, QueryTriggerInteraction.Ignore);
            }

            var count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, _layers, QueryTriggerInteraction.Ignore);
            nearest = default;
            var best = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = _hits[index];
                if (hit.distance < best && !TemporalCollision.Ignores(hit.collider))
                {
                    nearest = hit;
                    best = hit.distance;
                }
            }

            return nearest.collider != null;
        }
    }
}
