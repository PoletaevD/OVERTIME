using System;
using UnityEngine;

namespace Overtime
{
    [RequireComponent(typeof(CapsuleCollider))]
    internal sealed class GravityMotor : MonoBehaviour
    {
        internal enum TransitionReason
        {
            Spawn,
            ManualAttachment,
            InnerCorner,
            OuterCorner,
            Jump,
            EdgeRelease,
            ManualRelease,
            RotationBlocked
        }

        private const float ContactProbeDistance = 0.12f;

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
        [SerializeField, Min(0f), InspectorName("Corner Transition Grace")] private float _detachmentGrace = 0.10f;
        [SerializeField, Min(0f)] private float _reattachDelay = 0.36f;
        [SerializeField, Min(0f), InspectorName("Manual Attachment Hold Duration")] private float _attachmentHoldDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float _magneticReleaseHoldDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float _automaticCornerDetectionDistance = 0.95f;
        [SerializeField, Min(0f)] private float _automaticCornerMinimumSpeed = 1.8f;

        private CapsuleCollision _collision;
        private GravitySurfaceProbe _probe;
        private Vector3 _center;
        private Quaternion _orientation;
        private Quaternion _rotationStart;
        private Quaternion _rotationTarget;
        private RaycastHit _lastSupport;
        private RaycastHit _manualCandidate;
        private RaycastHit _outerCorner;
        private RaycastHit _previousSupport;
        private Vector3 _heldCornerTravel;
        private float _rotationProgress;
        private float _rotationBlockedTime;
        private float _cornerReverseDelay;
        private float _timeWithoutGround;
        private float _jumpRemaining;
        private float _reattachRemaining;
        private float _holdTime;
        private float _magneticHoldTime;
        private bool _magnetic;
        private bool _holdingMagneticJump;
        private bool _waitForNewPress;

        internal Vector3 Velocity { get; private set; }
        internal Vector3 Up { get; private set; } = Vector3.up;
        internal Vector3 ContactNormal { get; private set; }
        internal Collider Surface { get; private set; }
        internal Collider ManualCandidate => _manualCandidate.collider;
        internal Collider AutomaticCandidate { get; private set; }
        internal float ManualAttachmentProgress => ManualCandidate != null ? Mathf.Clamp01(_holdTime / Mathf.Max(_attachmentHoldDuration, 0.001f)) : 0f;
        internal bool JumpInFlight { get; private set; }
        internal TransitionReason CurrentTransitionReason { get; private set; }
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
            Obstructed = false;
            _magnetic = false;
            ClearMagneticHold();
            _waitForNewPress = false;
            JumpInFlight = false;
            CurrentTransitionReason = TransitionReason.Spawn;
            _lastSupport = default;
            _outerCorner = default;
            _previousSupport = default;
            AutomaticCandidate = null;
            _rotationProgress = 1f;
            _rotationBlockedTime = 0f;
            _cornerReverseDelay = 0f;
            _reattachRemaining = 0f;
            _timeWithoutGround = float.PositiveInfinity;
            _jumpRemaining = 0f;
            ClearManualCandidate();
            transform.SetPositionAndRotation(_center, _orientation);
        }

        internal Snapshot Capture()
        {
            return new Snapshot(this);
        }

        internal void SetTemporalCollision(TemporalCollision collision)
        {
            _collision.TemporalCollision = collision;
            _probe.TemporalCollision = collision;
        }

        internal void Restore(in Snapshot snapshot)
        {
            snapshot.ApplyTo(this);
            transform.SetPositionAndRotation(_center, _orientation);
        }

