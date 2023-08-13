using System.Linq;
using TournamentAssistantShared.Models;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.UI.CustomListItems
{
    public class MatchCellInfo : CustomCellInfo
    {
        public string TournamentId { get; set; }
        public Match Match { get; set; }

        public MatchCellInfo(string tournamentId, Match match) : base(GetTitleFromMatch(tournamentId, match))
        {
            TournamentId = tournamentId;
            Match = match;
        }

        private static string GetTitleFromMatch(string tournamentId, Match match)
        {
            /*string title = string.Empty;
            foreach (var player in match.Players) title += player.Name + " / ";
            return title.Substring(0, title.Length - 3);*/

            /*var leader = Plugin.client.StateManager.GetUser(tournamentId, match.Leader);
            var playersInMatch = match.AssociatedUsers.Where(x => Plugin.client.StateManager.GetUser(tournamentId, x).ClientType == User.ClientTypes.Player);*/

            return $"todo";
        }
    }
}
