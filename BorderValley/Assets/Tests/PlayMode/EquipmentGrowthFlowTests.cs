using System.Collections;
using System.IO;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using BorderValley.Inventory;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayMode.Tests
{
    public sealed class EquipmentGrowthFlowTests
    {
        [UnityTest]
        public IEnumerator World_ConsumesBattleResult_AddsLootAndAwardsExperience()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var beforeGold = inventory.Gold;

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                4,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));
            var entry = Object.FindFirstObjectByType<WorldBattleEntryView>();
            entry.ConsumePendingResultForTests();

            Assert.That(progression.Members.Any(member => member.Experience > 0), Is.True);
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator World_EnemyVictory_ReturnsToSafePointAndClearsWipeFlag()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var progression = context.Get<PartyProgressionService>();

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.EnemyVictory,
                2,
                new[]
                {
                    new BattleUnitResult("player.warrior", 0, 0),
                    new BattleUnitResult("player.ranger", 0, 0),
                    new BattleUnitResult("player.mage", 0, 0)
                }));
            var entry = Object.FindAnyObjectByType<WorldBattleEntryView>();
            Assert.That(entry.ConsumePendingResultForTests(), Is.True);

            Assert.That(progression.SafePointId, Is.EqualTo(PartyProgressionService.DefaultSafePointId));
            Assert.That(progression.HasPendingWipeReturn, Is.False);
            Assert.That(entry.ResultLabel.text, Is.EqualTo("battle.result.enemy_victory"));
            Assert.That(entry.PartyHealthLabel.text, Does.Contain(PartyProgressionService.DefaultSafePointId));
        }

        [UnityTest]
        public IEnumerator World_AutosaveFailure_KeepsRewardsAndShowsError()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var context = GameBootstrapper.Context;
            var save = context.Get<SaveService>();
            save.Delete(0);
            save.Save(0, "World");

            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var entry = Object.FindAnyObjectByType<WorldBattleEntryView>();
            var beforeGold = inventory.Gold;
            var beforeItems = inventory.Items.Count;

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                1,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));

            using (new FileStream(save.GetPrimaryPathForTests(0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(entry.ConsumePendingResultForTests(), Is.True);
            }

            Assert.That(entry.ResultLabel.text, Is.EqualTo("save.error.autosave_failed"));
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(beforeItems));
            Assert.That(progression.Members.Any(member => member.Experience > 0), Is.True);
            save.Delete(0);
        }

        [UnityTest]
        public IEnumerator SaveLoad_RestoresInventoryAndProgressionBeforeWorld()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var context = GameBootstrapper.Context;
            var save = context.Get<SaveService>();
            save.Delete(0);

            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var startingGold = inventory.Gold;
            var item = new ItemInstance(
                "persisted.item",
                "item.frost_longsword",
                4,
                ItemRarity.Rare,
                new[]
                {
                    new AffixInstance("affix.flat_power", 3),
                    new AffixInstance("affix.crit_bps", 2)
                });

            Assert.That(inventory.TryAdd(item, out _), Is.True);
            Assert.That(inventory.TryEquip(item.InstanceId, "class.warrior", out _), Is.True);
            inventory.AddGold(123);
            Assert.That(inventory.TryAddMaterial(CraftingService.OreMaterialId, 17), Is.True);
            progression.AwardExperience(150);
            Assert.That(
                progression.TrySpendSkillPoint("player.warrior", "skill.shield_bash", out _),
                Is.True);
            progression.ApplyBattleUnitStates(
                new[] { new BattleUnitResult("player.warrior", 3, 4) });
            save.Save(0, "World");

            inventory.Reset();
            progression.Reset();
            Assert.That(save.Load(0), Is.True);

            var restoredItem = inventory.GetItem("persisted.item");
            Assert.That(restoredItem.ItemDefinitionId, Is.EqualTo("item.frost_longsword"));
            Assert.That(restoredItem.ItemLevel, Is.EqualTo(4));
            Assert.That(restoredItem.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(
                restoredItem.Affixes.Select(affix => affix.AffixId),
                Is.EquivalentTo(new[] { "affix.flat_power", "affix.crit_bps" }));
            Assert.That(inventory.IsEquipped("persisted.item"), Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(startingGold + 123));
            Assert.That(inventory.Materials[CraftingService.OreMaterialId], Is.EqualTo(17));

            var warrior = progression.Members.Single(member => member.MemberId == "player.warrior");
            Assert.That(warrior.Level, Is.EqualTo(2));
            Assert.That(warrior.Experience, Is.EqualTo(50));
            Assert.That(warrior.SkillRanks["skill.shield_bash"], Is.EqualTo(1));
            Assert.That(warrior.CurrentHealth, Is.EqualTo(3));
            Assert.That(warrior.CurrentMana, Is.EqualTo(4));

            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var entry = Object.FindAnyObjectByType<WorldBattleEntryView>();
            Assert.That(entry.PartyHealthLabel.text, Does.Contain("player.warrior 3"));
            save.Delete(0);
        }

        [UnityTest]
        public IEnumerator World_OnApplicationPause_AutosavesCurrentState()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var context = GameBootstrapper.Context;
            var save = context.Get<SaveService>();
            var inventory = context.Get<InventoryService>();
            var entry = Object.FindAnyObjectByType<WorldBattleEntryView>();
            save.Delete(0);
            inventory.AddGold(7);

            var primary = save.GetPrimaryPathForTests(0);
            Assert.That(File.Exists(primary), Is.False);
            entry.SendMessage("OnApplicationPause", true);
            Assert.That(File.Exists(primary), Is.True);
            save.Delete(0);
        }

        [UnityTest]
        public IEnumerator World_CanOpenInventoryCraftAndReturnToBattle()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var entry = Object.FindFirstObjectByType<WorldBattleEntryView>();
            entry.InventoryButton.onClick.Invoke();
            Assert.That(entry.InventoryPanel.IsOpen, Is.True);
            entry.InventoryPanel.Close();
            entry.BattleButton.onClick.Invoke();

            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != "Battle"; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Battle"));
        }
    }
}
