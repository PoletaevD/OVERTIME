using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class KnifePresentation : MonoBehaviour
    {
        [SerializeField] private Transform _blade;
        [SerializeField] private TrailRenderer _trail;

        private PlaygroundPlayer _player;
        private KnifeAttack _knife;
        private bool _liveSwing;
        private float _swingAccent;
        private Tween _accent;

        private void OnEnable()
        {
            _player = GetComponentInParent<PlaygroundPlayer>();
            _knife = _player.Combat.Knife;
            _knife.Started += Begin;
            _trail.emitting = false;
            _blade.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _knife.Started -= Begin;
            _accent?.Kill();
            _trail.Clear();
        }

        private void Begin()
        {
            _liveSwing = true;
            _trail.Clear();
            _accent?.Kill();
            _swingAccent = 0f;
            if (!_player.ReducedMotion)
            {
                _accent = DOVirtual.Float(1f, 0f, 0.23f, value => _swingAccent = value).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }

        private void LateUpdate()
        {
            if (!_knife.Active || _player.CameraRig.IsRewinding)
            {
                _liveSwing = false;
                _trail.emitting = false;
                _blade.gameObject.SetActive(false);
                return;
            }

            var seconds = _knife.State.Elapsed;
            var windup = _knife.Tuning.Windup;
            var sweep = DOVirtual.EasedValue(0f, 1f, Mathf.InverseLerp(windup - 0.035f, windup + 0.095f, seconds), Ease.InOutQuad);
            var enter = Mathf.Clamp01(seconds / 0.06f);
            var leave = Mathf.Clamp01((_knife.Tuning.Duration - seconds) / 0.15f);
            var height = Mathf.Min(enter, leave);
            var right = Mathf.Lerp(0.33f, -0.19f, sweep);
            _blade.gameObject.SetActive(true);
            _blade.localPosition = new Vector3(right, -0.38f + height * 0.27f, 0.32f + Mathf.Sin(sweep * Mathf.PI) * 0.13f);
            _blade.localRotation = Quaternion.Euler(-18f - _swingAccent * 7f, Mathf.Lerp(-50f, 72f, sweep), Mathf.Lerp(-72f, 56f, sweep));
            _trail.emitting = _liveSwing && !_player.ReducedMotion && sweep > 0.06f && sweep < 0.98f;
        }
    }
}
