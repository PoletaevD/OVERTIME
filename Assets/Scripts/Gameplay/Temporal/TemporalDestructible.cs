using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalDestructible : TemporalActor<bool>
    {
        [SerializeField] private GameObject _intact;
        [SerializeField] private GameObject _damaged;
        [SerializeField] private Collider _hitbox;

        private bool _alive;

        internal bool TryHit(Collider hit)
        {
            if (!_alive || hit != _hitbox)
            {
                return false;
            }

            RestoreState(false);
            return true;
        }

        internal override void Simulate(float step)
        {
        }

        internal override void ResetState()
        {
            RestoreState(true);
        }

        private protected override bool CaptureState()
        {
            return _alive;
        }

        private protected override void RestoreState(in bool snapshot)
        {
            _alive = snapshot;
            if (_intact.activeSelf != snapshot)
            {
                _intact.SetActive(snapshot);
            }

            if (_damaged.activeSelf == snapshot)
            {
                _damaged.SetActive(!snapshot);
            }
        }
    }
}
