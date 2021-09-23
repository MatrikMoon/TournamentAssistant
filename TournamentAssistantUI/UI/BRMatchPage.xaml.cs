using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
using static TournamentAssistantShared.GlobalConstants;
using static TournamentAssistantShared.SharedConstructs;
using Application = System.Windows.Application;
using File = System.IO.File;
using FileModel = TournamentAssistantShared.Models.Packets.File;
using MessageBox = System.Windows.Forms.MessageBox;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for BRMatchPage.xaml
    /// </summary>
    public partial class BRMatchPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private MainPage _mainPage;
        private Match _match;
        public ICommand ButtonBackCommand { get; }
        public ICommand DestroyMatch { get; }
        public ICommand AddSong { get; }
        public ICommand LoadPlaylist { get; }
        public ICommand UnLoadPlaylist { get; }
        public ICommand DownloadAll { get; }
        public ICommand CancelDownload { get; }
        public ICommand DownloadSong { get; }
        public ICommand LoadNext { get; }
        public ICommand PlaySong { get; }
        public ICommand ReplayCurrent { get; }
        public ICommand PlayerControlPanelPlayCommand { get; }
        public ICommand PlayerControlPanelPauseCommand { get; }
        public ICommand PlayerControlPanelStopCommand { get; }
        public ICommand LoadRuleFile { get; }

        private NavigationService navigationService = null;
        public ObservableCollection<string> PlaylistLocation_Source { get; set; }
        public ObservableCollection<string> RuleFileLocation_Source { get; set; }
        public ObservableCollection<RoomRule> RoomRules { get; set; }
        public Playlist Playlist { get; set; }
        public Screen[] Screens { get; } = Screen.AllScreens;

        private MusicPlayer MusicPlayer = new();

        public PlaylistItem LoadedSong { get; set; }
        private CancellationTokenSource TokenSource { get; set; }
        private CancellationTokenSource _syncCancellationToken;
        private bool DownloadAttemptRunning = false;
        private bool StreamSyncRunning { get; set; }

        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;

        private event Action PlayersAreInGame;
        public BRMatchPage(MainPage mainPage, Match match)
        {

            InitializeComponent();

            DataContext = this;

            PlaylistLocation_Source = new ObservableCollection<string> //At some point I would like to save this across sessions and re-load it, so it could make choosing a playlist easier, but this is enough for now
            {
                "",
                "<<< Select from filesystem >>>"
            };

            RuleFileLocation_Source = new ObservableCollection<string> //At some point I would like to save this across sessions and re-load it, so it could make choosing a playlist easier, but this is enough for now
            {
                "",
                "<<< Select from filesystem >>>"
            };

            _mainPage = mainPage;
            _match = match;

            ButtonBackCommand = new CommandImplementation(ButtonBack_Executed, (_) => true);
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);
            AddSong = new CommandImplementation(AddSong_Executed, AddSong_CanExecute);
            LoadPlaylist = new CommandImplementation(LoadPlaylist_Executed, LoadPlaylist_CanExecute);
            UnLoadPlaylist = new CommandImplementation(UnLoadPlaylist_Executed, (_) => true);
            DownloadAll = new CommandImplementation(DownloadAll_Executed, DownloadAll_CanExecute);
            CancelDownload = new CommandImplementation(CancelDownload_Executed, CancelDownload_CanExecute);
            DownloadSong = new CommandImplementation(DownloadSong_Executed, DownloadSong_CanExecute);
            LoadNext = new CommandImplementation(LoadNext_Executed, LoadNext_CanExecute);
            PlaySong = new CommandImplementation(PlaySong_Executed, PlaySong_CanExecute);
            ReplayCurrent = new CommandImplementation(ReplayCurrent_Executed, ReplayCurrent_CanExecute);
            PlayerControlPanelPlayCommand = new CommandImplementation(PlayerControlPanelPlayCommand_Executed, PlayerControlPanelPlayCommand_CanExecute);
            PlayerControlPanelPauseCommand = new CommandImplementation(PlayerControlPanelPauseCommand_Executed, PlayerControlPanelPauseCommand_CanExecute);
            PlayerControlPanelStopCommand = new CommandImplementation(PlayerControlPanelStopCommand_Executed, PlayerControlPanelStopCommand_CanExecute);
            LoadRuleFile = new CommandImplementation(LoadRuleFile_Executed, LoadRuleFile_CanExecute);

            MusicPlayer.player.Stopped += Player_Stopped;
            MusicPlayer.player.Paused += Player_Paused;
            MusicPlayer.player.Playing += Player_Playing;
            MusicPlayer.player.TimeChanged += Player_TimeChanged;

            _mainPage.Connection.PlayerFinishedSong += Connection_PlayerFinishedSong;
            _mainPage.Connection.PlayerInfoUpdated += Connection_PlayerInfoUpdated;
            _mainPage.Connection.MatchInfoUpdated += Connection_MatchInfoUpdated;
            _mainPage.Connection.MatchDeleted += Connection_MatchDeleted;

            StreamSyncScreenSelectionBox.SelectedItem = Screen.PrimaryScreen; //Initialize the combo box
        }

        private void Connection_MatchDeleted(Match match)
        {
            DeleteMatch(match);
        }

        private void Connection_PlayerInfoUpdated(Player player)
        {
            var index = _match.Players.ToList().FindIndex(x => x.Id == player.Id);
            if (index >= 0)
            {
                _match.Players[index] = player;

                if (_match.Players.All(player => player.PlayState == Player.PlayStates.InGame))
                {
                    PlayersAreInGame?.Invoke();
                }

                //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
                Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
            }
        }

        private void Connection_MatchInfoUpdated(Match match)
        {
            if (match.Guid == _match.Guid) _match = match;
        }

        private void Connection_PlayerFinishedSong(SongFinished obj)
        {
            //Moon's note: it may not be safe to assume that the player update comes before the finishedSong
            if (_match.Players.All(player => player.PlayState == Player.PlayStates.Waiting))
            {
                if (MusicPlayer.player.IsPlaying) MusicPlayer.player.Stop();

                Dispatcher.Invoke(new Action(() =>
                {
                    PlaySongButton.IsEnabled = true;
                    PlaySongButton.Content = "Play Song";
                    PlaySongButton.Visibility = Visibility.Hidden;

                    LoadNextButton.IsEnabled = true;
                    LoadNextButton.Visibility = Visibility.Visible;

                    ReplayCurrentButton.IsEnabled = true;
                    PlaylistSongTable.IsHitTestVisible = true;

                    for (int i = 0; i < Playlist.Songs.Count; i++)
                    {
                        if (Playlist.Songs[i].SongInfo.name == LoadedSong.SongInfo.name)
                        {
                            Playlist.Songs[i].Played = true;
                        }
                    }

                    Playlist.SelectedSong.Played = true;
                    LoadedSong.Played = true;
                    PlaylistSongTable.Items.Refresh();
                    if (RoomRules != null) KickPlayersWithRules();
                    if (Playlist.Songs.All(song => song.Played)) 
                    {
                        LoadNextButton.IsEnabled = false;
                        LoadNextButton.Content = "End of playlist";
                    }

                    //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
                    Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
                }));
            }
        }

        private void KickPlayersWithRules()
        {
            int amountPlayers = _match.Players.Count();
            var currentRule = RoomRules.Aggregate((ruleX, ruleY) => Math.Abs(ruleX.AmountOfPlayers - amountPlayers) < Math.Abs(ruleY.AmountOfPlayers - amountPlayers) ? ruleX : ruleY);

            var playersToBeKicked = _match.Players.OrderBy(x => x.Score).Take(currentRule.PlayersToKick);

            string names = "";
            foreach (var player in playersToBeKicked)
            {
                names += player.Name;
                names += ", ";
            }
            char[] trim = { ',', ' ' };
            names.TrimEnd(trim);

            var dialogResult = MessageBox.Show($"These players are about to be kicked, do you agree?\n{names}", "KickPlayers", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            switch (dialogResult)
            {
                case DialogResult.Yes:
                    if ((bool)EnableBotMessageBox.IsChecked &&
                        !string.IsNullOrWhiteSpace(BotChannelIDBox.Text) &&
                        ulong.TryParse(BotChannelIDBox.Text, out var botChannelId))
                    {
                        var topNumber = Math.Min(10, _match.Players.Length);

                        var eliminated = string.Join("\n", playersToBeKicked.Select(x => $"{x.Name}: {x.Score}"));
                        var topPlayers = string.Join("\n", _match.Players.OrderByDescending(x => x.Score).Take(topNumber).Select(x => $"{x.Name}: {x.Score}"));

                        var botMessage = $"{_match.SelectedLevel.Name} finished!\n\n" +
                            $"Eliminated Players:\n" +
                            $"```\n{eliminated}```\n" +
                            $"Top {topNumber} Players:\n" +
                            $"```{topPlayers}```";

                        _mainPage.Connection.Send(new Packet(new SendBotMessage()
                        {
                            Channel = new TournamentAssistantShared.Models.Discord.Channel()
                            {
                                Id = botChannelId,
                                Name = "Unknown"
                            },
                            Message = botMessage
                        })); //Sends to Host
                    }

                    //Moon's note: you can remove more than one player at a time. You should favor that over sending an individual match update for each player
                    //Remove players from list
                    var newPlayers = _match.Players.ToList();
                    newPlayers.RemoveAll(x => playersToBeKicked.Contains(x));
                    _match.Players = newPlayers.ToArray();

                    //Notify all the UI that needs to be notified, and propegate the info across the network
                    NotifyPropertyChanged(nameof(_match));
                    _mainPage.Connection.UpdateMatch(_match);
                    break;
                case DialogResult.No:
                    break;
                default:
                    break;
            }
        }

        #region CanExecute
        private bool LoadRuleFile_CanExecute(object arg)
        {
            return true;
        }

        private bool ReplayCurrent_CanExecute(object arg)
        {
            return LoadedSong != null;
        }

        private bool PlaySong_CanExecute(object arg)
        {
            if (_match.Players.All(player => player.DownloadState == Player.DownloadStates.Downloaded && player.PlayState != Player.PlayStates.InGame || player.DownloadState == Player.DownloadStates.DownloadError && player.PlayState != Player.PlayStates.InGame))
            {
                PlaySongButton.Content = "Play Song";
                return true;
            }
            else if (_match.Players.All(player => player.PlayState == Player.PlayStates.InGame && !StreamSyncRunning))
            {
                PlaySongButton.Content = "In Game";
                return false;
            }
            else if (StreamSyncRunning)
            {
                PlaySongButton.Content = "Syncing...";
                return false;
            }
            else if (_match.Players.Any(player => player.DownloadState == Player.DownloadStates.Downloading))
            {
                PlaySongButton.Content = "Players are downloading map...";
                return false;
            }
            else
            {
                PlaySongButton.Content = "Play Song";
                return false;
            }
        }

        private bool LoadNext_CanExecute(object arg)
        {
            return true;
        }

        private bool DownloadSong_CanExecute(object arg)
        {
            return Playlist?.SelectedSong != null;
        }

        private bool CancelDownload_CanExecute(object arg)
        {
            try
            {
                if (TokenSource != null) return TokenSource.Token.CanBeCanceled;
            }
            catch (ObjectDisposedException e)
            {
                Logger.Warning("Token already disposed");
                Logger.Warning(e.ToString());
            }

            //If TokenSource is null, we cannot execute
            return false;
        }

        private bool DownloadAll_CanExecute(object arg)
        {
            return Playlist != null && Playlist.Songs.Count > 1 && _match.Players.All(player => player.PlayState == Player.PlayStates.Waiting);
        }

        private bool LoadPlaylist_CanExecute(object arg)
        {
            return PlaylistLocationBox.Text != string.Empty;
        }

        private bool AddSong_CanExecute(object arg)
        {
            if (SongUrlBox.Text.Length > 0)
            {
                DownloadSongButton.Visibility = Visibility.Visible;
                LoadNextButton.Visibility = Visibility.Hidden;
                PlaySongButton.Visibility = Visibility.Hidden;
                return true;
            }
            return false;
        } 
        #endregion

        private void LoadRuleFile_Executed(object obj)
        {
            if (RoomRules == null) RoomRules = new ObservableCollection<RoomRule>();

            if (RuleLocationBox.Text == "<<< Select from filesystem >>>")
            {
                OpenFileDialog openFileDialog = new();
                switch (openFileDialog.ShowDialog())
                {
                    case DialogResult.OK:
                        if (RuleFileLocation_Source.Contains(openFileDialog.FileName))
                            RuleFileLocation_Source.Remove(openFileDialog.FileName); //Removing and re-adding is easier than moving index, so here we are
                        RuleFileLocation_Source.Insert(1, openFileDialog.FileName);
                        RuleLocationBox.SelectedIndex = 1;
                        break;
                    case DialogResult.Cancel:
                        Logger.Warning("Dialog box returned 'Cancel'");
                        return;
                    default: //Lets just not care about any other value
                        return;
                }
            }

            string ruleLocation = RuleLocationBox.Text;

            if (!File.Exists(ruleLocation))
            {
                return; //dialog later
            }

            RoomRules.Clear();

            var data = File.ReadAllLines(ruleLocation);

            foreach (var line in data)
            {
                var rule = line.Split(':');

                RoomRules.Add(new RoomRule()
                { 
                    AmountOfPlayers = Int32.Parse(rule[0]), 
                    PlayersToKick = Int32.Parse(rule[1]) 
                });
            }
            RuleTable.ItemsSource = RoomRules;
            RuleTable.Items.Refresh();
        }

        private void ButtonBack_Executed(object obj)
        {
            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(_mainPage);
        }

        private void DestroyMatch_Executed(object obj)
        {
            DeleteMatch(_match);
        }

        private void DeleteMatch(Match match)
        {
            if (match.Guid == _match.Guid)
            {
                _mainPage.Connection.MatchInfoUpdated -= Connection_MatchInfoUpdated;
                _mainPage.Connection.MatchDeleted -= Connection_MatchDeleted;
                _mainPage.Connection.PlayerFinishedSong -= Connection_PlayerFinishedSong;
                _mainPage.Connection.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;

                var navigationService = NavigationService.GetNavigationService(this);
                if (navigationService != null) navigationService.GoBack();
            }
        }

        //TODO: This logic should be mergable with PlaySong
        private async void ReplayCurrent_Executed(object obj)
        {
            //UpdateLoadedSong(); //A safeguard for "other curious coordinators" changing the loaded song
                                //Moon's note: Love the idea, but I may have broken it in this refactor. Commenting out for now

            var successfullyPlayed = await SetUpAndPlaySong(EnableStreamSyncBox.IsChecked);

            Dispatcher.Invoke(new Action(() =>
            {
                if (successfullyPlayed && !(bool)EnableStreamSyncBox.IsChecked)
                {
                    ReplayCurrentButton.IsEnabled = false;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    PlaySongButton.Visibility = Visibility.Visible;
                    MusicPlayer.player.Play();
                    PlaylistSongTable.IsHitTestVisible = false;
                }
                else if (successfullyPlayed && (bool)EnableStreamSyncBox.IsChecked)
                {
                    PlayersAreInGame += StreamSync;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        ReplayCurrentButton.IsEnabled = false;
                        LoadNextButton.Visibility = Visibility.Hidden;
                        StreamSyncRunning = true;
                        PlaySongButton.Visibility = Visibility.Visible;
                        PlaylistSongTable.IsHitTestVisible = false;
                    }));
                }
            }));
        }

        private async void PlaySong_Executed(object obj)
        {
            var successfullyPlayed = await SetUpAndPlaySong(EnableStreamSyncBox.IsChecked);

            Dispatcher.Invoke(new Action(() =>
            {
                if (successfullyPlayed && !(bool)EnableStreamSyncBox.IsChecked)
                {
                    MusicPlayer.player.Play();
                    PlaylistSongTable.IsHitTestVisible = false;
                }
                else if (successfullyPlayed && (bool)EnableStreamSyncBox.IsChecked)
                {
                    PlayersAreInGame += StreamSync;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        ReplayCurrentButton.IsEnabled = false;
                        LoadNextButton.Visibility = Visibility.Hidden;
                        StreamSyncRunning = true;
                        PlaySongButton.Visibility = Visibility.Visible;
                        PlaylistSongTable.IsHitTestVisible = false;
                    }));
                }
            }));

            //navigate to ingame page
        }

        private void LoadNext_Executed(object obj)
        {
            if ((bool)RandomPlaylistOrder.IsChecked)
            {
                var notPlayed = Playlist.Songs.Where(song => !song.Played).ToList();
                Random rnd = new Random();
                var song = notPlayed[rnd.Next(0, notPlayed.Count)];

                int index = Playlist.Songs.IndexOf(song);
                PlaylistSongTable.SelectedIndex = index;
            }
            else
            {
                int index = PlaylistSongTable.SelectedIndex;
                index++; //for some reason
                PlaylistSongTable.SelectedIndex = index;
            }

            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private async void DownloadSong_Executed(object obj)
        {
            if (MusicPlayer.player.IsPlaying)
                MusicPlayer.player.Stop();

            string songURLBoxText = string.Empty;
            bool useSongURL = SongUrlBox.Text.Length > 0;
            if (useSongURL) songURLBoxText = SongUrlBox.Text;

            DownloadAllButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            LoadNextButton.Visibility = Visibility.Hidden;
            ReplayCurrentButton.IsEnabled = false;
            DownloadSongButton.Content = "Processing...";
            DownloadSongButton.IsEnabled = false;

            TokenSource = new CancellationTokenSource();
            IProgress<int> progress = new Progress<int>(percent => DownloadProgressBar.Value = percent);

            BeatSaverDownloader_Ari beatSaverDownloader = new();

            //Moon's note: this is another place where I may have made this run on the main thread. Worth testing.
            PlaylistItem song;
            int songIndex = 0;
            PlaylistHandler playlistHandler = new PlaylistHandler(
                new Progress<int>(percent => Dispatcher.Invoke(new Action(() =>
                {
                    DownloadProgressBar.IsIndeterminate = false;
                    DownloadProgressBar.Value = percent;
                }))));

            if (useSongURL)
            {
                song = await BeatSaverDownloader_Ari.DownloadSongFromInfo(await BeatSaverDownloader_Ari.GetSongInfoByIDAsync(songURLBoxText, progress));
            }
            else
            {
                song = Playlist.SelectedSong;
                songIndex = Playlist.Songs.IndexOf(song);
            }

            var data = await BeatSaverDownloader_Ari.GetSong(song.SongInfo, progress);
            if (data == null)
            {
                var dialogResult = MessageBox.Show($"An error occured when trying to download song {song.SongInfo.name}\nAborting will remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Exclamation);
                switch (dialogResult)
                {
                    case DialogResult.Cancel:
                        return;
                    case DialogResult.Abort:
                        Playlist.Songs.Remove(song);
                        return;
                    case DialogResult.Retry:
                        beatSaverDownloader = new();
                        beatSaverDownloader.RetrySongDownloadFinished += BeatSaverDownloader_RetrySongDownloadFinished;
                        beatSaverDownloader.RetrySongDownloadAsync(song.SongInfo, progress);
                        return;
                    default:
                        return;
                }
            }

            song.SongDataPath = data; //Moon's note: this is literally the same object by reference as the line below, right? And also the line below that? Probably only need one of these
            Playlist.SelectedSong.SongDataPath = data;
            Playlist.Songs[songIndex].SongDataPath = data;

            LoadedSong = song;
            NotifyPropertyChanged(nameof(LoadedSong));
            MusicPlayer.LoadSong(LoadedSong);
            UpdateMusicPlayerTime();

            try
            {
                TokenSource.Dispose();
            }
            catch (ObjectDisposedException e)
            {
                Logger.Warning("Token already disposed");
                Logger.Warning(e.ToString());
            }
            TokenSource = null; //Regardless of if we encounter an exception, we need to set this to null

            Dispatcher.Invoke(() =>
            {
                UpdateCurrentMatchToNewLoadedSong();
                DownloadProgressBar.Visibility = Visibility.Hidden;
                DownloadSongButton.IsEnabled = true;
                DownloadSongButton.Visibility = Visibility.Hidden;
                DownloadSongButton.Content = "Download Song";
                PlaySongButton.Visibility = Visibility.Visible;
            });

            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private void CancelDownload_Executed(object obj)
        {
            CancelDownloadButton.Visibility = Visibility.Hidden;
            try
            {
                TokenSource.Cancel();
            }
            catch (ObjectDisposedException e)
            {
                Logger.Warning("Token already disposed");
                Logger.Warning(e.ToString());
            }
            finally
            {
                TokenSource.Dispose();
                TokenSource = null;
            }
            DownloadAllProgressBar.Visibility = Visibility.Hidden;
            ReplayCurrentButton.IsEnabled = true;
            LoadNextButton.IsEnabled = true;
            DownloadAllButton.IsEnabled = true;
            DownloadAllButton.Content = "Download All Now";
        }

        private void DownloadAll_Executed(object obj)
        {
            var dialogResult = MessageBox.Show(
                $"You are about to download {Playlist.Songs.Count} songs to yourself and ALL players in this room." +
                $"\n\n!!This could be considered API spam by some people!!" +
                $"\n\nWhile {SharedConstructs.Name} will adhere to all API ratelimits, please keep in mind that in some cases this will take a lot of time and API resources." +
                $"\n\nIf you can spare the time to wait for songs to download each round it is recommended to not use this feature." +
                $"\n\nDo you wish to proceed?", "Download all?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            switch (dialogResult)
            {
                case DialogResult.Yes:
                    break;
                case DialogResult.No:
                    return;
                default:
                    return;
            }

            DownloadAllButton.IsEnabled = false;
            DownloadAllButton.Content = "Processing...";
            CancelDownloadButton.Visibility = Visibility.Visible;
            DownloadAllProgressBar.Visibility = Visibility.Visible;
            if (PlaylistSongTable.SelectedIndex != 0)
            {
                LoadNextButton.IsEnabled = false;
                DownloadSongButton.Visibility = Visibility.Hidden;
            }
            else
            {
                LoadNextButton.Visibility = Visibility.Hidden;
                DownloadSongButton.IsEnabled = false;
            }

            TokenSource = new CancellationTokenSource();
            IProgress<int> progress = new Progress<int>(percent => DownloadAllProgressBar.Value = percent);

            BeatSaverDownloader_Ari beatSaverDownloader = new();
            beatSaverDownloader.SongDownloadFinished += BeatSaverDownloader_SongDownloadFinished;

            var songsToDownload = Playlist.Songs.Where(x => x.SongDataPath == null).Select(x => x.SongInfo);
            Task.Run(async () => await beatSaverDownloader.GetSongs(songsToDownload.ToArray(), progress, TokenSource.Token));
        }

        private async void BeatSaverDownloader_SongDownloadFinished(Dictionary<string, string> data)
        {
            foreach (var hash in data.Keys)
            {
                for (int i = 0; i < Playlist.Songs.Count; i++)
                {
                    var playlistItem = Playlist.Songs[i];
                    if (playlistItem.SongInfo.CurrentVersion.hash != hash) continue;

                    if (data[hash] == null)
                    {
                        var dialogResult = MessageBox.Show(
                            $"An error occured when trying to download song {playlistItem.SongInfo.name}" +
                            $"\nAborting will remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Exclamation);
                        switch (dialogResult)
                        {
                            case DialogResult.Ignore:
                                continue;
                            case DialogResult.Abort:
                                Playlist.Songs.RemoveAt(i);
                                continue;
                            case DialogResult.Retry:
                                DownloadAttemptRunning = true;
                                BeatSaverDownloader_Ari beatSaverDownloader = new();
                                beatSaverDownloader.RetrySongDownloadFinished += BeatSaverDownloader_RetrySongDownloadFinished;
                                beatSaverDownloader.RetrySongDownloadAsync(Playlist.Songs[i].SongInfo, new Progress<int>(percent => DownloadAllProgressBar.Value = percent));

                                while (DownloadAttemptRunning)
                                {
                                    Logger.Info("Waiting for download task...");
                                    await Task.Delay(1000); //!!
                                }
                                continue;
                            default:
                                break;
                        }
                    }

                    //Playlist.Songs[i].SongDataPath = data[hash];
                }
            }

            Dispatcher.Invoke(new Action(() => 
            {
                DownloadAllProgressBar.Visibility = Visibility.Hidden;
                DownloadAllButton.Content = "Downloading to players...";
            }));

            //Download to all clients
            foreach (var playlistItem in Playlist.Songs)
            {
                SendLoadSong(playlistItem.SongInfo);
                await Task.Delay(BeatsaverRateLimit); //Moon's note: maybe we should do this in a queue on the client side?

                var ignoredErrors = new List<Player>();
                while (_match.Players.All(player => player.DownloadState != Player.DownloadStates.Downloaded || ignoredErrors.Contains(player)))
                {
                    if (TokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Download cancelled");
                        break;
                    }
                    if (_match.Players.Any(player => player.DownloadState == Player.DownloadStates.DownloadError && !ignoredErrors.Contains(player)))
                    {
                        //I should do something about it, but there is no way to even know why the player has a download error, so lets just ignore it and notify of it.
                        var dialogResult = MessageBox.Show($"{_match.Players.Where(player => player.DownloadState == Player.DownloadStates.DownloadError && !ignoredErrors.Contains(player)).FirstOrDefault().Name} has reported a download error", "DownloadError", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        switch (dialogResult)
                        {
                            case DialogResult.OK:
                                break;
                            default:
                                break;
                        }

                        ignoredErrors.Add(_match.Players.Where(player => player.DownloadState == Player.DownloadStates.DownloadError && !ignoredErrors.Contains(player)).FirstOrDefault());
                    }
                    Logger.Info("Waiting for players to download..."); //!!
                    await Task.Delay(100);
                }

                if (TokenSource.IsCancellationRequested) break;

                //Give the client some exec time...
                await Task.Delay(300); //!!
            }

            await Task.Delay(1000); //!!

            UpdateCurrentMatchToNewLoadedSong();

            Dispatcher.Invoke(new Action(() =>
            {
                CancelDownloadButton.Visibility = Visibility.Hidden;
                try
                {
                    TokenSource.Dispose();
                }
                catch (ObjectDisposedException e)
                {
                    Logger.Warning("Token already disposed");
                    Logger.Warning(e.ToString());
                }
                finally
                {
                    TokenSource = null;
                }
                DownloadAllProgressBar.Visibility = Visibility.Hidden;
                ReplayCurrentButton.IsEnabled = true;
                LoadNextButton.IsEnabled = true;
                DownloadAllButton.IsEnabled = true;
                DownloadAllButton.Content = "Download All Now";
                LoadNextButton.IsEnabled = true;
                DownloadSongButton.Visibility = Visibility.Visible;
            }));
        }

        private void BeatSaverDownloader_RetrySongDownloadFinished(KeyValuePair<string, string> data)
        {
            var hash = data.Key;
            for (int i = 0; i < Playlist.Songs.Count; i++)
            {
                var song = Playlist.Songs[i];
                if (song.SongInfo.CurrentVersion.hash != hash) continue;

                if (data.Value == null)
                {
                    var dialogResult = MessageBox.Show($"Do you wish to remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    switch (dialogResult)
                    {
                        case DialogResult.Yes:
                            Playlist.Songs.RemoveAt(i);
                            DownloadAttemptRunning = false;
                            return;
                        case DialogResult.No:
                            DownloadAttemptRunning = false;
                            return;
                        default:
                            break;
                    }
                }

                Playlist.Songs[i].SongDataPath = data.Value;
            }

            DownloadAttemptRunning = false;
        }

        private void UnLoadPlaylist_Executed(object obj)
        {
            UnLoadPlaylistButton.IsEnabled = false;
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            Playlist.Songs.Clear();
            Playlist = null;
            LoadedSong = null;
            NotifyPropertyChanged(nameof(LoadedSong));

            PlaylistLoadingProgress.Visibility = Visibility.Hidden;
            UnLoadPlaylistButton.Visibility = Visibility.Hidden;
            UnLoadPlaylistButton.IsEnabled = true;
            LoadPlaylistButton.Visibility = Visibility.Visible;
            LoadPlaylistButton.IsEnabled = true;

            if (MusicPlayer.player.IsPlaying)
            {
                MusicPlayer.player.Stop();
            }
            MusicPlayer.player.Media = null;
        }

        private void LoadPlaylist_Executed(object obj)
        {
            
            if (PlaylistLocationBox.Text == "<<< Select from filesystem >>>")
            {
                OpenFileDialog openFileDialog = new();
                switch (openFileDialog.ShowDialog())
                {
                    case DialogResult.OK:
                        if (PlaylistLocation_Source.Contains(openFileDialog.FileName))
                            PlaylistLocation_Source.Remove(openFileDialog.FileName); //Removing and re-adding is easier than moving index, so here we are
                        PlaylistLocation_Source.Insert(1, openFileDialog.FileName);
                        PlaylistLocationBox.SelectedIndex = 1;
                        break;
                    case DialogResult.Cancel:
                        Logger.Warning("Dialog box returned 'Cancel'");
                        return;
                    default: //Lets just not care about any other value
                        return;
                }
            }

            LoadPlaylistButton.IsEnabled = false;
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            var playlistLocation = PlaylistLocationBox.Text;

            Task.Run(async () =>
            {
                PlaylistHandler playlistHandler = new PlaylistHandler(
                    new Progress<int>(percent => 
                    Dispatcher.Invoke(new Action(() =>
                    {
                        PlaylistLoadingProgress.IsIndeterminate = false;
                        PlaylistLoadingProgress.Value = percent;
                    }))));

                playlistHandler.PlaylistLoadComplete += PlaylistHandler_PlaylistLoadComplete;

                await playlistHandler.LoadPlaylist(playlistLocation);
            });
        }

        private void PlaylistHandler_PlaylistLoadComplete(Playlist playlist)
        {
            Playlist = playlist;
            LoadedSong = Playlist.SelectedSong; //Moon's note: there seem to be two of these
            Dispatcher.Invoke(new Action(() =>
            {
                PlaylistSongTable.ItemsSource = Playlist.Songs;
                PlaylistLoadingProgress.Visibility = Visibility.Hidden;
                PlaylistSongTable.SelectedIndex = 0;
                UnLoadPlaylistButton.Visibility = Visibility.Visible;

                LoadedSong = Playlist.SelectedSong; //Moon's note: there seem to be two of these

                if (LoadedSong.SongDataPath != null)
                {
                    MusicPlayer.LoadSong(LoadedSong);
                    UpdateMusicPlayerTime();
                    PlaySongButton.Visibility = Visibility.Visible;
                    DownloadSongButton.Visibility = Visibility.Hidden;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    ReplayCurrentButton.IsEnabled = false;

                    UpdateCurrentMatchToNewLoadedSong();
                }
                else
                {
                    PlaySongButton.Visibility = Visibility.Hidden;
                    DownloadSongButton.Visibility = Visibility.Visible;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    MusicPlayer.player.Media = null;
                }

                NotifyPropertyChanged(nameof(LoadedSong));
            }));

            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private async void AddSong_Executed(object obj)
        {
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            var song = await BeatSaverDownloader_Ari.DownloadSongFromInfo(await BeatSaverDownloader_Ari.GetSongInfoByIDAsync(SongUrlBox.Text));

            Dispatcher.Invoke(new Action(() =>
            {
                SongUrlBox.Text = "";
                if (Playlist == null) Playlist = new Playlist("Custom playlist", $"{_mainPage.Connection.Self.Name}");
                if (song != null)
                {
                    Playlist.Songs.Add(song);
                    Playlist.SelectedSong = Playlist.Songs.FirstOrDefault();
                }
                PlaylistLoadingProgress.Visibility = Visibility.Hidden;

                LoadedSong = Playlist.SelectedSong;
                if (LoadedSong.SongDataPath != null)
                {
                    MusicPlayer.LoadSong(LoadedSong);
                    UpdateMusicPlayerTime();
                    PlaySongButton.Visibility = Visibility.Visible;
                    DownloadSongButton.Visibility = Visibility.Hidden;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    ReplayCurrentButton.IsEnabled = false;

                    UpdateCurrentMatchToNewLoadedSong();
                }
                else
                {
                    PlaySongButton.Visibility = Visibility.Hidden;
                    DownloadSongButton.Visibility = Visibility.Visible;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    MusicPlayer.player.Media = null;
                }

                NotifyPropertyChanged(nameof(LoadedSong));

                PlaylistSongTable.ItemsSource = Playlist.Songs;
                PlaylistSongTable.SelectedIndex = 0;
                UnLoadPlaylistButton.Visibility = Visibility.Visible;
            }));

            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private void PreviewStartButton_Click(object sender, RoutedEventArgs e)
        {
            var startButton = sender as System.Windows.Controls.Button;
            var song = (sender as System.Windows.Controls.Button).DataContext as PlaylistItem;
            var grid = startButton.Parent as Grid;
            var progressBar = grid.Children[2] as System.Windows.Controls.ProgressBar;
            var stopButton = grid.Children[1] as System.Windows.Controls.Button;

            if (MusicPlayer.player.IsPlaying)
                MusicPlayer.player.Stop();

            startButton.Visibility = Visibility.Hidden;
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;

            EventHandler<EventArgs> handler = null;
            handler = (sender, args) =>
            {
                stopButton.Dispatcher.Invoke(new Action(() =>
                {
                    stopButton.Visibility = Visibility.Hidden;
                    progressBar.Visibility = Visibility.Hidden;
                    startButton.Visibility = Visibility.Visible;
                }));

                //Handle if song was loaded - put the media it had back
                //Running on a separate thread because of LibVLC bug
                Task.Run(() => 
                {
                    if (LoadedSong != null && LoadedSong.SongDataPath != null) MusicPlayer.LoadSong(LoadedSong);
                    else MusicPlayer.player.Media = null;

                    //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
                    Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
                });

                //Unsubscribe event handler after we are done with it
                //Yes I know how ugly this looks, and yes I know it can be done cleaner
                //If you dont like it feel free to implement a cleaner soulution :P
                //Moon's Note: Actually this one is pretty okay. A lot of beat saber does things the same way, iirc
                MusicPlayer.player.Stopped -= handler;
            };
            MusicPlayer.player.Stopped += handler;

            IProgress<int> progress = new Progress<int>(percent =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = percent;
                Logger.Debug($"Loading preview {percent}%");
                if (percent == 100)
                {
                    progressBar.Visibility = Visibility.Hidden;
                    stopButton.Visibility = Visibility.Visible;
                    var media = MusicPlayer.MediaInit($"{AppDataCache}{song.SongInfo.CurrentVersion.hash}\\preview.mp3");
                    MusicPlayer.player.Play(media);

                    //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
                    Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
                }
            });

            Task.Run(async () =>
            {
                if (!Directory.Exists($"{AppDataCache}{song.SongInfo.CurrentVersion.hash}")) Directory.CreateDirectory($"{AppDataCache}{song.SongInfo.CurrentVersion.hash}");
                if (!File.Exists($"{AppDataCache}{song.SongInfo.CurrentVersion.hash}\\preview.mp3"))
                {
                    using var client = new WebClient();
                    client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        progress.Report(e.ProgressPercentage);
                    };
                    string url = $"https://cdn.beatsaver.com/{song.SongInfo.CurrentVersion.hash.ToLower()}.mp3";
                    await client.DownloadFileTaskAsync(url, $"{AppDataCache}{song.SongInfo.CurrentVersion.hash}\\preview.mp3");
                }

                progress.Report(100);
            });
        }

        private void PreviewStopButton_Click(object sender, RoutedEventArgs e)
        {
            var stopButton = sender as System.Windows.Controls.Button;
            var grid = stopButton.Parent as Grid;
            var progressBar = grid.Children[2] as System.Windows.Controls.ProgressBar;
            var startButton = grid.Children[0] as System.Windows.Controls.Button;

            stopButton.Visibility = Visibility.Hidden;
            progressBar.Visibility = Visibility.Hidden;
            startButton.Visibility = Visibility.Visible;

            //Handle if song was loaded - put the media it had back
            if (LoadedSong != null && LoadedSong.SongDataPath != null) MusicPlayer.LoadSong(LoadedSong);
            else MusicPlayer.player.Media = null;

            MusicPlayer.player.Stop();

            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private void StreamSync()
        {
            PlayersAreInGame -= StreamSync;

            //Display screen highlighter
            Dispatcher.Invoke(() =>
            {
                if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
                {
                    _primaryDisplayHighlighter = new PrimaryDisplayHighlighter(StreamSyncScreenSelectionBox.SelectedItem as Screen);
                }

                _primaryDisplayHighlighter.Show();

                //LogBlock.Inlines.Add(new Run("Waiting for QR codes...\n") { Foreground = Brushes.Yellow });
            });

            Action<bool> allPlayersLocated = async (locationSuccess) =>
            {
                Dispatcher.Invoke(() => _primaryDisplayHighlighter.Close());

                Action<bool> allPlayersSynced = PlayersCompletedSync;
                if (locationSuccess)
                {
                    Logger.Debug("LOCATED ALL PLAYERS");
                    //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Players located. Waiting for green screen...\n") { Foreground = Brushes.Yellow })); ;

                    //Wait for players to download the green file
                    List<Guid> _playersWhoHaveDownloadedGreenImage = new List<Guid>();
                    _syncCancellationToken?.Cancel();
                    _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                    Action<Acknowledgement, Guid> greenAckReceived = (Acknowledgement a, Guid from) =>
                    {
                        if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && _match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedGreenImage.Add(from);
                    };
                    _mainPage.Connection.AckReceived += greenAckReceived;

                    //Send the green background
                    using (var greenBitmap = QRUtils.GenerateColoredBitmap())
                    {
                        SendToPlayers(new Packet(
                            new FileModel(
                                QRUtils.ConvertBitmapToPngBytes(greenBitmap),
                                intentions: FileModel.Intentions.SetPngToShowWhenTriggered
                            )
                        ));
                    }

                    while (!_syncCancellationToken.Token.IsCancellationRequested && !_match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedGreenImage.Contains(x))) await Task.Delay(0);

                    //If a player failed to download the background, bail            
                    _mainPage.Connection.AckReceived -= greenAckReceived;
                    if (_syncCancellationToken.Token.IsCancellationRequested)
                    {
                        var missingLog = string.Empty;
                        var missing = _match.Players.Where(x => !_playersWhoHaveDownloadedGreenImage.Contains(x.Id)).Select(x => x.Name);
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                        Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                        //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

                        allPlayersSynced.Invoke(false);

                        return;
                    }

                    //Set up color listener
                    List<PixelReader> pixelReaders = new List<PixelReader>();
                    for (int i = 0; i < _match.Players.Length; i++)
                    {
                        int playerId = i;
                        pixelReaders.Add(new PixelReader(new Point(_match.Players[i].StreamScreenCoordinates.x, _match.Players[i].StreamScreenCoordinates.y), (color) =>
                        {
                            return Colors.Green.R - 50 <= color.R && color.R <= Colors.Green.R + 50 &&
                                Colors.Green.G - 50 <= color.G && color.G <= Colors.Green.G + 50 &&
                                Colors.Green.B - 50 <= color.B && color.B <= Colors.Green.B + 50;

                        }, () =>
                        {
                            _match.Players[playerId].StreamDelayMs = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _match.Players[playerId].StreamSyncStartMs;

                            //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"DETECTED: {_match.Players[playerId].Name} (delay: {_match.Players[playerId].StreamDelayMs})\n") { Foreground = Brushes.YellowGreen })); ;

                            //Send updated delay info
                            _mainPage.Connection.UpdatePlayer(_match.Players[playerId]);

                            if (_match.Players.All(x => x.StreamDelayMs > 0))
                            {
                                //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("All players successfully synced. Sending PlaySong\n") { Foreground = Brushes.Green })); ;
                                allPlayersSynced.Invoke(true);
                            }
                        }));
                    }

                    //Loop through players and set their sync init time
                    for (int i = 0; i < _match.Players.Length; i++)
                    {
                        _match.Players[i].StreamSyncStartMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
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
                    //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run("Failed to locate all players on screen. Playing song without sync\n") { Foreground = Brushes.Red })); ;
                    allPlayersSynced.Invoke(false);
                }
            };

            Action scanForQrCodes = () =>
            {
                _syncCancellationToken?.Cancel();
                _syncCancellationToken = new CancellationTokenSource(45 * 1000);

                int boundsX = default;
                int boundsY = default;
                System.Drawing.Size boundsSize = default;
                Dispatcher.Invoke(new Action(() =>
                {
                    boundsX = (StreamSyncScreenSelectionBox.SelectedItem as Screen).Bounds.X;
                    boundsY = (StreamSyncScreenSelectionBox.SelectedItem as Screen).Bounds.Y;
                    boundsSize = (StreamSyncScreenSelectionBox.SelectedItem as Screen).Bounds.Size;
                }));

                //While not 20 seconds elapsed and not all players have locations
                while (!_syncCancellationToken.Token.IsCancellationRequested && !_match.Players.All(x => !x.StreamScreenCoordinates.Equals(default(Player.Point))))
                {
                    var returnedResults = QRUtils.ReadQRsFromScreen(boundsX, boundsY, boundsSize).ToList();
                    if (returnedResults.Count > 0)
                    {
                        //Logging
                        var successMessage = string.Empty;
                        returnedResults.ForEach(x => successMessage += $"{x}, ");
                        Logger.Debug(successMessage);

                        //Read the location of all the QRs
                        foreach (var result in returnedResults)
                        {
                            var player = _match.Players.FirstOrDefault(x => Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{x.UserId} {_match.Guid}") == result.Text);
                            if (player == null) continue;

                            Logger.Debug($"{player.Name} QR DETECTED");
                            var point = new Player.Point(); 
                            
                            //Ari' note: We need to add the screen offset, since we are now dealing with multiple non-primary screens
                            //The screen 0 will ALWAYS start with 0, 0 in the top-left corner
                            //So in case this still is on D0 we just add 0, 0.
                            //In case of any other display we take the coordinates and add the pixel "offset" from D0 top-left to selected screen top-left x, x.
                            point.x = (int)result.ResultPoints[3].X + boundsX; //ResultPoints[3] is the qr location square closest to the center of the qr. The oddball.
                            point.y = (int)result.ResultPoints[3].Y + boundsY;
                            player.StreamScreenCoordinates = point;
                        }

                        //Logging
                        var missing = _match.Players.Where(x => x.StreamScreenCoordinates.Equals(default(Player.Point))).Select(x => x.Name);
                        var missingLog = "Can't see QR for: ";
                        foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";
                        //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run(missingLog + "\n") { Foreground = Brushes.Yellow }));
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
                    if (a.Type == Acknowledgement.AcknowledgementType.FileDownloaded && _match.Players.Select(x => x.Id).Contains(from)) _playersWhoHaveDownloadedQrImage.Add(from);
                };
                _mainPage.Connection.AckReceived += ackReceived;

                //Loop through players and send the QR for them to display (but don't display it yet)
                //Also reset their stream syncing values to default
                for (int i = 0; i < _match.Players.Length; i++)
                {
                    _match.Players[i].StreamDelayMs = 0;
                    _match.Players[i].StreamScreenCoordinates = default;
                    _match.Players[i].StreamSyncStartMs = 0;

                    _mainPage.Connection.Send(
                        _match.Players[i].Id,
                        new Packet(
                            new FileModel(
                                QRUtils.GenerateQRCodePngBytes(Hashing.CreateSha1FromString($"Nice try. ;) https://scoresaber.com/u/{_match.Players[i].UserId} {_match.Guid}")),
                                intentions: FileModel.Intentions.SetPngToShowWhenTriggered
                            )
                        )
                    );
                }

                while (!_syncCancellationToken.Token.IsCancellationRequested && !_match.Players.Select(x => x.Id).All(x => _playersWhoHaveDownloadedQrImage.Contains(x))) await Task.Delay(0);

                //If a player failed to download the background, bail            
                _mainPage.Connection.AckReceived -= ackReceived;
                if (_syncCancellationToken.Token.IsCancellationRequested)
                {
                    var missingLog = string.Empty;
                    var missing = _match.Players.Where(x => !_playersWhoHaveDownloadedQrImage.Contains(x.Id)).Select(x => x.Name);
                    foreach (var missingPerson in missing) missingLog += $"{missingPerson}, ";

                    Logger.Error($"{missingLog} failed to download a sync image, bailing out of stream sync...");
                    //LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{missingLog} failed to download a sync image, bailing out of stream sync...\n") { Foreground = Brushes.Red })); ;

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
            StreamSyncRunning = false;
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
            Dispatcher.Invoke(new Action(() =>
            {
                ReplayCurrentButton.IsEnabled = false;
                LoadNextButton.Visibility = Visibility.Hidden;
                PlaySongButton.Visibility = Visibility.Visible;
                PlaylistSongTable.IsHitTestVisible = false;
            }));
        }

        #region ServerCommunication
        private void SendToPlayers(Packet packet)
        {
                var playersText = string.Empty;
                foreach (var player in _match.Players) playersText += $"{player.Name}, ";
                Logger.Debug($"Sending {packet.Type} to {playersText}");
                _mainPage.Connection.Send(_match.Players.Select(x => x.Id).ToArray(), packet);
        }

        private void SendToPlayersWithDelay(Packet packet)
        {
            var maxDelay = _match.Players.Max(x => x.StreamDelayMs);

            foreach (var player in _match.Players)
            {
                Task.Run(() =>
                {
                    Logger.Debug($"Sleeping {(int)maxDelay - (int)player.StreamDelayMs} ms for {player.Name}");
                    Thread.Sleep((int)maxDelay - (int)player.StreamDelayMs);
                    Logger.Debug($"Sending start to {player.Name}");
                    _mainPage.Connection.Send(player.Id, packet);
                });
            }

            Task.Run(() =>
            {
                Thread.Sleep((int)maxDelay + 1250); //Start music player and account for player delay + beatsaber unpause delay
                MusicPlayer.player.Play();
            });
        }

        private async Task<bool> SetUpAndPlaySong(bool? useSync = false)
        {
            //Check for banned mods before continuing
            if (_mainPage.Connection.State.ServerSettings.BannedMods.Length > 0)
            {
                var playersWithBannedMods = string.Empty;
                foreach (var player in _match.Players)
                {
                    if (player.ModList == null) continue;
                    string bannedMods = string.Join(", ", player.ModList.Intersect(_mainPage.Connection.State.ServerSettings.BannedMods));
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
            //_levelCompletionResults = new List<SongFinished>();

            var gm = new GameplayModifiers();
            if ((bool)NoFailBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.NoFail;
            if ((bool)DisappearingArrowsBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.DisappearingArrows;
            if ((bool)GhostNotesBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.GhostNotes;
            if ((bool)FastNotesBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.FastNotes;
            if ((bool)SlowSongBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.SlowSong;
            if ((bool)FastSongBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.FastSong;
            if ((bool)SuperFastSongBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.SuperFastSong;
            if ((bool)InstaFailBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.InstaFail;
            if ((bool)FailOnSaberClashBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.FailOnClash;
            if ((bool)BatteryEnergyBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.BatteryEnergy;
            if ((bool)NoBombsBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.NoBombs;
            if ((bool)NoWallsBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.NoObstacles;
            if ((bool)NoArrowsBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.NoArrows;
            if ((bool)ProModeBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.ProMode;
            if ((bool)ZenModeBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.ZenMode;
            if ((bool)SmallCubesBox.IsChecked) gm.Options |= GameplayModifiers.GameOptions.SmallCubes;

            var playSong = new PlaySong();
            var gameplayParameters = new GameplayParameters();
            gameplayParameters.Beatmap = new Beatmap();
            gameplayParameters.Beatmap.Characteristic = new Characteristic();
            gameplayParameters.Beatmap.Characteristic.SerializedName = _match.SelectedCharacteristic.SerializedName;
            gameplayParameters.Beatmap.Difficulty = _match.SelectedDifficulty;
            gameplayParameters.Beatmap.LevelId = _match.SelectedLevel.LevelId;

            gameplayParameters.GameplayModifiers = gm;
            gameplayParameters.PlayerSettings = new PlayerSpecificSettings();

            playSong.GameplayParameters = gameplayParameters;
            playSong.FloatingScoreboard = (bool)ScoreboardBox.IsChecked;
            playSong.StreamSync = (bool)useSync;
            playSong.DisableFail = (bool)DisableFailBox.IsChecked;
            playSong.DisablePause = (bool)DisablePauseBox.IsChecked;
            playSong.DisableScoresaberSubmission = (bool)DisableScoresaberBox.IsChecked;
            playSong.ShowNormalNotesOnStream = (bool)ShowNormalNotesBox.IsChecked;

            SendToPlayers(new Packet(playSong));

            return true;
        }

        private void UpdateCurrentMatchToNewLoadedSong()
        {
            var matchMap = new PreviewBeatmapLevel()
            {
                LevelId = LoadedSong.SongInfo.CurrentVersion.hash,
                Name = LoadedSong.SongInfo.name
            };

            List<Characteristic> characteristics = new List<Characteristic>();
            foreach (var characteristic in LoadedSong.SongInfo.Characteristics)
            {
                characteristics.Add(new Characteristic()
                {
                    SerializedName = characteristic,
                    Difficulties = LoadedSong.SongInfo.GetDifficultiesAsIntArray(characteristic).Select(x => (BeatmapDifficulty)x).ToArray()
                });
            }
            matchMap.Characteristics = characteristics.ToArray();

            _match.SelectedLevel = matchMap;
            _match.SelectedCharacteristic = matchMap.Characteristics.FirstOrDefault(x => x.SerializedName == "Standard") ?? matchMap.Characteristics.First();
            _match.SelectedDifficulty = LoadedSong.SongInfo.GetClosestDifficultyPreferLower(_match.SelectedCharacteristic.SerializedName, BeatmapDifficulty.ExpertPlus);
            _mainPage.Connection.UpdateMatch(_match);
        }

        private void SendLoadSong(SongInfo song)
        {
            SendToPlayers(new Packet(new LoadSong()
            {
                LevelId = song.CurrentVersion.hash
            }));
        }
        #endregion

        #region SelectionChanged
        private void PlaylistSongTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistSongTable.SelectedIndex == -1) return;
            if (e.AddedItems.Count > 0 && e.RemovedItems.Count == 0) return;
            if (Playlist.SelectedSong != Playlist.Songs[PlaylistSongTable.SelectedIndex] || Playlist.SelectedSong == Playlist.Songs[0])
            {
                if (MusicPlayer.player.IsPlaying) MusicPlayer.player.Stop();

                Playlist.SelectedSong = Playlist.Songs[PlaylistSongTable.SelectedIndex];

                LoadedSong = Playlist.SelectedSong;

                if (LoadedSong.SongDataPath != null)
                {
                    MusicPlayer.LoadSong(LoadedSong);
                    UpdateMusicPlayerTime();
                    PlaySongButton.Visibility = Visibility.Visible;
                    DownloadSongButton.Visibility = Visibility.Hidden;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    ReplayCurrentButton.IsEnabled = false;

                    UpdateCurrentMatchToNewLoadedSong();
                }
                else
                {
                    PlaySongButton.Visibility = Visibility.Hidden;
                    DownloadSongButton.Visibility = Visibility.Visible;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    MusicPlayer.player.Media = null;
                }

                NotifyPropertyChanged(nameof(LoadedSong));

                //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
                Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
            }
        }

        private void DifficultySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;

            //Handle null exception
            if (comboBox.Items.Count == 0) return;

            if ((comboBox.DataContext as PlaylistItem).SelectedDifficulty != ((BeatmapDifficulty)comboBox.SelectedItem))
            {
                PlaylistSongTable.Items.Refresh(); //This breaks down with large playlists, but I cant figure out NotifyPropertyChanged so here we are
                LoadedSong.SelectedDifficulty = (BeatmapDifficulty)comboBox.SelectedItem;
                UpdateCurrentMatchToNewLoadedSong();
            }
        }

        private void CharacteristicSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;

            //Handle null exception
            if (comboBox.Items.Count == 0) return;

            if ((comboBox.DataContext as PlaylistItem).SelectedCharacteristic != (comboBox.SelectedItem as Characteristic))
            {
                var index = Playlist.Songs.IndexOf(comboBox.DataContext as PlaylistItem);

                var newCharacteristic = comboBox.SelectedItem as Characteristic;
                PlaylistSongTable.Items.Refresh(); //This breaks down with large playlists, but I cant figure out NotifyPropertyChanged so here we are
                LoadedSong = Playlist.Songs[index];
                LoadedSong.SelectedCharacteristic = newCharacteristic;
                LoadedSong.SelectedDifficulty = newCharacteristic.Difficulties.Last();
                UpdateCurrentMatchToNewLoadedSong();
            }
        }

        private void DifficultySelectorControls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoadedSong != null)
            {
                var comboBox = sender as System.Windows.Controls.ComboBox;

                //Handle null exception
                if (comboBox.Items.Count == 0) return;
                if (comboBox.SelectedItem == null) return;

                //Moon's note: why aren't we just setting this every time, instead of checking and then only changing if it's different?
                if (((BeatmapDifficulty)comboBox.SelectedItem) != LoadedSong.SelectedDifficulty)
                {
                    LoadedSong.SelectedDifficulty = (BeatmapDifficulty)comboBox.SelectedItem;
                    UpdateCurrentMatchToNewLoadedSong();
                }
            }
        }

        private void CharacteristicSelectorControls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoadedSong != null)
            {
                var comboBox = sender as System.Windows.Controls.ComboBox;

                //Handle null exception
                if (comboBox.Items.Count == 0) return;
                if (comboBox.SelectedItem == null) return;

                if ((comboBox.SelectedItem as Characteristic) != LoadedSong.SelectedCharacteristic)
                {
                    var index = Playlist.Songs.IndexOf(comboBox.DataContext as PlaylistItem);

                    var newCharacteristic = comboBox.SelectedItem as Characteristic;
                    LoadedSong = Playlist.Songs[index];
                    LoadedSong.SelectedCharacteristic = newCharacteristic;
                    LoadedSong.SelectedDifficulty = newCharacteristic.Difficulties.Last();
                    UpdateCurrentMatchToNewLoadedSong(); //Commenting out since changing the characteristic won't matter until a new difficulty is selected. We will do this then.
                }
            }
        }
        #endregion

        #region MusicPlayer
        public void UpdateMusicPlayerTime()
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(MusicPlayer.player.Media.Duration);
            TimeSpan playerTime = TimeSpan.FromMilliseconds(MusicPlayer.player.Time);
            var percent = (double)decimal.Multiply(decimal.Divide((decimal)playerTime.TotalMilliseconds, (decimal)duration.TotalMilliseconds), 100);

            string playerTimeString = "";
            if (playerTime.Hours > 0)
                playerTimeString += playerTime.Hours.ToString();

            playerTimeString += $"{playerTime:mm\\:ss}";

            string durationString = "";
            if (duration.Hours > 0)
                durationString += duration.Hours.ToString();

            durationString += $"{duration:mm\\:ss}";

            Dispatcher.Invoke(new Action(() =>
            {
                PlayerProgressTextControlPanel.Text = $"{playerTimeString} / {durationString}";
                PlayerProgressBarControlPanel.Value = percent;
            }));
        }

        private void Player_TimeChanged(object sender, LibVLCSharp.Shared.MediaPlayerTimeChangedEventArgs e)
        {
            UpdateMusicPlayerTime();
        }

        private void Player_Playing(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                PlayerControlPanelPlay.Visibility = Visibility.Hidden;
                PlayerControlPanelPause.Visibility = Visibility.Visible;
            }));
        }

        private void Player_Paused(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                PlayerControlPanelPlay.Visibility = Visibility.Visible;
                PlayerControlPanelPause.Visibility = Visibility.Hidden;
            }));
        }

        private void Player_Stopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                PlayerControlPanelPause.Visibility = Visibility.Hidden;
                PlayerControlPanelPlay.Visibility = Visibility.Visible;
                PlayerProgressBarControlPanel.Value = 0;
                PlayerProgressTextControlPanel.Text = $"Stopped";
            }));
        }

        private void PlayerControlPanelStopCommand_Executed(object obj)
        {
            MusicPlayer.player.Stop();
        }

        private void PlayerControlPanelPauseCommand_Executed(object obj)
        {
            MusicPlayer.player.Pause();
        }

        private void PlayerControlPanelPlayCommand_Executed(object obj)
        {
            MusicPlayer.player.Play();
        }

        private bool PlayerControlPanelStopCommand_CanExecute(object arg)
        {
            if (MusicPlayer.player != null) return MusicPlayer.player.IsPlaying;
            return false;
        }

        private bool PlayerControlPanelPauseCommand_CanExecute(object arg)
        {
            if (MusicPlayer.player != null) return MusicPlayer.player.CanPause;
            return false;
        }

        private bool PlayerControlPanelPlayCommand_CanExecute(object arg)
        {
            if (MusicPlayer.player.Media == null)
            {
                PlayerProgressTextControlPanel.Text = "No Media";
                return false;
            }
            if (MusicPlayer.player != null && LoadedSong != null) return true;
            return false;
        }

        #endregion

        private void PlayedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as System.Windows.Controls.CheckBox;
            var song = checkBox.DataContext as PlaylistItem;
            var index = Playlist.Songs.IndexOf(song);
            Playlist.Songs[index].Played = (bool)checkBox.IsChecked;
        }
    }
}
