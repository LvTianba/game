using System;
using BorderValley.Core.Combat;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [Serializable]
    public struct StatValue
    {
        [SerializeField] private CombatStat stat;
        [SerializeField] private int value;

        public StatValue(CombatStat stat, int value)
        {
            this.stat = stat;
            this.value = value;
        }

        public CombatStat Stat => stat;
        public int Value => value;
    }
}
