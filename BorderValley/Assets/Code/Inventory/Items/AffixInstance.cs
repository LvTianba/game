namespace BorderValley.Inventory
{
    public sealed class AffixInstance
    {
        public AffixInstance(string affixId, int value)
        {
            AffixId = string.IsNullOrWhiteSpace(affixId)
                ? throw new System.ArgumentException(nameof(affixId))
                : affixId;
            Value = value;
        }

        public string AffixId { get; }
        public int Value { get; }
    }
}
