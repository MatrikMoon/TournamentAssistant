using BattleSaber.Misc;
using BattleSaber.UI.ViewControllers;
using BattleSaber.Utilities;
using BattleSaberShared;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace BattleSaber.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }

        public event Action DidFinishEvent;

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;
        private PlayerList _playerList;
        private SongDetail _songDetail;

        private bool isHost;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                //Set up UI
                title = "Room Screen";
                showBackButton = true;

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += songSelection_SongSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += songDetail_didPressPlayButtonEvent;
                _songDetail.DifficultyBeatmapChanged += songDetail_didChangeDifficultyBeatmapEvent;

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }
            if (activationType == ActivationType.AddedToHierarchy)
            {
                isHost = Match.Leader == Plugin.client.Self;
                _songSelection.SetSongs(SongUtils.masterLevelList);
                _playerList.Players = Match.Players;
                _splashScreen.StatusText = "Waiting for the host to select a song...";

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
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.PlaySong -= Client_PlaySong;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else DidFinishEvent?.Invoke();
        }

        private void songSelection_SongSelected(IPreviewBeatmapLevel level)
        {
            //Load the song, then display the detail info
            SongUtils.LoadSong(level.levelID, (loadedLevel) =>
            {
                if (!_songDetail.isInViewControllerHierarchy)
                {
                    PresentViewController(_songDetail, () =>
                    {
                        _songDetail.SetHost(isHost);
                        _songDetail.SetSelectedSong(loadedLevel);
                    });
                }
                else
                {
                    _songDetail.SetHost(isHost);
                    _songDetail.SetSelectedSong(loadedLevel);
                }
            });

            //Tell the other players to download the song, if we're host
            if (isHost)
            {
                var loadSong = new LoadSong();
                loadSong.levelId = level.levelID;

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
            }
        }

        private void songDetail_didChangeDifficultyBeatmapEvent(IDifficultyBeatmap beatmap)
        {
            var level = beatmap.level;

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
            Match.CurrentlySelectedCharacteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.CurrentlySelectedDifficulty = (SharedConstructs.BeatmapDifficulty)beatmap.difficulty;

            if (isHost)
            {
                Plugin.client.UpdateMatch(Match);
            }
        }

        private void songDetail_didPressPlayButtonEvent(IBeatmapLevel _, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            var gm = new BattleSaberShared.Models.GameplayModifiers();

            var playSong = new PlaySong();
            playSong.beatmap = new Beatmap();
            playSong.beatmap.characteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName);
            playSong.beatmap.difficulty = (SharedConstructs.BeatmapDifficulty)difficulty;
            playSong.beatmap.levelId = Match.CurrentlySelectedLevel.LevelId;

            playSong.gameplayModifiers = gm;
            playSong.playerSettings = new BattleSaberShared.Models.PlayerSpecificSettings();

            playSong.floatingScoreboard = true;

            Plugin.client.Send(Match.Players.Select(x => x.Guid).ToArray(), new Packet(playSong));
        }

        private void Client_MatchInfoUpdated(Match match)
        {
            if (Match.Guid == match.Guid)
            {
                Match = match;
                _playerList.Players = match.Players;

                if (!isHost && _songDetail && _songDetail.isInViewControllerHierarchy && match.CurrentlySelectedLevel != null && match.CurrentlySelectedCharacteristic != null)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        //`CurrentlySelectedDifficulty` is reset by SetSelectedCharacteristic, so we save it here
                        //Usually this is intended behavior so that a new difficulty is selected
                        //when the new characteristic doesn't have a corresponding difficulty to the one
                        //that was previously selected. However... We don't want that here. Here, we
                        //know that the CurrentlySelectedDifficulty *should* be available on the new
                        //characteristic, if the coordinator/leader hasn't messed up, and often changes simultaneously
                        var selectedDifficulty = (int)match.CurrentlySelectedDifficulty;

                        _songDetail.SetSelectedCharacteristic(match.CurrentlySelectedCharacteristic.SerializedName);
                        _songDetail.SetSelectedDifficulty(selectedDifficulty);
                    });
                }
            }
        }

        private void Client_MatchDeleted(Match match)
        {
            //If the match is destroyed while we're in here, back out
            if (match == Match)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (_songDetail && _songDetail.isInViewControllerHierarchy) DismissViewController(_songDetail, immediately: true);
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

                    songSelection_SongSelected(level);
                };
                UnityMainThreadDispatcher.Instance().Enqueue(setData);
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
    }
}
