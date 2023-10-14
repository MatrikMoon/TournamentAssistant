using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Database.Models;

namespace TournamentAssistantServer
{
    public class DatabaseTester
    {
        private static DatabaseService DatabaseService { get; set; }

        public static async Task TestDatabases()
        {
            //Set up the databases
            DatabaseService = new DatabaseService();

            //If it already exists, update it, if not add it
            var databaseModel = new Qualifier
            {
                Guid = Guid.NewGuid().ToString(),
                Name = "Test",
                Image = "",
                TournamentId = "1",
                GuildId = "1",
                GuildName = "1",
                InfoChannelId = "",
                InfoChannelName = "",
                Flags = 0
            };

            foreach (var qualifier in DatabaseService.QualifierDatabase.Qualifiers)
            {
                DatabaseService.QualifierDatabase.Remove(qualifier);
            }

            DatabaseService.QualifierDatabase.Qualifiers.Add(databaseModel);
            DatabaseService.QualifierDatabase.SaveChanges();

            /*Task.Run(TestThread1);
            Task.Run(TestThread2);*/

            var taskList = new List<Task>();

            Console.WriteLine("Writing...");

            for (int i = 0; i < 2000; i++)
            {
                taskList.Add(Task.Run(AddScoreThread));

                Console.WriteLine($"{taskList.Where(x => x.IsCompleted).Count()} / 1000 completed");
            }

            Task.WaitAll(taskList.ToArray());

            Console.WriteLine("Complete.");

            Console.ReadLine();
        }

        private static void AddScoreThread()
        {
            Console.WriteLine($"Starting thread {Thread.CurrentThread.ManagedThreadId}");

            var tournament = DatabaseService.QualifierDatabase.Qualifiers.First();
            DatabaseService.QualifierDatabase.Scores.Add(new Score
            {
                BeatmapDifficulty = 1,
                MapId = "1",
                _Score = 1,
                Characteristic = "1",
                EventId = tournament.Guid,
                FullCombo = false,
                GameOptions = 0,
                LevelId = "1",
                PlatformId = "1",
                PlayerOptions = 0,
                Username = $"{Thread.CurrentThread.ManagedThreadId}",
            });

            DatabaseService.QualifierDatabase.SaveChanges();
        }

        private static async Task TestThread1()
        {
            var tournament = DatabaseService.QualifierDatabase.Qualifiers.First();

            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Changing flags from {tournament.Flags} to {1}");

            tournament.Flags = 1;

            DatabaseService.QualifierDatabase.SaveChanges();

            await Task.Delay(2000);

            tournament = DatabaseService.QualifierDatabase.Qualifiers.First();

            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Reading flags again... {tournament.Flags}");
        }

        private static async Task TestThread2()
        {
            await Task.Delay(1000);

            var tournament = DatabaseService.QualifierDatabase.Qualifiers.First();

            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Reading flags... {tournament.Flags}");

            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Changing flags back to {0}...");

            tournament.Flags = 0;
            DatabaseService.QualifierDatabase.SaveChanges();
        }
    }
}
