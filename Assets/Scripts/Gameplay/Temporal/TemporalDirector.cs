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
        [SerializeField] private SectorRoute _sector;
        [SerializeField] private TemporalDestructible _testTarget;
        [SerializeField] private ChronoEnergy _energy;
        [SerializeField] private Transform _rechargeStation;
        [SerializeField, Min(0.1f)] private float _rechargeRadius = 1.8f;

        private PlaygroundPlayer _player;
        private PlaygroundPlayer.Snapshot[] _playerFrames;
        private TemporalCollision _collision;
        private Vector2 _pendingLook;
        private bool _pendingJump;
        private bool _pendingFire;
        private bool _pendingReload;
        private bool _pendingRecharge;
        private bool _playerChangedBetweenSteps;
        private bool _restartRequested;
        // A forward tick captures pending mutations. Entering reverse playback discards them.
        private bool _worldChangedBetweenSteps;
        private bool _blockJumpHold;
        private int _exhaustedButtons;
        private double _projectileCredit;
        private TemporalProjectileEmitter _playerProjectiles;

        internal event Action<TemporalMode> ModeChanged;
        internal event Action<Vector3> DirectionalDamage;
        internal event Action<ProjectileImpact> ProjectileHit;
        internal event Action<Vector3> AuditorCharging;
        internal TemporalProjectileEmitter PlayerProjectiles => _playerProjectiles;
        internal bool ReducedMotion => _player.ReducedMotion;

        internal ChronoEnergy Energy => _energy;
        internal float EnergyCost => _energy.Cost(Mode);
        internal CapsuleCollider PlayerCapsule => _player.Capsule;
        internal bool HasLivingPlayer => _player.Combat is { Alive: true, GameOver: false };
        internal bool CanRecharge => _rechargeStation != null && (_rechargeStation.position - _player.transform.position).sqrMagnitude <= _rechargeRadius * _rechargeRadius;
        internal bool RecoveryAvailable => _player.Combat is { Alive: false, GameOver: false } && (CanRecover(TemporalMode.Reset) || CanRecover(TemporalMode.Recall));

        internal TemporalTimeline PlayerTimeline { get; private set; }
        internal TemporalTimeline WorldTimeline { get; private set; }
        internal TemporalMode Mode { get; private set; }
        internal bool IsRewinding => IsPlayerProtected || WorldTimeline?.Direction == TemporalDirection.Reverse;
        internal bool IsPlayerProtected => PlayerTimeline?.Direction == TemporalDirection.Reverse;
        internal bool CanDamageWorld => WorldTimeline?.Direction == TemporalDirection.Forward && _player.Combat?.GameOver != true;
        internal bool CanAffectWorld => !IsPlayerProtected && CanDamageWorld;
        internal long SimulationTick { get; private set; }
        internal int PlayerSteps { get; private set; }
        internal int WorldSteps { get; private set; }
        internal int WorldActorCount => _worldActors.Length + (_playerProjectiles != null ? 1 : 0);
        internal int DynamicOverlaps => _collision?.OverlapCount ?? 0;
        internal int PhasedColliders => _collision?.PhasedCount ?? 0;

        internal void Initialize(PlaygroundPlayer player)
        {
            _player = player;
            _energy?.ResetRun();
            PlayerTimeline = new TemporalTimeline(_historySeconds);
            WorldTimeline = new TemporalTimeline(_historySeconds);
            SetTimelineRates();
            _playerFrames = new PlaygroundPlayer.Snapshot[PlayerTimeline.Capacity];
            if (_sector != null)
            {
                _worldActors = _sector.Prepare();
            }

            foreach (var actor in _worldActors)
            {
                actor.Initialize(WorldTimeline.Capacity);
            }

            if (player.Combat != null)
            {
                var pool = new GameObject("ARC-01 Projectile Pool");
                pool.transform.SetParent(transform, false);
                _playerProjectiles = pool.AddComponent<TemporalProjectileEmitter>();
                _playerProjectiles.ConfigurePlayerPool(this, player.Combat.Weapon.ProjectileCapacity, Resources.Load<Material>("ARC-01 Bolt"));
                _playerProjectiles.Initialize(WorldTimeline.Capacity);
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

            if (input.DebugReset || _restartRequested)
            {
                _restartRequested = false;
                if (_sector != null)
                {
                    _sector.Restart();
                }
                else
                {
                    Restart();
                }

                _exhaustedButtons = input.Selection.Held;
                return false;
            }

            if (_player.Combat?.GameOver == true || _sector?.Finished == true)
            {
                return false;
            }

            var realElapsed = Math.Max(0d, elapsed);
            var wasDead = _player.Combat is { Alive: false };
            if (!input.Focused)
            {
                ChangeMode(TemporalMode.Normal);
                ClearPendingInput();
                ResolveRecovery(realElapsed);
                return false;
            }

            var wasReversing = IsPlayerProtected;
            ChangeMode(SelectMode(input.Selection));
            var acceptsInput = !wasDead && !wasReversing && !IsPlayerProtected;
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
                _pendingReload |= input.ReloadPressed;
                _pendingRecharge |= CanAffectWorld && input.RechargePressed;
            }

            elapsed = Math.Min(realElapsed, MaxFrameTime);
            var realPerSimulatedSecond = elapsed > 0d ? realElapsed / elapsed : 1d;
            if (wasDead && IsPlayerProtected)
            {
                elapsed = Math.Min(elapsed, _player.Combat.RecoveryRemaining / realPerSimulatedSecond);
            }

            AdvanceModes(elapsed, realPerSimulatedSecond, input);
            if (_sector != null)
            {
                var room = _sector.Current;
                var state = room.State;
                _sector.Advance(realElapsed, this, _player);
                if (room.State != state)
                {
                    _exhaustedButtons |= input.Selection.Held;
                }
            }

            ResolveRecovery(wasDead ? realElapsed : 0d);
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
                    TemporalMode.Reset => Mode != TemporalMode.Reset && PlayerTimeline.AtStart && WorldTimeline.AtStart && !_worldChangedBetweenSteps && !_playerChangedBetweenSteps,
                    TemporalMode.Override => WorldTimeline.AtStart && !_worldChangedBetweenSteps,
                    TemporalMode.Recall => PlayerTimeline.AtStart && !_playerChangedBetweenSteps,
                    _ => false
                };

                if (mode != TemporalMode.Normal)
                {
                    unavailable |= _energy?.Empty == true || (_player.Combat is { Alive: false } && !CanRecover(mode));
                }

                if (!unavailable)
                {
                    return mode;
                }

                var button = 1 << (int)mode;
                excluded |= button;
                if (mode != TemporalMode.Reset || _energy?.Empty == true)
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
                if (_playerChangedBetweenSteps)
                {
                    RestorePlayer();
                }
            }

            if (worldReverses)
            {
                WorldTimeline.BeginReverse();
                _projectileCredit = 0d;
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
                _pendingRecharge = false;
            }

            Physics.SyncTransforms();
            _collision.Refresh(worldReverses);
            _sector?.CountMode(mode);
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

        private void AdvanceModes(double elapsed, double realPerSimulatedSecond, in TemporalInput input)
        {
            while (Mode != TemporalMode.Normal)
            {
                var mode = Mode;
                var availableHistory = mode switch
                {
                    TemporalMode.Reset => Math.Max(PlayerTimeline.TimeToStart, WorldTimeline.TimeToStart),
                    TemporalMode.Override => WorldTimeline.TimeToStart,
                    TemporalMode.Recall => PlayerTimeline.TimeToStart,
                    _ => double.PositiveInfinity
                };
                var unitsPerSecond = mode == TemporalMode.BulletTime ? realPerSimulatedSecond : mode == TemporalMode.Override ? WorldTimeline.Rate : PlayerTimeline.Rate;
                var costPerSecond = (_energy?.Cost(mode) ?? 0f) * unitsPerSecond;
                var affordable = _energy?.Affordable(costPerSecond) ?? double.PositiveInfinity;
                var slice = Math.Max(0d, Math.Min(elapsed, Math.Min(availableHistory, affordable)));

                _energy?.Spend(slice * costPerSecond);
                if (mode == TemporalMode.Reset)
                {
                    SeekReset(slice);
                }
                else
                {
                    RunSchedule(slice, input);
                }

                var atStart = mode == TemporalMode.Override && WorldTimeline.AtStart || mode == TemporalMode.Recall && PlayerTimeline.AtStart;
                if (_energy?.Empty != true && !atStart)
                {
                    if (slice >= elapsed || mode == TemporalMode.Reset)
                    {
                        return;
                    }

                    elapsed -= slice;
                    continue;
                }

                _exhaustedButtons |= 1 << (int)mode;
                elapsed = Math.Max(0d, elapsed - slice);
                ChangeMode(SelectMode(input.Selection));
            }

            RunSchedule(elapsed, input);
        }

        private bool CanRecover(TemporalMode mode)
        {
            if ((mode != TemporalMode.Reset && mode != TemporalMode.Recall) || _energy == null || _energy.Empty || _player.Combat.RecoveryRemaining <= 0d)
            {
                return false;
            }

            var rate = mode == TemporalMode.Reset ? _rewindRate : _recallRate;
            var distance = Math.Min(_energy.Affordable(_energy.Cost(mode)), _player.Combat.RecoveryRemaining * rate);
            var preview = mode == Mode ? PlayerTimeline.PreviewFraction : 0f;
            var steps = (long)Math.Floor(distance / TemporalTimeline.TickDuration + preview + 0.00001d);
            var oldest = Math.Max(PlayerTimeline.Oldest, PlayerTimeline.Cursor - steps);
            for (var tick = PlayerTimeline.Cursor; tick >= oldest; tick--)
            {
                if (_playerFrames[PlayerTimeline.Slot(tick)].Combat.HitPoints > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveRecovery(double elapsed)
        {
            if (_player.Combat is not { Alive: false, GameOver: false } combat)
            {
                return;
            }

            combat.AdvanceRecoveryClock(elapsed);
            if (!RecoveryAvailable)
            {
                combat.EndRecovery();
            }

            if (combat.GameOver)
            {
                ChangeMode(TemporalMode.Normal);
                ClearPendingInput();
                _player.SetGameOver(true);
            }
        }

        private void RunSchedule(double elapsed, in TemporalInput input)
        {
            PlayerTimeline.Accumulate(elapsed);
            WorldTimeline.Accumulate(elapsed);
            var projectilesAdvance = _playerProjectiles != null && CanDamageWorld;
            if (projectilesAdvance)
            {
                _projectileCredit += elapsed * TemporalTimeline.TicksPerSecond;
            }

            var look = Vector2.zero;
            if (!IsPlayerProtected && PlayerTimeline.StepsDue > 0)
            {
                look = _pendingLook / PlayerTimeline.StepsDue;
                _pendingLook = Vector2.zero;
            }

            while (PlayerTimeline.StepDue || WorldTimeline.StepDue || (projectilesAdvance && _projectileCredit + 0.00001d >= 1d))
            {
                var playerTime = PlayerTimeline.StepDue ? PlayerTimeline.NextStepOffset : double.PositiveInfinity;
                var worldTime = WorldTimeline.StepDue ? WorldTimeline.NextStepOffset : double.PositiveInfinity;
                var projectileTime = projectilesAdvance && _projectileCredit + 0.00001d >= 1d ? (1d - _projectileCredit) * TemporalTimeline.TickDuration : double.PositiveInfinity;
                var nextTime = Math.Min(projectileTime, Math.Min(playerTime, worldTime)) + SimultaneousTickTolerance;
                var stepWorld = worldTime <= nextTime;
                var stepPlayer = playerTime <= nextTime;
                var recordWorld = stepWorld && WorldTimeline.Direction == TemporalDirection.Forward;
                SimulationTick++;

                // Advance existing bolts before the player can launch a new one at this instant.
                if (projectileTime <= nextTime)
                {
                    _projectileCredit = Math.Max(0d, _projectileCredit - 1d);
                    _worldChangedBetweenSteps |= _playerProjectiles.ActiveCount > 0;
                    _playerProjectiles.Simulate(Step);
                }

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
                        if (_player.Combat?.Alive != false)
                        {
                            _player.Simulate(input.Move, look, _pendingJump, input.JumpHeld && !_blockJumpHold, Step);
                            SimulatePlayerCombat(input.FireHeld);
                        }

                        _pendingJump = false;
                        _pendingFire = false;
                        _pendingReload = false;
                        _pendingRecharge = false;
                        _playerFrames[PlayerTimeline.RecordNext()] = _player.Capture();
                        _playerChangedBetweenSteps = false;
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
            Physics.SyncTransforms();
            foreach (var actor in _worldActors)
            {
                actor.Simulate(Step);
            }
        }

        private void SimulatePlayerCombat(bool fireHeld)
        {
            if (_player.Combat != null)
            {
                if (_pendingRecharge && CanAffectWorld && CanRecharge)
                {
                    _energy.Recharge();
                }

                var generation = _playerProjectiles.Generation;
                _player.Combat.Weapon.Simulate(TemporalTimeline.TickDuration, _pendingFire || fireHeld, _pendingReload, CanAffectWorld, _player.AimRay, _player.AimUp, _playerProjectiles);
                _worldChangedBetweenSteps |= generation != _playerProjectiles.Generation;
                return;
            }

            if (_pendingFire && CanAffectWorld)
            {
                Physics.SyncTransforms();
                _worldChangedBetweenSteps |= _testTarget != null && _testTarget.TryHit(_player.AimRay);
                _player.ConfirmLegacyShot();
            }
        }

        internal DamageResult DamageTarget(ICombatDamageable target, Collider hit, int amount, out bool weakPoint)
        {
            weakPoint = false;
            if (!CanDamageWorld)
            {
                return default;
            }

            var damage = target.Hit(hit, amount, _player.ReducedMotion, out weakPoint);
            _worldChangedBetweenSteps |= damage.Amount > 0;
            if (damage.Killed)
            {
                _energy?.Reward(target.RewardId);
            }

            return damage;
        }

        internal void StartAuditorCharge(Vector3 origin)
        {
            AuditorCharging?.Invoke(origin);
        }

        internal void PublishImpact(in ProjectileImpact impact)
        {
            ProjectileHit?.Invoke(impact);
        }

        internal bool CanDamagePlayer => CanAffectWorld && HasLivingPlayer && (_sector == null || _sector.Current.State != EncounterState.Active || _sector.Current.LivingEnemies > 0);

        internal bool DamagePlayer(int amount, Vector3? source = null)
        {
            if (!CanDamagePlayer || !_player.Combat.TakeDamage(amount))
            {
                return false;
            }

            _playerChangedBetweenSteps = true;
            if (source.HasValue)
            {
                DirectionalDamage?.Invoke(source.Value);
            }

            return true;
        }

        private void RestorePlayer()
        {
            _player.Restore(_playerFrames[PlayerTimeline.Slot(PlayerTimeline.Cursor)]);
            _playerChangedBetweenSteps = false;
        }

        private void RestoreWorld()
        {
            var slot = WorldTimeline.Slot(WorldTimeline.Cursor);
            foreach (var actor in _worldActors)
            {
                actor.Restore(slot);
            }

            _playerProjectiles?.Restore(slot);
            _worldChangedBetweenSteps = false;
        }

        private void CaptureWorld(int slot)
        {
            foreach (var actor in _worldActors)
            {
                actor.Capture(slot);
            }

            _playerProjectiles?.Capture(slot);
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

            _playerProjectiles?.Present(WorldTimeline, Mode, !_player.ReducedMotion);
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

        internal void ReplaceEncounter(TemporalActor[] actors)
        {
            ChangeMode(TemporalMode.Normal);
            foreach (var actor in _worldActors)
            {
                if (actor != actors[0])
                {
                    actor.ReleaseHistory();
                }
            }

            _worldActors = actors;
            for (var index = 1; index < actors.Length; index++)
            {
                actors[index].Initialize(WorldTimeline.Capacity);
            }

            _collision = new TemporalCollision(PlayerCapsule, _worldActors);
            _player.SetTemporalCollision(_collision);
            SealHistory();
        }

        internal void SealHistory()
        {
            ChangeMode(TemporalMode.Normal);
            ClearPendingInput();
            _projectileCredit = 0d;
            _playerProjectiles?.ClearProjectiles();
            _sector.EnemyProjectiles.ClearProjectiles();

            PlayerTimeline.Clear();
            WorldTimeline.Clear();
            SetTimelineRates();
            _playerChangedBetweenSteps = false;
            Physics.SyncTransforms();
            _collision.Refresh(false);
            CaptureBaseline();
        }

        internal void RequestRestart()
        {
            _restartRequested = true;
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
            _projectileCredit = 0d;
            _playerProjectiles?.ResetState();

            foreach (var actor in _worldActors)
            {
                actor.ResetState();
            }

            _energy?.ResetRun();
            _player.Combat?.ResetState();
            _player.SetGameOver(false);
            _player.ResetAtSpawn();
            _playerChangedBetweenSteps = false;
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
            _pendingReload = false;
            _pendingRecharge = false;
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
        internal bool FireHeld { get; }
        internal bool ReloadPressed { get; }
        internal bool RechargePressed { get; }
        internal TemporalSelection Selection { get; }
        internal bool DebugReset { get; }
        internal bool Focused => !_unfocused;

        private readonly bool _unfocused;

        internal TemporalInput(Vector2 move, Vector2 look, bool jumpPressed, bool jumpHeld, bool firePressed, TemporalSelection selection, bool debugReset = false, bool focused = true, bool fireHeld = false, bool reloadPressed = false, bool rechargePressed = false)
        {
            Move = move;
            Look = look;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            FirePressed = firePressed;
            FireHeld = fireHeld;
            ReloadPressed = reloadPressed;
            RechargePressed = rechargePressed;
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
