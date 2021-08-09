using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Misc;
using TournamentAssistantUI.UI.Forms;
using TournamentAssistantUI.UI.UserControls;
using Brushes = System.Windows.Media.Brushes;
using ComboBox = System.Windows.Controls.ComboBox;
using Point = System.Windows.Point;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for MatchPage.xaml
    /// </summary>
    public partial class MatchPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private Match _match;
        public Match Match
        {
            get
            {
                return _match;
            }
            set
            {
                _match = value;
                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));
            }
        }

        public string[] AvailableOSTs
        {
            get
            {
                var noneArray = new string[] { "None" }.ToList();
                noneArray.AddRange(OstHelper.packs[0].SongDictionary.Select(x => x.Value)
                    .Union(OstHelper.packs[1].SongDictionary.Select(x => x.Value))
                    .Union(OstHelper.packs[2].SongDictionary.Select(x => x.Value))
                    .Union(OstHelper.packs[3].SongDictionary.Select(x => x.Value)));
                return noneArray.ToArray();
            }
        }

        private double _loadSongButtonProgress;
        public double LoadSongButtonProgress
        {
            get
            {
                return _loadSongButtonProgress;
            }
            set
            {
                _loadSongButtonProgress = value;
                NotifyPropertyChanged(nameof(LoadSongButtonProgress));
            }
        }

        private bool _songLoading;
        public bool SongLoading
        {
            get
            {
                return _songLoading;
            }
            set
            {
                _songLoading = value;
                NotifyPropertyChanged(nameof(SongLoading));
            }
        }

        private List<SongFinished> _levelCompletionResults = new List<SongFinished>();
        public event Action AllPlayersFinishedSong;

        public MainPage MainPage{ get; set; }

        public ICommand LoadSong { get; }
        public ICommand PlaySong { get; }
        public ICommand PlaySongWithSync { get; }
        public ICommand PlaySongWithQRSync { get; }
        public ICommand PlaySongWithDualSync { get; }
        public ICommand PlaySongWithDelayedStart { get; }
        public ICommand CheckForBannedMods { get; }
        public ICommand ReturnToMenu { get; }
        public ICommand ClosePage { get; }
        public ICommand DestroyAndCloseMatch { get; }

        //Necessary for QR Sync
        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;
        private ResizableLocationSpecifier _resizableLocationSpecifier;
        private int sourceX = Screen.PrimaryScreen.Bounds.X;
        private int sourceY = Screen.PrimaryScreen.Bounds.Y;
        private Size size = Screen.PrimaryScreen.Bounds.Size;
        private CancellationTokenSource _syncCancellationToken;

        private bool _matchPlayersHaveDownloadedSong;
        private bool _matchPlayersAreInGame;

        private event Action PlayersDownloadedSong;
        private event Action PlayersAreInGame;

        public MatchPage(Match match, MainPage mainPage)
        {
            InitializeComponent();

            DataContext = this;

            Match = match;
            MainPage = mainPage;

            //Due to my inability to use a custom converter to successfully use DataBinding to accomplish this same goal,
            //we are left doing it this weird gross way
            SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = Match.SelectedLevel != null);

            //If the match info is updated, we need to catch that and show the changes on this page
            MainPage.Connection.MatchInfoUpdated += Connection_MatchInfoUpdated;

            //If the match is externally deleted, we need to close the page
            MainPage.Connection.MatchDeleted += Connection_MatchDeleted;

            //If player info is updated (ie: download state) we need to know it
            MainPage.Connection.PlayerInfoUpdated += Connection_PlayerInfoUpdated;

            //Let's get notified when a player finishes a song
            MainPage.Connection.PlayerFinishedSong += Connection_PlayerFinishedSong;

            //When all players finish a song, show the finished song dialog
            AllPlayersFinishedSong += MatchPage_AllPlayersFinishedSong;

            MatchBox.PlayerListBox.SelectionChanged += PlayerListBox_SelectionChanged;

            LoadSong = new CommandImplementation(LoadSong_Executed, LoadSong_CanExecute);
            PlaySong = new CommandImplementation(PlaySong_Executed, PlaySong_CanExecute);
            PlaySongWithSync = new CommandImplementation(PlaySongWithSync_Executed, (a) => PlaySong_CanExecute(a) && (MainPage.Connection.Self.Id == Guid.Empty || MainPage.Connection.Self.Name == "Moon" || MainPage.Connection.Self.Name == "Olaf"));
            PlaySongWithQRSync = new CommandImplementation(PlaySongWithQRSync_Executed, (a) => PlaySong_CanExecute(a) && (MainPage.Connection.Self.Id == Guid.Empty || MainPage.Connection.Self.Name == "Moon" || MainPage.Connection.Self.Name == "Olaf"));
            PlaySongWithDualSync = new CommandImplementation(PlaySongWithDualSync_Executed, PlaySong_CanExecute);
            PlaySongWithDelayedStart = new CommandImplementation(PlaySongWithDelayedStart_Executed, PlaySong_CanExecute);
            CheckForBannedMods = new CommandImplementation(CheckForBannedMods_Executed, (_) => true);
            ReturnToMenu = new CommandImplementation(ReturnToMenu_Executed, ReturnToMenu_CanExecute);
            ClosePage = new CommandImplementation(ClosePage_Executed, ClosePage_CanExecute);
            DestroyAndCloseMatch = new CommandImplementation(DestroyAndCloseMatch_Executed, (_) => true);
        }

        private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = Dispatcher.Invoke(async () =>
            {
                var result = await DialogHost.Show(new PlayerDialog(MatchBox.PlayerListBox.SelectedItem as Player, new CommandImplementation(KickPlayer_Executed)), "RootDialog");
            });
        }

        private void MatchPage_AllPlayersFinishedSong()
        {
            _ = Dispatcher.Invoke(async () =>
            {
                //If teams are enabled
                if (MainPage.Connection.State.ServerSettings.EnableTeams)
                {
                    await DialogHost.Show(new GameOverDialogTeams(_levelCompletionResults), "RootDialog");
                }
                else await DialogHost.Show(new GameOverDialog(_levelCompletionResults), "RootDialog");
            });
        }

        private void Connection_PlayerFinishedSong(SongFinished results)
        {
            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{results.User.Name} has scored {results.Score}\n")));

            if (Match.Players.Contains(results.User)) _levelCompletionResults.Add(results);

            var playersText = string.Empty;
            foreach (var matchPlayer in Match.Players) playersText += $"{matchPlayer.Name}, ";
            Logger.Debug($"{results.User.Name} FINISHED SONG, FOR A TOTAL OF {_levelCompletionResults.Count} FINISHED PLAYERS OUT OF {Match.Players.Length}");
            if (_levelCompletionResults.Count == Match.Players.Length)
            {
                AllPlayersFinishedSong?.Invoke();
            }
            /*using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "TournamentAssistant");
                System.IO.File.WriteAllText($"BK_QUALS_RESULTS_{results.User.UserId}_{results.Beatmap.LevelId}_{DateTime.Now.Ticks}.json", JsonConvert.SerializeObject(results));
                List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
                list.Add(new KeyValuePair<string, string>("score", results.Score.ToString()));
                list.Add(new KeyValuePair<string, string>("userId", results.User.UserId.ToString()));
                list.Add(new KeyValuePair<string, string>("map", results.Beatmap.LevelId));
                await client.PostAsync("https://cube.community/api/ta_scores", new FormUrlEncodedContent(list));
            }*/
        }

        private void Connection_PlayerInfoUpdated(Player player)
        {
            //If the updated player is part of our match 
            var index = Match.Players.ToList().FindIndex(x => x.Id == player.Id);
            if (index >= 0)
            {
                Match.Players[index] = player;

                //Check for potential events we'd need to fire
                var oldMatchPlayersHaveDownloadedSong = _matchPlayersHaveDownloadedSong;
                var oldMatchPlayersAreInGame = _matchPlayersAreInGame;

                _matchPlayersHaveDownloadedSong = Match.Players.All(x => x.DownloadState == Player.DownloadStates.Downloaded);
                _matchPlayersAreInGame = Match.Players.All(x => x.PlayState == Player.PlayStates.InGame);

                if (!oldMatchPlayersHaveDownloadedSong && _matchPlayersHaveDownloadedSong) PlayersDownloadedSong?.Invoke();
                if (!oldMatchPlayersAreInGame && _matchPlayersAreInGame) PlayersAreInGame?.Invoke();
            }
        }

        private void Connection_MatchInfoUpdated(Match updatedMatch)
        {
            if (updatedMatch == Match)
            {
                Match = updatedMatch;

                //If the Match has a song now, be super sure the song box is enabled
                if (Match.SelectedLevel != null) SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = true);
            }
        }

        private void Connection_MatchDeleted(Match deletedMatch)
        {
            if (deletedMatch.Guid == Match.Guid)
            {
                MainPage.Connection.MatchInfoUpdated -= Connection_MatchInfoUpdated;
                MainPage.Connection.MatchDeleted -= Connection_MatchDeleted;
                MainPage.Connection.PlayerFinishedSong -= Connection_PlayerFinishedSong;
                MainPage.Connection.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;

                Dispatcher.Invoke(() => ClosePage.Execute(this));
            }
        }

        private void KickPlayer_Executed(object parameter)
        {
            //Remove player from list
            var player = parameter as Player;
            var newPlayers = Match.Players.ToList();
            newPlayers.RemoveAt(newPlayers.IndexOf(player));
            Match.Players = newPlayers.ToArray();

            //Notify all the UI that needs to be notified, and propegate the info across the network
            NotifyPropertyChanged(nameof(Match));
            MainPage.Connection.UpdateMatch(Match);
        }

        private async void LoadSong_Executed(object obj)
        {
            SongLoading = true;

            //TODO: This got swapped around backwards somehow
            var songId = OstHelper.allLevels.FirstOrDefault(x => x.Value == SongUrlBox.Text).Key ?? GetSongIdFromUrl(SongUrlBox.Text);
            
            //var customHost = string.IsNullOrWhiteSpace(CustomSongHostBox.Text) ? null : CustomSongHostBox.Text;
            string customHost = null;

            if (OstHelper.IsOst(songId))
            {
                SongLoading = false;
                var matchMap = new PreviewBeatmapLevel()
                {
                    LevelId = songId,
                    Name = SongUrlBox.Text
                };
                matchMap.Characteristics = new Characteristic[]
                {
                    new Characteristic()
                    {
                        SerializedName = "Standard",
                        Difficulties = new SharedConstructs.BeatmapDifficulty[]
                        {
                            SharedConstructs.BeatmapDifficulty.Easy,
                            SharedConstructs.BeatmapDifficulty.Normal,
                            SharedConstructs.BeatmapDifficulty.Hard,
                            SharedConstructs.BeatmapDifficulty.Expert,
                            SharedConstructs.BeatmapDifficulty.ExpertPlus,
                        }
                    },
                    new Characteristic()
                    {
                        SerializedName = "NoArrows",
                        Difficulties = new SharedConstructs.BeatmapDifficulty[]
                        {
                            SharedConstructs.BeatmapDifficulty.Easy,
                            SharedConstructs.BeatmapDifficulty.Normal,
                            SharedConstructs.BeatmapDifficulty.Hard,
                            SharedConstructs.BeatmapDifficulty.Expert,
                            SharedConstructs.BeatmapDifficulty.ExpertPlus,
                        }
                    },
                    new Characteristic()
                    {
                        SerializedName = "OneSaber",
                        Difficulties = new SharedConstructs.BeatmapDifficulty[]
                        {
                            SharedConstructs.BeatmapDifficulty.Easy,
                            SharedConstructs.BeatmapDifficulty.Normal,
                            SharedConstructs.BeatmapDifficulty.Hard,
                            SharedConstructs.BeatmapDifficulty.Expert,
                            SharedConstructs.BeatmapDifficulty.ExpertPlus,
                        }
                    },
                    new Characteristic()
                    {
                        SerializedName = "90Degree",
                        Difficulties = new SharedConstructs.BeatmapDifficulty[]
                        {
                            SharedConstructs.BeatmapDifficulty.Easy,
                            SharedConstructs.BeatmapDifficulty.Normal,
                            SharedConstructs.BeatmapDifficulty.Hard,
                            SharedConstructs.BeatmapDifficulty.Expert,
                            SharedConstructs.BeatmapDifficulty.ExpertPlus,
                        }
                    },
                    new Characteristic()
                    {
                        SerializedName = "360Degree",
                        Difficulties = new SharedConstructs.BeatmapDifficulty[]
                        {
                            SharedConstructs.BeatmapDifficulty.Easy,
                            SharedConstructs.BeatmapDifficulty.Normal,
                            SharedConstructs.BeatmapDifficulty.Hard,
                            SharedConstructs.BeatmapDifficulty.Expert,
                            SharedConstructs.BeatmapDifficulty.ExpertPlus,
                        }
                    }
                };

                Match.SelectedLevel = matchMap;
                Match.SelectedCharacteristic = null;
                Match.SelectedDifficulty = SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                //Notify all the UI that needs to be notified, and propegate the info across the network
                NotifyPropertyChanged(nameof(Match));
                MainPage.Connection.UpdateMatch(Match);

                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                var loadSong = new LoadSong();
                loadSong.LevelId = Match.SelectedLevel.LevelId;
                SendToPlayers(new Packet(loadSong));
            }
            else
            {
                //If we're using a custom host, we don't need to find a new hash, we can just download it by id
                try
                {
                    var hash = TournamentAssistantShared.BeatSaver.BeatSaverDownloader.GetHashFromID(songId);
                    TournamentAssistantShared.BeatSaver.BeatSaverDownloader.DownloadSongThreaded(hash,
                        (successfulDownload) =>
                        {
                            SongLoading = false;
                            LoadSongButtonProgress = 0;
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
                                Match.SelectedLevel = matchMap;
                                Match.SelectedCharacteristic = null;
                                Match.SelectedDifficulty = SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                            //Notify all the UI that needs to be notified, and propegate the info across the network
                            Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));
                                MainPage.Connection.UpdateMatch(Match);

                            //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                            var loadSong = new LoadSong();
                                loadSong.LevelId = Match.SelectedLevel.LevelId;
                                loadSong.CustomHostUrl = customHost;
                                SendToPlayers(new Packet(loadSong));
                            }

                        //Due to my inability to use a custom converter to successfully use DataBinding to accomplish this same goal,
                        //we are left doing it this weird gross way
                        SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = true);
                        },
                        (progress) =>
                        {
                            LoadSongButtonProgress = progress;
                        },
                        customHost
                    );
                }
                catch (Exception e)
                {
                    SongLoading = false;

                    var sampleMessageDialog = new SampleMessageDialog
                    {
                        Message = { Text = $"There was an error downloading the song:\n{e}" }
                    };

                    await DialogHost.Show(sampleMessageDialog, "RootDialog");
                }
            }
        }

        private bool LoadSong_CanExecute(object arg) => !SongLoading && (GetSongIdFromUrl(SongUrlBox.Text) != null || (!string.IsNullOrWhiteSpace(SongUrlBox.Text) && SongUrlBox.Text != "None"));

        private string GetSongIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            //Sanitize input
            if (url.StartsWith("https://beatsaver.com/") || url.StartsWith("https://bsaber.com/"))
            {
                //Strip off the trailing slash if there is one
                if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);

                //Strip off the beginning of the url to leave the id
                url = url.Substring(url.LastIndexOf("/") + 1);
            }

            if (url.Contains("&"))
            {
                url = url.Substring(0, url.IndexOf("&"));
            }

            if (url.EndsWith(".zip")) url = url.Substring(0, url.Length - 4);

            return url;
        }

        private async void PlaySong_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting)) return;

            await SetUpAndPlaySong();
        }

        private async Task<bool> SetUpAndPlaySong(bool useSync = false)
        {
            //Check for banned mods before continuing
            if (MainPage.Connection.State.ServerSettings.BannedMods.Length > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in Match.Players)
                {
                    string bannedMods = string.Join(", ", player.ModList.Intersect(MainPage.Connection.State.ServerSettings.BannedMods));
                    if (bannedMods != string.Empty) playersWithBannedMods += $"{player.Name}: {bannedMods}\n";
                }

                if (playersWithBannedMods != string.Empty)
                {
                    var sampleMessageDialog = new SampleMessageDialog
                    {
                        Message = { Text = $"Some players have banned mods:\n{playersWithBannedMods}" }
                    };

                    if (!(bool)(await DialogHost.Show(sampleMessageDialog, "RootDialog"))) return false;
                }
            }

            //If we're loading a new song, we can assume we're done with the old level completion results
            _levelCompletionResults = new List<SongFinished>();

            var gm = new GameplayModifiers();
            if ((bool)NoFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoFail;
            if ((bool)DisappearingArrowsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.DisappearingArrows;
            if ((bool)GhostNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.GhostNotes;
            if ((bool)FastNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FastNotes;
            if ((bool)SlowSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.SlowSong;
            if ((bool)FastSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FastSong;
            if ((bool)SuperFastSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.SuperFastSong;
            if ((bool)InstaFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.InstaFail;
            if ((bool)FailOnSaberClashBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FailOnClash;
            if ((bool)BatteryEnergyBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.BatteryEnergy;
            if ((bool)NoBombsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoBombs;
            if ((bool)NoWallsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoObstacles;
            if ((bool)NoArrowsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoArrows;
            if ((bool)ProModeBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.ProMode;
            if ((bool)ZenModeBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.ZenMode;
            if ((bool)SmallCubesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.SmallCubes;

            var playSong = new PlaySong();
            var gameplayParameters = new GameplayParameters();
            gameplayParameters.Beatmap = new Beatmap();
            gameplayParameters.Beatmap.Characteristic = new Characteristic();
            gameplayParameters.Beatmap.Characteristic.SerializedName = Match.SelectedCharacteristic.SerializedName;
            gameplayParameters.Beatmap.Difficulty = Match.SelectedDifficulty;
            gameplayParameters.Beatmap.LevelId = Match.SelectedLevel.LevelId;

            gameplayParameters.GameplayModifiers = gm;
            gameplayParameters.PlayerSettings = new PlayerSpecificSettings();

            playSong.GameplayParameters = gameplayParameters;
            playSong.FloatingScoreboard = (bool)ScoreboardBox.IsChecked;
            playSong.StreamSync = useSync;
            playSong.DisableFail = (bool)DisableFailBox.IsChecked;
            playSong.DisablePause = (bool)DisablePauseBox.IsChecked;
            playSong.DisableScoresaberSubmission = (bool)DisableScoresaberBox.IsChecked;
            playSong.ShowNormalNotesOnStream = (bool)ShowNormalNotesBox.IsChecked;

            SendToPlayers(new Packet(playSong));

            return true;
        }

        private async void PlaySongWithSync_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += DoSync;
        }

        private async void DoSync()
        {
            PlayersAreInGame -= DoSync;

            await Dispatcher.Invoke(async () =>
             {
                //Loop through players and set their stream screen position
                for (int i = 0; i < Match.Players.Length; i++)
                 {
                     Match.Players[i].StreamDelayMs = 0;
                     Match.Players[i].StreamScreenCoordinates = default;
                     Match.Players[i].StreamSyncStartMs = 0;

                     await DialogHost.Show(new ColorDropperDialog((point) =>
                     {
                        //Set player's stream screen coordinates
                        var streamCoordinates = new Player.Point();
                         streamCoordinates.x = (int)point.X;
                         streamCoordinates.y = (int)point.Y;
                         Match.Players[i].StreamScreenCoordinates = streamCoordinates;
                     }, Match.Players[i].Name)
                     {
                         Username = Match.Players[i].Name
                     },
                     "RootDialog");
                 }
             });

            int _playersWhoHaveCompletedStreamSync = 0;

            //Set up color listener
            List<PixelReader> pixelReaders = new List<PixelReader>();
            Action<bool> allPlayersSynced = PlayersCompletedSync;
            for (int i = 0; i < Match.Players.Length; i++)
            {
                int playerId = i;
                pixelReaders.Add(new PixelReader(new Point(Match.Players[i].StreamScreenCoordinates.x, Match.Players[i].StreamScreenCoordinates.y), (color) =>
                {
                    return (Colors.Green.R - 50 <= color.R && color.R <= Colors.Green.R + 50) &&
                        (Colors.Green.G - 50 <= color.G && color.G <= Colors.Green.G + 50) &&
                        (Colors.Green.B - 50 <= color.B && color.B <= Colors.Green.B + 50);

                }, () =>
                {
                    Logger.Debug($"{Match.Players[playerId].Name} GREEN DETECTED");
                    Match.Players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Match.Players[playerId].StreamSyncStartMs;

                    //Send updated delay info
                    MainPage.Connection.UpdatePlayer(Match.Players[playerId]);

                    _playersWhoHaveCompletedStreamSync++;
                    if (_playersWhoHaveCompletedStreamSync == Match.Players.Length) allPlayersSynced.Invoke(true);
                }));
            }

            Action waitForPlayersToDownloadGreen = async () =>
            {
                //Wait for players to download the Green file
                List<Guid> _playersWhoHaveDownloadedGreenImage = new List<Guid>();
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                Action<Acknowledgement, Guid> ackReceived = (Acknowledgement a, Guid from) =>
                {
                    if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedGreenImage.Add(from);
                };
                MainPage.Connection.AckReceived += ackReceived;

                //Send Green background for players to display
                using (var greenBitmap = QRUtils.GenerateColoredBitmap())
                {
                    SendToPlayers(new Packet(
                        new File(
                            QRUtils.ConvertBitmapToPngBytes(greenBitmap),
                            intentions: File.Intentions.SetPngToShowWhenTriggered
                        )
                    ));
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedGreenImage.Contains(x))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                MainPage.Connection.AckReceived -= ackReceived;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedGreenImage.Contains(x.Id)).Select(x => x.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                    SendToPlayers(new Packet(new Command()
                    {
                        CommandType = Command.CommandTypes.DelayTest_Finish
                    }));

                    return;
                }

                //Loop through players and set their sync init time
                for (int i = 0; i < Match.Players.Length; i++)
                {
                    Match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }

                //Start watching pixels for color change
                pixelReaders.ForEach(x => x.StartWatching());

                //By now, all the players should be loaded into the game (god forbid they aren't),
                //so we'll send the signal to change the color now, and also start the timer.
                SendToPlayers(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.ScreenOverlay_ShowPng
                }));
            };
            new Task(waitForPlayersToDownloadGreen).Start();
        }

        private async void PlaySongWithQRSync_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += DoQRSync;
        }

        private void DoQRSync()
        {
            PlayersAreInGame -= DoQRSync;

            //Display region selector
            if (_resizableLocationSpecifier == null || _resizableLocationSpecifier.IsDisposed)
            {
                _resizableLocationSpecifier = new ResizableLocationSpecifier();
                _resizableLocationSpecifier.LocationOrSizeChanged += (startX, startY, newSize) => {
                    sourceX = startX;
                    sourceY = startY;
                    size = newSize;
                };
            }
            _resizableLocationSpecifier.ShowDialog();

            Action<bool> allPlayersSynced = PlayersCompletedSync;
            List<string> _playersWhoHaveCompletedStreamSync = new List<string>();

            Action scanForQrCodes = () =>
            {
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.UserId).All(x => _playersWhoHaveCompletedStreamSync.Contains(x)))
                {
                    var returnedIds = QRUtils.ReadQRsFromScreenIntoUserIds(sourceX, sourceY, size).ToList();
                    if (returnedIds.Count > 0)
                    {
                        var successMessage = string.Empty;
                        returnedIds.ForEach(x => successMessage += $"{x}, ");
                        Logger.Debug(successMessage);

                        foreach (var id in returnedIds.Select(x => x.Substring("https://scoresaber.com/u/".Length))) //Filter out scoresaber url as we go
                        {
                            if (_playersWhoHaveCompletedStreamSync.Contains(id)) continue; //Skip people we already have

                            var player = Match.Players.FirstOrDefault(x => x.UserId == id);
                            if (player == null) continue;

                            Logger.Debug($"{player.Name} QR DETECTED");
                            player.StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - player.StreamSyncStartMs;

                            //Send updated delay info
                            MainPage.Connection.UpdatePlayer(player);

                            _playersWhoHaveCompletedStreamSync.Add(id);
                        }
                    }
                }

                allPlayersSynced.Invoke(!_syncCancellationToken.Token.IsCancellationRequested);
            };

            Action waitForPlayersToDownloadQr = async () =>
            {
                //Wait for players to download the QR file
                List<Guid> _playersWhoHaveDownloadedQrImage = new List<Guid>();
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                Action<Acknowledgement, Guid> ackReceived = (Acknowledgement a, Guid from) =>
                {
                    if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedQrImage.Add(from);
                };
                MainPage.Connection.AckReceived += ackReceived;

                //Loop through players and send the QR for them to display
                for (int i = 0; i < Match.Players.Length; i++)
                {
                    Match.Players[i].StreamDelayMs = 0;
                    Match.Players[i].StreamScreenCoordinates = default;
                    Match.Players[i].StreamSyncStartMs = 0;
                    
                    MainPage.Connection.Send(
                        Match.Players[i].Id,
                        new Packet(
                            new File(
                                QRUtils.GenerateQRCodePngBytes($"https://scoresaber.com/u/{Match.Players[i].UserId}"),
                                intentions: File.Intentions.SetPngToShowWhenTriggered
                            )
                        )
                    );
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedQrImage.Contains(x))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                MainPage.Connection.AckReceived -= ackReceived;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedQrImage.Contains(x.Id)).Select(x => x.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                    SendToPlayers(new Packet(new Command()
                    {
                        CommandType = Command.CommandTypes.DelayTest_Finish
                    }));

                    return;
                }

                new Task(scanForQrCodes).Start();

                //Loop through players and set their sync init time
                //Also reset their stream syncing values to default
                for (int i = 0; i < Match.Players.Length; i++)
                {
                    Match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }

                //By now, all the players should be loaded into the game (god forbid they aren't),
                //so we'll send the signal to change the color now, and also start the timer.
                SendToPlayers(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.ScreenOverlay_ShowPng
                }));
            };
            new Task(waitForPlayersToDownloadQr).Start();
        }

        private async void PlaySongWithDualSync_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += DoDualSync;
        }

        private async void PlaySongWithDelayedStart_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += async () =>
            {
                await Task.Delay(5000);

                //Send "continue" to players
                SendToPlayers(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.DelayTest_Finish
                }));
            };
        }

        private void DoDualSync()
        {
            PlayersAreInGame -= DoDualSync;

            //Display screen highlighter
            Dispatcher.Invoke(() => {
                if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
                {
                    _primaryDisplayHighlighter = new PrimaryDisplayHighlighter(Screen.PrimaryScreen.Bounds);
                }

                _primaryDisplayHighlighter.Show();

                LogBlock.Inlines.Add(new Run("Waiting for QR codes...\n") { Foreground = Brushes.Yellow });
            });

            Action<bool> allPlayersLocated = async (locationSuccess) =>
            {
                Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                Action<bool> allPlayersSynced = PlayersCompletedSync;
                if (locationSuccess)
                {
                    Logger.Debug("LOCATED ALL PLAYERS");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Players located. Waiting for green screen...\n") { Foreground = Brushes.Yellow })); ;

                    //Wait for players to download the green file
                    List<Guid> _playersWhoHaveDownloadedGreenImage = new List<Guid>();
                    _syncCancellationToken?.Cancel();
                    _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                    Action<Acknowledgement, Guid> greenAckReceived = (Acknowledgement a, Guid from) =>
                    {
                        if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedGreenImage.Add(from);
                    };
                    MainPage.Connection.AckReceived += greenAckReceived;

                    //Send the green background
                    using (var greenBitmap = QRUtils.GenerateColoredBitmap())
                    {
                        SendToPlayers(new Packet(
                            new File(
                                QRUtils.ConvertBitmapToPngBytes(greenBitmap),
                                intentions: File.Intentions.SetPngToShowWhenTriggered
                            )
                        ));
                    }

                    while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedGreenImage.Contains(x))) await Task.Delay(0);

                    //If a player failed to download the background, bail            
                    MainPage.Connection.AckReceived -= greenAckReceived;
                    if (_syncCancellationToken.Token.IsCancellationRequested)
                    {
                        var missingLog = string.Empty;
                        var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedGreenImage.Contains(x.Id)).Select(x => x.Name);
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                        Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                        LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                        allPlayersSynced.Invoke(false);

                        return;
                    }

                    //Set up color listener
                    List<PixelReader> pixelReaders = new List<PixelReader>();
                    for (int i = 0; i < Match.Players.Length; i++)
                    {
                        int playerId = i;
                        pixelReaders.Add(new PixelReader(new Point(Match.Players[i].StreamScreenCoordinates.x, Match.Players[i].StreamScreenCoordinates.y), (color) =>
                        {
                            return (Colors.Green.R - 50 <= color.R && color.R <= Colors.Green.R + 50) &&
                                (Colors.Green.G - 50 <= color.G && color.G <= Colors.Green.G + 50) &&
                                (Colors.Green.B - 50 <= color.B && color.B <= Colors.Green.B + 50);

                        }, () =>
                        {
                            Match.Players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Match.Players[playerId].StreamSyncStartMs;
                            
                            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"DETECTED: {Match.Players[playerId].Name} (delay: {Match.Players[playerId].StreamDelayMs})\n") { Foreground = Brushes.YellowGreen })); ;

                            //Send updated delay info
                            MainPage.Connection.UpdatePlayer(Match.Players[playerId]);

                            if (Match.Players.All(x => x.StreamDelayMs > 0))
                            {
                                LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("All players successfully synced. Sending PlaySong\n") { Foreground = Brushes.Green })); ;
                                allPlayersSynced.Invoke(true);
                            }
                        }));
                    }

                    //Loop through players and set their sync init time
                    for (int i = 0; i < Match.Players.Length; i++)
                    {
                        Match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    }

                    //Start watching pixels for color change
                    pixelReaders.ForEach(x => x.StartWatching());

                    //Show the green
                    SendToPlayers(new Packet(new Command()
                    {
                        CommandType = Command.CommandTypes.ScreenOverlay_ShowPng
                    }));
                }
                else
                {
                    //If the qr scanning failed, bail and just play the song
                    Logger.Warning("Failed to locate all players on screen. Playing song without sync");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Failed to locate all players on screen. Playing song without sync\n") { Foreground = Brushes.Red })); ;
                    allPlayersSynced.Invoke(false);
                }
            };

            Action scanForQrCodes = () =>
            {
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                //While not 20 seconds elapsed and not all players have locations
                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.All(x => !x.StreamScreenCoordinates.Equals(default(Player.Point))))
                {
                    var returnedResults = QRUtils.ReadQRsFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, Screen.PrimaryScreen.Bounds.Size).ToList();
                    if (returnedResults.Count > 0)
                    {
                        //Logging
                        var successMessage = string.Empty;
                        returnedResults.ForEach(x => successMessage += $"{x}, ");
                        Logger.Debug(successMessage);

                        //Read the location of all the QRs
                        foreach (var result in returnedResults)
                        {
                            var player = Match.Players.FirstOrDefault(x => Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{x.UserId} {Match.Guid}") == result.Text);
                            if (player == null) continue;

                            Logger.Debug($"{player.Name} QR DETECTED");
                            var point = new Player.Point();
                            point.x = (int)result.ResultPoints[3].X; //ResultPoints[3] is the qr location square closest to the center of the qr. The oddball.
                            point.y = (int)result.ResultPoints[3].Y;
                            player.StreamScreenCoordinates = point;
                        }

                        //Logging
                        var missing = Match.Players.Where(x => x.StreamScreenCoordinates.Equals(default(Player.Point))).Select(x => x.Name);
                        var missingLog = "Can't see QR for: ";
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";
                        LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run(missingLog + "\n") { Foreground = Brushes.Yellow }));
                    }
                }

                allPlayersLocated.Invoke(!_syncCancellationToken.Token.IsCancellationRequested);
            };

            Action waitForPlayersToDownloadQr = async () =>
            {
                //Wait for players to download the QR file
                List<Guid> _playersWhoHaveDownloadedQrImage = new List<Guid>();
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                Action<Acknowledgement, Guid> ackReceived = (Acknowledgement a, Guid from) =>
                {
                    if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedQrImage.Add(from);
                };
                MainPage.Connection.AckReceived += ackReceived;

                //Loop through players and send the QR for them to display (but don't display it yet)
                //Also reset their stream syncing values to default
                for (int i = 0; i < Match.Players.Length; i++)
                {
                    Match.Players[i].StreamDelayMs = 0;
                    Match.Players[i].StreamScreenCoordinates = default;
                    Match.Players[i].StreamSyncStartMs = 0;

                    MainPage.Connection.Send(
                        Match.Players[i].Id,
                        new Packet(
                            new File(
                                QRUtils.GenerateQRCodePngBytes(Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{Match.Players[i].UserId} {Match.Guid}")),
                                intentions: File.Intentions.SetPngToShowWhenTriggered
                            )
                        )
                    );
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedQrImage.Contains(x))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                MainPage.Connection.AckReceived -= ackReceived;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedQrImage.Contains(x.Id)).Select(x => x.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                    SendToPlayers(new Packet(new Command()
                    {
                        CommandType = Command.CommandTypes.DelayTest_Finish
                    }));

                    Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                    return;
                }

                new Task(scanForQrCodes).Start();

                //All players should be loaded in by now, so let's get the players to show their location QRs
                SendToPlayers(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.ScreenOverlay_ShowPng
                }));
            };
            new Task(waitForPlayersToDownloadQr).Start();
        }

        private void PlayersCompletedSync(bool successfully)
        {
            if (successfully)
            {
                Logger.Success("All players synced successfully, starting matches with delay...");

                //Send "continue" to players, but with their delay accounted for
                SendToPlayersWithDelay(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.DelayTest_Finish
                }));
            }
            else
            {
                Logger.Error("Failed to sync players, falling back to normal play");
                SendToPlayers(new Packet(new Command()
                {
                    CommandType = Command.CommandTypes.DelayTest_Finish
                }));
            }
        }

        private bool PlaySong_CanExecute(object arg) => !SongLoading && DifficultyDropdown.SelectedItem != null && _matchPlayersHaveDownloadedSong && Match.Players.All(x => x.PlayState == Player.PlayStates.Waiting);

        private async void CheckForBannedMods_Executed(object obj)
        {
            //Check for banned mods before continuing
            if (MainPage.Connection.State.ServerSettings.BannedMods.Length > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in Match.Players)
                {
                    string bannedMods = string.Join(", ", player.ModList.Intersect(MainPage.Connection.State.ServerSettings.BannedMods));
                    if (bannedMods != string.Empty) playersWithBannedMods += $"{player.Name}: {bannedMods}\n";
                }

                if (playersWithBannedMods != string.Empty)
                {
                    var sampleMessageDialog = new SampleMessageDialog
                    {
                        Message = { Text = $"Some players have banned mods:\n{playersWithBannedMods}" }
                    };

                    await DialogHost.Show(sampleMessageDialog, "RootDialog");
                }
            }
        }

        private void ReturnToMenu_Executed(object obj)
        {
            _syncCancellationToken?.Cancel();

            var returnToMenu = new Command();
            returnToMenu.CommandType = Command.CommandTypes.ReturnToMenu;
            SendToPlayers(new Packet(returnToMenu));
        }

        private bool ReturnToMenu_CanExecute(object arg) => !SongLoading && Match.Players.Any(x => x.PlayState == Player.PlayStates.InGame);

        private void DestroyAndCloseMatch_Executed(object obj)
        {
            if (MainPage.DestroyMatch.CanExecute(Match)) MainPage.DestroyMatch.Execute(Match);
        }

        private void ClosePage_Executed(object obj)
        {
            MainPage.Connection.MatchInfoUpdated -= Connection_MatchInfoUpdated;
            MainPage.Connection.MatchDeleted -= Connection_MatchDeleted;
            MainPage.Connection.PlayerFinishedSong -= Connection_PlayerFinishedSong;
            MainPage.Connection.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;

            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.GoBack();
        }

        private bool ClosePage_CanExecute(object arg)
        {
            return (MainPage.Connection.Self.Id == Guid.Empty || MainPage.Connection.Self.Name == "Moon" || MainPage.Connection.Self.Name == "Olaf");
        }

        private void CharacteristicBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedItem != null)
            {
                var oldCharacteristic = Match.SelectedCharacteristic;

                Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == (sender as ComboBox).SelectedItem.ToString());

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.SelectedCharacteristic != oldCharacteristic) MainPage.Connection.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private void DifficultyDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyDropdown.SelectedItem != null)
            {
                var oldDifficulty = Match.SelectedDifficulty;
                
                Match.SelectedDifficulty = Match.SelectedCharacteristic.Difficulties.First(x => x.ToString() == DifficultyDropdown.SelectedItem.ToString());

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.SelectedDifficulty != oldDifficulty) MainPage.Connection.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private void SendToPlayers(Packet packet)
        {
            var playersText = string.Empty;
            foreach (var player in Match.Players) playersText += $"{player.Name}, ";
            Logger.Debug($"Sending {packet.Type} to {playersText}");
            MainPage.Connection.Send(Match.Players.Select(x => x.Id).ToArray(), packet);
        }

        private void SendToPlayersWithDelay(Packet packet)
        {
            var maxDelay = Match.Players.Max(x => x.StreamDelayMs);

            foreach (var player in Match.Players)
            {
                Task.Run(() =>
                {
                    Logger.Debug($"Sleeping {(int)maxDelay - (int)player.StreamDelayMs} ms for {player.Name}");
                    Thread.Sleep((int)maxDelay - (int)player.StreamDelayMs);
                    Logger.Debug($"Sending start to {player.Name}");
                    MainPage.Connection.Send(player.Id, packet);
                });
            }
        }
    }
}
