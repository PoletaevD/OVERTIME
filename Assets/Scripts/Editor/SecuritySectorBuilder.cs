using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private const string ScenePath = "Assets/Scenes/Security Sector.unity";
        private const string ArtPath = "Assets/Art/Gravity Playground";

        private readonly PlaygroundArt _art = new();
        private readonly Material _shell = Load("Porcelain Shell");
        private readonly Material _deck = Load("Warm Composite");
        private readonly Material _graphite = Load("Graphite Alloy");
        private readonly Material _orange = Load("Safety Orange");
        private readonly Material _cyan = Load("Cyan Guide");
        private readonly Material _violet = Load("Violet Guide");

        [MenuItem("OVERTIME/Create Security Sector")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                var existing = SceneManager.GetSceneByPath(ScenePath);
                SceneManager.SetActiveScene(existing.isLoaded ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive));
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            new SecuritySectorBuilder().Build(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Failed to save {ScenePath}.");
            }

            CombatPlaygroundBuilder.RegisterScene(ScenePath);
        }

        [MenuItem("OVERTIME/Install Security Sector Run Protocols")]
        public static void InstallRunProtocols()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before installing run protocols.");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var wasLoaded = scene.isLoaded;
            if (wasLoaded && scene.isDirty)
            {
                throw new InvalidOperationException("Save the Security Sector scene before installing run protocols.");
            }

            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            SectorRoute route = null;
            PlaygroundPlayer player = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                route ??= root.GetComponentInChildren<SectorRoute>();
                player ??= root.GetComponentInChildren<PlaygroundPlayer>();
            }

            if (route == null || player == null)
            {
                throw new InvalidOperationException("The Security Sector scene requires a route and a player.");
            }

            if (route.GetComponent<RunProgression>() == null)
            {
                AddRunProtocols(route, player, player.GetComponentInChildren<PlaygroundHud>());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("OVERTIME/Install Security Sector Weapon Loadout")]
        public static void InstallWeaponLoadout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before installing the loadout.");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var wasLoaded = scene.isLoaded;
            if (wasLoaded && scene.isDirty)
            {
                throw new InvalidOperationException("Save the Security Sector scene before installing the loadout.");
            }

            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                SectorRoute route = null;
                PlaygroundPlayer player = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    route ??= root.GetComponentInChildren<SectorRoute>();
                    player ??= root.GetComponentInChildren<PlaygroundPlayer>();
                }

                if (route == null || player == null)
                {
                    throw new InvalidOperationException("The Security Sector scene requires a route and a player.");
                }

                var movement = player.GetComponentInChildren<PlaygroundHud>();
                if (route.GetComponent<RunProgression>() == null)
                {
                    AddRunProtocols(route, player, movement);
                }

                AddWeaponLoadout(route, player, movement);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"Failed to save {ScenePath}.");
                }
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AddWeaponLoadout(SectorRoute route, PlaygroundPlayer player, PlaygroundHud movement)
        {
            var definitions = new WeaponAssets().LoadOrCreate();
            var catalog = new RunUpgradeAssets().LoadOrCreate();
            PlaygroundSceneBuilder.Wire(route.GetComponent<RunProgression>(), "_catalog", catalog);
            var options = route.Generated ? new[] { definitions[1], definitions[2], definitions[3] } : new[] { definitions[1], definitions[2] };
            PlaygroundSceneBuilder.Wire(route, "_secondaryOptions", options);
            var weapon = player.Combat.Weapon;
            PlaygroundSceneBuilder.Wire(weapon, "_primary", definitions[0]);
            var data = new SerializedObject(weapon);
            data.FindProperty("_projectileCapacity").intValue = 64;
            data.ApplyModifiedPropertiesWithoutUndo();

            if (movement.GetComponent<WeaponSelectionHud>() != null)
            {
                return;
            }

            var presentation = player.GetComponentInChildren<WeaponPresentation>();
            var pivot = presentation.transform.Find("Movement Pivot/Sway Pivot/Recoil Pivot");
            var viewmodels = new WeaponViewmodel[options.Length];
            for (var index = 0; index < options.Length; index++)
            {
                var view = (GameObject)PrefabUtility.InstantiatePrefab(options[index].Viewmodel, pivot);
                viewmodels[index] = view.GetComponent<WeaponViewmodel>();
                view.SetActive(false);
            }

            PlaygroundSceneBuilder.Wire(presentation, "_alternatives", viewmodels);
            PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            var hud = new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset());
            hud.ExtendRunInspection(movement);
            hud.BuildWeaponHud(movement, route, route.GetComponent<TemporalDirector>(), options);
            var bindings = movement.transform.Find("Controls/Bindings").GetComponent<TMPro.TMP_Text>();
            bindings.text = bindings.text.Replace("R  RELOAD   F5  RETRY SECTOR", "R  RELOAD   1 / 2 / WHEEL  SWAP");
            PrefabUtility.RecordPrefabInstancePropertyModifications(bindings);
        }

        private static void AddRunProtocols(SectorRoute route, PlaygroundPlayer player, PlaygroundHud movement)
        {
            var catalog = new RunUpgradeAssets().LoadOrCreate();
            var director = route.GetComponent<TemporalDirector>();
            var progression = route.gameObject.AddComponent<RunProgression>();
            PlaygroundSceneBuilder.Wire(progression, "_catalog", catalog);
            PlaygroundSceneBuilder.Wire(route, "_progression", progression);

            var echo = PlaygroundSceneBuilder.Child("RECALL Echo", route.transform).gameObject;
            var outline = echo.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = 16;
            outline.startWidth = 0.06f;
            outline.endWidth = 0.06f;
            outline.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Temporal Playground/Temporal Trace.mat");
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.enabled = false;
            var presentation = echo.AddComponent<RecallEchoPresentation>();
            PlaygroundSceneBuilder.Wire(presentation, "_progression", progression, "_director", director, "_outline", outline);

            new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset()).BuildUpgrades(movement, route, director, player, progression);
            var viewmodel = player.GetComponentInChildren<WeaponPresentation>();
            var chargeStrips = new List<Renderer>(2);
            foreach (var renderer in viewmodel.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name == "Charge Strip")
                {
                    chargeStrips.Add(renderer);
                }
            }

            PlaygroundSceneBuilder.Wire(viewmodel, "_chargeStrips", chargeStrips.ToArray());
            PrefabUtility.RecordPrefabInstancePropertyModifications(viewmodel);
            var bindings = movement.transform.Find("Controls/Bindings").GetComponent<TMPro.TMP_Text>();
            bindings.text = bindings.text.Replace("F3  DEBUG   F4  MOTION   H  HINTS", "TAB  BUILD   F4  MOTION   H  HINTS");
            PrefabUtility.RecordPrefabInstancePropertyModifications(bindings);
        }

        private void Build(Scene scene, RoomDefinition[] library = null)
        {
            var root = _art.Root;
            root.name = library == null ? "MERIDIAN | Security Sector" : "MERIDIAN | Security Route";
            PlaygroundSceneBuilder.AddLighting(_art, AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{ArtPath}/Facility Volume.asset"));
            CreateBoltMaterial();
            var timeline = PlaygroundSceneBuilder.Child("Sector Timeline", root).gameObject;
            var director = timeline.AddComponent<TemporalDirector>();
            var energy = timeline.AddComponent<ChronoEnergy>();
            var route = timeline.AddComponent<SectorRoute>();
            var armory = new SecurityPlaygroundBuilder(_art, timeline.transform);
            var pool = armory.BuildPool(director, library == null ? 40 : 128, _violet);
            if (library == null)
            {
                var rooms = new[]
                {
                    BuildRoom("01", "Security Checkpoint", 0f, 24f, 18f, 7.4f, _cyan, director, pool),
                    BuildRoom("02", "Surveillance Shaft", 30f, 24f, 16f, 13f, _violet, director, pool),
                    BuildRoom("03", "Containment Chamber", 60f, 28f, 20f, 8.4f, _orange, director, pool)
                };
                PlaygroundSceneBuilder.Wire(route, "_rooms", rooms);
            }
            else
            {
                PlaygroundSceneBuilder.Wire(route, "_library", library);
            }

            PlaygroundSceneBuilder.Wire(route, "_enemyProjectiles", pool);
            PlaygroundSceneBuilder.Wire(director, "_energy", energy, "_sector", route);

            var player = CombatPlaygroundBuilder.BuildPlayer(scene, director, new Vector3(0f, 0.89f, 1.7f));
            player.gameObject.AddComponent<CombatAudio>();
            PlaygroundSceneBuilder.Wire(player.GetComponent<CombatAudio>(), "_director", director, "_player", player);
            var camera = player.GetComponentInChildren<Camera>();
            var cinemachine = player.GetComponentInChildren<CinemachineCamera>();
            camera.fieldOfView = 56f;
            cinemachine.Lens.FieldOfView = 56f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cinemachine);
            var rig = player.GetComponent<GravityCamera>();
            var chamber = new CombatPlaygroundBuilder.Chamber(_art, root);
            chamber.BuildImpact(director, player);
            var hud = new PlaygroundHudBuilder(PlaygroundSceneBuilder.TMPAsset());
            var movement = player.GetComponentInChildren<PlaygroundHud>();
            var pulse = hud.BuildTemporal(movement, director, rig);
            var combatHud = hud.BuildCombat(movement, director, player);
            hud.BuildSector(movement, combatHud, player, director, route, player.GetComponentInChildren<Camera>());
            chamber.BuildTemporalEffects(director, rig, pulse);
            AddRunProtocols(route, player, movement);
            AddWeaponLoadout(route, player, movement);
            if (library != null)
            {
                hud.BuildRouteHud(movement, route, director);
            }
        }

        private EncounterController BuildRoom(string number, string title, float start, float length, float width, float height, Material accent, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            var room = PlaygroundSceneBuilder.Child($"{number} | {title}", _art.Root);
            room.localPosition = Vector3.forward * start;
            var structures = PlaygroundSceneBuilder.Child("Architecture", room);
            Enclosure(structures, "Room", 0f, length, width, height, accent);
            Bulkhead(structures, 0f, width, height, number == "01");
            Bulkhead(structures, length, width, height, false);
            Enclosure(structures, "Transfer", length, 6f, 4.8f, 3.6f, accent);
            if (number == "03")
            {
                Block(structures, "Terminal Containment Wall", new Vector3(0f, 1.8f, length + 6f), new Vector3(5.6f, 3.6f, 0.6f), _graphite);
            }

            Header(room, $"MERIDIAN    /    {number}    {title.ToUpperInvariant()}", 6.25f, 3.58f, accent);
            Header(room, number == "03" ? "SECURITY SECTOR  /  EXIT" : "TRANSFER AIRLOCK", length - 0.38f, 4.23f, accent);
            var gate = Gate(room, length + 1.1f, accent);
            var entry = Zone(room, "Combat Zone", new Vector3(0f, height * 0.5f, (length + 3.4f) * 0.5f), new Vector3(width, height + 0.1f, length - 3.4f));
            var exit = Zone(room, "Exit Zone", new Vector3(0f, 1.8f, length + 3.9f), new Vector3(5f, 3.8f, 1.4f));
            var enemies = new SecurityPlaygroundBuilder(_art, room);
            var actors = number switch
            {
                "01" => Checkpoint(room, enemies, director, pool),
                "02" => Surveillance(room, enemies, director, pool),
                _ => Containment(room, enemies, director, pool)
            };

            var encounter = room.gameObject.AddComponent<EncounterController>();
            PlaygroundSceneBuilder.Wire(encounter, "_enemies", actors, "_combatZone", entry, "_exitZone", exit, "_gate", gate);
            var data = new SerializedObject(encounter);
            data.FindProperty("_roomName").stringValue = title;
            data.ApplyModifiedPropertiesWithoutUndo();
            return encounter;
        }

        private void CheckpointGeometry(Transform room)
        {
            foreach (var side in new[] { -1, 1 })
            {
                Bay(room, "Checkpoint Column", side * 5.9f, 8.4f, 1.25f, 7.38f, 1.3f);
                Bay(room, "Credential Turnstile", side * 2.75f, 6.5f, 1.72f, 1.0f, 0.62f);
                Bay(room, "Screening Console", side * 4.2f, 13.8f, 2.6f, 1.42f, 1.15f);
            }

            Bay(room, "Auditor Podium", 0f, 19.6f, 4.6f, 1.44f, 3.5f);
            Bay(room, "Podium Step", 2.6f, 18.43f, 1.6f, 0.57f, 1.7f);
        }

        private SecurityEnemy[] Checkpoint(Transform room, SecurityPlaygroundBuilder enemies, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            CheckpointGeometry(room);
            return new[]
            {
                Place(enemies, SecurityEnemyKind.Guard, "Security Unit 01", 301, new Vector3(-5f, 0.92f, 11.7f), director, pool, new Vector3(-6.35f, 0.92f, 14.8f), new Vector3(-4.95f, 0.92f, 11.7f), new Vector3(-2.9f, 0.92f, 10.05f)),
                Place(enemies, SecurityEnemyKind.Guard, "Security Unit 02", 302, new Vector3(5.1f, 0.92f, 12.5f), director, pool, new Vector3(6.25f, 0.92f, 15.85f), new Vector3(4.9f, 0.92f, 11.6f), new Vector3(3.1f, 0.92f, 10f)),
                Place(enemies, SecurityEnemyKind.Auditor, "Laser Auditor", 303, new Vector3(0f, 2.35f, 19.6f), director, null)
            };
        }

        private void SurveillanceGeometry(Transform room)
        {
            for (var index = 0; index < 3; index++)
            {
                var side = index % 2 == 0 ? -1f : 1f;
                var y = 2.8f + index * 2.8f;
                var z = 8.8f + index * 3.75f;
                Block(room, "Inspection Catwalk", new Vector3(side * 6.4f, y, z), new Vector3(3.2f, 0.42f, 4.8f), _graphite);
                Block(room, "Catwalk Edge", new Vector3(side * 4.82f, y + 0.215f, z), new Vector3(0.12f, 0.035f, 4.75f), _violet, false);
                Block(room, "Vertical Dispatch Guide", new Vector3(side * 7.974f, 6.45f, z - 2.6f), new Vector3(0.032f, 12.65f, 0.15f), _cyan, false);
            }

            Bay(room, "Scanner Housing", -2.9f, 11.1f, 2.25f, 1.85f, 1.12f);
            Bay(room, "Relay Cabinet", 2.6f, 16.6f, 2.1f, 2.15f, 1.4f);
            Block(room, "Auditor Service Deck", new Vector3(0f, 2.45f, 21.1f), new Vector3(5.2f, 0.46f, 1.7f), _graphite);
        }

        private SecurityEnemy[] Surveillance(Transform room, SecurityPlaygroundBuilder enemies, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            SurveillanceGeometry(room);
            var drones = new[]
            {
                Place(enemies, SecurityEnemyKind.Drone, "Surveillance Drone 01", 304, new Vector3(-3.3f, 3.1f, 8.5f), director, pool, new Vector3(-3.1f, 4.6f, 9.6f), new Vector3(-0.6f, 2.9f, 7.2f)),
                Place(enemies, SecurityEnemyKind.Drone, "Surveillance Drone 02", 305, new Vector3(3.3f, 5.1f, 13.6f), director, pool, new Vector3(3f, 6.2f, 12.5f), new Vector3(1.2f, 4.7f, 14.8f)),
                Place(enemies, SecurityEnemyKind.Drone, "Surveillance Drone 03", 306, new Vector3(-1.9f, 8.1f, 17.5f), director, pool, new Vector3(-1.9f, 7.9f, 16f), new Vector3(1.9f, 6.3f, 18.5f)),
                Place(enemies, SecurityEnemyKind.Auditor, "Laser Auditor", 307, new Vector3(0f, 3.58f, 21.1f), director, null)
            };

            for (var index = 0; index < 3; index++)
            {
                var data = new SerializedObject(drones[index]);
                data.FindProperty("_flightHalfExtents").vector3Value = new Vector3(4f, 2.4f, 4.8f);
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            return drones;
        }

        private void ContainmentGeometry(Transform room)
        {
            Bay(room, "Isolation Vessel", 0f, 13.5f, 5.0f, 4.15f, 5.8f);
            Block(room, "Containment Spine", new Vector3(0f, 4.26f, 13.5f), new Vector3(4.2f, 0.23f, 5.3f), _graphite);
            Block(room, "Containment Status Strip", new Vector3(0f, 4.38f, 13.5f), new Vector3(3.4f, 0.035f, 0.18f), _orange, false);
            foreach (var side in new[] { -1, 1 })
            {
                Bay(room, "Emergency Shield Station", side * 6.6f, 18.4f, 2.7f, 1.48f, 1.15f);
                Block(room, "Service Conduit", new Vector3(side * 8.15f, 7.42f, 13f), new Vector3(3.7f, 1.98f, 0.82f), _graphite);
                Block(room, "Overhead Transit Strip", new Vector3(side * 4.5f, 8.37f, 18f), new Vector3(0.14f, 0.04f, 13.5f), _cyan, false);
            }

        }

        private SecurityEnemy[] Containment(Transform room, SecurityPlaygroundBuilder enemies, TemporalDirector director, TemporalProjectileEmitter pool)
        {
            ContainmentGeometry(room);
            return new[]
            {
                Place(enemies, SecurityEnemyKind.Enforcer, "Compliance Enforcer", 308, new Vector3(1.8f, 1.0f, 9.4f), director, pool, new Vector3(1.4f, 1f, 7.8f), new Vector3(3.4f, 1f, 9f)),
                Place(enemies, SecurityEnemyKind.Guard, "Security Unit 01", 309, new Vector3(-6.2f, 0.92f, 13f), director, pool, new Vector3(-6.45f, 0.92f, 15.6f), new Vector3(-4.9f, 0.92f, 10.3f)),
                Place(enemies, SecurityEnemyKind.Guard, "Security Unit 02", 310, new Vector3(6.2f, 0.92f, 19.9f), director, pool, new Vector3(6.4f, 0.92f, 22f), new Vector3(4.8f, 0.92f, 18.1f)),
                Place(enemies, SecurityEnemyKind.Drone, "Surveillance Drone", 311, new Vector3(-1.4f, 6.14f, 17f), director, pool, new Vector3(-3.1f, 5.8f, 15f), new Vector3(2.8f, 6.5f, 18.1f))
            };
        }

        private SecurityEnemy Place(SecurityPlaygroundBuilder builder, SecurityEnemyKind kind, string name, int id, Vector3 position, TemporalDirector director, TemporalProjectileEmitter pool, params Vector3[] patrol)
        {
            return builder.PlaceEnemy(builder.GetPrefab(kind), name, id, position, director, pool, patrol);
        }

        private void Enclosure(Transform root, string name, float start, float depth, float width, float height, Material accent)
        {
            var z = start + depth * 0.5f;
            Block(root, $"{name} Deck", new Vector3(0f, -0.39f, z), new Vector3(width + 0.8f, 0.78f, depth), _deck);
            Block(root, $"{name} Ceiling", new Vector3(0f, height + 0.35f, z), new Vector3(width + 0.8f, 0.70f, depth), _shell);
            for (var side = -1; side <= 1; side += 2)
            {
                var edge = side * width * 0.5f;
                Block(root, $"{name} Load Wall", new Vector3(edge + side * 0.31f, height * 0.5f, z), new Vector3(0.62f, height, depth), _shell);
                Block(root, "Deck Light", new Vector3(edge - side * 0.08f, 0.28f, z), new Vector3(0.13f, 0.10f, depth - 0.16f), accent, false);
                Block(root, "Ceiling Light", new Vector3(edge - side * 0.85f, height - 0.027f, z), new Vector3(0.18f, 0.045f, depth - 0.16f), accent, false);
                for (var rib = start + 2.2f; rib < start + depth - 0.7f; rib += 4f)
                {
                    Block(root, $"{name} Wall Fascia", new Vector3(edge - side * 0.025f, height * 0.72f, rib), new Vector3(0.05f, height * 0.52f, 0.38f), _graphite, false);
                }
            }

            AddLight(root, "Access Lighting", new Vector3(0f, height - 0.85f, start + depth * 0.24f), width * 0.95f, new Color(0.84f, 0.93f, 1f), 34f);
            if (depth > 10f)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    Block(root, "Department Floor Lane", new Vector3(side * width * 0.17f, 0.011f, start + depth * 0.48f), new Vector3(0.07f, 0.021f, depth * 0.80f), accent, false);
                }

                AddLight(root, "Transfer Lighting", new Vector3(0f, height - 1.45f, start + depth * 0.76f), width, new Color(1f, 0.89f, 0.73f), 40f);
                AddLight(root, "Bounce Lighting", new Vector3(-width * 0.24f, height * 0.50f, z), width * 0.82f, new Color(0.76f, 0.87f, 1f), 18f);
            }
        }

        private void Bulkhead(Transform root, float z, float width, float height, bool closed)
        {
            if (closed)
            {
                Block(root, "Sector Entry Wall", new Vector3(0f, height * 0.5f, z), new Vector3(width, height, 0.48f), _graphite);
                return;
            }

            var jambWidth = (width - 4.8f) * 0.5f;
            for (var side = -1; side <= 1; side += 2)
            {
                Block(root, "Airlock Jamb", new Vector3(side * (2.4f + jambWidth * 0.5f), height * 0.5f, z), new Vector3(jambWidth, height, 0.50f), _graphite);
                Block(root, "Airlock Indicator", new Vector3(side * 2.57f, 1.65f, z - 0.263f), new Vector3(0.10f, 2.8f, 0.045f), _orange, false);
            }

            Block(root, "Airlock Lintel", new Vector3(0f, (height + 3.6f) * 0.5f, z), new Vector3(4.82f, height - 3.6f, 0.50f), _graphite);
        }

        private EncounterGate Gate(Transform root, float z, Material accent)
        {
            var gate = PlaygroundSceneBuilder.Child("Encounter Interlock", root);
            gate.localPosition = new Vector3(0f, 1.81f, z);
            var barrier = gate.gameObject.AddComponent<BoxCollider>();
            barrier.size = new Vector3(4.86f, 3.65f, 0.43f);
            var panel = Block(gate, "Sliding Panel", Vector3.zero, new Vector3(4.82f, 3.58f, 0.42f), _graphite, false);
            Block(panel, "Interlock Frame", new Vector3(0f, 0f, -0.52f), new Vector3(0.84f, 0.74f, 0.07f), _shell, false);
            Block(panel, "Interlock Light", new Vector3(0f, 0.31f, -0.57f), new Vector3(0.7f, 0.052f, 0.08f), accent, false);
            var label = PlaygroundArt.Label(gate, "MERIDIAN  /  SECURITY INTERLOCK", new Vector3(0f, -0.54f, -0.29f), 0.23f, new Color(0.86f, 0.94f, 0.99f));
            label.transform.SetParent(panel, true);
            var component = gate.gameObject.AddComponent<EncounterGate>();
            PlaygroundSceneBuilder.Wire(component, "_barrier", barrier, "_panel", panel);
            return component;
        }

        private BoxCollider Zone(Transform root, string name, Vector3 center, Vector3 size)
        {
            var zone = PlaygroundSceneBuilder.Child(name, root).gameObject.AddComponent<BoxCollider>();
            zone.center = center;
            zone.size = size;
            zone.isTrigger = true;
            return zone;
        }

        private void Bay(Transform root, string name, float x, float z, float width, float height, float depth)
        {
            Block(root, name, new Vector3(x, height * 0.5f, z), new Vector3(width, height, depth), _shell);
            Block(root, $"{name} Recess", new Vector3(x, height * 0.42f, z - depth * 0.5f - 0.025f), new Vector3(width * 0.88f, height * 0.54f, 0.05f), _graphite, false);
            Block(root, $"{name} Warning Edge", new Vector3(x, height * 0.83f, z - depth * 0.5f - 0.03f), new Vector3(width * 0.92f, 0.073f, 0.056f), _orange, false);
        }

        private void Header(Transform room, string text, float z, float y, Material accent)
        {
            Block(room, "Department Sign", new Vector3(0f, y, z), new Vector3(9f, 0.78f, 0.17f), _graphite, false);
            Block(room, "Department Sign Light", new Vector3(0f, y - 0.35f, z - 0.11f), new Vector3(8.8f, 0.055f, 0.04f), accent, false);
            PlaygroundArt.Label(room, text, new Vector3(0f, y + 0.02f, z - 0.118f), 0.31f, new Color(0.93f, 0.96f, 1f));
        }

        private void AddLight(Transform root, string name, Vector3 position, float range, Color color, float intensity)
        {
            var light = PlaygroundSceneBuilder.Child(name, root).gameObject.AddComponent<Light>();
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
        }

        private Transform Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            return _art.Block(parent, name, position.x, position.y, position.z, size.x, size.y, size.z, material, collision);
        }

        private void CreateBoltMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            const string path = "Assets/Resources/ARC-01 Bolt.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
            {
                var bolt = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                bolt.SetColor("_BaseColor", new Color(0.13f, 1.42f, 1.83f));
                AssetDatabase.CreateAsset(bolt, path);
            }
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{ArtPath}/{name}.mat");
        }
    }
}
