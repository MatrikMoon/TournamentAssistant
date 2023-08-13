using BS_Utils.Gameplay;
using IPA.Utilities.Async;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace TournamentAssistant.Utilities
{
    public class PlayerUtils
    {
        public static async Task GetPlatformUserData(Func<string, string, Task> usernameResolved)
        {
            var user = await GetUserInfo.GetUserAsync();
            await usernameResolved(user.userName, user.platformUserId);
        }

        public static void ReturnToMenu()
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var results = Resources.FindObjectsOfTypeAll<PrepareLevelCompletionResults>().FirstOrDefault()?.FillLevelCompletionResults(LevelCompletionResults.LevelEndStateType.Incomplete, LevelCompletionResults.LevelEndAction.Quit);
                if (results != null) Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault()?.Finish(results);
            });
        }
    }
}
