using HMUI;
using System;
using System.Linq;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.UI;
using TournamentAssistant.Misc;
using System.Collections.Generic;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }

        public event Action DidFinishEvent;

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;

        private StandardLevelDetailViewController _detailViewController;

        private IPreviewBeatmapLevel selectedLevel;
        private IDifficultyBeatmap selectedBeatmap;
        private bool isHost;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Room Screen";
                showBackButton = true;

                _songSelection = _songSelection ?? BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += songSelection_SongSelected;

                _songSelection.SetSongs(SongUtils.masterLevelList);

                _splashScreen = _splashScreen ?? BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = "Waiting for the host to select a song...";

                isHost = Match.Leader == Plugin.client.Self;
                if (isHost)
                {
                    ProvideInitialViewControllers(_songSelection);
                }
                else
                {
                    ProvideInitialViewControllers(_splashScreen);
                }

                Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
                Plugin.client.LoadedSong += Client_LoadedSong;
                Plugin.client.PlaySong += Client_PlaySong;
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                if (_detailViewController && isHost)
                {
                    _detailViewController.didPressPlayButtonEvent -= detailViewController_didPressPlayButtonEvent;
                    _detailViewController.didChangeDifficultyBeatmapEvent -= detailViewController_didChangeDifficultyBeatmapEvent;
                }

                Plugin.client.MatchInfoUpdated -= Client_MatchInfoUpdated;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.PlaySong -= Client_PlaySong;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }

        private void Client_MatchInfoUpdated(Match match)
        {
            if (!isHost)
            {
                if (match.CurrentlySelectedDifficulty != Match.CurrentlySelectedDifficulty || match.CurrentlySelectedCharacteristic != Match.CurrentlySelectedCharacteristic)
                {
                    
                }
            }

            Match = match;
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
            var gm = new TournamentAssistantShared.Models.GameplayModifiers();

            var playSong = new PlaySong();
            playSong.characteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == controller.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            playSong.difficulty = (SharedConstructs.BeatmapDifficulty)controller.selectedDifficultyBeatmap.difficulty;
            playSong.gameplayModifiers = gm;
            playSong.playerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings();
            playSong.levelId = Match.CurrentlySelectedLevel.LevelId;

            Plugin.client.Send(Match.Players.Select(x => x.Guid).ToArray(), new Packet(playSong));
        }

        private void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useSync = false)
        {
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
            selectedLevel = level;

            if (_detailViewController == null)
            {
                _detailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                if (isHost)
                {
                    _detailViewController.didPressPlayButtonEvent += detailViewController_didPressPlayButtonEvent;
                    _detailViewController.didChangeDifficultyBeatmapEvent += detailViewController_didChangeDifficultyBeatmapEvent;
                }
            }

            //_detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_playButton").gameObject.SetActive(false);
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

                //We don't want to recieve this since it would cause an infinite song loading loop.
                //Our song is already loaded inherently since we're selecting it as the host
                Plugin.client.Send(Match.Players.Except(new Player[] { Plugin.client.Self as Player }).Select(x => x.Guid).ToArray(), new Packet(loadSong));
            }
        }

        public void SwitchBeatmapSelection(IDifficultyBeatmap beatmap)
        {
            selectedBeatmap = beatmap;

            Match.CurrentlySelectedCharacteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.CurrentlySelectedDifficulty = (SharedConstructs.BeatmapDifficulty)beatmap.difficulty;

            if (isHost)
            {
                Plugin.client.UpdateMatch(Match);
            }
        }
    }
}
