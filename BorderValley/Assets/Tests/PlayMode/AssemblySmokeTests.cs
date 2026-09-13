using NUnit.Framework;

namespace BorderValley.PlayModeTests
{
    public sealed class AssemblySmokeTests
    {
        [Test]
        public void CoreAssembly_IsLoadable()
        {
            Assert.That(typeof(BorderValley.Core.GameContext).Assembly.GetName().Name,
                Is.EqualTo("BorderValley.Core"));
        }
    }
}
