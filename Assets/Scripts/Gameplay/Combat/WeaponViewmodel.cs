using UnityEngine;

namespace Overtime
{
    internal sealed class WeaponViewmodel : MonoBehaviour
    {
        [SerializeField] private Transform _module;
        [SerializeField] private Transform _flash;
        [SerializeField] private Transform _slide;
        [SerializeField] private Renderer[] _chargeRails;

        private Renderer[] _renderers;
        private bool[] _rendererStates;
        private bool _scoped;

        internal void SetScopeView(bool scoped)
        {
            if (_scoped == scoped)
            {
                return;
            }

            _scoped = scoped;
            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
                _rendererStates = new bool[_renderers.Length];
            }

            for (var index = 0; index < _renderers.Length; index++)
            {
                var renderer = _renderers[index];
                if (scoped)
                {
                    _rendererStates[index] = renderer.forceRenderingOff;
                }

                renderer.forceRenderingOff = scoped || _rendererStates[index];
            }
        }

        internal Transform Module => _module;
        internal Transform Slide => _slide;
        internal Transform Flash => _flash;
        internal Renderer[] ChargeRails => _chargeRails;
    }
}
