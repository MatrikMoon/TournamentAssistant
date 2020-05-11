using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using System.Linq;
using TournamentAssistant.Behaviors;
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

        private MainFlowCoordinator _mainFlowCoordinator;
        private ServerSelectionCoordinator _serverSelectionCoordinator;
        private UnityMainThreadDispatcher _threadDispatcher;

        [OnEnable]
        public void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SongUtils.OnEnable();
            CreateMenuButton();
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
                    (client.Self as Player).CurrentPlayState = Player.PlayState.InGame;
                    var playerUpdated = new Event();
                    playerUpdated.Type = Event.EventType.PlayerUpdated;
                    playerUpdated.ChangedObject = client.Self;
                    client.Send(new Packet(playerUpdated));

                    if (UseFloatingScoreboard)
                    {
                        new GameObject("ScoreMonitor").AddComponent<InGameScoreMonitor>();
                        new GameObject("FloatingScoreScreen").AddComponent<FloatingScoreScreen>();
                        UseFloatingScoreboard = false;
                    }

                    if (UseSyncController)
                    {
                        new GameObject("SyncController").AddComponent<InGameSyncHandler>();
                        UseSyncController = false;
                    }
                }
            }
        }

        public void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameCore")
            {
                if (InGameSyncHandler.Instance != null) InGameSyncHandler.Destroy();
                if (InGameScoreMonitor.Instance != null) InGameScoreMonitor.Destroy();
                if (FloatingScoreScreen.Instance != null) FloatingScoreScreen.Destroy();

                if (client != null && client.Connected)
                {
                    (client.Self as Player).CurrentPlayState = Player.PlayState.Waiting;
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
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>();
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
