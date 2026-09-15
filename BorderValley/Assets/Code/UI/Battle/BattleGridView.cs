using System;
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI.Battle
{
    public sealed class BattleGridView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, CellView> cells = new();
        private readonly Dictionary<string, CellView> unitCells =
            new(StringComparer.Ordinal);

        private BattleUiPresenter presenter;
        private IPresentationService presentation;
        private Action<GridPosition> cellSelected;

        public int CellCount => cells.Count;
        public int UnitCount { get; private set; }
        public int RenderedUnitSpriteCount { get; private set; }
        public Vector2Int UnitSpriteSize { get; private set; }

        public void Initialize(Action<GridPosition> onCellSelected) =>
            Initialize(onCellSelected, new NullPresentationService());

        public void Initialize(
            Action<GridPosition> onCellSelected,
            IPresentationService presentationService)
        {
            cellSelected = onCellSelected ?? throw new ArgumentNullException(nameof(onCellSelected));
            presentation = presentationService ?? new NullPresentationService();

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

        public void Render(BattleUiPresenter value) => Render(value, presentation);

        public void Render(
            BattleUiPresenter value,
            IPresentationService presentationService)
        {
            presenter = value ?? throw new ArgumentNullException(nameof(value));
            presentation = presentationService ?? new NullPresentationService();
            EnsureCells(presenter.Engine.State.Map);

            foreach (var cell in cells.Values)
            {
                cell.UnitLabel.text = string.Empty;
                cell.UnitImage.sprite = null;
                cell.VisualPrefix = string.Empty;
                cell.Background.color = GetHighlightColor(presenter.GetHighlight(cell.Position));
                cell.Button.interactable = !presenter.IsFinished && presenter.IsPlayerTurn;
            }

            unitCells.Clear();
            UnitCount = 0;
            RenderedUnitSpriteCount = 0;
            UnitSpriteSize = Vector2Int.zero;

            foreach (var unit in presenter.Engine.State.Units)
            {
                if (!cells.TryGetValue(unit.Position, out var cell))
                    continue;

                var prefix = ResolveVisualPrefix(presentation, unit.DefinitionId);
                var idleClip = ResolveClip(presentation, prefix + ".idle");
                var idleSprite = FirstFrame(idleClip);
                cell.UnitLabel.text = unit.DefinitionId;
                cell.UnitLabel.color = unit.Team == Team.Player
                    ? new Color(0.48f, 0.78f, 1f, 1f)
                    : new Color(1f, 0.48f, 0.42f, 1f);
                cell.UnitImage.sprite = idleSprite;
                cell.VisualPrefix = prefix;
                if (idleClip != null && idleSprite != null)
                    cell.UnitAnimator.Play(idleClip);

                unitCells[unit.Id] = cell;
                if (idleSprite != null)
                {
                    RenderedUnitSpriteCount++;
                    if (UnitSpriteSize == Vector2Int.zero)
                    {
                        UnitSpriteSize = new Vector2Int(
                            Mathf.RoundToInt(idleSprite.rect.width),
                            Mathf.RoundToInt(idleSprite.rect.height));
                    }
                }

                if (unit.IsAlive)
                    UnitCount++;
            }
        }

        public void PlayEvent(
            BattlePresentationEvent value,
            IPresentationService presentationService)
        {
            if (presentationService == null ||
                !unitCells.TryGetValue(value.UnitId, out var cell) ||
                string.IsNullOrWhiteSpace(cell.VisualPrefix))
            {
                return;
            }

            var state = value.Kind switch
            {
                BattlePresentationEventKind.Move => "move",
                BattlePresentationEventKind.Attack => "attack",
                BattlePresentationEventKind.Hit => "hit",
                BattlePresentationEventKind.Down => "down",
                _ => "idle"
            };
            var clip = ResolveClip(presentationService, cell.VisualPrefix + "." + state);
            if (clip == null)
                clip = ResolveClip(presentationService, cell.VisualPrefix + ".idle");
            if (clip != null)
                cell.UnitAnimator.Play(clip);
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
            unitCells.Clear();
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

            var imageObject = new GameObject(
                "UnitImage",
                typeof(RectTransform),
                typeof(Image),
                typeof(SpriteAnimator));
            imageObject.transform.SetParent(root.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.08f, 0.20f);
            imageRect.anchorMax = new Vector2(0.92f, 0.96f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            var unitImage = imageObject.GetComponent<Image>();
            unitImage.preserveAspect = true;
            unitImage.raycastTarget = false;

            var labelObject = new GameObject("UnitLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.22f);
            labelRect.offsetMin = new Vector2(2f, 1f);
            labelRect.offsetMax = new Vector2(-2f, -1f);
            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = 10;
            label.alignment = TextAnchor.LowerCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var captured = position;
            button.onClick.AddListener(() => cellSelected(captured));
            cells.Add(
                position,
                new CellView(
                    position,
                    button,
                    background,
                    unitImage,
                    label,
                    imageObject.GetComponent<SpriteAnimator>()));
        }

        private static string ResolveVisualPrefix(
            IPresentationService presentationService,
            string definitionId)
        {
            var prefix = presentationService.GetCharacterVisualPrefix(definitionId);
            if (!string.IsNullOrWhiteSpace(prefix) &&
                !string.Equals(prefix, "battle.unit", StringComparison.Ordinal))
            {
                return prefix;
            }

            var legacyPrefix = BattleTextKeys.Unit(definitionId);
            return string.Equals(legacyPrefix, "battle.unit.unknown", StringComparison.Ordinal)
                ? prefix
                : legacyPrefix;
        }

        private static VisualClip ResolveClip(
            IPresentationService presentationService,
            string clipId)
        {
            var clip = presentationService.GetVisualClip(clipId);
            return clip != null && string.Equals(clip.Id, clipId, StringComparison.Ordinal)
                ? clip
                : null;
        }

        private static Sprite FirstFrame(VisualClip clip)
        {
            if (clip?.Frames != null)
            {
                foreach (var frame in clip.Frames)
                {
                    if (frame != null)
                        return frame;
                }
            }

            return clip?.Fallback;
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
                Image unitImage,
                Text unitLabel,
                SpriteAnimator unitAnimator)
            {
                Position = position;
                Button = button;
                Background = background;
                UnitImage = unitImage;
                UnitLabel = unitLabel;
                UnitAnimator = unitAnimator;
            }

            public GridPosition Position { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Image UnitImage { get; }
            public Text UnitLabel { get; }
            public SpriteAnimator UnitAnimator { get; }
            public string VisualPrefix { get; set; } = string.Empty;
        }
    }
}