        internal readonly struct Snapshot
        {
            private readonly Quaternion _orientation;
            private readonly Quaternion _rotationStart;
            private readonly Quaternion _rotationTarget;
            private readonly RaycastHit _lastSupport;
            private readonly RaycastHit _manualCandidate;
            private readonly RaycastHit _outerCorner;
            private readonly RaycastHit _previousSupport;
            private readonly Vector3 _heldCornerTravel;
            private readonly float _rotationProgress;
            private readonly float _rotationBlockedTime;
            private readonly float _cornerReverseDelay;
            private readonly float _timeWithoutGround;
            private readonly float _jumpRemaining;
            private readonly float _reattachRemaining;
            private readonly float _holdTime;
            private readonly float _magneticHoldTime;
            private readonly bool _magnetic;
            private readonly bool _holdingMagneticJump;
            private readonly bool _waitForNewPress;
            private readonly Vector3 _velocity;
            private readonly Vector3 _up;
            private readonly Vector3 _contactNormal;
            private readonly Collider _surface;
            private readonly Collider _automaticCandidate;
            private readonly bool _jumpInFlight;
            private readonly TransitionReason _transitionReason;
            private readonly bool _obstructed;

            internal Vector3 Position { get; }

            internal Snapshot(GravityMotor motor)
            {
                Position = motor._center;
                _orientation = motor._orientation;
                _rotationStart = motor._rotationStart;
                _rotationTarget = motor._rotationTarget;
                _lastSupport = motor._lastSupport;
                _manualCandidate = motor._manualCandidate;
                _outerCorner = motor._outerCorner;
                _previousSupport = motor._previousSupport;
                _heldCornerTravel = motor._heldCornerTravel;
                _rotationProgress = motor._rotationProgress;
                _rotationBlockedTime = motor._rotationBlockedTime;
                _cornerReverseDelay = motor._cornerReverseDelay;
                _timeWithoutGround = motor._timeWithoutGround;
                _jumpRemaining = motor._jumpRemaining;
                _reattachRemaining = motor._reattachRemaining;
                _holdTime = motor._holdTime;
                _magneticHoldTime = motor._magneticHoldTime;
                _magnetic = motor._magnetic;
                _holdingMagneticJump = motor._holdingMagneticJump;
                _waitForNewPress = motor._waitForNewPress;
                _velocity = motor.Velocity;
                _up = motor.Up;
                _contactNormal = motor.ContactNormal;
                _surface = motor.Surface;
                _automaticCandidate = motor.AutomaticCandidate;
                _jumpInFlight = motor.JumpInFlight;
                _transitionReason = motor.CurrentTransitionReason;
                _obstructed = motor.Obstructed;
            }

            internal void ApplyTo(GravityMotor motor)
            {
                motor._center = Position;
                motor._orientation = _orientation;
                motor._rotationStart = _rotationStart;
                motor._rotationTarget = _rotationTarget;
                motor._lastSupport = _lastSupport;
                motor._manualCandidate = _manualCandidate;
                motor._outerCorner = _outerCorner;
                motor._previousSupport = _previousSupport;
                motor._heldCornerTravel = _heldCornerTravel;
                motor._rotationProgress = _rotationProgress;
                motor._rotationBlockedTime = _rotationBlockedTime;
                motor._cornerReverseDelay = _cornerReverseDelay;
                motor._timeWithoutGround = _timeWithoutGround;
                motor._jumpRemaining = _jumpRemaining;
                motor._reattachRemaining = _reattachRemaining;
                motor._holdTime = _holdTime;
                motor._magneticHoldTime = _magneticHoldTime;
                motor._magnetic = _magnetic;
                motor._holdingMagneticJump = _holdingMagneticJump;
                motor._waitForNewPress = _waitForNewPress;
                motor.Velocity = _velocity;
                motor.Up = _up;
                motor.ContactNormal = _contactNormal;
                motor.Surface = _surface;
                motor.AutomaticCandidate = _automaticCandidate;
                motor.JumpInFlight = _jumpInFlight;
                motor.CurrentTransitionReason = _transitionReason;
                motor.Obstructed = _obstructed;
            }
        }

        internal void QueueJump()
        {
            _waitForNewPress = false;
            ClearMagneticHold();
            _holdingMagneticJump = _magnetic;
            _jumpRemaining = _jumpBuffer;
        }

