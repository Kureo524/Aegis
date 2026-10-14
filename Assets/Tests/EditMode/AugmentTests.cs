using System.Collections.Generic;
using Features.Augments;
using Features.Economy;
using Features.Events;
using Features.Passives;
using Features.Run;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;

namespace Game.Tests.EditMode {
    /// <summary>Augment drafts (design doc §9.4) on in-memory data.</summary>
    public class AugmentTests {
        private readonly List<Object> _assets = new();
        private EventBus _bus;
        private Wallet _wallet;
        private PlayerUpgrades _upgrades;
        private ShopService _shop;
        private AugmentController _augments;
        private int _stepsDone;

        private T Make<T>(string name, params (string field, object value)[] fields) where T : ScriptableObject {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            foreach ((string field, object value) in fields)
                typeof(T).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(asset, value);
            _assets.Add(asset);
            return asset;
        }

        [SetUp]
        public void SetUp() {
            var augments = new List<AugmentDefinition>();
            foreach (RarityTier rarity in new[] { RarityTier.Silver, RarityTier.Gold, RarityTier.Prismatic })
                for (int i = 0; i < 5; i++) {
                    var passive = Make<PassiveDefinition>($"{rarity}{i}", ("displayName", $"{rarity} {i}"));
                    augments.Add(Make<AugmentDefinition>($"Augment{rarity}{i}", ("rarity", rarity), ("passive", passive)));
                }
            var statChoices = new List<StatChoiceDefinition>();
            foreach (RarityTier rarity in new[] { RarityTier.Silver, RarityTier.Gold, RarityTier.Prismatic })
                for (int i = 0; i < 3; i++)
                    statChoices.Add(Make<StatChoiceDefinition>($"Choice{rarity}{i}", ("rarity", rarity),
                                                               ("stats", new[] { new StatBonus(StatType.MaxHealth, ModifierType.Flat, 10) })));
            var statAnvil = Make<AnvilDefinition>("StatAnvil", ("kind", AnvilKind.Stat));
            var shopCatalog = Make<ShopCatalog>("Shop", ("statAnvils", new[] { statAnvil }), ("statChoices", statChoices.ToArray()));
            var catalog = Make<AugmentCatalog>("Augments", ("augments", augments.ToArray()), ("baseSlots", 2), ("maxSlots", 3),
                                               ("rerollsPerCard", 1), ("fullSlotsAnvil", statAnvil), ("goldPerRarity", new[] { 100, 200, 300 }));

            _bus = new EventBus();
            var stats = new StatController();
            stats.Init(null);
            _wallet = new Wallet(_bus);
            _upgrades = new PlayerUpgrades(_bus);
            _upgrades.Bind(stats);
            _shop = new ShopService(shopCatalog, _wallet, new Inventory(_upgrades, _bus), _upgrades, _bus);
            _shop.Start();
            _augments = new AugmentController(catalog, _upgrades, _shop, _wallet, _bus);
            _augments.Start();
            _stepsDone = 0;
            _bus.Subscribe<RewardStepDoneEvent>(_ => _stepsDone++);
        }

