using System;
using Features.Characters;
using Features.Economy;
using UnityEngine;

namespace Features.Enemies {
    /// <summary>
    /// A boss (design doc §9.6): an enemy with a pattern in phases and up to 3 items, like TFT
    /// (their stats and passives go through the same systems as the player's).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Boss Definition", fileName = "Boss_")]
    public class BossDefinition : EnemyDefinition {
        [Header("Boss")]
        [SerializeField] private string title = "Boss";
        [SerializeField] private PatternDefinition pattern;
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

        public string Title => title;
        public PatternDefinition Pattern => pattern;
        public ItemDefinition[] Items => items;

#if UNITY_EDITOR
        private void OnValidate() {
            if (items.Length > 3) Debug.LogWarning($"[BossDefinition] {name}: 3 items at most.", this);
        }
#endif
    }
}
