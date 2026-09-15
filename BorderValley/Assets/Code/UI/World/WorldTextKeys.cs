using BorderValley.Data.Narrative;
using BorderValley.Inventory;
using BorderValley.Narrative;

namespace BorderValley.UI.World
{
    public static class WorldTextKeys
    {
        public const string UnknownNpc = NarrativeTextKeys.UnknownNpc;
        public const string UnknownShop = NarrativeTextKeys.UnknownShop;
        public const string UnknownOffer = NarrativeTextKeys.UnknownOffer;
        public const string UnknownItem = InventoryTextKeys.ItemMissing;
        public const string ServiceUnavailable = "world.ui.error.service_unavailable";
        public const string PendingSettlement = "world.ui.error.pending_settlement";

        public const string DialoguePanelTitle = "world.ui.dialogue.title";
        public const string DialogueSpeakerUnknown = "world.ui.dialogue.speaker.unknown";
        public const string DialogueContinue = "world.ui.dialogue.continue";
        public const string DialogueClose = "world.ui.dialogue.close";

        public const string ShopTitle = "world.ui.shop.title";
        public const string ShopBuyTab = "world.ui.shop.buy_tab";
        public const string ShopSellTab = "world.ui.shop.sell_tab";
        public const string ShopGold = "world.ui.shop.gold";
        public const string ShopBuy = "world.ui.shop.buy";
        public const string ShopSell = "world.ui.shop.sell";
        public const string ShopBuyPrice = "world.ui.shop.buy_price";
        public const string ShopSellPrice = "world.ui.shop.sell_price";
        public const string ShopEmpty = "world.ui.shop.empty";
        public const string ShopClose = "world.ui.shop.close";

        public const string QuestLogTitle = "world.ui.quest_log.title";
        public const string QuestLogClose = "world.ui.quest_log.close";
        public const string QuestLogEmpty = "world.ui.quest_log.empty";
        public const string QuestStateActive = "world.ui.quest.state.active";
        public const string QuestStateReadyToTurnIn = "world.ui.quest.state.ready_to_turn_in";
        public const string QuestStateCompleted = "world.ui.quest.state.completed";
        public const string QuestStateFailed = "world.ui.quest.state.failed";
        public const string QuestObjectiveProgress = "world.ui.quest.objective.progress";
        public const string QuestRewardGold = "world.ui.quest.reward.gold";
        public const string QuestRewardExperience = "world.ui.quest.reward.experience";
        public const string QuestRewardEquipment = "world.ui.quest.reward.equipment";
        public const string QuestRewardMaterial = "world.ui.quest.reward.material";
        public const string QuestRewardUnlockShop = "world.ui.quest.reward.unlock_shop";
        public const string QuestRewardUnknown = "world.ui.quest.reward.unknown";

        public static string QuestStateKey(QuestState state) =>
            state switch
            {
                QuestState.Active => QuestStateActive,
                QuestState.ReadyToTurnIn => QuestStateReadyToTurnIn,
                QuestState.Completed => QuestStateCompleted,
                QuestState.Failed => QuestStateFailed,
                _ => string.Empty
            };

        public static string QuestRewardKey(QuestRewardKind kind) =>
            kind switch
            {
                QuestRewardKind.Gold => QuestRewardGold,
                QuestRewardKind.Experience => QuestRewardExperience,
                QuestRewardKind.Equipment => QuestRewardEquipment,
                QuestRewardKind.Material => QuestRewardMaterial,
                QuestRewardKind.UnlockShop => QuestRewardUnlockShop,
                _ => QuestRewardUnknown
            };

        public static string QuestRewardKey(QuestRewardDefinition reward) =>
            reward == null ? QuestRewardUnknown : QuestRewardKey(reward.Kind);

        public static string NpcSpeakerKey(string npcId) =>
            string.IsNullOrWhiteSpace(npcId) ? DialogueSpeakerUnknown : npcId + ".name";
    }
}
