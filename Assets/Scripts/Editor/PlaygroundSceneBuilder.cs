using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Overtime.Editor
{
    public static class PlaygroundSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gravity Playground.unity";
        private const int ViewmodelLayer = 30;

        [MenuItem("OVERTIME/Create Gravity Playground")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputAsset == null || TMPAsset() == null)
            {
                throw new InvalidOperationException("Gravity Playground needs the project Input Actions and TMP Essential Resources.");
            }

            Directory.CreateDirectory("Assets/Art/Gravity Playground");
            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var art = new PlaygroundArt();
            art.BuildCourse();
            AddLighting(art, CreateFacilityProfile());
            var weapon = art.BuildWeapon(ViewmodelLayer);
            CreatePlayer(inputAsset, weapon);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            var otherScenes = EditorBuildSettings.scenes.Where(item => item.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(otherScenes).ToArray();
        }

        internal static TMPro.TMP_FontAsset TMPAsset()
        {
            return AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        private static void CreatePlayer(InputActionAsset actions, GameObject weapon)
        {
            var player = new GameObject("Gravity Player");
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.37f;
            capsule.height = 1.7f;
            var motor = player.AddComponent<GravityMotor>();
            var input = player.AddComponent<PlaygroundInput>();
            var driver = player.AddComponent<PlaygroundPlayer>();
            var rig = player.AddComponent<GravityCamera>();
            Wire(input, "_actions", actions);

            var gravity = Child("Gravity Frame  |  World Up", player.transform);
            var yaw = Child("Yaw", gravity);
            var pitch = Child("Pitch", yaw);
            var effects = Child("Camera Effects", pitch);
            var target = Child("Camera Target", effects);
            var cm = Child("Cinemachine FPS", player.transform).gameObject.AddComponent<CinemachineCamera>();
            cm.Follow = target;
            cm.gameObject.AddComponent<CinemachineHardLockToTarget>();
            cm.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();

            var world = Child("Main Camera", player.transform).gameObject.AddComponent<Camera>();
            world.tag = "MainCamera";
            world.nearClipPlane = 0.045f;
            world.farClipPlane = 110f;
            world.cullingMask &= ~(1 << ViewmodelLayer);
            world.clearFlags = CameraClearFlags.SolidColor;
            world.backgroundColor = new Color(0.065f, 0.085f, 0.12f);
            world.gameObject.AddComponent<AudioListener>();
            var worldData = world.GetUniversalAdditionalCameraData();
            worldData.renderPostProcessing = true;
            var brain = world.gameObject.AddComponent<CinemachineBrain>();
            brain.WorldUpOverride = gravity;
            brain.IgnoreTimeScale = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;

            var view = Child("Weapon Camera", world.transform).gameObject.AddComponent<Camera>();
            view.nearClipPlane = 0.025f;
            view.farClipPlane = 4f;
            view.fieldOfView = 64f;
            view.cullingMask = 1 << ViewmodelLayer;
            var viewData = view.GetUniversalAdditionalCameraData();
            viewData.renderType = CameraRenderType.Overlay;
            viewData.renderShadows = false;
            worldData.cameraStack.Add(view);

            var weaponInstance = (GameObject)PrefabUtility.InstantiatePrefab(weapon);
            weaponInstance.transform.SetParent(view.transform, false);
            weaponInstance.transform.localPosition = new Vector3(0.31f, -0.25f, 0.46f);
            var presentation = weaponInstance.AddComponent<WeaponPresentation>();
            var movement = weaponInstance.transform.GetChild(0);
            var sway = movement.GetChild(0);
            var recoil = sway.GetChild(0);
            Wire(presentation, "_input", input, "_motor", motor, "_cameraRig", rig, "_movementPivot", movement, "_swayPivot", sway, "_recoilPivot", recoil, "_muzzleFlash", recoil.Find("Muzzle Flash"));

            Wire(rig, "_motor", motor, "_cinemachineCamera", cm, "_gravityFrame", gravity, "_yawPivot", yaw, "_pitchPivot", pitch, "_effectsPivot", effects);
            Wire(driver, "_input", input, "_motor", motor, "_cameraRig", rig);
            new PlaygroundHudBuilder(TMPAsset()).Build(player.transform, input, motor, rig);

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(player, "Assets/Prefabs/Gravity Player.prefab", InteractionMode.AutomatedAction);
            if (prefab == null)
            {
                throw new InvalidOperationException("Failed to save Gravity Player prefab.");
            }

            player.transform.position = new Vector3(0f, 0.89f, -8.2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        }

        internal static void AddLighting(PlaygroundArt art, VolumeProfile profile)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.63f, 0.69f, 0.77f);
            RenderSettings.ambientEquatorColor = new Color(0.37f, 0.43f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.29f, 0.34f);
            var light = Child("Facility Key", art.Root).gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.93f, 0.83f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            var volume = Child("URP Volume", art.Root).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static VolumeProfile CreateFacilityProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.19f);
            bloom.scatter.Override(0.55f);
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);
            const string path = "Assets/Art/Gravity Playground/Facility Volume.asset";
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
            return profile;
        }

        internal static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        internal static void Wire(UnityEngine.Object target, params object[] pairs)
        {
            var data = new SerializedObject(target);
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var property = data.FindProperty((string)pairs[i]);
                if (pairs[i + 1] is UnityEngine.Object[] values)
                {
                    property.arraySize = values.Length;
                    for (var index = 0; index < values.Length; index++)
                    {
                        property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
                    }
                }
                else
                {
                    property.objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
                }
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
