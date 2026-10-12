using UnityEngine;

namespace Features.Flow {
    /// <summary>Which scenes the game flow loads (design doc §14.5). Scene names must be in the
    /// Build Profile scene list.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Game Flow Settings", fileName = "GameFlowSettings")]
    public class GameFlowSettings : ScriptableObject {
        [SerializeField] private string menuScene = "Menu";
        [SerializeField] private string gameScene = "TestScene";

        public string MenuScene => menuScene;
        public string GameScene => gameScene;
    }
}
