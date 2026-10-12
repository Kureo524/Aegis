using UnityEngine;

namespace Features.Characters {
    /// <summary>A character that rewards the player when killed (design doc §9.2, §9.6).
    /// Wave scaling, TFT-style mana and special spell come with phase 6.</summary>
    [CreateAssetMenu(menuName = "Game/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : CharacterDefinition {
        [Header("Rewards")]
        [SerializeField] private int xpReward = 50;
        [SerializeField] private int goldReward = 10;

        public int XpReward => xpReward;
        public int GoldReward => goldReward;
    }
}
