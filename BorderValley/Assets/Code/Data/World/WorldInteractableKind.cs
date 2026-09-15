namespace BorderValley.Data.World
{
    public enum WorldInteractableKind
    {
        Npc,
        AreaExit,
        Chest,
        Gather,
        Investigate,
        Encounter,
        Exit = AreaExit,
        Treasure = Chest,
        Collection = Gather
    }
}
