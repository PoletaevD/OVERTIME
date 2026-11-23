using TMPro;
using UnityEditor;
using UnityEngine;

namespace Overtime.Editor
{
    internal sealed class PlaygroundArt
    {
        private readonly Material _concrete;
        private readonly Material _panel;
        private readonly Material _graphite;
        private readonly Material _orange;
        private readonly Material _cyan;
        private readonly Material _violet;
        private readonly Material _white;

        internal Transform Root { get; }

        internal PlaygroundArt()
        {
            Root = new GameObject("MERIDIAN | Gravity Test Facility").transform;
            _concrete = Material("Warm Composite", new Color(0.65f, 0.65f, 0.60f));
            _panel = Material("Ceramic Panel", new Color(0.37f, 0.43f, 0.48f));
            _graphite = Material("Graphite Alloy", new Color(0.09f, 0.13f, 0.17f));
            _orange = Material("Safety Orange", new Color(1f, 0.31f, 0.065f), 0.8f);
            _cyan = Material("Cyan Guide", new Color(0.13f, 0.85f, 1f), 2.2f);
            _violet = Material("Violet Guide", new Color(0.57f, 0.32f, 1f), 1.45f);
            _white = Material("Porcelain Shell", new Color(0.86f, 0.88f, 0.84f));
        }

        internal void BuildCourse()
        {
            BuildCalibration();
            BuildShaft();
            BuildCeilingRoute();
            BuildTransitions();
            BuildFlow();
            Block(Root, "South Bulkhead", 0f, 4f, -11.7f, 15f, 8f, 1f, _graphite);
            Block(Root, "North Bulkhead", 0f, 5f, 80.7f, 15f, 11f, 1f, _graphite);
        }

        internal Transform BuildTemporalRoom()
        {
            Root.name = "MERIDIAN | Temporal Test Facility";
            var room = Hall("Temporal Testing Chamber", -10f, 14f, 7f, 0f, true);
            Block(room, "South Bulkhead", 0f, 3.5f, -10.45f, 15f, 7f, 0.9f, _graphite);
            Block(room, "North Bulkhead", 0f, 3.5f, 14.45f, 15f, 7f, 0.9f, _graphite);
            Gate(room, "MERIDIAN  /  TEMPORAL TEST CHAMBER", 2f, 4.7f, _cyan);

            for (var z = -3f; z <= 12f; z += 5f)
            {
                Block(room, "Wall Transit Marker", 6.98f, 3.5f, z, 0.034f, 6.9f, 0.12f, _cyan, false);
                Block(room, "Ceiling Transit Marker", 0f, 6.98f, z, 13.9f, 0.034f, 0.12f, _cyan, false);
            }

            return room;
        }

        private void BuildCalibration()
        {
            var zone = Hall("A | Movement Calibration", -11.5f, 8f, 7f, 0f, false);
            Gate(zone, "A   MOVEMENT CALIBRATION", -1.5f, 4.2f, _orange);
            Label(zone, "MERIDIAN  /  APPLIED GRAVITY", new Vector3(0f, 3.44f, -1.62f), 0.24f, new Color(0.94f, 0.95f, 0.90f));
            for (var i = 0; i < 4; i++)
            {
                var x = -4.4f + 1.1f * i;
                var z = 0.9f + 1.9f * i;
                var height = 0.35f + 0.17f * i;
                Block(zone, "Jump Step", x, height * 0.5f, z, 2.1f, height, 1.6f, _panel);
                Block(zone, "Step Leading Edge", x, height + 0.014f, z - 0.7f, 2.06f, 0.026f, 0.075f, _orange, false);
            }

            Block(zone, "Slalom Barrier", 3.7f, 0.54f, 3.6f, 3.2f, 1.08f, 1.4f, _graphite);
        }

        private void BuildShaft()
        {
            var zone = Hall("B | Gravity Walls", 8f, 25f, 10f, 0f, true);
            Gate(zone, "B   GRAVITY WALLS", 8.3f, 4.8f, _cyan);
            for (var i = 0; i < 4; i++)
            {
                var z = 11f + 3.5f * i;
                Block(zone, "Vertical Ascent Guide", 6.98f, 5f, z, 0.034f, 9.75f, 0.095f, _cyan, false);
                Block(zone, "Ceiling Guide", 0f, 9.98f, z, 13.9f, 0.034f, 0.095f, _cyan, false);
            }

            Label(zone, "HOLD SPACE TO ATTACH  /  KEEP MOVING TO TURN", new Vector3(0f, 4.04f, 8.18f), 0.24f, new Color(0.47f, 0.88f, 0.91f));
            Block(zone, "Upper Deck | Entrance", 0f, 3.72f, 23.2f, 14f, 0.55f, 3.6f, _panel);
            Block(zone, "Upper Deck Leading Edge", 0f, 4f, 21.45f, 13.94f, 0.03f, 0.095f, _orange, false);
        }

