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
        private int notesElapsed;
        private int multiplier;
        private Guid[] otherPlayersInMatch;
        private PreviewBeatmapLevel lastLoadedLevel;
        private Beatmap currentlyPlayingMap;
        private DownloadedSong currentlyPlayingSong;
        private int currentMaxScore;

        private static readonly Random random = new Random();

        public MockClient(string endpoint, int port, string username, string userId = "0") : base(endpoint, port, username, Connect.ConnectTypes.Player, userId) {
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
            if (OstHelper.IsOst(map.LevelId)) return;

            var match = State.Matches.First(x => x.Players.Contains(Self));
            otherPlayersInMatch = match.Players.Select(x => x.Id).Union(new Guid[] { match.Leader.Id }).ToArray();

            currentlyPlayingMap = map;
            currentlyPlayingSong = new DownloadedSong(HashFromLevelId(map.LevelId));
            currentMaxScore = 0;
            notesElapsed = 0;

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

            (Self as Player).PlayState = Player.PlayStates.InGame;

            (Self as Player).Score = 0;
            (Self as Player).Combo = 0;
            (Self as Player).Accuracy = 0;
            (Self as Player).SongPosition = 0;
            multiplier = 1;

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
            notesElapsed++;

            // 0.5% chance to miss a note
            if (random.Next(1, 200) == 1)
            {
                (Self as Player).Combo = 0;
                if (multiplier > 1) multiplier /= 2;
            }
            else
            {
                var combo = (Self as Player).Combo;

                // Handle multiplier like the game does
                if (combo >= 1 && combo < 5)
                {
                    if (multiplier < 2) multiplier = 2;
                } else if (combo >= 5 && combo < 13)
                {
                    if (multiplier < 4) multiplier = 4;
                } else if (combo >= 13)
                {
                    multiplier = 8;
                }

                (Self as Player).Score += random.Next(100, 115) * multiplier;
                (Self as Player).Combo += 1;
            }

            currentMaxScore = currentlyPlayingSong.GetMaxScore(notesElapsed);

            (Self as Player).Accuracy = (Self as Player).Score / (float)currentMaxScore;
            (Self as Player).SongPosition += 1.345235f;
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
            currentMaxScore = 0;

            Logger.Debug($"SENDING RESULTS: {(Self as Player).Score}");

            var songFinished = new SongFinished();
            songFinished.Type = SongFinished.CompletionType.Passed;
            songFinished.User = Self as Player;
            songFinished.Beatmap = currentlyPlayingMap;
            songFinished.Score = (Self as Player).Score;
            Send(new Packet(songFinished));

            (Self as Player).PlayState = Player.PlayStates.Waiting;
            var playerUpdated = new Event();
            playerUpdated.Type = Event.EventType.PlayerUpdated;
            playerUpdated.ChangedObject = Self;
            Send(new Packet(playerUpdated));
        }

        protected override void Client_PacketReceived(Packet packet)
        {
            base.Client_PacketReceived(packet);

            if (packet.Type == PacketType.PlaySong)
            {
                PlaySong playSong = packet.SpecificPacket as PlaySong;
                PlaySong?.Invoke(playSong.GameplayParameters.Beatmap);
            }
            else if (packet.Type == PacketType.Command)
            {
                Command command = packet.SpecificPacket as Command;
                if (command.CommandType == Command.CommandTypes.ReturnToMenu)
                {
                    if ((Self as Player).PlayState == Player.PlayStates.InGame) ReturnToMenu?.Invoke();
                }
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                LoadSong loadSong = packet.SpecificPacket as LoadSong;

                //Send updated download status
                (Self as Player).DownloadState = Player.DownloadStates.Downloading;

                var playerUpdate = new Event();
                playerUpdate.Type = Event.EventType.PlayerUpdated;
                playerUpdate.ChangedObject = Self;
                Send(new Packet(playerUpdate));

                var hash = HashFromLevelId(loadSong.LevelId);
                TournamentAssistantShared.BeatSaver.BeatSaverDownloader.DownloadSongThreaded(hash,
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
                            (Self as Player).DownloadState = Player.DownloadStates.Downloaded;

                            playerUpdate = new Event();
                            playerUpdate.Type = Event.EventType.PlayerUpdated;
                            playerUpdate.ChangedObject = Self;
                            Send(new Packet(playerUpdate));

                            LoadedSong?.Invoke(matchMap);

                            Logger.Debug($"SENT DOWNLOADED SIGNAL {(playerUpdate.ChangedObject as Player).DownloadState}");
                        }
                        else
                        {
                            //Send updated download status
                            (Self as Player).DownloadState = Player.DownloadStates.DownloadError;

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
