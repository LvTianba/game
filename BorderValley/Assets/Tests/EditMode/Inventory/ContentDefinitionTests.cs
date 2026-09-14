using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Inventory.Tests
{
    public sealed class ContentDefinitionTests
    {
        [Test]
        public void Validate_AffixWithImpossibleRange_ReturnsIssue()
        {
            var affix = ScriptableObject.CreateInstance<AffixDefinition>();
            affix.EditorConfigure(
                "affix.bad",
                "affix.bad.name",
                new[] { ItemSlot.Weapon },
                ItemRarity.Fine,
                AffixEffectKind.FlatStat,
                CombatStat.Power,
                default,
                default,
                5,
                2,
                1,
                10,
                "affix.bad.cost",
                System.Array.Empty<string>());
            var issues = ContentValidator.Validate(new[] { affix }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "invalid_affix_range"), Is.True);
            Object.DestroyImmediate(affix);
        }

        [Test]
        public void Validate_ItemAndDropTableReferences_AreResolved()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                new[] { "class.warrior" },
                false,
                100,
                new[] { new StatValue(CombatStat.Power, 4) });
            var table = ScriptableObject.CreateInstance<ItemDropTableDefinition>();
            table.EditorConfigure(
                "loot.test",
                2,
                10,
                new[] { new LootEntry(item, 1) },
                new[]
                {
                    new RarityWeight(ItemRarity.Common, 60),
                    new RarityWeight(ItemRarity.Fine, 40)
                });
            var issues = ContentValidator.Validate(new ContentDefinition[] { item, table }).ToList();
            Assert.That(issues, Is.Empty);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void ItemDefinition_BaseStat_GrowsWithItemLevel()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                System.Array.Empty<string>(),
                false,
                100,
                new[] { new StatValue(CombatStat.Power, 4) });

            Assert.That(item.GetBaseStat(CombatStat.Power, 3), Is.EqualTo(6));

            Object.DestroyImmediate(item);
        }

        [Test]
        public void BattleCombatantSnapshot_ClampsValuesAndCopiesCollections()
        {
            var modifiers = new List<BattleSkillModifierSnapshot>
            {
                new("skill.test", SkillModifierKind.Radius, 1)
            };
            var snapshot = new BattleCombatantSnapshot(
                "unit",
                "class.warrior",
                "class.warrior",
                maxHealth: 20,
                maxMana: 10,
                power: -1,
                armor: -1,
                speed: -1,
                critChanceBps: 12000,
                resistance: -1,
                currentHealth: 0,
                currentMana: 20,
                skillIds: new[] { "skill.test" },
                skillModifiers: modifiers,
                passives: System.Array.Empty<BattlePassiveSnapshot>());
            modifiers.Clear();

            Assert.That(snapshot.Power, Is.Zero);
            Assert.That(snapshot.CritChanceBps, Is.EqualTo(10000));
            Assert.That(snapshot.CurrentHealth, Is.EqualTo(1));
            Assert.That(snapshot.CurrentMana, Is.EqualTo(10));
            Assert.That(snapshot.SkillModifiers, Has.Count.EqualTo(1));
        }

        [Test]
        public void BattlePartySnapshot_RequiresAtLeastOneMember()
        {
            Assert.Throws<ArgumentException>(
                () => new BattlePartySnapshot(System.Array.Empty<BattleCombatantSnapshot>()));
        }
    }
}
