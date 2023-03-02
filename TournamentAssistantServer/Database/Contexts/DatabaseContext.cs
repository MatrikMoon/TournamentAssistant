using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TournamentAssistantServer.Database.Contexts
{
    public class DatabaseContext : DbContext
    {
        private string location;

        public DatabaseContext(string location) : base()
        {
            this.location = location;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite(new SqliteConnection()
            {
                ConnectionString = new SqliteConnectionStringBuilder() { DataSource = location }.ConnectionString
            });
        }
    }
}
