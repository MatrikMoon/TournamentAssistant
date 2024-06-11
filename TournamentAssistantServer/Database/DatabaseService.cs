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

        public DatabaseService()
        {
            // Ensure database is created
            using var tournamentDatabase = NewTournamentDatabaseContext();
            using var qualifierDatabase = NewQualifierDatabaseContext();

            tournamentDatabase.Database.EnsureCreated();
            qualifierDatabase.Database.EnsureCreated();
        }
    }
}
