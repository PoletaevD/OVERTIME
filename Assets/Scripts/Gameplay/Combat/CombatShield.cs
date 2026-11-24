using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatShield : MonoBehaviour
    {
        private static readonly int s_color = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _surface;

        private MaterialPropertyBlock _properties;
        private Tween _pulse;
        private Color _restColor;
        private float _intensity;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _restColor = _surface.sharedMaterial.GetColor(s_color);
            _pulse = DOVirtual.Float(1f, 0f, 0.18f, Present).SetUpdate(true).SetAutoKill(false).SetEase(Ease.OutQuad).Pause();
        }

        private void OnDestroy()
        {
            _pulse?.Kill();
        }

        internal void React(bool reducedMotion)
        {
            _intensity = reducedMotion ? 0.45f : 0.92f;
            Present(1f);
            _pulse.Restart();
        }

        internal void Clear()
        {
            _pulse.Pause();
            Present(0f);
        }

        private void Present(float amount)
        {
            _properties.SetColor(s_color, Color.Lerp(_restColor, new Color(0.32f, 0.70f, 1f), amount * _intensity));
            _surface.SetPropertyBlock(_properties);
        }
    }
}
