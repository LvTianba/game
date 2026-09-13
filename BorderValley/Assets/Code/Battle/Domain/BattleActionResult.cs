using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleActionResult
    {
        private BattleActionResult(
            bool success,
            string errorCode,
            string message,
            IEnumerable<string> affectedUnitIds)
        {
            Success = success;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            AffectedUnitIds = (affectedUnitIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()
                .AsReadOnly();
        }

        public bool Success { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public IReadOnlyList<string> AffectedUnitIds { get; }
        public string AffectedUnitId =>
            AffectedUnitIds.Count == 0 ? string.Empty : AffectedUnitIds[0];

        public static BattleActionResult Succeeded(
            string message,
            params string[] affectedUnitIds) =>
            new(true, string.Empty, message, affectedUnitIds);

        public static BattleActionResult Failed(
            string errorCode,
            string message = null,
            params string[] affectedUnitIds) =>
            new(false, errorCode, message ?? errorCode, affectedUnitIds);
    }
}