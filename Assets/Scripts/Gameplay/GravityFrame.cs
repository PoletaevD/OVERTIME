using UnityEngine;

namespace Overtime
{
    public static class GravityFrame
    {
        public static Quaternion Transport(Vector3 previousUp, Vector3 nextUp, Vector3 forward)
        {
            // Antiparallel normals have no unique rotation axis. Use heading to make the roll repeatable.
            return Vector3.Dot(previousUp, nextUp) < -0.99999f
                ? Quaternion.FromToRotation(-previousUp, nextUp) * Quaternion.AngleAxis(180f, Tangent(forward, previousUp))
                : Quaternion.FromToRotation(previousUp, nextUp);
        }

        public static Vector3 Tangent(Vector3 direction, Vector3 up)
        {
            var projected = Vector3.ProjectOnPlane(direction, up);
            if (projected.sqrMagnitude < 0.000001f)
            {
                var fallback = Mathf.Abs(up.z) < 0.9f ? Vector3.forward : Vector3.right;
                projected = Vector3.ProjectOnPlane(fallback, up);
            }

            return projected.normalized;
        }

        public static Vector3 MoveDirection(Vector2 input, Vector3 forward, Vector3 up)
        {
            var tangent = Tangent(forward, up);
            return tangent * input.y + Vector3.Cross(up, tangent) * input.x;
        }
    }
}
