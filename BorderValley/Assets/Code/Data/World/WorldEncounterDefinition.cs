using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.World
{
    [CreateAssetMenu(menuName = "BorderValley/World/Encounter Definition", fileName = "WorldEncounterDefinition")]
    public sealed class WorldEncounterDefinition : ContentDefinition
    {
        [SerializeField] private string encounterId = string.Empty;
        [SerializeField] private string scenarioId = string.Empty;
        [SerializeField] private string[] enemyDefinitionIds = Array.Empty<string>();
        [SerializeField] private string rewardTableId = string.Empty;
        [SerializeField] private int goldReward;
        [SerializeField] private int experienceReward;
        [SerializeField] private Vector2 position;
        [SerializeField] private float triggerRadius = 1f;
        [SerializeField] private bool repeatable;
        [SerializeField] private string requiredEventId = string.Empty;
        [SerializeField] private string completionEventId = string.Empty;

        public string EncounterId => string.IsNullOrWhiteSpace(encounterId) ? Id : encounterId;
        public string ScenarioId => scenarioId;
        public string[] EnemyDefinitionIds => enemyDefinitionIds;
        public string RewardTableId => rewardTableId;
        public int GoldReward => goldReward;
        public int ExperienceReward => experienceReward;
        public Vector2 Position => position;
        public float TriggerRadius => triggerRadius;
        public bool Repeatable => repeatable;
        public string RequiredEventId => requiredEventId;
        public string CompletionEventId => completionEventId;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string scenarioId,
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
            EditorSetId(id);
            encounterId = id ?? string.Empty;
            this.scenarioId = scenarioId ?? string.Empty;
            this.enemyDefinitionIds = (enemyDefinitionIds ?? Array.Empty<string>()).ToArray();
            this.rewardTableId = rewardTableId ?? string.Empty;
            this.goldReward = goldReward;
            this.experienceReward = experienceReward;
            this.position = position;
            this.triggerRadius = triggerRadius;
            this.repeatable = repeatable;
            this.requiredEventId = requiredEventId ?? string.Empty;
            this.completionEventId = completionEventId ?? string.Empty;
        }
#endif
    }
}
