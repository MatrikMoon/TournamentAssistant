using HarmonyLib;
using System.Collections;
using System.Linq;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UnityUtilities
{
    public class AntiPause
    {
        static readonly Harmony _harmony = new("TA:AntiPause");

        static bool _forcePause;

        static bool _allowPause = true;
        static bool _allowContinueAfterPause = true;

        public static bool AllowPause
        {
            get { return _allowPause; }
            set
            {
                if (value == _allowPause)
                {
                    return;
                }

                if (value)
                {
                    Logger.Info($"Harmony unpatching {nameof(PauseController)}.{nameof(PauseController.Pause)}");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(PauseController), nameof(PauseController.Pause)),
                          AccessTools.Method(typeof(AntiPause), nameof(PausePrefix))
                    );
                }
                else
                {
                    Logger.Info($"Harmony patching {nameof(PauseController)}.{nameof(PauseController.Pause)}");
                    _harmony.Patch(
                        AccessTools.Method(typeof(PauseController), nameof(PauseController.Pause)),
                        new(AccessTools.Method(typeof(AntiPause), nameof(PausePrefix)))
                    );
                }
                _allowPause = value;
            }
        }

        public static bool AllowContinueAfterPause
        {
            get { return _allowContinueAfterPause; }
            set
            {
                _allowContinueAfterPause = value;

                var pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();
                var pauseController = Resources.FindObjectsOfTypeAll<PauseController>().FirstOrDefault();

                // If this is called outside the GameCore scene, these might not be available, which is okay since they'll
                // be reset on the next load of the scene anyway
                if (pauseMenuManager == null || pauseController == null)
                {
                    return;
                }

                if (value)
                {
                    Logger.Info($"Reenabling ability to continue in pause menu");

                    //Allow players to unpause in the future
                    pauseMenuManager.didPressContinueButtonEvent += pauseController.HandlePauseMenuManagerDidPressContinueButton;
                    pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
                }
                else
                {
                    Logger.Info($"Preventnig ability to continue in pause menu");

                    //Prevent players from unpausing with their menu buttons
                    pauseMenuManager.didPressContinueButtonEvent -= pauseController.HandlePauseMenuManagerDidPressContinueButton;
                    pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
                }
            }
        }

        static bool PausePrefix()
        {
            bool runOriginal = _forcePause || AllowPause;
            Logger.Debug($"PausePrefix: {runOriginal}");
            return runOriginal;
        }

        public static IEnumerator WaitCanPause()
        {
            var pauseController = Resources.FindObjectsOfTypeAll<PauseController>().First();
            var standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => pauseController.GetProperty<bool>("canPause"));
        }

        public static void Pause()
        {
            _forcePause = true;
            try
            {
                var pauseController = Resources.FindObjectsOfTypeAll<PauseController>().First();
                pauseController.Pause();
            }
            finally
            {
                _forcePause = false;
            }
        }
    }
}
