using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using TournamentAssistantShared.Utilities;
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
                noneArray.AddRange(OstHelper.packs.SelectMany(x => x.SongDictionary.Select(y => y.Value)));
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
                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(LoadSongButtonProgress)));
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
                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(SongLoading)));
            }
        }

        private List<Push.SongFinished> _levelCompletionResults = new();
        public event Action AllPlayersFinishedSong;

        public MainPage MainPage { get; set; }

        public ICommand LoadSong { get; }
        public CommandImplementation PlaySong { get; }
        public CommandImplementation PlaySongWithDualSync { get; }
        public CommandImplementation PlaySongWithDelayedStart { get; }
        public ICommand CheckForBannedMods { get; }
        public ICommand ReturnToMenu { get; }
        public ICommand ClosePage { get; }
        public ICommand DestroyAndCloseMatch { get; }

        //Necessary for QR Sync
        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;
        private CancellationTokenSource _syncCancellationToken;

        private bool _matchPlayersHaveDownloadedSong;
        private bool _matchPlayersAreInGame;

        private event Func<Task> PlayersDownloadedSong;
        private event Func<Task> PlayersAreInGame;

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
            MainPage.Client.MatchInfoUpdated += Connection_MatchInfoUpdated;

            //If the match is externally deleted, we need to close the page
            MainPage.Client.MatchDeleted += Connection_MatchDeleted;

            //If player info is updated (ie: download state) we need to know it
            MainPage.Client.UserInfoUpdated += Connection_UserInfoUpdated;

            //Let's get notified when a player finishes a song
            MainPage.Client.PlayerFinishedSong += Connection_PlayerFinishedSong;

            //When all players finish a song, show the finished song dialog
            AllPlayersFinishedSong += MatchPage_AllPlayersFinishedSong;

            MatchBox.PlayerListBox.SelectionChanged += PlayerListBox_SelectionChanged;

            LoadSong = new CommandImplementation(LoadSong_Executed, LoadSong_CanExecute);
            PlaySong = new CommandImplementation(PlaySong_Executed, PlaySong_CanExecute);
            PlaySongWithDualSync = new CommandImplementation(PlaySongWithDualSync_Executed, PlaySong_CanExecute);
            PlaySongWithDelayedStart = new CommandImplementation(PlaySongWithDelayedStart_Executed, PlaySong_CanExecute);
            CheckForBannedMods = new CommandImplementation(CheckForBannedMods_Executed, (_) => true);
            ReturnToMenu = new CommandImplementation(ReturnToMenu_Executed, ReturnToMenu_CanExecute);
            ClosePage = new CommandImplementation(ClosePage_Executed, ClosePage_CanExecute);
            DestroyAndCloseMatch = new CommandImplementation(DestroyAndCloseMatch_Executed, (_) => true);
        }

        private User[] GetPlayersInMatch()
        {
            return MainPage.Client.State.Users.Where(x => x.ClientType == User.ClientTypes.Player && Match.AssociatedUsers.Contains(x.Guid)).ToArray();
        }

        private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = Dispatcher.Invoke(async () =>
            {
                var result = await DialogHost.Show(new UserDialog(MatchBox.PlayerListBox.SelectedItem as User, new CommandImplementation(KickPlayer_Executed)), "RootDialog");
            });
        }

        private void MatchPage_AllPlayersFinishedSong()
        {
            _ = Dispatcher.Invoke(async () =>
            {
                //If teams are enabled
                if (MainPage.Client.State.ServerSettings.EnableTeams)
                {
                    await DialogHost.Show(new GameOverDialogTeams(_levelCompletionResults), "RootDialog");
                }
                else await DialogHost.Show(new GameOverDialog(_levelCompletionResults), "RootDialog");
            });
        }

        private Task Connection_PlayerFinishedSong(Push.SongFinished results)
        {
            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{results.Player.Name} has scored {results.Score}\n")));

            if (Match.AssociatedUsers.Contains(results.Player.Guid)) _levelCompletionResults.Add(results);

            var playersText = string.Empty;
            foreach (var matchPlayer in GetPlayersInMatch()) playersText += $"{matchPlayer.Name}, ";
            Logger.Debug($"{results.Player.Name} FINISHED SONG, FOR A TOTAL OF {_levelCompletionResults.Count} FINISHED PLAYERS OUT OF {GetPlayersInMatch().Count()}");
            if (_levelCompletionResults.Count == GetPlayersInMatch().Count())
            {
                AllPlayersFinishedSong?.Invoke();
            }
            return Task.CompletedTask;
        }

        private Task Connection_UserInfoUpdated(User player)
        {
            //If the updated player is part of our match 
            if (Match.AssociatedUsers.Contains(player.Guid))
            {
                //Check for potential events we'd need to fire
                var oldMatchPlayersHaveDownloadedSong = _matchPlayersHaveDownloadedSong;
                var oldMatchPlayersAreInGame = _matchPlayersAreInGame;

                var matchPlayers = GetPlayersInMatch();

                _matchPlayersHaveDownloadedSong = matchPlayers.All(x => x.DownloadState == User.DownloadStates.Downloaded);
                _matchPlayersAreInGame = matchPlayers.All(x => x.PlayState == User.PlayStates.InGame);

                if (!oldMatchPlayersHaveDownloadedSong && _matchPlayersHaveDownloadedSong) PlayersDownloadedSong?.Invoke();
                if (!oldMatchPlayersAreInGame && _matchPlayersAreInGame) PlayersAreInGame?.Invoke();
            }
            return Task.CompletedTask;
        }

        private async Task Connection_MatchInfoUpdated(Match updatedMatch)
        {
            if (!updatedMatch.MatchEquals(Match))
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                Match = updatedMatch;
                if (Match.SelectedLevel?.LevelId?.StartsWith("custom_level_") == true)
                {
                    SongUrlBox.Text = Match.SelectedLevel.LevelId.Replace("custom_level_", "").ToLowerInvariant();
                    SongBox.IsEnabled = true;
                    PlaySong.Refresh();
                    PlaySongWithDelayedStart.Refresh();
                    PlaySongWithDualSync.Refresh();
                }
                NotifyPropertyChanged(nameof(Match));
            });
        }

        private Task Connection_MatchDeleted(Match deletedMatch)
        {
            if (deletedMatch.MatchEquals(Match))
            {
                MainPage.Client.MatchInfoUpdated -= Connection_MatchInfoUpdated;
                MainPage.Client.MatchDeleted -= Connection_MatchDeleted;
                MainPage.Client.PlayerFinishedSong -= Connection_PlayerFinishedSong;
                MainPage.Client.UserInfoUpdated -= Connection_UserInfoUpdated;

                Dispatcher.Invoke(() => ClosePage.Execute(this));
            }
            return Task.CompletedTask;
        }

        private void KickPlayer_Executed(object parameter)
        {
            //Remove player from list
            Match.AssociatedUsers.Remove((parameter as User).Guid);

            //Notify all the UI that needs to be notified, and propegate the info across the network
            NotifyPropertyChanged(nameof(Match));

            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
            Task.Run(() => MainPage.Client.UpdateMatch(Match));
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

                var allDifficulties = new int[]
                {
                    (int)Constants.BeatmapDifficulty.Easy,
                    (int)Constants.BeatmapDifficulty.Normal,
                    (int)Constants.BeatmapDifficulty.Hard,
                    (int)Constants.BeatmapDifficulty.Expert,
                    (int)Constants.BeatmapDifficulty.ExpertPlus,
                };

                var standardCharacteristic = new Characteristic()
                {
                    SerializedName = "Standard"
                };
                standardCharacteristic.Difficulties = allDifficulties;

                var noArrowsCharacteristic = new Characteristic()
                {
                    SerializedName = "NoArrows"
                };
                noArrowsCharacteristic.Difficulties = allDifficulties;

                var oneSaberCharacteristic = new Characteristic()
                {
                    SerializedName = "OneSaber"
                };
                oneSaberCharacteristic.Difficulties = allDifficulties;

                var ninetyDegreeCharacteristic = new Characteristic()
                {
                    SerializedName = "90Degree"
                };
                ninetyDegreeCharacteristic.Difficulties = allDifficulties;

                var threeSixtyDegreeCharacteristic = new Characteristic()
                {
                    SerializedName = "360Degree"
                };
                threeSixtyDegreeCharacteristic.Difficulties = allDifficulties;

                matchMap.Characteristics.AddRange(new Characteristic[]
                {
                    standardCharacteristic,
                    noArrowsCharacteristic,
                    oneSaberCharacteristic,
                    ninetyDegreeCharacteristic,
                    threeSixtyDegreeCharacteristic
                });

                Match.SelectedLevel = matchMap;
                Match.SelectedCharacteristic = null;
                Match.SelectedDifficulty = (int)Constants.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                //Notify all the UI that needs to be notified, and propegate the info across the network
                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));

                await MainPage.Client.UpdateMatch(Match);

                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                await SendToPlayers(new Packet
                {
                    Command = new Command
                    {
                        load_song = new Command.LoadSong
                        {
                            LevelId = Match.SelectedLevel.LevelId
                        }
                    }
                });
            }
            else
            {
                //If we're using a custom host, we don't need to find a new hash, we can just download it by id
                try
                {
                    var hash = BeatSaverDownloader.GetHashFromID(songId);
                    BeatSaverDownloader.DownloadSong(hash,
                        (songDir) =>
                        {
                            SongLoading = false;
                            LoadSongButtonProgress = 0;
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
                                    var newCharacteristic = new Characteristic()
                                    {
                                        SerializedName = characteristic,
                                    };
                                    newCharacteristic.Difficulties = song.GetBeatmapDifficulties(characteristic).Select(x => (int)x).ToArray();
                                    characteristics.Add(newCharacteristic);
                                }
                                matchMap.Characteristics.AddRange(characteristics.ToArray());
                                Match.SelectedLevel = matchMap;
                                Match.SelectedCharacteristic = null;
                                Match.SelectedDifficulty = (int)Constants.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                                //Notify all the UI that needs to be notified, and propegate the info across the network
                                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));

                                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
                                Task.Run(() => MainPage.Client.UpdateMatch(Match));

                                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                                var loadSong = new Command.LoadSong
                                {
                                    LevelId = Match.SelectedLevel.LevelId,
                                };
                                if (!string.IsNullOrWhiteSpace(customHost)) loadSong.CustomHostUrl = customHost;

                                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
                                Task.Run(() =>
                                    SendToPlayers(new Packet
                                    {
                                        Command = new Command
                                        {
                                            load_song = loadSong
                                        }
                                    })
                                );
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
            if (!GetPlayersInMatch().All(x => x.PlayState == User.PlayStates.Waiting)) return;

            await SetUpAndPlaySong();
        }

        private async Task<bool> SetUpAndPlaySong(bool useSync = false)
        {
            //Check for banned mods before continuing
            if (MainPage.Client.State.ServerSettings.BannedMods.Count > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in GetPlayersInMatch())
                {
                    string bannedMods = string.Join(", ", player.ModLists.Intersect(MainPage.Client.State.ServerSettings.BannedMods));
                    if (bannedMods != string.Empty) playersWithBannedMods += $"{player.Name}: {bannedMods}\n";
                }

                if (playersWithBannedMods != string.Empty)
                {
                    var sampleMessageDialog = new SampleMessageDialog
                    {
                        Message = { Text = $"Some players have banned mods:\n{playersWithBannedMods}" }
                    };

                    if (!(bool)await DialogHost.Show(sampleMessageDialog, "RootDialog")) return false;
                }
            }

            //If we're loading a new song, we can assume we're done with the old level completion results
            _levelCompletionResults = new List<Push.SongFinished>();

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
            if ((bool)StrictAnglseBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.GameOptions.StrictAngles;

            await SendToPlayers(new Packet
            {
                Command = new Command
                {
                    play_song = new Command.PlaySong
                    {
                        GameplayParameters = new GameplayParameters
                        {
                            Beatmap = new Beatmap
                            {
                                Characteristic = new Characteristic
                                {
                                    SerializedName = Match.SelectedCharacteristic.SerializedName
                                },
                                Difficulty = Match.SelectedDifficulty,
                                LevelId = Match.SelectedLevel.LevelId
                            },

                            GameplayModifiers = gm,
                            PlayerSettings = new PlayerSpecificSettings()
                        },
                        FloatingScoreboard = (bool)ScoreboardBox.IsChecked,
                        StreamSync = useSync,
                        DisableFail = (bool)DisableFailBox.IsChecked,
                        DisablePause = (bool)DisablePauseBox.IsChecked,
                        DisableScoresaberSubmission = (bool)DisableScoresaberBox.IsChecked,
                        ShowNormalNotesOnStream = (bool)ShowNormalNotesBox.IsChecked
                    }
                }
            });

            return true;
        }

        private async void PlaySongWithDualSync_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!GetPlayersInMatch().All(x => x.PlayState == User.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += DoDualSync;
        }

        private async void PlaySongWithDelayedStart_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!GetPlayersInMatch().All(x => x.PlayState == User.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += async () =>
            {
                await Task.Delay(5000);

                // add seconds to account for loading into the map
                Match.StartTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffZZZ");
                await MainPage.Client.UpdateMatch(Match);

                //Send "continue" to players
                await SendToPlayers(new Packet
                {
                    Command = new Command()
                    {
                        DelayTestFinish = true
                    }
                });
            };
        }

        private Task DoDualSync()
        {
            PlayersAreInGame -= DoDualSync;

            //Display screen highlighter
            Dispatcher.Invoke(() =>
            {
                if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
                {
                    _primaryDisplayHighlighter = new PrimaryDisplayHighlighter(Screen.PrimaryScreen.Bounds);
                }

                _primaryDisplayHighlighter.Show();

                LogBlock.Inlines.Add(new Run("Waiting for QR codes...\n") { Foreground = Brushes.Yellow });
            });

            Func<bool, Task> allPlayersLocated = async (locationSuccess) =>
            {
                Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                Func<bool, Task> allPlayersSynced = PlayersCompletedSync;
                if (locationSuccess)
                {
                    var players = GetPlayersInMatch().ToArray();
                    Logger.Debug("LOCATED ALL PLAYERS");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Players located. Waiting for green screen...\n") { Foreground = Brushes.Yellow })); ;

                    //Wait for players to download the green file
                    List<Guid> _playersWhoHaveDownloadedGreenImage = new List<Guid>();
                    _syncCancellationToken?.Cancel();
                    _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                    Func<Response.ImagePreloaded, Guid, Task> greenImagePreloaded = (Response.ImagePreloaded a, Guid from) =>
                    {
                        if (players.Select(x => x.Guid).Contains(from.ToString())) _playersWhoHaveDownloadedGreenImage.Add(from);
                        return Task.CompletedTask;
                    };
                    MainPage.Client.ImagePreloaded += greenImagePreloaded;

                    //Send the green background
                    using (var greenBitmap = QRUtils.GenerateColoredBitmap())
                    {
                        await SendToPlayers(new Packet
                        {
                            Request = new Request
                            {
                                preload_image_for_stream_sync = new Request.PreloadImageForStreamSync
                                {
                                    Data = QRUtils.ConvertBitmapToPngBytes(greenBitmap)
                                }
                            }
                        });
                    }

                    //TODO: Use proper waiting
                    while (!_syncCancellationToken.Token.IsCancellationRequested && !players.Select(x => x.Guid).All(x => _playersWhoHaveDownloadedGreenImage.Contains(Guid.Parse(x)))) await Task.Delay(0);

                    //If a player failed to download the background, bail            
                    MainPage.Client.ImagePreloaded -= greenImagePreloaded;
                    if (_syncCancellationToken.Token.IsCancellationRequested)
                    {
                        var missingLog = string.Empty;
                        var missing = players.Where(x => !_playersWhoHaveDownloadedGreenImage.Contains(Guid.Parse(x.Guid))).Select(x => x.Name);
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                        Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                        LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                        await allPlayersSynced(false);

                        return;
                    }

                    //Set up color listener
                    List<PixelReader> pixelReaders = new List<PixelReader>();
                    for (int i = 0; i < players.Length; i++)
                    {
                        int playerId = i;
                        pixelReaders.Add(new PixelReader(new Point(players[i].StreamScreenCoordinates.X, players[i].StreamScreenCoordinates.Y), (color) =>
                        {
                            return Colors.Green.R - 50 <= color.R && color.R <= Colors.Green.R + 50 &&
                                Colors.Green.G - 50 <= color.G && color.G <= Colors.Green.G + 50 &&
                                Colors.Green.B - 50 <= color.B && color.B <= Colors.Green.B + 50;

                        }, () =>
                        {
                            players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - players[playerId].StreamSyncStartMs;

                            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"DETECTED: {players[playerId].Name} (delay: {players[playerId].StreamDelayMs})\n") { Foreground = Brushes.YellowGreen })); ;

                            //Send updated delay info
                            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
                            Task.Run(() => MainPage.Client.UpdateUser(players[playerId]));

                            if (players.All(x => x.StreamDelayMs > 0))
                            {
                                LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("All players successfully synced. Sending PlaySong\n") { Foreground = Brushes.Green })); ;
                                allPlayersSynced.Invoke(true);
                            }
                        }));
                    }

                    //Loop through players and set their sync init time
                    for (int i = 0; i < players.Length; i++)
                    {
                        players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    }

                    //Start watching pixels for color change
                    pixelReaders.ForEach(x => x.StartWatching());

                    //Show the green
                    await SendToPlayers(new Packet
                    {
                        Command = new Command()
                        {
                            StreamSyncShowImage = true
                        }
                    });
                }
                else
                {
                    //If the qr scanning failed, bail and just play the song
                    Logger.Warning("Failed to locate all players on screen. Playing song without sync");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Failed to locate all players on screen. Playing song without sync\n") { Foreground = Brushes.Red })); ;
                    await allPlayersSynced(false);
                }
            };

            Action scanForQrCodes = () =>
            {
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);
                var players = GetPlayersInMatch();

                //While not 20 seconds elapsed and not all players have locations
                while (!_syncCancellationToken.Token.IsCancellationRequested && !players.All(x => x.StreamScreenCoordinates != null))
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
                            var player = players.FirstOrDefault(x => Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{x.UserId} {Match.Guid}") == result.Text);
                            if (player == null) continue;

                            Logger.Debug($"{player.Name} QR DETECTED");
                            var point = new User.Point
                            {
                                X = (int)result.ResultPoints[3].X, //ResultPoints[3] is the qr location square closest to the center of the qr. The oddball.
                                Y = (int)result.ResultPoints[3].Y
                            };
                            player.StreamScreenCoordinates = point;
                        }

                        //Logging
                        var missing = players.Where(x => x.StreamScreenCoordinates == null).Select(x => x.Name);
                        var missingLog = "Can't see QR for: ";
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";
                        LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run(missingLog + "\n") { Foreground = Brushes.Yellow }));
                    }
                }

                allPlayersLocated(!_syncCancellationToken.Token.IsCancellationRequested);
            };

            Func<Task> waitForPlayersToDownloadQr = async () =>
            {
                //Wait for players to download the QR file
                List<Guid> _playersWhoHaveDownloadedQrImage = new List<Guid>();
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);
                var players = GetPlayersInMatch();

                Func<Response.ImagePreloaded, Guid, Task> qrImagePreloaded = (Response.ImagePreloaded a, Guid from) =>
                {
                    if (players.Select(x => x.Guid).Contains(from.ToString())) _playersWhoHaveDownloadedQrImage.Add(from);
                    return Task.CompletedTask;
                };
                MainPage.Client.ImagePreloaded += qrImagePreloaded;

                //Loop through players and send the QR for them to display (but don't display it yet)
                //Also reset their stream syncing values to default
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].StreamDelayMs = 0;
                    players[i].StreamScreenCoordinates = null;
                    players[i].StreamSyncStartMs = 0;

                    await MainPage.Client.Send(
                        Guid.Parse(players[i].Guid),
                        new Packet
                        {
                            Request = new Request
                            {
                                preload_image_for_stream_sync = new Request.PreloadImageForStreamSync
                                {
                                    Data = QRUtils.GenerateQRCodePngBytes(Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{players[i].UserId} {Match.Guid}"))
                                }
                            }
                        }
                    );
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !players.Select(x => x.Guid).All(x => _playersWhoHaveDownloadedQrImage.Contains(Guid.Parse(x)))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                MainPage.Client.ImagePreloaded -= qrImagePreloaded;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = players.Where(x => !_playersWhoHaveDownloadedQrImage.Contains(Guid.Parse(x.Guid))).Select(x => x.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                    // add seconds to account for loading into the map
                    Match.StartTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffZZZ");
                    await MainPage.Client.UpdateMatch(Match);

                    await SendToPlayers(new Packet
                    {
                        Command = new Command()
                        {
                            DelayTestFinish = true
                        }
                    });

                    Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                    return;
                }

                var scanTask = Task.Run(scanForQrCodes);

                //All players should be loaded in by now, so let's get the players to show their location QRs
                await SendToPlayers(new Packet
                {
                    Command = new Command()
                    {
                        StreamSyncShowImage = true
                    }
                });
            };

            //This call not awaited intentionally
            Task.Run(waitForPlayersToDownloadQr);

            Console.WriteLine("a)");
            return Task.CompletedTask;
        }

        private async Task PlayersCompletedSync(bool successfully)
        {
            // add seconds to account for loading into the map
            Match.StartTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffZZZ");
            await MainPage.Client.UpdateMatch(Match);

            if (successfully)
            {
                Logger.Success("All players synced successfully, starting matches with delay...");

                //Send "continue" to players, but with their delay accounted for
                SendToPlayersWithDelay(new Packet
                {
                    Command = new Command()
                    {
                        DelayTestFinish = true
                    }
                });
            }
            else
            {
                Logger.Error("Failed to sync players, falling back to normal play");
                await SendToPlayers(new Packet
                {
                    Command = new Command()
                    {
                        DelayTestFinish = true
                    }
                });
            }
        }

        private bool PlaySong_CanExecute(object arg) => !SongLoading && DifficultyDropdown.SelectedItem != null && _matchPlayersHaveDownloadedSong && GetPlayersInMatch().All(x => x.PlayState == User.PlayStates.Waiting);

        private async void CheckForBannedMods_Executed(object obj)
        {
            //Check for banned mods before continuing
            if (MainPage.Client.State.ServerSettings.BannedMods.Count > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in GetPlayersInMatch())
                {
                    string bannedMods = string.Join(", ", player.ModLists.Intersect(MainPage.Client.State.ServerSettings.BannedMods));
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

        private async void ReturnToMenu_Executed(object obj)
        {
            _syncCancellationToken?.Cancel();

            await SendToPlayers(new Packet
            {
                Command = new Command
                {
                    ReturnToMenu = true,
                }
            });
        }

        private bool ReturnToMenu_CanExecute(object arg) => !SongLoading && GetPlayersInMatch().Any(x => x.PlayState == User.PlayStates.InGame);

        private void DestroyAndCloseMatch_Executed(object obj)
        {
            if (MainPage.DestroyMatch.CanExecute(Match)) MainPage.DestroyMatch.Execute(Match);
        }

        private void ClosePage_Executed(object obj)
        {
            MainPage.Client.MatchInfoUpdated -= Connection_MatchInfoUpdated;
            MainPage.Client.MatchDeleted -= Connection_MatchDeleted;
            MainPage.Client.PlayerFinishedSong -= Connection_PlayerFinishedSong;
            MainPage.Client.UserInfoUpdated -= Connection_UserInfoUpdated;

            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.GoBack();
        }

        private bool ClosePage_CanExecute(object arg)
        {
            return (MainPage.Client.Self.Guid == Guid.Empty.ToString() || MainPage.Client.Self.Name == "Moon" || MainPage.Client.Self.Name == "Olaf");
        }

        private async void CharacteristicBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedItem != null)
            {
                var oldCharacteristic = Match.SelectedCharacteristic?.SerializedName;

                Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == (string)(sender as ComboBox).SelectedItem);

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.SelectedCharacteristic.SerializedName != oldCharacteristic) await MainPage.Client.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private async void DifficultyDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyDropdown.SelectedItem != null)
            {
                var oldDifficulty = Match.SelectedDifficulty;

                Match.SelectedDifficulty = Match.SelectedCharacteristic.Difficulties.First(x => ((Constants.BeatmapDifficulty)x).ToString() == DifficultyDropdown.SelectedItem.ToString());

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.SelectedDifficulty != oldDifficulty) await MainPage.Client.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private async Task SendToPlayers(Packet packet)
        {
            var playersText = string.Empty;
            foreach (var player in GetPlayersInMatch()) playersText += $"{player.Name}, ";
            Logger.Debug($"Sending {packet.packetCase} to {playersText}");
            await MainPage.Client.Send(GetPlayersInMatch().Select(x => Guid.Parse(x.Guid)).ToArray(), packet);
        }

        private void SendToPlayersWithDelay(Packet packet)
        {
            var players = GetPlayersInMatch();
            var maxDelay = players.Max(x => x.StreamDelayMs);

            foreach (var player in players)
            {
                Task.Run(() =>
                {
                    Logger.Debug($"Sleeping {(int)maxDelay - (int)player.StreamDelayMs} ms for {player.Name}");
                    Thread.Sleep((int)maxDelay - (int)player.StreamDelayMs);
                    Logger.Debug($"Sending start to {player.Name}");
                    MainPage.Client.Send(Guid.Parse(player.Guid), packet);
                });
            }
        }
    }
}
