namespace BorderValley.Core.Random
{
    public interface IRandomSource
    {
        uint NextUInt();
        int Range(int minInclusive, int maxExclusive);
        float Value01();
        IRandomSource Fork(string label);
    }
}
