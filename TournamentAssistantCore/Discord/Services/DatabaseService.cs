using TournamentAssistantCore.Discord.Database;

namespace TournamentAssistantCore.Discord.Services
{
    public class DatabaseService
    {
        public QualifierDatabaseContext DatabaseContext { get; private set; }

        public DatabaseService(string location = "BotDatabase.db")
        {
            DatabaseContext = new QualifierDatabaseContext(location);

            //Ensure database is created
            DatabaseContext.Database.EnsureCreated();
        }
    }
}
