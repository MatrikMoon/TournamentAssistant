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

        public DatabaseService()
        {
            // Ensure database is created
            using var tournamentDatabase = NewTournamentDatabaseContext();
            using var qualifierDatabase = NewQualifierDatabaseContext();
            using var userDatabase = NewUserDatabaseContext();

            tournamentDatabase.Database.EnsureCreated();
            qualifierDatabase.Database.EnsureCreated();
            userDatabase.Database.EnsureCreated();
        }
    }
}
