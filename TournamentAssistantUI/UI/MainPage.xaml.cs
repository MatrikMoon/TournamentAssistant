using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public ICommand AddAllPlayersToMatch { get; }
        public ICommand DestroyMatch { get; }

        public SystemClient Client { get; }
        
        public Player[] PlayersNotInMatch
        {
            get
            {
                List<Player> playersInMatch = new List<Player>();
                foreach (var match in Client.State.Matches)
                {
                    playersInMatch.AddRange(match.Players);
                }
                return Client.State.Players.Except(playersInMatch).ToArray();
            }
        }


        public MainPage(string endpoint = null, int port = 10156, string username = null, string password = null)
        {
            InitializeComponent();

            DataContext = this;

            CreateMatch = new CommandImplementation(CreateMatch_Executed, CreateMatch_CanExecute);
            AddAllPlayersToMatch = new CommandImplementation(AddAllPlayersToMatch_Executed, AddAllPlayersToMatch_CanExecute);
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            Client = new SystemClient(endpoint, port, username, TournamentAssistantShared.Models.Packets.Connect.ConnectTypes.Coordinator, password: password);

            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
            Task.Run(Client.Start);

            //This marks the death of me trying to do WPF correctly. This became necessary after the switch to protobuf, when NotifyUpdate stopped having an effect on certain ui elements
            Client.MatchCreated += Client_MatchChanged;
            Client.MatchInfoUpdated += Client_MatchChanged;
            Client.MatchDeleted += Client_MatchChanged;

            Client.PlayerConnected += Client_PlayerChanged;
            Client.PlayerInfoUpdated += Client_PlayerChanged;
            Client.PlayerDisconnected += Client_PlayerChanged;

            Client.CoordinatorConnected += Client_CoordinatorChanged;
            Client.CoordinatorDisconnected += Client_CoordinatorChanged;
        }

        private Task Client_MatchChanged(Match arg)
        {
            MatchListBox.Items.Refresh();
            return Task.CompletedTask;
        }

        private Task Client_PlayerChanged(Player arg)
        {
            PlayerListBox.Items.Refresh();
            return Task.CompletedTask;
        }

        private Task Client_CoordinatorChanged(Coordinator arg)
        {
            CoordinatorListBox.Items.Refresh();
            return Task.CompletedTask;
        }

        private void DestroyMatch_Executed(object obj)
        {
            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
            Task.Run(() => Client.DeleteMatch(obj as Match));
        }

        private void CreateMatch_Executed(object o)
        {
            var players = PlayerListBox.SelectedItems.Cast<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString()
            };
            match.Players.AddRange(players);
            match.Leader = Client.Self;

            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
            Task.Run(() => Client.CreateMatch(match));
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
                Leader = Client.Self
            };
            match.Players.AddRange(players);

            //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
            Task.Run(() => Client.CreateMatch(match));
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
