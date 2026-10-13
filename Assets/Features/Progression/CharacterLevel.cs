using System;
using Features.Stats;

namespace Features.Progression {
    /// <summary>
    /// Level, XP and skill points of one character (design doc §5.5, §7.8, §9.2). Pure C#, owned by
    /// <see cref="Features.Characters.Character"/>.
    ///
    /// - Every level from 1 to <see cref="ExperienceCurve.LastSkillPointLevel"/> gives one skill
    ///   point (level 1 included); levels above give an overcharge point instead.
    /// - Stat growth is applied as modifiers whose source is this object, replaced on every level
    ///   (never stacked by mistake).
    /// </summary>
    public sealed class CharacterLevel {
        private readonly StatController _stats;
        private readonly StatGrowth _growth;
        private readonly ExperienceCurve _curve;

        public int Level { get; private set; } = 1;
        /// <summary>XP accumulated inside the current level.</summary>
        public int Xp { get; private set; }
        public int XpToNext => _curve.XpToNext(Level);
        public bool IsMaxLevel => Level >= _curve.MaxLevel;
        public int SkillPoints { get; private set; } = 1;
        public int OverchargePoints { get; private set; }

        public event Action<int> LevelChanged;
        public event Action SkillPointsChanged;
        public event Action XpChanged;

        public CharacterLevel(StatController stats, StatGrowth growth, ExperienceCurve curve) {
            _stats = stats;
            _growth = growth;
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            ApplyGrowth();
        }

        public void AddXp(int amount) {
            if (amount <= 0 || IsMaxLevel) return;

            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNext) {
                Xp -= XpToNext;
                LevelUp();
            }
            if (IsMaxLevel) Xp = 0;

            XpChanged?.Invoke();
        }

        /// <summary>Flat growth of <paramref name="stat"/> at the current level (part of the base value, LoL style).</summary>
        public float FlatGrowth(StatType stat) {
            if (_growth == null) return 0f;
            float total = 0f;
            foreach (StatGrowthEntry entry in _growth.Entries)
                if (entry.Stat == stat && entry.Type == ModifierType.Flat) total += _growth.BonusAt(entry, Level);
            return total;
        }

        /// <summary>Consumes one skill point (spent on an ability rank, §7.8).</summary>
        public bool TrySpendSkillPoint() {
            if (SkillPoints <= 0) return false;
            SkillPoints--;
            SkillPointsChanged?.Invoke();
            return true;
        }

        private void LevelUp() {
            Level++;
            if (Level <= _curve.LastSkillPointLevel) SkillPoints++;
            else OverchargePoints++;
            SkillPointsChanged?.Invoke();

            ApplyGrowth();
            LevelChanged?.Invoke(Level);
        }

        private void ApplyGrowth() {
            if (_growth == null) return;
            foreach (StatGrowthEntry entry in _growth.Entries) {
                Stat stat = _stats.Get(entry.Stat);
                if (stat == null) continue;
                stat.RemoveAllFromSource(this);
                float bonus = _growth.BonusAt(entry, Level);
                if (bonus != 0f) stat.AddModifier(new StatModifier(bonus, entry.Type, this));
            }
        }
    }
}
