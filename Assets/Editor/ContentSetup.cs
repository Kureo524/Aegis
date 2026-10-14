#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Features.Augments;
using Features.Combat;
using Features.Economy;
using Features.Passives;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;
using P = EditorTools.PassiveSetup.P;

namespace EditorTools {
    /// <summary>
    /// Phase 9b content (design doc §5.7, §9.3, §9.4), called by <see cref="ProjectSetup"/> after the
    /// boss:
    /// - statuses on the enemy spells; tenacity for the brute and Grokk;
    /// - 7 augments, built from the generic passive bricks and added to the augment catalog (the
    ///   9b items were replaced by ItemsSetup's).
    /// Everything is only filled when missing (a spell whose statuses were set by hand is kept), so
    /// running it again changes nothing. Values are placeholders, names are original.
    /// </summary>
    public static class ContentSetup {
        private const string ItemIconsDir = "Assets/GraphicAssets/Sprites/Items";

        private static StatBonus B(StatType s, float v, ModifierType t = ModifierType.Flat) => new(s, t, v);

        private static StatusApplication Stun(float s) => new() { Kind = StatusKind.Stun, Duration = s };
        private static StatusApplication Slow(float power, float s) => new() { Kind = StatusKind.Slow, Power = power, Duration = s };
        private static StatusApplication Burn(float dps, float ratio, float s, int stacks) =>
            new() { Kind = StatusKind.Burn, Power = dps, ScalingStat = StatType.MagicAttack, Ratio = ratio, Duration = s, MaxStacks = stacks };
        private static StatusApplication Bleed(float dps, float ratio, float s, int stacks) =>
            new() { Kind = StatusKind.Bleed, Power = dps, ScalingStat = StatType.PhysicalAttack, Ratio = ratio, Duration = s, MaxStacks = stacks };

        private static PassiveTrigger OnHitStatus(PassiveEvent on, int everyN, StatusApplication status) =>
            new() { On = on, EveryN = everyN, Chance = 1f, Effect = PassiveEffectKind.ApplyStatus, Status = status };

        private static readonly Dictionary<string, StatusApplication[]> EnemySpellStatuses = new() {
            ["Assets/Data/Spells/Enemies/Spell_BruteSlam.asset"] = new[] { Slow(0.4f, 1.5f) },
            ["Assets/Data/Spells/Enemies/Spell_GrokkSlam.asset"] = new[] { Stun(0.75f) },
            ["Assets/Data/Spells/Enemies/Spell_GrokkNova.asset"] = new[] { Slow(0.35f, 2f) },
        };

        private static readonly (string path, float tenacity)[] Tenacities = {
            ("Assets/Data/Characters/Enemies/StatBlock_Brute.asset", 0.3f),
            ("Assets/Data/Characters/Bosses/StatBlock_GoblinKing.asset", 0.5f),
        };

        // ---- Augments ----
        private static readonly (RarityTier rarity, P passive)[] Augments = {
            (RarityTier.Gold, new P { Id = "StaticShock", Name = "Choc statique", Description = "Un coup de sort sur six étourdit la cible 0,75 s.",
                Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 6, Stun(0.75f)) } }),
            (RarityTier.Gold, new P { Id = "ArcaneBarrier", Name = "Barrière arcanique", Description = "Après un sort : bouclier de 40 (+25 % puissance magique) pendant 3 s. Recharge 4 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 1, Chance = 1f, Cooldown = 4f, Effect = PassiveEffectKind.Shield,
                                                        Amount = 40f, AmountStat = StatType.MagicAttack, AmountRatio = 0.25f, Duration = 3f } } }),
            (RarityTier.Prismatic, new P { Id = "Phoenix", Name = "Phénix", Description = "Touché sous 30 % de PV : rend 30 % de tes PV max. Recharge 90 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, HealthBelow = 0.3f, Cooldown = 90f,
                                                        Effect = PassiveEffectKind.Heal, AmountStat = StatType.MaxHealth, AmountRatio = 0.3f } } }),
        };

        public static void Run() {
            ConfigureSpells();
            ConfigureEnemies();
            AddAugments();
        }

        private static void ConfigureSpells() {
            foreach ((string path, StatusApplication[] statuses) in EnemySpellStatuses) {
                var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
                if (spell != null && spell.Statuses.Length == 0) Set(spell, "statuses", statuses);
            }
        }

        private static void ConfigureEnemies() {
            foreach ((string path, float tenacity) in Tenacities) {
                var block = AssetDatabase.LoadAssetAtPath<StatBlock>(path);
                if (block == null || block.TryGet(StatType.Tenacity, out _)) continue;
                var so = new SerializedObject(block);
                SerializedProperty entries = so.FindProperty("entries");
                int i = entries.arraySize++;
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Type").enumValueIndex = (int)StatType.Tenacity;
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Value").floatValue = tenacity;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AddAugments() {
            var catalog = AssetDatabase.LoadAssetAtPath<AugmentCatalog>(PassiveSetup.AugmentCatalogPath);
            if (catalog == null) return;
            var augments = new List<Object>(catalog.Augments);
            foreach ((RarityTier rarity, P passive) in Augments) {
                string path = $"Assets/Data/Augments/Augment_{passive.Id}.asset";
                var augment = AssetDatabase.LoadAssetAtPath<AugmentDefinition>(path);
                if (augment == null) {
                    augment = CreateAsset<AugmentDefinition>(path);
                    var so = new SerializedObject(augment);
                    so.FindProperty("rarity").enumValueIndex = (int)rarity;
                    so.FindProperty("passive").objectReferenceValue = PassiveSetup.Passive(passive, $"Augment_{passive.Id}");
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!augments.Contains(augment)) augments.Add(augment);
            }
            if (augments.Count == catalog.Augments.Count) return;
            var cso = new SerializedObject(catalog);
            Fill(cso.FindProperty("augments"), augments);
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // Struct arrays are simplest to write through reflection, like PassiveSetup
        private static void Set(Object target, string field, object value) {
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }

        private static void Fill(SerializedProperty list, List<Object> values) {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
