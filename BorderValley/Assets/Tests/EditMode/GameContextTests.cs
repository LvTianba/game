using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class GameContextTests
    {
        [Test]
        public void Get_AfterRegister_ReturnsSameInstance()
        {
            var context = new GameContext();
            var service = new TestService();
            context.Register(service);
            Assert.That(context.Get<TestService>(), Is.SameAs(service));
        }

        [Test]
        public void Get_WhenNotRegistered_Throws()
        {
            var context = new GameContext();
            Assert.Throws<System.InvalidOperationException>(() => context.Get<TestService>());
        }

        private sealed class TestService { }
    }
}
