using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json.Linq;

namespace BorderValley.Inventory
{
    public sealed class InventoryService : ISaveParticipant, ISaveParticipantPostRestore
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

            var goldToken = state["gold"];
            if (goldToken == null || goldToken.Type != JTokenType.Integer)
                throw new InvalidOperationException("Save contains malformed inventory gold.");
            var gold = goldToken.Value<int>();
            if (gold < 0)
                throw new InvalidOperationException("Save contains negative inventory gold.");
            Gold = gold;

            if (state["materials"] is JObject materialsState)
            {
                foreach (var property in materialsState.Properties())
                {
                    if (string.IsNullOrWhiteSpace(property.Name) ||
                        property.Value.Type != JTokenType.Integer ||
                        property.Value.Value<int>() < 0)
                        throw new InvalidOperationException("Save contains malformed inventory materials.");
                    materials[property.Name] = property.Value.Value<int>();
                }
            }
            else if (state["materials"] != null && state["materials"].Type != JTokenType.Null)
            {
                throw new InvalidOperationException("Save contains malformed inventory materials.");
            }

            if (state["items"] is JArray itemState)
            {
                foreach (var token in itemState)
                {
                    if (!(token is JObject itemObject))
                        throw new InvalidOperationException("Save contains a malformed inventory item.");
                    RestoreItem(itemObject);
                }
            }
            else if (state["items"] != null && state["items"].Type != JTokenType.Null)
            {
                throw new InvalidOperationException("Save contains malformed inventory items.");
            }

            var loadedByMember = false;
            if (state["equippedByMember"] != null && state["equippedByMember"].Type != JTokenType.Null)
            {
                if (!(state["equippedByMember"] is JObject byMember))
                    throw new InvalidOperationException("Save contains malformed member equipment.");
                foreach (var property in byMember.Properties())
                {
                    if (string.IsNullOrWhiteSpace(property.Name) || !(property.Value is JObject loadout))
                        throw new InvalidOperationException("Save contains a malformed member loadout.");
                    RestoreLoadout(property.Name, loadout);
                    loadedByMember = true;
                }
            }

            if (!loadedByMember && state["equipped"] != null && state["equipped"].Type != JTokenType.Null)
            {
                if (!(state["equipped"] is JObject legacy))
                    throw new InvalidOperationException("Save contains malformed legacy equipment.");
                RestoreLoadout(LegacyMemberId, legacy);
            }

