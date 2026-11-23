using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class WeaponPresentation : MonoBehaviour
    {
        [SerializeField] private PlaygroundInput _input;
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private Transform _movementPivot;
        [SerializeField] private Transform _swayPivot;
        [SerializeField] private Transform _recoilPivot;
        [SerializeField] private Transform _muzzleFlash;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _idleSway = 0.005f;
        [SerializeField, Min(0f)] private float _movementBob = 0.033f;
        [SerializeField, Min(0f)] private float _mouseSway = 1.05f;
        [SerializeField, Min(0f)] private float _strafeLean = 6f;
        [SerializeField, Min(0f)] private float _jumpOffset = 0.055f;
        [SerializeField, Min(0f)] private float _fallOffset = 0.035f;
        [SerializeField, Min(0f)] private float _landingOffset = 0.055f;
        [SerializeField, Min(0f)] private float _gravityLean = 5f;

        [Header("Test Fire")]
        [SerializeField, Min(0f)] private float _recoilDistance = 0.075f;
        [SerializeField, Min(0f)] private float _recoilAngle = 8f;
        [SerializeField, Min(0.01f)] private float _recoveryTime = 0.16f;

        private float _idlePhase;
        private float _airOffset;
        private float _motionImpulse;
        private float _gravityImpulse;
        private float _recoil;
        private Vector2 _sway;
        private Tween _motionTween;
        private Tween _gravityTween;
        private Tween _recoilTween;

        private void OnEnable()
        {
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _motor.GravityChanged += OnGravityChanged;
        }

        private void OnDisable()
        {
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _motor.GravityChanged -= OnGravityChanged;
            _motionTween?.Kill();
            _gravityTween?.Kill();
            _recoilTween?.Kill();
            _motionImpulse = 0f;
            _gravityImpulse = 0f;
            _recoil = 0f;
            _muzzleFlash.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_input.FirePressed)
            {
                Fire();
            }

            var deltaTime = Time.unscaledDeltaTime;
            var blend = 1f - Mathf.Exp(-deltaTime * 13f);
            var motion = _cameraRig.ReducedMotion ? 0.25f : 1f;
            var phase = _cameraRig.BobPhase;
            _idlePhase = Mathf.Repeat(_idlePhase + deltaTime * 1.85f, Mathf.PI * 2f);

            var idle = new Vector3(Mathf.Cos(_idlePhase), Mathf.Sin(_idlePhase), 0f) * _idleSway;
            var bob = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 2f) * 0.65f, 0f) * (_movementBob * _cameraRig.MovementWeight);
            var fall = _motor.Grounded ? 0f : _fallOffset * Mathf.Clamp01(Vector3.Dot(-_motor.Up, _motor.Velocity) / 8f);
            _airOffset = Mathf.Lerp(_airOffset, fall, blend);
            _movementPivot.localPosition = motion * (idle + bob + Vector3.up * (_airOffset + _motionImpulse));
            _movementPivot.localRotation = Quaternion.Euler(motion * (Mathf.Sin(_idlePhase) * 0.35f + _motionImpulse * 30f), 0f, motion * (-_cameraRig.Lateral * _strafeLean + _gravityImpulse));

            _sway = Vector2.Lerp(_sway, Vector2.ClampMagnitude(_cameraRig.LookDelta * _mouseSway / Mathf.Max(deltaTime * 60f, 0.3f), 7f), blend);
            _swayPivot.localPosition = new Vector3(-_sway.x, -_sway.y, 0f) * (0.0045f * motion);
            _swayPivot.localRotation = Quaternion.Euler(_sway.y * motion, -_sway.x * motion, 0f);
            _recoilPivot.localPosition = Vector3.back * (_recoil * _recoilDistance * motion);
            _recoilPivot.localRotation = Quaternion.AngleAxis(-_recoil * _recoilAngle * motion, Vector3.right);
        }

        private void Fire()
        {
            _recoilTween?.Kill();
            _muzzleFlash.gameObject.SetActive(true);
            _recoilTween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _recoil, value => _recoil = value, Mathf.Min(1.3f, _recoil + 1f), 0.026f).SetEase(Ease.OutCubic))
                .InsertCallback(0.047f, () => _muzzleFlash.gameObject.SetActive(false))
                .Append(DOTween.To(() => _recoil, value => _recoil = value, 0f, _recoveryTime).SetEase(Ease.OutCubic));
        }

        private void OnJumped()
        {
            Kick(-_jumpOffset, 0.30f);
        }

        private void OnLanded(float strength)
        {
            Kick(-_landingOffset * strength, 0.22f);
        }

        private void Kick(float offset, float recovery)
        {
            _motionTween?.Kill();
            _motionTween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _motionImpulse, value => _motionImpulse = value, offset, 0.06f))
                .Append(DOTween.To(() => _motionImpulse, value => _motionImpulse = value, 0f, recovery).SetEase(Ease.OutCubic));
        }

        private void OnGravityChanged(Quaternion transport)
        {
            _gravityTween?.Kill();
            var turn = Vector3.Dot(transport * _cameraRig.Heading, _motor.Up);
            _gravityImpulse = (turn < 0f ? -1f : 1f) * _gravityLean;
            _gravityTween = DOTween.To(() => _gravityImpulse, value => _gravityImpulse = value, 0f, 0.30f).SetUpdate(true).SetEase(Ease.OutCubic);
        }
    }
}
