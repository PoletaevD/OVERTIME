using UnityEngine;

namespace Overtime
{
    [DefaultExecutionOrder(-100)]
    internal sealed class PlaygroundPlayer : MonoBehaviour
    {
        private const float StepDuration = (float)TemporalTimeline.TickDuration;

        [SerializeField] private PlaygroundInput _input;
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private GravityCamera _cameraRig;
        [SerializeField] private TemporalDirector _temporalDirector;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _accumulator;
        private CapsuleCollider _capsule;
        private bool _capsuleWasTrigger;

        internal Ray AimRay => _cameraRig.AimRay;
        internal bool ReducedMotion => _cameraRig.ReducedMotion;

        private void Start()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _cameraRig.ResetView(_spawnRotation);

            if (_temporalDirector != null)
            {
                _cameraRig.UseSimulationClock();
                _temporalDirector.Initialize(this);
            }
        }

        private void Update()
        {
            _input.Poll();
            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_temporalDirector != null)
            {
                var look = _input.ReadLook(deltaTime);
                var input = new TemporalInput(Vector2.ClampMagnitude(_input.Move, 1f), look, _input.JumpPressed, _input.JumpHeld, _input.FirePressed, _input.TakeTemporalSelection(), _input.ResetPressed, _input.Focused);
                if (!_temporalDirector.Advance(Time.unscaledDeltaTime, input))
                {
                    _input.SuppressFire();
                }

                return;
            }

            if (_input.ResetPressed)
            {
                ResetAtSpawn();
            }

            _cameraRig.Look(_input.ReadLook(deltaTime));
            if (_input.JumpPressed)
            {
                _motor.QueueJump();
            }

            _accumulator += deltaTime;
            var move = Vector2.ClampMagnitude(_input.Move, 1f);
            while (_accumulator >= StepDuration)
            {
                _motor.Step(move, _cameraRig.Heading, _input.JumpHeld, StepDuration);
                _accumulator -= StepDuration;
            }
        }

        internal void ResetAtSpawn()
        {
            _accumulator = 0f;
            _motor.ResetAt(_spawnPosition, _spawnRotation);
            _cameraRig.ResetView(_spawnRotation);
        }

        internal void Simulate(Vector2 move, Vector2 look, bool jumpPressed, bool jumpHeld, float step)
        {
            _cameraRig.Look(look);
            if (jumpPressed)
            {
                _motor.QueueJump();
            }

            _motor.Step(move, _cameraRig.Heading, jumpHeld, step);
            _cameraRig.AdvanceView(step);
        }

        internal Snapshot Capture()
        {
            return new Snapshot(_motor.Capture(), _cameraRig.Capture());
        }

        internal void Restore(in Snapshot snapshot)
        {
            _motor.Restore(snapshot.Motor);
            _cameraRig.Restore(snapshot.Camera);
        }

        internal void Preview(in Snapshot older, float fraction)
        {
            _cameraRig.Preview(older.Motor.Position, older.Camera, fraction);
        }

        internal void ClearPreview()
        {
            _cameraRig.ClearPreview();
        }

        internal void SetPlayback(bool active)
        {
            _cameraRig.SetPlayback(active);
            _capsule.isTrigger = active || _capsuleWasTrigger;
        }

        internal void SetTemporalCollision(TemporalCollision collision)
        {
            _capsule = GetComponent<CapsuleCollider>();
            _capsuleWasTrigger = _capsule.isTrigger;
            _motor.SetTemporalCollision(collision);
        }

        internal readonly struct Snapshot
        {
            internal readonly GravityMotor.Snapshot Motor;
            internal readonly GravityCamera.Snapshot Camera;

            internal Snapshot(GravityMotor.Snapshot motor, GravityCamera.Snapshot camera)
            {
                Motor = motor;
                Camera = camera;
            }
        }
    }
}
