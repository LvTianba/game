using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using BorderValley.Presentation;
using UnityEngine;
using UnityEngine.UI;
using InventorySystem = BorderValley.Inventory;

namespace BorderValley.UI.Inventory
{
    public enum InventoryPanelMode
    {
        Inventory,
        Craft
    }

    public sealed class InventoryPanelView : MonoBehaviour
    {
        private const int PageSize = 12;
        private readonly List<Button> itemButtons = new();
        private readonly List<Text> itemLabels = new();
        private readonly List<Image> itemIcons = new();
        private readonly List<Text> equipmentLabels = new();
        private readonly List<Image> equipmentIcons = new();
        private GameObject panelRoot;
        private Text goldLabel;
        private Text oreLabel;
        private Text filterLabel;
        private Text sortLabel;
        private Text detailLabel;
        private Text affixLabel;
        private Text costLabel;
        private Text errorLabel;
        private Text pageLabel;
        private InventoryUiPresenter presenter;
        private InventorySystem.InventoryService inventory;
        private InventorySystem.PartyProgressionService progression;
        private InventorySystem.CraftingService crafting;
        private InventorySystem.EconomyService economy;
        private InventorySystem.CraftingCosts costs;
        private IReadOnlyDictionary<string, AffixDefinition> affixDefinitions;
        private Func<string, bool> save;
        private IPresentationService presentation;
        private Text memberLabel;
        private Text lockLabel;
        private int page;
        private int filterIndex;
        private int memberIndex;
        private int lockIndex;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public InventoryPanelMode Mode { get; private set; }
        public string LastErrorKeyForTests => presenter?.LastErrorKey ?? string.Empty;
        public string CostTextForTests => costLabel == null ? string.Empty : costLabel.text;
        public bool HasUnsavedChangesForTests => presenter?.HasUnsavedChanges ?? false;
        public RectTransform PanelRootRect => panelRoot == null ? null : panelRoot.GetComponent<RectTransform>();
        public RectTransform RectTransform => GetComponent<RectTransform>();

        public void Initialize(
            InventorySystem.InventoryService inventory,
            InventorySystem.PartyProgressionService progression,
            InventorySystem.CraftingService crafting,
            InventorySystem.CraftingCosts costs,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions,
            InventorySystem.EconomyService economy = null,
            Func<string, bool> save = null,
            IPresentationService presentation = null)
        {
            this.inventory = inventory;
            this.progression = progression;
            this.crafting = crafting;
            this.costs = costs ?? new InventorySystem.CraftingCosts();
            this.affixDefinitions = affixDefinitions ??
                new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            this.economy = economy;
            this.save = save;
            this.presentation = presentation ?? PresentationUiUtility.GetOrNull();
            memberIndex = 0;
            lockIndex = 0;
            presenter?.Dispose();
            presenter = inventory == null
                ? null
                : new InventoryUiPresenter(
                    inventory,
                    crafting,
                    progression,
                    economy,
                    this.costs,
                    this.affixDefinitions,
                    save);
            if (presenter != null)
            {
                presenter.Changed += Refresh;
                presenter.SetActiveMember(ActiveMemberId);
            }
            ConfigureRootRect();
            EnsureBuilt();
            Refresh();
        }

        public void SelectItemForTests(string instanceId)
        {
            lockIndex = 0;
            presenter?.Select(instanceId);
        }

        public void CycleLockForTests() => CycleLock();

        public Button GetButtonForTests(string name) =>
            panelRoot == null ? null : panelRoot.transform.Find(name)?.GetComponent<Button>();

