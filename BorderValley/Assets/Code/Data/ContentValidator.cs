using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BorderValley.Data.Items;

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
            var all = definitions.ToArray();
            var seen = new HashSet<string>();
            foreach (var definition in all)
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

            foreach (var issue in ValidateSpecialized(all))
                yield return issue;
        }

        private static IEnumerable<ContentValidationIssue> ValidateSpecialized(IEnumerable<ContentDefinition> definitions)
        {
            var all = definitions.Where(definition => definition != null).ToArray();
            var ids = all.Select(definition => definition.Id).Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var affix in all.OfType<AffixDefinition>())
            {
                if (affix.MinValue > affix.MaxValue)
                    yield return Issue("invalid_affix_range", affix.Id, affix);
                if (affix.CompatibleSlots.Length == 0)
                    yield return Issue("affix_without_slot", affix.Id, affix);
                foreach (var excluded in affix.MutuallyExclusiveAffixIds)
                    if (!ids.Contains(excluded))
                        yield return Issue("missing_affix_exclusion", $"{affix.Id} -> {excluded}", affix);
            }

            foreach (var table in all.OfType<ItemDropTableDefinition>())
            {
                if (table.MinItemLevel > table.MaxItemLevel)
                    yield return Issue("invalid_drop_level_range", table.Id, table);
                foreach (var entry in table.Entries)
                    if (entry.Item == null || !ids.Contains(entry.Item.Id))
                        yield return Issue("invalid_drop_item", table.Id, table);
            }
        }

        private static ContentValidationIssue Issue(string code, string message, UnityEngine.Object context) =>
            new(code, message, context);
    }
}
