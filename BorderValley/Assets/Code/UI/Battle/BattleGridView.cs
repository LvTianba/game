using System;
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI.Battle
{
    public sealed class BattleGridView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, CellView> cells = new();
        private BattleUiPresenter presenter;
        private Action<GridPosition> cellSelected;

        public int CellCount => cells.Count;
        public int UnitCount { get; private set; }

        public void Initialize(Action<GridPosition> onCellSelected)
        {
            cellSelected = onCellSelected ?? throw new ArgumentNullException(nameof(onCellSelected));

            var rect = GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.72f, 1f);
            rect.offsetMin = new Vector2(35f, 25f);
            rect.offsetMax = new Vector2(-25f, -175f);

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.12f, 0.16f, 1f);

            var layout = gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = new Vector2(6f, 6f);
            layout.cellSize = new Vector2(125f, 125f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        public void Render(BattleUiPresenter value)
        {
            presenter = value ?? throw new ArgumentNullException(nameof(value));
            EnsureCells(presenter.Engine.State.Map);

            foreach (var cell in cells.Values)
            {
                cell.UnitLabel.text = string.Empty;
                cell.Background.color = GetHighlightColor(presenter.GetHighlight(cell.Position));
                cell.Button.interactable = !presenter.IsFinished && presenter.IsPlayerTurn;
            }

            UnitCount = 0;
            foreach (var unit in presenter.Engine.State.LivingUnits)
            {
                if (!cells.TryGetValue(unit.Position, out var cell))
                    continue;

                cell.UnitLabel.text = unit.DefinitionId;
                cell.UnitLabel.color = unit.Team == Team.Player
                    ? new Color(0.48f, 0.78f, 1f, 1f)
                    : new Color(1f, 0.48f, 0.42f, 1f);
                UnitCount++;
            }
        }

        public void Tap(GridPosition position)
        {
            if (cells.TryGetValue(position, out var cell))
                cell.Button.onClick.Invoke();
        }

        private void EnsureCells(BattleMap map)
        {
            if (cells.Count == map.Width * map.Height &&
                cells.ContainsKey(new GridPosition(map.Width - 1, map.Height - 1)))
            {
                return;
            }

            foreach (var cell in cells.Values)
                Destroy(cell.Button.gameObject);

            cells.Clear();
            var layout = GetComponent<GridLayoutGroup>();
            layout.constraintCount = map.Width;

            for (var y = map.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < map.Width; x++)
                    CreateCell(new GridPosition(x, y));
            }
        }

        private void CreateCell(GridPosition position)
        {
            var root = new GameObject(
                $"Cell_{position.X}_{position.Y}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            root.transform.SetParent(transform, false);

            var background = root.GetComponent<Image>();
            background.color = GetHighlightColor(BattleHighlightKind.None);
            var button = root.GetComponent<Button>();
            button.targetGraphic = background;

            var labelObject = new GameObject("Unit", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 4f);
            labelRect.offsetMax = new Vector2(-4f, -4f);
            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var captured = position;
            button.onClick.AddListener(() => cellSelected(captured));
            cells.Add(position, new CellView(position, button, background, label));
        }

        private static Color GetHighlightColor(BattleHighlightKind highlight) =>
            highlight switch
            {
                BattleHighlightKind.Move => new Color(0.18f, 0.43f, 0.82f, 1f),
                BattleHighlightKind.SkillRange => new Color(0.82f, 0.67f, 0.16f, 1f),
                BattleHighlightKind.Target => new Color(0.78f, 0.20f, 0.20f, 1f),
                _ => new Color(0.18f, 0.21f, 0.27f, 1f)
            };

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private sealed class CellView
        {
            public CellView(
                GridPosition position,
                Button button,
                Image background,
                Text unitLabel)
            {
                Position = position;
                Button = button;
                Background = background;
                UnitLabel = unitLabel;
            }

            public GridPosition Position { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Text UnitLabel { get; }
        }
    }
}