        internal void Step(Vector2 input, Vector3 heading, bool jumpHeld, float deltaTime)
        {
            _cornerReverseDelay = Mathf.Max(0f, _cornerReverseDelay - deltaTime);
            _reattachRemaining = Mathf.Max(0f, _reattachRemaining - deltaTime);
            if (!Transitioning)
            {
                AutomaticCandidate = null;
            }

            if (!jumpHeld)
            {
                _waitForNewPress = false;
                ClearMagneticHold();
            }

            var wish = GravityFrame.MoveDirection(input, heading, Up);
            var hasSupport = FindSupport(out var support);
            if (hasSupport)
            {
                RememberSupport(support);
            }
            else
            {
                _timeWithoutGround += deltaTime;
            }

            var liftOff = false;
            var coastPastEdge = false;
            var hasManualCandidate = UpdateManualCandidate(wish, heading, jumpHeld, deltaTime);
            var canJump = _jumpRemaining > 0f && !JumpInFlight && (!Transitioning || (_magnetic && hasSupport)) && _timeWithoutGround <= _coyoteTime;
            if (canJump && !hasManualCandidate)
            {
                liftOff = true;
                JumpInFlight = true;
                CurrentTransitionReason = TransitionReason.Jump;
                _reattachRemaining = _reattachDelay;
                _jumpRemaining = 0f;
                hasSupport = false;
                AutomaticCandidate = null;
                Surface = null;
                ContactNormal = Vector3.zero;
                _timeWithoutGround = float.PositiveInfinity;
            }
            else if (hasManualCandidate && _holdTime >= _attachmentHoldDuration)
            {
                wish = ChangeGravity(_manualCandidate.normal, heading, wish, TransitionReason.ManualAttachment);
                ClearManualCandidate();
                _jumpRemaining = 0f;
                hasSupport = false;
            }
            else if (_magnetic && !JumpInFlight)
            {
                hasSupport = FollowSurface(heading, ref wish, hasSupport, out coastPastEdge);
            }

            if (_holdingMagneticJump && ResolveMagneticHold(heading, ref wish, deltaTime))
            {
                hasSupport = false;
                coastPastEdge = false;
            }

            if (!(canJump && hasManualCandidate))
            {
                _jumpRemaining = Mathf.Max(0f, _jumpRemaining - deltaTime);
            }

            UpdateCapsuleRotation(deltaTime);
            var velocity = Accelerate(wish, hasSupport, coastPastEdge, deltaTime);
            if (liftOff)
            {
                velocity = Vector3.ProjectOnPlane(velocity, Up) + Up * _jumpSpeed;
                Jumped?.Invoke();
            }

            var downwardSpeed = Mathf.Max(0f, Vector3.Dot(-Up, velocity));
            Obstructed = _collision.Move(ref _center, _orientation * Vector3.up, ref velocity, deltaTime);
            Velocity = velocity;
            UpdateContact(downwardSpeed);
            transform.SetPositionAndRotation(_center, _orientation);
        }

        private bool UpdateManualCandidate(Vector3 wish, Vector3 heading, bool jumpHeld, float deltaTime)
        {
            var aim = wish.sqrMagnitude > 0.01f ? wish.normalized : GravityFrame.Tangent(heading, Up);
            if (!jumpHeld || _magnetic || _waitForNewPress || Transitioning || _reattachRemaining > 0f || !_probe.FindManual(_center, Up, aim, _detectionDistance, _transitionSensitivity, _normalHysteresis, out var candidate))
            {
                ClearManualCandidate();
                return false;
            }

            if (_manualCandidate.collider != candidate.collider || Vector3.Dot(_manualCandidate.normal, candidate.normal) < 0.99f)
            {
                _holdTime = 0f;
            }

            _manualCandidate = candidate;
            _holdTime += deltaTime;
            return true;
        }

        private void ClearManualCandidate()
        {
            _manualCandidate = default;
            _holdTime = 0f;
        }

        private bool FollowSurface(Vector3 heading, ref Vector3 wish, bool hasSupport, out bool coastPastEdge)
        {
            coastPastEdge = false;
            if (FindAutomaticCorner(wish, Vector3.ProjectOnPlane(Velocity, Up), out var next, out var outer))
            {
                AutomaticCandidate = next.collider;
                coastPastEdge = !HasCornerClearance(next, outer);
                if (!coastPastEdge)
                {
                    wish = TurnCorner(next, outer, heading, wish);
                    return false;
                }
            }

            if (!hasSupport && !coastPastEdge && (!Transitioning || _lastSupport.collider != null) && _timeWithoutGround >= _detachmentGrace)
            {
                wish = ChangeGravity(Vector3.up, heading, wish, TransitionReason.EdgeRelease);
            }

            return hasSupport;
        }

