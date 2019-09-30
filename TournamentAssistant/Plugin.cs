using CustomUI.MenuButton;
using IPA;
using Oculus.Platform;
using Oculus.Platform.Models;
using SongCore;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = TournamentAssistantShared.Logger;
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

        private MainFlowCoordinator _mainFlowCoordinator;
        private UI.FlowCoordinators.IntroFlowCoordinator _introFlowCoordinator;

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
                SharedCoroutineStarter.instance.StartCoroutine(SetupUI());
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
                }
            }
        }

        //Waits for menu scenes to be loaded then creates UI elements
        //Courtesy of BeatSaverDownloader
        private IEnumerator SetupUI()
        {
            List<Scene> menuScenes = new List<Scene>() { SceneManager.GetSceneByName("MenuCore"), SceneManager.GetSceneByName("MenuViewControllers"), SceneManager.GetSceneByName("MainMenu") };
            yield return new WaitUntil(() => { return menuScenes.All(x => x.isLoaded); });

            CreateMenuButton();
        }

        private void CreateMenuButton()
        {
            if (_mainFlowCoordinator == null) _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            if (_introFlowCoordinator == null) _introFlowCoordinator = _mainFlowCoordinator.gameObject.AddComponent<UI.FlowCoordinators.IntroFlowCoordinator>();

            MenuButtonUI.AddButton("Tournament Room", "", () => _introFlowCoordinator.PresentUI());
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
