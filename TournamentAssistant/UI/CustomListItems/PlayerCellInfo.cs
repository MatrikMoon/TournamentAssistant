using TournamentAssistantShared.Models;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.UI.CustomListItems
{
    public class PlayerCellInfo : CustomCellInfo
    {
        public Player Player { get; set; }

        public PlayerCellInfo(Player player) : base(GetTitleFromMatch(player))
        {
            Player = player;
        }

        private static string GetTitleFromMatch(Player player)
        {
            return $"{player.Name}";
        }
    }
}