        public void Open(InventoryPanelMode mode)
        {
            EnsureBuilt();
            Mode = mode;
            if (panelRoot != null)
                panelRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void Awake()
        {
            ConfigureRootRect();
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (presenter != null)
            {
                presenter.Changed -= Refresh;
                presenter.Dispose();
            }
        }

        private void EnsureBuilt()
        {
            if (panelRoot != null)
                return;

            var root = gameObject;
            panelRoot = CreatePanel(root.transform, "InventoryPanel", Vector2.zero, Vector2.one);
            panelRoot.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
            CreateText(panelRoot.transform, "Title", InventoryTextKeys.InventoryTitle, 28, new Vector2(0.02f, 0.94f), new Vector2(0.98f, 0.99f), TextAnchor.MiddleLeft);

            goldLabel = CreateText(panelRoot.transform, "Gold", InventoryTextKeys.Gold, 22, new Vector2(0.02f, 0.89f), new Vector2(0.24f, 0.94f), TextAnchor.MiddleLeft);
            oreLabel = CreateText(panelRoot.transform, "Ore", InventoryTextKeys.Ore, 22, new Vector2(0.26f, 0.89f), new Vector2(0.48f, 0.94f), TextAnchor.MiddleLeft);
            filterLabel = CreateText(panelRoot.transform, "Filter", InventoryTextKeys.Filter, 20, new Vector2(0.50f, 0.89f), new Vector2(0.65f, 0.94f), TextAnchor.MiddleLeft);
            sortLabel = CreateText(panelRoot.transform, "Sort", InventoryTextKeys.Sort, 20, new Vector2(0.66f, 0.89f), new Vector2(0.82f, 0.94f), TextAnchor.MiddleLeft);
            CreateButton(panelRoot.transform, "CycleFilter", InventoryTextKeys.Filter, new Vector2(0.50f, 0.89f), new Vector2(0.65f, 0.94f), CycleFilter);
            CreateButton(panelRoot.transform, "CycleSort", InventoryTextKeys.Sort, new Vector2(0.66f, 0.89f), new Vector2(0.82f, 0.94f), CycleSort);
            CreateButton(panelRoot.transform, "Close", InventoryTextKeys.Empty, new Vector2(0.84f, 0.89f), new Vector2(0.98f, 0.94f), Close);

            CreateText(panelRoot.transform, "EquipmentTitle", InventoryTextKeys.EquipmentTitle, 22, new Vector2(0.02f, 0.82f), new Vector2(0.14f, 0.87f), TextAnchor.MiddleLeft);
            memberLabel = CreateText(panelRoot.transform, "Member", InventoryTextKeys.ActiveMember, 17, new Vector2(0.02f, 0.76f), new Vector2(0.14f, 0.81f), TextAnchor.MiddleLeft);
            CreateButton(panelRoot.transform, "NextMember", InventoryTextKeys.NextMember, new Vector2(0.15f, 0.76f), new Vector2(0.20f, 0.81f), CycleMember);
            for (var index = 0; index < 6; index++)
            {
                equipmentLabels.Add(CreateText(panelRoot.transform, "Equipment" + index, InventoryTextKeys.Empty, 18, new Vector2(0.06f, 0.75f - index * 0.09f), new Vector2(0.20f, 0.81f - index * 0.09f), TextAnchor.MiddleLeft));
                equipmentIcons.Add(CreateIcon(
                    panelRoot.transform,
                    "EquipmentIcon" + index,
                    new Vector2(0.02f, 0.78f - index * 0.09f),
                    Vector2.zero,
                    new Vector2(32f, 32f)));
            }

            CreateText(panelRoot.transform, "BagTitle", InventoryTextKeys.Details, 22, new Vector2(0.22f, 0.82f), new Vector2(0.72f, 0.87f), TextAnchor.MiddleLeft);
            for (var index = 0; index < PageSize; index++)
            {
                var column = index % 3;
                var row = index / 3;
                var min = new Vector2(0.22f + column * 0.17f, 0.72f - row * 0.16f);
                var max = new Vector2(0.38f + column * 0.17f, 0.86f - row * 0.16f);
                var captured = index;
                var button = CreateButton(panelRoot.transform, "Item" + index, InventoryTextKeys.Empty, min, max, () => SelectVisible(captured));
                itemButtons.Add(button);
                var label = button.GetComponentInChildren<Text>();
                var labelRect = label.rectTransform;
                labelRect.offsetMin = new Vector2(40f, 4f);
                labelRect.offsetMax = new Vector2(-4f, -4f);
                itemLabels.Add(label);
                itemIcons.Add(CreateIcon(
                    button.transform,
                    "Icon",
                    new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f),
                    new Vector2(32f, 32f),
                    new Vector2(0f, 0.5f)));
            }

            pageLabel = CreateText(panelRoot.transform, "Page", InventoryTextKeys.Page, 18, new Vector2(0.22f, 0.06f), new Vector2(0.48f, 0.12f), TextAnchor.MiddleCenter);
            CreateButton(panelRoot.transform, "Previous", InventoryTextKeys.PreviousPage, new Vector2(0.22f, 0.01f), new Vector2(0.34f, 0.06f), () => ChangePage(-1));
            CreateButton(panelRoot.transform, "Next", InventoryTextKeys.NextPage, new Vector2(0.36f, 0.01f), new Vector2(0.48f, 0.06f), () => ChangePage(1));

            CreateText(panelRoot.transform, "DetailsTitle", InventoryTextKeys.Details, 22, new Vector2(0.74f, 0.82f), new Vector2(0.98f, 0.87f), TextAnchor.MiddleLeft);
            detailLabel = CreateText(panelRoot.transform, "Details", InventoryTextKeys.Empty, 18, new Vector2(0.74f, 0.62f), new Vector2(0.98f, 0.81f), TextAnchor.UpperLeft);
            affixLabel = CreateText(panelRoot.transform, "Affixes", InventoryTextKeys.AffixRanges, 18, new Vector2(0.74f, 0.42f), new Vector2(0.98f, 0.61f), TextAnchor.UpperLeft);
            CreateButton(panelRoot.transform, "Equip", InventoryTextKeys.Equip, new Vector2(0.74f, 0.34f), new Vector2(0.85f, 0.40f), EquipSelected);
            CreateButton(panelRoot.transform, "Unequip", InventoryTextKeys.Unequip, new Vector2(0.86f, 0.34f), new Vector2(0.98f, 0.40f), UnequipSelected);
            CreateButton(panelRoot.transform, "Dismantle", InventoryTextKeys.Dismantle, new Vector2(0.74f, 0.27f), new Vector2(0.85f, 0.33f), DismantleSelected);
            CreateButton(panelRoot.transform, "Reforge", InventoryTextKeys.Reforge, new Vector2(0.86f, 0.27f), new Vector2(0.98f, 0.33f), ReforgeSelected);
            CreateButton(panelRoot.transform, "CycleLock", InventoryTextKeys.CycleLock, new Vector2(0.74f, 0.20f), new Vector2(0.85f, 0.26f), CycleLock);
            lockLabel = CreateText(panelRoot.transform, "Lock", InventoryTextKeys.Locked, 16, new Vector2(0.86f, 0.20f), new Vector2(0.98f, 0.26f), TextAnchor.MiddleLeft);
            CreateText(panelRoot.transform, "CostsTitle", InventoryTextKeys.Cost, 20, new Vector2(0.74f, 0.14f), new Vector2(0.98f, 0.19f), TextAnchor.MiddleLeft);
            costLabel = CreateText(panelRoot.transform, "Costs", InventoryTextKeys.Empty, 17, new Vector2(0.74f, 0.07f), new Vector2(0.98f, 0.14f), TextAnchor.UpperLeft);
            CreateButton(panelRoot.transform, "RetrySave", InventoryTextKeys.RetrySave, new Vector2(0.74f, 0.01f), new Vector2(0.85f, 0.07f), RetrySave);
            CreateButton(panelRoot.transform, "Craft", InventoryTextKeys.Craft, new Vector2(0.86f, 0.01f), new Vector2(0.98f, 0.07f), CraftSelected);
            errorLabel = CreateText(panelRoot.transform, "Error", InventoryTextKeys.Empty, 18, new Vector2(0.50f, 0.01f), new Vector2(0.73f, 0.07f), TextAnchor.MiddleLeft);

            panelRoot.SetActive(false);
        }

