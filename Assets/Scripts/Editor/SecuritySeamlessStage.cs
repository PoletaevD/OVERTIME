using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private const string LootPrefabs = "Assets/Prefabs/Security Route/Loot";
        private const string WeaponMaterialPath = "Assets/Art/Gravity Playground";
        private const int KnifeLayer = 30;

        [MenuItem("OVERTIME/Apply Stage 05A.2 Security Route")]
        public static void ApplyStage052()
        {
            SaveRouteChanges(WireSeamlessStage);
        }

        private static void SaveRouteChanges(Action<Scene> apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before applying the Security Route update.");
            }

            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(RouteScenePath);
            var wasOpen = scene.isLoaded;
            if (wasOpen && scene.isDirty)
            {
                var backup = $"Temp/SecurityRoute-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.unity";
                if (!EditorSceneManager.SaveScene(scene, backup, true))
                {
                    throw new InvalidOperationException("Could not back up the open Security Route.");
                }
            }

            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(RouteScenePath, OpenSceneMode.Additive);
            }

            try
            {
                SceneManager.SetActiveScene(scene);
                apply(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("Could not save the Security Route scene.");
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if (previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void WireSeamlessStage(Scene scene)
        {
            SectorRoute route = null;
            PlaygroundPlayer player = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                route ??= root.GetComponentInChildren<SectorRoute>(true);
                player ??= root.GetComponentInChildren<PlaygroundPlayer>(true);
            }

            if (route == null || player == null)
            {
                throw new InvalidOperationException("The playable Security Route requires a sector controller and a player.");
            }

            EnsureFolder(RoutePrefabs, "Loot");
            var definitions = new WeaponAssets().LoadOrCreate();
            var models = new GameObject[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                ConfigureOptics(definition);
                models[index] = SaveLootModel(definition);
            }

            PrepareArenaAssets(route.DebugLibrary);
            var routeData = new SerializedObject(route);
            routeData.FindProperty("_seamless").boolValue = true;
            routeData.ApplyModifiedPropertiesWithoutUndo();
            var icons = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RouteArt}/MERIDIAN Reward Icons.png");
            PlaygroundSceneBuilder.Wire(route, "_rewardIcons", icons, "_pickupModels", models, "_ammoIcons", BuildAmmoIcons(), "_secondaryOptions", new[] { definitions[1], definitions[2], definitions[3] });
            var catalog = new RunUpgradeAssets().LoadOrCreate();
            PlaygroundSceneBuilder.Wire(route.Progression, "_catalog", catalog);
            var weapon = player.Combat.Weapon;
            var weaponData = new SerializedObject(weapon);
            weaponData.FindProperty("_primary").objectReferenceValue = definitions[0];
            weaponData.FindProperty("_projectileCapacity").intValue = 72;
            weaponData.ApplyModifiedPropertiesWithoutUndo();

            var view = player.GetComponentInChildren<WeaponPresentation>(true).transform.parent;
            var knife = SaveKnife();
            if (view.GetComponentInChildren<KnifePresentation>(true) == null)
            {
                PrefabUtility.InstantiatePrefab(knife, view);
            }

            var movement = player.GetComponentInChildren<PlaygroundHud>(true);
            movement.GetComponent<RouteSelectionHud>().enabled = false;
            movement.GetComponent<WeaponSelectionHud>().enabled = false;
            var scaler = movement.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var bindings = movement.transform.Find("Controls/Bindings").GetComponent<TMPro.TMP_Text>();
            bindings.text = "WASD  MOVE   SPACE  JUMP / MAGNET\nLMB  FIRE   RMB  AIM   F  KNIFE\nG  PICK UP   R  RELOAD   1 / 2  SWAP\nQ  RESET   E  OVERRIDE   C  RECALL   SHIFT  SLOW\nH  HINTS   F4  MOTION   F5  RETRY   TAB  BUILD";
            var hud = new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset());
            hud.ExtendRunInspection(movement);
            hud.BuildLootHud(movement);
            BindOpticalHud(movement);
        }

        private static void PrepareArenaAssets(RoomDefinition[] library)
        {
            foreach (var definition in library)
            {
                if (definition.Prefab.AmmoSockets.Length > 0)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(definition.Prefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AuthorAmmoSockets(contents.GetComponent<RoomRuntime>());
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static void AuthorAmmoSockets(RoomRuntime room)
        {
            var root = PlaygroundSceneBuilder.Child("Ammunition Sockets", room.transform);
            var zone = room.Encounter.CombatZone;
            var fixtures = room.GetComponentsInChildren<Collider>();
            var points = new List<Transform>(24);
            var floor = room.Entry.localPosition.y - room.EntryHeight;
            for (var row = 0; row < 6; row++)
            {
                var z = Mathf.Lerp(5f, room.Exit.localPosition.z - 3f, row / 5f);
                for (var column = 0; column < 4; column++)
                {
                    var x = zone.size.x * Mathf.Lerp(-0.36f, 0.36f, column / 3f);
                    var point = new Vector3(x, floor + 0.52f, z);
                    var standingRoom = new Bounds(room.transform.TransformPoint(point), new Vector3(1.25f, 0.83f, 1.25f));
                    var blocked = false;
                    foreach (var fixture in fixtures)
                    {
                        if (fixture.enabled && !fixture.isTrigger && fixture.bounds.Intersects(standingRoom))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                    {
                        continue;
                    }

                    var socket = PlaygroundSceneBuilder.Child($"Ammo {points.Count + 1:00}", root);
                    socket.localPosition = point;
                    points.Add(socket);
                }
            }

            if (points.Count < 5)
            {
                throw new InvalidOperationException($"{room.name} has only {points.Count} safe ammunition sockets.");
            }

            PlaygroundSceneBuilder.Wire(room, "_ammoSockets", points.ToArray());
        }

        private static Sprite[] BuildAmmoIcons()
        {
            var art = new RunUpgradeAssets();
            return new[]
            {
                art.CreateIcon("Assets/Art/Weapons/Carbine Cells.png", "5,8 10,8 10,24 5,24 5,8|13,8 18,8 18,24 13,24 13,8|21,8 26,8 26,24 21,24 21,8"),
                art.CreateIcon("Assets/Art/Weapons/Scatter Shells.png", "6,7 14,7 14,25 6,25 6,7|18,7 26,7 26,25 18,25 18,7|6,11 14,11|18,11 26,11"),
                art.CreateIcon("Assets/Art/Weapons/Rail Cells.png", "11,5 21,5 25,10 25,22 21,27 11,27 7,22 7,10 11,5|17,9 12,17 18,17 14,23"),
                art.CreateIcon("Assets/Art/Weapons/Pistol Cells.png", "6,9 11,7 16,9 16,24 6,24 6,9|17,9 22,7 27,9 27,24 17,24 17,9|6,13 16,13|17,13 27,13")
            };
        }

        private static GameObject SaveLootModel(WeaponDefinition definition)
        {
            var title = $"{definition.Id} World Pickup";
            var root = new GameObject(title);
            var model = new GameObject("World Model").transform;
            model.SetParent(root.transform, false);
            var name = definition.Id.ToString();
            var bounds = new Bounds();
            var found = false;
            foreach (var source in definition.Viewmodel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!source.enabled || !source.TryGetComponent<MeshFilter>(out var mesh) || UnderMuzzle(source.transform))
                {
                    continue;
                }

                var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(model, false);
                part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                part.transform.localScale = source.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            var length = definition.Type == WeaponType.SemiAutomatic ? 0.70f : definition.Type == WeaponType.Charged ? 1.35f : 1.02f;
            var scale = length / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.01f);
            model.localScale = Vector3.one * scale;
            model.localPosition = -bounds.center * scale;
            var asset = PrefabUtility.SaveAsPrefabAsset(root, $"{LootPrefabs}/{name} World Pickup.prefab");
            Object.DestroyImmediate(root);
            return asset;
        }

        private static bool UnderMuzzle(Transform node)
        {
            while (node != null)
            {
                if (node.name == "Muzzle Flash")
                {
                    return true;
                }

                node = node.parent;
            }

            return false;
        }

        private static GameObject SaveKnife()
        {
            var root = new GameObject("MERIDIAN Knife Presentation");
            var handle = PlaygroundSceneBuilder.Child("Knife Swing Pivot", root.transform);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Weapons/MERIDIAN Utility Knife.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(imported, handle);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var surface = renderer.name switch
                {
                    "Blade" => "Porcelain Shell",
                    "Emitter" => "Cyan Guide",
                    "Grip" => "Ceramic Panel",
                    _ => "Graphite Alloy"
                };
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{WeaponMaterialPath}/{surface}.mat");
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            var pivot = PlaygroundSceneBuilder.Child("Blade Tip", handle);
            pivot.localPosition = new Vector3(0f, -0.01f, 0.238f);
            var trail = pivot.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.105f;
            trail.widthMultiplier = 0.095f;
            trail.minVertexDistance = 0.009f;
            trail.startColor = new Color(0.43f, 0.98f, 1f, 0.86f);
            trail.endColor = new Color(0.25f, 0.76f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/ARC-01 Bolt.mat");
            trail.emitting = false;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = KnifeLayer;
            }

            var presentation = root.AddComponent<KnifePresentation>();
            PlaygroundSceneBuilder.Wire(presentation, "_blade", handle, "_trail", trail);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, $"{LootPrefabs}/MERIDIAN Knife Presentation.prefab");
            Object.DestroyImmediate(root);
            return asset;
        }
    }
}
