namespace BorderValley.Data.Narrative
{
    public enum QuestState
    {
        NotStarted,
        Active,
        InProgress = Active,
        ReadyToTurnIn,
        Completed,
        Failed
    }
}
