using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private const string ScopeMaterial = "Assets/Resources/Deadline Scope.mat";
        private const string SightsRoot = "MERIDIAN Sight Assembly";

        [MenuItem("OVERTIME/Calibrate Weapon ADS")]
        public static void ApplyWeaponOptics()
        {
            SaveRouteChanges(UpdateOptics);
        }

        private static void UpdateOptics(Scene scene)
        {
            foreach (var definition in new WeaponAssets().LoadOrCreate())
            {
                ConfigureOptics(definition);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<PlaygroundHud>(true) is { } hud)
                {
                    BindOpticalHud(hud);
                    return;
                }
            }

            throw new InvalidOperationException("The Security Route scene has no player HUD.");
        }

        private static void BindOpticalHud(PlaygroundHud hud)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ScopeMaterial);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Weapons/Deadline Scope.shader");
                if (shader == null)
                {
                    throw new InvalidOperationException("The Deadline optics shader must be imported first.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ScopeMaterial);
            }

            var scope = hud.GetComponentInChildren<DeadlineScope>(true);
            if (scope == null)
            {
                scope = DeadlineScope.Create(hud.transform);
            }

            PlaygroundSceneBuilder.Wire(scope, "_glass", scope.GetComponent<RawImage>());
            PlaygroundSceneBuilder.Wire(hud, "_deadlineScope", scope);
        }

        private static void ConfigureOptics(WeaponDefinition weapon)
        {
            var (front, rear, eyeRelief, fov) = weapon.Id switch
            {
                WeaponId.Directive => (new Vector3(0f, 0.0715f, 0.261f), new Vector3(0f, 0.0710f, -0.061f), 0.29f, 62f),
                WeaponId.Termination => (new Vector3(0f, 0.096f, 0.373f), new Vector3(0f, 0.094f, -0.102f), 0.23f, 63f),
                WeaponId.Deadline => (new Vector3(0f, 0.107f, 0.127f), new Vector3(0f, 0.107f, -0.048f), 0.083f, 34f),
                _ => (new Vector3(0f, 0.132f, 0.361f), new Vector3(0f, 0.131f, 0.033f), 0.23f, 55f)
            };

            SaveSightGeometry(weapon, front, rear);
            var data = new SerializedObject(weapon);
            var aim = data.FindProperty("_aim");
            aim.FindPropertyRelative("_frontSight").vector3Value = front;
            aim.FindPropertyRelative("_rearSight").vector3Value = rear;
            aim.FindPropertyRelative("_eyeRelief").floatValue = eyeRelief;
            aim.FindPropertyRelative("_fieldOfView").floatValue = fov;
            aim.FindPropertyRelative("_transition").floatValue = weapon.Id == WeaponId.Deadline ? 0.25f : 0.19f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveSightGeometry(WeaponDefinition weapon, Vector3 front, Vector3 rear)
        {
            var path = AssetDatabase.GetAssetPath(weapon.Viewmodel);
            if (FindChild(weapon.Viewmodel.transform, SightsRoot) != null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var root = contents.transform;
                var graphite = AssetDatabase.LoadAssetAtPath<Material>($"{WeaponMaterialPath}/Graphite Alloy.mat");
                var emit = AssetDatabase.LoadAssetAtPath<Material>($"{WeaponMaterialPath}/Cyan Guide.mat");
                if (weapon.Id == WeaponId.Deadline)
                {
                    BuildScopeHousing(root, rear, graphite, emit);
                }
                else
                {
                    BuildIronSights(root, weapon.Id, front, rear, graphite, emit);
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new InvalidOperationException($"Could not save calibrated sights to {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void BuildIronSights(Transform root, WeaponId id, Vector3 front, Vector3 rear, Material graphite, Material emit)
        {
            var rearName = id == WeaponId.Directive ? "Slide Rear Sight" : id == WeaponId.Termination ? "Rear Sight Alloy" : "Rear Sight";
            var old = FindChild(root, rearName);
            if (old == null)
            {
                throw new InvalidOperationException($"Missing rear sight source {rearName} in {root.name}.");
            }

            old.GetComponent<Renderer>().enabled = false;
            var assembly = PlaygroundSceneBuilder.Child(SightsRoot, old.parent);
            assembly.gameObject.layer = old.gameObject.layer;
            var width = id == WeaponId.Compliance ? 0.084f : 0.070f;
            var notch = id == WeaponId.Directive ? 0.036f : 0.032f;
            var ear = (width - notch) * 0.5f;
            var depth = id == WeaponId.Compliance ? 0.032f : 0.027f;
            var deck = new Vector3(0f, rear.y - 0.023f, rear.z + depth * 0.5f);
            Geometry(root, assembly, "Sight base", PrimitiveType.Cube, deck, new Vector3(width, 0.013f, depth), Quaternion.identity, graphite);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * ((notch + ear) * 0.5f);
                var post = new Vector3(x, rear.y - 0.003f, deck.z);
                Geometry(root, assembly, side < 0 ? "Left sight ear" : "Right sight ear", PrimitiveType.Cube, post, new Vector3(ear, 0.030f, depth), Quaternion.identity, graphite);
            }

            var frontName = id == WeaponId.Directive ? "Slide Front Sight" : id == WeaponId.Termination ? "Front Sight Alloy" : "Front Sight";
            var mount = FindChild(root, frontName);
            if (mount == null)
            {
                throw new InvalidOperationException($"Missing front sight source {frontName} in {root.name}.");
            }

            var bead = front;
            bead.z -= 0.005f;
            Geometry(root, mount.parent, "Front sight insert", PrimitiveType.Cube, bead, new Vector3(0.0075f, 0.0048f, 0.009f), Quaternion.identity, emit);
        }

        private static void BuildScopeHousing(Transform root, Vector3 rear, Material graphite, Material emit)
        {
            var old = FindChild(root, "Optic Glass Violet");
            var lens = old.GetComponent<Renderer>();
            lens.enabled = false;
            var assembly = PlaygroundSceneBuilder.Child(SightsRoot, old.parent);
            assembly.gameObject.layer = old.gameObject.layer;
            var axis = Quaternion.Euler(90f, 0f, 0f);
            Geometry(root, assembly, "Optic tube", PrimitiveType.Cylinder, new Vector3(0f, rear.y, 0.048f), new Vector3(0.057f, 0.067f, 0.057f), axis, graphite);
            Geometry(root, assembly, "Objective bell", PrimitiveType.Cylinder, new Vector3(0f, rear.y, 0.116f), new Vector3(0.084f, 0.015f, 0.084f), axis, graphite);
            Geometry(root, assembly, "Eyepiece", PrimitiveType.Cylinder, rear + Vector3.forward * 0.016f, new Vector3(0.085f, 0.016f, 0.085f), axis, graphite);
            Geometry(root, assembly, "Rear optical glass", PrimitiveType.Cylinder, rear - Vector3.forward * 0.001f, new Vector3(0.064f, 0.001f, 0.064f), axis, lens.sharedMaterial);
            Geometry(root, assembly, "Lens guide", PrimitiveType.Cylinder, rear + Vector3.forward * 0.038f, new Vector3(0.079f, 0.002f, 0.079f), axis, emit);
            Geometry(root, assembly, "Elevation dial", PrimitiveType.Cylinder, new Vector3(0f, rear.y + 0.032f, 0.046f), new Vector3(0.031f, 0.010f, 0.031f), Quaternion.identity, graphite);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == name)
                {
                    return node;
                }
            }

            return null;
        }

        private static void Geometry(Transform root, Transform parent, string title, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = title;
            part.layer = parent.gameObject.layer;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetPositionAndRotation(root.TransformPoint(position), root.rotation * rotation);
            part.transform.localScale = scale;
            part.transform.SetParent(parent, true);
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
        }
    }
}
