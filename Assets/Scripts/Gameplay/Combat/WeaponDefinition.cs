using System;
using UnityEngine;

namespace Overtime
{
    internal enum WeaponId
    {
        Compliance = 0,
        Termination = 1,
        Deadline = 2
    }

    internal enum WeaponType
    {
        Carbine,
        Scatter,
        Charged
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
        [SerializeField] private Vector3 _recoil = new(0.075f, 8f, 0.16f);
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
        internal Vector3 Recoil => _recoil;
        internal AudioClip Shot => _shot;
        internal AudioClip Reload => _reload;
        internal AudioClip Charge => _charge;
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
    }

    internal enum WeaponSlotInput
    {
        None,
        Primary,
        Secondary,
        Toggle
    }
}
