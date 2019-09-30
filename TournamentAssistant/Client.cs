using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Packet;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant
{
    public class Client
    {
        public event Action ConnectedToServer;
        public event Action FailedToConnectToServer;
        public event Action<IBeatmapLevel> LoadedSong;

        public Player Self { get; set; }

        private Network.Client client;
        private Timer heartbeatTimer = new Timer();
        private string endpoint;
        private string username;

        public bool Connected {
            get => client?.Connected ?? false;
        }

        public Client(string endpoint, string username)
        {
            this.endpoint = endpoint;
            this.username = username;
        }

        public void Start()
        {
            ConnectToServer();

            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            heartbeatTimer.Start();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            try
            {
                var command = new Command();
                command.commandType = Command.CommandType.Heartbeat;
                Send(new Packet(command));
            }
            catch (Exception ___)
            {
                //Logger.Debug("HEARTBEAT FAILED");
                //Logger.Debug(e.ToString());

                ConnectToServer();
            }
        }

        private void ConnectToServer()
        {
            try
            {
                client = new Network.Client(endpoint, 10156);
                client.PacketRecieved += Client_PacketRecieved;
                client.ServerConnected += Client_ServerConnected;
                client.ServerFailedToConnect += Client_ServerFailedToConnect;
                client.ServerDisconnected += Client_ServerDisconnected;

                client.Start();
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
        }

        private void Client_ServerFailedToConnect()
        {
            FailedToConnectToServer?.Invoke();
        }

        private void Client_ServerConnected()
        {
            Send(new Packet(new Connect()
            {
                clientType = Connect.ConnectType.Player,
                name = username
            }));

            ConnectedToServer?.Invoke();
        }

        public void Shutdown()
        {
            if (client.Connected) client.Shutdown();
            heartbeatTimer.Stop();
        }

        private void Client_ServerDisconnected()
        {
            Logger.Debug("Server disconnected!");
        }

        private void Client_PacketRecieved(Packet packet)
        {
            if (packet.Type == PacketType.Event)
            {
                var @event = packet.SpecificPacket as Event;
                if (@event.eventType == Event.EventType.SetSelf)
                {
                    Self = @event.changedObject as Player;
                }
            }

            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).levelId + " : " + (packet.SpecificPacket as PlaySong).difficulty;
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                secondaryInfo = (packet.SpecificPacket as LoadSong).levelId;
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }
            else if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
            }

            Logger.Info($"Recieved: ({packet.Type}) ({secondaryInfo})");

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;
                var mapFormattedLevelId = $"custom_level_{playSong.levelId.ToUpper()}";

                var desiredLevel = OstHelper.IsOst(playSong.levelId) ? SongUtils.masterLevelList.First(x => x.levelID == playSong.levelId) : SongUtils.masterLevelList.First(x => x.levelID == mapFormattedLevelId);
                var desiredCharacteristic = desiredLevel.beatmapCharacteristics.First(x => x.serializedName == playSong.characteristic.SerializedName);
                var desiredDifficulty = (BeatmapDifficulty)playSong.difficulty;

                var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First().playerData;
                
                var gameplayModifiers = new GameplayModifiers();
                gameplayModifiers.batteryEnergy = playSong.gameplayModifiers.batteryEnergy;
                gameplayModifiers.disappearingArrows = playSong.gameplayModifiers.disappearingArrows;
                gameplayModifiers.failOnSaberClash = playSong.gameplayModifiers.failOnSaberClash;
                gameplayModifiers.fastNotes = playSong.gameplayModifiers.fastNotes;
                gameplayModifiers.ghostNotes = playSong.gameplayModifiers.ghostNotes;
                gameplayModifiers.instaFail = playSong.gameplayModifiers.instaFail;
                gameplayModifiers.noBombs = playSong.gameplayModifiers.noBombs;
                gameplayModifiers.noFail = playSong.gameplayModifiers.noFail;
                gameplayModifiers.noObstacles = playSong.gameplayModifiers.noObstacles;
                gameplayModifiers.songSpeed = (GameplayModifiers.SongSpeed)playSong.gameplayModifiers.songSpeed;

                //Reset score
                Logger.Info($"RESETTING SCORE: 0");
                Self.CurrentScore = 0;
                var playerUpdate = new Event();
                playerUpdate.eventType = Event.EventType.PlayerUpdated;
                playerUpdate.changedObject = Self;
                Send(new Packet(playerUpdate));

                Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> finishedCallback = (data, results) =>
                {
                    Logger.Info($"SENDING SCORE: {results.modifiedScore}");
                    Self.CurrentScore = results.modifiedScore;
                    var playerUpdated = new Event();
                    playerUpdated.eventType = Event.EventType.PlayerUpdated;
                    playerUpdated.changedObject = Self;
                    Send(new Packet(playerUpdated));

                    var matchFlowCoordinator = Resources.FindObjectsOfTypeAll<UI.FlowCoordinators.MatchFlowCoordinator>().First();
                    matchFlowCoordinator.SongFinished(data, results);
                };

                var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;
                SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, playerData.overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerData.playerSpecificSettings, finishedCallback);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    if (Self.CurrentPlayState == Player.PlayState.InGame) PlayerUtils.ReturnToMenu();
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.eventType)
                {
                    case Event.EventType.SetSelf:
                        Self = @event.changedObject as Player;
                        SongUtils.RefreshLoadedSongs();
                        break;
                    case Event.EventType.MatchUpdated:
                        break;
                    default:
                        Logger.Error($"Unknown command recieved!");
                        break;
                }
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                LoadSong loadSong = packet.SpecificPacket as LoadSong;
                var mapFormattedLevelId = $"custom_level_{loadSong.levelId.ToUpper()}";

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    //Send updated download status
                    Self.CurrentDownloadState = Player.DownloadState.Downloaded;

                    var playerUpdate = new Event();
                    playerUpdate.eventType = Event.EventType.PlayerUpdated;
                    playerUpdate.changedObject = Self;
                    Send(new Packet(playerUpdate));

                    //Notify any listeners of the client that a song has been loaded
                    LoadedSong?.Invoke(loadedLevel);

                    Logger.Info($"SENT DOWNLOADED SIGNAL {(playerUpdate.changedObject as Player).CurrentDownloadState}");
                };

                if (OstHelper.IsOst(loadSong.levelId))
                {
                    SongLoaded?.Invoke(SongUtils.masterLevelList.First(x => x.levelID == loadSong.levelId) as BeatmapLevelSO);
                }
                else
                {
                    if (SongUtils.masterLevelList.Any(x => x.levelID == mapFormattedLevelId))
                    {
                        SongUtils.LoadSong(mapFormattedLevelId, SongLoaded);
                    }
                    else
                    {
                        Action<bool> loadSongAction = (succeeded) =>
                        {
                            if (succeeded)
                            {
                                SongUtils.LoadSong(mapFormattedLevelId, SongLoaded);
                            }
                            else
                            {
                                Self.CurrentDownloadState = Player.DownloadState.DownloadError;

                                var playerUpdated = new Event();
                                playerUpdated.eventType = Event.EventType.PlayerUpdated;
                                playerUpdated.changedObject = Self;

                                Send(new Packet(playerUpdated));

                                Logger.Info($"SENT DOWNLOADED SIGNAL {(playerUpdated.changedObject as Player).CurrentDownloadState}");
                            }
                        };

                        Self.CurrentDownloadState = Player.DownloadState.Downloading;

                        var playerUpdate = new Event();
                        playerUpdate.eventType = Event.EventType.PlayerUpdated;
                        playerUpdate.changedObject = Self;
                        Send(new Packet(playerUpdate));

                        Logger.Info($"SENT DOWNLOAD SIGNAL {(playerUpdate.changedObject as Player).CurrentDownloadState}");

                        SongDownloader.DownloadSong(loadSong.levelId, songDownloaded: loadSongAction, downloadProgressChanged: (progress) => Logger.Info($"DOWNLOAD PROGRESS: {progress}"));
                    }
                }
            }
        }

        public void Send(string guid, Packet packet) => Send(new string[] { guid }, packet);

        public void Send(string[] guids, Packet packet)
        {
            var forwardedPacket = new ForwardedPacket();
            forwardedPacket.ForwardTo = guids;
            forwardedPacket.Type = packet.Type;
            forwardedPacket.SpecificPacket = packet.SpecificPacket;

            Send(new Packet(forwardedPacket));
        }

        public void Send(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo})");
            client.Send(packet.ToBytes());
        }

        public void SendSongList(List<IPreviewBeatmapLevel> levels)
        {
            if (client != null && client.Connected)
            {
                var subpacketList = new List<PreviewBeatmapLevel>();
                subpacketList.AddRange(
                    levels.Select(x =>
                    {
                        var level = new PreviewBeatmapLevel
                        {
                            LevelId = x.levelID,
                            Name = x.songName
                        };
                        return level;
                    })
                );

                var songList = new SongList();
                songList.Levels = subpacketList.ToArray();

                Self.SongList = songList;

                var updatePlayer = new Event();
                updatePlayer.eventType = Event.EventType.PlayerUpdated;
                updatePlayer.changedObject = Self;

                Send(new Packet(updatePlayer));
            }
            else Logger.Debug("Skipped sending songs because there is no server connected");
        }
    }
}