using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TournamentAssistantUI.Models;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for MatchItem.xaml
    /// </summary>
    public partial class MatchItem : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty MatchProperty = DependencyProperty.Register(nameof(Match), typeof(Match), typeof(MatchItem), new PropertyMetadata(new Match()));

        public Match Match
        {
            get {
                return (Match)GetValue(MatchProperty);
            }
            set {
                SetValue(MatchProperty, value);
                NotifyPropertyChanged(nameof(Match));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public MatchItem()
        {
            InitializeComponent();

            DataContext = this;
        }
    }
}
