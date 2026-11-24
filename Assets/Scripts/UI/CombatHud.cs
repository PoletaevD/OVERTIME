using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class CombatHud : MonoBehaviour
    {
        private static readonly Color s_text = new(0.91f, 0.95f, 0.97f);
        private static readonly Color s_chrono = new(0.27f, 0.88f, 0.90f);
        private static readonly Color s_damage = new(1f, 0.36f, 0.26f);
        private static readonly Color s_weak = new(1f, 0.74f, 0.34f);

        [SerializeField] private TemporalDirector _director;
        [SerializeField] private PlaygroundPlayer _player;
        [SerializeField] private TMP_Text _healthLabel;
        [SerializeField] private Image _healthFill;
        [SerializeField] private CanvasGroup _damageFlash;
        [SerializeField] private TMP_Text _ammoLabel;
        [SerializeField] private TMP_Text _reloadLabel;
        [SerializeField] private Image _reloadFill;
        [SerializeField] private Image _weaponIcon;
        [SerializeField] private TMP_Text _stowedLabel;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private TMP_Text _energyLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Image _energyFill;
        [SerializeField] private TMP_Text _stationHint;
        [SerializeField] private TMP_Text _hitMarker;
        [SerializeField] private TMP_Text _hitLabel;
        [SerializeField] private CanvasGroup _hitGroup;
        [SerializeField] private CanvasGroup _deathGroup;
        [SerializeField] private TMP_Text _deathTitle;
        [SerializeField] private TMP_Text _deathInfo;
        [SerializeField] private Button _restart;
        [SerializeField] private SecurityEnemy[] _enemies;
        [SerializeField] private TMP_Text _remainingLabel;
        [SerializeField] private Sprite _damageArc;

        private DamageIndicators _indicators;
        private Tween _remainingPulse;
        private int _remaining = -1;

        private Tween _healthPulse;
        private Tween _ammoPulse;
        private Tween _energyPulse;
        private Tween _hitPulse;
        private Tween _stationTween;
        private Tween _weaponPulse;
        private int _slot = -1;
        private WeaponDefinition _activeDefinition;
        private WeaponDefinition _stowedDefinition;
        private int _stowedMagazine = -1;
        private int _stowedReserve = -1;
        private string _weaponAction;
        private float _hitWindow;
        private int _groupedDamage;
        private bool _groupedKill;
        private bool _groupedWeak;
        private bool _groupedShield;
        private Tween _deathTween;
        private CanvasGroup _stationGroup;
        private Color _healthColor;
        private Color _ammoColor;
        private float _energy = -1f;
        private float _maximumEnergy = -1f;
        private int _health = -1;
        private int _magazine = -1;
        private int _capacity = -1;
        private int _reserve = -1;
        private bool _nearStation;
        private bool _showDeath;
        private bool _wasGameOver;

        private void Awake()
        {
            _stationGroup = _stationHint.GetComponent<CanvasGroup>();
            _healthPulse = BuildPulse(0.34f, PresentHealthPulse);
            _ammoPulse = BuildPulse(0.16f, value => _ammoLabel.color = Color.Lerp(s_text, _ammoColor, value));
            _energyPulse = BuildPulse(0.42f, value => _energyFill.color = Color.Lerp(s_chrono, Color.white, value));
            _hitPulse = BuildPulse(0.22f, PresentHitPulse);
            _restart.onClick.AddListener(_director.RequestRestart);
            if (_weaponIcon != null)
            {
                _weaponPulse = _weaponIcon.rectTransform.DOScale(1f, 0.20f).From(0.84f, false).SetAutoKill(false).SetUpdate(true).Pause();
            }
            var legacy = transform.Find("Directional Damage");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }

            var arc = _damageArc != null ? _damageArc : Resources.Load<Sprite>("Damage Arc");
            if (arc != null)
            {
                _indicators = new DamageIndicators(transform, arc);
            }

            if (_remainingLabel != null)
            {
                _remainingPulse = BuildPulse(0.38f, value => _remainingLabel.color = Color.Lerp(s_chrono, s_weak, value));
            }
        }

        private void OnEnable()
        {
            _director.ProjectileHit += OnImpact;
            _player.Combat.Weapon.SwitchStarted += OnSwitch;
            _player.Combat.Health.Damaged += OnDamage;
            _director.ModeChanged += OnModeChanged;
            _director.DirectionalDamage += OnDirectionalDamage;
        }

        private void OnDisable()
        {
            _director.ProjectileHit -= OnImpact;
            _player.Combat.Weapon.SwitchStarted -= OnSwitch;
            _player.Combat.Health.Damaged -= OnDamage;
            _director.ModeChanged -= OnModeChanged;
            _director.DirectionalDamage -= OnDirectionalDamage;
            _hitPulse.Pause();
            _hitGroup.alpha = 0f;
            _indicators?.Clear();
        }

        private void OnDestroy()
        {
            _restart.onClick.RemoveListener(_director.RequestRestart);
            _healthPulse.Kill();
            _ammoPulse.Kill();
            _energyPulse.Kill();
            _hitPulse.Kill();
            _stationTween?.Kill();
            _weaponPulse?.Kill();
            _deathTween?.Kill();
            _remainingPulse?.Kill();
            _indicators?.Dispose();
        }

        private void LateUpdate()
        {
            var combat = _player.Combat;
            RefreshHealth(combat.Health);
            RefreshAmmo(combat.Weapon);
            RefreshEnergy();
            RefreshDeath(combat);
            if (_remainingLabel != null)
            {
                RefreshEncounter();
            }

            if (_director.CanAffectWorld)
            {
                _indicators?.Orient(_player.CameraRig);
            }
            else
            {
                _indicators?.Clear();
            }
        }

        private void RefreshEncounter()
        {
            var remaining = 0;
            foreach (var enemy in _enemies)
            {
                remaining += enemy.Health.Alive ? 1 : 0;
            }

            if (_remaining == remaining)
            {
                return;
            }

            _remainingLabel.SetText(remaining > 0 ? "HOSTILES  {0:00}" : "AREA SECURE  /  F5", remaining);
            if (_remaining >= 0 && !_player.ReducedMotion)
            {
                _remainingPulse.Restart();
            }

            _remaining = remaining;
        }

        private void OnDirectionalDamage(Vector3 source, int damage)
        {
            _indicators?.Signal(_player.CameraRig, source, damage, _player.ReducedMotion);
        }

        private void RefreshHealth(CombatHealth health)
        {
            if (_health == health.Current)
            {
                return;
            }

            _healthLabel.SetText("VITALS  {0:0} / {1:0}", health.Current, health.Maximum);
            SetFill(_healthFill, (float)health.Current / health.Maximum);
            if (_health >= 0 && health.Current > _health)
            {
                PulseHealth(s_chrono);
            }

            _health = health.Current;
        }

        private void OnDamage(DamageResult damage)
        {
            PulseHealth(s_damage);
        }

        private void PulseHealth(Color color)
        {
            _healthColor = color;
            PresentHealthPulse(1f);
            _healthPulse.Restart();
        }

        private void PresentHealthPulse(float amount)
        {
            var intensity = amount * (_player.ReducedMotion ? 0.55f : 1f);
            _healthFill.color = Color.Lerp(s_text, _healthColor, intensity);
            _damageFlash.alpha = _healthColor == s_damage ? intensity * 0.65f : 0f;
        }

        private void RefreshAmmo(CombatWeapon weapon)
        {
            if (_magazine != weapon.Magazine || _reserve != weapon.Reserve || _capacity != weapon.MagazineCapacity)
            {
                _ammoLabel.SetText("{0:00} <size=55%>/ {1:00}    RES {2:000}</size>", weapon.Magazine, weapon.MagazineCapacity, weapon.Reserve);
                _ammoColor = weapon.Magazine == 0 ? s_damage : s_chrono;
                if (!_director.IsPlayerProtected)
                {
                    _ammoPulse.Restart();
                }

                _magazine = weapon.Magazine;
                _capacity = weapon.MagazineCapacity;
                _reserve = weapon.Reserve;
            }

            var action = weapon.IsSwitching ? "EXCHANGING" : weapon.IsReloading ? "RELOADING" : weapon.IsCharging ? "CHARGING" : weapon.Magazine == 0 ? (weapon.Reserve > 0 ? "R  /  RELOAD" : "NO AMMUNITION") : string.Empty;
            if (_actionLabel == null)
            {
                _reloadLabel.SetText(action.Length == 0 ? weapon.ActiveName : action);
            }
            else
            {
                RefreshLoadout(weapon, action);
            }

            _reloadFill.color = weapon.IsCharging ? s_chrono : s_weak;
            SetFill(_reloadFill, weapon.IsSwitching ? weapon.SwitchProgress : weapon.IsCharging ? weapon.ChargeProgress : weapon.ReloadProgress);
        }

        private void OnSwitch()
        {
            if (!_player.ReducedMotion)
            {
                _weaponPulse?.Restart();
            }
        }

        private void RefreshLoadout(CombatWeapon weapon, string action)
        {
            if (_activeDefinition != weapon.ActiveDefinition || _slot != weapon.ActiveSlot)
            {
                _activeDefinition = weapon.ActiveDefinition;
                _slot = weapon.ActiveSlot;
                _weaponIcon.sprite = _activeDefinition?.Icon;
                _weaponIcon.enabled = _weaponIcon.sprite != null;
                _reloadLabel.SetText(weapon.ActiveName);
            }

            if (_weaponAction != action)
            {
                _weaponAction = action;
                _actionLabel.SetText(action);
            }

            _stowedLabel.gameObject.SetActive(weapon.HasSecondary);
            if (!weapon.HasSecondary)
            {
                return;
            }

            var stowedSlot = 1 - _slot;
            var stowed = weapon.State(stowedSlot);
            var definition = weapon.Definition(stowedSlot);
            if (_stowedMagazine != stowed.Magazine || _stowedReserve != stowed.Reserve || _stowedDefinition != definition)
            {
                _stowedMagazine = stowed.Magazine;
                _stowedReserve = stowed.Reserve;
                _stowedDefinition = definition;
                _stowedLabel.SetText($"[{stowedSlot + 1}]   {weapon.Name(stowedSlot)}   {stowed.Magazine:00} / {stowed.Reserve:000}");
            }
        }

        private void RefreshEnergy()
        {
            var energy = _director.Energy;
            var value = energy.Current;
            if (!Mathf.Approximately(_energy, value) || !Mathf.Approximately(_maximumEnergy, energy.Maximum))
            {
                _energyLabel.SetText("CHRONO  {0:0.0} / {1:0}", value, energy.Maximum);
                SetFill(_energyFill, value / energy.Maximum);
                if (_energy >= 0f && value > _energy)
                {
                    _energyPulse.Restart();
                }

                _energy = value;
                _maximumEnergy = energy.Maximum;
            }

            var cost = _director.EnergyCost;
            _costLabel.color = energy.Empty ? s_damage : s_text;
            if (energy.Empty)
            {
                _costLabel.SetText("CHRONO DEPLETED");
            }
            else if (_director.Mode == TemporalMode.Reset && _director.PlayerTimeline.AtStart && _director.WorldTimeline.AtStart)
            {
                _costLabel.SetText("HISTORY START  /  NO DRAIN");
            }
            else if (cost > 0f)
            {
                _costLabel.SetText(_director.Mode == TemporalMode.BulletTime ? "-{0:0} / REAL SECOND" : "-{0:0.0} / REWOUND SECOND", cost);
            }
            else
            {
                _costLabel.SetText(string.Empty);
            }

            var near = _director.CanRecharge && !_showDeath && _director.CanAffectWorld;
            if (_nearStation != near)
            {
                _nearStation = near;
                Fade(_stationGroup, ref _stationTween, near);
            }
        }

        private void RefreshDeath(PlayerCombat combat)
        {
            var show = !combat.Alive;
            if (show != _showDeath)
            {
                _showDeath = show;
                Fade(_deathGroup, ref _deathTween, show);
            }

            if (!show)
            {
                _wasGameOver = false;
                _restart.gameObject.SetActive(false);
                _deathGroup.blocksRaycasts = false;
                return;
            }

            if (combat.GameOver)
            {
                if (!_wasGameOver)
                {
                    _deathTitle.SetText("GAME OVER");
                    _deathInfo.SetText("F5  /  RESTART DRILL");
                    _restart.gameObject.SetActive(true);
                    _deathGroup.blocksRaycasts = true;
                }
            }
            else
            {
                _deathTitle.SetText(_director.RecoveryAvailable ? "TEMPORAL RECOVERY AVAILABLE" : "CRITICAL DAMAGE");
                _deathInfo.SetText("Q  RESET      C  RECALL      {0:1} s", (float)combat.RecoveryRemaining);
            }

            _wasGameOver = combat.GameOver;
        }

        private void OnImpact(ProjectileImpact shot)
        {
            if (!shot.FromPlayer || (shot.Damage.Amount == 0 && !shot.Shielded))
            {
                return;
            }

            var fresh = Time.unscaledTime >= _hitWindow;
            if (fresh)
            {
                _hitWindow = Time.unscaledTime + 0.075f;
                _groupedDamage = 0;
                _groupedKill = _groupedWeak = _groupedShield = false;
            }

            _groupedDamage += shot.Damage.Amount;
            _groupedKill |= shot.Damage.Killed;
            _groupedWeak |= shot.WeakPoint;
            _groupedShield |= shot.Shielded;
            var blocked = _groupedDamage == 0 && _groupedShield;
            _hitMarker.color = blocked ? new Color(0.48f, 0.70f, 1f) : _groupedKill ? s_damage : _groupedWeak ? s_weak : s_text;
            _hitLabel.color = _hitMarker.color;
            _hitMarker.SetText(blocked ? "[]" : _groupedKill ? "X" : _groupedWeak ? "<>" : "+");
            _hitLabel.SetText(blocked ? "SHIELD BLOCK" : _groupedKill ? "TARGET DESTROYED" : _groupedWeak ? "WEAK POINT  {0:0}" : "{0:0}", _groupedDamage);
            if (fresh)
            {
                PresentHitPulse(1f);
                _hitPulse.Restart();
            }
        }

        private void OnModeChanged(TemporalMode mode)
        {
            if (!_director.CanDamageWorld)
            {
                _hitWindow = 0f;
                _hitPulse.Pause();
                _hitGroup.alpha = 0f;
                _indicators?.Clear();
            }
        }

        private void PresentHitPulse(float value)
        {
            _hitGroup.alpha = Mathf.Clamp01(value * 1.9f);
            _hitMarker.rectTransform.localScale = Vector3.one * (1f + (_player.ReducedMotion ? 0f : value * 0.2f));
        }

        private void Fade(CanvasGroup group, ref Tween tween, bool visible)
        {
            tween?.Kill();
            if (_player.ReducedMotion)
            {
                group.alpha = visible ? 1f : 0f;
                return;
            }

            tween = DOTween.To(() => group.alpha, value => group.alpha = value, visible ? 1f : 0f, 0.18f).SetUpdate(true).SetEase(Ease.OutCubic);
        }

        private static Tween BuildPulse(float seconds, TweenCallback<float> present)
        {
            return DOVirtual.Float(1f, 0f, seconds, present).SetUpdate(true).SetAutoKill(false).SetEase(Ease.OutQuad).Pause();
        }

        private static void SetFill(Image image, float value)
        {
            image.rectTransform.localScale = new Vector3(Mathf.Clamp01(value), 1f, 1f);
        }
    }
}
