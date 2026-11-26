using UnityEngine;

namespace Overtime
{
    internal sealed class EncounterReinforcements : TemporalActor<EncounterReinforcements.Snapshot>
    {
        private const float WarningTime = 0.65f;
        private SecurityEnemy[] _enemies;
        private GameObject[] _signals;
        private int _initial;
        private int _threshold;
        private int _batchSize;
        private Snapshot _state;
        private TemporalDirector _director;
        private SupervisorBoss _boss;
        private bool _disengaged;

        internal int Pending => _disengaged ? 0 : _enemies.Length - _state.Released;

        internal void Configure(SecurityEnemy[] enemies, GameObject[] signals, EncounterVariant variant, TemporalDirector director, SupervisorBoss boss = null)
        {
            _director = director;
            _enemies = enemies;
            _signals = signals;
            _initial = variant.InitialActive;
            _threshold = variant.ReinforcementThreshold;
            _batchSize = variant.ReinforcementBatch;
            _boss = boss;
        }

        internal void Begin()
        {
            var initial = _boss != null ? 0 : Mathf.Min(_initial, _enemies.Length);
            RestoreState(new Snapshot(true, initial, 0, _boss == null ? 0.35f : 0f));
        }

        internal override void ResetState()
        {
            RestoreState(default);
        }

        internal override void Simulate(float step)
        {
            var available = _boss == null ? Pending : Mathf.Min(Pending, _boss.ReinforcementLimit - _state.Released);
            if (!_state.Started || available <= 0 || (_boss != null && !_boss.Health.Alive))
            {
                return;
            }

            _state.Timer = Mathf.Max(0f, _state.Timer - step);
            if (_state.Timer > 0f)
            {
                return;
            }

            if (_state.Warning > 0)
            {
                var player = _director.PlayerCapsule;
                var center = player.transform.TransformPoint(player.center);
                var end = _state.Released + _state.Warning;
                while (_state.Released < end)
                {
                    var index = _state.Released;
                    var enemy = _enemies[index];
                    if (enemy.Health.Alive && !enemy.HasClearDeployment(center))
                    {
                        _state.Timer = 0.14f;
                        return;
                    }

                    enemy.SetEncounterReady(true);
                    SetSignal(index, false);
                    _state.Released++;
                    _state.Warning--;
                }

                _state.Warning = 0;
                _state.Timer = 0.24f;
                return;
            }

            var living = 0;
            for (var index = 0; index < _state.Released; index++)
            {
                living += _enemies[index].Health.Alive ? 1 : 0;
            }

            if (_boss != null || living <= _threshold)
            {
                _state.Warning = Mathf.Min(_batchSize, available);
                _state.Timer = WarningTime;
                _director.Sound(FieldCue.Reinforcement, _enemies[_state.Released].transform.position);
                for (var index = _state.Released; index < _state.Released + _state.Warning; index++)
                {
                    SetSignal(index, true);
                }
            }
        }

        internal void Disengage()
        {
            _disengaged = true;
            RestoreState(default);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugReleaseAll()
        {
            RestoreState(new Snapshot(true, _enemies.Length, 0, 0f));
        }
#endif

        private void SetSignal(int index, bool enabled)
        {
            var signal = _signals[index];
            if (signal != null && signal.activeSelf != enabled)
            {
                signal.SetActive(enabled);
            }
        }

        private protected override Snapshot CaptureState() => _state;

        private protected override void RestoreState(in Snapshot snapshot)
        {
            _state = snapshot;
            for (var index = 0; index < _enemies.Length; index++)
            {
                _enemies[index].SetEncounterReady(index < snapshot.Released);
                SetSignal(index, index >= snapshot.Released && index < snapshot.Released + snapshot.Warning);
            }
        }

        internal struct Snapshot
        {
            internal bool Started;
            internal int Released;
            internal int Warning;
            internal float Timer;

            internal Snapshot(bool started, int released, int warning, float timer)
            {
                Started = started;
                Released = released;
                Warning = warning;
                Timer = timer;
            }
        }
    }
}
