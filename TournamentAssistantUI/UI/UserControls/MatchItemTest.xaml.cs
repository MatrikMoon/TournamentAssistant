using System;
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

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for MatchItem.xaml
    /// </summary>
    public partial class MatchItemTest : UserControl
    {
        public Match Match
        {
            get { return (Match)GetValue(MatchProperty); }
            set { SetValue(MatchProperty, value); }
        }

        public static readonly DependencyProperty MatchProperty = DependencyProperty.Register(nameof(Match), typeof(Match), typeof(MatchItemTest));

        public MatchItemTest()
        {
            InitializeComponent();
        }
    }
}
