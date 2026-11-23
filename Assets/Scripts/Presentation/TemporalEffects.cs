using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class TemporalEffects : MonoBehaviour
    {
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private Volume _volume;
        [SerializeField] private CanvasGroup _pulse;
        [SerializeField] private LineRenderer _playerTrace;

        private ColorAdjustments _grade;
        private VolumeProfile _ownedProfile;
        private Image _tint;
        private Tween _gradeTween;
        private Tween _pulseTween;

        private void Awake()
        {
            var alreadyInstantiated = _volume.HasInstantiatedProfile();
            var profile = _volume.profile;
            if (!alreadyInstantiated)
            {
                _ownedProfile = profile;
            }

            profile.TryGet(out _grade);
            _tint = _pulse.GetComponent<Image>();
        }

        private void OnEnable()
        {
            _director.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            _director.ModeChanged -= OnModeChanged;
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

            if (_playerTrace != null)
            {
                _playerTrace.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_cameraRig.ReducedMotion && _pulse.alpha > 0f)
            {
                _pulseTween?.Kill();
                _pulse.alpha = 0f;
            }

            if (_playerTrace != null && _director.PlayerTimeline != null)
            {
                _director.PresentPlayerTrace(_playerTrace);
            }
        }

        private void OnDestroy()
        {
            if (_ownedProfile == null)
            {
                return;
            }

            if (_volume != null)
            {
                _volume.profile = null;
            }

            foreach (var component in _ownedProfile.components)
            {
                Destroy(component);
            }

            Destroy(_ownedProfile);
        }

        private void OnModeChanged(TemporalMode mode)
        {
            _gradeTween?.Kill();
            _pulseTween?.Kill();
            if (mode != TemporalMode.Normal && _grade != null)
            {
                _grade.colorFilter.Override(mode switch
                {
                    TemporalMode.Override => new Color(0.89f, 0.77f, 1f),
                    TemporalMode.Recall => new Color(1f, 0.88f, 0.73f),
                    TemporalMode.BulletTime => new Color(0.84f, 1f, 0.94f),
                    _ => new Color(0.73f, 0.94f, 1f)
                });
                _grade.saturation.Override(mode == TemporalMode.BulletTime ? -26f : -13f);
            }

            var active = mode != TemporalMode.Normal;
            var weight = active ? 1f : 0f;
            if (_cameraRig.ReducedMotion)
            {
                _volume.weight = weight;
                _pulse.alpha = 0f;
                return;
            }

            _gradeTween = DOTween.To(() => _volume.weight, value => _volume.weight = value, weight, active ? 0.14f : 0.3f).SetUpdate(true).SetEase(Ease.OutCubic);
            if (mode == TemporalMode.Override)
            {
                _pulse.alpha = 0f;
            }
            else if (active)
            {
                var tint = ModeColor(mode);
                tint.a = mode == TemporalMode.BulletTime ? 0.08f : 0.13f;
                _tint.color = tint;
                _pulseTween = DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 1f, 0.06f).SetEase(Ease.OutQuad))
                    .Append(DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 0f, 0.24f).SetEase(Ease.OutCubic));
            }
            else
            {
                _pulseTween = DOTween.To(() => _pulse.alpha, value => _pulse.alpha = value, 0f, 0.16f).SetUpdate(true);
            }
        }

        internal static Color ModeColor(TemporalMode mode)
        {
            return mode switch
            {
                TemporalMode.Override => new Color(0.76f, 0.53f, 1f),
                TemporalMode.Recall => new Color(1f, 0.62f, 0.32f),
                TemporalMode.BulletTime => new Color(0.36f, 0.94f, 0.73f),
                _ => new Color(0.44f, 0.83f, 0.91f)
            };
        }
    }
}
