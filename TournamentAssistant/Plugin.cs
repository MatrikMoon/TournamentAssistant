using IPA;
using Oculus.Platform;
using Oculus.Platform.Models;
using SongCore;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantShared;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private AlwaysOwnedContentModelSO _alwaysOwnedContentModel;
        private BeatmapLevelCollectionSO _primaryLevelCollection;
        private BeatmapLevelCollectionSO _secondaryLevelCollection;
        private BeatmapLevelCollectionSO _extrasLevelCollection;

        public static List<IPreviewBeatmapLevel> masterLevelList;

        private Client client;

        public void OnApplicationStart()
        {
            Action<string> onUsernameResolved = (username) =>
            {
                client = new Client("beatsaber.networkauditor.org", username);
                client.Start();

                Loader.SongsLoadedEvent += (Loader _, Dictionary<string, CustomPreviewBeatmapLevel> __) =>
                {
                    if (_alwaysOwnedContentModel == null) _alwaysOwnedContentModel = Resources.FindObjectsOfTypeAll<AlwaysOwnedContentModelSO>().First();
                    if (_primaryLevelCollection == null) _primaryLevelCollection = _alwaysOwnedContentModel.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[0].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
                    if (_secondaryLevelCollection == null) _secondaryLevelCollection = _alwaysOwnedContentModel.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[1].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
                    if (_extrasLevelCollection == null) _extrasLevelCollection = _alwaysOwnedContentModel.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[2].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;

                    masterLevelList = new List<IPreviewBeatmapLevel>();
                    masterLevelList.AddRange(_primaryLevelCollection.beatmapLevels);
                    masterLevelList.AddRange(_secondaryLevelCollection.beatmapLevels);
                    masterLevelList.AddRange(_extrasLevelCollection.beatmapLevels);
                    masterLevelList.AddRange(Loader.CustomLevelsCollection.beatmapLevels);

                    //client.SendSongList(masterLevelList);
                };
            };

            GetPlatformUsername(onUsernameResolved);
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
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        private static void GetPlatformUsername(Action<string> usernameResolved)
        {
            if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR || Environment.CommandLine.Contains("-vrmode oculus"))
            {
                GetSteamUser(usernameResolved);
            }
            else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                GetOculusUser(usernameResolved);
            }
            else GetSteamUser(usernameResolved);
        }

        private static void GetSteamUser(Action<string> usernameResolved)
        {
            if (SteamManager.Initialized)
            {
                usernameResolved?.Invoke(SteamFriends.GetPersonaName());
            }
        }

        private static void GetOculusUser(Action<string> usernameResolved)
        {
            Users.GetLoggedInUser().OnComplete((Message<User> msg) =>
            {
                if (!msg.IsError)
                {
                    usernameResolved?.Invoke(msg.Data.OculusID);
                }
            });
        }
    }
}
