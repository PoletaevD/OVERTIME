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
        [SerializeField] private TMP_Text _lootHint;
        [SerializeField] private TMP_Text _ammoNotice;

        private DamageNumbers _numbers;
        private DamageIndicators _indicators;
        private Tween _remainingPulse;
        private int _remaining = -1;

        private Tween _healthPulse;
        private Sequence _ammoNoticeFade;
        private Tween _lootHintFade;
        private string _lastLootHint;
        private RawImage[] _modulePlates;
        private Tween _energyPulse;
        private Tween _hitPulse;
        private Tween _stationTween;
        private Tween _weaponPulse;
        private int _slot = -1;
        private WeaponDefinition _activeDefinition;
        private WeaponDefinition _stowedDefinition;
        private int _stowedSlot = -1;
        private string _weaponAction;
        private float _hitWindow;
        private int _groupedDamage;
        private bool _groupedKill;
        private bool _groupedWeak;
        private bool _groupedShield;
        private Tween _deathTween;
        private CanvasGroup _stationGroup;
        private Color _healthColor;
        private float _energy = -1f;
        private float _maximumEnergy = -1f;
        private int _health = -1;
        private bool _nearStation;
        private bool _showDeath;
        private bool _wasGameOver;

        private void Awake()
        {
            var layout = new CombatHudLayout();
            layout.Apply(transform);
            if (_lootHint != null)
            {
                _modulePlates = layout.CreateModulePlates(_lootHint.rectTransform);
            }

            _ammoLabel.enabled = false;
            _numbers = new DamageNumbers((RectTransform)transform, _player.GetComponentInChildren<Camera>(), _hitLabel);
            _stationGroup = _stationHint.GetComponent<CanvasGroup>();
            _healthPulse = BuildPulse(0.34f, PresentHealthPulse);
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
            _director.DamageConfirmed += _numbers.Show;
            _director.ProjectileHit += OnImpact;
            _player.Combat.Weapon.SwitchStarted += OnSwitch;
            _player.Combat.Weapon.AmmoCollected += OnAmmoCollected;
            _player.Combat.Health.Damaged += OnDamage;
            _director.ModeChanged += OnModeChanged;
            _director.DirectionalDamage += OnDirectionalDamage;
        }

        private void OnDisable()
        {
            _director.DamageConfirmed -= _numbers.Show;
            _numbers.Clear();
            _director.ProjectileHit -= OnImpact;
            _player.Combat.Weapon.SwitchStarted -= OnSwitch;
            _player.Combat.Weapon.AmmoCollected -= OnAmmoCollected;
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
            _ammoNoticeFade?.Kill();
            _lootHintFade?.Kill();
            _energyPulse.Kill();
            _hitPulse.Kill();
            _stationTween?.Kill();
            _weaponPulse?.Kill();
            _deathTween?.Kill();
            _remainingPulse?.Kill();
            _numbers?.Dispose();
            _indicators?.Dispose();
        }

        private void LateUpdate()
        {
            _numbers.Project(_player.ReducedMotion);
            var combat = _player.Combat;
            RefreshHealth(combat.Health);
            RefreshAmmo(combat.Weapon);
            RefreshLootHint();
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

        private void OnAmmoCollected(int amount)
        {
            if (_ammoNotice == null)
            {
                return;
            }

            _ammoNotice.SetText("+{0:0} AMMO", amount);
            _ammoNotice.alpha = 1f;
            _ammoNoticeFade?.Kill();
            _ammoNoticeFade = DOTween.Sequence().SetUpdate(true).AppendInterval(0.8f);
            _ammoNoticeFade.Append(DOTween.To(() => _ammoNotice.alpha, value => _ammoNotice.alpha = value, 0f, _player.ReducedMotion ? 0f : 0.24f));
        }

        private void RefreshLootHint()
        {
            if (_lootHint == null)
            {
                return;
            }

            var hint = _director.RewardSelection ? string.Empty : _director.LootHint;
            if (hint == _lastLootHint)
            {
                return;
            }

            _lastLootHint = hint;
            var hovered = string.IsNullOrEmpty(hint) ? null : _director.HoveredWeapon;
            var count = Mathf.Min(_modulePlates.Length, hovered?.Affixes.Count ?? 0);
            for (var index = 0; index < _modulePlates.Length; index++)
            {
                var plate = _modulePlates[index];
                plate.enabled = index < count;
                if (plate.enabled)
                {
                    var kind = (int)hovered.Affixes[index].Kind;
                    plate.uvRect = new Rect(kind % 5 * 0.2f, kind < 5 ? 0.5f : 0f, 0.2f, 0.5f);
                    plate.rectTransform.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * 48f, -7f);
                }
            }

            _lootHintFade?.Kill();
            _lootHint.SetText(hint);
            _lootHint.alpha = string.IsNullOrEmpty(hint) ? 0f : 1f;
            if (!string.IsNullOrEmpty(hint) && !_player.ReducedMotion)
            {
                _lootHint.alpha = 0f;
                _lootHintFade = DOTween.To(() => _lootHint.alpha, value => _lootHint.alpha = value, 1f, 0.12f).SetUpdate(true);
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

            _healthLabel.color = health.Current * 4 <= health.Maximum ? s_damage : s_text;
            _healthLabel.SetText("<b><i>{0:0}</i></b> <size=38%> / {1:0}  HP</size>", health.Current, health.Maximum);
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
            var definition = weapon.Definition(stowedSlot);
            if (_stowedDefinition != definition || _stowedSlot != stowedSlot)
            {
                _stowedSlot = stowedSlot;
                _stowedDefinition = definition;
                _stowedLabel.SetText($"[{stowedSlot + 1}]   {weapon.Name(stowedSlot)}");
            }
        }

        private void RefreshEnergy()
        {
            var energy = _director.Energy;
            var value = energy.Current;
            if (!Mathf.Approximately(_energy, value) || !Mathf.Approximately(_maximumEnergy, energy.Maximum))
            {
                _energyLabel.SetText("<b>{0:0}</b> <size=70%>CHRONO</size>", value, energy.Maximum);
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

            if (shot.Shielded)
            {
                _numbers.Block(shot.Point);
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
            _hitMarker.SetText(blocked ? "[ ]" : _groupedKill ? "<b>X</b>" : _groupedWeak ? "< >" : "x");
            _hitLabel.SetText(blocked ? "SHIELD BLOCK" : _groupedKill ? "TARGET DESTROYED" : _groupedWeak ? "WEAK POINT" : string.Empty);
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
                _numbers.Clear();
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
