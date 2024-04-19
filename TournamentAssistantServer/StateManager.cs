using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantServer
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
        private TAServer Server { get; set; }
        private DatabaseService DatabaseService { get; set; }

        public StateManager(TAServer server, DatabaseService databaseService)
        {
            State = new State();

            Server = server;
            DatabaseService = databaseService;
        }

        public async Task LoadSavedTournaments()
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            //Translate Tournaments from database to model format
            foreach (var tournament in tournamentDatabase.Tournaments.Where(x => !x.Old))
            {
                var tournamentModel = await tournamentDatabase.LoadModelFromDatabase(tournament);
                var qualifierModels = await qualifierDatabase.LoadModelsFromDatabase(tournamentModel);

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

        public List<User> GetUsers(string tournamentId)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                return tournament.Users.ToList();
            }
        }

        public User GetUserById(string tournamentId, string guid)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                return tournament.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches(string tournamentId)
        {
            var tournament = GetTournamentByGuid(tournamentId);
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
                    TournamentId = tournamentId,
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
                    TournamentId = tournamentId,
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
                    TournamentId = tournamentId,
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
                // TODO: Moon you're gonna fall for this one later.
                // You don't *really* want a match to be deleted when
                // the coordinator leaves, but for now it's a nice QOL
                // feature. In the future, consider changing the frontend
                // to handle the case where the leader's guid returns no
                // results after a user lookup. Perhaps, "Match with no
                // coordinator," or something better.
                if (match.Leader == user.Guid)
                {
                    await DeleteMatch(tournamentId, match);
                }
                else
                {
                    var remainingUsers = -1;

                    lock (match.AssociatedUsers)
                    {
                        if (match.AssociatedUsers.Contains(user.Guid))
                        {
                            match.AssociatedUsers.RemoveAll(x => x == user.Guid);

                            remainingUsers = match.AssociatedUsers.Count;
                        }
                    }

                    if (remainingUsers > 0)
                    {
                        await UpdateMatch(tournamentId, match);
                    }
                    else if (remainingUsers == 0)
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
                    TournamentId = tournamentId,
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
                    TournamentId = tournamentId,
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
                    TournamentId = tournamentId,
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
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var tournament = GetTournamentByGuid(tournamentId);

            //Assign a random GUID here, since it should not be the client's responsibility
            qualifierEvent.Guid = Guid.NewGuid().ToString();

            qualifierDatabase.SaveModelToDatabase(tournamentId, qualifierEvent);

            lock (tournament.Qualifiers)
            {
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_created = new Event.QualifierCreated
                {
                    TournamentId = tournamentId,
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
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var tournament = GetTournamentByGuid(tournamentId);

            //Update Event entry
            qualifierDatabase.SaveModelToDatabase(tournamentId, qualifierEvent);

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
                    TournamentId = tournamentId,
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
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var tournament = GetTournamentByGuid(tournamentId);

            //Mark all songs and scores as old
            qualifierDatabase.DeleteFromDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                tournament.Qualifiers.Remove(eventToRemove);
            }

            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentId = tournamentId,
                    Event = qualifierEvent
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });
        }

        public async Task<Tournament> CreateTournament(Tournament tournament)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            //Assign a random GUID here, since it should not be the client's responsibility
            tournament.Guid = Guid.NewGuid().ToString();

            tournamentDatabase.SaveModelToDatabase(tournament);

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

            return tournament;
        }

        public async Task UpdateTournament(Tournament tournament)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            //Update Event entry
            tournamentDatabase.SaveModelToDatabase(tournament);

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
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            tournamentDatabase.DeleteFromDatabase(tournament);

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
