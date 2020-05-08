using BattleSaberCore.Shared.Discord.Database;
using BattleSaberShared.Database;
using System.Data.Entity;

namespace BattleSaberShared.Discord.Database
{
    public class QualifierDatabaseContext : DatabaseContext
    {
        public QualifierDatabaseContext(string location) : base(location) { }

        public DbSet<Player> Players { get; set; }
    }
}