        private void BuildCeilingRoute()
        {
            var zone = Hall("C | Ceiling Traversal", 25f, 43f, 10f, -3f, true);
            Gate(zone, "C   CEILING TRAVERSAL", 26f, 6.3f, _violet);
            for (var i = 0; i < 3; i++)
            {
                var z = 29.5f + 4.7f * i;
                Block(zone, "Floor Baffle", i % 2 == 0 ? -2.9f : 2.9f, 1.2f, z, 8.2f, 8.4f, 1.05f, _panel);
                Block(zone, "Baffle Crest", i % 2 == 0 ? -2.9f : 2.9f, 5.39f, z, 8.14f, 0.045f, 0.93f, _violet, false);
            }

            Block(zone, "Ceiling Route Strip", -2.1f, 9.978f, 34f, 0.105f, 0.032f, 16.8f, _violet, false);
            Block(zone, "Ceiling Route Strip", 2.1f, 9.978f, 34f, 0.105f, 0.032f, 16.8f, _violet, false);
            Block(zone, "Upper Deck | Exit", 0f, 3.72f, 42f, 14f, 0.55f, 2f, _panel);
        }

        private void BuildTransitions()
        {
            var zone = Hall("D | Gravity Transitions", 43f, 62f, 10f, 0f, true);
            Gate(zone, "D   SURFACE TRANSITIONS", 44f, 6.3f, _orange);
            Block(zone, "Right Wall Return", 5.8f, 5f, 49.5f, 2.4f, 10f, 3.3f, _panel);
            Block(zone, "Left Wall Return", -5.8f, 5f, 55.2f, 2.4f, 10f, 3.3f, _panel);
            Block(zone, "Convex Practice Plinth", 0f, 0.8f, 53.3f, 3.8f, 1.6f, 4.1f, _graphite);
            Block(zone, "Convex Edge Mark", 0f, 1.615f, 55.28f, 3.7f, 0.028f, 0.12f, _orange, false);
            Block(zone, "Cross Axis | 90 Degrees", 0f, 8.9f, 58.7f, 14f, 2.2f, 2.6f, _graphite);
            Block(zone, "Low Overhead Guide", 0f, 7.783f, 58.7f, 13.8f, 0.03f, 0.1f, _cyan, false);
        }

        private void BuildFlow()
        {
            var zone = Hall("E | Movement Flow", 62f, 80.5f, 10f, 0f, true);
            Gate(zone, "E   MOVEMENT FLOW", 62.5f, 4.8f, _cyan);
            for (var i = 0; i < 4; i++)
            {
                var z = 65.5f + i * 3.1f;
                var x = i % 2 == 0 ? -3.4f : 3.4f;
                Block(zone, "Flow Vault", x, 0.64f, z, 7.2f, 1.28f, 1.15f, _panel);
                Block(zone, "Vault Safety Edge", x, 1.29f, z - 0.51f, 7.15f, 0.03f, 0.07f, _orange, false);
            }

            Gate(zone, "RUN COMPLETE  /  R TO RESET", 79.9f, 3.1f, _orange);
        }

        private Transform Hall(string title, float start, float end, float height, float floor, bool ceiling)
        {
            var zone = PlaygroundSceneBuilder.Child(title, Root);
            var length = end - start;
            var center = (end + start) * 0.5f;
            Block(zone, "Floor Deck", 0f, floor - 0.45f, center, 15f, 0.9f, length, _concrete);
            Block(zone, "Left Load Wall", -7.45f, (height + floor) * 0.5f, center, 0.9f, height - floor, length, _concrete);
            Block(zone, "Right Load Wall", 7.45f, (height + floor) * 0.5f, center, 0.9f, height - floor, length, _concrete);
            if (ceiling)
            {
                Block(zone, "Ceiling Deck", 0f, height + 0.45f, center, 15f, 0.9f, length, _panel);
            }

            for (var x = -6f; x <= 6f; x += 12f)
            {
                Block(zone, "Floor Lane Inlay", x, floor + 0.012f, center, 0.09f, 0.022f, length - 0.1f, _orange, false);
            }

            for (var z = start + 2f; z < end - 1f; z += 4f)
            {
                Block(zone, "Left Modular Rib", -6.98f, floor + 1.72f, z, 0.05f, 3.4f, 0.24f, _graphite, false);
                Block(zone, "Right Modular Rib", 6.98f, floor + 1.72f, z, 0.05f, 3.4f, 0.24f, _graphite, false);
                Block(zone, "Floor Expansion Joint", 0f, floor + 0.006f, z, 13.94f, 0.009f, 0.042f, _panel, false);
            }

            return zone;
        }

