using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using System.Linq;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Misc;
using TournamentAssistant.UI;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        public static bool UseSyncController { get; set; }

        private MainFlowCoordinator _mainFlowCoordinator;
        private ServerSelectionCoordinator _serverSelectionCoordinator;
        private UnityMainThreadDispatcher _threadDispatcher;

        [OnEnable]
        public void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneUnloaded;
            SongUtils.OnEnable();
            CreateMenuButton();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
            {
                _threadDispatcher = _threadDispatcher ?? new GameObject("Thread Dispatcher").AddComponent<UnityMainThreadDispatcher>();

                if (InGameSyncController.Instance != null) InGameSyncController.Destroy();
                if (InGameScoreMonitor.Instance != null) InGameScoreMonitor.Destroy();
            }
            else if (scene.name == "GameCore")
            {
                if (client != null && client.Connected)
                {
                    (client.Self as Player).CurrentPlayState = Player.PlayState.InGame;
                    var playerUpdated = new Event();
                    playerUpdated.eventType = Event.EventType.PlayerUpdated;
                    playerUpdated.changedObject = client.Self;
                    client.Send(new Packet(playerUpdated));

                    new GameObject("ScoreMonitor").AddComponent<InGameScoreMonitor>();

                    if (UseSyncController)
                    {
                        new GameObject("SyncController").AddComponent<InGameSyncController>();
                        UseSyncController = false;
                    }
                }
            }
        }

        public void OnSceneUnloaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name == "GameCore")
            {
                if (client != null && client.Connected)
                {
                    (client.Self as Player).CurrentPlayState = Player.PlayState.Waiting;
                    var playerUpdated = new Event();
                    playerUpdated.eventType = Event.EventType.PlayerUpdated;
                    playerUpdated.changedObject = client.Self;
                    client.Send(new Packet(playerUpdated));
                }
            }
        }

        private void CreateMenuButton()
        {
            MenuButtons.instance.RegisterButton(new MenuButton("BattleSaber", MenuButtonPressed));
        }

        private void MenuButtonPressed()
        {
            _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _serverSelectionCoordinator = _serverSelectionCoordinator ?? BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>(_mainFlowCoordinator.gameObject);
            _serverSelectionCoordinator.DidFinishEvent += introFlowCoordinator_DidFinishEvent;

            _mainFlowCoordinator.PresentFlowCoordinatorOrAskForTutorial(_serverSelectionCoordinator);
        }

        private void introFlowCoordinator_DidFinishEvent()
        {
            _serverSelectionCoordinator.DidFinishEvent -= introFlowCoordinator_DidFinishEvent;
            _mainFlowCoordinator.DismissFlowCoordinator(_serverSelectionCoordinator);
        }

        public static bool IsInMenu() => SceneManager.GetActiveScene().name == "MenuViewControllers";
    }
}
