using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed class SecurityPlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/Security Playground.unity";
        private const string ArtPath = "Assets/Art/Security Playground";
        private const string PrefabPath = "Assets/Prefabs/Security";
        private const int SecurityLayer = 8;
        private static readonly string[] s_names = { "Security Unit", "Surveillance Drone", "Laser Auditor" };

        private readonly PlaygroundArt _art;
        private readonly Transform _room;
        private readonly Material _graphite = Load("Graphite Alloy");
        private readonly Material _shell = Load("Porcelain Shell");
        private readonly Material _cyan = Load("Cyan Guide");
        private readonly Material _orange = Load("Safety Orange");
        private readonly Material _violet = Load("Violet Guide");
        private readonly Material _trace = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Temporal Playground/Temporal Trace.mat");

        internal SecurityEnemy[] Enemies { get; private set; }

        internal SecurityPlaygroundBuilder(PlaygroundArt art, Transform room)
        {
            _art = art;
            _room = room;
        }

        [MenuItem("OVERTIME/Create Security Playground")]
        public static void Create()
        {
            CombatPlaygroundBuilder.Create(ScenePath, true);
        }

        internal TemporalActor[] Build(TemporalDirector director)
        {
            RegisterLayer();
            Directory.CreateDirectory(PrefabPath);
            AssetDatabase.Refresh();

            BuildArena();
            var projectiles = BuildPool(director);
            var guard = GetPrefab(SecurityEnemyKind.Guard);
            var drone = GetPrefab(SecurityEnemyKind.Drone);
            var auditor = GetPrefab(SecurityEnemyKind.Auditor);
            Enemies = new[]
            {
                PlaceEnemy(guard, "Security Unit 01", 101, new Vector3(-3.95f, 0.92f, 2.5f), director, projectiles,
                    new Vector3(-4.7f, 0.92f, 1.9f), new Vector3(-5.25f, 0.92f, 5.8f), new Vector3(-3.05f, 0.92f, 5.3f), new Vector3(-3.4f, 0.92f, 0.5f)),
                PlaceEnemy(guard, "Security Unit 02", 102, new Vector3(4.75f, 0.92f, 4.1f), director, projectiles,
                    new Vector3(5.35f, 0.92f, 2.1f), new Vector3(3.9f, 0.92f, 1.2f), new Vector3(2.7f, 0.92f, 5.9f), new Vector3(5.35f, 0.92f, 6.5f)),
                PlaceEnemy(drone, "Surveillance Drone", 103, new Vector3(-0.7f, 3.9f, 7.9f), director, projectiles,
                    new Vector3(-2.2f, 4.5f, 4.2f), new Vector3(-0.8f, 5.2f, 8.9f), new Vector3(2.2f, 3.7f, 8.4f), new Vector3(0.9f, 3.1f, 5.1f)),
                PlaceEnemy(auditor, "Laser Auditor", 104, new Vector3(3.15f, 2.86f, 10.1f), director, null)
            };

            var actors = new TemporalActor[Enemies.Length + 1];
            actors[0] = projectiles;
            Enemies.CopyTo(actors, 1);
            return actors;
        }

        private void BuildArena()
        {
            var geometry = PlaygroundSceneBuilder.Child("Live Trial Structures", _room);
            var elevated = PlaygroundSceneBuilder.Child("Upper Gallery", geometry);
            var cover = PlaygroundSceneBuilder.Child("Floor Cover", geometry);
            Barrier(cover, "Entry Blast Screen", 0f, -4.4f, 4.2f, 1.8f, 0.87f);
            Barrier(cover, "West Load Rack", -1.55f, 0.55f, 1.65f, 2.28f, 1.1f);
            Barrier(cover, "East Checkpoint", 3.05f, -2.65f, 1.84f, 1.25f, 1.2f);
            Barrier(cover, "Central Inspection Pylon", -0.35f, 5.35f, 1.52f, 2.65f, 1.35f);
            Barrier(cover, "West Service Bench", -4.6f, 9.6f, 2.35f, 1.24f, 1.3f);

            Block(elevated, "Auditor Gallery", 3.65f, 0.975f, 10f, 3.9f, 1.95f, 4.05f, _graphite, true);
            Block(elevated, "Gallery Fascia", 3.65f, 1.64f, 7.96f, 3.82f, 0.22f, 0.055f, _orange);
            Block(elevated, "Gallery Step", 4.75f, 0.42f, 7.37f, 1.56f, 0.84f, 1.15f, _shell, true);
            Barrier(elevated, "Escape Hatch Housing", 3.0f, 12.68f, 2.1f, 1.12f, 0.86f);

            Block(elevated, "Overhead Line-of-Sight Baffle", 4.67f, 5.86f, 5.6f, 4.66f, 2.28f, 0.9f, _graphite, true);
            Block(elevated, "Baffle Safety Light", 4.65f, 4.78f, 5.13f, 4.55f, 0.10f, 0.07f, _orange);
            Block(elevated, "Ceiling Junction", -4.98f, 6.44f, 8.15f, 4.04f, 1.12f, 1.75f, _graphite, true);
            Block(elevated, "Junction Luminous Edge", -4.98f, 5.92f, 7.25f, 3.94f, 0.11f, 0.055f, _cyan);

            for (var index = 0; index < 4; index++)
            {
                var z = -6.9f + index * 1.1f;
                Block(geometry, "Ascent Route / Wall", -6.97f, 3.5f, z, 0.034f, 6.85f, 0.105f, _cyan);
                Block(geometry, "Ascent Route / Ceiling", -4.5f, 6.973f, z, 4.95f, 0.034f, 0.105f, _cyan);
            }

            Block(geometry, "Overhead Service Track", -1.3f, 6.971f, 5f, 0.085f, 0.034f, 14f, _violet);
            Block(geometry, "Overhead Service Track", -2.2f, 6.971f, 5f, 0.085f, 0.034f, 14f, _violet);
            var wallSign = PlaygroundArt.Label(_room, "WALL ACCESS   /   HOLD SPACE", new Vector3(-6.94f, 2.63f, -5.8f), 0.23f, new Color(0.37f, 0.91f, 0.97f));
            wallSign.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            PlaygroundArt.Label(elevated, "AUDITOR  /  CONTROLLED AREA", new Vector3(3.65f, 1.41f, 7.944f), 0.25f, Color.white);
            AddFill("Entry Fill", new Vector3(-3f, 5.3f, -4.5f), new Color(0.76f, 0.88f, 1f), 18f);
            AddFill("Arena Fill", new Vector3(2.1f, 5.15f, 2.05f), new Color(0.89f, 0.94f, 1f), 22f);
            AddFill("Gallery Fill", new Vector3(0.2f, 5.3f, 9.7f), new Color(1f, 0.87f, 0.74f), 17f);
        }

        private void Barrier(Transform parent, string name, float x, float z, float width, float height, float depth)
        {
            var shell = Block(parent, name, x, height * 0.5f, z, width, height, depth, _shell, true);
            var edge = depth * 0.5f + 0.026f;
            Block(parent, $"{shell.name} / Fascia", x, height * 0.34f, z - edge, width * 0.88f, height * 0.52f, 0.06f, _graphite);
            Block(parent, $"{shell.name} / Warning Edge", x, height * 0.88f, z - edge, width * 0.92f, 0.08f, 0.06f, _orange);
        }

        private void AddFill(string name, Vector3 position, Color color, float intensity)
        {
            var light = PlaygroundSceneBuilder.Child(name, _room).gameObject.AddComponent<Light>();
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = intensity;
            light.color = color;
        }

        private TemporalProjectileEmitter BuildPool(TemporalDirector director)
        {
            var root = PlaygroundSceneBuilder.Child("Enemy Projectile Pool", _room);
            var slots = new Transform[20];
            for (var index = 0; index < slots.Length; index++)
            {
                var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bolt.name = $"Bolt {index + 1:00}";
                bolt.transform.SetParent(root, false);
                bolt.transform.localScale = new Vector3(0.26f, 0.26f, 0.66f);
                bolt.GetComponent<Renderer>().sharedMaterial = _orange;
                Object.DestroyImmediate(bolt.GetComponent<Collider>());
                AddLine(bolt, 0);
                bolt.SetActive(false);
                slots[index] = bolt.transform;
            }

            var pool = root.gameObject.AddComponent<TemporalProjectileEmitter>();
            PlaygroundSceneBuilder.Wire(pool, "_projectiles", slots, "_combatDirector", director, "_droneBolt", _cyan);
            var data = new SerializedObject(pool);
            data.FindProperty("_automatic").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }

        private GameObject GetPrefab(SecurityEnemyKind kind)
        {
            var name = s_names[(int)kind];
            var path = $"{PrefabPath}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(name) { layer = SecurityLayer };
            var model = PlaygroundSceneBuilder.Child("Model", root.transform);
            var aim = PlaygroundSceneBuilder.Child("Aim Pivot", model);
            var recoil = PlaygroundSceneBuilder.Child("Recoil Pivot", aim);
            var wreck = PlaygroundSceneBuilder.Child("Wreck", root.transform);
            var rupture = BuildRupture(root.transform);
            var health = root.AddComponent<CombatHealth>();
            var presentation = root.AddComponent<SecurityEnemyPresentation>();
            var enemy = root.AddComponent<SecurityEnemy>();
            var body = root.AddComponent<BoxCollider>();
            Collider weak = null;
            Renderer shell;
            Renderer lens;
            var attackOrigin = kind == SecurityEnemyKind.Drone ? Vector3.zero : new Vector3(0f, 0.36f, 0f);
            aim.localPosition = attackOrigin;
            LineRenderer line = null;

            switch (kind)
            {
                case SecurityEnemyKind.Guard:
                    body.size = new Vector3(0.98f, 1.76f, 0.86f);
                    shell = BuildGuard(model, recoil, out lens);
                    break;
                case SecurityEnemyKind.Drone:
                    body.size = new Vector3(1.28f, 0.66f, 0.94f);
                    shell = BuildDrone(model, recoil, out lens);
                    wreck.localPosition = new Vector3(0f, -0.68f, 0f);
                    break;
                default:
                    body.center = new Vector3(0f, -0.13f, 0f);
                    body.size = new Vector3(1.48f, 1.63f, 1.38f);
                    shell = BuildAuditor(model, recoil, out lens);
                    var core = Block(root.transform, "Exposed Rear Power Cell", 0f, -0.29f, -0.72f, 0.38f, 0.32f, 0.17f, _orange, true);
                    core.gameObject.layer = SecurityLayer;
                    weak = core.GetComponent<Collider>();
                    // Keep the visual cell on the collapsing model and the hitbox on the actor.
                    var coreRenderer = core.GetComponent<MeshRenderer>();
                    Object.DestroyImmediate(coreRenderer);
                    Object.DestroyImmediate(core.GetComponent<MeshFilter>());
                    Block(model, "Rear Power Cell", 0f, -0.29f, -0.72f, 0.38f, 0.32f, 0.17f, _orange);
                    line = AddLine(PlaygroundSceneBuilder.Child("Laser", root.transform).gameObject, 2);
                    break;
            }

            BuildWreck(wreck);
            PlaygroundSceneBuilder.Wire(presentation, "_model", model, "_aimPivot", aim, "_recoilPivot", recoil, "_wreck", wreck, "_rupture", rupture, "_shell", shell, "_lens", lens, "_beam", line);
            PlaygroundSceneBuilder.Wire(enemy, "_health", health, "_body", body, "_weakPoint", weak, "_presentation", presentation);
            Configure(kind, enemy, presentation, health, attackOrigin);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab : throw new InvalidOperationException($"Failed to save {path}.");
        }

        private Renderer BuildGuard(Transform model, Transform aim, out Renderer lens)
        {
            var shell = Armor(model, "Guard Chassis", new Vector3(0f, 0.02f, 0f), new Vector3(0.84f, 0.68f, 0.60f));
            Block(model, "Waist Bearing", 0f, -0.39f, 0f, 0.48f, 0.18f, 0.45f, _graphite);
            for (var side = -1; side <= 1; side += 2)
            {
                Block(model, "Actuator", side * 0.27f, -0.58f, 0f, 0.27f, 0.43f, 0.31f, _graphite);
                Block(model, "Shin Guard", side * 0.29f, -0.58f, 0.16f, 0.24f, 0.35f, 0.13f, _shell);
                Block(model, "Traction Boot", side * 0.28f, -0.82f, 0.04f, 0.30f, 0.145f, 0.48f, _graphite);
            }

            Block(aim, "Sensor Head", -0.16f, 0.21f, 0.08f, 0.42f, 0.34f, 0.46f, _graphite);
            Block(aim, "Sensor Brow", -0.16f, 0.36f, 0.065f, 0.47f, 0.10f, 0.51f, _shell);
            lens = Block(aim, "Visor", -0.16f, 0.24f, 0.32f, 0.32f, 0.065f, 0.055f, _cyan).GetComponent<Renderer>();
            Block(aim, "Weapon Housing", 0.28f, -0.12f, 0.24f, 0.38f, 0.31f, 0.56f, _graphite);
            Block(aim, "Weapon Shroud", 0.28f, -0.08f, 0.49f, 0.32f, 0.22f, 0.34f, _shell);
            Block(aim, "Muzzle", 0.28f, -0.08f, 0.675f, 0.19f, 0.15f, 0.06f, _orange);
            return shell;
        }

        private Renderer BuildDrone(Transform model, Transform aim, out Renderer lens)
        {
            var shell = Armor(model, "Drone Fuselage", Vector3.zero, new Vector3(1.02f, 0.59f, 0.88f));
            for (var side = -1; side <= 1; side += 2)
            {
                var fin = Block(model, "Stabilizer", side * 0.59f, -0.015f, -0.09f, 0.40f, 0.15f, 0.65f, _graphite);
                fin.localRotation = Quaternion.Euler(0f, side * 21f, side * 16f);
                Block(model, "Thrust Strip", side * 0.68f, -0.092f, -0.09f, 0.25f, 0.055f, 0.38f, _cyan);
            }

            lens = Lens(aim, "Gimballed Optic", new Vector3(0f, 0f, 0.48f), 0.34f, _cyan);
            Block(aim, "Rear Scan Stripe", 0f, 0.07f, -0.415f, 0.34f, 0.065f, 0.052f, _orange);
            return shell;
        }

        private Renderer BuildAuditor(Transform model, Transform aim, out Renderer lens)
        {
            Block(model, "Anchored Foot", 0f, -0.80f, 0f, 1.85f, 0.23f, 1.58f, _graphite);
            Block(model, "Bearing Column", 0f, -0.38f, 0f, 1.28f, 0.78f, 1.14f, _graphite);
            Block(model, "Column Stripe", 0f, -0.4f, 0.587f, 0.76f, 0.10f, 0.055f, _orange);
            var shell = Armor(aim, "Auditor Mantle", new Vector3(0f, 0f, 0.12f), new Vector3(1.5f, 0.79f, 1.11f));
            for (var side = -1; side <= 1; side += 2)
            {
                Block(aim, "Cooling Cheek", side * 0.45f, 0f, 0.42f, 0.21f, 0.48f, 0.55f, _graphite);
                Block(aim, "Emitter Cheek Inlay", side * 0.44f, 0f, 0.707f, 0.11f, 0.36f, 0.04f, _orange);
            }

            lens = Lens(aim, "Laser Emitter Lens", new Vector3(0f, 0f, 0.65f), 0.46f, _orange);
            return shell;
        }

        private Renderer Lens(Transform parent, string name, Vector3 position, float diameter, Material material)
        {
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = $"{name} / Aperture";
            rim.transform.SetParent(parent, false);
            rim.transform.localPosition = position;
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localScale = new Vector3(diameter * 1.35f, 0.11f, diameter * 1.35f);
            rim.GetComponent<Renderer>().sharedMaterial = _graphite;
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            var glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glass.name = name;
            glass.transform.SetParent(parent, false);
            glass.transform.localPosition = position + Vector3.forward * 0.097f;
            glass.transform.localRotation = rim.transform.localRotation;
            glass.transform.localScale = new Vector3(diameter, 0.035f, diameter);
            var renderer = glass.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            return renderer;
        }

        private Transform BuildRupture(Transform root)
        {
            var burst = PlaygroundSceneBuilder.Child("Circuit Rupture", root);
            for (var index = 0; index < 4; index++)
            {
                var spark = Block(burst, "Arc", 0f, 0f, 0f, 0.082f, 0.67f, 0.08f, index < 2 ? _orange : _cyan);
                spark.localRotation = Quaternion.Euler(index * 47f, index * 41f, index * 53f);
            }

            burst.gameObject.SetActive(false);
            return burst;
        }

        private void BuildWreck(Transform wreck)
        {
            Block(wreck, "Fallen Shell", 0.25f, -0.66f, 0.03f, 0.62f, 0.24f, 0.68f, _shell).localRotation = Quaternion.Euler(13f, 28f, 20f);
            Block(wreck, "Drive Fragment", -0.29f, -0.75f, -0.19f, 0.5f, 0.22f, 0.39f, _graphite).localRotation = Quaternion.Euler(7f, -38f, -11f);
            Block(wreck, "Hot Core", 0.05f, -0.59f, -0.18f, 0.23f, 0.094f, 0.25f, _orange).localRotation = Quaternion.Euler(12f, 30f, 0f);
            wreck.gameObject.SetActive(false);
        }

        private void Configure(SecurityEnemyKind kind, SecurityEnemy enemy, SecurityEnemyPresentation presentation, CombatHealth health, Vector3 attackOrigin)
        {
            var drone = kind == SecurityEnemyKind.Drone;
            var turret = kind == SecurityEnemyKind.Auditor;
            var healthData = new SerializedObject(health);
            healthData.FindProperty("_maximum").intValue = turret ? 90 : drone ? 45 : 70;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            var visualData = new SerializedObject(presentation);
            visualData.FindProperty("_kind").enumValueIndex = (int)kind;
            visualData.ApplyModifiedPropertiesWithoutUndo();

            var data = new SerializedObject(enemy);
            data.FindProperty("_kind").enumValueIndex = (int)kind;
            data.FindProperty("_attackOrigin").vector3Value = attackOrigin;
            data.FindProperty("_moveRadius").floatValue = drone ? 0.80f : 0.48f;
            data.FindProperty("_moveSpeed").floatValue = turret ? 0f : drone ? 4.5f : 3.5f;
            data.FindProperty("_detectionRange").floatValue = turret ? 25f : drone ? 22f : 18f;
            data.FindProperty("_yawSpeed").floatValue = turret ? 500f : drone ? 420f : 320f;
            data.FindProperty("_pitchSpeed").floatValue = turret ? 340f : drone ? 360f : 120f;
            data.FindProperty("_damage").intValue = turret ? 30 : drone ? 13 : 8;
            data.FindProperty("_burstSize").intValue = drone ? 1 : 3;
            data.FindProperty("_cooldownSeconds").floatValue = turret ? 2.2f : drone ? 1.65f : 1.3f;
            data.FindProperty("_projectileSpeed").floatValue = drone ? 9f : 16f;
            data.FindProperty("_projectileRadius").floatValue = drone ? 0.19f : 0.13f;
            data.FindProperty("_projectileLifetime").floatValue = drone ? 2.6f : 2.2f;
            data.FindProperty("_flightHalfExtents").vector3Value = new Vector3(5.35f, 2.12f, 12f);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private SecurityEnemy PlaceEnemy(GameObject prefab, string name, int id, Vector3 position, TemporalDirector director, TemporalProjectileEmitter projectiles, params Vector3[] points)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _room);
            instance.name = name;
            instance.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var route = new Transform[points.Length];
            if (route.Length > 0)
            {
                var group = PlaygroundSceneBuilder.Child($"{name} Patrol", _room);
                for (var index = 0; index < route.Length; index++)
                {
                    route[index] = PlaygroundSceneBuilder.Child($"Waypoint {index + 1}", group);
                    route[index].localPosition = points[index];
                }
            }

            var enemy = instance.GetComponent<SecurityEnemy>();
            PlaygroundSceneBuilder.Wire(enemy, "_director", director, "_projectiles", projectiles, "_patrol", route);
            var data = new SerializedObject(enemy);
            data.FindProperty("_rewardId").intValue = id;
            data.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private Renderer Armor(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var part = PlaygroundSceneBuilder.Child(name, parent);
            part.SetLocalPositionAndRotation(position, Quaternion.identity);
            part.localScale = scale;
            var filter = part.gameObject.AddComponent<MeshFilter>();
            var path = $"{ArtPath}/{name}.obj";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Mesh mesh)
                {
                    filter.sharedMesh = mesh;
                    break;
                }
            }

            if (filter.sharedMesh == null)
            {
                throw new InvalidOperationException($"Missing armor mesh at {path}.");
            }

            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _shell;
            return renderer;
        }

        private LineRenderer AddLine(GameObject host, int positions)
        {
            var line = host.AddComponent<LineRenderer>();
            line.sharedMaterial = _trace;
            line.positionCount = positions;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }

        private Transform Block(Transform parent, string name, float x, float y, float z, float width, float height, float depth, Material material, bool collision = false)
        {
            return _art.Block(parent, name, x, y, z, width, height, depth, material, collision);
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Gravity Playground/{name}.mat");
        }

        private static void RegisterLayer()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(SecurityLayer);
            if (layer.stringValue.Length > 0 && layer.stringValue != "Security Enemy")
            {
                throw new InvalidOperationException($"Layer {SecurityLayer} is already assigned to {layer.stringValue}.");
            }

            layer.stringValue = "Security Enemy";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
