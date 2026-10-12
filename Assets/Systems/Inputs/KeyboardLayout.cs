using System.Collections.Generic;

namespace Systems.Inputs {
    /// <summary>
    /// Physical keyboard layout of the player. Letter bindings in the action maps are written for
    /// the letter they stand for (Q = attack-move…) at its QWERTY position, and remapped at startup
    /// to wherever that letter sits on the player's layout.
    ///
    /// Why not Input System display-name bindings (<c>&lt;Keyboard&gt;/#(a)</c>)? The Linux player
    /// does not report the OS layout (it falls back to QWERTY names) and the headless Linux Editor
    /// crashes resolving them. An explicit setting works everywhere and is the base of the rebind menu.
    /// </summary>
    public enum KeyboardLayout { Qwerty, Azerty }

    public static class KeyboardLayouts {
        // Letters whose physical key differs from QWERTY: letter → QWERTY control name of its key
        private static readonly Dictionary<char, string> Azerty = new() {
            ['a'] = "q", ['q'] = "a",
            ['z'] = "w", ['w'] = "z",
            ['m'] = "semicolon",
        };

        /// <summary>Control name of the key that types <paramref name="letter"/> on <paramref name="layout"/>.</summary>
        public static string PhysicalKey(KeyboardLayout layout, char letter) {
            if (layout == KeyboardLayout.Azerty && Azerty.TryGetValue(letter, out string key)) return key;
            return letter.ToString();
        }
    }
}
