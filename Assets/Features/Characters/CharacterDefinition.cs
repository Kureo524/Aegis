using Features.Combat;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using UnityEngine;
using UnityEngine.Serialization;

namespace Features.Characters {
    /// <summary>
    /// Everything that makes one character different from another (design doc §8): one generic
    /// prefab + one of these = one character. Read-only at runtime.
    /// Champions carry 4 abilities (Q W E R) and 2 summoner spells (D F); enemies a special spell
    /// (EnemyDefinition).
    /// Which components drive the character (player input, AI) is the prefab variant's job, not this.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character Definition", fileName = "Char_")]
    public class CharacterDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction = Faction.Enemy;

        [Header("Body")]
        [Tooltip("Facing right, pivot at the feet.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Animated look (idle, run, attack, casts, death). Without it the sprite above stands still.")]
        [SerializeField] private SpriteAnimationSet animations;
        [Tooltip("HUD portrait; the sprite when empty.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("x = width (and depth), y = height of the physics body, in world units.")]
        [SerializeField] private Vector2 bodySize = new(0.8f, 1.8f);

        [Header("Stats")]
        [SerializeField] private StatBlock baseStats;
        [SerializeField] private StatGrowth growthPerLevel;

        [Header("Combat")]
        [Tooltip("Optional: null = never attacks (training dummy…).")]
        [SerializeField] private BasicAttackDefinition basicAttack;

        [Header("Abilities (champions, design doc §7)")]
        [Tooltip("Q, W, E, R: on A, Z, E, R (physical Q W E R keys). Empty for characters without abilities.")]
        [SerializeField] private SpellDefinition[] abilities = new SpellDefinition[0];
        [Tooltip("Summoner spell on D (optional; a picked-up summoner spell fills D when it is empty, §9.5).")]
        [SerializeField] private SpellDefinition summonerD;
        [Tooltip("Summoner spell on F (the blink, Bond éclair).")]
        [FormerlySerializedAs("signatureAbility")]
        [SerializeField] private SpellDefinition summonerF;

        [Header("Passive (design doc §8.1)")]
        [Tooltip("Optional: the character's own passive.")]
        [SerializeField] private Passives.PassiveDefinition passive;

        public string DisplayName => displayName;
        public Faction Faction => faction;
        public Sprite Sprite => sprite;
        public SpriteAnimationSet Animations => animations;
        public Sprite Portrait => portrait != null ? portrait : sprite;
        public Vector2 BodySize => bodySize;
        public StatBlock BaseStats => baseStats;
        public StatGrowth GrowthPerLevel => growthPerLevel;
        public BasicAttackDefinition BasicAttack => basicAttack;
        public System.Collections.Generic.IReadOnlyList<SpellDefinition> Abilities => abilities;
        public SpellDefinition SummonerD => summonerD;
        public SpellDefinition SummonerF => summonerF;
        public Passives.PassiveDefinition Passive => passive;
        public bool HasAbilities => abilities.Length == AbilitySlots.AbilityCount && System.Array.TrueForAll(abilities, a => a != null);

#if UNITY_EDITOR
        private void OnValidate() {
            if (sprite == null) Debug.LogWarning($"[CharacterDefinition] {name}: no sprite.", this);
            if (bodySize.x <= 0f || bodySize.y <= 0f) Debug.LogWarning($"[CharacterDefinition] {name}: body size must be positive.", this);
            if (baseStats == null) Debug.LogWarning($"[CharacterDefinition] {name}: no base stats.", this);
            if (basicAttack == null && faction == Faction.Player)
                Debug.LogWarning($"[CharacterDefinition] {name}: the player needs a basic attack.", this);
            if (abilities.Length != 0 && !HasAbilities)
                Debug.LogWarning($"[CharacterDefinition] {name}: abilities must be exactly Q, W, E and R.", this);
        }
#endif
    }
}
