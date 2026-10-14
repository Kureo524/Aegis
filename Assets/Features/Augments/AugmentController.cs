using System;
using System.Collections.Generic;
using Features.Economy;
using Features.Events;
using Features.Passives;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Features.Augments {
    /// <summary>
    /// Augments of the run (design doc §9.4), arena-draft style:
    /// - Draft (RewardKind.Augment): a rarity is drawn, 3 augments of that rarity not owned yet.
    /// - Rerolls: each card of a draft has its own reroll (RerollsPerCard, + meta bonus), reset on
    ///   every draft; a reroll keeps the card's rarity. A draft may also offer a golden reroll
    ///   (~5 %): a separate, single reroll that gives one card an augment of rarity +1. Only the
    ///   golden reroll changes a rarity.
    /// - The chosen augment's passive is added to the player (Source = the augment).
    /// - All slots full: the player takes one benefit instead: a stat anvil of the drafted rarity,
    ///   a replacement (Silver → an owned higher one, Gold → same, Prismatic → lower), or +1 slot
    ///   (the augment goes in it) / gold once at MaxSlots.
    /// Requests and snapshots go through the bus. Game scope service.
    /// </summary>
    public sealed class AugmentController : IStartable, IDisposable {
        private readonly AugmentCatalog _catalog;
        private readonly PlayerUpgrades _upgrades;
        private readonly ShopService _shop;
        private readonly Wallet _wallet;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private readonly List<(AugmentDefinition augment, Passive passive)> _owned = new();
        private readonly List<AugmentDefinition> _choices = new();

        private AugmentDraftPhase _phase;
        private RarityTier _rarity;
        private bool _golden;
        private AugmentDefinition _pending;
        private readonly List<int> _cardRerolls = new();
        private int _bonusRerolls;

        public int Slots { get; private set; }
        /// <summary>Rerolls each card gets in a draft.</summary>
        public int RerollsPerCard => _catalog.RerollsPerCard + _bonusRerolls;
        /// <summary>Rerolls left on card <paramref name="index"/> of the current draft.</summary>
        public int RerollsLeft(int index) => index >= 0 && index < _cardRerolls.Count ? _cardRerolls[index] : 0;
        public AugmentDraftPhase Phase => _phase;
        public RarityTier Rarity => _rarity;
        public bool GoldenReroll => _golden;
        public IReadOnlyList<AugmentDefinition> Choices => _choices;
        public IReadOnlyList<AugmentDefinition> Owned {
            get {
                var list = new List<AugmentDefinition>(_owned.Count);
                foreach ((AugmentDefinition a, Passive _) in _owned) list.Add(a);
                return list;
            }
        }

        public AugmentController(AugmentCatalog catalog, PlayerUpgrades upgrades, ShopService shop, Wallet wallet, IEventBus eventBus) {
            _catalog = catalog;
            _upgrades = upgrades;
            _shop = shop;
            _wallet = wallet;
            _eventBus = eventBus;
            Slots = catalog.BaseSlots;
        }

        public void Start() {
            _subscriptions.Add(_eventBus.Subscribe<RewardStepEvent>(e => { if (e.Kind == RewardKind.Augment) OpenDraft(); }));
            _subscriptions.Add(_eventBus.Subscribe<AugmentPickRequestedEvent>(e => Pick(e.Index)));
            _subscriptions.Add(_eventBus.Subscribe<AugmentRerollRequestedEvent>(e => Reroll(e.Index, e.Golden)));
            _subscriptions.Add(_eventBus.Subscribe<AugmentFullSlotsRequestedEvent>(e => ChooseFullSlotsOption(e.Option, e.Index)));
            PublishOwned();
        }

        public void OpenDraft() => OpenDraft(DrawRarity(), Random.value < _catalog.GoldRerollChance);

        /// <summary>Opens a draft of <paramref name="rarity"/> (tests force the rarity and the golden reroll).</summary>
        public void OpenDraft(RarityTier rarity, bool golden) {
            _rarity = rarity;
            _golden = golden;
            _pending = null;
            _choices.Clear();
            _cardRerolls.Clear();
            for (int i = 0; i < _catalog.ChoiceCount; i++) {
                AugmentDefinition pick = PickAugment(rarity);
                if (pick == null) break;
                _choices.Add(pick);
                _cardRerolls.Add(RerollsPerCard);
            }
            if (_choices.Count == 0) {   // nothing left to offer
                Close();
                return;
            }
            _phase = AugmentDraftPhase.Choose;
            PublishDraft();
        }

        /// <summary>More rerolls on every card of every draft of the run (meta progression, §9.7).</summary>
        public void AddRerollsPerCard(int count) {
            _bonusRerolls += Mathf.Max(0, count);
            for (int i = 0; i < _cardRerolls.Count; i++) _cardRerolls[i] += Mathf.Max(0, count);
            if (_phase != AugmentDraftPhase.Closed) PublishDraft();
        }

        /// <summary>A random augment of <paramref name="rarity"/>, outside any draft (meta starting augment, §9.7).</summary>
        public AugmentDefinition GrantRandom(RarityTier rarity) {
            AugmentDefinition pick = PickAugment(rarity);
            if (pick == null || _owned.Count >= Slots) return null;
            Add(pick);
            _eventBus.Publish(new GameplayFeedbackEvent($"Augment de départ : {pick.DisplayName}"));
            return pick;
        }

        public void Pick(int index) {
            if (_phase != AugmentDraftPhase.Choose || index < 0 || index >= _choices.Count) return;
            AugmentDefinition chosen = _choices[index];
            if (_owned.Count < Slots) {
                Add(chosen);
                Close();
                return;
            }
            _pending = chosen;
            _phase = AugmentDraftPhase.SlotsFull;
            PublishDraft();
        }

        /// <summary>Rerolls card <paramref name="index"/>: with its own reroll (same rarity), or with the golden one (rarity + 1).</summary>
        public void Reroll(int index, bool golden = false) {
            if (_phase != AugmentDraftPhase.Choose || index < 0 || index >= _choices.Count) return;
            if (golden ? !_golden : _cardRerolls[index] <= 0) return;
            RarityTier rarity = _choices[index].Rarity;
            if (golden) rarity = (RarityTier)Mathf.Min((int)rarity + 1, (int)RarityTier.Prismatic);
            AugmentDefinition pick = PickAugment(rarity);
            if (pick == null) {
                _eventBus.Publish(new GameplayFeedbackEvent("Plus rien à proposer"));
                return;
            }
            _choices[index] = pick;
            if (golden) _golden = false;
            else _cardRerolls[index]--;
            PublishDraft();
        }

        public void ChooseFullSlotsOption(FullSlotsOption option, int index) {
            if (_phase != AugmentDraftPhase.SlotsFull || _pending == null) return;
            switch (option) {
                case FullSlotsOption.Anvil:
                    if (_catalog.FullSlotsAnvil == null) return;
                    _phase = AugmentDraftPhase.Closed;
                    PublishDraft();
                    _shop.OpenAnvil(_catalog.FullSlotsAnvil, endsStep: true, forcedRarity: _pending.Rarity);   // its choice ends the step
                    return;
                case FullSlotsOption.Replace:
                    List<int> replaceable = ReplaceableIndices(_pending.Rarity);
                    if (index < 0 || index >= replaceable.Count) return;
                    int slot = replaceable[index];
                    _upgrades.RemovePassive(_owned[slot].passive);
                    _owned.RemoveAt(slot);
                    Add(_pending);
                    break;
                case FullSlotsOption.SlotOrGold:
                    if (Slots < _catalog.MaxSlots) {
                        Slots++;
                        Add(_pending);
                    }
                    else _wallet.Add(_catalog.GoldFor(_pending.Rarity));
                    break;
            }
            Close();
        }

        // Silver replaces a higher rarity, Gold the same, Prismatic a lower one (design doc §9.4)
        private List<int> ReplaceableIndices(RarityTier rarity) {
            var result = new List<int>();
            for (int i = 0; i < _owned.Count; i++) {
                RarityTier owned = _owned[i].augment.Rarity;
                bool ok = rarity switch {
                    RarityTier.Silver => owned > RarityTier.Silver,
                    RarityTier.Gold => owned == RarityTier.Gold,
                    _ => owned < RarityTier.Prismatic,
                };
                if (ok) result.Add(i);
            }
            return result;
        }

        private void Add(AugmentDefinition augment) {
            Passive passive = _upgrades.AddPassive(augment.Passive, augment);
            _owned.Add((augment, passive));
            PublishOwned();
            _eventBus.Publish(new GameplayFeedbackEvent($"Augment : {augment.DisplayName}"));
        }

        private void Close() {
            _phase = AugmentDraftPhase.Closed;
            _choices.Clear();
            _pending = null;
            PublishDraft();
            _eventBus.Publish(new RewardStepDoneEvent());
        }

        private RarityTier DrawRarity() {
            float[] w = _catalog.RarityWeights;
            float total = 0f;
            for (int i = 0; i < 3 && i < w.Length; i++) total += Mathf.Max(0f, w[i]);
            float roll = Random.value * total;
            for (int i = 0; i < 3 && i < w.Length; i++) {
                roll -= Mathf.Max(0f, w[i]);
                if (roll <= 0f && w[i] > 0f) return (RarityTier)i;
            }
            return RarityTier.Silver;
        }

        // Not owned and not already offered
        private AugmentDefinition PickAugment(RarityTier rarity) {
            var candidates = new List<AugmentDefinition>();
            foreach (AugmentDefinition a in _catalog.Augments)
                if (a != null && a.Rarity == rarity && !IsOwned(a) && !_choices.Contains(a)) candidates.Add(a);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private bool IsOwned(AugmentDefinition augment) {
            foreach ((AugmentDefinition a, Passive _) in _owned) if (a == augment) return true;
            return false;
        }

        private void PublishDraft() {
            var replaceable = new List<AugmentDefinition>();
            if (_phase == AugmentDraftPhase.SlotsFull && _pending != null)
                foreach (int i in ReplaceableIndices(_pending.Rarity)) replaceable.Add(_owned[i].augment);
            _eventBus.Publish(new AugmentDraftEvent(_phase, _rarity, _choices.ToArray(), _cardRerolls.ToArray(), _golden, _pending,
                                                    replaceable, Slots < _catalog.MaxSlots,
                                                    _pending != null ? _catalog.GoldFor(_pending.Rarity) : 0));
        }

        private void PublishOwned() => _eventBus.Publish(new AugmentsChangedEvent(Owned, Slots));

        public void Dispose() => _subscriptions.Dispose();
    }
}
