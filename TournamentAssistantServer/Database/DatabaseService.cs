using System.Collections.Generic;
using System.Threading;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Database
{
    internal class DatabaseService
    {
        // Due to the following issue: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues,
        // we need to give each thread its own database context. We'll do that as follows.

        private Dictionary<string, TournamentDatabaseContext> TournamentDatabaseContexts { get; set; } = new Dictionary<string, TournamentDatabaseContext>();
        private Dictionary<string, QualifierDatabaseContext> QualifierDatabaseContexts { get; set; } = new Dictionary<string, QualifierDatabaseContext>();
        private Dictionary<string, UserDatabaseContext> UserDatabaseContexts { get; set; } = new Dictionary<string, UserDatabaseContext>();

        public TournamentDatabaseContext TournamentDatabase
        {
            get
            {
                var key = $"{Thread.CurrentThread.Name}-{Thread.CurrentThread.ManagedThreadId}";
                if (!TournamentDatabaseContexts.ContainsKey(key))
                {
                    TournamentDatabaseContexts[key] = new TournamentDatabaseContext("TournamentDatabase.db");
                }

                return TournamentDatabaseContexts[key];
            }
        }

        public QualifierDatabaseContext QualifierDatabase
        {
            get
            {
                var key = $"{Thread.CurrentThread.Name}-{Thread.CurrentThread.ManagedThreadId}";
                if (!QualifierDatabaseContexts.ContainsKey(key))
                {
                    QualifierDatabaseContexts[key] = new QualifierDatabaseContext("QualifierDatabase.db");
                }

                return QualifierDatabaseContexts[key];
            }
        }

        public UserDatabaseContext UserDatabase
        {
            get
            {
                var key = $"{Thread.CurrentThread.Name}-{Thread.CurrentThread.ManagedThreadId}";
                if (!UserDatabaseContexts.ContainsKey(key))
                {
                    UserDatabaseContexts[key] = new UserDatabaseContext("UserDatabase.db");
                }

                return UserDatabaseContexts[key];
            }
        }

        public DatabaseService()
        {
            //Ensure database is created
            TournamentDatabase.Database.EnsureCreated();
            QualifierDatabase.Database.EnsureCreated();
            UserDatabase.Database.EnsureCreated();
        }
    }
}
