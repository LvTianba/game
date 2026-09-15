using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BorderValley.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class SaveServiceTests : IDisposable
    {
        private string root;

        [SetUp]
        public void SetUp() =>
            root = Path.Combine(Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() => DeleteRoot();

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
        public void Load_WhenPrimaryCorrupt_PrefersNewerBackupOverOlderPrevious()
        {
            var participant = new FakeParticipant { Value = 1 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Oldest");
            var primary = service.GetPrimaryPathForTests(0);
            var oldestFixture = primary + ".fixture";
            File.Copy(primary, oldestFixture, true);

            participant.Value = 2;
            service.Save(0, "NewerBackup");
            File.Copy(primary, primary + ".bak", true);
            File.Copy(oldestFixture, primary + ".previous", true);
            File.WriteAllText(primary, "{broken");

            participant.Reset();
            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(2));
            Assert.That(participant.CurrentScene, Is.EqualTo("NewerBackup"));
        }

        [Test]
        public void Load_WhenPrimaryAndPreviousExist_UsesPrimary()
        {
            var participant = new FakeParticipant { Value = 1 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Old");
            var primary = service.GetPrimaryPathForTests(0);
            var oldFixture = primary + ".fixture";
            File.Copy(primary, oldFixture, true);

            participant.Value = 2;
            service.Save(0, "New");
            File.Copy(oldFixture, primary + ".previous", true);

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(2));
            Assert.That(participant.CurrentScene, Is.EqualTo("New"));
        }

        [Test]
        public void Load_WhenOnlyBackupPreviousSnapshotExists_UsesIt()
        {
            var participant = new FakeParticipant { Value = 9 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Recovery");
            var primary = service.GetPrimaryPathForTests(0);
            File.Move(primary, primary + ".bak.previous");

            participant.Reset();
            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(9));
            Assert.That(participant.CurrentScene, Is.EqualTo("Recovery"));
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
        public void Save_WhenInvalidPrimaryAndPreviousCommitFails_PreservesPreviousRecovery()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Recovery");
            var primary = service.GetPrimaryPathForTests(0);
            var previous = primary + ".previous";
            var temporary = primary + ".tmp";
            File.Move(primary, previous);
            File.WriteAllText(primary, "{broken");

            var operations = new FaultInjectingSaveCommitOperations
            {
                MoveFault = (source, destination, _) =>
                    string.Equals(source, temporary, StringComparison.Ordinal) &&
                    string.Equals(destination, primary, StringComparison.Ordinal)
                        ? new IOException("forced invalid-primary commit failure")
                        : null
            };

            participant.Value = 22;
            var faultedService = new SaveService(root, new[] { participant }, operations);
            Assert.Throws<IOException>(() => faultedService.Save(0, "Failed"));

            participant.Reset();
            Assert.That(faultedService.HasSave(0), Is.True);
            Assert.That(faultedService.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("Recovery"));
        }

        [Test]
        public void Save_WhenInvalidPrimaryWithoutPrevious_CommitsNewSave()
        {
            var participant = new FakeParticipant { Value = 5 };
            var service = new SaveService(root, new[] { participant });
            var primary = service.GetPrimaryPathForTests(0);
            File.WriteAllText(primary, "{broken");

            participant.Value = 6;
            service.Save(0, "New");

            participant.Reset();
            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(6));
            Assert.That(participant.CurrentScene, Is.EqualTo("New"));
        }

        [Test]
        public void Save_WhenInvalidPrimaryWithPrevious_CommitsAndCleansRecovery()
        {
            var participant = new FakeParticipant { Value = 5 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Old");
            var primary = service.GetPrimaryPathForTests(0);
            var previous = primary + ".previous";
            File.Move(primary, previous);
            File.WriteAllText(primary, "{broken");

            participant.Value = 6;
            service.Save(0, "New");

            Assert.That(File.Exists(previous), Is.False);
            Assert.That(
                Directory.GetFiles(root, Path.GetFileName(previous) + ".stale.*"),
                Is.Empty);
            participant.Reset();
            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(6));
            Assert.That(participant.CurrentScene, Is.EqualTo("New"));
        }

        [Test]
        public void Save_FallbackCopyInterrupted_PreservesPrimaryAndBackupAndLoadsOldVersions()
        {
            var participant = new FakeParticipant { Value = 1 };
            var setup = new SaveService(root, new[] { participant });
            setup.Save(0, "First");
            participant.Value = 2;
            setup.Save(0, "Second");

            var primary = setup.GetPrimaryPathForTests(0);
            var backup = primary + ".bak";
            var operations = new FaultInjectingSaveCommitOperations
            {
                ReplaceFault = (_, _, _) => new UnauthorizedAccessException("forced File.Replace failure")
            };
            operations.CopyFault = (_, destination, _) =>
            {
                if (!string.Equals(destination, backup, StringComparison.Ordinal) &&
                    !string.Equals(destination, backup + ".tmp", StringComparison.Ordinal))
                    return null;
                File.WriteAllText(destination, "{partial");
                return new IOException("forced copy interruption");
            };

            participant.Value = 3;
            var service = new SaveService(root, new[] { participant }, operations);
            Assert.Throws<IOException>(() => service.Save(0, "Third"));
            AssertNoCommitTemps(primary, backup);
            AssertPrimaryAndBackup(service, participant, 2, "Second", 1, "First");
        }

        [Test]
        public void Save_FallbackBackupReplaceFails_PreservesPrimaryAndBackupAndLoadsOldVersions()
        {
            var participant = new FakeParticipant { Value = 1 };
            var setup = new SaveService(root, new[] { participant });
            setup.Save(0, "First");
            participant.Value = 2;
            setup.Save(0, "Second");

            var primary = setup.GetPrimaryPathForTests(0);
            var backup = primary + ".bak";
            var backupMoveAttempts = 0;
            var operations = new FaultInjectingSaveCommitOperations
            {
                ReplaceFault = (_, _, _) => new UnauthorizedAccessException("forced File.Replace failure"),
                MoveFault = (_, destination, _) =>
                {
                    if (!string.Equals(destination, backup, StringComparison.Ordinal) ||
                        backupMoveAttempts++ > 0)
                        return null;
                    return new IOException("forced backup replacement failure");
                }
            };

            participant.Value = 3;
            var service = new SaveService(root, new[] { participant }, operations);
            Assert.Throws<IOException>(() => service.Save(0, "Third"));
            AssertNoCommitTemps(primary, backup);
            AssertPrimaryAndBackup(service, participant, 2, "Second", 1, "First");
        }

        [Test]
        public void Save_FallbackPrimaryReplaceFails_PreservesPrimaryAndBackupAndLoadsOldVersions()
        {
            var participant = new FakeParticipant { Value = 1 };
            var setup = new SaveService(root, new[] { participant });
            setup.Save(0, "First");
            participant.Value = 2;
            setup.Save(0, "Second");

            var primary = setup.GetPrimaryPathForTests(0);
            var backup = primary + ".bak";
            var temporary = primary + ".tmp";
            var operations = new FaultInjectingSaveCommitOperations
            {
                ReplaceFault = (_, _, _) => new UnauthorizedAccessException("forced File.Replace failure"),
                MoveFault = (source, destination, _) =>
                    string.Equals(source, temporary, StringComparison.Ordinal) &&
                    string.Equals(destination, primary, StringComparison.Ordinal)
                        ? new IOException("forced primary replacement failure")
                        : null
            };

            participant.Value = 3;
            var service = new SaveService(root, new[] { participant }, operations);
            Assert.Throws<IOException>(() => service.Save(0, "Third"));
            AssertNoCommitTemps(primary, backup);
            AssertPrimaryAndBackup(service, participant, 2, "Second", 1, "First");
        }

        [Test]
        public void Save_WhenPreviousCleanupDeleteFails_StillCommitsNewPrimary()
        {
            var participant = new FakeParticipant { Value = 1 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "First");
            var primary = service.GetPrimaryPathForTests(0);
            var previousPrimary = primary + ".previous";
            var previousBackup = primary + ".bak.previous";
            File.WriteAllText(previousPrimary, "{stale-primary");
            File.WriteAllText(previousBackup, "{stale-backup");

            var operations = new FaultInjectingSaveCommitOperations
            {
                DeleteFault = path =>
                    string.Equals(path, previousPrimary, StringComparison.Ordinal) ||
                    string.Equals(path, previousBackup, StringComparison.Ordinal)
                        ? new IOException("forced delete failure")
                        : null
            };

            participant.Value = 2;
            var faultedService = new SaveService(root, new[] { participant }, operations);
            Assert.DoesNotThrow(() => faultedService.Save(0, "Second"));
            Assert.That(File.Exists(previousPrimary), Is.False);
            Assert.That(File.Exists(previousBackup), Is.False);

            participant.Reset();
            Assert.That(faultedService.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(2));
            Assert.That(participant.CurrentScene, Is.EqualTo("Second"));
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
        public void HasSave_WhenOnlyPreviousSnapshotExists_ReturnsTrue()
        {
            var participant = new FakeParticipant { Value = 7 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            var primary = service.GetPrimaryPathForTests(0);
            File.Move(primary, primary + ".previous");

            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(7));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [TestCase(".previous")]
        [TestCase(".bak.previous")]
        public void Save_WhenOnlyRecoverySnapshotExists_AndPreCommitFails_PreservesRecovery(
            string recoverySuffix)
        {
            var participant = new FakeParticipant { Value = 7 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "Recovery");
            var primary = service.GetPrimaryPathForTests(0);
            File.Move(primary, primary + recoverySuffix);

            participant.Value = 8;
            participant.CaptureException = new IOException("forced capture failure");

            Assert.Throws<IOException>(() => service.Save(0, "FailedSave"));
            Assert.That(service.HasSave(0), Is.True);

            participant.CaptureException = null;
            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(7));
            Assert.That(participant.CurrentScene, Is.EqualTo("Recovery"));
        }

        [Test]
        public void Save_WhenPreviousDeleteAndQuarantineFail_PreservesRecovery()
        {
            var participant = new FakeParticipant { Value = 1 };
            var setup = new SaveService(root, new[] { participant });
            setup.Save(0, "First");
            participant.Value = 2;
            setup.Save(0, "Second");

            var primary = setup.GetPrimaryPathForTests(0);
            var backup = primary + ".bak";
            var previousPrimary = primary + ".previous";
            File.Copy(backup, previousPrimary, true);
            var primaryBefore = File.ReadAllBytes(primary);
            var backupBefore = File.ReadAllBytes(backup);
            var previousBefore = File.ReadAllBytes(previousPrimary);
            var backupMoved = false;
            var operations = new FaultInjectingSaveCommitOperations
            {
                ReplaceFault = (_, _, _) => new UnauthorizedAccessException("forced File.Replace failure"),
                DeleteFault = path => string.Equals(path, previousPrimary, StringComparison.Ordinal)
                    ? new IOException("forced delete failure")
                    : null,
                MoveFault = (source, _, _) =>
                {
                    if (string.Equals(source, previousPrimary, StringComparison.Ordinal))
                        return new IOException("forced quarantine failure");
                    if (string.Equals(source, backup, StringComparison.Ordinal))
                        backupMoved = true;
                    return null;
                }
            };

            participant.Value = 3;
            var service = new SaveService(root, new[] { participant }, operations);
            Assert.Throws<IOException>(() => service.Save(0, "Third"));
            Assert.That(backupMoved, Is.False);
            CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primary));
            CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backup));
            CollectionAssert.AreEqual(previousBefore, File.ReadAllBytes(previousPrimary));

            participant.Reset();
            Assert.That(service.HasSave(0), Is.True);
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(2));
            Assert.That(participant.CurrentScene, Is.EqualTo("Second"));
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

        [Test]
        public void Load_WhenSchemaOneSaveMissesNarrative_MigratesDefaultNarrativeAndKeepsLegacyParticipants()
        {
            var inventory = new FakeParticipant { ParticipantKey = "inventory", Value = 41 };
            var party = new FakeParticipant { ParticipantKey = "party", Value = 42 };
            var narrative = new FakeParticipant { ParticipantKey = "narrative" };
            var service = new SaveService(root, new[] { inventory, party, narrative });
            WriteLegacySchemaOneSave(
                service,
                "World",
                new JObject
                {
                    ["inventory"] = new JObject { ["value"] = 7 },
                    ["party"] = new JObject { ["value"] = 9 }
                });
            inventory.Value = 100;
            party.Value = 200;
            narrative.Value = 300;

            Assert.That(service.Load(0), Is.True);

            Assert.That(inventory.Value, Is.EqualTo(7));
            Assert.That(party.Value, Is.EqualTo(9));
            Assert.That(narrative.Value, Is.EqualTo(0));
            Assert.That(inventory.CurrentScene, Is.EqualTo("World"));
            Assert.That(party.CurrentScene, Is.EqualTo("World"));
            Assert.That(narrative.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Load_WhenSchemaOneMigrationRestoreFails_RollsBackAllParticipants()
        {
            var inventory = new FakeParticipant { ParticipantKey = "inventory", Value = 5 };
            var narrative = new FakeParticipant { ParticipantKey = "narrative" };
            var service = new SaveService(root, new[] { inventory, narrative });
            WriteLegacySchemaOneSave(
                service,
                "World",
                new JObject { ["inventory"] = new JObject { ["value"] = 8 } });
            inventory.Value = 77;
            narrative.Value = 88;
            inventory.RejectValue = 8;

            Assert.That(service.Load(0), Is.False);

            Assert.That(inventory.Value, Is.EqualTo(77));
            Assert.That(narrative.Value, Is.EqualTo(88));

            inventory.RejectValue = 0;
            Assert.That(service.Load(0), Is.True);
            Assert.That(inventory.Value, Is.EqualTo(8));
            Assert.That(narrative.Value, Is.EqualTo(0));
        }

        [Test]
        public void Load_WhenSchemaIsNewerThanSupported_ReturnsFalse()
        {
            var participant = new FakeParticipant { Value = 5 };
            var service = new SaveService(root, new[] { participant });
            WriteLegacySave(
                service,
                SaveGameData.CurrentSchemaVersion + 1,
                "World",
                new JObject { ["fake"] = new JObject { ["value"] = 5 } });
            participant.Value = 77;

            Assert.That(service.Load(0), Is.False);
            Assert.That(participant.Value, Is.EqualTo(77));
        }

        private void WriteLegacySchemaOneSave(SaveService service, string sceneName, JObject participants) =>
            WriteLegacySave(service, 1, sceneName, participants);

        private static void AssertPrimaryAndBackup(
            SaveService service,
            FakeParticipant participant,
            int primaryValue,
            string primaryScene,
            int backupValue,
            string backupScene)
        {
            var primary = service.GetPrimaryPathForTests(0);
            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(primaryValue));
            Assert.That(participant.CurrentScene, Is.EqualTo(primaryScene));

            File.WriteAllText(primary, "{broken");
            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(backupValue));
            Assert.That(participant.CurrentScene, Is.EqualTo(backupScene));
        }

        private static void AssertNoCommitTemps(string primary, string backup)
        {
            Assert.That(File.Exists(primary + ".tmp"), Is.False);
            Assert.That(File.Exists(primary + ".previous"), Is.False);
            Assert.That(File.Exists(backup + ".tmp"), Is.False);
            Assert.That(File.Exists(backup + ".previous"), Is.False);
        }

        private static void WriteLegacySave(
            SaveService service,
            int schemaVersion,
            string sceneName,
            JObject participants)
        {
            var payload = new JObject
            {
                ["SchemaVersion"] = schemaVersion,
                ["SceneName"] = sceneName,
                ["SavedAtUtc"] = DateTime.UtcNow,
                ["Participants"] = participants
            }.ToString(Formatting.Indented);
            var envelope = new JObject
            {
                ["payload"] = payload,
                ["checksum"] = ComputeChecksum(payload)
            };
            File.WriteAllText(
                service.GetPrimaryPathForTests(0),
                envelope.ToString(Formatting.None),
                new UTF8Encoding(false));
        }

        private static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty);
        }

        public void Dispose()
        {
            DeleteRoot();
        }

        private void DeleteRoot()
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private sealed class FakeParticipant : ISaveParticipant
        {
            public string ParticipantKey { get; set; } = "fake";
            public string Key => ParticipantKey;
            public int Value { get; set; }
            public int RejectValue { get; set; } = int.MinValue;
            public Exception CaptureException { get; set; }
            public string CurrentScene { get; private set; } = string.Empty;
            public JObject Capture()
            {
                if (CaptureException != null)
                    throw CaptureException;
                return new JObject { ["value"] = Value };
            }
            public void Restore(JObject state)
            {
                Value = state.Value<int>("value");
                if (Value == RejectValue)
                    throw new InvalidOperationException("Rejected semantic payload.");
            }
            public void RestoreContext(string sceneName) => CurrentScene = sceneName;
            public void Reset() { Value = 0; CurrentScene = string.Empty; }
        }

        private sealed class FaultInjectingSaveCommitOperations : ISaveFileCommitOperations
        {
            private readonly ISaveFileCommitOperations inner = new SystemSaveFileCommitOperations();

            public Func<string, string, bool, Exception> CopyFault { get; set; }
            public Func<string, Exception> DeleteFault { get; set; }
            public Func<string, string, bool, Exception> MoveFault { get; set; }
            public Func<string, string, string, Exception> ReplaceFault { get; set; }

            public void Copy(string source, string destination, bool overwrite)
            {
                ThrowIfFaulted(CopyFault, source, destination, overwrite);
                inner.Copy(source, destination, overwrite);
            }

            public void Delete(string path)
            {
                var exception = DeleteFault?.Invoke(path);
                if (exception != null) throw exception;
                inner.Delete(path);
            }

            public void Move(string source, string destination, bool overwrite)
            {
                ThrowIfFaulted(MoveFault, source, destination, overwrite);
                inner.Move(source, destination, overwrite);
            }

            public void Replace(string source, string destination, string backup)
            {
                ThrowIfFaulted(ReplaceFault, source, destination, backup);
                inner.Replace(source, destination, backup);
            }

            private static void ThrowIfFaulted(
                Func<string, string, bool, Exception> fault,
                string source,
                string destination,
                bool overwrite)
            {
                var exception = fault?.Invoke(source, destination, overwrite);
                if (exception != null) throw exception;
            }

            private static void ThrowIfFaulted(
                Func<string, string, string, Exception> fault,
                string source,
                string destination,
                string backup)
            {
                var exception = fault?.Invoke(source, destination, backup);
                if (exception != null) throw exception;
            }
        }
    }
}
