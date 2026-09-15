using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("BorderValley.EditModeTests")]

namespace BorderValley.Core.Persistence
{
    internal interface ISaveFileCommitOperations
    {
        void Copy(string source, string destination, bool overwrite);
        void Delete(string path);
        void Move(string source, string destination, bool overwrite);
        void Replace(string source, string destination, string backup);
    }

    internal sealed class SystemSaveFileCommitOperations : ISaveFileCommitOperations
    {
        private const uint MoveFileReplaceExisting = 0x1;
        private const uint MoveFileWriteThrough = 0x8;

        public void Copy(string source, string destination, bool overwrite) =>
            File.Copy(source, destination, overwrite);

        public void Delete(string path) => File.Delete(path);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public void Move(string source, string destination, bool overwrite)
        {
            if (!overwrite)
            {
                File.Move(source, destination);
                return;
            }

            if (MoveFileEx(
                    source,
                    destination,
                    MoveFileReplaceExisting | MoveFileWriteThrough))
            {
                return;
            }

            throw new IOException(
                "MoveFileEx failed with Win32 error " + Marshal.GetLastWin32Error() + ".");
        }
#else
        public void Move(string source, string destination, bool overwrite)
        {
            if (!overwrite || !File.Exists(destination))
            {
                File.Move(source, destination);
                return;
            }

            File.Replace(source, destination, null);
        }
#endif

        public void Replace(string source, string destination, string backup) =>
            File.Replace(source, destination, backup);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(
            string existingFileName,
            string newFileName,
            uint flags);
#endif
    }

    public sealed class SaveService
    {
        private enum SaveFileState
        {
            Missing,
            Valid,
            Invalid,
            Unreadable
        }

        private readonly string root;
        private readonly IReadOnlyDictionary<string, ISaveParticipant> participants;
        private readonly ISaveFileCommitOperations commitOperations;

        public SaveService(string root, IEnumerable<ISaveParticipant> participants)
            : this(root, participants, new SystemSaveFileCommitOperations())
        {
        }

        internal SaveService(
            string root,
            IEnumerable<ISaveParticipant> participants,
            ISaveFileCommitOperations commitOperations)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.participants = participants.ToDictionary(item => item.Key, StringComparer.Ordinal);
            this.commitOperations = commitOperations ?? throw new ArgumentNullException(nameof(commitOperations));
            Directory.CreateDirectory(root);
        }

        public bool HasSave(int slot)
        {
            foreach (var path in GetRecoveryPaths(slot))
            {
                if (File.Exists(path))
                    return true;
            }

            return false;
        }
        public string GetPrimaryPathForTests(int slot) => GetPrimaryPath(slot);

        public void Save(int slot, string sceneName)
        {
            var primary = GetPrimaryPath(slot);
            var temporary = primary + ".tmp";
            var backup = primary + ".bak";

            CleanupPreviousSnapshots(primary, backup);
            TryDeleteFile(temporary);

            try
            {
                var envelopeJson = BuildEnvelopeJson(sceneName);
                File.WriteAllText(temporary, envelopeJson, new UTF8Encoding(false));

                var temporaryState = InspectSaveFile(temporary, out var temporaryReadException);
                if (temporaryState != SaveFileState.Valid)
                {
                    if (temporaryState == SaveFileState.Unreadable && temporaryReadException != null)
                        throw temporaryReadException;
                    throw new IOException("Temporary save failed validation.");
                }
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }

            try
            {
                var primaryState = InspectSaveFile(primary, out var primaryReadException);
                switch (primaryState)
                {
                    case SaveFileState.Missing:
                        commitOperations.Move(temporary, primary, false);
                        break;
                    case SaveFileState.Valid:
                        if (FilesHaveSamePayload(temporary, primary))
                        {
                            TryDeleteFile(temporary);
                            break;
                        }

                        try
                        {
                            commitOperations.Replace(temporary, primary, backup);
                        }
                        catch (Exception exception) when (
                            exception is UnauthorizedAccessException ||
                            exception is PlatformNotSupportedException ||
                            exception is IOException)
                        {
                            ReplaceWithValidatedBackup(temporary, primary, backup);
                        }
                        break;
                    case SaveFileState.Invalid:
                        ReplaceInvalidPrimary(temporary, primary);
                        break;
                    case SaveFileState.Unreadable:
                        throw primaryReadException ?? new IOException(
                            "Primary save exists but cannot be read; save aborted to protect it.");
                    default:
                        throw new IOException("Unexpected primary save state.");
                }
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }

            CleanupPreviousSnapshots(primary, backup);
        }

