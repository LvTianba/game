using System.Collections.Generic;
using BorderValley.Core.Persistence;

namespace BorderValley.Core.Boot
{
    public interface IGameServiceInstaller
    {
        void Install(GameContext context, ICollection<ISaveParticipant> participants);
    }
}
