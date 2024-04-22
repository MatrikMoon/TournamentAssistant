namespace TournamentAssistantServer.Database.Contexts
{
    public class UserDatabaseContext : DatabaseContext
    {
        public UserDatabaseContext() : base("files/UserDatabase.db") { }
    }
}