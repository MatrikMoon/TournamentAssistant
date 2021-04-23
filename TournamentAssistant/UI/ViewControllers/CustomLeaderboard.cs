#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Collections.Generic;
using TMPro;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class CustomLeaderboard : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);
        public event Action ScoreboardPageUp;
        public event Action ScoreboardPageDown;
        public event Action ScoreboardReset;

        //team events
        public event Action StandardView;
        public event Action TeamView;
        public event Action TeamScrollerLeft;
        public event Action TeamScrollRight;
        public event Action TeamComparason;
        public event Action TeamScore;

        [UIComponent("leaderboard")]
        internal CustomCellListTableData leaderboard;

        [UIComponent("page-number-text")]
        private TextMeshProUGUI _pageNumberText;

        [UIObject("bottom-panel")]
        internal GameObject Background = null;

        [UIValue("cells")]
        int cells = 10;

        [UIValue("scores")]
        public List<object> scoreboard = new();

        public void FillWithEmpty(List<object> emptyValues)
        {
            scoreboard = emptyValues;
        }
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            ItemOpacityChanger.OpacityChanger(Background, 0.5f);
        }

        public void SetScores(List<Score> scores, int myScorePos, Score myScore, int currentLeaderboardPos, int maxLeaderboardPos)
        {
            scoreboard.Clear();
            bool playerScoreAdded = false;
            for (int i = 0; i < scores.Count; i++)
            {
                LeaderboardText currentScore = new()
                {
                    LeftText = $"{i + 1 + (currentLeaderboardPos * 10)}   {scores[i].Username}",
                    RightText = $"{scores[i]._Score}   ",
                };

                if (scores[i].FullCombo) currentScore.RightText.Insert(currentScore.RightText.Length, "FC");
                else currentScore.RightText.Insert(currentScore.RightText.Length, "  ");

                if (myScorePos == i + 1 + (currentLeaderboardPos * 10))
                {
                    currentScore.TextColor = "cyan";
                    playerScoreAdded = true;
                }
                else currentScore.TextColor = "white";

                scoreboard.Add(currentScore);
            }

            //add our player if it wasnt added already & is set
            if (!playerScoreAdded && myScorePos != -1)
            {
                LeaderboardText currentScore = new()
                {
                    LeftText = $"{myScorePos}   {myScore.Username}",
                    RightText = $"{myScore._Score}   ",
                };
                if (myScore.FullCombo) currentScore.RightText.Insert(currentScore.RightText.Length, "FC");
                else currentScore.RightText.Insert(currentScore.RightText.Length, "  ");
                scoreboard.Add(currentScore);
                playerScoreAdded = false;
                cells = 11;
            }
            if (scoreboard != null) leaderboard?.tableView.ReloadData();
            cells = 10;

            //Page numbering
            _pageNumberText.text = $"{currentLeaderboardPos + 1} / {maxLeaderboardPos}";
        }

        [UIAction("leaderboard#PageUp")]
        void PageUp()
        {
            ScoreboardPageUp?.Invoke();
        }

        [UIAction("leaderboard#PageDown")]
        void PageDown()
        {
            ScoreboardPageDown?.Invoke();
        }

        [UIAction("goto-pg1")]
        void ScoreReset()
        {
            ScoreboardReset?.Invoke();
        }

        [UIAction("standard-view")]
        void GoToStandardView()
        {
            StandardView?.Invoke();
        }

        [UIAction("team-view")]
        void GoToTeamView()
        {
            TeamView?.Invoke();
        }

        [UIAction("scroll-left")]
        void ScrollLeft()
        {
            TeamScrollerLeft?.Invoke();
        }

        [UIAction("scroll-right")]
        void ScrollRight()
        {
            TeamScrollRight?.Invoke();
        }

        [UIAction("team-comparason")]
        void GoToTeamComparason()
        {
            TeamComparason?.Invoke();
        }

        [UIAction("team-score")]
        void GoToTeamScore()
        {
            TeamScore?.Invoke();
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            leaderboard?.tableView.ReloadData();
        }
    }
}
