#if UNITY_EDITOR
using UnityEditor;

namespace EditorTools {
    /// <summary>Writes the rank tables of a SpellDefinition being created (design doc §7.2).</summary>
    internal static class SpellAssets {
        /// <summary>Single-rank damage: base + ratio of the attack stat of the spell's kind (Physical: AD, else AP).</summary>
        public static void SetDamage(SerializedObject so, float baseDamage, float ratio) {
            SetFloats(so.FindProperty("baseDamage"), baseDamage);
            bool physical = so.FindProperty("kind").enumValueIndex == (int)Features.Spells.SpellKind.Physical;
            so.FindProperty(physical ? "adRatio" : "apRatio").floatValue = ratio;
        }

        public static void SetCooldown(SerializedObject so, params float[] seconds) => SetFloats(so.FindProperty("cooldowns"), seconds);

        public static void SetFloats(SerializedProperty list, params float[] values) {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        public static void SetInts(SerializedProperty list, params int[] values) {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }
}
#endif
