using TournamentAssistantShared.Models.Packets;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class GameOverDialog : UserControl
    {
        public List<Push.SongFinished> Results { get; set; }

        public GameOverDialog(List<Push.SongFinished> results)
        {
            Results = results.OrderByDescending(x => x.Score).ToList();

            DataContext = this;

            InitializeComponent();
        }

        private void Copy_Click(object _, RoutedEventArgs __)
        {
            var copyToClipboard = "RESULTS:\n";

            var index = 1;
            foreach (var result in Results) copyToClipboard += $"{index++}: {result.Player.Name} - {result.Score}\n";

            Clipboard.SetText(copyToClipboard);
        }
    }
}
