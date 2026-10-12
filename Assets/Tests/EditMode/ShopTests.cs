using System.Collections.Generic;
using Features.Economy;
using Features.Events;
using Features.Run;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;

namespace Game.Tests.EditMode {
    /// <summary>Shops, anvils, inventory and elixirs (design doc §9.3) on in-memory data.</summary>
    public class ShopTests {
        private readonly List<Object> _assets = new();
        private EventBus _bus;
        private Wallet _wallet;
        private Inventory _inventory;
        private PlayerUpgrades _upgrades;
        private ShopService _shop;
        private StatController _stats;
        private ShopCatalog _catalog;
        private ElixirDefinition _elixir;
        private AnvilDefinition _statAnvil, _legendaryAnvil, _prismaticAnvil;
        private readonly List<ItemDefinition> _legendaries = new();

        private T Make<T>(string name, params (string field, object value)[] fields) where T : ScriptableObject {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            foreach ((string field, object value) in fields)
                typeof(T).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(asset, value);
            _assets.Add(asset);
            return asset;
        }

        private static StatBonus[] Bonus(StatType stat, float value) => new[] { new StatBonus(stat, ModifierType.Flat, value) };

        [SetUp]
        public void SetUp() {
            _legendaries.Clear();
            for (int i = 0; i < 8; i++)
                _legendaries.Add(Make<ItemDefinition>($"Legendary{i}", ("cost", 100), ("tier", ItemTier.Legendary),
                                                      ("stats", Bonus(StatType.PhysicalAttack, 10 + i))));
            var prismatics = new List<ItemDefinition>();
            for (int i = 0; i < 4; i++)
                prismatics.Add(Make<ItemDefinition>($"Prismatic{i}", ("tier", ItemTier.Prismatic), ("stats", Bonus(StatType.MagicAttack, 50))));
            var choices = new List<StatChoiceDefinition>();
            foreach (RarityTier rarity in new[] { RarityTier.Silver, RarityTier.Gold, RarityTier.Prismatic })
                for (int i = 0; i < 4; i++)
                    choices.Add(Make<StatChoiceDefinition>($"{rarity}{i}", ("rarity", rarity), ("stats", Bonus(StatType.MaxHealth, 100))));

            _elixir = Make<ElixirDefinition>("Elixir", ("cost", 40), ("stats", Bonus(StatType.Armor, 20)));
            _statAnvil = Make<AnvilDefinition>("StatAnvil", ("kind", AnvilKind.Stat), ("cost", 60), ("rarityWeights", new[] { 0f, 1f, 0f }));
            _legendaryAnvil = Make<AnvilDefinition>("LegendaryAnvil", ("kind", AnvilKind.Item), ("itemTier", ItemTier.Legendary), ("cost", 80), ("rerollsPerChoice", 1));
            _prismaticAnvil = Make<AnvilDefinition>("PrismaticAnvil", ("kind", AnvilKind.Item), ("itemTier", ItemTier.Prismatic), ("cost", 300), ("rerollsPerChoice", 1));
            var allItems = new List<ItemDefinition>(_legendaries);
            allItems.AddRange(prismatics);
            _catalog = Make<ShopCatalog>("Catalog", ("elixirs", new[] { _elixir }), ("statAnvils", new[] { _statAnvil }),
                                         ("itemAnvils", new[] { _legendaryAnvil, _prismaticAnvil }), ("items", allItems.ToArray()),
                                         ("statChoices", choices.ToArray()), ("freeAnvil", _prismaticAnvil));

            _bus = new EventBus();
            _stats = new StatController();
            _stats.Init(null);
            _wallet = new Wallet(_bus);
            _upgrades = new PlayerUpgrades(_bus);
            _upgrades.Bind(_stats);
            _inventory = new Inventory(_upgrades, _bus);
            _shop = new ShopService(_catalog, _wallet, _inventory, _upgrades, _bus);
            _shop.Start();
            _wallet.Add(1000);
        }

