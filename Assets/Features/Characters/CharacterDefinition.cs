using Features.Combat;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using UnityEngine;

namespace Features.Characters {
    /// <summary>
    /// Everything that makes one character different from another (design doc §8): one generic
    /// prefab + one of these = one character. Read-only at runtime.
    /// Elements, passive, signature ability, spell book and AI are added with their phases.
    /// Which components drive the character (player input, AI) is the prefab variant's job, not this.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character Definition", fileName = "Char_")]
    public class CharacterDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction = Faction.Enemy;

        [Header("Body")]
        [Tooltip("Facing right, pivot at the feet.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("x = width (and depth), y = height of the physics body, in world units.")]
        [SerializeField] private Vector2 bodySize = new(0.8f, 1.8f);

        [Header("Stats")]
        [SerializeField] private StatBlock baseStats;
        [SerializeField] private StatGrowth growthPerLevel;

        [Header("Combat")]
        [Tooltip("Optional: null = never attacks (training dummy…).")]
        [SerializeField] private BasicAttackDefinition basicAttack;

        [Header("Elements (player characters, design doc §7.8)")]
        [Tooltip("Exactly 3, in A / Z / E key order.")]
        [SerializeField] private ElementDefinition[] elements = new ElementDefinition[3];
        [Tooltip("Character level needed to unlock the 1st / 2nd / 3rd element.")]
        [SerializeField] private int[] elementUnlockLevels = { 1, 3, 5 };
        [SerializeField] private SpellBook spellBook;

        public string DisplayName => displayName;
        public Faction Faction => faction;
        public Sprite Sprite => sprite;
        public Vector2 BodySize => bodySize;
        public StatBlock BaseStats => baseStats;
        public StatGrowth GrowthPerLevel => growthPerLevel;
        public BasicAttackDefinition BasicAttack => basicAttack;
        public System.Collections.Generic.IReadOnlyList<ElementDefinition> Elements => elements;
        public System.Collections.Generic.IReadOnlyList<int> ElementUnlockLevels => elementUnlockLevels;
        public SpellBook SpellBook => spellBook;
        public bool HasElements => spellBook != null && elements.Length == ElementSelection.SlotCount;

#if UNITY_EDITOR
        private void OnValidate() {
            if (sprite == null) Debug.LogWarning($"[CharacterDefinition] {name}: no sprite.", this);
            if (bodySize.x <= 0f || bodySize.y <= 0f) Debug.LogWarning($"[CharacterDefinition] {name}: body size must be positive.", this);
            if (baseStats == null) Debug.LogWarning($"[CharacterDefinition] {name}: no base stats.", this);
            if (basicAttack == null && faction == Faction.Player)
                Debug.LogWarning($"[CharacterDefinition] {name}: the player needs a basic attack.", this);
            if (spellBook != null) ValidateSpellBook();
        }

        // Every recipe of the 3 elements must have exactly one spell (§7.2).
        private void ValidateSpellBook() {
            if (elements.Length != ElementSelection.SlotCount || System.Array.Exists(elements, e => e == null)) {
                Debug.LogWarning($"[CharacterDefinition] {name}: needs exactly 3 elements.", this);
                return;
            }
            var resolver = new SpellResolver(spellBook, elements);
            foreach (string problem in resolver.Problems) Debug.LogWarning($"[CharacterDefinition] {name}: {problem}", this);
            int missing = resolver.MissingKeys(3, 3).Count;
            if (missing > 0) Debug.LogWarning($"[CharacterDefinition] {name}: {missing} recipe(s) without a spell in {spellBook.name}.", this);
        }
#endif
    }
}
