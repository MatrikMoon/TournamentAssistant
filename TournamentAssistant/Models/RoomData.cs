using TournamentAssistantShared.Models;

namespace TournamentAssistant.Models
{
    public class RoomData
    {
        public readonly Match match;
        public readonly bool tournamentMode;

        public RoomData(Match match, bool tournamentMode)
        {
            this.match = match;
            this.tournamentMode = tournamentMode;
        }
    }
}
