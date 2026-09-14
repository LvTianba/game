using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Narrative;

namespace BorderValley.Narrative
{
    public sealed class DialogueSession
    {
        private readonly Func<DialogueChoiceDefinition, bool> choiceIsVisible;
        private IReadOnlyList<DialogueChoiceDefinition> allChoices =
            Array.Empty<DialogueChoiceDefinition>();

        internal DialogueSession(
            DialogueDefinition dialogue,
            DialogueNodeDefinition currentNode,
            bool isCurrentNodeRead,
            Func<DialogueChoiceDefinition, bool> choiceIsVisible)
        {
            Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            CurrentNode = currentNode ?? throw new ArgumentNullException(nameof(currentNode));
            IsCurrentNodeRead = isCurrentNodeRead;
            this.choiceIsVisible = choiceIsVisible ?? throw new ArgumentNullException(nameof(choiceIsVisible));
        }

        public DialogueDefinition Dialogue { get; }
        public DialogueNodeDefinition CurrentNode { get; private set; }
        public IReadOnlyList<DialogueChoiceDefinition> VisibleChoices =>
            allChoices.Where(choiceIsVisible).ToArray();
        public string OpenedShopId { get; private set; } = string.Empty;
        public bool IsComplete { get; private set; }
        public bool IsCurrentNodeRead { get; private set; }
        public bool ShouldSkipCurrentNodeText => IsCurrentNodeRead;

        internal void EnterNode(
            DialogueNodeDefinition node,
            bool isCurrentNodeRead,
            IReadOnlyList<DialogueChoiceDefinition> choices,
            string openedShopId)
        {
            CurrentNode = node ?? throw new ArgumentNullException(nameof(node));
            IsCurrentNodeRead = isCurrentNodeRead;
            allChoices = choices ?? Array.Empty<DialogueChoiceDefinition>();
            if (!string.IsNullOrWhiteSpace(openedShopId)) OpenedShopId = openedShopId;
            IsComplete = allChoices.Count == 0 && string.IsNullOrWhiteSpace(node.NextNodeId);
        }

        internal void SetOpenedShopId(string shopId)
        {
            if (!string.IsNullOrWhiteSpace(shopId)) OpenedShopId = shopId;
        }

        internal void Complete()
        {
            allChoices = Array.Empty<DialogueChoiceDefinition>();
            IsComplete = true;
        }
    }
}
