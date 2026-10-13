#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Features.Augments;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Loot;
using Features.Passives;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Phase 8 content (design doc §8.1, §9.4, §9.5), called by <see cref="ProjectSetup"/>: 12 augments (4 Silver, 5 Gold, 3 Prismatic), two item passives, the summoner
    /// spells and the loot settings, with their icons. Everything is a PassiveDefinition built from
    /// the generic bricks: no code per augment. Created once, never overwritten; values are
    /// placeholders, names are original.
    /// </summary>
    public static class PassiveSetup {
        public const string Dir = "Assets/Data/Passives";
        public const string AugmentCatalogPath = "Assets/Data/Augments/AugmentCatalog.asset";
        public const string LootSettingsPath = "Assets/Data/Settings/LootSettings.asset";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Augments";

        private static StatBonus B(StatType s, float v, ModifierType t = ModifierType.Flat) => new(s, t, v);

        // A passive is written as a small description of its bricks
        internal sealed class P {
            public string Id, Name, Description;
            public StatBonus[] Stats = System.Array.Empty<StatBonus>();
            public Capability[] Capabilities = System.Array.Empty<Capability>();
            public PassiveTrigger[] Triggers = System.Array.Empty<PassiveTrigger>();
            public StackSettings Stacks;
            public NearbyEnemiesBonus Nearby;
        }

        private static readonly (RarityTier rarity, P passive)[] Augments = {
            (RarityTier.Silver, new P { Id = "StrongArm", Name = "Gros bras", Description = "+12 % vitesse d'attaque, +8 attaque physique.",
                Stats = new[] { B(StatType.AttackSpeed, 0.12f, ModifierType.PercentAdd), B(StatType.PhysicalAttack, 8) } }),
            (RarityTier.Silver, new P { Id = "Stoneskin", Name = "Peau de pierre", Description = "+25 armure, +25 résistance magique.",
                Stats = new[] { B(StatType.Armor, 25), B(StatType.MagicResist, 25) } }),
            (RarityTier.Silver, new P { Id = "Focus", Name = "Concentration", Description = "+15 accélération.",
                Stats = new[] { B(StatType.AbilityHaste, 15) } }),
            (RarityTier.Silver, new P { Id = "HotBlood", Name = "Sang chaud", Description = "+180 PV, +2 régénération de PV.",
                Stats = new[] { B(StatType.MaxHealth, 180), B(StatType.HealthRegen, 2) } }),
            (RarityTier.Gold, new P { Id = "JeweledGauntlet", Name = "Gantelet serti", Description = "Tes sorts peuvent infliger des coups critiques. +15 % chances de critique.",
                Stats = new[] { B(StatType.CritChance, 0.15f) }, Capabilities = new[] { Capability.SpellCrit } }),
            (RarityTier.Gold, new P { Id = "SoulCollector", Name = "Collectionneur d'âmes", Description = "Chaque kill donne +1 puissance magique et +0,5 attaque physique, pour toute la partie.",
                Stacks = new StackSettings { Source = StackSource.PerKill, Reset = ResetPolicy.Never,
                                             PerStack = new[] { B(StatType.MagicAttack, 1), B(StatType.PhysicalAttack, 0.5f) } } }),
            (RarityTier.Gold, new P { Id = "Frenzy", Name = "Frénésie", Description = "Chaque attaque de base donne +6 % vitesse d'attaque (6 max), perdus après 3 s sans attaquer.",
                Stacks = new StackSettings { Source = StackSource.PerBasicAttack, Reset = ResetPolicy.AfterDuration, Max = 6, Duration = 3f,
                                             PerStack = new[] { B(StatType.AttackSpeed, 0.06f, ModifierType.PercentAdd) } } }),
            (RarityTier.Gold, new P { Id = "Gambler", Name = "Flambeur", Description = "6 % de chances par kill de faire tomber une enclume à stats, ouverte en fin de vague.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 0.06f, Effect = PassiveEffectKind.DropLoot, Loot = LootKind.StatAnvil } } }),
            (RarityTier.Gold, new P { Id = "Melee", Name = "Au cœur de la mêlée", Description = "+3 attaque physique et +5 puissance magique par ennemi à moins de 5 m (8 max).",
                Nearby = new NearbyEnemiesBonus { Radius = 5f, Max = 8, PerEnemy = new[] { B(StatType.PhysicalAttack, 3), B(StatType.MagicAttack, 5) } } }),
            (RarityTier.Prismatic, new P { Id = "ArcaneStorm", Name = "Tempête arcanique", Description = "Un sort lancé sur trois : +30 % vitesse de déplacement et +40 puissance magique pendant 4 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 3, Chance = 1f, Effect = PassiveEffectKind.TimedStats, Duration = 4f,
                                                        Stats = new[] { B(StatType.MoveSpeed, 0.3f, ModifierType.PercentAdd), B(StatType.MagicAttack, 40) } } } }),
            (RarityTier.Prismatic, new P { Id = "Vampirism", Name = "Vampirisme", Description = "+15 % omnivampirisme, +20 attaque physique, +30 puissance magique.",
                Stats = new[] { B(StatType.Omnivamp, 0.15f), B(StatType.PhysicalAttack, 20), B(StatType.MagicAttack, 30) } }),
            (RarityTier.Prismatic, new P { Id = "Summoner", Name = "Invocateur", Description = "8 % de chances par kill de faire tomber un sort d'invocateur (usage unique sur F). +10 accélération.",
                Stats = new[] { B(StatType.AbilityHaste, 10) },
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 0.08f, Effect = PassiveEffectKind.DropLoot, Loot = LootKind.SummonerSpell } } }),
        };

        // (item asset id, passive): item passives on top of their stats
        private static readonly (string item, P passive)[] ItemPassives = {
            ("SwiftBow", new P { Id = "Item_SwiftBow", Name = "Salve", Description = "Une attaque de base sur quatre inflige 15 (+30 % attaque physique) dégâts physiques en plus.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 4, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                                        Damage = 15f, ScalingStat = StatType.PhysicalAttack, DamageRatio = 0.3f, DamageType = DamageType.Physical } } }),
            ("MageCrown", new P { Id = "Item_MageCrown", Name = "Inspiration", Description = "Après un sort : +20 puissance magique pendant 3 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.TimedStats,
                                                        Duration = 3f, Stats = new[] { B(StatType.MagicAttack, 20) } } } }),
        };

        public static (AugmentCatalog augments, LootSettings loot) Run() {
            foreach (string png in Directory.GetFiles(IconsDir, "*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));

            foreach ((string itemId, P passive) in ItemPassives) {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{EconomySetup.Dir}/Items/Item_{itemId}.asset");
                if (item == null || item.Passive != null) continue;
                var so = new SerializedObject(item);
                so.FindProperty("passive").objectReferenceValue = Passive(passive, null, item.Icon);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AugmentCatalog>(AugmentCatalogPath);
            if (catalog == null) {
                var augments = new List<Object>();
                foreach ((RarityTier rarity, P passive) in Augments) {
                    var augment = CreateAsset<AugmentDefinition>($"Assets/Data/Augments/Augment_{passive.Id}.asset");
                    var so = new SerializedObject(augment);
                    so.FindProperty("rarity").enumValueIndex = (int)rarity;
                    so.FindProperty("passive").objectReferenceValue = Passive(passive, $"Augment_{passive.Id}");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    augments.Add(augment);
                }
                catalog = CreateAsset<AugmentCatalog>(AugmentCatalogPath);
                var cso = new SerializedObject(catalog);
                SerializedProperty list = cso.FindProperty("augments");
                list.arraySize = augments.Count;
                for (int i = 0; i < augments.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = augments[i];
                cso.FindProperty("fullSlotsAnvil").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnvilDefinition>($"{EconomySetup.Dir}/Anvils/Anvil_Stats.asset");
                cso.ApplyModifiedPropertiesWithoutUndo();
            }

            var loot = AssetDatabase.LoadAssetAtPath<LootSettings>(LootSettingsPath);
            if (loot == null) {
                loot = CreateAsset<LootSettings>(LootSettingsPath);
                var so = new SerializedObject(loot);
                so.FindProperty("statAnvilSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GraphicAssets/Sprites/Items/Anvil_Stats.png");
                so.FindProperty("summonerSpellSprite").objectReferenceValue = Icon("Loot_SummonerSpell");
                SerializedProperty spells = so.FindProperty("summonerSpells");
                spells.arraySize = 2;
                spells.GetArrayElementAtIndex(0).objectReferenceValue = CreateGhost();
                spells.GetArrayElementAtIndex(1).objectReferenceValue = CreateIgnite();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return (catalog, loot);
        }

        internal static PassiveDefinition Passive(P p, string iconId, Sprite icon = null) {
            string path = $"{Dir}/Passive_{p.Id}.asset";
            var passive = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(path);
            if (passive != null) return passive;
            passive = CreateAsset<PassiveDefinition>(path);
            var so = new SerializedObject(passive);
            so.FindProperty("displayName").stringValue = p.Name;
            so.FindProperty("description").stringValue = p.Description;
            so.FindProperty("icon").objectReferenceValue = icon != null ? icon : Icon(iconId);
            so.ApplyModifiedPropertiesWithoutUndo();
            // Struct arrays are simplest to write through reflection on the fresh asset
            SetField(passive, "stats", p.Stats);
            SetField(passive, "capabilities", p.Capabilities);
            SetField(passive, "triggers", p.Triggers);
            SetField(passive, "stacks", p.Stacks);
            SetField(passive, "nearbyEnemies", p.Nearby);
            EditorUtility.SetDirty(passive);
            return passive;
        }

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(target, value);

        // Summoner spells (§9.5): single use on F
        private static SpellDefinition CreateGhost() {
            const string path = "Assets/Data/Spells/Summoner/Spell_Ghost.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell != null) return spell;
            spell = CreateAsset<SpellDefinition>(path);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = "Fantôme";
            so.FindProperty("icon").objectReferenceValue = Icon("Spell_Ghost");
            so.FindProperty("color").colorValue = new Color(0.75f, 0.9f, 1f);
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Self;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Buff;
            so.FindProperty("canMoveWhileCasting").boolValue = true;
            so.FindProperty("castTime").floatValue = 0f;   // summoner spells are instant
            SpellAssets.SetCooldown(so, 1f);
            SerializedProperty buffs = so.FindProperty("buffs");
            buffs.arraySize = 1;
            SerializedProperty b = buffs.GetArrayElementAtIndex(0);
            b.FindPropertyRelative("Stat").enumValueIndex = (int)StatType.MoveSpeed;
            b.FindPropertyRelative("Type").enumValueIndex = (int)ModifierType.PercentAdd;
            b.FindPropertyRelative("Value").floatValue = 0.4f;
            b.FindPropertyRelative("Duration").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spell;
        }

        private static SpellDefinition CreateIgnite() {
            const string path = "Assets/Data/Spells/Summoner/Spell_Ignite.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell != null) return spell;
            spell = CreateAsset<SpellDefinition>(path);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = "Embrasement";
            so.FindProperty("icon").objectReferenceValue = Icon("Spell_Ignite");
            so.FindProperty("color").colorValue = new Color(1f, 0.5f, 0.2f);
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.UnitTarget;
            so.FindProperty("range").floatValue = 7f;
            so.FindProperty("castTime").floatValue = 0f;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
            SpellAssets.SetDamage(so, 150f, 0.4f);
            so.FindProperty("projectileSpeed").floatValue = 30f;
            SpellAssets.SetCooldown(so, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return spell;
        }

        private static Sprite Icon(string id) => id == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{id}.png");

        private static T CreateAsset<T>(string path) where T : ScriptableObject {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
