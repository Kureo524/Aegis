using System;

namespace Features.Spells {
    /// <summary>
    /// Elements stacked for the next spell (design doc §7.2). The recipe is the number of stacks per
    /// slot, so the order of the key presses never matters: Key = Σ stacks[i] × 4^i.
    /// </summary>
    public sealed class ElementSelection {
        public const int SlotCount = 3;

        private readonly int[] _stacks = new int[SlotCount];
        private readonly int _maxTotal;
        private readonly int _maxPerElement;

        public int Total { get; private set; }
        public int Key { get; private set; }
        public bool IsEmpty => Total == 0;

        public event Action Changed;

        public ElementSelection(int maxTotal, int maxPerElement) {
            _maxTotal = maxTotal;
            _maxPerElement = maxPerElement;
        }

        public int Stacks(int slot) => _stacks[slot];

        public bool CanAdd(int slot) =>
            slot >= 0 && slot < SlotCount && Total < _maxTotal && _stacks[slot] < _maxPerElement;

        public bool Add(int slot) {
            if (!CanAdd(slot)) return false;
            _stacks[slot]++;
            Total++;
            Key += Pow4(slot);
            Changed?.Invoke();
            return true;
        }

        public void Clear() {
            if (Total == 0) return;
            Array.Clear(_stacks, 0, SlotCount);
            Total = 0;
            Key = 0;
            Changed?.Invoke();
        }

        /// <summary>Key of a recipe given as stacks per slot.</summary>
        public static int KeyOf(int stacks0, int stacks1, int stacks2) => stacks0 + stacks1 * 4 + stacks2 * 16;

        private static int Pow4(int slot) => 1 << (2 * slot);
    }
}
