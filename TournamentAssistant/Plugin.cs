using HMUI;
using IPA;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                _threadDispatcher = _threadDispatcher ?? new GameObject("Media Panel").AddComponent<UnityMainThreadDispatcher>();
                SharedCoroutineStarter.instance.StartCoroutine(SetupUI());

                if (InGameSyncController.Instance != null) InGameSyncController.Destroy();
                if (InGameScoreMonitor.Instance != null)
                {
                    InGameScoreMonitor.Instance.ScoreUpdated -= ScoreMonitor_ScoreUpdated;
                    InGameScoreMonitor.Destroy();
                }
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

                    var scoreMonitor = new GameObject("ScoreMonitor").AddComponent<InGameScoreMonitor>();
                    scoreMonitor.ScoreUpdated += ScoreMonitor_ScoreUpdated;

                    if (UseSyncController)
                    {
                        new GameObject("SyncController").AddComponent<InGameSyncController>();
                        UseSyncController = false;
                    }
                }
            }
        }

        private void ScoreMonitor_ScoreUpdated(int score, float songTime)
        {
            //Send score update
            //Threaded, so we don't stall the game with network stuff
            new Thread(() =>
            {
                TournamentAssistantShared.Logger.Info($"SENDING UPDATE SCORE: {score}");
                client.Self.CurrentScore = score;
                var playerUpdate = new Event();
                playerUpdate.eventType = Event.EventType.PlayerUpdated;
                playerUpdate.changedObject = client.Self;
                client.Send(new Packet(playerUpdate));
            }).Start();
        }

        private IEnumerator SetupUI()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().Any());

            CreateMenuButton();
        }

        private void CreateMenuButton()
        {
            if (_mainFlowCoordinator == null) _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            if (_introFlowCoordinator == null)
            {
                _introFlowCoordinator = _mainFlowCoordinator.gameObject.AddComponent<IntroFlowCoordinator>();
                FieldInfo fieldInfo = typeof(FlowCoordinator).GetField("_baseInputModule", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                fieldInfo.SetValue(_introFlowCoordinator, fieldInfo.GetValue(_mainFlowCoordinator));
            }

            //MenuButtonUI.AddButton("Tournament Room", "", () => _introFlowCoordinator.PresentUI());
            BeatSaberMarkupLanguage.MenuButtons.MenuButtons.instance.RegisterButton(new BeatSaberMarkupLanguage.MenuButtons.MenuButton("Tournament Room", () => _introFlowCoordinator.PresentUI()));
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
    }
}
