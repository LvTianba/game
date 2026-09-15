using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Editor
{
    public static class WorldContentBuilder
    {
        public const string CatalogPath = EquipmentContentBuilder.CatalogPath;
        private const string Root = "Assets/Resources/Content/World";
        private const string NarrativeRoot = "Assets/Resources/Content/Narrative";

        public static void Build()
        {
            EquipmentContentBuilder.Build();
            var catalog = LoadOrCreate<ContentCatalog>(CatalogPath);
            var generated = new List<ContentDefinition>();
            var rewardTable = catalog.All.OfType<ItemDropTableDefinition>()
                .Single(table => table.Id == "loot.bandit.core");
            var items = catalog.All.OfType<ItemDefinition>()
                .ToDictionary(item => item.Id, StringComparer.Ordinal);

            var mainQuest = Quest(
                "quest.main.crypt",
                "quest.main.crypt.title",
                "quest.main.crypt.description",
                new[]
                {
                    new QuestObjectiveDefinition(
                        "objective.main.boss",
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.crypt_boss",
                        1,
                        false,
                        "quest.main.crypt.objective.boss")
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 100) });
            var rangerQuest = Quest(
                "quest.side.ranger",
                "quest.side.ranger.title",
                "quest.side.ranger.description",
                new[]
                {
                    new QuestObjectiveDefinition(
                        "objective.side.ranger.enemies",
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.ranger",
                        1,
                        false,
                        "quest.side.ranger.objective.enemies")
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 40) });
            var survivorQuest = Quest(
                "quest.side.survivor",
                "quest.side.survivor.title",
                "quest.side.survivor.description",
                new[]
                {
                    new QuestObjectiveDefinition(
                        "objective.side.survivor.bandits",
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.bandit",
                        2,
                        false,
                        "quest.side.survivor.objective.bandits")
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 35) });
            generated.AddRange(new[] { mainQuest, rangerQuest, survivorQuest });

            var elderDialogue = CreateElderDialogue();
            var merchantDialogue = CreateOpenShopDialogue(
                "dialogue.merchant",
                "npc.merchant",
                "event.shop.general_unlocked",
                "shop.general");
            var blacksmithDialogue = CreateOpenShopDialogue(
                "dialogue.blacksmith",
                "npc.blacksmith",
                "event.shop.blacksmith_unlocked",
                "shop.blacksmith");
            var rangerDialogue = CreateSideQuestDialogue(
                "dialogue.ranger_companion",
                "npc.ranger_companion",
                "quest.side.ranger");
            var survivorDialogue = CreateSideQuestDialogue(
                "dialogue.survivor",
                "npc.survivor",
                "quest.side.survivor");
            var innkeeperDialogue = CreateSimpleDialogue("dialogue.innkeeper", "npc.innkeeper");
            var mageDialogue = CreateSimpleDialogue("dialogue.mage_companion", "npc.mage_companion");
            var hunterDialogue = CreateSimpleDialogue("dialogue.hunter", "npc.hunter");
            generated.AddRange(new[]
            {
                elderDialogue,
                merchantDialogue,
                blacksmithDialogue,
                rangerDialogue,
                survivorDialogue,
                innkeeperDialogue,
                mageDialogue,
                hunterDialogue
            });

            var elderNpc = Npc("npc.elder", "dialogue.elder");
            var blacksmithNpc = Npc("npc.blacksmith", "dialogue.blacksmith", "shop.blacksmith");
            var merchantNpc = Npc("npc.merchant", "dialogue.merchant", "shop.general");
            var innkeeperNpc = Npc("npc.innkeeper", "dialogue.innkeeper");
            var rangerNpc = Npc("npc.ranger_companion", "dialogue.ranger_companion");
            var mageNpc = Npc("npc.mage_companion", "dialogue.mage_companion");
            var hunterNpc = Npc("npc.hunter", "dialogue.hunter");
            var survivorNpc = Npc("npc.survivor", "dialogue.survivor");
            generated.AddRange(new[]
            {
                elderNpc,
                blacksmithNpc,
                merchantNpc,
                innkeeperNpc,
                rangerNpc,
                mageNpc,
                hunterNpc,
                survivorNpc
            });

            var generalShop = Shop(
                "shop.general",
                "event.shop.general_unlocked",
                new[]
                {
                    Offer("offer.general.leather_armor", items["item.leather_armor"]),
                    Offer("offer.general.swift_boots", items["item.swift_boots"])
                });
            var blacksmithShop = Shop(
                "shop.blacksmith",
                "event.shop.blacksmith_unlocked",
                new[]
                {
                    Offer("offer.blacksmith.frost_longsword", items["item.frost_longsword"])
                });
            generated.AddRange(new[] { generalShop, blacksmithShop });

            var forestBandits = Encounter(
                "encounter.forest.bandits",
                new[] { "enemy.bandit", "enemy.bandit" },
                rewardTable.Id,
                20,
                35,
                new Vector2(8f, 3f),
                1.5f,
                true,
                string.Empty,
                string.Empty);
            var forestWolves = Encounter(
                "encounter.forest.wolves",
                new[] { "enemy.bandit", "enemy.bandit", "enemy.bandit" },
                rewardTable.Id,
                25,
                40,
                new Vector2(18f, 12f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            var watchtowerRangers = Encounter(
                "encounter.watchtower.rangers",
                new[] { "enemy.ranger", "enemy.ranger" },
                rewardTable.Id,
                30,
                45,
                new Vector2(8f, 3f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            var watchtowerElite = Encounter(
                "encounter.watchtower.elite",
                new[] { "enemy.ranger", "enemy.mage" },
                rewardTable.Id,
                45,
                65,
                new Vector2(18f, 12f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            var cryptGuardians = Encounter(
                "encounter.crypt.guardians",
                new[] { "enemy.bandit", "enemy.bandit" },
                rewardTable.Id,
                35,
                50,
                new Vector2(8f, 3f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            var cryptNecromancers = Encounter(
                "encounter.crypt.necromancers",
                new[] { "enemy.mage", "enemy.mage" },
                rewardTable.Id,
                55,
                80,
                new Vector2(18f, 3f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            var cryptBoss = Encounter(
                "encounter.crypt.boss",
                new[] { "enemy.crypt_boss" },
                rewardTable.Id,
                100,
                120,
                new Vector2(18f, 13f),
                1.4f,
                false,
                string.Empty,
                "event.encounter.crypt.boss.defeated");
            var roadPatrol = Encounter(
                "encounter.road.patrol",
                new[] { "enemy.ranger", "enemy.bandit" },
                rewardTable.Id,
                25,
                40,
                new Vector2(17f, 8f),
                1.4f,
                true,
                string.Empty,
                string.Empty);
            generated.AddRange(new[]
            {
                forestBandits,
                forestWolves,
                watchtowerRangers,
                watchtowerElite,
                cryptGuardians,
                cryptNecromancers,
                cryptBoss,
                roadPatrol
            });

            var village = Area(
                "area.village",
                new Rect(0f, 0f, 24f, 16f),
                new[] { new Rect(10f, 5f, 3f, 3f), new Rect(15f, 10f, 4f, 2f) },
                new[] { elderNpc, blacksmithNpc, merchantNpc, innkeeperNpc },
                new[] { roadPatrol },
                new[]
                {
                    NpcInteraction("interaction.village.elder", elderNpc.Id, new Vector2(6f, 2f)),
                    NpcInteraction("interaction.village.blacksmith", blacksmithNpc.Id, new Vector2(6f, 6f)),
                    NpcInteraction("interaction.village.merchant", merchantNpc.Id, new Vector2(6f, 10f)),
                    NpcInteraction("interaction.village.innkeeper", innkeeperNpc.Id, new Vector2(11f, 13f)),
                    RewardInteraction("interaction.village.gather", WorldInteractableKind.Gather, "item.leather_armor", new Vector2(13f, 3f)),
                    RewardInteraction("interaction.village.chest", WorldInteractableKind.Chest, "item.swift_boots", new Vector2(18f, 13f)),
                    InvestigateInteraction("interaction.village.investigate", "event.village.old_marker", new Vector2(15f, 3f)),
                    ExitInteraction("interaction.village.exit.forest", "area.forest", new Vector2(22f, 2f), new Vector2(3f, 2f)),
                    EncounterInteraction("interaction.village.encounter.road_patrol", roadPatrol.EncounterId, roadPatrol.Position)
                },
                new[]
                {
                    "event.village.old_marker",
                    "event.shop.general_unlocked",
                    "event.shop.blacksmith_unlocked"
                },
                rewardTable);

            var forest = Area(
                "area.forest",
                new Rect(0f, 0f, 24f, 16f),
                new[] { new Rect(8f, 0f, 1.5f, 5f), new Rect(14f, 7f, 4f, 4f) },
                new[] { rangerNpc, mageNpc },
                new[] { forestBandits, forestWolves },
                new[]
                {
                    NpcInteraction("interaction.forest.ranger", rangerNpc.Id, new Vector2(6f, 3f)),
                    NpcInteraction("interaction.forest.mage", mageNpc.Id, new Vector2(6f, 11f)),
                    RewardInteraction("interaction.forest.gather", WorldInteractableKind.Gather, "item.leather_armor", new Vector2(12f, 4f)),
                    RewardInteraction("interaction.forest.chest", WorldInteractableKind.Chest, "item.iron_helmet", new Vector2(18f, 5f)),
                    InvestigateInteraction("interaction.forest.investigate", "event.forest.tracks", new Vector2(13f, 12f)),
                    ExitInteraction("interaction.forest.exit.village", "area.village", new Vector2(2f, 2f), new Vector2(20f, 2f)),
                    ExitInteraction("interaction.forest.exit.watchtower", "area.watchtower", new Vector2(22f, 2f), new Vector2(3f, 2f)),
                    EncounterInteraction("interaction.forest.encounter.bandits", forestBandits.EncounterId, forestBandits.Position),
                    EncounterInteraction("interaction.forest.encounter.wolves", forestWolves.EncounterId, forestWolves.Position)
                },
                new[] { "event.forest.tracks" },
                rewardTable);

            var watchtower = Area(
                "area.watchtower",
                new Rect(0f, 0f, 24f, 16f),
                new[] { new Rect(10f, 6f, 3f, 3f) },
                new[] { hunterNpc },
                new[] { watchtowerRangers, watchtowerElite },
                new[]
                {
                    NpcInteraction("interaction.watchtower.hunter", hunterNpc.Id, new Vector2(6f, 3f)),
                    RewardInteraction("interaction.watchtower.gather", WorldInteractableKind.Gather, "item.iron_helmet", new Vector2(12f, 3f)),
                    RewardInteraction("interaction.watchtower.chest", WorldInteractableKind.Chest, "item.frost_longsword", new Vector2(18f, 12f)),
                    InvestigateInteraction("interaction.watchtower.investigate", "event.watchtower.orders", new Vector2(13f, 12f)),
                    ExitInteraction("interaction.watchtower.exit.forest", "area.forest", new Vector2(2f, 2f), new Vector2(20f, 2f)),
                    ExitInteraction("interaction.watchtower.exit.crypt", "area.crypt", new Vector2(22f, 2f), new Vector2(3f, 2f)),
                    EncounterInteraction("interaction.watchtower.encounter.rangers", watchtowerRangers.EncounterId, watchtowerRangers.Position),
                    EncounterInteraction("interaction.watchtower.encounter.elite", watchtowerElite.EncounterId, watchtowerElite.Position)
                },
                new[] { "event.watchtower.orders" },
                rewardTable);

            var crypt = Area(
                "area.crypt",
                new Rect(0f, 0f, 24f, 16f),
                new[] { new Rect(10f, 6f, 3f, 3f) },
                new[] { survivorNpc },
                new[] { cryptGuardians, cryptNecromancers, cryptBoss },
                new[]
                {
                    NpcInteraction("interaction.crypt.survivor", survivorNpc.Id, new Vector2(6f, 3f)),
                    RewardInteraction("interaction.crypt.gather", WorldInteractableKind.Gather, "item.swift_boots", new Vector2(12f, 3f)),
                    RewardInteraction("interaction.crypt.chest", WorldInteractableKind.Chest, "item.ember_charm", new Vector2(18f, 13f)),
                    InvestigateInteraction("interaction.crypt.investigate", "event.crypt.seal", new Vector2(12f, 12f)),
                    ExitInteraction("interaction.crypt.exit.watchtower", "area.watchtower", new Vector2(2f, 2f), new Vector2(20f, 2f)),
                    EncounterInteraction("interaction.crypt.encounter.guardians", cryptGuardians.EncounterId, cryptGuardians.Position),
                    EncounterInteraction("interaction.crypt.encounter.necromancers", cryptNecromancers.EncounterId, cryptNecromancers.Position),
                    EncounterInteraction("interaction.crypt.encounter.boss", cryptBoss.EncounterId, cryptBoss.Position)
                },
                new[]
                {
                    "event.crypt.seal",
                    "event.encounter.crypt.boss.defeated"
                },
                rewardTable);

            generated.AddRange(new[] { village, forest, watchtower, crypt });

            var generatedIds = new HashSet<string>(generated.Select(value => value.Id), StringComparer.Ordinal);
            var merged = catalog.All
                .Where(value => value != null && !generatedIds.Contains(value.Id))
                .Concat(generated)
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var issues = ContentValidator.Validate(merged).ToArray();
            if (issues.Length > 0)
            {
                throw new InvalidOperationException(
                    "World content validation failed: " +
                    string.Join(" | ", issues.Select(issue => issue.Code + ": " + issue.Message)));
            }

            catalog.EditorSetDefinitions(merged);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static QuestDefinition Quest(
            string id,
            string titleKey,
            string descriptionKey,
            IEnumerable<QuestObjectiveDefinition> objectives,
            IEnumerable<QuestRewardDefinition> rewards)
        {
            var asset = GetOrCreate<QuestDefinition>(NarrativeRoot + "/Quests/" + id + ".asset");
            asset.EditorConfigure(
                id,
                titleKey,
                descriptionKey,
                Array.Empty<string>(),
                objectives,
                rewards);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static NpcDefinition Npc(string id, string dialogueId, string shopId = "")
        {
            var asset = GetOrCreate<NpcDefinition>(NarrativeRoot + "/Npcs/" + id + ".asset");
            asset.EditorConfigure(id, id + ".name", dialogueId, shopId, 0);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DialogueDefinition Dialogue(string id, params DialogueNodeDefinition[] nodes)
        {
            var asset = GetOrCreate<DialogueDefinition>(NarrativeRoot + "/Dialogues/" + id + ".asset");
            asset.EditorConfigure(id, nodes[0].NodeId, nodes);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DialogueNodeDefinition Node(
            string nodeId,
            string speakerId,
            string textKey,
            IEnumerable<DialogueChoiceDefinition> choices = null,
            string nextNodeId = "",
            IEnumerable<DialogueActionDefinition> actions = null,
            IEnumerable<DialogueConditionDefinition> conditions = null) =>
            new(
                nodeId,
                speakerId,
                textKey,
                conditions,
                actions,
                choices,
                nextNodeId);

        private static DialogueChoiceDefinition Choice(
            string id,
            string labelKey,
            string nextNodeId,
            IEnumerable<DialogueActionDefinition> actions = null,
            IEnumerable<DialogueConditionDefinition> conditions = null) =>
            new(id, labelKey, nextNodeId, conditions, actions);

        private static DialogueDefinition CreateElderDialogue()
        {
            var start = Node(
                "dialogue.elder.start",
                "npc.elder",
                "dialogue.elder.start.text",
                new[]
                {
                    Choice(
                        "dialogue.elder.choice.accept",
                        "dialogue.elder.choice.accept.label",
                        "dialogue.elder.accepted",
                        new[] { new DialogueActionDefinition(DialogueActionKind.AcceptQuest, "quest.main.crypt") },
                        new[]
                        {
                            new DialogueConditionDefinition(
                                DialogueConditionKind.QuestState,
                                "quest.main.crypt",
                                (int)QuestState.NotStarted)
                        }),
                    Choice(
                        "dialogue.elder.choice.turn_in",
                        "dialogue.elder.choice.turn_in.label",
                        "dialogue.elder.completed",
                        new[] { new DialogueActionDefinition(DialogueActionKind.TurnInQuest, "quest.main.crypt") },
                        new[]
                        {
                            new DialogueConditionDefinition(
                                DialogueConditionKind.QuestState,
                                "quest.main.crypt",
                                (int)QuestState.ReadyToTurnIn)
                        }),
                    Choice(
                        "dialogue.elder.choice.leave",
                        "dialogue.elder.choice.leave.label",
                        "dialogue.elder.leave")
                });
            return Dialogue(
                "dialogue.elder",
                start,
                Node("dialogue.elder.accepted", "npc.elder", "dialogue.elder.accepted.text"),
                Node("dialogue.elder.completed", "npc.elder", "dialogue.elder.completed.text"),
                Node("dialogue.elder.leave", "npc.elder", "dialogue.elder.leave.text"));
        }

        private static DialogueDefinition CreateOpenShopDialogue(
            string id,
            string npcId,
            string unlockEventId,
            string shopId)
        {
            var node = Node(
                id + ".start",
                npcId,
                id + ".start.text",
                actions: new[]
                {
                    new DialogueActionDefinition(DialogueActionKind.SetEvent, unlockEventId),
                    new DialogueActionDefinition(DialogueActionKind.OpenShop, shopId)
                });
            return Dialogue(id, node);
        }

        private static DialogueDefinition CreateSideQuestDialogue(
            string id,
            string npcId,
            string questId)
        {
            var start = Node(
                id + ".start",
                npcId,
                id + ".start.text",
                new[]
                {
                    Choice(
                        id + ".choice.accept",
                        id + ".choice.accept.label",
                        id + ".accepted",
                        new[] { new DialogueActionDefinition(DialogueActionKind.AcceptQuest, questId) },
                        new[]
                        {
                            new DialogueConditionDefinition(
                                DialogueConditionKind.QuestState,
                                questId,
                                (int)QuestState.NotStarted)
                        }),
                    Choice(
                        id + ".choice.turn_in",
                        id + ".choice.turn_in.label",
                        id + ".completed",
                        new[] { new DialogueActionDefinition(DialogueActionKind.TurnInQuest, questId) },
                        new[]
                        {
                            new DialogueConditionDefinition(
                                DialogueConditionKind.QuestState,
                                questId,
                                (int)QuestState.ReadyToTurnIn)
                        }),
                    Choice(id + ".choice.leave", id + ".choice.leave.label", id + ".leave")
                });
            return Dialogue(
                id,
                start,
                Node(id + ".accepted", npcId, id + ".accepted.text"),
                Node(id + ".completed", npcId, id + ".completed.text"),
                Node(id + ".leave", npcId, id + ".leave.text"));
        }

        private static DialogueDefinition CreateSimpleDialogue(string id, string npcId) =>
            Dialogue(id, Node(id + ".start", npcId, id + ".start.text"));

        private static ShopDefinition Shop(
            string id,
            string requiredEventId,
            IEnumerable<ShopOfferDefinition> offers)
        {
            var asset = GetOrCreate<ShopDefinition>(NarrativeRoot + "/Shops/" + id + ".asset");
            asset.EditorConfigure(id, id + ".name", requiredEventId, offers);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ShopOfferDefinition Offer(string offerId, ItemDefinition item) =>
            new(offerId, item, ItemRarity.Common, 1, Array.Empty<AffixDefinition>());

        private static WorldEncounterDefinition Encounter(
            string id,
            IEnumerable<string> enemyDefinitionIds,
            string rewardTableId,
            int goldReward,
            int experienceReward,
            Vector2 position,
            float triggerRadius,
            bool repeatable,
            string requiredEventId,
            string completionEventId)
        {
            var asset = GetOrCreate<WorldEncounterDefinition>(Root + "/Encounters/" + id + ".asset");
            asset.EditorConfigure(
                id,
                "core",
                enemyDefinitionIds,
                rewardTableId,
                goldReward,
                experienceReward,
                position,
                triggerRadius,
                repeatable,
                requiredEventId,
                completionEventId);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WorldAreaDefinition Area(
            string id,
            Rect bounds,
            IEnumerable<Rect> obstacles,
            IEnumerable<NpcDefinition> npcs,
            IEnumerable<WorldEncounterDefinition> encounters,
            IEnumerable<WorldInteractableDefinition> interactables,
            IEnumerable<string> eventIds,
            ItemDropTableDefinition rewardTable)
        {
            var asset = GetOrCreate<WorldAreaDefinition>(Root + "/Areas/" + id + ".asset");
            asset.EditorConfigure(
                id,
                bounds,
                obstacles,
                npcs,
                encounters,
                interactables,
                new[] { rewardTable },
                eventIds,
                new[] { rewardTable.Id });
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WorldInteractableDefinition NpcInteraction(
            string id,
            string npcId,
            Vector2 position) =>
            new(
                id,
                WorldInteractableKind.Npc,
                id + ".label",
                position,
                1.2f,
                npcId,
                position,
                string.Empty);

        private static WorldInteractableDefinition RewardInteraction(
            string id,
            WorldInteractableKind kind,
            string itemId,
            Vector2 position) =>
            new(
                id,
                kind,
                id + ".label",
                position,
                1.1f,
                itemId,
                position,
                string.Empty);

        private static WorldInteractableDefinition InvestigateInteraction(
            string id,
            string eventId,
            Vector2 position) =>
            new(
                id,
                WorldInteractableKind.Investigate,
                id + ".label",
                position,
                1.1f,
                eventId,
                position,
                string.Empty);

        private static WorldInteractableDefinition ExitInteraction(
            string id,
            string targetAreaId,
            Vector2 position,
            Vector2 arrivalPosition) =>
            new(
                id,
                WorldInteractableKind.AreaExit,
                id + ".label",
                position,
                1.3f,
                targetAreaId,
                arrivalPosition,
                string.Empty);

        private static WorldInteractableDefinition EncounterInteraction(
            string id,
            string encounterId,
            Vector2 position) =>
            new(
                id,
                WorldInteractableKind.Encounter,
                id + ".label",
                position,
                1.4f,
                encounterId,
                position,
                string.Empty);

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
