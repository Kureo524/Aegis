#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Features.Economy;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Phase 7b content (design doc §9.3), called by <see cref="ProjectSetup"/>: 8 legendary and 4
    /// prismatic items, 3 elixirs, the stat choices of the stat anvils, the anvils and the shop
    /// catalog, with their pixel-art icons. Original names (no LoL item names). Created once and
    /// never overwritten: prices and stats are placeholders tuned in the assets.
    /// </summary>
    public static class EconomySetup {
        public const string Dir = "Assets/Data/Economy";
        public const string CatalogPath = Dir + "/ShopCatalog.asset";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Items";

        private static StatBonus B(StatType s, float v, ModifierType t = ModifierType.Flat) => new(s, t, v);

        private static readonly (string id, string name, ItemTier tier, int cost, StatBonus[] stats)[] Items = {
            ("SharpBlade", "Lame tranchante", ItemTier.Legendary, 260, new[] { B(StatType.PhysicalAttack, 40), B(StatType.CritChance, 0.2f) }),
            ("MageCrown", "Couronne du mage", ItemTier.Legendary, 280, new[] { B(StatType.MagicAttack, 70), B(StatType.AbilityHaste, 10) }),
            ("SwiftBow", "Arc rapide", ItemTier.Legendary, 260, new[] { B(StatType.AttackSpeed, 0.35f, ModifierType.PercentAdd), B(StatType.CritChance, 0.15f) }),
            ("FrostHeart", "Cœur de givre", ItemTier.Legendary, 250, new[] { B(StatType.Armor, 45), B(StatType.MaxMana, 300), B(StatType.AbilityHaste, 15) }),
            ("VoidStaff", "Bâton du néant", ItemTier.Legendary, 270, new[] { B(StatType.MagicAttack, 50), B(StatType.MagicPenPercent, 0.3f) }),
            ("ManaStone", "Pierre de mana", ItemTier.Legendary, 240, new[] { B(StatType.MaxMana, 400), B(StatType.ManaRegen, 4), B(StatType.MagicAttack, 35) }),
            ("ColossusArmor", "Armure du colosse", ItemTier.Legendary, 250, new[] { B(StatType.MaxHealth, 450), B(StatType.HealthRegen, 3), B(StatType.PhysicalAttack, 15) }),
            ("VampireFang", "Croc vampirique", ItemTier.Legendary, 260, new[] { B(StatType.PhysicalAttack, 25), B(StatType.AttackSpeed, 0.2f, ModifierType.PercentAdd), B(StatType.Lifesteal, 0.12f) }),
            ("Starshard", "Éclat d'étoile", ItemTier.Prismatic, 0, new[] { B(StatType.MagicAttack, 90), B(StatType.AbilityHaste, 20), B(StatType.MaxHealth, 300) }),
            ("DawnBlade", "Lame de l'aube", ItemTier.Prismatic, 0, new[] { B(StatType.PhysicalAttack, 60), B(StatType.AttackSpeed, 0.3f, ModifierType.PercentAdd), B(StatType.CritChance, 0.25f) }),
            ("PrismAegis", "Égide prismatique", ItemTier.Prismatic, 0, new[] { B(StatType.Armor, 60), B(StatType.MagicResist, 60), B(StatType.MaxHealth, 600) }),
            ("VoidHeart", "Cœur du néant", ItemTier.Prismatic, 0, new[] { B(StatType.MagicAttack, 70), B(StatType.ManaRegen, 6), B(StatType.ManaCostReduction, 0.2f) }),
        };

        private static readonly (string id, string name, int cost, StatBonus[] stats)[] Elixirs = {
            ("Strength", "Élixir de force", 40, new[] { B(StatType.PhysicalAttack, 25), B(StatType.AttackSpeed, 0.15f, ModifierType.PercentAdd) }),
            ("Sorcery", "Élixir de sorcellerie", 40, new[] { B(StatType.MagicAttack, 40), B(StatType.ManaRegen, 3) }),
            ("Iron", "Élixir de fer", 40, new[] { B(StatType.MaxHealth, 300), B(StatType.Armor, 20), B(StatType.MagicResist, 20) }),
        };

        private static readonly (RarityTier rarity, StatBonus[] stats)[] StatChoices = {
            (RarityTier.Silver, new[] { B(StatType.PhysicalAttack, 8) }),
            (RarityTier.Silver, new[] { B(StatType.MagicAttack, 12) }),
            (RarityTier.Silver, new[] { B(StatType.MaxHealth, 80) }),
            (RarityTier.Silver, new[] { B(StatType.AttackSpeed, 0.1f, ModifierType.PercentAdd) }),
            (RarityTier.Silver, new[] { B(StatType.Armor, 8), B(StatType.MagicResist, 8) }),
            (RarityTier.Silver, new[] { B(StatType.ManaRegen, 1.5f) }),
            (RarityTier.Silver, new[] { B(StatType.AbilityHaste, 8) }),
            (RarityTier.Gold, new[] { B(StatType.PhysicalAttack, 16) }),
            (RarityTier.Gold, new[] { B(StatType.MagicAttack, 25) }),
            (RarityTier.Gold, new[] { B(StatType.MaxHealth, 180) }),
            (RarityTier.Gold, new[] { B(StatType.AttackSpeed, 0.2f, ModifierType.PercentAdd) }),
            (RarityTier.Gold, new[] { B(StatType.CritChance, 0.1f) }),
            (RarityTier.Gold, new[] { B(StatType.AbilityHaste, 15) }),
            (RarityTier.Gold, new[] { B(StatType.MoveSpeed, 0.06f, ModifierType.PercentAdd) }),
            (RarityTier.Prismatic, new[] { B(StatType.PhysicalAttack, 30) }),
            (RarityTier.Prismatic, new[] { B(StatType.MagicAttack, 50) }),
            (RarityTier.Prismatic, new[] { B(StatType.MaxHealth, 350) }),
            (RarityTier.Prismatic, new[] { B(StatType.AttackSpeed, 0.35f, ModifierType.PercentAdd) }),
            (RarityTier.Prismatic, new[] { B(StatType.CritChance, 0.2f) }),
            (RarityTier.Prismatic, new[] { B(StatType.AbilityHaste, 30) }),
        };

        public static ShopCatalog Run() {
            foreach (string png in Directory.GetFiles(IconsDir, "*.png")) ConfigureIcon(png.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            var items = new List<Object>();
            foreach (var i in Items) {
                var item = CreateAsset<ItemDefinition>($"{Dir}/Items/Item_{i.id}.asset");
                var so = new SerializedObject(item);
                so.FindProperty("displayName").stringValue = i.name;
                so.FindProperty("icon").objectReferenceValue = Icon($"Item_{i.id}");
                so.FindProperty("cost").intValue = i.cost;
                so.FindProperty("tier").enumValueIndex = (int)i.tier;
                WriteStats(so.FindProperty("stats"), i.stats);
                so.ApplyModifiedPropertiesWithoutUndo();
                items.Add(item);
            }

            var elixirs = new List<Object>();
            foreach (var e in Elixirs) {
                var elixir = CreateAsset<ElixirDefinition>($"{Dir}/Elixirs/Elixir_{e.id}.asset");
                var so = new SerializedObject(elixir);
                so.FindProperty("displayName").stringValue = e.name;
                so.FindProperty("icon").objectReferenceValue = Icon($"Elixir_{e.id}");
                so.FindProperty("cost").intValue = e.cost;
                WriteStats(so.FindProperty("stats"), e.stats);
                so.ApplyModifiedPropertiesWithoutUndo();
                elixirs.Add(elixir);
            }

            var choices = new List<Object>();
            for (int i = 0; i < StatChoices.Length; i++) {
                var c = StatChoices[i];
                var choice = CreateAsset<StatChoiceDefinition>($"{Dir}/StatChoices/StatChoice_{c.rarity}_{i:00}.asset");
                var so = new SerializedObject(choice);
                so.FindProperty("rarity").enumValueIndex = (int)c.rarity;
                WriteStats(so.FindProperty("stats"), c.stats);
                so.ApplyModifiedPropertiesWithoutUndo();
                choices.Add(choice);
            }

            var statAnvil = Anvil("Anvil_Stats", "Enclume à stats", AnvilKind.Stat, ItemTier.Legendary, 60, 0);
            var legendaryAnvil = Anvil("Anvil_Legendary", "Enclume légendaire", AnvilKind.Item, ItemTier.Legendary, 220, 1);
            var prismaticAnvil = Anvil("Anvil_Prismatic", "Enclume prismatique", AnvilKind.Item, ItemTier.Prismatic, 480, 1);

            catalog = CreateAsset<ShopCatalog>(CatalogPath);
            var cso = new SerializedObject(catalog);
            Fill(cso.FindProperty("elixirs"), elixirs);
            Fill(cso.FindProperty("statAnvils"), new List<Object> { statAnvil });
            Fill(cso.FindProperty("itemAnvils"), new List<Object> { legendaryAnvil, prismaticAnvil });
            Fill(cso.FindProperty("items"), items);
            Fill(cso.FindProperty("statChoices"), choices);
            cso.FindProperty("freeAnvil").objectReferenceValue = prismaticAnvil;
            cso.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static AnvilDefinition Anvil(string id, string name, AnvilKind kind, ItemTier tier, int cost, int rerolls) {
            var anvil = CreateAsset<AnvilDefinition>($"{Dir}/Anvils/{id}.asset");
            var so = new SerializedObject(anvil);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("icon").objectReferenceValue = Icon(id);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("itemTier").enumValueIndex = (int)tier;
            so.FindProperty("cost").intValue = cost;
            so.FindProperty("rerollsPerChoice").intValue = rerolls;
            so.ApplyModifiedPropertiesWithoutUndo();
            return anvil;
        }

        private static void WriteStats(SerializedProperty list, StatBonus[] stats) {
            list.arraySize = stats.Length;
            for (int i = 0; i < stats.Length; i++) {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Stat").enumValueIndex = (int)stats[i].Stat;
                e.FindPropertyRelative("Type").enumValueIndex = (int)stats[i].Type;
                e.FindPropertyRelative("Value").floatValue = stats[i].Value;
            }
        }

        private static void Fill(SerializedProperty list, List<Object> values) {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Sprite Icon(string id) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{id}.png");

        // UI icons: single sprite, pixel art, centered. Idempotent.
        private static void ConfigureIcon(string path) {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || (importer.textureType == TextureImporterType.Sprite && importer.filterMode == FilterMode.Point)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
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
