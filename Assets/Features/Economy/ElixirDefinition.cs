using System;
using UnityEngine;

namespace Features.Economy {
    /// <summary>
    /// Consumable of the shops (design doc §9.3): its bonuses start at once and last until the end
    /// of the wave that follows the purchase. One per shop.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Economy/Elixir", fileName = "Elixir_")]
    public class ElixirDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int cost = 40;
        [SerializeField] private StatBonus[] stats = Array.Empty<StatBonus>();

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Cost => cost;
        public StatBonus[] Stats => stats;
    }
}
