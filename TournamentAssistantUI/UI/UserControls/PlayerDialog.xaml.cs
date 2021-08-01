using System.Windows.Controls;
using System.Windows.Input;
using TournamentAssistantShared.Models;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class PlayerDialog : UserControl
    {
        public Player Player { get; set; }

        public ICommand KickPlayer { get; set; }

        public PlayerDialog(Player player, ICommand kickPlayer)
        {
            Player = player;
            KickPlayer = kickPlayer;

            DataContext = this;

            InitializeComponent();
        }
    }
}
