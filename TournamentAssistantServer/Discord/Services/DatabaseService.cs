using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Discord.Services
{
    public class DatabaseService
    {
        public TournamentDatabaseContext TournamentDatabaseContext { get; private set; }
        public QualifierDatabaseContext QualifierDatabaseContext { get; private set; }
        public UserDatabaseContext UserDatabaseContext { get; private set; }

        public DatabaseService()
        {
            TournamentDatabaseContext = new TournamentDatabaseContext($"TournamentDatabase.db");
            QualifierDatabaseContext = new QualifierDatabaseContext($"QualifierDatabase.db");
            UserDatabaseContext = new UserDatabaseContext($"UserDatabase.db");

            //Ensure database is created
            TournamentDatabaseContext.Database.EnsureCreated();
            QualifierDatabaseContext.Database.EnsureCreated();
            UserDatabaseContext.Database.EnsureCreated();
        }
    }
}
