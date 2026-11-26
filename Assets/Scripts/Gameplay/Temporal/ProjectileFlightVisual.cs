using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class ProjectileFlightVisual
    {
        internal enum Form
        {
            Compliance, Pellet, Deadline, Directive, Sentinel, Drone, Blast, Slug, KnifeWave
        }

        private static readonly int s_tint = Shader.PropertyToID("_Tint");
        private static readonly int s_emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Vector4 s_playerColor = new(1f, 0.76f, 0.012f, 1f);
        private static readonly Vector4 s_enemyColor = new(1f, 0.024f, 0.035f, 1f);

        private readonly Transform _root;
        private readonly MeshFilter _filter;
        private readonly MeshRenderer _body;
        private readonly MeshRenderer _halo;
        private readonly LineRenderer _trace;
        private readonly Assets _assets;
        private readonly bool _player;
        private Form _form;
        private float _rear;
        private float _traceWidth;
        private float _traceLength;
        private bool _wake;

        internal ProjectileFlightVisual(Transform root, Assets assets, bool player)
        {
            _root = root;
            _assets = assets;
            _player = player;
            _filter = GetOrAdd<MeshFilter>(root);
            _body = GetOrAdd<MeshRenderer>(root);
            _body.sharedMaterial = assets.Metal;
            _body.shadowCastingMode = ShadowCastingMode.Off;
            _body.receiveShadows = true;
            _body.lightProbeUsage = LightProbeUsage.BlendProbes;
            _body.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            _body.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            var halo = new GameObject("Flight Halo");
            halo.layer = root.gameObject.layer;
            halo.transform.SetParent(root, false);
            halo.AddComponent<MeshFilter>().sharedMesh = assets.HaloQuad;
            _halo = halo.AddComponent<MeshRenderer>();
            _halo.sharedMaterial = assets.Halo;
            // The shader turns the quad toward the camera and grows distant, undersized sprites.
            _halo.localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            ConfigureUnlitRenderer(_halo);

            // Older scenes already have a renderer on each pool slot.
            _trace = GetOrAdd<LineRenderer>(root);
            _trace.sharedMaterial = assets.Tracer;
            _trace.useWorldSpace = true;
            _trace.alignment = LineAlignment.View;
            _trace.textureMode = LineTextureMode.Stretch;
            _trace.textureScale = Vector2.one;
            _trace.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _trace.numCapVertices = 0;
            _trace.numCornerVertices = 0;
            _trace.positionCount = 2;
            ConfigureUnlitRenderer(_trace);
            _trace.enabled = false;
        }

        internal void SetStyle(Form form, float radius, float referenceRadius, bool hot)
        {
            _form = form;
            _filter.sharedMesh = _assets.Forms[(int)form];
            var scale = Mathf.Clamp(1f + (radius / referenceRadius - 1f) * 0.55f, 0.65f, 1.45f);
            _root.localScale = Vector3.one * scale;
            _rear = -_filter.sharedMesh.bounds.min.z * Mathf.Abs(_root.lossyScale.z);
            var (width, length, haloDiameter) = form switch
            {
                Form.Pellet => (0.042f, 0.22f, 0.085f),
                Form.Directive => (0.070f, 0.48f, 0.158f),
                Form.Deadline => (0.086f, 1.24f, 0.177f),
                Form.Slug => (0.088f, 0.62f, 0.182f),
                Form.Drone => (0.094f, 0.51f, 0.196f),
                Form.Sentinel => (0.106f, 0.70f, 0.226f),
                Form.Blast => (0.108f, 0.76f, 0.215f),
                _ => (0.072f, 0.65f, 0.164f)
            };
            _traceWidth = width * scale;
            _traceLength = length;
            _halo.transform.localScale = Vector3.one * haloDiameter;

            var color = _player ? s_playerColor : s_enemyColor;
            var properties = _assets.Properties;
            properties.Clear();
            properties.SetVector(s_emissionColor, color * (hot ? 2.9f : 2.05f));
            _body.SetPropertyBlock(properties);
            properties.Clear();
            color.w = hot ? 1.24f : form == Form.Pellet ? 0.80f : 1f;
            properties.SetVector(s_tint, color);
            _halo.SetPropertyBlock(properties);
            _trace.SetPropertyBlock(properties);

            _body.enabled = true;
            _halo.enabled = form != Form.KnifeWave;
            ConfigureTrace(_wake);
            _trace.enabled = false;
        }

        internal void SetPose(Vector3 position, Vector3 direction)
        {
            _root.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        internal void Present(Vector3 position, Vector3 direction, float segment, float speed, Ray view, bool showTraces, bool rewind, bool slowMotion)
        {
            _root.position = position;
            var depth = _player ? Vector3.Dot(position - view.origin, view.direction) : float.PositiveInfinity;
            var visible = depth > 0.24f;
            _body.enabled = visible;
            _halo.enabled = visible && _form != Form.KnifeWave;
            if (!showTraces || !visible || _form == Form.KnifeWave)
            {
                _trace.enabled = false;
                return;
            }

            var available = segment;
            if (_player)
            {
                var heading = Vector3.Dot(direction, view.direction);
                if (heading > 0.001f)
                {
                    // Keep the ribbon out of the near-camera weapon view.
                    available = Mathf.Min(available, Mathf.Max(0f, depth - 0.36f) / heading);
                }
            }

            var wake = rewind || (!_player && slowMotion);
            var length = Mathf.Min(_traceLength, speed * (_player ? 0.018f : 0.043f));
            if (wake)
            {
                length = Mathf.Min(1.28f, speed * 0.115f);
                if (_form == Form.Pellet)
                {
                    length *= 0.43f;
                }
            }

            // Segment distance resets at launch and ricochet, including when restored from history.
            var trailLength = Mathf.Min(available - _rear, length);
            _trace.enabled = trailLength > 0.012f;
            if (!_trace.enabled)
            {
                return;
            }

            if (_wake != wake)
            {
                ConfigureTrace(wake);
            }

            var rear = position - direction * _rear;
            _trace.SetPosition(0, rear);
            _trace.SetPosition(1, rear - direction * trailLength);
        }

        private void ConfigureTrace(bool wake)
        {
            _wake = wake;
            _trace.sharedMaterial = wake ? _assets.Wake : _assets.Tracer;
            _trace.widthMultiplier = wake ? _traceWidth * 1.32f : _traceWidth;
            _trace.startColor = Color.white;
            _trace.endColor = new Color(1f, 1f, 1f, 0f);
        }

        private static void ConfigureUnlitRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static T GetOrAdd<T>(Transform slot) where T : Component
        {
            return slot.TryGetComponent<T>(out var component) ? component : slot.gameObject.AddComponent<T>();
        }

        internal sealed class Assets
        {
            private const string Folder = "Projectile Forms/";

            internal readonly Material Metal = Resources.Load<Material>(Folder + "Ballistic Metals");
            internal readonly Material Tracer = Resources.Load<Material>(Folder + "Flight Tracer");
            internal readonly Material Wake = Resources.Load<Material>(Folder + "Powder Wake");
            internal readonly Material Halo = Resources.Load<Material>(Folder + "Flight Halo");
            internal readonly Mesh HaloQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            internal readonly MaterialPropertyBlock Properties = new();
            internal readonly Mesh[] Forms =
            {
                Load("Compliance"), Load("Termination"), Load("Deadline"), Load("Directive"), Load("Sentinel"),
                Load("Drone"), Load("Blast"), Load("Slug"), Load("Knife Wave")
            };

            private static Mesh Load(string name)
            {
                return Resources.Load<GameObject>(Folder + name).GetComponentInChildren<MeshFilter>().sharedMesh;
            }
        }
    }
}
