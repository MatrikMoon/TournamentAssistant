using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Misc;
using TournamentAssistantUI.UI.UserControls;
using Color = System.Drawing.Color;
using Point = System.Windows.Point;
using TournamentAssistantShared.BeatSaver;
using TournamentAssistantUI.UI.Forms;
using System.Windows.Forms;
using System.Drawing;
using ComboBox = System.Windows.Controls.ComboBox;
using Discord.Rest;

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
        public ICommand ReturnToMenu { get; }
        public ICommand ClosePage { get; }
        public ICommand DestroyAndCloseMatch { get; }

        //Necessary for QR Sync
        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;
        private ResizableLocationSpecifier _resizableLocationSpecifier;
        private int sourceX = Screen.PrimaryScreen.Bounds.X;
        private int sourceY = Screen.PrimaryScreen.Bounds.Y;
        private Size size = Screen.PrimaryScreen.Bounds.Size;

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
            SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = Match.CurrentlySelectedLevel != null);

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
            PlaySongWithSync = new CommandImplementation(PlaySongWithSync_Executed, PlaySong_CanExecute);
            PlaySongWithQRSync = new CommandImplementation(PlaySongWithQRSync_Executed, PlaySong_CanExecute);
            PlaySongWithDualSync = new CommandImplementation(PlaySongWithDualSync_Executed, PlaySong_CanExecute);
            ReturnToMenu = new CommandImplementation(ReturnToMenu_Executed, ReturnToMenu_CanExecute);
            ClosePage = new CommandImplementation(ClosePage_Executed, ClosePage_CanExecute);
            DestroyAndCloseMatch = new CommandImplementation(DestroyAndCloseMatch_Executed, (_) => true);

            OSTPicker.SelectionChanged += OSTPicker_SelectionChanged;
            SongUrlBox.TextChanged += SongUrlBox_TextChanged;



            //Set up log monitor
            /*Logger.MessageLogged += (type, message) =>
            {
                SolidColorBrush textBrush = null;
                switch (type)
                {
                    case Logger.LogType.Debug:
                        textBrush = Brushes.LightSkyBlue;
                        break;
                    case Logger.LogType.Error:
                        textBrush = Brushes.Red;
                        break;
                    case Logger.LogType.Info:
                        textBrush = Brushes.White;
                        break;
                    case Logger.LogType.Success:
                        textBrush = Brushes.Green;
                        break;
                    case Logger.LogType.Warning:
                        textBrush = Brushes.Yellow;
                        break;
                    default:
                        break;
                }

                LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{message}\n") { Foreground = textBrush }));
            };*/
        }

        private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = Dispatcher.Invoke(async () =>
            {
                var result = await DialogHost.Show(new PlayerDialog(MatchBox.PlayerListBox.SelectedItem as Player), "RootDialog");
            });
        }

        private void MatchPage_AllPlayersFinishedSong()
        {
            _ = Dispatcher.Invoke(async () =>
            {
                //If teams are enabled
                if (MainPage.Connection.State.ServerSettings.Teams.Length > 0)
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
        }

        private void SongUrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OSTPicker.IsEnabled = string.IsNullOrEmpty(SongUrlBox.Text);
        }

        private void OSTPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SongUrlBox.IsEnabled = OSTPicker.SelectedItem as string == "None";
        }

        private void Connection_PlayerInfoUpdated(Player player)
        {
            //If the updated player is part of our match 
            var index = Match.Players.ToList().FindIndex(x => x.Guid == player.Guid);
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
                if (Match.CurrentlySelectedLevel != null) SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = true);
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

        private void LoadSong_Executed(object obj)
        {
            SongLoading = true;
            var songId = GetSongIdFromUrl(SongUrlBox.Text) ?? OstHelper.allLevels.First(x => x.Value == OSTPicker.SelectedItem as string).Key;

            //If we're loading a new song, we can assume we're done with the old level completion results
            _levelCompletionResults = new List<SongFinished>();

            if (OstHelper.IsOst(songId))
            {
                SongLoading = false;
                var matchMap = new PreviewBeatmapLevel()
                {
                    LevelId = songId,
                    Name = OSTPicker.SelectedItem as string
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

                Match.CurrentlySelectedLevel = matchMap;
                Match.CurrentlySelectedCharacteristic = null;
                Match.CurrentlySelectedDifficulty = SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                //Notify all the UI that needs to be notified, and propegate the info across the network
                NotifyPropertyChanged(nameof(Match));
                MainPage.Connection.UpdateMatch(Match);

                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                var loadSong = new LoadSong();
                loadSong.LevelId = Match.CurrentlySelectedLevel.LevelId;
                SendToPlayers(new Packet(loadSong));
            }
            else
            {
                var hash = BeatSaverDownloader.GetHashFromID(songId);
                BeatSaverDownloader.DownloadSongInfoThreaded(hash,
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
                            Match.CurrentlySelectedLevel = matchMap;
                            Match.CurrentlySelectedCharacteristic = null;
                            Match.CurrentlySelectedDifficulty = SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                            //Notify all the UI that needs to be notified, and propegate the info across the network
                            Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));
                            MainPage.Connection.UpdateMatch(Match);

                            //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                            var loadSong = new LoadSong();
                            loadSong.LevelId = Match.CurrentlySelectedLevel.LevelId;
                            SendToPlayers(new Packet(loadSong));
                        }

                        //Due to my inability to use a custom converter to successfully use DataBinding to accomplish this same goal,
                        //we are left doing it this weird gross way
                        SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = true);
                    },
                    (progress) =>
                    {
                        LoadSongButtonProgress = progress;
                    }
                );
            }
        }

        private bool LoadSong_CanExecute(object arg) => !SongLoading && (GetSongIdFromUrl(SongUrlBox.Text) != null || (OSTPicker.SelectedItem != null && (OSTPicker.SelectedItem as string) != "None"));

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

            return url.Length == 2 || url.Length == 3 || url.Length == 4 || OstHelper.IsOst(url) ? url : null;
        }

        private void PlaySong_Executed(object obj)
        {
            SetUpAndPlaySong();
        }

        private void SetUpAndPlaySong(bool useSync = false)
        {
            var gm = new GameplayModifiers();
            if ((bool)NoFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoFail;
            if ((bool)DisappearingArrowsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.DisappearingArrows;
            if ((bool)GhostNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.GhostNotes;
            if ((bool)FastNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FastNotes;
            if ((bool)SlowSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.SlowSong;
            if ((bool)FastSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FastSong;
            if ((bool)InstaFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.InstaFail;
            if ((bool)FailOnSaberClashBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.FailOnClash;
            if ((bool)BatteryEnergyBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.BatteryEnergy;
            if ((bool)NoBombsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoBombs;
            if ((bool)NoWallsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.NoObstacles;

            var playSong = new PlaySong();
            playSong.Beatmap = new Beatmap();
            playSong.Beatmap.Characteristic = new Characteristic();
            playSong.Beatmap.Characteristic.SerializedName = Match.CurrentlySelectedCharacteristic.SerializedName;
            playSong.Beatmap.Difficulty = Match.CurrentlySelectedDifficulty;
            playSong.Beatmap.LevelId = Match.CurrentlySelectedLevel.LevelId;

            playSong.GameplayModifiers = gm;
            playSong.PlayerSettings = new PlayerSpecificSettings();

            playSong.StreamSync = useSync;
            SendToPlayers(new Packet(playSong));
        }

        private void PlaySongWithSync_Executed(object obj)
        {
            SetUpAndPlaySong(true);

            PlayersAreInGame += DoSync;
        }

        private async void DoSync()
        {
            PlayersAreInGame -= DoSync;

            await Dispatcher.Invoke(async () =>
             {
                //Loop through players and set their stream screen position
                for (int i = 0; i < Match.Players.Length; i++)
                 {
                     Match.Players[i].StreamScreenCoordinates = new Player.Point();
                     Match.Players[i].StreamDelayMs = 0;
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
                    _playersWhoHaveCompletedStreamSync++;
                    if (_playersWhoHaveCompletedStreamSync == Match.Players.Length) allPlayersSynced.Invoke(true);
                }));
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
                CommandType = Command.CommandTypes.ShowStreamImage
            }));
        }

        private void PlaySongWithQRSync_Executed(object obj)
        {
            SetUpAndPlaySong(true);

            //Wait until all players are in the game to do sync stuff
            PlayersAreInGame += DoQRSync;
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

            //Loop through players and send the QR for them to display
            for (int i = 0; i < Match.Players.Length; i++)
            {
                MainPage.Connection.Send(Match.Players[i].Guid, new Packet(new File()
                {
                    Intention = File.Intentions.SetPngToShowWhenTriggered,
                    Compressed = true,
                    Data = CompressionUtils.Compress(QRUtils.GenerateQRCodePngBytes($"https://scoresaber.com/u/{Match.Players[i].UserId}"))
                }));
            }

            Action<bool> allPlayersSynced = PlayersCompletedSync;
            List<string> _playersWhoHaveCompletedStreamSync = new List<string>();

            Action scanForQrCodes = () =>
            {
                var cancellationToken = new CancellationTokenSource(20 * 1000).Token;

                Match.Players.ToList().ForEach(x => Logger.Debug($"LOOKING FOR: {x.UserId}"));

                while (!cancellationToken.IsCancellationRequested && !Match.Players.Select(x => x.UserId).All(x => _playersWhoHaveCompletedStreamSync.Contains(x)))
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
                            _playersWhoHaveCompletedStreamSync.Add(id);
                        }
                    }
                }

                allPlayersSynced.Invoke(!cancellationToken.IsCancellationRequested);
            };
            new Task(scanForQrCodes).Start();

            //Loop through players and set their sync init time
            for (int i = 0; i < Match.Players.Length; i++)
            {
                Match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }

            //By now, all the players should be loaded into the game (god forbid they aren't),
            //so we'll send the signal to change the color now, and also start the timer.
            SendToPlayers(new Packet(new Command()
            {
                CommandType = Command.CommandTypes.ShowStreamImage
            }));
        }

        private void PlaySongWithDualSync_Executed(object obj)
        {
            SetUpAndPlaySong(true);

            //Wait until all players are in the game to do sync stuff
            PlayersAreInGame += DoDualSync;
        }

        private void DoDualSync()
        {
            PlayersAreInGame -= DoDualSync;

            //Display screen highlighter
            Dispatcher.Invoke(() => {
                if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
                {
                    _primaryDisplayHighlighter = new PrimaryDisplayHighlighter();
                }

                _primaryDisplayHighlighter.Show();
            });

            //Loop through players and send the QR for them to display (but don't display it yet)
            for (int i = 0; i < Match.Players.Length; i++)
            {
                MainPage.Connection.Send(Match.Players[i].Guid, new Packet(new File()
                {
                    Intention = File.Intentions.SetPngToShowWhenTriggered,
                    Compressed = true,
                    Data = CompressionUtils.Compress(QRUtils.GenerateQRCodePngBytes($"https://scoresaber.com/u/{Match.Players[i].UserId}"))
                }));
            }

            Action<bool> allPlayersLocated = (locationSuccess) =>
            {
                Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                if (locationSuccess)
                {
                    Logger.Debug("LOCATED ALL PLAYERS");

                    using (var greenBitmap = QRUtils.GenerateGreenBitmap())
                    {
                        //Setting the image data to null will cause it to use the default green image
                        SendToPlayers(new Packet(new File()
                        {
                            Intention = File.Intentions.SetPngToShowWhenTriggered,
                            Compressed = true,
                            Data = CompressionUtils.Compress(QRUtils.ConvertBitmapToPngBytes(greenBitmap))
                        }));
                    }

                    //Set up color listener
                    int _playersWhoHaveCompletedStreamSync = 0;
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
                            _playersWhoHaveCompletedStreamSync++;
                            if (_playersWhoHaveCompletedStreamSync == Match.Players.Length) allPlayersSynced.Invoke(true);
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
                        CommandType = Command.CommandTypes.ShowStreamImage
                    }));
                }
            };

            Action scanForQrCodes = () =>
            {
                var cancellationToken = new CancellationTokenSource(20 * 1000).Token;

                Match.Players.ToList().ForEach(x => Logger.Debug($"LOOKING FOR: {x.Guid}"));

                //While not 20 seconds elapsed and not all players have locations
                while (!cancellationToken.IsCancellationRequested && !Match.Players.All(x => !x.StreamScreenCoordinates.Equals(default(Player.Point))))
                {
                    var returnedResults = QRUtils.ReadQRsFromScreen(sourceX, sourceY, size).ToList();
                    if (returnedResults.Count > 0)
                    {
                        var successMessage = string.Empty;
                        returnedResults.ForEach(x => successMessage += $"{x}, ");
                        Logger.Debug(successMessage);

                        foreach (var result in returnedResults)
                        {
                            var player = Match.Players.FirstOrDefault(x => x.UserId == result.Text.Substring("https://scoresaber.com/u/".Length));
                            if (player == null) continue;

                            Logger.Debug($"{player.Name} QR DETECTED");
                            var point = new Player.Point();
                            point.x = (int)result.ResultPoints[3].X; //ResultPoints[3] is the qr location square closest to the center of the qr. The oddball.
                            point.y = (int)result.ResultPoints[3].Y;
                            player.StreamScreenCoordinates = point;
                        }
                    }
                }

                allPlayersLocated.Invoke(!cancellationToken.IsCancellationRequested);
            };
            new Task(scanForQrCodes).Start();

            //All players should be loaded in by now, so let's get the players to show their location QRs
            SendToPlayers(new Packet(new Command()
            {
                CommandType = Command.CommandTypes.ShowStreamImage
            }));
        }

        private void PlayersCompletedSync(bool successfully)
        {
            //Send "continue" to players, but with their delay accounted for
            SendToPlayersWithDelay(new Packet(new Command()
            {
                CommandType = Command.CommandTypes.DelayTest_Finish
            }));
        }

        private bool PlaySong_CanExecute(object arg) => !SongLoading && DifficultyDropdown.SelectedItem != null && _matchPlayersHaveDownloadedSong;

        private void ReturnToMenu_Executed(object obj)
        {
            var returnToMenu = new Command();
            returnToMenu.CommandType = Command.CommandTypes.ReturnToMenu;
            SendToPlayers(new Packet(returnToMenu));
        }

        private bool ReturnToMenu_CanExecute(object arg) => !SongLoading;

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
            return MainPage.Connection.Self.Guid == "0" || MainPage.Connection.Self.Name == "Moon";
        }

        private void CharacteristicBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedItem != null)
            {
                var oldCharacteristic = Match.CurrentlySelectedCharacteristic;

                Match.CurrentlySelectedCharacteristic = Match.CurrentlySelectedLevel.Characteristics.First(x => x.SerializedName == (sender as ComboBox).SelectedItem.ToString());

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.CurrentlySelectedCharacteristic != oldCharacteristic) MainPage.Connection.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private void DifficultyDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyDropdown.SelectedItem != null)
            {
                var oldDifficulty = Match.CurrentlySelectedDifficulty;
                
                Match.CurrentlySelectedDifficulty = Match.CurrentlySelectedCharacteristic.Difficulties.First(x => x.ToString() == DifficultyDropdown.SelectedItem.ToString());

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.CurrentlySelectedDifficulty != oldDifficulty) MainPage.Connection.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private void SendToPlayers(Packet packet)
        {
            var playersText = string.Empty;
            foreach (var player in Match.Players) playersText += $"{player.Name}, ";
            Logger.Debug($"Sending {packet.Type} to {playersText}");
            MainPage.Connection.Send(Match.Players.Select(x => x.Guid).ToArray(), packet);
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
                    MainPage.Connection.Send(player.Guid, packet);
                });
            }
        }
    }
}