        private bool ResolveMagneticHold(Vector3 heading, ref Vector3 wish, float deltaTime)
        {
            _magneticHoldTime += deltaTime;
            var tangent = Vector3.ProjectOnPlane(Velocity, Up);
            var hasCorner = FindAutomaticCorner(wish, tangent, out var next, out var outer);
            if (hasCorner)
            {
                _heldCornerTravel = tangent;
            }
            else if (FindAutomaticCorner(wish, _heldCornerTravel, out next, out outer) && !outer && _collision.Cast(_center, _orientation * Vector3.up, _heldCornerTravel.normalized, ContactProbeDistance, out var obstruction) && obstruction.collider == next.collider && Vector3.Dot(obstruction.normal, next.normal) >= _normalHysteresis)
            {
                // Preserve the approach while this face blocks the capsule and the player still presses toward it.
                hasCorner = true;
            }
            else
            {
                _heldCornerTravel = Vector3.zero;
            }

            if (hasCorner)
            {
                AutomaticCandidate = next.collider;
            }

            if (_magneticHoldTime < _magneticReleaseHoldDuration || (hasCorner && !HasCornerClearance(next, outer)))
            {
                return false;
            }

            wish = hasCorner ? TurnCorner(next, outer, heading, wish) : ChangeGravity(Vector3.up, heading, wish, TransitionReason.ManualRelease);
            return true;
        }

        private void ClearMagneticHold()
        {
            _holdingMagneticJump = false;
            _magneticHoldTime = 0f;
            _heldCornerTravel = Vector3.zero;
        }

        private bool FindAutomaticCorner(Vector3 wish, Vector3 tangent, out RaycastHit next, out bool outer)
        {
            next = default;
            outer = false;
            if (_lastSupport.collider == null || wish.sqrMagnitude <= 0.01f || tangent.magnitude < _automaticCornerMinimumSpeed)
            {
                return false;
            }

            var travel = tangent.normalized;
            var desired = wish.normalized;
            if (Vector3.Dot(travel, desired) <= 0.5f)
            {
                return false;
            }

            var hasFooting = _probe.HasFooting(_center, Up, _lastSupport.point, _normalHysteresis);
            if (!_probe.FindCorner(_center, Up, travel, desired, _lastSupport.point, _automaticCornerDetectionDistance, _normalHysteresis, hasFooting, out next, out outer))
            {
                return false;
            }

            if (_cornerReverseDelay > 0f && _previousSupport.collider != null && Vector3.Dot(next.normal, _previousSupport.normal) >= _normalHysteresis && Mathf.Abs(Vector3.Dot(next.point - _previousSupport.point, next.normal)) < 0.12f)
            {
                return false;
            }

            return true;
        }

        private bool HasCornerClearance(RaycastHit next, bool outer)
        {
            return !outer || Vector3.Dot(_center - next.point, next.normal) >= _collision.Radius + 0.04f;
        }

        private Vector3 TurnCorner(RaycastHit next, bool outer, Vector3 heading, Vector3 wish)
        {
            var transportedWish = ChangeGravity(next.normal, heading, wish, outer ? TransitionReason.OuterCorner : TransitionReason.InnerCorner);
            _outerCorner = outer ? next : default;
            return transportedWish;
        }

        private Vector3 Accelerate(Vector3 wish, bool hasSupport, bool coastPastEdge, float deltaTime)
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
            if (coastPastEdge)
            {
                vertical = Mathf.Max(0f, vertical);
            }
            else
            {
                vertical = hasSupport ? -2.4f : Mathf.Max(-35f, vertical - gravity * deltaTime);
            }

