using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BattleSaberShared.Models;

namespace BattleSaberUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for UserDialog.xaml
    /// </summary>
    public partial class GameOverDialog : UserControl
    {
        public List<Player> Players { get; set; }

        public GameOverDialog(List<Player> players)
        {
            Players = players.OrderByDescending(x => x.CurrentScore).ToList();

            DataContext = this;

            InitializeComponent();
        }
    }
}
