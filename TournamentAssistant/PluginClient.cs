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
        public event Action<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers, PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool, bool> PlaySong;

        public PluginClient(string endpoint, int port, string username, ulong userId) : base(endpoint, port, username, Connect.ConnectType.Player, userId) {}

        protected override void Client_PacketRecieved(Packet packet)
        {
            base.Client_PacketRecieved(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;

                var desiredLevel = SongUtils.masterLevelList.First(x => x.levelID == playSong.beatmap.levelId);
                var desiredCharacteristic = desiredLevel.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == playSong.beatmap.characteristic.SerializedName).beatmapCharacteristic ?? desiredLevel.previewDifficultyBeatmapSets.First().beatmapCharacteristic;
                var desiredDifficulty = (BeatmapDifficulty)playSong.beatmap.difficulty;

                var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
                var playerSettings = playerData.playerSpecificSettings;

                //Override defaults if we have forced options enabled
                if (playSong.playerSettings.Options != PlayerOptions.None)
                {
                    playerSettings = new PlayerSpecificSettings();
                    playerSettings.leftHanded = playSong.playerSettings.Options.HasFlag(PlayerOptions.LeftHanded);
                    playerSettings.staticLights = playSong.playerSettings.Options.HasFlag(PlayerOptions.StaticLights);
                    playerSettings.noTextsAndHuds = playSong.playerSettings.Options.HasFlag(PlayerOptions.NoHud);
                    playerSettings.advancedHud = playSong.playerSettings.Options.HasFlag(PlayerOptions.AdvancedHud);
                    playerSettings.reduceDebris = playSong.playerSettings.Options.HasFlag(PlayerOptions.ReduceDebris);
                }

                var gameplayModifiers = new GameplayModifiers();
                gameplayModifiers.batteryEnergy = playSong.gameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy);
                gameplayModifiers.disappearingArrows = playSong.gameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows);
                gameplayModifiers.failOnSaberClash = playSong.gameplayModifiers.Options.HasFlag(GameOptions.FailOnClash);
                gameplayModifiers.fastNotes = playSong.gameplayModifiers.Options.HasFlag(GameOptions.FastNotes);
                gameplayModifiers.ghostNotes = playSong.gameplayModifiers.Options.HasFlag(GameOptions.GhostNotes);
                gameplayModifiers.instaFail = playSong.gameplayModifiers.Options.HasFlag(GameOptions.InstaFail);
                gameplayModifiers.noBombs = playSong.gameplayModifiers.Options.HasFlag(GameOptions.NoBombs);
                gameplayModifiers.noFail = playSong.gameplayModifiers.Options.HasFlag(GameOptions.NoFail);
                gameplayModifiers.noObstacles = playSong.gameplayModifiers.Options.HasFlag(GameOptions.NoObstacles);

                if (playSong.gameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Slower;
                if (playSong.gameplayModifiers.Options.HasFlag(GameOptions.FastSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Faster;

                var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

                PlaySong?.Invoke(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerSettings, playerData.overrideEnvironmentSettings, colorScheme, playSong.floatingScoreboard, playSong.streamSync);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    if (InGameSyncHandler.Instance != null) InGameSyncHandler.Instance.ClearBackground();
                    if ((Self as Player).PlayState == Player.PlayStates.InGame) PlayerUtils.ReturnToMenu();
                }
                else if (command.commandType == Command.CommandType.DelayTest_Trigger)
                {
                    InGameSyncHandler.Instance.TriggerColorChange();
                }
                else if (command.commandType == Command.CommandType.DelayTest_Finish)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        InGameSyncHandler.Instance.Resume();
                        InGameSyncHandler.Destroy();
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

                if (OstHelper.IsOst(loadSong.levelId))
                {
                    SongLoaded?.Invoke(SongUtils.masterLevelList.First(x => x.levelID == loadSong.levelId) as BeatmapLevelSO);
                }
                else
                {
                    if (SongUtils.masterLevelList.Any(x => x.levelID == loadSong.levelId))
                    {
                        SongUtils.LoadSong(loadSong.levelId, SongLoaded);
                    }
                    else
                    {
                        Action<bool> loadSongAction = (succeeded) =>
                        {
                            if (succeeded)
                            {
                                SongUtils.LoadSong(loadSong.levelId, SongLoaded);
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

                        SongDownloader.DownloadSong(loadSong.levelId, songDownloaded: loadSongAction, downloadProgressChanged: (progress) => Logger.Debug($"DOWNLOAD PROGRESS: {progress}"));
                    }
                }
            }
            else if (packet.Type == PacketType.File)
            {
                File file = packet.SpecificPacket as File;
                if (file.Intention == File.Intentions.UseForStreamSync)
                {
                    var pngBytes = file.Compressed ? CompressionUtils.Decompress(file.Data) : file.Data;
                    if (InGameSyncHandler.Instance != null)
                    {
                        InGameSyncHandler.Instance.SetPngToUse(pngBytes);
                    }
                }
                else if (file.Intention == File.Intentions.UseForStreamFiller)
                {
                    var pngBytes = file.Compressed ? CompressionUtils.Decompress(file.Data) : file.Data;
                    if (InGameSyncHandler.Instance != null)
                    {
                        InGameSyncHandler.Instance.SetPngToUse(pngBytes);
                        InGameSyncHandler.Instance.TriggerColorChange();
                    }
                }
            }
        }
    }
}