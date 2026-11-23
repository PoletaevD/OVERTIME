using System;
using UnityEngine;

namespace Overtime
{
    internal enum TemporalMode
    {
        Normal,
        Reset,
        Override,
        Recall,
        BulletTime
    }

    internal sealed class TemporalDirector : MonoBehaviour
    {
        private const double MaxFrameTime = 0.1d;
        private const double SimultaneousTickTolerance = 0.000001d;
        private const float Step = (float)TemporalTimeline.TickDuration;

        [SerializeField, Range(0.1f, 12f)] private float _historySeconds = 4f;
        [SerializeField, Range(0.1f, 2f)] private float _playerForwardRate = 1f;
        [SerializeField, Range(0.1f, 2f)] private float _worldForwardRate = 1f;
        [SerializeField, Range(0.1f, 8f)] private float _rewindRate = 2f;
        [SerializeField, Range(0.1f, 8f)] private float _overrideRate = 2f;
        [SerializeField, Range(0.1f, 8f)] private float _recallRate = 2f;
        [SerializeField, Range(0.05f, 1f)] private float _bulletTimeRate = 0.25f;
        [SerializeField] private TemporalActor[] _worldActors = Array.Empty<TemporalActor>();
        [SerializeField] private TemporalDestructible _testTarget;

        private PlaygroundPlayer _player;
        private PlaygroundPlayer.Snapshot[] _playerFrames;
        private TemporalCollision _collision;
        private Vector2 _pendingLook;
        private bool _pendingJump;
        private bool _pendingFire;
        private bool _worldChangedBetweenSteps;
        private bool _blockJumpHold;
        private int _exhaustedButtons;

        internal event Action<TemporalMode> ModeChanged;

        internal TemporalTimeline PlayerTimeline { get; private set; }
        internal TemporalTimeline WorldTimeline { get; private set; }
        internal TemporalMode Mode { get; private set; }
        internal bool IsRewinding => IsPlayerProtected || WorldTimeline?.Direction == TemporalDirection.Reverse;
        internal bool IsPlayerProtected => PlayerTimeline?.Direction == TemporalDirection.Reverse;
        internal bool CanAffectWorld => !IsPlayerProtected && WorldTimeline?.Direction == TemporalDirection.Forward;
        internal long SimulationTick { get; private set; }
        internal int PlayerSteps { get; private set; }
        internal int WorldSteps { get; private set; }
        internal int DynamicOverlaps => _collision?.OverlapCount ?? 0;
        internal int PhasedColliders => _collision?.PhasedCount ?? 0;

        internal void Initialize(PlaygroundPlayer player)
        {
            _player = player;
            PlayerTimeline = new TemporalTimeline(_historySeconds);
            WorldTimeline = new TemporalTimeline(_historySeconds);
            SetTimelineRates();
            _playerFrames = new PlaygroundPlayer.Snapshot[PlayerTimeline.Capacity];

            foreach (var actor in _worldActors)
            {
                actor.Initialize(WorldTimeline.Capacity);
            }

            _collision = new TemporalCollision(player.GetComponent<CapsuleCollider>(), _worldActors);
            player.SetTemporalCollision(_collision);
            Physics.SyncTransforms();
            CaptureBaseline();
        }

        internal bool Advance(double elapsed, in TemporalInput input)
        {
            PlayerSteps = 0;
            WorldSteps = 0;
            _exhaustedButtons &= input.Selection.Held & ~input.Selection.Released;

            if (input.DebugReset)
            {
                Restart();
                _exhaustedButtons = input.Selection.Held;
                return false;
            }

            if (!input.Focused)
            {
                ChangeMode(TemporalMode.Normal);
                ClearPendingInput();
                return false;
            }

            var wasReversing = IsPlayerProtected;
            ChangeMode(SelectMode(input.Selection));
            var acceptsInput = !wasReversing && !IsPlayerProtected;
            if (!input.JumpHeld)
            {
                _blockJumpHold = false;
            }
            else if (!acceptsInput && input.JumpPressed)
            {
                _blockJumpHold = true;
            }

            if (acceptsInput)
            {
                _pendingLook += input.Look;
                _pendingJump |= input.JumpPressed;
                _pendingFire |= CanAffectWorld && input.FirePressed;
            }

            elapsed = Math.Min(Math.Max(0d, elapsed), MaxFrameTime);
            if (Mode == TemporalMode.Reset)
            {
                // RESET only seeks existing frames. Its cost is independent of the distance.
                SeekReset(elapsed);
            }
            else
            {
                AdvanceIndependent(elapsed, input);
            }

            Present();
            return acceptsInput;
        }

