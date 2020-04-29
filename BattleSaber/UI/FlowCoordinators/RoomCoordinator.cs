using HMUI;
using System;
using System.Linq;
using BattleSaber.UI.ViewControllers;
using BattleSaber.Utilities;
using BattleSaberShared.Models;
using UnityEngine;
using UnityEngine.UI;
using BattleSaber.Misc;
using System.Collections.Generic;
using BattleSaberShared;
using BattleSaberShared.Models.Packets;
using BeatSaberMarkupLanguage;

namespace BattleSaber.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }

        public event Action DidFinishEvent;

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;
        private PlayerList _playerList;

        private StandardLevelDetailViewController _detailViewController;

        private bool isHost;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Room Screen";
                showBackButton = true;

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += songSelection_SongSelected;
                _songSelection.SetSongs(SongUtils.masterLevelList);

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = "Waiting for the host to select a song...";

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
                _playerList.Players = Match.Players;

                isHost = Match.Leader == Plugin.client.Self;
                if (isHost)
                {
                    ProvideInitialViewControllers(_songSelection, _playerList);
                }
                else
                {
                    ProvideInitialViewControllers(_splashScreen, _playerList);
                }

                Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
                Plugin.client.MatchDeleted += Client_MatchDeleted;
                Plugin.client.LoadedSong += Client_LoadedSong;
                Plugin.client.PlaySong += Client_PlaySong;
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                _songSelection.SongSelected -= songSelection_SongSelected;

                if (isHost)
                {
                    if (_detailViewController)
                    {
                        _detailViewController.didPressPlayButtonEvent -= detailViewController_didPressPlayButtonEvent;
                        _detailViewController.didChangeDifficultyBeatmapEvent -= detailViewController_didChangeDifficultyBeatmapEvent;
                        _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_practiceButton").gameObject.SetActive(true);

                        _detailViewController = null; //Only necessary because I'm doing dumb things with the SLDVC. Please, future me, remove this later
                    }
                }

                Plugin.client.MatchDeleted -= Client_MatchDeleted;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.PlaySong -= Client_PlaySong;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            //SLVC can't do back button listening so we handle it for it
            if (topViewController is StandardLevelDetailViewController) DismissViewController(topViewController);
            else DidFinishEvent?.Invoke();
        }

        private void songSelection_SongSelected(IPreviewBeatmapLevel level)
        {
            SwitchLevelSelection(level);
        }

        private void detailViewController_didChangeDifficultyBeatmapEvent(StandardLevelDetailViewController _, IDifficultyBeatmap beatmap)
        {
            SwitchBeatmapSelection(beatmap);
        }

        private void detailViewController_didPressPlayButtonEvent(StandardLevelDetailViewController controller)
        {
            var gm = new BattleSaberShared.Models.GameplayModifiers();

            var playSong = new PlaySong();
            playSong.characteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == controller.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            playSong.difficulty = (SharedConstructs.BeatmapDifficulty)controller.selectedDifficultyBeatmap.difficulty;
            playSong.gameplayModifiers = gm;
            playSong.playerSettings = new BattleSaberShared.Models.PlayerSpecificSettings();
            playSong.levelId = Match.CurrentlySelectedLevel.LevelId;

            playSong.floatingScoreboard = true;

            Plugin.client.Send(Match.Players.Select(x => x.Guid).ToArray(), new Packet(playSong));
        }

        private void Client_MatchInfoUpdated(Match match)
        {
            Match = match;
            _playerList.Players = match.Players;
        }

        private void Client_MatchDeleted(Match match)
        {
            //If the match is destroyed while we're in here, back out
            if (match == Match)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (_detailViewController && _detailViewController.isInViewControllerHierarchy) DismissViewController(_detailViewController, immediately: true);
                    DidFinishEvent?.Invoke();
                });
            }
        }

        private void Client_LoadedSong(IBeatmapLevel level)
        {
            if (Plugin.IsInMenu())
            {

                Action setData = () =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    //if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                    SwitchLevelSelection(level);
                };
                UnityMainThreadDispatcher.Instance().Enqueue(setData);
            }
        }

        private void LoadSongAsHost(LoadSong loadSong, Action<IBeatmapLevel> onCompleted)
        {
            //Ost's are preloaded
            if (OstHelper.IsOst(loadSong.levelId))
            {
                onCompleted?.Invoke(SongUtils.masterLevelList.First(x => x.levelID == loadSong.levelId) as BeatmapLevelSO);
            }
            //Custom songs we're picking out of a list are already downloaded and only need to be loaded
            else if (SongUtils.masterLevelList.Any(x => x.levelID == loadSong.levelId))
            {
                SongUtils.LoadSong(loadSong.levelId, onCompleted);
            }
        }

        private void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useFloatingScoreboard = false, bool useSync = false)
        {
            //Set up per-play settings
            Plugin.UseSyncController = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;

            //Reset score
            (Plugin.client.Self as Player).CurrentScore = 0;
            var playerUpdate = new Event();
            playerUpdate.eventType = Event.EventType.PlayerUpdated;
            playerUpdate.changedObject = Plugin.client.Self;
            Plugin.client.Send(new Packet(playerUpdate));

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                //If the player is still on the results screen, go ahead and boot them out
                //if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings);
            });
        }

        private void SwitchLevelSelection(IPreviewBeatmapLevel level)
        {
            if (_detailViewController == null)
            {
                _detailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                if (isHost)
                {
                    _detailViewController.didPressPlayButtonEvent += detailViewController_didPressPlayButtonEvent;
                    _detailViewController.didChangeDifficultyBeatmapEvent += detailViewController_didChangeDifficultyBeatmapEvent;
                }
            }

            _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_practiceButton").gameObject.SetActive(false);
            _detailViewController.SetData(level, true, true, true);
            if (!_detailViewController.isActivated) PresentViewController(_detailViewController);

            //Assemble new match info and update the match
            var matchLevel = new PreviewBeatmapLevel()
            {
                LevelId = level.levelID,
                Name = level.songName
            };

            List<Characteristic> characteristics = new List<Characteristic>();
            foreach (var beatmapSet in level.previewDifficultyBeatmapSets)
            {
                characteristics.Add(new Characteristic()
                {
                    SerializedName = beatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = beatmapSet.beatmapDifficulties.Select(x => (SharedConstructs.BeatmapDifficulty)x).ToArray()
                });
            }
            matchLevel.Characteristics = characteristics.ToArray();
            Match.CurrentlySelectedLevel = matchLevel;
            Match.CurrentlySelectedCharacteristic = null;
            Match.CurrentlySelectedDifficulty = SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

            //Tell the other players to download the song and update the match, if we're host
            if (isHost)
            {
                Plugin.client.UpdateMatch(Match);

                var loadSong = new LoadSong();
                loadSong.levelId = Match.CurrentlySelectedLevel.LevelId;

                Action<IBeatmapLevel> callback = (loadedLevel) =>
                {
                    //Send updated download status
                    (Plugin.client.Self as Player).CurrentDownloadState = Player.DownloadState.Downloaded;

                    var playerUpdate = new Event();
                    playerUpdate.eventType = Event.EventType.PlayerUpdated;
                    playerUpdate.changedObject = Plugin.client.Self;
                    Plugin.client.Send(new Packet(playerUpdate));

                    //We don't want to recieve this since it would cause an infinite song loading loop.
                    //Our song is already loaded inherently since we're selecting it as the host
                    Plugin.client.Send(Match.Players.Except(new Player[] { Plugin.client.Self as Player }).Select(x => x.Guid).ToArray(), new Packet(loadSong));
                };

                //Load the song ourself
                LoadSongAsHost(loadSong, callback);
            }
        }

        public void SwitchBeatmapSelection(IDifficultyBeatmap beatmap)
        {
            Match.CurrentlySelectedCharacteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.CurrentlySelectedDifficulty = (SharedConstructs.BeatmapDifficulty)beatmap.difficulty;

            if (isHost)
            {
                Plugin.client.UpdateMatch(Match);
            }
        }
    }
}
