using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private const string RouteScenePath = "Assets/Scenes/Security Route Stage 05A.1.unity";
        private const string RoutePrefabs = "Assets/Prefabs/Security Route";
        private const string RouteArt = "Assets/Art/Security Route";

        private GameObject _airlock;
        private Mesh _cassette;
        private Material _poster;

        [MenuItem("OVERTIME/Build Security Route")]
        public static void BuildSecurityRoute()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetSceneByPath(RouteScenePath).isLoaded)
            {
                throw new InvalidOperationException("Close Security Route and exit Play Mode before rebuilding its assets.");
            }

            EnsureFolder("Assets/Prefabs", "Security Route");
            EnsureFolder("Assets/Art", "Security Route");
            EnsureFolder(RoutePrefabs, "Rooms");
            EnsureFolder(RouteArt, "Surveys");

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var builder = new SecuritySectorBuilder();
                var library = builder.BuildLibrary();
                builder.Build(scene, library);
                if (!EditorSceneManager.SaveScene(scene, RouteScenePath))
                {
                    throw new InvalidOperationException($"Failed to save {RouteScenePath}.");
                }

                CombatPlaygroundBuilder.RegisterScene(RouteScenePath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        private RoomDefinition[] BuildLibrary()
        {
            _cassette = AssetDatabase.LoadAssetAtPath<GameObject>($"{RouteArt}/Meridian Service Cassette.obj")?.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            _poster = PosterMaterial();
            _airlock = BuildTransferPrefab();
            var rooms = new[]
            {
                BuildAssignment("checkpoint", "Security Checkpoint", "Credential lanes and columns provide a protected first approach.", RoomCategory.Entry, RoomGeometry.CloseQuarters | RoomGeometry.CoverHeavy, 1, 1, 1, new Vector3(18f, 7.4f, 24f), _cyan, CheckpointGeometry, CheckpointMarkers(), new[] { Variant("Credential Patrol", 1, 0, 1, 2), Variant("Secondary Screening", 1, 3, 4, 2) }, RoomReward.ChronoCache, RoomReward.MedicalSupplies),
                BuildAssignment("shaft", "Surveillance Shaft", "Elevated catwalks divide a tall, open flight volume.", RoomCategory.Operations, RoomGeometry.Vertical | RoomGeometry.DroneHeavy, 2, 4, 3, new Vector3(16f, 13f, 24f), _violet, SurveillanceGeometry, ShaftMarkers(), new[] { Variant("Shaft Sweep", 2, 0, 1, 3), Variant("Overwatch Grid", 3, 0, 1, 2, 3), Variant("Lockdown Circuit", 4, 0, 1, 2, 3, 4) }, RoomReward.ChronoCache, RoomReward.AmmunitionCache, RoomReward.PriorityProtocol),
                BuildAssignment("containment", "Containment Chamber", "Overhead transit and two flanks surround the isolation vessel.", RoomCategory.Containment, RoomGeometry.CoverHeavy | RoomGeometry.Shielded | RoomGeometry.Vertical, 5, 5, 5, new Vector3(20f, 8.4f, 28f), _orange, ContainmentGeometry, ContainmentMarkers(), new[] { Variant("Containment Response", 5, 0, 1, 2, 3, 4, 6), Variant("Isolation Lock", 5, 0, 1, 2, 3, 4, 5), Variant("Maximum Isolation", 5, 0, 1, 3, 4, 5, 6) }, RoomReward.AmmunitionCache, RoomReward.MedicalSupplies, RoomReward.ChronoCache),
                BuildAssignment("surveillance-center", "Surveillance Center", "Break line of sight between server banks. The perimeter stays open.", RoomCategory.Operations, RoomGeometry.CloseQuarters | RoomGeometry.CoverHeavy, 2, 3, 2, new Vector3(19f, 7.2f, 25f), _cyan, ServerGeometry, ServerMarkers(), new[] { Variant("Data Watch", 2, 0, 1, 2), Variant("Server Sweep", 3, 0, 1, 3, 4), Variant("Restricted Access", 3, 1, 2, 5, 6) }, RoomReward.MedicalSupplies, RoomReward.ChronoCache),
                BuildAssignment("atrium", "Central Atrium", "Long outlooks from the galleries overlook a split central concourse.", RoomCategory.Operations, RoomGeometry.Open | RoomGeometry.Vertical | RoomGeometry.LongRange, 2, 4, 3, new Vector3(24f, 10.5f, 33f), _cyan, AtriumGeometry, AtriumMarkers(), new[] { Variant("Gallery Watch", 2, 0, 1, 2, 3), Variant("Crossfire", 3, 0, 2, 3, 4), Variant("Atrium Lockdown", 5, 0, 2, 3, 4, 5) }, RoomReward.ChronoCache, RoomReward.AmmunitionCache, RoomReward.PriorityProtocol),
                BuildAssignment("cargo", "Cargo Inspection", "Stacked containers alternate narrow lanes with broad inspection bays.", RoomCategory.Logistics, RoomGeometry.CoverHeavy | RoomGeometry.CloseQuarters | RoomGeometry.Shielded, 2, 4, 3, new Vector3(22f, 8.8f, 29f), _orange, CargoGeometry, CargoMarkers(), new[] { Variant("Containment Team", 2, 0, 1, 3), Variant("Guard Patrol", 3, 0, 1, 2, 4), Variant("Heavy Inspection", 5, 3, 4, 5, 6) }, RoomReward.MedicalSupplies, RoomReward.AmmunitionCache, RoomReward.PriorityProtocol),
                BuildAssignment("power", "Power Distribution", "Circle the reactor housing. Side routes keep moving builds in motion.", RoomCategory.Operations, RoomGeometry.Open | RoomGeometry.Vertical | RoomGeometry.Shielded, 3, 4, 4, new Vector3(22f, 9.4f, 27f), _violet, PowerGeometry, PowerMarkers(), new[] { Variant("Circuit Watch", 3, 0, 1, 2, 3), Variant("Protected Relay", 4, 0, 1, 2, 4), Variant("Emergency Distribution", 5, 1, 2, 3, 4, 5) }, RoomReward.ChronoCache, RoomReward.MedicalSupplies, RoomReward.PriorityProtocol),
                BuildAssignment("tactical", "Tactical Assessment", "Thread staggered covers and climb shallow ramps to line up shots.", RoomCategory.Assessment, RoomGeometry.LongRange | RoomGeometry.CoverHeavy | RoomGeometry.Vertical, 3, 4, 4, new Vector3(21f, 8.3f, 32f), _orange, TacticalGeometry, TacticalMarkers(), new[] { Variant("Live-Fire Drill", 3, 0, 1, 3, 5), Variant("Firing Line", 4, 0, 1, 2, 3, 4), Variant("Combined Assessment", 5, 0, 1, 3, 5, 6) }, RoomReward.AmmunitionCache, RoomReward.MedicalSupplies, RoomReward.PriorityProtocol),
                BuildExpansion("processing", "Processing Hall", "Parallel production lines flank two moving service blocks.", RoomCategory.Operations, RoomGeometry.Open | RoomGeometry.CoverHeavy, 2, new Vector3(30f, 10f, 40f), _cyan, ProcessingGeometry),
                BuildExpansion("junction", "Gravity Junction", "Offset bridge decks meet above a clear junction.", RoomCategory.Operations, RoomGeometry.Vertical | RoomGeometry.DroneHeavy, 2, new Vector3(24f, 15f, 31f), _violet, JunctionGeometry),
                BuildExpansion("transit", "Transit Exchange", "A long two-track freight interchange with mobile containers.", RoomCategory.Logistics, RoomGeometry.LongRange | RoomGeometry.CoverHeavy, 2, new Vector3(27f, 11f, 45f), _orange, TransitGeometry),
                BuildExpansion("live-wire", "Live Wire", "Cycling power panels threaten the floor, wall and ceiling.", RoomCategory.Operations, RoomGeometry.Vertical | RoomGeometry.CloseQuarters, 2, new Vector3(19f, 8f, 26f), _violet, WireGeometry),
                BuildExpansion("inspection-maze", "Inspection Maze", "Three connected screening cells with clear overpass routes.", RoomCategory.Assessment, RoomGeometry.CloseQuarters | RoomGeometry.CoverHeavy, 2, new Vector3(17f, 8.2f, 24f), _orange, MazeGeometry),
                BuildExpansion("turbine", "Turbine Chamber", "A ring route around active turbine machinery.", RoomCategory.Operations, RoomGeometry.Open | RoomGeometry.Vertical, 3, new Vector3(28f, 13f, 34f), _violet, TurbineGeometry),
                BuildExpansion("observatory", "Crossfire Observatory", "Staggered observation decks overlook a long shooting gallery.", RoomCategory.Assessment, RoomGeometry.LongRange | RoomGeometry.Vertical | RoomGeometry.DroneHeavy, 3, new Vector3(30f, 15f, 39f), _cyan, ObservatoryGeometry),
                BuildExpansion("lockdown", "Emergency Lockdown", "Alternating shutters leave flanking lanes through a partitioned arena.", RoomCategory.Logistics, RoomGeometry.CoverHeavy | RoomGeometry.Shielded, 3, new Vector3(29f, 11f, 37f), _orange, LockdownGeometry)
            };
            return rooms;
        }

        private RoomDefinition BuildAssignment(string id, string title, string description, RoomCategory category, RoomGeometry tags, int minDepth, int maxDepth, int difficulty, Vector3 size, Material accent, Action<Transform> layout, SpawnSpec[] markers, VariantSpec[] variants, params RoomReward[] rewards)
        {
            var room = new GameObject(title).transform;
            try
            {
                var structure = PlaygroundSceneBuilder.Child("Architecture", room);
                Enclosure(structure, "Room", 0f, size.z, size.x, size.y, accent);
                Bulkhead(structure, 0f, size.x, size.y, false);
                Block(structure, "Arrival Door", new Vector3(0f, 1.8f, -0.2f), new Vector3(4.87f, 3.6f, 0.48f), _graphite);
                Bulkhead(structure, size.z, size.x, size.y, false);

                var module = (GameObject)PrefabUtility.InstantiatePrefab(_airlock, room);
                module.transform.localPosition = new Vector3(0f, 0f, size.z);
                layout(PlaygroundSceneBuilder.Child("Arena Fixtures", room));
                Header(room, $"MERIDIAN   /   {title.ToUpperInvariant()}", 5.3f, 3.59f, accent);
                Header(room, "VERTICAL TRANSFER  /  MERIDIAN", size.z - 0.37f, 4.23f, accent);
                Poster(room, size.x * 0.5f - 0.036f, 6.2f);

                var sockets = PlaygroundSceneBuilder.Child("Room Sockets", room);
                var entry = PlaygroundSceneBuilder.Child("Entry Point", sockets);
                entry.localPosition = new Vector3(0f, 0.89f, 1.7f);
                var exit = PlaygroundSceneBuilder.Child("Exit Point", sockets);
                exit.localPosition = new Vector3(0f, 0.89f, size.z);
                var combat = Zone(room, "Combat Zone", new Vector3(0f, size.y * 0.5f, (size.z + 3.5f) * 0.5f), new Vector3(size.x, size.y + 0.1f, size.z - 3.5f));
                var exitZone = module.transform.Find("Exit Zone").GetComponent<BoxCollider>();
                var encounter = room.gameObject.AddComponent<EncounterController>();
                PlaygroundSceneBuilder.Wire(encounter, "_combatZone", combat, "_exitZone", exitZone, "_gate", module.GetComponentInChildren<EncounterGate>());
                var encounterData = new SerializedObject(encounter);
                encounterData.FindProperty("_roomName").stringValue = title;
                encounterData.ApplyModifiedPropertiesWithoutUndo();

                var runtime = room.gameObject.AddComponent<RoomRuntime>();
                PlaygroundSceneBuilder.Wire(runtime, "_encounter", encounter, "_entry", entry, "_exit", exit);
                AddRoomMechanisms(runtime, id);
                Physics.SyncTransforms();
                var roster = new List<SpawnSpec>(72);
                variants = ExpandEncounter(id, size, markers, variants, roster);
                WriteMarkers(runtime, roster.ToArray());
                var thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RouteArt}/Surveys/{id}.png");
                var prefab = PrefabUtility.SaveAsPrefabAsset(room.gameObject, $"{RoutePrefabs}/Rooms/{title}.prefab");
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Failed to save room {title}.");
                }

                return SaveDefinition(id, title, description, category, tags, minDepth, maxDepth, difficulty, prefab.GetComponent<RoomRuntime>(), thumbnail, variants, rewards);
            }
            finally
            {
                Object.DestroyImmediate(room.gameObject);
            }
        }

        private GameObject BuildTransferPrefab()
        {
            var root = new GameObject("MERIDIAN Transfer Airlock").transform;
            Enclosure(root, "Transfer", 0f, 5.2f, 4.8f, 3.6f, _cyan);
            Block(root, "Elevator Backplate", new Vector3(0f, 1.8f, 5.15f), new Vector3(5.4f, 3.6f, 0.36f), _graphite);
            Block(root, "Cabin Identification", new Vector3(0f, 1.8f, 4.94f), new Vector3(3.9f, 1.1f, 0.06f), _shell, false);
            PlaygroundArt.Label(root, "MERIDIAN / VERTICAL TRANSFER", new Vector3(0f, 1.8f, 4.89f), 0.17f, new Color(0.09f, 0.14f, 0.16f));
            Block(root, "Threshold Stripe", new Vector3(0f, 0.012f, 3f), new Vector3(4.7f, 0.024f, 0.18f), _orange, false);
            var gate = Gate(root, 0.75f, _cyan);
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("_waitForClearance").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Zone(root, "Exit Zone", new Vector3(0f, 1.8f, 3.8f), new Vector3(4.8f, 3.8f, 1.45f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{RoutePrefabs}/MERIDIAN Transfer Airlock.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab != null ? prefab : throw new InvalidOperationException("Failed to save transfer airlock.");
        }

        private void WriteMarkers(RoomRuntime runtime, SpawnSpec[] markers)
        {
            var root = PlaygroundSceneBuilder.Child("Encounter Markers", runtime.transform);
            var armory = new SecurityPlaygroundBuilder(_art, root);
            var serialized = new SerializedObject(runtime);
            var roster = serialized.FindProperty("_markers");
            roster.arraySize = markers.Length;

            for (var index = 0; index < markers.Length; index++)
            {
                var marker = markers[index];
                var data = roster.GetArrayElementAtIndex(index);
                var point = PlaygroundSceneBuilder.Child($"{index + 1:00}  {marker.Kind}", root);
                point.SetLocalPositionAndRotation(marker.Position, Quaternion.Euler(0f, marker.Yaw, 0f));
                data.FindPropertyRelative("_enemy").objectReferenceValue = armory.GetPrefab(marker.Kind).GetComponent<SecurityEnemy>();
                data.FindPropertyRelative("_point").objectReferenceValue = point;
                data.FindPropertyRelative("_flightHalfExtents").vector3Value = marker.Volume;
                var signal = PlaygroundSceneBuilder.Child("Reinforcement Warning", point);
                Block(signal, "Amber Arrival Beacon", new Vector3(0f, 0.32f, 0f), new Vector3(0.14f, 1.18f, 0.14f), _orange, false);
                Block(signal, "Dock Signal", Vector3.down * 0.24f, new Vector3(0.68f, 0.065f, 0.68f), _violet, false);
                signal.gameObject.SetActive(false);
                data.FindPropertyRelative("_warning").objectReferenceValue = signal.gameObject;
                var patrol = data.FindPropertyRelative("_patrol");
                patrol.arraySize = marker.Patrol.Length;
                for (var step = 0; step < marker.Patrol.Length; step++)
                {
                    var waypoint = PlaygroundSceneBuilder.Child($"Patrol {index + 1:00}.{step + 1}", root);
                    waypoint.localPosition = marker.Patrol[step];
                    patrol.GetArrayElementAtIndex(step).objectReferenceValue = waypoint;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private RoomDefinition SaveDefinition(string id, string title, string description, RoomCategory category, RoomGeometry tags, int minDepth, int maxDepth, int difficulty, RoomRuntime prefab, Texture2D thumbnail, VariantSpec[] variants, RoomReward[] rewards)
        {
            var path = $"{RoutePrefabs}/Rooms/{title}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<RoomDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            PlaygroundSceneBuilder.Wire(definition, "_prefab", prefab, "_thumbnail", thumbnail);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_title").stringValue = title;
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_category").intValue = (int)category;
            serialized.FindProperty("_geometry").intValue = (int)tags;
            serialized.FindProperty("_minDepth").intValue = minDepth;
            serialized.FindProperty("_maxDepth").intValue = maxDepth;
            serialized.FindProperty("_difficulty").intValue = difficulty;
            var entries = serialized.FindProperty("_variants");
            entries.arraySize = variants.Length;
            for (var index = 0; index < variants.Length; index++)
            {
                var variant = variants[index];
                var data = entries.GetArrayElementAtIndex(index);
                data.FindPropertyRelative("_title").stringValue = variant.Title;
                data.FindPropertyRelative("_difficulty").intValue = variant.Difficulty;
                data.FindPropertyRelative("_initialActive").intValue = variant.Initial;
                data.FindPropertyRelative("_reinforcementThreshold").intValue = variant.Threshold;
                data.FindPropertyRelative("_reinforcementBatch").intValue = variant.Batch;
                data.FindPropertyRelative("_hazardMask").intValue = variant.HazardMask;
                var markers = data.FindPropertyRelative("_markers");
                markers.arraySize = variant.Markers.Length;
                for (var marker = 0; marker < variant.Markers.Length; marker++)
                {
                    markers.GetArrayElementAtIndex(marker).intValue = variant.Markers[marker];
                }
            }

            var bonuses = serialized.FindProperty("_rewards");
            bonuses.arraySize = rewards.Length;
            for (var index = 0; index < rewards.Length; index++)
            {
                bonuses.GetArrayElementAtIndex(index).intValue = (int)rewards[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private Texture2D CaptureSurvey(Transform room, string id, Vector3 size)
        {
            const int width = 720;
            const int height = 304;
            var camera = new GameObject("Arena Survey Camera").AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var transforms = room.GetComponentsInChildren<Transform>(true);
            var layers = new int[transforms.Length];
            try
            {
                for (var index = 0; index < transforms.Length; index++)
                {
                    var node = transforms[index];
                    layers[index] = node.gameObject.layer;
                    node.gameObject.layer = 31;
                    if (node.name is "Room Ceiling" or "Transfer Ceiling" && node.TryGetComponent<Renderer>(out var roof))
                    {
                        roof.enabled = false;
                    }
                }

                var center = new Vector3(0f, 0f, size.z * 0.5f + 1f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, 0.18f), Vector3.right);
                camera.transform.position = center - camera.transform.forward * 40f;
                camera.orthographic = true;
                camera.orthographicSize = (size.x + 3f) * 0.5f;
                camera.aspect = (float)width / height;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.036f, 0.064f, 0.09f);
                camera.enabled = false;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                var path = $"{RouteArt}/Surveys/{id}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally
            {
                for (var index = 0; index < transforms.Length; index++)
                {
                    var node = transforms[index];
                    node.gameObject.layer = layers[index];
                    if (node.name is "Room Ceiling" or "Transfer Ceiling" && node.TryGetComponent<Renderer>(out var roof))
                    {
                        roof.enabled = true;
                    }
                }

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private Material PosterMaterial()
        {
            var path = $"{RouteArt}/MERIDIAN Safety Poster.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{RouteArt}/Verify Your Timeline.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private void Poster(Transform room, float x, float z)
        {
            var poster = GameObject.CreatePrimitive(PrimitiveType.Quad);
            poster.name = "MERIDIAN Safety Placard";
            poster.transform.SetParent(room, false);
            poster.transform.SetLocalPositionAndRotation(new Vector3(x, 1.85f, z), Quaternion.Euler(0f, 90f, 0f));
            poster.transform.localScale = new Vector3(0.93f, 1.4f, 1f);
            poster.GetComponent<Renderer>().sharedMaterial = _poster;
            Object.DestroyImmediate(poster.GetComponent<Collider>());
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}"))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
