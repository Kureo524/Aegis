using Features.Economy;
using Features.Passives;
using UnityEngine;

namespace Features.Augments {
    /// <summary>
    /// An augment (design doc §9.4): a rarity and a passive, which carries its name, description,
    /// icon and every effect (stats, capabilities, triggers, stacks…). No code per augment.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Augment", fileName = "Augment_")]
    public class AugmentDefinition : ScriptableObject {
        [SerializeField] private RarityTier rarity;
        [SerializeField] private PassiveDefinition passive;

        public RarityTier Rarity => rarity;
        public PassiveDefinition Passive => passive;
        public string DisplayName => passive != null ? passive.DisplayName : name;
        public string Description => passive != null ? passive.Description : "";
        public Sprite Icon => passive != null ? passive.Icon : null;
    }
}
