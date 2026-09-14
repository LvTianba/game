using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;

namespace BorderValley.World
{
    public sealed class WorldBattleSettlementService
    {
        private readonly InventoryService inventory;
        private readonly PartyProgressionService progression;
        private readonly LootGenerator lootGenerator;
        private readonly QuestService questService;
        private readonly NarrativeStateService narrativeState;
        private readonly Func<string, ItemDropTableDefinition> rewardTableResolver;
        private readonly IRandomSource random;
        private readonly ItemDropTableDefinition legacyRewardTable;

        public WorldBattleSettlementService(
            InventoryService inventory,
            PartyProgressionService progression,
            LootGenerator lootGenerator,
            QuestService questService,
            NarrativeStateService narrativeState,
            Func<string, ItemDropTableDefinition> rewardTableResolver,
            IRandomSource random,
            ItemDropTableDefinition legacyRewardTable = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.lootGenerator = lootGenerator ?? throw new ArgumentNullException(nameof(lootGenerator));
            this.questService = questService;
            this.narrativeState = narrativeState;
            this.rewardTableResolver = rewardTableResolver ?? throw new ArgumentNullException(nameof(rewardTableResolver));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.legacyRewardTable = legacyRewardTable;
        }

        public bool Settle(
            BattleResult result,
            WorldEncounterDefinition encounter,
            out WorldBattleSettlementResult settlement)
        {
            if (result == null)
            {
                settlement = WorldBattleSettlementResult.Failure(
                    WorldBattleSettlementResult.InvalidInputKey);
                return false;
            }

            return result.Outcome switch
            {
                BattleFlowOutcome.PlayerVictory => SettleVictory(result, encounter, out settlement),
                BattleFlowOutcome.EnemyVictory => SettleDefeat(result, out settlement),
                _ => Fail(WorldBattleSettlementResult.InvalidInputKey, out settlement)
            };
        }

        private bool SettleVictory(
            BattleResult result,
            WorldEncounterDefinition encounter,
            out WorldBattleSettlementResult settlement)
        {
            var context = result.Context;
            var legacySettlement = context == null;
            if (!legacySettlement &&
                (encounter == null ||
                 questService == null ||
                 narrativeState == null ||
                 !string.Equals(context.EncounterId, encounter.EncounterId, StringComparison.Ordinal)))
            {
                return Fail(WorldBattleSettlementResult.InvalidInputKey, out settlement);
            }

            var repeatable = context?.Repeatable ?? encounter?.Repeatable ?? true;
            var completionEventId = encounter?.CompletionEventId ?? string.Empty;
            if (!repeatable && narrativeState == null)
                return Fail(WorldBattleSettlementResult.InvalidInputKey, out settlement);
            if (!repeatable &&
                !string.IsNullOrWhiteSpace(completionEventId) &&
                narrativeState.HasEvent(completionEventId))
            {
                settlement = WorldBattleSettlementResult.NoOp();
                return true;
            }
            if (!repeatable && string.IsNullOrWhiteSpace(completionEventId))
                return Fail(WorldBattleSettlementResult.CompletionFailedKey, out settlement);

            var rewardTable = legacySettlement
                ? legacyRewardTable
                : rewardTableResolver(context.RewardTableId);
            if (rewardTable == null)
                return Fail(WorldBattleSettlementResult.MissingRewardTableKey, out settlement);

            var loot = lootGenerator.Generate(
                CreateLootInstanceId(context, encounter),
                rewardTable,
                progression.HighestLevel,
                random);
            if (!CanAdd(loot, out var addError))
                return Fail(addError, out settlement);

            var goldReward = context?.GoldReward ?? 25 + result.Rounds * 5;
            var experienceReward = context?.ExperienceReward ?? 35 + result.Rounds * 5;
            var defeatedEnemyIds = DefeatedEnemyIds(result);
            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression.Capture();
            var narrativeSnapshot = legacySettlement ? null : narrativeState.Capture();

            try
            {
                if (!inventory.TryAdd(loot, out addError))
                {
                    Restore(inventorySnapshot, progressionSnapshot, narrativeSnapshot);
                    return Fail(addError, out settlement);
                }

                inventory.AddGold(goldReward);
                progression.AwardExperience(experienceReward);
                progression.ApplyBattleUnitStates(result.UnitStates);

                if (!legacySettlement)
                {
                    foreach (var enemyDefinitionId in defeatedEnemyIds)
                    {
                        if (questService.RecordBattleDefeat(enemyDefinitionId, out var questError))
                            continue;

                        Restore(inventorySnapshot, progressionSnapshot, narrativeSnapshot);
                        return Fail(questError, out settlement);
                    }
                }

                if (!repeatable && !narrativeState.SetEvent(completionEventId))
                {
                    Restore(inventorySnapshot, progressionSnapshot, narrativeSnapshot);
                    return Fail(NarrativeTextKeys.UnknownEvent, out settlement);
                }

                settlement = WorldBattleSettlementResult.Applied(
                    true,
                    goldReward,
                    experienceReward,
                    defeatedEnemyIds);
                return true;
            }
            catch
            {
                Restore(inventorySnapshot, progressionSnapshot, narrativeSnapshot);
                throw;
            }
        }

