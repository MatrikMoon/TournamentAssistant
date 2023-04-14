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
        public class SongFinishedWithDistanceFromFirstPlayer : Push.SongFinished
        {
            public int Distance { get; set; }
        }

        public List<SongFinishedWithDistanceFromFirstPlayer> Results { get; set; }

        public GameOverDialog(List<Push.SongFinished> results)
        {
            var orderedResults = results.OrderByDescending(x => x.Score);
            var firstPlace = orderedResults.First();

            Results = orderedResults.Select(x => new SongFinishedWithDistanceFromFirstPlayer
            {
                Beatmap = x.Beatmap,
                Player = x.Player,
                Score = x.Score,
                Type = x.Type,
                Distance = x.Score - firstPlace.Score,
            }).ToList();

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
