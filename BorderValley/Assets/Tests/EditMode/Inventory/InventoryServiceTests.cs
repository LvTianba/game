using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BorderValley.Core.Combat;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class InventoryServiceTests
    {
        [Test]
        public void TryEquip_ReplacesExistingItemAndReturnsItToBag()
        {
            var definitions = Definitions();
            var service = new InventoryService(2, definitions, 100);
            var sword = Item("i1", "item.sword", ItemRarity.Common);
            var axe = Item("i2", "item.axe", ItemRarity.Common);
            Assert.That(service.TryAdd(sword, out _), Is.True);
            Assert.That(service.TryAdd(axe, out _), Is.True);

            Assert.That(service.TryEquip("i1", "class.warrior", out _), Is.True);
            Assert.That(service.TryEquip("i2", "class.warrior", out _), Is.True);

            Assert.That(service.Equipped[ItemSlot.Weapon], Is.EqualTo("i2"));
            Assert.That(service.Items.Any(item => item.InstanceId == "i1"), Is.True);
        }

        [Test]
        public void TryEquip_ByMember_KeepsIndependentLoadoutsAndRejectsCrossMemberReuse()
        {
            var service = new InventoryService(4, Definitions(), 100);
            Assert.That(service.TryAdd(Item("i1", "item.sword", ItemRarity.Common), out _), Is.True);
            Assert.That(service.TryAdd(Item("i2", "item.axe", ItemRarity.Common), out _), Is.True);

            Assert.That(service.TryEquip("i1", "player.warrior", "class.warrior", out _), Is.True);
            Assert.That(service.TryEquip("i2", "player.ranger", "class.warrior", out _), Is.True);

            Assert.That(service.GetEquipped("player.warrior")[ItemSlot.Weapon], Is.EqualTo("i1"));
            Assert.That(service.GetEquipped("player.ranger")[ItemSlot.Weapon], Is.EqualTo("i2"));
            Assert.That(service.TryEquip("i1", "player.mage", "class.warrior", out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.AlreadyEquipped));
        }

        [Test]
        public void SaveParticipant_RestoresItemsEquipmentGoldAndMaterials()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "BorderValleyTests", System.Guid.NewGuid().ToString("N"));
            var service = new InventoryService(3, Definitions(), 25);
            service.TryAdd(Item("i1", "item.sword", ItemRarity.Fine), out _);
            service.TryEquip("i1", "class.warrior", out _);
            service.TryAddMaterial("material.ore", 4);
            var save = new SaveService(root, new ISaveParticipant[] { service });
            save.Save(0, "World");

            var restored = new InventoryService(3, Definitions(), 0);
            new SaveService(root, new ISaveParticipant[] { restored }).Load(0);

            Assert.That(restored.Gold, Is.EqualTo(25));
            Assert.That(restored.Items.Single().InstanceId, Is.EqualTo("i1"));
            Assert.That(restored.Equipped[ItemSlot.Weapon], Is.EqualTo("i1"));
            Assert.That(restored.Materials["material.ore"], Is.EqualTo(4));
            System.IO.Directory.Delete(root, true);
        }

        [Test]
        public void SaveLoad_WithChecksumValidMalformedPrimary_RollsBackAndUsesBackup()
        {
            var root = Path.Combine(Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));
            var inventory = new InventoryService(4, Definitions(), 0);
            var progression = Progression();
            Assert.That(inventory.TryAdd(Item("backup.item", "item.sword", ItemRarity.Common), out _), Is.True);
            Assert.That(inventory.TryEquip("backup.item", "player.warrior", "class.warrior", out _), Is.True);
            inventory.AddGold(11);
            var save = new SaveService(root, new ISaveParticipant[] { inventory, progression });
            save.Save(0, "Backup");

            inventory.AddGold(11);
            Assert.That(inventory.TryAdd(Item("primary.item", "item.sword", ItemRarity.Common), out _), Is.True);
            save.Save(0, "Primary");

            var primaryPath = save.GetPrimaryPathForTests(0);
            var envelope = JObject.Parse(File.ReadAllText(primaryPath));
            var data = JsonConvert.DeserializeObject<SaveGameData>(envelope.Value<string>("payload"));
            var malformedInventory = (JObject)data.Participants["inventory"];
            malformedInventory["equippedByMember"] = new JObject
            {
                ["player.warrior"] = new JObject { ["NotASlot"] = "backup.item" }
            };
            WriteEnvelope(primaryPath, data);

            inventory.Reset();
            progression.Reset();
            Assert.That(save.Load(0), Is.True);

            Assert.That(inventory.Gold, Is.EqualTo(11));
            Assert.That(inventory.GetEquipped("player.warrior")[ItemSlot.Weapon], Is.EqualTo("backup.item"));
            Assert.That(inventory.Items.Any(value => value.InstanceId == "primary.item"), Is.False);
            Directory.Delete(root, true);
        }

        [Test]
        public void SaveLoad_LegacyEquipment_MigratesToCompatibleMemberAndAppliesStats()
        {
            var root = Path.Combine(Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));
            var definitions = Definitions();
            var legacyItem = Item("legacy.sword", "item.sword", ItemRarity.Common);
            var legacyData = new SaveGameData { SceneName = "Legacy" };
            legacyData.Participants["inventory"] = new JObject
            {
                ["gold"] = 7,
                ["materials"] = new JObject(),
                ["items"] = new JArray(ItemJson(legacyItem)),
                ["equipped"] = new JObject { ["Weapon"] = legacyItem.InstanceId }
            };
            legacyData.Participants["party"] = Progression().Capture();
            var path = new SaveService(root, Array.Empty<ISaveParticipant>()).GetPrimaryPathForTests(0);
            WriteEnvelope(path, legacyData);

            var inventory = new InventoryService(4, definitions, 0);
            var progression = Progression();
            var save = new SaveService(root, new ISaveParticipant[] { inventory, progression });
            Assert.That(save.Load(0), Is.True);

            Assert.That(inventory.GetEquipped("player.warrior")[ItemSlot.Weapon], Is.EqualTo("legacy.sword"));
            Assert.That(inventory.GetEquipped(null), Is.Empty);
            var snapshot = new PartyBattleSnapshotBuilder(
                    progression,
                    inventory,
                    definitions,
                    new Dictionary<string, AffixDefinition>())
                .BuildPartySnapshot();
            Assert.That(snapshot.Members.Single(value => value.UnitId == "player.warrior").Power, Is.EqualTo(14));

            Assert.That(inventory.TryUnequip("player.warrior", ItemSlot.Weapon, out _), Is.True);
            Assert.That(inventory.IsEquipped("legacy.sword"), Is.False);
            Assert.That(inventory.TryEquip("legacy.sword", "player.warrior", "class.warrior", out _), Is.True);
            Assert.That(inventory.GetEquipped("player.warrior")[ItemSlot.Weapon], Is.EqualTo("legacy.sword"));
            Directory.Delete(root, true);
        }

        [Test]
        public void TryAddMaterial_RejectsBlankIdsAndNonPositiveAmounts()
        {
            var service = new InventoryService(3, Definitions(), 0);

            Assert.That(service.TryAddMaterial(string.Empty, 1), Is.False);
            Assert.That(service.TryAddMaterial("material.ore", 0), Is.False);
            Assert.That(service.TryAddMaterial("material.ore", 2), Is.True);
            Assert.That(service.TryAddMaterial("material.ore", 3), Is.True);
            Assert.That(service.Materials["material.ore"], Is.EqualTo(5));
        }

        [Test]
        public void InventoryQuery_FiltersByDefinitionEquipmentAndAffixKind()
        {
            var definitions = Definitions();
            var affixes = AffixDefinitions();
            var source = new[]
            {
                Item("i1", "item.sword", ItemRarity.Fine, 1, new AffixInstance("affix.flat", 1)),
                Item("i2", "item.axe", ItemRarity.Fine, 1, new AffixInstance("affix.percent", 5)),
                Item("i3", "item.sword", ItemRarity.Rare, 2, new AffixInstance("affix.percent", 5)),
                Item("i4", "item.sword", ItemRarity.Fine, 2, new AffixInstance("affix.flat", 2))
            };

            var result = InventoryQuery.Apply(
                source,
                definitions,
                new InventoryFilter
                {
                    Slot = ItemSlot.Weapon,
                    Rarity = ItemRarity.Fine,
                    Equipped = false,
                    AffixKind = AffixEffectKind.PercentStat
                },
                InventorySort.ItemLevelThenName,
                instanceId => instanceId == "i1",
                affixes);

            Assert.That(result.Select(item => item.InstanceId), Is.EqualTo(new[] { "i2" }));
            Assert.That(source, Has.Length.EqualTo(4));
        }

        [Test]
        public void InventoryQuery_SortsByItemLevelThenName()
        {
            var source = new[]
            {
                Item("i1", "item.axe", ItemRarity.Common, 2),
                Item("i2", "item.sword", ItemRarity.Common, 2),
                Item("i3", "item.sword", ItemRarity.Common, 1)
            };

            var result = InventoryQuery.Apply(
                source,
                Definitions(),
                new InventoryFilter(),
                InventorySort.ItemLevelThenName);

            Assert.That(result.Select(item => item.InstanceId), Is.EqualTo(new[] { "i3", "i1", "i2" }));
        }

        [Test]
        public void InventoryQuery_ReturnsEmptyArrayForNoMatches()
        {
            var result = InventoryQuery.Apply(
                new[] { Item("i1", "item.sword", ItemRarity.Common) },
                Definitions(),
                new InventoryFilter { Slot = ItemSlot.Offhand },
                InventorySort.SlotThenRarity);

            Assert.That(result, Is.Empty);
            Assert.That(result.GetType(), Is.EqualTo(typeof(ItemInstance[])));
        }

        private static PartyProgressionService Progression() => new(
            Characters(),
            new[]
            {
                new PartyMemberState("player.warrior", "class.warrior", 1, 0, 0, 20, 10),
                new PartyMemberState("player.ranger", "class.ranger", 1, 0, 0, 16, 10),
                new PartyMemberState("player.mage", "class.mage", 1, 0, 0, 12, 20)
            });

        private static IReadOnlyDictionary<string, CharacterDefinition> Characters()
        {
            var warrior = ScriptableObject.CreateInstance<CharacterDefinition>();
            warrior.EditorConfigure(
                "class.warrior",
                "class.warrior.name",
                new[] { new StatValue(CombatStat.MaxHealth, 20), new StatValue(CombatStat.MaxMana, 10), new StatValue(CombatStat.Power, 9) },
                Array.Empty<StatValue>(),
                new[] { "skill.basic" },
                Array.Empty<SkillUnlock>());
            var ranger = ScriptableObject.CreateInstance<CharacterDefinition>();
            ranger.EditorConfigure(
                "class.ranger",
                "class.ranger.name",
                new[] { new StatValue(CombatStat.MaxHealth, 16), new StatValue(CombatStat.MaxMana, 10), new StatValue(CombatStat.Power, 7) },
                Array.Empty<StatValue>(),
                new[] { "skill.shot" },
                Array.Empty<SkillUnlock>());
            var mage = ScriptableObject.CreateInstance<CharacterDefinition>();
            mage.EditorConfigure(
                "class.mage",
                "class.mage.name",
                new[] { new StatValue(CombatStat.MaxHealth, 12), new StatValue(CombatStat.MaxMana, 20), new StatValue(CombatStat.Power, 5) },
                Array.Empty<StatValue>(),
                new[] { "skill.fireball" },
                Array.Empty<SkillUnlock>());
            return new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal)
            {
                [warrior.Id] = warrior,
                [ranger.Id] = ranger,
                [mage.Id] = mage
            };
        }

        private static void WriteEnvelope(string path, SaveGameData data)
        {
            var payload = JsonConvert.SerializeObject(data, Formatting.Indented);
            var envelope = new JObject
            {
                ["payload"] = payload,
                ["checksum"] = Checksum(payload)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, envelope.ToString(Formatting.Indented));
        }

        private static string Checksum(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }

        private static JObject ItemJson(ItemInstance item) => new()
        {
            ["instanceId"] = item.InstanceId,
            ["definitionId"] = item.ItemDefinitionId,
            ["itemLevel"] = item.ItemLevel,
            ["rarity"] = item.Rarity.ToString(),
            ["affixes"] = new JArray()
        };

        private static System.Collections.Generic.Dictionary<string, ItemDefinition> Definitions()
        {
            var sword = ScriptableObject.CreateInstance<ItemDefinition>();
            sword.EditorConfigure("item.sword", "item.sword.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, new[] { new StatValue(CombatStat.Power, 5) });
            var axe = ScriptableObject.CreateInstance<ItemDefinition>();
            axe.EditorConfigure("item.axe", "item.axe.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, System.Array.Empty<StatValue>());
            return new System.Collections.Generic.Dictionary<string, ItemDefinition>
            {
                [sword.Id] = sword,
                [axe.Id] = axe
            };
        }

        private static System.Collections.Generic.Dictionary<string, AffixDefinition> AffixDefinitions()
        {
            var flat = ScriptableObject.CreateInstance<AffixDefinition>();
            flat.EditorConfigure("affix.flat", "affix.flat.name", new[] { ItemSlot.Weapon },
                ItemRarity.Common, AffixEffectKind.FlatStat, CombatStat.Power,
                default, default, 1, 1, 1, 0, "budget.flat", System.Array.Empty<string>());
            var percent = ScriptableObject.CreateInstance<AffixDefinition>();
            percent.EditorConfigure("affix.percent", "affix.percent.name", new[] { ItemSlot.Weapon },
                ItemRarity.Common, AffixEffectKind.PercentStat, CombatStat.Power,
                default, default, 1, 1, 1, 0, "budget.percent", System.Array.Empty<string>());
            return new System.Collections.Generic.Dictionary<string, AffixDefinition>
            {
                [flat.Id] = flat,
                [percent.Id] = percent
            };
        }

        private static ItemInstance Item(
            string id,
            string definitionId,
            ItemRarity rarity,
            int itemLevel = 1,
            params AffixInstance[] affixes) =>
            new(id, definitionId, itemLevel, rarity, affixes);
    }
}
