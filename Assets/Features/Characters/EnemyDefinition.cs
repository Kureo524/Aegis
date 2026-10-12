using Features.Spells;
using UnityEngine;

namespace Features.Characters {
    /// <summary>How an enemy finds its way to the player (design doc §9.6).</summary>
    public enum EnemyNavigation {
        /// <summary>Hordes: follow the shared flow field towards the player (no pathfinding per enemy).</summary>
        FlowField,
        /// <summary>Elites and bosses: own NavMesh path.</summary>
        NavMesh,
    }

    /// <summary>
    /// A character that rewards the player when killed (design doc §9.2, §9.6): XP and gold, how it
    /// moves, and its TFT-style mana: gained per second, per attack and per hit taken; when full,
    /// the special spell is cast and the mana goes back to StartMana. The mana max is the MaxMana
    /// stat of its StatBlock. Wave scaling comes with phase 7.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : CharacterDefinition {
        [Header("Rewards")]
        [SerializeField] private int xpReward = 50;
        [SerializeField] private int goldReward = 10;

        [Header("AI")]
        [SerializeField] private EnemyNavigation navigation = EnemyNavigation.FlowField;

        [Header("Special spell (TFT mana)")]
        [Tooltip("Optional: null = no special spell, mana unused.")]
        [SerializeField] private SpellDefinition specialSpell;
        [SerializeField, Min(0f)] private float startMana;
        [SerializeField, Min(0f)] private float manaPerSecond = 5f;
        [SerializeField, Min(0f)] private float manaPerAttack = 10f;
        [SerializeField, Min(0f)] private float manaPerHitTaken = 2f;

        public int XpReward => xpReward;
        public int GoldReward => goldReward;
        public EnemyNavigation Navigation => navigation;
        public SpellDefinition SpecialSpell => specialSpell;
        public float StartMana => startMana;
        public float ManaPerSecond => manaPerSecond;
        public float ManaPerAttack => manaPerAttack;
        public float ManaPerHitTaken => manaPerHitTaken;
    }
}
