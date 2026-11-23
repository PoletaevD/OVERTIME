using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public static class TemporalPlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/Temporal Playground.unity";
        private const string ArtPath = "Assets/Art/Temporal Playground";

        [MenuItem("OVERTIME/Create Temporal Playground")]
        public static void Create()
        {
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                var loaded = existing.isLoaded ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(loaded);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gravity Player.prefab");
            var facilityProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Art/Gravity Playground/Facility Volume.asset");
            var font = PlaygroundSceneBuilder.TMPAsset();
            if (prefab == null || facilityProfile == null || font == null)
            {
                throw new InvalidOperationException("Temporal Playground requires the Gravity Player prefab, facility volume, and TMP font.");
            }

            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var art = new PlaygroundArt();
            var room = art.BuildTemporalRoom();
            PlaygroundSceneBuilder.AddLighting(art, facilityProfile);
            var builder = new Chamber(art, room);
            var director = PlaygroundSceneBuilder.Child("Temporal Director", room).gameObject.AddComponent<TemporalDirector>();
            var target = builder.BuildDestructible();
            var actors = new TemporalActor[] { builder.BuildMovingTarget(false), builder.BuildMovingTarget(true), builder.BuildDoor(), builder.BuildEmitter(), target };
            PlaygroundSceneBuilder.Wire(director, "_worldActors", actors, "_testTarget", target);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = new Vector3(0f, 0.89f, -7f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            var rig = player.GetComponent<GravityCamera>();
            PlaygroundSceneBuilder.Wire(player.GetComponent<PlaygroundPlayer>(), "_temporalDirector", director);
            var pulse = new PlaygroundHudBuilder(font).BuildTemporal(player.GetComponentInChildren<PlaygroundHud>(), director, rig);
            var volume = builder.BuildVolume();
            var effects = director.gameObject.AddComponent<TemporalEffects>();
            PlaygroundSceneBuilder.Wire(effects, "_director", director, "_cameraRig", rig, "_volume", volume, "_pulse", pulse);
            builder.ConfigureModeDemos(director, effects);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save Temporal Playground.");
            }

            var scenes = EditorBuildSettings.scenes;
            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }

        [MenuItem("OVERTIME/Update Temporal Playground")]
        public static void UpdateStage02B()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException("Open Temporal Playground before updating its demos.");
            }

            TemporalDirector director = null;
            TemporalHud hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                director ??= root.GetComponentInChildren<TemporalDirector>(true);
                hud ??= root.GetComponentInChildren<TemporalHud>(true);
            }

            var movementHud = hud.GetComponentInParent<PlaygroundHud>();
            var font = hud.transform.Find("Playback Status").GetComponent<TMP_Text>().font;
            new PlaygroundHudBuilder(font).ConfigureTemporalReadouts(movementHud, hud);
            var traceMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ArtPath}/Temporal Trace.mat");
            Chamber.ConfigureModeDemos(director, director.GetComponent<TemporalEffects>(), traceMaterial);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private sealed class Chamber
        {
            private readonly PlaygroundArt _art;
            private readonly Transform _room;
            private readonly Material _graphite;
            private readonly Material _white;
            private readonly Material _orange;
            private readonly Material _cyan;
            private readonly Material _trace;

            internal Chamber(PlaygroundArt art, Transform room)
            {
                _art = art;
                _room = room;
                _graphite = LoadMaterial("Graphite Alloy");
                _white = LoadMaterial("Porcelain Shell");
                _orange = LoadMaterial("Safety Orange");
                _cyan = LoadMaterial("Cyan Guide");

                var tracePath = $"{ArtPath}/Temporal Trace.mat";
                _trace = AssetDatabase.LoadAssetAtPath<Material>(tracePath);
                if (_trace == null)
                {
                    _trace = new Material(Shader.Find("Sprites/Default")) { name = "Temporal Trace" };
                    AssetDatabase.CreateAsset(_trace, tracePath);
                }
            }

            internal void ConfigureModeDemos(TemporalDirector director, TemporalEffects effects)
            {
                ConfigureModeDemos(director, effects, _trace);
            }

            internal static void ConfigureModeDemos(TemporalDirector director, TemporalEffects effects, Material traceMaterial)
            {
                if (director.transform.Find("Recall Path") != null)
                {
                    return;
                }

                var room = director.transform.parent;
                var trail = PlaygroundSceneBuilder.Child("Recall Path", director.transform);
                var trace = Trace(trail.gameObject, 0.042f, traceMaterial);
                var warm = TemporalEffects.ModeColor(TemporalMode.Recall);
                trace.startColor = new Color(warm.r, warm.g, warm.b, 0.8f);
                trace.endColor = new Color(warm.r, warm.g, warm.b, 0f);
                Undo.RegisterCreatedObjectUndo(trail.gameObject, "Add Recall Path Trace");
                Undo.RecordObject(effects, "Wire Recall Path Trace");
                PlaygroundSceneBuilder.Wire(effects, "_playerTrace", trace);

                SetDemoLabel(room, "Airlock 02A", "E  /  REWIND THE AIRLOCK", TemporalMode.Override);
                var projectileHint = SetDemoLabel(room, "Projectile Lane Emitter", "SHIFT  /  SLOW PROJECTILES", TemporalMode.BulletTime);
                Undo.RecordObject(projectileHint.transform, "Position Projectile Hint");
                projectileHint.transform.localPosition += Vector3.right * 0.7f;
                SetDemoLabel(room, "Destructible Test Target", "LMB  TEST SHOT  /  Q  RESTORE", TemporalMode.Reset);
                var recallHint = PlaygroundArt.Label(room, "C  /  RECALL YOUR WALL RUN", new Vector3(4.35f, 4.36f, 3.68f), 0.24f, warm);
                Undo.RegisterCreatedObjectUndo(recallHint.gameObject, "Add Recall Route Hint");
            }

            private static TMP_Text SetDemoLabel(Transform room, string demoObject, string text, TemporalMode mode)
            {
                var label = room.Find(demoObject).GetComponentInChildren<TMP_Text>();
                Undo.RecordObject(label, "Update Temporal Demo Hint");
                label.text = text;
                label.color = TemporalEffects.ModeColor(mode);
                return label;
            }

            internal TemporalMovingTarget BuildMovingTarget(bool vertical)
            {
                var root = Anchor(vertical ? "Vertical Track Target" : "Horizontal Track Target", new Vector3(vertical ? 4.5f : -4.1f, vertical ? 2.55f : 2f, 3.7f));
                var slider = PlaygroundSceneBuilder.Child("Moving Target", root);
                Block(root, "Rail", new Vector3(0f, 0f, 0.33f), vertical ? new Vector3(0.15f, 2.85f, 0.28f) : new Vector3(3.7f, 0.15f, 0.28f), _graphite);
                Block(slider, "Target Plate", Vector3.zero, new Vector3(0.78f, 0.78f, 0.16f), _white).Rotate(0f, 0f, 45f);
                Block(slider, "Cyan Core", Vector3.back * 0.11f, new Vector3(0.38f, 0.38f, 0.08f), _cyan).Rotate(0f, 0f, 45f);
                PlaygroundArt.Label(root, vertical ? "VERTICAL TRACK" : "HORIZONTAL TRACK", new Vector3(0f, vertical ? -1.72f : -0.71f, -0.05f), 0.24f, Color.white);

                var target = root.gameObject.AddComponent<TemporalMovingTarget>();
                PlaygroundSceneBuilder.Wire(target, "_visual", slider, "_trace", Trace(root.gameObject, 0.058f, _trace));
                var data = new SerializedObject(target);
                data.FindProperty("_travel").vector3Value = vertical ? Vector3.up * 1.05f : Vector3.right * 1.55f;
                data.FindProperty("_period").floatValue = vertical ? 2.65f : 3.6f;
                data.FindProperty("_startPhase").floatValue = vertical ? 0.13f : 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
                return target;
            }

            internal TemporalSlidingDoor BuildDoor()
            {
                var frame = Anchor("Airlock 02A", new Vector3(2.65f, 0f, 8.8f));
                Block(frame, "Left Jamb", new Vector3(-1.68f, 2.42f, 0f), new Vector3(0.4f, 4.84f, 0.85f), _graphite, true);
                Block(frame, "Right Jamb", new Vector3(1.68f, 2.42f, 0f), new Vector3(0.4f, 4.84f, 0.85f), _graphite, true);
                Block(frame, "Door Housing", new Vector3(0f, 3.87f, -0.34f), new Vector3(3.03f, 2.03f, 0.18f), _graphite, true);
                Block(frame, "Airlock Marking", new Vector3(0f, 2.9f, -0.45f), new Vector3(3.03f, 0.06f, 0.035f), _orange);
                PlaygroundArt.Label(frame, "AUTO AIRLOCK", new Vector3(0f, 3.24f, -0.46f), 0.26f, Color.white);
                var door = PlaygroundSceneBuilder.Child("Sliding Door", frame);
                Block(door, "Door Leaf", new Vector3(0f, 1.19f, 0f), new Vector3(2.94f, 2.36f, 0.24f), _white, true);
                Block(door, "Door Accent", new Vector3(0f, 0.35f, -0.13f), new Vector3(2.75f, 0.13f, 0.045f), _orange);
                Block(door, "Airlock Index", new Vector3(0f, 1.45f, -0.13f), new Vector3(0.09f, 0.64f, 0.045f), _cyan);
                return door.gameObject.AddComponent<TemporalSlidingDoor>();
            }

            internal TemporalDestructible BuildDestructible()
            {
                var root = Anchor("Destructible Test Target", new Vector3(0f, 1.46f, 3.2f));
                Block(root, "Mount Pedestal", new Vector3(0f, -1.06f, 0.32f), new Vector3(1.24f, 0.8f, 0.48f), _graphite);
                PlaygroundArt.Label(root, "FRANGIBLE  /  LMB TEST", new Vector3(0f, -0.96f, 0.038f), 0.21f, Color.white);
                var intact = Block(root, "Intact Target", Vector3.zero, new Vector3(1.26f, 1.15f, 0.22f), _orange, true);
                Block(root, "Reference Frame", Vector3.forward * 0.2f, new Vector3(1.43f, 1.32f, 0.12f), _graphite);
                var damaged = PlaygroundSceneBuilder.Child("Broken Target", root);
                Block(damaged, "Plate Fragment Left", new Vector3(-0.51f, -0.3f, -0.05f), new Vector3(0.30f, 0.45f, 0.15f), _orange).Rotate(0f, 0f, -26f);
                Block(damaged, "Plate Fragment Right", new Vector3(0.38f, -0.5f, -0.11f), new Vector3(0.35f, 0.23f, 0.14f), _orange).Rotate(0f, 0f, 17f);
                damaged.gameObject.SetActive(false);
                var target = root.gameObject.AddComponent<TemporalDestructible>();
                PlaygroundSceneBuilder.Wire(target, "_intact", intact.gameObject, "_damaged", damaged.gameObject, "_hitbox", intact.GetComponent<Collider>());
                return target;
            }

            internal TemporalProjectileEmitter BuildEmitter()
            {
                var root = Anchor("Projectile Lane Emitter", new Vector3(-5.75f, 1.18f, -0.35f));
                Block(root, "Emitter Plinth", new Vector3(0f, -0.58f, -0.5f), new Vector3(0.78f, 1.16f, 0.8f), _graphite);
                Block(root, "Emitter Casing", new Vector3(0f, 0f, -0.4f), new Vector3(0.86f, 0.47f, 0.58f), _white);
                Block(root, "Emitter Port", new Vector3(0f, 0f, -0.08f), new Vector3(0.28f, 0.28f, 0.10f), _orange);
                PlaygroundArt.Label(root, "PROJECTILE LANE", new Vector3(0f, 0.55f, -0.03f), 0.22f, Color.white);
                Block(_room, "Projectile Lane Marking", new Vector3(-5.75f, 0.016f, 5.4f), new Vector3(0.075f, 0.022f, 11.5f), _cyan);
                var slots = new Transform[8];
                for (var slot = 0; slot < slots.Length; slot++)
                {
                    var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    projectile.name = $"Projectile Slot {slot + 1}";
                    projectile.transform.SetParent(root, false);
                    projectile.transform.localScale = new Vector3(0.16f, 0.16f, 0.34f);
                    projectile.GetComponent<MeshRenderer>().sharedMaterial = _cyan;
                    Object.DestroyImmediate(projectile.GetComponent<SphereCollider>());
                    Trace(projectile, 0.035f, _trace);
                    projectile.SetActive(false);
                    slots[slot] = projectile.transform;
                }

                var emitter = root.gameObject.AddComponent<TemporalProjectileEmitter>();
                PlaygroundSceneBuilder.Wire(emitter, "_projectiles", slots);
                return emitter;
            }

            internal Volume BuildVolume()
            {
                var path = $"{ArtPath}/Reset Grade.asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                    var grade = profile.Add<ColorAdjustments>();
                    grade.colorFilter.Override(new Color(0.73f, 0.94f, 1f));
                    grade.saturation.Override(-13f);
                    AssetDatabase.AddObjectToAsset(grade, profile);
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }

                var volume = PlaygroundSceneBuilder.Child("RESET Color Grade", _room).gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.weight = 0f;
                volume.sharedProfile = profile;
                return volume;
            }

            private static LineRenderer Trace(GameObject host, float width, Material material)
            {
                var trace = host.AddComponent<LineRenderer>();
                trace.sharedMaterial = material;
                trace.useWorldSpace = true;
                trace.startWidth = width;
                trace.endWidth = width * 0.16f;
                trace.startColor = new Color(0.35f, 0.95f, 1f, 0.73f);
                trace.endColor = new Color(0.35f, 0.95f, 1f, 0f);
                trace.shadowCastingMode = ShadowCastingMode.Off;
                trace.receiveShadows = false;
                trace.positionCount = 0;
                trace.enabled = false;
                return trace;
            }

            private Transform Anchor(string name, Vector3 position)
            {
                var root = PlaygroundSceneBuilder.Child(name, _room);
                root.localPosition = position;
                return root;
            }

            private Transform Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = false)
            {
                return _art.Block(parent, name, position.x, position.y, position.z, size.x, size.y, size.z, material, collision);
            }

            private Material LoadMaterial(string name)
            {
                return AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Gravity Playground/{name}.mat");
            }
        }
    }
}
