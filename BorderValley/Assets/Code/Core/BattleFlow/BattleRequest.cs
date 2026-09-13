namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleRequest
    {
        public BattleRequest(string scenarioId, string seed, string returnScene)
        {
            ScenarioId = Require(scenarioId, nameof(scenarioId));
            Seed = Require(seed, nameof(seed));
            ReturnScene = Require(returnScene, nameof(returnScene));
        }

        public string ScenarioId { get; }
        public string Seed { get; }
        public string ReturnScene { get; }

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new System.ArgumentException(name)
                : value;
    }
}
