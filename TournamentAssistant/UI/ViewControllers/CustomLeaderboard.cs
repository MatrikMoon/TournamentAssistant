#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class CustomLeaderboard : BSMLAutomaticViewController
    {
        [UIValue("leaderboard-text")]
        private string leaderboardText = Plugin.GetLocalized("leaderboard");

        [UIComponent("leaderboard")]
        private Transform leaderboardTransform;

        [UIComponent("leaderboard")]
        internal LeaderboardTableView leaderboard;

        public void SetScores(List<TournamentAssistantShared.Models.LeaderboardEntry> scores, QualifierEvent.LeaderboardSort sort, string selfPlatformId)
        {
            var scoresToUse = scores.Take(10).Select((x, index) => new LeaderboardTableView.ScoreData(x.GetScoreValueByQualifierSettings(sort), x.Username, index + 1, x.FullCombo)).ToList();
            var myScoreIndex = scores.FindIndex(x => x.PlatformId == selfPlatformId);

            if (myScoreIndex >= 10)
            {
                var myScore = scores.ElementAt(myScoreIndex);
                scoresToUse.Add(new LeaderboardTableView.ScoreData(myScore.GetScoreValueByQualifierSettings(sort), myScore.Username, myScoreIndex + 1, myScore.FullCombo));

                myScoreIndex = 10; // Be sure the newly added score is highlighted
            }

            SetScores(scoresToUse, myScoreIndex);
        }

        public void SetScores(List<LeaderboardTableView.ScoreData> scores, int myScorePos)
        {
            var numberOfExistingScores = (scores != null) ? scores.Count : 0;
            for (int j = numberOfExistingScores; j < 10; j++)
            {
                scores.Add(new LeaderboardTableView.ScoreData(-1, string.Empty, j + 1, false));
            }

            leaderboard.SetScores(scores, myScorePos);
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            leaderboardTransform.Find("LoadingControl").gameObject.SetActive(false);
        }
    }
}
