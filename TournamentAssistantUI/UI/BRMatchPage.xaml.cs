using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Shared.Models;

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
        public ICommand LoadSong { get; }
        public ICommand LoadNext { get; }
        public ICommand PlaySong { get; }
        public ICommand ReplayCurrent { get; }
        public ICommand PlayerControlPanelPlayCommand { get; }
        public ICommand PlayerControlPanelPauseCommand { get; }
        public ICommand PlayerControlPanelStopCommand { get; }

        private NavigationService navigationService = null;
        public ObservableCollection<string> PlaylistLocation_Source { get; set; }
        PlaylistHandler PlaylistHandler { get; set; }
        public Playlist Playlist { get; set; }

        private MusicPlayer MusicPlayer = new();
        public Song LoadedSong { get; set; }
        private CancellationTokenSource TokenSource { get; set; }

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

            _mainPage = mainPage;
            _match = match;

            ButtonBackCommand = new CommandImplementation(ButtonBack_Executed, (_) => true);
            AddSong = new CommandImplementation(AddSong_Executed, AddSong_CanExecute);
            LoadPlaylist = new CommandImplementation(LoadPlaylist_Executed, LoadPlaylist_CanExecute);
            UnLoadPlaylist = new CommandImplementation(UnLoadPlaylist_Executed, (_) => true);
            DownloadAll = new CommandImplementation(DownloadAll_Executed, DownloadAll_CanExecute);
            CancelDownload = new CommandImplementation(CancelDownload_Executed, CancelDownload_CanExecute);
            LoadSong = new CommandImplementation(LoadSong_Executed, LoadSong_CanExecute);
            LoadNext = new CommandImplementation(LoadNext_Executed, LoadNext_CanExecute);
            PlaySong = new CommandImplementation(PlaySong_Executed, PlaySong_CanExecute);
            ReplayCurrent = new CommandImplementation(ReplayCurrent_Executed, ReplayCurrent_CanExecute);
            PlayerControlPanelPlayCommand = new CommandImplementation(PlayerControlPanelPlayCommand_Executed, PlayerControlPanelPlayCommand_CanExecute);
            PlayerControlPanelPauseCommand = new CommandImplementation(PlayerControlPanelPauseCommand_Executed, PlayerControlPanelPauseCommand_CanExecute);
            PlayerControlPanelStopCommand = new CommandImplementation(PlayerControlPanelStopCommand_Executed, PlayerControlPanelStopCommand_CanExecute);

            MusicPlayer.player.Stopped += Player_Stopped;
            MusicPlayer.player.Paused += Player_Paused;
            MusicPlayer.player.Playing += Player_Playing;
            MusicPlayer.player.TimeChanged += Player_TimeChanged;
        }

        private void ButtonBack_Executed(object obj)
        {
            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(_mainPage);
        }

        private void Player_TimeChanged(object sender, LibVLCSharp.Shared.MediaPlayerTimeChangedEventArgs e)
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(MusicPlayer.player.Media.Duration);
            TimeSpan playerTime = TimeSpan.FromMilliseconds(MusicPlayer.player.Time);
            var percent = (double)decimal.Multiply(decimal.Divide((decimal)playerTime.TotalMilliseconds, (decimal)duration.TotalMilliseconds), 100);
            Dispatcher.BeginInvoke(new Action(() => 
            {
                PlayerProgressTextControlPanel.Text = $"{playerTime:mm\\:ss} / {duration:mm\\:ss}";
                PlayerProgressBarControlPanel.Value = percent;
            }));
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
            MusicPlayer.player.Time = 0;
            TimeSpan duration = TimeSpan.FromMilliseconds(MusicPlayer.player.Media.Duration);
            TimeSpan playerTime = TimeSpan.FromMilliseconds(MusicPlayer.player.Time);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PlayerControlPanelPause.Visibility = Visibility.Hidden;
                PlayerControlPanelPlay.Visibility = Visibility.Visible;
                PlayerProgressBarControlPanel.Value = 0;
                PlayerProgressTextControlPanel.Text = $"Stopped";
            }));
        }

        private bool PlayerControlPanelStopCommand_CanExecute(object arg)
        {
            if (MusicPlayer.player != null) return MusicPlayer.player.IsPlaying;
            return false;
        }

        private void PlayerControlPanelStopCommand_Executed(object obj)
        {
            MusicPlayer.player.Stop();
        }

        private bool PlayerControlPanelPauseCommand_CanExecute(object arg)
        {
            if (MusicPlayer.player != null) return MusicPlayer.player.CanPause;
            return false;
        }

        private void PlayerControlPanelPauseCommand_Executed(object obj)
        {
            MusicPlayer.player.Pause();
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

        private void PlayerControlPanelPlayCommand_Executed(object obj)
        {
            MusicPlayer.player.Play();
        }

        private bool ReplayCurrent_CanExecute(object arg)
        {
            return false;
        }

        private void ReplayCurrent_Executed(object obj)
        {
            
        }

        private bool PlaySong_CanExecute(object arg)
        {
            return false;
        }

        private void PlaySong_Executed(object obj)
        {
            
        }

        private bool LoadNext_CanExecute(object arg)
        {
            return false;
        }

        private void LoadNext_Executed(object obj)
        {
            
        }

        private bool LoadSong_CanExecute(object arg)
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

        private void LoadSong_Executed(object obj)
        {
            string songURLBoxText = string.Empty;
            bool useSongURL = SongUrlBox.Text.Length > 0;
            if (useSongURL) songURLBoxText = SongUrlBox.Text;
            DownloadAllButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            LoadNextButton.Visibility = Visibility.Hidden;
            ReplayCurrentButton.IsEnabled = false;
            LoadSongButton.Content = "Processing...";
            LoadSongButton.IsEnabled = false;

            TokenSource = new CancellationTokenSource();
            IProgress<int> prog = new Progress<int>(percent => DownloadProgressBar.Value = percent);
            IProgress<int> progress = new Progress<int>(percent =>
            {
                DownloadProgressBar.Value = percent;
                if (percent == 100)
                {
                    DownloadProgressBar.Visibility = Visibility.Hidden;
                    LoadSongButton.IsEnabled = true;
                    LoadSongButton.Visibility = Visibility.Hidden;
                    LoadSongButton.Content = "Load Song";
                    PlaySongButton.Visibility = Visibility.Visible;
                }
            });

            BeatSaverDownloader beatSaverDownloader = new();
            Task.Run(async () =>
            {
                if (useSongURL)
                {
                    if (PlaylistHandler == null) PlaylistHandler = new(_mainPage.Connection);
                    Song song = await PlaylistHandler.SetupSongAsyncCall(songURLBoxText, prog);
                    LoadedSong = await beatSaverDownloader.GetSong(song, progress);
                    NotifyPropertyChanged(nameof(LoadedSong));
                    MusicPlayer.player.Media = MusicPlayer.MediaInit(Directory.GetFiles(LoadedSong.SongDataPath, "*.egg")[0]); //We can assume (no shit) that there is only a single .egg file 
                    return;
                }

                LoadedSong = await beatSaverDownloader.GetSong(Playlist.SelectedSong, progress);
                NotifyPropertyChanged(nameof(LoadedSong));
                MusicPlayer.player.Media = MusicPlayer.MediaInit(Directory.GetFiles(LoadedSong.SongDataPath, "*.egg")[0]); //We can assume (no shit) that there is only a single .egg file 

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
            }, TokenSource.Token);
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

        private bool DownloadAll_CanExecute(object arg)
        {
            return Playlist != null && Playlist.Songs.Count > 1;
        }

        private void DownloadAll_Executed(object obj)
        {
            DownloadAllButton.IsEnabled = false;
            DownloadAllButton.Content = "Processing...";
            CancelDownloadButton.Visibility = Visibility.Visible;
            DownloadAllProgressBar.Visibility = Visibility.Visible;
            LoadNextButton.IsEnabled = false;
            LoadSongButton.Visibility = Visibility.Hidden;

            TokenSource = new CancellationTokenSource();
            IProgress<int> progress = new Progress<int>(percent => 
            {
                DownloadAllProgressBar.Value = percent;
                if (percent == 100)
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
                    LoadNextButton.IsEnabled = true;
                    LoadSongButton.Visibility = Visibility.Visible;
                }
            });

            BeatSaverDownloader beatSaverDownloader = new();
            Task.Run(() =>
            {
                beatSaverDownloader.GetSongs(Playlist.Songs.ToArray<Song>(), progress, TokenSource.Token);
            }, TokenSource.Token);
        }

        private void UnLoadPlaylist_Executed(object obj)
        {
            UnLoadPlaylistButton.IsEnabled = false;
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            Playlist.Songs.Clear();

            PlaylistLoadingProgress.Visibility = Visibility.Hidden;
            UnLoadPlaylistButton.Visibility = Visibility.Hidden;
            LoadPlaylistButton.Visibility = Visibility.Visible;
            LoadPlaylistButton.IsEnabled = true;
        }

        private bool LoadPlaylist_CanExecute(object arg)
        {
            //While this is pretty and all, I have to add some way of informing the user why its not executable, so this is subject to change
            return PlaylistLocationBox.Text != string.Empty || File.Exists(PlaylistLocationBox.Text) || PlaylistLocationBox.Text == "<Select from filesystem>";
        }

        private void LoadPlaylist_Executed(object obj)
        {
            if (PlaylistLocationBox.Text == "<<< Select from filesystem >>>")
            {
                OpenFileDialog openFileDialog = new();
                switch (openFileDialog.ShowDialog())
                {
                    case DialogResult.None:
                        Logger.Warning("Dialog box returned 'None'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.OK:
                        if (PlaylistLocation_Source.Contains(openFileDialog.FileName))
                            PlaylistLocation_Source.Remove(openFileDialog.FileName); //Removing and re-adding is easier than moving index, so here we are
                        PlaylistLocation_Source.Insert(1, openFileDialog.FileName);
                        PlaylistLocationBox.SelectedIndex = 1;
                        break;
                    case DialogResult.Cancel:
                        Logger.Warning("Dialog box returned 'Cancel'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.Abort:
                        Logger.Warning("Dialog box returned 'Abort'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.Retry:
                        Logger.Warning("Dialog box returned 'Retry'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.Ignore:
                        Logger.Warning("Dialog box returned 'Ignore'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.Yes:
                        Logger.Warning("Dialog box returned 'Yes'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    case DialogResult.No:
                        Logger.Warning("Dialog box returned 'No'");
                        Logger.Warning("Is this intended behaviour?");
                        return;
                    default:
                        Logger.Warning("Dialog box did not return any value");
                        return;
                }
            }
            LoadPlaylistButton.IsEnabled = false;

            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;
            NotifyPropertyChanged(nameof(PlaylistLoadingProgress));
            var progress = new Progress<int>(percent =>
            {
                PlaylistLoadingProgress.IsIndeterminate = false;
                PlaylistLoadingProgress.Value = percent;
                if (percent == 100)
                {
                    LoadPlaylistButton.Visibility = Visibility.Hidden;
                    PlaylistLoadingProgress.Visibility = Visibility.Hidden;
                    UnLoadPlaylistButton.IsEnabled = true;
                    UnLoadPlaylistButton.Visibility = Visibility.Visible;
                }
                NotifyPropertyChanged(nameof(PlaylistLoadingProgress));
            });

            PlaylistHandler = new PlaylistHandler(PlaylistLocationBox.Text, progress);


            Playlist = PlaylistHandler.Playlist;
            PlaylistSongTable.ItemsSource = Playlist.Songs;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(Playlist.Songs, PlaylistHandler.PlaylistSongTableSync); }));
        }

        private bool AddSong_CanExecute(object arg)
        {
            if (SongUrlBox.Text.Length > 0) LoadSongButton.Visibility = Visibility.Visible;
            return SongUrlBox.Text.Length > 0;
        }

        private async void AddSong_Executed(object obj)
        {
            if (PlaylistHandler == null) PlaylistHandler = new PlaylistHandler(_mainPage.Connection);
            if (Playlist == null) Playlist = PlaylistHandler.Playlist;

            LoadPlaylistButton.IsEnabled = false;
            PlaylistLoadingProgress.Visibility = Visibility.Visible;
            PlaylistLoadingProgress.IsIndeterminate = true;

            var progress = new Progress<int>(percent =>
            {
                PlaylistLoadingProgress.IsIndeterminate = false;
                PlaylistLoadingProgress.Value = percent;
                if (percent == 100)
                {
                    LoadPlaylistButton.Visibility = Visibility.Hidden;
                    PlaylistLoadingProgress.Visibility = Visibility.Hidden;
                    UnLoadPlaylistButton.IsEnabled = true;
                    UnLoadPlaylistButton.Visibility = Visibility.Visible;
                }
                NotifyPropertyChanged(nameof(PlaylistLoadingProgress));
            });

            PlaylistSongTable.ItemsSource = Playlist.Songs;
            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(Playlist.Songs, PlaylistHandler.PlaylistSongTableSync); }));

            var song = await PlaylistHandler.SetupSongAsyncCall(SongUrlBox.Text, progress);
            lock (PlaylistHandler.PlaylistSongTableSync) Playlist.Songs.Add(song);

            SongUrlBox.Text = "";
        }

        private void DifficultySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;

            //Handle null exception
            if (comboBox.Items.Count == 0) return;


            if ((comboBox.DataContext as Song).SelectedDifficulty != comboBox.SelectedItem as SongDifficulty)
            {
                (comboBox.DataContext as Song).SelectedDifficulty = comboBox.SelectedItem as SongDifficulty;
                PlaylistSongTable.Items.Refresh(); //This breaks down with large playlists, but I cant figure out NotifyPropertyChanged so here we are
            }
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
                    var media = MusicPlayer.MediaInit($"{environmentPath}\\cache\\{song.Hash}\\preview.mp3");
                    MusicPlayer.player.Play(media);
                }
            });

            Task.Run(async () =>
            {
                if (!Directory.Exists($"{environmentPath}\\cache\\{song.Hash}")) Directory.CreateDirectory($"{environmentPath}\\cache\\{song.Hash}");
                if (!File.Exists($"{environmentPath}\\cache\\{song.Hash}\\preview.mp3"))
                {
                    using var client = new WebClient();
                    client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                        prog.Report(e.ProgressPercentage);
                    };
                    string url = $"https://cdn.beatsaver.com/{song.Hash.ToLower()}.mp3";
                    await client.DownloadFileTaskAsync(url, $"{environmentPath}\\cache\\{song.Hash}\\preview.mp3");
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


            MusicPlayer.player.Stop();
        }

        private void PlaylistSongTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistSongTable.SelectedIndex == -1) return;
            Playlist.SelectedSong = Playlist.Songs[PlaylistSongTable.SelectedIndex];

            LoadNextButton.Visibility = Visibility.Hidden;
            PlaySongButton.Visibility = Visibility.Hidden;
            LoadSongButton.Visibility = Visibility.Visible;
            LoadedSong = null;
            if (MusicPlayer.player.IsPlaying) MusicPlayer.player.Stop();
            NotifyPropertyChanged(nameof(LoadedSong));
        }
    }
}
