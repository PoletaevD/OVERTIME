using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        [MenuItem("OVERTIME/Update Stage 05A.1 Security Route Scene")]
        public static void UpdateSecurityRouteScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before updating Security Route.");
            }

            var previous = SceneManager.GetActiveScene();
            var routeScene = SceneManager.GetSceneByPath(RouteScenePath);
            var wasOpen = routeScene.isLoaded;
            if (wasOpen && routeScene.isDirty)
            {
                throw new InvalidOperationException("Save the open Security Route scene before applying the stage update.");
            }

            if (!wasOpen)
            {
                routeScene = EditorSceneManager.OpenScene(RouteScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var rooms = AssetDatabase.FindAssets("t:RoomDefinition", new[] { $"{RoutePrefabs}/Rooms" });
                var library = new RoomDefinition[rooms.Length];
                for (var index = 0; index < rooms.Length; index++)
                {
                    library[index] = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(rooms[index]));
                }

                if (library.Length != 16)
                {
                    throw new InvalidOperationException("Build the 16 Security Route room assets first.");
                }

                SceneManager.SetActiveScene(routeScene);
                UpdateRouteScene(routeScene, library);
                EditorSceneManager.MarkSceneDirty(routeScene);
                if (!EditorSceneManager.SaveScene(routeScene))
                {
                    throw new InvalidOperationException("Could not save Security Route.");
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(routeScene, true);
                }

                if (previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        public static void CreateStagePreview()
        {
            const string legacyScenePath = "Assets/Scenes/Security Route.unity";
            if (System.IO.File.Exists(RouteScenePath))
            {
                throw new InvalidOperationException("The preview scene already exists.");
            }

            var source = SceneManager.GetSceneByPath(legacyScenePath);
            if (!source.isLoaded)
            {
                source = EditorSceneManager.OpenScene(legacyScenePath, OpenSceneMode.Additive);
            }

            if (!EditorSceneManager.SaveScene(source, RouteScenePath, true))
            {
                throw new InvalidOperationException("Could not create a copy of Security Route.");
            }

            var preview = EditorSceneManager.OpenScene(RouteScenePath, OpenSceneMode.Additive);
            var rooms = AssetDatabase.FindAssets("t:RoomDefinition", new[] { $"{RoutePrefabs}/Rooms" });
            var library = new RoomDefinition[rooms.Length];
            for (var index = 0; index < rooms.Length; index++)
            {
                library[index] = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(rooms[index]));
            }

            SceneManager.SetActiveScene(preview);
            UpdateRouteScene(preview, library);
            EditorSceneManager.SaveScene(preview);
            AssetDatabase.SaveAssets();
            EditorSceneManager.CloseScene(preview, true);
            SceneManager.SetActiveScene(source);
        }

        public static void BuildStageAssets()
        {
            BuildSecurityRouteRooms();
            new WeaponAssets().LoadOrCreate();
        }

        [MenuItem("OVERTIME/Build Stage 05A.1 Room Assets")]
        public static void BuildSecurityRouteRooms()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before building rooms.");
            }

            var previous = SceneManager.GetActiveScene();
            var isolated = Application.isBatchMode && string.IsNullOrEmpty(previous.path);
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, isolated ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            try
            {
                new SecuritySectorBuilder().BuildLibrary();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!isolated)
                {
                    EditorSceneManager.CloseScene(scratch, true);
                }

                if (previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void UpdateRouteScene(Scene scene, RoomDefinition[] library)
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
                throw new InvalidOperationException("Security Route is missing its player or sector controller.");
            }

            var director = route.GetComponent<TemporalDirector>();
            PlaygroundSceneBuilder.Wire(route, "_library", library);
            ExpandProjectilePool(route.EnemyProjectiles);

            var definitions = new WeaponAssets().LoadOrCreate();
            var options = new[] { definitions[1], definitions[2], definitions[3] };
            PlaygroundSceneBuilder.Wire(route, "_secondaryOptions", options);
            var presentation = player.GetComponentInChildren<WeaponPresentation>(true);
            var recoil = presentation.transform.Find("Movement Pivot/Sway Pivot/Recoil Pivot");
            var models = new WeaponViewmodel[options.Length];
            for (var index = 0; index < options.Length; index++)
            {
                var existing = recoil.Find(options[index].Viewmodel.name);
                var instance = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(options[index].Viewmodel, recoil);
                instance.SetActive(false);
                models[index] = instance.GetComponent<WeaponViewmodel>();
            }

            PlaygroundSceneBuilder.Wire(presentation, "_alternatives", models);
            PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            var hud = new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset());
            var movement = player.GetComponentInChildren<PlaygroundHud>(true);
            var rootHud = movement.transform;
            var secondaryHud = rootHud.GetComponent<WeaponSelectionHud>();
            var routeHud = rootHud.GetComponent<RouteSelectionHud>();
            var transfer = route.GetComponent<RouteTransfer>();
            var archive = rootHud.Find("Previous HUD Authoring");
            if (archive == null)
            {
                archive = PlaygroundSceneBuilder.Child("Previous HUD Authoring", rootHud);
                archive.gameObject.SetActive(false);
                ArchivePanel(rootHud, archive, "Secondary Weapon Selection");
                ArchivePanel(rootHud, archive, "Route Selection");
                ArchivePanel(rootHud, archive, "Airlock Blackout");
                ArchivePanel(rootHud, archive, "Directional Damage");
                hud.BuildSecondarySelection(rootHud, route, director, options, secondaryHud);
                hud.BuildRouteHud(movement, route, director, routeHud, transfer);
            }

            hud.InstallDamageArcs(rootHud, rootHud.GetComponent<CombatHud>());
        }

        private static void ExpandProjectilePool(TemporalProjectileEmitter pool)
        {
            var data = new SerializedObject(pool);
            var entries = data.FindProperty("_projectiles");
            var existingCount = entries.arraySize;
            const int capacity = 128;
            if (existingCount >= capacity)
            {
                return;
            }

            var prototype = (Transform)entries.GetArrayElementAtIndex(0).objectReferenceValue;
            entries.arraySize = capacity;
            for (var index = existingCount; index < capacity; index++)
            {
                var clone = Object.Instantiate(prototype, pool.transform, false);
                clone.name = $"Bolt {index + 1:00}";
                clone.gameObject.SetActive(false);
                entries.GetArrayElementAtIndex(index).objectReferenceValue = clone;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ArchivePanel(Transform hud, Transform archive, string name)
        {
            var panel = hud.Find(name);
            if (panel != null)
            {
                panel.SetParent(archive, true);
            }
        }

    }
}
