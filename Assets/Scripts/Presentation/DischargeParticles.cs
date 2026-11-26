using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class DischargeParticles
    {
        private readonly ParticleSystem _particles;
        private int _phase;

        internal DischargeParticles(Transform parent, int layer, int capacity, float velocityStretch = 0.07f,
            ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.World)
        {
            var host = new GameObject("Discharge Particles");
            host.layer = layer;
            host.transform.SetParent(parent, false);
            _particles = host.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.playOnAwake = false;
            main.maxParticles = capacity;
            main.simulationSpace = simulationSpace;
            main.useUnscaledTime = true;
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;
            var fade = _particles.sizeOverLifetime;
            fade.enabled = true;
            fade.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.04f));
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.5f;
            renderer.velocityScale = velocityStretch;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = Resources.Load<Material>("Meridian Spark Particles");
            _particles.Play();
        }

        internal bool HasParticles => _particles.particleCount > 0;

        // Point and axis are relative to the configured simulation space.
        internal void Burst(Vector3 point, Vector3 axis, Color tint, int count, float spread, float speed, float width, bool ring = false)
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, axis);
            _phase = (_phase + 1) % 32;
            for (var index = 0; index < count; index++)
            {
                var fraction = (index + 0.5f) / count;
                var angle = _phase + index * (ring ? 2f * Mathf.PI / count : 2.39996f);
                var radius = ring ? 1f : Mathf.Sqrt(fraction) * spread;
                var direction = rotation * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, ring ? 0.12f : 1f).normalized;
                var particle = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = direction * speed * (ring ? 1f : 0.52f + fraction),
                    startColor = tint,
                    startSize = width * (1.2f - fraction * 0.38f),
                    startLifetime = ring ? 0.16f : 0.07f + fraction * 0.16f
                };
                _particles.Emit(particle, 1);
            }
        }

        internal void Clear()
        {
            _particles.Clear();
        }

        internal void SetRate(float rate)
        {
            var main = _particles.main;
            main.simulationSpeed = rate;
        }
    }
}
