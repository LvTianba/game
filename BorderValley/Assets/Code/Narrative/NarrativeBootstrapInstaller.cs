using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;
using UnityEngine;

namespace BorderValley.Narrative
{
    public sealed class NarrativeBootstrapInstaller : MonoBehaviour, IGameServiceInstaller
    {
        public void Install(GameContext context, ICollection<ISaveParticipant> participants)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (participants == null) throw new ArgumentNullException(nameof(participants));

            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null)
                throw new InvalidOperationException("Required ContentCatalog resource is missing.");

            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var items = context.Get<IReadOnlyDictionary<string, ItemDefinition>>();
            var state = new NarrativeStateService(catalog.All);
            var rewardService = new QuestRewardService(inventory, progression, items, state);
            var quests = catalog.All
                .OfType<QuestDefinition>()
                .ToDictionary(quest => quest.Id, StringComparer.Ordinal);
            var questService = new QuestService(quests, state, inventory, rewardService);

            context.Register(state);
            context.Register<IQuestRewardService>(rewardService);
            context.Register(rewardService);
            context.Register(questService);
            participants.Add(state);
        }
    }
}
