using UnityEngine;

namespace Systems.Inputs {
    /// <summary>
    /// Default control settings (design doc §14.5: GameOptions). The player's own choices
    /// (UserOptions, rebind menu) will be saved in JSON on top of these.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/Controls Settings", fileName = "ControlsSettings")]
    public class ControlsSettings : ScriptableObject {
        [SerializeField] private KeyboardLayout keyboardLayout = KeyboardLayout.Azerty;

        public KeyboardLayout KeyboardLayout => keyboardLayout;
    }
}
