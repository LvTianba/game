using System;
using System.Linq;
using System.Collections.Generic;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using UnityEngine;

namespace BorderValley.Data.World
{
    [CreateAssetMenu(menuName = "BorderValley/World/Area Definition", fileName = "WorldAreaDefinition")]
    public sealed class WorldAreaDefinition : ContentDefinition
    {
        [SerializeField] private Rect bounds;
        [SerializeField] private Rect[] obstacles = Array.Empty<Rect>();
        [SerializeField] private NpcDefinition[] npcs = Array.Empty<NpcDefinition>();
        [SerializeField] private WorldEncounterDefinition[] encounters = Array.Empty<WorldEncounterDefinition>();
        [SerializeField] private WorldInteractableDefinition[] interactables = Array.Empty<WorldInteractableDefinition>();
        [SerializeField] private ItemDropTableDefinition[] rewardTables = Array.Empty<ItemDropTableDefinition>();
        [SerializeField] private string[] eventIds = Array.Empty<string>();
        [SerializeField] private string[] rewardTableIds = Array.Empty<string>();

        public Rect Bounds => bounds;
        public Rect[] Obstacles => obstacles;
        public NpcDefinition[] Npcs => npcs;
        public NpcDefinition[] NpcDefinitions => npcs;
        public WorldEncounterDefinition[] Encounters => encounters;
        public WorldEncounterDefinition[] EncounterDefinitions => encounters;
        public WorldInteractableDefinition[] Interactables => interactables;
        public WorldInteractableDefinition[] WorldInteractables => interactables;
        public ItemDropTableDefinition[] RewardTables => rewardTables;
        public string[] EventIds => eventIds;
        public string[] RewardTableIds => rewardTableIds;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            Rect bounds,
            IEnumerable<Rect> obstacles,
            IEnumerable<NpcDefinition> npcs,
            IEnumerable<WorldEncounterDefinition> encounters,
            IEnumerable<WorldInteractableDefinition> interactables,
            IEnumerable<ItemDropTableDefinition> rewardTables)
        {
            EditorConfigure(
                id,
                bounds,
                obstacles,
                npcs,
                encounters,
                interactables,
                rewardTables,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        public void EditorConfigure(
            string id,
            Rect bounds,
            IEnumerable<Rect> obstacles,
            IEnumerable<NpcDefinition> npcs,
            IEnumerable<WorldEncounterDefinition> encounters,
            IEnumerable<WorldInteractableDefinition> interactables,
            IEnumerable<ItemDropTableDefinition> rewardTables,
            IEnumerable<string> eventIds,
            IEnumerable<string> rewardTableIds)
        {
            EditorSetId(id);
            this.bounds = bounds;
            this.obstacles = (obstacles ?? Array.Empty<Rect>()).ToArray();
            this.npcs = (npcs ?? Array.Empty<NpcDefinition>()).ToArray();
            this.encounters = (encounters ?? Array.Empty<WorldEncounterDefinition>()).ToArray();
            this.interactables = (interactables ?? Array.Empty<WorldInteractableDefinition>()).ToArray();
            this.rewardTables = (rewardTables ?? Array.Empty<ItemDropTableDefinition>()).ToArray();
            this.eventIds = (eventIds ?? Array.Empty<string>()).ToArray();
            this.rewardTableIds = (rewardTableIds ?? Array.Empty<string>()).ToArray();
        }
#endif
    }
}
