using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 2;
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string SceneName { get; set; } = string.Empty;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public Dictionary<string, JObject> Participants { get; set; } = new();
    }
}
