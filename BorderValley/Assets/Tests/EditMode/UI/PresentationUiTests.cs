using System;
using System.Collections.Generic;
using BorderValley.Data.Items;
using BorderValley.Inventory;
using BorderValley.Presentation;
using BorderValley.UI;
using BorderValley.UI.Inventory;
using BorderValley.UI.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BorderValley.UI.Tests
{
    public sealed class PresentationUiTests
    {
        [Test]
        public void ApplyPanel_SetsSpriteAndSlicedType()
        {
            var root = new GameObject("panel", typeof(RectTransform), typeof(Image));
            var image = root.GetComponent<Image>();
            var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 16, 16),
                Vector2.one * 0.5f,
                32f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4, 4, 4, 4));

            try
            {
                PresentationUiUtility.ApplyPanel(image, sprite);

                Assert.That(image.sprite, Is.SameAs(sprite));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f));
                Assert.That(image.raycastTarget, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ApplyButton_SetsNormalAndPressedSprites()
        {
            var root = new GameObject("button", typeof(RectTransform), typeof(Image), typeof(Button));
            var image = root.GetComponent<Image>();
            var button = root.GetComponent<Button>();
            var normalTexture = new Texture2D(16, 16);
            var pressedTexture = new Texture2D(16, 16);
            var normal = Sprite.Create(normalTexture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
            var pressed = Sprite.Create(pressedTexture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);

            try
            {
                PresentationUiUtility.ApplyButton(button, normal, pressed);

                Assert.That(button.targetGraphic, Is.SameAs(image));
                Assert.That(image.sprite, Is.SameAs(normal));
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                Assert.That(button.spriteState.pressedSprite, Is.SameAs(pressed));
                Assert.That(button.spriteState.highlightedSprite, Is.SameAs(pressed));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(pressed);
                Object.DestroyImmediate(normalTexture);
                Object.DestroyImmediate(pressedTexture);
            }
        }

        [Test]
        public void DialoguePanelView_Render_Uses256PortraitAndMissingFallback()
        {
            var root = new GameObject("dialogue", typeof(RectTransform));
            var portraitTexture = new Texture2D(256, 256);
            var portrait = Sprite.Create(
                portraitTexture,
                new Rect(0f, 0f, 256f, 256f),
                Vector2.one * 0.5f);
            var view = root.AddComponent<DialoguePanelView>();

            try
            {
                view.Render(new DialoguePanelViewData(
                    "npc.elder.name",
                    "dialogue.text",
                    Array.Empty<DialogueChoiceBinding>(),
                    false,
                    true,
                    string.Empty,
                    "npc.elder",
                    portrait));

                var portraitImage = root.transform.Find("DialoguePanel/Portrait")?.GetComponent<Image>();
                Assert.That(portraitImage, Is.Not.Null);
                Assert.That(portraitImage.sprite, Is.SameAs(portrait));
                Assert.That(portraitImage.preserveAspect, Is.True);
                Assert.That(portraitImage.raycastTarget, Is.False);
                Assert.That(portraitImage.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(256f, 256f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(portrait);
                Object.DestroyImmediate(portraitTexture);
            }
        }

        [Test]
        public void InventoryPanelView_Render_ShowsItemAndEquipmentIcons()
        {
            var service = LoadPresentation();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.EditorConfigure(
                "item.unmapped_armor",
                "item.unmapped_armor.name",
                ItemSlot.Body,
                Array.Empty<string>(),
                false,
                100,
                Array.Empty<StatValue>());
            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [item.Id] = item
            };
            var inventory = new InventoryService(4, definitions, 10);
            Assert.That(inventory.TryAdd(
                new ItemInstance(
                    "instance.sword",
                    item.Id,
                    1,
                    ItemRarity.Common,
                    Array.Empty<AffixInstance>()), out var addError), Is.True, addError);
            Assert.That(inventory.TryEquip(
                "instance.sword",
                null,
                "class.warrior",
                out var equipError), Is.True, equipError);

            var root = new GameObject("inventory", typeof(RectTransform));
            var view = root.AddComponent<InventoryPanelView>();
            try
            {
                view.Initialize(
                    inventory,
                    null,
                    null,
                    null,
                    new Dictionary<string, AffixDefinition>(StringComparer.Ordinal),
                    null,
                    null,
                    service);

                var expected = service.GetItemIcon(item.Id, "armor");
                var itemIcon = root.transform.Find("InventoryPanel/Item0/Icon")?.GetComponent<Image>();
                var equipmentIcon = root.transform.Find("InventoryPanel/EquipmentIcon3")?.GetComponent<Image>();

                Assert.That(itemIcon, Is.Not.Null);
                Assert.That(itemIcon.sprite, Is.SameAs(expected));
                Assert.That(itemIcon.preserveAspect, Is.True);
                Assert.That(itemIcon.raycastTarget, Is.False);
                Assert.That(itemIcon.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(32f, 32f)));
                Assert.That(equipmentIcon, Is.Not.Null);
                Assert.That(equipmentIcon.sprite, Is.SameAs(expected));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void ShopPanelView_Render_ShowsBuyAndSellIcons()
        {
            var service = LoadPresentation();
            var root = new GameObject("shop", typeof(RectTransform));
            var view = root.AddComponent<ShopPanelView>();
            try
            {
                view.Initialize(service);
                view.Render(new ShopPanelViewData(
                    "shop.title",
                    10,
                    new[]
                    {
                        new ShopOfferBinding(
                            "offer.sword",
                            "item.frost_longsword.name",
                            20,
                            "item.frost_longsword",
                            ItemSlot.Weapon)
                    },
                    new[]
                    {
                        new ShopSellBinding(
                            "instance.sword",
                            "item.frost_longsword.name",
                            8,
                            "item.frost_longsword",
                            ItemSlot.Weapon)
                    },
                    string.Empty));

                var expected = service.GetItemIcon("item.frost_longsword", "weapon");
                var buyIcon = root.transform.Find("ShopPanel/ListScroll/Viewport/List/Buy_0/Icon")
                    ?.GetComponent<Image>();
                Assert.That(buyIcon, Is.Not.Null);
                Assert.That(buyIcon.sprite, Is.SameAs(expected));
                Assert.That(buyIcon.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(32f, 32f)));

                root.transform.Find("ShopPanel/SellTab").GetComponent<Button>().onClick.Invoke();

                var sellIcon = root.transform.Find("ShopPanel/ListScroll/Viewport/List/Sell_0/Icon")
                    ?.GetComponent<Image>();
                Assert.That(sellIcon, Is.Not.Null);
                Assert.That(sellIcon.sprite, Is.SameAs(expected));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static IPresentationService LoadPresentation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(
                "Assets/Resources/PresentationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            return new PresentationService(catalog, null);
        }
    }
}
