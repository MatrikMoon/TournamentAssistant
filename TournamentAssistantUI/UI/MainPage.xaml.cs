using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Models;

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

        public MainPage(bool server, string endpoint = null, string username = null)
        {
            InitializeComponent();

            DataContext = this;

            CreateMatch = new CommandImplementation(CreateMatch_Executed, CreateMatch_CanExecute);
            DestroyMatch = new CommandImplementation(DestroyMatch_Executed, (_) => true);

            if (server)
            {
                Connection = new Server();
                (Connection as Server).Start();
            }
            else
            {
                Connection = new Client(endpoint, username);
                (Connection as Client).Start();
            }

            /*var matchItem = new MatchItem()
            {
                Match = new Match()
                {
                    Guid = "1",
                    Coordinator = new MatchCoordinator()
                    {
                        Guid = "1",
                        Name = "Match Coordinator"
                    },
                    Players = new Player[]
                    {
                        new Player()
                        {
                            Guid = "11",
                            Name = "Player name"
                        }
                    }
                }
            };

            MatchListBox.Items.Insert(0, matchItem);*/
        }

        private void DestroyMatch_Executed(object obj)
        {
            Connection.DeleteMatch(obj as Match);
        }

        private void CreateMatch_Executed(object o)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            var players = PlayerListBox.SelectedItems.Cast<Player>();
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Players = players.ToArray(),
                Coordinator = Connection.Self
            };

            Connection.CreateMatch(match);
            navigationService.Navigate(new MatchPage(match, this));
        }

        private bool CreateMatch_CanExecute(object o)
        {
            return PlayerListBox.SelectedItems.Count > 1;
        }
    }
}
