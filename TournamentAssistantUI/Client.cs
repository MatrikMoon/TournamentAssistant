using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI
{
    class Client : IConnection, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private TournamentState _state;
        public TournamentState State {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                NotifyPropertyChanged(nameof(State));
            }
        }

        public MatchCoordinator Self { get; set; }

        private Network.Client client;
        private Timer heartbeatTimer = new Timer();
        private string endpoint;
        private string username;

        public Client(string endpoint, string username)
        {
            this.endpoint = endpoint;
            this.username = username;
        }

        public void Start()
        {
            ConnectToServer();

            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            heartbeatTimer.Start();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            try
            {
                var command = new Command();
                command.commandType = Command.CommandType.Heartbeat;
                client.Send(new Packet(command).ToBytes());
            }
            catch (Exception e)
            {
                Logger.Debug("HEARTBEAT FAILED");
                Logger.Debug(e.ToString());

                ConnectToServer();
            }
        }

        private void ConnectToServer()
        {
            State = new TournamentState();
            State.Players = new Player[0];
            State.Coordinators = new MatchCoordinator[0];
            State.Matches = new Match[0];

            client = new Network.Client(endpoint, 10155);
            client.PacketRecieved += Client_PacketRecieved;
            client.ServerDisconnected += Client_ServerDisconnected;
            client.Start();

            client.Send(new Packet(new Connect()
            {
                clientType = Connect.ConnectType.Coordinator,
                name = username
            }).ToBytes());
        }

        private void Client_ServerDisconnected()
        {
            Logger.Debug("Server disconnected!");
        }

        private void Client_PacketRecieved(Packet packet)
        {
            if (packet.Type == PacketType.TournamentState)
            {
                State = packet.SpecificPacket as TournamentState;
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.eventType)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinatorToUI(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinatorFromUI(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        AddMatchToUI(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatchFromUI(@event.changedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayerToUI(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayerFromUI(@event.changedObject as Player);
                        break;
                    case Event.EventType.SetSelf:
                        Self = @event.changedObject as MatchCoordinator;
                        break;
                    default:
                        Logger.Error($"Unknown command recieved!");
                        break;
                }
            }
        }

        private void Send(Packet packet) => client.Send(packet.ToBytes());

        public void AddPlayer(Player player)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.PlayerAdded;
            @event.changedObject = player;
            Send(new Packet(@event));
        }

        private void AddPlayerToUI(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.Add(player);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void RemovePlayer(Player player)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.PlayerLeft;
            @event.changedObject = player;
            Send(new Packet(@event));
        }

        private void RemovePlayerFromUI(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Guid == player.Guid);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void AddCoordinator(MatchCoordinator coordinator)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorAdded;
            @event.changedObject = coordinator;
            Send(new Packet(@event));
        }

        private void AddCoordinatorToUI(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void RemoveCoordinator(MatchCoordinator coordinator)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorLeft;
            @event.changedObject = coordinator;
            Send(new Packet(@event));
        }

        private void RemoveCoordinatorFromUI(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Guid == coordinator.Guid);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void CreateMatch(Match match)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.MatchCreated;
            @event.changedObject = match;
            Send(new Packet(@event));
        }

        private void AddMatchToUI(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.Add(match);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void DeleteMatch(Match match)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.MatchDeleted;
            @event.changedObject = match;
            Send(new Packet(@event));
        }

        private void DeleteMatchFromUI(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));
        }
    }
}
