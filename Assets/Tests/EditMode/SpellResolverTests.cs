using System.Collections.Generic;
using Features.Spells;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class SpellResolverTests {
        private ElementDefinition _fire, _water, _earth;
        private ElementDefinition[] _slots;
        private SpellSettings _settings;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp() {
            // Design doc §7.3 example: Fire 10, Water 10, Earth 12
            _fire = Track(SpellTestData.Element("Fire", 10f));
            _water = Track(SpellTestData.Element("Water", 10f));
            _earth = Track(SpellTestData.Element("Earth", 12f));
            _slots = new[] { _fire, _water, _earth };
            _settings = Track(SpellTestData.Settings());
        }

        [TearDown]
        public void TearDown() {
            foreach (Object o in _assets) Object.DestroyImmediate(o);
            _assets.Clear();
        }

        private T Track<T>(T asset) where T : Object {
            _assets.Add(asset);
            return asset;
        }

        private float Cost(params int[] presses) {
            var selection = new ElementSelection(3, 3);
            foreach (int slot in presses) selection.Add(slot);
            return SpellResolver.ManaCost(selection, _slots, _settings, manaCostReduction: 0f);
        }

        [Test]
        public void ManaCost_MatchesTheDesignDocExamples() {
            Assert.AreEqual(10f, Cost(0), 1e-3f, "Fire x1");
            Assert.AreEqual(30f, Cost(0, 1), 1e-3f, "Fire + Water");
            Assert.AreEqual(67.5f, Cost(0, 0, 0), 1e-3f, "Fire x3");
            Assert.AreEqual(72f, Cost(0, 1, 2), 1e-3f, "Fire + Water + Earth");
        }

        [Test]
        public void ManaCostReduction_IsApplied() {
            var selection = new ElementSelection(3, 3);
            selection.Add(0);
            Assert.AreEqual(7.5f, SpellResolver.ManaCost(selection, _slots, _settings, 0.25f), 1e-3f);
        }

        [Test]
        public void Resolve_UsesTheCharacterSlotOrder() {
            SpellDefinition steam = Track(SpellTestData.Spell("Steam", (_fire, 1), (_water, 1)));
            SpellBook book = Track(SpellTestData.Book(steam));

            var resolver = new SpellResolver(book, _slots);
            Assert.AreEqual(steam, resolver.Resolve(ElementSelection.KeyOf(1, 1, 0)));

            // Same book, other character with Water on A and Fire on E: keys follow its own slots
            var swapped = new SpellResolver(book, new[] { _water, _earth, _fire });
            Assert.AreEqual(steam, swapped.Resolve(ElementSelection.KeyOf(1, 0, 1)));
            Assert.IsNull(swapped.Resolve(ElementSelection.KeyOf(1, 1, 0)));
        }

        [Test]
        public void MissingAndDuplicateRecipes_AreReported() {
            SpellDefinition a = Track(SpellTestData.Spell("A", (_fire, 1)));
            SpellDefinition b = Track(SpellTestData.Spell("B", (_fire, 1)));
            SpellBook book = Track(SpellTestData.Book(a, b));

            var resolver = new SpellResolver(book, _slots);

            Assert.AreEqual(1, resolver.Problems.Count, "duplicate recipe");
            Assert.AreEqual(18, resolver.MissingKeys(3, 3).Count, "19 recipes for 3 elements, 1 covered");
        }

        [Test]
        public void SpellWithForeignElement_IsReported() {
            ElementDefinition ice = Track(SpellTestData.Element("Ice", 5f));
            SpellBook book = Track(SpellTestData.Book(Track(SpellTestData.Spell("Frost", (ice, 1)))));

            var resolver = new SpellResolver(book, _slots);
            Assert.AreEqual(1, resolver.Problems.Count);
            Assert.AreEqual(0, resolver.Count);
        }
    }
}
