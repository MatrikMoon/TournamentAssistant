#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Collections.Generic;
using TMPro;
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

        [UIComponent("leaderboard")]
        private Transform leaderboardTransform;

        [UIComponent("leaderboard")]
        internal CustomCellListTableData leaderboard;

        [UIComponent("page-number-text")]
        private TextMeshProUGUI _pageNumberText;

        [UIValue("up-interactable")]
        internal bool _leaderboardPageUp = false;

        [UIValue("down-interactable")]
        internal bool _leaderboardPageDown = true;

        [UIValue("cells")]
        int cells = 10;

        [UIValue("scores")]
        List<TableScore> scoreboard = new();

        public void SetScores(List<Score> scores, int myScorePos, Score myScore, int currentLeaderboardPos, int maxLeaderboardPos)
        {
            TournamentAssistantShared.Logger.Debug($"scores: {scores.Count}, myScorePos: {myScorePos}, myScore: {myScore.UserId}, currentLeaderboardPos: {currentLeaderboardPos}, maxLeaderboardPos: {maxLeaderboardPos}");
            scoreboard.Clear();
            bool playerScoreAdded = false;
            for (int i = 0; i < scores.Count; i++)
            {
                TableScore currentScore = new()
                {
                    UserId = scores[i].UserId,
                    Username = scores[i].Username,
                    Score = scores[i]._Score,
                    FullCombo = scores[i].FullCombo,
                    Color = scores[i].Color,
                    ScoreboardPosition = i + 1 + (currentLeaderboardPos * 10)
                };

                if (myScorePos == i + 1 + ((currentLeaderboardPos - 1) * 10))
                {
                    currentScore.TextColor = "cyan";
                    playerScoreAdded = true;
                }
                else
                {
                    currentScore.TextColor = "white";
                }

                scoreboard.Add(currentScore);
            }

            //add our player if it wasnt added already & is set
            if (!playerScoreAdded && myScorePos != -1)
            {
                TableScore playerScore = new()
                {
                    UserId = myScore.UserId,
                    Username = myScore.Username,
                    Score = myScore._Score,
                    FullCombo = myScore.FullCombo,
                    Color = myScore.Color,
                    ScoreboardPosition = myScorePos,
                    TextColor = "cyan"
                };
                scoreboard.Add(playerScore);
                playerScoreAdded = false;
            }

            TournamentAssistantShared.Logger.Debug("About to reload data");
            if (scoreboard != null) leaderboard?.tableView.ReloadData();

            //Page numbering
            _pageNumberText.text = $"{currentLeaderboardPos} / {maxLeaderboardPos}";

            //Disable page buttons if at the start/end of scoreboard
            _leaderboardPageUp = !(currentLeaderboardPos <= 1);
            _leaderboardPageDown = !(currentLeaderboardPos >= maxLeaderboardPos);
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

        [UIAction("#post-parse")]
        private void PostParse()
        {
            leaderboardTransform.Find("LoadingControl").gameObject.SetActive(false);
        }
    }

    [Serializable]
    public class TableScore
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public int Score { get; set; }
        public bool FullCombo { get; set; }
        public string Color { get; set; }
        public string TextColor { get; set; }
        public int ScoreboardPosition { get; set; }
        public string bgColor { get; set; } //preparation for team colors, player background will be used
    }
}
