using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BorderValley.Core.Combat;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;

namespace BorderValley.Data
{
    public static class ContentValidator
    {
        private static readonly Regex ValidId = new("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled);

        public static IEnumerable<ContentValidationIssue> Validate(IEnumerable<ContentDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            return ValidateCore(definitions);
        }

        private static IEnumerable<ContentValidationIssue> ValidateCore(IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.ToArray();
            var seen = new HashSet<string>();
            foreach (var definition in all)
            {
                if (definition == null)
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition is null.", null);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition has no ID.", definition);
                    continue;
                }

                if (!ValidId.IsMatch(definition.Id))
                {
                    yield return new ContentValidationIssue("invalid_id", $"Invalid ID: {definition.Id}", definition);
                    continue;
                }

                if (!seen.Add(definition.Id))
                    yield return new ContentValidationIssue("duplicate_id", $"Duplicate ID: {definition.Id}", definition);
            }

            foreach (var issue in ValidateSpecialized(all))
                yield return issue;
        }

        private static IEnumerable<ContentValidationIssue> ValidateSpecialized(IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.Where(definition => definition != null).ToArray();
            var ids = all.Select(definition => definition.Id).Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var affix in all.OfType<AffixDefinition>())
            {
                if (affix.MinValue > affix.MaxValue)
                    yield return Issue("invalid_affix_range", affix.Id, affix);
                if (affix.CompatibleSlots.Length == 0)
                    yield return Issue("affix_without_slot", affix.Id, affix);
                foreach (var excluded in affix.MutuallyExclusiveAffixIds)
                    if (!ids.Contains(excluded))
                        yield return Issue("missing_affix_exclusion", $"{affix.Id} -> {excluded}", affix);
            }

            foreach (var table in all.OfType<ItemDropTableDefinition>())
            {
                if (table.MinItemLevel > table.MaxItemLevel)
                    yield return Issue("invalid_drop_level_range", table.Id, table);
                foreach (var entry in table.Entries)
                    if (entry.Item == null || !ids.Contains(entry.Item.Id))
                        yield return Issue("invalid_drop_item", table.Id, table);
            }

