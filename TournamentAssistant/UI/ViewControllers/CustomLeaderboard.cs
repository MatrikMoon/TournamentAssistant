#pragma warning disable 0649

using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections.Generic;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class CustomLeaderboard : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        [UIComponent("leaderboard")]
        private Transform leaderboardTransform;

        [UIComponent("leaderboard")]
        internal LeaderboardTableView leaderboard;

        public void SetScores(List<LeaderboardTableView.ScoreData> scores, int myScorePos)
        {
            if (scores == null)
                scores = new List<LeaderboardTableView.ScoreData>();
            int num = scores.Count;
            for (int j = num; j < 10; j++)
            {
                scores.Add(new LeaderboardTableView.ScoreData(-1, string.Empty, j + 1, false));
            }

            leaderboard.SetScores(scores, myScorePos);
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            TournamentAssistantShared.Logger.Debug("Post Parse CustomLeaderboard! Leaderboard: " + leaderboard);
            leaderboardTransform.Find("LoadingControl").gameObject.SetActive(false);
            SetScores(null, -1);
        }
    }
}