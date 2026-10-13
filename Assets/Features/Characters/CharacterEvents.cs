using System;
using Features.Combat;
using Features.Spells;

namespace Features.Characters {
    /// <summary>
    /// Per-character event hub (design doc §8.1, §14.3): what passives, items and augments react to.
    /// Plain C# events on each character, off the global bus (hundreds of enemies).
    /// </summary>
    public sealed class CharacterEvents {
        /// <summary>A basic attack of this character landed (target, damage taken).</summary>
        public event Action<Character, float> BasicAttackHit;
        /// <summary>This character dealt damage (target, damage taken, the hit).</summary>
        public event Action<Character, float, DamageInfo> DamageDealt;
        /// <summary>This character took damage (amount).</summary>
        public event Action<float> DamageTaken;
        /// <summary>This character killed someone (victim).</summary>
        public event Action<Character> Kill;
        /// <summary>A spell of this character hit someone (target, damage taken).</summary>
        public event Action<Character, float> SpellHit;
        /// <summary>This character cast a spell (after its cast time).</summary>
        public event Action<SpellDefinition> SpellCast;
        /// <summary>A hit asks to reduce this character's ability cooldowns (seconds, Ezreal Q).</summary>
        public event Action<float> CooldownRefund;
        /// <summary>This character's level went up (new level).</summary>
        public event Action<int> LevelUp;

        public void RaiseBasicAttackHit(Character target, float damage) => BasicAttackHit?.Invoke(target, damage);
        public void RaiseDamageDealt(Character target, float damage, in DamageInfo info) => DamageDealt?.Invoke(target, damage, info);
        public void RaiseDamageTaken(float damage) => DamageTaken?.Invoke(damage);
        public void RaiseKill(Character victim) => Kill?.Invoke(victim);
        public void RaiseSpellHit(Character target, float damage) => SpellHit?.Invoke(target, damage);
        public void RaiseSpellCast(SpellDefinition spell) => SpellCast?.Invoke(spell);
        public void RaiseCooldownRefund(float seconds) => CooldownRefund?.Invoke(seconds);
        public void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
    }
}