            foreach (var issue in ValidateQuestDefinitions(all))
                yield return issue;
            foreach (var issue in ValidateDialogueDefinitions(all))
                yield return issue;
            foreach (var issue in ValidateNpcDefinitions(all))
                yield return issue;
            foreach (var issue in ValidateShopDefinitions(all))
                yield return issue;
            foreach (var issue in ValidateWorldDefinitions(all))
                yield return issue;
        }

        private static IEnumerable<ContentValidationIssue> ValidateQuestDefinitions(IEnumerable<ContentDefinition> definitions)
        {
            var quests = ById(definitions.OfType<QuestDefinition>(), quest => quest.Id);
            var items = ById(definitions.OfType<ItemDefinition>(), item => item.Id);
            var npcs = ById(definitions.OfType<NpcDefinition>(), npc => npc.Id);
            var shops = ById(definitions.OfType<ShopDefinition>(), shop => shop.Id);
            var areas = ById(definitions.OfType<WorldAreaDefinition>(), area => area.Id);

            foreach (var quest in quests.Values)
            {
                foreach (var prerequisite in quest.PrerequisiteQuestIds)
                {
                    if (string.IsNullOrWhiteSpace(prerequisite) || !quests.ContainsKey(prerequisite))
                    {
                        yield return Issue(
                            "cyclic_quest_prerequisite",
                            $"{quest.Id} references missing prerequisite: {prerequisite}",
                            quest);
                    }
                }

                foreach (var objective in quest.Objectives)
                {
                    if (objective == null)
                    {
                        yield return Issue("missing_id", $"{quest.Id} contains a null objective.", quest);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(objective.TargetId))
                    {
                        yield return Issue("missing_id", $"{quest.Id} contains an objective without a target ID.", quest);
                        continue;
                    }

                    switch (objective.Kind)
                    {
                        case QuestObjectiveKind.SubmitItem:
                            if (!items.ContainsKey(objective.TargetId))
                                yield return Issue("missing_shop_item", $"{quest.Id} -> {objective.TargetId}", quest);
                            break;
                        case QuestObjectiveKind.TalkToNpc:
                            if (!npcs.ContainsKey(objective.TargetId))
                                yield return Issue("missing_world_target", $"{quest.Id} -> {objective.TargetId}", quest);
                            break;
                        case QuestObjectiveKind.ReachLocation:
                            if (!areas.ContainsKey(objective.TargetId))
                                yield return Issue("missing_world_target", $"{quest.Id} -> {objective.TargetId}", quest);
                            break;
                    }
                }

                foreach (var reward in quest.Rewards)
                {
                    if (reward == null)
                    {
                        yield return Issue("missing_id", $"{quest.Id} contains a null reward.", quest);
                        continue;
                    }

                    if (reward.Kind is QuestRewardKind.Equipment or QuestRewardKind.Material or QuestRewardKind.Item)
                    {
                        if (string.IsNullOrWhiteSpace(reward.TargetId) || !items.ContainsKey(reward.TargetId))
                            yield return Issue("missing_shop_item", $"{quest.Id} reward -> {reward.TargetId}", quest);
                    }
                    else if (reward.Kind == QuestRewardKind.UnlockShop)
                    {
                        if (string.IsNullOrWhiteSpace(reward.TargetId) || !shops.ContainsKey(reward.TargetId))
                            yield return Issue("missing_shop_item", $"{quest.Id} reward -> {reward.TargetId}", quest);
                    }
                }
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var quest in quests.Values)
            {
                visiting.Clear();
                if (HasQuestPrerequisiteCycle(quest.Id, quests, visiting, visited))
                    yield return Issue("cyclic_quest_prerequisite", $"Cyclic prerequisite at {quest.Id}", quest);
            }
        }

        private static bool HasQuestPrerequisiteCycle(
            string questId,
            IReadOnlyDictionary<string, QuestDefinition> quests,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visited.Contains(questId))
                return false;
            if (!visiting.Add(questId))
                return true;
            if (!quests.TryGetValue(questId, out var quest))
            {
                visiting.Remove(questId);
                return false;
            }

            foreach (var prerequisite in quest.PrerequisiteQuestIds)
            {
                if (!string.IsNullOrWhiteSpace(prerequisite) &&
                    HasQuestPrerequisiteCycle(prerequisite, quests, visiting, visited))
                    return true;
            }

            visiting.Remove(questId);
            visited.Add(questId);
            return false;
        }

        private static IEnumerable<ContentValidationIssue> ValidateDialogueDefinitions(
            IEnumerable<ContentDefinition> definitions)
        {
            var dialogues = ById(definitions.OfType<DialogueDefinition>(), dialogue => dialogue.Id);
            var npcs = ById(definitions.OfType<NpcDefinition>(), npc => npc.Id);
            var quests = ById(definitions.OfType<QuestDefinition>(), quest => quest.Id);
            var shops = ById(definitions.OfType<ShopDefinition>(), shop => shop.Id);
            var items = ById(definitions.OfType<ItemDefinition>(), item => item.Id);
            var knownEvents = CollectKnownEventIds(definitions);

            foreach (var dialogue in dialogues.Values)
            {
                var nodes = dialogue.Nodes ?? Array.Empty<DialogueNodeDefinition>();
                var nodesById = new Dictionary<string, DialogueNodeDefinition>(StringComparer.Ordinal);
                var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);
                var seenChoiceIds = new HashSet<string>(StringComparer.Ordinal);

                if (string.IsNullOrWhiteSpace(dialogue.StartNodeId))
                    yield return Issue("missing_dialogue_node", $"{dialogue.Id} has no start node.", dialogue);

                foreach (var node in nodes)
                {
                    if (node == null)
                    {
                        yield return Issue("missing_dialogue_node", $"{dialogue.Id} contains a null node.", dialogue);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.NodeId))
                    {
                        yield return Issue("missing_dialogue_node", $"{dialogue.Id} contains a node without an ID.", dialogue);
                        continue;
                    }

                    if (!seenNodeIds.Add(node.NodeId))
                        yield return Issue("duplicate_dialogue_node", $"{dialogue.Id} -> {node.NodeId}", dialogue);
                    else
                        nodesById[node.NodeId] = node;

                    if (!string.IsNullOrWhiteSpace(node.SpeakerNpcId) && !npcs.ContainsKey(node.SpeakerNpcId))
                        yield return Issue("missing_dialogue_node", $"{dialogue.Id} speaker -> {node.SpeakerNpcId}", dialogue);

                    if (!string.IsNullOrWhiteSpace(node.NextNodeId) && !seenNodeIds.Contains(node.NextNodeId))
                    {
                        // The direct target is checked after all nodes are collected.
                    }

                    foreach (var condition in node.Conditions)
                    {
                        if (!IsDialogueConditionTargetValid(condition, npcs, quests, items, knownEvents))
                            yield return Issue("missing_dialogue_node", $"{dialogue.Id} condition target is invalid.", dialogue);
                    }

                    foreach (var action in node.Actions)
                    {
                        if (!IsDialogueActionTargetValid(action, npcs, quests, shops, knownEvents))
                            yield return Issue("missing_dialogue_node", $"{dialogue.Id} action target is invalid.", dialogue);
                    }

                    foreach (var choice in node.Choices)
                    {
                        if (choice == null || string.IsNullOrWhiteSpace(choice.ChoiceId))
                        {
                            yield return Issue("missing_dialogue_node", $"{dialogue.Id} contains an invalid choice.", dialogue);
                            continue;
                        }

                        if (!seenChoiceIds.Add(choice.ChoiceId))
                            yield return Issue("duplicate_dialogue_node", $"{dialogue.Id} choice -> {choice.ChoiceId}", dialogue);

                        foreach (var condition in choice.Conditions)
                        {
                            if (!IsDialogueConditionTargetValid(condition, npcs, quests, items, knownEvents))
                                yield return Issue("missing_dialogue_node", $"{dialogue.Id} choice condition target is invalid.", dialogue);
                        }

                        foreach (var action in choice.Actions)
                        {
                            if (!IsDialogueActionTargetValid(action, npcs, quests, shops, knownEvents))
                                yield return Issue("missing_dialogue_node", $"{dialogue.Id} choice action target is invalid.", dialogue);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(dialogue.StartNodeId) && !nodesById.ContainsKey(dialogue.StartNodeId))
                    yield return Issue("missing_dialogue_node", $"{dialogue.Id} -> {dialogue.StartNodeId}", dialogue);

                foreach (var node in nodes)
                {
                    if (node == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(node.NextNodeId) && !nodesById.ContainsKey(node.NextNodeId))
                        yield return Issue("missing_dialogue_node", $"{dialogue.Id}/{node.NodeId} -> {node.NextNodeId}", dialogue);

                    foreach (var choice in node.Choices)
                    {
                        if (choice != null &&
                            !string.IsNullOrWhiteSpace(choice.NextNodeId) &&
                            !nodesById.ContainsKey(choice.NextNodeId))
                        {
                            yield return Issue(
                                "missing_dialogue_node",
                                $"{dialogue.Id}/{node.NodeId}/{choice.ChoiceId} -> {choice.NextNodeId}",
                                dialogue);
                        }
                    }
                }
            }
        }
        private static IEnumerable<ContentValidationIssue> ValidateNpcDefinitions(
            IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.ToArray();
            var dialogues = ById(all.OfType<DialogueDefinition>(), dialogue => dialogue.Id);
            var shops = ById(all.OfType<ShopDefinition>(), shop => shop.Id);

            foreach (var npc in all.OfType<NpcDefinition>())
            {
                if (!string.IsNullOrWhiteSpace(npc.DialogueId) && !dialogues.ContainsKey(npc.DialogueId))
                    yield return Issue("missing_dialogue_node", $"{npc.Id} -> {npc.DialogueId}", npc);
                if (!string.IsNullOrWhiteSpace(npc.OpenShopId) && !shops.ContainsKey(npc.OpenShopId))
                    yield return Issue("missing_shop_item", $"{npc.Id} -> {npc.OpenShopId}", npc);
            }
        }

        private static IEnumerable<ContentValidationIssue> ValidateShopDefinitions(
            IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.ToArray();
            var items = ById(all.OfType<ItemDefinition>(), item => item.Id);
            var affixes = ById(all.OfType<AffixDefinition>(), affix => affix.Id);
            var knownEvents = CollectKnownEventIds(definitions);

            foreach (var shop in all.OfType<ShopDefinition>())
            {
                if (!string.IsNullOrWhiteSpace(shop.RequiredEventId) &&
                    !knownEvents.Contains(shop.RequiredEventId))
                {
                    yield return Issue("missing_world_target", $"{shop.Id} event -> {shop.RequiredEventId}", shop);
                }

                var seenOfferIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var offer in shop.Offers)
                {
                    if (offer == null)
                    {
                        yield return Issue("missing_shop_item", $"{shop.Id} contains a null offer.", shop);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(offer.OfferId) || !seenOfferIds.Add(offer.OfferId))
                    {
                        yield return Issue("missing_shop_item", $"{shop.Id} has an invalid offer ID.", shop);
                        continue;
                    }

                    if (offer.Item == null || !items.ContainsKey(offer.Item.Id))
                    {
                        yield return Issue("missing_shop_item", $"{shop.Id}/{offer.OfferId}", shop);
                        continue;
                    }

                    var error = ValidateShopOfferCombination(offer, affixes);
                    if (!string.IsNullOrWhiteSpace(error))
                        yield return Issue("missing_shop_item", $"{shop.Id}/{offer.OfferId}: {error}", shop);
                }
            }
        }

        private static string ValidateShopOfferCombination(
            ShopOfferDefinition offer,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            if (offer.ItemLevel < 1)
                return "item level must be positive";

            var definitions = offer.Affixes ?? Array.Empty<AffixDefinition>();
            if (definitions.Length != ExpectedAffixCount(offer.Rarity))
                return "affix count does not match rarity";

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var totalCost = 0;
            var hasSpecial = false;
            var requiresSpecial = offer.Rarity == ItemRarity.Epic &&
                                  affixes.Values.Any(affix =>
                                      IsEligibleAffix(affix, offer.Item.Slot, offer.Rarity, offer.ItemLevel) &&
                                      IsSpecialAffix(affix));
            foreach (var affix in definitions)
            {
                if (affix == null || string.IsNullOrWhiteSpace(affix.Id) || !affixes.ContainsKey(affix.Id))
                    return "affix reference is missing";
                if (!seen.Add(affix.Id))
                    return "duplicate affix";
                if (!IsEligibleAffix(affix, offer.Item.Slot, offer.Rarity, offer.ItemLevel))
                    return "affix is not eligible for the item";
                if (seen.Any(existing =>
                        existing != affix.Id &&
                        (affix.IsMutuallyExclusive(existing) || affixes[existing].IsMutuallyExclusive(affix.Id))))
                {
                    return "affixes are mutually exclusive";
                }

                hasSpecial |= IsSpecialAffix(affix);
                totalCost += AffixValueCost(affix.MinValue, affix);
            }

            if (requiresSpecial && !hasSpecial)
                return "eligible special affix is required for epic";
            if (totalCost > AffixBudget(offer.Rarity))
                return "affix budget is exceeded";

            return string.Empty;
        }

        private static bool IsEligibleAffix(
            AffixDefinition affix,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel) =>
            affix != null &&
            affix.Weight > 0 &&
            affix.Supports(slot, rarity, itemLevel);

        private static bool IsSpecialAffix(AffixDefinition affix) =>
            affix != null &&
            affix.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger;

        private static int AffixValueCost(int value, AffixDefinition affix)
        {
            var units = affix.Stat == CombatStat.CritChanceBps
                ? Math.Max(1, (Math.Abs(value) + 99) / 100)
                : Math.Max(1, Math.Abs(value));
            return units * Math.Max(1, affix.BudgetCost);
        }

        private static int ExpectedAffixCount(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 1,
            ItemRarity.Rare => 2,
            ItemRarity.Epic => 3,
            _ => -1
        };

        private static int AffixBudget(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 12,
            ItemRarity.Rare => 24,
            ItemRarity.Epic => 40,
            _ => 0
        };

        private static HashSet<string> CollectKnownEventIds(IEnumerable<ContentDefinition> definitions)
        {
            var knownEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var area in definitions.OfType<WorldAreaDefinition>())
            {
                foreach (var eventId in area.EventIds)
                {
                    if (!string.IsNullOrWhiteSpace(eventId))
                        knownEvents.Add(eventId);
                }

                foreach (var interactable in area.Interactables)
                {
                    if (interactable != null &&
                        interactable.Kind == WorldInteractableKind.Investigate &&
                        !string.IsNullOrWhiteSpace(interactable.TargetId))
                    {
                        knownEvents.Add(interactable.TargetId);
                    }
                }
            }

            foreach (var encounter in definitions.OfType<WorldEncounterDefinition>())
            {
                if (!string.IsNullOrWhiteSpace(encounter.CompletionEventId))
                    knownEvents.Add(encounter.CompletionEventId);
            }

            return knownEvents;
        }

        private static bool IsDialogueConditionTargetValid(
            DialogueConditionDefinition condition,
            IReadOnlyDictionary<string, NpcDefinition> npcs,
            IReadOnlyDictionary<string, QuestDefinition> quests,
            IReadOnlyDictionary<string, ItemDefinition> items,
            ISet<string> knownEvents)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.TargetId))
                return false;

            return condition.Kind switch
            {
                DialogueConditionKind.QuestState => quests.ContainsKey(condition.TargetId),
                DialogueConditionKind.HasItem => items.ContainsKey(condition.TargetId),
                DialogueConditionKind.Event => knownEvents.Contains(condition.TargetId),
                DialogueConditionKind.Favor => npcs.ContainsKey(condition.TargetId),
                _ => false
            };
        }

        private static bool IsDialogueActionTargetValid(
            DialogueActionDefinition action,
            IReadOnlyDictionary<string, NpcDefinition> npcs,
            IReadOnlyDictionary<string, QuestDefinition> quests,
            IReadOnlyDictionary<string, ShopDefinition> shops,
            ISet<string> knownEvents)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.TargetId))
                return false;

            return action.Kind switch
            {
                DialogueActionKind.AcceptQuest => quests.ContainsKey(action.TargetId),
                DialogueActionKind.AdvanceQuest => quests.ContainsKey(action.TargetId),
                DialogueActionKind.TurnInQuest => quests.ContainsKey(action.TargetId),
                DialogueActionKind.OpenShop => shops.ContainsKey(action.TargetId),
                DialogueActionKind.ChangeFavor => npcs.ContainsKey(action.TargetId),
                DialogueActionKind.SetEvent => knownEvents.Contains(action.TargetId),
                _ => false
            };
        }
        private static IEnumerable<ContentValidationIssue> ValidateWorldDefinitions(
            IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.ToArray();
            var areas = ById(all.OfType<WorldAreaDefinition>(), area => area.Id);
            var npcs = ById(all.OfType<NpcDefinition>(), npc => npc.Id);
            var encounters = ById(all.OfType<WorldEncounterDefinition>(), encounter => encounter.EncounterId);
            var rewardTables = ById(all.OfType<ItemDropTableDefinition>(), table => table.Id);
            var items = ById(all.OfType<ItemDefinition>(), item => item.Id);
            var knownEvents = CollectKnownEventIds(all);

            foreach (var encounter in encounters.Values)
            {
                if (!string.IsNullOrWhiteSpace(encounter.RewardTableId) &&
                    !rewardTables.ContainsKey(encounter.RewardTableId))
                {
                    yield return Issue("missing_world_target", $"{encounter.Id} -> {encounter.RewardTableId}", encounter);
                }

                if (!string.IsNullOrWhiteSpace(encounter.RequiredEventId) &&
                    !knownEvents.Contains(encounter.RequiredEventId))
                {
                    yield return Issue("missing_world_target", $"{encounter.Id} event -> {encounter.RequiredEventId}", encounter);
                }
            }

            foreach (var area in areas.Values)
            {
                var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var eventId in area.EventIds)
                {
                    if (string.IsNullOrWhiteSpace(eventId))
                    {
                        yield return Issue("missing_id", $"{area.Id} contains an empty event ID.", area);
                        continue;
                    }

                    if (!seenEventIds.Add(eventId))
                        yield return Issue("duplicate_id", $"{area.Id} event -> {eventId}", area);
                }

                foreach (var npc in area.Npcs)
                {
                    if (npc == null || !npcs.ContainsKey(npc.Id))
                        yield return Issue("missing_world_target", $"{area.Id} npc reference is missing.", area);
                }

                foreach (var encounter in area.Encounters)
                {
                    if (encounter == null || !encounters.ContainsKey(encounter.EncounterId))
                        yield return Issue("missing_world_target", $"{area.Id} encounter reference is missing.", area);
                }

                foreach (var table in area.RewardTables)
                {
                    if (table == null || !rewardTables.ContainsKey(table.Id))
                        yield return Issue("missing_world_target", $"{area.Id} reward table reference is missing.", area);
                }

                foreach (var rewardTableId in area.RewardTableIds)
                {
                    if (string.IsNullOrWhiteSpace(rewardTableId) || !rewardTables.ContainsKey(rewardTableId))
                        yield return Issue("missing_world_target", $"{area.Id} reward table -> {rewardTableId}", area);
                }

                var seenInteractableIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var interactable in area.Interactables)
                {
                    if (interactable == null || string.IsNullOrWhiteSpace(interactable.Id))
                    {
                        yield return Issue("missing_world_target", $"{area.Id} contains an interactable without an ID.", area);
                        continue;
                    }

                    if (!seenInteractableIds.Add(interactable.Id))
                        yield return Issue("duplicate_id", $"{area.Id} -> {interactable.Id}", area);

                    if (!string.IsNullOrWhiteSpace(interactable.RequiredEventId) &&
                        !knownEvents.Contains(interactable.RequiredEventId))
                    {
                        yield return Issue(
                            "missing_world_target",
                            $"{area.Id}/{interactable.Id} event -> {interactable.RequiredEventId}",
                            area);
                    }

                    var targetValid = interactable.Kind switch
                    {
                        WorldInteractableKind.Npc => npcs.ContainsKey(interactable.TargetId),
                        WorldInteractableKind.AreaExit => areas.ContainsKey(interactable.TargetId),
                        WorldInteractableKind.Encounter => encounters.ContainsKey(interactable.TargetId),
                        WorldInteractableKind.Chest =>
                            rewardTables.ContainsKey(interactable.TargetId) ||
                            items.ContainsKey(interactable.TargetId),
                        WorldInteractableKind.Gather =>
                            items.ContainsKey(interactable.TargetId) ||
                            rewardTables.ContainsKey(interactable.TargetId),
                        WorldInteractableKind.Investigate => !string.IsNullOrWhiteSpace(interactable.TargetId),
                        _ => false
                    };

                    if (!targetValid)
                    {
                        yield return Issue(
                            "missing_world_target",
                            $"{area.Id}/{interactable.Id} -> {interactable.TargetId}",
                            area);
                    }
                }
            }
        }

        private static Dictionary<string, T> ById<T>(
            IEnumerable<T> definitions,
            Func<T, string> idSelector) where T : class
        {
            return definitions
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(idSelector(definition)))
                .GroupBy(idSelector, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static ContentValidationIssue Issue(string code, string message, UnityEngine.Object context) =>
            new(code, message, context);
    }
}
