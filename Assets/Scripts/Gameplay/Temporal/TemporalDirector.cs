using System;
using UnityEngine;

namespace Overtime
{
    internal sealed class TemporalDirector : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 12f)] private float _historySeconds = 4f;
        [SerializeField, Range(0.1f, 8f)] private float _rewindRate = 2f;
        [SerializeField] private TemporalActor[] _worldActors = Array.Empty<TemporalActor>();
        [SerializeField] private TemporalDestructible _testTarget;

        private PlaygroundPlayer _player;
        private PlaygroundPlayer.Snapshot[] _playerFrames;
        private Vector2 _pendingLook;
        private bool _pendingJump;
        private bool _pendingFire;

        internal event Action<bool> ResetChanged;

        internal TemporalTimeline PlayerTimeline { get; private set; }
        internal TemporalTimeline WorldTimeline { get; private set; }
        internal bool IsRewinding { get; private set; }
        internal long SimulationTick { get; private set; }

        internal void Initialize(PlaygroundPlayer player)
        {
            _player = player;
            PlayerTimeline = new TemporalTimeline(_historySeconds);
            WorldTimeline = new TemporalTimeline(_historySeconds);
            _playerFrames = new PlaygroundPlayer.Snapshot[PlayerTimeline.Capacity];

            foreach (var actor in _worldActors)
            {
                actor.Initialize(WorldTimeline.Capacity);
            }

            Physics.SyncTransforms();
            CaptureBaseline();
        }

        internal bool Advance(double elapsed, in TemporalInput input)
        {
            if (input.DebugReset)
            {
                Restart();
                return false;
            }

            if (IsRewinding && !input.RewindHeld)
            {
                EndReset();
                var resumed = new TemporalInput(input.Move, Vector2.zero, false, input.JumpHeld, false, false, false);
                SimulateForward(elapsed, resumed);
                return false;
            }

            if (input.RewindHeld && (IsRewinding || (PlayerTimeline.Count > 1 && WorldTimeline.Count > 1)))
            {
                if (!IsRewinding)
                {
                    BeginReset();
                }

                PlayBackward(elapsed);
                return false;
            }

            SimulateForward(elapsed, input);
            return true;
        }

        private void BeginReset()
        {
            IsRewinding = true;
            PlayerTimeline.BeginReverse();
            WorldTimeline.BeginReverse();
            ClearPendingInput();
            _player.SetPlayback(true);
            ResetChanged?.Invoke(true);
        }

        private void PlayBackward(double elapsed)
        {
            var worldMoved = WorldTimeline.MoveBack(elapsed, _rewindRate);
            var playerMoved = PlayerTimeline.MoveBack(elapsed, _rewindRate);
            if (worldMoved)
            {
                RestoreWorld();
            }

            if (playerMoved)
            {
                RestorePlayer();
            }

            if (worldMoved || playerMoved)
            {
                Physics.SyncTransforms();
            }

            if (PlayerTimeline.PreviewFraction > 0f)
            {
                var older = PlayerTimeline.Slot(PlayerTimeline.Cursor - 1);
                _player.Preview(_playerFrames[older], PlayerTimeline.PreviewFraction);
            }
            else
            {
                _player.ClearPreview();
            }

            PresentWorld();
        }

        private void EndReset()
        {
            IsRewinding = false;
            PlayerTimeline.CommitBranch();
            WorldTimeline.CommitBranch();
            _player.SetPlayback(false);
            PresentWorld();
            ResetChanged?.Invoke(false);
        }

        private void SimulateForward(double elapsed, in TemporalInput input)
        {
            const float step = (float)TemporalTimeline.TickDuration;
            PlayerTimeline.AccumulateForward(elapsed);
            WorldTimeline.AccumulateForward(elapsed);
            _pendingLook += input.Look;
            _pendingJump |= input.JumpPressed;
            _pendingFire |= input.FirePressed;

            var playerSteps = PlayerTimeline.StepsDue;
            var worldSteps = WorldTimeline.StepsDue;
            var look = Vector2.zero;
            if (playerSteps > 0)
            {
                look = _pendingLook / playerSteps;
                _pendingLook = Vector2.zero;
            }

            while (playerSteps > 0 || worldSteps > 0)
            {
                SimulationTick++;
                var stepWorld = worldSteps > 0;
                if (stepWorld)
                {
                    SimulateWorld(step);
                    worldSteps--;
                }

                if (playerSteps > 0)
                {
                    _player.Simulate(input.Move, look, _pendingJump, input.JumpHeld, step);
                    _pendingJump = false;
                    _playerFrames[PlayerTimeline.RecordNext()] = _player.Capture();
                    playerSteps--;
                }

                if (stepWorld)
                {
                    ResolveTestShot();
                    CaptureWorld(WorldTimeline.RecordNext());
                }
            }
        }

        private void SimulateWorld(float step)
        {
            foreach (var actor in _worldActors)
            {
                actor.Simulate(step);
            }

            // The capsule solver casts against the world pose from this same tick.
            Physics.SyncTransforms();
        }

        private void ResolveTestShot()
        {
            if (_pendingFire && _testTarget != null)
            {
                var ray = _player.AimRay;
                if (Physics.Raycast(ray, out var hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    _testTarget.TryHit(hit.collider);
                }
            }

            _pendingFire = false;
        }

        private void RestorePlayer()
        {
            var slot = PlayerTimeline.Slot(PlayerTimeline.Cursor);
            _player.Restore(_playerFrames[slot]);
        }

        private void RestoreWorld()
        {
            var slot = WorldTimeline.Slot(WorldTimeline.Cursor);
            foreach (var actor in _worldActors)
            {
                actor.Restore(slot);
            }
        }

        private void CaptureWorld(int slot)
        {
            foreach (var actor in _worldActors)
            {
                actor.Capture(slot);
            }
        }

        private void PresentWorld()
        {
            foreach (var actor in _worldActors)
            {
                actor.Present(WorldTimeline, !_player.ReducedMotion);
            }
        }

        private void Restart()
        {
            if (IsRewinding)
            {
                EndReset();
            }

            ClearPendingInput();
            PlayerTimeline.Clear();
            WorldTimeline.Clear();
            SimulationTick = 0;

            foreach (var actor in _worldActors)
            {
                actor.ResetState();
            }

            _player.ResetAtSpawn();
            Physics.SyncTransforms();
            CaptureBaseline();
        }

        private void CaptureBaseline()
        {
            _playerFrames[0] = _player.Capture();
            CaptureWorld(0);
        }

        private void ClearPendingInput()
        {
            _pendingLook = Vector2.zero;
            _pendingJump = false;
            _pendingFire = false;
        }
    }

    internal readonly struct TemporalInput
    {
        internal readonly Vector2 Move;
        internal readonly Vector2 Look;
        internal readonly bool JumpPressed;
        internal readonly bool JumpHeld;
        internal readonly bool FirePressed;
        internal readonly bool RewindHeld;
        internal readonly bool DebugReset;

        internal TemporalInput(Vector2 move, Vector2 look, bool jumpPressed, bool jumpHeld, bool firePressed, bool rewindHeld, bool debugReset)
        {
            Move = move;
            Look = look;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            FirePressed = firePressed;
            RewindHeld = rewindHeld;
            DebugReset = debugReset;
        }
    }
}
