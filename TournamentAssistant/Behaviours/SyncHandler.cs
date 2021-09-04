using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistant.Models;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.Behaviours
{
    public class SyncHandler : IInitializable, IDisposable
    {
        private readonly RoomData _roomData;
        private readonly PauseController _pauseController;
        private readonly PauseMenuManager _pauseMenuManager;
        private readonly LevelStateManager _levelStateManager;
        private readonly StandardLevelGameplayManager _standardLevelGameplayManager;

        private readonly Button _backButton;
        private readonly Button _restartButton;
        private readonly Button _continueButton;

        private readonly LevelBar _levelBar;
        private readonly TextMeshProUGUI _songNameText;
        private readonly TextMeshProUGUI _authorNameText;
        private readonly TextMeshProUGUI _difficultyText;

        public SyncHandler(RoomData roomData, PauseController pauseController, PauseMenuManager pauseMenuManager, LevelStateManager levelStateManager, ILevelEndActions standardLevelGameplayManager)
        {
            _roomData = roomData;
            _pauseController = pauseController;
            _pauseMenuManager = pauseMenuManager;
            _levelStateManager = levelStateManager;
            _standardLevelGameplayManager = (standardLevelGameplayManager as StandardLevelGameplayManager)!;


            _backButton = _pauseMenuManager.GetField<Button, PauseMenuManager>("_backButton");
            _restartButton = _pauseMenuManager.GetField<Button, PauseMenuManager>("_restartButton");
            _continueButton = _pauseMenuManager.GetField<Button, PauseMenuManager>("_continueButton");

            _levelBar = _pauseMenuManager.GetField<LevelBar, PauseMenuManager>("_levelBar");
            _difficultyText = _levelBar.GetField<TextMeshProUGUI, LevelBar>("_difficultyText");
            _authorNameText = _levelBar.GetField<TextMeshProUGUI, LevelBar>("_authorNameText");
            _songNameText = _levelBar.GetField<TextMeshProUGUI, LevelBar>("_songNameText");
        }

        public void Initialize()
        {
            if (_roomData.matchOptions.DisablePausing)
            {
                //We know pausecontroller will be guaranteed true here since we've already waited for it when disabling pause
                var guaranteedPauseController = _pauseController;
                guaranteedPauseController.canPauseEvent -= AntiPause.HandlePauseControllerCanPause_AlwaysFalse;
                guaranteedPauseController.canPauseEvent += _standardLevelGameplayManager.HandlePauseControllerCanPause;
            }
            else
            {
                _levelStateManager.LevelFullyStarted += LevelStateManager_LevelFullyStarted;
            }
        }

        private void LevelStateManager_LevelFullyStarted()
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(async () => await SetupPauseMenu());
        }

        private async Task SetupPauseMenu()
        {
            //Prevent players from unpausing with their menu buttons
            _pauseMenuManager.didPressContinueButtonEvent -= _pauseController.HandlePauseMenuManagerDidPressContinueButton;

            _pauseController.Pause();

            //Wait for the pauseMenuManager to have started and set the pause menu text
            //The text we're checking for is the default text for that field
            while (_songNameText.text == "!Not Defined!")
                await Task.Yield();

            _restartButton.gameObject.SetActive(false);
            _continueButton.gameObject.SetActive(false);
            _backButton.gameObject.SetActive(false);

            _levelBar.hide = false;
            _difficultyText.gameObject.SetActive(false);
            _authorNameText.text = "Setting up synchronized streams";
            _songNameText.text = "Please wait";
        }

        public void Resume()
        {
            _restartButton.gameObject.SetActive(true);
            _continueButton.gameObject.SetActive(true);
            _backButton.gameObject.SetActive(true);

            _levelBar.hide = false;
            _difficultyText.gameObject.SetActive(true);
            _pauseMenuManager.Start(); // Restores the text

            // Allow players to unpause in the future
            _pauseMenuManager.didPressContinueButtonEvent += _pauseController.HandlePauseMenuManagerDidPressContinueButton;

            // Resume the game
            _pauseMenuManager.ContinueButtonPressed();

            if (_roomData.matchOptions.DisablePausing)
            {
                _pauseController.canPauseEvent -= _standardLevelGameplayManager.HandlePauseControllerCanPause;
                _pauseController.canPauseEvent += AntiPause.HandlePauseControllerCanPause_AlwaysFalse;
            }
        }

        public void Dispose()
        {
            _levelStateManager.LevelFullyStarted -= LevelStateManager_LevelFullyStarted;
        }
    }
}