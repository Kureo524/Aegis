using UnityEngine;

namespace Features.Flow {
    /// <summary>Which scenes the game flow loads (design doc §14.5). Scene names must be in the
    /// Build Profile scene list.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Game Flow Settings", fileName = "GameFlowSettings")]
    public class GameFlowSettings : ScriptableObject {
        [SerializeField] private string menuScene = "Menu";
        [SerializeField] private string gameScene = "TestScene";
        [Tooltip("Until the stage lives and the game over (phase 7): the dead player comes back at the spawn after this delay.")]
        [SerializeField, Min(0f)] private float playerRespawnDelay = 3f;

        public string MenuScene => menuScene;
        public string GameScene => gameScene;
        public float PlayerRespawnDelay => playerRespawnDelay;
    }
}
