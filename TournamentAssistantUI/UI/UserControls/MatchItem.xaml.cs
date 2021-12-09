using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

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

        public SystemClient Connection
        {
            get { return (SystemClient)GetValue(ConnectionProperty); }
            set { SetValue(ConnectionProperty, value); }
        }

        public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(nameof(Connection), typeof(SystemClient), typeof(MatchItem));


        public MatchItem()
        {
            InitializeComponent();

            Loaded += MatchItem_Loaded;
            Unloaded += MatchItem_Unloaded;
        }

        private void MatchItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (Connection != null)
            {
                Connection.PlayerInfoUpdated += Connection_PlayerInfoUpdated;
            }
        }

        private void MatchItem_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Connection != null)
            {
                Connection.PlayerInfoUpdated -= Connection_PlayerInfoUpdated;
            }
        }

        private Task Connection_PlayerInfoUpdated(Player player)
        {
            Dispatcher.Invoke(() =>
            {
                var index = Match.Players.ToList().FindIndex(x => x == player);
                if (index >= 0)
                {
                    Match.Players.OrderByDescending(x => x.Score);
                    PlayerListBox.Items.Refresh();
                }
            });
            return Task.CompletedTask;
        }
    }
}
