using System;
using System.Collections.Generic;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Combat;
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
            var affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            var banditDropTable = default(ItemDropTableDefinition);
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
                        case AffixDefinition affix:
                            affixes[affix.Id] = affix;
                            break;
                        case ItemDropTableDefinition dropTable when
                            string.Equals(dropTable.Id, "loot.bandit.core", StringComparison.Ordinal):
                            banditDropTable = dropTable;
                            break;
                    }
                }
            }

            if (banditDropTable == null)
                throw new InvalidOperationException("Required drop table 'loot.bandit.core' is missing.");

            var inventory = new InventoryService(30, items, 100);
            var progression = new PartyProgressionService(characters, CreateInitialParty(characters));
            var snapshotBuilder = new PartyBattleSnapshotBuilder(progression, inventory, items, affixes);
            var lootGenerator = new LootGenerator(items, affixes);
            var craftingCosts = new CraftingCosts();
            var economy = new EconomyService(inventory, items, affixes);
            var crafting = new CraftingService(inventory, items, affixes, lootGenerator, craftingCosts);
            context.Register(inventory);
            context.Register(progression);
            context.Register(snapshotBuilder);
            context.Register(lootGenerator);
            context.Register(craftingCosts);
            context.Register(economy);
            context.Register(crafting);
            context.Register<IReadOnlyDictionary<string, ItemDefinition>>(items);
            context.Register<IReadOnlyDictionary<string, CharacterDefinition>>(characters);
            context.Register<IReadOnlyDictionary<string, AffixDefinition>>(affixes);
            context.Register(banditDropTable);
            participants.Add(inventory);
            participants.Add(progression);
        }

        private static IEnumerable<PartyMemberState> CreateInitialParty(
            IReadOnlyDictionary<string, CharacterDefinition> characters)
        {
            var members = new[]
            {
                (MemberId: "player.warrior", CharacterId: "class.warrior"),
                (MemberId: "player.ranger", CharacterId: "class.ranger"),
                (MemberId: "player.mage", CharacterId: "class.mage")
            };
            foreach (var member in members)
            {
                if (!characters.TryGetValue(member.CharacterId, out var character)) continue;
                yield return new PartyMemberState(
                    member.MemberId,
                    member.CharacterId,
                    1,
                    0,
                    0,
                    character.GetBaseStat(CombatStat.MaxHealth, 1),
                    character.GetBaseStat(CombatStat.MaxMana, 1));
            }
        }
    }
}
