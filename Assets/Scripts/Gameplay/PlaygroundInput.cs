using UnityEngine;
using UnityEngine.InputSystem;

namespace Overtime
{
    internal sealed class PlaygroundInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;

        private InputActionAsset _instance;
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _rewind;
        private bool _focused;
        private bool _captured;
        private bool _discardLook;

        internal Vector2 Move => _captured ? _move.ReadValue<Vector2>() : Vector2.zero;
        internal bool JumpPressed => _captured && _jump.WasPressedThisFrame();
        internal bool JumpHeld => _captured && _jump.IsPressed();
        internal bool RewindHeld => _focused && _rewind.IsPressed();
        internal bool FirePressed { get; private set; }
        internal bool ResetPressed => _focused && Keyboard.current?.rKey.wasPressedThisFrame == true;
        internal bool DebugPressed => _focused && Keyboard.current?.f3Key.wasPressedThisFrame == true;
        internal bool HintsPressed => _focused && Keyboard.current?.hKey.wasPressedThisFrame == true;
        internal bool ReducedMotionPressed => _focused && Keyboard.current?.f4Key.wasPressedThisFrame == true;

        private void Awake()
        {
            _instance = Instantiate(_actions);
            var map = _instance.FindActionMap("Player", true);
            _move = map.FindAction("Move", true);
            _look = map.FindAction("Look", true);
            _jump = map.FindAction("Jump", true);
            _attack = map.FindAction("Attack", true);
            _rewind = map.FindAction("Rewind", true);
        }

        private void OnEnable()
        {
            _instance.Enable();
            _focused = Application.isFocused;
#if !UNITY_WEBGL || UNITY_EDITOR
            SetCapture(_focused && !Application.isEditor);
#endif
        }

        private void OnDisable()
        {
            SetCapture(false);
            _instance.Disable();
        }

        private void OnDestroy()
        {
            Destroy(_instance);
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused)
            {
                SetCapture(false);
            }
        }

        internal void Poll()
        {
            FirePressed = false;
            if (!_focused)
            {
                return;
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                SetCapture(false);
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                _captured = false;
            }

            if (!_captured && Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                SetCapture(true);
                return;
            }

            FirePressed = _captured && _attack.WasPressedThisFrame();
        }

        internal void SuppressFire()
        {
            FirePressed = false;
        }

        internal Vector2 ReadLook(float deltaTime)
        {
            if (!_captured || _discardLook)
            {
                _discardLook = false;
                return Vector2.zero;
            }

            var look = _look.ReadValue<Vector2>();
            return _look.activeControl?.device is Gamepad ? look * (900f * deltaTime) : look;
        }

        private void SetCapture(bool active)
        {
            _captured = active;
            _discardLook = true;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }
    }
}
