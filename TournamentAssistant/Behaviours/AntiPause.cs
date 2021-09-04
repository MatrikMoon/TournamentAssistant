using System;
using Zenject;

namespace TournamentAssistant.Behaviours
{
    public class AntiPause : IInitializable, IDisposable
    {
        private readonly PauseController _pauseController;
        private readonly LevelStateManager _levelStateManager;
        private readonly StandardLevelGameplayManager _standardLevelGameplayManager;

        public AntiPause(PauseController pauseController, LevelStateManager levelStateManager, ILevelEndActions standardLevelGameplayManager)
        {
            _pauseController = pauseController;
            _levelStateManager = levelStateManager;
            _standardLevelGameplayManager = (standardLevelGameplayManager as StandardLevelGameplayManager)!;
        }

        public void Initialize()
        {
            _levelStateManager.LevelFullyStarted += LevelStateManager_LevelFullyStarted;
        }

        private void LevelStateManager_LevelFullyStarted()
        {
            _pauseController.canPauseEvent -= _standardLevelGameplayManager.HandlePauseControllerCanPause;
            _pauseController.canPauseEvent += HandlePauseControllerCanPause_AlwaysFalse;
        }

        public void Dispose()
        {
            _levelStateManager.LevelFullyStarted -= LevelStateManager_LevelFullyStarted;
        }

        public static void HandlePauseControllerCanPause_AlwaysFalse(Action<bool> canPause)
        {
            canPause?.Invoke(false);
        }
    }
}