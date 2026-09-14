using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Data.Items;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Editor
{
    public static class EquipmentContentBuilder
    {
        public const string CatalogPath = "Assets/Resources/ContentCatalog.asset";
        private const string Root = "Assets/Resources/Content";

        public static void Build()
        {
            EnsureFolder(Root);
            var catalog = LoadOrCreate<ContentCatalog>(CatalogPath);
            var generated = new List<ContentDefinition>();

            var frostLongsword = Item(
                "item.frost_longsword",
                "item.frost_longsword.name",
                ItemSlot.Weapon,
                new[] { "class.warrior" },
                120,
                Stat(CombatStat.Power, 5),
                Stat(CombatStat.MaxHealth, 2));
            var hunterBow = Item(
                "item.hunter_bow",
                "item.hunter_bow.name",
                ItemSlot.Weapon,
                new[] { "class.ranger" },
                110,
                Stat(CombatStat.Power, 4),
                Stat(CombatStat.Speed, 1));
            var oakStaff = Item(
                "item.oak_staff",
                "item.oak_staff.name",
                ItemSlot.Weapon,
                new[] { "class.mage" },
                115,
                Stat(CombatStat.Power, 4),
                Stat(CombatStat.MaxMana, 2));
            var ironHelmet = Item(
                "item.iron_helmet",
                "item.iron_helmet.name",
                ItemSlot.Head,
                Array.Empty<string>(),
                80,
                Stat(CombatStat.Armor, 3),
                Stat(CombatStat.MaxHealth, 2));
            var leatherArmor = Item(
                "item.leather_armor",
                "item.leather_armor.name",
                ItemSlot.Body,
                Array.Empty<string>(),
                100,
                Stat(CombatStat.Armor, 4),
                Stat(CombatStat.MaxHealth, 3));
            var swiftBoots = Item(
                "item.swift_boots",
                "item.swift_boots.name",
                ItemSlot.Boots,
                Array.Empty<string>(),
                75,
                Stat(CombatStat.Speed, 2),
                Stat(CombatStat.Armor, 1));
            var emberCharm = Item(
                "item.ember_charm",
                "item.ember_charm.name",
                ItemSlot.Accessory,
                Array.Empty<string>(),
                130,
                Stat(CombatStat.Power, 3),
                Stat(CombatStat.MaxMana, 3));

            generated.AddRange(new[] { frostLongsword, hunterBow, oakStaff, ironHelmet, leatherArmor, swiftBoots, emberCharm });

            var flatHealth = Affix(
                "affix.flat_health",
                AffixEffectKind.FlatStat,
                ItemRarity.Common,
                new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Accessory },
                CombatStat.MaxHealth,
                default,
                default,
                1,
                4,
                1,
                0,
                10,
                1);
            var flatPower = Affix(
                "affix.flat_power",
                AffixEffectKind.FlatStat,
                ItemRarity.Common,
                new[] { ItemSlot.Weapon, ItemSlot.Accessory },
                CombatStat.Power,
                default,
                default,
                1,
                3,
                1,
                0,
                10,
                1);
            var flatArmor = Affix(
                "affix.flat_armor",
                AffixEffectKind.FlatStat,
                ItemRarity.Common,
                new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Boots },
                CombatStat.Armor,
                default,
                default,
                1,
                4,
                1,
                0,
                10,
                1);
            var crit = Affix(
                "affix.crit_bps",
                AffixEffectKind.PercentStat,
                ItemRarity.Fine,
                new[] { ItemSlot.Weapon, ItemSlot.Accessory },
                CombatStat.CritChanceBps,
                default,
                default,
                100,
                500,
                1,
                0,
                6,
                1);
            var whirlwindRadius = Affix(
                "affix.skill.whirlwind_radius",
                AffixEffectKind.SkillModifier,
                ItemRarity.Fine,
                new[] { ItemSlot.Weapon },
                CombatStat.Power,
                SkillModifierKind.Radius,
                default,
                1,
                1,
                2,
                0,
                4,
                4,
                "skill.whirlwind");
            var fireballMana = Affix(
                "affix.skill.fireball_mana",
                AffixEffectKind.SkillModifier,
                ItemRarity.Fine,
                new[] { ItemSlot.Weapon },
                CombatStat.Power,
                SkillModifierKind.ManaCost,
                default,
                -1,
                -1,
                2,
                0,
                4,
                3,
                "skill.fireball");
            var slow = Affix(
                "affix.trigger.slow",
                AffixEffectKind.Trigger,
                ItemRarity.Fine,
                new[] { ItemSlot.Weapon, ItemSlot.Accessory, ItemSlot.Head, ItemSlot.Body },
                CombatStat.Power,
                default,
                PassiveEffectKind.OnAttackApplySlow,
                1,
                1,
                2,
                2,
                5,
                4);
            var lowHealthArmor = Affix(
                "affix.conditional.low_health_armor",
                AffixEffectKind.Conditional,
                ItemRarity.Rare,
                new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Boots, ItemSlot.Accessory },
                CombatStat.Armor,
                default,
                PassiveEffectKind.LowHealthArmor,
                2,
                5,
                3,
                0,
                4,
                1);
            var highGroundDamage = Affix(
                "affix.conditional.high_ground_damage",
                AffixEffectKind.Conditional,
                ItemRarity.Rare,
                new[] { ItemSlot.Weapon, ItemSlot.Accessory },
                CombatStat.Power,
                default,
                PassiveEffectKind.HighGroundDamage,
                10,
                25,
                3,
                0,
                4,
                1);

            generated.AddRange(new[]
            {
                flatHealth,
                flatPower,
                flatArmor,
                crit,
                whirlwindRadius,
                fireballMana,
                slow,
                lowHealthArmor,
                highGroundDamage
            });

            var loot = GetOrCreate<ItemDropTableDefinition>(Root + "/Loot/loot.bandit.core.asset");
            loot.EditorConfigure(
                "loot.bandit.core",
                1,
                10,
                new[]
                {
                    new LootEntry(frostLongsword, 1),
                    new LootEntry(hunterBow, 1),
                    new LootEntry(oakStaff, 1),
                    new LootEntry(ironHelmet, 1),
                    new LootEntry(leatherArmor, 1),
                    new LootEntry(swiftBoots, 1),
                    new LootEntry(emberCharm, 1)
                },
                new[]
                {
                    new RarityWeight(ItemRarity.Common, 60),
                    new RarityWeight(ItemRarity.Fine, 25),
                    new RarityWeight(ItemRarity.Rare, 12),
                    new RarityWeight(ItemRarity.Epic, 3)
                });
            EditorUtility.SetDirty(loot);
            generated.Add(loot);

            generated.Add(Character(
                "class.warrior",
                new[]
                {
                    Stat(CombatStat.MaxHealth, 24),
                    Stat(CombatStat.MaxMana, 8),
                    Stat(CombatStat.Power, 9),
                    Stat(CombatStat.Armor, 4),
                    Stat(CombatStat.Speed, 5),
                    Stat(CombatStat.CritChanceBps, 500),
                    Stat(CombatStat.Resistance, 3)
                },
                new[]
                {
                    Stat(CombatStat.MaxHealth, 3),
                    Stat(CombatStat.MaxMana, 1),
                    Stat(CombatStat.Power, 2),
                    Stat(CombatStat.Armor, 1),
                    Stat(CombatStat.Speed, 1)
                },
                new[] { "skill.shield_bash" },
                new[]
                {
                    new SkillUnlock("skill.whirlwind", 2),
                    new SkillUnlock("skill.iron_guard", 4),
                    new SkillUnlock("skill.taunt", 6)
                }));
            generated.Add(Character(
                "class.ranger",
                new[]
                {
                    Stat(CombatStat.MaxHealth, 18),
                    Stat(CombatStat.MaxMana, 10),
                    Stat(CombatStat.Power, 8),
                    Stat(CombatStat.Armor, 2),
                    Stat(CombatStat.Speed, 7),
                    Stat(CombatStat.CritChanceBps, 1000),
                    Stat(CombatStat.Resistance, 2)
                },
                new[]
                {
                    Stat(CombatStat.MaxHealth, 2),
                    Stat(CombatStat.MaxMana, 1),
                    Stat(CombatStat.Power, 2),
                    Stat(CombatStat.Armor, 1),
                    Stat(CombatStat.Speed, 1)
                },
                new[] { "skill.piercing_shot" },
                new[]
                {
                    new SkillUnlock("skill.snare", 2),
                    new SkillUnlock("skill.twin_shot", 4)
                }));
            generated.Add(Character(
                "class.mage",
                new[]
                {
                    Stat(CombatStat.MaxHealth, 14),
                    Stat(CombatStat.MaxMana, 20),
                    Stat(CombatStat.Power, 5),
                    Stat(CombatStat.Armor, 1),
                    Stat(CombatStat.Speed, 6),
                    Stat(CombatStat.CritChanceBps, 500),
                    Stat(CombatStat.Resistance, 4)
                },
                new[]
                {
                    Stat(CombatStat.MaxHealth, 1),
                    Stat(CombatStat.MaxMana, 3),
                    Stat(CombatStat.Power, 2),
                    Stat(CombatStat.Speed, 1)
                },
                new[] { "skill.fireball" },
                new[]
                {
                    new SkillUnlock("skill.frost_nova", 2),
                    new SkillUnlock("skill.arcane_ward", 4)
                }));

            var generatedIds = new HashSet<string>(generated.Select(value => value.Id), StringComparer.Ordinal);
            var merged = catalog.All
                .Where(value => value != null && !generatedIds.Contains(value.Id))
                .Concat(generated)
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            catalog.EditorSetDefinitions(merged);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static ItemDefinition Item(
            string id,
            string localizationKey,
            ItemSlot slot,
            IEnumerable<string> classes,
            int value,
            params StatValue[] stats)
        {
            var item = GetOrCreate<ItemDefinition>(Root + "/Items/" + id + ".asset");
            item.EditorConfigure(id, localizationKey, slot, classes, false, value, stats);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static AffixDefinition Affix(
            string id,
            AffixEffectKind kind,
            ItemRarity rarity,
            IEnumerable<ItemSlot> slots,
            CombatStat stat,
            SkillModifierKind modifier,
            PassiveEffectKind passive,
            int minValue,
            int maxValue,
            int minItemLevel,
            int duration,
            int weight,
            int budgetCost,
            string targetSkillId = null)
        {
            var affix = GetOrCreate<AffixDefinition>(Root + "/Affixes/" + id + ".asset");
            affix.EditorConfigure(
                id,
                id + ".name",
                slots.ToArray(),
                rarity,
                kind,
                stat,
                modifier,
                passive,
                minValue,
                maxValue,
                minItemLevel,
                duration,
                weight,
                budgetCost,
                Array.Empty<string>());
            if (!string.IsNullOrWhiteSpace(targetSkillId))
                affix.EditorSetTargetSkillId(targetSkillId);
            EditorUtility.SetDirty(affix);
            return affix;
        }

        private static CharacterDefinition Character(
            string id,
            IEnumerable<StatValue> baseStats,
            IEnumerable<StatValue> growthStats,
            IEnumerable<string> startingSkills,
            IEnumerable<SkillUnlock> unlocks)
        {
            var character = GetOrCreate<CharacterDefinition>(Root + "/Characters/" + id + ".asset");
            character.EditorConfigure(id, id + ".name", baseStats, growthStats, startingSkills, unlocks);
            EditorUtility.SetDirty(character);
            return character;
        }

        private static StatValue Stat(CombatStat stat, int value) => new(stat, value);

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
