using System;
using System;
using System.Collections.Generic;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using InventorySystem = BorderValley.Inventory;

namespace BorderValley.UI.Inventory
{
    public sealed class InventoryUiPresenter : IDisposable
    {
        private readonly InventorySystem.InventoryService inventory;
        private readonly InventorySystem.CraftingService crafting;
        private readonly InventorySystem.PartyProgressionService progression;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixDefinitions;
        private readonly Action<string> save;
        private InventorySystem.InventoryFilter filter = new();
        private InventorySystem.InventorySort sort = InventorySystem.InventorySort.SlotThenRarity;

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.CraftingService crafting,
            InventorySystem.PartyProgressionService progression,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Action<string> save = null)
            : this(inventory, crafting, progression, null, null, affixDefinitions, save)
        {
        }

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.PartyProgressionService progression,
            InventorySystem.CraftingService crafting,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Action<string> save = null)
            : this(inventory, crafting, progression, null, null, affixDefinitions, save)
        {
        }

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.CraftingService crafting,
            InventorySystem.PartyProgressionService progression,
            InventorySystem.EconomyService economy,
            InventorySystem.CraftingCosts costs,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Action<string> save = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.crafting = crafting;
            this.progression = progression;
            Economy = economy;
            Costs = costs;
            this.affixDefinitions = affixDefinitions ??
                new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            this.save = save;
            this.inventory.Changed += Notify;
            if (this.progression != null)
                this.progression.Changed += Notify;
        }

        public event Action Changed;

        public string SelectedInstanceId { get; private set; }
        public InventorySystem.InventoryFilter Filter => filter;
        public InventorySystem.InventorySort Sort => sort;
        public string LastErrorKey { get; private set; } = string.Empty;
        public InventorySystem.EconomyService Economy { get; }
        public InventorySystem.CraftingCosts Costs { get; }
        public IReadOnlyDictionary<string, AffixDefinition> AffixDefinitions => affixDefinitions;

        public IReadOnlyList<InventorySystem.ItemInstance> VisibleItems =>
            InventorySystem.InventoryQuery.Apply(
                inventory.Items,
                inventory.Definitions,
                filter,
                sort,
                inventory.IsEquipped,
                affixDefinitions);

        public void SetFilter(InventorySystem.InventoryFilter value)
        {
            filter = value ?? new InventorySystem.InventoryFilter();
            Notify();
        }

        public void SetSort(InventorySystem.InventorySort value)
        {
            sort = value;
            Notify();
        }

        public void Select(string instanceId)
        {
            SelectedInstanceId = instanceId;
            Notify();
        }

        public void SetSelectedForTests(string instanceId) => Select(instanceId);

        public bool EquipSelected(string classId)
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);
            return SetResult(
                inventory.TryEquip(SelectedInstanceId, classId, out var error),
                error);
        }

        public bool Unequip(ItemSlot slot) =>
            SetResult(inventory.TryUnequip(slot, out var error), error);

        public bool DismantleSelected()
        {
            if (crafting == null || string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);

            var result = crafting.Dismantle(SelectedInstanceId);
            if (!result.Success)
                return Fail(result.Error);

            SelectedInstanceId = null;
            return Succeed(saveCraft: true);
        }

        public bool ReforgeSelected(string lockedAffixId, IRandomSource random)
        {
            if (crafting == null || string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var result = crafting.Reforge(SelectedInstanceId, lockedAffixId, random);
            return result.Success
                ? Succeed(saveCraft: true)
                : Fail(result.Error);
        }

        public bool Craft(
            string instanceId,
            string itemDefinitionId,
            string classId,
            int itemLevel,
            IRandomSource random)
        {
            if (crafting == null ||
                string.IsNullOrWhiteSpace(itemDefinitionId) ||
                !inventory.Definitions.TryGetValue(itemDefinitionId, out var definition))
            {
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.UnknownDefinition);
            }

            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var result = crafting.Craft(instanceId, definition, classId, itemLevel, random);
            return result.Success
                ? Succeed(saveCraft: true)
                : Fail(result.Error);
        }

        public void RestParty(int amount)
        {
            if (progression == null)
            {
                Fail(BorderValley.UI.Inventory.InventoryTextKeys.ServiceUnavailable);
                return;
            }

            progression.RecoverOutOfCombat(amount);
            Succeed();
        }

        public void Dispose()
        {
            inventory.Changed -= Notify;
            if (progression != null)
                progression.Changed -= Notify;
        }

        private bool SetResult(bool success, string error)
        {
            if (success)
                return Succeed();
            return Fail(error);
        }

        private bool Succeed(bool saveCraft = false)
        {
            LastErrorKey = string.Empty;
            if (saveCraft)
                save?.Invoke("inventory");
            Notify();
            return true;
        }

        private bool Fail(string error)
        {
            LastErrorKey = string.IsNullOrWhiteSpace(error)
                ? BorderValley.UI.Inventory.InventoryTextKeys.ServiceUnavailable
                : error;
            Notify();
            return false;
        }

        private void Notify() => Changed?.Invoke();
    }
}
