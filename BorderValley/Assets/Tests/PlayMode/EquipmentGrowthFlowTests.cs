using System.Collections;
using System.IO;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.UI.Battle;
using BorderValley.UI.Inventory;
using BorderValley.UI.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using InventoryTextKeys = BorderValley.UI.Inventory.InventoryTextKeys;

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

        [UnityTest]
        public IEnumerator World_PreparationPanel_EquipCraftReforgeRestAndPersist()
        {
            yield return LoadWorld();

            var context = GameBootstrapper.Context;
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var save = context.Get<SaveService>();
            Assert.That(controller.InventoryButton, Is.Not.Null);
            Assert.That(controller.CraftButton, Is.Not.Null);
            Assert.That(controller.RestButton, Is.Not.Null);
            Assert.That(controller.InventoryPanel, Is.Not.Null);

            controller.InventoryButton.onClick.Invoke();
            var panel = controller.InventoryPanel;
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(controller.InteractButton.interactable, Is.False);

            Assert.That(
                inventory.TryAdd(
                    new ItemInstance(
                        "world.equip",
                        "item.frost_longsword",
                        1,
                        ItemRarity.Common,
                        new AffixInstance[0]),
                    out var equipError),
                Is.True,
                equipError);
            panel.SelectItemForTests("world.equip");
            panel.GetButtonForTests("Equip").onClick.Invoke();
            Assert.That(inventory.IsEquipped("world.equip"), Is.True);

            inventory.AddGold(1000);
            inventory.AddMaterial(CraftingService.OreMaterialId, 100);
            var itemsBeforeCraft = inventory.Items.Count;
            controller.CraftButton.onClick.Invoke();
            Assert.That(panel.Mode, Is.EqualTo(InventoryPanelMode.Craft));
            panel.GetButtonForTests("Craft").onClick.Invoke();
            Assert.That(inventory.Items.Count, Is.EqualTo(itemsBeforeCraft + 1));
            Assert.That(panel.HasUnsavedChangesForTests, Is.False);

            Assert.That(
                inventory.TryAdd(
                    new ItemInstance(
                        "world.reforge",
                        "item.frost_longsword",
                        1,
                        ItemRarity.Rare,
                        new[]
                        {
                            new AffixInstance("affix.flat_power", 3),
                            new AffixInstance("affix.crit_bps", 100)
                        }),
                    out var reforgeError),
                Is.True,
                reforgeError);
            panel.SelectItemForTests("world.reforge");
            var affixesBeforeReforge = inventory.GetItem("world.reforge").Affixes
                .Select(value => (value.AffixId, value.Value))
                .ToArray();
            panel.GetButtonForTests("Reforge").onClick.Invoke();
            var affixesAfterReforge = inventory.GetItem("world.reforge").Affixes
                .Select(value => (value.AffixId, value.Value))
                .ToArray();
            CollectionAssert.AreNotEqual(affixesBeforeReforge, affixesAfterReforge);
            Assert.That(panel.HasUnsavedChangesForTests, Is.False);

            panel.GetButtonForTests("Close").onClick.Invoke();
            progression.ApplyBattleUnitStates(new[]
            {
                new BattleUnitResult("player.warrior", 1, 0)
            });
            var healthBeforeRest = progression.Members
                .Single(member => member.MemberId == "player.warrior")
                .CurrentHealth;
            var attemptsBeforeRest = controller.AutosaveAttemptCount;
            controller.RestButton.onClick.Invoke();
            Assert.That(
                progression.Members.Single(member => member.MemberId == "player.warrior").CurrentHealth,
                Is.GreaterThan(healthBeforeRest));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeRest + 1));

            var savedGold = inventory.Gold;
            inventory.AddGold(7);
            Assert.That(save.Load(0), Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(savedGold));
            Assert.That(inventory.IsEquipped("world.equip"), Is.True);
            Assert.That(inventory.GetItem("world.reforge"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator World_CraftAutosaveFailure_RetainsDirtyStateAndRetryPersists()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var context = GameBootstrapper.Context;
            var save = context.Get<SaveService>();
            save.Delete(0);
            save.Save(0, "World");
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var inventory = context.Get<InventoryService>();
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            inventory.AddGold(1000);
            inventory.AddMaterial(CraftingService.OreMaterialId, 100);
            controller.CraftButton.onClick.Invoke();
            var panel = controller.InventoryPanel;
            var beforeItems = inventory.Items.Count;

            using (new FileStream(save.GetPrimaryPathForTests(0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                panel.GetButtonForTests("Craft").onClick.Invoke();
            }

            Assert.That(inventory.Items.Count, Is.EqualTo(beforeItems + 1));
            Assert.That(panel.HasUnsavedChangesForTests, Is.True);
            Assert.That(panel.LastErrorKeyForTests, Is.EqualTo(InventoryTextKeys.AutoSaveFailed));

            panel.GetButtonForTests("RetrySave").onClick.Invoke();
            Assert.That(panel.HasUnsavedChangesForTests, Is.False);
            Assert.That(panel.LastErrorKeyForTests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator World_PreparationPanel_CanEnterBattleAndReturnToWorld()
        {
            yield return LoadWorld();
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            controller.InventoryButton.onClick.Invoke();
            Assert.That(controller.InventoryPanel.IsOpen, Is.True);
            controller.InventoryPanel.GetButtonForTests("Close").onClick.Invoke();

            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            var encounter = catalog.All.OfType<WorldEncounterDefinition>()
                .Single(value => value.EncounterId == "encounter.forest.bandits");
            Assert.That(controller.BeginEncounter(encounter), Is.True);
            yield return WaitForScene("Battle");
            yield return null;

            var battle = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(battle, Is.Not.Null);
            KillAllEnemies(battle);
            Object.FindAnyObjectByType<BattleHudView>().ContinueButton.onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller.InventoryButton, Is.Not.Null);
            Assert.That(controller.CraftButton, Is.Not.Null);
            Assert.That(controller.RestButton, Is.Not.Null);
            Assert.That(controller.InventoryPanel, Is.Not.Null);
        }

        private static IEnumerator LoadWorld()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingAutosave_RetriesOnFixedIntervalInsteadOfEveryTick()
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
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                1,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));

            using (new FileStream(save.GetPrimaryPathForTests(0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(controller.ProcessPendingBattleResult(), Is.False);
                Assert.That(controller.HasPendingSettlement, Is.True);
                var attemptsAfterFailure = controller.AutosaveAttemptCount;

                for (var index = 0; index < 5; index++)
                    controller.Tick(0.1f);
                Assert.That(
                    controller.AutosaveAttemptCount,
                    Is.EqualTo(attemptsAfterFailure),
                    "Ticks inside the backoff window must not retry the autosave.");

                controller.Tick(1f);
                Assert.That(
                    controller.AutosaveAttemptCount,
                    Is.EqualTo(attemptsAfterFailure + 1),
                    "One retry is expected once the fixed interval elapses.");

                controller.HandleApplicationPause(true);
                Assert.That(
                    controller.AutosaveAttemptCount,
                    Is.EqualTo(attemptsAfterFailure + 2),
                    "Pause is a key event and must retry immediately.");
            }

            Assert.That(controller.ProcessPendingBattleResult(), Is.True);
            Assert.That(controller.HasPendingSettlement, Is.False);
        }

        private static void KillAllEnemies(BattleSceneController controller)
        {
            foreach (var enemy in controller.EngineForTests.State.Units
                         .Where(unit => unit.Team == BorderValley.Battle.Domain.Team.Enemy)
                         .ToArray())
            {
                enemy.ApplyRawDamage(int.MaxValue);
            }

            controller.EngineForTests.Execute(null);
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != sceneName; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }
    }
}
