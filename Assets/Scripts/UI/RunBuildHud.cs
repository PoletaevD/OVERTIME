using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class RunBuildHud : MonoBehaviour
    {
        [SerializeField] private RunProgression _progression;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private PlaygroundPlayer _player;
        [SerializeField] private Image[] _icons;
        [SerializeField] private TMP_Text[] _levels;
        [SerializeField] private TMP_Text[] _timers;
        [SerializeField] private CanvasGroup _protection;
        [SerializeField] private CanvasGroup _notice;
        [SerializeField] private TMP_Text _noticeText;
        [SerializeField] private CanvasGroup _inspection;
        [SerializeField] private TMP_Text _weaponStats;
        [SerializeField] private TMP_Text _chronoStats;
        [SerializeField] private TMP_Text[] _columns;

        private readonly StringBuilder _left = new(768);
        private readonly StringBuilder _right = new(768);
        private readonly StringBuilder _stats = new(256);
        private WeaponDefinition _secondary;
        private Tween _inspectFade;
        private Sequence _noticeFade;
        private int _revision = -1;
        private TemporalMode _mode;
        private bool _inspecting;

        private void OnEnable()
        {
            _progression.Acquired += ShowAcquired;
        }

        private void OnDisable()
        {
            _progression.Acquired -= ShowAcquired;
            _inspectFade?.Kill();
            _noticeFade?.Kill();
        }

        private void LateUpdate()
        {
            var build = _progression.Build;
            if (build == null)
            {
                return;
            }

            if (_revision != build.Revision)
            {
                RefreshBuild(build);
                _revision = build.Revision;
                RefreshStats();
            }
            else if (_mode != _director.Mode || _secondary != _player.Combat.Weapon.Definition(1))
            {
                RefreshStats();
            }

            var show = !_director.RewardSelection && _director.HasLivingPlayer && Keyboard.current?.tabKey.isPressed == true;
            if (show != _inspecting)
            {
                SetInspecting(show);
            }

            if (show)
            {
                RefreshStats();
            }

            var effects = _progression.PlayerEffects.State;
            Timer(0, effects.Kinetic, "KINETIC  {0:0.0}");
            Timer(1, effects.SecondDraft, "DRAFT  {0:0.0}");
            Timer(2, effects.QualityAssurance, "QA  {0:0.0}");
            Timer(3, effects.ExploitWindow, "EXPLOIT  {0:0.0}");
            Timer(4, effects.SafeReturn, "SAFE  {0:0.0}");
            Timer(5, effects.AutoFeed, "FEED  {0:0.0}");
            if (_timers.Length > 6)
            {
                Timer(6, effects.Exchange, "EXCHANGE  {0:0.0}");
            }
            _protection.alpha = effects.QualityAssurance > 0f || effects.SafeReturn > 0f ? 1f : 0f;
        }

        private void RefreshBuild(RunUpgradeState build)
        {
            var index = 0;
            _left.Clear();
            _right.Clear();
            for (var id = UpgradeId.OverclockedReceiver; id < UpgradeId.Count; id++)
            {
                if (build.Definition(id) is not { } definition)
                {
                    continue;
                }

                var level = build.Level(id);
                if (index >= _icons.Length)
                {
                    break;
                }

                var icon = _icons[index];
                icon.gameObject.SetActive(true);
                icon.sprite = definition.Icon;
                icon.color = ProtocolCard.CategoryColor(definition.Category);
                _levels[index].SetText("{0:0}", level);
                var column = index < _icons.Length / 2 ? _left : _right;
                column.Append("<color=#E8F4F7><b>").Append(definition.Title).Append("</b>  /  L").Append(level).Append("</color>  ").AppendLine(definition.Numbers);
                column.AppendLine(definition.Description);
                index++;
            }

            for (; index < _icons.Length; index++)
            {
                _icons[index].gameObject.SetActive(false);
            }

            _columns[0].SetText(_left);
            _columns[1].SetText(_right);
        }

        private void RefreshStats()
        {
            _mode = _director.Mode;
            var gun = _player.Combat.Weapon;
            var energy = _director.Energy;
            _secondary = gun.Definition(1);
            _stats.Clear();
            var count = gun.HasSecondary ? 2 : 1;
            for (var slot = 0; slot < count; slot++)
            {
                _stats.Append(gun.Name(slot)).Append("    ");
                var tuning = gun.Tuning(slot);
                if (tuning.Pellets > 1)
                {
                    _stats.Append(tuning.Pellets).Append(" Г— ");
                }

                _stats.AppendFormat("{0:0.#}", gun.DamageFor(slot));
                _stats.AppendFormat(" DMG    {0:0} RPM    {1:0} MAG    {2:0.00}s RELOAD    {3:0.#} m/s", 60f / gun.CycleSeconds(slot, _mode), gun.Capacity(slot), gun.ReloadFor(slot), gun.SpeedFor(slot, _mode));
                if (tuning.Charge > 0f)
                {
                    _stats.AppendFormat("    {0:0.00}s CHARGE", gun.ChargeSeconds(slot, _mode));
                }
                else if (gun.Definition(slot)?.Type == WeaponType.SemiAutomatic)
                {
                    _stats.Append("    SEMI-AUTO");
                }

                if (gun.Instance(slot) is { } instance)
                {
                    _stats.Append("    ").Append(instance.Rarity);
                    foreach (var modifier in instance.Affixes)
                    {
                        _stats.Append(" / ").Append(modifier.Kind);
                    }
                }

                _stats.AppendLine();
            }

            if (gun.Instance(0) != null)
            {
                var knife = _player.Combat.Knife.Tuning;
                _stats.AppendFormat("RESERVE  PISTOL {0}   CARBINE {1}   SCATTER {2}   RAIL {3}\n", gun.Ammo(WeaponId.Directive), gun.Ammo(WeaponId.Compliance), gun.Ammo(WeaponId.Termination), gun.Ammo(WeaponId.Deadline));
                _stats.AppendFormat("[F] KNIFE  {0:0} DMG / {1:0.#} m   [RMB] ADS  {2:0}° / {3:0.00} s", knife.Damage, knife.Reach, gun.ActiveDefinition.Aim.FieldOfView(gun.ActiveType), gun.ActiveDefinition.Aim.Transition);
            }

            _weaponStats.SetText(_stats);
            _chronoStats.SetText("CHRONO {0:0} MAX   /   RESET {1:0.0}     OVERRIDE {2:0.0}     RECALL {3:0.0}     BULLET TIME {4:0.0} / s", energy.Maximum, energy.Cost(TemporalMode.Reset), energy.Cost(TemporalMode.Override), energy.Cost(TemporalMode.Recall), energy.Cost(TemporalMode.BulletTime));
        }

        private void Timer(int index, float seconds, string label)
        {
            var timer = _timers[index];
            var active = seconds > 0f;
            if (timer.gameObject.activeSelf != active)
            {
                timer.gameObject.SetActive(active);
            }

            if (active)
            {
                timer.SetText(label, seconds);
            }
        }

        private void ShowAcquired(RunUpgradeDefinition definition)
        {
            _noticeText.SetText($"PROTOCOL ACQUIRED  /  {definition.Title}");
            _noticeFade?.Kill();
            _notice.alpha = 1f;
            _noticeFade = DOTween.Sequence().SetUpdate(true).AppendInterval(1.7f)
                .Append(DOTween.To(() => _notice.alpha, value => _notice.alpha = value, 0f, _player.ReducedMotion ? 0f : 0.2f));
        }

        private void SetInspecting(bool show)
        {
            _inspecting = show;
            _inspectFade?.Kill();
            if (_player.ReducedMotion)
            {
                _inspection.alpha = show ? 1f : 0f;
                return;
            }

            _inspectFade = DOTween.To(() => _inspection.alpha, value => _inspection.alpha = value, show ? 1f : 0f, 0.16f).SetUpdate(true);
        }
    }
}
