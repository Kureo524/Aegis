using System.Collections;
using System.Linq;
using Features.Economy;
using Features.Enemies;
using Features.UI;
using Features.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>The MOBA-style HUD and shop (design doc §7.9, §9.3) in the real test scene.</summary>
    public class HudTests {
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static VisualElement HudRoot => Object.FindFirstObjectByType<PlayerHud>().GetComponent<UIDocument>().rootVisualElement;
        private static ItemDefinition Item(string id) => Game.Resolve<ShopCatalog>().Items.First(i => i.name == "Item_" + id);

        [UnityTest]
        public IEnumerator Hud_ShowsTheStatsPanel_TheSpellBar_AndTheItems() {
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return null;
            Assert.AreEqual(12, HudRoot.Query(className: "ae-stat").ToList().Count, "12 stats in the panel");
            Assert.AreEqual(6, HudRoot.Query(className: "ae-item").ToList().Count, "6 item slots");
            Game.Resolve<Inventory>().TryAdd(Item("SunsteelBlade"));
            yield return null;
            Assert.AreEqual(1, HudRoot.Query(className: "ae-item").ToList().Count(e => !e.ClassListContains("ae-slot--empty")));
            Assert.IsNotNull(HudRoot.Q(className: "ae-portrait__image").resolvedStyle.backgroundImage.sprite, "champion portrait");
        }

        [UnityTest]
        public IEnumerator Hud_BuffBar_ShowsAChargedSpellblade() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var player = Game.Resolve<EnemyDirector>().Player;
            ItemDefinition trinity = Item("TriadSigil");
            player.Passives.Add(trinity.Passive, trinity);
            player.Events.RaiseSpellCast(player.Definition.Abilities[0]);
            float end = Time.time + 2f;
            while (HudRoot.Q(className: "ae-buff--charged") == null && Time.time < end) yield return null;
            Assert.IsNotNull(HudRoot.Q(className: "ae-buff--charged"), "the charged spellblade shows on the buff bar");
        }

        [UnityTest]
        public IEnumerator Shop_OpensAnytime_RecommendedThenAll_BuyAndSellThroughTheBus() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var bus = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container.Resolve<Systems.EventBusses.IEventBus>();
            Game.Resolve<Wallet>().Add(1000);
            var panel = Object.FindFirstObjectByType<ShopPanel>();
            Assert.IsFalse(panel.IsOpen);
            bus.Publish(new Features.Events.ShopToggleRequestedEvent());   // HUD button / P
            yield return null;
            Assert.IsTrue(panel.IsOpen);
            VisualElement root = panel.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsTrue(panel.ShowsRecommended, "the champion's recommendations first");
            Assert.IsTrue(root.Query<Label>(className: "ae-shop__section").ToList().Any(l => l.text.StartsWith("Recommandés pour")));

            panel.ShowAllItems();
            Assert.Greater(root.Query(className: "ae-offer").ToList().Count, 50, "every offer: drawers, anvils and items");
            string[] drawers = root.Query<Button>(className: "ae-shop__drawer-title").ToList().Select(l => l.text).ToArray();
            CollectionAssert.AreEqual(new[] { "Élixirs & enclumes à stats", "Bottes", "Mes objets" }, drawers, "the three drawers on the left");
            Assert.AreEqual(6, root.Query(className: "ae-shop__anvils").Children<VisualElement>().ToList().Count,
                            "every item anvil: prismatic + one legendary per role");

            // A category: its own legendary anvil and only its items
            panel.SetCategory(ItemRole.Mage);
            Assert.AreEqual(ItemRole.Mage, panel.Category);
            Assert.AreEqual(2, root.Query(className: "ae-shop__anvils").Children<VisualElement>().ToList().Count, "prismatic + the mage's anvil");
            var shop = Game.Resolve<ShopCatalog>();
            int mages = shop.Items.Count(i => i.Tier == ItemTier.Legendary && i.UniqueGroup != "Bottes" && (i.Roles & ItemRole.Mage) != 0);
            Assert.AreEqual(mages, root.Q(className: "ae-shop__main").Query(className: "ae-offer").ToList().Count - 2, "the mage items in the grid");
            Assert.Greater(root.Query(className: "ae-iconbutton").ToList().Count, 15, "categories and stat filters as icons");
            panel.ShowAllItems();

            ItemDefinition edge = Item("SunsteelBlade");
            panel.Select(edge);
            Assert.AreSame(edge, panel.Selected);
            Assert.IsTrue(root.Query<Label>(className: "ae-detail__name").ToList().Any(l => l.text == edge.DisplayName), "details of the selection");
            Button buy = root.Query<Button>().ToList().First(b => b.text == "Acheter");
            Assert.IsTrue(buy.enabledSelf, "affordable, between waves: Buy is enabled");
            // What the Buy button, a double click or a right click on the icon send
            bus.Publish(new Features.Events.ShopBuyRequestedEvent(edge));
            yield return null;
            Assert.IsTrue(Game.Resolve<Inventory>().Contains(edge));

            panel.SelectSlot(0);
            Button sell = root.Query<Button>().ToList().First(b => b.text.StartsWith("Vendre"));
            Assert.IsTrue(sell.enabledSelf);
            StringAssert.Contains(edge.SellValue.ToString(), sell.text);
            bus.Publish(new Features.Events.ShopSellRequestedEvent(0));
            yield return null;
            Assert.IsFalse(Game.Resolve<Inventory>().Contains(edge), "sold");

            bus.Publish(new Features.Events.ShopCloseRequestedEvent());   // Escape / ✕
            Assert.IsFalse(panel.IsOpen);
        }

        private static void Press(VisualElement target, int button = 0) {
            var e = new Event { type = EventType.MouseDown, button = button, mousePosition = target.worldBound.center, clickCount = 1 };
            using var down = PointerDownEvent.GetPooled(e);
            down.target = target;
            target.SendEvent(down);
        }

        private static void Click(VisualElement target) {
            var e = new Event { type = EventType.MouseUp, button = 0, mousePosition = target.worldBound.center, clickCount = 1 };
            using var click = ClickEvent.GetPooled(e);
            click.target = target;
            target.SendEvent(click);
        }

        [UnityTest]
        public IEnumerator Shop_DoubleClickOnACell_Buys_OneClickSelects() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var bus = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container.Resolve<Systems.EventBusses.IEventBus>();
            Game.Resolve<Wallet>().Add(1000);
            var panel = Object.FindFirstObjectByType<ShopPanel>();
            bus.Publish(new Features.Events.ShopToggleRequestedEvent());
            yield return null;
            panel.ShowAllItems();
            yield return null;
            VisualElement root = panel.GetComponent<UIDocument>().rootVisualElement;
            ItemDefinition item = Item("SunsteelBlade");
            VisualElement Cell() => root.Query(className: "ae-offer").ToList()
                                        .First(c => c.Q(className: "ae-offer__icon").resolvedStyle.backgroundImage.sprite == item.Icon);
            Press(Cell());
            Assert.AreSame(item, panel.Selected, "one click selects");
            Assert.IsFalse(Game.Resolve<Inventory>().Contains(item));
            Press(Cell());
            Assert.IsTrue(Game.Resolve<Inventory>().Contains(item), "the second click buys");
        }

        [UnityTest]
        public IEnumerator AugmentDraft_TheWholeCardPicks_RerollUnderIt() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var augments = Game.Resolve<Features.Augments.AugmentController>();
            augments.OpenDraft(RarityTier.Gold, false);
            yield return null;
            VisualElement root = Object.FindFirstObjectByType<AugmentPanel>().GetComponent<UIDocument>().rootVisualElement;
            Assert.IsFalse(root.Query<Button>().ToList().Any(b => b.text == "Choisir"), "no Choose button");
            VisualElement[] columns = root.Query(className: "ae-card-column").ToList().ToArray();
            Assert.AreEqual(3, columns.Length);
            Button reroll = columns[1].Q<Button>(className: "ae-card-column__button");
            Assert.IsNotNull(reroll, "Reroll under the card");
            Assert.IsNull(columns[1].Q(className: "ae-card").Q<Button>(), "nothing to press on the card itself");
            Features.Augments.AugmentDefinition second = augments.Choices[1];
            Click(columns[1].Q(className: "ae-card"));
            CollectionAssert.Contains(augments.Owned, second, "a click on the card picks it");
            Assert.AreEqual(Features.Events.AugmentDraftPhase.Closed, augments.Phase);
        }
    }
}
