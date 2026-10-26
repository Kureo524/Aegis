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
        private ItemDefinition _boots;

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
                                                      ("stats", Bonus(StatType.PhysicalAttack, 10 + i)),
                                                      ("roles", i < 4 ? ItemRole.Mage : ItemRole.Tank)));
            _boots = Make<ItemDefinition>("Boots", ("cost", 100), ("tier", ItemTier.Legendary), ("uniqueGroup", ItemDefinition.BootsGroup),
                                          ("stats", Bonus(StatType.MoveSpeed, 1)), ("roles", ItemRole.Mage | ItemRole.Tank));
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
            var allItems = new List<ItemDefinition>(_legendaries) { _boots };
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

        private void Break() => _bus.Publish(new RunStateChangedEvent(RunState.Reward, RunState.PreWave));
        private void Wave() => _bus.Publish(new RunStateChangedEvent(RunState.PreWave, RunState.Combat));

        [Test]
        public void BetweenWaves_NoLimitButGold_ElixirsItemsAndAnvils() {
            Break();
            Assert.IsTrue(_shop.CanBuy);
            Assert.IsTrue(_shop.TryBuy(_elixir));
            Assert.AreEqual(960, _wallet.Gold);
            Assert.AreEqual(20f, _stats.Value(StatType.Armor), "elixir active at once");
            Assert.IsTrue(_shop.TryBuy(_legendaries[0]));
            Assert.IsTrue(_shop.TryBuy(_legendaries[1]), "no limit of one item per visit");
            Assert.IsTrue(_shop.TryBuy(_legendaries[2]));
            Assert.AreEqual(10f + 11f + 12f, _stats.Value(StatType.PhysicalAttack));
            Assert.IsFalse(_shop.TryBuy(_legendaries[0]), "unique: already owned");

            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(_shop.TryBuy(_statAnvil));
                Assert.IsTrue(_shop.AnvilOpen);
                Assert.AreEqual(RarityTier.Gold, _shop.AnvilRarity, "rarity drawn from the weights");
                Assert.AreEqual(3, _shop.AnvilChoices.Count);
                CollectionAssert.AllItemsAreUnique(_shop.AnvilChoices);
                foreach (ScriptableObject c in _shop.AnvilChoices) Assert.AreEqual(RarityTier.Gold, ((StatChoiceDefinition)c).Rarity);
                Assert.IsFalse(_shop.TryBuy(_elixir), "nothing else while an anvil is open");
                _shop.Choose(0);
                Assert.IsFalse(_shop.AnvilOpen);
            }
            Assert.AreEqual(200f, _stats.Value(StatType.MaxHealth), "two permanent stat bonuses");

            Assert.IsTrue(_shop.TryBuy(_legendaryAnvil));
            CollectionAssert.DoesNotContain(_shop.AnvilChoices, _legendaries[0], "owned items are not offered");

            ScriptableObject picked = _shop.AnvilChoices[0];   // random: never the item checked below
            _wallet.TrySpend(_wallet.Gold);
            _shop.Choose(0);
            ItemDefinition notOwned = _legendaries[5] != picked ? _legendaries[5] : _legendaries[4];
            Assert.AreEqual("Pas assez d'or", _shop.RefusalReason(notOwned), "gold is the only limit");
        }

        [Test]
        public void DuringAWave_ShopBrowsable_NothingBoughtOrSold() {
            Wave();
            Assert.IsFalse(_shop.CanBuy);
            Assert.AreEqual(ShopService.BetweenWavesOnly, _shop.RefusalReason(_legendaries[0]));
            Assert.IsFalse(_shop.TryBuy(_elixir));
            Assert.AreEqual(1000, _wallet.Gold);
            _inventory.TryAdd(_legendaries[0]);
            Assert.IsFalse(_shop.TrySell(0), "no selling during a wave");
            int published = 0;
            _bus.Subscribe<ShopStateEvent>(e => { if (e.CanBuy) published++; });
            Break();
            Assert.AreEqual(1, published, "the break opens the purchases");
        }

        [Test]
        public void Sell_GivesBackSeventyPercent_AndTakesTheStatsBack() {
            Break();
            _shop.TryBuy(_legendaries[3]);
            Assert.AreEqual(13f, _stats.Value(StatType.PhysicalAttack));
            int gold = _wallet.Gold;
            Assert.AreEqual(70, _legendaries[3].SellValue);
            Assert.IsTrue(_shop.TrySell(0));
            Assert.AreEqual(gold + 70, _wallet.Gold);
            Assert.AreEqual(0, _inventory.Items.Count);
            Assert.AreEqual(0f, _stats.Value(StatType.PhysicalAttack), "the item's stats are gone");
            Assert.IsTrue(_shop.TryBuy(_legendaries[3]), "and it can be bought again");
        }

        [Test]
        public void ItemAnvil_RerollsOncePerChoice() {
            Break();
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
        public void RoleAnvil_OffersOnlyItemsOfItsRole() {
            var mageAnvil = Make<AnvilDefinition>("MageAnvil", ("kind", AnvilKind.Item), ("itemTier", ItemTier.Legendary), ("cost", 80),
                                                  ("rerollsPerChoice", 1), ("role", ItemRole.Mage));
            Break();
            for (int n = 0; n < 5; n++) {
                Assert.IsTrue(_shop.TryBuy(mageAnvil));
                Assert.AreEqual(3, _shop.AnvilChoices.Count);
                foreach (ScriptableObject c in _shop.AnvilChoices) Assert.AreEqual(ItemRole.Mage, ((ItemDefinition)c).Roles, "mage items only, never boots");
                _shop.Reroll(0);
                Assert.AreEqual(ItemRole.Mage, ((ItemDefinition)_shop.AnvilChoices[0]).Roles, "rerolls too");
                _shop.Choose(0);
                _shop.TrySell(0);
            }
        }

        [Test]
        public void InventoryFull_RefusesItems() {
            for (int i = 0; i < Inventory.Capacity; i++) Assert.IsTrue(_inventory.TryAdd(_legendaries[i]));
            Break();
            Assert.IsFalse(_shop.TryBuy(_legendaries[7]));
            Assert.AreEqual("Inventaire plein", _shop.RefusalReason(_legendaryAnvil));
        }

        [Test]
        public void Elixir_LastsUntilTheEndOfTheNextWave() {
            Break();
            _shop.TryBuy(_elixir);
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

            _bus.Publish(new WaveRewardEvent(2, false, new[] { RewardKind.FreePrismaticAnvil, RewardKind.FreeStatAnvil }));
            Assert.IsTrue(_shop.AnvilOpen, "free prismatic anvil first");
            Assert.AreEqual(1000, _wallet.Gold, "free");
            _shop.Choose(0);
            Assert.AreEqual(ItemTier.Prismatic, _inventory.Items[0].Tier);
            Assert.IsTrue(_shop.AnvilOpen, "then the free stat anvil");
            Assert.AreEqual(0, closed);
            _shop.Choose(0);
            Assert.AreEqual(1, closed, "all rewards done");
            controller.Dispose();
        }
    }
}
