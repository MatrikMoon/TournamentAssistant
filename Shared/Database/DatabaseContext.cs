using Microsoft.Data.Sqlite;
using SQLite.CodeFirst;
using System.Data.Entity;
using DbContext = System.Data.Entity.DbContext;

namespace TournamentAssistantShared.Database
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(string location) :
            base(new SqliteConnection()
            {
                ConnectionString = new SqliteConnectionStringBuilder() { DataSource = location }.ConnectionString
            }, true)
        { }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<DatabaseContext>(modelBuilder);
            System.Data.Entity.Database.SetInitializer(sqliteConnectionInitializer);
            base.OnModelCreating(modelBuilder);
        }
    }
}
