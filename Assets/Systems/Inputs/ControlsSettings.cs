using UnityEngine;

namespace Systems.Inputs {
    /// <summary>
    /// Default control settings (design doc §14.5: GameOptions). The player's own choices
    /// (Features.Options.UserOptions) are saved in JSON on top of these.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/Controls Settings", fileName = "ControlsSettings")]
    public class ControlsSettings : ScriptableObject {
        [SerializeField] private KeyboardLayout keyboardLayout = KeyboardLayout.Azerty;
        [Tooltip("Default cast mode of R / F (player option).")]
        [SerializeField] private CastMode castMode = CastMode.QuickWithIndicator;

        public KeyboardLayout KeyboardLayout => keyboardLayout;
        public CastMode CastMode => castMode;
    }
}
