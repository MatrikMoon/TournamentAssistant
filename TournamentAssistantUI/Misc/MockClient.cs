using TournamentAssistantShared;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI.Misc
{
    public class MockClient : SystemClient
    {
        private event Action<PreviewBeatmapLevel> LoadedSong;
        private event Action<Beatmap> PlaySong;
        private event Action ReturnToMenu;

        private Timer songTimer;
        private Timer noteTimer;
        private string[] otherPlayersInMatch;
        private PreviewBeatmapLevel lastLoadedLevel;
        private Beatmap currentlyPlayingMap;
        private DownloadedSong currentlyPlayingSong;

        private static readonly Random random = new Random();

        public MockClient(string endpoint, int port, string username) : base(endpoint, port, username, Connect.ConnectType.Player) {
            LoadedSong += MockClient_LoadedSong;
            PlaySong += MockClient_PlaySong;
            ReturnToMenu += MockClient_ReturnToMenu;
        }

        private void MockClient_LoadedSong(PreviewBeatmapLevel level)
        {
            lastLoadedLevel = level;
        }

        private void MockClient_PlaySong(Beatmap map)
        {
            if (OstHelper.IsOst(map.levelId)) return;

            var match = State.Matches.First(x => x.Players.Contains(Self));
            otherPlayersInMatch = match.Players.Select(x => x.Guid).Union(new string[] { match.Leader.Guid }).ToArray();

            currentlyPlayingMap = map;
            currentlyPlayingSong = new DownloadedSong(HashFromLevelId(map.levelId));

            /*using (var libVLC = new LibVLC())
            {
                var media = new Media(libVLC, currentlyPlayingSong.GetAudioPath(), FromType.FromPath);
                await media.Parse();

                songTimer = new Timer();
                songTimer.AutoReset = false;
                songTimer.Interval = media.Duration;
                songTimer.Elapsed += SongTimer_Elapsed;

                noteTimer = new Timer();
                noteTimer.AutoReset = false;
                noteTimer.Interval = 500;
                noteTimer.Elapsed += NoteTimer_Elapsed;

                noteTimer.Start();
                songTimer.Start();
            }*/

            songTimer = new Timer();
            songTimer.AutoReset = false;
            songTimer.Interval = 60 * 3 * 1000;
            songTimer.Elapsed += SongTimer_Elapsed;

            noteTimer = new Timer();
            noteTimer.AutoReset = false;
            noteTimer.Interval = 500;
            noteTimer.Elapsed += NoteTimer_Elapsed;

            noteTimer.Start();
            songTimer.Start();

            (Self as Player).CurrentPlayState = Player.PlayState.InGame;
            var playerUpdated = new Event();
            playerUpdated.Type = Event.EventType.PlayerUpdated;
            playerUpdated.ChangedObject = Self;
            Send(new Packet(playerUpdated));
        }

        private void MockClient_ReturnToMenu()
        {
            if (songTimer != null) SongTimer_Elapsed(null, null);
        }

        private void NoteTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Send score update
            (Self as Player).CurrentScore += random.Next(0, 115);
            var playerUpdate = new Event();
            playerUpdate.Type = Event.EventType.PlayerUpdated;
            playerUpdate.ChangedObject = Self;

            //NOTE:/TODO: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator
            Send(otherPlayersInMatch, new Packet(playerUpdate));

            //Random distance to next note
            noteTimer.Interval = random.Next(480, 600);
            noteTimer.Start();
        }

        private void SongTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            noteTimer.Stop();
            noteTimer.Elapsed -= SongTimer_Elapsed;
            noteTimer.Dispose();
            noteTimer = null;

            songTimer.Stop();
            songTimer.Elapsed -= SongTimer_Elapsed;
            songTimer.Dispose();
            songTimer = null;

            currentlyPlayingMap = null;
            currentlyPlayingSong = null;

            Logger.Debug($"SENDING RESULTS: {(Self as Player).CurrentScore}");

            var songFinished = new SongFinished();
            songFinished.Type = SongFinished.CompletionType.Passed;
            songFinished.User = Self;
            songFinished.Map = currentlyPlayingMap;
            songFinished.Score = (Self as Player).CurrentScore;
            Send(new Packet(songFinished));

            (Self as Player).CurrentPlayState = Player.PlayState.Waiting;
            var playerUpdated = new Event();
            playerUpdated.Type = Event.EventType.PlayerUpdated;
            playerUpdated.ChangedObject = Self;
            Send(new Packet(playerUpdated));
        }

        protected override void Client_PacketRecieved(Packet packet)
        {
            base.Client_PacketRecieved(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;
                PlaySong?.Invoke(playSong.beatmap);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.commandType == Command.CommandType.ReturnToMenu)
                {
                    if ((Self as Player).CurrentPlayState == Player.PlayState.InGame) ReturnToMenu?.Invoke();
                }
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                LoadSong loadSong = packet.SpecificPacket as LoadSong;

                //Send updated download status
                (Self as Player).CurrentDownloadState = Player.DownloadState.Downloading;

                var playerUpdate = new Event();
                playerUpdate.Type = Event.EventType.PlayerUpdated;
                playerUpdate.ChangedObject = Self;
                Send(new Packet(playerUpdate));

                var hash = HashFromLevelId(loadSong.levelId);
                BeatSaverDownloader.DownloadSongInfoThreaded(hash,
                    (successfulDownload) =>
                    {
                        if (successfulDownload)
                        {
                            var song = new DownloadedSong(hash);

                            var mapFormattedLevelId = $"custom_level_{hash.ToUpper()}";

                            var matchMap = new PreviewBeatmapLevel()
                            {
                                LevelId = mapFormattedLevelId,
                                Name = song.Name
                            };

                            List<Characteristic> characteristics = new List<Characteristic>();
                            foreach (var characteristic in song.Characteristics)
                            {
                                characteristics.Add(new Characteristic()
                                {
                                    SerializedName = characteristic,
                                    Difficulties = song.GetBeatmapDifficulties(characteristic)
                                });
                            }
                            matchMap.Characteristics = characteristics.ToArray();

                            //Send updated download status
                            (Self as Player).CurrentDownloadState = Player.DownloadState.Downloaded;

                            playerUpdate = new Event();
                            playerUpdate.Type = Event.EventType.PlayerUpdated;
                            playerUpdate.ChangedObject = Self;
                            Send(new Packet(playerUpdate));

                            LoadedSong?.Invoke(matchMap);

                            Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdate.ChangedObject as Player).CurrentDownloadState}");
                        }
                        else
                        {
                            //Send updated download status
                            (Self as Player).CurrentDownloadState = Player.DownloadState.DownloadError;

                            playerUpdate = new Event();
                            playerUpdate.Type = Event.EventType.PlayerUpdated;
                            playerUpdate.ChangedObject = Self;
                            Send(new Packet(playerUpdate));
                        }
                    }
                );
            }
        }

        private static string HashFromLevelId(string levelId) => levelId.Replace("custom_level_", "").ToLower();
    }
}
