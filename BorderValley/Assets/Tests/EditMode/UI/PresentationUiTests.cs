using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BorderValley.Core.SceneManagement;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;
using BorderValley.Narrative;
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
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
            created.Clear();
        }

        [Test]
        public void MainMenuStart_PlaysConfirmAndLoadsWorld()
        {
            var calls = new RecordingPresentationService();
            var loader = new FakeSceneLoader();
            var presenter = new MainMenuPresenter(loader, calls);

            presenter.StartNewGame();

            Assert.That(calls.SfxCalls, Does.Contain("sfx.ui.confirm"));
            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { "World" }));
        }

        [Test]
        public void MainMenuLoadFailure_PlaysError()
        {
            var calls = new RecordingPresentationService();
            var loader = new FakeSceneLoader
            {
                LoadException = new InvalidOperationException("load failed")
            };
            var presenter = new MainMenuPresenter(loader, calls);

            presenter.StartNewGame();

            Assert.That(calls.SfxCalls, Does.Contain("sfx.ui.error"));
        }

        [Test]
        public void DialogueOpenAndContinue_PlaysPageThenClosePlaysCancel()
        {
            var calls = new RecordingPresentationService();
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(
                CreateDialogueService(
                    DialogueNode("node.start", nextNodeId: "node.end"),
                    DialogueNode("node.end")),
                view,
                calls);

            Assert.That(presenter.Open("npc.elder"), Is.True);
            Assert.That(presenter.Continue(), Is.True);
            presenter.Close();

            Assert.That(calls.SfxCalls, Is.EqualTo(new[]
            {
                "sfx.dialogue.page",
                "sfx.dialogue.page",
                "sfx.ui.cancel"
            }));
        }

        [Test]
        public void DialogueFailurePaths_PlayErrorCue()
        {
            var calls = new RecordingPresentationService();
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(
                CreateDialogueService(DialogueNode("node.start")),
                view,
                calls);

            Assert.That(presenter.Open("npc.missing"), Is.False);
            Assert.That(presenter.Continue(), Is.False);
            Assert.That(presenter.SelectChoice(0), Is.False);

            Assert.That(
                calls.SfxCalls.Count(cueId => cueId == "sfx.ui.error"),
                Is.EqualTo(3));
        }

        [Test]
        public void ShopBuyAndSell_PlaysTransactionalSoundsAndFailurePlaysError()
        {
            var calls = new RecordingPresentationService();
            var runtime = CreateShopRuntime();
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(
                runtime.Shop,
                runtime.Inventory,
                runtime.Economy,
                runtime.State,
                view,
                calls);

            Assert.That(presenter.Open("shop.general"), Is.True);
            view.RaiseBuy("offer.sword");
            view.RaiseSell(runtime.Inventory.Items.Single().InstanceId);
            view.RaiseBuy("offer.missing");

            Assert.That(calls.SfxCalls, Does.Contain("sfx.shop.buy"));
            Assert.That(calls.SfxCalls, Does.Contain("sfx.shop.sell"));
            Assert.That(calls.SfxCalls, Does.Contain("sfx.ui.error"));
        }

        [Test]
        public void InventoryActions_PlayEquipmentCancelRewardAndErrorSounds()
        {
            var calls = new RecordingPresentationService();
            var runtime = CreateInventoryRuntime();
            var presenter = new InventoryUiPresenter(
                runtime.Inventory,
                null,
                runtime.Progression,
                null,
                null,
                runtime.Definitions,
                null,
                calls);
            presenter.Select("sword.0");

            Assert.That(presenter.EquipSelected("class.warrior"), Is.True);
            Assert.That(presenter.Unequip(ItemSlot.Weapon), Is.True);
            Assert.That(presenter.EquipSelected("class.mage"), Is.False);
            presenter.RestParty(10);

            Assert.That(calls.SfxCalls, Does.Contain("sfx.inventory.equip"));
            Assert.That(calls.SfxCalls, Does.Contain("sfx.ui.cancel"));
            Assert.That(calls.SfxCalls, Does.Contain("sfx.world.reward"));
            Assert.That(calls.SfxCalls, Does.Contain("sfx.ui.error"));
        }

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

        private DialogueService CreateDialogueService(params DialogueNodeDefinition[] nodes)
        {
            var npc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            npc.EditorConfigure("npc.elder", "npc.elder.name", "dialogue.test", string.Empty);
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure("dialogue.test", "node.start", nodes);

            var definitions = new ContentDefinition[] { npc, dialogue };
            var state = new NarrativeStateService(definitions);
            var inventory = new InventoryService(
                8,
                new Dictionary<string, ItemDefinition>(StringComparer.Ordinal),
                0);
            var rewards = new QuestRewardService(inventory, null, inventory.Definitions, state);
            var quests = new QuestService(
                new Dictionary<string, QuestDefinition>(StringComparer.Ordinal),
                state,
                inventory,
                rewards);
            return new DialogueService(definitions, state, quests, inventory);
        }

        private static DialogueNodeDefinition DialogueNode(
            string nodeId,
            string nextNodeId = "") =>
            new(
                nodeId,
                "npc.elder",
                nodeId + ".text",
                Array.Empty<DialogueConditionDefinition>(),
                Array.Empty<DialogueActionDefinition>(),
                Array.Empty<DialogueChoiceDefinition>(),
                nextNodeId);

        private ShopRuntime CreateShopRuntime()
        {
            var sword = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            sword.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                Array.Empty<string>(),
                false,
                100,
                Array.Empty<StatValue>());
            var owner = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            owner.EditorConfigure("npc.merchant", "npc.merchant.name", string.Empty, "shop.general");
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                "shop.general",
                "shop.general.name",
                string.Empty,
                new[]
                {
                    new ShopOfferDefinition(
                        "offer.sword",
                        sword,
                        ItemRarity.Common,
                        1,
                        Array.Empty<AffixDefinition>())
                });

            var definitions = new ContentDefinition[] { sword, owner, shop };
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [sword.Id] = sword
            };
            var state = new NarrativeStateService(definitions);
            Assert.That(state.MarkShopUnlocked(shop.Id), Is.True);
            var inventory = new InventoryService(8, items, 100);
            var economy = new EconomyService(
                inventory,
                items,
                new Dictionary<string, AffixDefinition>(StringComparer.Ordinal));
            var service = new ShopService(definitions, state, economy);
            return new ShopRuntime(state, inventory, economy, service);
        }

        private InventoryRuntime CreateInventoryRuntime()
        {
            var sword = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            sword.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                new[] { "class.warrior" },
                false,
                100,
                Array.Empty<StatValue>());
            var character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            character.EditorConfigure(
                "class.warrior",
                "class.warrior.name",
                Array.Empty<StatValue>(),
                Array.Empty<StatValue>(),
                Array.Empty<string>(),
                Array.Empty<SkillUnlock>());

            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [sword.Id] = sword
            };
            var inventory = new InventoryService(8, definitions, 0);
            Assert.That(
                inventory.TryAdd(
                    new ItemInstance(
                        "sword.0",
                        sword.Id,
                        1,
                        ItemRarity.Common,
                        Array.Empty<AffixInstance>()),
                    out var error),
                Is.True,
                error);
            var progression = new PartyProgressionService(
                new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal)
                {
                    [character.Id] = character
                },
                new[]
                {
                    new PartyMemberState("player.warrior", character.Id, 1, 0, 0, 1, 0)
                });
            return new InventoryRuntime(
                inventory,
                progression,
                new Dictionary<string, AffixDefinition>(StringComparer.Ordinal));
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public List<string> LoadedScenes { get; } = new();
            public string ActiveSceneName => string.Empty;
            public Exception LoadException { get; set; }

            public Task LoadAsync(string sceneName)
            {
                LoadedScenes.Add(sceneName);
                if (LoadException != null)
                    return Task.FromException(LoadException);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingPresentationService : IPresentationService
        {
            public bool IsAvailable => true;
            public List<string> MusicCalls { get; } = new();
            public List<string> SfxCalls { get; } = new();
            public int StopMusicCount { get; private set; }

            public VisualClip GetVisualClip(string clipId) => null;
            public Sprite GetSprite(string spriteId) => null;
            public AudioCue GetAudioCue(string cueId) => null;
            public string GetAreaMusicCueId(string areaId) =>
                areaId?.Replace("area.", "bgm.world.") ?? string.Empty;
            public string GetCharacterVisualPrefix(string definitionId) => string.Empty;
            public Sprite GetNpcPortrait(string npcId) => null;
            public Sprite GetItemIcon(string itemDefinitionId, string slotId) => null;
            public Sprite GetUiSprite(string partId) => null;
            public void PlayMusic(string cueId) => MusicCalls.Add(cueId);
            public void PlaySfx(string cueId) => SfxCalls.Add(cueId);
            public void StopMusic() => StopMusicCount++;
        }

        private sealed class ShopRuntime
        {
            public ShopRuntime(
                NarrativeStateService state,
                InventoryService inventory,
                EconomyService economy,
                ShopService shop)
            {
                State = state;
                Inventory = inventory;
                Economy = economy;
                Shop = shop;
            }

            public NarrativeStateService State { get; }
            public InventoryService Inventory { get; }
            public EconomyService Economy { get; }
            public ShopService Shop { get; }
        }

        private sealed class InventoryRuntime
        {
            public InventoryRuntime(
                InventoryService inventory,
                PartyProgressionService progression,
                IReadOnlyDictionary<string, AffixDefinition> definitions)
            {
                Inventory = inventory;
                Progression = progression;
                Definitions = definitions;
            }

            public InventoryService Inventory { get; }
            public PartyProgressionService Progression { get; }
            public IReadOnlyDictionary<string, AffixDefinition> Definitions { get; }
        }

        private sealed class StubDialoguePanelView : IDialoguePanelView
        {
            public event Action<int> ChoiceSelected;
            public event Action ContinueRequested;
            public event Action CloseRequested;

            public void SetVisible(bool visible)
            {
            }

            public void Render(DialoguePanelViewData data)
            {
            }

            public void RaiseChoice(int index) => ChoiceSelected?.Invoke(index);
            public void RaiseContinue() => ContinueRequested?.Invoke();
            public void RaiseClose() => CloseRequested?.Invoke();
        }

        private sealed class StubShopPanelView : IShopPanelView
        {
            public event Action<string> BuyRequested;
            public event Action<string> SellRequested;
            public event Action CloseRequested;

            public void SetVisible(bool visible)
            {
            }

            public void Render(ShopPanelViewData data)
            {
            }

            public void RaiseBuy(string offerId) => BuyRequested?.Invoke(offerId);
            public void RaiseSell(string instanceId) => SellRequested?.Invoke(instanceId);
            public void RaiseClose() => CloseRequested?.Invoke();
        }
    }
}
