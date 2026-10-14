using System;
using Features.Augments;
using Features.Characters;
using Features.Economy;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Meta {
    /// <summary>
    /// Applies the run's meta bonuses (RunModifiers, design doc §9.7) when the player spawns: stat
    /// bonuses, starting gold, extra augment rerolls and the free starting augment. Game scope.
    /// </summary>
    public sealed class MetaApplier : IStartable, IDisposable {
        private readonly RunModifiers _modifiers;
        private readonly Wallet _wallet;
        private readonly AugmentController _augments;
        private readonly IDisposable _subscription;
        private bool _applied;

        public MetaApplier(RunModifiers modifiers, Wallet wallet, AugmentController augments, IEventBus eventBus) {
            _modifiers = modifiers;
            _wallet = wallet;
            _augments = augments;
            // Subscribed at construction: the player can spawn before Start
            _subscription = eventBus.Subscribe<PlayerSpawnedEvent>(e => Apply(e.Player.GetComponent<Character>()));
        }

        public void Start() { }

        private void Apply(Character player) {
            if (_applied || player == null) return;
            _applied = true;
            _modifiers.ApplyStats(player.Stats);
            if (_modifiers.StartingGold > 0) _wallet.Add(_modifiers.StartingGold);
            if (_modifiers.AugmentRerolls > 0) _augments.AddRerollsPerCard(_modifiers.AugmentRerolls);
            if (_modifiers.StartingAugment is { } rarity) _augments.GrantRandom(rarity);
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
