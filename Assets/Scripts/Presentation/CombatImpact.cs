using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatImpact : MonoBehaviour
    {
        private static readonly int s_color = Shader.PropertyToID("_BaseColor");
        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private PlaygroundPlayer _player;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private Renderer[] _sparks;

        private Impact[] _pool;
        private MaterialPropertyBlock _properties;
        private int _next;
        private DischargeParticles _debris;
        private DischargeParticles _hostileDebris;

        private void Awake()
        {
            _debris = new DischargeParticles(transform, gameObject.layer, 144);
            _hostileDebris = new DischargeParticles(transform, gameObject.layer, 48);
            _properties = new MaterialPropertyBlock();
            _pool = new Impact[_sparks.Length];
            for (var index = 0; index < _pool.Length; index++)
            {
                _pool[index] = new Impact(_sparks[index]);
            }
        }

        private void OnEnable()
        {
            _director.ProjectileHit += OnImpact;
            _director.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            _director.ProjectileHit -= OnImpact;
            _director.ModeChanged -= OnModeChanged;
            Clear();
        }

        private void OnDestroy()
        {
            foreach (var impact in _pool)
            {
                impact.Tween.Kill();
            }
        }

        private void OnImpact(ProjectileImpact shot)
        {
            var impact = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            var color = shot.Explosion ? new Color(2.2f, 0.55f, 0.16f) : shot.Ricochet ? new Color(1f, 0.76f, 0.32f) : shot.Piercing ? new Color(0.43f, 0.74f, 1.4f) : shot.Shielded ? new Color(0.42f, 0.63f, 1f) : !shot.FromPlayer ? new Color(1f, 0.48f, 0.19f) : shot.Damage.Killed ? new Color(1f, 0.37f, 0.16f) : shot.WeakPoint ? new Color(1f, 0.8f, 0.38f) : new Color(0.54f, 0.92f, 1f);
            var count = shot.Explosion || shot.Damage.Killed ? 20 : shot.Shielded || shot.WeakPoint ? 10 : shot.Melee ? 7 : 4;
            var ring = shot.Shielded || shot.Explosion;
            var reflected = shot.Incoming != Vector3.zero ? Vector3.Reflect(shot.Incoming, shot.Normal) : shot.Normal;
            var axis = shot.Piercing ? -shot.Normal : shot.Ricochet ? reflected : Vector3.Slerp(shot.Normal, reflected, 0.4f);
            if (shot.Chilled)
            {
                _debris.Burst(shot.Point, shot.Normal, new Color(0.32f, 0.8f, 1f), 6, 0.8f, 1.45f, 0.066f, true);
            }

            var particles = shot.FromPlayer ? _debris : _hostileDebris;
            particles.Burst(shot.Point + shot.Normal * 0.02f, axis, color, _player.ReducedMotion ? 3 : count, shot.Ricochet ? 0.24f : shot.Melee ? 2.5f : 1.2f, shot.Explosion ? 6f : 3.1f, shot.Explosion ? 0.085f : 0.042f, ring);
            _properties.SetColor(s_color, color);
            _properties.SetColor(s_emission, color * (_player.ReducedMotion ? 0.7f : 2f));
            impact.Show(shot.Point + shot.Normal * 0.025f, shot.Normal, _properties, (shot.Damage.Killed || shot.Explosion) && !_player.ReducedMotion);
        }

        private void OnModeChanged(TemporalMode mode)
        {
            _hostileDebris.SetRate(_director.WorldTimeline?.Rate ?? 1f);
            if (!_director.CanDamageWorld)
            {
                Clear();
            }
        }

        private void Clear()
        {
            _debris.Clear();
            _hostileDebris.Clear();
            foreach (var impact in _pool)
            {
                impact.Tween.Pause();
                impact.Renderer.enabled = false;
            }
        }

        private sealed class Impact
        {
            private float _size;

            internal Renderer Renderer { get; }
            internal Tween Tween { get; }

            internal Impact(Renderer renderer)
            {
                Renderer = renderer;
                Tween = DOVirtual.Float(1f, 0f, 0.15f, Present).SetUpdate(true).SetAutoKill(false).SetEase(Ease.InQuad).Pause();
            }

            internal void Show(Vector3 point, Vector3 normal, MaterialPropertyBlock properties, bool killed)
            {
                _size = killed ? 0.33f : 0.13f;
                Renderer.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.forward, normal));
                Renderer.SetPropertyBlock(properties);
                Present(1f);
                Tween.Restart();
            }

            private void Present(float amount)
            {
                Renderer.enabled = amount > 0f;
                Renderer.transform.localScale = new Vector3(1f, 0.66f, 0.34f) * (amount * _size);
            }
        }
    }
}
