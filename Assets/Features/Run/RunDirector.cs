using System;
using System.Collections.Generic;
using Features.Enemies;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;

namespace Features.Run {
    /// <summary>
    /// Plays a stage (design doc §9.1) through the <see cref="RunStateMachine"/>:
    /// PreWave (countdown, nothing spawns) → Combat (timer + kill quota, the horde spawns) →
    /// Reward (timer over AND quota reached: the horde vanishes, the end-of-wave screen opens, game
    /// paused) → PreWave… If the timer ends before the quota: Overtime, one stage life lost at once
    /// and one more every LifeLossInterval until the quota is reached. Dying is an instant defeat
    /// (StageDefinition.PlayerDeath, or a life with LoseLife). 0 life → RunEndedEvent(lost); the last bonus wave
    /// cleared → RunEndedEvent(won). Once the quota is reached, kills give gold only (no XP):
    /// this is where kill XP is granted (XpGrantedEvent).
    /// Game scope entry point.
    /// </summary>
    public sealed class RunDirector : IStartable, ITickable, IDisposable {
        private const float StatusInterval = 0.2f;

        private readonly StageDefinition _stage;
        private readonly RunStateMachine _machine;
        private readonly HordeSpawner _spawner;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();

        private int _waveIndex;          // 0-based, bonus waves after the regular ones
        private int _kills;
        private int _lives;
        private float _timer;            // PreWave: countdown; Combat: wave time left; Overtime: next life loss
        private float _nextStatus;
        private bool _halted;
        private bool _ended;

        public RunState State => _machine.CurrentState;
        public int WaveNumber => _waveIndex + 1;
        public int Kills => _kills;
        public int Lives => _lives;
        public float TimeLeft => _timer;
        public bool IsBonusWave => _waveIndex >= _stage.Waves.Length;
        public WaveDefinition CurrentWave => WaveAt(_waveIndex);
        public bool QuotaReached => CurrentWave != null && _kills >= CurrentWave.KillQuota;

        public RunDirector(StageDefinition stage, RunStateMachine machine, HordeSpawner spawner, IEventBus eventBus) {
            _stage = stage;
            _machine = machine;
            _spawner = spawner;
            _eventBus = eventBus;
        }

        public void Start() {
            _lives = _stage.Lives;
            _machine.Changed += OnStateChanged;
            _subscriptions.Add(_eventBus.Subscribe<WaveEnemyKilledEvent>(_ => OnWaveKill()));
            _subscriptions.Add(_eventBus.Subscribe<EnemyKilledEvent>(e => {
                if (!QuotaReachedForXp) _eventBus.Publish(new XpGrantedEvent(e.Xp));
            }));
            _subscriptions.Add(_eventBus.Subscribe<PlayerDiedEvent>(_ => {
                if (_halted) return;
                if (_stage.PlayerDeath == PlayerDeathRule.GameOver) End(won: false);
                else LoseLife();
            }));
            _subscriptions.Add(_eventBus.Subscribe<RewardClosedEvent>(_ => OnRewardClosed()));
            _subscriptions.Add(_eventBus.Subscribe<SkipWaveRequestedEvent>(_ => SkipWave()));

            _spawner.SetSpawning(false);
            _timer = _stage.FirstWaveCountdown;
            _machine.TryChange(RunState.PreWave);
        }

        /// <summary>Stops the run where it is (tests, sandbox): no more waves, no more spawn.</summary>
        public void Halt() {
            _halted = true;
            _spawner.SetSpawning(false);
        }

        // Kills only stop giving XP during the combat of a wave whose quota is reached
        private bool QuotaReachedForXp => !_halted && (State == RunState.Combat || State == RunState.Overtime) && QuotaReached;

