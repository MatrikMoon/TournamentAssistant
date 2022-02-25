using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using System.Linq;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Config = TournamentAssistantShared.Config;

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

        public static bool UseSync { get; set; }
        public static bool UseFloatingScoreboard { get; set; }
        public static bool DisableFail { get; set; }
        public static bool DisablePause { get; set; }
        public static bool DisableScoresaberSubmission { get; set; }

        private MainFlowCoordinator _mainFlowCoordinator;
        private ModeSelectionCoordinator _modeSelectionCoordinator;
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
                InitScoreSaber();
            }

            //This behaviour stays always
            new GameObject("ScreenOverlay").AddComponent<ScreenOverlay>();
        }

        //Broken off so that if scoresaber isn't installed, we don't try to load anything from it
        private static void InitScoreSaber()
        {
            ScoreSaberInterop.InitAndSignIn();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MainMenu")
            {
                _threadDispatcher = _threadDispatcher ??
                                    new GameObject("Thread Dispatcher").AddComponent<UnityMainThreadDispatcher>();
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

                    if (DisableFail)
                    {
                        new GameObject("AntiFail").AddComponent<AntiFail>();
                        DisableFail = false;
                    }

                    if (DisablePause) new GameObject("AntiPause").AddComponent<AntiPause>();
                    else if
                        (UseSync) //DisablePause will invoke UseSync after it's done to ensure they don't interfere with each other
                    {
                        new GameObject("SyncHandler").AddComponent<SyncHandler>();
                        UseSync = false;
                    }

                    var player = client.State.Players.FirstOrDefault(x => x.User.UserEquals(client.Self));
                    player.PlayState = Player.PlayStates.InGame;
                    var playerUpdated = new Event
                    {
                        player_updated_event = new Event.PlayerUpdatedEvent
                        {
                            Player = player
                        }
                    };

                    client.Send(new Packet
                    {
                        Event = playerUpdated
                    });
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
                if (DisablePause)
                    DisablePause =
                        false; //We can't disable this up above since SyncHandler might need to know info about its status

                if (client != null && client.Connected)
                {
                    var player = client.State.Players.FirstOrDefault(x => x.User.UserEquals(client.Self));
                    player.PlayState = Player.PlayStates.Waiting;
                    var playerUpdated = new Event
                    {
                        player_updated_event = new Event.PlayerUpdatedEvent
                        {
                            Player = player
                        }
                    };

                    client.Send(new Packet
                    {
                        Event = playerUpdated
                    });
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
            _modeSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ModeSelectionCoordinator>();
            _modeSelectionCoordinator.DidFinishEvent += modeSelectionCoordinator_DidFinishEvent;

            _mainFlowCoordinator.PresentFlowCoordinatorOrAskForTutorial(_modeSelectionCoordinator);
        }

        private void modeSelectionCoordinator_DidFinishEvent()
        {
            _modeSelectionCoordinator.DidFinishEvent -= modeSelectionCoordinator_DidFinishEvent;
            _mainFlowCoordinator.DismissFlowCoordinator(_modeSelectionCoordinator);
        }

        public static bool IsInMenu() => SceneManager.GetActiveScene().name == "MainMenu";
    }
}