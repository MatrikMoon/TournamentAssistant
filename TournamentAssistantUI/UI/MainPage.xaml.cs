using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        public ICommand DestroyMatch { get; }

        public IConnection Connection { get; }
        
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


        public MainPage(bool server, string endpoint = null, string username = null)
        {
            InitializeComponent();

            DataContext = this;

            CreateMatch = new CommandImplementation(CreateMatch_Executed, CreateMatch_CanExecute);
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            if (server)
            {
                Connection = new TournamentAssistantServer();
                (Connection as TournamentAssistantServer).Start();
            }
            else
            {
                Connection = new TournamentAssistantClient(endpoint, username);
                (Connection as TournamentAssistantClient).Start();
            }

            /*Connection.PlayerConnected += RefreshPlayerListBox;
            Connection.PlayerDisconnected += RefreshPlayerListBox;
            Connection.MatchCreated += RefreshPlayerListBox;
            Connection.MatchDeleted += RefreshPlayerListBox;*/
        }

        private void RefreshPlayerListBox(object _)
        {
            Dispatcher.Invoke(() =>
            {
                PlayerListBox.Items.Clear();
                foreach (var player in PlayersNotInMatch) PlayerListBox.Items.Add(player);
            });
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
        }

        private void MatchListItemGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var matchItem = (sender as Grid).Children[0] as MatchItem;
            NavigateToMatchPage(matchItem.DataContext as Match);
        }

        private void NavigateToMatchPage(Match match)
        {
            var navigationService = NavigationService.GetNavigationService(this);
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
    }
}
