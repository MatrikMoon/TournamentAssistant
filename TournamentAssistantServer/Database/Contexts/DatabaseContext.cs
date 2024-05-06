using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using TournamentAssistantShared;

namespace TournamentAssistantServer.Database.Contexts
{
    public class DatabaseContext : DbContext
    {
        private string location;

        public DatabaseContext(string location) : base()
        {
            this.location = location;

            if (Database.GetPendingMigrations().Count() > 0)
            {
                if (File.Exists(location))
                {
                    Logger.Warning($"Migrating database: {location} Backing up existing database...");
                    File.Copy(location, $"{location}.bak");
                    Logger.Success("Backup created! Migrating...");
                }
                else
                {
                    Logger.Warning($"Creating database: {location}");
                }
                Database.Migrate();
                Logger.Success("Successful!");
            }
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
