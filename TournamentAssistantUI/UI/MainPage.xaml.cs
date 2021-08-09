using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.UI.UserControls;

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

        public ObservableCollection<Player> ListBoxLeft { get; }
        private readonly object ListBoxLeftSync = new object();
        public ObservableCollection<Player> ListBoxRight { get; }
        private readonly object ListBoxRightSync = new object();


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

            CreateStandardMatch = new CommandImplementation(CreateStandardMatch_Executed, CreateStandardMatch_CanExecute);
            CreateBRMatch = new CommandImplementation(CreateBRMatch_Executed, CreateBRMatch_CanExecute);
            
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            MoveAllRight = new CommandImplementation(MoveAllRight_Executed, MoveAllRight_CanExecute);
            MoveSelectedRight = new CommandImplementation(MoveSelectedRight_Executed, MoveSelectedRight_CanExecute);
            MoveAllLeft = new CommandImplementation(MoveAllLeft_Executed, MoveAllLeft_CanExecute);
            MoveSelectedLeft = new CommandImplementation(MoveSelectedLeft_Executed, MoveSelectedLeft_CanExecute);

            DisconnectFromServer = new CommandImplementation(DisconnectFromServer_Executed, (_) => true);

            ListBoxLeft = new ObservableCollection<Player>();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(ListBoxLeft, ListBoxLeftSync); }));
            ListBoxRight = new ObservableCollection<Player>();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { BindingOperations.EnableCollectionSynchronization(ListBoxRight, ListBoxRightSync); }));

            if (server)
            {
                Connection = new SystemServer();
                (Connection as SystemServer).Start();
            }
            else
            {
                Connection = new SystemClient(endpoint, port, username, TournamentAssistantShared.Models.Packets.Connect.ConnectTypes.Coordinator, password: password);
                (Connection as SystemClient).Start();
            }

            (Connection as SystemClient).PlayerConnected += MainPage_PlayerConnected;
            (Connection as SystemClient).PlayerDisconnected += MainPage_PlayerDisconnected;
            (Connection as SystemClient).ConnectedToServer += MainPage_ConnectedToServer;
        }

        private void DisconnectFromServer_Executed(object obj)
        {
            (Connection as SystemClient).PlayerConnected -= MainPage_PlayerConnected;
            (Connection as SystemClient).PlayerDisconnected -= MainPage_PlayerDisconnected;
            (Connection as SystemClient).ConnectedToServer -= MainPage_ConnectedToServer;

            (Connection as SystemClient).Shutdown();

            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new ConnectPage());
        }

        private void MainPage_ConnectedToServer(TournamentAssistantShared.Models.Packets.ConnectResponse response)
        {
            foreach (var item in response.State.Players)
            {
                lock (ListBoxLeftSync)
                {
                    ListBoxLeft.Add(item);
                }
            }
        }
        private void MainPage_PlayerDisconnected(Player obj)
        {
            lock (ListBoxLeftSync)
            {
                ListBoxLeft.Remove(obj);
            }
            lock (ListBoxRightSync)
            {
                ListBoxRight.Remove(obj);
            }
        }
        private void MainPage_PlayerConnected(Player obj)
        {
            lock (ListBoxLeftSync)
            {
                ListBoxLeft.Add(obj);
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

            Connection.CreateMatch(match);
            NavigateToBrMatchPage(match);
        }
        private bool CreateBRMatch_CanExecute(object arg)
        {
            return ListBoxRight.Count > 0;
        }

        private void DestroyMatch_Executed(object obj)
        {
            Connection.DeleteMatch(obj as Match);
        }

        private void MatchListItemGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var matchItem = (sender as MatchItem);
            NavigateToStandardMatchPage(matchItem.Match);
        }

        private void NavigateToBrMatchPage(Match match)
        {
            if (navigationService == null) navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new BRMatchPage(this, match));
        }

        private void NavigateToStandardMatchPage(Match match)
        {
            navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MatchPage(match, this));
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
