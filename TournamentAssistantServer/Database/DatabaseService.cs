using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Database
{
    internal class DatabaseService
    {
        public TournamentDatabaseContext TournamentDatabase { get; private set; }
        public QualifierDatabaseContext QualifierDatabase { get; private set; }
        public UserDatabaseContext UserDatabase { get; private set; }

        public DatabaseService()
        {
            TournamentDatabase = new TournamentDatabaseContext($"TournamentDatabase.db");
            QualifierDatabase = new QualifierDatabaseContext($"QualifierDatabase.db");
            UserDatabase = new UserDatabaseContext($"UserDatabase.db");

            //Ensure database is created
            TournamentDatabase.Database.EnsureCreated();
            QualifierDatabase.Database.EnsureCreated();
            UserDatabase.Database.EnsureCreated();
        }
    }
}
