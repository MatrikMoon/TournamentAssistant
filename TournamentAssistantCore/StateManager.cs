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

        private SystemServer Server { get; set; }

        public StateManager(SystemServer server)
        {
            Server = server;
        }

        public List<User> GetUsers()
        {
            lock (Server.State.Users)
            {
                return Server.State.Users.ToList();
            }
        }

        public User GetUserById(string guid)
        {
            lock (Server.State.Users)
            {
                return Server.State.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches()
        {
            lock (Server.State.Matches)
            {
                return Server.State.Matches.ToList();
            }
        }

        public List<CoreServer> GetServers()
        {
            lock (Server.State.KnownHosts)
            {
                return Server.State.KnownHosts.ToList();
            }
        }

        #region EventManagement
        public async Task AddUser(User user)
        {
            lock (Server.State.Users)
            {
                Server.State.Users.Add(user);
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
            lock (Server.State.Users)
            {
                var userToReplace = Server.State.Users.FirstOrDefault(x => x.UserEquals(user));
                Server.State.Users.Remove(userToReplace);
                Server.State.Users.Add(user);
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
            lock (Server.State.Users)
            {
                userToRemove = Server.State.Users.FirstOrDefault(x => x.UserEquals(user));
                if (userToRemove == null)
                {
                    return;
                }
                Server.State.Users.Remove(userToRemove);
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

            for (int i = 0; i < Server.State.Matches.Count; i++)
            {
                var m = Server.State.Matches[i];
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
            lock (Server.State.Matches)
            {
                Server.State.Matches.Add(match);
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
            lock (Server.State.Matches)
            {
                var matchToReplace = Server.State.Matches.FirstOrDefault(x => x.MatchEquals(match));
                Server.State.Matches.Remove(matchToReplace);
                Server.State.Matches.Add(match);
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
            lock (Server.State.Matches)
            {
                var matchToRemove = Server.State.Matches.FirstOrDefault(x => x.MatchEquals(match));
                Server.State.Matches.Remove(matchToRemove);
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