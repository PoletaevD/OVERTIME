using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatTarget : TemporalActor<int>, ICombatDamageable
    {
        private static readonly int s_color = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CombatHealth _health;
        [SerializeField] private GameObject _intact;
        [SerializeField] private GameObject _wreck;
        [SerializeField] private Collider _body;
        [SerializeField] private Collider _weakPoint;
        [SerializeField] private Renderer _surface;
        [SerializeField, Min(1f)] private float _weakPointMultiplier = 2f;
        [SerializeField, Min(0)] private int _rewardId;

        private MaterialPropertyBlock _properties;
        private Tween _flash;
        private Color _baseColor;
        private Color _hitColor;
        private float _flashPeak;

        internal CombatHealth Health => _health;
        internal int RewardId => _rewardId;

        int ICombatDamageable.RewardId => RewardId;
        CombatHealth ICombatDamageable.Health => Health;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _baseColor = _surface.sharedMaterial.GetColor(s_color);
            _flash = DOVirtual.Float(1f, 0f, 0.14f, value => PresentFlash(value * _flashPeak)).SetUpdate(true).SetAutoKill(false).SetEase(Ease.OutQuad).Pause();
        }

        private void OnDestroy()
        {
            _flash?.Kill();
        }

        DamageResult ICombatDamageable.Hit(Collider collider, float amount, bool reducedMotion, out bool weakPoint)
        {
            return Hit(collider, amount, reducedMotion, out weakPoint);
        }

        internal DamageResult Hit(Collider collider, float amount, bool reducedMotion, out bool weakPoint)
        {
            weakPoint = _weakPoint != null && collider == _weakPoint;
            if (collider != _body && !weakPoint)
            {
                return default;
            }

            var result = _health.ApplyDamage(Mathf.RoundToInt(amount * (weakPoint ? _weakPointMultiplier : 1f)));
            if (result.Amount > 0)
            {
                ApplyAlive();
                if (!result.Killed)
                {
                    _hitColor = weakPoint ? new Color(1f, 0.71f, 0.35f) : new Color(0.52f, 1f, 0.96f);
                    _flashPeak = reducedMotion ? 0.35f : 0.85f;
                    PresentFlash(_flashPeak);
                    _flash.Restart();
                }
            }

            return result;
        }

        internal override void CollectColliders(List<Collider> colliders)
        {
            colliders.Add(_body);
            if (_weakPoint != null)
            {
                colliders.Add(_weakPoint);
            }
        }

        internal override void Simulate(float step)
        {
        }

        internal override void ResetState()
        {
            RestoreState(_health.Maximum);
        }

        private protected override int CaptureState()
        {
            return _health.Current;
        }

        private protected override void RestoreState(in int snapshot)
        {
            _health.Restore(snapshot);
            _flash.Pause();
            PresentFlash(0f);
            ApplyAlive();
        }

        private void ApplyAlive()
        {
            if (_intact.activeSelf != _health.Alive)
            {
                _intact.SetActive(_health.Alive);
                _wreck.SetActive(!_health.Alive);
            }
        }

        private void PresentFlash(float intensity)
        {
            _properties.SetColor(s_color, Color.Lerp(_baseColor, _hitColor, intensity));
            _surface.SetPropertyBlock(_properties);
        }
    }
}
