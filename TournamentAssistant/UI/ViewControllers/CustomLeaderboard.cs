#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections.Generic;
using UnityEngine;

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

        public void SetScores(List<LeaderboardTableView.ScoreData> scores, int myScorePos)
        {
            int num = (scores != null) ? scores.Count : 0;
            for (int j = num; j < 10; j++)
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
