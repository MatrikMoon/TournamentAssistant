using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

/**
 * Created by Moon 8/20(?)/2019
 * This class was simple at first, but adding the connection event listener made it a giant mess. In the future,
 * I should use proper techniques to ensure the binding causes the view to update automatically, however
 * I am not experienced enough with wpf to implement that at this time
 */

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for MatchItem.xaml
    /// </summary>
    public partial class MatchItem : UserControl
    {
        public Match Match
        {
            get { return (Match)GetValue(MatchProperty); }
            set { SetValue(MatchProperty, value); }
        }

        public static readonly DependencyProperty MatchProperty = DependencyProperty.Register(nameof(Match), typeof(Match), typeof(MatchItem));

        public SystemClient Client
        {
            get { return (SystemClient)GetValue(ConnectionProperty); }
            set { SetValue(ConnectionProperty, value); }
        }

        public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(nameof(Client), typeof(SystemClient), typeof(MatchItem));


        public MatchItem()
        {
            InitializeComponent();

            Loaded += MatchItem_Loaded;
            Unloaded += MatchItem_Unloaded;
        }

        private void MatchItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (Client != null)
            {
                Client.UserInfoUpdated += Connection_PlayerInfoUpdated;
                RefreshUserBoxes();
            }
        }

        private void MatchItem_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Client != null)
            {
                Client.UserInfoUpdated -= Connection_PlayerInfoUpdated;
            }
        }

        private void RefreshUserBoxes()
        {
            //I've given up on bindnigs now that I need to filter a user list for each box. We're doing this instead since WPF was supposed to be a temporary solution anyway
            Dispatcher.Invoke(() =>
            {
                PlayerListBox.Items.Clear();

                if (Client?.State?.Users != null)
                {
                    foreach (var player in Client.State.Users.Where(x => x.ClientType == User.ClientTypes.Player))
                    {
                        PlayerListBox.Items.Add(player);
                    }
                }
            });
        }

        private Task Connection_PlayerInfoUpdated(User player)
        {
            Dispatcher.Invoke(() =>
            {
                var index = Match.AssociatedUsers.ToList().FindIndex(x => x.UserEquals(player));
                if (index >= 0)
                {
                    Match.AssociatedUsers.OrderByDescending(x => x.Score);
                    RefreshUserBoxes();
                }
            });
            return Task.CompletedTask;
        }
    }
}
