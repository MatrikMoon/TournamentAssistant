using CustomUI.BeatSaber;
using Oculus.Platform;
using Oculus.Platform.Models;
using SongCore;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    class CenterViewController : CustomViewController
    {
        private AlwaysOwnedContentSO _alwaysOwnedContent;
        private BeatmapLevelCollectionSO _primaryLevelCollection;
        private BeatmapLevelCollectionSO _secondaryLevelCollection;
        private BeatmapLevelCollectionSO _tertiaryLevelCollection;
        private BeatmapLevelCollectionSO _extrasLevelCollection;

        private TextMeshProUGUI _artistText;
        private TextMeshProUGUI _helpText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _difficultyText;
        private TextMeshProUGUI _njsText;
        private TextMeshProUGUI _bpmText;
        private TextMeshProUGUI _notesText;
        private TextMeshProUGUI _durationText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _artistText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0, 28f));
                _artistText.enableWordWrapping = true;
                _artistText.alignment = TextAlignmentOptions.Center;

                _helpText = BeatSaberUI.CreateText(rectTransform, $"Welcome to the Tournament waiting room!", new Vector2(0, 20f));
                _helpText.enableWordWrapping = true;
                _helpText.alignment = TextAlignmentOptions.Center;

                _statusText = BeatSaberUI.CreateText(rectTransform, $"<color=\"green\">Status: </color>", new Vector2(-46f, -8f));
                _statusText.rectTransform.sizeDelta -= new Vector2(20f, 0);
                _statusText.enableWordWrapping = true;
                _statusText.alignment = TextAlignmentOptions.Center;

                _difficultyText= BeatSaberUI.CreateText(rectTransform, "", new Vector2(0, 12f));
                _difficultyText.enableWordWrapping = true;
                _difficultyText.alignment = TextAlignmentOptions.Center;

                _njsText = BeatSaberUI.CreateText(rectTransform, "NJS: ", new Vector2(0, 0f));
                _njsText.enableWordWrapping = true;
                _njsText.alignment = TextAlignmentOptions.Center;

                _bpmText = BeatSaberUI.CreateText(rectTransform, "BPM: ", new Vector2(0, -8f));
                _bpmText.enableWordWrapping = true;
                _bpmText.alignment = TextAlignmentOptions.Center;

                _notesText = BeatSaberUI.CreateText(rectTransform, "NOTES: ", new Vector2(0, -16f));
                _notesText.enableWordWrapping = true;
                _notesText.alignment = TextAlignmentOptions.Center;

                _durationText = BeatSaberUI.CreateText(rectTransform, "DURATION: ", new Vector2(0, -24f));
                _durationText.enableWordWrapping = true;
                _durationText.alignment = TextAlignmentOptions.Center;

                _artistText.gameObject.SetActive(false);
                _difficultyText.gameObject.SetActive(false);
                _njsText.gameObject.SetActive(false);
                _bpmText.gameObject.SetActive(false);
                _notesText.gameObject.SetActive(false);
                _durationText.gameObject.SetActive(false);

                //Set up Client
                Action<string> onUsernameResolved = (username) =>
                {
                    UpdateStatus("Connecting to server...");
                    Plugin.client = new Client("beatsaber.networkauditor.org", username);
                    Plugin.client.Start();

                    Plugin.client.LoadedSong += SetUIToLevel;
                    Plugin.client.DelayTestTriggered += () => StartCoroutine(DelayTestText());
                    Plugin.client.MatchUpdated += SetUIToMatch;

                    if (Loader.AreSongsLoaded)
                    {
                        UpdateStatus("Sending song list...");

                        RefreshLoadedSongs();
                        SendLoadedSongs();

                        if (Plugin.client.Connected)
                        {
                            UpdateStatus("Waiting for Coordinator to start match...");
                        }
                        else UpdateStatus("Initial connection attempt failed...\nYou may want to restart Beat Saber or contact your Moderator.", "orange");
                    }
                    else
                    {
                        UpdateStatus("Waiting for songs to load...");
                    }

                    //If songs are reloaded after the fact, we should update the list and send it to the server again
                    Loader.SongsLoadedEvent += SongsLoaded;
                };

                UpdateStatus("Getting username...");
                GetPlatformUsername(onUsernameResolved);
            }
        }

        IEnumerator DelayTestText()
        {
            _helpText.color = Color.red;
            yield return new WaitForSeconds(3);
            _helpText.color = Color.white;
        }

        private void SetUIToLevel(IBeatmapLevel level)
        {
            currentlySelectedMap = level;

            _artistText.gameObject.SetActive(true);
            _njsText.gameObject.SetActive(true);
            _bpmText.gameObject.SetActive(true);
            _notesText.gameObject.SetActive(true);
            _durationText.gameObject.SetActive(true);

            _artistText.SetText($"{level.songAuthorName} - {level.levelAuthorName}");
            _helpText.SetText(level.songName);
            _helpText.fontSize = 8;

            _njsText.SetText($"NJS: (Depends on difficulty)");
            _bpmText.SetText($"BPM: {level.beatsPerMinute}");
            _notesText.SetText($"NOTES: (Depends on difficulty)");
            _durationText.SetText($"DURATION: {string.Format("{0}:{1:00}", Math.Floor(level.beatmapLevelData.audioClip.length / 60), level.beatmapLevelData.audioClip.length % 60)}");
        }

        //TEMP ----------
        private IBeatmapLevel currentlySelectedMap;
        private void SetUIToMatch(Match match)
        {
            if (currentlySelectedMap != null)
            {
                var beatmapSet = currentlySelectedMap.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName.ToLower() == match.CurrentlySelectedCharacteristic?.SerializedName.ToLower());
                if (beatmapSet != null)
                {
                    var beatmap = beatmapSet.difficultyBeatmaps.FirstOrDefault(x => x.difficulty == match?.CurrentlySelectedDifficulty);
                    if (beatmap != null)
                    {
                        _difficultyText.gameObject.SetActive(true);
                        _difficultyText.SetText($"({match.CurrentlySelectedDifficulty.ToString()})");

                        _njsText.SetText($"NJS: {beatmap.noteJumpMovementSpeed}");
                        _notesText.SetText($"NOTES: {beatmap.beatmapData.notesCount}");
                    }
                }
            }
        }
        //---------------

        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        private void UpdateStatus(string message, string color = "green")
        {
            _statusText.text = $"<color=\"{color}\">Status: {message}</color>";
        }

        private void RefreshLoadedSongs()
        {
            if (_alwaysOwnedContent == null) _alwaysOwnedContent = Resources.FindObjectsOfTypeAll<AlwaysOwnedContentSO>().First();
            if (_primaryLevelCollection == null) _primaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[0].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_secondaryLevelCollection == null) _secondaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[1].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_tertiaryLevelCollection == null) _tertiaryLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[2].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
            if (_extrasLevelCollection == null) _extrasLevelCollection = _alwaysOwnedContent.alwaysOwnedPacks.First(x => x.packID == OstHelper.packs[3].PackID).beatmapLevelCollection as BeatmapLevelCollectionSO;
        }

        private void SendLoadedSongs()
        {
            Plugin.masterLevelList = new List<IPreviewBeatmapLevel>();
            Plugin.masterLevelList.AddRange(_primaryLevelCollection.beatmapLevels);
            Plugin.masterLevelList.AddRange(_secondaryLevelCollection.beatmapLevels);
            Plugin.masterLevelList.AddRange(_tertiaryLevelCollection.beatmapLevels);
            Plugin.masterLevelList.AddRange(_extrasLevelCollection.beatmapLevels);
            Plugin.masterLevelList.AddRange(Loader.CustomLevelsCollection.beatmapLevels);

            if (Plugin.client != null && Plugin.client.Self != null) Plugin.client.SendSongList(Plugin.masterLevelList);
        }

        private void SongsLoaded(Loader _, Dictionary<string, CustomPreviewBeatmapLevel> __)
        {
            RefreshLoadedSongs();
            SendLoadedSongs();
        }

        private static void GetPlatformUsername(Action<string> usernameResolved)
        {
            if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR || Environment.CommandLine.Contains("-vrmode oculus"))
            {
                GetSteamUser(usernameResolved);
            }
            else if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
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
