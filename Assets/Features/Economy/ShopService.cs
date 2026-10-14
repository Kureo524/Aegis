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
    /// The shop and the anvils (design doc §9.3). The shop is always there to browse; purchases and
    /// sales happen between waves only (RunState.PreWave: the break after the rewards, or before the
    /// first wave). No limit per visit: gold is the only limit. Driven by bus requests, answers with
    /// snapshots (ShopStateEvent, AnvilOfferEvent):
    /// - Elixirs (one active at a time: a new one replaces it), stat anvils, legendary items and
    ///   boots (inventory space, unique groups), item anvils.
    /// - Selling an item gives back ItemDefinition.SellValue (70 % of its price).
    /// - Anvil: 3 choices of the same rarity. Stat anvil: rarity drawn at opening (RarityWeights),
    ///   no reroll. Item anvil: fixed tier, items not owned yet, RerollsPerChoice per choice.
    /// - The free anvils of the reward schedule open on their own and end their reward step.
    /// Game scope service.
    /// </summary>
    public sealed class ShopService : IStartable, IDisposable {
        public const string BetweenWavesOnly = "Achats entre les vagues";

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

        /// <summary>Purchases and sales are open (between waves). True outside a run (sandbox).</summary>
        public bool CanBuy { get; private set; } = true;
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
                    case RewardKind.FreePrismaticAnvil: OpenAnvil(_catalog.FreeAnvil, endsStep: true); break;
                    case RewardKind.FreeStatAnvil:
                        OpenAnvil(_catalog.StatAnvils.Count > 0 ? _catalog.StatAnvils[0] : null, endsStep: true);
                        break;
                }
            }));
            _subscriptions.Add(_eventBus.Subscribe<RunStateChangedEvent>(e => {
                CanBuy = e.To == RunState.PreWave;
                PublishShop();
            }));
            _subscriptions.Add(_eventBus.Subscribe<ShopBuyRequestedEvent>(e => TryBuy(e.Offer)));
            _subscriptions.Add(_eventBus.Subscribe<ShopSellRequestedEvent>(e => TrySell(e.Slot)));
            _subscriptions.Add(_eventBus.Subscribe<AnvilChooseRequestedEvent>(e => Choose(e.Index)));
            _subscriptions.Add(_eventBus.Subscribe<AnvilRerollRequestedEvent>(e => Reroll(e.Index)));
            _subscriptions.Add(_eventBus.Subscribe<AnvilSkipRequestedEvent>(_ => Skip()));
            _subscriptions.Add(_eventBus.Subscribe<GoldChangedEvent>(_ => PublishShop()));
            _subscriptions.Add(_eventBus.Subscribe<InventoryChangedEvent>(_ => PublishShop()));
            PublishShop();
        }

        /// <summary>Why <paramref name="offer"/> cannot be bought now (null: it can).</summary>
        public string RefusalReason(ScriptableObject offer) {
            if (AnvilOpen) return "Une enclume est ouverte";
            if (!CanBuy) return BetweenWavesOnly;
            switch (offer) {
                case ElixirDefinition elixir:
                    return Afford(elixir.Cost);
                case ItemDefinition item:
                    if (item.Tier != ItemTier.Legendary) return "Seulement dans les enclumes";
                    if (_inventory.IsFull) return "Inventaire plein";
                    if (item.Unique && _inventory.Contains(item)) return "Déjà possédé";
                    if (_inventory.GroupOwner(item) != null) return $"Un seul objet « {item.UniqueGroup} »";
                    return Afford(item.Cost);
                case AnvilDefinition anvil when anvil.Kind == AnvilKind.Item:
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
                    break;
                case ItemDefinition item:
                    _wallet.TrySpend(item.Cost);
                    _inventory.TryAdd(item);
                    break;
                case AnvilDefinition anvil:
                    _wallet.TrySpend(anvil.Cost);
                    OpenAnvil(anvil, endsStep: false);
                    break;
            }
            PublishShop();
            return true;
        }

        /// <summary>Why the item of slot <paramref name="slot"/> cannot be sold now (null: it can).</summary>
        public string SellRefusal(int slot) {
            if (slot < 0 || slot >= _inventory.Items.Count) return "Emplacement vide";
            if (AnvilOpen) return "Une enclume est ouverte";
            return CanBuy ? null : BetweenWavesOnly;
        }

        /// <summary>Sells the item of slot <paramref name="slot"/> for its SellValue.</summary>
        public bool TrySell(int slot) {
            string refusal = SellRefusal(slot);
            if (refusal != null) {
                _eventBus.Publish(new GameplayFeedbackEvent(refusal));
                return false;
            }
            ItemDefinition item = _inventory.RemoveAt(slot);
            _wallet.Add(item.SellValue);
            _eventBus.Publish(new GameplayFeedbackEvent($"{item.DisplayName} vendu : +{item.SellValue} or"));
            PublishShop();
            return true;
        }

        /// <summary>
        /// Opens <paramref name="anvil"/>; with <paramref name="endsStep"/> its resolution ends the reward
        /// step. <paramref name="forcedRarity"/>: stat anvil of that rarity instead of a drawn one (augments, §9.4).
        /// </summary>
        public void OpenAnvil(AnvilDefinition anvil, bool endsStep, RarityTier? forcedRarity = null) {
            if (anvil == null) {
                if (endsStep) _eventBus.Publish(new RewardStepDoneEvent());
                return;
            }
            _anvil = anvil;
            _anvilEndsStep = endsStep;
            _choices.Clear();
            _rerolls.Clear();
            _rarity = anvil.Kind == AnvilKind.Stat ? forcedRarity ?? DrawRarity(anvil.RarityWeights) : RarityTier.Prismatic;
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
                    if (item != null && item.Tier == _anvil.ItemTier && !item.IsBoots && (_anvil.Role == ItemRole.None || (item.Roles & _anvil.Role) != 0)
                        && !_choices.Contains(item) && _inventory.CanAdd(item)) candidates.Add(item);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>What the shop sells, in display order: elixirs, anvils, then the items (boots and legendaries).</summary>
        public List<ScriptableObject> Offers() {
            var offers = new List<ScriptableObject>();
            foreach (ElixirDefinition e in _catalog.Elixirs) if (e != null) offers.Add(e);
            foreach (AnvilDefinition a in _catalog.StatAnvils) if (a != null) offers.Add(a);
            foreach (AnvilDefinition a in _catalog.ItemAnvils) if (a != null) offers.Add(a);
            foreach (ItemDefinition i in _catalog.Items) if (i != null && i.Tier == ItemTier.Legendary) offers.Add(i);
            return offers;
        }

        private void PublishShop() {
            List<ScriptableObject> offers = Offers();
            var refusals = new string[offers.Count];
            for (int i = 0; i < offers.Count; i++) refusals[i] = RefusalReason(offers[i]);
            _eventBus.Publish(new ShopStateEvent(CanBuy, offers, refusals));
        }

        private void PublishAnvil() =>
            _eventBus.Publish(new AnvilOfferEvent(AnvilOpen, _anvil, _rarity, _choices.ToArray(), _rerolls.ToArray(), CanSkip));

        public void Dispose() => _subscriptions.Dispose();
    }
}
