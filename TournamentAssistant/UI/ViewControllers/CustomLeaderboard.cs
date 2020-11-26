#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections.Generic;

namespace TournamentAssistant.UI.ViewControllers
{
    class CustomLeaderboard : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        [UIComponent("leaderboard")]
        internal LeaderboardTableView leaderboard;

        public void SetScores(List<LeaderboardTableView.ScoreData> scores, int myScorePos)
        {
            int num = (scores != null) ? scores.Count : 0;
            for (int j = num; j < 10; j++)
            {
                scores.Add(new LeaderboardTableView.ScoreData(-1, string.Empty, j + 1, false));
            }

            leaderboard.SetScores(scores, myScorePos);
        }
    }
}
