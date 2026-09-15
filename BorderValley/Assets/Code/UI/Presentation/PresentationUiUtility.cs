using BorderValley.Core;
using BorderValley.Data.Items;
using BorderValley.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI
{
    public static class PresentationUiUtility
    {
        public static IPresentationService GetOrNull()
        {
            var context = GameBootstrapper.Context;
            return context != null && context.TryGet<IPresentationService>(out var presentation)
                ? presentation
                : null;
        }

        public static void ApplyPanel(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        public static void ApplyButton(Button button, Sprite normal, Sprite pressed)
        {
            if (button == null)
                return;

            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null)
                return;

            if (normal != null)
            {
                image.sprite = normal;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                button.targetGraphic = image;
                button.transition = Selectable.Transition.SpriteSwap;
            }

            if (pressed != null)
            {
                var state = button.spriteState;
                state.pressedSprite = pressed;
                state.highlightedSprite = pressed;
                button.spriteState = state;
            }
        }

        public static Sprite ResolvePanel(IPresentationService presentation) =>
            presentation?.GetUiSprite("ui.panel");

        public static Sprite ResolveButton(IPresentationService presentation) =>
            presentation?.GetUiSprite("ui.button");

        public static Sprite ResolvePressedButton(IPresentationService presentation) =>
            presentation?.GetUiSprite("ui.button.pressed");

        public static string ResolveItemSlotId(ItemSlot slot) => slot switch
        {
            ItemSlot.Weapon => "weapon",
            ItemSlot.Offhand => "shield",
            ItemSlot.Head => "helmet",
            ItemSlot.Body => "armor",
            ItemSlot.Accessory => "accessory",
            ItemSlot.Boots => "boots",
            _ => slot.ToString().ToLowerInvariant()
        };
    }
}
