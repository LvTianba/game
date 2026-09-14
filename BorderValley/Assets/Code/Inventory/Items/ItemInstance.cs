namespace BorderValley.Inventory
{
    public sealed class ItemInstance
    {
        public ItemInstance(
            string instanceId,
            string itemDefinitionId,
            int itemLevel,
            Data.Items.ItemRarity rarity,
            System.Collections.Generic.IEnumerable<AffixInstance> affixes)
        {
            InstanceId = Require(instanceId, nameof(instanceId));
            ItemDefinitionId = Require(itemDefinitionId, nameof(itemDefinitionId));
            ItemLevel = System.Math.Clamp(itemLevel, 1, 10);
            Rarity = rarity;
            Affixes = Copy(affixes);
        }

        public string InstanceId { get; }
        public string ItemDefinitionId { get; }
        public int ItemLevel { get; }
        public Data.Items.ItemRarity Rarity { get; }
        public System.Collections.Generic.IReadOnlyList<AffixInstance> Affixes { get; private set; }

        public void ReplaceAffixes(System.Collections.Generic.IEnumerable<AffixInstance> values) =>
            Affixes = Copy(values);

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new System.ArgumentException(name) : value;

        private static System.Collections.Generic.IReadOnlyList<AffixInstance> Copy(
            System.Collections.Generic.IEnumerable<AffixInstance> values)
        {
            var result = new System.Collections.Generic.List<AffixInstance>(
                values ?? System.Array.Empty<AffixInstance>());
            if (result.Exists(value => value == null)) throw new System.ArgumentException("Affixes cannot contain null.");
            return result.AsReadOnly();
        }
    }
}
