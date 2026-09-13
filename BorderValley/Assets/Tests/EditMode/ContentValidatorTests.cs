using System.Linq;
using BorderValley.Data;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Data.Tests
{
    public sealed class ContentValidatorTests
    {
        [Test]
        public void Validate_DuplicateIds_ReturnsIssue()
        {
            var first = ScriptableObject.CreateInstance<ContentDefinition>();
            var second = ScriptableObject.CreateInstance<ContentDefinition>();
            first.EditorSetId("item.sword");
            second.EditorSetId("item.sword");
            var issues = ContentValidator.Validate(new[] { first, second }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "duplicate_id"), Is.True);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Validate_InvalidId_ReturnsIssue()
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId("Item Sword!");
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "invalid_id"), Is.True);
            Object.DestroyImmediate(definition);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Validate_MissingOrWhitespaceId_ReturnsMissingId(string id)
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId(id);
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "missing_id"), Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Validate_NullDefinition_ReturnsMissingIdWithNullContext()
        {
            var issues = ContentValidator.Validate(new ContentDefinition[] { null }).ToList();
            var issue = issues.Single(candidate => candidate.Code == "missing_id");
            Assert.That(issue.Context, Is.Null);
        }

        [Test]
        public void Validate_ValidId_ReturnsNoIssues()
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId("item.sword");
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues, Is.Empty);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Validate_EmptySequence_ReturnsNoIssues()
        {
            var issues = ContentValidator.Validate(new ContentDefinition[0]).ToList();
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_NullSequence_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => ContentValidator.Validate(null));
        }
    }
}
