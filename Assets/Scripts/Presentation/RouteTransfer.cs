using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class RouteTransfer : MonoBehaviour
    {
        internal const float CloseSeconds = 0.24f;
        internal const float OpenSeconds = 0.18f;

        [SerializeField] private CanvasGroup _overlay;

        private Tween _fade;

        internal void Fade(bool closed, bool reducedMotion)
        {
            _fade?.Kill();
            var alpha = closed ? 1f : 0f;
            if (reducedMotion)
            {
                _overlay.alpha = alpha;
                return;
            }

            _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, alpha, closed ? CloseSeconds : OpenSeconds).SetEase(Ease.InOutQuad).SetUpdate(true);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
        }
    }
}
