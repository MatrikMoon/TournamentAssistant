using TournamentAssistantShared.Models.Packets;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using TournamentAssistantShared.Models;
using MessagingToolkit.Barcode;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class GameOverDialog : UserControl
    {
        public class SongFinishedWithDistanceFromFirstPlayer : Push.SongFinished
        {
            public int NonGoodCuts { get; set; }
            public int Distance { get; set; }
        }

        public List<SongFinishedWithDistanceFromFirstPlayer> Results { get; set; }
        private Dictionary<string, RealtimeScore> latestRealtimeScores { get; set; }

        public GameOverDialog(List<Push.SongFinished> results, Dictionary<string, RealtimeScore> latestScores)
        {
            latestRealtimeScores = latestScores ?? new Dictionary<string, RealtimeScore>();

            var orderedResults = results.OrderByDescending(x => x.Score);
            var firstPlace = orderedResults.First();

            Results = orderedResults.Select(x => {
                // latestRealtimeScores.TryGetValue(x.Player.Guid, out var lastRealtimeScore);
                return new SongFinishedWithDistanceFromFirstPlayer
                {
                    Beatmap = x.Beatmap,
                    Player = x.Player,
                    Score = x.Score,
                    Type = x.Type,
                    Distance = x.Score - firstPlace.Score,
                    Misses = x.Misses,
                    BadCuts = x.BadCuts,
                    GoodCuts = x.GoodCuts,
                    NonGoodCuts = x.Misses + x.BadCuts,
                    EndTime = x.EndTime
                };
            }).ToList();

            DataContext = this;

            InitializeComponent();
        }

        private void Copy_Click(object _, RoutedEventArgs __)
        {
            var copyToClipboard = "RESULTS:\n";

            var index = 1;
            foreach (var result in Results)
            {
                copyToClipboard += $"## {index++}: {result.Player.Name}\n";
                copyToClipboard += $" - Score: {result.Score}\n";
                copyToClipboard += $" - Misses: {result.Misses}\n";
                copyToClipboard += $" - Bad Cuts: {result.BadCuts}\n";
                copyToClipboard += $" - Good Cuts: {result.GoodCuts}\n";
                copyToClipboard += $" - Non-Good Cuts: {result.NonGoodCuts}\n\n";
            }

            Clipboard.SetText(copyToClipboard);
        }

        private void SortByMisses_Checked(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Results = Results.OrderByDescending(x => x.Misses).ToList();
                PlayerListBox.ItemsSource = Results;
            });
        }

        private void SortByMissesCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Results = Results.OrderByDescending(x => x.Score).ToList();
                PlayerListBox.ItemsSource = Results;
            });
        }
    }
}
