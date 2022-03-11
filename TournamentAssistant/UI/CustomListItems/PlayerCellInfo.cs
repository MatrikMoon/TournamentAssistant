using TournamentAssistantShared.Models;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.UI.CustomListItems
{
    public class PlayerCellInfo : CustomCellInfo
    {
        public Player Player { get; set; }

        public PlayerCellInfo(Player player) : base(GetInfoFromPlayer(player))
        {
            Player = player;
        }

        private static string GetInfoFromPlayer(Player player)
        {
            return $"{player.User.Name}\t{player.DownloadState}";
        }
    }
}
