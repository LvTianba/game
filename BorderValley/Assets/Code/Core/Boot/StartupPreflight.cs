using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.Boot
{
    public static class StartupPreflight
    {
        public static bool ShouldContinue(
            IEnumerable<IPreflightCheck> checks,
            bool blockOnFailure,
            out string errors)
        {
            var messages = new List<string>();
            foreach (var check in checks ?? Array.Empty<IPreflightCheck>())
            {
                try
                {
                    if (!check.Validate(out var error))
                    {
                        messages.Add(string.IsNullOrWhiteSpace(error)
                            ? check.GetType().FullName
                            : error);
                    }
                }
                catch (Exception exception)
                {
                    messages.Add($"{check.GetType().FullName}: {exception.Message}");
                }
            }

            errors = string.Join(Environment.NewLine, messages.Distinct());
            return !blockOnFailure || messages.Count == 0;
        }
    }
}
