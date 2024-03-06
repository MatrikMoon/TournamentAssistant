using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Generic;
using System.Threading;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Database
{
    public class DatabaseService
    {
        public TournamentDatabaseContext NewTournamentDatabaseContext()
        {
            return new TournamentDatabaseContext("files/TournamentDatabase.db");
        }

        public QualifierDatabaseContext NewQualifierDatabaseContext()
        {
            return new QualifierDatabaseContext("files/QualifierDatabase.db");
        }

        public UserDatabaseContext NewUserDatabaseContext()
        {
            return new UserDatabaseContext("files/UserDatabase.db");
        }

        public DatabaseService()
        {
            //Ensure database is created
            using var tournamentDatabase = NewTournamentDatabaseContext();
            using var qualifierDatabase = NewQualifierDatabaseContext();
            using var userDatabase = NewUserDatabaseContext();

            tournamentDatabase.Database.EnsureCreated();
            qualifierDatabase.Database.EnsureCreated();
            userDatabase.Database.EnsureCreated();
        }
    }
}
