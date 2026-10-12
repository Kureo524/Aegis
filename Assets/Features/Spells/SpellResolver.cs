using System.Collections.Generic;
using UnityEngine;

namespace Features.Spells {
    /// <summary>
    /// Recipe key → spell for one character (design doc §7.5): the shared SpellBook converted with
    /// the character's own A/Z/E element order.
    /// </summary>
    public sealed class SpellResolver {
        private readonly Dictionary<int, SpellDefinition> _byKey = new();

        /// <summary>Recipes found twice or using an element the character does not have (editor checks).</summary>
        public List<string> Problems { get; } = new();

        public SpellResolver(SpellBook book, IReadOnlyList<ElementDefinition> slots) {
            if (book == null) return;
            foreach (SpellDefinition spell in book.Spells) {
                if (spell == null) continue;
                if (!TryKey(spell, slots, out int key)) {
                    Problems.Add($"{spell.name}: uses an element that is not in the character's slots");
                    continue;
                }
                if (_byKey.ContainsKey(key)) {
                    Problems.Add($"{spell.name}: same recipe as {_byKey[key].name}");
                    continue;
                }
                _byKey[key] = spell;
            }
        }

        public int Count => _byKey.Count;

        public SpellDefinition Resolve(int key) => _byKey.TryGetValue(key, out SpellDefinition spell) ? spell : null;

        /// <summary>Keys of every possible recipe (1..maxTotal stacks) that has no spell.</summary>
        public List<int> MissingKeys(int maxTotal, int maxPerElement) {
            var missing = new List<int>();
            for (int a = 0; a <= maxPerElement; a++)
            for (int b = 0; b <= maxPerElement; b++)
            for (int c = 0; c <= maxPerElement; c++) {
                int total = a + b + c;
                if (total == 0 || total > maxTotal) continue;
                int key = ElementSelection.KeyOf(a, b, c);
                if (!_byKey.ContainsKey(key)) missing.Add(key);
            }
            return missing;
        }

        private static bool TryKey(SpellDefinition spell, IReadOnlyList<ElementDefinition> slots, out int key) {
            key = 0;
            foreach (ElementCount entry in spell.Recipe) {
                int slot = -1;
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i] == entry.Element) slot = i;
                if (slot < 0 || entry.Count <= 0) return false;
                key += entry.Count * (1 << (2 * slot));
            }
            return key > 0;
        }

        /// <summary>Total mana cost of the selection (§7.3): Σ stacks × element cost × curve × (1 − reduction).</summary>
        public static float ManaCost(ElementSelection selection, IReadOnlyList<ElementDefinition> slots,
                                     SpellSettings settings, float manaCostReduction) {
            if (selection.IsEmpty) return 0f;
            float baseCost = 0f;
            for (int i = 0; i < ElementSelection.SlotCount && i < slots.Count; i++) {
                if (slots[i] != null) baseCost += selection.Stacks(i) * slots[i].ManaCost;
            }
            return baseCost * settings.CostMultiplier(selection.Total) * (1f - Mathf.Clamp01(manaCostReduction));
        }
    }
}
