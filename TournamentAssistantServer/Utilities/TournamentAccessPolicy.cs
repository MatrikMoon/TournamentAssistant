using System;
using System.IO;
using System;
using System.Linq;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantShared;
using TournamentAssistantShared.Utilities;
using Tournament = TournamentAssistantShared.Models.Tournament;
using User = TournamentAssistantShared.Models.User;

namespace TournamentAssistantServer.Utilities
{
    public static class TournamentAccessPolicy
    {
        public static bool CanDiscoverTournament(Tournament tournament, User user, TournamentDatabaseContext database)
        {
            // A mock certificate proves that this is a test client, not that it owns the
            // platform/Discord account named in its token. Never use those identifiers
            // to disclose a private tournament to a mock client.
            if (user.IsMock)
            {
                return tournament.Settings.AllowUnauthorizedView || tournament.Settings.AllowMockClients;
            }

            return (user.discord_info != null && database.IsUserAuthorized(tournament.Guid, user.discord_info.UserId, Permissions.ViewTournamentInList)) ||
                   database.IsUserAuthorized(tournament.Guid, user.PlatformId, Permissions.ViewTournamentInList);
        }

        public static Tournament SanitizeTournament(Tournament tournament, User user, TournamentDatabaseContext database)
        {
            var settings = TournamentSanitization.CanJoinTournament(tournament, user, database)
                ? tournament.Settings.ProtoSerialize().ProtoDeserialize<Tournament.TournamentSettings>()
                : new Tournament.TournamentSettings
                {
                    TournamentName = tournament.Settings.TournamentName,
                    TournamentImage = tournament.Settings.TournamentImage,
                    AllowMockClients = tournament.Settings.AllowMockClients,
                };

            settings.MyPermissions.Clear();
            if (user.IsMock && tournament.Settings.AllowMockClients)
            {
                settings.MyPermissions.AddRange(Constants.DefaultRoles.GetPlayer(tournament.Guid).Permissions);
            }
            else
            {
                settings.MyPermissions.AddRange(
                    database.GetUserPermissions(tournament.Guid, user.discord_info?.UserId)
                        .Concat(database.GetUserPermissions(tournament.Guid, user.PlatformId))
                        .Distinct());
            }

            return new Tournament
            {
                Guid = tournament.Guid,
                Settings = settings,
                Server = tournament.Server,
            };
        }
    }
}
