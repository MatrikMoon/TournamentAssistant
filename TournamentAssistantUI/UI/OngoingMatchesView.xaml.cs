using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for OngoingMatchesView.xaml
    /// </summary>
    public partial class OngoingMatchesView : Page
    {
        NavigationService navigationService = null;
        public MainPage MainPage;
        public OngoingMatchesView(MainPage mainPage)
        {
            InitializeComponent();
            MainPage = mainPage;
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

        private void CloseMenuOnClick(object sender)
        {
            var item = sender as Button;
            var ParentGrid = ((item.Parent as StackPanel).Parent as StackPanel).Parent as Grid;

            Storyboard MenuClose = ParentGrid.TryFindResource("MenuClose") as Storyboard;
            BeginStoryboard(MenuClose);
        }

        private void NewMatch_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
            MenuNavigate(MainPage);
        }

        private void OngoingMatches_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
        }

        private void ConnectedCoordinators_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuOnClick(sender);
            MenuNavigate(new ConnectedCoordinatorsView(MainPage));
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
