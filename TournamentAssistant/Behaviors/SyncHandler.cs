﻿using HarmonyLib;
using System.Collections;
using System.Linq;
using TMPro;
using TournamentAssistant.UnityUtilities;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TournamentAssistant.Behaviors
{
    class SyncHandler : MonoBehaviour
    {
        public static SyncHandler Instance { get; set; }

        static readonly Harmony _harmony = new("TA:SyncHandler");

        private PauseMenuManager pauseMenuManager;
        private PauseController pauseController;

        private string oldLevelText;
        private string oldAuthorText;

        private bool pauseWasPreviouslyAllowed = true;
        private bool continueAfterPauseWasPreviouslyAllowed = true;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().First();
            pauseController = Resources.FindObjectsOfTypeAll<PauseController>().First();

            StartCoroutine(PauseOnStart());
        }

        public IEnumerator PauseOnStart()
        {
            yield return AntiPause.WaitCanPause();

            pauseWasPreviouslyAllowed = AntiPause.AllowPause;
            continueAfterPauseWasPreviouslyAllowed = AntiPause.AllowPause;

            AntiPause.Pause();
            AntiPause.AllowPause = false;
            AntiPause.AllowContinueAfterPause = false;

            var levelBar = pauseMenuManager.GetField<LevelBar>("_levelBar");

            //Wait for the pauseMenuManager to have started and set the pause menu text
            //The text we're checking for is the default text for that field
            yield return new WaitUntil(() => levelBar.GetField<TextMeshProUGUI>("_songNameText").text != "!Not Defined!");

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(false);

            levelBar.hide = false;
            levelBar.GetField<TextMeshProUGUI>("_difficultyText").gameObject.SetActive(false);
            oldLevelText = levelBar.GetField<TextMeshProUGUI>("_songNameText").text;
            oldAuthorText = levelBar.GetField<TextMeshProUGUI>("_authorNameText").text;
            levelBar.GetField<TextMeshProUGUI>("_songNameText").text = Plugin.GetLocalized("please_wait");
            levelBar.GetField<TextMeshProUGUI>("_authorNameText").text = Plugin.GetLocalized("setting_up_synchronized_streams");
        }

        public void Resume()
        {
            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);

            var levelBar = pauseMenuManager.GetField<LevelBar>("_levelBar");
            levelBar.hide = false;
            levelBar.GetField<TextMeshProUGUI>("_difficultyText").gameObject.SetActive(true);
            levelBar.GetField<TextMeshProUGUI>("_songNameText").text = oldLevelText;
            levelBar.GetField<TextMeshProUGUI>("_authorNameText").text = oldAuthorText;

            //Resume the game
            pauseMenuManager.InvokeMethod("ContinueButtonPressed");

            //Reenable pausing if it's not been disabled intentionally
            AntiPause.AllowPause = pauseWasPreviouslyAllowed;
            AntiPause.AllowContinueAfterPause = continueAfterPauseWasPreviouslyAllowed;

            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}