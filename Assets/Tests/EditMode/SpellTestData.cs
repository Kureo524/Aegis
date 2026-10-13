using Features.Spells;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode {
    /// <summary>Builds in-memory spell assets for the spell tests.</summary>
    internal static class SpellTestData {
        /// <param name="unlockLevels">Champion level per rank (empty: a single rank).</param>
        public static SpellDefinition Spell(string name, int[] unlockLevels = null, float[] baseDamage = null,
                                            float adRatio = 0f, float bonusAdRatio = 0f, float apRatio = 0f) {
            var s = ScriptableObject.CreateInstance<SpellDefinition>();
            s.name = name;
            var so = new SerializedObject(s);
            so.FindProperty("displayName").stringValue = name;
            Fill(so.FindProperty("rankUnlockLevels"), unlockLevels ?? new int[0]);
            Fill(so.FindProperty("baseDamage"), baseDamage ?? new[] { 0f });
            so.FindProperty("adRatio").floatValue = adRatio;
            so.FindProperty("bonusAdRatio").floatValue = bonusAdRatio;
            so.FindProperty("apRatio").floatValue = apRatio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return s;
        }

        private static void Fill(SerializedProperty list, int[] values) {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void Fill(SerializedProperty list, float[] values) {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }
}
