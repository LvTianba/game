using System.Collections;
using System.IO;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using BorderValley.Inventory;
using BorderValley.UI.World;
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
            yield return LoadWorld();

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            var beforeGold = inventory.Gold;
            var beforeItems = inventory.Items.Count;

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                4,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));

            Assert.That(controller.ProcessPendingBattleResult(), Is.True);
            Assert.That(progression.Members.Any(member => member.Experience > 0), Is.True);
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(beforeItems));
        }

        [UnityTest]
        public IEnumerator World_EnemyVictory_ReturnsToSafePointAndClearsWipeFlag()
        {
            yield return LoadWorld();

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var progression = context.Get<PartyProgressionService>();
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.EnemyVictory,
                2,
                new[]
                {
                    new BattleUnitResult("player.warrior", 0, 0),
                    new BattleUnitResult("player.ranger", 0, 0),
                    new BattleUnitResult("player.mage", 0, 0)
                }));

            Assert.That(controller.ProcessPendingBattleResult(), Is.True);
            Assert.That(progression.SafePointId, Is.EqualTo(PartyProgressionService.DefaultSafePointId));
            Assert.That(progression.HasPendingWipeReturn, Is.False);
            Assert.That(controller.LastBattleResultKey, Is.EqualTo("battle.result.enemy_victory"));
        }

        [UnityTest]
        public IEnumerator World_AutosaveFailure_KeepsRewardsAndRetriesWithoutResettling()
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
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            var beforeGold = inventory.Gold;
            var beforeItems = inventory.Items.Count;

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                1,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));

            using (new FileStream(save.GetPrimaryPathForTests(0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(controller.ProcessPendingBattleResult(), Is.False);
            }

            Assert.That(controller.LastErrorKey, Is.EqualTo(BorderValley.UI.Inventory.InventoryTextKeys.AutoSaveFailed));
            Assert.That(controller.HasPendingSettlement, Is.True);
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(beforeItems));
            Assert.That(progression.Members.Any(member => member.Experience > 0), Is.True);
            var settledGold = inventory.Gold;
            var settledItems = inventory.Items.Count;
            var settledExperience = progression.TotalExperience;

            Assert.That(controller.ProcessPendingBattleResult(), Is.True);
            Assert.That(controller.HasPendingSettlement, Is.False);
            Assert.That(controller.SettlementCount, Is.EqualTo(1));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(2));
            Assert.That(inventory.Gold, Is.EqualTo(settledGold));
            Assert.That(inventory.Items.Count, Is.EqualTo(settledItems));
            Assert.That(progression.TotalExperience, Is.EqualTo(settledExperience));
        }

        [UnityTest]
        public IEnumerator World_FullBagSettlementFailure_RetriesSameResultWithoutDoubleReward()
        {
            yield return LoadWorld();

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            for (var index = 0; index < inventory.Capacity; index++)
            {
                Assert.That(
                    inventory.TryAdd(
                        new ItemInstance(
                            "fullbag." + index,
                            "item.wooden_buckler",
                            1,
                            ItemRarity.Common,
                            new AffixInstance[0]),
                        out var error),
                    Is.True,
                    error);
            }

            var goldBefore = inventory.Gold;
            var experienceBefore = progression.TotalExperience;
            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                2,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));

            Assert.That(controller.ProcessPendingBattleResult(), Is.False);
            Assert.That(controller.HasPendingSettlement, Is.True);
            Assert.That(controller.SettlementCount, Is.EqualTo(0));
            Assert.That(inventory.Gold, Is.EqualTo(goldBefore));
            Assert.That(progression.TotalExperience, Is.EqualTo(experienceBefore));
            Assert.That(inventory.Items.Count, Is.EqualTo(inventory.Capacity));

            Assert.That(inventory.TryRemove("fullbag.0"), Is.True);
            Assert.That(controller.ProcessPendingBattleResult(), Is.True);
            Assert.That(controller.HasPendingSettlement, Is.False);
            Assert.That(controller.SettlementCount, Is.EqualTo(1));
            Assert.That(inventory.Gold, Is.GreaterThan(goldBefore));
            Assert.That(progression.TotalExperience, Is.GreaterThan(experienceBefore));
            Assert.That(inventory.Items.Count, Is.EqualTo(inventory.Capacity));

            var settledGold = inventory.Gold;
            var settledItems = inventory.Items.Count;
            var settledExperience = progression.TotalExperience;
            Assert.That(controller.ProcessPendingBattleResult(), Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(settledGold));
            Assert.That(inventory.Items.Count, Is.EqualTo(settledItems));
            Assert.That(progression.TotalExperience, Is.EqualTo(settledExperience));
        }
        private static IEnumerator LoadWorld()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;
        }
    }
}