        [TearDown]
        public void TearDown() {
            _augments.Dispose();
            _shop.Dispose();
            _upgrades.Dispose();
            _wallet.Dispose();
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private void Take(RarityTier rarity) {
            _augments.OpenDraft(rarity, golden: false);
            _augments.Pick(0);
        }

        [Test]
        public void Draft_ThreeOfTheSameRarity_NotOwned() {
            _augments.OpenDraft(RarityTier.Gold, golden: false);
            Assert.AreEqual(3, _augments.Choices.Count);
            CollectionAssert.AllItemsAreUnique(_augments.Choices);
            foreach (AugmentDefinition a in _augments.Choices) Assert.AreEqual(RarityTier.Gold, a.Rarity);

            AugmentDefinition taken = _augments.Choices[1];
            _augments.Pick(1);
            Assert.AreEqual(1, _stepsDone, "draft done");
            CollectionAssert.Contains(_augments.Owned, taken);

            _augments.OpenDraft(RarityTier.Gold, golden: false);
            CollectionAssert.DoesNotContain(_augments.Choices, taken, "owned augments are not offered again");
        }

        [Test]
        public void Rerolls_OnePerCardPerDraft_KeepTheRarity_NotKeptForTheNextDraft() {
            _augments.OpenDraft(RarityTier.Silver, golden: false);
            for (int card = 0; card < _augments.Choices.Count; card++) {
                Assert.AreEqual(1, _augments.RerollsLeft(card));
                _augments.Reroll(card);
                Assert.AreEqual(RarityTier.Silver, _augments.Choices[card].Rarity, "a reroll keeps the rarity");
                Assert.AreEqual(0, _augments.RerollsLeft(card));
            }
            AugmentDefinition before = _augments.Choices[0];
            _augments.Reroll(0);
            Assert.AreSame(before, _augments.Choices[0], "one reroll per card");

            _augments.Pick(0);
            _augments.OpenDraft(RarityTier.Silver, golden: false);
            Assert.AreEqual(1, _augments.RerollsLeft(0), "a new draft, fresh rerolls (none kept from the last one)");
        }

        [Test]
        public void GoldenReroll_IsSeparate_Once_RarityPlusOne() {
            _augments.OpenDraft(RarityTier.Silver, golden: true);
            _augments.Reroll(0);
            Assert.AreEqual(RarityTier.Silver, _augments.Choices[0].Rarity, "the card's own reroll never changes the rarity");
            Assert.IsTrue(_augments.GoldenReroll);

            _augments.Reroll(1, golden: true);
            Assert.AreEqual(RarityTier.Gold, _augments.Choices[1].Rarity, "golden reroll: rarity + 1");
            Assert.AreEqual(1, _augments.RerollsLeft(1), "the card keeps its own reroll");
            Assert.IsFalse(_augments.GoldenReroll, "once per draft");
            AugmentDefinition before = _augments.Choices[2];
            _augments.Reroll(2, golden: true);
            Assert.AreSame(before, _augments.Choices[2]);

            _augments.AddRerollsPerCard(1);
            Assert.AreEqual(2, _augments.RerollsLeft(2), "meta bonus: one more per card");
        }

        [Test]
        public void SlotsFull_Replace_FollowsTheRarityRule() {
            Take(RarityTier.Gold);
            Take(RarityTier.Silver);   // 2 slots full
            _augments.OpenDraft(RarityTier.Silver, golden: false);
            _augments.Pick(0);
            Assert.AreEqual(AugmentDraftPhase.SlotsFull, _augments.Phase);

            AugmentDefinition gold = _augments.Owned[0];
            _augments.ChooseFullSlotsOption(FullSlotsOption.Replace, 0);   // Silver replaces a higher rarity: the Gold one
            Assert.AreEqual(2, _augments.Owned.Count);
            CollectionAssert.DoesNotContain(_augments.Owned, gold);
        }

        [Test]
        public void SlotsFull_ExtraSlotThenGold() {
            Take(RarityTier.Silver);
            Take(RarityTier.Silver);
            _augments.OpenDraft(RarityTier.Prismatic, golden: false);
            _augments.Pick(0);
            _augments.ChooseFullSlotsOption(FullSlotsOption.SlotOrGold, 0);
            Assert.AreEqual(3, _augments.Slots, "+1 slot");
            Assert.AreEqual(3, _augments.Owned.Count, "the augment goes in it");

            _augments.OpenDraft(RarityTier.Gold, golden: false);
            _augments.Pick(0);
            _augments.ChooseFullSlotsOption(FullSlotsOption.SlotOrGold, 0);
            Assert.AreEqual(3, _augments.Slots, "MaxSlots reached");
            Assert.AreEqual(200, _wallet.Gold, "gold for a Gold augment instead");
        }

        [Test]
        public void SlotsFull_Anvil_OfTheDraftedRarity() {
            Take(RarityTier.Silver);
            Take(RarityTier.Silver);
            _augments.OpenDraft(RarityTier.Prismatic, golden: false);
            _augments.Pick(0);
            int done = _stepsDone;
            _augments.ChooseFullSlotsOption(FullSlotsOption.Anvil, 0);
            Assert.IsTrue(_shop.AnvilOpen);
            Assert.AreEqual(RarityTier.Prismatic, _shop.AnvilRarity, "stat anvil of the augment's rarity");
            Assert.AreEqual(done, _stepsDone, "the step ends with the anvil");
            _shop.Choose(0);
            Assert.AreEqual(done + 1, _stepsDone);
            Assert.AreEqual(2, _augments.Owned.Count, "no augment added");
        }
    }
}
