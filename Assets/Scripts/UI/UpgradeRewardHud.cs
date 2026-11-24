using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overtime
{
    internal sealed class UpgradeRewardHud : MonoBehaviour
    {
        [SerializeField] private SectorRoute _route;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private ProtocolCard[] _cards;

        private Tween _fade;
        private bool _open;

        private void Awake()
        {
            for (var index = 0; index < _cards.Length; index++)
            {
                var card = index;
                _cards[index].Button.onClick.AddListener(() => Choose(card));
            }

            _overlay.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
        }

        private void Update()
        {
            var showProtocols = _director.RewardSelection && !_route.SelectingSecondary && !_route.SelectingRoute;
            if (showProtocols != _open)
            {
                if (showProtocols)
                {
                    Show();
                    return;
                }

                Hide();
            }

            if (!_open)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    Choose(0);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    Choose(1);
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    Choose(2);
                }
            }

            var pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            var count = _route.Progression.Selection.Count;
            for (var index = 0; index < count; index++)
            {
                _cards[index].Tilt(pointer, _director.ReducedMotion);
            }
        }

        private void Show()
        {
            _open = true;
            _fade?.Kill();
            _overlay.gameObject.SetActive(true);
            _overlay.alpha = _director.ReducedMotion ? 1f : 0f;
            _overlay.interactable = true;
            _overlay.blocksRaycasts = true;
            var progression = _route.Progression;
            var count = progression.Selection.Count;
            for (var index = 0; index < _cards.Length; index++)
            {
                if (index < count)
                {
                    _cards[index].Present(progression.Selection.Offer(index), progression.Build, index, count, _director.ReducedMotion);
                }
                else
                {
                    _cards[index].gameObject.SetActive(false);
                }
            }

            if (!_director.ReducedMotion)
            {
                _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, 1f, 0.17f).SetUpdate(true);
            }
        }

        private void Choose(int index)
        {
            if (!_open || !_route.ConfirmUpgrade(index))
            {
                return;
            }

            _cards[index].MarkSelected();
            Hide();
        }

        private void Hide()
        {
            _open = false;
            _overlay.interactable = false;
            _overlay.blocksRaycasts = false;
            _fade?.Kill();
            if (_director.ReducedMotion)
            {
                _overlay.gameObject.SetActive(false);
                return;
            }

            _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, 0f, 0.19f).SetUpdate(true).OnComplete(() => _overlay.gameObject.SetActive(false));
        }
    }
}
