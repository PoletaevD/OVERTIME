using System.Collections.Generic;
using UnityEngine;

namespace Overtime
{
    internal abstract class TemporalActor : MonoBehaviour
    {
        internal abstract void Initialize(int capacity);
        internal abstract void ResetState();
        internal abstract void Simulate(float step);
        internal abstract void Capture(int slot);
        internal abstract void Restore(int slot);

        internal virtual void ReleaseHistory()
        {
        }

        internal virtual void CollectColliders(List<Collider> colliders)
        {
        }

        internal virtual void Present(TemporalTimeline timeline, TemporalMode mode, bool showTraces)
        {
        }
    }

    internal abstract class TemporalActor<T> : TemporalActor where T : struct
    {
        private T[] _history;

        internal override void Initialize(int capacity)
        {
            _history = new T[capacity];
            ResetState();
        }

        internal override void ReleaseHistory()
        {
            _history = null;
        }

        internal sealed override void Capture(int slot)
        {
            _history[slot] = CaptureState();
        }

        internal sealed override void Restore(int slot)
        {
            RestoreState(_history[slot]);
        }

        private protected T Read(TemporalTimeline timeline, long tick)
        {
            return _history[timeline.Slot(tick)];
        }

        private protected abstract T CaptureState();
        private protected abstract void RestoreState(in T snapshot);
    }
}
