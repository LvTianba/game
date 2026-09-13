using System;
using System.Collections.Generic;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Items;
using UnityEngine;

namespace BorderValley.Inventory
{
    public sealed class InventoryBootstrapInstaller : MonoBehaviour, IGameServiceInstaller
    {
        public void Install(GameContext context, ICollection<ISaveParticipant> participants)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (participants == null) throw new ArgumentNullException(nameof(participants));

            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            var characters = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog != null)
            {
                foreach (var definition in catalog.All)
                {
                    switch (definition)
                    {
                        case ItemDefinition item:
                            items[item.Id] = item;
                            break;
                        case CharacterDefinition character:
                            characters[character.Id] = character;
                            break;
                    }
                }
            }

            var inventory = new InventoryService(30, items, 100);
            context.Register(inventory);
            context.Register<IReadOnlyDictionary<string, ItemDefinition>>(items);
            context.Register<IReadOnlyDictionary<string, CharacterDefinition>>(characters);
            participants.Add(inventory);
        }
    }
}
