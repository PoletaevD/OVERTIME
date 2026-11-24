using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class PlaygroundHud : MonoBehaviour
    {
        [SerializeField] private PlaygroundInput _input;
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private RectTransform _movementPanel;
        [SerializeField] private RectTransform[] _reticleArms;
        [SerializeField] private RectTransform _chargeReticle;
        [SerializeField] private Image[] _chargeTicks;
        [SerializeField] private Image _gravityIndicator;
        [SerializeField] private Image _speedFill;
        [SerializeField] private TMP_Text _speed;
        [SerializeField] private TMP_Text _state;
        [SerializeField] private TMP_Text _gravity;
        [SerializeField] private TMP_Text _debug;
        [SerializeField] private CanvasGroup _hints;
        [SerializeField] private GameObject _debugPanel;
        [SerializeField] private TemporalDirector _temporalDirector;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _swayIntensity = 1.9f;
        [SerializeField, Min(0f)] private float _bobIntensity = 3.1f;
        [SerializeField, Min(0f)] private float _crosshairMovement = 6f;
        [SerializeField, Min(0f)] private float _crosshairJump = 4f;
        [SerializeField, Min(0f)] private float _crosshairRecoil = 5f;
        [SerializeField, Min(0f)] private float _landingFeedback = 6f;
        [SerializeField, Range(0f, 1f)] private float _gravityFeedback = 0.95f;
        [SerializeField, Min(0.1f)] private float _minimumPanelScale = 0.53f;
        [SerializeField] private bool _reducedMotion;

        private Canvas _canvas;
        private Camera _aimView;
        private Transform _reticle;
        private Vector2 _panelOrigin;
        private Vector2 _sway;
        private float _landing;
        private float _gravityPulse;
        private float _shot;
        private float _jump;
        private float _gap = 8f;
        private float _nextReadout;
        private float _fillAmount;
        private Tween _landingTween;
        private Tween _gravityTween;
        private Tween _shotTween;
        private CombatWeapon _weapon;
        private Tween _jumpTween;
        private Tween _hintsTween;
        private bool _showHints = true;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _reticle = _reticleArms[0].parent;
            _panelOrigin = _movementPanel.anchoredPosition;
            _cameraRig.ReducedMotion = _reducedMotion;
            var player = GetComponentInParent<PlaygroundPlayer>();
            _weapon = player?.Combat?.Weapon;
            _aimView = player?.GetComponentInChildren<Camera>();
            _shotTween = DOTween.To(() => _shot, value => _shot = value, 0f, 0.18f).From(_crosshairRecoil, false).SetUpdate(true).SetAutoKill(false).SetEase(Ease.OutCubic).Pause();
        }

        private void OnEnable()
        {
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _motor.GravityChanged += OnGravityChanged;
            if (_weapon != null)
            {
                _weapon.ShotFired += OnShot;
            }
        }

        private void OnDisable()
        {
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _motor.GravityChanged -= OnGravityChanged;
            _landingTween?.Kill();
            _gravityTween?.Kill();
            if (_weapon != null)
            {
                _weapon.ShotFired -= OnShot;
            }

            _shotTween?.Pause();
            _shot = 0f;
            _jumpTween?.Kill();
            _hintsTween?.Kill();
        }

        private void OnDestroy()
        {
            _shotTween?.Kill();
        }

        private void OnShot()
        {
            _shot = _crosshairRecoil;
            _shotTween.Restart();
        }

        private void LateUpdate()
        {
            ProcessInput();
            UpdatePanelScale();
            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
            var motion = _reducedMotion ? 0f : 1f;
            _sway = Vector2.Lerp(_sway, -Vector2.ClampMagnitude(_cameraRig.LookDelta, 5f) * _swayIntensity, blend);
            var bob = Mathf.Sin(_cameraRig.BobPhase * 2f) * _bobIntensity * _cameraRig.MovementWeight;
            _movementPanel.anchoredPosition = _panelOrigin + motion * (_sway + Vector2.up * (bob - _landing));
            PresentReticle(motion, blend);

            var color = _motor.Magnetic ? new Color(0.27f, 0.88f, 0.90f) : new Color(1f, 0.42f, 0.17f);
            _gravityIndicator.color = Color.Lerp(color, Color.white, Mathf.Max(_gravityPulse * _gravityFeedback, _shot * 0.05f) * (0.35f + 0.65f * motion));
            _fillAmount = Mathf.Lerp(_fillAmount, _motor.SpeedRatio, blend);
            _speedFill.rectTransform.localScale = new Vector3(_fillAmount, 1f, 1f);
            if (Time.unscaledTime >= _nextReadout)
            {
                RefreshReadout();
            }
        }

        private void PresentReticle(float motion, float blend)
        {
            var type = _weapon?.ActiveType ?? WeaponType.Carbine;
            var charged = type == WeaponType.Charged && _chargeReticle != null;
            var gap = type == WeaponType.Scatter ? SpreadGap() : 8f;
            _gap = Mathf.Lerp(_gap, gap + motion * (_motor.SpeedRatio * _crosshairMovement + _jump + _shot), blend);
            _reticleArms[0].anchoredPosition = Vector2.left * _gap;
            _reticleArms[1].anchoredPosition = Vector2.right * _gap;
            _reticleArms[2].anchoredPosition = Vector2.up * _gap;
            _reticleArms[3].anchoredPosition = Vector2.down * _gap;
            foreach (var arm in _reticleArms)
            {
                if (arm.gameObject.activeSelf == charged)
                {
                    arm.gameObject.SetActive(!charged);
                }
            }

            if (_chargeReticle == null)
            {
                return;
            }

            if (_chargeReticle.gameObject.activeSelf != charged)
            {
                _chargeReticle.gameObject.SetActive(charged);
            }

            if (charged)
            {
                var filled = Mathf.CeilToInt(_weapon.ChargeProgress * _chargeTicks.Length);
                for (var index = 0; index < _chargeTicks.Length; index++)
                {
                    _chargeTicks[index].color = index < filled ? new Color(0.27f, 0.88f, 0.90f) : new Color(0.57f, 0.76f, 0.82f, 0.25f);
                }
            }
        }

        private float SpreadGap()
        {
            var spread = _weapon.Tuning(_weapon.ActiveSlot).Spread;
            if (_aimView == null)
            {
                return Mathf.Max(8f, spread * 8.5f);
            }

            var verticalScale = 0.5f * Screen.height / Mathf.Tan(_aimView.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var reticleScale = Mathf.Max(0.01f, _canvas.scaleFactor * _reticle.localScale.y);
            return Mathf.Max(8f, verticalScale * Mathf.Tan(spread * Mathf.Deg2Rad) / reticleScale);
        }

        private void UpdatePanelScale()
        {
            var minimum = Mathf.Min(_minimumPanelScale, Mathf.Min(Screen.width / 880f, Screen.height / 520f));
            var scale = Mathf.Max(1f, minimum / Mathf.Max(_canvas.scaleFactor, 0.01f));
            var localScale = Vector3.one * scale;
            if (_movementPanel.localScale == localScale)
            {
                return;
            }

            _movementPanel.localScale = localScale;
            _hints.transform.localScale = localScale;
            _debugPanel.transform.localScale = localScale;
            _reticle.localScale = localScale;
        }

        private void ProcessInput()
        {
            if (_weapon == null && _input.FirePressed)
            {
                OnShot();
            }

            if (_input.DebugPressed)
            {
                _debugPanel.SetActive(!_debugPanel.activeSelf);
            }

            if (_input.HintsPressed)
            {
                _showHints = !_showHints;
                _hintsTween?.Kill();
                _hintsTween = DOTween.To(() => _hints.alpha, value => _hints.alpha = value, _showHints ? 1f : 0f, 0.22f).SetUpdate(true);
            }

            if (_input.ReducedMotionPressed)
            {
                _reducedMotion = !_reducedMotion;
                _cameraRig.ReducedMotion = _reducedMotion;
            }
        }

        private void RefreshReadout()
        {
            _nextReadout = Time.unscaledTime + 0.1f;
            _speed.SetText("{0:1} <size=50%>M/S</size>", _motor.Speed);
            _state.SetText(_motor.Grounded ? (_motor.Magnetic ? "ATTACHED  /  MAG LOCK" : "GROUNDED  /  STANDARD") : (_motor.Magnetic ? "AIRBORNE  /  MAG ACTIVE" : "AIRBORNE  /  STANDARD"));
            var up = _motor.Up;
            _gravity.SetText("UP  [{0:1}  {1:1}  {2:1}]", up.x, up.y, up.z);

            if (_debugPanel.activeSelf)
            {
                var readout = $"MOTOR TELEMETRY\nGravity up  {up:F2}\nCurrent surface  {_motor.Surface?.name ?? "-"}\nManual candidate  {_motor.ManualCandidate?.name ?? "-"}\nManual attachment  {_motor.ManualAttachmentProgress:P0}\nAutomatic candidate  {_motor.AutomaticCandidate?.name ?? "-"}\nJump detached  {_motor.JumpInFlight}    Rotating  {_motor.Transitioning}\nTransition  {_motor.CurrentTransitionReason}\nVelocity  {_motor.Velocity:F2}    Collision  {_motor.Obstructed}";
                if (_temporalDirector != null)
                {
                    var player = _temporalDirector.PlayerTimeline;
                    var world = _temporalDirector.WorldTimeline;
                    var collision = _temporalDirector.IsPlayerProtected ? "Playback / protected" : _motor.Obstructed ? "Obstructed" : "Active";
                    readout += $"\n\nTEMPORAL TELEMETRY  /  {_temporalDirector.Mode}\nPlayer  {player.Direction} {player.Rate:0.##}x  /  Cursor {player.Cursor}\nWorld   {world.Direction} {world.Rate:0.##}x  /  Cursor {world.Cursor}\nHistory P {player.Duration:F2} s / W {world.Duration:F2} s\nDepth   P {player.Depth:F2} s / W {world.Depth:F2} s\nSteps/frame  P {_temporalDirector.PlayerSteps} / W {_temporalDirector.WorldSteps}\nCapsule  {collision}\nDynamic overlap {_temporalDirector.DynamicOverlaps} / Phased {_temporalDirector.PhasedColliders}";
                }

                _debug.SetText(readout);
            }
        }

        private void OnJumped()
        {
            _jumpTween?.Kill();
            _jump = _crosshairJump;
            _jumpTween = DOTween.To(() => _jump, value => _jump = value, 0f, 0.29f).SetUpdate(true);
        }

        private void OnLanded(float strength)
        {
            _landingTween?.Kill();
            _landing = strength * _landingFeedback;
            _landingTween = DOTween.To(() => _landing, value => _landing = value, 0f, 0.28f).SetUpdate(true).SetEase(Ease.OutCubic);
        }

        private void OnGravityChanged(Quaternion transport)
        {
            _gravityTween?.Kill();
            _gravityPulse = 1f;
            _gravityTween = DOTween.To(() => _gravityPulse, value => _gravityPulse = value, 0f, 0.42f).SetUpdate(true).SetEase(Ease.OutCubic);
        }
    }
}
