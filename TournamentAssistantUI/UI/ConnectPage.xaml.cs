using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TournamentAssistantUI.Misc;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for ConnectPage.xaml
    /// </summary>
    public partial class ConnectPage : Page
    {
        public ConnectPage()
        {
            InitializeComponent();

            //WinConsole.Initialize();
        }

        private void Host_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MainPage(true));
        }

        private void Mock_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MockClient());
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MainPage(false, HostIP.Text, Username.Text));
        }
    }
}
