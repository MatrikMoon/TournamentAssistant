using System.Windows.Controls;
using BattleSaberShared.Models;

namespace BattleSaberUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class PlayerDialog : UserControl
    {
        public Player Player { get; set; }

        public PlayerDialog(Player player)
        {
            Player = player;

            DataContext = this;

            InitializeComponent();
        }
    }
}
