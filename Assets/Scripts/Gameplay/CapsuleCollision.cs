using UnityEngine;

namespace Overtime
{
    internal sealed class CapsuleCollision
    {
        private const float Skin = 0.022f;
        private const int SolverPasses = 5;

        private readonly CapsuleCollider _shape;
        private readonly LayerMask _mask;
        private readonly RaycastHit[] _hits = new RaycastHit[24];
        private readonly Collider[] _overlaps = new Collider[24];

        internal float Segment => _shape.height * 0.5f - _shape.radius;

        internal CapsuleCollision(CapsuleCollider shape, LayerMask mask)
        {
            _shape = shape;
            _mask = mask;
        }

        internal bool Cast(Vector3 center, Vector3 axis, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var offset = axis * Segment;
            var count = Physics.CapsuleCastNonAlloc(center + offset, center - offset, _shape.radius, direction, _hits, distance + Skin, _mask, QueryTriggerInteraction.Ignore);
            nearest = default;
            var best = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == _shape || hit.distance >= best || Vector3.Dot(direction, hit.normal) > -0.001f)
                {
                    continue;
                }

                best = hit.distance;
                nearest = hit;
            }

            return nearest.collider != null;
        }

        internal bool Move(ref Vector3 center, Vector3 axis, ref Vector3 velocity, float deltaTime)
        {
            var remaining = velocity * deltaTime;
            var collided = false;
            for (var i = 0; i < SolverPasses && remaining.sqrMagnitude > 0.00000001f; i++)
            {
                var length = remaining.magnitude;
                var direction = remaining / length;
                if (!Cast(center, axis, direction, length, out var hit))
                {
                    center += remaining;
                    break;
                }

                collided = true;
                var travel = Mathf.Clamp(hit.distance - Skin, 0f, length);
                center += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, hit.normal);
                velocity -= hit.normal * Mathf.Min(0f, Vector3.Dot(velocity, hit.normal));
            }

            return collided;
        }

        internal bool Resolve(ref Vector3 center, Quaternion orientation, float maximumCorrection)
        {
            var start = center;
            var axis = orientation * Vector3.up;
            var offset = axis * Segment;
            for (var pass = 0; pass < SolverPasses; pass++)
            {
                var count = Physics.OverlapCapsuleNonAlloc(center + offset, center - offset, _shape.radius, _overlaps, _mask, QueryTriggerInteraction.Ignore);
                var corrected = false;
                for (var i = 0; i < count; i++)
                {
                    var other = _overlaps[i];
                    if (other == _shape || !Physics.ComputePenetration(_shape, center, orientation, other, other.transform.position, other.transform.rotation, out var direction, out var depth))
                    {
                        continue;
                    }

                    corrected = true;
                    center += direction * (depth + Skin);
                    if ((center - start).sqrMagnitude > maximumCorrection * maximumCorrection)
                    {
                        center = start;
                        return false;
                    }
                }

                if (!corrected)
                {
                    return true;
                }
            }

            center = start;
            return false;
        }
    }
}
