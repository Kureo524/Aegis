using System;
using System.Collections.Generic;
using Features.Progression;

namespace Features.Spells {
    /// <summary>Slot indices of the player's spells (design doc §7.1): Q W E R, then D F.</summary>
    public static class AbilitySlots {
        public const int AbilityCount = 4;
        public const int Ultimate = 3;
        public const int SummonerD = 4;
        public const int SummonerF = 5;
        public const int Count = 6;

        public static bool IsAbility(int slot) => slot >= 0 && slot < AbilityCount;
    }

    /// <summary>
    /// Ranks of a champion's 4 abilities (design doc §7.8), MOBA style: every level gives a skill
    /// point (CharacterLevel), spent with Ctrl + key or the HUD "+". A rank needs the champion level
    /// written in the ability (SpellDefinition.UnlockLevel: Q W E 1/3/5/7/9, R 6/11/16). Rank 0 =
    /// not learned, the ability cannot be cast. Pure C#, owned by AbilityCaster.
    /// </summary>
    public sealed class AbilityRanks {
        private readonly IReadOnlyList<SpellDefinition> _abilities;
        private readonly CharacterLevel _level;
        private readonly int[] _ranks;

        public event Action Changed;

        public AbilityRanks(IReadOnlyList<SpellDefinition> abilities, CharacterLevel level) {
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _ranks = new int[abilities.Count];
        }

        public int Count => _ranks.Length;
        public int Rank(int slot) => slot >= 0 && slot < _ranks.Length ? _ranks[slot] : 0;

        public bool CanRankUp(int slot) {
            if (slot < 0 || slot >= _ranks.Length || _abilities[slot] == null || _level.SkillPoints <= 0) return false;
            SpellDefinition ability = _abilities[slot];
            int next = _ranks[slot] + 1;
            return next <= ability.MaxRank && _level.Level >= ability.UnlockLevel(next);
        }

        public bool TryRankUp(int slot) {
            if (!CanRankUp(slot) || !_level.TrySpendSkillPoint()) return false;
            _ranks[slot]++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Why <paramref name="slot"/> cannot rank up (player feedback), null if it can.</summary>
        public string RefusalReason(int slot) {
            if (slot < 0 || slot >= _ranks.Length || _abilities[slot] == null) return "Aucun sort";
            SpellDefinition ability = _abilities[slot];
            if (_ranks[slot] >= ability.MaxRank) return "Rang maximum";
            if (_level.SkillPoints <= 0) return "Aucun point de compétence";
            int needed = ability.UnlockLevel(_ranks[slot] + 1);
            return _level.Level < needed ? $"Disponible au niveau {needed}" : null;
        }
    }
}