        private TemporalMode SelectMode(TemporalSelection selection)
        {
            var excluded = _exhaustedButtons;
            while (true)
            {
                var mode = selection.Preferred(excluded);
                var unavailable = mode switch
                {
                    TemporalMode.Reset => Mode != TemporalMode.Reset && PlayerTimeline.AtStart && WorldTimeline.AtStart,
                    TemporalMode.Override => WorldTimeline.AtStart,
                    TemporalMode.Recall => PlayerTimeline.AtStart,
                    _ => false
                };

                if (!unavailable)
                {
                    return mode;
                }

                var button = 1 << (int)mode;
                excluded |= button;
                if (mode != TemporalMode.Reset)
                {
                    _exhaustedButtons |= button;
                }
            }
        }

        private void ChangeMode(TemporalMode mode)
        {
            if (mode == Mode)
            {
                return;
            }

            var playerWasReversing = IsPlayerProtected;
            var worldWasReversing = WorldTimeline.Direction == TemporalDirection.Reverse;
            var playerReverses = mode == TemporalMode.Reset || mode == TemporalMode.Recall;
            var worldReverses = mode == TemporalMode.Reset || mode == TemporalMode.Override;

            // Commit the departing mode before any new rewind can read from these groups.
            if (playerWasReversing)
            {
                PlayerTimeline.CommitBranch();
            }

            if (worldWasReversing)
            {
                WorldTimeline.CommitBranch();
            }

            Mode = mode;
            if (playerReverses)
            {
                PlayerTimeline.BeginReverse();
            }

            if (worldReverses)
            {
                WorldTimeline.BeginReverse();
                if (_worldChangedBetweenSteps)
                {
                    // A hit between world ticks is newer than the current world snapshot.
                    RestoreWorld();
                }
            }

            SetTimelineRates();

            if (playerWasReversing != playerReverses)
            {
                ClearPendingInput();
                _player.SetPlayback(playerReverses);
            }

            if (worldReverses || worldWasReversing)
            {
                _pendingFire = false;
            }

            Physics.SyncTransforms();
            _collision.Refresh(worldReverses);
            ModeChanged?.Invoke(mode);
        }

        private void SetTimelineRates()
        {
            PlayerTimeline.SetRate(Mode switch
            {
                TemporalMode.Reset => _rewindRate,
                TemporalMode.Recall => _recallRate,
                _ => _playerForwardRate
            });
            WorldTimeline.SetRate(Mode switch
            {
                TemporalMode.Reset => _rewindRate,
                TemporalMode.Override => _overrideRate,
                TemporalMode.BulletTime => _bulletTimeRate,
                _ => _worldForwardRate
            });
        }

        private void SeekReset(double elapsed)
        {
            var playerStart = PlayerTimeline.Cursor;
            var worldStart = WorldTimeline.Cursor;
            if (WorldTimeline.MoveBack(elapsed, WorldTimeline.Rate))
            {
                RestoreWorld();
            }

            if (PlayerTimeline.MoveBack(elapsed, PlayerTimeline.Rate))
            {
                RestorePlayer();
            }

            PlayerSteps += (int)(playerStart - PlayerTimeline.Cursor);
            WorldSteps += (int)(worldStart - WorldTimeline.Cursor);
            if (PlayerSteps > 0 || WorldSteps > 0)
            {
                Physics.SyncTransforms();
                _collision.Refresh(true);
            }
        }

