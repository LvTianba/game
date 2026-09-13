using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json.Linq;

namespace BorderValley.Inventory
{
    public sealed class InventoryService : ISaveParticipant
    {
        private readonly Dictionary<string, ItemInstance> items = new(StringComparer.Ordinal);
        private readonly Dictionary<ItemSlot, string> equipped = new();
        private readonly Dictionary<string, int> materials = new(StringComparer.Ordinal);

        public InventoryService(
            int capacity,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            int startingGold)
        {
            Capacity = Math.Max(1, capacity);
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            Gold = Math.Max(0, startingGold);
        }

        public event Action Changed;
        public string Key => "inventory";
        public int Capacity { get; }
        public int Gold { get; private set; }
        public IReadOnlyDictionary<string, ItemDefinition> Definitions { get; }
        public IReadOnlyDictionary<ItemSlot, string> Equipped => equipped;
        public IReadOnlyDictionary<string, int> Materials => materials;
        public IReadOnlyList<ItemInstance> Items =>
            items.Values.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();

        public ItemInstance GetItem(string instanceId) =>
            items.TryGetValue(instanceId, out var item)
                ? item
                : throw new KeyNotFoundException($"Unknown item instance: {instanceId}");

        public bool IsEquipped(string instanceId) => equipped.Values.Contains(instanceId, StringComparer.Ordinal);

        public bool TryAdd(ItemInstance item, out string error)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (items.Count >= Capacity) { error = InventoryTextKeys.BagFull; return false; }
            if (items.ContainsKey(item.InstanceId)) { error = InventoryTextKeys.DuplicateInstance; return false; }
            if (!Definitions.ContainsKey(item.ItemDefinitionId)) { error = InventoryTextKeys.UnknownDefinition; return false; }
            items.Add(item.InstanceId, item);
            error = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryEquip(string instanceId, string classId, out string error)
        {
            if (!items.TryGetValue(instanceId, out var item)) { error = InventoryTextKeys.ItemMissing; return false; }
            var definition = Definitions[item.ItemDefinitionId];
            if (!definition.AllowsClass(classId)) { error = InventoryTextKeys.ClassRestricted; return false; }
            if (equipped.TryGetValue(definition.Slot, out _)) equipped.Remove(definition.Slot);
            equipped[definition.Slot] = instanceId;
            error = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip(ItemSlot slot, out string error)
        {
            if (equipped.Remove(slot)) { error = string.Empty; Changed?.Invoke(); return true; }
            error = InventoryTextKeys.SlotEmpty;
            return false;
        }

        public bool TryRemove(string instanceId)
        {
            if (IsEquipped(instanceId) || !items.Remove(instanceId)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddGold(int amount)
        {
            Gold += Math.Max(0, amount);
            Changed?.Invoke();
        }

        public bool TrySpendMaterial(string materialId, int amount)
        {
            if (amount < 0 || !materials.TryGetValue(materialId, out var current) || current < amount) return false;
            materials[materialId] = current - amount;
            Changed?.Invoke();
            return true;
        }

        public bool TryAddMaterial(string materialId, int amount)
        {
            if (string.IsNullOrWhiteSpace(materialId) || amount <= 0) return false;
            AddMaterial(materialId, amount);
            return true;
        }

        public void AddMaterial(string materialId, int amount)
        {
            if (string.IsNullOrWhiteSpace(materialId) || amount <= 0) return;
            materials[materialId] = materials.GetValueOrDefault(materialId) + amount;
            Changed?.Invoke();
        }

        public JObject Capture() => new()
        {
            ["gold"] = Gold,
            ["equipped"] = JObject.FromObject(equipped),
            ["materials"] = JObject.FromObject(materials),
            ["items"] = new JArray(items.Values.OrderBy(value => value.InstanceId, StringComparer.Ordinal).Select(ToJson))
        };

        public void Restore(JObject state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Reset();
            Gold = Math.Max(0, state.Value<int>("gold"));
            foreach (var pair in state["materials"]?.ToObject<Dictionary<string, int>>() ?? new())
                materials[pair.Key] = Math.Max(0, pair.Value);
            foreach (var token in state["items"] as JArray ?? new JArray())
            {
                var item = FromJson((JObject)token);
                if (Definitions.ContainsKey(item.ItemDefinitionId)) items[item.InstanceId] = item;
            }
            foreach (var pair in state["equipped"]?.ToObject<Dictionary<ItemSlot, string>>() ?? new())
                if (items.ContainsKey(pair.Value)) equipped[pair.Key] = pair.Value;
            Changed?.Invoke();
        }

        public void RestoreContext(string sceneName) { }

        public void Reset()
        {
            items.Clear();
            equipped.Clear();
            materials.Clear();
            Gold = 0;
            Changed?.Invoke();
        }

        private static JObject ToJson(ItemInstance item) => new()
        {
            ["instanceId"] = item.InstanceId,
            ["definitionId"] = item.ItemDefinitionId,
            ["itemLevel"] = item.ItemLevel,
            ["rarity"] = item.Rarity.ToString(),
            ["affixes"] = new JArray(item.Affixes.Select(affix => new JObject
            {
                ["id"] = affix.AffixId,
                ["value"] = affix.Value
            }))
        };

        private static ItemInstance FromJson(JObject value) => new(
            value.Value<string>("instanceId"),
            value.Value<string>("definitionId"),
            value.Value<int>("itemLevel"),
            Enum.Parse<ItemRarity>(value.Value<string>("rarity")),
            (value["affixes"] as JArray ?? new JArray()).Select(affix => new AffixInstance(
                affix.Value<string>("id"), affix.Value<int>("value"))));
    }
}
