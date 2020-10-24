using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections.Generic;
using TournamentAssistant.UI.Views;

namespace TournamentAssistant.UI.ViewControllers
{
    class CustomLeaderboard : BSMLResourceViewController
    {
        public override string ResourceName => $"TournamentAssistant.UI.Views.{GetType().Name}.bsml";

        private CustomLeaderboardTable leaderboard;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation && addedToHierarchy)
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
