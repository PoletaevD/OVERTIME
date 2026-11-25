using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class EncounterGate : MonoBehaviour
    {
        [SerializeField] private Collider _barrier;
        [SerializeField] private Transform _panel;
        [SerializeField] private bool _waitForClearance;
        [SerializeField] private Vector3 _travel = new(0f, 3.5f, 0f);

        private Tween _slide;
        private Vector3 _closed;
        private bool _initialized;
        private float _clearanceRemaining;

        internal void SetOpen(bool open, bool reducedMotion)
        {
            if (!_initialized)
            {
                _closed = _panel.localPosition;
                _initialized = true;
            }

            _barrier.enabled = !open || (_waitForClearance && !reducedMotion);
            _slide?.Kill();
            _clearanceRemaining = open && _waitForClearance && !reducedMotion ? 0.32f : 0f;
            var position = open ? _closed + _travel : _closed;
            if (reducedMotion)
            {
                _panel.localPosition = position;
                return;
            }

            _slide = _panel.DOLocalMove(position, 0.32f).SetUpdate(true).SetEase(Ease.OutCubic);
        }

        private void Update()
        {
            if (_clearanceRemaining <= 0f)
            {
                return;
            }

            _clearanceRemaining -= Time.unscaledDeltaTime;
            if (_clearanceRemaining <= 0f)
            {
                _barrier.enabled = false;
            }
        }

        private void OnDestroy()
        {
            _slide?.Kill();
        }
    }
}
