using System;
using System.ComponentModel;
using System.Linq;
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

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for MatchItem.xaml
    /// </summary>
    public partial class MatchItem : UserControl
    {
        public IConnection Connection
        {
            get { return (IConnection)GetValue(ConnectionProperty); }
            set { SetValue(ConnectionProperty, value); }
        }

        public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(nameof(Connection), typeof(IConnection), typeof(MatchItem), new PropertyMetadata(null, new PropertyChangedCallback(OnConnectionChanged)));

        private static void OnConnectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //Once the connection is set, we can register here to update our listbox in case any player state changes
            if (e.NewValue != null)
            {
                (e.NewValue as IConnection).PlayerInfoUpdated += (d as MatchItem).MatchItem_PlayerInfoUpdated;
            }
        }

        private void MatchItem_PlayerInfoUpdated(Player player)
        {
            Dispatcher.Invoke(() =>
            {
                if ((DataContext as Match).Players.Any(x => x.Guid == player.Guid))
                {
                    PlayerListBox.Items.Refresh();
                }
            });
        }

        public MatchItem()
        {
            InitializeComponent();

            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            if (Connection != null) Connection.PlayerInfoUpdated -= MatchItem_PlayerInfoUpdated;
        }
    }
}