        public bool Load(int slot)
        {
            foreach (var path in GetRecoveryPaths(slot))
            {
                if (TryRestoreFile(path))
                    return true;
            }

            return false;
        }

        public void Delete(int slot)
        {
            var primary = GetPrimaryPath(slot);
            foreach (var path in new[]
            {
                primary,
                primary + ".bak",
                primary + ".bak.tmp",
                primary + ".bak.previous",
                primary + ".tmp",
                primary + ".previous",
                primary + ".sha256",
                primary + ".bak.sha256"
            })
            {
                TryDeleteFile(path);
            }
        }

        private string BuildEnvelopeJson(string sceneName)
        {
            var data = new SaveGameData { SceneName = sceneName };
            foreach (var participant in participants.Values)
                data.Participants[participant.Key] = participant.Capture();

            var payload = JsonConvert.SerializeObject(data, Formatting.Indented);
            var checksum = ComputeChecksum(payload);
            var envelope = new JObject
            {
                ["payload"] = payload,
                ["checksum"] = checksum
            };
            return envelope.ToString(Formatting.Indented);
        }

        private bool TryRestoreFile(string path)
        {
            try
            {
                var data = ReadValidSaveData(path);
                if (data == null) return false;

                var snapshots = participants.Values.ToDictionary(
                    participant => participant.Key,
                    participant => participant.Capture(),
                    StringComparer.Ordinal);

                try
                {
                    ApplySchemaMigration(data);
                    foreach (var participant in participants.Values)
                    {
                        participant.Reset();
                        if (!data.Participants.TryGetValue(participant.Key, out var state))
                            throw new InvalidOperationException(
                                "Save is missing participant: " + participant.Key);
                        participant.Restore(state);
                        participant.RestoreContext(data.SceneName);
                    }

                    foreach (var participant in participants.Values)
                    {
                        if (participant is ISaveParticipantPostRestore postRestore)
                            postRestore.CompleteRestore(participants);
                    }

                    return true;
                }
                catch (Exception)
                {
                    Rollback(snapshots);
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Rollback(IReadOnlyDictionary<string, JObject> snapshots)
        {
            try
            {
                foreach (var participant in participants.Values)
                {
                    participant.Reset();
                    if (snapshots.TryGetValue(participant.Key, out var state))
                        participant.Restore(state);
                    participant.RestoreContext(string.Empty);
                }
            }
            catch (Exception)
            {
                // The next load path will attempt a full reset/restore again.
            }
        }

        private void ApplySchemaMigration(SaveGameData data)
        {
            if (data.SchemaVersion >= SaveGameData.CurrentSchemaVersion)
                return;

            // Schema 1 predates the narrative participant. Participants absent from a legacy
            // payload keep their default (post-reset) state; existing participants are preserved.
            foreach (var participant in participants.Values)
            {
                if (data.Participants.ContainsKey(participant.Key))
                    continue;

                participant.Reset();
                data.Participants[participant.Key] = participant.Capture();
            }

            data.SchemaVersion = SaveGameData.CurrentSchemaVersion;
        }

        private SaveFileState InspectSaveFile(string path, out Exception readException)
        {
            readException = null;
            if (!File.Exists(path)) return SaveFileState.Missing;

            try
            {
                return ReadValidSaveData(path) != null ? SaveFileState.Valid : SaveFileState.Invalid;
            }
            catch (IOException exception)
            {
                readException = exception;
                return SaveFileState.Unreadable;
            }
            catch (UnauthorizedAccessException exception)
            {
                readException = exception;
                return SaveFileState.Unreadable;
            }
            catch (JsonException)
            {
                return SaveFileState.Invalid;
            }
        }

        private SaveGameData ReadValidSaveData(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var envelope = JObject.Parse(json);
            var payload = envelope.Value<string>("payload");
            var checksum = envelope.Value<string>("checksum");
            if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(checksum)) return null;
            if (!string.Equals(ComputeChecksum(payload), checksum, StringComparison.Ordinal)) return null;

            var data = JsonConvert.DeserializeObject<SaveGameData>(payload);
            if (data == null ||
                data.SchemaVersion < 1 ||
                data.SchemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                return null;
            }

            return data;
        }

        private string GetPrimaryPath(int slot)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(root, $"slot-{slot}.json");
        }

        private bool FilesHaveSamePayload(string left, string right)
        {
            var leftData = ReadValidSaveData(left);
            var rightData = ReadValidSaveData(right);
            return leftData != null &&
                   rightData != null &&
                   leftData.SchemaVersion == rightData.SchemaVersion &&
                   string.Equals(leftData.SceneName, rightData.SceneName, StringComparison.Ordinal) &&
                   JToken.DeepEquals(
                       JObject.FromObject(leftData.Participants),
                       JObject.FromObject(rightData.Participants));
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) commitOperations.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private IEnumerable<string> GetRecoveryPaths(int slot)
        {
            var primary = GetPrimaryPath(slot);
            yield return primary;
            yield return primary + ".bak";
            yield return primary + ".previous";
            yield return primary + ".bak.previous";
        }

        private void CleanupPreviousSnapshots(string primary, string backup)
        {
            CleanupPreviousSnapshot(primary + ".previous");
            CleanupPreviousSnapshot(backup + ".previous");
        }

        private void CleanupPreviousSnapshot(string path)
        {
            TryDeleteFile(path);
            if (!File.Exists(path))
                return;

            var quarantine = path + ".stale." + Guid.NewGuid().ToString("N");
            try
            {
                commitOperations.Move(path, quarantine, false);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            TryDeleteFile(quarantine);
        }

        private void ReplaceWithValidatedBackup(
            string temporary,
            string primary,
            string backup)
        {
            // EFS can reject replace-existing APIs, so every rename below targets a
            // vacant path and leaves a complete previous snapshot for recovery.
            var backupTemporary = backup + ".tmp";
            var previousBackup = backup + ".previous";
            var previousPrimary = primary + ".previous";
            var hadBackup = File.Exists(backup);
            var previousBackupMoved = false;
            var backupUpdated = false;
            var primaryMoved = false;

            TryDeleteFile(backupTemporary);

            try
            {
                commitOperations.Copy(primary, backupTemporary, false);
                var backupState = InspectSaveFile(backupTemporary, out var backupReadException);
                if (backupState != SaveFileState.Valid)
                {
                    if (backupState == SaveFileState.Unreadable && backupReadException != null)
                        throw backupReadException;
                    throw new IOException("Backup copy failed validation.");
                }

                if (hadBackup)
                {
                    commitOperations.Move(backup, previousBackup, false);
                    previousBackupMoved = true;
                }

                commitOperations.Move(backupTemporary, backup, false);
                backupUpdated = true;

                commitOperations.Move(primary, previousPrimary, false);
                primaryMoved = true;
                commitOperations.Move(temporary, primary, false);

                TryDeleteFile(previousBackup);
                TryDeleteFile(previousPrimary);
            }
            catch
            {
                if (primaryMoved)
                    TryMove(previousPrimary, primary);

                if (backupUpdated)
                {
                    TryDeleteFile(backup);
                    if (previousBackupMoved)
                        TryMove(previousBackup, backup);
                }
                else if (previousBackupMoved)
                    TryMove(previousBackup, backup);
                throw;
            }
            finally
            {
                TryDeleteFile(backupTemporary);
            }
        }

        private void TryMove(string source, string destination)
        {
            try
            {
                if (File.Exists(source))
                    commitOperations.Move(source, destination, false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void ReplaceInvalidPrimary(string temporary, string primary)
        {
            var previousPrimary = primary + ".previous";
            TryDeleteFile(previousPrimary);
            commitOperations.Move(primary, previousPrimary, false);
            try
            {
                commitOperations.Move(temporary, primary, false);
            }
            catch
            {
                TryMove(previousPrimary, primary);
                throw;
            }

            TryDeleteFile(previousPrimary);
        }

        private static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }
    }
}
