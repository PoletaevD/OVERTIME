using UnityEngine;

namespace Overtime.Editor
{
    public sealed partial class SecuritySectorBuilder
    {
        private void ServerGeometry(Transform root)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                for (var index = 0; index < 3; index++)
                {
                    var x = side * (index == 1 ? 4.7f : 3.5f);
                    var z = 8.3f + index * 5.2f;
                    Block(root, "Server Bank", new Vector3(x, 1.62f, z), new Vector3(2.12f, 3.24f, 2.4f), _graphite);
                    for (var shelf = 0; shelf < 3; shelf++)
                    {
                        Cassette(root, "Rack Cassette", new Vector3(x, 0.85f + shelf * 0.8f, z - 1.19f), new Vector3(1.86f, 0.68f, 0.22f), _shell);
                        Block(root, "Rack Status", new Vector3(x + 0.72f, 0.81f + shelf * 0.8f, z - 1.32f), new Vector3(0.14f, 0.045f, 0.035f), shelf == 2 ? _orange : _cyan, false);
                    }
                }

                Block(root, "Cable Bridge", new Vector3(side * 4.4f, 6.82f, 13.5f), new Vector3(1.15f, 0.56f, 14f), _graphite, false);
                Block(root, "Emergency Deck Guide", new Vector3(side * 7.45f, 0.022f, 13.5f), new Vector3(0.11f, 0.038f, 19f), _cyan, false);
            }

