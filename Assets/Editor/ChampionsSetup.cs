#if UNITY_EDITOR
using System.Linq;
using Containers;
using Features.Characters;
using Features.UI.Panels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// The playable champions (design doc §7.10), called by <see cref="ProjectSetup"/>: the roster
    /// (rewritten on every run, first = default) in the root scope, and the champion select screen
    /// of the menu scene.
    /// </summary>
    public static class ChampionsSetup {
        public const string RosterPath = "Assets/Data/Settings/ChampionRoster.asset";

        public static ChampionRoster Run(string rootPrefabPath, string menuScenePath, params CharacterDefinition[] champions) {
            var roster = AssetDatabase.LoadAssetAtPath<ChampionRoster>(RosterPath);
            if (roster == null) {
                roster = ScriptableObject.CreateInstance<ChampionRoster>();
                AssetDatabase.CreateAsset(roster, RosterPath);
            }
            var so = new SerializedObject(roster);
            CharacterDefinition[] list = champions.Where(c => c != null).ToArray();
            SerializedProperty array = so.FindProperty("champions");
            array.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roster);

            GameObject root = PrefabUtility.LoadPrefabContents(rootPrefabPath);
            var scopeSo = new SerializedObject(root.GetComponent<RootLifetimeScope>());
            if (scopeSo.FindProperty("championRoster").objectReferenceValue != roster) {
                scopeSo.FindProperty("championRoster").objectReferenceValue = roster;
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, rootPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(root);

            var scene = EditorSceneManager.OpenScene(menuScenePath, OpenSceneMode.Single);
            var menu = Object.FindFirstObjectByType<MenuLifetimeScope>();
            if (menu != null && menu.championSelect == null) {
                var panel = Object.FindFirstObjectByType<ChampionSelectPanel>();
                if (panel == null) panel = TestSceneSetup.CreateOverlayDocument<ChampionSelectPanel>("ChampionSelectPanel");
                menu.championSelect = panel;
                EditorUtility.SetDirty(menu);
                EditorSceneManager.SaveScene(scene);
            }
            return roster;
        }
    }
}
#endif
