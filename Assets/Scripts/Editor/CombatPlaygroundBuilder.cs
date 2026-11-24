using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public static class CombatPlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/Combat Playground.unity";
        private const string GravityArt = "Assets/Art/Gravity Playground";
        private const string TemporalArt = "Assets/Art/Temporal Playground";
        private const int IgnoreRaycastLayer = 2;

        [MenuItem("OVERTIME/Create Combat Playground")]
        public static void Create()
        {
            Create(ScenePath, false);
        }

        internal static void Create(string path, bool security)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                var existing = SceneManager.GetSceneByPath(path);
                SceneManager.SetActiveScene(existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var art = new PlaygroundArt();
            var room = art.BuildTemporalRoom(security ? "SECURITY DIVISION  /  LIVE TRIAL" : "COMBAT EVALUATION");
            art.Root.name = security ? "MERIDIAN | Security Evaluation Facility" : "MERIDIAN | Combat Evaluation Facility";
            room.name = security ? "Security Evaluation Chamber" : "Combat Evaluation Chamber";
            PlaygroundSceneBuilder.AddLighting(art, AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{GravityArt}/Facility Volume.asset"));
            var chamber = new Chamber(art, room);
            var control = PlaygroundSceneBuilder.Child("Combat Timeline", room).gameObject;
            var director = control.AddComponent<TemporalDirector>();
            var energy = control.AddComponent<ChronoEnergy>();
            var station = chamber.BuildStation();
            var encounter = security ? new SecurityPlaygroundBuilder(art, room) : null;
            var actors = security ? encounter.Build(director) : new TemporalActor[]
            {
                chamber.BuildDummy("Training Dummy 01", 1, new Vector3(0f, 1.52f, 3.3f)),
                chamber.BuildDummy("Training Dummy 02", 2, new Vector3(-2.65f, 1.52f, 7.4f)),
                chamber.BuildDummy("Ceiling Training Dummy", 3, new Vector3(1f, 5.48f, 10.8f), ceiling: true),
                chamber.BuildDummy("Armored Dummy", 4, new Vector3(3.5f, 1.52f, 7.4f), armored: true),
                chamber.BuildEmitter(director)
            };
            PlaygroundSceneBuilder.Wire(director, "_energy", energy, "_rechargeStation", station, "_worldActors", actors);
            if (!security)
            {
                chamber.BuildRoute();
            }

            var player = BuildPlayer(scene, director, new Vector3(0f, 0.89f, security ? -8.2f : -7f));
            var playerObject = player.gameObject;
            var rig = player.GetComponent<GravityCamera>();
            chamber.BuildImpact(director, player);

            var hud = new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset());
            var movement = playerObject.GetComponentInChildren<PlaygroundHud>();
            var pulse = hud.BuildTemporal(movement, director, rig);
            var combatHud = hud.BuildCombat(movement, director, player);
            if (security)
            {
                hud.BuildSecurity(movement, combatHud, encounter.Enemies, playerObject.GetComponentInChildren<Camera>());
            }
            chamber.BuildTemporalEffects(director, rig, pulse);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Failed to save {path}.");
            }

            RegisterScene(path);
        }

        internal static PlaygroundPlayer BuildPlayer(Scene scene, TemporalDirector director, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gravity Player.prefab");
            var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            playerObject.layer = IgnoreRaycastLayer;
            playerObject.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerObject.transform);
            var player = playerObject.GetComponent<PlaygroundPlayer>();
            var health = playerObject.AddComponent<CombatHealth>();
            var weapon = playerObject.AddComponent<CombatWeapon>();
            var combat = playerObject.AddComponent<PlayerCombat>();
            PlaygroundSceneBuilder.Wire(combat, "_health", health, "_weapon", weapon);
            PlaygroundSceneBuilder.Wire(player, "_combat", combat, "_temporalDirector", director);
            return player;
        }

        internal static void RegisterScene(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            if (!Array.Exists(scenes, entry => entry.path == path))
            {
                Array.Resize(ref scenes, scenes.Length + 1);
                scenes[^1] = new EditorBuildSettingsScene(path, true);
                EditorBuildSettings.scenes = scenes;
                var settings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/EditorBuildSettings.asset");
                if (settings != null)
                {
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
        }

        internal sealed class Chamber
        {
            private readonly PlaygroundArt _art;
            private readonly Transform _room;
            private readonly Material _graphite = Load("Graphite Alloy");
            private readonly Material _shell = Load("Porcelain Shell");
            private readonly Material _cyan = Load("Cyan Guide");
            private readonly Material _orange = Load("Safety Orange");
            private readonly Material _violet = Load("Violet Guide");
            private readonly Material _trace = AssetDatabase.LoadAssetAtPath<Material>($"{TemporalArt}/Temporal Trace.mat");

            internal Chamber(PlaygroundArt art, Transform room)
            {
                _art = art;
                _room = room;
            }

            internal CombatTarget BuildDummy(string name, int id, Vector3 position, bool armored = false, bool ceiling = false)
            {
                var root = Place(name, position);
                if (ceiling)
                {
                    root.localRotation = Quaternion.Euler(0f, 0f, 180f);
                }

                var intact = PlaygroundSceneBuilder.Child("Live Target", root);
                var wreck = PlaygroundSceneBuilder.Child("Wreck", root);
                var body = Block(intact, "Hit Plate", 0f, 0f, 0f, armored ? 1.18f : 0.92f, 1.14f, 0.22f, armored ? _graphite : _shell, true);
                Block(intact, "Shoulder L", -0.56f, 0.32f, 0.02f, 0.30f, 0.29f, 0.23f, _graphite);
                Block(intact, "Shoulder R", 0.56f, 0.32f, 0.02f, 0.30f, 0.29f, 0.23f, _graphite);
                Block(intact, "Receiver Stripe", 0f, -0.29f, -0.12f, 0.62f, 0.052f, 0.028f, armored ? _shell : _cyan);
                Block(intact, "Receiver Stripe", 0f, -0.41f, -0.12f, 0.48f, 0.033f, 0.028f, armored ? _shell : _cyan);
                var weak = armored ? Block(intact, "Weak Point", 0f, 0.18f, -0.19f, 0.31f, 0.31f, 0.14f, _orange, true) : null;
                if (weak != null)
                {
                    weak.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }

                Block(root, "Plinth", 0f, -1.34f, 0.20f, 1.48f, 0.34f, 0.91f, _graphite, true);
                Block(root, "Support", 0f, -0.84f, 0.18f, 0.29f, 0.74f, 0.29f, _graphite);
                Block(wreck, "Shattered Plate L", -0.35f, -0.45f, 0.04f, 0.43f, 0.48f, 0.16f, _graphite).localRotation = Quaternion.Euler(0f, 0f, -24f);
                Block(wreck, "Shattered Plate R", 0.31f, -0.57f, -0.04f, 0.36f, 0.27f, 0.16f, _graphite).localRotation = Quaternion.Euler(0f, 0f, 28f);
                Block(wreck, "Fault Indicator", 0f, -0.43f, -0.14f, 0.14f, 0.061f, 0.052f, _orange);
                wreck.gameObject.SetActive(false);

                var label = armored ? "ARMORED / CORE x2" : $"TRAINING / {id:00}";
                PlaygroundArt.Label(root, label, new Vector3(0f, 0.86f, -0.15f), 0.23f, new Color(0.87f, 0.95f, 0.98f));
                var health = root.gameObject.AddComponent<CombatHealth>();
                SetInt(health, "_maximum", armored ? 160 : 60);
                var target = root.gameObject.AddComponent<CombatTarget>();
                SetInt(target, "_rewardId", id);
                PlaygroundSceneBuilder.Wire(target, "_health", health, "_intact", intact.gameObject, "_wreck", wreck.gameObject, "_body", body.GetComponent<Collider>(), "_weakPoint", weak != null ? weak.GetComponent<Collider>() : null, "_surface", body.GetComponent<Renderer>());
                return target;
            }

            internal Transform BuildStation()
            {
                var station = Place("Chrono Recharge Station", new Vector3(3.60f, 0.90f, -6.3f));
                Block(station, "Base", 0f, -0.70f, 0f, 1.30f, 0.38f, 0.97f, _graphite, true);
                Block(station, "Charging Cell", 0f, 0f, 0f, 0.77f, 1.05f, 0.58f, _graphite, true);
                Block(station, "Working Cell", 0f, 0.02f, -0.31f, 0.45f, 0.71f, 0.085f, _cyan);
                Block(station, "Cell Divider", 0f, 0.02f, -0.37f, 0.51f, 0.061f, 0.055f, _shell);
                Block(station, "Left Rail", -0.43f, 0.02f, -0.14f, 0.06f, 1.14f, 0.32f, _shell);
                Block(station, "Right Rail", 0.43f, 0.02f, -0.14f, 0.06f, 1.14f, 0.32f, _shell);
                PlaygroundArt.Label(station, "CHRONO SERVICE / F", new Vector3(0f, 0.90f, -0.30f), 0.25f, Color.white);
                return station;
            }

            internal TemporalProjectileEmitter BuildEmitter(TemporalDirector director)
            {
                const float laneX = -5f;
                var root = Place("Projectile Lane Emitter", new Vector3(laneX, 1f, 11.3f));
                root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Block(root, "Emitter Plinth", 0f, -0.52f, -0.76f, 1.00f, 0.96f, 0.94f, _graphite, true);
                Block(root, "Emitter Housing", 0f, 0f, -0.48f, 0.89f, 0.54f, 0.79f, _shell);
                Block(root, "Emitter Port", 0f, 0f, -0.05f, 0.28f, 0.28f, 0.13f, _orange);
                PlaygroundArt.Label(_room, "LIVE FIRE LANE", new Vector3(laneX, 1.76f, 12f), 0.28f, new Color(1f, 0.66f, 0.40f));
                for (var index = 0; index < 10; index++)
                {
                    Block(_room, "Live Fire Lane Stripe", laneX, 0.014f, -8f + index * 2f, 0.92f, 0.024f, 0.12f, _orange);
                }

                var slots = new Transform[6];
                for (var index = 0; index < slots.Length; index++)
                {
                    slots[index] = Sphere(root, $"Projectile Slot {index + 1}", _orange, new Vector3(0.26f, 0.26f, 0.34f));
                    AddTrace(slots[index], 0.035f);
                    slots[index].gameObject.SetActive(false);
                }

                var emitter = root.gameObject.AddComponent<TemporalProjectileEmitter>();
                PlaygroundSceneBuilder.Wire(emitter, "_combatDirector", director, "_projectiles", slots);
                var data = new SerializedObject(emitter);
                data.FindProperty("_speed").floatValue = 9f;
                data.FindProperty("_lifetime").floatValue = 2.6f;
                data.FindProperty("_interval").floatValue = 0.72f;
                data.ApplyModifiedPropertiesWithoutUndo();
                return emitter;
            }

            internal void BuildRoute()
            {
                for (var index = 0; index < 4; index++)
                {
                    var z = -4.6f + index * 4.6f;
                    Block(_room, "Wall Transit / Edge Guide", 6.973f, 2.5f, z, 0.035f, 4.1f, 0.08f, _violet);
                    Block(_room, "Overhead Route", 4.1f, 6.978f, z, 5.6f, 0.035f, 0.10f, _violet);
                }

                Block(_room, "Overhead Firing Position", 1f, 6.974f, 6.4f, 2.4f, 0.036f, 0.24f, _cyan);
                PlaygroundArt.Label(_room, "WALL / CEILING ROUTE", new Vector3(4.8f, 3.62f, 12.9f), 0.29f, new Color(0.80f, 0.67f, 1f));
            }

            internal void BuildImpact(TemporalDirector director, PlaygroundPlayer player)
            {
                var impacts = Place("Impact Pool", Vector3.zero);
                var renderers = new Renderer[4];
                for (var index = 0; index < renderers.Length; index++)
                {
                    renderers[index] = Sphere(impacts, $"Impact {index + 1}", _cyan, Vector3.one).GetComponent<Renderer>();
                    renderers[index].enabled = false;
                }

                var effect = impacts.gameObject.AddComponent<CombatImpact>();
                PlaygroundSceneBuilder.Wire(effect, "_player", player, "_director", director, "_sparks", renderers);
            }

            internal void BuildTemporalEffects(TemporalDirector director, GravityCamera rig, CanvasGroup pulse)
            {
                var volume = Place("Temporal Grade", Vector3.zero).gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.weight = 0f;
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{TemporalArt}/Reset Grade.asset");
                var trace = AddTrace(Place("Recall Path", Vector3.zero), 0.042f);
                trace.startColor = TemporalEffects.ModeColor(TemporalMode.Recall);
                trace.endColor = new Color(1f, 0.62f, 0.32f, 0f);
                var effects = director.gameObject.AddComponent<TemporalEffects>();
                PlaygroundSceneBuilder.Wire(effects, "_director", director, "_cameraRig", rig, "_pulse", pulse, "_volume", volume, "_playerTrace", trace);
            }

            private Transform Place(string name, Vector3 position)
            {
                var root = PlaygroundSceneBuilder.Child(name, _room);
                root.localPosition = position;
                return root;
            }

            private Transform Block(Transform parent, string name, float x, float y, float z, float width, float height, float depth, Material material, bool collision = false)
            {
                return _art.Block(parent, name, x, y, z, width, height, depth, material, collision);
            }

            private LineRenderer AddTrace(Transform parent, float width)
            {
                var renderer = parent.gameObject.AddComponent<LineRenderer>();
                renderer.sharedMaterial = _trace;
                renderer.useWorldSpace = true;
                renderer.startWidth = width;
                renderer.endWidth = width * 0.16f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.positionCount = 0;
                renderer.enabled = false;
                return renderer;
            }

            private static Transform Sphere(Transform parent, string name, Material material, Vector3 scale)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = name;
                sphere.transform.SetParent(parent, false);
                sphere.transform.localScale = scale;
                sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                return sphere.transform;
            }

            private static void SetInt(Object target, string field, int value)
            {
                var data = new SerializedObject(target);
                data.FindProperty(field).intValue = value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            private static Material Load(string name)
            {
                return AssetDatabase.LoadAssetAtPath<Material>($"{GravityArt}/{name}.mat");
            }
        }
    }
}
