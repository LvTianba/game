using BorderValley.Battle.Domain;

namespace BorderValley.UI.Battle
{
    public static class BattleTextKeys
    {
        public const string EndTurn = "battle.ui.end_turn";
        public const string Continue = "battle.ui.continue";
        public const string InvalidTarget = "battle.ui.error.invalid_target";
        public const string NotPlayerTurn = "battle.ui.error.not_player_turn";
        public const string Round = "battle.hud.round";
        public const string ActiveUnit = "battle.hud.active_unit";
        public const string ActiveUnitNone = "battle.hud.active_unit.none";
        public const string TurnOrder = "battle.hud.turn_order";
        public const string Health = "battle.hud.health";
        public const string Mana = "battle.hud.mana";
        public const string UnitNone = "battle.hud.unit.none";
        public const string Moved = "battle.ui.moved";
        public const string Acted = "battle.ui.acted";
        public const string StatusLabel = "battle.ui.status";
        public const string StatusNone = "battle.ui.status.none";
        public const string Yes = "battle.ui.yes";
        public const string No = "battle.ui.no";
        public const string PlayerVictory = "battle.result.player_victory";
        public const string EnemyVictory = "battle.result.enemy_victory";
        public const string AiCommandFailed = "battle.ui.error.ai_command_failed";
        public const string AiFallbackFailed = "battle.ui.error.ai_fallback_failed";
        public const string AiException = "battle.ui.error.ai_exception";

        public static string Unit(string definitionId) =>
            definitionId switch
            {
                "unit.warrior" => "battle.unit.warrior",
                "unit.ranger" => "battle.unit.ranger",
                "unit.mage" => "battle.unit.mage",
                "unit.bandit" => "battle.unit.bandit",
                "enemy.bandit" => "battle.unit.bandit",
                "enemy.ranger" => "battle.unit.ranger",
                "enemy.mage" => "battle.unit.mage",
                _ => "battle.unit.unknown"
            };

        public static string Flag(bool value) => value ? Yes : No;

        public static string StatusKey(StatusType type) =>
            type switch
            {
                StatusType.Burning => "battle.status.burning",
                StatusType.Poisoned => "battle.status.poisoned",
                StatusType.Stunned => "battle.status.stunned",
                StatusType.Slowed => "battle.status.slowed",
                StatusType.Shielded => "battle.status.shielded",
                StatusType.Taunted => "battle.status.taunted",
                _ => "battle.status.unknown"
            };
    }
}