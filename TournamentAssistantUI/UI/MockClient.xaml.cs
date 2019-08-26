using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI.UI
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
                NameBlock.Dispatcher.Invoke(() => NameBlock.Text = value.Name);
                DownloadStateBlock.Dispatcher.Invoke(() => DownloadStateBlock.Text = value.CurrentDownloadState.ToString());
                PlayStateBlock.Dispatcher.Invoke(() => PlayStateBlock.Text = value.CurrentPlayState.ToString());
            }
        }

        private TournamentState State { get; set; }
        private Network.Client client;

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

        private void Send(Packet packet, Network.Client overrideClient = null)
        {
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo})");
            (overrideClient ?? client).Send(packet.ToBytes());
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            State = new TournamentState();
            State.Players = new Player[0];
            State.Coordinators = new MatchCoordinator[0];
            State.Matches = new Match[0];

            client = new Network.Client("", 10155);
            client.PacketRecieved += Client_PacketRecieved;
            client.ServerDisconnected += Client_ServerDisconnected;

            client.Start();

            Send(new Packet(new Connect()
            {
                clientType = Connect.ConnectType.Player,
                name = NameBox.Text
            }));
        }

        private void Client_ServerDisconnected()
        {
        }

        private void Client_PacketRecieved(Packet packet)
        {
            if (packet.Type == PacketType.Event)
            {
                var @event = packet.SpecificPacket as Event;
                if (@event.eventType == Event.EventType.SetSelf)
                {
                    Self = @event.changedObject as Player;
                }
            }

            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).levelId + " : " + (packet.SpecificPacket as PlaySong).difficulty;
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                secondaryInfo = (packet.SpecificPacket as LoadSong).levelId;
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }
            else if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
            }

            Logger.Info($"Recieved: ({packet.Type}) ({secondaryInfo})");
        }

        private void PlayState_Click(object sender, RoutedEventArgs e)
        {
            if ((int)Self.CurrentPlayState < 1) Self.CurrentPlayState++;
            else Self.CurrentPlayState = 0;

            Self = Self;

            Send(new Packet(new Event()
            {
                eventType = Event.EventType.PlayerUpdated,
                changedObject = Self
            }));
        }

        private void DownloadState_Click(object sender, RoutedEventArgs e)
        {
            if ((int)Self.CurrentDownloadState < 3) Self.CurrentDownloadState++;
            else Self.CurrentDownloadState = 0;

            Self = Self;

            Send(new Packet(new Event()
            {
                eventType = Event.EventType.PlayerUpdated,
                changedObject = Self
            }));
        }

        private void Stress_Click(object sender, RoutedEventArgs e)
        {
            var rand = new Random();

            var numberOfActions = 100;
            var intervalBetweenActions = 0.1;

            Network.Client stressClient = null;
            Player stressSelf = new Player()
            {
                Name = $"TEST-({Guid.NewGuid()})",
                Guid = "test",
            };

            Action connect = () =>
            {
                if (stressClient != null && stressClient.Connected) stressClient.Shutdown();

                State = new TournamentState();
                State.Players = new Player[0];
                State.Coordinators = new MatchCoordinator[0];
                State.Matches = new Match[0];

                stressClient = new Network.Client("", 10155);
                stressClient.PacketRecieved += Client_PacketRecieved;
                stressClient.ServerDisconnected += Client_ServerDisconnected;

                stressClient.Start();

                Send(new Packet(new Connect()
                {
                    clientType = Connect.ConnectType.Player,
                    name = stressSelf.Name
                }), stressClient);
            };

            Action changeDownloadState = () =>
            {
                if ((int)stressSelf.CurrentDownloadState < 3) stressSelf.CurrentDownloadState++;
                else stressSelf.CurrentDownloadState = 0;

                Send(new Packet(new Event()
                {
                    eventType = Event.EventType.PlayerUpdated,
                    changedObject = stressSelf
                }), stressClient);
            };

            Action changePlayState = () =>
            {
                if ((int)stressSelf.CurrentPlayState < 1) stressSelf.CurrentPlayState++;
                else stressSelf.CurrentPlayState = 0;

                Send(new Packet(new Event()
                {
                    eventType = Event.EventType.PlayerUpdated,
                    changedObject = stressSelf
                }), stressClient);
            };

            Action[] possibleActions = new Action[] { connect, changeDownloadState, changePlayState };

            connect.Invoke();
            Thread.Sleep(10 * 1000);

            for (int i = 0; i < numberOfActions;  i++)
            {
                possibleActions[rand.Next(possibleActions.Length)]?.Invoke();

                Thread.Sleep((int)(intervalBetweenActions * 1000));
            }
        }
    }
}