            Changed?.Invoke();
        }

        public void CompleteRestore(IReadOnlyDictionary<string, ISaveParticipant> participants)
        {
            var progression = participants.Values.OfType<PartyProgressionService>().FirstOrDefault();
            if (progression == null) return;

            ValidateMemberLoadouts(progression);
            if (!equippedByMember.TryGetValue(LegacyMemberId, out var legacy)) return;
            if (equippedByMember.Keys.Any(key => key != LegacyMemberId))
            {
                equippedByMember.Remove(LegacyMemberId);
                return;
            }

            foreach (var pair in legacy
                         .OrderBy(value => value.Key)
                         .ThenBy(value => value.Value, StringComparer.Ordinal))
            {
                if (!items.TryGetValue(pair.Value, out var item))
                    throw new InvalidOperationException("Legacy equipment references an item that is not in the bag.");
                if (!Definitions.TryGetValue(item.ItemDefinitionId, out var definition))
                    throw new InvalidOperationException("Legacy equipment references an unknown item definition.");

                var candidates = progression.Members
                    .Where(member => definition.AllowsClass(member.CharacterId))
                    .OrderBy(member => member.MemberId, StringComparer.Ordinal)
                    .ToArray();
                if (candidates.Length == 0)
                    throw new InvalidOperationException("Legacy equipment has no compatible party member.");

                var target = candidates.FirstOrDefault(member =>
                                 !GetEquipped(member.MemberId).ContainsKey(pair.Key)) ??
                             candidates[0];
                var loadout = GetOrCreateLoadout(target.MemberId);
                loadout[pair.Key] = pair.Value;
            }

            equippedByMember.Remove(LegacyMemberId);
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
            if (loadout == null) throw new InvalidOperationException("Save contains a null loadout.");
            var normalizedMemberId = NormalizeMemberId(memberId);
            var restored = new Dictionary<ItemSlot, string>();
            foreach (var property in loadout.Properties())
            {
                if (!Enum.TryParse<ItemSlot>(property.Name, out var slot) ||
                    !Enum.IsDefined(typeof(ItemSlot), slot))
                    throw new InvalidOperationException("Save contains an unknown equipment slot.");
                if (property.Value.Type != JTokenType.String)
                    throw new InvalidOperationException("Save contains a malformed equipped item reference.");

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

        private void RestoreItem(JObject value)
        {
            var instanceId = value.Value<string>("instanceId");
            var definitionId = value.Value<string>("definitionId");
            var itemLevelToken = value["itemLevel"];
            var rarityToken = value["rarity"];
            if (string.IsNullOrWhiteSpace(instanceId) ||
                string.IsNullOrWhiteSpace(definitionId) ||
                itemLevelToken == null ||
                itemLevelToken.Type != JTokenType.Integer ||
                itemLevelToken.Value<int>() < 1 ||
                itemLevelToken.Value<int>() > 10 ||
                rarityToken == null ||
                rarityToken.Type != JTokenType.String ||
                !Enum.TryParse<ItemRarity>(rarityToken.Value<string>(), out var rarity) ||
                !Enum.IsDefined(typeof(ItemRarity), rarity))
                throw new InvalidOperationException("Save contains a malformed inventory item.");

            if (!Definitions.ContainsKey(definitionId))
                throw new InvalidOperationException("Save contains an item with an unknown definition.");
            if (items.ContainsKey(instanceId))
                throw new InvalidOperationException("Save contains duplicate item instances.");

            var affixes = new List<AffixInstance>();
            var affixToken = value["affixes"];
            if (affixToken != null && affixToken.Type != JTokenType.Null)
            {
                if (!(affixToken is JArray affixArray))
                    throw new InvalidOperationException("Save contains malformed item affixes.");
                foreach (var affixTokenValue in affixArray)
                {
                    if (!(affixTokenValue is JObject affix) ||
                        string.IsNullOrWhiteSpace(affix.Value<string>("id")) ||
                        affix["value"] == null ||
                        affix["value"].Type != JTokenType.Integer)
                        throw new InvalidOperationException("Save contains a malformed item affix.");
                    affixes.Add(new AffixInstance(affix.Value<string>("id"), affix.Value<int>("value")));
                }
            }

            items[instanceId] = new ItemInstance(
                instanceId,
                definitionId,
                itemLevelToken.Value<int>(),
                rarity,
                affixes);
        }

        private void ValidateMemberLoadouts(PartyProgressionService progression)
        {
            var members = progression.Members.ToDictionary(
                member => member.MemberId,
                member => member,
                StringComparer.Ordinal);
            foreach (var loadoutPair in equippedByMember)
            {
                if (loadoutPair.Key == LegacyMemberId) continue;
                if (!members.TryGetValue(loadoutPair.Key, out var member))
                    throw new InvalidOperationException("Save contains equipment for an unknown party member.");
                foreach (var itemPair in loadoutPair.Value)
                {
                    if (!items.TryGetValue(itemPair.Value, out var item))
                        throw new InvalidOperationException("Save contains an equipped item that is not in the bag.");
                    if (!Definitions.TryGetValue(item.ItemDefinitionId, out var definition) ||
                        definition.Slot != itemPair.Key ||
                        !definition.AllowsClass(member.CharacterId))
                        throw new InvalidOperationException("Save contains equipment that is invalid for its party member.");
                }
            }
        }

        private Dictionary<ItemSlot, string> GetOrCreateLoadout(string memberId)
        {
            var normalizedMemberId = NormalizeMemberId(memberId);
            if (!equippedByMember.TryGetValue(normalizedMemberId, out var loadout))
            {
                loadout = new Dictionary<ItemSlot, string>();
                equippedByMember[normalizedMemberId] = loadout;
            }
            return loadout;
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
    }
}
