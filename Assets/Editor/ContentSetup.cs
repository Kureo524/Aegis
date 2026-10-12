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
    /// - statuses on the test spells (fire burns, electricity stuns, wind slows) and self shields /
    ///   heals on the buff spells; statuses on the enemy spells; tenacity for the brute and Grokk;
    /// - 12 legendary and 2 prismatic items, 7 augments, built from the generic passive bricks and
    ///   added to the shop and augment catalogs.
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

        // ---- Spells: fire burns, electricity stuns, wind slows (by display name) ----
        private static readonly Dictionary<string, StatusApplication[]> SpellStatuses = new() {
            ["Boule de feu"]        = new[] { Burn(10f, 0.05f, 3f, 3) },
            ["Étincelle"]           = new[] { Stun(0.25f) },
            ["Rafale"]              = new[] { Slow(0.3f, 1.5f) },
            ["Grande boule de feu"] = new[] { Burn(18f, 0.08f, 3f, 3) },
            ["Arc électrique"]      = new[] { Stun(0.4f) },
            ["Plasma"]              = new[] { Burn(15f, 0.06f, 3f, 3), Stun(0.3f) },
            ["Tornade de feu"]      = new[] { Burn(15f, 0.06f, 3f, 3), Slow(0.25f, 1.5f) },
            ["Orage"]               = new[] { Slow(0.35f, 2f), Stun(0.3f) },
            ["Météore"]             = new[] { Burn(30f, 0.1f, 4f, 2), Stun(0.5f) },
            ["Foudre"]              = new[] { Stun(0.8f) },
            ["Cyclone"]             = new[] { Slow(0.45f, 2f) },
            ["Lance de plasma"]     = new[] { Burn(20f, 0.08f, 3f, 3), Stun(0.35f) },
            ["Tempête de feu"]      = new[] { Burn(25f, 0.1f, 3f, 3), Slow(0.2f, 1.5f) },
            ["Tempête électrique"]  = new[] { Stun(0.6f), Slow(0.3f, 2f) },
            ["Souffle ardent"]      = new[] { Burn(20f, 0.08f, 3f, 3), Slow(0.3f, 2f) },
            ["Cataclysme"]          = new[] { Burn(30f, 0.12f, 4f, 3), Stun(0.75f) },
        };

        // (name, heal, shield, power ratio, shield duration) on the buff spells
        private static readonly Dictionary<string, SelfSustain> SpellSustains = new() {
            ["Bourrasque"]       = new SelfSustain { Heal = 40f, PowerRatio = 0.2f },
            ["Surcharge"]        = new SelfSustain { Shield = 80f, PowerRatio = 0.3f, ShieldDuration = 4f },
            ["Ouragan statique"] = new SelfSustain { Shield = 60f, PowerRatio = 0.25f, ShieldDuration = 5f },
        };

        private static readonly Dictionary<string, StatusApplication[]> EnemySpellStatuses = new() {
            ["Assets/Data/Spells/Enemies/Spell_BruteSlam.asset"] = new[] { Slow(0.4f, 1.5f) },
            ["Assets/Data/Spells/Enemies/Spell_GrokkSlam.asset"] = new[] { Stun(0.75f) },
            ["Assets/Data/Spells/Enemies/Spell_GrokkNova.asset"] = new[] { Slow(0.35f, 2f) },
        };

        private static readonly (string path, float tenacity)[] Tenacities = {
            ("Assets/Data/Characters/Enemies/StatBlock_Brute.asset", 0.3f),
            ("Assets/Data/Characters/Bosses/StatBlock_GoblinKing.asset", 0.5f),
        };

        // ---- Items ----
        private static readonly (string id, string name, ItemTier tier, int cost, StatBonus[] stats, P passive)[] Items = {
            ("BleedingBlade", "Lame hémorragique", ItemTier.Legendary, 270,
                new[] { B(StatType.PhysicalAttack, 30), B(StatType.AttackSpeed, 0.15f, ModifierType.PercentAdd) },
                new P { Id = "Item_BleedingBlade", Name = "Entaille", Description = "Tes attaques de base font saigner : 8 (+15 % attaque physique) dégâts physiques par seconde pendant 3 s, cumulable 5 fois.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.BasicAttackHit, 1, Bleed(8f, 0.15f, 3f, 5)) } }),
            ("FrostScepter", "Sceptre glacial", ItemTier.Legendary, 280,
                new[] { B(StatType.MagicAttack, 60), B(StatType.MaxHealth, 200) },
                new P { Id = "Item_FrostScepter", Name = "Givre", Description = "Tes sorts ralentissent de 35 % pendant 1,5 s.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 1, Slow(0.35f, 1.5f)) } }),
            ("ThunderHammer", "Marteau-tonnerre", ItemTier.Legendary, 290,
                new[] { B(StatType.PhysicalAttack, 35), B(StatType.MaxHealth, 250) },
                new P { Id = "Item_ThunderHammer", Name = "Onde de choc", Description = "Une attaque de base sur cinq étourdit 0,6 s.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.BasicAttackHit, 5, Stun(0.6f)) } }),
            ("EmberTalisman", "Talisman du brasier", ItemTier.Legendary, 260,
                new[] { B(StatType.MagicAttack, 45), B(StatType.AbilityHaste, 10) },
                new P { Id = "Item_EmberTalisman", Name = "Braises", Description = "Tes sorts brûlent : 12 (+10 % puissance magique) dégâts magiques par seconde pendant 3 s, cumulable 3 fois.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 1, Burn(12f, 0.1f, 3f, 3)) } }),
            ("GuardianShield", "Bouclier du gardien", ItemTier.Legendary, 250,
                new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 30) },
                new P { Id = "Item_GuardianShield", Name = "Dernier rempart", Description = "Touché sous 35 % de PV : bouclier de 150 (+15 % PV max) pendant 4 s. Recharge 30 s.",
                        Triggers = new[] { new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, HealthBelow = 0.35f, Cooldown = 30f,
                                                                Effect = PassiveEffectKind.Shield, Amount = 150f, AmountStat = StatType.MaxHealth, AmountRatio = 0.15f, Duration = 4f } } }),
            ("MercuryBoots", "Bottes de vif-argent", ItemTier.Legendary, 200,
                new[] { B(StatType.MoveSpeed, 0.12f, ModifierType.PercentAdd), B(StatType.MagicResist, 30), B(StatType.Tenacity, 0.3f) }, null),
            ("LifeChalice", "Calice de vie", ItemTier.Legendary, 240,
                new[] { B(StatType.MaxHealth, 250), B(StatType.HealthRegen, 3), B(StatType.HealShieldPower, 0.2f) },
                new P { Id = "Item_LifeChalice", Name = "Communion", Description = "Chaque kill rend 10 PV (+1 % PV max).",
                        Triggers = new[] { new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Heal,
                                                                Amount = 10f, AmountStat = StatType.MaxHealth, AmountRatio = 0.01f } } }),
            ("RuneGrimoire", "Grimoire runique", ItemTier.Legendary, 270,
                new[] { B(StatType.MagicAttack, 55), B(StatType.MaxMana, 200), B(StatType.ManaCostReduction, 0.15f) }, null),
            ("HunterSpear", "Lance du chasseur", ItemTier.Legendary, 260,
                new[] { B(StatType.PhysicalAttack, 35), B(StatType.ArmorPenPercent, 0.25f), B(StatType.CritChance, 0.1f) }, null),
            ("TrollHeart", "Cœur de troll", ItemTier.Legendary, 260,
                new[] { B(StatType.MaxHealth, 400), B(StatType.HealthRegen, 8) }, null),
            ("RunicHourglass", "Sablier runique", ItemTier.Legendary, 280,
                new[] { B(StatType.MagicAttack, 50), B(StatType.Armor, 40), B(StatType.AbilityHaste, 15) }, null),
            ("WindFangs", "Crocs du vent", ItemTier.Legendary, 250,
                new[] { B(StatType.AttackSpeed, 0.3f, ModifierType.PercentAdd), B(StatType.MoveSpeed, 0.06f, ModifierType.PercentAdd) },
                new P { Id = "Item_WindFangs", Name = "Vent arrière", Description = "Chaque kill donne +20 % vitesse de déplacement pendant 2 s.",
                        Triggers = new[] { new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.TimedStats, Duration = 2f,
                                                                Stats = new[] { B(StatType.MoveSpeed, 0.2f, ModifierType.PercentAdd) } } } }),
            ("GoblinCrown", "Couronne du roi gobelin", ItemTier.Prismatic, 0,
                new[] { B(StatType.MaxHealth, 500), B(StatType.PhysicalAttack, 30), B(StatType.Tenacity, 0.25f) },
                new P { Id = "Item_GoblinCrown", Name = "Coup royal", Description = "Une attaque de base sur quatre étourdit 0,8 s et te donne un bouclier de 60 (+5 % PV max) pendant 3 s.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.BasicAttackHit, 4, Stun(0.8f)),
                                           new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 4, Chance = 1f, Effect = PassiveEffectKind.Shield,
                                                                Amount = 60f, AmountStat = StatType.MaxHealth, AmountRatio = 0.05f, Duration = 3f } } }),
            ("EclipseOrb", "Orbe de l'éclipse", ItemTier.Prismatic, 0,
                new[] { B(StatType.MagicAttack, 100), B(StatType.MagicPenFlat, 15) },
                new P { Id = "Item_EclipseOrb", Name = "Éclipse", Description = "Tes sorts brûlent : 25 (+15 % puissance magique) dégâts magiques par seconde pendant 3 s.",
                        Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 1, Burn(25f, 0.15f, 3f, 1)) } }),
        };

        // ---- Augments ----
        private static readonly (RarityTier rarity, P passive)[] Augments = {
            (RarityTier.Silver, new P { Id = "IronWill", Name = "Volonté de fer", Description = "+25 % ténacité, +100 PV.",
                Stats = new[] { B(StatType.Tenacity, 0.25f), B(StatType.MaxHealth, 100) } }),
            (RarityTier.Silver, new P { Id = "Healer", Name = "Main guérisseuse", Description = "+25 % puissance des soins et boucliers, +2 régénération de PV.",
                Stats = new[] { B(StatType.HealShieldPower, 0.25f), B(StatType.HealthRegen, 2) } }),
            (RarityTier.Gold, new P { Id = "Bloodletting", Name = "Saignée", Description = "Tes attaques de base font saigner : 10 (+20 % attaque physique) par seconde pendant 3 s, cumulable 4 fois.",
                Triggers = new[] { OnHitStatus(PassiveEvent.BasicAttackHit, 1, Bleed(10f, 0.2f, 3f, 4)) } }),
            (RarityTier.Gold, new P { Id = "StaticShock", Name = "Choc statique", Description = "Un coup de sort sur six étourdit la cible 0,75 s.",
                Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 6, Stun(0.75f)) } }),
            (RarityTier.Gold, new P { Id = "ArcaneBarrier", Name = "Barrière arcanique", Description = "Après un sort à 2 éléments ou plus : bouclier de 40 (+25 % puissance magique) pendant 3 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 1, Chance = 1f, MinSpellStacks = 2, Effect = PassiveEffectKind.Shield,
                                                        Amount = 40f, AmountStat = StatType.MagicAttack, AmountRatio = 0.25f, Duration = 3f } } }),
            (RarityTier.Prismatic, new P { Id = "Phoenix", Name = "Phénix", Description = "Touché sous 30 % de PV : rend 30 % de tes PV max. Recharge 90 s.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, HealthBelow = 0.3f, Cooldown = 90f,
                                                        Effect = PassiveEffectKind.Heal, AmountStat = StatType.MaxHealth, AmountRatio = 0.3f } } }),
            (RarityTier.Prismatic, new P { Id = "Inferno", Name = "Brasier éternel", Description = "Tes sorts brûlent : 20 (+20 % puissance magique) par seconde pendant 4 s, cumulable 5 fois.",
                Triggers = new[] { OnHitStatus(PassiveEvent.SpellHit, 1, Burn(20f, 0.2f, 4f, 5)) } }),
        };

        public static void Run() {
            ConfigureSpells();
            ConfigureEnemies();
            AddItems();
            AddAugments();
        }

        private static void ConfigureSpells() {
            var book = AssetDatabase.LoadAssetAtPath<SpellBook>(ProjectSetup.TestSpellBookPath);
            if (book != null) {
                foreach (SpellDefinition spell in book.Spells) {
                    if (spell == null) continue;
                    if (spell.Statuses.Length == 0 && SpellStatuses.TryGetValue(spell.DisplayName, out StatusApplication[] statuses))
                        Set(spell, "statuses", statuses);
                    if (spell.SelfSustain.IsEmpty && SpellSustains.TryGetValue(spell.DisplayName, out SelfSustain sustain))
                        Set(spell, "selfSustain", sustain);
                }
            }
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

        private static void AddItems() {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(EconomySetup.CatalogPath);
            if (catalog == null) return;
            var items = new List<Object>(catalog.Items);
            foreach (var i in Items) {
                string path = $"{EconomySetup.Dir}/Items/Item_{i.id}.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) {
                    Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{ItemIconsDir}/Item_{i.id}.png");
                    item = CreateAsset<ItemDefinition>(path);
                    var so = new SerializedObject(item);
                    so.FindProperty("displayName").stringValue = i.name;
                    so.FindProperty("icon").objectReferenceValue = icon;
                    so.FindProperty("cost").intValue = i.cost;
                    so.FindProperty("tier").enumValueIndex = (int)i.tier;
                    EconomySetup.WriteStats(so.FindProperty("stats"), i.stats);
                    if (i.passive != null) so.FindProperty("passive").objectReferenceValue = PassiveSetup.Passive(i.passive, null, icon);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!items.Contains(item)) items.Add(item);
            }
            if (items.Count == catalog.Items.Count) return;
            var cso = new SerializedObject(catalog);
            Fill(cso.FindProperty("items"), items);
            cso.ApplyModifiedPropertiesWithoutUndo();
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