        private void Refresh()
        {
            if (panelRoot == null)
                return;

            if (presenter == null)
            {
                goldLabel.text = InventoryTextKeys.Gold;
                oreLabel.text = InventoryTextKeys.Ore;
                errorLabel.text = InventoryTextKeys.ServiceUnavailable;
                return;
            }

            goldLabel.text = InventoryTextKeys.Gold + ": " + inventory.Gold;
            oreLabel.text = InventoryTextKeys.Ore + ": " + inventory.Materials.GetValueOrDefault(InventorySystem.CraftingService.OreMaterialId);
            filterLabel.text = FilterKey(filterIndex);
            sortLabel.text = SortKey(presenter.Sort);
            errorLabel.text = presenter.LastErrorKey;
            memberLabel.text = InventoryTextKeys.ActiveMember + ": " + ActiveMemberId;
            lockLabel.text = InventoryTextKeys.Locked + ": " + (presenter.LockedAffixId ?? InventoryTextKeys.Empty);

            var visible = presenter.VisibleItems;
            var pageCount = Math.Max(1, (visible.Count + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pageCount - 1));
            pageLabel.text = InventoryTextKeys.Page + ": " + (page + 1) + "/" + pageCount;

            for (var index = 0; index < itemButtons.Count; index++)
            {
                var itemIndex = page * PageSize + index;
                var active = itemIndex < visible.Count;
                itemButtons[index].gameObject.SetActive(active);
                if (!active)
                {
                    SetItemIcon(itemIcons[index], null, default);
                    continue;
                }

                var item = visible[itemIndex];
                var definition = inventory.Definitions[item.ItemDefinitionId];
                SetItemIcon(itemIcons[index], item.ItemDefinitionId, definition.Slot);
                var equipped = inventory.IsEquipped(item.InstanceId) ? " [" + InventoryTextKeys.EquippedMarker + "]" : string.Empty;
                itemLabels[index].text = definition.LocalizationKey + " " + InventoryTextKeys.RarityKey(item.Rarity) + " " + InventoryTextKeys.ItemLevel + " " + item.ItemLevel + equipped;
            }

            for (var index = 0; index < equipmentLabels.Count; index++)
            {
                var slot = (ItemSlot)index;
                var equipmentText = InventoryTextKeys.Empty;
                ItemDefinition equippedDefinition = null;
                if (inventory.GetEquipped(ActiveMemberId).TryGetValue(slot, out var instanceId))
                {
                    var item = inventory.Items.FirstOrDefault(value => value.InstanceId == instanceId);
                    equipmentText = item == null
                        ? instanceId
                        : inventory.Definitions[item.ItemDefinitionId].LocalizationKey + " " + instanceId;
                    if (item != null)
                        inventory.Definitions.TryGetValue(item.ItemDefinitionId, out equippedDefinition);
                }
                equipmentLabels[index].text = SlotKey(slot) + ": " + equipmentText;
                if (equippedDefinition == null)
                {
                    equipmentIcons[index].sprite = null;
                }
                else
                {
                    equipmentIcons[index].sprite = Presentation?.GetItemIcon(
                        equippedDefinition.Id,
                        PresentationUiUtility.ResolveItemSlotId(equippedDefinition.Slot));
                }
            }

            var selected = SelectedItem();
            if (selected == null)
            {
                detailLabel.text = InventoryTextKeys.Empty;
                affixLabel.text = InventoryTextKeys.AffixRanges + ": " + InventoryTextKeys.Empty;
                lockIndex = 0;
            }
            else
            {
                var definition = inventory.Definitions[selected.ItemDefinitionId];
                var stats = string.Join("\n", definition.Stats.Select(value => StatKey(value.Stat) + ": " + value.Value));
                detailLabel.text = definition.LocalizationKey + "\n" + InventoryTextKeys.BaseStats + "\n" + stats;
                if (lockIndex > selected.Affixes.Count) lockIndex = 0;
                var ranges = selected.Affixes.Select(affix =>
                {
                    var value = affixDefinitions.TryGetValue(affix.AffixId, out var definitionAffix)
                        ? definitionAffix.LocalizationKey + ": " + definitionAffix.MinValue + "-" + definitionAffix.MaxValue
                        : affix.AffixId;
                    return affix.AffixId == LockedAffixId
                        ? value + " [" + InventoryTextKeys.Locked + "]"
                        : value;
                }).ToArray();
                affixLabel.text = InventoryTextKeys.AffixRanges + "\n" + string.Join("\n", ranges);
            }

            UpdateCosts(selected);
        }

