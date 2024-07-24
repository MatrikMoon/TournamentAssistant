#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class CustomLeaderboard : BSMLAutomaticViewController
    {
        static readonly Harmony _harmony = new("TA:CustomLeaderboard");

        [UIValue("leaderboard-text")]
        private string leaderboardText = Plugin.GetLocalized("leaderboard");

        [UIComponent("leaderboard")]
        private Transform leaderboardTransform;

        [UIComponent("leaderboard")]
        private LeaderboardTableView leaderboard;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (addedToHierarchy)
            {
                Logger.Info($"Harmony patching {nameof(LeaderboardTableView)}.{nameof(LeaderboardTableView.CellForIdx)}");
                _harmony.Patch(
                    AccessTools.Method(typeof(LeaderboardTableView), nameof(LeaderboardTableView.CellForIdx)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(CustomLeaderboard), nameof(CellForIdx_Postfix)))
                );
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            if (removedFromHierarchy)
            {
                Logger.Info($"Harmony unpatching {nameof(LeaderboardTableView)}.{nameof(LeaderboardTableView.CellForIdx)}");
                _harmony.Unpatch(
                    AccessTools.Method(typeof(LeaderboardTableView), nameof(LeaderboardTableView.CellForIdx)),
                    AccessTools.Method(typeof(CustomLeaderboard), nameof(CellForIdx_Postfix))
                );
            }
        }

        public void SetScores(List<TournamentAssistantShared.Models.LeaderboardEntry> scores, QualifierEvent.LeaderboardSort sort, string selfPlatformId)
        {
            List<LeaderboardTableView.ScoreData> scoresToUse = scores.Take(10).Select((x, index) => new CustomScoreData(x.GetScoreValueByQualifierSettings(sort), x.Username, index + 1, x.FullCombo, x.Color)).ToList<LeaderboardTableView.ScoreData>();
            var myScoreIndex = scores.FindIndex(x => x.PlatformId == selfPlatformId);

            if (myScoreIndex >= 10)
            {
                var myScore = scores.ElementAt(myScoreIndex);
                scoresToUse.Add(new CustomScoreData(myScore.GetScoreValueByQualifierSettings(sort), myScore.Username, myScoreIndex + 1, myScore.FullCombo));

                myScoreIndex = 10; // Be sure the newly added score is highlighted
            }

            SetScores(scoresToUse, myScoreIndex);
        }

        public void SetScores(List<LeaderboardTableView.ScoreData> scores, int myScorePos)
        {
            var numberOfExistingScores = (scores != null) ? scores.Count : 0;
            for (int j = numberOfExistingScores; j < 10; j++)
            {
                scores.Add(new CustomScoreData(-1, string.Empty, j + 1, false));
            }

            leaderboard.SetScores(scores, myScorePos);
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            leaderboardTransform.Find("LoadingControl").gameObject.SetActive(false);
        }

        public static void CellForIdx_Postfix(ref LeaderboardTableView __instance, ref TableCell __result, int row)
        {
            var scores = __instance.GetField<List<LeaderboardTableView.ScoreData>>("_scores");
            var specialScorePos = __instance.GetField<int>("_specialScorePos");

            /*if (specialScorePos != row)
            {
                // We trust that the actual object we're working with was created above, and is therefore a CustomScoreData
                ColorUtility.TryParseHtmlString((scores[row] as CustomScoreData).Color, out var parsedColor);
                __result.GetField<TextMeshProUGUI>("_playerNameText").color = parsedColor;
            }*/

            // We trust that the actual object we're working with was created above, and is therefore a CustomScoreData
            var colorString = (scores[row] as CustomScoreData).Color;

            // For now, we'll show users their custom color even when the score would usually be highlighted
            // in the regular "my score" way. Above is the commented out alternative code
            if (colorString != "#ffffff")
            {
                ColorUtility.TryParseHtmlString(colorString, out var parsedColor);
                __result.GetField<TextMeshProUGUI>("_playerNameText").color = parsedColor;
            }
        }

        public class CustomScoreData : LeaderboardTableView.ScoreData
        {
            public string Color
            {
                get;
                private set;
            }

            public CustomScoreData(int score, string playerName, int place, bool fullCombo, string color = "#ffffff") : base(score, playerName, place, fullCombo)
            {
                Color = color;
            }
        }
    }
}
