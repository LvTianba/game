using System;
using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleTurnEngine
    {
        private readonly BattleState state;
        private readonly List<BattleUnit> order = new();
        private int activeIndex;

        public BattleTurnEngine(BattleState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public BattleUnit ActiveUnit { get; private set; }

        public void Start()
        {
            var livingUnits = TurnOrder.Build(state.LivingUnits);
            if (livingUnits.Count == 0) throw new InvalidOperationException("Cannot start a battle without living units.");

            order.Clear();
            order.AddRange(livingUnits);
            activeIndex = 0;
            ActivateCurrent();
        }

        public void EndTurn()
        {
            if (ActiveUnit == null) throw new InvalidOperationException("Battle has not started.");

            while (true)
            {
                activeIndex++;
                if (activeIndex >= order.Count)
                {
                    state.Round++;
                    order.Clear();
                    order.AddRange(TurnOrder.Build(state.LivingUnits));
                    activeIndex = 0;
                }

                if (order.Count == 0) throw new InvalidOperationException("No living units remain.");
                if (order[activeIndex].IsAlive) break;
            }

            ActivateCurrent();
        }

        private void ActivateCurrent()
        {
            ActiveUnit = order[activeIndex];
            ActiveUnit.RefreshForTurn();
        }
    }
}
