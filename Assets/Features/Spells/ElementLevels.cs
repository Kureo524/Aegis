using System;
using System.Collections.Generic;
using Features.Progression;

namespace Features.Spells {
    /// <summary>
    /// Levels of the 3 elements, bought with skill points (design doc §7.8). Level 0 = locked; the
    /// first point unlocks it. The n-th element can only be unlocked from character level
    /// unlockLevels[n-1] (LoL style: 1 / 3 / 5). Raising an unlocked element is limited by maxLevel.
    /// </summary>
    public sealed class ElementLevels {
        private readonly int[] _levels = new int[ElementSelection.SlotCount];
        private readonly CharacterLevel _characterLevel;
        private readonly IReadOnlyList<int> _unlockLevels;
        private readonly int _maxLevel;

        public event Action Changed;

        public ElementLevels(CharacterLevel characterLevel, IReadOnlyList<int> unlockLevels, int maxLevel) {
            _characterLevel = characterLevel;
            _unlockLevels = unlockLevels;
            _maxLevel = maxLevel;
            _characterLevel.LevelChanged += _ => Changed?.Invoke();   // new unlocks may become possible
        }

        public int Level(int slot) => _levels[slot];
        public bool IsUnlocked(int slot) => _levels[slot] > 0;

        public int UnlockedCount {
            get {
                int n = 0;
                foreach (int l in _levels) if (l > 0) n++;
                return n;
            }
        }

        /// <summary>Character level needed to unlock one more element (0 = no more unlock).</summary>
        public int NextUnlockLevel => UnlockedCount < _unlockLevels.Count ? _unlockLevels[UnlockedCount] : 0;

        /// <summary>Elements the character level currently allows to have unlocked.</summary>
        public int AllowedUnlocks {
            get {
                int n = 0;
                foreach (int required in _unlockLevels) if (_characterLevel.Level >= required) n++;
                return n;
            }
        }

        public bool CanLevelUp(int slot) {
            if (slot < 0 || slot >= _levels.Length || _characterLevel.SkillPoints <= 0) return false;
            if (_levels[slot] >= _maxLevel) return false;
            return IsUnlocked(slot) || UnlockedCount < AllowedUnlocks;
        }

        public bool TryLevelUp(int slot) {
            if (!CanLevelUp(slot) || !_characterLevel.TrySpendSkillPoint()) return false;
            _levels[slot]++;
            Changed?.Invoke();
            return true;
        }
    }
}
