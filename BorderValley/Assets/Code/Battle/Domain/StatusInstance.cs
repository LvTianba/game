namespace BorderValley.Battle.Domain
{
    public sealed class StatusInstance
    {
        public StatusInstance(StatusType type, int magnitude, int remainingTurns, string sourceUnitId)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTurns = remainingTurns;
            SourceUnitId = sourceUnitId;
        }

        public StatusType Type { get; }
        public int Magnitude { get; internal set; }
        public int RemainingTurns { get; internal set; }
        public string SourceUnitId { get; }
    }
}

