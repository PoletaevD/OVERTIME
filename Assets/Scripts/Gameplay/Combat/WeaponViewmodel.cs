using UnityEngine;

namespace Overtime
{
    internal sealed class WeaponViewmodel : MonoBehaviour
    {
        [SerializeField] private Transform _module;
        [SerializeField] private Transform _flash;
        [SerializeField] private Transform _slide;
        [SerializeField] private Renderer[] _chargeRails;

        internal Transform Module => _module;
        internal Transform Slide => _slide;
        internal Transform Flash => _flash;
        internal Renderer[] ChargeRails => _chargeRails;
    }
}
