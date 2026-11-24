using UnityEngine;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private readonly struct SpawnSpec
        {
            internal SecurityEnemyKind Kind { get; }
            internal Vector3 Position { get; }
            internal float Yaw { get; }
            internal Vector3 Volume { get; }
            internal Vector3[] Patrol { get; }

            internal SpawnSpec(SecurityEnemyKind kind, Vector3 position, float yaw, Vector3 volume, Vector3[] patrol)
            {
                Kind = kind;
                Position = position;
                Yaw = yaw;
                Volume = volume;
                Patrol = patrol;
            }
        }

        private readonly struct VariantSpec
        {
            internal string Title { get; }
            internal int Difficulty { get; }
            internal int[] Markers { get; }
            internal int Initial { get; }
            internal int Threshold { get; }
            internal int Batch { get; }
            internal int HazardMask { get; }

            internal VariantSpec(string title, int difficulty, int[] markers, int initial = 0, int threshold = 0, int batch = 3, int hazardMask = -1)
            {
                Title = title;
                Difficulty = difficulty;
                Markers = markers;
                Initial = initial;
                Threshold = threshold;
                Batch = batch;
                HazardMask = hazardMask;
            }
        }

        private VariantSpec Variant(string title, int difficulty, params int[] markers)
        {
            return new VariantSpec(title, difficulty, markers);
        }

        private SpawnSpec Guard(float x, float z, float patrolX, float patrolZ, float yaw = 180f)
        {
            return new SpawnSpec(SecurityEnemyKind.Guard, new Vector3(x, 0.92f, z), yaw, Vector3.zero, new[] { new Vector3(patrolX, 0.92f, patrolZ), new Vector3(x, 0.92f, z) });
        }

        private SpawnSpec Shield(float x, float z, float patrolX, float patrolZ, float yaw = 180f)
        {
            return new SpawnSpec(SecurityEnemyKind.Enforcer, new Vector3(x, 1f, z), yaw, Vector3.zero, new[] { new Vector3(patrolX, 1f, patrolZ), new Vector3(x, 1f, z) });
        }

        private SpawnSpec Auditor(float x, float y, float z, float yaw = 180f)
        {
            return new SpawnSpec(SecurityEnemyKind.Auditor, new Vector3(x, y, z), yaw, Vector3.zero, System.Array.Empty<Vector3>());
        }

        private SpawnSpec Drone(float x, float y, float z, float patrolX, float patrolY, float patrolZ, float yaw = 180f)
        {
            var home = new Vector3(x, y, z);
            return new SpawnSpec(SecurityEnemyKind.Drone, home, yaw, new Vector3(3.4f, 1.5f, 3.4f), new[] { new Vector3(patrolX, patrolY, patrolZ), home });
        }

        private SpawnSpec[] CheckpointMarkers()
        {
            return new[]
            {
                Guard(-5f, 11.7f, -6.35f, 14.8f, 150f),
                Guard(5.1f, 12.5f, 6.25f, 15.85f, 210f),
                Auditor(0f, 2.40f, 19.6f),
                Guard(-6.8f, 15.4f, -6.8f, 18.6f, 150f),
                Guard(7.05f, 11.3f, 7.05f, 16.5f, 215f)
            };
        }

        private SpawnSpec[] ShaftMarkers()
        {
            return new[]
            {
                Drone(-3.3f, 3.1f, 8.5f, -1.4f, 4.6f, 9.6f, 150f),
                Drone(3.3f, 5.1f, 13.6f, 1.8f, 6.2f, 12.5f, 200f),
                Drone(-1.9f, 8.1f, 17.5f, 0.9f, 7.9f, 16f),
                Auditor(0f, 3.65f, 21.1f),
                Guard(0.1f, 16.8f, -1.2f, 19.4f)
            };
        }

        private SpawnSpec[] ContainmentMarkers()
        {
            return new[]
            {
                Shield(1.8f, 8.8f, 4.4f, 9.7f, 190f),
                Guard(-6.2f, 13f, -6.45f, 15.6f, 155f),
                Guard(6.2f, 21f, 6.4f, 23.8f, 225f),
                Drone(-1.4f, 6.14f, 17f, -3.1f, 5.8f, 15f),
                Auditor(-6.55f, 2.42f, 18.4f, 154f),
                Shield(-4.1f, 22.5f, -4.1f, 18.7f, 145f),
                Drone(2.9f, 5.65f, 21.5f, 4.65f, 6.25f, 20.2f, 215f)
            };
        }

        private SpawnSpec[] ServerMarkers()
        {
            return new[]
            {
                Guard(-6.75f, 10.75f, -7f, 14.1f, 135f),
                Guard(6.9f, 11.15f, 7.1f, 14.3f, 225f),
                Guard(1.5f, 21.95f, 3.7f, 22.2f),
                Guard(-2.1f, 16.9f, -1.85f, 12.7f, 145f),
                Drone(0.5f, 4.75f, 13.5f, -1.7f, 5.3f, 15.9f),
                Shield(0.75f, 10.3f, 0.8f, 12.1f, 190f),
                Auditor(-4.35f, 0.96f, 22.1f, 160f)
            };
        }

        private SpawnSpec[] AtriumMarkers()
        {
            return new[]
            {
                Guard(-4.95f, 15.95f, -4f, 18.7f, 150f),
                Guard(5.45f, 13.6f, 5.65f, 17.9f, 210f),
                Auditor(8.8f, 4.01f, 16.5f, 222f),
                Auditor(-8.9f, 6.99f, 22.4f, 140f),
                Drone(0.3f, 5.3f, 24.1f, 2.1f, 6.65f, 25.9f, 195f),
                Shield(0.85f, 21.5f, 1.2f, 25.35f, 185f)
            };
        }

        private SpawnSpec[] CargoMarkers()
        {
            return new[]
            {
                Guard(-8.45f, 11.85f, -8.6f, 14.9f, 145f),
                Guard(3.15f, 17.8f, 2.2f, 20.8f, 200f),
                Guard(7.4f, 25f, 6.35f, 26.6f, 220f),
                Shield(-0.6f, 13.9f, 1.85f, 16.9f, 175f),
                Drone(1.85f, 5.65f, 15.2f, 0.7f, 6.05f, 18.35f, 205f),
                Drone(-3.3f, 6.35f, 20f, -1.2f, 6.65f, 22.7f, 157f),
                Auditor(5.75f, 3.57f, 21.5f, 227f)
            };
        }

        private SpawnSpec[] PowerMarkers()
        {
            return new[]
            {
                Guard(-6.2f, 12.1f, -6.5f, 15.4f, 150f),
                Guard(6.9f, 17.2f, 6.7f, 20.2f, 220f),
                Drone(5.1f, 5.1f, 10.5f, 6.9f, 5.75f, 12.3f, 225f),
                Auditor(-4.25f, 0.98f, 22.2f, 150f),
                Shield(-1.9f, 20.45f, -4.4f, 21f, 125f),
                Drone(-4.8f, 5.6f, 18.7f, -6.5f, 5.3f, 16f, 135f)
            };
        }

        private SpawnSpec[] TacticalMarkers()
        {
            return new[]
            {
                Guard(0.5f, 10.8f, 0.55f, 14.9f),
                Guard(0.6f, 18.4f, 1.1f, 21.5f),
                Guard(0.5f, 25.7f, 1.65f, 28.1f, 190f),
                Auditor(-8.15f, 2.40f, 23.9f, 139f),
                Auditor(8.3f, 2.40f, 18.7f, 221f),
                Drone(-2.6f, 4.6f, 22.7f, -0.7f, 5.25f, 24f, 155f),
                Shield(5.25f, 26.8f, 3.2f, 28.2f, 202f)
            };
        }
    }
}
