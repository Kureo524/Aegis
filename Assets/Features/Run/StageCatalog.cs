using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Run {
    /// <summary>
    /// The stages offered on the champion select screen (design doc §9.1), in display order. The
    /// first one is the default: the short run (2 waves, ~3 minutes).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Stage Catalog", fileName = "StageCatalog")]
    public class StageCatalog : ScriptableObject {
        [SerializeField] private StageDefinition[] stages = Array.Empty<StageDefinition>();

        public IReadOnlyList<StageDefinition> Stages => stages;
        public StageDefinition Default => stages.Length > 0 ? stages[0] : null;
    }
}
