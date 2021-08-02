using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        public ICommand CreateMatch { get; }
        public ICommand AddAllPlayersToMatch { get; }
        public ICommand DestroyMatch { get; }

        public IConnection Connection { get; }

        public ICommand KickPlayer { get; }
        NavigationService navigationService = null;

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


        public MainPage(bool server, string endpoint = null, int port = 10156, string username = null, string password = null)
        {
            InitializeComponent();

            DataContext = this;

            CreateMatch = new CommandImplementation(CreateMatch_Executed, CreateMatch_CanExecute);
            AddAllPlayersToMatch = new CommandImplementation(AddAllPlayersToMatch_Executed, AddAllPlayersToMatch_CanExecute);
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            KickPlayer = new CommandImplementation(KickPlayer_Executed, (_) => true);

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
        }

        private void KickPlayer_Executed(object obj)
        {

        }

        private void DestroyMatch_Executed(object obj)
        {
            Connection.DeleteMatch(obj as Match);
        }

        private void CreateMatch_Executed(object o)
        {
            var players = PlayerListBox.SelectedItems.Cast<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Players = players.ToArray(),
                Leader = Connection.Self
            };

            Connection.CreateMatch(match);
            NavigateToMatchPage(match);
        }

        private bool CreateMatch_CanExecute(object o)
        {
            //return PlayerListBox.SelectedItems.Count > 1;
            return PlayerListBox.SelectedItems.Count > 0;
            //return true;
        }

        private void AddAllPlayersToMatch_Executed(object o)
        {
            var players = PlayerListBox.Items.Cast<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Players = players.ToArray(),
                Leader = Connection.Self
            };

            Connection.CreateMatch(match);
            NavigateToMatchPage(match);
        }

        private bool AddAllPlayersToMatch_CanExecute(object o)
        {
            return PlayerListBox.Items.Count > 0;
        }

        private void MatchListItemGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var matchItem = (sender as MatchItem);
            NavigateToMatchPage(matchItem.Match);
        }

        private void NavigateToMatchPage(Match match)
        {
            navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MatchPage(match, this));
        }

        private void MatchListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViwer = GetScrollViewer(sender as DependencyObject) as ScrollViewer;
            if (scrollViwer != null)
            {
                if (e.Delta < 0)
                {
                    scrollViwer.ScrollToVerticalOffset(scrollViwer.VerticalOffset + 15);
                }
                else if (e.Delta > 0)
                {
                    scrollViwer.ScrollToVerticalOffset(scrollViwer.VerticalOffset - 15);
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

        private void NewMatch_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as Button;
            var ParentGrid = ((item.Parent as StackPanel).Parent as StackPanel).Parent as Grid;

            Storyboard MenuClose = ParentGrid.TryFindResource("MenuClose") as Storyboard;
            BeginStoryboard(MenuClose);
        }

        private void OngoingMatches_Click(object sender, RoutedEventArgs e)
        {
            MenuNavigate(new OngoingMatchesView());
        }

        private void ConnectedCoordinators_Click(object sender, RoutedEventArgs e)
        {

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
