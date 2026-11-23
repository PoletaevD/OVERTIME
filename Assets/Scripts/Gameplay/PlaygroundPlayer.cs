using UnityEngine;

namespace Overtime
{
    [DefaultExecutionOrder(-100)]
    internal sealed class PlaygroundPlayer : MonoBehaviour
    {
        private const float StepDuration = 1f / 120f;

        [SerializeField] private PlaygroundInput _input;
        [SerializeField] private GravityMotor _motor;
        [SerializeField] private GravityCamera _cameraRig;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _accumulator;

        private void Start()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _cameraRig.ResetView(_spawnRotation);
        }

        private void Update()
        {
            _input.Poll();
            if (_input.ResetPressed)
            {
                _accumulator = 0f;
                _motor.ResetAt(_spawnPosition, _spawnRotation);
                _cameraRig.ResetView(_spawnRotation);
            }

            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
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
    }
}
