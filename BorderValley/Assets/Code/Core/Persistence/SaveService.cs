using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
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

        public SaveService(string root, IEnumerable<ISaveParticipant> participants)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.participants = participants.ToDictionary(item => item.Key, StringComparer.Ordinal);
            Directory.CreateDirectory(root);
        }

        public bool HasSave(int slot) => File.Exists(GetPrimaryPath(slot));
        public string GetPrimaryPathForTests(int slot) => GetPrimaryPath(slot);

        public void Save(int slot, string sceneName)
        {
            var primary = GetPrimaryPath(slot);
            var temporary = primary + ".tmp";
            var backup = primary + ".bak";

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
                        File.Move(temporary, primary);
                        break;
                    case SaveFileState.Valid:
                        if (FilesHaveSamePayload(temporary, primary))
                        {
                            TryDeleteFile(temporary);
                            break;
                        }
                        File.Replace(temporary, primary, backup);
                        break;
                    case SaveFileState.Invalid:
                        File.Delete(primary);
                        File.Move(temporary, primary);
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
        }

        public bool Load(int slot)
        {
            var primary = GetPrimaryPath(slot);
            return TryRestoreFile(primary) || TryRestoreFile(primary + ".bak");
        }

        public void Delete(int slot)
        {
            var primary = GetPrimaryPath(slot);
            foreach (var path in new[]
            {
                primary,
                primary + ".bak",
                primary + ".tmp",
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
            if (data == null || data.SchemaVersion != SaveGameData.CurrentSchemaVersion) return null;
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

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }
    }
}