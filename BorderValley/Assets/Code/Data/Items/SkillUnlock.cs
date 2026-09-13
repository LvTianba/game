using System;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [Serializable]
    public struct SkillUnlock
    {
        [SerializeField] private string skillId;
        [SerializeField] private int level;

        public SkillUnlock(string skillId, int level)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                throw new ArgumentException(nameof(skillId));
            this.skillId = skillId;
            this.level = level;
        }

        public string SkillId => skillId;
        public int Level => level;
    }
}
