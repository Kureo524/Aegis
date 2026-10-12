using System.Collections.Generic;
using Features.Economy;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;

namespace Features.Events {
    /// <summary>The run's items changed (HUD, shop).</summary>
    public readonly struct InventoryChangedEvent : IEvent {
        public readonly IReadOnlyList<ItemDefinition> Items;
        public readonly int Capacity;
        public InventoryChangedEvent(IReadOnlyList<ItemDefinition> items, int capacity) {
            Items = items;
            Capacity = capacity;
        }
    }

    /// <summary>The active elixir (null: none), for the HUD.</summary>
    public readonly struct ElixirChangedEvent : IEvent {
        public readonly ElixirDefinition Elixir;
        public ElixirChangedEvent(ElixirDefinition elixir) => Elixir = elixir;
    }

    /// <summary>
    /// Shop snapshot (ShopService → ShopPanel): open or not, which shop, what was bought, the offers
    /// in display order and, for each, why it cannot be bought (null: it can).
    /// </summary>
    public readonly struct ShopStateEvent : IEvent {
        public readonly bool Open;
        public readonly bool Full;
        public readonly bool ElixirBought;
        public readonly bool ItemBought;
        public readonly IReadOnlyList<ScriptableObject> Offers;
        public readonly IReadOnlyList<string> Refusals;
        public ShopStateEvent(bool open, bool full, bool elixirBought, bool itemBought,
                              IReadOnlyList<ScriptableObject> offers, IReadOnlyList<string> refusals) {
            Open = open;
            Full = full;
            ElixirBought = elixirBought;
            ItemBought = itemBought;
            Offers = offers;
            Refusals = refusals;
        }
    }

    /// <summary>Buy an elixir, an item or an anvil of the catalog.</summary>
    public readonly struct ShopBuyRequestedEvent : IEvent {
        public readonly ScriptableObject Offer;
        public ShopBuyRequestedEvent(ScriptableObject offer) => Offer = offer;
    }

    public readonly struct ShopCloseRequestedEvent : IEvent { }

    /// <summary>
    /// Anvil snapshot (ShopService → AnvilPanel): its choices (ItemDefinition or
    /// StatChoiceDefinition), the rerolls left per choice, the rarity drawn.
    /// </summary>
    public readonly struct AnvilOfferEvent : IEvent {
        public readonly bool Open;
        public readonly AnvilDefinition Anvil;
        public readonly RarityTier Rarity;
        public readonly IReadOnlyList<ScriptableObject> Choices;
        public readonly IReadOnlyList<int> Rerolls;
        public readonly bool CanSkip;
        public AnvilOfferEvent(bool open, AnvilDefinition anvil, RarityTier rarity, IReadOnlyList<ScriptableObject> choices,
                               IReadOnlyList<int> rerolls, bool canSkip) {
            Open = open;
            Anvil = anvil;
            Rarity = rarity;
            Choices = choices;
            Rerolls = rerolls;
            CanSkip = canSkip;
        }
    }

    public readonly struct AnvilChooseRequestedEvent : IEvent {
        public readonly int Index;
        public AnvilChooseRequestedEvent(int index) => Index = index;
    }

    public readonly struct AnvilRerollRequestedEvent : IEvent {
        public readonly int Index;
        public AnvilRerollRequestedEvent(int index) => Index = index;
    }

    /// <summary>Leave a free item anvil without an item (inventory full).</summary>
    public readonly struct AnvilSkipRequestedEvent : IEvent { }

    /// <summary>One step of the end-of-wave rewards starts (RewardController → shop, anvil, augment screen).</summary>
    public readonly struct RewardStepEvent : IEvent {
        public readonly RewardKind Kind;
        public RewardStepEvent(RewardKind kind) => Kind = kind;
    }

    /// <summary>The current reward step is over (shop closed, anvil resolved, augment screen closed).</summary>
    public readonly struct RewardStepDoneEvent : IEvent { }
}
