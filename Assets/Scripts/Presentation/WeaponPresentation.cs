using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class WeaponPresentation : MonoBehaviour
    {
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private Transform _movementPivot;
        [SerializeField] private Transform _swayPivot;
        [SerializeField] private Transform _recoilPivot;
        [SerializeField] private Transform _muzzleFlash;
        [SerializeField] private Renderer[] _chargeStrips;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _idleSway = 0.005f;
        [SerializeField, Min(0f)] private float _movementBob = 0.033f;
        [SerializeField, Min(0f)] private float _mouseSway = 1.05f;
        [SerializeField, Min(0f)] private float _strafeLean = 6f;
        [SerializeField, Min(0f)] private float _jumpOffset = 0.055f;
        [SerializeField, Min(0f)] private float _fallOffset = 0.035f;
        [SerializeField, Min(0f)] private float _landingOffset = 0.055f;
        [SerializeField, Min(0f)] private float _gravityLean = 5f;

        [Header("Fire")]
        [SerializeField, Min(0f)] private float _recoilDistance = 0.075f;
        [SerializeField, Min(0f)] private float _recoilAngle = 8f;
        [SerializeField, Min(0.01f)] private float _recoveryTime = 0.16f;

        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _chargeProperties;
        private bool _isCharged;
        private float _idlePhase;
        private float _airOffset;
        private float _motionImpulse;
        private float _gravityImpulse;
        private float _recoil;
        private float _recoilRoll;
        private float _kick;
        private float _flashTime;
        private uint _confirmedShots;
        private PlaygroundPlayer _player;
        private CombatWeapon _weapon;
        private float _resetKick;
        private bool _wasRewinding;
        private Vector2 _sway;
        private Tween _motionTween;
        private Tween _gravityTween;
        private Tween _recoilTween;
        private Tween _resetTween;

        internal uint ConfirmedShots => _confirmedShots;

        private void OnEnable()
        {
            _player = GetComponentInParent<PlaygroundPlayer>();
            _weapon = _player.Combat?.Weapon;
            if (_chargeStrips is { Length: > 0 })
            {
                _chargeProperties ??= new MaterialPropertyBlock();
            }
            if (_weapon != null)
            {
                _weapon.ShotFired += Fire;
            }
            else
            {
                _player.LegacyShotFired += Fire;
            }

            _recoilTween = DOVirtual.Float(1f, 0f, _recoveryTime, value => _recoil = value * _kick).SetAutoKill(false).SetUpdate(true).SetEase(Ease.OutCubic).Pause();
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _motor.GravityChanged += OnGravityChanged;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ShotFired -= Fire;
            }
            else
            {
                _player.LegacyShotFired -= Fire;
            }

            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _motor.GravityChanged -= OnGravityChanged;
            SetChargeGlow(false);
            _motionTween?.Kill();
            _gravityTween?.Kill();
            _recoilTween?.Kill();
            _resetTween?.Kill();
            _resetKick = 0f;
            _wasRewinding = false;
            _motionImpulse = 0f;
            _gravityImpulse = 0f;
            _recoil = 0f;
            _flashTime = 0f;
            _muzzleFlash.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_cameraRig.IsRewinding != _wasRewinding)
            {
                _wasRewinding = _cameraRig.IsRewinding;
                SyncTemporalMotion();
            }

            SetChargeGlow(_weapon?.HasQueuedBoost == true);
            var deltaTime = Time.unscaledDeltaTime;
            if (_flashTime > 0f)
            {
                _flashTime -= deltaTime;
            }
            else if (_muzzleFlash.gameObject.activeSelf)
            {
                _muzzleFlash.gameObject.SetActive(false);
            }


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
            var temporal = _cameraRig.ReducedMotion ? 0f : _resetKick;
            _recoilPivot.localPosition = Vector3.back * (_recoil * _recoilDistance * motion + temporal * 0.035f);
            _recoilPivot.localRotation = Quaternion.Euler(-_recoil * _recoilAngle * motion + temporal * 4f, 0f, _recoil * _recoilRoll * motion);
        }

        private void SetChargeGlow(bool charged)
        {
            if (_chargeProperties == null || _isCharged == charged)
            {
                return;
            }

            _isCharged = charged;
            _chargeProperties.Clear();
            if (charged)
            {
                _chargeProperties.SetColor(s_emission, new Color(2.9f, 0.88f, 0.24f));
            }

            foreach (var strip in _chargeStrips)
            {
                strip.SetPropertyBlock(_chargeProperties);
            }
        }

        private void SyncTemporalMotion()
        {
            _resetTween?.Kill();
            if (!_wasRewinding)
            {
                _resetTween = DOTween.To(() => _resetKick, value => _resetKick = value, 0f, 0.2f).SetUpdate(true).SetEase(Ease.OutCubic);
                return;
            }

            _motionTween?.Kill();
            _gravityTween?.Kill();
            _recoilTween?.Pause();
            _flashTime = 0f;
            _motionImpulse = 0f;
            _gravityImpulse = 0f;
            _recoil = 0f;
            _muzzleFlash.gameObject.SetActive(false);
            if (!_cameraRig.ReducedMotion)
            {
                _resetTween = DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => _resetKick, value => _resetKick = value, 1f, 0.10f).SetEase(Ease.OutQuad))
                    .Append(DOTween.To(() => _resetKick, value => _resetKick = value, 0.32f, 0.26f).SetEase(Ease.OutCubic));
            }
        }

        private void Fire()
        {
            if (_wasRewinding && !_cameraRig.IsRewinding)
            {
                _wasRewinding = false;
                SyncTemporalMotion();
            }

            _confirmedShots++;
            var variation = (_confirmedShots % 5) * 0.04f;
            _recoilRoll = (_confirmedShots % 3 - 1f) * 1.4f;
            _kick = Mathf.Min(1.35f, _recoil + 0.84f + variation);
            _recoil = _kick;
            _recoilTween.Restart();
            _flashTime = _cameraRig.ReducedMotion ? 0.02f : 0.032f;
            _muzzleFlash.localScale = Vector3.one * (_cameraRig.ReducedMotion ? 0.55f : 0.85f + variation);
            _muzzleFlash.gameObject.SetActive(true);
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
