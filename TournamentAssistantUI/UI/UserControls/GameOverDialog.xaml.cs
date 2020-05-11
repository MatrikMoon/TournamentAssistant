using TournamentAssistantShared.Models.Packets;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class GameOverDialog : UserControl
    {
        public List<SongFinished> Results { get; set; }

        public GameOverDialog(List<SongFinished> results)
        {
            Results = results.OrderByDescending(x => x.Score).ToList();

            DataContext = this;

            InitializeComponent();
        }
    }
}
