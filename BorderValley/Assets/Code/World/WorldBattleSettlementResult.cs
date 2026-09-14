using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.World
{
    public sealed class WorldBattleSettlementResult
    {
        public const string InvalidInputKey = "world.battle_settlement.invalid_input";
        public const string MissingRewardTableKey = "world.battle_settlement.reward_table_missing";
        public const string RewardApplyFailedKey = "world.battle_settlement.reward_apply_failed";
        public const string CompletionFailedKey = "world.battle_settlement.completion_failed";

        private WorldBattleSettlementResult(
            bool success,
            bool requiresAutosave,
            bool lootAdded,
            int goldAwarded,
            int experienceAwarded,
            IEnumerable<string> defeatedEnemyIds,
            string errorKey)
        {
            Success = success;
            RequiresAutosave = requiresAutosave;
            LootAdded = lootAdded;
            GoldAwarded = goldAwarded;
            ExperienceAwarded = experienceAwarded;
            DefeatedEnemyIds = (defeatedEnemyIds ?? Array.Empty<string>()).ToArray();
            ErrorKey = errorKey ?? string.Empty;
        }

        public bool Success { get; }
        public bool RequiresAutosave { get; }
        public bool LootAdded { get; }
        public int GoldAwarded { get; }
        public int ExperienceAwarded { get; }
        public IReadOnlyList<string> DefeatedEnemyIds { get; }
        public string ErrorKey { get; }

        public static WorldBattleSettlementResult NoOp()
        {
            return new WorldBattleSettlementResult(
                true,
                false,
                false,
                0,
                0,
                Array.Empty<string>(),
                string.Empty);
        }

        public static WorldBattleSettlementResult Applied(
            bool lootAdded,
            int goldAwarded,
            int experienceAwarded,
            IEnumerable<string> defeatedEnemyIds)
        {
            return new WorldBattleSettlementResult(
                true,
                true,
                lootAdded,
                Math.Max(0, goldAwarded),
                Math.Max(0, experienceAwarded),
                defeatedEnemyIds,
                string.Empty);
        }

        public static WorldBattleSettlementResult Failure(string errorKey)
        {
            return new WorldBattleSettlementResult(
                false,
                false,
                false,
                0,
                0,
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(errorKey) ? RewardApplyFailedKey : errorKey);
        }
    }
}
