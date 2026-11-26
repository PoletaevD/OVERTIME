using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private const string SupervisorScene = "Assets/Scenes/Security Route Stage 05B.unity";
        private const string SupervisorTitle = "Supervisor Chamber";

        [MenuItem("OVERTIME/Build Stage 05B Supervisor")]
        public static void BuildStage05B()
        {
            var scene = SceneManager.GetSceneByPath(SupervisorScene);
            if (EditorApplication.isPlayingOrWillChangePlaymode || (scene.isLoaded && scene.isDirty))
            {
                throw new InvalidOperationException("Finish Play Mode and save any open Stage 05B edits before building the Supervisor.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SupervisorScene) == null && !AssetDatabase.CopyAsset(RouteScenePath, SupervisorScene))
            {
                throw new InvalidOperationException("Failed to copy the Stage 05A.1 playable scene.");
            }

            var previous = SceneManager.GetActiveScene();
            var opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(SupervisorScene, OpenSceneMode.Additive);
            }

            SceneManager.SetActiveScene(scene);
            try
            {
                var builder = new SecuritySectorBuilder();
                var boss = builder.SaveSupervisorPrefab();
                var chamber = builder.SaveSupervisorChamber(boss);
                SectorRoute route = null;
                SectorHud sectorHud = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    route ??= root.GetComponentInChildren<SectorRoute>(true);
                    sectorHud ??= root.GetComponentInChildren<SectorHud>(true);
                }

                PlaygroundSceneBuilder.Wire(route, "_supervisorChamber", chamber);
                builder.BuildSupervisorHud(sectorHud, route);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("Failed to save the Stage 05B scene.");
                }

                CombatPlaygroundBuilder.RegisterScene(SupervisorScene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if (previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        private RoomDefinition SaveSupervisorChamber(GameObject bossPrefab)
        {
            const float size = 32f;
            const float height = 13.4f;
            var room = new GameObject(SupervisorTitle).transform;
            try
            {
                var structure = PlaygroundSceneBuilder.Child("Architecture", room);
                Enclosure(structure, "Supervisor Chamber", 0f, size, size, height, _cyan);
                Bulkhead(structure, 0f, size, height, false);
                Bulkhead(structure, size, size, height, false);
                Block(structure, "Arrival Door", new Vector3(0f, 1.8f, -0.2f), new Vector3(4.87f, 3.6f, 0.48f), _graphite);

                var airlock = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoutePrefabs}/MERIDIAN Transfer Airlock.prefab");
                var terminal = (GameObject)PrefabUtility.InstantiatePrefab(airlock, room);
                terminal.transform.localPosition = new Vector3(0f, 0f, size);
                Header(room, "MERIDIAN  /  CENTRAL SECURITY", 8.6f, 4.65f, _orange);
                Header(room, "SUPERVISOR CHAMBER", 31.6f, 5.65f, _cyan);

                var fixtures = PlaygroundSceneBuilder.Child("Command Gallery Fixtures", room);
                SupervisorChamberGeometry(fixtures);
                var leftCover = MovingBlock(fixtures, "West Security Shutter", new Vector3(-5.25f, 0.92f, 21.5f), new Vector3(1.1f, 1.84f, 3.35f), new Vector3(-3.6f, 0f, 0f), 7.4f);
                var rightCover = MovingBlock(fixtures, "East Security Shutter", new Vector3(5.4f, 1.05f, 12.6f), new Vector3(1.1f, 2.1f, 3.4f), new Vector3(3.6f, 0f, 0f), 6.8f);
                var hazard = Hazard(fixtures, ArenaHazardKind.ElectricalPanel, new Vector3(-10.4f, 0.05f, 23.2f), Quaternion.identity, new Vector3(1.35f, 0.45f, 2.0f), 3.4f);

                var boss = ((GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, room)).GetComponent<SupervisorBoss>();
                boss.transform.localPosition = new Vector3(0f, 0f, 18f);
                var sockets = PlaygroundSceneBuilder.Child("Room Sockets", room);
                var entry = PlaygroundSceneBuilder.Child("Entry Point", sockets);
                entry.localPosition = new Vector3(0f, 0.89f, 1.7f);
                var exit = PlaygroundSceneBuilder.Child("Exit Point", sockets);
                exit.localPosition = new Vector3(0f, 0.89f, size);
                var arrival = Zone(room, "Safe Arrival", new Vector3(0f, 2.2f, 5.7f), new Vector3(size, 4.4f, 4.05f));
                var combat = Zone(room, "Combat Zone", new Vector3(0f, height * 0.5f, (size + 7.55f) * 0.5f), new Vector3(size, height + 0.1f, size - 7.55f));
                var encounter = room.gameObject.AddComponent<EncounterController>();
                PlaygroundSceneBuilder.Wire(encounter, "_combatZone", combat, "_exitZone", terminal.transform.Find("Exit Zone").GetComponent<BoxCollider>(), "_gate", terminal.GetComponentInChildren<EncounterGate>(), "_boss", boss);
                var data = new SerializedObject(encounter);
                data.FindProperty("_roomName").stringValue = SupervisorTitle;
                data.ApplyModifiedPropertiesWithoutUndo();

                var runtime = room.gameObject.AddComponent<RoomRuntime>();
                PlaygroundSceneBuilder.Wire(runtime, "_encounter", encounter, "_entry", entry, "_exit", exit, "_arrivalZone", arrival, "_worldActors", new TemporalActor[] { boss, leftCover, rightCover, hazard });
                WriteMarkers(runtime, SupervisorReinforcementMarkers());
                var ammo = new Transform[5];
                var ammoPoints = new[] { new Vector3(-1.6f, 0.52f, 5.4f), new Vector3(1.6f, 0.52f, 5.4f), new Vector3(-9.2f, 0.52f, 15.1f), new Vector3(9.2f, 0.52f, 22.5f), new Vector3(1.5f, 0.52f, 28.3f) };
                for (var index = 0; index < ammo.Length; index++)
                {
                    ammo[index] = PlaygroundSceneBuilder.Child($"Chamber Supply {index + 1}", sockets);
                    ammo[index].localPosition = ammoPoints[index];
                }

                PlaygroundSceneBuilder.Wire(runtime, "_ammoSockets", ammo);
                var prefab = PrefabUtility.SaveAsPrefabAsset(room.gameObject, $"{RoutePrefabs}/Rooms/{SupervisorTitle}.prefab");
                if (prefab == null)
                {
                    throw new InvalidOperationException("Failed to save the Supervisor Chamber prefab.");
                }

                var variant = new VariantSpec("Central Security", 6, new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, batch: 4, hazardMask: 1);
                return SaveDefinition("supervisor", SupervisorTitle, "Central security command. Service galleries flank the Supervisor's weapons platform.", RoomCategory.Supervisor, RoomGeometry.Open | RoomGeometry.Vertical | RoomGeometry.CoverHeavy, 6, 6, 6, prefab.GetComponent<RoomRuntime>(), null, new[] { variant }, Array.Empty<RoomReward>());
            }
            finally
            {
                Object.DestroyImmediate(room.gameObject);
            }
        }

        private SpawnSpec[] SupervisorReinforcementMarkers()
        {
            return new[]
            {
                Guard(-10.4f, 14.9f, -7.2f, 11.4f, 145f),
                Drone(9.0f, 4.7f, 21.7f, 7f, 5f, 18f, 210f),
                Guard(10.4f, 19.7f, 7.1f, 24.8f, 215f),
                Drone(-8.6f, 5.2f, 24.6f, -6.8f, 5.6f, 20.3f, 155f),
                Guard(7.8f, 26.5f, 5.5f, 23.6f, 185f),
                Drone(-8.5f, 6.4f, 13.2f, -5.4f, 6.1f, 16.8f, 165f),
                Guard(-8.1f, 26.3f, -5.3f, 25.4f, 160f),
                Drone(8.6f, 5.6f, 25.9f, 5.3f, 6.2f, 23.1f, 210f)
            };
        }

        private void SupervisorChamberGeometry(Transform room)
        {
            Block(room, "Boss Platform", new Vector3(0f, 0.12f, 18f), new Vector3(6.0f, 0.24f, 5.7f), _graphite, false);
            for (var side = -1; side <= 1; side += 2)
            {
                Block(room, "Platform Warning Rail", new Vector3(side * 2.92f, 0.246f, 18f), new Vector3(0.095f, 0.025f, 5.56f), _orange, false);
                Block(room, "Preparation Guide", new Vector3(side * 2.26f, 0.025f, 4.15f), new Vector3(0.07f, 0.03f, 5.4f), _cyan, false);
                Block(room, "Supply Alcove", new Vector3(side * 2.65f, 0.48f, 5.05f), new Vector3(0.57f, 0.96f, 2.2f), _shell);
                for (var post = 0; post < 2; post++)
                {
                    var z = 10.8f + 13.2f * post + side * 0.6f;
                    Block(room, "Command Column", new Vector3(side * 11.75f, 6.7f, z), new Vector3(1.46f, 13.4f, 1.7f), _graphite);
                    Block(room, "Column Cladding", new Vector3(side * 11.70f, 5.9f, z - 0.87f), new Vector3(1.10f, 6.4f, 0.12f), _shell, false);
                    Block(room, "Column Beacon", new Vector3(side * 11.65f, 5.88f, z - 0.98f), new Vector3(0.14f, 4.8f, 0.07f), _cyan, false);
                }

                for (var level = 0; level < 2; level++)
                {
                    var y = 2.55f + level * 4.3f;
                    var z = level == 0 ? 17.6f : 20.8f;
                    Block(room, "Observation Gallery", new Vector3(side * 13.89f, y, z), new Vector3(4.26f, 0.44f, 13.1f), _graphite);
                    Block(room, "Gallery Edge Light", new Vector3(side * 11.79f, y + 0.24f, z), new Vector3(0.085f, 0.045f, 13f), level == 0 ? _orange : _cyan, false);
                    Block(room, "Gallery Underlay", new Vector3(side * 12.74f, y - 0.24f, z), new Vector3(1.65f, 0.08f, 12.2f), _shell, false);
                }

                var ramp = Block(room, "Gallery Access Ramp", new Vector3(side * 13.88f, 1.29f, 8.05f), new Vector3(2.7f, 0.23f, 6.45f), _deck);
                ramp.localRotation = Quaternion.Euler(-24f, 0f, 0f);
                Block(room, "Ceiling Service Track", new Vector3(side * 4.9f, 13.09f, 18f), new Vector3(0.87f, 0.32f, 19.0f), _graphite, false);
                Block(room, "Ceiling Track Light", new Vector3(side * 4.9f, 12.912f, 18f), new Vector3(0.19f, 0.052f, 18.3f), _cyan, false);
                Bay(room, "Command Relay", side * 7.55f, 15.7f + side * 2.8f, 2.85f, 1.48f, 1.39f);
                Bay(room, "Auxiliary Routing Cabinet", side * 8.9f, 28.7f, 3.7f, 2.65f, 1.6f);
            }

            Block(room, "Upper Cross Gallery", new Vector3(0f, 6.85f, 27.4f), new Vector3(23.8f, 0.44f, 1.65f), _graphite);
            Block(room, "Cross Gallery Edge", new Vector3(0f, 7.09f, 26.61f), new Vector3(23.6f, 0.045f, 0.09f), _cyan, false);
            AddLight(room, "Supervisor Key Light", new Vector3(-4f, 6.2f, 12.7f), 18f, new Color(0.80f, 0.90f, 1f), 18f);
            AddLight(room, "Rear Reactor Fill", new Vector3(4f, 4.6f, 24.1f), 15f, new Color(0.47f, 0.86f, 1f), 12f);
        }

        private void BuildSupervisorHud(SectorHud sectorHud, SectorRoute route)
        {
            var position = new Vector2(0f, -76f);
            var roomPanel = sectorHud.transform.Find("Sector Encounter").gameObject;
            if (sectorHud.TryGetComponent<SupervisorHud>(out var existing))
            {
                var data = new SerializedObject(existing);
                var panelObject = (GameObject)data.FindProperty("_panel").objectReferenceValue;
                ((RectTransform)panelObject.transform).anchoredPosition = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(panelObject.transform);
                PlaygroundSceneBuilder.Wire(existing, "_roomPanel", roomPanel);
                return;
            }

            var panel = HudRect(sectorHud.GetComponentInParent<Canvas>().transform, "Supervisor HUD", position, new Vector2(572f, 67f), new Vector2(0.5f, 1f));
            var title = HudText(panel, "Name", "THE SUPERVISOR", new Vector2(-110f, 18f), new Vector2(320f, 23f), 22f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold | FontStyles.Italic;
            title.characterSpacing = 4f;
            title.fontSize = 24f;
            var vitals = HudText(panel, "Hit Points", string.Empty, new Vector2(184f, 16f), new Vector2(170f, 21f), 17f, TextAlignmentOptions.Right);
            vitals.fontStyle = FontStyles.Bold;
            var phase = HudText(panel, "Phase", string.Empty, new Vector2(0f, -21f), new Vector2(540f, 17f), 14f, TextAlignmentOptions.Left);
            phase.characterSpacing = 3f;
            var track = HudImage(panel, "Health Track", new Vector2(0f, -3f), new Vector2(542f, 9f), new Color(0.17f, 0.24f, 0.31f));
            var fill = HudImage(track.rectTransform, "Health Fill", Vector2.zero, Vector2.zero, Color.cyan);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            foreach (var fraction in new[] { SupervisorBoss.EmergencyThreshold, SupervisorBoss.CorrectiveThreshold })
            {
                var threshold = HudImage(track.rectTransform, "Phase Threshold", new Vector2((fraction - 0.5f) * 542f, 0f), new Vector2(4.5f, 15f), new Color(0.92f, 0.96f, 0.99f));
                threshold.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            }

            var controller = sectorHud.gameObject.AddComponent<SupervisorHud>();
            PlaygroundSceneBuilder.Wire(controller, "_route", route, "_panel", panel.gameObject, "_roomPanel", roomPanel, "_fill", fill, "_phase", phase, "_vitals", vitals);
            panel.gameObject.SetActive(false);
        }

        private RectTransform HudRect(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Image HudImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = HudRect(parent, name, position, size, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text HudText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var label = HudRect(parent, name, position, size, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = PlaygroundSceneBuilder.TMPAsset();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.88f, 0.95f, 1f);
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
