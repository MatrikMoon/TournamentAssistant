using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.UI.UserControls;
using static TournamentAssistantShared.GlobalConstants;
using File = System.IO.File;
using System.ComponentModel;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public ICommand CreateStandardMatch { get; }
        public ICommand CreateBRMatch { get; }
        public ICommand DestroyMatch { get; }
        public ICommand MoveAllRight { get; }
        public ICommand MoveSelectedRight { get; }
        public ICommand MoveSelectedLeft { get; }
        public ICommand MoveAllLeft { get; }
        public ICommand DisconnectFromServer { get; }

        public IConnection Connection { get; }

        public CollectionView ListBoxLeftView { get; }
        public ObservableCollection<Player> ListBoxLeft { get; }
        private readonly object ListBoxLeftSync = new object();
        public CollectionView ListBoxRightView { get; }
        public ObservableCollection<Player> ListBoxRight { get; }
        private readonly object ListBoxRightSync = new object();

        Dictionary<Match, object> ActiveMatchPages { get; set; }

        private NavigationService navigationService = null;

        public Player[] PlayersNotInMatch
        {
            get
            {
                List<Player> playersInMatch = new List<Player>();
                foreach (var match in Connection.State.Matches)
                {
                    playersInMatch.AddRange(match.Players);
                }
                return Connection.State.Players.Except(playersInMatch).ToArray();
            }
        }


        public MainPage(string endpoint = null, int port = 10156, string username = null, string password = null)
        {
            InitializeComponent();

            DataContext = this;

            Application.Current.Exit += (sender, e) => 
            {
                foreach (var match in ActiveMatchPages.Keys)
                {
                    Connection.DeleteMatch(match);
                }

                (Connection as SystemClient).Shutdown();

                Logger.ArchiveLogs();
            };

            CreateStandardMatch = new CommandImplementation(CreateStandardMatch_Executed, CreateStandardMatch_CanExecute);
            CreateBRMatch = new CommandImplementation(CreateBRMatch_Executed, CreateBRMatch_CanExecute);
            
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            MoveAllRight = new CommandImplementation(MoveAllRight_Executed, MoveAllRight_CanExecute);
            MoveSelectedRight = new CommandImplementation(MoveSelectedRight_Executed, MoveSelectedRight_CanExecute);
            MoveAllLeft = new CommandImplementation(MoveAllLeft_Executed, MoveAllLeft_CanExecute);
            MoveSelectedLeft = new CommandImplementation(MoveSelectedLeft_Executed, MoveSelectedLeft_CanExecute);

            DisconnectFromServer = new CommandImplementation(DisconnectFromServer_Executed, (_) => true);

            ListBoxLeft = new ObservableCollection<Player>();
            ListBoxLeftView = (CollectionView)CollectionViewSource.GetDefaultView(ListBoxLeft);
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(ListBoxLeft, ListBoxLeftSync); }));
            ListBoxLeft.CollectionChanged += ObservableCollectionChanged;
            ListBoxRight = new ObservableCollection<Player>();
            ListBoxRightView = (CollectionView)CollectionViewSource.GetDefaultView(ListBoxRight);
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(ListBoxRight, ListBoxRightSync); }));
            ListBoxRight.CollectionChanged += ObservableCollectionChanged;

            ActiveMatchPages = new();

            Connection = new SystemClient(endpoint, port, username, Connect.ConnectTypes.Coordinator, password: password);
            (Connection as SystemClient).Start();
            (Connection as SystemClient).PlayerConnected += MainPage_PlayerConnected;
            (Connection as SystemClient).PlayerDisconnected += MainPage_PlayerDisconnected;
            (Connection as SystemClient).ConnectedToServer += MainPage_ConnectedToServer;
            (Connection as SystemClient).MatchDeleted += MainPage_MatchDeleted;
            (Connection as SystemClient).MatchInfoUpdated += MainPage_MatchInfoUpdated;

            ListBoxLeftView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            ListBoxRightView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        private void ObservableCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private void MainPage_MatchInfoUpdated(Match obj)
        {
            var playersNotInMatch = from players in Connection.State.Players
                                    where Connection.State.Matches.All(match => !match.Players.Contains(players))
                                    select players;
            lock (ListBoxLeftSync)
            {
                foreach (var player in playersNotInMatch)
                {
                    if (ListBoxLeft.Contains(player)) continue;
                    ListBoxLeft.Add(player);
                }
            }
        }

        private void MainPage_MatchDeleted(Match match)
        {
            if (ActiveMatchPages.Keys.Contains(match)) ActiveMatchPages.Remove(match);
            var playersReleasedFromMatch = from players in match.Players select players;
            lock (ListBoxLeftSync)
            {
                foreach (var player in playersReleasedFromMatch)
                {
                    if (ListBoxLeft.Contains(player)) continue;
                    ListBoxLeft.Add(player);
                }
            }
        }

        private void DisconnectFromServer_Executed(object obj)
        {
            (Connection as SystemClient).PlayerConnected -= MainPage_PlayerConnected;
            (Connection as SystemClient).PlayerDisconnected -= MainPage_PlayerDisconnected;
            (Connection as SystemClient).ConnectedToServer -= MainPage_ConnectedToServer;

            foreach (var match in ActiveMatchPages.Keys)
            {
                Connection.DeleteMatch(match);
            }

            (Connection as SystemClient).Shutdown();

            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new ConnectPage());
        }

        private void MainPage_ConnectedToServer(ConnectResponse response)
        {
            var playersNotInMatch = from players in response.State.Players 
                                    where response.State.Matches.All(match => !match.Players.Contains(players)) 
                                    select players;
            lock (ListBoxLeftSync)
            {
                foreach (var player in playersNotInMatch)
                {
                    ListBoxLeft.Add(player);
                }
            }

            Application.Current.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested); //WPF not updating CanExecute workaround (basically manually raise the event that causes it to get called eventually)
        }
        private void MainPage_PlayerDisconnected(Player player)
        {
            lock (ListBoxLeftSync)
            {
                ListBoxLeft.Remove(player);
            }
            lock (ListBoxRightSync)
            {
                ListBoxRight.Remove(player);
            }

            MessageBox.Show($"Player {player.Name} disconnected!", SharedConstructs.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        private void MainPage_PlayerConnected(Player player)
        {
            lock (ListBoxLeftSync)
            {
                ListBoxLeft.Add(player);
            }
        }

        private bool MoveAllRight_CanExecute(object arg)
        {
            return ListBoxLeft.Count > 0;
        }
        private bool MoveSelectedRight_CanExecute(object arg)
        {
            return PlayerListBoxLeft.SelectedItems.Count > 0;
        }
        private bool MoveAllLeft_CanExecute(object arg)
        {
            return ListBoxRight.Count > 0;
        }
        private bool MoveSelectedLeft_CanExecute(object arg)
        {
            return PlayerListBoxRight.SelectedItems.Count > 0;
        }
        private void MoveAllRight_Executed(object obj)
        {
            foreach (var item in ListBoxLeft)
            {
                ListBoxRight.Add(item);
            }
            ListBoxLeft.Clear();
        }
        private void MoveSelectedRight_Executed(object obj)
        {
            var players = PlayerListBoxLeft.SelectedItems.Cast<Player>().ToArray();
            foreach (var player in players)
            {
                ListBoxRight.Add(player);
                ListBoxLeft.Remove(player);
            }
        }
        private void MoveAllLeft_Executed(object obj)
        {
            foreach (var item in ListBoxRight)
            {
                ListBoxLeft.Add(item);
            }
            ListBoxRight.Clear();
        }
        private void MoveSelectedLeft_Executed(object obj)
        {
            var players = PlayerListBoxRight.SelectedItems.Cast<Player>().ToArray();
            foreach (var player in players)
            {
                ListBoxLeft.Add(player);
                ListBoxRight.Remove(player);
            }
        }

        private void CreateStandardMatch_Executed(object obj)
        {
            var players = ListBoxRight.ToArray<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Players = players,
                Leader = Connection.Self
            };

            lock (ListBoxRightSync)
            {
                ListBoxRight.Clear();
            }

            Connection.CreateMatch(match);
            NavigateToStandardMatchPage(match);
        }
        private bool CreateStandardMatch_CanExecute(object arg)
        {
            return ListBoxRight.Count > 0;
        }
        private void CreateBRMatch_Executed(object obj)
        {
            var players = ListBoxRight.ToArray<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Players = players,
                Leader = Connection.Self
            };

            lock (ListBoxRightSync)
            {
                ListBoxRight.Clear();
            }

            Connection.CreateMatch(match);
            NavigateToBrMatchPage(match);
        }
        private bool CreateBRMatch_CanExecute(object arg)
        {
            return ListBoxRight.Count > 0;
        }

        private void DestroyMatch_Executed(object obj)
        {
            ActiveMatchPages.Remove(obj as Match);
            Connection.DeleteMatch(obj as Match);
        }

        private void MatchListItemGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var matchItem = (sender as MatchItem);
            NavigateToStandardMatchPage(matchItem.Match);
        }

        private void NavigateToBrMatchPage(Match match)
        {
            var page = new BRMatchPage(this, match);

            ActiveMatchPages.Add(match, page);

            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(page);
        }

        private void NavigateToStandardMatchPage(Match match)
        {
            var page = new MatchPage(match, this);

            ActiveMatchPages.Add(match, page);

            navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(page);
        }

        private void MatchListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (GetScrollViewer(sender as DependencyObject) is ScrollViewer scrollViewer)
            {
                if (e.Delta < 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 15);
                }
                else if (e.Delta > 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 15);
                }
            }
        }

        private static DependencyObject GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer)
            { return o; }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);

                var result = GetScrollViewer(child);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }
            return null;
        }

        private void MenuNavigate(object target)
        {
            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(target);
        }

        private void CloseMenuOnClick(object sender)
        {
            var item = sender as Button;
            var ParentGrid = ((item.Parent as StackPanel).Parent as StackPanel).Parent as Grid;

            Storyboard MenuClose = ParentGrid.TryFindResource("MenuClose") as Storyboard;
            BeginStoryboard(MenuClose);
        }

        private void NewMatch_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
        }

        private void OngoingMatches_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
            MenuNavigate(new OngoingMatchesView(this));
        }

        private void ConnectedCoordinators_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
            MenuNavigate(new ConnectedCoordinatorsView(this));
        }

        private void ConnectedClients_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ConnectionLog_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ServerSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OpenMenuOutsideBounds_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as Button;
            var ParentGrid = item.Parent as Grid;

            Storyboard MenuClose = ParentGrid.TryFindResource("MenuClose") as Storyboard;
            BeginStoryboard(MenuClose);
        }
    }
}
