using Features.Spells;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode {
    /// <summary>Builds in-memory element / spell / settings assets for the spell tests.</summary>
    internal static class SpellTestData {
        public static ElementDefinition Element(string name, float cost) {
            var e = ScriptableObject.CreateInstance<ElementDefinition>();
            e.name = name;
            var so = new SerializedObject(e);
            so.FindProperty("manaCost").floatValue = cost;
            so.ApplyModifiedPropertiesWithoutUndo();
            return e;
        }

        public static SpellDefinition Spell(string name, params (ElementDefinition element, int count)[] recipe) {
            var s = ScriptableObject.CreateInstance<SpellDefinition>();
            s.name = name;
            var so = new SerializedObject(s);
            so.FindProperty("displayName").stringValue = name;
            SerializedProperty list = so.FindProperty("recipe");
            list.arraySize = recipe.Length;
            for (int i = 0; i < recipe.Length; i++) {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Element").objectReferenceValue = recipe[i].element;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Count").intValue = recipe[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return s;
        }

        public static SpellBook Book(params SpellDefinition[] spells) {
            var b = ScriptableObject.CreateInstance<SpellBook>();
            var so = new SerializedObject(b);
            SerializedProperty list = so.FindProperty("spells");
            list.arraySize = spells.Length;
            for (int i = 0; i < spells.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return b;
        }

        // Defaults of the asset: 3 stacks max, curve 1 / 1.5 / 2.25, element level max 5
        public static SpellSettings Settings() => ScriptableObject.CreateInstance<SpellSettings>();
    }
}
