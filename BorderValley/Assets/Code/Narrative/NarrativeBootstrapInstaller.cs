using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
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
            var economy = context.Get<EconomyService>();
            var items = context.Get<IReadOnlyDictionary<string, ItemDefinition>>();
            var state = new NarrativeStateService(catalog.All);
            var rewardService = new QuestRewardService(inventory, progression, items, state);
            var quests = catalog.All
                .OfType<QuestDefinition>()
                .ToDictionary(quest => quest.Id, StringComparer.Ordinal);
            var questService = new QuestService(quests, state, inventory, rewardService);
            var dialogueService = new DialogueService(catalog.All, state, questService, inventory, progression);
            var shopService = new ShopService(catalog.All, state, economy);
            var areas = catalog.All
                .OfType<WorldAreaDefinition>()
                .ToDictionary(area => area.Id, StringComparer.Ordinal);
            var encounters = catalog.All
                .OfType<WorldEncounterDefinition>()
                .ToDictionary(encounter => encounter.EncounterId, StringComparer.Ordinal);
            var rewardTables = catalog.All
                .OfType<ItemDropTableDefinition>()
                .ToDictionary(table => table.Id, StringComparer.Ordinal);

            context.Register(catalog);
            context.Register(state);
            context.Register<IQuestRewardService>(rewardService);
            context.Register(rewardService);
            context.Register(questService);
            context.Register(dialogueService);
            context.Register(shopService);
            context.Register<IReadOnlyDictionary<string, WorldAreaDefinition>>(areas);
            context.Register<IReadOnlyDictionary<string, WorldEncounterDefinition>>(encounters);
            context.Register<IReadOnlyDictionary<string, ItemDropTableDefinition>>(rewardTables);
            participants.Add(state);
        }
    }
}
