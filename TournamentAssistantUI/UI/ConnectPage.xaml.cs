using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantUI.Misc;
using static TournamentAssistantShared.GlobalConstants;

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

            //Create work directories, if they don't exist
            if (!Directory.Exists(AppDataTemp)) Directory.CreateDirectory(AppDataTemp);
            if (!Directory.Exists(AppDataLogs)) Directory.CreateDirectory(AppDataLogs);
            if (!Directory.Exists(AppDataCache)) Directory.CreateDirectory(AppDataCache);
            if (!Directory.Exists(AppDataSongDataPath)) Directory.CreateDirectory(AppDataSongDataPath);

            Logger.LoggerFileInit();

#if DEBUG
            WinConsole.Initialize();
#else
            MockButton.Visibility = Visibility.Hidden;
#endif
        }

        private void Mock_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new MockPage());
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var hostText = HostIP.Text.Split(':');

            var mainPage = new MainPage(hostText[0], hostText.Length > 1 ? int.Parse(hostText[1]) : 10156, string.IsNullOrEmpty(Username.Text) ? "Coordinator" : Username.Text, Password.Text);

            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(mainPage);
        }
    }
}
