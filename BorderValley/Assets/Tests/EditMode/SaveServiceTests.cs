using System;
using System.IO;
using BorderValley.Core.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class SaveServiceTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));

        [Test]
        public void SaveThenLoad_RestoresParticipant()
        {
            var participant = new FakeParticipant { Value = 7 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Reset();
            service.Load(0);
            Assert.That(participant.Value, Is.EqualTo(7));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Load_WhenPrimaryIsCorrupt_UsesBackup()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Value = 22;
            service.Save(0, "Forest");
            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");
            participant.Reset();
            service.Load(0);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Load_WhenPrimaryPayloadFailsSemantically_RollsBackAndUsesBackup()
        {
            var first = new FakeParticipant { ParticipantKey = "first", Value = 11 };
            var second = new FakeParticipant { ParticipantKey = "second", Value = 110 };
            var service = new SaveService(root, new[] { first, second });
            service.Save(0, "First");
            first.Value = 22;
            second.Value = 220;
            service.Save(0, "Second");
            first.Value = 33;
            second.Value = 330;
            service.Save(0, "Third");

            first.RejectValue = 33;
            first.Value = 99;
            second.Value = 990;

            Assert.That(service.Load(0), Is.True);
            Assert.That(first.Value, Is.EqualTo(22));
            Assert.That(second.Value, Is.EqualTo(220));
            Assert.That(first.CurrentScene, Is.EqualTo("Second"));
            Assert.That(second.CurrentScene, Is.EqualTo("Second"));
        }

        [Test]
        public void Save_WhenPayloadUnchanged_PreservesExistingBackup()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Value = 22;
            service.Save(0, "Forest");
            service.Save(0, "Forest");
            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WhenPrimaryIsCorrupt_PreservesExistingBackup()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Value = 22;
            service.Save(0, "Forest");
            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");

            participant.Value = 33;
            service.Save(0, "Cave");

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(33));
            Assert.That(participant.CurrentScene, Is.EqualTo("Cave"));

            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");
            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WritesSinglePrimaryFile_WithoutSidecar()
        {
            var participant = new FakeParticipant { Value = 5 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");

            var primary = service.GetPrimaryPathForTests(0);
            Assert.That(File.Exists(primary), Is.True);
            Assert.That(File.Exists(primary + ".sha256"), Is.False);
            Assert.That(File.Exists(primary + ".tmp"), Is.False);
            Assert.That(Directory.GetFiles(root, "*.sha256"), Is.Empty);
        }

        [Test]
        public void Save_CleansStaleTempFile()
        {
            var participant = new FakeParticipant { Value = 6 };
            var service = new SaveService(root, new[] { participant });
            var primary = service.GetPrimaryPathForTests(0);
            File.WriteAllText(primary + ".tmp", "{stale");

            service.Save(0, "World");

            Assert.That(File.Exists(primary), Is.True);
            Assert.That(File.Exists(primary + ".tmp"), Is.False);

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(6));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WhenPrimaryIsLocked_ThrowsAndPreservesSave()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(1, "World");

            var primary = service.GetPrimaryPathForTests(1);
            using (var lockStream = new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                participant.Value = 22;
                Assert.Throws<IOException>(() => service.Save(1, "Forest"));
            }

            Assert.That(File.Exists(primary + ".tmp"), Is.False);
            Assert.That(File.Exists(primary + ".bak"), Is.False);
            Assert.That(service.Load(1), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private sealed class FakeParticipant : ISaveParticipant
        {
            public string ParticipantKey { get; set; } = "fake";
            public string Key => ParticipantKey;
            public int Value { get; set; }
            public int RejectValue { get; set; }
            public string CurrentScene { get; private set; } = string.Empty;
            public JObject Capture() => new JObject { ["value"] = Value };
            public void Restore(JObject state)
            {
                Value = state.Value<int>("value");
                if (Value == RejectValue)
                    throw new InvalidOperationException("Rejected semantic payload.");
            }
            public void RestoreContext(string sceneName) => CurrentScene = sceneName;
            public void Reset() { Value = 0; CurrentScene = string.Empty; }
        }
    }
}
