using BattleSaberShared.Discord.Database;
using System;

namespace BattleSaberShared.Discord.Services
{
    public class DatabaseService
    {
        public QualifierDatabaseContext DatabaseContext { get; private set; }

        public DatabaseService(string location, IServiceProvider serviceProvider)
        {
            DatabaseContext = new QualifierDatabaseContext(location);
        }
    }
}
