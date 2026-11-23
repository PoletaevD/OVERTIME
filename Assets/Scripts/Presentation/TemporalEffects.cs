using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class TemporalEffects : MonoBehaviour
    {
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private Volume _volume;
        [SerializeField] private CanvasGroup _pulse;

        private Tween _gradeTween;
        private Tween _pulseTween;

        private void OnEnable()
        {
            _director.ResetChanged += OnResetChanged;
        }

        private void OnDisable()
        {
            _director.ResetChanged -= OnResetChanged;
            _gradeTween?.Kill();
            _pulseTween?.Kill();
            if (_volume != null)
            {
                _volume.weight = 0f;
            }

            if (_pulse != null)
            {
                _pulse.alpha = 0f;
            }
        }

        private void LateUpdate()
        {
            if (_cameraRig.ReducedMotion && _pulse.alpha > 0f)
            {
                _pulseTween?.Kill();
                _pulse.alpha = 0f;
            }
        }

        private void OnResetChanged(bool active)
        {
            _gradeTween?.Kill();
            _pulseTween?.Kill();
            _gradeTween = DOTween.To(() => _volume.weight, value => _volume.weight = value, active ? 1f : 0f, active ? 0.14f : 0.3f).SetUpdate(true).SetEase(Ease.OutCubic);

            if (active && !_cameraRig.ReducedMotion)
            {
                _pulseTween = DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 1f, 0.06f).SetEase(Ease.OutQuad))
                    .Append(DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 0f, 0.24f).SetEase(Ease.OutCubic));
            }
            else
            {
                _pulseTween = DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 0f, 0.16f).SetUpdate(true);
            }
        }
    }
}
