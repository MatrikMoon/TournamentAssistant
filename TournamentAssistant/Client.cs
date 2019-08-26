using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistant
{
    public class Client
    {
        public Player Self { get; set; }

        private Network.Client client;
        private Timer heartbeatTimer = new Timer();
        private string endpoint;
        private string username;

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
            catch (Exception e)
            {
                Logger.Debug("HEARTBEAT FAILED");
                Logger.Debug(e.ToString());

                ConnectToServer();
            }
        }

        private void ConnectToServer()
        {
            try
            {
                client = new Network.Client(endpoint, 10155);
                client.PacketRecieved += Client_PacketRecieved;
                client.ServerDisconnected += Client_ServerDisconnected;

                client.Start();

                Send(new Packet(new Connect()
                {
                    clientType = Connect.ConnectType.Player,
                    name = username
                }));
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
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

                var desiredLevel = Plugin.masterLevelList.First(x => x.levelID == playSong.levelId);
                var desiredCharacteristic = desiredLevel.beatmapCharacteristics.First(x => x.serializedName == playSong.characteristic.SerializedName);
                var desiredDifficulty = (BeatmapDifficulty)playSong.difficulty;

                var playerSpecificSettings = new PlayerSpecificSettings();
                playerSpecificSettings.advancedHud = playSong.playerSettings.advancedHud;
                playerSpecificSettings.leftHanded = playSong.playerSettings.leftHanded;
                playerSpecificSettings.noTextsAndHuds = playSong.playerSettings.noTextsAndHuds;
                playerSpecificSettings.reduceDebris = playSong.playerSettings.reduceDebris;
                playerSpecificSettings.staticLights = playSong.playerSettings.staticLights;

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

                Utilities.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerSpecificSettings);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    Utilities.ReturnToMenu();
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.eventType)
                {
                    case Event.EventType.SetSelf:
                        Self = @event.changedObject as Player;
                        //if (Plugin.masterLevelList != null) SendSongList(Plugin.masterLevelList);
                        break;
                    default:
                        Logger.Error($"Unknown command recieved!");
                        break;
                }
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                LoadSong loadSong = packet.SpecificPacket as LoadSong;

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    Logger.Debug("SONG LOADED");

                    var loadedSong = new LoadedSong();
                    var beatmapLevel = new PreviewBeatmapLevel();
                    beatmapLevel.Characteristics = loadedLevel.beatmapCharacteristics.ToList().Select(x => {
                        var characteristic = new Characteristic();
                        characteristic.SerializedName = x.serializedName;
                        characteristic.Difficulties =
                            loadedLevel.beatmapLevelData.difficultyBeatmapSets
                                .First(y => y.beatmapCharacteristic.serializedName == x.serializedName)
                                .difficultyBeatmaps.Select(y => y.difficulty).ToArray();

                        return characteristic;
                    }).ToArray();

                    beatmapLevel.LevelId = loadedLevel.levelID;
                    beatmapLevel.Name = loadedLevel.songName;
                    beatmapLevel.Loaded = true;

                    loadedSong.level = beatmapLevel;

                    client.Send(new Packet(loadedSong).ToBytes());
                };

                Utilities.LoadSong(loadSong.levelId, SongLoaded);
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

        private void Send(Packet packet)
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

                client.Send(new Packet(updatePlayer).ToBytes());
            }
            else Logger.Debug("Skipped sending songs because there is no server connected");
        }
    }
}