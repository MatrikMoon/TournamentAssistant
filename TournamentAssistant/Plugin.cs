using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using System.Collections;
using System.Linq;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Config = TournamentAssistant.Misc.Config;
using Packet = TournamentAssistantShared.Packet;

/**
 * Created by Moon on 8/5/2019
 * Base plugin class for the TournamentAssistant plugin
 * Intended to be the player-facing UI for tournaments, where
 * players' games can be handled by their match coordinators
 */

namespace TournamentAssistant
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public string Name => SharedConstructs.Name;
        public string Version => SharedConstructs.Version;

        public static PluginClient client;

        public static Config config = new Config();

        public static bool UseSyncController { get; set; }
        public static bool UseFloatingScoreboard { get; set; }
        public static bool DisablePause { get; set; }

        private MainFlowCoordinator _mainFlowCoordinator;
        private ServerModeSelectionCoordinator _modeSelectionCoordinator;
        private UnityMainThreadDispatcher _threadDispatcher;

        [OnEnable]
        public void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SongUtils.OnEnable();
            CreateMenuButton();

            var scoreSaber = IPA.Loader.PluginManager.GetPluginFromId("ScoreSaber");
            if (scoreSaber != null)
            {
                ScoreSaberInterop.InitAndSignIn();
            }

            //This behaviour stays always
            new GameObject("ScreenOverlay").AddComponent<ScreenOverlay>();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
            {
                _threadDispatcher = _threadDispatcher ?? new GameObject("Thread Dispatcher").AddComponent<UnityMainThreadDispatcher>();
            }
            else if (scene.name == "GameCore")
            {
                if (client != null && client.Connected)
                {
                    new GameObject("ScoreMonitor").AddComponent<ScoreMonitor>();

                    if (UseFloatingScoreboard)
                    {
                        new GameObject("FloatingScoreScreen").AddComponent<FloatingScoreScreen>();
                        UseFloatingScoreboard = false;
                    }

                    if (DisablePause)
                    {
                        DisablePause = false;
                        SharedCoroutineStarter.instance.StartCoroutine(DisablePauseWhenAvailable(UseSyncController));
                    }
                    else if (UseSyncController)
                    {
                        new GameObject("SyncController").AddComponent<SyncHandler>();
                        UseSyncController = false;
                    }

                    (client.Self as Player).PlayState = Player.PlayStates.InGame;
                    var playerUpdated = new Event();
                    playerUpdated.Type = Event.EventType.PlayerUpdated;
                    playerUpdated.ChangedObject = client.Self;
                    client.Send(new Packet(playerUpdated));
                }
            }
        }

        public void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameCore")
            {
                if (SyncHandler.Instance != null) SyncHandler.Destroy();
                if (ScoreMonitor.Instance != null) ScoreMonitor.Destroy();
                if (FloatingScoreScreen.Instance != null) FloatingScoreScreen.Destroy();

                if (client != null && client.Connected)
                {
                    (client.Self as Player).PlayState = Player.PlayStates.Waiting;
                    var playerUpdated = new Event();
                    playerUpdated.Type = Event.EventType.PlayerUpdated;
                    playerUpdated.ChangedObject = client.Self;
                    client.Send(new Packet(playerUpdated));
                }
            }
        }

        private void CreateMenuButton()
        {
            MenuButtons.instance.RegisterButton(new MenuButton("TournamentAssistant", MenuButtonPressed));
        }

        private void MenuButtonPressed()
        {
            _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _modeSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerModeSelectionCoordinator>();
            _modeSelectionCoordinator.DidFinishEvent += modeSelectionCoordinator_DidFinishEvent;

            _mainFlowCoordinator.PresentFlowCoordinatorOrAskForTutorial(_modeSelectionCoordinator);
        }

        private void modeSelectionCoordinator_DidFinishEvent()
        {
            _modeSelectionCoordinator.DidFinishEvent -= modeSelectionCoordinator_DidFinishEvent;
            _mainFlowCoordinator.DismissFlowCoordinator(_modeSelectionCoordinator);
        }

        public static bool IsInMenu() => SceneManager.GetActiveScene().name == "MenuViewControllers";

        private IEnumerator DisablePauseWhenAvailable(bool doSyncAfterwards)
        {
            var standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<PauseController>("_pauseController").GetProperty<bool>("canPause"));

            var pauseController = standardLevelGameplayManager.GetField<PauseController>("_pauseController");
            var pauseMenuManager = pauseController.GetField<PauseMenuManager>("_pauseMenuManager");

            pauseController.canPauseEvent -= standardLevelGameplayManager.HandlePauseControllerCanPause;

            if (doSyncAfterwards)
            {
                new GameObject("SyncController").AddComponent<SyncHandler>();
                UseSyncController = false;
            }
        }
    }
}
