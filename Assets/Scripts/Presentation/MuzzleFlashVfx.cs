using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class MuzzleFlashVfx
    {
        private const float FlashDuration = 0.072f;
        private const float LightDuration = 0.057f;
        private const float LightPeak = 5.2f;

        private static readonly int s_flash = Shader.PropertyToID("_Flash");
        private static readonly Vector3 s_flashOffset = Vector3.forward * 0.055f;

        private readonly Transform _root;
        private readonly Mesh _quad;
        private readonly MeshRenderer _flare;
        private readonly MeshRenderer _jetA;
        private readonly MeshRenderer _jetB;
        private readonly MaterialPropertyBlock _ink = new();
        private readonly Light _light;
        private readonly DischargeParticles _sparks;
        private Transform _muzzle;
        private float _started;
        private float _roll;
        private bool _reducedMotion;
        private bool _emitSparks;

        internal MuzzleFlashVfx(Transform parent, int layer)
        {
            _root = new GameObject("Player Muzzle Flash VFX").transform;
            _root.SetParent(parent, false);
            _quad = new Mesh
            {
                name = "Muzzle Flash Quad",
                vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            var material = Resources.Load<Material>("Player Muzzle Flash");
            _flare = CreateCard("Flash Core", layer, material);
            _flare.transform.localPosition = s_flashOffset;
            _jetA = CreateCard("Flame Jet A", layer, material);
            _jetB = CreateCard("Flame Jet B", layer, material);
            _sparks = new DischargeParticles(_root, layer, 64, velocityStretch: 0.024f,
                simulationSpace: ParticleSystemSimulationSpace.Local);

            var lamp = new GameObject("Muzzle Light");
            lamp.transform.SetParent(_root, false);
            lamp.transform.localPosition = Vector3.forward * 0.23f;
            _light = lamp.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.69f, 0.38f);
            _light.range = 3.8f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        internal bool IsFlashing => _muzzle != null && (_emitSparks || Time.unscaledTime - _started < FlashDuration);

        internal void Play(Transform muzzle, uint shot, bool reducedMotion)
        {
            _muzzle = muzzle;
            _roll = shot % 24 * 137.508f;
            _reducedMotion = reducedMotion;
            _emitSparks = true;
        }

        internal void Present(Camera camera)
        {
            if (_muzzle == null)
            {
                return;
            }

            // The shared effect sits outside the weapon renderers that are hidden while scoped.
            _root.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
            if (!IsFlashing)
            {
                if (_flare.enabled)
                {
                    HideFlash();
                }

                if (!_sparks.HasParticles)
                {
                    _muzzle = null;
                }

                return;
            }

            if (_emitSparks)
            {
                _started = Time.unscaledTime;
                _sparks.Burst(s_flashOffset, Vector3.forward, new Color(1f, 0.63f, 0.25f),
                    _reducedMotion ? 3 : 12, 0.72f, 3.1f, 0.011f);
                _emitSparks = false;
            }

            var elapsed = Time.unscaledTime - _started;
            var age = elapsed / FlashDuration;
            var scale = (_reducedMotion ? 0.66f : 1f) * (0.91f + 0.22f * age);
            var energy = (1f - age) * (1f - age) * (_reducedMotion ? 0.48f : 1f);
            var phase = _roll * Mathf.Deg2Rad;
            var facing = camera != null ? camera.transform.rotation : _root.rotation;
            _flare.transform.rotation = facing * Quaternion.Euler(0f, 0f, _roll);
            _flare.transform.localScale = new Vector3(0.36f, 0.34f, 1f) * scale;
            Draw(_flare, phase, age, energy, 0f);
            var jetPosition = Vector3.forward * (0.2f * scale);
            var jetScale = new Vector3(0.24f, 0.43f, 1f) * scale;
            DrawJet(_jetA, jetPosition, jetScale, _roll, phase, age, energy);
            DrawJet(_jetB, jetPosition, jetScale, _roll + 90f, phase + 2.4f, age, energy);

            var lightEnergy = Mathf.Clamp01(1f - elapsed / LightDuration);
            _light.intensity = LightPeak * lightEnergy * lightEnergy;
            _light.enabled = !_reducedMotion && lightEnergy > 0f;
        }

        internal void Clear()
        {
            _muzzle = null;
            _emitSparks = false;
            HideFlash();
            _sparks.Clear();
        }

        internal void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        private void HideFlash()
        {
            _flare.enabled = _jetA.enabled = _jetB.enabled = _light.enabled = false;
        }

        private MeshRenderer CreateCard(string name, int layer, Material material)
        {
            var card = new GameObject(name) { layer = layer };
            card.transform.SetParent(_root, false);
            card.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = card.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private void DrawJet(MeshRenderer jet, Vector3 position, Vector3 scale, float roll, float phase, float age, float energy)
        {
            jet.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(90f, 0f, 0f));
            jet.transform.localScale = scale;
            Draw(jet, phase, age, energy, 1f);
        }

        private void Draw(MeshRenderer card, float phase, float age, float energy, float jet)
        {
            _ink.SetVector(s_flash, new Vector4(phase, age, energy, jet));
            card.SetPropertyBlock(_ink);
            card.enabled = true;
        }
    }
}
