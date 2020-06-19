using System;
using System.Linq;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Misc;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using static TournamentAssistantShared.Packet;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant
{
    public class PluginClient : SystemClient
    {
        public event Action<IBeatmapLevel> LoadedSong;
        public event Action<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers, PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool, bool, bool, bool> PlaySong;

        public PluginClient(string endpoint, int port, string username, string userId) : base(endpoint, port, username, Connect.ConnectTypes.Player, userId) {}

        protected override void Client_PacketRecieved(Packet packet)
        {
            base.Client_PacketRecieved(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;

                var desiredLevel = SongUtils.masterLevelList.First(x => x.levelID == playSong.Beatmap.LevelId);
                var desiredCharacteristic = desiredLevel.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == playSong.Beatmap.Characteristic.SerializedName).beatmapCharacteristic ?? desiredLevel.previewDifficultyBeatmapSets.First().beatmapCharacteristic;
                var desiredDifficulty = (BeatmapDifficulty)playSong.Beatmap.Difficulty;

                var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
                var playerSettings = playerData.playerSpecificSettings;

                //Override defaults if we have forced options enabled
                if (playSong.PlayerSettings.Options != PlayerOptions.None)
                {
                    playerSettings = new PlayerSpecificSettings();
                    playerSettings.leftHanded = playSong.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded);
                    playerSettings.staticLights = playSong.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights);
                    playerSettings.noTextsAndHuds = playSong.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud);
                    playerSettings.advancedHud = playSong.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud);
                    playerSettings.reduceDebris = playSong.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris);
                }

                var gameplayModifiers = new GameplayModifiers();
                gameplayModifiers.batteryEnergy = playSong.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy);
                gameplayModifiers.disappearingArrows = playSong.GameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows);
                gameplayModifiers.failOnSaberClash = playSong.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash);
                gameplayModifiers.fastNotes = playSong.GameplayModifiers.Options.HasFlag(GameOptions.FastNotes);
                gameplayModifiers.ghostNotes = playSong.GameplayModifiers.Options.HasFlag(GameOptions.GhostNotes);
                gameplayModifiers.instaFail = playSong.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail);
                gameplayModifiers.noBombs = playSong.GameplayModifiers.Options.HasFlag(GameOptions.NoBombs);
                gameplayModifiers.noFail = playSong.GameplayModifiers.Options.HasFlag(GameOptions.NoFail);
                gameplayModifiers.noObstacles = playSong.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles);
                gameplayModifiers.noArrows = playSong.GameplayModifiers.Options.HasFlag(GameOptions.NoArrows);

                if (playSong.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Slower;
                if (playSong.GameplayModifiers.Options.HasFlag(GameOptions.FastSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Faster;

                var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

                PlaySong?.Invoke(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerSettings, playerData.overrideEnvironmentSettings, colorScheme, playSong.FloatingScoreboard, playSong.StreamSync, playSong.DisablePause, playSong.DisableFail);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.CommandType == Command.CommandTypes.ReturnToMenu)
                {
                    if (SyncHandler.Instance != null) ScreenOverlay.Instance.Clear();
                    if ((Self as Player).PlayState == Player.PlayStates.InGame) PlayerUtils.ReturnToMenu();
                }
                else if (command.CommandType == Command.CommandTypes.ScreenOverlay_ShowPng)
                {
                    ScreenOverlay.Instance.ShowPng();
                }
                else if (command.CommandType == Command.CommandTypes.DelayTest_Finish)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        ScreenOverlay.Instance.Clear();
                        SyncHandler.Instance.Resume();
                        SyncHandler.Destroy();
                    });
                }
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                LoadSong loadSong = packet.SpecificPacket as LoadSong;

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    //Send updated download status
                    (Self as Player).DownloadState = Player.DownloadStates.Downloaded;

                    var playerUpdate = new Event();
                    playerUpdate.Type = Event.EventType.PlayerUpdated;
                    playerUpdate.ChangedObject = Self;
                    Send(new Packet(playerUpdate));

                    //Notify any listeners of the client that a song has been loaded
                    LoadedSong?.Invoke(loadedLevel);

                    Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdate.ChangedObject as Player).DownloadState}");
                };

                if (OstHelper.IsOst(loadSong.LevelId))
                {
                    SongLoaded?.Invoke(SongUtils.masterLevelList.First(x => x.levelID == loadSong.LevelId) as BeatmapLevelSO);
                }
                else
                {
                    if (SongUtils.masterLevelList.Any(x => x.levelID == loadSong.LevelId))
                    {
                        SongUtils.LoadSong(loadSong.LevelId, SongLoaded);
                    }
                    else
                    {
                        Action<bool> loadSongAction = (succeeded) =>
                        {
                            if (succeeded)
                            {
                                SongUtils.LoadSong(loadSong.LevelId, SongLoaded);
                            }
                            else
                            {
                                (Self as Player).DownloadState = Player.DownloadStates.DownloadError;

                                var playerUpdated = new Event();
                                playerUpdated.Type = Event.EventType.PlayerUpdated;
                                playerUpdated.ChangedObject = Self;

                                Send(new Packet(playerUpdated));

                                Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdated.ChangedObject as Player).DownloadState}");
                            }
                        };

                        (Self as Player).DownloadState = Player.DownloadStates.Downloading;

                        var playerUpdate = new Event();
                        playerUpdate.Type = Event.EventType.PlayerUpdated;
                        playerUpdate.ChangedObject = Self;
                        Send(new Packet(playerUpdate));

                        Logger.Debug($"SENT DOWNLOAD SIGNAL {(playerUpdate.ChangedObject as Player).DownloadState}");

                        SongDownloader.DownloadSong(loadSong.LevelId, songDownloaded: loadSongAction, downloadProgressChanged: (progress) => Logger.Debug($"DOWNLOAD PROGRESS: {progress}"));
                    }
                }
            }
            else if (packet.Type == PacketType.File)
            {
                File file = packet.SpecificPacket as File;
                if (file.Intention == File.Intentions.SetPngToShowWhenTriggered)
                {
                    var pngBytes = file.Compressed ? CompressionUtils.Decompress(file.Data) : file.Data;
                    ScreenOverlay.Instance.SetPngBytes(pngBytes);
                }
                else if (file.Intention == File.Intentions.ShowPngImmediately)
                {
                    var pngBytes = file.Compressed ? CompressionUtils.Decompress(file.Data) : file.Data;
                    ScreenOverlay.Instance.SetPngBytes(pngBytes);
                    ScreenOverlay.Instance.ShowPng();
                }

                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id,
                    Type = Acknowledgement.AcknowledgementType.FileDownloaded
                }));
            }
        }
    }
}