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
        private const string LegacyMemberId = "__legacy";
        private static readonly IReadOnlyDictionary<ItemSlot, string> EmptyEquipment =
            new Dictionary<ItemSlot, string>();

        private readonly Dictionary<string, ItemInstance> items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<ItemSlot, string>> equippedByMember =
            new(StringComparer.Ordinal);
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
        public IReadOnlyDictionary<ItemSlot, string> Equipped => GetEquipped(null);
        public IReadOnlyDictionary<string, int> Materials => materials;
        public IReadOnlyList<ItemInstance> Items =>
            items.Values.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();

        public ItemInstance GetItem(string instanceId) =>
            items.TryGetValue(instanceId, out var item)
                ? item
                : throw new KeyNotFoundException($"Unknown item instance: {instanceId}");

        public IReadOnlyDictionary<ItemSlot, string> GetEquipped(string memberId) =>
            equippedByMember.TryGetValue(NormalizeMemberId(memberId), out var loadout)
                ? loadout
                : EmptyEquipment;

        public bool IsEquipped(string instanceId) =>
            equippedByMember.Values.Any(loadout => loadout.Values.Contains(instanceId, StringComparer.Ordinal));

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

        public bool TryEquip(string instanceId, string classId, out string error) =>
            TryEquip(instanceId, null, classId, out error);

        public bool TryEquip(string instanceId, string memberId, string classId, out string error)
        {
            if (!items.TryGetValue(instanceId, out var item)) { error = InventoryTextKeys.ItemMissing; return false; }
            var definition = Definitions[item.ItemDefinitionId];
            if (!definition.AllowsClass(classId)) { error = InventoryTextKeys.ClassRestricted; return false; }

            var normalizedMemberId = NormalizeMemberId(memberId);
            if (!equippedByMember.TryGetValue(normalizedMemberId, out var loadout))
            {
                loadout = new Dictionary<ItemSlot, string>();
                equippedByMember[normalizedMemberId] = loadout;
            }

            if (IsEquipped(instanceId) &&
                (!loadout.TryGetValue(definition.Slot, out var current) || current != instanceId))
            {
                error = InventoryTextKeys.AlreadyEquipped;
                return false;
            }

            if (loadout.TryGetValue(definition.Slot, out current) && current == instanceId)
            {
                error = string.Empty;
                return true;
            }

            loadout[definition.Slot] = instanceId;
            error = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip(ItemSlot slot, out string error) =>
            TryUnequip(null, slot, out error);

        public bool TryUnequip(string memberId, ItemSlot slot, out string error)
        {
            var normalizedMemberId = NormalizeMemberId(memberId);
            if (equippedByMember.TryGetValue(normalizedMemberId, out var loadout) && loadout.Remove(slot))
            {
                if (loadout.Count == 0) equippedByMember.Remove(normalizedMemberId);
                error = string.Empty;
                Changed?.Invoke();
                return true;
            }

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
            ["equipped"] = JObject.FromObject(GetEquipped(null)),
            ["equippedByMember"] = new JObject(
                equippedByMember
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new JProperty(pair.Key, JObject.FromObject(pair.Value)))),
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
                if (Definitions.ContainsKey(item.ItemDefinitionId))
                    items[item.InstanceId] = item;
            }

            var loadedByMember = false;
            if (state["equippedByMember"] is JObject byMember)
            {
                foreach (var property in byMember.Properties())
                {
                    if (!(property.Value is JObject loadout)) continue;
                    RestoreLoadout(property.Name, loadout);
                    loadedByMember = true;
                }
            }

            if (!loadedByMember && state["equipped"] is JObject legacy)
                RestoreLoadout(LegacyMemberId, legacy);

            Changed?.Invoke();
        }

        public void RestoreContext(string sceneName) { }

        public void Reset()
        {
            items.Clear();
            equippedByMember.Clear();
            materials.Clear();
            Gold = 0;
            Changed?.Invoke();
        }

        private void RestoreLoadout(string memberId, JObject loadout)
        {
            var normalizedMemberId = NormalizeMemberId(memberId);
            var restored = new Dictionary<ItemSlot, string>();
            foreach (var property in loadout.Properties())
            {
                if (!Enum.TryParse<ItemSlot>(property.Name, out var slot)) continue;
                var instanceId = property.Value.Value<string>();
                if (string.IsNullOrWhiteSpace(instanceId) || !items.ContainsKey(instanceId))
                    throw new InvalidOperationException("Save contains an equipped item that is not in the bag.");
                if (IsEquipped(instanceId) || restored.Values.Contains(instanceId, StringComparer.Ordinal))
                    throw new InvalidOperationException("Save equips the same item more than once.");
                restored[slot] = instanceId;
            }

            if (restored.Count > 0)
                equippedByMember[normalizedMemberId] = restored;
        }

        private static string NormalizeMemberId(string memberId) =>
            string.IsNullOrWhiteSpace(memberId) ? LegacyMemberId : memberId;

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
