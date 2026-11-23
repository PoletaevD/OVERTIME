using System;
using UnityEngine;

namespace Overtime
{
    [RequireComponent(typeof(CapsuleCollider))]
    internal sealed class GravityMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 9f;
        [SerializeField, Min(0.1f)] private float _acceleration = 60f;
        [SerializeField, Min(0.1f)] private float _deceleration = 72f;
        [SerializeField, Range(0f, 1f)] private float _airControl = 0.32f;
        [SerializeField, Min(0.1f)] private float _jumpSpeed = 8.3f;
        [SerializeField, Min(0f)] private float _coyoteTime = 0.105f;
        [SerializeField, Min(0f)] private float _jumpBuffer = 0.13f;
        [SerializeField, Min(0.1f)] private float _maximumSpeed = 13f;

        [Header("Gravity")]
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [SerializeField] private LayerMask _attachmentLayers = ~0;
        [SerializeField, Min(0.5f)] private float _detectionDistance = 1.47f;
        [SerializeField, Min(0.1f)] private float _attachmentAcceleration = 52f;
        [SerializeField, Min(0.1f)] private float _gravityForce = 26f;
        [SerializeField, Min(0.01f)] private float _rotationDuration = 0.21f;
        [SerializeField, Range(0.1f, 1f)] private float _transitionSensitivity = 0.46f;
        [SerializeField, Range(0.1f, 0.99f)] private float _normalHysteresis = 0.82f;
        [SerializeField, Min(0f)] private float _detachmentGrace = 0.34f;
        [SerializeField, Min(0f)] private float _reattachDelay = 0.36f;
        [SerializeField, Min(0f)] private float _attachmentHoldDuration = 0.11f;

        private CapsuleCollision _collision;
        private GravitySurfaceProbe _probe;
        private Vector3 _center;
        private Quaternion _orientation;
        private Quaternion _rotationStart;
        private Quaternion _rotationTarget;
        private Vector3 _blockedNormal;
        private Vector3 _edgeUp;
        private float _blockedTime;
        private float _rotationProgress;
        private float _surfaceLock;
        private float _timeWithoutGround;
        private float _jumpRemaining;
        private float _ignoreGround;
        private float _holdTime;
        private float _edgeHeight = float.NegativeInfinity;
        private bool _magnetic;

        internal Vector3 Velocity { get; private set; }
        internal Vector3 Up { get; private set; } = Vector3.up;
        internal Vector3 ContactNormal { get; private set; }
        internal Collider Surface { get; private set; }
        internal bool Grounded => Surface != null;
        internal bool Magnetic => _magnetic;
        internal bool Transitioning => _rotationProgress < 1f;
        internal bool Obstructed { get; private set; }
        internal float Speed => Vector3.ProjectOnPlane(Velocity, Up).magnitude;
        internal float SpeedRatio => Mathf.Clamp01(Speed / _walkSpeed);

        internal event Action<float> Landed;
        internal event Action Jumped;
        internal event Action<Quaternion> GravityChanged;

        private void Awake()
        {
            _collision = new CapsuleCollision(GetComponent<CapsuleCollider>(), _collisionLayers);
            _probe = new GravitySurfaceProbe(_attachmentLayers);
            ResetAt(transform.position, transform.rotation);
        }

        internal void ResetAt(Vector3 position, Quaternion orientation)
        {
            _center = position;
            _orientation = orientation;
            Up = orientation * Vector3.up;
            Velocity = Vector3.zero;
            Surface = null;
            ContactNormal = Vector3.zero;
            _magnetic = false;
            _rotationProgress = 1f;
            _surfaceLock = 0f;
            _ignoreGround = 0f;
            _blockedTime = 0f;
            _timeWithoutGround = float.PositiveInfinity;
            _jumpRemaining = 0f;
            _holdTime = 0f;
            _edgeHeight = float.NegativeInfinity;
            transform.SetPositionAndRotation(_center, _orientation);
        }

        internal void QueueJump()
        {
            _jumpRemaining = _jumpBuffer;
        }

