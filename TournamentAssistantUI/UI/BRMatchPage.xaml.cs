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
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Shared.Models;
using static TournamentAssistantShared.GlobalConstants;
using static TournamentAssistantShared.Song;
using static TournamentAssistantShared.BeatSaverDownloader;
using MessageBox = System.Windows.Forms.MessageBox;
using File = System.IO.File;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.UI.UserControls;
using MaterialDesignThemes.Wpf;
using static TournamentAssistantShared.SharedConstructs;

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
        public Dictionary<int, int> RoomRules { get; set; }
        public Playlist Playlist { get; set; }

        private MusicPlayer MusicPlayer = new();
        public Song LoadedSong { get; set; }
        private CancellationTokenSource TokenSource { get; set; }

        private bool IsFinishable { get; set; } = false;

        string environmentPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUI";
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
            _mainPage.Connection.MatchInfoUpdated += Connection_MatchInfoUpdated;
        }

        private void Connection_MatchInfoUpdated(Match obj)
        {
            _match = obj;
        }

        private void Connection_PlayerFinishedSong(SongFinished obj)
        {
            if (_match.Players.All(player => player.PlayState == Player.PlayStates.Waiting) && IsFinishable)
            {
                IsFinishable = false; //This is pretty stupid but the probability of threads calling this in such a quick succession that this is not updated in time is practically zero, so IDGAF
                if (MusicPlayer.player.IsPlaying) MusicPlayer.player.Stop();

                Dispatcher.BeginInvoke(new Action(() =>
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
                        if (Playlist.Songs[i].Name == LoadedSong.Name)
                        {
                            Playlist.Songs[i].Played = true;
                        }
                    }
                    Playlist.SelectedSong.Played = true;
                    LoadedSong.Played = true;
                    if (RoomRules != null) KickPlayersWithRules();
                }));
            }
        }

        private void KickPlayersWithRules()
        {
            int amountPlayers = _match.Players.Count();

            int currentRuleKey = RoomRules.Keys.Aggregate((x, y) => Math.Abs(x - amountPlayers) < Math.Abs(y - amountPlayers) ? x : y);

            int currentRule = RoomRules[currentRuleKey];

            List<int> scores = new List<int>();

            scores.AddRange(from players in _match.Players select players.Score);

            scores = scores.OrderBy(x => x).ToList();

            List<int> scoresBelowLine = new List<int>();

            for (int i = 0; i < currentRule; i++)
            {
                scoresBelowLine.Add(scores[i]);
            }

            var playersToBeKicked = from players in _match.Players
                                    where scoresBelowLine.Contains(players.Score)
                                    select players;

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
                    foreach (var player in playersToBeKicked)
                    {
                        //Remove player from list
                        var newPlayers = _match.Players.ToList();
                        newPlayers.RemoveAt(newPlayers.IndexOf(player));
                        _match.Players = newPlayers.ToArray();

                        //Notify all the UI that needs to be notified, and propegate the info across the network
                        NotifyPropertyChanged(nameof(_match));
                        _mainPage.Connection.UpdateMatch(_match);
                    }
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
            return true;
        }



        private bool LoadNext_CanExecute(object arg)
        {
            return true;
        }



        private bool DownloadSong_CanExecute(object arg)
        {
            try
            {
                return SongUrlBox.Text.Length > 0 || Playlist.SelectedSong != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
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
            return Playlist != null && Playlist.Songs.Count > 1;
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
            if (RoomRules == null) RoomRules = new Dictionary<int, int>();

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

            var data = File.ReadAllLines(ruleLocation);

            foreach (var line in data)
            {
                var rule = line.Split(':');

                RoomRules.Add(Int32.Parse(rule[0]), Int32.Parse(rule[1]));
            }

            RuleTable.Items.Refresh();
        }

        private void ButtonBack_Executed(object obj)
        {
            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(_mainPage);
        }

        private void ReplayCurrent_Executed(object obj)
        {
            _ = SetUpAndPlaySong().ContinueWith(task => 
            {
                if (task.Result)
                {
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        ReplayCurrentButton.IsEnabled = false;
                        LoadNextButton.Visibility = Visibility.Hidden;
                        PlaySongButton.IsEnabled = false;
                        PlaySongButton.Content = "In Game";
                        PlaySongButton.Visibility = Visibility.Visible;
                        MusicPlayer.player.Play();
                        PlaylistSongTable.IsHitTestVisible = false;
                    }));
                }
            });
        }

        private void PlaySong_Executed(object obj)
        {
            _ = SetUpAndPlaySong().ContinueWith(task =>
            {
                if (task.Result)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PlaySongButton.IsEnabled = false;
                        PlaySongButton.Content = "In Game";
                        MusicPlayer.player.Play();
                        PlaylistSongTable.IsHitTestVisible = false;
                    }));

                    //Another stupid fix, but why not LUL
                    Task.Run(() => 
                    {
                        Task.Delay(5000).Wait();
                        IsFinishable = true;
                    });
                }
            });

            //navigate to ingame page
        }

        private void LoadNext_Executed(object obj)
        {
            int currentIndex = PlaylistSongTable.SelectedIndex;
            int newIndex = currentIndex++;
            PlaylistSongTable.SelectedIndex = newIndex;
        }

        private void DownloadSong_Executed(object obj)
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

            BeatSaverDownloader beatSaverDownloader = new();
            Task.Run(async () =>
            {
                Song song;
                int songIndex = 0;
                PlaylistHandler playlistHandler = new PlaylistHandler(
                    new Progress<int>(percent => Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DownloadProgressBar.IsIndeterminate = false;
                        DownloadProgressBar.Value = percent;
                    }))));

                switch (useSongURL)
                {
                    case true:
                        song = await GetSongByIDAsync(songURLBoxText, progress);
                        break;
                    case false:
                        song = Playlist.SelectedSong;
                        songIndex = Playlist.Songs.IndexOf(song);
                        break;
                }

                var data = await GetSong(song, progress);
                if (data.Value == null)
                {
                    var dialogResult = MessageBox.Show($"An error occured when trying to download song {song.Name}\nAborting will remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Exclamation);
                    switch (dialogResult)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Abort:
                            Playlist.Songs.Remove(song);
                            return;
                        case DialogResult.Retry:
                            BeatSaverDownloader beatSaverDownloader = new();
                            beatSaverDownloader.RetrySongDownloadFinished += BeatSaverDownloader_RetrySongDownloadFinished;
                            beatSaverDownloader.RetrySongDownloadAsync(song, progress);
                            return;
                        default:
                            return;
                    }
                }

                song.SongDataPath = data.Value;
                Playlist.SelectedSong.SongDataPath = data.Value;
                Playlist.Songs[songIndex].SongDataPath = data.Value;
                

                LoadedSong = song;
                NotifyPropertyChanged(nameof(LoadedSong));
                MusicPlayer.LoadSong(LoadedSong);
                UpdateMusicPlayerTime();

                try
                {
                    TokenSource.Dispose();
                    TokenSource = null;
                }
                catch (ObjectDisposedException e)
                {
                    Logger.Warning("Token already disposed");
                    Logger.Warning(e.ToString());
                }
            }, TokenSource.Token).ContinueWith(task => Dispatcher.BeginInvoke(() =>
            {
                UpdateLoadedSong();
                DownloadProgressBar.Visibility = Visibility.Hidden;
                DownloadSongButton.IsEnabled = true;
                DownloadSongButton.Visibility = Visibility.Hidden;
                DownloadSongButton.Content = "Download Song";
                PlaySongButton.Visibility = Visibility.Visible;
            }));
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

            BeatSaverDownloader beatSaverDownloader = new();
            beatSaverDownloader.SongDownloadFinished += BeatSaverDownloader_SongDownloadFinished;

            var songsToDownload = from songs in Playlist.Songs
                                  where Playlist.Songs.All(song => song.SongDataPath == null)
                                  select songs;
            Task.Run(() => beatSaverDownloader.GetSongs(songsToDownload.ToArray(), progress, TokenSource.Token));
        }

        private void BeatSaverDownloader_SongDownloadFinished(Dictionary<string, string> data)
        {
            foreach (var hash in data.Keys)
            {
                for (int i = 0; i < Playlist.Songs.Count; i++)
                {
                    var song = Playlist.Songs[i];
                    if (song.Hash != hash) continue;

                    if (data[hash] == null)
                    {
                        var dialogResult = MessageBox.Show($"An error occured when trying to download song {song.Name}\nAborting will remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Exclamation);
                        switch (dialogResult)
                        {
                            case DialogResult.Cancel:
                                continue;
                            case DialogResult.Abort:
                                continue;
                            case DialogResult.Retry:
                                BeatSaverDownloader beatSaverDownloader = new();
                                beatSaverDownloader.RetrySongDownloadFinished += BeatSaverDownloader_RetrySongDownloadFinished;
                                beatSaverDownloader.RetrySongDownloadAsync(Playlist.Songs[i], new Progress<int>(percent => DownloadAllProgressBar.Value = percent));
                                return;
                            default:
                                break;
                        }
                    }

                    Playlist.Songs[i].SongDataPath = data[hash];
                }
            }

            UpdateLoadedSong();

            Dispatcher.BeginInvoke(new Action(() =>
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
                if (song.Hash != hash) continue;

                if (data.Value == null)
                {
                    var dialogResult = MessageBox.Show($"Do you wish to remove the offending song from the loaded playlist (File will not be edited)", "DownloadError", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    switch (dialogResult)
                    {
                        case DialogResult.Yes:
                            Playlist.Songs.RemoveAt(i);
                            break;
                        case DialogResult.No:
                            break;
                        default:
                            break;
                    }
                }

                Playlist.Songs[i].SongDataPath = data.Value;
                Playlist.Songs[i].SetLegacyData();
            }

            UpdateLoadedSong();

            Dispatcher.BeginInvoke(new Action(() =>
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

            Task.Run(new Action(() =>
            {
                PlaylistHandler playlistHandler = new PlaylistHandler(
                    new Progress<int>(percent => Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PlaylistLoadingProgress.IsIndeterminate = false;
                        PlaylistLoadingProgress.Value = percent;
                    }))));

                playlistHandler.PlaylistLoadComplete += PlaylistHandler_PlaylistLoadComplete;

                playlistHandler.LoadPlaylist(playlistLocation);
            }));
        }

        private void PlaylistHandler_PlaylistLoadComplete(Playlist playlist)
        {
            Playlist = playlist;
            LoadedSong = Playlist.SelectedSong;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PlaylistSongTable.ItemsSource = Playlist.Songs;
                PlaylistLoadingProgress.Visibility = Visibility.Hidden;
                PlaylistSongTable.SelectedIndex = 0;
                UnLoadPlaylistButton.Visibility = Visibility.Visible;

                //Send load song to players
                var loadSong = new LoadSong();
                loadSong.LevelId = LoadedSong.ID;
                loadSong.CustomHostUrl = null;
                SendToPlayers(new Packet(loadSong));
            }));
        }

        private void AddSong_Executed(object obj)
        {
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            var text = SongUrlBox.Text;

            Task.Run(() => GetSongByIDAsync(text)).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
            {
                var song = task.Result;
                if (song != null) Playlist.Songs.Add(song);
                PlaylistLoadingProgress.Visibility = Visibility.Hidden;
            })));

            SongUrlBox.Text = "";
        }

        private void PreviewStartButton_Click(object sender, RoutedEventArgs e)
        {
            var startButton = sender as System.Windows.Controls.Button;
            var song = (sender as System.Windows.Controls.Button).DataContext as Song;
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
                stopButton.Dispatcher.BeginInvoke(new Action(() =>
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
                });

                //Unsubscribe event handler after we are done with it
                //Yes I know how ugly this looks, and yes I know it can be done cleaner
                //If you dont like it feel free to implement a cleaner soulution :P
                MusicPlayer.player.Stopped -= handler;
            };
            MusicPlayer.player.Stopped += handler;

            IProgress<int> prog = new Progress<int>(percent =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = percent;
                Logger.Debug($"Loading preview {percent}%");
                if (percent == 100)
                {
                    progressBar.Visibility = Visibility.Hidden;
                    stopButton.Visibility = Visibility.Visible;
                    var media = MusicPlayer.MediaInit($"{Cache}{song.Hash}\\preview.mp3");
                    MusicPlayer.player.Play(media);
                }
            });

            Task.Run(async () =>
            {
                if (!Directory.Exists($"{Cache}{song.Hash}")) Directory.CreateDirectory($"{Cache}{song.Hash}");
                if (!File.Exists($"{Cache}{song.Hash}\\preview.mp3"))
                {
                    using var client = new WebClient();
                    client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        prog.Report(e.ProgressPercentage);
                    };
                    string url = $"https://cdn.beatsaver.com/{song.Hash.ToLower()}.mp3";
                    await client.DownloadFileTaskAsync(url, $"{Cache}{song.Hash}\\preview.mp3");
                }

                prog.Report(100);
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
        }

        #region ServerCommunication
        private void SendToPlayers(Packet packet)
        {
            var playersText = string.Empty;
            foreach (var player in _match.Players) playersText += $"{player.Name}, ";
            Logger.Debug($"Sending {packet.Type} to {playersText}");
            _mainPage.Connection.Send(_match.Players.Select(x => x.Id).ToArray(), packet);
        }

        private async Task<bool> SetUpAndPlaySong(bool useSync = false)
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
            playSong.StreamSync = useSync;
            playSong.DisableFail = (bool)DisableFailBox.IsChecked;
            playSong.DisablePause = (bool)DisablePauseBox.IsChecked;
            playSong.DisableScoresaberSubmission = (bool)DisableScoresaberBox.IsChecked;
            playSong.ShowNormalNotesOnStream = (bool)ShowNormalNotesBox.IsChecked;

            SendToPlayers(new Packet(playSong));

            return true;
        }

        private void SetupMatchSong(Song song)
        {
            song.SelectedCharacteristicObject = GetSelectedCharacteristic(song.SelectedCharacteristic);
            _match.SelectedLevel = song.PreviewBeatmapLevelObject;
            _match.SelectedCharacteristic = song.SelectedCharacteristicObject;
            _match.SelectedDifficulty = (BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), song.SelectedCharacteristic.SelectedDifficulty.Type);
            _mainPage.Connection.UpdateMatch(_match);

            var loadSong = new LoadSong();
            loadSong.LevelId = _match.SelectedLevel.LevelId;
            loadSong.CustomHostUrl = null;
            SendToPlayers(new Packet(loadSong));
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

                    UpdateLoadedSong();
                }
                else
                {
                    PlaySongButton.Visibility = Visibility.Hidden;
                    DownloadSongButton.Visibility = Visibility.Visible;
                    LoadNextButton.Visibility = Visibility.Hidden;
                    MusicPlayer.player.Media = null;
                }

                NotifyPropertyChanged(nameof(LoadedSong));
            }
        }

        private void UpdateLoadedSong()
        {
            LoadedSong.SetLegacyData();
            SetupMatchSong(LoadedSong);

            //Send load song to players
            var loadSong = new LoadSong();
            loadSong.LevelId = _match.SelectedLevel.LevelId;
            loadSong.CustomHostUrl = null;
            SendToPlayers(new Packet(loadSong));
        }

        private void DifficultySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;

            //Handle null exception
            if (comboBox.Items.Count == 0) return;


            if ((comboBox.DataContext as Song).SelectedCharacteristic.SelectedDifficulty != (comboBox.SelectedItem as SongDifficulty))
            {
                (comboBox.DataContext as Song).SelectedCharacteristic.SelectedDifficulty = comboBox.SelectedItem as SongDifficulty;
                PlaylistSongTable.Items.Refresh(); //This breaks down with large playlists, but I cant figure out NotifyPropertyChanged so here we are
                LoadedSong.SelectedCharacteristic.SelectedDifficulty = comboBox.SelectedItem as SongDifficulty;
                UpdateLoadedSong();
            }
        }

        private void CharacteristicSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;

            //Handle null exception
            if (comboBox.Items.Count == 0) return;


            if ((comboBox.DataContext as Song).SelectedCharacteristic != (comboBox.SelectedItem as SongCharacteristic))
            {
                (comboBox.DataContext as Song).SelectedCharacteristic = comboBox.SelectedItem as SongCharacteristic;
                PlaylistSongTable.Items.Refresh(); //This breaks down with large playlists, but I cant figure out NotifyPropertyChanged so here we are
                LoadedSong.SelectedCharacteristic = comboBox.SelectedItem as SongCharacteristic;
                UpdateLoadedSong();
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


                if ((comboBox.SelectedItem as SongCharacteristic) != LoadedSong.SelectedCharacteristic)
                {
                    LoadedSong.SelectedCharacteristic = comboBox.SelectedItem as SongCharacteristic;
                    UpdateLoadedSong();
                }
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


                if ((comboBox.SelectedItem as SongDifficulty) != LoadedSong.SelectedCharacteristic.SelectedDifficulty)
                {
                    LoadedSong.SelectedCharacteristic.SelectedDifficulty = comboBox.SelectedItem as SongDifficulty;
                    UpdateLoadedSong();
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


            Dispatcher.BeginInvoke(new Action(() =>
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
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PlayerControlPanelPlay.Visibility = Visibility.Hidden;
                PlayerControlPanelPause.Visibility = Visibility.Visible;
            }));
        }

        private void Player_Paused(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PlayerControlPanelPlay.Visibility = Visibility.Visible;
                PlayerControlPanelPause.Visibility = Visibility.Hidden;
            }));
        }

        private void Player_Stopped(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
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
    }
}
