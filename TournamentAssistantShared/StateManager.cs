using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;
using Match = TournamentAssistantShared.Models.Match;

namespace TournamentAssistantShared
{
    public class StateManager
    {
        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;

        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;
        public event Func<Match, Task> MatchInfoUpdated;

        public event Func<Tournament, Task> TournamentCreated;
        public event Func<Tournament, Task> TournamentDeleted;
        public event Func<Tournament, Task> TournamentInfoUpdated;

        //Tournament State in the client *should* only be modified by the server connection thread, so thread-safety shouldn't be an issue here
        private State State { get; set; }
        private string SelfGuid { get; set; }
        private string LastConnectedtournamentId { get; set; }

        public StateManager()
        {
            SelfGuid = Guid.NewGuid().ToString(); //??? Is this used before being reassigned?
            State = new State();
        }

        // -- Packet handler -- //
        public async Task HandlePacket(Packet packet)
        {
            if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;
                switch (@event.ChangedObjectCase)
                {
                    case Event.ChangedObjectOneofCase.match_created:
                        await CreateMatchReceived(@event.match_created.TournamentGuid, @event.match_created.Match);
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
                        CreateQualifierEventReceived(@event.qualifier_created.TournamentGuid, @event.qualifier_created.Event);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_updated:
                        UpdateQualifierEventReceived(@event.qualifier_updated.TournamentGuid, @event.qualifier_updated.Event);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_deleted:
                        DeleteQualifierEventReceived(@event.qualifier_deleted.TournamentGuid, @event.qualifier_deleted.Event);
                        break;
                    case Event.ChangedObjectOneofCase.tournament_created:
                        await CreateTournamentReceived(@event.tournament_created.Tournament);
                        break;
                    case Event.ChangedObjectOneofCase.tournament_updated:
                        await UpdateTournamentReceived(@event.tournament_updated.Tournament);
                        break;
                    case Event.ChangedObjectOneofCase.tournament_deleted:
                        await DeleteTournamentReceived(@event.tournament_deleted.Tournament);
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
            else if (packet.packetCase == Packet.packetOneofCase.Response)
            {
                var response = packet.Response;
                if (response.DetailsCase == Response.DetailsOneofCase.connect)
                {
                    var connectResponse = response.connect;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        State = connectResponse.State;
                    }
                }
                else if (response.DetailsCase == Response.DetailsOneofCase.join)
                {
                    var joinResponse = response.join;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        SelfGuid = joinResponse.SelfGuid;
                        State = joinResponse.State;
                    }
                }
            }
        }

        // -- Helpers -- //
        public string GetSelfGuid()
        {
            return SelfGuid;
        }

        public Tournament GetTournament(string id)
        {
            return State.Tournaments.FirstOrDefault(x => x.Guid == id);
        }

        public List<User> GetUsers(string tournamentId)
        {
            return GetTournament(tournamentId).Users;
        }

        public User GetUser(string tournamentId, string userId)
        {
            return GetUsers(tournamentId).FirstOrDefault(x => x.Guid == userId);
        }

        public List<Match> GetMatches(string tournamentId)
        {
            return GetTournament(tournamentId).Matches;
        }

        public Match GetMatch(string tournamentId, string matchId)
        {
            return GetMatches(tournamentId).First(x => x.Guid == matchId);
        }

        public List<Team> GetTeams(string tournamentId)
        {
            return GetTournament(tournamentId).Settings.Teams;
        }

        public Team GetTeam(string tournamentId, string id)
        {
            return GetTeams(tournamentId).First(x => x.Guid == id);
        }

        public List<CoreServer> GetKnownServers()
        {
            return State.KnownServers;
        }

        // -- Event handlers -- //
        private async Task AddUserReceived(string tournamentId, User user)
        {
            GetTournament(tournamentId).Users.Add(user);

            if (UserConnected != null) await UserConnected.Invoke(user);
        }

        public async Task UpdateUserReceived(string tournamentId, User user)
        {
            var tournament = GetTournament(tournamentId);
            var userToReplace = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
            tournament.Users.Remove(userToReplace);
            tournament.Users.Add(user);

            if (UserInfoUpdated != null) await UserInfoUpdated.Invoke(user);
        }

        private async Task RemoveUserReceived(string tournamentId, User user)
        {
            var tournament = GetTournament(tournamentId);
            var userToRemove = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
            tournament.Users.Remove(userToRemove);

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        private async Task CreateMatchReceived(string tournamentId, Match match)
        {
            GetTournament(tournamentId).Matches.Add(match);

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatchReceived(string tournamentId, Match match)
        {
            var tournament = GetTournament(tournamentId);
            var matchToReplace = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
            if (matchToReplace == null)
            {
                return;
            }
            tournament.Matches.Remove(matchToReplace);
            tournament.Matches.Add(match);

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        private async Task DeleteMatchReceived(string tournamentId, Match match)
        {
            var tournament = GetTournament(tournamentId);
            var matchToRemove = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
            tournament.Matches.Remove(matchToRemove);

            if (MatchDeleted != null) await MatchDeleted?.Invoke(match);
        }

        private void CreateQualifierEventReceived(string tournamentId, QualifierEvent qualifierEvent)
        {
            GetTournament(tournamentId).Qualifiers.Add(qualifierEvent);
        }

        public void UpdateQualifierEventReceived(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournament(tournamentId);
            var eventToReplace = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToReplace);
            tournament.Qualifiers.Add(qualifierEvent);
        }

        private void DeleteQualifierEventReceived(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournament(tournamentId);
            var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
            tournament.Qualifiers.Remove(eventToRemove);
        }

        private async Task CreateTournamentReceived(Tournament tournament)
        {
            State.Tournaments.Add(tournament);

            if (TournamentCreated != null) await TournamentCreated.Invoke(tournament);
        }

        public async Task UpdateTournamentReceived(Tournament tournament)
        {
            var tournamentToReplace = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
            State.Tournaments.Remove(tournamentToReplace);
            State.Tournaments.Add(tournament);

            if (TournamentInfoUpdated != null) await TournamentInfoUpdated.Invoke(tournament);
        }

        private async Task DeleteTournamentReceived(Tournament tournament)
        {
            var tournamentToRemove = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
            State.Tournaments.Remove(tournamentToRemove);

            if (TournamentDeleted != null) await TournamentDeleted.Invoke(tournament);
        }


    }
}
