using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BorderValley.UI.World
{
    public interface IDialoguePanelView
    {
        event Action<int> ChoiceSelected;
        event Action ContinueRequested;
        event Action CloseRequested;
        void SetVisible(bool visible);
        void Render(DialoguePanelViewData data);
    }

    public sealed class DialogueChoiceBinding
    {
        public DialogueChoiceBinding(int index, string choiceId, string labelKey)
        {
            Index = index;
            ChoiceId = choiceId ?? string.Empty;
            LabelKey = labelKey ?? string.Empty;
        }

        public int Index { get; }
        public string ChoiceId { get; }
        public string LabelKey { get; }
    }

    public sealed class DialoguePanelViewData
    {
        public DialoguePanelViewData(
            string speakerKey,
            string textKey,
            IEnumerable<DialogueChoiceBinding> choices,
            bool showContinue,
            bool showClose,
            string errorKey)
        {
            SpeakerKey = speakerKey ?? string.Empty;
            TextKey = textKey ?? string.Empty;
            Choices = (choices ?? Array.Empty<DialogueChoiceBinding>()).ToArray();
            ShowContinue = showContinue;
            ShowClose = showClose;
            ErrorKey = errorKey ?? string.Empty;
        }

        public string SpeakerKey { get; }
        public string TextKey { get; }
        public IReadOnlyList<DialogueChoiceBinding> Choices { get; }
        public bool ShowContinue { get; }
        public bool ShowClose { get; }
        public string ErrorKey { get; }
    }

    public sealed class DialoguePanelView : MonoBehaviour, IDialoguePanelView
    {
        private readonly List<GameObject> choiceObjects = new();
        private GameObject panelRoot;
        private Text speakerLabel;
        private Text textLabel;
        private Text errorLabel;
        private Button continueButton;
        private Button closeButton;

        public event Action<int> ChoiceSelected;
        public event Action ContinueRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            for (var index = 0; index < choiceObjects.Count; index++)
            {
                var button = choiceObjects[index] == null
                    ? null
                    : choiceObjects[index].GetComponent<Button>();
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
            choiceObjects.Clear();
            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            panelRoot.SetActive(visible);
        }

        public void Render(DialoguePanelViewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            EnsureBuilt();
            speakerLabel.text = string.IsNullOrWhiteSpace(data.SpeakerKey)
                ? WorldTextKeys.DialogueSpeakerUnknown
                : data.SpeakerKey;
            textLabel.text = data.TextKey;
            errorLabel.text = data.ErrorKey;
            continueButton.gameObject.SetActive(data.ShowContinue);
            closeButton.gameObject.SetActive(data.ShowClose);
            RebuildChoices(data.Choices);
        }

        private void RebuildChoices(IReadOnlyList<DialogueChoiceBinding> choices)
        {
            foreach (var choice in choiceObjects)
            {
                if (choice != null)
                {
                    var button = choice.GetComponent<Button>();
                    if (button != null)
                        button.onClick.RemoveAllListeners();
                    choice.SetActive(false);
                    WorldPanelViewFactory.DestroyForMode(choice);
                }
            }
            choiceObjects.Clear();

            var choiceRoot = panelRoot.transform.Find("Choices") as RectTransform;
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                var choiceIndex = choice.Index;
                var button = WorldPanelViewFactory.CreateButton(
                    choiceRoot,
                    "Choice_" + index,
                    choice.LabelKey,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    () => ChoiceSelected?.Invoke(choiceIndex));
                var rect = button.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(0f, -((index + 1) * 38f));
                rect.offsetMax = new Vector2(0f, -(index * 38f));
                choiceObjects.Add(button.gameObject);
            }
        }

        private void EnsureBuilt()
        {
            if (panelRoot != null) return;

            panelRoot = WorldPanelViewFactory.CreatePanel(transform, "DialoguePanel");
            speakerLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Speaker",
                WorldTextKeys.DialogueSpeakerUnknown,
                22,
                new Vector2(0.08f, 0.82f),
                new Vector2(0.92f, 0.94f),
                TextAnchor.MiddleLeft);
            textLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Text",
                string.Empty,
                20,
                new Vector2(0.08f, 0.38f),
                new Vector2(0.92f, 0.8f),
                TextAnchor.UpperLeft);
            errorLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Error",
                string.Empty,
                16,
                new Vector2(0.08f, 0.22f),
                new Vector2(0.92f, 0.34f),
                TextAnchor.MiddleLeft);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);

            var choices = new GameObject("Choices", typeof(RectTransform));
            choices.transform.SetParent(panelRoot.transform, false);
            var choicesRect = choices.GetComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0.12f, 0.24f);
            choicesRect.anchorMax = new Vector2(0.88f, 0.38f);
            choicesRect.offsetMin = Vector2.zero;
            choicesRect.offsetMax = Vector2.zero;

            continueButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "Continue",
                WorldTextKeys.DialogueContinue,
                new Vector2(0.27f, 0.06f),
                new Vector2(0.47f, 0.16f),
                () => ContinueRequested?.Invoke());
            closeButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "Close",
                WorldTextKeys.DialogueClose,
                new Vector2(0.53f, 0.06f),
                new Vector2(0.73f, 0.16f),
                () => CloseRequested?.Invoke());
            panelRoot.SetActive(false);
        }
    }

    internal static class WorldPanelViewFactory
    {
        public static GameObject CreatePanel(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>();
            image.color = new Color(0.05f, 0.08f, 0.14f, 0.97f);
            image.raycastTarget = true;
            return root;
        }

        public static Text CreateText(
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = key;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string key,
            Vector2 min,
            Vector2 max,
            UnityAction action)
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
            CreateText(
                root.transform,
                "Label",
                key,
                16,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            return button;
        }

        public static void DestroyForMode(GameObject value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
