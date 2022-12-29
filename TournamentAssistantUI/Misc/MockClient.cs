using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Stopwatch songTimeElapsed;
        private int notesElapsed;
        private int multiplier;
        private Guid[] otherPlayersInMatch;
        private PreviewBeatmapLevel lastLoadedLevel;
        private Beatmap currentlyPlayingMap;
        private DownloadedSong currentlyPlayingSong;
        private int currentScore;
        private int currentCombo;
        private int currentMaxScore;
        private int currentMisses;

        private static readonly Random random = new();

        public MockClient(string endpoint, int port, string username, string userId = "0") : base(endpoint, port, username, User.ClientTypes.Player, userId)
        {
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

            var match = State.Matches.First(x => x.AssociatedUsers.Contains(Self.Guid));
            otherPlayersInMatch = match.AssociatedUsers.Where(x => GetUserByGuid(x).ClientType != User.ClientTypes.Player).Select(Guid.Parse).ToArray();

            currentlyPlayingMap = map;
            currentlyPlayingSong = new DownloadedSong(HashFromLevelId(map.LevelId));
            currentScore = 0;
            currentCombo = 0;
            currentMaxScore = 0;
            currentMisses = 0;
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
                //Interval = 60 * 3 * 1000
                Interval = 10 * 1000
            };
            songTimer.Elapsed += SongTimer_Elapsed;

            songTimeElapsed = new Stopwatch();

            noteTimer = new Timer
            {
                AutoReset = false,
                Interval = 500
            };
            noteTimer.Elapsed += NoteTimer_Elapsed;

            noteTimer.Start();
            songTimer.Start();
            songTimeElapsed.Start();

            var player = State.Users.FirstOrDefault(x => x.UserEquals(Self));
            player.PlayState = User.PlayStates.InGame;
            multiplier = 1;

            var playerUpdate = new Event
            {
                user_updated_event = new Event.UserUpdatedEvent
                {
                    User = player
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
            notesElapsed++;

            // 0.5% chance to miss a note
            if (random.Next(1, 200) == 1)
            {
                currentCombo = 0;
                currentMisses++;
                if (multiplier > 1) multiplier /= 2;
            }
            else
            {
                // Handle multiplier like the game does
                if (currentCombo >= 1 && currentCombo < 5)
                {
                    if (multiplier < 2) multiplier = 2;
                }
                else if (currentCombo >= 5 && currentCombo < 13)
                {
                    if (multiplier < 4) multiplier = 4;
                }
                else if (currentCombo >= 13)
                {
                    multiplier = 8;
                }

                currentScore += random.Next(100, 115) * multiplier;
                currentCombo += 1;
            }

            currentMaxScore = currentlyPlayingSong.GetMaxScore(notesElapsed);

            //NOTE: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator

            //TODO: Temporarily disabled for the sake of getting 0.6.7 out quickly
            Send(otherPlayersInMatch, new Packet
            {
                Push = new Push
                {
                    realtime_score = new Push.RealtimeScore
                    {
                        UserGuid = Self.Guid,
                        Score = currentScore,
                        ScoreWithModifiers = currentScore,
                        MaxScore = currentMaxScore,
                        MaxScoreWithModifiers = currentScore,
                        Combo = currentCombo,
                        PlayerHealth = 100,
                        Accuracy = currentScore / (float)currentMaxScore,
                        SongPosition = (float)songTimeElapsed.Elapsed.TotalSeconds,
                        scoreTracker = new ScoreTracker
                        {
                            //TODO: Proper real-looking random numbers
                            notesMissed = currentMisses,
                            badCuts = currentMisses,
                            bombHits = currentMisses,
                            wallHits = currentMisses,
                            maxCombo = currentCombo,
                            leftHand = new ScoreTrackerHand
                            {
                                Hit = currentCombo,
                                Miss = currentMisses,
                                badCut = currentMisses,
                                avgCuts = new float[] { 1, 1, 1 },
                            },
                            rightHand = new ScoreTrackerHand
                            {
                                Hit = currentCombo,
                                Miss = currentMisses,
                                badCut = currentMisses,
                                avgCuts = new float[] { 1, 1, 1 },
                            }
                        }
                    }
                }
            });

            //Random distance to next note
            if (noteTimer != null)
            {
                noteTimer.Interval = random.Next(480, 600);
                noteTimer.Start();
            }
        }

        private void SongTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var player = State.Users.FirstOrDefault(x => x.UserEquals(Self));

            noteTimer.Stop();
            noteTimer.Elapsed -= SongTimer_Elapsed;
            noteTimer.Dispose();
            noteTimer = null;

            songTimer.Stop();
            songTimer.Elapsed -= SongTimer_Elapsed;
            songTimer.Dispose();
            songTimer = null;

            songTimeElapsed.Stop();

            //Logger.Debug($"SENDING RESULTS: {player.Score}");

            Send(new Packet
            {
                Push = new Push
                {
                    song_finished = new Push.SongFinished
                    {
                        Type = Push.SongFinished.CompletionType.Passed,
                        Player = player,
                        Beatmap = currentlyPlayingMap,
                        Score = currentScore,
                    }
                }
            });

            player.PlayState = User.PlayStates.Waiting;
            var playerUpdate = new Event
            {
                user_updated_event = new Event.UserUpdatedEvent
                {
                    User = player
                }
            };

            Send(new Packet
            {
                Event = playerUpdate
            });

            currentlyPlayingMap = null;
            currentlyPlayingSong = null;
            currentMaxScore = 0;
        }

        protected override async Task Client_PacketReceived(Packet packet)
        {
            await base.Client_PacketReceived(packet);

            if (Self == null) return;
            var player = State.Users.FirstOrDefault(x => x.UserEquals(Self));

            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.ReturnToMenu)
                {
                    if (player.PlayState == User.PlayStates.InGame) ReturnToMenu?.Invoke();
                }
                else if (command.TypeCase == Command.TypeOneofCase.play_song)
                {
                    var playSong = command.play_song;

                    PlaySong?.Invoke(playSong.GameplayParameters.Beatmap);
                }
                else if (command.TypeCase == Command.TypeOneofCase.load_song)
                {
                    var loadSong = command.load_song;

                    //Send updated download status
                    player.DownloadState = User.DownloadStates.Downloading;

                    var playerUpdate = new Event
                    {
                        user_updated_event = new Event.UserUpdatedEvent
                        {
                            User = player
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
                                player.DownloadState = User.DownloadStates.Downloaded;

                                var playerUpdate = new Event
                                {
                                    user_updated_event = new Event.UserUpdatedEvent
                                    {
                                        User = player
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
                                player.DownloadState = User.DownloadStates.DownloadError;

                                var playerUpdate = new Event
                                {
                                    user_updated_event = new Event.UserUpdatedEvent
                                    {
                                        User = player
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
        }

        private static string HashFromLevelId(string levelId) => levelId.Replace("custom_level_", "").ToLower();
    }
}