        public void Tick() {
            if (_halted || _ended || !_machine.HasState) return;
            switch (State) {
                case RunState.PreWave:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) _machine.TryChange(RunState.Combat);
                    break;
                case RunState.Combat:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) _machine.TryChange(QuotaReached ? RunState.Reward : RunState.Overtime);
                    break;
                case RunState.Overtime:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) {
                        _timer = _stage.LifeLossInterval;
                        LoseLife();
                    }
                    break;
            }
            if (Time.unscaledTime >= _nextStatus) PublishStatus();
        }

        private void OnStateChanged(RunState? from, RunState to) {
            _eventBus.Publish(new RunStateChangedEvent(from, to));
            switch (to) {
                case RunState.Combat:
                    StartWave();
                    break;
                case RunState.Overtime:
                    _timer = _stage.LifeLossInterval;
                    LoseLife();   // timer over, quota not reached
                    break;
                case RunState.Reward:
                    EndWave();
                    break;
            }
            PublishStatus();
        }

        private void StartWave() {
            WaveDefinition wave = CurrentWave;
            _kills = 0;
            _timer = wave.TimeLimit;
            _spawner.Configure(wave.SpawnTable, wave.SpawnInterval, wave.MaxAlive, wave.HealthMultiplier, wave.DamageMultiplier);
            _spawner.SetSpawning(true);
        }

        private void OnWaveKill() {
            if (State != RunState.Combat && State != RunState.Overtime) return;
            _kills++;
            if (_kills == CurrentWave.KillQuota) {
                _spawner.SetInterval(CurrentWave.PostQuotaSpawnInterval);   // gold only from now on
                if (State == RunState.Overtime) _machine.TryChange(RunState.Reward);
            }
            PublishStatus();
        }

        // Remaining enemies vanish; the last bonus wave wins, otherwise the scheduled rewards open
        private void EndWave() {
            _spawner.SetSpawning(false);
            _spawner.Clear();
            if (_waveIndex >= _stage.TotalWaves - 1) {
                End(won: true);
                return;
            }

            IReadOnlyList<RewardKind> rewards = Array.Empty<RewardKind>();
            if (_stage.RewardSchedule != null) {
                rewards = IsBonusWave
                    ? _stage.RewardSchedule.AfterBonusWave(_waveIndex - _stage.Waves.Length)
                    : _stage.RewardSchedule.AfterWave(_waveIndex);
            }
            _eventBus.Publish(new WaveRewardEvent(WaveNumber, IsBonusWave, rewards));
        }

        private void OnRewardClosed() {
            if (_ended || State != RunState.Reward) return;
            _waveIndex++;
            _timer = _stage.PreWaveCountdown;
            _machine.TryChange(RunState.PreWave);
        }

        private void LoseLife() {
            if (_ended) return;
            _lives = Mathf.Max(0, _lives - 1);
            _eventBus.Publish(new LifeLostEvent(_lives));
            if (_lives == 0) End(won: false);
            PublishStatus();
        }

        private void End(bool won) {
            if (_ended) return;
            _ended = true;
            _spawner.SetSpawning(false);
            int cleared = won ? _stage.TotalWaves : _waveIndex;
            _eventBus.Publish(new RunEndedEvent(won, cleared));
        }

        // Debug: the current wave ends as if its timer and quota were done
        private void SkipWave() {
            if (_halted || _ended) return;
            if (State == RunState.PreWave) {
                _timer = 0f;
                return;
            }
            if (State == RunState.Combat || State == RunState.Overtime) {
                _kills = Mathf.Max(_kills, CurrentWave.KillQuota);
                _machine.TryChange(RunState.Reward);
            }
        }

        private WaveDefinition WaveAt(int index) {
            if (index < 0) return null;
            if (index < _stage.Waves.Length) return _stage.Waves[index];
            int bonus = index - _stage.Waves.Length;
            return bonus < _stage.BonusWaves.Length ? _stage.BonusWaves[bonus] : null;
        }

        private void PublishStatus() {
            _nextStatus = Time.unscaledTime + StatusInterval;
            WaveDefinition wave = CurrentWave;
            _eventBus.Publish(new RunStatusEvent(State, WaveNumber, _stage.Waves.Length, _stage.TotalWaves,
                                                 _kills, wave != null ? wave.KillQuota : 0, _lives, Mathf.Max(0f, _timer)));
        }

        public void Dispose() {
            _machine.Changed -= OnStateChanged;
            _subscriptions.Dispose();
        }
    }
}
