using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI
{
    class Client : IConnection, INotifyPropertyChanged
    {
        public event Action<Player> PlayerInfoUpdated;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Match> MatchDeleted;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State in the client can ONLY be modified by the server connection thread, so thread-safety shouldn't be an issue here
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
                Send(new Packet(command));
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
            try
            {
                State = new TournamentState();
                State.Players = new Player[0];
                State.Coordinators = new MatchCoordinator[0];
                State.Matches = new Match[0];

                client = new Network.Client(endpoint, 10156);
                client.PacketRecieved += Client_PacketRecieved;
                client.ServerDisconnected += Client_ServerDisconnected;

                client.Start();

                Send(new Packet(new Connect()
                {
                    clientType = Connect.ConnectType.Coordinator,
                    name = username
                }));
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
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
                    case Event.EventType.MatchUpdated:
                        UpdateMatchInUI(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatchFromUI(@event.changedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayerToUI(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        UpdatePlayerInUI(@event.changedObject as Player);
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

        public void Send(string guid, Packet packet) => Send(new string[] { guid }, packet);

        public void Send(string[] guids, Packet packet)
        {
            var forwardedPacket = new ForwardedPacket();
            forwardedPacket.ForwardTo = guids;
            forwardedPacket.Type = packet.Type;
            forwardedPacket.SpecificPacket = packet.SpecificPacket;

            Send(new Packet(forwardedPacket));
        }

        private void Send(Packet packet)
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
            client.Send(packet.ToBytes());
        }

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

        public void UpdatePlayer(Player player)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.PlayerUpdated;
            @event.changedObject = player;
            Send(new Packet(@event));
        }

        public void UpdatePlayerInUI(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Guid == player.Guid)] = player;
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            PlayerInfoUpdated?.Invoke(player);
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

        public void UpdateMatch(Match match)
        {
            var @event = new Event();
            @event.eventType = Event.EventType.MatchUpdated;
            @event.changedObject = match;
            Send(new Packet(@event));
        }

        public void UpdateMatchInUI(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            MatchInfoUpdated?.Invoke(match);
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

            MatchDeleted?.Invoke(match);
        }
    }
}
