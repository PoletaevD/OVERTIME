using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private RoomDefinition BuildExpansion(string id, string title, string description, RoomCategory category, RoomGeometry tags, int minDepth, Vector3 size, Material accent, Action<Transform> layout)
        {
            var variants = new[] { Variant("Cross Shift", 2), Variant("Lateral Response", 3), Variant("Upper Watch", 4) };
            return BuildAssignment(id, title, description, category, tags, minDepth, 4, 4, size, accent, layout, Array.Empty<SpawnSpec>(), variants, RoomReward.MedicalSupplies, RoomReward.ChronoCache, RoomReward.AmmunitionCache, RoomReward.PriorityProtocol);
        }

        private void ProcessingGeometry(Transform root)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                Block(root, "Production Spine", new Vector3(6.5f * side, 1f, 23f + side * 4f), new Vector3(2.6f, 2f, 12f), _graphite);
                Block(root, "Service Walk", new Vector3(11.7f * side, 4.7f, 23f), new Vector3(4.5f, 0.5f, 26f), _shell);
                Block(root, "Work Line Indicator", new Vector3(5f * side, 0.03f, 23f), new Vector3(0.14f, 0.03f, 23f), _cyan, false);
            }

            Bay(root, "Receiving Desk", 0f, 32f, 4f, 1.8f, 2f);
        }

        private void JunctionGeometry(Transform root)
        {
            Block(root, "West Crossover Deck", new Vector3(-7.6f, 3.5f, 15f), new Vector3(8.4f, 0.7f, 14f), _shell);
            Block(root, "East Crossover Deck", new Vector3(7.2f, 7.4f, 20f), new Vector3(9.6f, 0.7f, 16f), _graphite);
            Block(root, "North Link", new Vector3(1.2f, 10.9f, 25f), new Vector3(17.5f, 0.63f, 3.3f), _shell);
            Block(root, "Nested Wall", new Vector3(-7.1f, 6f, 21.3f), new Vector3(1.4f, 8f, 3.4f), _shell);
            Block(root, "East Riser", new Vector3(8.9f, 5.3f, 9.2f), new Vector3(3.4f, 10.6f, 2.1f), _graphite);
        }

        private void TransitGeometry(Transform root)
        {
            Block(root, "Track Divider", new Vector3(0f, 1.6f, 22f), new Vector3(1.8f, 3.2f, 15f), _shell);
            Block(root, "Dispatch Overpass", new Vector3(0f, 6.15f, 29f), new Vector3(22.6f, 0.56f, 3.2f), _graphite);
            for (var side = -1; side <= 1; side += 2)
            {
                Block(root, "Freight Gallery", new Vector3(side * 11.9f, 3.25f, 25f), new Vector3(3.1f, 0.46f, 28f), _shell);
                Block(root, "Traffic Marking", new Vector3(side * 7.4f, 0.02f, 23f), new Vector3(0.2f, 0.024f, 34f), _orange, false);
                Bay(root, "Dock Gate Pylon", side * 8f, 36f + 3f * side, 2.2f, 4.6f, 1.4f);
            }
        }

        private void WireGeometry(Transform root)
        {
            Bay(root, "Distribution Cabinet", -3f, 13.6f, 2.7f, 3.5f, 2.6f);
            Bay(root, "Converter Column", 4.6f, 19f, 2.2f, 7.95f, 2.7f);
            Block(root, "Insulated Traverse", new Vector3(-6.5f, 2.85f, 17f), new Vector3(5.1f, 0.54f, 9.3f), _shell);
            Block(root, "Busbar Casing", new Vector3(0f, 7.65f, 11f), new Vector3(6.6f, 0.54f, 10f), _graphite);
        }

        private void MazeGeometry(Transform root)
        {
            for (var cell = 0; cell < 3; cell++)
            {
                var side = cell == 1 ? -1f : 1f;
                var z = 8f + cell * 5.4f;
                Block(root, "Inspection Partition", new Vector3(side * 2.65f, 1.9f, z), new Vector3(10.3f, 3.8f, 0.7f), _shell);
                Block(root, "Visible Overpass Edge", new Vector3(side * 2.65f, 3.71f, z - 0.37f), new Vector3(10.2f, 0.13f, 0.045f), _orange, false);
            }

            Block(root, "Inspection Gangway", new Vector3(-7.1f, 4.9f, 13.8f), new Vector3(2.45f, 0.45f, 14f), _graphite);
        }

        private void TurbineGeometry(Transform root)
        {
            Block(root, "Turbine Pedestal", new Vector3(0f, 1.15f, 19f), new Vector3(5.3f, 2.3f, 5.2f), _graphite);
            Block(root, "Turbine Axle Support", new Vector3(0f, 4.95f, 19f), new Vector3(1.4f, 5.3f, 1.4f), _shell);
            for (var side = -1; side <= 1; side += 2)
            {
                Block(root, "Ring Balcony", new Vector3(side * 11.4f, 3.8f, 20f), new Vector3(5.2f, 0.54f, 19f), _graphite);
                Bay(root, "Service Island", side * 6.8f, 19f + side * 8f, 2.35f, 1.5f, 3.25f);
            }
        }

        private void ObservatoryGeometry(Transform root)
        {
            Block(root, "West Viewing Platform", new Vector3(-10.9f, 4.25f, 19f), new Vector3(7.35f, 0.75f, 21f), _shell);
            Block(root, "East Viewing Platform", new Vector3(10.9f, 6.4f, 24f), new Vector3(7.35f, 0.75f, 23f), _shell);
            Block(root, "Observation Flyover", new Vector3(0f, 10.65f, 31f), new Vector3(22f, 0.62f, 3.5f), _graphite);
            Bay(root, "Optical Housing", -4.8f, 15f, 3.6f, 3.8f, 3.1f);
            Bay(root, "Telemetry Stack", 4.45f, 26f, 3.6f, 3.5f, 3.4f);
        }

        private void LockdownGeometry(Transform root)
        {
            Block(root, "West Security Partition", new Vector3(-8f, 2.7f, 14f), new Vector3(12.9f, 5.4f, 0.95f), _shell);
            Block(root, "East Security Partition", new Vector3(8.2f, 2.7f, 25.8f), new Vector3(12.6f, 5.4f, 0.95f), _shell);
            Block(root, "High Egress Route", new Vector3(-11f, 6.4f, 18f), new Vector3(4.7f, 0.52f, 24f), _graphite);
            Bay(root, "Checkpoint Island", -0.5f, 20f, 4.6f, 2.8f, 3.1f);
        }

        private void AddRoomMechanisms(RoomRuntime room, string id)
        {
            var root = PlaygroundSceneBuilder.Child("World Machinery", room.transform);
            var actors = new List<TemporalActor>();
            switch (id)
            {
                case "processing":
                    actors.Add(MovingBlock(root, "Service Block West", new Vector3(-4f, 1.25f, 14f), new Vector3(3.2f, 2.5f, 2f), Vector3.right * 4f, 6.7f));
                    actors.Add(MovingBlock(root, "Service Block East", new Vector3(3.8f, 1.25f, 28f), new Vector3(3f, 2.5f, 2f), Vector3.back * 4f, 8.5f));
                    break;
                case "cargo":
                    actors.Add(MovingBlock(root, "Automated Inspection Rail", new Vector3(-1.9f, 1.3f, 16f), new Vector3(1.8f, 2.6f, 2f), Vector3.right * 4f, 7.2f));
                    break;
                case "junction":
                    actors.Add(MovingBlock(root, "Bridge Counterweight", new Vector3(1.5f, 8.7f, 18f), new Vector3(3.3f, 2.3f, 2f), Vector3.down * 5f, 9f));
                    break;
                case "transit":
                    actors.Add(MovingBlock(root, "West Automated Freight", new Vector3(-5.9f, 1.45f, 17f), new Vector3(2.7f, 2.9f, 4.9f), Vector3.forward * 12f, 16f));
                    actors.Add(MovingBlock(root, "East Automated Freight", new Vector3(5.9f, 1.45f, 27f), new Vector3(2.7f, 2.9f, 4.9f), Vector3.back * 11f, 14f));
                    break;
                case "live-wire":
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(-2.7f, 0.11f, 9.9f), Quaternion.identity, new Vector3(2.1f, 0.46f, 2.6f), 1.25f));
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(9.36f, 3.2f, 15f), Quaternion.Euler(0f, 0f, 90f), new Vector3(2.35f, 0.46f, 2.3f), 2.1f));
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(-3.3f, 7.86f, 19f), Quaternion.Euler(0f, 0f, 180f), new Vector3(2.3f, 0.46f, 2.5f), 3.1f));
                    break;
                case "power":
                    actors.Add(Rotor(root, new Vector3(0f, 6.2f, 13.9f), 4.4f, 12.5f));
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(6.2f, 0.11f, 18f), Quaternion.identity, new Vector3(1.7f, 0.46f, 2.15f), 2.3f));
                    break;
                case "turbine":
                    actors.Add(Rotor(root, new Vector3(0f, 5.4f, 19f), 6.7f, 12.5f));
                    actors.Add(Hazard(root, ArenaHazardKind.SweepingLaser, new Vector3(0f, 1.65f, 22.03f), Quaternion.identity, new Vector3(1f, 1f, 16f), 2.4f));
                    actors.Add(Hazard(root, ArenaHazardKind.SweepingLaser, new Vector3(0f, 3.05f, 15.96f), Quaternion.Euler(0f, 180f, 32f), new Vector3(1f, 1f, 17f), 4.1f));
                    break;
                case "lockdown":
                    actors.Add(MovingBlock(root, "Lockdown Shutter", new Vector3(6.3f, 2.1f, 14f), new Vector3(3.25f, 4.2f, 0.52f), Vector3.right * 5f, 6.85f));
                    actors.Add(Hazard(root, ArenaHazardKind.SecurityBarrier, new Vector3(-5.8f, 1.55f, 25.8f), Quaternion.identity, new Vector3(2.75f, 1.55f, 0.27f), 1.7f));
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(6.1f, 0.11f, 19.4f), Quaternion.identity, new Vector3(2.6f, 0.46f, 2.2f), 2.65f));
                    actors.Add(Hazard(root, ArenaHazardKind.SecurityBarrier, new Vector3(5.9f, 1.55f, 14f), Quaternion.identity, new Vector3(2.8f, 1.55f, 0.27f), 3.5f));
                    break;
                case "containment":
                    actors.Add(MovingBlock(root, "Auxiliary Containment Shutter", new Vector3(-1.9f, 2f, 8.15f), new Vector3(2.2f, 4f, 0.35f), Vector3.up * 3.5f, 8.7f));
                    actors.Add(Hazard(root, ArenaHazardKind.SweepingLaser, new Vector3(0f, 2.1f, 24.7f), Quaternion.Euler(0f, 180f, 0f), new Vector3(1f, 1f, 16f), 1.8f));
                    actors.Add(Hazard(root, ArenaHazardKind.ElectricalPanel, new Vector3(5f, 0.11f, 13f), Quaternion.identity, new Vector3(1.75f, 0.46f, 1.8f), 2.4f));
                    actors.Add(Hazard(root, ArenaHazardKind.SecurityBarrier, new Vector3(-5.8f, 1.5f, 21f), Quaternion.identity, new Vector3(1.95f, 1.5f, 0.22f), 3.3f));
                    break;
            }

            PlaygroundSceneBuilder.Wire(room, "_worldActors", actors.ToArray());
        }

        private ArenaMechanism MovingBlock(Transform parent, string name, Vector3 position, Vector3 dimensions, Vector3 travel, float period)
        {
            var root = PlaygroundSceneBuilder.Child(name, parent);
            root.localPosition = position;
            Block(root, "Armoured Moving Housing", Vector3.zero, dimensions, _graphite);
            Block(root, "Motion Warning Strip", new Vector3(0f, dimensions.y * 0.30f, -dimensions.z * 0.51f), new Vector3(dimensions.x * 0.83f, 0.13f, 0.045f), _orange, false);
            return ConfigureMechanism(root, travel, 0f, period);
        }

        private ArenaMechanism Rotor(Transform parent, Vector3 position, float radius, float period)
        {
            var root = PlaygroundSceneBuilder.Child("Slow Turbine Rotor", parent);
            root.localPosition = position;
            for (var index = 0; index < 4; index++)
            {
                var vane = PlaygroundSceneBuilder.Child("Rotor Vane Pivot", root);
                vane.localRotation = Quaternion.Euler(0f, 90f * index, 0f);
                Block(vane, "Machined Vane", Vector3.forward * (radius * 0.64f), new Vector3(1.3f, 0.66f, radius * 0.66f), index % 2 == 0 ? _shell : _graphite);
                Block(vane, "Vane Tip", Vector3.forward * (radius * 0.95f), new Vector3(1.16f, 0.71f, 0.1f), _cyan, false);
            }

            return ConfigureMechanism(root, Vector3.zero, 360f, period);
        }

        private ArenaMechanism ConfigureMechanism(Transform root, Vector3 travel, float turns, float period)
        {
            var actor = root.gameObject.AddComponent<ArenaMechanism>();
            PlaygroundSceneBuilder.Wire(actor, "_obstacles", root.GetComponentsInChildren<Collider>());
            var data = new SerializedObject(actor);
            data.FindProperty("_travel").vector3Value = travel;
            data.FindProperty("_rotationDegrees").floatValue = turns;
            data.FindProperty("_period").floatValue = period;
            data.ApplyModifiedPropertiesWithoutUndo();
            return actor;
        }

        private ArenaHazard Hazard(Transform parent, ArenaHazardKind kind, Vector3 position, Quaternion rotation, Vector3 extent, float delay)
        {
            var root = PlaygroundSceneBuilder.Child(kind.ToString(), parent);
            root.SetLocalPositionAndRotation(position, rotation);
            var warning = PlaygroundSceneBuilder.Child("Amber Warning", root);
            var active = PlaygroundSceneBuilder.Child("Hazard Active", root);
            LineRenderer beam = null;
            if (kind == ArenaHazardKind.SweepingLaser)
            {
                Block(root, "Emitter Shroud", Vector3.back * 0.22f, new Vector3(0.42f, 0.5f, 0.42f), _graphite, false);
                Block(warning, "Charge Emitter", Vector3.zero, Vector3.one * 0.22f, _orange, false);
                Block(active, "Active Emitter", Vector3.zero, Vector3.one * 0.24f, _cyan, false);
                beam = root.gameObject.AddComponent<LineRenderer>();
                beam.positionCount = 2;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/ARC-01 Bolt.mat");
                beam.enabled = false;
            }
            else
            {
                var size = extent * 2f;
                if (kind == ArenaHazardKind.ElectricalPanel)
                {
                    Block(root, "Panel Border", Vector3.zero, new Vector3(size.x, 0.08f, size.z), _graphite, false);
                    for (var index = 0; index < 6; index++)
                    {
                        var offset = new Vector3(size.x * (index / 6f - 0.42f), 0.062f, 0f);
                        Block(warning, "Warning Strip", offset, new Vector3(size.x * 0.11f, 0.045f, size.z * 0.95f), _orange, false);
                        Block(active, "Live Conductor", offset, new Vector3(size.x * 0.13f, 0.056f, size.z * 0.95f), _violet, false);
                    }
                }
                else
                {
                    for (var index = 0; index < 6; index++)
                    {
                        var offset = Vector3.up * Mathf.Lerp(-extent.y, extent.y, (index + 0.5f) / 6f);
                        Block(warning, "Barrier Warning", offset, new Vector3(size.x, 0.015f, 0.08f), _orange, false);
                        Block(active, "Barrier Field", offset, new Vector3(size.x, 0.095f, 0.15f), _violet, false);
                    }
                }
            }

            warning.gameObject.SetActive(false);
            active.gameObject.SetActive(false);
            var hazard = root.gameObject.AddComponent<ArenaHazard>();
            PlaygroundSceneBuilder.Wire(hazard, "_warning", warning.gameObject, "_active", active.gameObject, "_beam", beam);
            var data = new SerializedObject(hazard);
            data.FindProperty("_kind").enumValueIndex = (int)kind;
            data.FindProperty("_halfExtents").vector3Value = extent;
            data.FindProperty("_length").floatValue = extent.z;
            data.FindProperty("_delay").floatValue = delay;
            data.FindProperty("_recoverySeconds").floatValue = 2.4f + delay * 0.34f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return hazard;
        }
    }
}