        private void UpdateCosts(InventorySystem.ItemInstance selected)
        {
            if (costs == null || inventory == null)
            {
                costLabel.text = InventoryTextKeys.ServiceUnavailable;
                return;
            }

            var ore = inventory.Materials.GetValueOrDefault(InventorySystem.CraftingService.OreMaterialId);
            var craftGold = costs.CraftGold(1, ItemRarity.Common);
            var craftOre = costs.CraftMaterial(1, ItemRarity.Common);
            var lines = new List<string>
            {
                InventoryTextKeys.Cost + " " + InventoryTextKeys.Craft + ": " + craftGold + "g/" + craftOre + "ore",
                InventoryTextKeys.Cost + " " + InventoryTextKeys.Dismantle + ": " + (selected == null ? 0 : costs.DismantleMaterial(selected)) + "ore"
            };
            if (selected != null)
            {
                var locksAffix = !string.IsNullOrWhiteSpace(presenter.LockedAffixId);
                lines.Add(InventoryTextKeys.Cost + " " + InventoryTextKeys.Reforge + ": " +
                    costs.ReforgeGold(selected, locksAffix) + "g/" + costs.ReforgeMaterial(selected, locksAffix) + "ore");
            }
            if (inventory.Gold < craftGold || ore < craftOre)
                lines.Add(InventoryTextKeys.MaterialsInsufficient);
            costLabel.text = string.Join("\n", lines);
        }

