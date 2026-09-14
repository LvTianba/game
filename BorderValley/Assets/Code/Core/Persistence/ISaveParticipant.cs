using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
    public interface ISaveParticipant
    {
        string Key { get; }
        JObject Capture();
        void Restore(JObject state);
        void RestoreContext(string sceneName);
        void Reset();
    }

    public interface ISaveParticipantPostRestore
    {
        void CompleteRestore(IReadOnlyDictionary<string, ISaveParticipant> participants);
    }
}
