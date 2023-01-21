using TournamentAssistantCore.Database.Contexts;

namespace TournamentAssistantCore.Discord.Services
{
    public class DatabaseService
    {
        public TournamentDatabaseContext TournamentDatabaseContext { get; private set; }
        public QualifierDatabaseContext QualifierDatabaseContext { get; private set; }

        public DatabaseService()
        {
            TournamentDatabaseContext = new TournamentDatabaseContext($"TournamentDatabase.db");
            QualifierDatabaseContext = new QualifierDatabaseContext($"QualifierDatabase.db");

            //Ensure database is created
            TournamentDatabaseContext.Database.EnsureCreated();
            QualifierDatabaseContext.Database.EnsureCreated();
        }
    }
}