            Bay(root, "Offline Data Core", 0f, 14.4f, 1.95f, 2.3f, 2.15f);
            Bay(root, "Service Console", 0f, 20.5f, 3.4f, 1.15f, 1.17f);
        }

        private void AtriumGeometry(Transform root)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                Block(root, "Continuous Side Gallery", new Vector3(side * 9.9f, 2.8f, 18f), new Vector3(4.2f, 0.52f, 19f), _shell);
                Block(root, "Gallery Accent", new Vector3(side * 7.85f, 2.96f, 18f), new Vector3(0.11f, 0.10f, 18.8f), _cyan, false);
                Block(root, "Upper Observation Gallery", new Vector3(side * 10.1f, 5.82f, 23f), new Vector3(3.8f, 0.47f, 10f), _graphite);
                Bay(root, "Gallery Support", side * 10.9f, 9.3f, 1.28f, 2.6f, 0.85f);
                Bay(root, "Gallery Support", side * 10.9f, 26.6f, 1.28f, 2.6f, 0.85f);
                Bay(root, "Wayfinding Column", side * 6.6f, 20.3f, 1.15f, 10.5f, 1.18f);
                Block(root, "Gallery Approach", new Vector3(side * 8.6f, 0.46f, 6.8f), new Vector3(3.6f, 0.92f, 2.5f), _graphite);
                Block(root, "Gallery Approach", new Vector3(side * 9.05f, 0.94f, 8.3f), new Vector3(3.6f, 1.88f, 1.8f), _graphite);
                Block(root, "Gallery Approach", new Vector3(side * 9.5f, 1.27f, 9.75f), new Vector3(3.6f, 2.54f, 1.6f), _shell);
            }

            Bay(root, "Information Island", -3.2f, 12.1f, 3.05f, 1.28f, 3.1f);
            Bay(root, "Information Island", 3.8f, 23.7f, 3.2f, 1.52f, 3.8f);
            Block(root, "Dispatch Bridge", new Vector3(0f, 6.4f, 28.45f), new Vector3(16.9f, 0.46f, 2.4f), _graphite);
        }

        private void CargoGeometry(Transform root)
        {
            Container(root, -5.65f, 9.5f, 3.5f, 2.55f, 5.1f, false);
            Container(root, 5.6f, 11.3f, 3.55f, 2.65f, 5.55f, true);
            Container(root, -4.6f, 19.4f, 4.1f, 2.9f, 5.6f, true);
            Container(root, 5.7f, 21.5f, 3.55f, 2.6f, 5.4f, false);
            Bay(root, "Freight Scanner Base", 0f, 23.5f, 2.65f, 1.12f, 1.5f);
            for (var side = -1; side <= 1; side += 2)
            {
                Bay(root, "Gantry Leg", side * 8.9f, 16.2f, 1.2f, 8.8f, 1.12f);
                Block(root, "Overhead Gantry", new Vector3(side * 7.8f, 6.6f, 16.2f), new Vector3(5.8f, 0.63f, 1.78f), _graphite);
                Block(root, "Gantry Marking", new Vector3(side * 7.8f, 6.23f, 16.2f), new Vector3(5.72f, 0.05f, 0.16f), _orange, false);
            }
        }

        private void Container(Transform root, float x, float z, float width, float height, float length, bool stacked)
        {
            Block(root, "Freight Container", new Vector3(x, height * 0.5f, z), new Vector3(width, height, length), _graphite);
            Cassette(root, "Freight Door", new Vector3(x, height * 0.5f, z - length * 0.5f), new Vector3(width * 0.9f, height * 0.88f, 0.22f), _shell);
            for (var rib = 0; rib < 5; rib++)
            {
                var ribZ = z + (rib - 2) * length * 0.18f;
                Block(root, "Freight Rib", new Vector3(x, height * 0.5f, ribZ), new Vector3(width + 0.07f, height, 0.09f), _shell, false);
            }

            Block(root, "Lifting Rail", new Vector3(x, height - 0.14f, z - length * 0.5f - 0.08f), new Vector3(width * 0.91f, 0.065f, 0.13f), _orange, false);
            if (stacked)
            {
                Block(root, "Inspection Stack", new Vector3(x, height + 0.82f, z + 0.3f), new Vector3(width * 0.86f, 1.64f, length * 0.77f), _shell);
                Block(root, "Inspection Stack Band", new Vector3(x, height + 1.34f, z + 0.3f - length * 0.385f), new Vector3(width * 0.85f, 0.12f, 0.1f), _cyan, false);
            }
        }

        private void PowerGeometry(Transform root)
        {
            Block(root, "Generator Foundation", new Vector3(0f, 0.56f, 13.9f), new Vector3(7.6f, 1.12f, 8.6f), _shell);
            Block(root, "Generator Housing", new Vector3(0f, 3.15f, 13.9f), new Vector3(5.95f, 4.08f, 6.25f), _graphite);
            Cassette(root, "Generator Access Face", new Vector3(0f, 2.99f, 10.79f), new Vector3(5.2f, 3.1f, 0.3f), _shell);
            Block(root, "Generator Crown", new Vector3(0f, 5.38f, 13.9f), new Vector3(5.1f, 0.5f, 5.6f), _shell);
            Block(root, "Busbar Spine", new Vector3(0f, 8.94f, 13.9f), new Vector3(2.9f, 0.8f, 14.6f), _graphite);
            for (var side = -1; side <= 1; side += 2)
            {
                Bay(root, "Isolation Console", side * 7.5f, 7.7f, 2.3f, 1.25f, 1.05f);
                Block(root, "Circuit Wall Bus", new Vector3(side * 10.81f, 6.5f, 13.9f), new Vector3(0.33f, 1.5f, 15.2f), _graphite, false);
                Block(root, "Ring Lane", new Vector3(side * 4.3f, 0.016f, 13.9f), new Vector3(0.12f, 0.028f, 10f), _violet, false);
                Block(root, "Circuit Bus Indicator", new Vector3(side * 10.61f, 6.65f, 13.9f), new Vector3(0.04f, 0.12f, 14.7f), _violet, false);
                Block(root, "Coupling", new Vector3(side * 1.6f, 6.9f, 13.9f), new Vector3(0.7f, 3.1f, 1.65f), _graphite, false);
            }

            Bay(root, "Power Control Console", 0f, 21.6f, 2.8f, 1.35f, 1.2f);
        }

        private void TacticalGeometry(Transform root)
        {
            Bay(root, "Cover Module A", -3.7f, 8.35f, 4.1f, 1.15f, 0.85f);
            Bay(root, "Cover Module B", 4.6f, 12.45f, 4.0f, 1.4f, 0.94f);
            Bay(root, "Cover Module C", -3.9f, 17.55f, 3.5f, 1.25f, 0.88f);
            Bay(root, "Cover Module D", 4.7f, 23.45f, 3.4f, 1.45f, 0.9f);
            Block(root, "Review Platform West", new Vector3(-7.95f, 0.72f, 24f), new Vector3(4.4f, 1.44f, 5.8f), _graphite);
            Block(root, "Review Platform East", new Vector3(8.1f, 0.72f, 18f), new Vector3(4.1f, 1.44f, 5.9f), _graphite);
            Ramp(root, "Assessment Ramp West", -8f, 17.75f);
            Ramp(root, "Assessment Ramp East", 8.15f, 11.75f);
            Block(root, "Baffle Rail", new Vector3(-6.3f, 7.94f, 17f), new Vector3(1.6f, 0.63f, 15.5f), _graphite, false);
            Block(root, "Baffle Guide", new Vector3(6.3f, 8.26f, 17f), new Vector3(0.14f, 0.04f, 15.5f), _cyan, false);
            for (var index = 0; index < 5; index++)
            {
                Block(root, "Firing Lane Mark", new Vector3(0f, 0.013f, 9f + index * 3.6f), new Vector3(0.1f, 0.022f, 1.35f), _orange, false);
            }
        }

        private void Ramp(Transform root, string name, float x, float z)
        {
            var ramp = Block(root, name, new Vector3(x, 0.42f, z), new Vector3(3.6f, 0.56f, 7.05f), _shell);
            ramp.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            var stripe = Block(ramp, "Ramp Edge", new Vector3(-0.43f, 0.506f, 0f), new Vector3(0.025f, 0.025f, 0.97f), _orange, false);
            stripe.gameObject.isStatic = true;
        }

        private void Cassette(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            if (_cassette == null)
            {
                throw new System.InvalidOperationException("The MERIDIAN Service Cassette model is missing.");
            }

            var part = PlaygroundSceneBuilder.Child(name, root);
            part.localPosition = position;
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = _cassette;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
