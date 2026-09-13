using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI.Battle
{
    public sealed class BattleHudView : MonoBehaviour
    {
        private readonly List<SkillButtonView> skillButtons = new();

        private Text roundText;
        private Text activeUnitText;
        private Text actionOrderText;
        private Text unitStatsText;
        private Text statusText;
        private RectTransform skillsRoot;
        private Text skillsTitleText;
        private Button endTurnButton;
        private GameObject resultPanel;
        private Text resultText;
        private Button continueButton;
        private Action<string> skillSelected;
        private Action endTurnSelected;
        private Action continueSelected;
        private string renderedSkillOwnerId;

        public Button EndTurnButton => endTurnButton;
        public Button ContinueButton => continueButton;
        public IReadOnlyList<Button> SkillButtons
        {
            get
            {
                var result = new Button[skillButtons.Count];
                for (var i = 0; i < skillButtons.Count; i++)
                    result[i] = skillButtons[i].Button;
                return result;
            }
        }

        public void Initialize(
            Action<string> onSkillSelected,
            Action onEndTurn,
            Action onContinue)
        {
            skillSelected = onSkillSelected ?? throw new ArgumentNullException(nameof(onSkillSelected));
            endTurnSelected = onEndTurn ?? throw new ArgumentNullException(nameof(onEndTurn));
            continueSelected = onContinue ?? throw new ArgumentNullException(nameof(onContinue));

            BuildLayout();
            endTurnButton.onClick.AddListener(() => endTurnSelected());
            continueButton.onClick.AddListener(() => continueSelected());
            resultPanel.SetActive(false);
        }

        public void Render(BattleUiPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            var state = presenter.Engine.State;
            var activeUnit = presenter.Engine.ActiveUnit;
            roundText.text = Key(BattleTextKeys.Round) + ": " + state.Round;

            if (activeUnit == null)
            {
                activeUnitText.text = Key(BattleTextKeys.ActiveUnitNone);
                unitStatsText.text = Key(BattleTextKeys.UnitNone);
                statusText.text = Key(BattleTextKeys.StatusNone);
            }
            else
            {
                activeUnitText.text = Key(BattleTextKeys.ActiveUnit) + ": " +
                                      Key(BattleTextKeys.Unit(activeUnit.DefinitionId));
                unitStatsText.text =
                    Key(BattleTextKeys.Health) + ": " + activeUnit.Health + "/" + activeUnit.Stats.MaxHealth + "\n" +
                    Key(BattleTextKeys.Mana) + ": " + activeUnit.Mana + "/" + activeUnit.Stats.MaxMana + "\n" +
                    Key(BattleTextKeys.Moved) + ": " + Key(BattleTextKeys.Flag(activeUnit.HasMoved)) + "  " +
                    Key(BattleTextKeys.Acted) + ": " + Key(BattleTextKeys.Flag(activeUnit.HasActed));
                statusText.text = activeUnit.Statuses.Count == 0
                    ? Key(BattleTextKeys.StatusNone)
                    : Key(BattleTextKeys.StatusLabel) + ": " + string.Join(
                        ", ",
                        activeUnit.Statuses.Select(status =>
                            Key(BattleTextKeys.StatusKey(status.Type)) +
                            "(" + status.RemainingTurns + ")"));
            }

            actionOrderText.text = Key(BattleTextKeys.TurnOrder) + ": " + string.Join(
                "  ",
                TurnOrder.Build(state.LivingUnits)
                    .Select(unit => Key(BattleTextKeys.Unit(unit.DefinitionId))));

            RebuildSkillButtonsIfNeeded(presenter);
            UpdateSkillButtons(presenter);
            endTurnButton.interactable = presenter.IsPlayerTurn && !presenter.IsFinished;

            resultPanel.SetActive(presenter.IsFinished);
            if (presenter.IsFinished)
            {
                resultText.text = presenter.Outcome == BattleOutcome.PlayerVictory
                    ? Key(BattleTextKeys.PlayerVictory)
                    : Key(BattleTextKeys.EnemyVictory);
            }
        }

        private void BuildLayout()
        {
            var panelColor = new Color(0.07f, 0.085f, 0.12f, 0.96f);
            var top = CreatePanel(
                transform,
                "TopHud",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 150f),
                panelColor);

            roundText = CreateText(
                top.transform,
                "Round",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(30f, 0f),
                new Vector2(300f, 100f),
                30,
                TextAnchor.MiddleLeft);

            activeUnitText = CreateText(
                top.transform,
                "ActiveUnit",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(340f, 0f),
                new Vector2(500f, 100f),
                26,
                TextAnchor.MiddleLeft);

            actionOrderText = CreateText(
                top.transform,
                "TurnOrder",
                new Vector2(0.46f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-30f, -20f),
                24,
                TextAnchor.MiddleRight);

            var statsPanel = CreatePanel(
                transform,
                "Stats",
                new Vector2(0.72f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 310f),
                new Vector2(-20f, 275f),
                panelColor);
            unitStatsText = CreateText(
                statsPanel.transform,
                "UnitStats",
                Vector2.zero,
                new Vector2(1f, 0.67f),
                new Vector2(0.5f, 0.5f),
                new Vector2(20f, 0f),
                new Vector2(-40f, -20f),
                27,
                TextAnchor.UpperLeft);
            statusText = CreateText(
                statsPanel.transform,
                "Status",
                new Vector2(0f, 0f),
                new Vector2(1f, 0.36f),
                new Vector2(0.5f, 0.5f),
                new Vector2(20f, 0f),
                new Vector2(-40f, -20f),
                24,
                TextAnchor.MiddleLeft);

            var skillsPanel = CreatePanel(
                transform,
                "Skills",
                new Vector2(0.72f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 25f),
                new Vector2(-20f, 275f),
                panelColor);
            skillsTitleText = CreateText(
                skillsPanel.transform,
                "SkillsTitle",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -10f),
                new Vector2(-30f, 40f),
                24,
                TextAnchor.MiddleLeft);

            var skillsRootObject = new GameObject(
                "SkillButtons",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            skillsRootObject.transform.SetParent(skillsPanel.transform, false);
            skillsRoot = skillsRootObject.GetComponent<RectTransform>();
            skillsRoot.anchorMin = new Vector2(0f, 0f);
            skillsRoot.anchorMax = new Vector2(1f, 0f);
            skillsRoot.pivot = new Vector2(0.5f, 0f);
            skillsRoot.anchoredPosition = new Vector2(0f, 82f);
            skillsRoot.sizeDelta = new Vector2(-30f, 168f);
            var skillLayout = skillsRootObject.GetComponent<GridLayoutGroup>();
            skillLayout.padding = new RectOffset(5, 5, 5, 5);
            skillLayout.spacing = new Vector2(8f, 8f);
            skillLayout.cellSize = new Vector2(210f, 48f);
            skillLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            skillLayout.constraintCount = 2;
            skillLayout.childAlignment = TextAnchor.UpperCenter;

            endTurnButton = CreateButton(
                skillsPanel.transform,
                "EndTurn",
                BattleTextKeys.EndTurn,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(-24f, 58f));

            var result = CreatePanel(
                transform,
                "Result",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(640f, 320f),
                new Color(0.05f, 0.065f, 0.09f, 1f));
            resultPanel = result;
            resultText = CreateText(
                result.transform,
                "ResultText",
                new Vector2(0f, 0.42f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-40f, -30f),
                34,
                TextAnchor.MiddleCenter);
            continueButton = CreateButton(
                result.transform,
                "Continue",
                BattleTextKeys.Continue,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 36f),
                new Vector2(-180f, 72f));
        }

        private void RebuildSkillButtonsIfNeeded(BattleUiPresenter presenter)
        {
            var ownerId = presenter.Engine.ActiveUnit?.Id;
            if (string.Equals(ownerId, renderedSkillOwnerId, StringComparison.Ordinal))
                return;

            renderedSkillOwnerId = ownerId;
            foreach (var view in skillButtons)
                Destroy(view.Button.gameObject);
            skillButtons.Clear();

            if (presenter.Engine.ActiveUnit == null)
                return;

            foreach (var skill in presenter.Engine
                         .GetOwnedSkills(presenter.Engine.ActiveUnit.Id)
                         .Values
                         .OrderBy(definition => definition.LocalizationKey, StringComparer.Ordinal))
            {
                var button = CreateButton(
                    skillsRoot,
                    "Skill_" + skill.Id,
                    skill.LocalizationKey,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero);
                var skillId = skill.Id;
                button.onClick.AddListener(() => skillSelected(skillId));
                skillButtons.Add(new SkillButtonView(skillId, button, button.image));
            }
        }

        private void UpdateSkillButtons(BattleUiPresenter presenter)
        {
            var activeUnit = presenter.Engine.ActiveUnit;
            var canSelect = activeUnit != null &&
                            presenter.IsPlayerTurn &&
                            !presenter.IsFinished &&
                            !activeUnit.HasActed;

            foreach (var view in skillButtons)
            {
                view.Button.interactable = canSelect;
                view.Background.color = presenter.SelectedSkillId == view.SkillId
                    ? new Color(0.30f, 0.58f, 0.84f, 1f)
                    : new Color(0.20f, 0.31f, 0.46f, 1f);
            }
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            root.GetComponent<Image>().color = color;
            return root;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string localizationKey,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.20f, 0.31f, 0.46f, 1f);
            var button = root.GetComponent<Button>();
            button.targetGraphic = background;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = Key(localizationKey);
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            int fontSize,
            TextAnchor alignment)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = root.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static string Key(string localizationKey) => localizationKey;

        private sealed class SkillButtonView
        {
            public SkillButtonView(string skillId, Button button, Image background)
            {
                SkillId = skillId;
                Button = button;
                Background = background;
            }

            public string SkillId { get; }
            public Button Button { get; }
            public Image Background { get; }
        }
    }
}