using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

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
            public List<(User, int)> Players { get; set; }
            public int TotalScore { get; set; } = 0;
            public int TotalMisses { get;set; } = 0;
            public int TotalBadCuts { get; set; } = 0;
            public int TotalGoodCuts { get; set; } = 0;
            public int TotalNonGoodCuts { get; set; } = 0;
            public string IndividualScores
            {
                get
                {
                    var rankIndex = 1;
                    var totalScoreText = string.Empty;
                    Players.OrderByDescending(x => x.Item2).ToList().ForEach(x => totalScoreText += $"{rankIndex++}: {x.Item1.Name} - {x.Item2}\n");
                    return totalScoreText;
                }
            }
        }

        public List<TeamResult> TeamResults { get; set; }

        public GameOverDialogTeams(List<Push.SongFinished> results)
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
                        Players = new List<(User, int)>()
                    };
                    TeamResults.Add(teamResult);
                }

                teamResult.Players.Add((x.Player, x.Score));
                teamResult.TotalScore += x.Score;
                teamResult.TotalMisses += x.Misses;
                teamResult.TotalBadCuts += x.BadCuts;
                teamResult.TotalGoodCuts += x.GoodCuts;
                teamResult.TotalNonGoodCuts += x.BadCuts + x.Misses;
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
                    copyToClipboard += $"\t\t{player.Item1.Name} - {player.Item2}\n";
                }
                copyToClipboard += "\n";
            }

            Clipboard.SetText(copyToClipboard);
        }
    }
}