        private void CycleFilter()
        {
            filterIndex = (filterIndex + 1) % 5;
            var filter = filterIndex switch
            {
                1 => new InventorySystem.InventoryFilter { Slot = ItemSlot.Weapon },
                2 => new InventorySystem.InventoryFilter { Slot = ItemSlot.Body },
                3 => new InventorySystem.InventoryFilter { Slot = ItemSlot.Accessory },
                4 => new InventorySystem.InventoryFilter { Equipped = true },
                _ => new InventorySystem.InventoryFilter()
            };
            presenter.SetFilter(filter);
            page = 0;
            Refresh();
        }

        private void CycleSort()
        {
            var next = presenter.Sort switch
            {
                InventorySystem.InventorySort.SlotThenRarity => InventorySystem.InventorySort.RarityThenItemLevel,
                InventorySystem.InventorySort.RarityThenItemLevel => InventorySystem.InventorySort.ItemLevelThenName,
                InventorySystem.InventorySort.ItemLevelThenName => InventorySystem.InventorySort.ValueThenName,
                _ => InventorySystem.InventorySort.SlotThenRarity
            };
            presenter.SetSort(next);
            page = 0;
            Refresh();
        }

        private void CycleMember()
        {
            if (progression == null || progression.Members.Count == 0) return;
            memberIndex = (memberIndex + 1) % progression.Members.Count;
            lockIndex = 0;
            presenter.SetActiveMember(ActiveMemberId);
            Refresh();
        }

        private void CycleLock()
        {
            var selected = SelectedItem();
            if (selected == null || selected.Affixes.Count == 0) return;
            lockIndex = (lockIndex + 1) % (selected.Affixes.Count + 1);
            presenter.SetLockedAffix(lockIndex == 0
                ? null
                : selected.Affixes[lockIndex - 1].AffixId);
            Refresh();
        }

        private void RetrySave()
        {
            presenter.RetrySave();
            Refresh();
        }

        private void ChangePage(int delta)
        {
            page += delta;
            Refresh();
        }

        private void SelectVisible(int index)
        {
            var visible = presenter.VisibleItems;
            var itemIndex = page * PageSize + index;
            if (itemIndex < visible.Count)
                presenter.Select(visible[itemIndex].InstanceId);
        }

        private void EquipSelected()
        {
            presenter.EquipSelected(ActiveMemberId, ActiveClassId);
            Refresh();
        }

