using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using System.Linq;
using System.Threading;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Misc;
using TournamentAssistant.UI;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
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
    public class Plugin : IBeatSaberPlugin
    {
        public string Name => SharedConstructs.Name;
        public string Version => SharedConstructs.Version;

        public static Client client;

        public static bool UseSyncController { get; set; }

        private MainFlowCoordinator _mainFlowCoordinator;
        private IntroFlowCoordinator _introFlowCoordinator;
        private UnityMainThreadDispatcher _threadDispatcher;

        public void OnApplicationStart()
        {
            SongUtils.OnApplicationStart();
            CreateMenuButton();
        }

        public void OnApplicationQuit()
        {
        }

        public void OnLevelWasLoaded(int level)
        {

        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
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
                    client.Self.CurrentPlayState = TournamentAssistantShared.Models.Player.PlayState.InGame;
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

        private void CreateMenuButton()
        {
            MenuButtons.instance.RegisterButton(new MenuButton("Tournament Room", MenuButtonPressed));
        }

        private void MenuButtonPressed()
        {
            if (_mainFlowCoordinator == null) _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            if (_introFlowCoordinator == null) _introFlowCoordinator = BeatSaberUI.CreateFlowCoordinator<IntroFlowCoordinator>(_mainFlowCoordinator.gameObject);
            _introFlowCoordinator.PresentUI();
        }

        public void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameCore")
            {
                if (client != null && client.Connected)
                {
                    client.Self.CurrentPlayState = TournamentAssistantShared.Models.Player.PlayState.Waiting;
                    var playerUpdated = new Event();
                    playerUpdated.eventType = Event.EventType.PlayerUpdated;
                    playerUpdated.changedObject = client.Self;
                    client.Send(new Packet(playerUpdated));
                }
            }
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        public static bool IsInMenu() => SceneManager.GetActiveScene().name == "MenuViewControllers";
    }
}