        internal void Step(Vector2 input, Vector3 heading, bool jumpHeld, float deltaTime)
        {
            _blockedTime = Mathf.Max(0f, _blockedTime - deltaTime);
            _surfaceLock = Mathf.Max(0f, _surfaceLock - deltaTime);
            _ignoreGround = Mathf.Max(0f, _ignoreGround - deltaTime);
            _holdTime = jumpHeld ? _holdTime + deltaTime : 0f;
            var wish = GravityFrame.MoveDirection(input, heading, Up);
            var hasSupport = FindSupport(out _);
            _timeWithoutGround = hasSupport ? 0f : _timeWithoutGround + deltaTime;

            var aim = wish.sqrMagnitude > 0.01f ? wish.normalized : GravityFrame.Tangent(heading, Up);
            var canRoundEdge = wish.sqrMagnitude > 0.01f && !hasSupport && _timeWithoutGround <= _detachmentGrace;
            var candidate = default(RaycastHit);
            var edgeHeight = float.NegativeInfinity;
            var wantsAttachment = jumpHeld && _surfaceLock <= 0f && !Transitioning && _probe.Find(_center, Up, aim, _blockedTime > 0f ? _blockedNormal : Vector3.zero, _detectionDistance, _transitionSensitivity, _normalHysteresis, canRoundEdge, out candidate, out edgeHeight);
            var decidingAttachment = wantsAttachment && _holdTime < _attachmentHoldDuration;
            if (wantsAttachment && !decidingAttachment)
            {
                var oldUp = Up;
                ChangeGravity(candidate.normal.normalized, heading, true);
                _edgeUp = oldUp;
                _edgeHeight = edgeHeight;
                wish = GravityFrame.Transport(oldUp, Up, heading) * wish;
                hasSupport = false;
                _jumpRemaining = 0f;
            }
            else if (_magnetic && _timeWithoutGround > _detachmentGrace && !Transitioning)
            {
                var oldUp = Up;
                ChangeGravity(Vector3.up, heading, false);
                wish = GravityFrame.Transport(oldUp, Up, heading) * wish;
                hasSupport = false;
            }

            UpdateCapsuleRotation(deltaTime);
            var velocity = Accelerate(wish, hasSupport, deltaTime);
            if (_jumpRemaining > 0f && !decidingAttachment && _timeWithoutGround <= _coyoteTime && _ignoreGround <= 0f && _surfaceLock <= 0f)
            {
                _blockedNormal = Up;
                _blockedTime = _reattachDelay;
                _ignoreGround = 0.14f;
                _timeWithoutGround = _coyoteTime + deltaTime;
                _jumpRemaining = 0f;
                velocity = Vector3.ProjectOnPlane(velocity, Up) + Up * _jumpSpeed;
                Jumped?.Invoke();
            }

            if (!decidingAttachment)
            {
                _jumpRemaining -= deltaTime;
            }
            var downwardSpeed = Mathf.Max(0f, Vector3.Dot(-Up, velocity));
            Obstructed = _collision.Move(ref _center, _orientation * Vector3.up, ref velocity, deltaTime);
            Velocity = velocity;
            UpdateContact(downwardSpeed);
            transform.SetPositionAndRotation(_center, _orientation);
        }

        private Vector3 Accelerate(Vector3 wish, bool hasSupport, float deltaTime)
        {
            var vertical = Vector3.Dot(Velocity, Up);
            var horizontal = Vector3.ProjectOnPlane(Velocity, Up);
            var target = wish * _walkSpeed;
            if (wish.sqrMagnitude > 0.001f || hasSupport)
            {
                var rate = wish.sqrMagnitude > 0.001f ? _acceleration : _deceleration;
                horizontal = Vector3.MoveTowards(horizontal, target, rate * (hasSupport ? 1f : _airControl) * deltaTime);
            }

            var gravity = _magnetic && vertical <= 0f ? _attachmentAcceleration : _gravityForce;
            vertical = hasSupport ? -2.4f : Mathf.Max(-35f, vertical - gravity * deltaTime);
            if (Vector3.Dot(_center, _edgeUp) > _edgeHeight && !float.IsNegativeInfinity(_edgeHeight))
            {
                // Clear the old top before pulling inward so a convex turn cannot cut through the lip.
                vertical = 0.85f;
            }
            else
            {
                _edgeHeight = float.NegativeInfinity;
            }

            return Vector3.ClampMagnitude(horizontal, _maximumSpeed) + Up * vertical;
        }

        private bool FindSupport(out RaycastHit hit)
        {
            var axis = _orientation * Vector3.up;
            hit = default;
            return _ignoreGround <= 0f && _collision.Cast(_center, axis, -Up, 0.12f, out hit) && Vector3.Dot(hit.normal, Up) > 0.69f;
        }

        private void UpdateContact(float downwardSpeed)
        {
            var wasGrounded = Grounded;
            if (!FindSupport(out var ground))
            {
                Surface = null;
                ContactNormal = Vector3.zero;
                return;
            }

            Surface = ground.collider;
            ContactNormal = ground.normal;
            if (!wasGrounded && downwardSpeed > 1.8f)
            {
                Landed?.Invoke(Mathf.Clamp01(downwardSpeed / 12f));
            }
        }

        private void ChangeGravity(Vector3 nextUp, Vector3 heading, bool magnet)
        {
            var previousUp = Up;
            var transport = GravityFrame.Transport(previousUp, nextUp, heading);
            var tangent = Vector3.ProjectOnPlane(Velocity, previousUp);
            Velocity = magnet ? transport * tangent : Velocity;
            Up = nextUp;
            _magnetic = magnet && Vector3.Dot(nextUp, Vector3.up) < 0.999f;
            _edgeHeight = float.NegativeInfinity;

            _blockedNormal = previousUp;
            _blockedTime = _reattachDelay;
            _surfaceLock = _rotationDuration;
            _timeWithoutGround = 0f;
            _rotationProgress = 0f;
            _rotationStart = _orientation;
            _rotationTarget = transport * _orientation;
            Surface = null;
            GravityChanged?.Invoke(transport);
        }

        private void UpdateCapsuleRotation(float deltaTime)
        {
            if (!Transitioning)
            {
                return;
            }

            var progress = Mathf.Min(1f, _rotationProgress + deltaTime / _rotationDuration);
            var rotation = Quaternion.Slerp(_rotationStart, _rotationTarget, Mathf.SmoothStep(0f, 1f, progress));
            if (_collision.Resolve(ref _center, rotation, 0.16f))
            {
                _rotationProgress = progress;
                _orientation = rotation;
                return;
            }

            // A tight obstruction can delay body rotation. Revert to the last clear axis if it persists.
            if (_timeWithoutGround > _detachmentGrace + _rotationDuration)
            {
                var clearUp = _orientation * Vector3.up;
                var transport = GravityFrame.Transport(Up, clearUp, _orientation * Vector3.forward);
                Up = clearUp;
                _rotationProgress = 1f;
                _timeWithoutGround = _detachmentGrace + deltaTime;
                _magnetic = Vector3.Dot(Up, Vector3.up) < 0.999f;
                GravityChanged?.Invoke(transport);
            }
        }
    }
}
