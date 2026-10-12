using Features.Economy;
using Features.Enemies;
using Features.Events;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class FlowFieldTests {
        // 10 x 10 cells of 1 unit, corner at the origin
        private static FlowField Grid(System.Func<int, int, bool> wall) =>
            new(Vector3.zero, 10, 10, 1f, p => !wall(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)));

        [Test]
        public void OpenGround_PointsStraightAtTheTarget() {
            FlowField field = Grid((x, z) => false);
            Assert.IsTrue(field.Compute(new Vector3(8.5f, 0f, 5.5f)));
            Assert.IsTrue(field.TryGetDirection(new Vector3(1.5f, 0f, 5.5f), out Vector3 dir));
            Assert.Greater(dir.x, 0.99f, "same row: straight to the right");
            Assert.IsFalse(field.TryGetDirection(new Vector3(8.4f, 0f, 5.4f), out _), "target cell: go straight to the target");
        }

        [Test]
        public void Wall_IsWalkedAround() {
            // Wall on column x = 5 from z = 0 to z = 7: the way is through the top (z >= 8)
            FlowField field = Grid((x, z) => x == 5 && z <= 7);
            field.Compute(new Vector3(8.5f, 0f, 1.5f));
            Vector3 position = new(1.5f, 0f, 1.5f);
            for (int step = 0; step < 40; step++) {
                if (!field.TryGetDirection(position, out Vector3 dir)) break;
                position += dir * 0.5f;
                Assert.IsTrue(field.IsWalkable(field.CellOf(position)), $"stays out of the wall (step {step}, {position})");
            }
            Assert.AreEqual(field.TargetCell, field.CellOf(position), "reached the target cell");
        }

        [Test]
        public void Diagonals_DoNotCutWallCorners() {
            // Two walls touching by a corner: (4,4) and (5,5). From (4,5) to (5,4) the diagonal is closed.
            FlowField field = Grid((x, z) => (x == 4 && z == 4) || (x == 5 && z == 5));
            field.Compute(new Vector3(5.5f, 0f, 4.5f));
            Assert.IsTrue(field.TryGetDirection(new Vector3(4.5f, 0f, 5.5f), out Vector3 dir));
            Assert.IsFalse(Mathf.Abs(dir.x) > 0.3f && Mathf.Abs(dir.z) > 0.3f, $"no diagonal through the corner ({dir})");
        }

        [Test]
        public void EnclosedCell_IsUnreachable() {
            // Cell (1,1) closed by a ring of walls
            FlowField field = Grid((x, z) => x <= 2 && z <= 2 && !(x == 1 && z == 1));
            field.Compute(new Vector3(8.5f, 0f, 8.5f));
            Assert.IsFalse(field.IsReachable(field.CellOf(new Vector3(1.5f, 0f, 1.5f))));
            Assert.IsTrue(field.IsReachable(field.CellOf(new Vector3(5.5f, 0f, 1.5f))));
            Assert.AreEqual(-1, field.CellOf(new Vector3(-1f, 0f, 3f)), "off the grid");
        }

        [Test]
        public void Wallet_CreditsKills_AndRefusesOverspending() {
            var bus = new EventBus();
            int published = 0;
            bus.Subscribe<GoldChangedEvent>(_ => published++);
            var wallet = new Wallet(bus);

            bus.Publish(new EnemyKilledEvent(10, 7, Vector3.zero));
            Assert.AreEqual(7, wallet.Gold);
            Assert.IsFalse(wallet.TrySpend(8));
            Assert.IsTrue(wallet.TrySpend(5));
            Assert.AreEqual(2, wallet.Gold);
            Assert.AreEqual(2, published);
            wallet.Dispose();
        }
    }
}
