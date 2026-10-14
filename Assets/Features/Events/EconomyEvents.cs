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
    /// Shop snapshot (ShopService → ShopPanel): everything it sells and, for each offer, why it
    /// cannot be bought right now (null: it can). Purchases happen between waves only (CanBuy).
    /// </summary>
    public readonly struct ShopStateEvent : IEvent {
        public readonly bool CanBuy;
        public readonly IReadOnlyList<ScriptableObject> Offers;
        public readonly IReadOnlyList<string> Refusals;
        public ShopStateEvent(bool canBuy, IReadOnlyList<ScriptableObject> offers, IReadOnlyList<string> refusals) {
            CanBuy = canBuy;
            Offers = offers;
            Refusals = refusals;
        }
    }

    /// <summary>Buy an elixir, an item or an anvil of the catalog.</summary>
    public readonly struct ShopBuyRequestedEvent : IEvent {
        public readonly ScriptableObject Offer;
        public ShopBuyRequestedEvent(ScriptableObject offer) => Offer = offer;
    }

    /// <summary>Close the shop screen (Escape, close button).</summary>
    public readonly struct ShopCloseRequestedEvent : IEvent { }

    /// <summary>Open or close the shop screen (shop key, HUD button).</summary>
    public readonly struct ShopToggleRequestedEvent : IEvent { }

    /// <summary>The shop screen opened or closed (input: Escape closes it first).</summary>
    public readonly struct ShopVisibilityChangedEvent : IEvent {
        public readonly bool Open;
        public ShopVisibilityChangedEvent(bool open) => Open = open;
    }

    /// <summary>Sell the item of inventory slot <see cref="Slot"/> (between waves, 70 % of its price).</summary>
    public readonly struct ShopSellRequestedEvent : IEvent {
        public readonly int Slot;
        public ShopSellRequestedEvent(int slot) => Slot = slot;
    }

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

namespace Features.Events {
    /// <summary>A passive of the player dropped a loot (LootService spawns it on the ground).</summary>
    public readonly struct LootDropRequestedEvent : Systems.EventBusses.IEvent {
        public readonly Passives.LootKind Kind;
        public readonly UnityEngine.Vector3 Position;
        public LootDropRequestedEvent(Passives.LootKind kind, UnityEngine.Vector3 position) {
            Kind = kind;
            Position = position;
        }
    }
}

namespace Features.Events {
    /// <summary>A reward earned during the wave, opened at its end before the scheduled ones (loot, §9.5).</summary>
    public readonly struct BonusRewardEarnedEvent : Systems.EventBusses.IEvent {
        public readonly Run.RewardKind Kind;
        public BonusRewardEarnedEvent(Run.RewardKind kind) => Kind = kind;
    }

    /// <summary>A summoner spell was picked up: it fills D until used, if D is empty (§9.5).</summary>
    public readonly struct SummonerSpellPickedEvent : Systems.EventBusses.IEvent {
        public readonly Spells.SpellDefinition Spell;
        public SummonerSpellPickedEvent(Spells.SpellDefinition spell) => Spell = spell;
    }

    /// <summary>Whether D currently holds a picked-up summoner spell (no other one can be picked up then).</summary>
    public readonly struct SummonerSlotChangedEvent : Systems.EventBusses.IEvent {
        public readonly bool HoldsConsumable;
        public SummonerSlotChangedEvent(bool holdsConsumable) => HoldsConsumable = holdsConsumable;
    }
}
