#if UNITY_EDITOR
using System.IO;
using Containers;
using Features.Stats;
using Features.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools {
    /// <summary>
    /// The LoL-style UI (design doc §7.9), called by <see cref="ProjectSetup"/>: the UiTheme asset
    /// (stats of the HUD panel and their icons) registered by the root scope, and the Aegis theme
    /// (Assets/UI/AegisTheme.tss = Unity's default runtime theme + AegisTheme.uss) on the shared
    /// PanelSettings, so every document gets the styles. Idempotent.
    /// </summary>
    public static class UiSetup {
        private const string ThemePath = "Assets/Data/Settings/UiTheme.asset";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/UI/Stats";
        private const string StyleSheetPath = "Assets/UI/AegisTheme.tss";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

        private static readonly StatType[] PanelStats = {
            StatType.PhysicalAttack, StatType.MagicAttack, StatType.Armor, StatType.MagicResist,
            StatType.AttackSpeed, StatType.AbilityHaste, StatType.CritChance, StatType.MoveSpeed,
            StatType.Lifesteal, StatType.ArmorPenPercent, StatType.MagicPenPercent, StatType.Tenacity,
        };

        public static void Run() {
            foreach (string png in Directory.GetFiles(IconsDir, "*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null) {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
                var so = new SerializedObject(theme);
                SerializedProperty list = so.FindProperty("stats");
                list.arraySize = PanelStats.Length;
                for (int i = 0; i < PanelStats.Length; i++) {
                    SerializedProperty e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Stat").enumValueIndex = (int)PanelStats[i];
                    e.FindPropertyRelative("Icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/Stat_{PanelStats[i]}.png");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ProjectSetup.RootPrefabPath);
            var scopeSo = new SerializedObject(root.GetComponent<RootLifetimeScope>());
            if (scopeSo.FindProperty("uiTheme").objectReferenceValue != theme) {
                scopeSo.FindProperty("uiTheme").objectReferenceValue = theme;
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ProjectSetup.RootPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(root);

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(StyleSheetPath);
            if (panel != null && tss != null && panel.themeStyleSheet != tss) {
                panel.themeStyleSheet = tss;
                EditorUtility.SetDirty(panel);
            }
        }
    }
}
#endif
