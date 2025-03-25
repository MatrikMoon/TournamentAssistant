using System.Diagnostics;
using System.Timers;
using TournamentAssistantShared;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Timer = System.Timers.Timer;

public class MockClient : TAClient
{
    private event Action ReturnToMenu;
    private event Action<Beatmap> LoadedSong;
    private event Action<Beatmap> PlaySong;

    private string SelectedTournamentName { get; set; }
    private string SelectedTournamentId { get; set; }
    private string SelectedMatchId { get; set; }

    private Timer songTimer;
    private Timer noteTimer;
    private Stopwatch songTimeElapsed;

    private int notesElapsed;
    private int multiplier;
    private Beatmap currentlyPlayingSong;
    private int currentScore;
    private int currentCombo;
    private int currentMaxScore;
    private int currentMisses;
    private int currentBadCuts;
    private int currentGoodCuts;

    private static readonly Random random = new();

    private static bool hasTakenLongTimeYet = true;
    private static bool hasDisconnectedYet = true;

    public MockClient(string endpoint, int port, string tournamentName) : base(endpoint, port)
    {
        LoadedSong += MockClient_LoadedSong;
        PlaySong += MockClient_PlaySong;
        ReturnToMenu += MockClient_ReturnToMenu;
        SelectedTournamentName = tournamentName;
    }

    public Task SubmitQualifierScore(string tournamentName, string qualifierName, string levelId, int multipliedScore, int modifiedScore, int maxPossibleScore, double accuracy, int notesMissed, int badCuts, int goodCuts, int maxCombo, bool fullCombo, bool isPlaceholder = false)
    {
        var tournament = StateManager.GetTournaments().First(x => x.Settings.TournamentName == tournamentName);
        var qualifier = tournament.Qualifiers.First(x => x.Name == qualifierName);
        var map = qualifier.QualifierMaps.First(x => x.GameplayParameters.Beatmap.LevelId == levelId);
        var user = StateManager.GetUser(tournament.Guid, StateManager.GetSelfGuid());

        return SendQualifierScore(tournament.Guid, qualifier.Guid, map, user.PlatformId, user.Name, multipliedScore, modifiedScore, maxPossibleScore, accuracy, notesMissed, badCuts, goodCuts, maxCombo, fullCombo, isPlaceholder);
    }

    private void MockClient_LoadedSong(Beatmap level)
    {
        currentlyPlayingSong = level;
    }

    private void MockClient_PlaySong(Beatmap _)
    {
        currentScore = 0;
        currentCombo = 0;
        currentMaxScore = 0;
        currentMisses = 0;
        currentBadCuts = 0;
        currentGoodCuts = 0;
        notesElapsed = 0;
        multiplier = 1;

        songTimer = new Timer
        {
            AutoReset = false,
            Interval = 1000 * 20
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

        Task.Run(async () =>
        {
            var user = StateManager.GetUser(SelectedTournamentId, StateManager.GetSelfGuid());
            user.PlayState = User.PlayStates.InGame;
            await UpdateUser(SelectedTournamentId, user);
        });
    }

    private void MockClient_ReturnToMenu()
    {
        if (songTimer != null) SongTimer_Elapsed(null, null);
    }

    private void NoteTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        notesElapsed++;

        // Mock mid-song disconnect
        if (notesElapsed > 5 && !hasDisconnectedYet)
        {
            hasDisconnectedYet = true;
            noteTimer.Stop();
            noteTimer.Elapsed -= SongTimer_Elapsed;
            noteTimer.Dispose();
            noteTimer = null;

            songTimer.Stop();
            songTimer.Elapsed -= SongTimer_Elapsed;
            songTimer.Dispose();
            songTimer = null;
            Shutdown();
        }

        // 0.5% chance to miss a note
        if (random.Next(1, 200) == 1)
        {
            currentCombo = 0;
            currentMisses++;
            if (multiplier > 1) multiplier /= 2;
        }
        else
        {
            currentGoodCuts++;

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

        currentMaxScore = DownloadedSong.GetMaxScore(notesElapsed);

        var currentMatch = StateManager.GetMatches(SelectedTournamentId).First(x => x.AssociatedUsers.Contains(StateManager.GetSelfGuid()));
        var otherPlayersInMatch = StateManager.GetUsers(SelectedTournamentId).Where(x => currentMatch.AssociatedUsers.Contains(x.Guid));

        // Sneakily save this for later
        SelectedMatchId = currentMatch.Guid;

        Task.Run(async () =>
        {
            await SendRealtimeScore(otherPlayersInMatch.Select(x => x.Guid).ToArray(), new RealtimeScore
            {
                UserGuid = StateManager.GetSelfGuid(),
                Score = currentScore,
                ScoreWithModifiers = currentScore,
                MaxScore = currentMaxScore,
                MaxScoreWithModifiers = currentScore,
                Combo = currentCombo,
                PlayerHealth = 100,
                Accuracy = currentScore / (float)currentMaxScore,
                SongPosition = (float)songTimeElapsed.Elapsed.TotalSeconds,

                //TODO: Proper real-looking random numbers
                NotesMissed = currentMisses,
                BadCuts = currentMisses,
                BombHits = currentMisses,
                WallHits = currentMisses,
                MaxCombo = currentCombo,
                LeftHand = new ScoreTrackerHand
                {
                    Hit = currentCombo,
                    Miss = currentMisses,
                    BadCut = currentMisses,
                    AvgCuts = new float[] { 1, 1, 1 },
                },
                RightHand = new ScoreTrackerHand
                {
                    Hit = currentCombo,
                    Miss = currentMisses,
                    BadCut = currentMisses,
                    AvgCuts = new float[] { 1, 1, 1 },
                }
            });
        });

        //Random distance to next note
        if (noteTimer != null)
        {
            noteTimer.Interval = random.Next(480, 600);
            noteTimer.Start();
        }
    }

    private async void SongTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        var user = StateManager.GetUser(SelectedTournamentId, StateManager.GetSelfGuid());
        user.PlayState = User.PlayStates.WaitingForCoordinator;
        await UpdateUser(SelectedTournamentId, user);

        await SendSongFinished(SelectedTournamentId, SelectedMatchId, user, currentlyPlayingSong.LevelId, currentlyPlayingSong.Difficulty, currentlyPlayingSong.Characteristic, Push.SongFinished.CompletionType.Passed, currentScore, currentMisses, currentBadCuts, currentGoodCuts, (float)songTimeElapsed.Elapsed.TotalSeconds);

        noteTimer.Stop();
        noteTimer.Elapsed -= SongTimer_Elapsed;
        noteTimer.Dispose();
        noteTimer = null;

        songTimer.Stop();
        songTimer.Elapsed -= SongTimer_Elapsed;
        songTimer.Dispose();
        songTimer = null;

        songTimeElapsed.Stop();

        currentMaxScore = 0;
    }

