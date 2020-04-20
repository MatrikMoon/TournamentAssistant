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
using TournamentAssistantUI.BeatSaver;
using TournamentAssistantUI.Misc;
using TournamentAssistantUI.UI.UserControls;
using Color = System.Drawing.Color;
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

        private int _playersWhoHaveFinishedSong;
        public event Action AllPlayersFinishedSong;

        private int _playersWhoHaveCompletedStreamSync;
        public event Action AllPlayersSynced;


        public MainPage MainPage{ get; set; }

        public ICommand LoadSong { get; }
        public ICommand PlaySong { get; }
        public ICommand PlaySongWithSync { get; }
        public ICommand ReturnToMenu { get; }
        public ICommand ClosePage { get; }
        public ICommand DestroyAndCloseMatch { get; }

        private bool _matchPlayersHaveDownloadedSong;

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
            ReturnToMenu = new CommandImplementation(ReturnToMenu_Executed, ReturnToMenu_CanExecute);
            ClosePage = new CommandImplementation(ClosePage_Executed, (_) => true);
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
                var result = await DialogHost.Show(new GameOverDialog(Match.Players.ToList()), "RootDialog");
            });
        }

        private void Connection_PlayerFinishedSong(Player player)
        {
            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{player.Name} has scored {player.CurrentScore}\n")));

            if (Match.Players.Contains(player)) _playersWhoHaveFinishedSong++;

            var playersText = string.Empty;
            foreach (var matchPlayer in Match.Players) playersText += $"{matchPlayer.Name}, ";
            Logger.Debug($"{player.Name} FINISHED SONG, FOR A TOTAL OF {_playersWhoHaveFinishedSong} FINISHED PLAYERS OUT OF {playersText}");
            if (_playersWhoHaveFinishedSong == Match.Players.Length)
            {
                AllPlayersFinishedSong?.Invoke();
                _playersWhoHaveFinishedSong = 0;
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

                //Update this little flag accordingly
                _matchPlayersHaveDownloadedSong = Match.Players.All(x => x.CurrentDownloadState == Player.DownloadState.Downloaded);
            }
        }

        private void Connection_MatchInfoUpdated(Match updatedMatch)
        {
            if (updatedMatch.Guid == Match.Guid)
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
                loadSong.levelId = Match.CurrentlySelectedLevel.LevelId;
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
                            var song = new Song(hash);
                            var matchMap = new PreviewBeatmapLevel()
                            {
                                LevelId = hash,
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
                            loadSong.levelId = Match.CurrentlySelectedLevel.LevelId;
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

            return url.Length == 3 || url.Length == 4 || OstHelper.IsOst(url) ? url : null;
        }


        private void PlaySong_Executed(object obj)
        {
            var gm = new GameplayModifiers();
            gm.noFail = (bool)NoFailBox.IsChecked;
            gm.disappearingArrows = (bool)DisappearingArrowsBox.IsChecked;
            gm.ghostNotes = (bool)GhostNotesBox.IsChecked;
            gm.fastNotes = (bool)FastNotesBox.IsChecked;
            gm.songSpeed = (bool)FastSongBox.IsChecked ? GameplayModifiers.SongSpeed.Faster : ((bool)SlowSongBox.IsChecked ? GameplayModifiers.SongSpeed.Slower : GameplayModifiers.SongSpeed.Normal);
            gm.instaFail = (bool)InstaFailBox.IsChecked;
            gm.failOnSaberClash = (bool)FailOnSaberClashBox.IsChecked;
            gm.batteryEnergy = (bool)BatteryEnergyBox.IsChecked;
            gm.noBombs = (bool)NoBombsBox.IsChecked;
            gm.noObstacles = (bool)NoWallsBox.IsChecked;

            var playSong = new PlaySong();
            playSong.characteristic = new Characteristic();
            playSong.characteristic.SerializedName = Match.CurrentlySelectedCharacteristic.SerializedName;
            playSong.difficulty = Match.CurrentlySelectedDifficulty;
            playSong.gameplayModifiers = gm;
            playSong.playerSettings = new PlayerSpecificSettings();
            playSong.levelId = Match.CurrentlySelectedLevel.LevelId;

            SendToPlayers(new Packet(playSong));
        }

        private async void PlaySongWithSync_Executed(object obj)
        {
            var gm = new GameplayModifiers();
            gm.noFail = (bool)NoFailBox.IsChecked;
            gm.disappearingArrows = (bool)DisappearingArrowsBox.IsChecked;
            gm.ghostNotes = (bool)GhostNotesBox.IsChecked;
            gm.fastNotes = (bool)FastNotesBox.IsChecked;
            gm.songSpeed = (bool)FastSongBox.IsChecked ? GameplayModifiers.SongSpeed.Faster : ((bool)SlowSongBox.IsChecked ? GameplayModifiers.SongSpeed.Slower : GameplayModifiers.SongSpeed.Normal);
            gm.instaFail = (bool)InstaFailBox.IsChecked;
            gm.failOnSaberClash = (bool)FailOnSaberClashBox.IsChecked;
            gm.batteryEnergy = (bool)BatteryEnergyBox.IsChecked;
            gm.noBombs = (bool)NoBombsBox.IsChecked;
            gm.noObstacles = (bool)NoWallsBox.IsChecked;

            var playSong = new PlaySong();
            playSong.characteristic = new Characteristic();
            playSong.characteristic.SerializedName = Match.CurrentlySelectedCharacteristic.SerializedName;
            playSong.difficulty = Match.CurrentlySelectedDifficulty;
            playSong.gameplayModifiers = gm;
            playSong.playerSettings = new PlayerSpecificSettings();
            playSong.levelId = Match.CurrentlySelectedLevel.LevelId;

            playSong.playWithStreamSync = true;
            SendToPlayers(new Packet(playSong));

            _playersWhoHaveCompletedStreamSync = 0;

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

            //Set up color listener
            List<PixelReader> pixelReaders = new List<PixelReader>();
            AllPlayersSynced += PlayersCompletedSync;
            for (int i = 0; i < Match.Players.Length; i++)
            {
                int playerId = i;
                pixelReaders.Add(new PixelReader(new Point(Match.Players[i].StreamScreenCoordinates.x, Match.Players[i].StreamScreenCoordinates.y), (color) =>
                {
                    return (Colors.Green.R - 30 <= color.R && color.R <= Colors.Green.R + 30) &&
                        (Colors.Green.G - 30 <= color.G && color.G <= Colors.Green.G + 30) &&
                        (Colors.Green.B - 30 <= color.B && color.B <= Colors.Green.B + 30);

                }, () =>
                {
                    Logger.Debug($"{Match.Players[playerId].Name} GREEN DETECTED");
                    Match.Players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Match.Players[playerId].StreamSyncStartMs;
                    _playersWhoHaveCompletedStreamSync++;
                    if (_playersWhoHaveCompletedStreamSync == Match.Players.Length) AllPlayersSynced?.Invoke();
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
                commandType = Command.CommandType.DelayTest_Trigger
            }));
        }

        private void PlayersCompletedSync()
        {
            AllPlayersSynced -= PlayersCompletedSync;

            //Send "continue" to players, but with their delay accounted for
            SendToPlayersWithDelay(new Packet(new Command()
            {
                commandType = Command.CommandType.DelayTest_Finish
            }));
        }

        private bool PlaySong_CanExecute(object arg) => !SongLoading && DifficultyDropdown.SelectedItem != null && _matchPlayersHaveDownloadedSong;

        private void ReturnToMenu_Executed(object obj)
        {
            var returnToMenu = new Command();
            returnToMenu.commandType = Command.CommandType.ReturnToMenu;
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
