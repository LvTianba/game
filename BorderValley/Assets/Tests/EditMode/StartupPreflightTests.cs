using BorderValley.Core.Boot;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class StartupPreflightTests
    {
        [Test]
        public void ShouldContinue_FailedCheckInDebug_Blocks()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(false, "bad content") };
            Assert.That(StartupPreflight.ShouldContinue(checks, true, out var errors), Is.False);
            Assert.That(errors, Does.Contain("bad content"));
        }

        [Test]
        public void ShouldContinue_FailedCheckInProduction_ContinuesAndReports()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(false, "bad content") };
            Assert.That(StartupPreflight.ShouldContinue(checks, false, out var errors), Is.True);
            Assert.That(errors, Does.Contain("bad content"));
        }

        [Test]
        public void ShouldContinue_AllChecksPass_Continues()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(true, string.Empty) };
            Assert.That(StartupPreflight.ShouldContinue(checks, true, out var errors), Is.True);
            Assert.That(errors, Is.Empty);
        }

        private sealed class FakeCheck : IPreflightCheck
        {
            private readonly bool result;
            private readonly string error;
            public FakeCheck(bool result, string error) { this.result = result; this.error = error; }
            public bool Validate(out string error) { error = this.error; return result; }
        }
    }
}
