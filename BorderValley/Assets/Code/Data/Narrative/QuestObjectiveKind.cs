namespace BorderValley.Data.Narrative
{
    public enum QuestObjectiveKind
    {
        DefeatEnemy,
        SubmitItem,
        ReachLocation,
        TalkToNpc,
        CollectItem = SubmitItem,
        VisitLocation = ReachLocation,
        SpeakToNpc = TalkToNpc
    }
}
