using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

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

        private static readonly Random random = new();

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

            var match = State.Matches.First(x => x.Players.Select(x => x.User).Contains(Self));
            otherPlayersInMatch = match.Players.Select(x => Guid.Parse(x.User.Id)).Union(new Guid[] { Guid.Parse(match.Leader.Id) }).ToArray();

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

            songTimer = new Timer
            {
                AutoReset = false,
                Interval = 60 * 3 * 1000
            };
            songTimer.Elapsed += SongTimer_Elapsed;

            noteTimer = new Timer
            {
                AutoReset = false,
                Interval = 500
            };
            noteTimer.Elapsed += NoteTimer_Elapsed;

            noteTimer.Start();
            songTimer.Start();

            var player = State.Players.FirstOrDefault(x => x.User.UserEquals(Self));
            player.PlayState = Player.PlayStates.InGame;
            player.Score = 0;
            player.Combo = 0;
            player.Accuracy = 0;
            player.SongPosition = 0;
            multiplier = 1;

            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };

            Send(new Packet
            {
                Event = playerUpdate
            });
        }

        private void MockClient_ReturnToMenu()
        {
            if (songTimer != null) SongTimer_Elapsed(null, null);
        }

        private void NoteTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var player = State.Players.FirstOrDefault(x => x.User.UserEquals(Self));

            notesElapsed++;

            // 0.5% chance to miss a note
            if (random.Next(1, 200) == 1)
            {
                player.Combo = 0;
                if (multiplier > 1) multiplier /= 2;
            }
            else
            {
                var combo = player.Combo;

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

                player.Score += random.Next(100, 115) * multiplier;
                player.Combo += 1;
            }

            currentMaxScore = currentlyPlayingSong.GetMaxScore(notesElapsed);

            player.Accuracy = player.Score / (float)currentMaxScore;
            player.SongPosition += 1.345235f;

            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };

            //NOTE:/TODO: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator
            Send(otherPlayersInMatch, new Packet
            {
                Event = playerUpdate
            });

            //Random distance to next note
            noteTimer.Interval = random.Next(480, 600);
            noteTimer.Start();
        }

        private void SongTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var player = State.Players.FirstOrDefault(x => x.User.UserEquals(Self));

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

            //Logger.Debug($"SENDING RESULTS: {player.Score}");

            var songFinished = new SongFinished
            {
                Type = SongFinished.CompletionType.Passed,
                Player = player,
                Beatmap = currentlyPlayingMap,
                Score = player.Score
            };
            Send(new Packet
            {
                SongFinished = songFinished
            });

            player.PlayState = Player.PlayStates.Waiting;
            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };
            Send(new Packet
            {
                Event = playerUpdate
            });
        }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);
            var player = State.Players.FirstOrDefault(x => x.User.UserEquals(Self));

            if (packet.packetCase == Packet.packetOneofCase.PlaySong)
            {
                var playSong = packet.PlaySong;
                PlaySong?.Invoke(playSong.GameplayParameters.Beatmap);
            }
            else if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.CommandType == Command.CommandTypes.ReturnToMenu)
                {
                    if (player.PlayState == Player.PlayStates.InGame) ReturnToMenu?.Invoke();
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.LoadSong)
            {
                var loadSong = packet.LoadSong;

                //Send updated download status
                player.DownloadState = Player.DownloadStates.Downloading;

                var playerUpdate = new Event
                {
                    player_updated_event = new Event.PlayerUpdatedEvent
                    {
                        Player = player
                    }
                };
                await Send(new Packet
                {
                    Event = playerUpdate
                });

                var hash = HashFromLevelId(loadSong.LevelId);
                BeatSaverDownloader.DownloadSong(hash,
                    (songDir) =>
                    {
                        if (songDir != null)
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
                                    Difficulties = song.GetBeatmapDifficulties(characteristic).Select(x => (int)x).ToArray()
                                });
                            }
                            matchMap.Characteristics.AddRange(characteristics.ToArray());

                            //Send updated download status
                            player.DownloadState = Player.DownloadStates.Downloaded;

                            var playerUpdate = new Event
                            {
                                player_updated_event = new Event.PlayerUpdatedEvent
                                {
                                    Player = player
                                }
                            };
                            Send(new Packet
                            {
                                Event = playerUpdate
                            });

                            LoadedSong?.Invoke(matchMap);

                            Logger.Debug($"SENT DOWNLOADED SIGNAL {player.DownloadState}");
                        }
                        else
                        {
                            //Send updated download status
                            player.DownloadState = Player.DownloadStates.DownloadError;

                            var playerUpdate = new Event
                            {
                                player_updated_event = new Event.PlayerUpdatedEvent
                                {
                                    Player = player
                                }
                            };
                            Send(new Packet
                            {
                                Event = playerUpdate
                            });
                        }
                    }
                );
            }
        }

        private static string HashFromLevelId(string levelId) => levelId.Replace("custom_level_", "").ToLower();
    }
}
