using System.Linq;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Utilities
{
    public static class AuthoritativeAccessPolicy
    {
        public static bool HasTournamentAccess(
            AuthorizationService.TokenKind tokenKind,
            string tournamentId,
            TournamentDatabaseContext database)
        {
            return tokenKind == AuthorizationService.TokenKind.BeatKhanaAuthoritative
                && !string.IsNullOrWhiteSpace(tournamentId)
                && database.Tournaments.Any(x => !x.Old && x.Guid == tournamentId && x.IsBKTournament);
        }
    }
}
