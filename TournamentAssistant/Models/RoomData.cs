using TournamentAssistantShared.Models;

namespace TournamentAssistant.Models
{
    public class RoomData
    {
        public readonly Match? match;
        public readonly bool tournamentMode;
        public readonly MatchOptions matchOptions;

        public RoomData(Match? match, MatchOptions matchOptions)
        {
            this.match = match;
            this.matchOptions = matchOptions;
        }
    }
}