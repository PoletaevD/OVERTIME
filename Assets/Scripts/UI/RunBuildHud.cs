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
            else if (_mode != _director.Mode)
            {
                RefreshStats();
            }

            var show = !_director.RewardSelection && _director.HasLivingPlayer && Keyboard.current?.tabKey.isPressed == true;
            if (show != _inspecting)
            {
                SetInspecting(show);
            }

            var effects = _progression.PlayerEffects.State;
            Timer(0, effects.Kinetic, "KINETIC  {0:0.0}");
            Timer(1, effects.SecondDraft, "DRAFT  {0:0.0}");
            Timer(2, effects.QualityAssurance, "QA  {0:0.0}");
            Timer(3, effects.ExploitWindow, "EXPLOIT  {0:0.0}");
            Timer(4, effects.SafeReturn, "SAFE  {0:0.0}");
            Timer(5, effects.AutoFeed, "FEED  {0:0.0}");
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
                var icon = _icons[index];
                icon.gameObject.SetActive(true);
                icon.sprite = definition.Icon;
                icon.color = ProtocolCard.CategoryColor(definition.Category);
                _levels[index].SetText("{0:0}", level);
                var column = index < 9 ? _left : _right;
                column.Append("<color=#E8F4F7><b>").Append(definition.Title).Append("</b>  /  L").Append(level).Append("</color>  ").AppendLine(definition.Numbers);
                column.AppendLine(definition.Description).AppendLine();
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
            _weaponStats.SetText("ARC-01   /   {0:0} DMG     {1:0} RPM     {2:0} MAG     {3:0.00} s RELOAD     {4:0.0} m/s", gun.EffectiveDamage, gun.RoundsPerMinute(_mode), gun.MagazineCapacity, gun.ReloadSeconds, gun.ProjectileSpeed(_mode));
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
