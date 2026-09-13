using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BorderValley.Data
{
    public static class ContentValidator
    {
        private static readonly Regex ValidId = new("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled);

        public static IEnumerable<ContentValidationIssue> Validate(IEnumerable<ContentDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            return ValidateCore(definitions);
        }

        private static IEnumerable<ContentValidationIssue> ValidateCore(IEnumerable<ContentDefinition> definitions)
        {
            var seen = new HashSet<string>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition is null.", null);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition has no ID.", definition);
                    continue;
                }

                if (!ValidId.IsMatch(definition.Id))
                {
                    yield return new ContentValidationIssue("invalid_id", $"Invalid ID: {definition.Id}", definition);
                    continue;
                }

                if (!seen.Add(definition.Id))
                    yield return new ContentValidationIssue("duplicate_id", $"Duplicate ID: {definition.Id}", definition);
            }
        }
    }
}
