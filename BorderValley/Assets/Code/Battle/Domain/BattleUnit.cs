using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleUnit
    {
        private readonly List<StatusInstance> statuses = new();

        public BattleUnit(
            string id,
            string definitionId,
            Team team,
            UnitStats stats,
            GridPosition position)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException(nameof(id)) : id;
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException(nameof(definitionId)) : definitionId;
            Team = team;
            Stats = stats;
            Position = position;
            Health = stats.MaxHealth;
            Mana = stats.MaxMana;
        }

        public string Id { get; }
        public string DefinitionId { get; }
        public Team Team { get; }
        public UnitStats Stats { get; }
        public GridPosition Position { get; private set; }
        public int Health { get; private set; }
        public int Mana { get; private set; }
        public bool IsAlive => Health > 0;
        public bool HasMoved { get; private set; }
        public bool HasActed { get; private set; }
        public IReadOnlyList<StatusInstance> Statuses => statuses;
        public Dictionary<string, int> Cooldowns { get; } = new(StringComparer.Ordinal);

        public void MoveTo(GridPosition position)
        {
            if (!IsAlive) throw new InvalidOperationException("Dead units cannot move.");
            Position = position;
            HasMoved = true;
        }

        public void MarkActionUsed()
        {
            if (!IsAlive) throw new InvalidOperationException("Dead units cannot act.");
            HasActed = true;
        }

        public void RefreshForTurn()
        {
            HasMoved = false;
            HasActed = false;

            foreach (var skillId in Cooldowns.Keys.ToArray())
                Cooldowns[skillId] = Math.Max(0, Cooldowns[skillId] - 1);
        }

        public void MoveForced(GridPosition position)
        {
            if (!IsAlive) throw new InvalidOperationException("Dead units cannot be moved.");
            Position = position;
        }

        public void ApplyRawDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            Health = Math.Min(Stats.MaxHealth, Health + Math.Max(0, amount));
        }

        public bool TrySpendMana(int amount)
        {
            if (amount < 0 || Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        public void RestoreMana(int amount)
        {
            Mana = Math.Min(Stats.MaxMana, Mana + Math.Max(0, amount));
        }

        public List<StatusInstance> MutableStatuses => statuses;
    }
}
