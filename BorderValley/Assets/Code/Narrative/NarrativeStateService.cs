using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BorderValley.Narrative
{
    public sealed class NarrativeStateService : ISaveParticipant
    {
        private const char KeySeparator = '\n';
        private Dictionary<string, QuestDefinition> quests = new(StringComparer.Ordinal);
        private Dictionary<string, NpcDefinition> npcs = new(StringComparer.Ordinal);
        private Dictionary<string, ShopDefinition> shops = new(StringComparer.Ordinal);
        private HashSet<string> areaIds = new(StringComparer.Ordinal);
        private HashSet<string> eventIds = new(StringComparer.Ordinal);
        private HashSet<string> interactableIds = new(StringComparer.Ordinal);
        private HashSet<string> dialogueNodeIds = new(StringComparer.Ordinal);
        private HashSet<string> shopOfferKeys = new(StringComparer.Ordinal);
        private HashSet<string> events = new(StringComparer.Ordinal);
        private HashSet<string> resolvedInteractables = new(StringComparer.Ordinal);
        private HashSet<string> readDialogueNodes = new(StringComparer.Ordinal);
        private HashSet<string> unlockedShops = new(StringComparer.Ordinal);
        private HashSet<string> purchasedOffers = new(StringComparer.Ordinal);
        private Dictionary<string, int> favorTiers = new(StringComparer.Ordinal);
        private Dictionary<string, QuestProgressRecord> questProgress = new(StringComparer.Ordinal);
        private string currentAreaId = string.Empty;
        private Vector2 currentPosition;

        public NarrativeStateService(IEnumerable<ContentDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            IndexContent(definitions);
        }

        public NarrativeStateService(ContentCatalog catalog)
            : this(catalog == null ? throw new ArgumentNullException(nameof(catalog)) : catalog.All)
        {
        }

        public string Key => "narrative";

        public string GetCurrentAreaId() => currentAreaId;

        public Vector2 GetCurrentPosition() => currentPosition;

        public bool SetCurrentLocation(string areaId, Vector2 position)
        {
            var normalizedAreaId = areaId ?? string.Empty;
            if (normalizedAreaId.Length > 0 && !areaIds.Contains(normalizedAreaId)) return false;
            if (!IsFinite(position.x) || !IsFinite(position.y)) return false;
            currentAreaId = normalizedAreaId;
            currentPosition = position;
            return true;
        }

        public bool HasEvent(string eventId) =>
            !string.IsNullOrWhiteSpace(eventId) && events.Contains(eventId);

        public bool SetEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || !eventIds.Contains(eventId)) return false;
            events.Add(eventId);
            return true;
        }

        public bool IsInteractableResolved(string interactableId) =>
            !string.IsNullOrWhiteSpace(interactableId) && resolvedInteractables.Contains(interactableId);

        public bool MarkInteractableResolved(string interactableId)
        {
            if (string.IsNullOrWhiteSpace(interactableId) || !interactableIds.Contains(interactableId))
                return false;
            resolvedInteractables.Add(interactableId);
            return true;
        }

        public bool IsDialogueNodeRead(string nodeId) =>
            !string.IsNullOrWhiteSpace(nodeId) && readDialogueNodes.Contains(nodeId);

        public bool MarkDialogueNodeRead(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || !dialogueNodeIds.Contains(nodeId)) return false;
            readDialogueNodes.Add(nodeId);
            return true;
        }

        public int GetFavorTier(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return 0;
            if (favorTiers.TryGetValue(npcId, out var tier)) return tier;
            return npcs.TryGetValue(npcId, out var npc)
                ? Math.Clamp(npc.InitialFavorTier, 0, 2)
                : 0;
        }

        public bool TryChangeFavor(string npcId, int delta, out string error)
        {
            if (string.IsNullOrWhiteSpace(npcId) || !npcs.ContainsKey(npcId))
            {
                error = NarrativeTextKeys.UnknownNpc;
                return false;
            }

            favorTiers[npcId] = Math.Clamp(GetFavorTier(npcId) + delta, 0, 2);
            error = string.Empty;
            return true;
        }

        public bool IsShopUnlocked(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId) || !shops.TryGetValue(shopId, out var shop))
                return false;
            if (unlockedShops.Contains(shopId)) return true;
            return !string.IsNullOrWhiteSpace(shop.RequiredEventId) && HasEvent(shop.RequiredEventId);
        }

        public bool MarkShopUnlocked(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId) || !shops.ContainsKey(shopId)) return false;
            unlockedShops.Add(shopId);
            return true;
        }

        public bool IsOfferPurchased(string offerId) =>
            !string.IsNullOrWhiteSpace(offerId) &&
            purchasedOffers.Any(key =>
                key.Split(KeySeparator, 2) is var parts &&
                parts.Length == 2 &&
                string.Equals(parts[1], offerId, StringComparison.Ordinal));

        public bool IsOfferPurchased(string shopId, string offerId) =>
            !string.IsNullOrWhiteSpace(shopId) &&
            !string.IsNullOrWhiteSpace(offerId) &&
            purchasedOffers.Contains(OfferKey(shopId, offerId));

        public bool MarkOfferPurchased(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId)) return false;
            var matching = shops.Values
                .SelectMany(shop => shop.Offers.Where(offer => offer != null &&
                    string.Equals(offer.OfferId, offerId, StringComparison.Ordinal))
                    .Select(_ => shop))
                .Take(2)
                .ToArray();
            return matching.Length == 1 && MarkOfferPurchased(matching[0].Id, offerId);
        }

        public bool MarkOfferPurchased(string shopId, string offerId)
        {
            if (string.IsNullOrWhiteSpace(shopId) ||
                string.IsNullOrWhiteSpace(offerId) ||
                !shopOfferKeys.Contains(OfferKey(shopId, offerId)))
                return false;
            purchasedOffers.Add(OfferKey(shopId, offerId));
            return true;
        }

        public bool TryAcceptQuest(string questId, out string error)
        {
            if (string.IsNullOrWhiteSpace(questId) || !quests.TryGetValue(questId, out var quest))
            {
                error = NarrativeTextKeys.QuestNotFound;
                return false;
            }

            if (questProgress.ContainsKey(questId))
            {
                error = GetQuestState(questId) == QuestState.Completed
                    ? NarrativeTextKeys.QuestAlreadyCompleted
                    : NarrativeTextKeys.QuestAlreadyStarted;
                return false;
            }

            if (quest.PrerequisiteQuestIds.Any(prerequisite =>
                    GetQuestState(prerequisite) != QuestState.Completed))
            {
                error = NarrativeTextKeys.QuestPrerequisiteMissing;
                return false;
            }

            var initialProgress = CreateInitialProgress(quest, out error);
            if (initialProgress == null) return false;
            questProgress[questId] = new QuestProgressRecord(QuestState.Active, initialProgress);
            error = string.Empty;
            return true;
        }

        public bool TryAdvanceQuestObjective(
            string questId,
            string objectiveId,
            int amount,
            out string error)
        {
            error = string.Empty;
            if (!TryGetActiveQuest(questId, out var quest, out var progress, out error)) return false;
            if (!TryFindUniqueObjective(quest, objectiveId, out var objective))
            {
                error = NarrativeTextKeys.QuestObjectiveInvalid;
                return false;
            }
            if (amount <= 0)
            {
                error = NarrativeTextKeys.QuestObjectiveAmountInvalid;
                return false;
            }
            if (!progress.Progress.TryGetValue(objectiveId, out var current))
            {
                error = NarrativeTextKeys.QuestObjectiveInvalid;
                return false;
            }

            progress.Progress[objectiveId] = Math.Min(objective.RequiredCount, current + amount);
            progress.State = quest.Objectives.All(value =>
                    value != null &&
                    progress.Progress.TryGetValue(value.ObjectiveId, out var count) &&
                    count >= value.RequiredCount)
                ? QuestState.ReadyToTurnIn
                : QuestState.Active;
            return true;
        }

        public bool TryMarkQuestCompleted(string questId, out string error)
        {
            error = string.Empty;
            if (!TryGetActiveQuest(questId, out _, out var progress, out error)) return false;
            progress.State = QuestState.Completed;
            return true;
        }

        public QuestState GetQuestState(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return QuestState.NotStarted;
            return questProgress.TryGetValue(questId, out var progress)
                ? progress.State
                : QuestState.NotStarted;
        }

        public int GetObjectiveProgress(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(questId) ||
                string.IsNullOrWhiteSpace(objectiveId) ||
                !questProgress.TryGetValue(questId, out var progress) ||
                !progress.Progress.TryGetValue(objectiveId, out var value))
                return 0;
            return value;
        }

        public bool HasShop(string shopId) =>
            !string.IsNullOrWhiteSpace(shopId) && shops.ContainsKey(shopId);

        public JObject Capture() => new()
        {
            ["currentAreaId"] = currentAreaId,
            ["currentPosition"] = new JObject
            {
                ["x"] = currentPosition.x,
                ["y"] = currentPosition.y
            },
            ["events"] = new JArray(events.OrderBy(value => value, StringComparer.Ordinal)),
            ["resolvedInteractables"] = new JArray(
                resolvedInteractables.OrderBy(value => value, StringComparer.Ordinal)),
            ["readDialogueNodes"] = new JArray(
                readDialogueNodes.OrderBy(value => value, StringComparer.Ordinal)),
            ["unlockedShops"] = new JArray(
                unlockedShops.OrderBy(value => value, StringComparer.Ordinal)),
            ["purchasedOffers"] = new JArray(
                purchasedOffers
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value =>
                    {
                        var parts = value.Split(KeySeparator, 2);
                        return new JObject
                        {
                            ["shopId"] = parts[0],
                            ["offerId"] = parts[1]
                        };
                    })),
            ["favorTiers"] = new JObject(
                favorTiers
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new JProperty(pair.Key, pair.Value))),
            ["questProgress"] = new JArray(
                questProgress
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new JObject
                    {
                        ["questId"] = pair.Key,
                        ["state"] = pair.Value.State.ToString(),
                        ["progress"] = new JObject(
                            pair.Value.Progress
                                .OrderBy(progressPair => progressPair.Key, StringComparer.Ordinal)
                                .Select(progressPair => new JProperty(progressPair.Key, progressPair.Value)))
                    }))
        };

        public void Restore(JObject state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var parsedAreaId = ParseAreaId(state["currentAreaId"]);
            var parsedPosition = ParsePosition(state["currentPosition"]);
            var parsedEvents = ParseStringSet(state["events"], "events", eventIds);
            var parsedInteractables = ParseStringSet(
                state["resolvedInteractables"],
                "resolved interactables",
                interactableIds);
            var parsedDialogueNodes = ParseStringSet(
                state["readDialogueNodes"],
                "read dialogue nodes",
                dialogueNodeIds);
            var parsedUnlockedShops = ParseStringSet(
                state["unlockedShops"],
                "unlocked shops",
                shops.Keys);
            var parsedFavorTiers = ParseFavorTiers(state["favorTiers"]);
            var parsedPurchasedOffers = ParsePurchasedOffers(state["purchasedOffers"]);
            var parsedQuestProgress = ParseQuestProgress(state["questProgress"]);

            currentAreaId = parsedAreaId;
            currentPosition = parsedPosition;
            events = parsedEvents;
            resolvedInteractables = parsedInteractables;
            readDialogueNodes = parsedDialogueNodes;
            unlockedShops = parsedUnlockedShops;
            favorTiers = parsedFavorTiers;
            purchasedOffers = parsedPurchasedOffers;
            questProgress = parsedQuestProgress;
        }

        public void RestoreContext(string sceneName) { }

        public void Reset()
        {
            events.Clear();
            resolvedInteractables.Clear();
            readDialogueNodes.Clear();
            unlockedShops.Clear();
            purchasedOffers.Clear();
            favorTiers.Clear();
            questProgress.Clear();
            currentAreaId = string.Empty;
            currentPosition = Vector2.zero;
        }

        private void IndexContent(IEnumerable<ContentDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                switch (definition)
                {
                    case QuestDefinition quest:
                        quests[quest.Id] = quest;
                        break;
                    case NpcDefinition npc:
                        npcs[npc.Id] = npc;
                        break;
                    case ShopDefinition shop:
                        shops[shop.Id] = shop;
                        foreach (var offer in shop.Offers)
                            if (offer != null && !string.IsNullOrWhiteSpace(offer.OfferId))
                                shopOfferKeys.Add(OfferKey(shop.Id, offer.OfferId));
                        break;
                    case DialogueDefinition dialogue:
                        foreach (var node in dialogue.Nodes)
                            if (node != null && !string.IsNullOrWhiteSpace(node.NodeId))
                                dialogueNodeIds.Add(node.NodeId);
                        break;
                    case WorldAreaDefinition area:
                        areaIds.Add(area.Id);
                        foreach (var eventId in area.EventIds)
                            if (!string.IsNullOrWhiteSpace(eventId))
                                eventIds.Add(eventId);
                        foreach (var npc in area.Npcs)
                            if (npc != null)
                                npcs[npc.Id] = npc;
                        foreach (var interactable in area.Interactables)
                            if (interactable != null && !string.IsNullOrWhiteSpace(interactable.Id))
                                interactableIds.Add(interactable.Id);
                        foreach (var encounter in area.Encounters)
                            if (encounter != null && !string.IsNullOrWhiteSpace(encounter.CompletionEventId))
                                eventIds.Add(encounter.CompletionEventId);
                        break;
                }
            }
        }

        private bool TryGetActiveQuest(
            string questId,
            out QuestDefinition quest,
            out QuestProgressRecord progress,
            out string error)
        {
            quest = null;
            progress = null;
            if (string.IsNullOrWhiteSpace(questId) || !quests.TryGetValue(questId, out quest))
            {
                error = NarrativeTextKeys.QuestNotFound;
                return false;
            }
            if (!questProgress.TryGetValue(questId, out progress))
            {
                error = NarrativeTextKeys.QuestNotActive;
                return false;
            }
            if (progress.State == QuestState.Completed)
            {
                error = NarrativeTextKeys.QuestAlreadyCompleted;
                return false;
            }
            if (progress.State != QuestState.Active && progress.State != QuestState.ReadyToTurnIn)
            {
                error = NarrativeTextKeys.QuestNotActive;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private string ParseAreaId(JToken token)
        {
            if (token == null || token.Type != JTokenType.String)
                throw new InvalidOperationException("Save contains a malformed current area.");
            var value = token.Value<string>() ?? string.Empty;
            if (value.Length > 0 && !areaIds.Contains(value))
                throw new InvalidOperationException("Save contains an unknown current area.");
            return value;
        }

        private static Vector2 ParsePosition(JToken token)
        {
            if (!(token is JObject position) ||
                position["x"] == null ||
                position["y"] == null ||
                (position["x"].Type != JTokenType.Float && position["x"].Type != JTokenType.Integer) ||
                (position["y"].Type != JTokenType.Float && position["y"].Type != JTokenType.Integer))
                throw new InvalidOperationException("Save contains a malformed current position.");

            var x = position["x"].Value<float>();
            var y = position["y"].Value<float>();
            if (!IsFinite(x) || !IsFinite(y))
                throw new InvalidOperationException("Save contains a non-finite current position.");
            return new Vector2(x, y);
        }

        private static HashSet<string> ParseStringSet(
            JToken token,
            string fieldName,
            IEnumerable<string> allowedValues)
        {
            var allowed = new HashSet<string>(allowedValues, StringComparer.Ordinal);
            var parsed = new HashSet<string>(StringComparer.Ordinal);
            if (token == null || token.Type == JTokenType.Null) return parsed;
            if (!(token is JArray array))
                throw new InvalidOperationException($"Save contains malformed {fieldName}.");

            foreach (var valueToken in array)
            {
                if (valueToken.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(valueToken.Value<string>()) ||
                    !allowed.Contains(valueToken.Value<string>()) ||
                    !parsed.Add(valueToken.Value<string>()))
                    throw new InvalidOperationException($"Save contains invalid {fieldName}.");
            }
            return parsed;
        }

        private Dictionary<string, int> ParseFavorTiers(JToken token)
        {
            var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
            if (token == null || token.Type == JTokenType.Null) return parsed;
            if (!(token is JObject favorState))
                throw new InvalidOperationException("Save contains malformed favor tiers.");

            foreach (var property in favorState.Properties())
            {
                if (!npcs.ContainsKey(property.Name) ||
                    property.Value.Type != JTokenType.Integer)
                    throw new InvalidOperationException("Save contains an invalid favor tier.");
                var tier = property.Value.Value<int>();
                if (tier < 0 || tier > 2 || !parsed.TryAdd(property.Name, tier))
                    throw new InvalidOperationException("Save contains an invalid favor tier.");
            }
            return parsed;
        }

        private HashSet<string> ParsePurchasedOffers(JToken token)
        {
            var parsed = new HashSet<string>(StringComparer.Ordinal);
            if (token == null || token.Type == JTokenType.Null) return parsed;
            if (!(token is JArray purchasedState))
                throw new InvalidOperationException("Save contains malformed purchased offers.");

            foreach (var valueToken in purchasedState)
            {
                if (!(valueToken is JObject value))
                    throw new InvalidOperationException("Save contains a malformed purchased offer.");
                var shopId = value.Value<string>("shopId");
                var offerId = value.Value<string>("offerId");
                var key = OfferKey(shopId, offerId);
                if (string.IsNullOrWhiteSpace(shopId) ||
                    string.IsNullOrWhiteSpace(offerId) ||
                    !shopOfferKeys.Contains(key) ||
                    !parsed.Add(key))
                    throw new InvalidOperationException("Save contains an invalid purchased offer.");
            }
            return parsed;
        }

        private Dictionary<string, QuestProgressRecord> ParseQuestProgress(JToken token)
        {
            var parsed = new Dictionary<string, QuestProgressRecord>(StringComparer.Ordinal);
            if (token == null || token.Type == JTokenType.Null) return parsed;
            if (!(token is JArray questStateArray))
                throw new InvalidOperationException("Save contains malformed quest progress.");

            foreach (var valueToken in questStateArray)
            {
                if (!(valueToken is JObject value))
                    throw new InvalidOperationException("Save contains a malformed quest.");
                var questId = value.Value<string>("questId");
                var stateText = value.Value<string>("state");
                if (string.IsNullOrWhiteSpace(questId) ||
                    !quests.TryGetValue(questId, out var quest) ||
                    string.IsNullOrWhiteSpace(stateText) ||
                    !Enum.TryParse<QuestState>(stateText, out var parsedQuestState) ||
                    !Enum.IsDefined(typeof(QuestState), parsedQuestState) ||
                    !(value["progress"] is JObject progressState))
                    throw new InvalidOperationException("Save contains invalid quest progress.");

                var objectives = BuildObjectiveMap(quest);
                if (objectives.Count != quest.Objectives.Length)
                    throw new InvalidOperationException("Save references invalid quest objectives.");
                if (progressState.Properties().Count() != objectives.Count)
                    throw new InvalidOperationException("Save contains incomplete quest progress.");

                var progress = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var property in progressState.Properties())
                {
                    if (!objectives.TryGetValue(property.Name, out var objective) ||
                        property.Value.Type != JTokenType.Integer)
                        throw new InvalidOperationException("Save contains invalid quest progress.");
                    var valueCount = property.Value.Value<int>();
                    if (valueCount < 0 || valueCount > objective.RequiredCount ||
                        !progress.TryAdd(property.Name, valueCount))
                        throw new InvalidOperationException("Save contains invalid quest progress.");
                }

                if (progress.Count != objectives.Count)
                    throw new InvalidOperationException("Save contains incomplete quest progress.");
                if (!parsed.TryAdd(questId, new QuestProgressRecord(parsedQuestState, progress)))
                    throw new InvalidOperationException("Save contains duplicate quest progress.");
            }
            return parsed;
        }

        private static Dictionary<string, int> CreateInitialProgress(
            QuestDefinition quest,
            out string error)
        {
            var objectives = BuildObjectiveMap(quest);
            if (objectives.Count != quest.Objectives.Length)
            {
                error = NarrativeTextKeys.QuestObjectiveInvalid;
                return null;
            }

            var progress = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var objective in quest.Objectives)
                progress[objective.ObjectiveId] = 0;
            error = string.Empty;
            return progress;
        }

        private static Dictionary<string, QuestObjectiveDefinition> BuildObjectiveMap(QuestDefinition quest)
        {
            var objectives = new Dictionary<string, QuestObjectiveDefinition>(StringComparer.Ordinal);
            foreach (var objective in quest.Objectives)
            {
                if (objective == null ||
                    string.IsNullOrWhiteSpace(objective.ObjectiveId) ||
                    !objectives.TryAdd(objective.ObjectiveId, objective))
                    return new Dictionary<string, QuestObjectiveDefinition>();
            }
            return objectives;
        }

        private static bool TryFindUniqueObjective(
            QuestDefinition quest,
            string objectiveId,
            out QuestObjectiveDefinition objective)
        {
            objective = null;
            if (string.IsNullOrWhiteSpace(objectiveId)) return false;
            foreach (var candidate in quest.Objectives)
            {
                if (candidate == null ||
                    !string.Equals(candidate.ObjectiveId, objectiveId, StringComparison.Ordinal))
                    continue;
                if (objective != null) return false;
                objective = candidate;
            }
            return objective != null;
        }

        private static string OfferKey(string shopId, string offerId) =>
            (shopId ?? string.Empty) + KeySeparator + (offerId ?? string.Empty);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class QuestProgressRecord
        {
            public QuestProgressRecord(QuestState state, Dictionary<string, int> progress)
            {
                State = state;
                Progress = progress;
            }

            public QuestState State { get; set; }
            public Dictionary<string, int> Progress { get; }
        }
    }
}
