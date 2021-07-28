using IPA.Utilities;
using System;
using System.Threading.Tasks;
using Zenject;

namespace TournamentAssistant.Behaviors
{
    public class LevelStateManager : IInitializable
    {
        public event Action? LevelFullyStarted;
        private PauseController _pauseController;
        private StandardLevelGameplayManager _standardLevelGameplayManager;
        private static readonly FieldAccessor<PauseController, bool>.Accessor CanPause = FieldAccessor<PauseController, bool>.GetAccessor("_gameState");
        private static readonly FieldAccessor<StandardLevelGameplayManager, StandardLevelGameplayManager.GameState>.Accessor GameState = FieldAccessor<StandardLevelGameplayManager, StandardLevelGameplayManager.GameState>.GetAccessor("_gameState");

        public LevelStateManager(PauseController pauseController, ILevelEndActions levelEndActions)
        {
            _pauseController = pauseController;
            _standardLevelGameplayManager = (levelEndActions as StandardLevelGameplayManager)!;
        }

        public void Initialize()
        {
            _standardLevelGameplayManager.StartCoroutine(IPA.Utilities.Async.Coroutines.WaitForTask(WaitForStart()));
        }

        private async Task WaitForStart()
        {
            while (GameState(ref _standardLevelGameplayManager) != StandardLevelGameplayManager.GameState.Playing)
                await Task.Yield();

            while (!CanPause(ref _pauseController))
                await Task.Yield();

            LevelFullyStarted?.Invoke();
        }
    }
}