        private void UnequipSelected()
        {
            var selected = SelectedItem();
            if (selected == null)
            {
                presenter.Unequip(ActiveMemberId, ItemSlot.Weapon);
            }
            else
            {
                presenter.Unequip(ActiveMemberId, inventory.Definitions[selected.ItemDefinitionId].Slot);
            }
            Refresh();
        }

        private void DismantleSelected()
        {
            presenter.DismantleSelected();
            Refresh();
        }

        private void ReforgeSelected()
        {
            presenter.ReforgeSelected(RandomSourceFactory.FromSeed("reforge:" + presenter.SelectedInstanceId));
            Refresh();
        }

        private void CraftSelected()
        {
            var selected = SelectedItem();
            var definition = selected == null ? "item.frost_longsword" : selected.ItemDefinitionId;
            presenter.Craft(
                "craft." + Guid.NewGuid().ToString("N"),
                definition,
                ActiveClassId,
                1,
                RandomSourceFactory.FromSeed("craft:" + Guid.NewGuid().ToString("N")));
            Refresh();
        }

        private InventorySystem.ItemInstance SelectedItem()
        {
            if (presenter == null || string.IsNullOrWhiteSpace(presenter.SelectedInstanceId))
                return null;
            return inventory.Items.FirstOrDefault(item => item.InstanceId == presenter.SelectedInstanceId);
        }

        private string ActiveMemberId =>
            progression?.Members.ElementAtOrDefault(memberIndex)?.MemberId ?? string.Empty;

        private string ActiveClassId =>
            progression?.Members.ElementAtOrDefault(memberIndex)?.CharacterId ?? "class.warrior";

        private string LockedAffixId => presenter?.LockedAffixId;

        private static string FilterKey(int index) => index switch
        {
            1 => InventoryTextKeys.FilterWeapon,
            2 => InventoryTextKeys.FilterArmor,
            3 => InventoryTextKeys.FilterAccessory,
            4 => InventoryTextKeys.FilterEquipped,
            _ => InventoryTextKeys.FilterAll
        };

        private static string SortKey(InventorySystem.InventorySort sort) => sort switch
        {
            InventorySystem.InventorySort.RarityThenItemLevel => InventoryTextKeys.SortRarity,
            InventorySystem.InventorySort.ItemLevelThenName => InventoryTextKeys.SortItemLevel,
            InventorySystem.InventorySort.ValueThenName => InventoryTextKeys.SortValue,
            _ => InventoryTextKeys.SortSlot
        };

        private static string SlotKey(ItemSlot slot) => InventoryTextKeys.SlotKey(slot);
        private static string StatKey(BorderValley.Core.Combat.CombatStat stat) => InventoryTextKeys.StatKey(stat);

        private void ConfigureRootRect()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>();
            image.raycastTarget = true;
            var presentation = PresentationUiUtility.GetOrNull();
            PresentationUiUtility.ApplyPanel(
                image,
                PresentationUiUtility.ResolvePanel(presentation));
            return root;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string key,
            int size,
            Vector2 min,
            Vector2 max,
            TextAnchor anchor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = root.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = key;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string key,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.28f, 0.42f, 1f);
            image.raycastTarget = true;
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var presentation = PresentationUiUtility.GetOrNull();
            PresentationUiUtility.ApplyButton(
                button,
                PresentationUiUtility.ResolveButton(presentation),
                PresentationUiUtility.ResolvePressedButton(presentation));
            var label = CreateText(root.transform, "Label", key, 17, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            return button;
        }

        private static Button CreateTextButton(Transform parent, string name, string key, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action) =>
            CreateButton(parent, name, key, min, max, action);

        private IPresentationService Presentation =>
            presentation ?? (presentation = PresentationUiUtility.GetOrNull());

        private void SetItemIcon(Image icon, string definitionId, ItemSlot slot)
        {
            if (icon == null)
                return;

            icon.sprite = string.IsNullOrWhiteSpace(definitionId)
                ? null
                : Presentation?.GetItemIcon(
                    definitionId,
                    PresentationUiUtility.ResolveItemSlotId(slot));
        }

        private static Image CreateIcon(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
