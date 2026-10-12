using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Features.Run;
using Features.Stats;
using Systems.EventBusses;

namespace Features.Economy {
    /// <summary>
    /// Applies the run's bonuses to the player's stats (design doc §9.3): permanent ones (items,
    /// stat anvils) and the elixir, which lasts until the end of the wave that follows its purchase
    /// (removed at the first Reward after a Combat). Every bonus is a StatModifier whose Source is
    /// what granted it, so it can be removed cleanly. Game scope: a new run starts clean.
    /// </summary>
    public sealed class PlayerUpgrades : IDisposable {
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private readonly List<(Stat stat, StatModifier modifier)> _elixirModifiers = new();
        private StatController _stats;
        private Passives.PassiveController _passives;
        private bool _elixirFoughtAWave;

        public ElixirDefinition ActiveElixir { get; private set; }

        public PlayerUpgrades(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<PlayerSpawnedEvent>(e => {
                var player = e.Player.GetComponent<Character>();
                Bind(player.Stats, player.Passives);
            }));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => {
                if (ActiveElixir == null) return;
                if (e.To == RunState.Combat) _elixirFoughtAWave = true;
                else if (e.To == RunState.Reward && _elixirFoughtAWave) RemoveElixir();
            }));
        }

        /// <summary>Whose stats get the bonuses (the spawned player; tests bind their own).</summary>
        public void Bind(StatController stats, Passives.PassiveController passives = null) {
            _stats = stats;
            _passives = passives;
        }

        /// <summary>Adds a passive to the player (items, augments). Null when there is no player passive controller.</summary>
        public Passives.Passive AddPassive(Passives.PassiveDefinition passive, object source) =>
            passive != null ? _passives?.Add(passive, source) : null;

        public void RemovePassive(Passives.Passive passive) => _passives?.Remove(passive);

        public void RemovePermanent(object source) {
            if (_stats == null) return;
            foreach (Stat stat in _stats.Stats.Values) stat.RemoveAllFromSource(source);
        }

        public void ApplyPermanent(IReadOnlyList<StatBonus> bonuses, object source) {
            if (_stats == null) return;
            foreach (StatBonus bonus in bonuses) _stats.Get(bonus.Stat)?.AddModifier(bonus.ToModifier(source));
        }

        public void ApplyElixir(ElixirDefinition elixir) {
            if (_stats == null || elixir == null) return;
            RemoveElixir();
            foreach (StatBonus bonus in elixir.Stats) {
                Stat stat = _stats.Get(bonus.Stat);
                if (stat == null) continue;
                StatModifier modifier = bonus.ToModifier(elixir);
                stat.AddModifier(modifier);
                _elixirModifiers.Add((stat, modifier));
            }
            ActiveElixir = elixir;
            _elixirFoughtAWave = false;
            _eventBus.Publish(new ElixirChangedEvent(elixir));
        }

        private void RemoveElixir() {
            foreach ((Stat stat, StatModifier modifier) in _elixirModifiers) stat.RemoveModifier(modifier);
            _elixirModifiers.Clear();
            if (ActiveElixir == null) return;
            ActiveElixir = null;
            _eventBus.Publish(new ElixirChangedEvent(null));
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
