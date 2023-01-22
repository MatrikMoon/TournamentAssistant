using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantShared
{
    public class SystemClient
    {
        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;

        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        public event Func<Response.Connect, Task> ConnectedToServer;
        public event Func<Response.Connect, Task> FailedToConnectToServer;
        public event Func<Task> ServerDisconnected;

        public event Func<Response.ImagePreloaded, Guid, Task> ImagePreloaded;
        public event Func<Command.ShowModal, Task> ShowModal;
        public event Func<Push.SongFinished, Task> PlayerFinishedSong;
        public event Func<RealtimeScore, Task> RealtimeScoreReceived;

        //Tournament State in the client *should* only be modified by the server connection thread, so thread-safety shouldn't be an issue here
        public State State { get; set; }

        public User Self { get; set; }

        protected Client client;

        public bool Connected => client?.Connected ?? false;

        private Timer heartbeatTimer = new();
        private bool shouldHeartbeat;
        private string endpoint;
        private int port;
        private string username;
        private string password;
        private string userId;
        private User.ClientTypes clientType;

        private List<string> modList;

        public SystemClient(string endpoint, int port, string username, User.ClientTypes clientType, string userId = "0", string password = null, List<string> modList = null)
        {
            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
            this.password = password;
            this.userId = userId;
            this.clientType = clientType;
            this.modList = modList ?? new List<string>();
        }

        //Blocks until connected (or failed), then returns
        public async Task Start()
        {
            shouldHeartbeat = true;
            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            await ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            //Send needs to be awaited so it will properly catch exceptions, but we can't make this timer callback async. So, we do this.
            async Task timerAction()
            {
                try
                {
                    await Send(new Packet
                    {
                        Command = new Command
                        {
                            Heartbeat = true
                        }
                    });
                }
                catch (Exception e)
                {
                    Logger.Debug("HEARTBEAT FAILED");
                    Logger.Debug(e.ToString());

                    await ConnectToServer();
                }
            }
            Task.Run(timerAction);
        }

        private async Task ConnectToServer()
        {
            //Don't heartbeat while connecting
            heartbeatTimer.Stop();

            try
            {
                State = new State();

                client = new Client(endpoint, port);
                client.PacketReceived += Client_PacketWrapperReceived;
                client.ServerConnected += Client_ServerConnected;
                client.ServerFailedToConnect += Client_ServerFailedToConnect;
                client.ServerDisconnected += Client_ServerDisconnected;

                await client.Start();
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
        }

        private async Task Client_ServerConnected()
        {
            //Resume heartbeat when connected
            if (shouldHeartbeat) heartbeatTimer.Start();

            /*Self = new User
            {
                Name = username,
                ClientType = clientType,
                UserId = userId
            };
            Self.ModLists.AddRange(modList);*/

            await Send(new Packet
            {
                Request = new Request
                {
                    info = new Request.Info
                    {
                        ClientVersion = Constants.VERSION_CODE
                    }
                }
            });
        }

        private async Task Client_ServerFailedToConnect()
        {
            //Resume heartbeat if we fail to connect
            //Basically the same as just doing another connect here...
            //But with some extra delay. I don't really know why
            //I'm doing it this way
            if (shouldHeartbeat) heartbeatTimer.Start();

            if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(null);
        }

        private async Task Client_ServerDisconnected()
        {
            Logger.Debug("SystemClient: Server disconnected!");
            if (ServerDisconnected != null) await ServerDisconnected.Invoke();
        }

        public void Shutdown()
        {
            client?.Shutdown();
            heartbeatTimer.Stop();

            //If the client was connecting when we shut it down, the FailedToConnect event might resurrect the heartbeat without this
            shouldHeartbeat = false;
        }

        public Task SendAndGetResponse(Packet requestPacket, Func<PacketWrapper, Task> onRecieved, Func<Task> onTimeout = null, int timeout = 5000)
        {
            return client.SendAndGetResponse(new PacketWrapper(requestPacket), onRecieved, onTimeout, timeout);
        }

        public Task Send(Guid id, Packet packet) => Send(new[] { id }, packet);

        public Task Send(Guid[] ids, Packet packet)
        {
            packet.From = Self?.Guid ?? Guid.Empty.ToString();
            var forwardedPacket = new ForwardingPacket
            {
                Packet = packet
            };
            forwardedPacket.ForwardToes.AddRange(ids.Select(x => x.ToString()));

            return Forward(forwardedPacket);
        }

        public Task Send(Packet packet)
        {
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            packet.From = Self?.Guid ?? Guid.Empty.ToString();
            return client.Send(new PacketWrapper(packet));
        }

        public Tournament GetTournamentByGuid(string guid)
        {
            return State.Tournaments.FirstOrDefault(x => x.Guid == guid);
        }

        public User GetUserByGuid(string tournamentGuid, string userGuid)
        {
            return GetTournamentByGuid(tournamentGuid).Users.FirstOrDefault(x => x.Guid == userGuid);
        }

        public Match GetMatchByGuid(string tournamentGuid, string matchGuid)
        {
            return GetTournamentByGuid(tournamentGuid).Matches.First(x => x.Guid == matchGuid);
        }

        private Task Forward(ForwardingPacket forwardingPacket)
        {
            var packet = forwardingPacket.Packet;
            Logger.Debug($"Forwarding data: {LogPacket(packet)}");

            packet.From = Self?.Guid ?? Guid.Empty.ToString();
            return Send(new Packet
            {
                ForwardingPacket = forwardingPacket
            });
        }

        static string LogPacket(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.play_song)
                {
                    var playSong = command.play_song;
                    secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " +
                                    playSong.GameplayParameters.Beatmap.Difficulty;
                }
                else if (command.TypeCase == Command.TypeOneofCase.load_song)
                {
                    var loadSong = command.load_song;
                    secondaryInfo = loadSong.LevelId;
                }
                else
                {
                    secondaryInfo = command.TypeCase.ToString();
                }
            }

            if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;

                secondaryInfo = @event.ChangedObjectCase.ToString();
                if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.user_updated)
                {
                    var user = @event.user_updated.User;
                    secondaryInfo =
                        $"{secondaryInfo} from ({user.Name} : {user.DownloadState}) : ({user.PlayState} : {user.StreamDelayMs})";
                }
                else if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.match_updated)
                {
                    var match = @event.match_updated.Match;
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }

            return $"({packet.packetCase}) ({secondaryInfo})";
        }

        #region EVENTS/ACTIONS
        public async Task AddUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_added = new Event.UserAdded
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private async Task AddUserReceived(string tournamentGuid, User user)
        {
            GetTournamentByGuid(tournamentGuid).Users.Add(user);

            if (UserConnected != null) await UserConnected.Invoke(user);
        }

        public async Task UpdateUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_updated = new Event.UserUpdated
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateUserReceived(string tournamentGuid, User user)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var userToReplace = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
            tournament.Users.Remove(userToReplace);
            tournament.Users.Add(user);

            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self
            if (Self.Guid == user.Guid) Self = user;

            if (UserInfoUpdated != null) await UserInfoUpdated.Invoke(user);
        }

        public async Task RemoveUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_left = new Event.UserLeft
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private async Task RemoveUserReceived(string tournamentGuid, User user)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var userToRemove = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
            tournament.Users.Remove(userToRemove);

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        public async Task CreateMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_created = new Event.MatchCreated
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private async Task AddMatchReceived(string tournamentGuid, Match match)
        {
            GetTournamentByGuid(tournamentGuid).Matches.Add(match);

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_updated = new Event.MatchUpdated
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateMatchReceived(string tournamentGuid, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var matchToReplace = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
            if (matchToReplace == null)
            {
                return;
            }
            tournament.Matches.Remove(matchToReplace);
            tournament.Matches.Add(match);

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_deleted = new Event.MatchDeleted
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private async Task DeleteMatchReceived(string tournamentGuid, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var matchToRemove = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
            tournament.Matches.Remove(matchToRemove);

            if (MatchDeleted != null) await MatchDeleted?.Invoke(match);
        }

        private void AddQualifierEventReceived(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            GetTournamentByGuid(tournamentGuid).Qualifiers.Add(qualifierEvent);
        }

        public async Task UpdateQualifierEvent(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_updated = new Event.QualifierUpdated
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        public void UpdateQualifierEventReceived(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var eventToReplace = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToReplace);
            tournament.Qualifiers.Add(qualifierEvent);
        }

        public async Task DeleteQualifierEvent(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private void DeleteQualifierEventReceived(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToRemove);
        }

        private void AddTournamentReceived(Tournament qualifierEvent)
        {
            GetTournamentByGuid(tournamentGuid).Qualifiers.Add(qualifierEvent);
        }

        public async Task UpdateTournament(Tournament qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_updated = new Event.QualifierUpdated
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        public void UpdateTournamentReceived(Tournament qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var eventToReplace = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToReplace);
            tournament.Qualifiers.Add(qualifierEvent);
        }

        public async Task DeleteTournament(Tournament qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };
            await Send(new Packet
            {
                Event = @event
            });
        }

        private void DeleteTournamentReceived(Tournament qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToRemove);
        }

        #endregion EVENTS/ACTIONS

        protected virtual async Task Client_PacketWrapperReceived(PacketWrapper packet)
        {
            await Client_PacketReceived(packet.Payload);
        }

        protected virtual async Task Client_PacketReceived(Packet packet)
        {
            Logger.Debug($"Received data: {LogPacket(packet)}");

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            /*if (packet.packetCase == Packet.packetOneofCase.Acknowledgement)
            {
                var acknowledgement = packet.Acknowledgement;
            }*/

            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.show_modal)
                {
                    if (ShowModal != null) await ShowModal.Invoke(command.show_modal);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Push)
            {
                var push = packet.Push;
                if (push.DataCase == Push.DataOneofCase.song_finished)
                {
                    if (PlayerFinishedSong != null) await PlayerFinishedSong.Invoke(push.song_finished);
                }
                else if (push.DataCase == Push.DataOneofCase.RealtimeScore)
                {
                    if (RealtimeScoreReceived != null) await RealtimeScoreReceived.Invoke(push.RealtimeScore);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Response)
            {
                var response = packet.Response;
                if (response.DetailsCase == Response.DetailsOneofCase.connect)
                {
                    var connectResponse = response.connect;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        Self.Guid = connectResponse.SelfGuid;
                        State = connectResponse.State;
                        if (ConnectedToServer != null) await ConnectedToServer.Invoke(connectResponse);
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(connectResponse);
                    }
                }
                else if (response.DetailsCase == Response.DetailsOneofCase.image_preloaded)
                {
                    var imagePreloaded = response.image_preloaded;
                    if (ImagePreloaded != null) await ImagePreloaded.Invoke(imagePreloaded, Guid.Parse(packet.From));
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;
                switch (@event.ChangedObjectCase)
                {
                    case Event.ChangedObjectOneofCase.match_created:
                        await AddMatchReceived(@event.match_created.TournamentGuid, @event.match_created.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_updated:
                        await UpdateMatchReceived(@event.match_updated.TournamentGuid, @event.match_updated.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_deleted:
                        await DeleteMatchReceived(@event.match_deleted.TournamentGuid, @event.match_deleted.Match);
                        break;
                    case Event.ChangedObjectOneofCase.user_added:
                        await AddUserReceived(@event.user_added.TournamentGuid, @event.user_added.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_updated:
                        await UpdateUserReceived(@event.user_updated.TournamentGuid, @event.user_updated.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_left:
                        await RemoveUserReceived(@event.user_left.TournamentGuid, @event.user_left.User);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_created:
                        AddQualifierEventReceived(@event.qualifier_created.TournamentGuid, @event.qualifier_created.Event);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_updated:
                        UpdateQualifierEventReceived(@event.qualifier_updated.TournamentGuid, @event.qualifier_updated.Event);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_deleted:
                        DeleteQualifierEventReceived(@event.qualifier_deleted.TournamentGuid, @event.qualifier_deleted.Event);
                        break;
                    case Event.ChangedObjectOneofCase.server_added:
                        break;
                    case Event.ChangedObjectOneofCase.server_deleted:
                        break;
                    default:
                        Logger.Error("Unknown command received!");
                        break;
                }
            }
        }
    }
}