        private void AdvanceIndependent(double elapsed, in TemporalInput input)
        {
            while (true)
            {
                if (Mode == TemporalMode.Reset)
                {
                    SeekReset(elapsed);
                    return;
                }

                var reversing = Mode == TemporalMode.Override ? WorldTimeline : Mode == TemporalMode.Recall ? PlayerTimeline : null;
                var slice = reversing == null ? elapsed : Math.Min(elapsed, reversing.TimeToStart);
                RunSchedule(slice, input);
                if (reversing == null || !reversing.AtStart)
                {
                    return;
                }

                _exhaustedButtons |= 1 << (int)Mode;
                elapsed = Math.Max(0d, elapsed - slice);
                ChangeMode(SelectMode(input.Selection));
            }
        }

        private void RunSchedule(double elapsed, in TemporalInput input)
        {
            PlayerTimeline.Accumulate(elapsed);
            WorldTimeline.Accumulate(elapsed);

            var look = Vector2.zero;
            if (!IsPlayerProtected && PlayerTimeline.StepsDue > 0)
            {
                look = _pendingLook / PlayerTimeline.StepsDue;
                _pendingLook = Vector2.zero;
            }

            while (PlayerTimeline.StepDue || WorldTimeline.StepDue)
            {
                var playerTime = PlayerTimeline.StepDue ? PlayerTimeline.NextStepOffset : double.PositiveInfinity;
                var worldTime = WorldTimeline.StepDue ? WorldTimeline.NextStepOffset : double.PositiveInfinity;
                var stepWorld = worldTime <= playerTime + SimultaneousTickTolerance;
                var stepPlayer = playerTime <= worldTime + SimultaneousTickTolerance;
                var recordWorld = stepWorld && WorldTimeline.Direction == TemporalDirection.Forward;
                SimulationTick++;

                if (stepWorld)
                {
                    WorldSteps++;
                    if (recordWorld)
                    {
                        SimulateWorld();
                    }
                    else
                    {
                        WorldTimeline.StepBack();
                        RestoreWorld();
                    }

                    Physics.SyncTransforms();
                    _collision.Refresh(WorldTimeline.Direction == TemporalDirection.Reverse);
                }

                if (stepPlayer)
                {
                    PlayerSteps++;
                    if (IsPlayerProtected)
                    {
                        PlayerTimeline.StepBack();
                        RestorePlayer();
                    }
                    else
                    {
                        _player.Simulate(input.Move, look, _pendingJump, input.JumpHeld && !_blockJumpHold, Step);
                        _pendingJump = false;
                        _playerFrames[PlayerTimeline.RecordNext()] = _player.Capture();
                        ResolveTestShot();
                    }

                    _collision.Refresh(WorldTimeline.Direction == TemporalDirection.Reverse);
                }

                if (recordWorld)
                {
                    CaptureWorld(WorldTimeline.RecordNext());
                }
            }
        }

        private void SimulateWorld()
        {
            foreach (var actor in _worldActors)
            {
                actor.Simulate(Step);
            }
        }

