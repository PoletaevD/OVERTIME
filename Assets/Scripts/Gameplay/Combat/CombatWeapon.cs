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
        private readonly CameraRecoil _fallbackRecoil = new();
        private Snapshot _state;
        private WeaponDefinition _secondary;
        private RunUpgradeState _upgrades;
        private PlayerUpgradeEffects _effects;
        private bool _instanceLoadout;
        private WeaponInstance _first;
        private WeaponInstance _second;
        private AmmoStock _collectedAmmo;
        private readonly System.Collections.Generic.HashSet<int> _ammoPickups = new();

        internal event Action ShotFired;
        internal event Action AutoFed;
        internal event Action SwitchStarted;
        internal event Action<int> AmmoCollected;

        internal int ActiveSlot => _state.ActiveSlot;
        internal WeaponDefinition ActiveDefinition => Definition(ActiveSlot);
        internal WeaponType ActiveType => ActiveDefinition?.Type ?? WeaponType.Carbine;
        internal string ActiveName => Name(ActiveSlot);
        internal bool HasSecondary => Definition(1) != null;
        internal bool IsSwitching => _state.SwitchRemaining > 0d;
        internal float SwitchProgress => IsSwitching ? Mathf.Clamp01(1f - (float)(_state.SwitchRemaining / _switchSeconds)) : 0f;
        internal int MagazineCapacity => Capacity(ActiveSlot);
        internal int EffectiveDamage => Mathf.FloorToInt(DamageFor(ActiveSlot) + 0.5f);
        internal float ReloadSeconds => ReloadFor(ActiveSlot);
        internal int Magazine => _state.Magazine;
        internal int Reserve => ReserveFor(ActiveSlot);
        internal bool IsReloading => _state.ReloadRemaining > 0d;
        internal float ReloadProgress => IsReloading ? 1f - (float)(_state.ReloadRemaining / _state.Active.ReloadDuration) : 0f;
        internal bool IsCharging => _state.Active.ChargeElapsed > 0d;
        internal float ChargeProgress => IsCharging ? Mathf.Clamp01((float)(_state.Active.ChargeElapsed / Tuning(ActiveSlot).Charge)) : 0f;
        internal int ProjectileCapacity => _projectileCapacity;
        internal bool HasQueuedBoost => _effects?.Charged == true;
        internal bool LastShotBoosted { get; private set; }
        internal float AimBlend => _state.AimBlend;
        internal float AimSeconds => _state.AimSeconds;
        internal bool Aiming => _state.Aiming;
        internal float AimSpread => Mathf.Lerp(1f, ActiveDefinition?.Aim.Spread ?? 1f, AimBlend);
        internal float AimMovement => Mathf.Lerp(1f, ActiveDefinition?.Aim.Movement ?? 1f, AimBlend);
        internal float AimSensitivity => Mathf.Lerp(1f, ActiveDefinition?.Aim.Sensitivity ?? 1f, AimBlend);

        internal void AdvanceAim(float step, bool held, bool blocked)
        {
            var tuning = ActiveDefinition?.Aim;
            _state.Aiming = held && !blocked && !IsReloading && !IsSwitching && tuning != null;
            _state.AimBlend = Mathf.MoveTowards(_state.AimBlend, _state.Aiming ? 1f : 0f, step / (tuning?.Transition ?? 0.18f));
            _state.AimSeconds = _state.Aiming ? Mathf.Min(1f, _state.AimSeconds + step) : 0f;
        }

        internal void CancelHandling()
        {
            ref var state = ref ActiveState();
            state.ReloadRemaining = 0d;
            state.ChargeElapsed = 0d;
            state.BurstLeft = 0;
        }

        internal WeaponDefinition AuthoredPrimary => _primary;
        internal WeaponInstance Instance(int slot) => slot == 0 ? _first : _second;
        internal WeaponDefinition Definition(int slot) => _instanceLoadout ? Instance(slot)?.Definition : slot == 0 ? _primary : _secondary;
        internal string Name(int slot) => Definition(slot)?.DisplayName ?? (_instanceLoadout ? "Empty" : "ARC-01  /  Compliance");

        internal WeaponState State(int slot)
        {
            var state = StateAt(slot);
            state.Reserve = ReserveFor(slot);
            return state;
        }

        internal int Ammo(WeaponId type) => _state.Stock.Get(type);
        internal bool Collected(int id) => _ammoPickups.Contains(id);

        internal bool CollectAmmo(int id, WeaponId type, int amount)
        {
            if (!_instanceLoadout || amount <= 0 || !_ammoPickups.Add(id))
            {
                return false;
            }

            _collectedAmmo.Add(type, amount);
            _state.Stock.Add(type, amount);
            if (ActiveDefinition.Id == type)
            {
                var chamber = Mathf.Min(amount, Mathf.RoundToInt(Bonus(UpgradeId.QuickChamber)));
                TransferReserve(ref ActiveState(), chamber, ActiveSlot);
            }

            _state.AmmoWatermark = _collectedAmmo;
            AmmoCollected?.Invoke(amount);
            return true;
        }

        internal void StartWithDirective(WeaponDefinition directive)
        {
            _instanceLoadout = true;
            _first = new WeaponInstance(1, directive, WeaponRarity.Common);
            _second = null;
            ResetState();
        }

        internal WeaponInstance CollectWeapon(WeaponInstance incoming)
        {
            var slot = HasSecondary ? ActiveSlot : 1;
            var outgoing = Instance(slot);
            if (outgoing != null)
            {
                var stored = StateAt(slot);
                stored.ReloadRemaining = 0d;
                stored.ChargeElapsed = 0d;
                stored.BurstLeft = 0;
                outgoing.Stored = stored;
            }

            if (slot == 0)
            {
                _first = incoming;
                _state.PrimaryId = incoming.InstanceId;
            }
            else
            {
                _second = incoming;
                _state.SecondaryId = incoming.InstanceId;
            }

            StateAt(slot) = incoming.Stored;
            _state.SwitchRemaining = 0d;
            return outgoing;
        }

        private int ReserveFor(int slot)
        {
            return _instanceLoadout ? Definition(slot) is { } definition ? Ammo(definition.Id) : 0 : StateAt(slot).Reserve;
        }
        internal WeaponTuning Tuning(int slot) => Definition(slot)?.Tuning ?? _fallback;
        internal int Capacity(int slot) => Tuning(slot).Magazine + Tuning(slot).MagazinePerLevel * (_upgrades?.Level(UpgradeId.ExtendedMagazine) ?? 0);
        internal float BaseDamage(WeaponDefinition definition) => (definition?.Tuning ?? _fallback).Damage * (1f + Bonus(UpgradeId.OverclockedReceiver));
        internal float DamageFor(int slot) => BaseDamage(Definition(slot));
        internal float PelletsFor(int slot) => Instance(slot)?.Modifier(WeaponModifier.SlugConversion) is > 0f and var slug ? slug : Tuning(slot).Pellets;
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
            if (_instanceLoadout || definition == null || _secondary != null)
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

        internal int CacheRounds(int slot)
        {
            return slot == 0 ? 45 : _secondary?.Id switch
            {
                WeaponId.Deadline => 6,
                WeaponId.Directive => 30,
                _ => 12
            };
        }

        internal void GrantAmmoCache()
        {
            _state.Primary.Reserve += CacheRounds(0);
            if (HasSecondary)
            {
                _state.Secondary.Reserve += CacheRounds(1);
            }
        }

        internal void ResetState()
        {
            _state = default;
            _ammoPickups.Clear();
            _collectedAmmo = default;
            _state.Primary = StartingState(0);
            if (_instanceLoadout)
            {
                _state.PrimaryId = _first.InstanceId;
                _state.SecondaryId = _second?.InstanceId ?? 0;
                _state.Stock.Add(_first.Definition.Id, _first.Definition.Tuning.Reserve);
            }

            if (HasSecondary)
            {
                _state.Secondary = StartingState(1);
            }
        }

        internal Snapshot Capture() => _state;

        internal void Restore(in Snapshot snapshot)
        {
            if (_instanceLoadout && (snapshot.PrimaryId != _first.InstanceId || snapshot.SecondaryId != (_second?.InstanceId ?? 0)))
            {
                return;
            }

            _state = snapshot;
            if (_instanceLoadout)
            {
                _state.Stock.Reconcile(_collectedAmmo, _state.AmmoWatermark);
                _state.AmmoWatermark = _collectedAmmo;
            }

            LastShotBoosted = false;
        }

        internal void Simulate(double step, bool fire, bool reload, bool canShoot, Ray aim, Vector3 aimUp, TemporalProjectileEmitter projectiles, TemporalMode mode = TemporalMode.Normal, WeaponSlotInput selection = WeaponSlotInput.None, bool fireHeld = false, GravityCamera camera = null)
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

            weapon.RecoilIdle = Mathf.Max(0f, weapon.RecoilIdle - (float)step);
            if (weapon.RecoilIdle <= 0f)
            {
                weapon.RecoilIndex = 0;
            }

            if (AdvanceSwitch(step, selection))
            {
                return;
            }

            if (weapon.ReloadRemaining > 0d)
            {
                weapon.ReloadRemaining = Math.Max(0d, weapon.ReloadRemaining - step);
                if (weapon.ReloadRemaining <= Tolerance)
                {
                    TransferReserve(ref weapon, ReserveFor(oldSlot), oldSlot);
                    weapon.ReloadRemaining = 0d;
                }
            }
            else if (canShoot && (reload || weapon.Magazine == 0) && weapon.Magazine < Capacity(oldSlot) && ReserveFor(oldSlot) > 0)
            {
                weapon.ChargeElapsed = 0d;
                weapon.ReloadDuration = ReloadFor(oldSlot);
                weapon.ReloadRemaining = weapon.ReloadDuration;
            }

            var tuning = Tuning(oldSlot);
            var trigger = ActiveType switch
            {
                WeaponType.Charged => fireHeld,
                WeaponType.SemiAutomatic => fire,
                _ => fire || fireHeld
            };
            var burst = Instance(oldSlot)?.Modifier(WeaponModifier.BurstModule) ?? 0f;
            var queued = weapon.BurstLeft > 0;
            if (!canShoot || (!trigger && !queued) || IsReloading || weapon.Magazine <= 0)
            {
                weapon.ChargeElapsed = 0d;
                weapon.BurstLeft = 0;
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
            if (burst > 0f)
            {
                weapon.BurstLeft = queued ? weapon.BurstLeft - 1 : 2;
            }

            var interval = weapon.BurstLeft > 0 ? burst : tuning.Interval;
            weapon.Cooldown = Math.Max(0d, interval / cycleMultiplier - carriedTime);
            weapon.ChargeElapsed = 0d;
            weapon.Magazine--;
            if (weapon.Magazine == 0)
            {
                weapon.BurstLeft = 0;
            }

            if (PrecisionReady)
            {
                _state.AimSeconds = 0f;
            }

            LastShotBoosted = HasQueuedBoost;
            _effects?.ConfirmShot();
            var recoil = ActiveDefinition?.GameplayRecoil ?? _fallbackRecoil;
            var aimRecoil = Mathf.Lerp(1f, (ActiveDefinition?.Aim.Recoil ?? 1f) * (1f - Bonus(UpgradeId.FocusProtocol)), AimBlend);
            var stabilizer = Instance(oldSlot)?.Modifier(WeaponModifier.GyroscopicStabilizer) ?? 0f;
            var impulse = recoil.Impulse(weapon.RecoilIndex, 1f - stabilizer);
            impulse.x *= 1f - stabilizer;
            camera?.AddRecoil(impulse * aimRecoil, recoil.Duration);
            weapon.RecoilIndex++;
            weapon.RecoilIdle = Mathf.Max(0.38f, (float)tuning.Interval + tuning.Charge + 0.15f);
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
            ActiveState().BurstLeft = 0;
            _state.SwitchTarget = target;
            _state.SwitchRemaining = _switchSeconds;
            SwitchStarted?.Invoke();
            return true;
        }

        private bool PrecisionReady => _state.AimSeconds >= 0.99999f && Bonus(UpgradeId.PrecisionLock) > 0f;

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
            var instance = Instance(ActiveSlot);
            var chamber = instance?.Modifier(WeaponModifier.RicochetChamber) ?? 0f;
            var matrix = Bonus(UpgradeId.RicochetMatrix);
            var reflection = Mathf.Max(chamber, matrix);
            var ricochets = (chamber > 0f ? 1 : 0) + (matrix > 0f ? 1 : 0);
            var targets = Mathf.Min(4, tuning.Targets + Mathf.RoundToInt(Bonus(UpgradeId.PenetrationProtocol)) + (PrecisionReady ? 1 : 0));
            var slug = instance?.Modifier(WeaponModifier.SlugConversion) ?? 0f;
            var overload = lastRound ? instance?.Modifier(WeaponModifier.OverloadCell) ?? 0f : 0f;
            var effects = new ProjectileBallistics(ActiveDefinition?.Id ?? WeaponId.Compliance, targets, ricochets, tuning.PenetrationRetention, reflection, terminal?.Magnitude ?? 0f, terminal?.Limit ?? 0f, close?.Magnitude ?? 0f, close?.Radius ?? 0f, instance?.Modifier(WeaponModifier.Splitter) ?? 0f, instance?.Modifier(WeaponModifier.ExplosiveCore) ?? 0f, instance?.Modifier(WeaponModifier.CryoPayload) ?? 0f, instance?.Modifier(WeaponModifier.PhaseAccelerator) ?? 0f, overload > 0f);
            var pellets = slug > 0f ? 1 : tuning.Pellets;
            var radius = slug > 0f ? tuning.Radius * 1.45f : tuning.Radius;
            var spread = slug > 0f ? 0f : tuning.Spread * AimSpread;
            var fan = instance?.Modifier(WeaponModifier.FanOutput) ?? 0f;
            damage *= Mathf.Max(1f, slug) * Mathf.Max(1f, overload);
            if (!projectiles.TryLaunchVolley(aim, aimUp, tuning.Muzzle, ProjectileSpeed(mode), tuning.Lifetime, tuning.Range, radius, damage, _hitLayers, HasQueuedBoost, hypervelocity, effects, pellets, spread, twin ? twinDamage : 0f, TwinOffset, fan))
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
            var transferred = Mathf.Clamp(rounds, 0, Mathf.Max(0, Mathf.Min(Capacity(slot) - weapon.Magazine, ReserveFor(slot))));
            weapon.Magazine += transferred;
            if (_instanceLoadout)
            {
                _state.Stock.Add(Definition(slot).Id, -transferred);
            }
            else
            {
                weapon.Reserve -= transferred;
            }

            return transferred;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugEquip(WeaponDefinition definition)
        {
            if (_instanceLoadout)
            {
                if (definition != null)
                {
                    var id = -(int)definition.Id - 2;
                    if (Instance(0)?.InstanceId == id || Instance(1)?.InstanceId == id)
                    {
                        DebugActivate(Instance(0)?.InstanceId == id ? 0 : 1);
                        return;
                    }

                    DebugEquipInstance(new WeaponInstance(id, definition, WeaponRarity.Common));
                }

                return;
            }

            _secondary = definition;
            _state.Secondary = StartingState(1);
            DebugActivate(1);
        }

        internal void DebugEquipInstance(WeaponInstance instance)
        {
            var slot = HasSecondary ? ActiveSlot : 1;
            CollectWeapon(instance);
            DebugActivate(slot);
        }

        internal void DebugSetAmmo(WeaponId type, int amount)
        {
            _state.Stock.Add(type, Mathf.Max(0, amount) - Ammo(type));
        }

        internal void DebugRefill()
        {
            if (_instanceLoadout)
            {
                for (var slot = 0; slot < (HasSecondary ? 2 : 1); slot++)
                {
                    StateAt(slot).Magazine = Capacity(slot);
                    var definition = Definition(slot);
                    DebugSetAmmo(definition.Id, definition.Tuning.Reserve);
                }

                return;
            }

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
            internal int PrimaryId;
            internal int SecondaryId;
            internal float AimBlend;
            internal float AimSeconds;
            internal bool Aiming;
            internal AmmoStock Stock;
            internal AmmoStock AmmoWatermark;

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
        internal readonly bool Melee;
        internal readonly bool Explosion;

        internal ProjectileImpact(Vector3 point, Vector3 normal, DamageResult damage, bool weakPoint, bool shielded, bool fromPlayer, bool ricochet = false, bool piercing = false, bool melee = false, bool explosion = false)
        {
            Point = point;
            Normal = normal;
            Damage = damage;
            WeakPoint = weakPoint;
            Shielded = shielded;
            FromPlayer = fromPlayer;
            Ricochet = ricochet;
            Piercing = piercing;
            Melee = melee;
            Explosion = explosion;
        }
    }
}
