using System.Linq;
using BorderValley.Core.Boot;
using UnityEngine;

namespace BorderValley.Data
{
    public sealed class ContentRuntimeValidator : MonoBehaviour, IPreflightCheck
    {
        public bool Validate(out string error)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null)
            {
                error = "ContentCatalog was not found in Resources.";
                Debug.LogError(error);
                return false;
            }

            var issues = ContentValidator.Validate(catalog.All).ToArray();
            foreach (var issue in issues)
            {
                Debug.LogError($"{issue.Code}: {issue.Message}", issue.Context);
            }

            error = string.Join(System.Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message}"));
            return issues.Length == 0;
        }
    }
}
