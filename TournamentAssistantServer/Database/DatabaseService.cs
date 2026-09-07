using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Database
{
    public class DatabaseService
    {
        public TournamentDatabaseContext NewTournamentDatabaseContext()
        {
            return new TournamentDatabaseContext();
        }

        public QualifierDatabaseContext NewQualifierDatabaseContext()
        {
            return new QualifierDatabaseContext();
        }

        public UserDatabaseContext NewUserDatabaseContext()
        {
            return new UserDatabaseContext();
        }

        public WebhookDatabaseContext NewWebhookDatabaseContext()
        {
            return new WebhookDatabaseContext();
        }

        public DatabaseService()
        {
            // Ensure database is created
            using var tournamentDatabase = NewTournamentDatabaseContext();
            using var qualifierDatabase = NewQualifierDatabaseContext();
            using var userDatabase = NewUserDatabaseContext();
            using var webhookDatabase = NewWebhookDatabaseContext();

            tournamentDatabase.Database.EnsureCreated();
            qualifierDatabase.Database.EnsureCreated();
            userDatabase.Database.EnsureCreated();
            webhookDatabase.Database.EnsureCreated();
        }
    }
}
