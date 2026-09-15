using System;
using System.Linq;
using BorderValley.Narrative;

namespace BorderValley.UI.World
{
    public sealed class DialogueUiPresenter
    {
        private readonly DialogueService service;
        private readonly IDialoguePanelView view;
        private DialogueSession session;
        private DialogueSession openedShopSourceSession;
        private string openedShopSourceId = string.Empty;
        private string pendingOpenedShopId = string.Empty;

        public DialogueUiPresenter(DialogueService service, IDialoguePanelView view)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.view.ChoiceSelected += index => SelectChoice(index);
            this.view.ContinueRequested += () => Continue();
            this.view.CloseRequested += Close;
        }

        public bool IsOpen { get; private set; }
        public string LastErrorKey { get; private set; } = string.Empty;
        public string PendingOpenedShopId => pendingOpenedShopId;

        public bool Open(string npcId)
        {
            ClearPendingOpenedShop();
            if (!service.TryStart(npcId, out var started, out var error))
            {
                session = null;
                return Fail(error, true);
            }

            session = started;
            SynchronizeOpenedShop();
            LastErrorKey = string.Empty;
            IsOpen = true;
            view.SetVisible(true);
            Render();
            return true;
        }

        public bool SelectChoice(int index)
        {
            if (session == null)
                return Fail(NarrativeTextKeys.DialogueSessionRequired, true);
            if (!service.TryChoose(session, index, out var error))
                return Fail(error, true);

            LastErrorKey = string.Empty;
            SynchronizeOpenedShop();
            Render();
            return true;
        }

        public bool Continue()
        {
            if (session == null)
                return Fail(NarrativeTextKeys.DialogueSessionRequired, true);
            if (!service.TryContinue(session, out var error))
                return Fail(error, true);

            LastErrorKey = string.Empty;
            SynchronizeOpenedShop();
            Render();
            return true;
        }

        public void Close()
        {
            session = null;
            LastErrorKey = string.Empty;
            IsOpen = false;
            ClearPendingOpenedShop();
            view.SetVisible(false);
            view.Render(new DialoguePanelViewData(
                string.Empty,
                string.Empty,
                Array.Empty<DialogueChoiceBinding>(),
                false,
                false,
                string.Empty));
        }

        public string ConsumeOpenedShopId()
        {
            var result = pendingOpenedShopId;
            pendingOpenedShopId = string.Empty;
            return result;
        }

        private void Render()
        {
            if (session == null)
            {
                view.Render(new DialoguePanelViewData(
                    string.Empty,
                    string.Empty,
                    Array.Empty<DialogueChoiceBinding>(),
                    false,
                    true,
                    LastErrorKey));
                return;
            }

            var choices = session.VisibleChoices
                .Select((choice, index) => new DialogueChoiceBinding(index, choice.ChoiceId, choice.LabelKey))
                .ToArray();
            var showContinue = choices.Length == 0 &&
                               !session.IsComplete &&
                               !string.IsNullOrWhiteSpace(session.CurrentNode.NextNodeId);
            var speakerNpcId = session.CurrentNode.SpeakerNpcId;
            view.Render(new DialoguePanelViewData(
                WorldTextKeys.NpcSpeakerKey(speakerNpcId),
                session.ShouldSkipCurrentNodeText ? string.Empty : session.CurrentNode.TextKey,
                choices,
                showContinue,
                true,
                LastErrorKey,
                speakerNpcId,
                PresentationUiUtility.GetOrNull()?.GetNpcPortrait(speakerNpcId)));
        }

        private bool Fail(string error, bool keepOpen)
        {
            LastErrorKey = string.IsNullOrWhiteSpace(error)
                ? WorldTextKeys.ServiceUnavailable
                : error;
            IsOpen = keepOpen;
            view.SetVisible(keepOpen);
            Render();
            return false;
        }

        private void SynchronizeOpenedShop()
        {
            if (session == null || string.IsNullOrWhiteSpace(session.OpenedShopId))
                return;
            if (ReferenceEquals(openedShopSourceSession, session) &&
                string.Equals(openedShopSourceId, session.OpenedShopId, StringComparison.Ordinal))
                return;

            pendingOpenedShopId = session.OpenedShopId;
            openedShopSourceSession = session;
            openedShopSourceId = session.OpenedShopId;
        }

        private void ClearPendingOpenedShop()
        {
            pendingOpenedShopId = string.Empty;
            openedShopSourceSession = null;
            openedShopSourceId = string.Empty;
        }
    }
}
