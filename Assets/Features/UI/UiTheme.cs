using System;
using Features.Stats;
using UnityEngine;

namespace Features.UI {
    /// <summary>
    /// UI data shared by the screens (design doc §7.9): the stat icons of the stats panel and
    /// tooltips. The look itself is the USS theme (Assets/UI/AegisTheme.uss, applied to every
    /// document through the PanelSettings theme). Registered in the root scope.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject {
        [Serializable]
        public struct StatIcon {
            public StatType Stat;
            public Sprite Icon;
        }

        [Tooltip("Stats of the HUD stats panel, in display order, with their icon.")]
        [SerializeField] private StatIcon[] stats = Array.Empty<StatIcon>();

        public StatIcon[] Stats => stats;

        public Sprite Icon(StatType stat) {
            foreach (StatIcon s in stats) if (s.Stat == stat) return s.Icon;
            return null;
        }
    }
}
