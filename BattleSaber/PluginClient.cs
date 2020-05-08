using System;
using System.Linq;
using BattleSaber.Behaviors;
using BattleSaber.Misc;
using BattleSaber.Utilities;
using BattleSaberShared;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using UnityEngine;
using static BattleSaberShared.Models.GameplayModifiers;
using static BattleSaberShared.Packet;
using Logger = BattleSaberShared.Logger;

namespace BattleSaber
{
    public class PluginClient : BattleSaberClient
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

                PlaySong?.Invoke(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerData.playerSpecificSettings, playerData.overrideEnvironmentSettings, colorScheme, playSong.floatingScoreboard, playSong.streamSync);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    if (InGameSyncHandler.Instance != null) InGameSyncHandler.Instance.ClearBackground();
                    if ((Self as Player).CurrentPlayState == Player.PlayState.InGame) PlayerUtils.ReturnToMenu();
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
                    (Self as Player).CurrentDownloadState = Player.DownloadState.Downloaded;

                    var playerUpdate = new Event();
                    playerUpdate.Type = Event.EventType.PlayerUpdated;
                    playerUpdate.ChangedObject = Self;
                    Send(new Packet(playerUpdate));

                    //Notify any listeners of the client that a song has been loaded
                    LoadedSong?.Invoke(loadedLevel);

                    Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdate.ChangedObject as Player).CurrentDownloadState}");
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
                                (Self as Player).CurrentDownloadState = Player.DownloadState.DownloadError;

                                var playerUpdated = new Event();
                                playerUpdated.Type = Event.EventType.PlayerUpdated;
                                playerUpdated.ChangedObject = Self;

                                Send(new Packet(playerUpdated));

                                Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdated.ChangedObject as Player).CurrentDownloadState}");
                            }
                        };

                        (Self as Player).CurrentDownloadState = Player.DownloadState.Downloading;

                        var playerUpdate = new Event();
                        playerUpdate.Type = Event.EventType.PlayerUpdated;
                        playerUpdate.ChangedObject = Self;
                        Send(new Packet(playerUpdate));

                        Logger.Debug($"SENT DOWNLOAD SIGNAL {(playerUpdate.ChangedObject as Player).CurrentDownloadState}");

                        SongDownloader.DownloadSong(loadSong.levelId, songDownloaded: loadSongAction, downloadProgressChanged: (progress) => Logger.Debug($"DOWNLOAD PROGRESS: {progress}"));
                    }
                }
            }
        }
    }
}