    protected override async Task Client_PacketReceived(Packet packet)
    {
        await base.Client_PacketReceived(packet);

        if (packet.packetCase == Packet.packetOneofCase.Command)
        {
            var command = packet.Command;
            if (command.ReturnToMenu)
            {
                var currentPlayer = StateManager.GetUser(SelectedTournamentId, StateManager.GetSelfGuid());
                if (currentPlayer.PlayState == User.PlayStates.InGame) ReturnToMenu?.Invoke();
            }
            else if (command.TypeCase == Command.TypeOneofCase.play_song)
            {
                var playSong = command.play_song;

                PlaySong?.Invoke(playSong.GameplayParameters.Beatmap);
            }
        }
        else if (packet.packetCase == Packet.packetOneofCase.Request)
        {
            var request = packet.Request;
            if (request.TypeCase == Request.TypeOneofCase.load_song)
            {
                var loadSong = request.load_song;

                // Update to Downloading status
                var user = StateManager.GetUser(SelectedTournamentId, StateManager.GetSelfGuid());
                user.DownloadState = User.DownloadStates.Downloading;
                await UpdateUser(SelectedTournamentId, user);

                // Wait random time to mock slow downloads
                if (!hasTakenLongTimeYet)
                {
                    hasTakenLongTimeYet = true;
                    await Task.Delay(1000 * 60);
                }
                else
                {
                    await Task.Delay(random.Next(3000) + 1000);
                }

                // Update to Downloaded status
                user.DownloadState = User.DownloadStates.Downloaded;
                await UpdateUser(SelectedTournamentId, user);

                await SendResponse([packet.From], new Response
                {
                    Type = Response.ResponseType.Success,
                    RespondingToPacketId = packet.Id,
                    load_song = new Response.LoadSong
                    {
                        LevelId = loadSong.LevelId
                    }
                });

                var targetSong = new Beatmap
                {
                    Characteristic = new Characteristic
                    {
                        SerializedName = "Standard"
                    },
                    Difficulty = 4,
                    LevelId = loadSong.LevelId,
                    Name = "Mock Song"
                };

                LoadedSong?.Invoke(targetSong);
            }
        }
        else if (packet.packetCase == Packet.packetOneofCase.Response)
        {
            var response = packet.Response;
            if (response.DetailsCase == Response.DetailsOneofCase.connect)
            {
                var connect = response.connect;
                if (response.Type == Response.ResponseType.Success)
                {
                    Logger.Info($"Connected to server, joining tournament: {SelectedTournamentName}");

                    SelectedTournamentId = connect.State.Tournaments.First(x => x.Settings.TournamentName == SelectedTournamentName).Guid;
                    await JoinTournament(SelectedTournamentId);
                }
                else
                {
                    Logger.Error($"Failed to connect to server {Endpoint}");
                }
            }
            else if (response.DetailsCase == Response.DetailsOneofCase.join)
            {
                var join = response.join;
                if (response?.Type == Response.ResponseType.Success)
                {
                    // Join the waiting for coordinator lobby
                    var user = StateManager.GetUser(SelectedTournamentId, StateManager.GetSelfGuid());
                    user.PlayState = User.PlayStates.WaitingForCoordinator;
                    await UpdateUser(SelectedTournamentId, user);
                }
            }
        }
    }
}