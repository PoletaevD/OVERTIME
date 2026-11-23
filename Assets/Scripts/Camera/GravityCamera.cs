using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace Overtime
{
    [DefaultExecutionOrder(-50)]
    internal sealed class GravityCamera : MonoBehaviour
    {
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private Transform _gravityFrame;
        [SerializeField] private Transform _yawPivot;
        [SerializeField] private Transform _pitchPivot;
        [SerializeField] private Transform _effectsPivot;
        [SerializeField, Min(0f)] private float _eyeHeight = 0.57f;
        [SerializeField, Min(0f)] private float _mouseSensitivity = 0.105f;
        [SerializeField, Range(50f, 105f)] private float _fieldOfView = 75f;
        [SerializeField, Min(0f)] private float _bobIntensity = 0.021f;
        [SerializeField, Min(0f)] private float _rollIntensity = 1.4f;
        [SerializeField, Min(0f)] private float _landingResponse = 0.055f;
        [SerializeField, Min(0f)] private float _jumpResponse = 0.024f;
        [SerializeField, Min(0f)] private float _gravityResponse = 0.68f;
        [SerializeField, Min(0f)] private float _fovKick = 2.2f;
        [SerializeField, Min(0.01f)] private float _gravitySmoothing = 0.22f;
        [SerializeField] private bool _motionEffects = true;

        private Quaternion _reference = Quaternion.identity;
        private Quaternion _display = Quaternion.identity;
        private Quaternion _transitionStart = Quaternion.identity;
        private float _transition = 1f;
        private float _yaw;
        private float _pitch;
        private float _phase;
        private float _speed;
        private float _lateral;
        private float _verticalKick;
        private float _gravityKick;
        private float _fovOffset;
        private bool _moving;
        private Tween _verticalTween;
        private Tween _gravityTween;
        private Tween _fovTween;

        internal Vector3 Heading => _reference * (Quaternion.AngleAxis(_yaw, Vector3.up) * Vector3.forward);
        internal Vector2 LookDelta { get; private set; }
        internal float BobPhase => _phase;
        internal float MovementWeight => _speed;
        internal float Lateral => _lateral;
        internal bool ReducedMotion { get; set; }

        private void OnEnable()
        {
            _motor.GravityChanged += OnGravityChanged;
            _motor.Landed += OnLanded;
            _motor.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            _motor.GravityChanged -= OnGravityChanged;
            _motor.Landed -= OnLanded;
            _motor.Jumped -= OnJumped;
            KillTweens();
        }

        internal void ResetView(Quaternion rotation)
        {
            KillTweens();
            _yaw = 0f;
            _pitch = 0f;
            _transition = 1f;
            _reference = rotation;
            _display = rotation;
            _transitionStart = rotation;
            _verticalKick = 0f;
            _gravityKick = 0f;
            _fovOffset = 0f;
            _moving = false;
            _phase = 0f;
            _speed = 0f;
            _lateral = 0f;
            LookDelta = Vector2.zero;
        }

        internal void Look(Vector2 input)
        {
            LookDelta = input * _mouseSensitivity;
            _yaw = Mathf.Repeat(_yaw + LookDelta.x, 360f);
            _pitch = Mathf.Clamp(_pitch - LookDelta.y, -88f, 88f);
        }

        private void LateUpdate()
        {
            var deltaTime = Time.unscaledDeltaTime;
            _transition = Mathf.Min(1f, _transition + deltaTime / _gravitySmoothing);
            _display = Quaternion.Slerp(_transitionStart, _reference, Mathf.SmoothStep(0f, 1f, _transition));

            var blend = 1f - Mathf.Exp(-deltaTime * 12f);
            _speed = Mathf.Lerp(_speed, _motor.Grounded ? _motor.SpeedRatio : 0f, blend);
            _phase = Mathf.Repeat(_phase + deltaTime * Mathf.Lerp(5f, 16f, _motor.SpeedRatio), Mathf.PI * 2f);
            var right = Vector3.Cross(_motor.Up, Heading);
            _lateral = Mathf.Lerp(_lateral, Mathf.Clamp(Vector3.Dot(_motor.Velocity, right) / 9f, -1f, 1f), blend);

            _gravityFrame.SetPositionAndRotation(_motor.transform.position + _display * Vector3.up * _eyeHeight, _display);
            _yawPivot.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);
            _pitchPivot.localRotation = Quaternion.AngleAxis(_pitch, Vector3.right);
            ApplyEffects();
        }

        private void ApplyEffects()
        {
            var weight = _motionEffects && !ReducedMotion ? 1f : 0f;
            var bob = new Vector3(Mathf.Cos(_phase) * 0.38f, Mathf.Sin(_phase * 2f), 0f) * (_speed * _bobIntensity);
            _effectsPivot.localPosition = weight * (bob + Vector3.up * _verticalKick);
            _effectsPivot.localRotation = Quaternion.AngleAxis(weight * (-_lateral * _rollIntensity + _gravityKick), Vector3.forward);

            var moving = _moving ? _motor.SpeedRatio > 0.53f : _motor.SpeedRatio > 0.7f;
            if (moving != _moving)
            {
                _moving = moving;
                _fovTween?.Kill();
                _fovTween = DOTween.To(() => _fovOffset, value => _fovOffset = value, moving ? _fovKick : 0f, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            var lens = _cinemachineCamera.Lens;
            lens.FieldOfView = _fieldOfView + _fovOffset * weight;
            _cinemachineCamera.Lens = lens;
        }

        private void OnLanded(float intensity)
        {
            VerticalImpulse(-_landingResponse * intensity);
        }

        private void OnJumped()
        {
            VerticalImpulse(_jumpResponse);
        }

        private void VerticalImpulse(float amount)
        {
            _verticalTween?.Kill();
            _verticalTween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _verticalKick, value => _verticalKick = value, amount, 0.055f))
                .Append(DOTween.To(() => _verticalKick, value => _verticalKick = value, 0f, 0.21f).SetEase(Ease.OutCubic));
        }

        private void OnGravityChanged(Quaternion transport)
        {
            _transitionStart = _display;
            _reference = transport * _reference;
            _transition = 0f;
            _gravityTween?.Kill();
            _gravityTween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _gravityKick, value => _gravityKick = value, _gravityResponse, 0.045f).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => _gravityKick, value => _gravityKick = value, 0f, 0.275f).SetEase(Ease.OutCubic));
        }

        private void KillTweens()
        {
            _verticalTween?.Kill();
            _gravityTween?.Kill();
            _fovTween?.Kill();
        }
    }
}
