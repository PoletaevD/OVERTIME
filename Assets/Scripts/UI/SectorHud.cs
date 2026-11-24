using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Overtime
{
    internal sealed class SectorHud : MonoBehaviour
    {
        [SerializeField] private SectorRoute _route;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private PlaygroundPlayer _player;
        [SerializeField] private Camera _view;
        [SerializeField] private TMP_Text _roomNumber;
        [SerializeField] private TMP_Text _roomTitle;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TMP_Text _exitArrow;
        [SerializeField] private CanvasGroup _intro;
        [SerializeField] private TMP_Text _introText;
        [SerializeField] private CanvasGroup _success;
        [SerializeField] private TMP_Text _stats;
        [SerializeField] private TMP_Text _debug;

        private Tween _banner;
        private Tween _finish;
        private int _roomIndex = -1;
        private EncounterState _state;
        private int _living = -1;
        private bool _finished;

        private void OnDestroy()
        {
            _banner?.Kill();
            _finish?.Kill();
        }

        private void LateUpdate()
        {
            var room = _route.Current;
            var roomChanged = _roomIndex != _route.RoomIndex;
            if (roomChanged)
            {
                _roomIndex = _route.RoomIndex;
                _roomNumber.SetText("ROOM {0:00}", _roomIndex + 1);
                _roomTitle.SetText(room.RoomName.ToUpperInvariant());
            }

            if (roomChanged || _state != room.State)
            {
                _state = room.State;
                _living = -1;
                _exitArrow.gameObject.SetActive(_state == EncounterState.Cleared);
                if (_state == EncounterState.Cleared)
                {
                    _status.SetText("CLEAR    /    +30 RESERVE");
                    Announce("CLEAR");
                }
                else if (_state == EncounterState.Waiting)
                {
                    _status.SetText("ENTER COMBAT ZONE");
                }
                else if (_state == EncounterState.Active)
                {
                    Announce(_roomTitle.text);
                }
            }

            if (_state == EncounterState.Active && room.LivingEnemies != _living)
            {
                _living = room.LivingEnemies;
                _status.SetText("HOSTILES  {0:00}", _living);
            }

            if (_state == EncounterState.Cleared)
            {
                var bearing = _view.transform.InverseTransformPoint(room.Exit);
                var angle = Mathf.Atan2(-bearing.x, bearing.z) * Mathf.Rad2Deg;
                _exitArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (_route.Finished && !_finished)
            {
                ShowSuccess();
            }

            if (_debug.gameObject.activeInHierarchy && _director.PlayerTimeline != null)
            {
                var player = _director.PlayerProjectiles;
                var enemy = _route.EnemyProjectiles;
                _debug.SetText("PROJECTILES  PLAYER {0:00} / ENEMY {1:00}   CAP {2:00}\nENCOUNTER {3:00}   LIVING {4:00}   WORLD ACTORS {5:00}", player.ActiveCount, enemy.ActiveCount, player.PoolCapacity + enemy.PoolCapacity, _roomIndex + 1, room.LivingEnemies, _director.WorldActorCount);
            }
        }

        private void Announce(string label)
        {
            _banner?.Kill();
            _introText.SetText(label);
            _intro.alpha = _player.ReducedMotion ? 1f : 0f;
            _banner = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _intro.alpha, value => _intro.alpha = value, 1f, _player.ReducedMotion ? 0f : 0.14f))
                .AppendInterval(0.78f)
                .Append(DOTween.To(() => _intro.alpha, value => _intro.alpha = value, 0f, _player.ReducedMotion ? 0f : 0.3f));
        }

        private void ShowSuccess()
        {
            _finished = true;
            var seconds = (int)_route.Elapsed;
            _stats.SetText($"TIME  {seconds / 60:00}:{seconds % 60:00}\nHOSTILES DOWN  {_route.EnemiesDefeated:00}\nTEMPORAL USES  {_route.PowersUsed:00}\nVITALS  {_player.Combat.Health.Current:0}");
            _finish = DOTween.To(() => _success.alpha, value => _success.alpha = value, 1f, _player.ReducedMotion ? 0f : 0.38f).SetUpdate(true).SetEase(Ease.OutCubic);
        }
    }
}
