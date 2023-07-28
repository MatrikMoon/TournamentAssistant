using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantServer.Helpers;

namespace TournamentAssistantServer
{
    internal class ServerStateManager
    {
        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;
        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
        private State State { get; set; }
        private TAServer Server { get; set; }
        private DatabaseService DatabaseService { get; set; }

        public ServerStateManager(TAServer server, DatabaseService databaseService)
        {
            Server = server;
            DatabaseService = databaseService;
        }

        public async Task LoadSavedTournaments()
        {
            //Translate Tournaments from database to model format
            foreach (var tournament in DatabaseService.TournamentDatabase.Tournaments.Where(x => !x.Old))
            {
                var tournamentModel = await DatabaseService.TournamentDatabase.LoadModelFromDatabase(tournament);
                var qualifierModels = await DatabaseService.QualifierDatabase.LoadModelsFromDatabase(tournamentModel);
                
                tournamentModel.Qualifiers.AddRange(qualifierModels);

                lock (State)
                {
                    State.Tournaments.Add(tournamentModel);
                }
            }
        }

        public List<Tournament> GetTournaments()
        {
            lock (State.Tournaments)
            {
                return State.Tournaments.ToList();
            }
        }

        public Tournament GetTournamentByGuid(string guid)
        {
            lock (State.Tournaments)
            {
                return State.Tournaments.FirstOrDefault(x => x.Guid == guid);
            }
        }

        public List<User> GetUsers(string tournamentGuid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Users)
            {
                return tournament.Users.ToList();
            }
        }

        public User GetUserById(string tournamentGuid, string guid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Users)
            {
                return tournament.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches(string tournamentGuid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Matches)
            {
                return tournament.Matches.ToList();
            }
        }

        public List<CoreServer> GetServers()
        {
            lock (State.KnownServers)
            {
                return State.KnownServers.ToList();
            }
        }

        #region EventManagement

        public async Task AddUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Normally we would assign a random GUID here, but for users we're
            //using the same GUID that's used in the lower level socket classes.
            //TL;DR: Don't touch it

            lock (tournament.Users)
            {
                tournament.Users.Add(user);
            }

            var @event = new Event
            {
                user_added = new Event.UserAdded
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (UserConnected != null) await UserConnected.Invoke(user);
        }

        public async Task UpdateUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                var userToReplace = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
                tournament.Users.Remove(userToReplace);
                tournament.Users.Add(user);
            }

            var @event = new Event
            {
                user_updated = new Event.UserUpdated
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (UserInfoUpdated != null) await UserInfoUpdated.Invoke(user);
        }

        public async Task RemoveUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                tournament.Users.RemoveAll(x => x.Guid == user.Guid);
            }

            var @event = new Event
            {
                user_left = new Event.UserLeft
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            //Remove disconnected user from any matches they're in
            foreach (var match in GetMatches(tournamentId))
            {
                if (match.AssociatedUsers.Contains(user.Guid))
                {
                    match.AssociatedUsers.RemoveAll(x => x == user.Guid);

                    if (match.AssociatedUsers.Count > 0)
                    {
                        await UpdateMatch(tournamentId, match);
                    }
                    else
                    {
                        await DeleteMatch(tournamentId, match);
                    }
                }
            }

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        public async Task CreateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Assign a random GUID here, since it should not be the client's responsibility
            match.Guid = Guid.NewGuid().ToString();

            lock (tournament.Matches)
            {
                tournament.Matches.Add(match);
            }

            var @event = new Event
            {
                match_created = new Event.MatchCreated
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Matches)
            {
                var matchToReplace = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
                tournament.Matches.Remove(matchToReplace);
                tournament.Matches.Add(match);
            }

            var @event = new Event
            {
                match_updated = new Event.MatchUpdated
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            var updatePacket = new Packet
            {
                Event = @event
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), updatePacket);

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Matches)
            {
                var matchToRemove = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
                tournament.Matches.Remove(matchToRemove);
            }

            var @event = new Event
            {
                match_deleted = new Event.MatchDeleted
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (MatchDeleted != null) await MatchDeleted.Invoke(match);
        }

        public async Task CreateQualifier(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Assign a random GUID here, since it should not be the client's responsibility
            qualifierEvent.Guid = Guid.NewGuid().ToString();

            await DatabaseService.QualifierDatabase.SaveModelToDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_created = new Event.QualifierCreated
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateQualifier(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Update Event entry
            await DatabaseService.QualifierDatabase.SaveModelToDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                var eventToReplace = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                tournament.Qualifiers.Remove(eventToReplace);
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_updated = new Event.QualifierUpdated
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });
        }

        public async Task DeleteQualifier(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Mark all songs and scores as old
            await DatabaseService.QualifierDatabase.DeleteFromDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                tournament.Qualifiers.Remove(eventToRemove);
            }

            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });
        }

        public async Task CreateTournament(Tournament tournament)
        {
            //Assign a random GUID here, since it should not be the client's responsibility
            tournament.Guid = Guid.NewGuid().ToString();

            await DatabaseService.TournamentDatabase.SaveModelToDatabase(tournament);

            lock (State.Tournaments)
            {
                State.Tournaments.Add(tournament);
            }

            var @event = new Event
            {
                tournament_created = new Event.TournamentCreated
                {
                    Tournament = tournament
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateTournament(Tournament tournament)
        {
            //Update Event entry
            await DatabaseService.TournamentDatabase.SaveModelToDatabase(tournament);

            lock (State.Tournaments)
            {
                var tournamentToReplace = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
                State.Tournaments.Remove(tournamentToReplace);
                State.Tournaments.Add(tournament);
            }

            var @event = new Event
            {
                tournament_updated = new Event.TournamentUpdated
                {
                    Tournament = tournament
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task DeleteTournament(Tournament tournament)
        {
            await DatabaseService.TournamentDatabase.DeleteFromDatabase(tournament);

            lock (State.Tournaments)
            {
                var tournamentToRemove = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
                State.Tournaments.Remove(tournamentToRemove);
            }

            var @event = new Event
            {
                tournament_deleted = new Event.TournamentDeleted
                {
                    Tournament = tournament,
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task AddServer(CoreServer host)
        {
            lock (State)
            {
                var oldHosts = State.KnownServers.ToArray();
                State.KnownServers.Clear();
                State.KnownServers.AddRange(oldHosts.Union(new[] { host }, new CoreServerEqualityComparer()));
            }

            var @event = new Event
            {
                server_added = new Event.ServerAdded
                {
                    Server = host
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task RemoveServer(CoreServer host)
        {
            lock (State)
            {
                var hostToRemove = State.KnownServers.FirstOrDefault(x => x.CoreServerEquals(host));
                State.KnownServers.Remove(hostToRemove);
            }

            var @event = new Event
            {
                server_deleted = new Event.ServerDeleted
                {
                    Server = host
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        #endregion EventManagement
    }
}
