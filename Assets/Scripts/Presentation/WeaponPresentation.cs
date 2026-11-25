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
        [SerializeField] private WeaponViewmodel[] _alternatives;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _idleSway = 0.005f;
        [SerializeField, Min(0f)] private float _movementBob = 0.033f;
        [SerializeField, Min(0f)] private float _mouseSway = 1.05f;
        [SerializeField, Min(0f)] private float _strafeLean = 6f;
        [SerializeField, Min(0f)] private float _jumpOffset = 0.055f;
        [SerializeField, Min(0f)] private float _fallOffset = 0.035f;
        [SerializeField, Min(0f)] private float _landingOffset = 0.055f;
        [SerializeField, Min(0f)] private float _gravityLean = 5f;

        private const float SpringStep = 1f / 240f;

        private readonly ViewmodelRecoil _fallbackRecoil = new();

        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _chargeProperties;
        private float _glow = -1f;
        private bool _glowBoosted;
        private GameObject _primaryReceiver;
        private Transform _primaryModule;
        private Vector3 _primaryModuleRest;
        private Vector3 _flashRest;
        private WeaponViewmodel _viewmodel;
        private Transform _activeFlash;
        private WeaponId _displayedWeapon;
        private float _idlePhase;
        private float _airOffset;
        private float _motionImpulse;
        private float _gravityImpulse;
        private Vector3 _recoilPosition;
        private Vector3 _recoilVelocity;
        private Vector3 _recoilAngles;
        private Vector3 _recoilAngularVelocity;
        private float _springCredit;
        private float _flashTime;
        private uint _confirmedShots;
        private PlaygroundPlayer _player;
        private CombatWeapon _weapon;
        private Camera _viewCamera;
        private float _resetKick;
        private bool _wasRewinding;
        private Vector2 _sway;
        private Tween _motionTween;
        private Tween _gravityTween;
        private Tween _resetTween;

        internal uint ConfirmedShots => _confirmedShots;

        private void OnEnable()
        {
            _player = GetComponentInParent<PlaygroundPlayer>();
            _weapon = _player.Combat?.Weapon;
            _viewCamera = GetComponentInParent<Camera>();
            _chargeProperties ??= new MaterialPropertyBlock();
            _primaryReceiver = _recoilPivot.Find("ARC-01 Receiver")?.gameObject;
            _primaryModule = _primaryReceiver != null ? _primaryReceiver.transform.Find("Power Cell") : null;
            if (_primaryModule != null)
            {
                _primaryModuleRest = _primaryModule.localPosition;
            }

            _activeFlash = _muzzleFlash;
            _flashRest = _muzzleFlash.localScale;
            if (_weapon != null)
            {
                _weapon.ShotFired += Fire;
            }
            else
            {
                _player.LegacyShotFired += Fire;
            }

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
            SetChargeGlow(0f, false);
            _motionTween?.Kill();
            _gravityTween?.Kill();
            _resetTween?.Kill();
            _resetKick = 0f;
            _wasRewinding = false;
            _motionImpulse = 0f;
            _gravityImpulse = 0f;
            ClearRecoil();
            _flashTime = 0f;
            _activeFlash.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_cameraRig.IsRewinding != _wasRewinding)
            {
                _wasRewinding = _cameraRig.IsRewinding;
                SyncTemporalMotion();
            }

            SyncViewmodel();
            var charge = _weapon?.ChargeProgress ?? 0f;
            var energized = _weapon?.LastShotBoosted == true && _flashTime > 0f;
            SetChargeGlow(Mathf.Max(charge, _displayedWeapon == WeaponId.Deadline ? Mathf.Clamp01(-_recoilPosition.z * 8f) * 0.65f : 0f), _weapon?.HasQueuedBoost == true || energized);
            var deltaTime = Time.unscaledDeltaTime;
            if (_flashTime > 0f)
            {
                _flashTime -= deltaTime;
            }
            else if (_activeFlash.gameObject.activeSelf)
            {
                _activeFlash.gameObject.SetActive(false);
            }


            var blend = 1f - Mathf.Exp(-deltaTime * 13f);
            var aim = _weapon?.AimBlend ?? 0f;
            var aimTuning = _weapon?.ActiveDefinition?.Aim;
            var motion = (_cameraRig.ReducedMotion ? 0.25f : 1f) * Mathf.Lerp(1f, 0.23f, aim);
            var phase = _cameraRig.BobPhase;
            _idlePhase = Mathf.Repeat(_idlePhase + deltaTime * 1.85f, Mathf.PI * 2f);

            var idle = new Vector3(Mathf.Cos(_idlePhase), Mathf.Sin(_idlePhase), 0f) * _idleSway;
            var bob = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 2f) * 0.65f, 0f) * (_movementBob * _cameraRig.MovementWeight);
            var fall = _motor.Grounded ? 0f : _fallOffset * Mathf.Clamp01(Vector3.Dot(-_motor.Up, _motor.Velocity) / 8f);
            _airOffset = Mathf.Lerp(_airOffset, fall, blend);
            var aimedPose = DOVirtual.EasedValue(0f, 1f, aim, Ease.InOutSine);
            var calibrated = aimTuning?.HasSight == true && _viewCamera != null;
            var aimOffset = calibrated ? Vector3.zero : (aimTuning?.Position ?? Vector3.zero) * aimedPose;
            _movementPivot.localPosition = aimOffset + motion * (idle + bob + Vector3.up * (_airOffset + _motionImpulse));
            _movementPivot.localRotation = Quaternion.Euler(motion * (Mathf.Sin(_idlePhase) * 0.35f + _motionImpulse * 30f), 0f, motion * (-_cameraRig.Lateral * _strafeLean + _gravityImpulse));

            if (!calibrated)
            {
                _movementPivot.localRotation = Quaternion.Slerp(Quaternion.identity, aimTuning?.Rotation ?? Quaternion.identity, aim) * _movementPivot.localRotation;
            }

            _sway = Vector2.Lerp(_sway, Vector2.ClampMagnitude(_cameraRig.LookDelta * _mouseSway / Mathf.Max(deltaTime * 60f, 0.3f), 7f), blend);
            _swayPivot.localPosition = new Vector3(-_sway.x, -_sway.y, 0f) * (0.0045f * motion);
            _swayPivot.localRotation = Quaternion.Euler(_sway.y * motion, -_sway.x * motion, 0f);
            var temporal = _cameraRig.ReducedMotion ? 0f : _resetKick;
            var reload = _weapon?.ReloadProgress ?? 0f;
            var switchProgress = _weapon?.SwitchProgress ?? 0f;
            var knife = _player.Combat?.Knife;
            var melee = knife?.Active == true ? ActionDip(knife.Progress, 0.10f, 0.72f) : 0f;
            var action = Mathf.Max(melee, Mathf.Max(ActionDip(reload, 0.28f, 0.72f), ActionDip(switchProgress, 0.5f, 0.5f)) * motion);
            var module = _viewmodel != null ? _viewmodel.Module : _primaryModule;
            if (module != null)
            {
                var rest = _viewmodel != null ? Vector3.zero : _primaryModuleRest;
                module.localPosition = rest + new Vector3(0.014f, -0.14f, -0.043f) * ActionDip(reload, 0.48f, 0.55f) * motion;
            }

            var style = _weapon?.ActiveDefinition?.VisualRecoil ?? _fallbackRecoil;
            _springCredit += Mathf.Min(deltaTime, 0.1f);
            while (_springCredit >= SpringStep)
            {
                style.Advance(SpringStep, ref _recoilPosition, ref _recoilVelocity, ref _recoilAngles, ref _recoilAngularVelocity);
                _springCredit -= SpringStep;
            }

            if (_viewmodel?.Slide != null)
            {
                _viewmodel.Slide.localPosition = Vector3.back * Mathf.Clamp(-_recoilPosition.z * 0.54f, 0f, 0.046f);
            }
            var vibration = new Vector3(Mathf.Sin(Time.unscaledTime * 119f), Mathf.Cos(Time.unscaledTime * 103f), 0f) * (charge * motion * 0.0022f);
            _recoilPivot.localPosition = _recoilPosition * motion + Vector3.back * (temporal * 0.035f) + Vector3.down * (action * 0.27f) + vibration;
            _recoilPivot.localRotation = Quaternion.Euler(_recoilAngles * motion + new Vector3(temporal * 4f + action * 28f, -action * 9f, action * 7f));
            if (calibrated && aimedPose > 0f)
            {
                AlignSights(aimTuning, aimedPose);
            }

            if (_displayedWeapon == WeaponId.Deadline && _viewmodel != null)
            {
                _viewmodel.SetScopeView(DeadlineScope.Blend(aim) >= 0.68f);
            }
        }

        private void AlignSights(WeaponAimTuning tuning, float weight)
        {
            SightPoints(tuning, out var rear, out var front);
            var localRear = _movementPivot.InverseTransformPoint(rear);
            var localFront = _movementPivot.InverseTransformPoint(front);
            var sightRotation = Quaternion.LookRotation(localFront - localRear, Vector3.up);
            var camera = _viewCamera.transform;
            var rotation = camera.rotation * Quaternion.Inverse(sightRotation);
            var depth = Mathf.Max(0.065f, tuning.EyeRelief + _recoilPosition.z * 0.45f);
            var position = camera.position + camera.forward * depth - rotation * localRear;
            _movementPivot.SetPositionAndRotation(Vector3.Lerp(_movementPivot.position, position, weight), Quaternion.Slerp(_movementPivot.rotation, rotation, weight));
        }

        internal void SightPoints(WeaponAimTuning tuning, out Vector3 rear, out Vector3 front)
        {
            var frame = _viewmodel != null ? _viewmodel.transform : _recoilPivot;
            var slide = _viewmodel?.Slide != null ? _viewmodel.Slide.localPosition : Vector3.zero;
            rear = frame.TransformPoint(tuning.RearSight + slide);
            front = frame.TransformPoint(tuning.FrontSight + slide);
        }

        private float ActionDip(float progress, float downEnd, float upStart)
        {
            var dip = Mathf.Min(progress / downEnd, (1f - progress) / (1f - upStart));
            return DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(dip), Ease.InOutSine);
        }

        private void SyncViewmodel()
        {
            var id = _weapon?.ActiveDefinition?.Id ?? WeaponId.Compliance;
            if (id == _displayedWeapon)
            {
                return;
            }

            SetChargeGlow(0f, false);
            _activeFlash.gameObject.SetActive(false);
            ClearRecoil();
            _flashTime = 0f;
            _displayedWeapon = id;
            var index = (int)id - 1;
            _viewmodel = _alternatives != null && index >= 0 && index < _alternatives.Length ? _alternatives[index] : null;
            if (_alternatives != null)
            {
                foreach (var alternative in _alternatives)
                {
                    if (alternative != null)
                    {
                        alternative.gameObject.SetActive(alternative == _viewmodel);
                    }
                }
            }

            if (_primaryReceiver != null)
            {
                _primaryReceiver.SetActive(_viewmodel == null);
            }

            _activeFlash = _viewmodel != null ? _viewmodel.Flash : _muzzleFlash;
            _flashRest = Vector3.one;
            _glow = -1f;
        }

        private void SetChargeGlow(float brightness, bool boosted)
        {
            var strips = _viewmodel != null ? _viewmodel.ChargeRails : _chargeStrips;
            if (strips == null || (Mathf.Approximately(_glow, brightness) && _glowBoosted == boosted))
            {
                return;
            }

            _glow = brightness;
            _glowBoosted = boosted;
            _chargeProperties.Clear();
            if (boosted)
            {
                _chargeProperties.SetColor(s_emission, new Color(2.9f, 0.88f, 0.24f));
            }
            else if (brightness > 0f)
            {
                _chargeProperties.SetColor(s_emission, new Color(0.24f, 1.8f, 3.5f) * (0.35f + brightness * 1.6f));
            }

            foreach (var strip in strips)
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
            _flashTime = 0f;
            _motionImpulse = 0f;
            _gravityImpulse = 0f;
            ClearRecoil();
            _activeFlash.gameObject.SetActive(false);
            if (!_cameraRig.ReducedMotion)
            {
                _resetTween = DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => _resetKick, value => _resetKick = value, 1f, 0.10f).SetEase(Ease.OutQuad))
                    .Append(DOTween.To(() => _resetKick, value => _resetKick = value, 0.32f, 0.26f).SetEase(Ease.OutCubic));
            }
        }

        private void Fire()
        {
            SyncViewmodel();
            if (_wasRewinding && !_cameraRig.IsRewinding)
            {
                _wasRewinding = false;
                SyncTemporalMotion();
            }

            _confirmedShots++;
            var style = _weapon?.ActiveDefinition?.VisualRecoil ?? _fallbackRecoil;
            var variation = (_confirmedShots % 5 - 2f) * style.ShotVariation * 0.5f;
            var side = _confirmedShots % 3 - 1f;
            _recoilVelocity += Vector3.Scale(style.PositionImpulse, new Vector3(side, 1f + variation, 1f + variation));
            _recoilAngularVelocity += Vector3.Scale(style.RotationImpulse, new Vector3(1f + variation, side, -side));
            _flashTime = _cameraRig.ReducedMotion ? 0.02f : _displayedWeapon == WeaponId.Termination ? 0.055f : 0.032f;
            _activeFlash.localScale = _flashRest * (_cameraRig.ReducedMotion ? 0.55f : 0.85f + variation);
            _activeFlash.gameObject.SetActive(true);
        }

        private void ClearRecoil()
        {
            _recoilPosition = _recoilVelocity = _recoilAngles = _recoilAngularVelocity = Vector3.zero;
            _springCredit = 0f;
            if (_viewmodel?.Slide != null)
            {
                _viewmodel.Slide.localPosition = Vector3.zero;
            }
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
