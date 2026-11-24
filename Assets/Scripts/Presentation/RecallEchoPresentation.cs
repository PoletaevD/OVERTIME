using UnityEngine;

namespace Overtime
{
    internal sealed class RecallEchoPresentation : MonoBehaviour
    {
        internal const float Duration = 0.42f;

        [SerializeField] private RunProgression _progression;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private LineRenderer _outline;

        private void LateUpdate()
        {
            var echo = _progression.Echo;
            _outline.enabled = echo.Remaining > 0f;
            if (!_outline.enabled)
            {
                return;
            }

            var progress = 1f - echo.Remaining / Duration;
            var phase = progress < 0.24f ? Mathf.Lerp(0.48f, 0.11f, progress / 0.24f) : Mathf.Lerp(0.11f, 1f, Mathf.Sqrt((progress - 0.24f) / 0.76f));
            var radius = echo.Radius * (_director.ReducedMotion ? 0.76f : phase);
            _outline.transform.position = echo.Position;
            for (var index = 0; index < _outline.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / _outline.positionCount;
                var height = progress < 0.24f && (index & 1) == 0 ? 0.45f : 0.06f;
                _outline.SetPosition(index, new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)) * radius);
            }

            var color = new Color(1f, 0.49f, 0.27f, echo.Remaining / Duration * 0.8f);
            _outline.startColor = color;
            _outline.endColor = color;
        }

        internal readonly struct Snapshot
        {
            internal readonly Vector3 Position;
            internal readonly float Radius;
            internal readonly float Remaining;

            internal Snapshot(Vector3 position, float radius, float remaining)
            {
                Position = position;
                Radius = radius;
                Remaining = remaining;
            }

            internal Snapshot Advance(float step)
            {
                return new Snapshot(Position, Radius, Mathf.Max(0f, Remaining - step));
            }
        }
    }
}
