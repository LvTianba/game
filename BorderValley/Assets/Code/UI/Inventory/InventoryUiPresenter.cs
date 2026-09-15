using System;
using System.Collections.Generic;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using BorderValley.Presentation;
using InventorySystem = BorderValley.Inventory;

namespace BorderValley.UI.Inventory
{
    public sealed class InventoryUiPresenter : IDisposable
    {
        private readonly InventorySystem.InventoryService inventory;
        private readonly InventorySystem.CraftingService crafting;
        private readonly InventorySystem.PartyProgressionService progression;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixDefinitions;
        private readonly Func<string, bool> save;
        private readonly IPresentationService presentation;
        private InventorySystem.InventoryFilter filter = new();
        private InventorySystem.InventorySort sort = InventorySystem.InventorySort.SlotThenRarity;
        private bool hasUnsavedChanges;

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.CraftingService crafting,
            InventorySystem.PartyProgressionService progression,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Action<string> save = null,
            IPresentationService presentation = null)
            : this(
                inventory,
                crafting,
                progression,
                null,
                null,
                affixDefinitions,
                save == null ? null : _ => { save(_); return true; },
                presentation)
        {
        }

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.PartyProgressionService progression,
            InventorySystem.CraftingService crafting,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Action<string> save = null,
            IPresentationService presentation = null)
            : this(inventory, crafting, progression, affixDefinitions, save, presentation)
        {
        }

        public InventoryUiPresenter(
            InventorySystem.InventoryService inventory,
            InventorySystem.CraftingService crafting,
            InventorySystem.PartyProgressionService progression,
            InventorySystem.EconomyService economy,
            InventorySystem.CraftingCosts costs,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null,
            Func<string, bool> save = null,
            IPresentationService presentation = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.crafting = crafting;
            this.progression = progression;
            Economy = economy;
            Costs = costs;
            this.affixDefinitions = affixDefinitions ??
                new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            this.save = save;
            this.presentation = presentation ?? new NullPresentationService();
            this.inventory.Changed += Notify;
            if (this.progression != null)
                this.progression.Changed += Notify;
        }

        public event Action Changed;

        public string SelectedInstanceId { get; private set; }
        public string SelectedMemberId { get; private set; } = string.Empty;
        public string LockedAffixId { get; private set; }
        public bool HasUnsavedChanges => hasUnsavedChanges;
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
            LockedAffixId = null;
            Notify();
        }

        public void SetSelectedForTests(string instanceId) => Select(instanceId);

        public void SetActiveMember(string memberId)
        {
            SelectedMemberId = memberId ?? string.Empty;
            Notify();
        }

        public void SetLockedAffix(string affixId)
        {
            LockedAffixId = string.IsNullOrWhiteSpace(affixId) ? null : affixId;
            Notify();
        }

        public bool EquipSelected(string classId) =>
            EquipSelected(SelectedMemberId, classId);

        public bool EquipSelected(string memberId, string classId)
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);
            return SetResult(
                inventory.TryEquip(SelectedInstanceId, memberId, classId, out var error),
                error,
                "sfx.inventory.equip",
                persistChanges: true);
        }

        public bool Unequip(ItemSlot slot) => Unequip(SelectedMemberId, slot);

        public bool Unequip(string memberId, ItemSlot slot) =>
            SetResult(
                inventory.TryUnequip(memberId, slot, out var error),
                error,
                "sfx.ui.cancel",
                persistChanges: true);

        public bool DismantleSelected()
        {
            if (crafting == null || string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);

            var result = crafting.Dismantle(SelectedInstanceId);
            if (!result.Success)
                return Fail(result.Error);

            SelectedInstanceId = null;
            LockedAffixId = null;
            return Succeed(persistChanges: true, sfxId: "sfx.inventory.craft");
        }

        public bool ReforgeSelected(string lockedAffixId, IRandomSource random)
        {
            SetLockedAffix(lockedAffixId);
            return ReforgeSelected(random);
        }

        public bool ReforgeSelected(IRandomSource random)
        {
            if (crafting == null || string.IsNullOrWhiteSpace(SelectedInstanceId))
                return Fail(BorderValley.UI.Inventory.InventoryTextKeys.ItemMissing);
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var result = crafting.Reforge(SelectedInstanceId, LockedAffixId, random);
            return result.Success
                ? Succeed(persistChanges: true, sfxId: "sfx.inventory.craft")
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
                ? Succeed(persistChanges: true, sfxId: "sfx.inventory.craft")
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
            Succeed(sfxId: "sfx.world.reward");
        }

        public bool RetrySave()
        {
            if (!hasUnsavedChanges) return true;
            if (save == null)
            {
                LastErrorKey = BorderValley.UI.Inventory.InventoryTextKeys.AutoSaveFailed;
                presentation.PlaySfx("sfx.ui.error");
                Notify();
                return false;
            }

            if (!TryInvokeSave())
            {
                LastErrorKey = BorderValley.UI.Inventory.InventoryTextKeys.AutoSaveFailed;
                presentation.PlaySfx("sfx.ui.error");
                Notify();
                return false;
            }

            hasUnsavedChanges = false;
            LastErrorKey = string.Empty;
            Notify();
            return true;
        }

        public void Dispose()
        {
            inventory.Changed -= Notify;
            if (progression != null)
                progression.Changed -= Notify;
        }

        private bool SetResult(
            bool success,
            string error,
            string sfxId,
            bool persistChanges = false)
        {
            if (success)
                return Succeed(persistChanges, sfxId);
            return Fail(error);
        }

        private bool Succeed(bool persistChanges = false, string sfxId = null)
        {
            LastErrorKey = string.Empty;
            if (persistChanges && save != null)
            {
                if (!TryInvokeSave())
                {
                    hasUnsavedChanges = true;
                    LastErrorKey = BorderValley.UI.Inventory.InventoryTextKeys.AutoSaveFailed;
                    presentation.PlaySfx("sfx.ui.error");
                    Notify();
                    return false;
                }

                hasUnsavedChanges = false;
            }

            if (!string.IsNullOrWhiteSpace(sfxId))
                presentation.PlaySfx(sfxId);
            Notify();
            return true;
        }

        private bool TryInvokeSave()
        {
            try
            {
                return save != null && save("inventory");
            }
            catch
            {
                return false;
            }
        }

        private bool Fail(string error)
        {
            LastErrorKey = string.IsNullOrWhiteSpace(error)
                ? BorderValley.UI.Inventory.InventoryTextKeys.ServiceUnavailable
                : error;
            presentation.PlaySfx("sfx.ui.error");
            Notify();
            return false;
        }

        private void Notify() => Changed?.Invoke();
    }
}
