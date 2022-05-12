using System.Windows.Controls;
using System.Windows.Input;
using TournamentAssistantShared.Models;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class UserDialog : UserControl
    {
        public User User { get; set; }

        public ICommand KickPlayer { get; set; }

        public UserDialog(User user, ICommand kickPlayer)
        {
            User = user;
            KickPlayer = kickPlayer;

            DataContext = this;

            InitializeComponent();
        }
    }
}
