using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

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
            public string IndividualScores {
                get
                {
                    var rankIndex = 1;
                    var totalScoreText = string.Empty;
                    Players.OrderByDescending(x => x.Score).ToList().ForEach(x => totalScoreText += $"{rankIndex++}: {x.Name} - {x.Score}\n");
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
                var teamResult = TeamResults.FirstOrDefault(y => y.Team.Guid == (x.User as Player).Team.Guid);

                //If there's no team in the results list for the current player
                if (teamResult == null) {
                    teamResult = new TeamResult()
                    {
                        Team = (x.User as Player).Team,
                        Players = new List<Player>()
                    };
                    TeamResults.Add(teamResult);
                }

                teamResult.Players.Add(x.User as Player);
                teamResult.TotalScore += (x.User as Player).Score;
            });

            TeamResults = TeamResults.OrderByDescending(x => x.TotalScore).ToList();

            DataContext = this;

            InitializeComponent();
        }
    }
}
