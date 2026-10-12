using UnityEngine;

namespace Features.Spells {
    /// <summary>One element (Fire, Electricity, Wind…), design doc §7.2. Read-only at runtime.</summary>
    [CreateAssetMenu(menuName = "Game/Spells/Element", fileName = "Elem_")]
    public class ElementDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color color = Color.white;
        [Tooltip("Base mana cost of ONE stack of this element (§7.3).")]
        [SerializeField] private float manaCost = 10f;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Color Color => color;
        public float ManaCost => manaCost;
    }
}
