using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class TemporalHud : MonoBehaviour
    {
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private RectTransform _track;
        [SerializeField] private RectTransform _recorded;
        [SerializeField] private RectTransform _depth;
        [SerializeField] private RectTransform _cursor;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private CanvasGroup _activeLabel;

        private readonly Color _ready = new(0.44f, 0.83f, 0.91f);
        private readonly Color _atStart = new(1f, 0.56f, 0.31f);
        private Tween _appearance;
        private bool _wasAtStart;

        private void OnEnable()
        {
            _director.ResetChanged += OnResetChanged;
        }

        private void OnDisable()
        {
            _director.ResetChanged -= OnResetChanged;
            _appearance?.Kill();
        }

        private void LateUpdate()
        {
            var timeline = _director.PlayerTimeline;
            if (timeline == null)
            {
                return;
            }

            var active = _director.IsRewinding;
            var start = active && timeline.AtStart;
            var window = (timeline.Capacity - 1) * TemporalTimeline.TickDuration;
            var available = (float)(timeline.Duration / window);
            var depth = (float)(timeline.Depth / window);
            var width = _track.rect.width;
            _recorded.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * available);
            _depth.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * depth);
            _cursor.anchoredPosition = Vector2.left * (width * depth);

            _status.SetText(active ? (start ? "HISTORY START" : "RESET ACTIVE") : (timeline.Count > 1 ? "HISTORY READY" : "RECORDING"));
            _status.color = start ? _atStart : _ready;
            if (active)
            {
                _time.SetText("−{0:2} / {1:2} s", (float)timeline.Depth, (float)timeline.Duration);
            }
            else
            {
                _time.SetText("{0:2} s", (float)timeline.Duration);
            }

            if (start && !_wasAtStart && !_cameraRig.ReducedMotion)
            {
                _appearance?.Kill();
                _activeLabel.alpha = 0.58f;
                FadeTo(1f, 0.28f);
            }

            _wasAtStart = start;
        }

        private void OnResetChanged(bool active)
        {
            _appearance?.Kill();
            FadeTo(active ? 1f : 0f, active ? 0.14f : 0.22f);
        }

        private void FadeTo(float alpha, float duration)
        {
            _appearance = DOTween.To(() => _activeLabel.alpha, value => _activeLabel.alpha = value, alpha, duration).SetUpdate(true).SetEase(Ease.OutCubic);
        }
    }
}
