using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantServer
{
    public class StateManager
    {
        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;
        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        // Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
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

            // Translate Tournaments from database to model format
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

        public Tournament GetTournament(string guid)
        {
            lock (State.Tournaments)
            {
                return State.Tournaments.FirstOrDefault(x => x.Guid == guid);
            }
        }

        public List<User> GetUsers(string tournamentId)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Users)
            {
                return tournament.Users.ToList();
            }
        }

        public User GetUser(string tournamentId, string guid)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Users)
            {
                return tournament.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches(string tournamentId)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Matches)
            {
                return tournament.Matches.ToList();
            }
        }

        public Match GetMatch(string tournamentId, string matchId)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Matches)
            {
                return tournament.Matches.FirstOrDefault(x => x.Guid == matchId);
            }
        }

        public List<QualifierEvent> GetQualifiers(string tournamentId)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Qualifiers)
            {
                return tournament.Qualifiers.ToList();
            }
        }

        public QualifierEvent GetQualifier(string tournamentId, string qualifierId)
        {
            var tournament = GetTournament(tournamentId);
            lock (tournament.Qualifiers)
            {
                return tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierId);
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

        public async Task AddUser(string tournamentId, User user, string[] modList = null)
        {
            var tournament = GetTournament(tournamentId);

            // Normally we would assign a random GUID here, but for users we're
            // using the same GUID that's used in the lower level socket classes.
            // TL;DR: Don't touch it

            // This is combined with the user state when they join a tourney
            if (modList != null)
            {
                user.ModLists.AddRange(modList);
            }

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
            var tournament = GetTournament(tournamentId);
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
            var tournament = GetTournament(tournamentId);
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

            // Remove disconnected user from any matches they're in
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
                    // await DeleteMatch(tournamentId, match.Guid);
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
                        await DeleteMatch(tournamentId, match.Guid);
                    }
                }
            }

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        public async Task<Match> CreateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournament(tournamentId);

            // Assign a random GUID here, since it should not be the client's responsibility
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

            return match;
        }

        public async Task UpdateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournament(tournamentId);
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

        public async Task<Match> DeleteMatch(string tournamentId, string matchId)
        {
            Match removedMatch;
            var tournament = GetTournament(tournamentId);
            lock (tournament.Matches)
            {
                removedMatch = tournament.Matches.FirstOrDefault(x => x.Guid == matchId);
                tournament.Matches.Remove(removedMatch);
            }

            var @event = new Event
            {
                match_deleted = new Event.MatchDeleted
                {
                    TournamentId = tournamentId,
                    Match = removedMatch
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (MatchDeleted != null) await MatchDeleted.Invoke(removedMatch);

            return removedMatch;
        }

        public async Task<QualifierEvent> CreateQualifier(string tournamentId, QualifierEvent qualifierEvent)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var tournament = GetTournament(tournamentId);

            // Assign a random GUID here, since it should not be the client's responsibility
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

            return qualifierEvent;
        }

        public async Task UpdateQualifier(string tournamentId, QualifierEvent qualifierEvent)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            var tournament = GetTournament(tournamentId);

            // Update Event entry
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

        public async Task<QualifierEvent> DeleteQualifier(string tournamentId, string qualifierId)
        {
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            QualifierEvent deletedQualifier;
            var tournament = GetTournament(tournamentId);

            // Mark all songs and scores as old
            qualifierDatabase.DeleteFromDatabase(qualifierId);

            lock (tournament.Qualifiers)
            {
                deletedQualifier = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierId);
                tournament.Qualifiers.Remove(deletedQualifier);
            }

            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentId = tournamentId,
                    Event = deletedQualifier
                }
            };

            await Server.BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            return deletedQualifier;
        }

        public async Task<Tournament> CreateTournament(Tournament tournament, User user)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            // Assign a random GUID here, since it should not be the client's responsibility
            tournament.Guid = Guid.NewGuid().ToString();

            tournamentDatabase.SaveNewModelToDatabase(tournament);

            var viewOnlyRole = Constants.DefaultRoles.GetViewOnly(tournament.Guid);
            var coordinatorRole = Constants.DefaultRoles.GetCoordinator(tournament.Guid);
            var playerRole = Constants.DefaultRoles.GetPlayer(tournament.Guid);
            var adminRole = Constants.DefaultRoles.GetAdmin(tournament.Guid);

            tournamentDatabase.AddRole(tournament, viewOnlyRole);
            tournamentDatabase.AddRole(tournament, coordinatorRole);
            tournamentDatabase.AddRole(tournament, playerRole);
            tournamentDatabase.AddRole(tournament, adminRole);

            tournamentDatabase.AddAuthorizedUser(tournament.Guid, user.discord_info.UserId, new string[] { adminRole.RoleId });

            lock (State.Tournaments)
            {
                tournament.Settings.Roles.Add(viewOnlyRole);
                tournament.Settings.Roles.Add(coordinatorRole);
                tournament.Settings.Roles.Add(playerRole);
                tournament.Settings.Roles.Add(adminRole);
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

        public async Task UpdateTournamentSettings(Tournament tournament)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.UpdateTournamentSettings(tournament);

            await UpdateTournamentState(tournament);
        }

        public async Task AddTournamentTeam(Tournament tournament, Tournament.TournamentSettings.Team team)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.AddTeam(tournament, team);

            await UpdateTournamentState(tournament);
        }

        public async Task UpdateTournamentTeam(Tournament tournament, Tournament.TournamentSettings.Team team)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.UpdateTeam(tournament, team);

            await UpdateTournamentState(tournament);
        }

        public async Task RemoveTournamentTeam(Tournament tournament, Tournament.TournamentSettings.Team team)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.RemoveTeam(tournament, team);

            await UpdateTournamentState(tournament);
        }

        public async Task AddTournamentRole(Tournament tournament, Role team)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.AddRole(tournament, team);

            await UpdateTournamentState(tournament);
        }

        public async Task UpdateTournamentRole(Tournament tournament, Role role)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.UpdateRole(tournament, role);

            await UpdateTournamentState(tournament);
        }

        public async Task RemoveTournamentRole(Tournament tournament, Role role)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.RemoveRole(tournament, role);

            await UpdateTournamentState(tournament);
        }

        public async Task AddTournamentPool(Tournament tournament, Tournament.TournamentSettings.Pool pool)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.AddPool(tournament, pool);
            tournamentDatabase.AddPoolSongs(pool, pool.Maps);

            await UpdateTournamentState(tournament);
        }

        public async Task UpdateTournamentPool(Tournament tournament, Tournament.TournamentSettings.Pool pool)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.UpdatePool(tournament, pool);

            await UpdateTournamentState(tournament);
        }

        public async Task RemoveTournamentPool(Tournament tournament, Tournament.TournamentSettings.Pool pool)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.RemovePool(tournament, pool);

            await UpdateTournamentState(tournament);
        }

        public async Task AddTournamentPoolSongs(Tournament tournament, Tournament.TournamentSettings.Pool pool, List<Map> maps)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.AddPoolSongs(pool, maps);

            await UpdateTournamentState(tournament);
        }

        public async Task UpdateTournamentPoolSong(Tournament tournament, Tournament.TournamentSettings.Pool pool, Map map)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.UpdatePoolSong(pool, map);

            await UpdateTournamentState(tournament);
        }

        public async Task RemoveTournamentPoolSong(Tournament tournament, Map map)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            tournamentDatabase.RemovePoolSong(map);

            await UpdateTournamentState(tournament);
        }

        private async Task UpdateTournamentState(Tournament tournament)
        {
            // Update Event entry
            lock (State.Tournaments)
            {
                var index = State.Tournaments.FindIndex(x => x.Guid == tournament.Guid);
                if (index == -1)
                {
                    return;
                }

                State.Tournaments[index] = tournament;
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

        public async Task<Tournament> DeleteTournament(string tournamentId)
        {
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            using var qualifierDatabase = DatabaseService.NewQualifierDatabaseContext();

            Tournament removedTournament;
            tournamentDatabase.DeleteFromDatabase(tournamentId);

            lock (State.Tournaments)
            {
                removedTournament = State.Tournaments.FirstOrDefault(x => x.Guid == tournamentId);
                State.Tournaments.Remove(removedTournament);
            }

            // Delete the tournament's qualifiers too
            foreach (var qualifier in removedTournament.Qualifiers)
            {
                qualifierDatabase.DeleteFromDatabase(qualifier.Guid);
            }

            var @event = new Event
            {
                tournament_deleted = new Event.TournamentDeleted
                {
                    Tournament = removedTournament,
                }
            };

            await Server.BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            return removedTournament;
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
