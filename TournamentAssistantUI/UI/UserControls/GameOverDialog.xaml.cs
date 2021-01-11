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
        public List<SongFinished> Results { get; set; }

        public GameOverDialog(List<SongFinished> results)
        {
            Results = results.OrderByDescending(x => x.Score).ToList();

            DataContext = this;

            InitializeComponent();
        }

        private void Copy_Click(object _, RoutedEventArgs __)
        {
            var copyToClipboard = "RESULTS:\n";

            var index = 1;
            foreach (var result in Results)
            {
                copyToClipboard += $"{index}: {result.User.Name} - {result.Score}\n";
                index++;
            }

            Clipboard.SetText(copyToClipboard);
        }
    }
}
