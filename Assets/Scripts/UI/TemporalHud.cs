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
        [SerializeField] private TMP_Text _playerReadout;
        [SerializeField] private TMP_Text _worldReadout;
        [SerializeField] private RectTransform _worldTrack;

        private Tween _appearance;
        private readonly RawImage[] _glyphs = new RawImage[4];
        private RectTransform _worldRecorded;
        private RectTransform _worldDepth;
        private RectTransform _worldCursor;
        private Image _playerDepthImage;
        private Image _worldDepthImage;
        private Image _accent;
        private bool _historyStart;
        private bool _fieldDisplay;

        private void Awake()
        {
            _fieldDisplay = GetComponentInParent<CombatHud>() != null && GetComponentInParent<SectorHud>() != null;
            if (_fieldDisplay)
            {
                _status.fontStyle |= FontStyles.Bold | FontStyles.Italic;
                _time.fontStyle |= FontStyles.Bold;
            }

            var atlas = Resources.Load<Texture2D>("Temporal Glyphs");
            var binding = _worldTrack.parent.Find("Temporal Bindings");
            if (binding != null && atlas != null)
            {
                binding.gameObject.SetActive(false);
                var labels = new[] { "Q   RESET", "E   OVERRIDE", "C   RECALL", "SHIFT   SLOW" };
                for (var index = 0; index < _glyphs.Length; index++)
                {
                    var glyph = new GameObject(labels[index], typeof(RectTransform)).AddComponent<RawImage>();
                    glyph.rectTransform.SetParent(_worldTrack.parent, false);
                    glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = Vector2.zero;
                    glyph.rectTransform.anchoredPosition = new Vector2(index * 96f + 49f, -1f);
                    glyph.rectTransform.sizeDelta = new Vector2(42f, 42f);
                    glyph.texture = atlas;
                    glyph.uvRect = new Rect(index % 2 * 0.5f, index < 2 ? 0.5f : 0f, 0.5f, 0.5f);
                    glyph.raycastTarget = false;
                    _glyphs[index] = glyph;
                    var key = new GameObject("Binding", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    key.rectTransform.SetParent(glyph.transform, false);
                    key.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                    key.rectTransform.sizeDelta = new Vector2(94f, 22f);
                    key.font = _status.font;
                    key.fontSize = 12.5f;
                    key.fontStyle = FontStyles.Bold;
                    key.alignment = TextAlignmentOptions.Center;
                    key.raycastTarget = false;
                    key.SetText(labels[index]);
                }
            }

            _playerDepthImage = _depth.GetComponent<Image>();
            _accent = _activeLabel.GetComponent<Image>();
            _worldRecorded = (RectTransform)_worldTrack.Find("Recorded History");
            _worldDepth = (RectTransform)_worldTrack.Find("Rewind Depth");
            _worldCursor = (RectTransform)_worldTrack.Find("Timeline Cursor");
            _worldDepthImage = _worldDepth.GetComponent<Image>();
        }

        private void OnEnable()
        {
            _director.ModeChanged += OnModeChanged;
            RefreshMode(_director.Mode);
        }

        private void OnDisable()
        {
            _director.ModeChanged -= OnModeChanged;
            _appearance?.Kill();
        }

        private void LateUpdate()
        {
            var player = _director.PlayerTimeline;
            var world = _director.WorldTimeline;
            if (player == null)
            {
                return;
            }

            var historyStart = _director.Mode == TemporalMode.Reset && player.AtStart && world.AtStart;
            if (_historyStart != historyStart)
            {
                _historyStart = historyStart;
                RefreshMode(_director.Mode);
            }

            UpdateTrack(player, _track, _recorded, _depth, _cursor);
            UpdateTrack(world, _worldTrack, _worldRecorded, _worldDepth, _worldCursor);
            UpdateReadout(player, _playerReadout, true);
            UpdateReadout(world, _worldReadout, false);
            if (_fieldDisplay)
            {
                _time.SetText("{0:1} s", (float)(world.Duration - world.Depth));
            }
            else
            {
                _time.SetText("P-W  {0:2} s", (float)(player.Position - world.Position));
            }
        }

        private void UpdateTrack(TemporalTimeline timeline, RectTransform track, RectTransform recorded, RectTransform depth, RectTransform cursor)
        {
            var window = (timeline.Capacity - 1) * TemporalTimeline.TickDuration;
            var width = track.rect.width;
            var position = width * (float)(timeline.Depth / window);
            recorded.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * (float)(timeline.Duration / window));
            depth.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, position);
            cursor.anchoredPosition = Vector2.left * position;
        }

        private void UpdateReadout(TemporalTimeline timeline, TMP_Text readout, bool player)
        {
            var history = (float)timeline.Duration;
            if (_fieldDisplay)
            {
                var available = Mathf.Max(0f, history - (float)timeline.Depth);
                readout.SetText(player ? "PERSONAL HISTORY   {0:1} s" : "WORLD HISTORY   {0:1} s", available);
                return;
            }

            if (timeline.Direction == TemporalDirection.Reverse)
            {
                readout.SetText(player ? "PLAYER  BACK {0:2}x   -{1:2} / {2:2} s" : "WORLD   BACK {0:2}x   -{1:2} / {2:2} s", timeline.Rate, (float)timeline.Depth, history);
            }
            else if (timeline.Rate != 1f)
            {
                readout.SetText(player ? "PLAYER  FWD {0:2}x   {1:2} s" : "WORLD   FWD {0:2}x   {1:2} s", timeline.Rate, history);
            }
            else
            {
                readout.SetText(player ? "PLAYER  FWD   {0:2} s" : "WORLD   FWD   {0:2} s", history);
            }
        }

        private void OnModeChanged(TemporalMode mode)
        {
            _historyStart = false;
            RefreshMode(mode);
            _appearance?.Kill();
            var alpha = mode == TemporalMode.Normal ? 0f : 1f;
            if (_cameraRig.ReducedMotion)
            {
                _activeLabel.alpha = alpha;
                return;
            }

            _appearance = DOTween.To(() => _activeLabel.alpha, value => _activeLabel.alpha = value, alpha, 0.18f).SetUpdate(true).SetEase(Ease.OutCubic);
        }

        private void RefreshMode(TemporalMode mode)
        {
            var color = TemporalEffects.ModeColor(mode);
            _status.SetText(mode switch
            {
                TemporalMode.Reset => _historyStart ? "RESET / HISTORY START" : "RESET",
                TemporalMode.Override => "OVERRIDE",
                TemporalMode.Recall => "RECALL",
                TemporalMode.BulletTime => "BULLET TIME",
                _ => _fieldDisplay ? "CHRONO BUFFER" : "NORMAL"
            });
            for (var index = 0; index < _glyphs.Length; index++)
            {
                if (_glyphs[index] != null)
                {
                    _glyphs[index].color = (int)mode == index + 1 ? color : new Color(0.86f, 0.90f, 0.93f);
                }
            }

            _status.color = color;
            _playerDepthImage.color = color;
            _worldDepthImage.color = color;
            _accent.color = color;
        }
    }
}
