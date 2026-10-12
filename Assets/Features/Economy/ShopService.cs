using System;
using System.Collections.Generic;
using Features.Events;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Features.Economy {
    /// <summary>
    /// Shops and anvils of the end of a wave (design doc §9.3). Driven by bus requests, answers with
    /// snapshots (ShopStateEvent, AnvilOfferEvent):
    /// - Light shop: elixirs and stat anvils. Full shop: + legendary items and item anvils.
    /// - Per shop: one elixir, one item (item anvils count as the item), stat anvils at will.
    /// - Anvil: 3 choices of the same rarity. Stat anvil: rarity drawn at opening (RarityWeights),
    ///   no reroll. Item anvil: fixed tier, items not owned yet, RerollsPerChoice per choice.
    /// - The free prismatic anvil of the schedule opens on its own (RewardKind.FreePrismaticAnvil).
    /// Closing the shop or resolving the free anvil ends the reward step (RewardStepDoneEvent).
    /// Game scope service.
    /// </summary>
    public sealed class ShopService : IStartable, IDisposable {
        private readonly ShopCatalog _catalog;
        private readonly Wallet _wallet;
        private readonly Inventory _inventory;
        private readonly PlayerUpgrades _upgrades;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();

        private AnvilDefinition _anvil;
        private RarityTier _rarity;
        private readonly List<ScriptableObject> _choices = new();
        private readonly List<int> _rerolls = new();
        private bool _anvilEndsStep;

        public bool IsOpen { get; private set; }
        public bool IsFull { get; private set; }
        public bool ElixirBought { get; private set; }
        public bool ItemBought { get; private set; }
        public bool AnvilOpen => _anvil != null;
        public IReadOnlyList<ScriptableObject> AnvilChoices => _choices;
        public RarityTier AnvilRarity => _rarity;

        public ShopService(ShopCatalog catalog, Wallet wallet, Inventory inventory, PlayerUpgrades upgrades, IEventBus eventBus) {
            _catalog = catalog;
            _wallet = wallet;
            _inventory = inventory;
            _upgrades = upgrades;
            _eventBus = eventBus;
        }

        public void Start() {
            _subscriptions.Add(_eventBus.Subscribe<RewardStepEvent>(e => {
                switch (e.Kind) {
                    case RewardKind.LightShop: Open(full: false); break;
                    case RewardKind.FullShop: Open(full: true); break;
                    case RewardKind.FreePrismaticAnvil: OpenAnvil(_catalog.FreeAnvil, endsStep: true); break;
                }
            }));
            _subscriptions.Add(_eventBus.Subscribe<ShopBuyRequestedEvent>(e => TryBuy(e.Offer)));
            _subscriptions.Add(_eventBus.Subscribe<ShopCloseRequestedEvent>(_ => Close()));
            _subscriptions.Add(_eventBus.Subscribe<AnvilChooseRequestedEvent>(e => Choose(e.Index)));
            _subscriptions.Add(_eventBus.Subscribe<AnvilRerollRequestedEvent>(e => Reroll(e.Index)));
            _subscriptions.Add(_eventBus.Subscribe<AnvilSkipRequestedEvent>(_ => Skip()));
            _subscriptions.Add(_eventBus.Subscribe<GoldChangedEvent>(_ => { if (IsOpen) PublishShop(); }));
        }

        public void Open(bool full) {
            IsOpen = true;
            IsFull = full;
            ElixirBought = ItemBought = false;
            PublishShop();
        }

        public void Close() {
            if (!IsOpen || AnvilOpen) return;
            IsOpen = false;
            PublishShop();
            _eventBus.Publish(new RewardStepDoneEvent());
        }

        /// <summary>Why <paramref name="offer"/> cannot be bought now (null: it can).</summary>
        public string RefusalReason(ScriptableObject offer) {
            if (!IsOpen || AnvilOpen) return "Boutique fermée";
            switch (offer) {
                case ElixirDefinition elixir:
                    if (ElixirBought) return "Un élixir par boutique";
                    return Afford(elixir.Cost);
                case ItemDefinition item:
                    if (!IsFull || item.Tier != ItemTier.Legendary) return "Indisponible";
                    if (ItemBought) return "Un item par boutique";
                    if (_inventory.IsFull) return "Inventaire plein";
                    if (item.Unique && _inventory.Contains(item)) return "Déjà possédé";
                    return Afford(item.Cost);
                case AnvilDefinition anvil when anvil.Kind == AnvilKind.Item:
                    if (!IsFull) return "Indisponible";
                    if (ItemBought) return "Un item par boutique";
                    if (_inventory.IsFull) return "Inventaire plein";
                    return Afford(anvil.Cost);
                case AnvilDefinition anvil:
                    return Afford(anvil.Cost);
                default:
                    return "Indisponible";
            }
        }

        private string Afford(int cost) => _wallet.Gold >= cost ? null : "Pas assez d'or";

        public bool TryBuy(ScriptableObject offer) {
            string refusal = RefusalReason(offer);
            if (refusal != null) {
                _eventBus.Publish(new GameplayFeedbackEvent(refusal));
                return false;
            }
            switch (offer) {
                case ElixirDefinition elixir:
                    _wallet.TrySpend(elixir.Cost);
                    _upgrades.ApplyElixir(elixir);
                    ElixirBought = true;
                    break;
                case ItemDefinition item:
                    _wallet.TrySpend(item.Cost);
                    _inventory.TryAdd(item);
                    ItemBought = true;
                    break;
                case AnvilDefinition anvil:
                    _wallet.TrySpend(anvil.Cost);
                    if (anvil.Kind == AnvilKind.Item) ItemBought = true;
                    OpenAnvil(anvil, endsStep: false);
                    break;
            }
            PublishShop();
            return true;
        }

        /// <summary>Opens <paramref name="anvil"/>; with <paramref name="endsStep"/> its resolution ends the reward step.</summary>
        public void OpenAnvil(AnvilDefinition anvil, bool endsStep) {
            if (anvil == null) {
                if (endsStep) _eventBus.Publish(new RewardStepDoneEvent());
                return;
            }
            _anvil = anvil;
            _anvilEndsStep = endsStep;
            _choices.Clear();
            _rerolls.Clear();
            _rarity = anvil.Kind == AnvilKind.Stat ? DrawRarity(anvil.RarityWeights) : RarityTier.Prismatic;
            for (int i = 0; i < anvil.ChoiceCount; i++) {
                ScriptableObject pick = PickChoice();
                if (pick == null) break;
                _choices.Add(pick);
                _rerolls.Add(anvil.Kind == AnvilKind.Item ? anvil.RerollsPerChoice : 0);
            }
            PublishAnvil();
        }

        public void Choose(int index) {
            if (!AnvilOpen || index < 0 || index >= _choices.Count) return;
            switch (_choices[index]) {
                case StatChoiceDefinition stat:
                    _upgrades.ApplyPermanent(stat.Stats, stat);
                    break;
                case ItemDefinition item:
                    if (!_inventory.TryAdd(item)) {
                        _eventBus.Publish(new GameplayFeedbackEvent("Inventaire plein"));
                        return;
                    }
                    break;
            }
            CloseAnvil();
        }

        public void Reroll(int index) {
            if (!AnvilOpen || index < 0 || index >= _choices.Count || _rerolls[index] <= 0) return;
            ScriptableObject pick = PickChoice();
            if (pick == null) {
                _eventBus.Publish(new GameplayFeedbackEvent("Plus rien à proposer"));
                return;
            }
            _choices[index] = pick;
            _rerolls[index]--;
            PublishAnvil();
        }

        // Only an item anvil that cannot give its item (inventory full) can be left empty-handed
        private bool CanSkip => AnvilOpen && (_choices.Count == 0 || (_anvil.Kind == AnvilKind.Item && _inventory.IsFull));

        public void Skip() {
            if (CanSkip) CloseAnvil();
        }

        private void CloseAnvil() {
            _anvil = null;
            _choices.Clear();
            _rerolls.Clear();
            PublishAnvil();
            PublishShop();
            if (_anvilEndsStep) _eventBus.Publish(new RewardStepDoneEvent());
        }

        private static RarityTier DrawRarity(float[] weights) {
            float total = 0f;
            for (int i = 0; i < 3 && i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
            float roll = Random.value * total;
            for (int i = 0; i < 3 && i < weights.Length; i++) {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f && weights[i] > 0f) return (RarityTier)i;
            }
            return RarityTier.Silver;
        }

        // A random candidate not already offered (and, for items, not owned)
        private ScriptableObject PickChoice() {
            var candidates = new List<ScriptableObject>();
            if (_anvil.Kind == AnvilKind.Stat) {
                foreach (StatChoiceDefinition c in _catalog.StatChoices)
                    if (c != null && c.Rarity == _rarity && !_choices.Contains(c)) candidates.Add(c);
            }
            else {
                foreach (ItemDefinition item in _catalog.Items)
                    if (item != null && item.Tier == _anvil.ItemTier && !_choices.Contains(item) && _inventory.CanAdd(item)) candidates.Add(item);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>What this shop sells, in display order: elixirs, stat anvils, then (full shop) legendary items and item anvils.</summary>
        public List<ScriptableObject> Offers() {
            var offers = new List<ScriptableObject>();
            foreach (ElixirDefinition e in _catalog.Elixirs) if (e != null) offers.Add(e);
            foreach (AnvilDefinition a in _catalog.StatAnvils) if (a != null) offers.Add(a);
            if (!IsFull) return offers;
            foreach (ItemDefinition i in _catalog.Items) if (i != null && i.Tier == ItemTier.Legendary) offers.Add(i);
            foreach (AnvilDefinition a in _catalog.ItemAnvils) if (a != null) offers.Add(a);
            return offers;
        }

        private void PublishShop() {
            List<ScriptableObject> offers = IsOpen ? Offers() : new List<ScriptableObject>();
            var refusals = new string[offers.Count];
            for (int i = 0; i < offers.Count; i++) refusals[i] = RefusalReason(offers[i]);
            _eventBus.Publish(new ShopStateEvent(IsOpen, IsFull, ElixirBought, ItemBought, offers, refusals));
        }

        private void PublishAnvil() =>
            _eventBus.Publish(new AnvilOfferEvent(AnvilOpen, _anvil, _rarity, _choices.ToArray(), _rerolls.ToArray(), CanSkip));

        public void Dispose() => _subscriptions.Dispose();
    }
}
