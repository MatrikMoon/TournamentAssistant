using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Misc;
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
        public event Action ServerDisconnected;
        public event Action<IBeatmapLevel> LoadedSong;
        public event Action<IPreviewBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty, GameplayModifiers, PlayerSpecificSettings, OverrideEnvironmentSettings, ColorScheme, bool> PlaySong;
        public event Action<TournamentState> StateUpdated;
        public event Action<Match> MatchCreated;
        public event Action<Match> MatchDeleted;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Player> PlayerJoined;
        public event Action<Player> PlayerLeft;
        public event Action<Player> PlayerInfoUpdated;


        public Player Self { get; set; }
        public TournamentState State { get; set; }

        private Network.Client client;
        private Timer heartbeatTimer = new Timer();
        private string endpoint;
        private string username;

        public bool Connected
        {
            get => client?.Connected ?? false;
        }

        public Client(string endpoint, string username)
        {
            this.endpoint = endpoint;
            this.username = username;

            State = new TournamentState();
            State.Players = new Player[] { };
            State.Matches = new Match[] { };
            State.Coordinators = new MatchCoordinator[] { };
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
            ServerDisconnected?.Invoke();
        }

        private void Client_PacketRecieved(Packet packet)
        {
            #region Logging
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
            #endregion Logging

            Logger.Info($"Recieved: ({packet.Type}) ({secondaryInfo})");

            if (packet.Type == PacketType.TournamentState)
            {
                State = packet.SpecificPacket as TournamentState;
                StateUpdated?.Invoke(State);
            }
            else if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;
                var mapFormattedLevelId = $"custom_level_{playSong.levelId.ToUpper()}";

                var desiredLevel = OstHelper.IsOst(playSong.levelId) ? SongUtils.masterLevelList.First(x => x.levelID == playSong.levelId) : SongUtils.masterLevelList.First(x => x.levelID == mapFormattedLevelId);
                var desiredCharacteristic = desiredLevel.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == playSong.characteristic.SerializedName).beatmapCharacteristic ?? desiredLevel.previewDifficultyBeatmapSets.First().beatmapCharacteristic;
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

                var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

                PlaySong?.Invoke(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerData.playerSpecificSettings, playerData.overrideEnvironmentSettings, colorScheme, playSong.playWithStreamSync);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    if (InGameSyncController.Instance != null) InGameSyncController.Instance.ClearBackground();
                    if (Self.CurrentPlayState == Player.PlayState.InGame) PlayerUtils.ReturnToMenu();
                }
                else if (command.commandType == Command.CommandType.DelayTest_Trigger)
                {
                    InGameSyncController.Instance.TriggerColorChange();
                }
                else if (command.commandType == Command.CommandType.DelayTest_Finish)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        InGameSyncController.Instance.Resume();
                        InGameSyncController.Destroy();
                    });
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.eventType)
                {
                    case Event.EventType.CoordinatorAdded:
                        CoordinatorAdded(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        CoordinatorRemoved(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        MatchAdded(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        MatchUpdated(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        MatchRemoved(@event.changedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        PlayerAdded(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        PlayerUpdated(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        PlayerRemoved(@event.changedObject as Player);
                        break;
                    case Event.EventType.SetSelf:
                        Self = @event.changedObject as Player;
                        SongUtils.RefreshLoadedSongs();
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

        #region EventHandling
        private void PlayerAdded(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.Add(player);
            State.Players = newPlayers.ToArray();

            PlayerJoined?.Invoke(player);
        }

        public void PlayerUpdated(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Guid == player.Guid)] = player;
            State.Players = newPlayers.ToArray();

            PlayerInfoUpdated?.Invoke(player);
        }

        private void PlayerRemoved(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Guid == player.Guid);
            State.Players = newPlayers.ToArray();

            PlayerLeft?.Invoke(player);
        }

        private void CoordinatorAdded(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
        }

        private void CoordinatorRemoved(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Guid == coordinator.Guid);
            State.Coordinators = newCoordinators.ToArray();
        }

        private void MatchAdded(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.Add(match);
            State.Matches = newMatches.ToArray();

            MatchCreated?.Invoke(match);
        }

        public void MatchUpdated(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
            State.Matches = newMatches.ToArray();

            MatchInfoUpdated?.Invoke(match);
        }

        private void MatchRemoved(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();

            MatchDeleted?.Invoke(match);
        }
        #endregion EventHandling
    }
}