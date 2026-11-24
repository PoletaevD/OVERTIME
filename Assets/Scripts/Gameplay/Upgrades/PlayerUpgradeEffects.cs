using UnityEngine;

namespace Overtime
{
    internal sealed class PlayerUpgradeEffects
    {
        private readonly RunUpgradeState _build;
        private readonly GravityMotor _motor;
        private readonly CombatWeapon _weapon;
        private Snapshot _state;
        private bool _canFeed = true;

        internal PlayerUpgradeEffects(RunUpgradeState build, GravityMotor motor, CombatWeapon weapon)
        {
            _build = build;
            _motor = motor;
            _weapon = weapon;
            _motor.SurfaceChanged += OnSurfaceChanged;
        }

        internal Snapshot State => _state;
        internal bool Charged => _state.Kinetic > 0f || _state.SecondDraft > 0f || _state.Exchange > 0f;
        internal float IncomingDamageMultiplier => Protection(_state.QualityAssurance, UpgradeId.QualityAssurance) * Protection(_state.SafeReturn, UpgradeId.SafeReturn);

        internal float ModifyShotDamage(float damage)
        {
            var freefall = _motor.Grounded ? 1f : 1f + _build.Bonus(UpgradeId.Freefall);
            var vertical = _motor.Magnetic && _motor.Grounded && Vector3.Dot(_motor.Up, Vector3.up) < 0.5f ? 1f + _build.Bonus(UpgradeId.VerticalAdvantage) : 1f;
            return damage * Boost(_state.Kinetic, UpgradeId.KineticCharge) * Boost(_state.SecondDraft, UpgradeId.SecondDraft) * Boost(_state.ExploitWindow, UpgradeId.ExploitWindow) * Boost(_state.Exchange, UpgradeId.TacticalExchange) * freefall * vertical;
        }

        internal void ConfirmShot()
        {
            _state.Kinetic = 0f;
            _state.SecondDraft = 0f;
            _state.Exchange = 0f;
        }

        internal void CompleteSwitch()
        {
            if (_state.ExchangeCooldown <= 0f && _build.Definition(UpgradeId.TacticalExchange) is { } exchange)
            {
                _state.Exchange = exchange.Seconds;
                _state.ExchangeCooldown = exchange.Limit;
            }
        }

        internal void Simulate(float step, bool canFeed = true)
        {
            _canFeed = canFeed;
            _state.Exchange = Mathf.Max(0f, _state.Exchange - step);
            _state.ExchangeCooldown = Mathf.Max(0f, _state.ExchangeCooldown - step);
            _state.Kinetic = Mathf.Max(0f, _state.Kinetic - step);
            _state.AutoFeed = Mathf.Max(0f, _state.AutoFeed - step);
            _state.SecondDraft = Mathf.Max(0f, _state.SecondDraft - step);
            _state.QualityAssurance = Mathf.Max(0f, _state.QualityAssurance - step);
            _state.ExploitWindow = Mathf.Max(0f, _state.ExploitWindow - step);
            _state.SafeReturn = Mathf.Max(0f, _state.SafeReturn - step);
        }

        internal bool Complete(TemporalMode mode)
        {
            switch (mode)
            {
                case TemporalMode.Reset:
                    _state.SecondDraft = Duration(UpgradeId.SecondDraft);
                    _state.QualityAssurance = Duration(UpgradeId.QualityAssurance);
                    return _state.SecondDraft > 0f || _state.QualityAssurance > 0f;
                case TemporalMode.Override:
                    _state.ExploitWindow = Duration(UpgradeId.ExploitWindow);
                    return _state.ExploitWindow > 0f;
                case TemporalMode.Recall:
                    _state.SafeReturn = Duration(UpgradeId.SafeReturn);
                    return _state.SafeReturn > 0f;
                default:
                    return false;
            }
        }

        internal void Restore(in Snapshot snapshot)
        {
            _state = snapshot;
        }

        internal void Unbind()
        {
            _motor.SurfaceChanged -= OnSurfaceChanged;
        }

        private void OnSurfaceChanged()
        {
            _state.Kinetic = Duration(UpgradeId.KineticCharge);
            var feed = _build.Definition(UpgradeId.AutoFeed);
            if (_canFeed && _motor.Magnetic && feed != null && _state.AutoFeed <= 0f && _weapon.TryAutoFeed(Mathf.RoundToInt(_build.Bonus(UpgradeId.AutoFeed))))
            {
                _state.AutoFeed = feed.Seconds;
            }
        }

        private float Duration(UpgradeId id)
        {
            return _build.Definition(id)?.Seconds ?? 0f;
        }

        private float Boost(float remaining, UpgradeId id)
        {
            return remaining > 0f ? 1f + _build.Bonus(id) : 1f;
        }

        private float Protection(float remaining, UpgradeId id)
        {
            return remaining > 0f ? 1f - Mathf.Clamp01(_build.Bonus(id)) : 1f;
        }

        internal struct Snapshot
        {
            internal float Kinetic;
            internal float AutoFeed;
            internal float SecondDraft;
            internal float QualityAssurance;
            internal float ExploitWindow;
            internal float SafeReturn;
            internal float Exchange;
            internal float ExchangeCooldown;
        }
    }
}
