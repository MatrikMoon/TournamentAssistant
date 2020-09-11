using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using System.Collections.Generic;
using TournamentAssistant.UI.Views;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(@"C:\Users\Moon\source\repos\TournamentAssistant\TournamentAssistant\UI\Views\CustomLeaderboard.bsml")]
    [ViewDefinition("TournamentAssistant.UI.Views.CustomLeaderboard.bsml")]
    class CustomLeaderboard : BSMLAutomaticViewController
    {
        public CustomLeaderboardTable leaderboard;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                gameObject.SetActive(false);

                if (leaderboard == null)
                {
                    leaderboard = gameObject.AddComponent<CustomLeaderboardTable>();
                    leaderboard.transform.SetParent(transform, false);
                    leaderboard.name = "Custom Leaderboard";
                }

                gameObject.SetActive(true);
            }
        }

        private IEnumerator SetScoreOnDelay()
        {
            yield return new WaitForSeconds(2);
            var scoreData = new List<CustomLeaderboardTable.CustomScoreData>();
            int myPos = 1;
            for (int i = 1; i <= 10; i++)
            {
                scoreData.Add(new CustomLeaderboardTable.CustomScoreData(
                    100 + i,
                    "Moon",
                    i,
                    true
                ));
            }
            SetScores(scoreData, myPos);
        }

        public void SetScores(List<CustomLeaderboardTable.CustomScoreData> scores, int myScorePos)
        {
            int num = (scores != null) ? scores.Count : 0;
            for (int j = num; j < 10; j++)
            {
                scores.Add(new CustomLeaderboardTable.CustomScoreData(-1, string.Empty, j + 1, false));
            }

            leaderboard.SetScores(scores, myScorePos);
        }
    }
}
