using System;
using UnityEngine;

namespace Features.Spells {
    public enum SpellKind { Magic, Physical, Buff }

    [Serializable]
    public struct ElementCount {
        public ElementDefinition Element;
        [Min(1)] public int Count;
    }

    /// <summary>
    /// One spell = one recipe (number of stacks per element), design doc §7.2. Phase 5b only needs
    /// what identifies and displays it; targeting, cast time, power and the runtime Spell come with
    /// phase 5c.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Spells/Spell", fileName = "Spell_")]
    public class SpellDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private SpellKind kind = SpellKind.Magic;
        [SerializeField] private ElementCount[] recipe = Array.Empty<ElementCount>();

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public SpellKind Kind => kind;
        public ElementCount[] Recipe => recipe;

        public int TotalStacks {
            get {
                int total = 0;
                foreach (ElementCount e in recipe) total += e.Count;
                return total;
            }
        }
    }
}
