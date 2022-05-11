using TournamentAssistantShared.Models;
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
    public partial class GameOverDialogTeams : UserControl
    {
        public class TeamResult
        {
            public Team Team { get; set; }
            public List<Player> Players { get; set; }
            public int TotalScore { get; set; } = 0;
            public string IndividualScores
            {
                get
                {
                    var rankIndex = 1;
                    var totalScoreText = string.Empty;
                    Players.OrderByDescending(x => x.Score).ToList().ForEach(x => totalScoreText += $"{rankIndex++}: {x.User.Name} - {x.Score}\n");
                    return totalScoreText;
                }
            }
        }

        public List<TeamResult> TeamResults { get; set; }

        public GameOverDialogTeams(List<SongFinished> results)
        {
            TeamResults = new List<TeamResult>();

            results.ForEach(x =>
            {
                var teamResult = TeamResults.FirstOrDefault(y => y.Team.Id == x.Player.Team.Id);

                //If there's no team in the results list for the current player
                if (teamResult == null)
                {
                    teamResult = new TeamResult()
                    {
                        Team = x.Player.Team,
                        Players = new List<Player>()
                    };
                    TeamResults.Add(teamResult);
                }

                x.Player.Score = x.Score;
                teamResult.Players.Add(x.Player);
                teamResult.TotalScore += x.Score;
            });

            TeamResults = TeamResults.OrderByDescending(x => x.TotalScore).ToList();

            DataContext = this;

            InitializeComponent();
        }

        private void Copy_Click(object _, RoutedEventArgs __)
        {
            var copyToClipboard = "RESULTS:\n";
            var index = 1;

            foreach (var result in TeamResults)
            {
                copyToClipboard += $"{index}: {result.Team.Name} - {result.TotalScore}\n";
                foreach (var player in result.Players)
                {
                    copyToClipboard += $"\t\t{player.User.Name} - {player.Score}\n";
                }
                copyToClipboard += "\n";
            }

            Clipboard.SetText(copyToClipboard);
        }
    }
}
