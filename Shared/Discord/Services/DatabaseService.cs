using Microsoft.EntityFrameworkCore;
using System;
using TournamentAssistantShared.Discord.Database;

namespace TournamentAssistantShared.Discord.Services
{
    public class DatabaseService
    {
        public QualifierDatabaseContext DatabaseContext { get; private set; }

        public DatabaseService(string location, IServiceProvider serviceProvider)
        {
            DatabaseContext = new QualifierDatabaseContext(location);

            //Ensure database is created
            DatabaseContext.Database.EnsureCreated();
            DatabaseContext.Database.Migrate();
        }
    }
}
