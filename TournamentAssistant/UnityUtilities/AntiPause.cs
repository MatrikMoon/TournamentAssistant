using HarmonyLib;
using System.Collections;
using System.Linq;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UnityUtilities
{
    public class AntiPause
    {
        static readonly Harmony _harmony = new("TA:AntiPause");

        static bool _forcePause;
        static bool _forceResume;

        static bool _allowPause = true;
        static bool _allowContinueAfterPause = true;
        static bool _allowRestart = true;

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
                        new HarmonyMethod(AccessTools.Method(typeof(AntiPause), nameof(PausePrefix)))
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
                if (value == _allowContinueAfterPause)
                {
                    return;
                }

                var pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();

                // If this is called outside the GameCore scene, this might not be available, which is okay since the gameObjects will be reset
                // next time the scene loads anyway
                if (pauseMenuManager != null)
                {
                    pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(value);
                }

                if (value)
                {
                    Logger.Info($"Reenabling ability to continue in pause menu");

                    //Allow players to unpause in the future
                    Logger.Info($"Harmony unpatching {nameof(PauseController)}.HandlePauseMenuManagerDidPressContinueButton");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(PauseController), "HandlePauseMenuManagerDidPressContinueButton"),
                          AccessTools.Method(typeof(AntiPause), nameof(HandlePauseMenuManagerDidPressContinueButtonPrefix))
                    );
                }
                else
                {
                    Logger.Info($"Preventing ability to continue in pause menu");

                    //Prevent players from unpausing with their menu buttons
                    Logger.Info($"Harmony patching {nameof(PauseController)}.HandlePauseMenuManagerDidPressContinueButton");
                    _harmony.Patch(
                        AccessTools.Method(typeof(PauseController), "HandlePauseMenuManagerDidPressContinueButton"),
                        new HarmonyMethod(AccessTools.Method(typeof(AntiPause), nameof(HandlePauseMenuManagerDidPressContinueButtonPrefix)))
                    );
                }
                _allowContinueAfterPause = value;
            }
        }

        public static bool AllowRestart
        {
            get { return _allowRestart; }
            set
            {
                if (value == _allowRestart)
                {
                    return;
                }

                var pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();

                // If this is called outside the GameCore scene, this might not be available, which is okay since the gameObjects will be reset
                // next time the scene loads anyway
                if (pauseMenuManager != null)
                {
                    pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(value);
                }

                _allowRestart = value;
            }
        }

        static bool PausePrefix()
        {
            bool runOriginal = _forcePause || AllowPause;
            return runOriginal;
        }

        static bool HandlePauseMenuManagerDidPressContinueButtonPrefix()
        {
            bool runOriginal = _forceResume || AllowContinueAfterPause;
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

        public static void Unpause()
        {
            _forceResume = true;
            try
            {
                var pauseController = Resources.FindObjectsOfTypeAll<PauseController>().First();
                pauseController.HandlePauseMenuManagerDidPressContinueButton();
            }
            finally
            {
                _forceResume = false;
            }
        }
    }
}
