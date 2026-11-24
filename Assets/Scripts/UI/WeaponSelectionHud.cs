using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class WeaponSelectionHud : MonoBehaviour
    {
        [SerializeField] private SectorRoute _route;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private Button[] _buttons;

        private Tween _fade;

        private void Awake()
        {
            for (var index = 0; index < _buttons.Length; index++)
            {
                var choice = index;
                _buttons[index].onClick.AddListener(() => Choose(choice));
            }

            _overlay.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
        }

        private void Update()
        {
            if (!_route.SelectingSecondary)
            {
                if (_overlay.gameObject.activeSelf)
                {
                    Hide();
                }

                return;
            }

            if (!_overlay.gameObject.activeSelf)
            {
                _overlay.gameObject.SetActive(true);
                _overlay.alpha = _director.ReducedMotion ? 1f : 0f;
                if (!_director.ReducedMotion)
                {
                    _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, 1f, 0.18f).SetUpdate(true);
                }

                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

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

        private void Choose(int index)
        {
            if (_route.ConfirmSecondary(index))
            {
                Hide();
            }
        }

        private void Hide()
        {
            _fade?.Kill();
            _overlay.gameObject.SetActive(false);
        }
    }
}
