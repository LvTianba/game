using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public interface IQuestRewardService
    {
        bool TryValidate(QuestDefinition quest, out string error);
        bool TryApply(QuestDefinition quest, out string error);
    }

    public sealed class QuestRewardService : IQuestRewardService
    {
        private readonly InventoryService inventory;
        private readonly PartyProgressionService progression;
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly NarrativeStateService state;

        public QuestRewardService(
            InventoryService inventory,
            PartyProgressionService progression,
            IReadOnlyDictionary<string, ItemDefinition> items,
            NarrativeStateService state)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progression = progression;
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool TryValidate(QuestDefinition quest, out string error)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.Id))
            {
                error = NarrativeTextKeys.QuestRewardInvalid;
                return false;
            }

            var equipmentCount = 0;
            for (var index = 0; index < quest.Rewards.Length; index++)
            {
                var reward = quest.Rewards[index];
                if (reward == null || reward.Amount <= 0)
                {
                    error = NarrativeTextKeys.QuestRewardInvalid;
                    return false;
                }

                switch (reward.Kind)
                {
                    case QuestRewardKind.Gold:
                        break;
                    case QuestRewardKind.Experience:
                        if (progression == null)
                        {
                            error = NarrativeTextKeys.QuestRewardProgressionMissing;
                            return false;
                        }
                        break;
                    case QuestRewardKind.Material:
                        if (!items.ContainsKey(reward.TargetId))
                        {
                            error = NarrativeTextKeys.QuestRewardItemMissing;
                            return false;
                        }
                        break;
                    case QuestRewardKind.Equipment:
                        if (!items.ContainsKey(reward.TargetId))
                        {
                            error = NarrativeTextKeys.QuestRewardItemMissing;
                            return false;
                        }
                        equipmentCount += reward.Amount;
                        for (var itemIndex = 0; itemIndex < reward.Amount; itemIndex++)
                        {
                            var instanceId = EquipmentInstanceId(quest.Id, index, itemIndex);
                            if (inventory.Items.Any(item =>
                                    string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal)))
                            {
                                error = NarrativeTextKeys.QuestRewardItemMissing;
                                return false;
                            }
                        }
                        break;
                    case QuestRewardKind.UnlockShop:
                        if (!state.HasShop(reward.TargetId))
                        {
                            error = NarrativeTextKeys.QuestRewardShopMissing;
                            return false;
                        }
                        break;
                    default:
                        error = NarrativeTextKeys.QuestRewardInvalid;
                        return false;
                }
            }

            if (equipmentCount > inventory.Capacity)
            {
                error = NarrativeTextKeys.QuestInventoryFull;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryApply(QuestDefinition quest, out string error)
        {
            if (!TryValidate(quest, out error)) return false;

            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression?.Capture();
            var stateSnapshot = state.Capture();
            try
            {
                for (var index = 0; index < quest.Rewards.Length; index++)
                {
                    var reward = quest.Rewards[index];
                    switch (reward.Kind)
                    {
                        case QuestRewardKind.Gold:
                            inventory.AddGold(reward.Amount);
                            break;
                        case QuestRewardKind.Experience:
                            progression.AwardExperience(reward.Amount);
                            break;
                        case QuestRewardKind.Material:
                            inventory.AddMaterial(reward.TargetId, reward.Amount);
                            break;
                        case QuestRewardKind.Equipment:
                            for (var itemIndex = 0; itemIndex < reward.Amount; itemIndex++)
                            {
                                if (!inventory.TryAdd(
                                        new ItemInstance(
                                            EquipmentInstanceId(quest.Id, index, itemIndex),
                                            reward.TargetId,
                                            1,
                                            ItemRarity.Common,
                                            Array.Empty<AffixInstance>()),
                                        out error))
                                    throw new InvalidOperationException(error);
                            }
                            break;
                        case QuestRewardKind.UnlockShop:
                            if (!state.MarkShopUnlocked(reward.TargetId))
                                throw new InvalidOperationException(NarrativeTextKeys.QuestRewardShopMissing);
                            break;
                    }
                }

                error = string.Empty;
                return true;
            }
            catch
            {
                if (progressionSnapshot != null) progression.Restore(progressionSnapshot);
                state.Restore(stateSnapshot);
                inventory.Restore(inventorySnapshot);
                error = NarrativeTextKeys.QuestRewardApplyFailed;
                return false;
            }
        }

        private static string EquipmentInstanceId(string questId, int rewardIndex, int itemIndex) =>
            "quest-reward:" + questId + ":" + rewardIndex + ":" + itemIndex;
    }
}
