#if UNITY_EDITOR
using Features.Economy;
using Features.Enemies;
using Features.Run;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Elites of the horde (design doc §9.6), called by <see cref="ProjectSetup"/> after the items:
    /// the items they may carry and their bonuses (HordeSettings), how often they come and how
    /// many items they carry in each wave of the test stage, and the break between waves (60 s).
    /// Rewritten on every run (the code is the reference). Values are placeholders, checked with
    /// the balance bot.
    /// </summary>
    public static class EliteSetup {
        private const string ItemsDir = "Assets/Data/Economy/Items";
        public const float Intermission = 60f;

        // Items whose passive works for an enemy: thorns, on-hit damage, slows, regeneration, shields, auras
        private static readonly string[] Pool = {
            "BrambleMail", "TitanHeart", "GlacierPlate", "HungryAxe", "GraveFang", "TrollhideVest", "WardingHelm",
            "DawnHammer", "ThunderAxe", "EmberPlate", "JuggernautPlate", "SealedBulwark", "MourningShroud", "IronCleaver",
        };

        // (elite chance, items) per wave, then per bonus wave
        private static readonly (float chance, int items)[] Waves = { (0f, 1), (0f, 1), (0.06f, 1), (0.07f, 1), (0.04f, 1) };   // the boss wave: fewer
        private static readonly (float chance, int items)[] BonusWaves = { (0.12f, 2), (0.15f, 3) };

        public static void Run(HordeSettings horde, StageDefinition stage) {
            if (horde != null) {
                var so = new SerializedObject(horde);
                SerializedProperty list = so.FindProperty("eliteItems");
                var items = new System.Collections.Generic.List<ItemDefinition>();
                foreach (string id in Pool) {
                    var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsDir}/Item_{id}.asset");
                    if (item != null) items.Add(item);
                }
                list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                so.FindProperty("eliteHealthMultiplier").floatValue = 1.7f;
                so.FindProperty("eliteRewardMultiplier").floatValue = 4f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (stage == null) return;
            var sso = new SerializedObject(stage);
            sso.FindProperty("intermission").floatValue = Intermission;
            sso.ApplyModifiedPropertiesWithoutUndo();
            Write(stage.Waves, Waves);
            Write(stage.BonusWaves, BonusWaves);
        }

        private static void Write(System.Collections.Generic.IReadOnlyList<WaveDefinition> waves, (float chance, int items)[] values) {
            for (int i = 0; i < waves.Count && i < values.Length; i++) {
                if (waves[i] == null) continue;
                var so = new SerializedObject(waves[i]);
                so.FindProperty("eliteChance").floatValue = values[i].chance;
                so.FindProperty("eliteItems").intValue = values[i].items;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