        private void ResolveTestShot()
        {
            if (!_pendingFire)
            {
                return;
            }

            _pendingFire = false;
            if (CanAffectWorld && _testTarget != null)
            {
                Physics.SyncTransforms();
                if (Physics.Raycast(_player.AimRay, out var hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && _testTarget.TryHit(hit.collider))
                {
                    _worldChangedBetweenSteps = true;
                }
            }
        }

        private void RestorePlayer()
        {
            _player.Restore(_playerFrames[PlayerTimeline.Slot(PlayerTimeline.Cursor)]);
        }

        private void RestoreWorld()
        {
            var slot = WorldTimeline.Slot(WorldTimeline.Cursor);
            foreach (var actor in _worldActors)
            {
                actor.Restore(slot);
            }

            _worldChangedBetweenSteps = false;
        }

        private void CaptureWorld(int slot)
        {
            foreach (var actor in _worldActors)
            {
                actor.Capture(slot);
            }

            _worldChangedBetweenSteps = false;
        }

        private void Present()
        {
            if (PlayerTimeline.PreviewFraction > 0f)
            {
                _player.Preview(_playerFrames[PlayerTimeline.Slot(PlayerTimeline.Cursor - 1)], PlayerTimeline.PreviewFraction);
            }
            else
            {
                _player.ClearPreview();
            }

            foreach (var actor in _worldActors)
            {
                actor.Present(WorldTimeline, Mode, !_player.ReducedMotion);
            }
        }

        internal void PresentPlayerTrace(LineRenderer trace)
        {
            const int spacing = 5;
            var samples = (int)Math.Min(12, 1 + (PlayerTimeline.Latest - PlayerTimeline.Cursor) / spacing);
            trace.enabled = Mode == TemporalMode.Recall && !_player.ReducedMotion && samples > 1;
            if (!trace.enabled)
            {
                return;
            }

            trace.positionCount = samples;
            for (var sample = 0; sample < samples; sample++)
            {
                var tick = PlayerTimeline.Cursor + sample * spacing;
                trace.SetPosition(sample, _playerFrames[PlayerTimeline.Slot(tick)].Motor.Position);
            }
        }

        private void Restart()
        {
            ChangeMode(TemporalMode.Normal);
            ClearPendingInput();
            _blockJumpHold = false;
            PlayerTimeline.Clear();
            WorldTimeline.Clear();
            SetTimelineRates();
            SimulationTick = 0;

            foreach (var actor in _worldActors)
            {
                actor.ResetState();
            }

            _player.ResetAtSpawn();
            Physics.SyncTransforms();
            _collision.Refresh(false);
            CaptureBaseline();
            Present();
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

    internal struct TemporalSelection
    {
        // Four mode IDs fit in four nibbles, with the most recent press in the low nibble.
        private int _order;

        internal int Held { get; private set; }
        internal int Released { get; private set; }

        internal void Press(TemporalMode mode)
        {
            if (mode == TemporalMode.Normal || (Held & (1 << (int)mode)) != 0)
            {
                return;
            }

            _order = (_order << 4) | (int)mode;
            Held |= 1 << (int)mode;
        }

        internal void Release(TemporalMode mode)
        {
            if (mode == TemporalMode.Normal)
            {
                return;
            }

            Released |= 1 << (int)mode;
            for (var shift = 0; shift < 16; shift += 4)
            {
                if (((_order >> shift) & 15) == (int)mode)
                {
                    var before = (1 << shift) - 1;
                    _order = (_order & before) | ((_order >> (shift + 4)) << shift);
                    Held &= ~(1 << (int)mode);
                    return;
                }
            }
        }

        internal void ClearReleases()
        {
            Released = 0;
        }

        internal TemporalMode Preferred(int excluded)
        {
            for (var order = _order; order != 0; order >>= 4)
            {
                var mode = order & 15;
                if ((excluded & (1 << mode)) == 0)
                {
                    return (TemporalMode)mode;
                }
            }

            return TemporalMode.Normal;
        }
    }

    internal readonly struct TemporalInput
    {
        internal Vector2 Move { get; }
        internal Vector2 Look { get; }
        internal bool JumpPressed { get; }
        internal bool JumpHeld { get; }
        internal bool FirePressed { get; }
        internal TemporalSelection Selection { get; }
        internal bool DebugReset { get; }
        internal bool Focused => !_unfocused;

        private readonly bool _unfocused;

        internal TemporalInput(Vector2 move, Vector2 look, bool jumpPressed, bool jumpHeld, bool firePressed, TemporalSelection selection, bool debugReset = false, bool focused = true)
        {
            Move = move;
            Look = look;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            FirePressed = firePressed;
            Selection = selection;
            DebugReset = debugReset;
            _unfocused = !focused;
        }

        internal TemporalInput(Vector2 move, Vector2 look, bool jumpPressed, bool jumpHeld, bool firePressed, bool rewindHeld, bool debugReset)
            : this(move, look, jumpPressed, jumpHeld, firePressed, ResetSelection(rewindHeld), debugReset)
        {
        }

        private static TemporalSelection ResetSelection(bool held)
        {
            var selection = new TemporalSelection();
            if (held)
            {
                selection.Press(TemporalMode.Reset);
            }

            return selection;
        }
    }
}
