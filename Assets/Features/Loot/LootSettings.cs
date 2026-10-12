using System;
using Features.Spells;
using UnityEngine;

namespace Features.Loot {
    /// <summary>Ground loot tuning (design doc §9.5). Registered by the GameLifetimeScope.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Loot Settings", fileName = "LootSettings")]
    public class LootSettings : ScriptableObject {
        [Tooltip("Picked up when the player comes this close (contact).")]
        [SerializeField, Min(0.1f)] private float pickupRadius = 1.2f;
        [Tooltip("Seconds before an untouched loot vanishes.")]
        [SerializeField, Min(1f)] private float lifetime = 30f;
        [SerializeField] private Sprite statAnvilSprite;
        [SerializeField] private Sprite summonerSpellSprite;
        [Tooltip("Single-use spells a summoner loot can give (one at random).")]
        [SerializeField] private SpellDefinition[] summonerSpells = Array.Empty<SpellDefinition>();

        public float PickupRadius => pickupRadius;
        public float Lifetime => lifetime;
        public Sprite StatAnvilSprite => statAnvilSprite;
        public Sprite SummonerSpellSprite => summonerSpellSprite;
        public SpellDefinition[] SummonerSpells => summonerSpells;
    }
}
