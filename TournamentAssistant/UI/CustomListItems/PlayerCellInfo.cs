using TournamentAssistantShared.Models;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace TournamentAssistant.UI.CustomListItems
{
    public class PlayerCellInfo : CustomCellInfo
    {
        public User User { get; set; }

        public PlayerCellInfo(User user) : base(GetInfoFromPlayer(user))
        {
            User = user;
        }

        private static string GetInfoFromPlayer(User user)
        {
            return $"{user.Name}\t{user.DownloadState}";
        }
    }
}