        private bool SettleDefeat(
            BattleResult result,
            out WorldBattleSettlementResult settlement)
        {
            var progressionSnapshot = progression.Capture();
            try
            {
                progression.ApplyBattleUnitStates(result.UnitStates);
                progression.ReturnToSafePoint();
                settlement = WorldBattleSettlementResult.Applied(
                    false,
                    0,
                    0,
                    Array.Empty<string>());
                return true;
            }
            catch
            {
                progression.Restore(progressionSnapshot);
                throw;
            }
        }

        private bool CanAdd(ItemInstance loot, out string error)
        {
            if (inventory.Items.Count >= inventory.Capacity)
            {
                error = BorderValley.Inventory.InventoryTextKeys.BagFull;
                return false;
            }
            if (inventory.Items.Any(item =>
                    string.Equals(item.InstanceId, loot.InstanceId, StringComparison.Ordinal)))
            {
                error = BorderValley.Inventory.InventoryTextKeys.DuplicateInstance;
                return false;
            }
            if (!inventory.Definitions.ContainsKey(loot.ItemDefinitionId))
            {
                error = BorderValley.Inventory.InventoryTextKeys.UnknownDefinition;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private string CreateLootInstanceId(
            BattleContext context,
            WorldEncounterDefinition encounter)
        {
            var encounterId = context?.EncounterId ?? encounter?.EncounterId ?? "legacy";
            var rewardTableId = context?.RewardTableId ??
                                legacyRewardTable?.Id ??
                                encounter?.RewardTableId ??
                                "legacy";
            return "world-loot:" + encounterId + ":" + rewardTableId + ":" +
                   random.NextUInt().ToString("x8");
        }

        private static IReadOnlyList<string> DefeatedEnemyIds(BattleResult result) =>
            result.UnitStates
                .Where(state => state != null && !string.IsNullOrWhiteSpace(state.DefinitionId))
                .Select(state => state.DefinitionId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        private void Restore(
            Newtonsoft.Json.Linq.JObject inventorySnapshot,
            Newtonsoft.Json.Linq.JObject progressionSnapshot,
            Newtonsoft.Json.Linq.JObject narrativeSnapshot)
        {
            inventory.Restore(inventorySnapshot);
            progression.Restore(progressionSnapshot);
            if (narrativeSnapshot != null)
                narrativeState.Restore(narrativeSnapshot);
        }

        private static bool Fail(
            string errorKey,
            out WorldBattleSettlementResult settlement)
        {
            settlement = WorldBattleSettlementResult.Failure(errorKey);
            return false;
        }
    }
}
