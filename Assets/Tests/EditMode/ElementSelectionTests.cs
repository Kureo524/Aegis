using Features.Spells;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class ElementSelectionTests {
        [Test]
        public void OrderOfPresses_DoesNotChangeTheRecipe() {
            var a = new ElementSelection(3, 3);
            a.Add(0); a.Add(1);
            var b = new ElementSelection(3, 3);
            b.Add(1); b.Add(0);

            Assert.AreEqual(a.Key, b.Key);
        }

        [Test]
        public void DifferentCounts_AreDifferentRecipes() {
            var twoFire = new ElementSelection(3, 3);
            twoFire.Add(0); twoFire.Add(0); twoFire.Add(1);
            var twoWater = new ElementSelection(3, 3);
            twoWater.Add(0); twoWater.Add(1); twoWater.Add(1);

            Assert.AreNotEqual(twoFire.Key, twoWater.Key);
            Assert.AreEqual(ElementSelection.KeyOf(2, 1, 0), twoFire.Key);
        }

        [Test]
        public void TotalIsCapped() {
            var s = new ElementSelection(3, 3);
            Assert.IsTrue(s.Add(0));
            Assert.IsTrue(s.Add(1));
            Assert.IsTrue(s.Add(2));
            Assert.IsFalse(s.Add(0), "a spell is 3 elements at most");
            Assert.AreEqual(3, s.Total);
        }

        [Test]
        public void StacksPerElementAreCapped() {
            var s = new ElementSelection(5, 2);
            s.Add(0); s.Add(0);
            Assert.IsFalse(s.Add(0));
            Assert.AreEqual(2, s.Stacks(0));
        }

        [Test]
        public void Clear_EmptiesAndNotifiesOnce() {
            var s = new ElementSelection(3, 3);
            s.Add(2);
            int changes = 0;
            s.Changed += () => changes++;

            s.Clear();
            s.Clear();

            Assert.IsTrue(s.IsEmpty);
            Assert.AreEqual(0, s.Key);
            Assert.AreEqual(1, changes);
        }
    }
}
