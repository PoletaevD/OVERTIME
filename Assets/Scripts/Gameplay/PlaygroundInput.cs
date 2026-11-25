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
        private InputAction _reload;
        private InputAction _interact;
        private InputAction _melee;
        private InputAction _aim;
        private InputAction _primaryWeapon;
        private InputAction _secondaryWeapon;
        private InputAction _swapWeapon;
        private WeaponSlotInput _weaponSelection;
        private InputAction _combatRestart;
        private InputAction _rechargeChrono;
        private InputAction _rewind;
        private InputAction _override;
        private InputAction _recall;
        private InputAction _bulletTime;
        private InputActionMap _playerActions;
        private TemporalSelection _temporalSelection;
        private int _focusBlockedButtons;
        private bool _focused;
        private bool _captured;
        private bool _discardLook;
        private bool _combatBindings;
        private bool _blockFireHold;
        private bool _gameOver;
        private bool _rewardSelection;
        private bool _blockJumpHold;
        private bool _captureRequested;
        private bool _fireRequested;
        private bool _reloadRequested;
        private bool _restartRequested;
        private bool _rechargeRequested;

        internal Vector2 Move => _captured ? _move.ReadValue<Vector2>() : Vector2.zero;
        internal bool JumpPressed => _captured && !_blockJumpHold && _jump.WasPressedThisFrame();
        internal bool JumpHeld => _captured && !_blockJumpHold && _jump.IsPressed();
        internal bool Focused => _focused;
        internal bool FirePressed { get; private set; }
        internal bool FireHeld => _captured && !_blockFireHold && _attack.IsPressed();
        internal bool ResetPressed => _focused && (_combatBindings ? _restartRequested : Keyboard.current?.rKey.wasPressedThisFrame == true);
        internal WeaponSlotInput WeaponSelection => _captured && !_rewardSelection ? _weaponSelection : WeaponSlotInput.None;
        internal bool ReloadPressed => _captured && _reloadRequested;
        internal bool InteractPressed => _captured && !_rewardSelection && _interact.WasPressedThisFrame();
        internal bool MeleePressed => _captured && !_rewardSelection && _melee.WasPressedThisFrame();
        internal bool AimHeld => _captured && !_rewardSelection && _aim.IsPressed();
        internal bool RechargePressed => _captured && _rechargeRequested;
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
            _reload = map.FindAction("Reload", true);
            _interact = map.FindAction("Interact", true);
            _melee = map.FindAction("Melee", true);
            _aim = map.FindAction("Aim", true);
            _primaryWeapon = map.FindAction("PrimaryWeapon", true);
            _secondaryWeapon = map.FindAction("SecondaryWeapon", true);
            _swapWeapon = map.FindAction("SwapWeapon", true);
            _combatRestart = map.FindAction("CombatRestart", true);
            _rechargeChrono = map.FindAction("RechargeChrono", true);
            _playerActions = map;
            _rewind = map.FindAction("Rewind", true);
            _override = map.FindAction("OverrideTime", true);
            _recall = map.FindAction("RecallTime", true);
            _bulletTime = map.FindAction("BulletTime", true);
        }

        private void OnEnable()
        {
            _playerActions.actionTriggered += OnActionTriggered;
            _instance.Enable();
            _focused = Application.isFocused;
#if !UNITY_WEBGL || UNITY_EDITOR
            SetCapture(_focused && !Application.isEditor);
#endif
        }

        private void OnDisable()
        {
            _playerActions.actionTriggered -= OnActionTriggered;
            _temporalSelection = default;
            _focusBlockedButtons = 0;
            ClearCombatCommands();
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
                _focusBlockedButtons |= _temporalSelection.Held;
                _temporalSelection = default;
                ClearCombatCommands();
                SetCapture(false);
            }
        }

        internal void UseCombatBindings(bool active)
        {
            _combatBindings = active;
        }

        internal void SetGameOver(bool active)
        {
            _gameOver = active;
            if (active)
            {
                SetCapture(false);
            }
        }

        internal void SetRewardSelection(bool active)
        {
            _rewardSelection = active;
            _focusBlockedButtons |= _temporalSelection.Held;
            _temporalSelection = default;
            FirePressed = false;
            ClearCombatCommands();
            _blockFireHold = true;
            _blockJumpHold = true;
            SetCapture(!active && _focused && !_gameOver);
        }

        internal void ClearCombatCommands()
        {
            _captureRequested = false;
            _fireRequested = false;
            _reloadRequested = false;
            _weaponSelection = WeaponSlotInput.None;
            _restartRequested = false;
            _rechargeRequested = false;
        }

        internal TemporalSelection TakeTemporalSelection()
        {
            var selection = _temporalSelection;
            _temporalSelection.ClearReleases();
            return selection;
        }

        private void OnActionTriggered(InputAction.CallbackContext context)
        {
            if (!_focused)
            {
                return;
            }

            var action = context.action;
            if (_combatBindings && context.performed && action == _combatRestart)
            {
                _restartRequested = true;
            }

            if (_combatBindings && context.performed && !_rewardSelection)
            {
                var attack = action == _attack;
                _captureRequested |= attack && context.control.device is Mouse;
                _fireRequested |= attack;
                _reloadRequested |= action == _reload;
                _rechargeRequested |= action == _rechargeChrono;
                if (action == _primaryWeapon || action == _secondaryWeapon)
                {
                    _weaponSelection = action == _primaryWeapon ? WeaponSlotInput.Primary : WeaponSlotInput.Secondary;
                }
                else if (action == _swapWeapon && Mathf.Abs(context.ReadValue<float>()) > 0.01f)
                {
                    _weaponSelection = WeaponSlotInput.Toggle;
                }
            }

            var mode = action == _rewind ? TemporalMode.Reset : action == _override ? TemporalMode.Override : action == _recall ? TemporalMode.Recall : action == _bulletTime ? TemporalMode.BulletTime : TemporalMode.Normal;
            if (mode == TemporalMode.Normal)
            {
                return;
            }

            var button = 1 << (int)mode;
            if (context.canceled)
            {
                _focusBlockedButtons &= ~button;
                _temporalSelection.Release(mode);
            }
            else if (context.performed && _rewardSelection)
            {
                _focusBlockedButtons |= button;
            }
            else if (context.performed && (_focusBlockedButtons & button) == 0)
            {
                _temporalSelection.Press(mode);
            }
        }

        internal void Poll()
        {
            FirePressed = false;
            if (!_focused || _gameOver || _rewardSelection)
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

            if (!_captured && (_captureRequested || Mouse.current?.leftButton.wasPressedThisFrame == true))
            {
                SetCapture(true);
                _blockFireHold = true;
                return;
            }

            if (!_jump.IsPressed())
            {
                _blockJumpHold = false;
            }

            if (!_attack.IsPressed())
            {
                _blockFireHold = false;
            }

            FirePressed = _captured && !_blockFireHold && (_combatBindings ? _fireRequested : _attack.WasPressedThisFrame());
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
