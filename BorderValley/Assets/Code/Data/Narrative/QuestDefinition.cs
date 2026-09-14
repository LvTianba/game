using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [CreateAssetMenu(menuName = "BorderValley/Narrative/Quest Definition", fileName = "QuestDefinition")]
    public sealed class QuestDefinition : ContentDefinition
    {
        [SerializeField] private string titleKey = string.Empty;
        [SerializeField] private string descriptionKey = string.Empty;
        [SerializeField] private string[] prerequisiteQuestIds = Array.Empty<string>();
        [SerializeField] private QuestObjectiveDefinition[] objectives = Array.Empty<QuestObjectiveDefinition>();
        [SerializeField] private QuestRewardDefinition[] rewards = Array.Empty<QuestRewardDefinition>();
        [SerializeField] private bool repeatable;

        public string TitleKey => titleKey;
        public string DescriptionKey => descriptionKey;
        public string[] PrerequisiteQuestIds => prerequisiteQuestIds;
        public QuestObjectiveDefinition[] Objectives => objectives;
        public QuestRewardDefinition[] Rewards => rewards;
        public bool Repeatable => repeatable;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string titleKey,
            string descriptionKey,
            IEnumerable<string> prerequisiteQuestIds,
            IEnumerable<QuestObjectiveDefinition> objectives,
            IEnumerable<QuestRewardDefinition> rewards)
        {
            EditorSetId(id);
            this.titleKey = titleKey ?? string.Empty;
            this.descriptionKey = descriptionKey ?? string.Empty;
            this.prerequisiteQuestIds = (prerequisiteQuestIds ?? Array.Empty<string>()).ToArray();
            this.objectives = (objectives ?? Array.Empty<QuestObjectiveDefinition>()).ToArray();
            this.rewards = (rewards ?? Array.Empty<QuestRewardDefinition>()).ToArray();
        }

        public void EditorSetRepeatable(bool value) => repeatable = value;
#endif
    }
}
