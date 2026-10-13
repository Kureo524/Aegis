#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Containers;
using Features.Characters;
using Features.Meta;
using Features.Stats;
using Features.UI.Panels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Meta-progression content (design doc §9.7), called by <see cref="ProjectSetup"/>: MetaSettings
    /// (currency, rewards) in the root scope and Ezreal's tree (stats, starting gold, augment
    /// rerolls, an extra life, a starting augment), plus the Progression screen of the menu scene.
    /// Created once; values are placeholders.
    /// </summary>
    public static class MetaSetup {
        private const string Dir = "Assets/Data/Meta";
        public const string SettingsPath = "Assets/Data/Settings/MetaSettings.asset";
        private const string StatIcons = "Assets/GraphicAssets/Sprites/UI/Stats";

        private sealed class N {
            public string Id, Name, Description, Icon;
            public int[] Costs;
            public MetaEffect[] Effects;
            public string[] Requires = new string[0];
            public Vector2Int At;
        }

        private static MetaEffect Stat(StatType s, float v, ModifierType t = ModifierType.Flat) => new() { Kind = MetaEffectKind.Stat, Stat = s, Type = t, Value = v };
        private static MetaEffect Of(MetaEffectKind k, float v) => new() { Kind = k, Value = v };

        private static readonly N[] Ezreal = {
            new() { Id = "Training", Name = "Entraînement", Description = "Ezreal frappe plus fort.", Icon = $"{StatIcons}/Stat_PhysicalAttack.png",
                    Costs = new[] { 15, 25, 35, 50, 70 }, Effects = new[] { Stat(StatType.PhysicalAttack, 3) }, At = new(0, 0) },
            new() { Id = "Endurance", Name = "Endurance", Description = "Plus de PV.", Icon = $"{StatIcons}/Stat_MaxHealth.png",
                    Costs = new[] { 15, 25, 35, 50, 70 }, Effects = new[] { Stat(StatType.MaxHealth, 40) }, At = new(0, 1) },
            new() { Id = "Purse", Name = "Bourse", Description = "De l'or dès le début de la partie.", Icon = "Assets/GraphicAssets/Sprites/Items/Anvil_Stats.png",
                    Costs = new[] { 20, 35, 55 }, Effects = new[] { Of(MetaEffectKind.StartingGold, 60) }, At = new(0, 2) },
            new() { Id = "Precision", Name = "Précision", Description = "Plus de coups critiques.", Icon = $"{StatIcons}/Stat_CritChance.png",
                    Costs = new[] { 25, 40, 60, 80, 110 }, Effects = new[] { Stat(StatType.CritChance, 0.03f) }, Requires = new[] { "Training" }, At = new(1, 0) },
            new() { Id = "Focus", Name = "Concentration", Description = "Les sorts reviennent plus vite.", Icon = $"{StatIcons}/Stat_AbilityHaste.png",
                    Costs = new[] { 25, 40, 60, 80, 110 }, Effects = new[] { Stat(StatType.AbilityHaste, 4) }, Requires = new[] { "Training" }, At = new(1, 1) },
            new() { Id = "Plating", Name = "Cuirasse", Description = "Armure et résistance magique.", Icon = $"{StatIcons}/Stat_Armor.png",
                    Costs = new[] { 30, 50, 75 }, Effects = new[] { Stat(StatType.Armor, 4), Stat(StatType.MagicResist, 4) }, Requires = new[] { "Endurance" }, At = new(1, 2) },
            new() { Id = "Rerolls", Name = "Second regard", Description = "Plus de relances d'augment pour la partie.", Icon = "Assets/GraphicAssets/Sprites/Augments/Augment_Restart.png",
                    Costs = new[] { 40, 80 }, Effects = new[] { Of(MetaEffectKind.AugmentRerolls, 1) }, Requires = new[] { "Purse" }, At = new(1, 3) },
            new() { Id = "ManaFlow", Name = "Flux de mana", Description = "Régénération de mana.", Icon = $"{StatIcons}/Stat_MaxMana.png",
                    Costs = new[] { 40, 65, 95 }, Effects = new[] { Stat(StatType.ManaRegen, 0.3f) }, Requires = new[] { "Focus" }, At = new(2, 1) },
            new() { Id = "Vigor", Name = "Vigueur", Description = "Régénération de PV.", Icon = $"{StatIcons}/Stat_Lifesteal.png",
                    Costs = new[] { 40, 65, 95 }, Effects = new[] { Stat(StatType.HealthRegen, 0.4f) }, Requires = new[] { "Plating" }, At = new(2, 2) },
            new() { Id = "Legacy", Name = "Héritage", Description = "Une vie de stage en plus.", Icon = "Assets/GraphicAssets/Sprites/Augments/Augment_Phoenix.png",
                    Costs = new[] { 150 }, Effects = new[] { Of(MetaEffectKind.ExtraLives, 1) }, Requires = new[] { "Vigor" }, At = new(3, 2) },
            new() { Id = "Mastery", Name = "Maîtrise", Description = "Un pourcentage de toute l'attaque physique.", Icon = "Assets/GraphicAssets/Sprites/Items/Item_InfinityEdge.png",
                    Costs = new[] { 80, 140, 220 }, Effects = new[] { Stat(StatType.PhysicalAttack, 0.04f, ModifierType.PercentAdd) }, Requires = new[] { "Precision" }, At = new(3, 0) },
            new() { Id = "Swiftness", Name = "Pas léger", Description = "Vitesse de déplacement.", Icon = $"{StatIcons}/Stat_MoveSpeed.png",
                    Costs = new[] { 60, 100 }, Effects = new[] { Stat(StatType.MoveSpeed, 0.03f, ModifierType.PercentAdd) }, Requires = new[] { "ManaFlow" }, At = new(3, 1) },
            new() { Id = "Favor", Name = "Faveur", Description = "Un augment au début de chaque partie : Argent, puis Or, puis Prismatique.", Icon = "Assets/GraphicAssets/Sprites/Augments/Augment_AcceleratingSorcery.png",
                    Costs = new[] { 90, 180, 320 }, Effects = new[] { Of(MetaEffectKind.StartingAugment, 1) }, Requires = new[] { "Rerolls" }, At = new(2, 3) },
        };

        public static MetaSettings Run(string rootPrefabPath, string menuScenePath) {
            var settings = AssetDatabase.LoadAssetAtPath<MetaSettings>(SettingsPath);
            if (settings == null) {
                Directory.CreateDirectory(Dir);
                var nodes = new Dictionary<string, MetaNodeDefinition>();
                foreach (N n in Ezreal) {
                    var node = ScriptableObject.CreateInstance<MetaNodeDefinition>();
                    AssetDatabase.CreateAsset(node, $"{Dir}/Meta_Ezreal_{n.Id}.asset");
                    nodes[n.Id] = node;
                }
                foreach (N n in Ezreal) {
                    var so = new SerializedObject(nodes[n.Id]);
                    so.FindProperty("displayName").stringValue = n.Name;
                    so.FindProperty("description").stringValue = n.Description;
                    so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(n.Icon);
                    SpellAssets.SetInts(so.FindProperty("costs"), n.Costs);
                    SerializedProperty pre = so.FindProperty("prerequisites");
                    pre.arraySize = n.Requires.Length;
                    for (int i = 0; i < n.Requires.Length; i++) pre.GetArrayElementAtIndex(i).objectReferenceValue = nodes[n.Requires[i]];
                    so.FindProperty("position").vector2IntValue = n.At;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    typeof(MetaNodeDefinition).GetField("effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                                              .SetValue(nodes[n.Id], n.Effects);
                    EditorUtility.SetDirty(nodes[n.Id]);
                }

                var tree = ScriptableObject.CreateInstance<MetaTreeDefinition>();
                AssetDatabase.CreateAsset(tree, $"{Dir}/MetaTree_Ezreal.asset");
                var tso = new SerializedObject(tree);
                tso.FindProperty("displayName").stringValue = "Ezreal";
                tso.FindProperty("character").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.TestHeroPath);
                SerializedProperty list = tso.FindProperty("nodes");
                list.arraySize = Ezreal.Length;
                for (int i = 0; i < Ezreal.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = nodes[Ezreal[i].Id];
                tso.ApplyModifiedPropertiesWithoutUndo();

                settings = ScriptableObject.CreateInstance<MetaSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                var sso = new SerializedObject(settings);
                SerializedProperty trees = sso.FindProperty("trees");
                trees.arraySize = 1;
                trees.GetArrayElementAtIndex(0).objectReferenceValue = tree;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject root = PrefabUtility.LoadPrefabContents(rootPrefabPath);
            var scopeSo = new SerializedObject(root.GetComponent<RootLifetimeScope>());
            if (scopeSo.FindProperty("metaSettings").objectReferenceValue != settings) {
                scopeSo.FindProperty("metaSettings").objectReferenceValue = settings;
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, rootPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(root);

            // The Progression screen above the main menu
            var scene = EditorSceneManager.OpenScene(menuScenePath, OpenSceneMode.Single);
            var menu = Object.FindFirstObjectByType<MenuLifetimeScope>();
            if (menu != null && menu.metaPanel == null) {
                var panel = Object.FindFirstObjectByType<MetaPanel>();
                if (panel == null) panel = TestSceneSetup.CreateOverlayDocument<MetaPanel>("MetaPanel");
                menu.metaPanel = panel;
                EditorUtility.SetDirty(menu);
                EditorSceneManager.SaveScene(scene);
            }
            return settings;
        }
    }
}
#endif
