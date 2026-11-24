using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class CombatWeapon : MonoBehaviour
    {
        private const double Tolerance = 0.0000001d;
        private const int TwinFrequency = 5;
        private const float TwinOffset = 2.5f;

        [SerializeField] private WeaponDefinition _primary;
        [SerializeField] private LayerMask _hitLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1)] private int _projectileCapacity = 48;
        [SerializeField, Min(0.05f)] private float _switchSeconds = 0.3f;

        private readonly WeaponTuning _fallback = new();
        private Snapshot _state;
        private WeaponDefinition _secondary;
        private RunUpgradeState _upgrades;
        private PlayerUpgradeEffects _effects;

        internal event Action ShotFired;
        internal event Action AutoFed;
        internal event Action SwitchStarted;

        internal int ActiveSlot => _state.ActiveSlot;
        internal WeaponDefinition ActiveDefinition => Definition(ActiveSlot);
        internal WeaponType ActiveType => ActiveDefinition?.Type ?? WeaponType.Carbine;
        internal string ActiveName => Name(ActiveSlot);
        internal bool HasSecondary => _secondary != null;
        internal bool IsSwitching => _state.SwitchRemaining > 0d;
        internal float SwitchProgress => IsSwitching ? Mathf.Clamp01(1f - (float)(_state.SwitchRemaining / _switchSeconds)) : 0f;
        internal int MagazineCapacity => Capacity(ActiveSlot);
        internal int EffectiveDamage => Mathf.FloorToInt(DamageFor(ActiveSlot) + 0.5f);
        internal float ReloadSeconds => ReloadFor(ActiveSlot);
        internal int Magazine => _state.Magazine;
        internal int Reserve => _state.Reserve;
        internal bool IsReloading => _state.ReloadRemaining > 0d;
        internal float ReloadProgress => IsReloading ? 1f - (float)(_state.ReloadRemaining / _state.Active.ReloadDuration) : 0f;
        internal bool IsCharging => _state.Active.ChargeElapsed > 0d;
        internal float ChargeProgress => IsCharging ? Mathf.Clamp01((float)(_state.Active.ChargeElapsed / Tuning(ActiveSlot).Charge)) : 0f;
        internal int ProjectileCapacity => _projectileCapacity;
        internal bool HasQueuedBoost => _effects?.Charged == true;
        internal bool LastShotBoosted { get; private set; }

        internal WeaponDefinition Definition(int slot) => slot == 0 ? _primary : _secondary;
        internal string Name(int slot) => Definition(slot)?.DisplayName ?? "ARC-01  /  Compliance";
        internal WeaponState State(int slot) => slot == 0 ? _state.Primary : _state.Secondary;
        internal WeaponTuning Tuning(int slot) => Definition(slot)?.Tuning ?? _fallback;
        internal int Capacity(int slot) => Tuning(slot).Magazine + Tuning(slot).MagazinePerLevel * (_upgrades?.Level(UpgradeId.ExtendedMagazine) ?? 0);
        internal float DamageFor(int slot) => Tuning(slot).Damage * (1f + Bonus(UpgradeId.OverclockedReceiver));
        internal float ReloadFor(int slot) => Mathf.Max(0.05f, Tuning(slot).Reload * (1f - Bonus(UpgradeId.RapidService)));
        internal float CycleSeconds(int slot, TemporalMode mode) => (float)(Tuning(slot).Interval + Tuning(slot).Charge) / CycleMultiplier(mode);
        internal float ChargeSeconds(int slot, TemporalMode mode) => Tuning(slot).Charge / CycleMultiplier(mode);
        internal float SpeedFor(int slot, TemporalMode mode) => Tuning(slot).Speed * (mode == TemporalMode.BulletTime ? 1f + Bonus(UpgradeId.Hypervelocity) : 1f);

        internal void BindUpgrades(RunUpgradeState upgrades, PlayerUpgradeEffects effects)
        {
            _upgrades = upgrades;
            _effects = effects;
        }

        internal float RoundsPerMinute(TemporalMode mode) => 60f / CycleSeconds(ActiveSlot, mode);
        internal float ProjectileSpeed(TemporalMode mode) => SpeedFor(ActiveSlot, mode);

        internal bool EquipSecondary(WeaponDefinition definition)
        {
            if (definition == null || _secondary != null)
            {
                return false;
            }

            _secondary = definition;
            _state.Secondary = StartingState(1);
            return true;
        }

        internal bool TryAutoFeed(int rounds)
        {
            ref var weapon = ref ActiveState();
            if (TransferReserve(ref weapon, rounds, ActiveSlot) == 0)
            {
                return false;
            }

            AutoFed?.Invoke();
            return true;
        }

        internal void ReplenishRoom(int primaryRounds)
        {
            _state.Primary.Reserve += primaryRounds;
            if (HasSecondary)
            {
                _state.Secondary.Reserve += Tuning(1).RoomReserve;
            }
        }

        internal void CancelCharge()
        {
            _state.Primary.ChargeElapsed = 0d;
            _state.Secondary.ChargeElapsed = 0d;
        }

        internal void GrantAmmoCache()
        {
            _state.Primary.Reserve += 45;
            if (HasSecondary)
            {
                _state.Secondary.Reserve += _secondary.Id == WeaponId.Deadline ? 6 : 12;
            }
        }

        internal void ResetState()
        {
            _state = default;
            _state.Primary = StartingState(0);
            if (HasSecondary)
            {
                _state.Secondary = StartingState(1);
            }
        }

        internal Snapshot Capture() => _state;

        internal void Restore(in Snapshot snapshot)
        {
            _state = snapshot;
            LastShotBoosted = false;
        }

        internal void Simulate(double step, bool fire, bool reload, bool canShoot, Ray aim, Vector3 aimUp, TemporalProjectileEmitter projectiles, TemporalMode mode = TemporalMode.Normal, WeaponSlotInput selection = WeaponSlotInput.None, bool fireHeld = false)
        {
            if (mode == TemporalMode.Override)
            {
                return;
            }

            var oldSlot = ActiveSlot;
            ref var weapon = ref ActiveState();
            var cooldown = weapon.Cooldown;
            weapon.Cooldown = Math.Max(0d, cooldown - step);
            ref var stowed = ref StateAt(1 - oldSlot);
            stowed.Cooldown = Math.Max(0d, stowed.Cooldown - step);

            if (AdvanceSwitch(step, selection))
            {
                return;
            }

            if (weapon.ReloadRemaining > 0d)
            {
                weapon.ReloadRemaining = Math.Max(0d, weapon.ReloadRemaining - step);
                if (weapon.ReloadRemaining <= Tolerance)
                {
                    TransferReserve(ref weapon, weapon.Reserve, oldSlot);
                    weapon.ReloadRemaining = 0d;
                }
            }
            else if (reload && weapon.Magazine < Capacity(oldSlot) && weapon.Reserve > 0)
            {
                weapon.ChargeElapsed = 0d;
                weapon.ReloadDuration = ReloadFor(oldSlot);
                weapon.ReloadRemaining = weapon.ReloadDuration;
            }

            var tuning = Tuning(oldSlot);
            var trigger = tuning.Charge > 0f ? fireHeld : fire;
            if (!canShoot || !trigger || IsReloading || weapon.Magazine <= 0)
            {
                weapon.ChargeElapsed = 0d;
                return;
            }

            if (weapon.Cooldown > Tolerance || !AdvanceCharge(ref weapon, tuning, (step - cooldown) * CycleMultiplier(mode)))
            {
                return;
            }

            if (!TryFire(aim, aimUp, projectiles, mode, tuning, weapon.Magazine == 1))
            {
                weapon.ChargeElapsed = Math.Min(weapon.ChargeElapsed, tuning.Charge);
                return;
            }

            var cycleMultiplier = CycleMultiplier(mode);
            var carriedTime = tuning.Charge > 0f ? (weapon.ChargeElapsed - tuning.Charge) / cycleMultiplier : cooldown > 0d ? Math.Max(0d, step - cooldown) : 0d;
            weapon.Cooldown = Math.Max(0d, tuning.Interval / cycleMultiplier - carriedTime);
            weapon.ChargeElapsed = 0d;
            weapon.Magazine--;
            LastShotBoosted = HasQueuedBoost;
            _effects?.ConfirmShot();
            ShotFired?.Invoke();
        }

        private bool AdvanceCharge(ref WeaponState weapon, WeaponTuning tuning, double available)
        {
            if (tuning.Charge <= 0f)
            {
                return true;
            }

            weapon.ChargeElapsed += Math.Max(0d, available);
            return weapon.ChargeElapsed + Tolerance >= tuning.Charge;
        }

        private bool AdvanceSwitch(double step, WeaponSlotInput selection)
        {
            if (IsSwitching)
            {
                _state.SwitchRemaining = Math.Max(0d, _state.SwitchRemaining - step);
                if (_state.SwitchRemaining <= _switchSeconds * 0.5d + Tolerance)
                {
                    _state.ActiveSlot = _state.SwitchTarget;
                }

                if (_state.SwitchRemaining <= Tolerance)
                {
                    _state.SwitchRemaining = 0d;
                    _effects?.CompleteSwitch();
                }

                return true;
            }

            var target = selection == WeaponSlotInput.Toggle ? 1 - ActiveSlot : (int)selection - 1;
            if (!HasSecondary || target < 0 || target == ActiveSlot)
            {
                return false;
            }

            ActiveState().ReloadRemaining = 0d;
            ActiveState().ChargeElapsed = 0d;
            _state.SwitchTarget = target;
            _state.SwitchRemaining = _switchSeconds;
            SwitchStarted?.Invoke();
            return true;
        }

        private bool TryFire(Ray aim, Vector3 aimUp, TemporalProjectileEmitter projectiles, TemporalMode mode, WeaponTuning tuning, bool lastRound)
        {
            var twinDamage = Bonus(UpgradeId.TwinOutput);
            var twin = twinDamage > 0f && _state.TwinCount == TwinFrequency - 1;
            var damage = DamageFor(ActiveSlot);
            damage = _effects?.ModifyShotDamage(damage) ?? damage;
            if (lastRound)
            {
                damage *= 1f + Bonus(UpgradeId.LastWord);
            }

            var hypervelocity = mode == TemporalMode.BulletTime && Bonus(UpgradeId.Hypervelocity) > 0f;
            var terminal = _upgrades?.Definition(UpgradeId.TerminalBallistics);
            var close = _upgrades?.Definition(UpgradeId.CloseQuarters);
            var ricochet = Bonus(UpgradeId.RicochetMatrix);
            var flight = new ProjectileBallistics(ActiveDefinition?.Id ?? WeaponId.Compliance, Mathf.Min(4, tuning.Targets + Mathf.RoundToInt(Bonus(UpgradeId.PenetrationProtocol))), ricochet > 0f ? 1 : 0, tuning.PenetrationRetention, ricochet, terminal?.Magnitude ?? 0f, terminal?.Limit ?? 0f, close?.Magnitude ?? 0f, close?.Radius ?? 0f);
            if (!projectiles.TryLaunchVolley(aim, aimUp, tuning.Muzzle, ProjectileSpeed(mode), tuning.Lifetime, tuning.Range, tuning.Radius, damage, _hitLayers, HasQueuedBoost, hypervelocity, flight, tuning.Pellets, tuning.Spread, twin ? twinDamage : 0f, TwinOffset))
            {
                return false;
            }

            if (twinDamage > 0f)
            {
                _state.TwinCount = (_state.TwinCount + 1) % TwinFrequency;
            }

            return true;
        }

        private float CycleMultiplier(TemporalMode mode) => mode == TemporalMode.BulletTime ? 1f + Bonus(UpgradeId.AcceleratedProcessing) : 1f;
        private float Bonus(UpgradeId id) => _upgrades?.Bonus(id) ?? 0f;

        private ref WeaponState ActiveState() => ref StateAt(ActiveSlot);

        private ref WeaponState StateAt(int slot)
        {
            if (slot == 0)
            {
                return ref _state.Primary;
            }

            return ref _state.Secondary;
        }

        private WeaponState StartingState(int slot)
        {
            return new WeaponState
            {
                Magazine = Capacity(slot),
                Reserve = Tuning(slot).Reserve
            };
        }

        private int TransferReserve(ref WeaponState weapon, int rounds, int slot)
        {
            var transferred = Mathf.Clamp(rounds, 0, Mathf.Max(0, Mathf.Min(Capacity(slot) - weapon.Magazine, weapon.Reserve)));
            weapon.Magazine += transferred;
            weapon.Reserve -= transferred;
            return transferred;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugEquip(WeaponDefinition definition)
        {
            _secondary = definition;
            _state.Secondary = StartingState(1);
            DebugActivate(1);
        }

        internal void DebugRefill()
        {
            _state.Primary.Magazine = Capacity(0);
            _state.Primary.Reserve = Tuning(0).Reserve;
            if (HasSecondary)
            {
                _state.Secondary.Magazine = Capacity(1);
                _state.Secondary.Reserve = Tuning(1).Reserve;
            }
        }

        internal void DebugActivate(int slot)
        {
            _state.ActiveSlot = slot == 1 && HasSecondary ? 1 : 0;
            _state.SwitchRemaining = 0d;
            _state.Primary.ReloadRemaining = _state.Primary.ChargeElapsed = 0d;
            _state.Secondary.ReloadRemaining = _state.Secondary.ChargeElapsed = 0d;
        }
#endif

        internal struct Snapshot
        {
            internal WeaponState Primary;
            internal WeaponState Secondary;
            internal int ActiveSlot;
            internal int SwitchTarget;
            internal double SwitchRemaining;
            internal int TwinCount;

            internal WeaponState Active => ActiveSlot == 0 ? Primary : Secondary;
            internal int Magazine => Active.Magazine;
            internal int Reserve => Active.Reserve;
            internal double Cooldown => Active.Cooldown;
            internal double ReloadRemaining => Active.ReloadRemaining;
        }
    }

    internal readonly struct ProjectileImpact
    {
        internal readonly Vector3 Point;
        internal readonly Vector3 Normal;
        internal readonly DamageResult Damage;
        internal readonly bool WeakPoint;
        internal readonly bool Shielded;
        internal readonly bool FromPlayer;
        internal readonly bool Ricochet;
        internal readonly bool Piercing;

        internal ProjectileImpact(Vector3 point, Vector3 normal, DamageResult damage, bool weakPoint, bool shielded, bool fromPlayer, bool ricochet = false, bool piercing = false)
        {
            Point = point;
            Normal = normal;
            Damage = damage;
            WeakPoint = weakPoint;
            Shielded = shielded;
            FromPlayer = fromPlayer;
            Ricochet = ricochet;
            Piercing = piercing;
        }
    }
}
