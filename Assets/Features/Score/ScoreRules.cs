using UnityEngine;

namespace Features.Score {
    /// <summary>
    /// How a run is scored (design doc §9.9): points per horde kill, per boss, per wave cleared, and
    /// a victory bonus with a bonus per stage life left. Read by <see cref="ScoreService"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Score Rules", fileName = "ScoreRules")]
    public class ScoreRules : ScriptableObject {
        [SerializeField, Min(0)] private int pointsPerKill = 10;
        [SerializeField, Min(0)] private int pointsPerBoss = 500;
        [SerializeField, Min(0)] private int pointsPerWave = 200;
        [SerializeField, Min(0)] private int victoryBonus = 1000;
        [Tooltip("Victory only: points per stage life left.")]
        [SerializeField, Min(0)] private int pointsPerLifeLeft = 250;

        public int PointsPerKill => pointsPerKill;
        public int PointsPerBoss => pointsPerBoss;
        public int PointsPerWave => pointsPerWave;
        public int VictoryBonus => victoryBonus;
        public int PointsPerLifeLeft => pointsPerLifeLeft;
    }
}
