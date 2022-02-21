using BS_Utils.Gameplay;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using UnityEngine;

namespace TournamentAssistant.Utilities
{
    public class PlayerUtils
    {
        public static async void GetPlatformUserData(Action<string, ulong> usernameResolved)
        {
            var user = await GetUserInfo.GetUserAsync();
            usernameResolved?.Invoke(user.userName, ulong.Parse(user.platformUserId));
        }

        public static void ReturnToMenu()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var results = Resources.FindObjectsOfTypeAll<PrepareLevelCompletionResults>().FirstOrDefault()?.FillLevelCompletionResults(LevelCompletionResults.LevelEndStateType.Incomplete, LevelCompletionResults.LevelEndAction.Quit);
                if (results != null) Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault()?.Finish(results);
            });
        }
    }
}
