using System;
using BorderValley.Core.BattleFlow;
using BorderValley.Data.World;

namespace BorderValley.World
{
    public static class WorldEncounterService
    {
        public const string WorldReturnScene = "World";

        public static BattleRequest BuildRequest(
            WorldEncounterDefinition encounter,
            BattlePartySnapshot party,
            string seed)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (party == null) throw new ArgumentNullException(nameof(party));

            var context = new BattleContext(
                encounter.EncounterId,
                encounter.RewardTableId,
                encounter.GoldReward,
                encounter.ExperienceReward,
                encounter.EnemyDefinitionIds,
                encounter.Repeatable);
            return new BattleRequest(
                encounter.ScenarioId,
                seed,
                WorldReturnScene,
                party,
                context);
        }
    }
}
