using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private BoxCollider[] _spawnBodies;

        private VariantSpec[] ExpandEncounter(string id, Vector3 size, SpawnSpec[] authored, VariantSpec[] variants, List<SpawnSpec> roster)
        {
            if (_spawnBodies == null)
            {
                var armory = new SecurityPlaygroundBuilder(_art, null);
                _spawnBodies = new BoxCollider[4];
                for (var index = 0; index < _spawnBodies.Length; index++)
                {
                    _spawnBodies[index] = (BoxCollider)armory.GetPrefab((SecurityEnemyKind)index).GetComponent<SecurityEnemy>().Body;
                }
            }

            var counts = id switch
            {
                "checkpoint" => new[] { 8, 10 },
                "shaft" => new[] { 14, 17, 18 },
                "containment" => new[] { 20, 22, 24 },
                "surveillance-center" => new[] { 12, 13, 15 },
                "atrium" => new[] { 14, 16, 19 },
                "cargo" => new[] { 12, 15, 17 },
                "power" => new[] { 14, 17, 18 },
                "tactical" => new[] { 14, 16, 18 },
                "processing" => new[] { 13, 15, 17 },
                "junction" => new[] { 14, 17, 19 },
                "transit" => new[] { 14, 16, 18 },
                "live-wire" => new[] { 12, 14, 16 },
                "inspection-maze" => new[] { 12, 13, 14 },
                "turbine" => new[] { 16, 18, 20 },
                "observatory" => new[] { 16, 18, 20 },
                "lockdown" => new[] { 16, 19, 20 },
                _ => throw new ArgumentException(id)
            };

            var seed = 571;
            foreach (var character in id)
            {
                seed = unchecked(seed * 31 + character);
            }

            var result = new VariantSpec[variants.Length];
            for (var index = 0; index < result.Length; index++)
            {
                var definition = variants[index];
                var total = counts[index];
                var initial = id switch
                {
                    "containment" => 12 + index * 2,
                    "inspection-maze" or "surveillance-center" => 7 + index,
                    "checkpoint" => index == 0 ? 8 : 7,
                    "atrium" or "processing" when index == 0 => total,
                    "observatory" or "transit" or "turbine" => 10 + index,
                    _ => 8 + index
                };

                var droneHeavy = id is "shaft" or "junction" or "observatory" or "turbine";
                var drones = droneHeavy ? total / 2 : total / 3;
                var auditors = total >= 22 ? 2 + index / 2 : total >= 16 && index != 1 ? 2 : index == 1 && id == "checkpoint" ? 0 : 1;
                var enforcers = id == "checkpoint" ? 0 : total >= 22 ? 2 + index / 2 : total >= 16 && index == 1 ? 2 : 1;
                var available = new[] { total - drones - auditors - enforcers, drones, auditors, enforcers };
                var random = new System.Random(unchecked(seed + index * 1907));
                var assigned = new List<SpawnSpec>(total);
                foreach (var marker in definition.Markers)
                {
                    var spawn = authored[marker];
                    if (available[(int)spawn.Kind] > 0 && ClearSpawn(spawn.Position, spawn.Kind, spawn.Yaw, true) && !TooClose(spawn.Position, assigned))
                    {
                        assigned.Add(spawn);
                        available[(int)spawn.Kind]--;
                    }
                }

                while (assigned.Count < total)
                {
                    var kind = random.Next(available.Length);
                    while (available[kind] == 0)
                    {
                        kind = (kind + 1) % available.Length;
                    }

                    assigned.Add(FindSpawn((SecurityEnemyKind)kind, size, assigned, random, assigned.Count < initial, index));
                    available[kind]--;
                }

                var markers = new int[total];
                for (var slot = 0; slot < total; slot++)
                {
                    markers[slot] = roster.Count;
                    roster.Add(assigned[slot]);
                }

                var tier = id == "checkpoint" ? 1 : id == "containment" ? 5 : total <= 14 ? 2 + index / 2 : total <= 17 ? 3 + index / 2 : 4 + index / 2;
                var mask = id is "live-wire" or "lockdown" or "containment" ? index switch { 0 => 5, 1 => 6, _ => 7 } : -1;
                result[index] = new VariantSpec(definition.Title, tier, markers, initial, Mathf.Max(2, initial - 3), id == "containment" ? 4 : index == 0 ? 2 : 3, mask);
            }

            return result;
        }

        private SpawnSpec FindSpawn(SecurityEnemyKind kind, Vector3 size, List<SpawnSpec> assigned, System.Random random, bool initial, int variant)
        {
            for (var attempt = 0; attempt < 640; attempt++)
            {
                var side = (assigned.Count + variant) % 2 == 0 ? -1f : 1f;
                var lateral = (float)random.NextDouble();
                var depth = (float)random.NextDouble();
                var x = side * Mathf.Lerp(initial ? 0.7f : size.x * 0.29f, size.x * 0.5f - 1.05f, lateral);
                var z = Mathf.Lerp(initial ? 7.9f : size.z * 0.47f, size.z - 2.1f, depth);
                if (attempt >= 160)
                {
                    x = Mathf.Lerp(-size.x * 0.5f + 1f, size.x * 0.5f - 1f, lateral);
                    z = Mathf.Lerp(7.2f, size.z - 1.6f, depth);
                }

                var y = kind switch
                {
                    SecurityEnemyKind.Drone => Mathf.Lerp(2.1f, size.y - 1.15f, (float)random.NextDouble()),
                    SecurityEnemyKind.Auditor => 0.96f,
                    SecurityEnemyKind.Enforcer => 1f,
                    _ => 0.92f
                };

                var position = new Vector3(x, y, z);
                if (kind == SecurityEnemyKind.Auditor && attempt < 70 && Physics.Raycast(new Vector3(x, size.y * 0.83f, z), Vector3.down, out var surface, size.y, 1, QueryTriggerInteraction.Ignore))
                {
                    position.y = surface.point.y + 0.96f;
                }

                var yaw = Quaternion.LookRotation(new Vector3(-x, 0f, 5f - z)).eulerAngles.y;
                if (!ClearSpawn(position, kind, yaw, true) || TooClose(position, assigned))
                {
                    continue;
                }

                var patrol = position + new Vector3(-side * (1.05f + lateral), kind == SecurityEnemyKind.Drone ? (variant - 1) * 0.84f : 0f, depth > 0.5f ? -2.6f : 2.6f);
                if (!ClearSpawn(patrol, kind, yaw, false) || Physics.Linecast(position, patrol, 1, QueryTriggerInteraction.Ignore))
                {
                    patrol = position + new Vector3(0f, 0f, -1.2f);
                    if (!ClearSpawn(patrol, kind, yaw, false) || Physics.Linecast(position, patrol, 1, QueryTriggerInteraction.Ignore))
                    {
                        patrol = position;
                    }
                }

                var volume = kind == SecurityEnemyKind.Drone ? new Vector3(6.5f, Mathf.Max(2.2f, size.y * 0.60f), 7.25f) : Vector3.zero;
                return new SpawnSpec(kind, position, yaw, volume, kind == SecurityEnemyKind.Auditor ? Array.Empty<Vector3>() : new[] { patrol, position });
            }

            throw new InvalidOperationException($"No clear spawn for {kind} in arena {size}.");
        }

        private bool ClearSpawn(Vector3 position, SecurityEnemyKind kind, float yaw, bool checkSupport)
        {
            var body = _spawnBodies[(int)kind];
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var center = position + rotation * body.center;
            var extent = body.size * 0.5f;
            if (Physics.CheckBox(center, extent - Vector3.one * 0.024f, rotation, 1, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !checkSupport || kind == SecurityEnemyKind.Drone || (Physics.Raycast(center, Vector3.down, out var support, extent.y + 0.20f, 1, QueryTriggerInteraction.Ignore) && support.collider.GetComponentInParent<ArenaMechanism>() == null);
        }

        private bool TooClose(Vector3 point, List<SpawnSpec> spawns)
        {
            foreach (var spawn in spawns)
            {
                if ((spawn.Position - point).sqrMagnitude < 2.05f * 2.05f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
