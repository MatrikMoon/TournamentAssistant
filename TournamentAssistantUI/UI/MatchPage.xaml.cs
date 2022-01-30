using Google.Protobuf;
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
using TournamentAssistantShared.Utillities;
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

        private List<SongFinished> _levelCompletionResults = new();
        public event Action AllPlayersFinishedSong;

        public MainPage MainPage{ get; set; }

        public ICommand LoadSong { get; }
        public ICommand PlaySong { get; }
        public ICommand PlaySongWithDualSync { get; }
        public ICommand PlaySongWithDelayedStart { get; }
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
            MainPage.Client.PlayerInfoUpdated += Connection_PlayerInfoUpdated;

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
                if (MainPage.Client.State.ServerSettings.EnableTeams)
                {
                    await DialogHost.Show(new GameOverDialogTeams(_levelCompletionResults), "RootDialog");
                }
                else await DialogHost.Show(new GameOverDialog(_levelCompletionResults), "RootDialog");
            });
        }

        private Task Connection_PlayerFinishedSong(SongFinished results)
        {
            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{results.Player.User.Name} has scored {results.Score}\n")));

            if (Match.Players.Select(x => x.User.Id).Contains(results.Player.User.Id)) _levelCompletionResults.Add(results);

            var playersText = string.Empty;
            foreach (var matchPlayer in Match.Players) playersText += $"{matchPlayer.User.Name}, ";
            Logger.Debug($"{results.Player.User.Name} FINISHED SONG, FOR A TOTAL OF {_levelCompletionResults.Count} FINISHED PLAYERS OUT OF {Match.Players.Count}");
            if (_levelCompletionResults.Count == Match.Players.Count)
            {
                AllPlayersFinishedSong?.Invoke();
            }
            return Task.CompletedTask;
        }

        private Task Connection_PlayerInfoUpdated(Player player)
        {
            //If the updated player is part of our match 
            var index = Match.Players.ToList().FindIndex(x => x.User.Id == player.User.Id);
            if (index >= 0)
            {
                Match.Players[index] = player;

                //Check for potential events we'd need to fire
                var oldMatchPlayersHaveDownloadedSong = _matchPlayersHaveDownloadedSong;
                var oldMatchPlayersAreInGame = _matchPlayersAreInGame;

                _matchPlayersHaveDownloadedSong = Match.Players.All(x => x.DownloadState == Player.Types.DownloadStates.Downloaded);
                _matchPlayersAreInGame = Match.Players.All(x => x.PlayState == Player.Types.PlayStates.InGame);

                if (!oldMatchPlayersHaveDownloadedSong && _matchPlayersHaveDownloadedSong) PlayersDownloadedSong?.Invoke();
                if (!oldMatchPlayersAreInGame && _matchPlayersAreInGame) PlayersAreInGame?.Invoke();
            }
            return Task.CompletedTask;
        }

        private Task Connection_MatchInfoUpdated(Match updatedMatch)
        {
            if (updatedMatch == Match)
            {
                Match = updatedMatch;

                //If the Match has a song now, be super sure the song box is enabled
                if (Match.SelectedLevel != null) SongBox.Dispatcher.Invoke(() => SongBox.IsEnabled = true);
            }
            return Task.CompletedTask;
        }

        private Task Connection_MatchDeleted(Match deletedMatch)
        {
            if (deletedMatch.Equals(Match))
            {
                MainPage.Client.MatchInfoUpdated -= Connection_MatchInfoUpdated;
                MainPage.Client.MatchDeleted -= Connection_MatchDeleted;
                MainPage.Client.PlayerFinishedSong -= Connection_PlayerFinishedSong;
                MainPage.Client.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;

                Dispatcher.Invoke(() => ClosePage.Execute(this));
            }
            return Task.CompletedTask;
        }

        private void KickPlayer_Executed(object parameter)
        {
            //Remove player from list
            var playerToRemove = Match.Players.FirstOrDefault(x => x.User.UserEquals((parameter as Player).User));
            Match.Players.Remove(playerToRemove);

            //Notify all the UI that needs to be notified, and propegate the info across the network
            NotifyPropertyChanged(nameof(Match));

            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            MainPage.Client.UpdateMatch(Match);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
                    (int)SharedConstructs.BeatmapDifficulty.Easy,
                    (int)SharedConstructs.BeatmapDifficulty.Normal,
                    (int)SharedConstructs.BeatmapDifficulty.Hard,
                    (int)SharedConstructs.BeatmapDifficulty.Expert,
                    (int)SharedConstructs.BeatmapDifficulty.ExpertPlus,
                };

                var standardCharacteristic = new Characteristic()
                {
                    SerializedName = "Standard"
                };
                standardCharacteristic.Difficulties.AddRange(allDifficulties);

                var noArrowsCharacteristic = new Characteristic()
                {
                    SerializedName = "NoArrows"
                };
                noArrowsCharacteristic.Difficulties.AddRange(allDifficulties);

                var oneSaberCharacteristic = new Characteristic()
                {
                    SerializedName = "OneSaber"
                };
                oneSaberCharacteristic.Difficulties.AddRange(allDifficulties);

                var ninetyDegreeCharacteristic = new Characteristic()
                {
                    SerializedName = "90Degree"
                };
                ninetyDegreeCharacteristic.Difficulties.AddRange(allDifficulties);

                var threeSixtyDegreeCharacteristic = new Characteristic()
                {
                    SerializedName = "360Degree"
                };
                threeSixtyDegreeCharacteristic.Difficulties.AddRange(allDifficulties);

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
                Match.SelectedDifficulty = (int)SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                //Notify all the UI that needs to be notified, and propegate the info across the network
                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));

                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                MainPage.Client.UpdateMatch(Match);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                var loadSong = new LoadSong
                {
                    LevelId = Match.SelectedLevel.LevelId
                };
                await SendToPlayers(new Packet
                {
                    LoadSong = loadSong
                });
            }
            else
            {
                //If we're using a custom host, we don't need to find a new hash, we can just download it by id
                try
                {
                    var hash = BeatSaverDownloader.GetHashFromID(songId);
                    BeatSaverDownloader.DownloadSongThreaded(hash,
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
                                    var newCharacteristic = new Characteristic()
                                    {
                                        SerializedName = characteristic,
                                    };
                                    newCharacteristic.Difficulties.Add(song.GetBeatmapDifficulties(characteristic).Select(x => (int)x));
                                    characteristics.Add(newCharacteristic);
                                }
                                matchMap.Characteristics.AddRange(characteristics.ToArray());
                                Match.SelectedLevel = matchMap;
                                Match.SelectedCharacteristic = null;
                                Match.SelectedDifficulty = (int)SharedConstructs.BeatmapDifficulty.Easy; //Easy, aka 0, aka null

                                //Notify all the UI that needs to be notified, and propegate the info across the network
                                Dispatcher.Invoke(() => NotifyPropertyChanged(nameof(Match)));

                                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                MainPage.Client.UpdateMatch(Match);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                                //Once we've downloaded it as the coordinator, we know it's a-ok for players to download too
                                var loadSong = new LoadSong
                                {
                                    LevelId = Match.SelectedLevel.LevelId,
                                };
                                if (!string.IsNullOrWhiteSpace(customHost)) loadSong.CustomHostUrl = customHost;

                                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                SendToPlayers(new Packet
                                {
                                    LoadSong = loadSong
                                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
            if (!Match.Players.All(x => x.PlayState == Player.Types.PlayStates.Waiting)) return;

            await SetUpAndPlaySong();
        }

        private async Task<bool> SetUpAndPlaySong(bool useSync = false)
        {
            //Check for banned mods before continuing
            if (MainPage.Client.State.ServerSettings.BannedMods.Count > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in Match.Players)
                {
                    string bannedMods = string.Join(", ", player.ModList.Intersect(MainPage.Client.State.ServerSettings.BannedMods));
                    if (bannedMods != string.Empty) playersWithBannedMods += $"{player.User.Name}: {bannedMods}\n";
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
            if ((bool)NoFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.NoFail;
            if ((bool)DisappearingArrowsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.DisappearingArrows;
            if ((bool)GhostNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.GhostNotes;
            if ((bool)FastNotesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.FastNotes;
            if ((bool)SlowSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.SlowSong;
            if ((bool)FastSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.FastSong;
            if ((bool)SuperFastSongBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.SuperFastSong;
            if ((bool)InstaFailBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.InstaFail;
            if ((bool)FailOnSaberClashBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.FailOnClash;
            if ((bool)BatteryEnergyBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.BatteryEnergy;
            if ((bool)NoBombsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.NoBombs;
            if ((bool)NoWallsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.NoObstacles;
            if ((bool)NoArrowsBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.NoArrows;
            if ((bool)ProModeBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.ProMode;
            if ((bool)ZenModeBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.ZenMode;
            if ((bool)SmallCubesBox.IsChecked) gm.Options = gm.Options | GameplayModifiers.Types.GameOptions.SmallCubes;

            var playSong = new PlaySong();
            var gameplayParameters = new GameplayParameters
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
            };

            playSong.GameplayParameters = gameplayParameters;
            playSong.FloatingScoreboard = (bool)ScoreboardBox.IsChecked;
            playSong.StreamSync = useSync;
            playSong.DisableFail = (bool)DisableFailBox.IsChecked;
            playSong.DisablePause = (bool)DisablePauseBox.IsChecked;
            playSong.DisableScoresaberSubmission = (bool)DisableScoresaberBox.IsChecked;
            playSong.ShowNormalNotesOnStream = (bool)ShowNormalNotesBox.IsChecked;

            await SendToPlayers(new Packet
            {
                PlaySong = playSong
            });

            return true;
        }

        private async void PlaySongWithDualSync_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.Types.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += DoDualSync;
        }

        private async void PlaySongWithDelayedStart_Executed(object obj)
        {
            //If not all players are in the waiting room, don't play
            //Aka: don't play if the players are already playing a song
            if (!Match.Players.All(x => x.PlayState == Player.Types.PlayStates.Waiting)) return;

            if (await SetUpAndPlaySong(true)) PlayersAreInGame += async () =>
            {
                await Task.Delay(5000);

                //Send "continue" to players
                await SendToPlayers(new Packet
                {
                    Command = new Command()
                    {
                        CommandType = Command.Types.CommandTypes.DelayTestFinish
                    }
                });
            };
        }

        private Task DoDualSync()
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

            Func<bool, Task> allPlayersLocated = async (locationSuccess) =>
            {
                Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                Func<bool, Task> allPlayersSynced = PlayersCompletedSync;
                if (locationSuccess)
                {
                    Logger.Debug("LOCATED ALL PLAYERS");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Players located. Waiting for green screen...\n") { Foreground = Brushes.Yellow })); ;

                    //Wait for players to download the green file
                    List<Guid> _playersWhoHaveDownloadedGreenImage = new List<Guid>();
                    _syncCancellationToken?.Cancel();
                    _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                    Func<Acknowledgement, Guid, Task> greenAckReceived = (Acknowledgement a, Guid from) =>
                    {
                        if (a.Type == Acknowledgement.Types.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.User.Id).Contains(from.ToString())) _playersWhoHaveDownloadedGreenImage.Add(from);
                        return Task.CompletedTask;
                    };
                    MainPage.Client.AckReceived += greenAckReceived;

                    //Send the green background
                    using (var greenBitmap = QRUtils.GenerateColoredBitmap())
                    {
                        var file = new File();
                        file.Data = ByteString.CopyFrom(QRUtils.ConvertBitmapToPngBytes(greenBitmap));
                        file.Intent = File.Types.Intentions.SetPngToShowWhenTriggered;
                        await SendToPlayers(new Packet
                        {
                            File = file
                        });
                    }

                    //TODO: Use proper waiting
                    while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.User.Id).All(x => _playersWhoHaveDownloadedGreenImage.Contains(Guid.Parse(x)))) await Task.Delay(0);

                    //If a player failed to download the background, bail            
                    MainPage.Client.AckReceived -= greenAckReceived;
                    if (_syncCancellationToken.Token.IsCancellationRequested)
                    {
                        var missingLog = string.Empty;
                        var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedGreenImage.Contains(Guid.Parse(x.User.Id))).Select(x => x.User.Name);
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                        Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                        LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                        await allPlayersSynced(false);

                        return;
                    }

                    //Set up color listener
                    List<PixelReader> pixelReaders = new List<PixelReader>();
                    for (int i = 0; i < Match.Players.Count; i++)
                    {
                        int playerId = i;
                        pixelReaders.Add(new PixelReader(new Point(Match.Players[i].StreamScreenCoordinates.X, Match.Players[i].StreamScreenCoordinates.Y), (color) =>
                        {
                            return (Colors.Green.R - 50 <= color.R && color.R <= Colors.Green.R + 50) &&
                                (Colors.Green.G - 50 <= color.G && color.G <= Colors.Green.G + 50) &&
                                (Colors.Green.B - 50 <= color.B && color.B <= Colors.Green.B + 50);

                        }, () =>
                        {
                            Match.Players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Match.Players[playerId].StreamSyncStartMs;
                            
                            LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"DETECTED: {Match.Players[playerId].User.Name} (delay: {Match.Players[playerId].StreamDelayMs})\n") { Foreground = Brushes.YellowGreen })); ;

                            //Send updated delay info

                            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            MainPage.Client.UpdatePlayer(Match.Players[playerId]);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                            if (Match.Players.All(x => x.StreamDelayMs > 0))
                            {
                                LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("All players successfully synced. Sending PlaySong\n") { Foreground = Brushes.Green })); ;
                                allPlayersSynced.Invoke(true);
                            }
                        }));
                    }

                    //Loop through players and set their sync init time
                    for (int i = 0; i < Match.Players.Count; i++)
                    {
                        Match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    }

                    //Start watching pixels for color change
                    pixelReaders.ForEach(x => x.StartWatching());

                    //Show the green
                    await SendToPlayers(new Packet
                    {
                        Command = new Command()
                        {
                            CommandType = Command.Types.CommandTypes.ScreenOverlayShowPng
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

                //While not 20 seconds elapsed and not all players have locations
                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.All(x => !x.StreamScreenCoordinates.Equals(default)))
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

                            Logger.Debug($"{player.User.Name} QR DETECTED");
                            var point = new Player.Types.Point
                            {
                                X = (int)result.ResultPoints[3].X, //ResultPoints[3] is the qr location square closest to the center of the qr. The oddball.
                                Y = (int)result.ResultPoints[3].Y
                            };
                            player.StreamScreenCoordinates = point;
                        }

                        //Logging
                        var missing = Match.Players.Where(x => x.StreamScreenCoordinates.Equals(default(Player.Types.Point))).Select(x => x.User.Name);
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

                Func<Acknowledgement, Guid, Task> ackReceived = (Acknowledgement a, Guid from) =>
                {
                    if (a.Type == Acknowledgement.Types.AcknowledgementType.FileDownloaded && Match.Players.Select(x => x.User.Id).Contains(from.ToString())) _playersWhoHaveDownloadedQrImage.Add(from);
                    return Task.CompletedTask;
                };
                MainPage.Client.AckReceived += ackReceived;

                //Loop through players and send the QR for them to display (but don't display it yet)
                //Also reset their stream syncing values to default
                for (int i = 0; i < Match.Players.Count; i++)
                {
                    Match.Players[i].StreamDelayMs = 0;
                    Match.Players[i].StreamScreenCoordinates = default;
                    Match.Players[i].StreamSyncStartMs = 0;

                    var file = new File();
                    file.Data = ByteString.CopyFrom(QRUtils.GenerateQRCodePngBytes(Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{Match.Players[i].UserId} {Match.Guid}")));
                    file.Intent = File.Types.Intentions.SetPngToShowWhenTriggered;

                    await MainPage.Client.Send(
                        Guid.Parse(Match.Players[i].User.Id),
                        new Packet(file)
                    );
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !Match.Players.Select(x => x.User.Id).All(x => _playersWhoHaveDownloadedQrImage.Contains(Guid.Parse(x)))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                MainPage.Client.AckReceived -= ackReceived;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = Match.Players.Where(x => !_playersWhoHaveDownloadedQrImage.Contains(Guid.Parse(x.User.Id))).Select(x => x.User.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                    await SendToPlayers(new Packet
                    {
                        Command = new Command()
                        {
                            CommandType = Command.Types.CommandTypes.DelayTestFinish
                        }
                    });

                    Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                    return;
                }

                new Task(scanForQrCodes).Start();

                //All players should be loaded in by now, so let's get the players to show their location QRs
                await SendToPlayers(new Packet
                {
                    Command = new Command()
                    {
                        CommandType = Command.Types.CommandTypes.ScreenOverlayShowPng
                    }
                });
            };

            //This call not awaited intentionally
            new Task(async () => await waitForPlayersToDownloadQr()).Start();
            return Task.CompletedTask;
        }

        private async Task PlayersCompletedSync(bool successfully)
        {
            if (successfully)
            {
                Logger.Success("All players synced successfully, starting matches with delay...");

                //Send "continue" to players, but with their delay accounted for
                SendToPlayersWithDelay(new Packet
                {
                    Command = new Command()
                    {
                        CommandType = Command.Types.CommandTypes.DelayTestFinish
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
                        CommandType = Command.Types.CommandTypes.DelayTestFinish
                    }
                });
            }
        }

        private bool PlaySong_CanExecute(object arg) => !SongLoading && DifficultyDropdown.SelectedItem != null && _matchPlayersHaveDownloadedSong && Match.Players.All(x => x.PlayState == Player.Types.PlayStates.Waiting);

        private async void CheckForBannedMods_Executed(object obj)
        {
            //Check for banned mods before continuing
            if (MainPage.Client.State.ServerSettings.BannedMods.Count > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in Match.Players)
                {
                    string bannedMods = string.Join(", ", player.ModList.Intersect(MainPage.Client.State.ServerSettings.BannedMods));
                    if (bannedMods != string.Empty) playersWithBannedMods += $"{player.User.Name}: {bannedMods}\n";
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

            var returnToMenu = new Command
            {
                CommandType = Command.Types.CommandTypes.ReturnToMenu
            };
            await SendToPlayers(new Packet
            {
                Command = returnToMenu
            });
        }

        private bool ReturnToMenu_CanExecute(object arg) => !SongLoading && Match.Players.Any(x => x.PlayState == Player.Types.PlayStates.InGame);

        private void DestroyAndCloseMatch_Executed(object obj)
        {
            if (MainPage.DestroyMatch.CanExecute(Match)) MainPage.DestroyMatch.Execute(Match);
        }

        private void ClosePage_Executed(object obj)
        {
            MainPage.Client.MatchInfoUpdated -= Connection_MatchInfoUpdated;
            MainPage.Client.MatchDeleted -= Connection_MatchDeleted;
            MainPage.Client.PlayerFinishedSong -= Connection_PlayerFinishedSong;
            MainPage.Client.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;

            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.GoBack();
        }

        private bool ClosePage_CanExecute(object arg)
        {
            return (MainPage.Client.Self.Id == Guid.Empty.ToString() || MainPage.Client.Self.Name == "Moon" || MainPage.Client.Self.Name == "Olaf");
        }

        private async void CharacteristicBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedItem != null)
            {
                var oldCharacteristic = Match.SelectedCharacteristic;

                Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == ((Characteristic)(sender as ComboBox).SelectedItem).SerializedName);

                //When we update the match, we actually get back an UpdateMatch event which causes this very same event again...
                //Usually I handle this infinite recursion by letting the Events control all the user controls, but that's
                //not possible in this case. So here, we're specifically not going to send an UpdateMatch event if
                //nothing changed because of the selection. It's hacky, and doesn't prevent *all* of the technically excess events
                //from being sent, but it works, so it's here.
                if (Match.SelectedCharacteristic != oldCharacteristic) await MainPage.Client.UpdateMatch(Match);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        private async void DifficultyDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyDropdown.SelectedItem != null)
            {
                var oldDifficulty = Match.SelectedDifficulty;
                
                Match.SelectedDifficulty = Match.SelectedCharacteristic.Difficulties.First(x => ((SharedConstructs.BeatmapDifficulty)x).ToString() == DifficultyDropdown.SelectedItem.ToString());

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
            foreach (var player in Match.Players) playersText += $"{player.User.Name}, ";
            Logger.Debug($"Sending {packet.PacketCase.ToString()} to {playersText}");
            await MainPage.Client.Send(Match.Players.Select(x => Guid.Parse(x.User.Id)).ToArray(), packet);
        }

        private void SendToPlayersWithDelay(Packet packet)
        {
            var maxDelay = Match.Players.Max(x => x.StreamDelayMs);

            foreach (var player in Match.Players)
            {
                Task.Run(() =>
                {
                    Logger.Debug($"Sleeping {(int)maxDelay - (int)player.StreamDelayMs} ms for {player.User.Name}");
                    Thread.Sleep((int)maxDelay - (int)player.StreamDelayMs);
                    Logger.Debug($"Sending start to {player.User.Name}");
                    MainPage.Client.Send(Guid.Parse(player.User.Id), packet);
                });
            }
        }
    }
}
