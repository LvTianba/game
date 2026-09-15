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
            string seed) =>
            BuildRequest(encounter, party, seed, WorldReturnScene);

        public static BattleRequest BuildRequest(
            WorldEncounterDefinition encounter,
            BattlePartySnapshot party,
            string seed,
            string returnScene)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (string.IsNullOrWhiteSpace(returnScene))
                throw new ArgumentException("Return scene cannot be empty.", nameof(returnScene));

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
                returnScene,
                party,
                context);
        }
    }
}
