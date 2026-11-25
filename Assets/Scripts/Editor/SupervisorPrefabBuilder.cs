using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private GameObject SaveSupervisorPrefab()
        {
            var chassis = SupervisorMesh("Supervisor Chassis");
            var panel = SupervisorMesh("Supervisor Panel");
            var coil = SupervisorMesh("Supervisor Coil");
            var root = new GameObject("The Supervisor");
            try
            {
                var assembly = PlaygroundSceneBuilder.Child("Central Assembly", root.transform);
                Machined(assembly, "Lower Chassis", new Vector3(0f, 1.74f, 0f), new Vector3(3.48f, 1.22f, 2.66f), chassis, _graphite);
                Machined(assembly, "Central Armored Housing", new Vector3(0f, 2.70f, 0f), new Vector3(3.25f, 2.16f, 2.48f), chassis, _shell);
                Machined(assembly, "Isolation Collar", new Vector3(0f, 3.70f, 0f), new Vector3(2.72f, 0.29f, 2.25f), panel, _graphite);
                Machined(assembly, "Frontal Security Mask", new Vector3(0f, 2.63f, -1.20f), new Vector3(2.28f, 1.28f, 0.45f), panel, _graphite, Quaternion.Euler(90f, 0f, 0f));
                var sensor = Block(assembly, "Optical Visor", new Vector3(0f, 2.99f, -1.442f), new Vector3(1.59f, 0.16f, 0.065f), _orange, false).GetComponent<Renderer>();
                var trim = Block(assembly, "Serial Warning", new Vector3(0f, 2.30f, -1.444f), new Vector3(0.65f, 0.06f, 0.047f), _orange, false).GetComponent<Renderer>();
                PlaygroundArt.Label(assembly, "MERIDIAN  /  SUPERVISOR 01", new Vector3(0f, 2.57f, -1.458f), 0.102f, new Color(0.82f, 0.92f, 0.93f));
                var body = Hitbox(root.transform, "Chassis Armor", new Vector3(0f, 2.49f, 0f), new Vector3(2.92f, 2.53f, 2.74f));
                var armor = new List<Collider>(8);
                var parts = new List<Transform> { assembly };
                var coreLights = new List<Renderer>(4);
                var cores = new List<Collider>(4);
                var shields = new List<Collider>(2);
                var pivots = new List<Transform>(2);
                var gunLights = new List<Renderer>(3);
                var turrets = new Transform[2];

                for (var side = -1; side <= 1; side += 2)
                {
                    for (var fore = -1; fore <= 1; fore += 2)
                    {
                        Support(root.transform, side, fore, chassis, panel, coil, armor);
                    }

                    var sideCore = new Vector3(side * 1.55f, 2.53f, 0.14f);
                    var aim = Quaternion.Euler(0f, 0f, -side * 90f);
                    coreLights.Add(Core(root.transform, sideCore, aim, coil));
                    cores.Add(Hitbox(root.transform, side < 0 ? "West Core" : "East Core", sideCore + Vector3.right * (side * 0.16f), new Vector3(0.48f, 0.78f, 0.78f)));
                    var turret = PlaygroundSceneBuilder.Child(side < 0 ? "West Suppression Mount" : "East Suppression Mount", root.transform);
                    turret.SetLocalPositionAndRotation(new Vector3(side * 2.06f, 3.37f, -0.15f), Quaternion.Euler(0f, 180f + side * 12f, 0f));
                    var gun = PlaygroundSceneBuilder.Child("Suppression Module", turret);
                    Machined(gun, "Gun Jacket", Vector3.forward * 0.24f, new Vector3(0.73f, 0.61f, 1.35f), panel, _graphite);
                    Machined(gun, "Heat Sink", new Vector3(0f, 0.15f, 0.27f), new Vector3(0.67f, 0.33f, 1.1f), panel, _shell);
                    for (var barrel = -1; barrel <= 1; barrel += 2)
                    {
                        var casing = new Vector3(barrel * 0.20f, -0.08f, 0.88f);
                        Machined(gun, "Coil Housing", casing, new Vector3(0.25f, 0.45f, 0.25f), coil, _graphite, Quaternion.Euler(90f, 0f, 0f));
                        gunLights.Add(Machined(gun, "Accelerator Bore", casing + Vector3.forward * 0.24f, new Vector3(0.135f, 0.041f, 0.135f), coil, _orange, Quaternion.Euler(90f, 0f, 0f)));
                    }

                    turrets[(side + 1) / 2] = turret;
                    parts.Add(gun);
                    Machined(root.transform, "Turret Bearing", turret.localPosition + Vector3.down * 0.36f, new Vector3(0.85f, 0.35f, 0.88f), coil, _graphite);
                    armor.Add(Hitbox(root.transform, "Turret Mount Armor", new Vector3(side * 1.82f, 3.19f, -0.20f), new Vector3(0.94f, 0.8f, 0.88f)));

                    var hinge = PlaygroundSceneBuilder.Child(side < 0 ? "West Core Cover Hinge" : "East Core Cover Hinge", root.transform);
                    hinge.localPosition = new Vector3(side * 0.91f, 4.00f, 0.36f);
                    var shell = Machined(hinge, "Hinged Porcelain Panel", new Vector3(-side * 0.45f, 0f, 0f), new Vector3(0.93f, 0.15f, 1.50f), panel, _shell);
                    Block(shell.transform, "Warning Inlay", new Vector3(0.27f * side, 0.54f, 0f), new Vector3(0.11f, 0.11f, 0.88f), _orange, false);
                    var deflector = Hitbox(hinge, "Impact Deflector", new Vector3(-side * 0.46f, 0f, 0f), new Vector3(0.96f, 0.17f, 1.48f));
                    var shield = deflector.gameObject.AddComponent<CombatShield>();
                    PlaygroundSceneBuilder.Wire(shield, "_surface", shell);
                    pivots.Add(hinge);
                    shields.Add(deflector);
                    parts.Add(shell.transform);
                }

                var rear = new Vector3(0f, 2.62f, 1.37f);
                coreLights.Add(Core(root.transform, rear, Quaternion.Euler(90f, 0f, 0f), coil));
                cores.Add(Hitbox(root.transform, "Rear Core", rear + Vector3.forward * 0.08f, new Vector3(0.84f, 0.82f, 0.38f)));
                var top = new Vector3(0f, 3.83f, 0.38f);
                coreLights.Add(Core(root.transform, top, Quaternion.identity, coil));
                cores.Add(Hitbox(root.transform, "Upper Core", top + Vector3.up * 0.04f, new Vector3(0.84f, 0.25f, 0.82f)));

                var laser = PlaygroundSceneBuilder.Child("Security Sweep Mount", root.transform);
                laser.SetLocalPositionAndRotation(new Vector3(0f, 4.30f, -0.73f), Quaternion.Euler(0f, 180f, 0f));
                var optic = PlaygroundSceneBuilder.Child("Sweep Optic", laser);
                Machined(optic, "Gimbal", Vector3.zero, new Vector3(0.88f, 0.64f, 0.70f), chassis, _graphite);
                gunLights.Add(Machined(optic, "Emitter Lens", Vector3.forward * 0.35f, new Vector3(0.36f, 0.09f, 0.36f), coil, _cyan, Quaternion.Euler(90f, 0f, 0f)));
                parts.Add(optic);

                var lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = Color.white };
                lineMaterial.name = "Supervisor Warning Lines";
                var linePath = $"{RouteArt}/Supervisor Warning Lines.mat";
                var saved = AssetDatabase.LoadAssetAtPath<Material>(linePath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(lineMaterial, linePath);
                    saved = lineMaterial;
                }
                else
                {
                    Object.DestroyImmediate(lineMaterial);
                }

                var beam = WarningLine(root.transform, "Sweep Warning Beam", saved, 2, true);
                beam.enabled = false;
                var volume = PlaygroundSceneBuilder.Child("Recorded Violation Volume", root.transform);
                var rings = ViolationRings(volume, saved);
                volume.gameObject.SetActive(false);
                var sparks = BuildSupervisorSparks(root.transform, saved);

                var boss = root.AddComponent<SupervisorBoss>();
                var attacks = root.GetComponent<SupervisorAttacks>();
                var art = root.GetComponent<SupervisorPresentation>();
                var health = root.GetComponent<CombatHealth>();
                var tuning = new SerializedObject(health);
                tuning.FindProperty("_maximum").intValue = 1800;
                tuning.ApplyModifiedPropertiesWithoutUndo();
                PlaygroundSceneBuilder.Wire(attacks, "_left", turrets[0], "_right", turrets[1], "_laser", laser);
                PlaygroundSceneBuilder.Wire(art, "_coreLights", coreLights.ToArray(), "_gunLights", gunLights.ToArray(), "_statusLights", new[] { sensor, trim }, "_beam", beam, "_violation", volume, "_violationRings", rings, "_breakaway", parts.ToArray(), "_sparks", sparks);
                PlaygroundSceneBuilder.Wire(boss, "_health", health, "_attacks", attacks, "_presentation", art, "_body", body, "_armor", armor.ToArray(), "_cores", cores.ToArray(), "_shields", shields.ToArray(), "_panels", pivots.ToArray());
                return PrefabUtility.SaveAsPrefabAsset(root, $"{RoutePrefabs}/The Supervisor.prefab") ?? throw new InvalidOperationException("Failed to save The Supervisor prefab.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private Mesh SupervisorMesh(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{RouteArt}/{name}.obj");
            return model != null ? model.GetComponentInChildren<MeshFilter>().sharedMesh : throw new InvalidOperationException($"Import the Blender mesh {name} before building the Supervisor.");
        }

        private MeshRenderer Machined(Transform parent, string name, Vector3 position, Vector3 scale, Mesh mesh, Material material, Quaternion? rotation = null)
        {
            var part = PlaygroundSceneBuilder.Child(name, parent);
            part.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void Support(Transform root, int side, int fore, Mesh chassis, Mesh panel, Mesh coil, List<Collider> armor)
        {
            var basePoint = new Vector3(side * 1.98f, 0.34f, fore * 1.44f);
            Machined(root, "Magnetic Foot", basePoint, new Vector3(0.94f, 0.37f, 1.28f), chassis, _graphite);
            Machined(root, "Safety Toe", basePoint + Vector3.forward * (fore * 0.49f), new Vector3(0.84f, 0.23f, 0.34f), panel, _orange);
            armor.Add(Hitbox(root, "Support Foot Armor", basePoint, new Vector3(0.90f, 0.37f, 1.21f)));
            var upper = new Vector3(side * 1.25f, 1.55f, fore * 0.78f);
            var end = basePoint + Vector3.up * 0.24f;
            var rotation = Quaternion.FromToRotation(Vector3.up, upper - end);
            var midpoint = (upper + end) * 0.5f;
            Machined(root, "Articulated Load Strut", midpoint, new Vector3(0.62f, (upper - end).magnitude, 0.70f), panel, _shell, rotation);
            Machined(root, "Actuator Cap", upper, new Vector3(0.65f, 0.28f, 0.65f), coil, _graphite, Quaternion.Euler(0f, 0f, 90f));
            var strut = Hitbox(root, "Load Strut Armor", midpoint, new Vector3(0.59f, (upper - end).magnitude, 0.66f));
            strut.transform.localRotation = rotation;
            armor.Add(strut);
        }

        private Renderer Core(Transform root, Vector3 point, Quaternion rotation, Mesh coil)
        {
            var mount = PlaygroundSceneBuilder.Child("Reactor Cartridge", root);
            mount.SetLocalPositionAndRotation(point, rotation);
            Machined(mount, "Core Flange", Vector3.zero, new Vector3(1.07f, 0.28f, 1.07f), coil, _graphite);
            Machined(mount, "Ceramic Isolator", Vector3.up * 0.12f, new Vector3(0.85f, 0.14f, 0.85f), coil, _shell);
            return Machined(mount, "Energy Core", Vector3.up * 0.21f, new Vector3(0.68f, 0.15f, 0.68f), coil, _cyan);
        }

        private Collider Hitbox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = PlaygroundSceneBuilder.Child(name, parent).gameObject.AddComponent<BoxCollider>();
            box.transform.localPosition = position;
            box.size = size;
            return box;
        }

        private LineRenderer WarningLine(Transform parent, string name, Material material, int points, bool world)
        {
            var line = PlaygroundSceneBuilder.Child(name, parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = world;
            line.positionCount = points;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private LineRenderer[] ViolationRings(Transform parent, Material material)
        {
            const int resolution = 48;
            var rings = new LineRenderer[3];
            for (var axis = 0; axis < rings.Length; axis++)
            {
                var ring = WarningLine(parent, $"Projected Sphere Arc {axis + 1}", material, resolution, false);
                ring.loop = true;
                ring.transform.localRotation = axis == 1 ? Quaternion.Euler(90f, 0f, 0f) : axis == 2 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                for (var index = 0; index < resolution; index++)
                {
                    var angle = index * (Mathf.PI * 2f / resolution);
                    ring.SetPosition(index, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                }

                rings[axis] = ring;
            }

            return rings;
        }

        private ParticleSystem BuildSupervisorSparks(Transform parent, Material material)
        {
            var node = PlaygroundSceneBuilder.Child("Chassis Sparks", parent);
            node.localPosition = new Vector3(0f, 3.62f, 0.20f);
            var sparks = node.gameObject.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 7.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.39f, 0.12f), new Color(1f, 0.83f, 0.43f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.10f);
            main.gravityModifier = 0.65f;
            main.maxParticles = 92;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;
            var emission = sparks.emission;
            emission.rateOverTime = 8f;
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.1f;
            return sparks;
        }
    }
}
