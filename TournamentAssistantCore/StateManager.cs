using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantCore
{
    internal class StateManager
    {
        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;
        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
        private State State { get; set; }
        private SystemServer Server { get; set; }

        public StateManager(SystemServer server)
        {
            State = new State();

            Server = server;
        }

        public List<User> GetUsers()
        {
            lock (State.Users)
            {
                return State.Users.ToList();
            }
        }

        public User GetUserById(string guid)
        {
            lock (State.Users)
            {
                return State.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches()
        {
            lock (State.Matches)
            {
                return State.Matches.ToList();
            }
        }

        public List<CoreServer> GetServers()
        {
            lock (State.KnownHosts)
            {
                return State.KnownHosts.ToList();
            }
        }

        #region EventManagement
        public async Task AddUser(User user)
        {
            lock (State.Users)
            {
                State.Users.Add(user);
            }

            var @event = new Event
            {
                user_added_event = new Event.UserAddedEvent
                {
                    User = user
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            if (UserConnected != null) await UserConnected.Invoke(user);
        }

        public async Task UpdateUser(User user)
        {
            lock (State.Users)
            {
                var userToReplace = State.Users.FirstOrDefault(x => x.UserEquals(user));
                State.Users.Remove(userToReplace);
                State.Users.Add(user);
            }

            var @event = new Event
            {
                user_updated_event = new Event.UserUpdatedEvent
                {
                    User = user
                }
            };
            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            if (UserInfoUpdated != null) await UserInfoUpdated.Invoke(user);
        }

        public async Task RemoveUser(User user)
        {
            User userToRemove;
            lock (State.Users)
            {
                userToRemove = State.Users.FirstOrDefault(x => x.UserEquals(user));
                if (userToRemove == null)
                {
                    return;
                }
                State.Users.Remove(userToRemove);
            }

            var @event = new Event
            {
                user_left_event = new Event.UserLeftEvent
                {
                    User = user
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            for (int i = 0; i < State.Matches.Count; i++)
            {
                var m = State.Matches[i];
                if (m.AssociatedUsers.Contains(userToRemove.Guid))
                {
                    m.AssociatedUsers.RemoveAll((x) => x == userToRemove.Guid);
                    await UpdateMatch(m);
                }
            }

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        public async Task CreateMatch(Match match)
        {
            lock (State.Matches)
            {
                State.Matches.Add(match);
            }

            var @event = new Event
            {
                match_created_event = new Event.MatchCreatedEvent
                {
                    Match = match
                }
            };
            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(Match match)
        {
            lock (State.Matches)
            {
                var matchToReplace = State.Matches.FirstOrDefault(x => x.MatchEquals(match));
                State.Matches.Remove(matchToReplace);
                State.Matches.Add(match);
            }

            var @event = new Event
            {
                match_updated_event = new Event.MatchUpdatedEvent
                {
                    Match = match
                }
            };

            var updatePacket = new Packet
            {
                Event = @event
            };

            await Server.BroadcastToAllClients(updatePacket);

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(Match match)
        {
            lock (State)
            {
                var matchToRemove = State.Matches.FirstOrDefault(x => x.MatchEquals(match));
                State.Matches.Remove(matchToRemove);
            }

            var @event = new Event
            {
                match_deleted_event = new Event.MatchDeletedEvent
                {
                    Match = match
                }
            };
            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            if (MatchDeleted != null) await MatchDeleted.Invoke(match);
        }
        #endregion EventManagement
    }
}