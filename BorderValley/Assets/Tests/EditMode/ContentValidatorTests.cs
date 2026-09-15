using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Narrative;
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
        public void Validate_ShopWithoutOwner_ReturnsMissingShopOwner()
        {
            var shop = ScriptableObject.CreateInstance<ShopDefinition>();
            shop.EditorConfigure("shop.general", "shop.general.name", string.Empty, new ShopOfferDefinition[0]);

            var issues = ContentValidator.Validate(new ContentDefinition[] { shop }).ToList();

            Assert.That(issues.Any(issue => issue.Code == "missing_shop_owner"), Is.True);
            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Validate_ShopWithTwoOwners_ReturnsMultipleShopOwners()
        {
            var shop = ScriptableObject.CreateInstance<ShopDefinition>();
            shop.EditorConfigure("shop.general", "shop.general.name", string.Empty, new ShopOfferDefinition[0]);
            var first = ScriptableObject.CreateInstance<NpcDefinition>();
            first.EditorConfigure("npc.first", "npc.first.name", string.Empty, shop.Id, 0);
            var second = ScriptableObject.CreateInstance<NpcDefinition>();
            second.EditorConfigure("npc.second", "npc.second.name", string.Empty, shop.Id, 1);

            var issues = ContentValidator.Validate(new ContentDefinition[] { shop, first, second }).ToList();

            Assert.That(issues.Any(issue => issue.Code == "multiple_shop_owners"), Is.True);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(shop);
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