        private void Gate(Transform zone, string title, float z, float y, Material accent)
        {
            Block(zone, "Sign Left Bracket", -6.61f, y, z, 0.8f, 0.18f, 0.22f, _graphite, false);
            Block(zone, "Sign Right Bracket", 6.61f, y, z, 0.8f, 0.18f, 0.22f, _graphite, false);
            Block(zone, "Sign Backplate", 0f, y, z, 12.5f, 1.03f, 0.21f, _graphite, false);
            Block(zone, "Sign Accent", 0f, y - 0.48f, z - 0.12f, 12.4f, 0.065f, 0.038f, accent, false);
            Label(zone, title, new Vector3(0f, y + 0.015f, z - 0.13f), 0.49f, new Color(0.94f, 0.95f, 0.90f));
        }

        internal static TextMeshPro Label(Transform parent, string title, Vector3 position, float scale, Color color)
        {
            var label = PlaygroundSceneBuilder.Child(title, parent).gameObject.AddComponent<TextMeshPro>();
            label.transform.localPosition = position;
            label.rectTransform.sizeDelta = new Vector2(14f, 2f);
            label.fontSize = scale * 10f;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = title;
            return label;
        }

        internal GameObject BuildWeapon(int layer)
        {
            var root = new GameObject("MERIDIAN | ARC-01");
            var movement = PlaygroundSceneBuilder.Child("Movement Pivot", root.transform);
            var sway = PlaygroundSceneBuilder.Child("Sway Pivot", movement);
            var recoil = PlaygroundSceneBuilder.Child("Recoil Pivot", sway);
            var shell = PlaygroundSceneBuilder.Child("ARC-01 Receiver", recoil);
            Block(shell, "Lower Receiver", 0f, -0.025f, 0.16f, 0.156f, 0.118f, 0.43f, _graphite, false);
            Block(shell, "Ceramic Upper", 0f, 0.036f, 0.155f, 0.176f, 0.094f, 0.40f, _white, false);
            Block(shell, "Reinforced Rail", 0f, 0.09f, 0.16f, 0.095f, 0.024f, 0.31f, _graphite, false);
            Block(shell, "Fore Receiver", 0f, 0.015f, 0.365f, 0.121f, 0.124f, 0.115f, _white, false);
            Block(shell, "Dark Barrel", 0f, 0.023f, 0.443f, 0.063f, 0.064f, 0.111f, _graphite, false);
            Block(shell, "Orange Muzzle Shroud", 0f, 0.023f, 0.497f, 0.094f, 0.083f, 0.057f, _orange, false);
            Block(shell, "Muzzle Aperture", 0f, 0.023f, 0.528f, 0.050f, 0.047f, 0.009f, _graphite, false);
            Block(shell, "Grip", 0f, -0.126f, 0.02f, 0.094f, 0.174f, 0.115f, _graphite, false).Rotate(-16f, 0f, 0f);
            Block(shell, "Power Cell", 0f, -0.107f, 0.23f, 0.124f, 0.123f, 0.133f, _graphite, false).Rotate(12f, 0f, 0f);
            Block(shell, "Rear Sight", 0f, 0.119f, 0.049f, 0.089f, 0.038f, 0.035f, _graphite, false);
            Block(shell, "Front Sight", 0f, 0.118f, 0.378f, 0.035f, 0.048f, 0.025f, _graphite, false);
            Block(shell, "Sight Emitter", 0f, 0.13f, 0.363f, 0.015f, 0.014f, 0.008f, _cyan, false);
            for (var side = -1; side <= 1; side += 2)
            {
                Block(shell, "Hull Accent", side * 0.09f, 0.042f, 0.104f, 0.011f, 0.035f, 0.091f, _orange, false);
                Block(shell, "Charge Strip", side * 0.092f, 0.033f, 0.252f, 0.009f, 0.014f, 0.109f, _cyan, false);
                for (var i = 0; i < 4; i++)
                {
                    Block(shell, "Mechanical Vent", side * 0.082f, -0.03f, 0.19f + i * 0.024f, 0.018f, 0.03f, 0.012f, _panel, false);
                }
            }

            var flash = PlaygroundSceneBuilder.Child("Muzzle Flash", recoil);
            flash.localPosition = new Vector3(0f, 0.023f, 0.572f);
            Block(flash, "Flash Core", 0f, 0f, 0.03f, 0.045f, 0.048f, 0.17f, _cyan, false).Rotate(0f, 0f, 45f);
            Block(flash, "Flash Streak", 0f, 0f, 0.01f, 0.075f, 0.018f, 0.092f, _orange, false).Rotate(0f, 0f, 27f);
            flash.gameObject.SetActive(false);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/ARC-01 Viewmodel.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal Transform Block(Transform parent, string name, float x, float y, float z, float width, float height, float depth, Material material, bool collision = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = new Vector3(x, y, z);
            block.transform.localScale = new Vector3(width, height, depth);
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision)
            {
                Object.DestroyImmediate(block.GetComponent<BoxCollider>());
            }

            return block.transform;
        }

        private Material Material(string name, Color color, float emission = 0f)
        {
            var path = $"Assets/Art/Gravity Playground/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name,
                color = color
            };
            material.SetFloat("_Smoothness", 0.28f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
