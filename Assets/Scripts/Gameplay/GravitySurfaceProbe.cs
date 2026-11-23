using UnityEngine;

namespace Overtime
{
    internal sealed class GravitySurfaceProbe
    {
        private readonly LayerMask _layers;

        internal GravitySurfaceProbe(LayerMask layers)
        {
            _layers = layers;
        }

        internal bool Find(Vector3 center, Vector3 up, Vector3 direction, Vector3 blockedNormal, float distance, float sensitivity, float hysteresis, bool allowEdge, out RaycastHit best, out float edgeHeight)
        {
            best = default;
            edgeHeight = float.NegativeInfinity;
            if (direction.sqrMagnitude < 0.01f)
            {
                return false;
            }

            var score = float.NegativeInfinity;
            var side = Vector3.Cross(up, direction);
            Consider(center, direction, direction, up, blockedNormal, distance, sensitivity, hysteresis, ref best, ref score);
            Consider(center, (direction + side * 0.45f).normalized, direction, up, blockedNormal, distance, sensitivity, hysteresis, ref best, ref score);
            Consider(center, (direction - side * 0.45f).normalized, direction, up, blockedNormal, distance, sensitivity, hysteresis, ref best, ref score);
            Consider(center - up * 0.35f, direction, direction, up, blockedNormal, distance, sensitivity, hysteresis, ref best, ref score);

            // Past a convex lip, probe back into the edge from the lower side of the old surface.
            if (best.collider == null && allowEdge && Physics.Raycast(center - up * 1.15f, -direction, out var edge, distance * 0.8f, _layers, QueryTriggerInteraction.Ignore))
            {
                var separation = Vector3.Dot(center - edge.point, edge.normal);
                if (Vector3.Dot(edge.normal, up) < hysteresis && Vector3.Dot(edge.normal, blockedNormal) < hysteresis && Vector3.Dot(edge.normal, direction) > 0.75f && separation > 0.1f && separation < distance)
                {
                    best = edge;
                    var origin = edge.point - edge.normal * 0.08f + up * 1.5f;
                    var top = Physics.Raycast(origin, -up, out var lip, 1.5f, _layers, QueryTriggerInteraction.Ignore) && lip.collider == edge.collider ? lip.point : edge.point;
                    edgeHeight = Vector3.Dot(top, up);
                }
            }

            return best.collider != null;
        }

        private void Consider(Vector3 origin, Vector3 ray, Vector3 direction, Vector3 up, Vector3 blockedNormal, float distance, float sensitivity, float hysteresis, ref RaycastHit best, ref float score)
        {
            if (!Physics.Raycast(origin, ray, out var hit, distance, _layers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var alignment = Vector3.Dot(-hit.normal, direction);
            if (alignment < sensitivity || Vector3.Dot(hit.normal, up) >= hysteresis || Vector3.Dot(hit.normal, blockedNormal) >= hysteresis)
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
    }
}
