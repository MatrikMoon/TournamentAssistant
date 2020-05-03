using MaterialDesignThemes.Wpf;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using BattleSaberShared;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using BattleSaberUI.UI.UserControls;
using static BattleSaberShared.Packet;

namespace BattleSaberUI.UI
{
    /// <summary>
    /// Interaction logic for MockClient.xaml
    /// </summary>
    public partial class MockClient : Page
    {
        private Player _self;
        public Player Self
        {
            get
            {
                return _self;
            }
            set
            {
                _self = value;
            }
        }

        private State State { get; set; }

        public MockClient()
        {
            InitializeComponent();

            //Set up log monitor
            Logger.MessageLogged += (type, message) =>
            {
                SolidColorBrush textBrush = null;
                switch (type)
                {
                    case Logger.LogType.Debug:
                        textBrush = Brushes.LightSkyBlue;
                        break;
                    case Logger.LogType.Error:
                        textBrush = Brushes.Red;
                        break;
                    case Logger.LogType.Info:
                        textBrush = Brushes.White;
                        break;
                    case Logger.LogType.Success:
                        textBrush = Brushes.Green;
                        break;
                    case Logger.LogType.Warning:
                        textBrush = Brushes.Yellow;
                        break;
                    default:
                        break;
                }

                LogBlock.Dispatcher.Invoke(() => LogBlock.Inlines.Add(new Run($"{message}\n") { Foreground = textBrush }));
            };
        }

        private void MouseCapture_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new DropperPage());
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Client_ServerDisconnected()
        {
        }

        private void Client_PacketRecieved(Packet packet)
        {

        }

        private void PlayState_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DownloadState_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Stress_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private async void Dialog_Click(object sender, RoutedEventArgs e)
        {
            var result = await DialogHost.Show(new PlayerDialog(Self), "RootDialog");

            Console.WriteLine(result);
        }

        private void SetScore_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SongFinished_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
