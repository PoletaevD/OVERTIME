using System;
using UnityEngine;

namespace Overtime
{
    internal enum WeaponId
    {
        Compliance = 0,
        Termination = 1,
        Deadline = 2,
        Directive = 3
    }

    internal enum WeaponType
    {
        Carbine,
        Scatter,
        Charged,
        SemiAutomatic
    }

    internal sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private WeaponId _id;
        [SerializeField] private WeaponType _type;
        [SerializeField] private string _displayName = "ARC-01  /  Compliance";
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _viewmodel;
        [SerializeField] private WeaponTuning _tuning = new();
        [SerializeField] private CameraRecoil _cameraRecoil = new();
        [SerializeField] private ViewmodelRecoil _viewmodelRecoil = new();
        [SerializeField] private AudioClip _shot;
        [SerializeField] private AudioClip _reload;
        [SerializeField] private AudioClip _charge;

        internal WeaponId Id => _id;
        internal WeaponType Type => _type;
        internal string DisplayName => _displayName;
        internal string Description => _description;
        internal Sprite Icon => _icon;
        internal GameObject Viewmodel => _viewmodel;
        internal WeaponTuning Tuning => _tuning;
        internal CameraRecoil GameplayRecoil => _cameraRecoil;
        internal ViewmodelRecoil VisualRecoil => _viewmodelRecoil;
        internal AudioClip Shot => _shot;
        internal AudioClip Reload => _reload;
        internal AudioClip Charge => _charge;
    }

    [Serializable]
    internal sealed class CameraRecoil
    {
        [SerializeField, Min(0f)] private float _vertical = 0.65f;
        [SerializeField, Min(0f)] private float _horizontal = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _verticalVariation = 0.12f;
        [SerializeField, Min(0.001f)] private float _duration = 0.035f;
        [SerializeField, Min(0f)] private float _sustainedPerShot = 0.035f;
        [SerializeField, Min(1f)] private float _maximumMultiplier = 1.35f;
        [SerializeField] private Vector2[] _pattern = { new(0.18f, -0.5f), new(-0.46f, 0.3f), new(-0.85f, 0.5f), new(0.7f, -0.25f), new(1f, 0.8f), new(-0.65f, -0.65f) };

        internal float Duration => _duration;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal float DebugVertical { get => _vertical; set => _vertical = value; }
        internal float DebugHorizontal { get => _horizontal; set => _horizontal = value; }
        internal float DebugDuration { get => _duration; set => _duration = value; }
#endif

        internal Vector2 Impulse(int index)
        {
            var phase = _pattern is { Length: > 0 } ? _pattern[index % _pattern.Length] : Vector2.zero;
            var strength = Mathf.Min(_maximumMultiplier, 1f + index * _sustainedPerShot);
            return new Vector2(phase.x * _horizontal, _vertical * (1f + phase.y * _verticalVariation)) * strength;
        }
    }

    [Serializable]
    internal sealed class ViewmodelRecoil
    {
        [SerializeField, Min(1f)] private float _positionStiffness = 720f;
        [SerializeField, Min(1f)] private float _positionDamping = 37f;
        [SerializeField, Min(1f)] private float _rotationStiffness = 610f;
        [SerializeField, Min(1f)] private float _rotationDamping = 31f;
        [SerializeField] private Vector3 _positionImpulse = new(0.25f, 0.72f, -3.2f);
        [SerializeField] private Vector3 _rotationImpulse = new(-330f, 42f, 55f);
        [SerializeField, Min(0.01f)] private float _maxPosition = 0.18f;
        [SerializeField, Min(0.1f)] private float _maxRotation = 22f;
        [SerializeField, Range(0f, 0.5f)] private float _shotVariation = 0.14f;

        internal Vector3 PositionImpulse => _positionImpulse;
        internal Vector3 RotationImpulse => _rotationImpulse;
        internal float ShotVariation => _shotVariation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal float DebugKick { get => -_positionImpulse.z; set => _positionImpulse.z = -value; }
        internal float DebugPitch { get => -_rotationImpulse.x; set => _rotationImpulse.x = -value; }
        internal float DebugStiffness { get => _positionStiffness; set => _positionStiffness = value; }
        internal float DebugDamping { get => _positionDamping; set => _positionDamping = value; }
#endif

        internal void Advance(float step, ref Vector3 position, ref Vector3 velocity, ref Vector3 angles, ref Vector3 angularVelocity)
        {
            AdvanceSpring(step, _positionStiffness, _positionDamping, _maxPosition, ref position, ref velocity);
            AdvanceSpring(step, _rotationStiffness, _rotationDamping, _maxRotation, ref angles, ref angularVelocity);
        }

        private void AdvanceSpring(float step, float stiffness, float damping, float limit, ref Vector3 offset, ref Vector3 velocity)
        {
            // Backward Euler remains stable for stiff springs.
            velocity = (velocity - offset * (step * stiffness)) / (1f + step * damping + step * step * stiffness);
            var next = offset + velocity * step;
            offset = Vector3.ClampMagnitude(next, limit);
            if (next.sqrMagnitude > limit * limit)
            {
                velocity -= Vector3.Project(velocity, offset);
            }
        }
    }

    [Serializable]
    internal sealed class WeaponTuning
    {
        [SerializeField, Min(1f)] private float _roundsPerMinute = 600f;
        [SerializeField, Min(0f)] private float _postShotDelay;
        [SerializeField, Min(1)] private int _magazine = 30;
        [SerializeField, Min(0)] private int _reserve = 180;
        [SerializeField, Min(0)] private int _magazinePerLevel = 10;
        [SerializeField, Min(0)] private int _roomReserve = 30;
        [SerializeField, Min(1)] private int _damage = 12;
        [SerializeField, Min(0.01f)] private float _reload = 1.5f;
        [SerializeField, Min(0.1f)] private float _speed = 45f;
        [SerializeField, Min(0.01f)] private float _radius = 0.045f;
        [SerializeField, Min(0.01f)] private float _lifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float _range = 80f;
        [SerializeField, Min(1)] private int _pellets = 1;
        [SerializeField, Range(0f, 45f)] private float _spread;
        [SerializeField, Min(0f)] private float _charge;
        [SerializeField, Range(1, 3)] private int _targets = 1;
        [SerializeField, Range(0f, 1f)] private float _penetrationRetention = 0.85f;
        [SerializeField] private Vector3 _muzzle = new(0.31f, -0.23f, 0.99f);

        internal double Interval => _postShotDelay > 0f ? _postShotDelay : 60d / _roundsPerMinute;
        internal int Magazine => _magazine;
        internal int Reserve => _reserve;
        internal int MagazinePerLevel => _magazinePerLevel;
        internal int RoomReserve => _roomReserve;
        internal int Damage => _damage;
        internal float Reload => _reload;
        internal float Speed => _speed;
        internal float Radius => _radius;
        internal float Lifetime => _lifetime;
        internal float Range => _range;
        internal int Pellets => _pellets;
        internal float Spread => _spread;
        internal float Charge => _charge;
        internal int Targets => _targets;
        internal float PenetrationRetention => _penetrationRetention;
        internal Vector3 Muzzle => _muzzle;
    }

    internal struct WeaponState
    {
        internal int Magazine;
        internal int Reserve;
        internal double Cooldown;
        internal double ReloadRemaining;
        internal float ReloadDuration;
        internal double ChargeElapsed;
        internal int RecoilIndex;
        internal float RecoilIdle;
    }

    internal enum WeaponSlotInput
    {
        None,
        Primary,
        Secondary,
        Toggle
    }
}