            return Vector3.ClampMagnitude(horizontal, _maximumSpeed) + Up * vertical;
        }

        private bool FindSupport(out RaycastHit hit)
        {
            var axis = _orientation * Vector3.up;
            hit = default;
            return (!JumpInFlight || Vector3.Dot(Velocity, Up) <= 0f) && _collision.Cast(_center, axis, -Up, ContactProbeDistance, out hit) && Vector3.Dot(hit.normal, Up) > 0.69f;
        }

        private void RememberSupport(RaycastHit support)
        {
            _timeWithoutGround = 0f;
            if (Vector3.Dot(support.normal, Up) >= _normalHysteresis)
            {
                _lastSupport = support;
            }
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
            RememberSupport(ground);
            JumpInFlight = false;
            if (!wasGrounded && downwardSpeed > 1.8f)
            {
                Landed?.Invoke(Mathf.Clamp01(downwardSpeed / 12f));
            }
        }

        private Vector3 ChangeGravity(Vector3 nextUp, Vector3 heading, Vector3 wish, TransitionReason reason)
        {
            var previousUp = Up;
            var transport = GravityFrame.Transport(previousUp, nextUp, heading);
            var releasing = reason is TransitionReason.EdgeRelease or TransitionReason.ManualRelease;
            Velocity = releasing ? Velocity - Vector3.up * Mathf.Max(0f, Velocity.y) : transport * Vector3.ProjectOnPlane(Velocity, previousUp);
            Up = nextUp;
            _magnetic = Vector3.Dot(Up, Vector3.up) < 0.999f;
            if (!_magnetic)
            {
                // Returning to world gravity ends the gesture that started on a magnetic face.
                _waitForNewPress = true;
                _jumpRemaining = 0f;
            }

            ClearMagneticHold();
            JumpInFlight = false;
            CurrentTransitionReason = reason;

            var previousTarget = Transitioning ? _rotationTarget : _orientation;
            _previousSupport = _lastSupport;
            _lastSupport = default;
            _outerCorner = default;
            _cornerReverseDelay = _rotationDuration + 0.08f;
            _timeWithoutGround = 0f;
            _rotationBlockedTime = 0f;
            _rotationStart = _orientation;
            _rotationTarget = transport * previousTarget;
            _rotationProgress = Quaternion.Angle(_rotationStart, _rotationTarget) < 0.01f ? 1f : 0f;
            Surface = null;
            ContactNormal = Vector3.zero;
            if (Vector3.Dot(previousUp, nextUp) < 0.99999f)
            {
                GravityChanged?.Invoke(transport);
            }

            return transport * wish;
        }

        private void UpdateCapsuleRotation(float deltaTime)
        {
            if (!Transitioning)
            {
                return;
            }

            var progress = Mathf.Min(1f, _rotationProgress + deltaTime / _rotationDuration);
            var rotation = Quaternion.Slerp(_rotationStart, _rotationTarget, Mathf.SmoothStep(0f, 1f, progress));
            var center = _center;
            if (TryRotate(ref center, rotation))
            {
                _center = center;
                _rotationProgress = progress;
                _orientation = rotation;
                _rotationBlockedTime = 0f;
                if (!Transitioning)
                {
                    _outerCorner = default;
                }

                return;
            }

            _rotationBlockedTime += deltaTime;
            if (_magnetic && !JumpInFlight && _rotationBlockedTime > _rotationDuration)
            {
                var clearUp = _orientation * Vector3.up;
                var transport = GravityFrame.Transport(Up, clearUp, _orientation * Vector3.forward);
                Velocity = transport * Velocity;
                Up = clearUp;
                _rotationProgress = 1f;
                _outerCorner = default;
                _timeWithoutGround = 0f;
                _magnetic = Vector3.Dot(Up, Vector3.up) < 0.999f;
                CurrentTransitionReason = TransitionReason.RotationBlocked;
                GravityChanged?.Invoke(transport);
            }
        }

        private bool TryRotate(ref Vector3 center, Quaternion rotation)
        {
            if (_outerCorner.collider != null)
            {
                // Keep the expanding capsule outside the lip while its axis rolls around the edge.
                var clearance = _collision.Radius + _collision.Segment * Mathf.Abs(Vector3.Dot(rotation * Vector3.up, Up));
                var correction = clearance + 0.024f - Vector3.Dot(center - _outerCorner.point, Up);
                if (correction > 0.001f)
                {
                    var offset = Up * correction;
                    _collision.Move(ref center, _orientation * Vector3.up, ref offset, 1f);
                    if (Vector3.Dot(center - _outerCorner.point, Up) < clearance)
                    {
                        return false;
                    }
                }
            }

            return _collision.Resolve(ref center, rotation, 0.16f);
        }
    }
}