        [TearDown]
        public void TearDown() {
            _shop.Dispose();
            _upgrades.Dispose();
            _wallet.Dispose();
            foreach (Object asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void LightShop_OneElixir_NoItems_StatAnvilsAtWill() {
            _shop.Open(full: false);
            Assert.IsTrue(_shop.TryBuy(_elixir));
            Assert.AreEqual(960, _wallet.Gold);
            Assert.AreEqual(20f, _stats.Value(StatType.Armor), "elixir active at once");
            Assert.IsFalse(_shop.TryBuy(_elixir), "one elixir per shop");
            Assert.IsFalse(_shop.TryBuy(_legendaries[0]), "no items in the light shop");
            Assert.IsFalse(_shop.TryBuy(_legendaryAnvil), "no item anvils in the light shop");

            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(_shop.TryBuy(_statAnvil));
                Assert.IsTrue(_shop.AnvilOpen);
                Assert.AreEqual(RarityTier.Gold, _shop.AnvilRarity, "rarity drawn from the weights");
                Assert.AreEqual(3, _shop.AnvilChoices.Count);
                CollectionAssert.AllItemsAreUnique(_shop.AnvilChoices);
                foreach (ScriptableObject c in _shop.AnvilChoices) Assert.AreEqual(RarityTier.Gold, ((StatChoiceDefinition)c).Rarity);
                _shop.Choose(0);
                Assert.IsFalse(_shop.AnvilOpen);
            }
            Assert.AreEqual(200f, _stats.Value(StatType.MaxHealth), "two permanent stat bonuses");
        }

        [Test]
        public void FullShop_OneItem_ItemAnvilsCountAsTheItem() {
            _shop.Open(full: true);
            Assert.IsTrue(_shop.TryBuy(_legendaries[0]));
            Assert.AreEqual(10f, _stats.Value(StatType.PhysicalAttack));
            Assert.IsFalse(_shop.TryBuy(_legendaries[1]), "one item per shop");
            Assert.IsFalse(_shop.TryBuy(_legendaryAnvil), "item anvils count as the item");

            _shop.Open(full: true);   // next full shop
            Assert.IsFalse(_shop.TryBuy(_legendaries[0]), "unique: already owned");
            Assert.IsTrue(_shop.TryBuy(_legendaryAnvil));
            CollectionAssert.DoesNotContain(_shop.AnvilChoices, _legendaries[0], "owned items are not offered");
            Assert.IsFalse(_shop.TryBuy(_legendaries[1]), "anvil bought: no other item");
        }

        [Test]
        public void ItemAnvil_RerollsOncePerChoice() {
            _shop.Open(full: true);
            _shop.TryBuy(_legendaryAnvil);
            ScriptableObject before = _shop.AnvilChoices[1];
            _shop.Reroll(1);
            Assert.AreNotSame(before, _shop.AnvilChoices[1]);
            CollectionAssert.AllItemsAreUnique(_shop.AnvilChoices);
            ScriptableObject after = _shop.AnvilChoices[1];
            _shop.Reroll(1);
            Assert.AreSame(after, _shop.AnvilChoices[1], "1 reroll per choice");
            _shop.Choose(1);
            Assert.IsTrue(_inventory.Contains((ItemDefinition)after));
        }

        [Test]
        public void InventoryFull_RefusesItems() {
            for (int i = 0; i < Inventory.Capacity; i++) Assert.IsTrue(_inventory.TryAdd(_legendaries[i]));
            _shop.Open(full: true);
            Assert.IsFalse(_shop.TryBuy(_legendaries[7]));
            Assert.AreEqual("Inventaire plein", _shop.RefusalReason(_legendaryAnvil));
        }

        [Test]
        public void Elixir_LastsUntilTheEndOfTheNextWave() {
            _shop.Open(full: false);
            _shop.TryBuy(_elixir);
            _bus.Publish(new RunStateChangedEvent(RunState.Reward, RunState.PreWave));
            _bus.Publish(new RunStateChangedEvent(RunState.PreWave, RunState.Combat));
            Assert.AreEqual(20f, _stats.Value(StatType.Armor), "still active during the next wave");
            _bus.Publish(new RunStateChangedEvent(RunState.Combat, RunState.Reward));
            Assert.AreEqual(0f, _stats.Value(StatType.Armor), "gone at its end");
            Assert.IsNull(_upgrades.ActiveElixir);
        }

        [Test]
        public void Rewards_PlayInOrder_ThenCloseTheRewardScreen() {
            var controller = new RewardController(_bus);
            controller.Start();
            int closed = 0;
            _bus.Subscribe<RewardClosedEvent>(_ => closed++);

            _bus.Publish(new WaveRewardEvent(2, false, new[] { RewardKind.FreePrismaticAnvil, RewardKind.LightShop }));
            Assert.IsTrue(_shop.AnvilOpen, "free prismatic anvil first");
            Assert.AreEqual(1000, _wallet.Gold, "free");
            _shop.Choose(0);
            Assert.AreEqual(ItemTier.Prismatic, _inventory.Items[0].Tier);
            Assert.IsTrue(_shop.IsOpen, "then the light shop");
            Assert.IsFalse(_shop.IsFull);
            Assert.AreEqual(0, closed);
            _shop.Close();
            Assert.AreEqual(1, closed, "all rewards done");
            controller.Dispose();
        }
    }
}
