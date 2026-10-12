using System.Collections.Generic;
using UnityEngine;

namespace Features.Spells {
    /// <summary>All the spells of a character's elements: one entry per recipe, 19 for 3 elements
    /// (design doc §7.2, §7.5). Shared asset: lookups are built per character by SpellResolver.</summary>
    [CreateAssetMenu(menuName = "Game/Spells/Spell Book", fileName = "SpellBook_")]
    public class SpellBook : ScriptableObject {
        [SerializeField] private List<SpellDefinition> spells = new();

        public IReadOnlyList<SpellDefinition> Spells => spells;
    }
}
