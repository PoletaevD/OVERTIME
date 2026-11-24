using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Overtime.Tests
{
    internal sealed class SyntheticKeyboard : IDisposable
    {
        private readonly InputSettings _original;
        private readonly InputSettings _settings;

        internal Keyboard Device { get; }

        internal SyntheticKeyboard()
        {
            _original = InputSystem.settings;
            _settings = Object.Instantiate(_original);
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = _settings;
            Device = InputSystem.AddDevice<Keyboard>();
        }

        public void Dispose()
        {
            InputSystem.RemoveDevice(Device);
            InputSystem.settings = _original;
            Object.Destroy(_settings);
        }
    }
}
